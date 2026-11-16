using System.Text.RegularExpressions;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

public sealed class AppHostDependencyTests
{
    /// <summary>
    /// Verifies the documented invariant that the Timesheets-owned AppHost SDK follows the imported catalog's
    /// canonical AppHost SDK contract and, per the platform's consumer rule, its <c>Aspire.Hosting</c> version, so
    /// the SDK-added AppHost, orchestration, and dashboard packages cannot mix with a second Aspire hosting line.
    /// </summary>
    [Fact]
    public void AppHostSdkTracksTheImportedStableAspireCatalogLine()
    {
        string appHostVersion = ReadAppHostSdkVersion();
        XDocument centralCatalog = LoadImportedPackageCatalog();
        string catalogAppHostSdkVersion = GetCatalogProperty(centralCatalog, "HexalithAspireAppHostSdkVersion");

        appHostVersion.ShouldBe(
            catalogAppHostSdkVersion,
            "Align Aspire.AppHost.Sdk with the imported catalog's HexalithAspireAppHostSdkVersion in the same change.");
        appHostVersion.ShouldBe(
            GetPackageVersion(centralCatalog, "Aspire.Hosting"),
            "Aspire.AppHost.Sdk must equal the imported Aspire.Hosting version, or the restored AppHost graph mixes two Aspire lines.");
    }

    /// <summary>
    /// Verifies that catalog defaults are resolved without selecting conditional overrides.
    /// </summary>
    [Fact]
    public void PackageCatalogReaderUsesUnconditionalDefaultsWithConditionalOverrides()
    {
        XDocument catalog = XDocument.Parse(
            """
            <Project>
              <PropertyGroup Condition="'$(MSBuildProjectName)' == 'Other'">
                <SharedVersion>9.0.0</SharedVersion>
              </PropertyGroup>
              <PropertyGroup>
                <SharedVersion>1.2.3</SharedVersion>
                <SharedVersion Condition="'$(MSBuildProjectName)' == 'Other'">8.0.0</SharedVersion>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Shared" Version="$(SharedVersion)" />
                <PackageVersion Include="Literal" Version="4.5.6" />
              </ItemGroup>
            </Project>
            """);

        GetPackageVersion(catalog, "Shared").ShouldBe("1.2.3");
        GetPackageVersion(catalog, "Literal").ShouldBe("4.5.6");
    }

    private static string GetPackageVersion(XDocument centralCatalog, string packageId)
    {
        string version = centralCatalog.Descendants("PackageVersion")
            .Single(element => string.Equals(element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value, packageId, StringComparison.Ordinal))
            .Attribute("Version")
            .ShouldNotBeNull()
            .Value;

        Match propertyReference = Regex.Match(version, @"^\$\((?<name>[^)]+)\)$");
        if (!propertyReference.Success)
        {
            return version;
        }

        return GetCatalogProperty(centralCatalog, propertyReference.Groups["name"].Value);
    }

    private static string GetCatalogProperty(XDocument centralCatalog, string propertyName)
        // Read the shared default; the catalog's conditional Folders override does not apply to Timesheets.
        => centralCatalog.Descendants(propertyName)
            .Single(element => element.AncestorsAndSelf().All(ancestor => ancestor.Attribute("Condition") is null))
            .Value
            .Trim();

    /// <summary>
    /// Loads the first existing catalog among the root <c>Directory.Packages.props</c> import candidates, in
    /// document order, after substituting that file's <c>PropertyGroup</c> values (last assignment wins) and the
    /// repository directory for <c>$(MSBuildThisFileDirectory)</c>. Import and property conditions and global properties are
    /// not evaluated; this approximates, rather than proves, the catalog MSBuild imports.
    /// </summary>
    private static XDocument LoadImportedPackageCatalog()
    {
        XElement rootProps = XDocument.Load(RepositoryRoot.PathTo("Directory.Packages.props")).Root.ShouldNotBeNull();
        string repositoryDirectory = RepositoryRoot.Find().FullName + Path.DirectorySeparatorChar;
        Dictionary<string, string> properties = rootProps.Elements("PropertyGroup")
            .Elements()
            .GroupBy(static element => element.Name.LocalName, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Last().Value.Trim(), StringComparer.Ordinal);

        string[] candidates = rootProps.Elements("Import")
            .Select(static import => import.Attribute("Project")?.Value ?? string.Empty)
            .Select(project => Regex.Replace(
                project,
                @"\$\((?<name>[^)]+)\)",
                match => properties.TryGetValue(match.Groups["name"].Value, out string? value) ? value : match.Value))
            .Select(path => path.Replace("$(MSBuildThisFileDirectory)", repositoryDirectory, StringComparison.Ordinal))
            .ToArray();
        candidates.ShouldNotBeEmpty("Directory.Packages.props must import the shared central package catalog.");

        string catalogPath = candidates.FirstOrDefault(File.Exists)
            .ShouldNotBeNull($"None of the imported catalog candidates exists: {string.Join(", ", candidates)}");
        return XDocument.Load(catalogPath);
    }

    private static string ReadAppHostSdkVersion()
    {
        const string appHostSdkPrefix = "Aspire.AppHost.Sdk/";
        XElement appHost = XDocument.Load(RepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets.AppHost",
            "Hexalith.Timesheets.AppHost.csproj")).Root.ShouldNotBeNull();
        string appHostSdk = appHost.Attribute("Sdk").ShouldNotBeNull().Value;
        appHostSdk.ShouldStartWith(appHostSdkPrefix);
        return appHostSdk[appHostSdkPrefix.Length..];
    }
}
