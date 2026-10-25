using System.Text.RegularExpressions;
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
    public void ContractsProjectHasNoInfrastructureRuntimeOrUiDependencies()
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
    public void PublicCommandAndQueryContractsDoNotExposeServerAuthorityOrEnvelopeFields()
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
    public void KernelProjectsDoNotReferenceRuntimeHostingUiOrDirectPersistencePackages()
    {
        string[] kernelProjects =
        [
            RepositoryRoot.PathTo("src", "Hexalith.Timesheets.Server", "Hexalith.Timesheets.Server.csproj"),
            RepositoryRoot.PathTo("src", "Hexalith.Timesheets.ReadModels", "Hexalith.Timesheets.ReadModels.csproj"),
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

    /// <summary>Checks every owned edge, including conditional edges from either dependency mode.</summary>
    [Fact]
    public void ProjectionsAndReadModelsOwnedClosureCannotReachServer()
    {
        string sourceRoot = RepositoryRoot.PathTo("src");
        var pending = new Stack<string>(new[] { "Hexalith.Timesheets.Projections", "Hexalith.Timesheets.ReadModels" });
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (pending.TryPop(out string? name))
        {
            name.ShouldNotBe("Hexalith.Timesheets.Server");
            if (!visited.Add(name)) continue;
            XDocument project = XDocument.Load(Path.Combine(sourceRoot, name, name + ".csproj"));
            foreach (XElement reference in project.Descendants("ProjectReference"))
            {
                string include = reference.Attribute("Include")!.Value.Replace('\\', '/');
                // SDK DomainService composition is intentional. Only Timesheets-owned edges
                // define this module boundary; platform runtime dependencies are not module edges.
                if (!include.StartsWith("../Hexalith.Timesheets.", StringComparison.Ordinal)) continue;
                pending.Push(Path.GetFileNameWithoutExtension(include));
            }
        }
        visited.ShouldContain("Hexalith.Timesheets.Contracts");
        AssertNoReferences(Path.Combine(sourceRoot, "Hexalith.Timesheets.ReadModels",
            "Hexalith.Timesheets.ReadModels.csproj"), "Hexalith.EventStore.DomainService", "Hexalith.Timesheets.Server");
    }

    [Fact]
    public void ContractsTakeNoWorksOrEventstoreReferenceAndServerTakesNoWorksReference()
    {
        // Contracts stay infrastructure-free. Server may reference the EventStore SDK for the owned
        // Story 3.6 magic-link state loader, but Works remains isolated behind Hexalith.Timesheets.Works.
        AssertNoReferences(
            RepositoryRoot.PathTo("src", "Hexalith.Timesheets.Contracts", "Hexalith.Timesheets.Contracts.csproj"),
            "Hexalith.Works",
            "Hexalith.EventStore");
        AssertNoReferences(
            RepositoryRoot.PathTo("src", "Hexalith.Timesheets.Server", "Hexalith.Timesheets.Server.csproj"),
            "Hexalith.Works");
    }

    /// <summary>Preserves sibling boundaries while allowing the conditional published-package graph.</summary>
    [Fact]
    public void SiblingModulesUseConditionalSourceAndPublishedPackageReferences()
    {
        string repositoryRoot = RepositoryRoot.Find().FullName;
        string[] projectFiles =
        [
            .. Directory.GetFiles(Path.Combine(repositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories),
            .. Directory.GetFiles(Path.Combine(repositoryRoot, "tests"), "*.csproj", SearchOption.AllDirectories),
        ];

        projectFiles.ShouldNotBeEmpty();

        foreach (string projectFile in projectFiles)
        {
            XDocument project = XDocument.Load(projectFile);
            XElement[] packageReferences = project
                .Descendants("PackageReference")
                .Where(static reference => reference.Attribute("Include")?.Value.StartsWith("Hexalith.", StringComparison.Ordinal) == true)
                .ToArray();

            foreach (XElement reference in packageReferences)
            {
                string package = reference.Attribute("Include")!.Value;
                (package.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal)
                    || package == "Hexalith.Works.Contracts").ShouldBeTrue(projectFile);
                string.Join(";", reference.AncestorsAndSelf().Attributes("Condition").Select(static value => value.Value))
                    .Contains("'$(UseHexalithProjectReferences)' != 'true'", StringComparison.Ordinal).ShouldBeTrue(projectFile);
            }

            foreach (XElement reference in project.Descendants("ProjectReference")
                .Where(static reference => reference.Attribute("Include")?.Value.StartsWith("$(Hexalith", StringComparison.Ordinal) == true))
            {
                reference.Attribute("Condition")?.Value.ShouldBe("'$(UseHexalithProjectReferences)' == 'true'", projectFile);
            }
        }
    }

    [Fact]
    public void DirectoryBuildPropsDetectsRequiredRootLevelSiblingModules()
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

        props.ShouldContain(@"references\Hexalith.");
        FindRepositoryRootModuleProbes(XDocument.Parse(props)).ShouldBeEmpty(
            "Dependency probes must use references/ or the supported sibling fallback.");
    }

    /// <summary>
    /// Rejects root probes across MSBuild quoting, separator and relative-path spellings.
    /// </summary>
    [Theory]
    [InlineData("Exists('$(MSBuildThisFileDirectory)Hexalith.Commons/src')", true)]
    [InlineData("Exists('$(MSBuildThisFileDirectory)/Hexalith.EventStore/src')", true)]
    [InlineData("Exists('$(MSBuildThisFileDirectory)\\Hexalith.Tenants\\src')", true)]
    [InlineData("Exists(\"$(MSBuildThisFileDirectory)Hexalith.Parties/src\")", true)]
    [InlineData("exists ( '$( MSBuildThisFileDirectory )/Hexalith.Projects/src' )", true)]
    [InlineData("Exists('$(MSBuildThisFileDirectory)./Hexalith.Works/src')", true)]
    [InlineData("Exists('$(MSBuildThisFileDirectory)references/../Hexalith.FrontComposer/src')", true)]
    [InlineData("Exists('$(MSBuildThisFileDirectory)references\\..\\Hexalith.Commons\\src')", true)]
    [InlineData("Exists('$(MSBuildThisFileDirectory)Hexalith%2eCommons/src')", true)]
    [InlineData("Exists('$(MSBuildThisFileDirectory)%2e/Hexalith.Commons/src')", true)]
    [InlineData("Exists('$(MSBuildThisFileDirectory)references%2f..%2fHexalith.Commons/src')", true)]
    [InlineData("Exists('$(MSBuildThisFileDirectory)Hexalith.Commons/src\"s')", true)]
    [InlineData("Exists('$(MSBuildThisFileDirectory)references/Hexalith.Works/src')", false)]
    [InlineData("Exists('$(MSBuildThisFileDirectory)references\\Hexalith.Works\\src')", false)]
    [InlineData("Exists('$(MSBuildThisFileDirectory)../Hexalith.Works/src')", false)]
    [InlineData("Exists('$(MSBuildThisFileDirectory)..\\Hexalith.Works\\src')", false)]
    [InlineData("Exists('$(MSBuildThisFileDirectory)Hexalith%252eCommons/src')", false)]
    [InlineData("'Exists(\"$(MSBuildThisFileDirectory)Hexalith.Commons/src\")' != ''", false)]
    [InlineData("\"Exists('$(MSBuildThisFileDirectory)Hexalith.Commons/src')\" != ''", false)]
    public void RootModuleProbeGuardRecognizesAlternateSpellings(string condition, bool forbidden)
    {
        XDocument props = new(new XElement("Project",
            new XElement("PropertyGroup", new XElement("DependencyRoot", new XAttribute("Condition", condition)))));

        FindRepositoryRootModuleProbes(props).Any().ShouldBe(forbidden);
    }

    private static IEnumerable<string> FindRepositoryRootModuleProbes(XDocument props)
    {
        string root = RepositoryRoot.Find().FullName;
        foreach (XAttribute condition in props.Descendants().Attributes("Condition"))
        {
            // Skip quoted condition literals, while consuming actual Exists arguments as a unit.
            // XML parsing has already decoded &quot; before this bounded literal-path scan.
            foreach (Match match in Regex.Matches(condition.Value,
                @"(?<literal>['""]).*?\k<literal>|\bExists\s*\(\s*(?<quote>['""])\s*\$\(\s*MSBuildThisFileDirectory\s*\)(?<relative>.*?)\k<quote>\s*\)",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                if (!match.Groups["relative"].Success)
                {
                    continue;
                }

                // MSBuild unescapes literal %xx sequences once, rather than recursively.
                string unescaped = Regex.Replace(match.Groups["relative"].Value, @"%([0-9a-fA-F]{2})",
                    static escape => ((char)Convert.ToInt32(escape.Groups[1].Value, 16)).ToString());
                string suffix = unescaped.Replace('\\', '/').TrimStart('/');
                string relative = Path.GetRelativePath(root, Path.GetFullPath(suffix, root));
                if (relative.Split(Path.DirectorySeparatorChar)[0].StartsWith("Hexalith.", StringComparison.OrdinalIgnoreCase))
                {
                    yield return match.Value;
                }
            }
        }
    }

    [Fact]
    public void TimesheetsHostDiscoversProjectionHandlersAndMapsCanonicalSdkRoutes()
    {
        string hostProject = File.ReadAllText(RepositoryRoot.PathTo(
            "src", "Hexalith.Timesheets", "Hexalith.Timesheets.csproj"));
        string projectionsProject = File.ReadAllText(RepositoryRoot.PathTo(
            "src", "Hexalith.Timesheets.Projections", "Hexalith.Timesheets.Projections.csproj"));
        string program = File.ReadAllText(RepositoryRoot.PathTo("src", "Hexalith.Timesheets", "Program.cs"));
        string indexHandler = File.ReadAllText(RepositoryRoot.PathTo(
            "src", "Hexalith.Timesheets.Projections", "MagicLinks", "MagicLinkTokenHashCapabilityIndexProjectionHandler.cs"));
        string catalogHandler = File.ReadAllText(RepositoryRoot.PathTo(
            "src", "Hexalith.Timesheets.Projections", "ActivityTypes", "TenantActivityTypeCatalogProjectionHandler.cs"));

        hostProject.ShouldContain("Hexalith.Timesheets.Projections");
        projectionsProject.ShouldContain("Hexalith.Timesheets.ReadModels");
        projectionsProject.ShouldNotContain("Hexalith.Timesheets.Server");
        projectionsProject.ShouldContain("Hexalith.EventStore.DomainService");
        program.ShouldContain("AddEventStoreDomainService");
        program.ShouldContain("TimesheetsProjectionsMarker");
        program.ShouldContain("UseEventStoreDomainService");
        indexHandler.ShouldContain("IAsyncDomainSharedProjectionStreamedOwnerHandler");
        catalogHandler.ShouldContain("IAsyncDomainSharedProjectionRebuildHandler");
        indexHandler.ShouldContain("ReadModelWritePolicy.UpdateAsync");
        catalogHandler.ShouldContain("ReadModelWritePolicy.UpdateAsync");
    }

    [Fact]
    public void TimesheetsHostDelegatesSharedDefaultsToTheEventstoreSdkOnce()
    {
        string hostProject = File.ReadAllText(RepositoryRoot.PathTo(
            "src", "Hexalith.Timesheets", "Hexalith.Timesheets.csproj"));
        string program = File.ReadAllText(RepositoryRoot.PathTo("src", "Hexalith.Timesheets", "Program.cs"));

        program.Split("AddEventStoreDomainService(", StringSplitOptions.None).Length.ShouldBe(2);
        program.ShouldNotContain("AddTimesheetsServiceDefaults");
        // Deliberately not asserted: that the host still carries the ServiceDefaults project
        // reference. It is orphaned by this host composition and decision 5 owns its disposition,
        // so cementing the orphan here would make that follow-up start by deleting this assertion.
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

    private static void AssertNoReferences(string projectPath, params string[] forbiddenReferences)
    {
        XDocument project = XDocument.Load(projectPath);
        string[] references = ReadIncludeValues(project).ToArray();

        foreach (string forbidden in forbiddenReferences)
        {
            references.ShouldNotContain(
                reference => reference.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                projectPath);
        }
    }
}
