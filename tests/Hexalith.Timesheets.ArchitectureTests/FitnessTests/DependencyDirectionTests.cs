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
