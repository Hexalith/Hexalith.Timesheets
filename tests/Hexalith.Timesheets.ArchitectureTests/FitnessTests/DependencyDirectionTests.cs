using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

public sealed class DependencyDirectionTests
{
    private static readonly string[] ForbiddenContractsReferences =
    [
        "Dapr",
        "Aspire",
        "Microsoft.AspNetCore",
        "Microsoft.FluentUI",
        "OpenTelemetry",
        "StackExchange.Redis",
        "EntityFrameworkCore"
    ];

    private static readonly string[] ForbiddenKernelReferences =
    [
        "Dapr",
        "Aspire",
        "Microsoft.AspNetCore",
        "Microsoft.FluentUI",
        "OpenTelemetry",
        "StackExchange.Redis",
        "EntityFrameworkCore",
        "ModelContextProtocol",
        "OpenAI",
        "SemanticKernel"
    ];

    [Fact]
    public void Contracts_project_has_no_infrastructure_runtime_or_ui_dependencies()
    {
        string projectPath = RepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets.Contracts",
            "Hexalith.Timesheets.Contracts.csproj");

        XDocument project = XDocument.Load(projectPath);
        string[] references = ReadIncludeValues(project).ToArray();

        foreach (string forbidden in ForbiddenContractsReferences)
        {
            references.ShouldNotContain(reference => reference.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Public_command_and_query_contracts_do_not_expose_server_authority_or_envelope_fields()
    {
        string contractsRoot = RepositoryRoot.PathTo("src", "Hexalith.Timesheets.Contracts");
        string[] contractFiles =
        [
            .. Directory.GetFiles(Path.Combine(contractsRoot, "Commands"), "*.cs", SearchOption.AllDirectories),
            .. Directory.GetFiles(Path.Combine(contractsRoot, "Queries"), "*.cs", SearchOption.AllDirectories)
        ];

        contractFiles.ShouldNotBeEmpty();

        string[] forbiddenTerms =
        [
            "TenantId",
            "UserId",
            "CorrelationId",
            "MessageId",
            "CausationId",
            "ClaimsPrincipal",
            "Authorization",
            "Jwt",
            "Token",
            "Stream",
            "Sequence"
        ];

        foreach (string contractFile in contractFiles)
        {
            string source = RemoveAllowedDomainTokenMetricTerms(File.ReadAllText(contractFile));

            foreach (string forbiddenTerm in forbiddenTerms)
            {
                source.Contains(forbiddenTerm, StringComparison.Ordinal).ShouldBeFalse(contractFile);
            }
        }
    }

    // "Token" is forbidden in contracts to block auth/bearer tokens and other credentials from
    // leaking into public command/query bodies. The AI-effort domain legitimately uses provider
    // token-metric vocabulary, which is not a credential. Only these exact identifiers are exempt;
    // any other "...Token..." member (e.g. AuthToken, AccessToken) still trips the scan because the
    // surrounding identifier survives the removal. Longest identifier is stripped first so the
    // shorter one cannot partially consume it.
    private static readonly string[] AllowedDomainTokenMetricIdentifiers =
    [
        "AiTokenMetricAvailability",
        "AiTokenAvailability"
    ];

    private static string RemoveAllowedDomainTokenMetricTerms(string source)
    {
        foreach (string allowed in AllowedDomainTokenMetricIdentifiers)
        {
            source = source.Replace(allowed, string.Empty, StringComparison.Ordinal);
        }

        return source;
    }

    [Fact]
    public void Kernel_projects_do_not_reference_runtime_hosting_ui_or_direct_persistence_packages()
    {
        string[] kernelProjects =
        [
            RepositoryRoot.PathTo("src", "Hexalith.Timesheets.Server", "Hexalith.Timesheets.Server.csproj"),
            RepositoryRoot.PathTo("src", "Hexalith.Timesheets.Projections", "Hexalith.Timesheets.Projections.csproj")
        ];

        foreach (string projectPath in kernelProjects)
        {
            XDocument project = XDocument.Load(projectPath);
            string[] references = ReadIncludeValues(project).ToArray();

            foreach (string forbidden in ForbiddenKernelReferences)
            {
                references.ShouldNotContain(reference => reference.Contains(forbidden, StringComparison.OrdinalIgnoreCase), projectPath);
            }
        }
    }

    [Fact]
    public void Sibling_modules_are_not_consumed_as_hexalith_nuget_packages()
    {
        string[] projectFiles = Directory.GetFiles(RepositoryRoot.Find().FullName, "*.csproj", SearchOption.AllDirectories)
            .Where(static path => path.Contains("Hexalith.Timesheets", StringComparison.Ordinal))
            .ToArray();

        projectFiles.ShouldNotBeEmpty();

        foreach (string projectFile in projectFiles)
        {
            XDocument project = XDocument.Load(projectFile);
            string[] packageReferences = project
                .Descendants("PackageReference")
                .Select(static reference => reference.Attribute("Include")?.Value)
                .Where(static value => value is not null)
                .Cast<string>()
                .ToArray();

            packageReferences.ShouldNotContain(package => package.StartsWith("Hexalith.", StringComparison.Ordinal), projectFile);
        }
    }

    [Fact]
    public void Directory_build_props_detects_required_root_level_sibling_modules()
    {
        string props = File.ReadAllText(RepositoryRoot.PathTo("Directory.Build.props"));

        foreach (string propertyName in new[]
        {
            "HexalithEventStoreRoot",
            "HexalithTenantsRoot",
            "HexalithPartiesRoot",
            "HexalithProjectsRoot",
            "HexalithWorksRoot",
            "HexalithFrontComposerRoot",
            "HexalithCommonsRoot"
        })
        {
            props.ShouldContain(propertyName);
        }
    }

    private static IEnumerable<string> ReadIncludeValues(XDocument project)
    {
        return project
            .Descendants()
            .Where(static element => element.Name.LocalName is "PackageReference" or "ProjectReference" or "FrameworkReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static value => value is not null)
            .Cast<string>();
    }
}
