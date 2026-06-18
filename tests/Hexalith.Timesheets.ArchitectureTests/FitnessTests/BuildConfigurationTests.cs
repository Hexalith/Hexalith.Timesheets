using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

public sealed class BuildConfigurationTests
{
    private static readonly string[] RequiredProjects =
    [
        "src/Hexalith.Timesheets/Hexalith.Timesheets.csproj",
        "src/Hexalith.Timesheets.Contracts/Hexalith.Timesheets.Contracts.csproj",
        "src/Hexalith.Timesheets.Client/Hexalith.Timesheets.Client.csproj",
        "src/Hexalith.Timesheets.Server/Hexalith.Timesheets.Server.csproj",
        "src/Hexalith.Timesheets.Projections/Hexalith.Timesheets.Projections.csproj",
        "src/Hexalith.Timesheets.Testing/Hexalith.Timesheets.Testing.csproj",
        "src/Hexalith.Timesheets.ServiceDefaults/Hexalith.Timesheets.ServiceDefaults.csproj",
        "src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj",
        "tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj",
        "tests/Hexalith.Timesheets.Contracts.Tests/Hexalith.Timesheets.Contracts.Tests.csproj",
        "tests/Hexalith.Timesheets.Server.Tests/Hexalith.Timesheets.Server.Tests.csproj",
        "tests/Hexalith.Timesheets.Projections.Tests/Hexalith.Timesheets.Projections.Tests.csproj",
        "tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj"
    ];

    [Fact]
    public void Repository_uses_slnx_and_central_package_management()
    {
        DirectoryInfo root = RepositoryRoot.Find();

        File.Exists(Path.Combine(root.FullName, "Hexalith.Timesheets.slnx")).ShouldBeTrue();
        File.Exists(Path.Combine(root.FullName, "Hexalith.Timesheets.sln")).ShouldBeFalse();

        XDocument packages = XDocument.Load(Path.Combine(root.FullName, "Directory.Packages.props"));
        packages.Descendants("ManagePackageVersionsCentrally").Single().Value.ShouldBe("true");
    }

    [Fact]
    public void Required_projects_exist_and_are_registered_in_solution()
    {
        string solution = File.ReadAllText(RepositoryRoot.PathTo("Hexalith.Timesheets.slnx"));

        foreach (string project in RequiredProjects)
        {
            File.Exists(RepositoryRoot.PathTo(project.Split('/'))).ShouldBeTrue(project);
            solution.ShouldContain(project);
        }
    }

    [Fact]
    public void Project_files_do_not_use_inline_package_versions()
    {
        string[] projectFiles = Directory.GetFiles(RepositoryRoot.Find().FullName, "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}Hexalith.", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}Hexalith.Timesheets", StringComparison.Ordinal))
            .ToArray();

        projectFiles.ShouldNotBeEmpty();

        foreach (string projectFile in projectFiles)
        {
            XDocument project = XDocument.Load(projectFile);
            IEnumerable<XElement> packageReferences = project.Descendants("PackageReference");
            foreach (XElement packageReference in packageReferences)
            {
                packageReference.Attribute("Version").ShouldBeNull(projectFile);
            }
        }
    }
}
