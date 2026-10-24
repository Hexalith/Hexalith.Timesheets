using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

/// <summary>Verifies actual restore, build and runtime identities across a consumer-owned source graph.</summary>
public sealed class SourceVersionGraphTests
{
    /// <summary>NuGet and MSBuild must select the same consumer version rather than the sibling default.</summary>
    /// <param name="solution">Whether to enter through a solution rather than a standalone project.</param>
    /// <param name="suppliedVersion">An optional explicit caller version.</param>
    /// <param name="pollutedEnvironment">Whether inherited lowercase MSBuild properties try to replace the graph.</param>
    [Theory]
    [InlineData(true, null, false)]
    [InlineData(true, "9.8.7", false)]
    [InlineData(false, null, false)]
    [InlineData(false, "9.8.7", false)]
    [InlineData(true, null, true)]
    [InlineData(true, "9.8.7", true)]
    [InlineData(false, null, true)]
    [InlineData(false, "9.8.7", true)]
    public async Task ConsumerVersionSurvivesRestoreBuildAndRuntime(bool solution, string? suppliedVersion, bool pollutedEnvironment)
    {
        DirectoryInfo temporary = Directory.CreateTempSubdirectory("timesheets-source-graph-");
        try
        {
            string root = temporary.FullName;
            string consumer = Path.Combine(root, "consumer");
            string works = Path.Combine(root, "works");
            string platform = Path.Combine(root, "platform");
            Directory.CreateDirectory(consumer);
            Directory.CreateDirectory(works);
            Directory.CreateDirectory(platform);
            foreach (string file in new[] { "global.json", "Directory.Build.targets", "Directory.Solution.props", "Directory.Solution.targets" })
                File.Copy(RepositoryRoot.PathTo(file), Path.Combine(root, file));
            File.WriteAllText(Path.Combine(root, "Directory.Build.props"), """
                <Project>
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <UseHexalithProjectReferences Condition="'$(Configuration)' == 'Debug'">true</UseHexalithProjectReferences>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(root, "Directory.Packages.props"), """
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                    <HexalithEventStoreVersion Condition="'$(HexalithEventStoreVersion)' == ''">4.5.6</HexalithEventStoreVersion>
                  </PropertyGroup>
                </Project>
                """);
            foreach (string sibling in new[] { works, platform })
            {
                File.WriteAllText(Path.Combine(sibling, "Directory.Build.props"), """
                    <Project>
                      <PropertyGroup>
                        <TargetFramework>net10.0</TargetFramework>
                        <HexalithEventStoreVersion Condition="'$(HexalithEventStoreVersion)' == ''">1.2.3</HexalithEventStoreVersion>
                        <Version>$(HexalithEventStoreVersion)</Version>
                      </PropertyGroup>
                      <ItemDefinitionGroup>
                        <ProjectReference>
                          <AdditionalProperties>HexalithEventStoreVersion=$(HexalithEventStoreVersion)</AdditionalProperties>
                        </ProjectReference>
                      </ItemDefinitionGroup>
                    </Project>
                    """);
                File.WriteAllText(Path.Combine(sibling, "Directory.Packages.props"), """
                    <Project>
                      <PropertyGroup>
                        <HexalithEventStoreVersion Condition="'$(HexalithEventStoreVersion)' == ''">1.2.3</HexalithEventStoreVersion>
                      </PropertyGroup>
                    </Project>
                    """);
            }
            const string probeName = "Hexalith.Timesheets.GraphProbe";
            string probe = Path.Combine(consumer, probeName + ".csproj");
            File.WriteAllText(probe, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><OutputType>Exe</OutputType></PropertyGroup>
                  <ItemGroup><ProjectReference Include="../works/Hexalith.Works.Contracts.csproj" /></ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(consumer, "Program.cs"), "System.Console.WriteLine(Stub.Works.WorkVersion.Value);");
            File.WriteAllText(Path.Combine(works, "Hexalith.Works.Contracts.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><Version>2.0.0</Version></PropertyGroup>
                  <ItemGroup><ProjectReference Include="../platform/Hexalith.EventStore.Contracts.csproj" /></ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(works, "WorkVersion.cs"), """
                namespace Stub.Works;
                public static class WorkVersion
                {
                    public static string Value => typeof(Stub.EventStore.Marker).Assembly.GetName().Version!.ToString();
                }
                """);
            File.WriteAllText(Path.Combine(platform, "Hexalith.EventStore.Contracts.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(Path.Combine(platform, "Marker.cs"), "namespace Stub.EventStore; public sealed class Marker { }");
            string solutionPath = Path.Combine(root, "Graph.slnx");
            File.WriteAllText(solutionPath, "<Solution><Project Path=\"consumer/Hexalith.Timesheets.GraphProbe.csproj\" /></Solution>");
            string entry = solution ? solutionPath : probe;
            List<string> properties = ["-p:Configuration=Debug", "-p:UseHexalithProjectReferences=true", "-m:1", "/nr:false"];
            if (suppliedVersion is not null) properties.Add("-p:HexalithEventStoreVersion=" + suppliedVersion);
            _ = await RunDotnetAsync(root, ["restore", entry, .. properties], pollutedEnvironment);
            string expected = suppliedVersion ?? "4.5.6";
            foreach (string projectDirectory in new[] { consumer, works })
            {
                using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectDirectory, "obj", "project.assets.json")));
                string[] identities = assets.RootElement.GetProperty("targets").EnumerateObject()
                    .SelectMany(target => target.Value.EnumerateObject())
                    .Where(item => item.Name.StartsWith("Hexalith.EventStore.Contracts/", StringComparison.Ordinal))
                    .Select(item => item.Name).Distinct().ToArray();
                identities.ShouldBe(["Hexalith.EventStore.Contracts/" + expected]);
            }
            _ = await RunDotnetAsync(root, ["build", entry, "--no-restore", .. properties], pollutedEnvironment);
            string binaryDirectory = Path.Combine(consumer, "bin", "Debug", "net10.0");
            AssemblyName.GetAssemblyName(Path.Combine(binaryDirectory, "Hexalith.EventStore.Contracts.dll"))
                .Version.ShouldBe(Version.Parse(expected + ".0"));
            using (FileStream stream = File.OpenRead(Path.Combine(works, "bin", "Debug", "net10.0", "Hexalith.Works.Contracts.dll")))
            using (PEReader image = new(stream))
            {
                MetadataReader metadata = image.GetMetadataReader();
                AssemblyReference reference = metadata.AssemblyReferences.Select(metadata.GetAssemblyReference)
                    .Single(item => metadata.GetString(item.Name) == "Hexalith.EventStore.Contracts");
                reference.Version.ShouldBe(Version.Parse(expected + ".0"));
            }
            string runtime = await RunDotnetAsync(root, [Path.Combine(binaryDirectory, probeName + ".dll")], pollutedEnvironment);
            runtime.Trim().ShouldBe(expected + ".0");
        }
        finally
        {
            temporary.Delete(recursive: true);
        }
    }

    private static async Task<string> RunDotnetAsync(string directory, string[] arguments, bool pollutedEnvironment)
    {
        using Process process = new();
        ProcessStartInfo start = process.StartInfo;
        start.FileName = "dotnet";
        start.WorkingDirectory = directory;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.UseShellExecute = false;
        start.Environment["DOTNET_CLI_HOME"] = Path.Combine(Path.GetTempPath(), "dotnet-cli-home");
        HashSet<string> propertyNames = new(["HexalithEventStoreVersion", "UseHexalithProjectReferences", "DirectoryBuildPropsPath", "DirectoryBuildTargetsPath", "DirectoryPackagesPropsPath"], StringComparer.OrdinalIgnoreCase);
        if (pollutedEnvironment)
        {
            foreach (string name in propertyNames)
                start.Environment[name.ToLowerInvariant()] = name switch
                {
                    "HexalithEventStoreVersion" => "0.0.1",
                    "UseHexalithProjectReferences" => "false",
                    _ => Path.Combine(directory, "missing-" + name + ".props"),
                };
        }
        foreach (string name in start.Environment.Keys.Where(propertyNames.Contains).ToArray())
            start.Environment.Remove(name);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        process.Start().ShouldBeTrue();
        Task<string> output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        string standardOutput = await output;
        process.ExitCode.ShouldBe(0, string.Join(' ', arguments) + Environment.NewLine + standardOutput + await error);
        return standardOutput;
    }
}
