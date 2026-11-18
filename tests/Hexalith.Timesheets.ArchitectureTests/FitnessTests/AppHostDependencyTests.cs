using System.Text.RegularExpressions;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

public sealed class AppHostDependencyTests
{
    /// <summary>
    /// Verifies that the Timesheets-owned AppHost SDK follows the local Aspire.Hosting override, so
    /// the SDK-added AppHost, orchestration, and dashboard packages cannot mix with a second Aspire hosting line.
    /// </summary>
    [Fact]
    public void AppHostSdkTracksTheLocalAspireHostingOverride()
    {
        const string approvedAspireVersion = "17.0.0";
        string appHostVersion = ReadAppHostSdkVersion();
        string hostingVersion = ReadLocalPackageVersionOverride("Aspire.Hosting");

        appHostVersion.ShouldBe(
            approvedAspireVersion,
            "Align Aspire.AppHost.Sdk with the Timesheets-owned Aspire 17 consumer line.");
        hostingVersion.ShouldBe(
            approvedAspireVersion,
            "Directory.Packages.props must override Aspire.Hosting to the same Aspire 17 line as the AppHost SDK.");
        appHostVersion.ShouldBe(
            hostingVersion,
            "Aspire.AppHost.Sdk must equal the local Aspire.Hosting override, or the restored AppHost graph mixes two Aspire lines.");
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

    private static string ReadLocalPackageVersionOverride(string packageId)
    {
        XElement rootProps = XDocument.Load(RepositoryRoot.PathTo("Directory.Packages.props")).Root.ShouldNotBeNull();
        return rootProps.Elements("ItemGroup")
            .Elements("PackageVersion")
            .Where(element => string.Equals(element.Attribute("Update")?.Value, packageId, StringComparison.Ordinal))
            .Select(element => element.Attribute("Version")?.Value)
            .LastOrDefault(static version => !string.IsNullOrWhiteSpace(version))
            .ShouldNotBeNull($"Directory.Packages.props must override {packageId} after importing the shared catalog.");
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
