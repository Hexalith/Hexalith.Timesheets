using System.Text.Json;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

public sealed class ScaffoldGovernanceTests
{
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
    public void Source_does_not_introduce_forbidden_authoritative_persistence_or_ui_patterns()
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
    public void No_fluent_ui_v4_component_package_is_introduced()
    {
        string[] projectFiles = Directory.GetFiles(RepositoryRoot.Find().FullName, "*.csproj", SearchOption.AllDirectories)
            .Where(static path => path.Contains("Hexalith.Timesheets", StringComparison.Ordinal))
            .ToArray();

        foreach (string projectFile in projectFiles)
        {
            File.ReadAllText(projectFile).ShouldNotContain("Microsoft.Fast.Components.FluentUI");
        }
    }

    [Fact]
    public void AppHost_initializes_security_through_eventstore_aspire_helper()
    {
        string program = File.ReadAllText(RepositoryRoot.PathTo("src", "Hexalith.Timesheets.AppHost", "Program.cs"));
        string projectPath = RepositoryRoot.PathTo("src", "Hexalith.Timesheets.AppHost", "Hexalith.Timesheets.AppHost.csproj");
        string projectText = File.ReadAllText(projectPath);
        XElement project = XDocument.Load(projectPath).Root.ShouldNotBeNull();
        XAttribute sdk = project.Attributes()
            .Where(static attribute => attribute.Name.LocalName == "Sdk")
            .ShouldHaveSingleItem("The AppHost must declare one local project SDK attribute.");
        XElement cliBundle = project.Descendants()
            .Where(static element => element.Name.LocalName == "AspireUseCliBundle")
            .ShouldHaveSingleItem("The AppHost must declare one unambiguous CLI-bundle setting.");

        program.ShouldContain("AddHexalithEventStoreSecurity(");
        program.ShouldNotContain("AddKeycloak(");
        project.Name.LocalName.ShouldBe("Project");
        sdk.Value.ShouldBe("Aspire.AppHost.Sdk/13.5.3");
        project.Elements().Where(static element => element.Name.LocalName == "Sdk").ShouldBeEmpty();
        cliBundle.Value.Trim().ShouldBe("true");
        cliBundle.AncestorsAndSelf()
            .SelectMany(static element => element.Attributes())
            .Where(static attribute => attribute.Name.LocalName == "Condition")
            .ShouldBeEmpty("The required CLI-bundle setting must be unconditional.");
        projectText.ShouldContain("Hexalith.EventStore.Aspire.csproj");
        projectText.ShouldContain("IsAspireProjectResource=\"false\"");
        File.Exists(RepositoryRoot.PathTo("src", "Hexalith.Timesheets.AppHost", "KeycloakRealms", "hexalith-realm.json")).ShouldBeTrue();
    }

    [Fact]
    public void Repository_pins_approved_dotnet_sdk()
    {
        using JsonDocument globalJson = JsonDocument.Parse(File.ReadAllText(RepositoryRoot.PathTo("global.json")));
        JsonElement sdk = globalJson.RootElement.GetProperty("sdk");

        sdk.GetProperty("version").GetString().ShouldBe("10.0.401");
        sdk.GetProperty("rollForward").GetString().ShouldBe("latestPatch");
    }

    [Fact]
    public void Nested_submodules_are_not_initialized_inside_root_level_submodules()
    {
        string gitmodules = File.ReadAllText(RepositoryRoot.PathTo(".gitmodules"));
        string[] submodulePaths = gitmodules
            .Split(Environment.NewLine)
            .Select(static line => line.Trim())
            .Where(static line => line.StartsWith("path = ", StringComparison.Ordinal))
            .Select(static line => line["path = ".Length..])
            .ToArray();

        submodulePaths.ShouldNotBeEmpty();

        foreach (string submodulePath in submodulePaths)
        {
            submodulePath.StartsWith("references/", StringComparison.Ordinal)
                .ShouldBeTrue($"Submodule path '{submodulePath}' must be under references/.");

            string absolutePath = RepositoryRoot.PathTo(submodulePath);
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
}
