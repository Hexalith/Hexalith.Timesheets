using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

public sealed class ScaffoldGovernanceTests
{
    private const string ApprovedAspireAppHostSdkVersion = "13.6.3";

    private static readonly Regex AspireDiagnosticCode = new(
        @"\bASPIRE[A-Z]*\d{3}\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] ForbiddenSourcePatterns =
    [
        "SqlConnection",
        "DbContext",
        "StackExchange.Redis",
        "SaveStateAsync",
        "GetStateAsync",
        "PublishEventAsync",
        "CreateProducer",
        "IProducer",
        "FluentUI.AspNetCore.Components.v4"
    ];

    [Fact]
    public void SourceDoesNotIntroduceForbiddenAuthoritativePersistenceOrUiPatterns()
    {
        string[] sourceFiles = Directory.GetFiles(RepositoryRoot.PathTo("src"), "*.cs", SearchOption.AllDirectories);
        sourceFiles.ShouldNotBeEmpty();

        foreach (string sourceFile in sourceFiles)
        {
            string source = File.ReadAllText(sourceFile);

            foreach (string forbidden in ForbiddenSourcePatterns)
            {
                source.ShouldNotContain(forbidden, Case.Insensitive, sourceFile);
            }
        }
    }

    [Fact]
    public void NoFluentUiV4ComponentPackageIsIntroduced()
    {
        foreach (string projectFile in RepositoryRoot.TimesheetsProjectFiles())
        {
            File.ReadAllText(projectFile).ShouldNotContain("Microsoft.Fast.Components.FluentUI");
        }
    }

    [Fact]
    public void AppHostInitializesSecurityThroughEventstoreAspireHelper()
    {
        string program = File.ReadAllText(RepositoryRoot.PathTo("src", "Hexalith.Timesheets.AppHost", "Program.cs"));
        XElement project = LoadAppHostProject();
        XElement eventStoreAspire = project.Descendants()
            .Where(static element => element.Name.LocalName == "ProjectReference")
            .Where(static element => (element.Attribute("Include")?.Value ?? string.Empty)
                .EndsWith("/src/Hexalith.EventStore.Aspire/Hexalith.EventStore.Aspire.csproj", StringComparison.Ordinal))
            .ShouldHaveSingleItem("The AppHost must reference the EventStore Aspire helper once in source mode.");

        program.ShouldContain("AddHexalithEventStoreSecurity(");
        program.ShouldNotContain("AddKeycloak(");
        eventStoreAspire.Attribute("IsAspireProjectResource").ShouldNotBeNull().Value.ShouldBe("false");
        File.Exists(RepositoryRoot.PathTo("src", "Hexalith.Timesheets.AppHost", "KeycloakRealms", "hexalith-realm.json")).ShouldBeTrue();
    }

    [Fact]
    public void AppHostPinsApprovedAspireApphostSdk()
    {
        // Approved pins are literal here, like the global.json SDK pin below; documentation guards derive
        // the same value from this project instead of restating it.
        XElement project = LoadAppHostProject();
        XAttribute sdk = project.Attributes()
            .Where(static attribute => attribute.Name.LocalName == "Sdk")
            .ShouldHaveSingleItem("The AppHost must declare one local project SDK attribute.");

        project.Name.LocalName.ShouldBe("Project");
        sdk.Value.ShouldBe($"Aspire.AppHost.Sdk/{ApprovedAspireAppHostSdkVersion}");
        project.Elements().Where(static element => element.Name.LocalName == "Sdk").ShouldBeEmpty();
    }

    [Fact]
    public void AppHostRetainsNugetOrchestrationAndSuppressesOnlyAspire010()
    {
        // Decided 2026-09-13 (Option 2): keep the SDK-added Aspire.Hosting.Orchestration and
        // Aspire.Dashboard.Sdk packages so restore/build stay self-contained, and suppress only the
        // ASPIRE010 notice that the CLI bundle is disabled.
        XElement project = LoadAppHostProject();
        XElement cliBundle = project.Descendants()
            .Where(static element => element.Name.LocalName == "AspireUseCliBundle")
            .ShouldHaveSingleItem("The AppHost must declare one unambiguous CLI-bundle setting.");
        XElement noWarn = project.Descendants()
            .Where(static element => element.Name.LocalName == "NoWarn")
            .ShouldHaveSingleItem("The AppHost must declare one unambiguous diagnostic suppression.");

        cliBundle.Value.Trim().ShouldBe("false");
        ConditionsOf(cliBundle).ShouldBeEmpty("The NuGet-orchestration setting must be unconditional.");
        noWarn.Value.Trim().ShouldBe("$(NoWarn);ASPIRE010");
        ConditionsOf(noWarn).ShouldBeEmpty("The ASPIRE010 suppression must be unconditional.");
        AspireDiagnosticCode.Matches(File.ReadAllText(AppHostProjectPath()))
            .Select(static match => match.Value)
            .ShouldHaveSingleItem("The AppHost project may name only the ASPIRE010 suppression.")
            .ShouldBe("ASPIRE010");

        // Other Timesheets-owned surfaces may not suppress or downgrade any Aspire diagnostic.
        foreach (string buildFile in new[] { "Directory.Build.props", "Directory.Build.targets" }
            .Select(static name => RepositoryRoot.PathTo(name))
            .Where(File.Exists))
        {
            AspireDiagnosticCode.IsMatch(File.ReadAllText(buildFile))
                .ShouldBeFalse($"{buildFile} must not suppress or downgrade an Aspire diagnostic.");
        }

        foreach (string editorConfig in new[] { ".editorconfig", ".globalconfig" }
            .SelectMany(static name => new[] { RepositoryRoot.PathTo(name), RepositoryRoot.PathTo("src", name), RepositoryRoot.PathTo("src", "Hexalith.Timesheets.AppHost", name) })
            .Where(File.Exists))
        {
            Regex.IsMatch(File.ReadAllText(editorConfig), @"dotnet_diagnostic\.ASPIRE", RegexOptions.IgnoreCase)
                .ShouldBeFalse($"{editorConfig} must not configure an Aspire diagnostic severity.");
        }

        string appHostDirectory = RepositoryRoot.PathTo("src", "Hexalith.Timesheets.AppHost");
        foreach (string sourceFile in Directory.GetFiles(appHostDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(appHostDirectory, path).Split(Path.DirectorySeparatorChar)
                .Any(static segment => segment is "bin" or "obj")))
        {
            string source = File.ReadAllText(sourceFile);
            // Match diagnostic IDs only, so comments and justifications that mention Aspire do not count.
            Regex.IsMatch(source, @"#pragma\s+warning\s+disable[^/\r\n]*\bASPIRE[A-Z]*\d{3}\b", RegexOptions.IgnoreCase)
                .ShouldBeFalse($"{sourceFile} must not disable an Aspire diagnostic.");
            Regex.IsMatch(source, @"SuppressMessage(Attribute)?\s*\([^)]*\bASPIRE[A-Z]*\d{3}\b", RegexOptions.IgnoreCase)
                .ShouldBeFalse($"{sourceFile} must not suppress an Aspire diagnostic.");
        }
    }

    [Fact]
    public void RepositoryPinsApprovedDotnetSdk()
    {
        using JsonDocument globalJson = JsonDocument.Parse(File.ReadAllText(RepositoryRoot.PathTo("global.json")));
        JsonElement sdk = globalJson.RootElement.GetProperty("sdk");

        sdk.GetProperty("version").GetString().ShouldBe("10.0.402");
        sdk.GetProperty("rollForward").GetString().ShouldBe("latestPatch");
    }

    /// <summary>
    /// Keeps the CI/CD guide's SDK statement aligned with the <c>global.json</c> pin.
    /// </summary>
    [Fact]
    public void CiCdGuideNamesThePinnedDotnetSdk()
    {
        using JsonDocument globalJson = JsonDocument.Parse(File.ReadAllText(RepositoryRoot.PathTo("global.json")));
        string sdkVersion = globalJson.RootElement.GetProperty("sdk").GetProperty("version").GetString().ShouldNotBeNull();

        File.ReadAllText(RepositoryRoot.PathTo("docs", "ci-cd.md")).ShouldContain($"The repository uses SDK `{sdkVersion}`");
    }

    /// <summary>
    /// Protects the no-resource boundary used by unsafe fixture configuration probes.
    /// </summary>
    [Fact]
    public void AppHostValidationOnlyReturnsBeforeSecurityResourcesAndExecution()
    {
        string program = File.ReadAllText(RepositoryRoot.PathTo("src", "Hexalith.Timesheets.AppHost", "Program.cs"));
        int probeStart = program.IndexOf("if (builder.Configuration[\"TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY\"] == \"1\")", StringComparison.Ordinal);
        probeStart.ShouldBeGreaterThanOrEqualTo(0);
        int probeEnd = program.IndexOf("\n}", probeStart, StringComparison.Ordinal);
        probeEnd.ShouldBeGreaterThan(probeStart);
        program[probeStart..probeEnd].ShouldContain("return;");

        // Even a regressed fixture-admission check must reach this exit before
        // any Add* helper (including security), Build, Run, or RunAsync call.
        MatchCollection setupCalls = Regex.Matches(program, @"\.\s*(?:Add\w*|Build|Run(?:Async)?)\s*(?:<|\()");
        setupCalls.ShouldNotBeEmpty();
        setupCalls.ShouldAllBe(match => match.Index > probeEnd);
    }

    [Fact]
    public async Task NestedSubmodulesAreNotInitializedInsideRootLevelSubmodules()
    {
        string[] submodulePaths = await ReadSubmodulePathsAsync(RepositoryRoot.PathTo(".gitmodules"));

        submodulePaths.ShouldNotBeEmpty();

        foreach (string submodulePath in submodulePaths)
        {
            IsUnderReferences(submodulePath)
                .ShouldBeTrue($"Submodule path '{submodulePath}' must be under references/.");

            string absolutePath = Path.GetFullPath(submodulePath.Replace('\\', '/'), RepositoryRoot.Find().FullName);
            if (!Directory.Exists(absolutePath))
            {
                continue;
            }

            string[] nestedGitEntries = Directory.GetFileSystemEntries(absolutePath, ".git", SearchOption.AllDirectories)
                .Where(entry => !string.Equals(entry, Path.Combine(absolutePath, ".git"), StringComparison.Ordinal))
                .ToArray();

            nestedGitEntries.ShouldBeEmpty($"Nested submodule initialized under {submodulePath}.");
        }
    }

    /// <summary>
    /// Uses Git's parser so whitespace, comments and quotes cannot hide an escaping path.
    /// </summary>
    [Theory]
    [InlineData("path=references/../Hexalith.Commons")]
    [InlineData("path\t=\treferences/../Hexalith.Commons")]
    [InlineData("path = \"references/..\"/Hexalith.Commons")]
    [InlineData("path = references/../Hexalith.Commons # comment")]
    public async Task SubmoduleConfigGuardReadsEveryPathAssignment(string assignment)
    {
        string temporaryFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(temporaryFile, $"[submodule \"escape\"]\n{assignment}\n", TestContext.Current.CancellationToken);
            string[] paths = await ReadSubmodulePathsAsync(temporaryFile);
            paths.ShouldHaveSingleItem().ShouldBe("references/../Hexalith.Commons");
            IsUnderReferences(paths[0]).ShouldBeFalse();
        }
        finally
        {
            File.Delete(temporaryFile);
        }
    }

    private static async Task<string[]> ReadSubmodulePathsAsync(string file)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = RepositoryRoot.Find().FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList = { "config", "--file", file, "--null", "--get-regexp", @"^submodule\..*\.path$" },
        };
        process.Start().ShouldBeTrue();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync();
            throw new TimeoutException("Git did not parse submodule paths within 30 seconds.");
        }

        process.ExitCode.ShouldBe(0, await error);
        return (await output).Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(static record => record.Split('\n', 2)[1]).ToArray();
    }

    /// <summary>
    /// Keeps normalized submodule paths within references rather than trusting a textual prefix.
    /// </summary>
    [Theory]
    [InlineData("references/Hexalith.Works", true)]
    [InlineData("references\\Hexalith.Commons", true)]
    [InlineData("./references/Hexalith.EventStore", true)]
    [InlineData("references/temporary/../Hexalith.Works", true)]
    [InlineData("references/../Hexalith.Works", false)]
    [InlineData("references\\..\\Hexalith.Commons", false)]
    [InlineData("references/a/../../Hexalith.Works", false)]
    [InlineData("references/../../outside", false)]
    [InlineData("references", false)]
    [InlineData("references/", false)]
    [InlineData("references/.", false)]
    [InlineData("references-other/Hexalith.Works", false)]
    [InlineData("Hexalith.Works", false)]
    [InlineData("../references/Hexalith.Works", false)]
    [InlineData("/references/Hexalith.Works", false)]
    public void SubmodulePathGuardRequiresAReferencesDescendant(string path, bool allowed)
    {
        IsUnderReferences(path).ShouldBe(allowed);
    }

    private static bool IsUnderReferences(string path)
    {
        string normalized = path.Replace('\\', '/');
        if (Path.IsPathRooted(normalized))
        {
            return false;
        }

        string absolutePath = Path.GetFullPath(normalized, RepositoryRoot.Find().FullName);
        string references = RepositoryRoot.PathTo("references") + Path.DirectorySeparatorChar;
        return Path.TrimEndingDirectorySeparator(absolutePath).StartsWith(references,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string AppHostProjectPath()
        => RepositoryRoot.PathTo("src", "Hexalith.Timesheets.AppHost", "Hexalith.Timesheets.AppHost.csproj");

    private static XElement LoadAppHostProject()
        => XDocument.Load(AppHostProjectPath()).Root.ShouldNotBeNull();

    private static string[] ConditionsOf(XElement element)
        => element.AncestorsAndSelf()
            .SelectMany(static node => node.Attributes())
            .Where(static attribute => attribute.Name.LocalName == "Condition")
            .Select(static attribute => attribute.Value)
            .ToArray();
}
