using System.Diagnostics;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

/// <summary>Exercises the actual adapter edge through SDK solution-reference assignment.</summary>
public sealed class WorksReferencePropagationTests
{
    /// <summary>Works receives the consumer's configuration and retains an explicit caller override.</summary>
    /// <param name="testConsumer">Whether to exercise the SDK's transitive reference in Works.Tests.</param>
    /// <param name="configuration">The consuming configuration.</param>
    /// <param name="suppliedVersion">An optional caller override.</param>
    [Theory]
    [InlineData(false, "Debug", null)]
    [InlineData(false, "Debug", "9.8.7")]
    [InlineData(false, "Release", null)]
    [InlineData(true, "Debug", null)]
    [InlineData(true, "Debug", "9.8.7")]
    [InlineData(true, "Release", null)]
    public async Task SolutionTraversalPreservesWorksDependencySelection(bool testConsumer, string configuration, string? suppliedVersion)
    {
        DirectoryInfo temporary = Directory.CreateTempSubdirectory("timesheets-works-edge-");
        try
        {
            string consumer = Path.Combine(temporary.FullName, "consumer");
            string works = Path.Combine(temporary.FullName, "works");
            Directory.CreateDirectory(consumer);
            Directory.CreateDirectory(Path.Combine(works, "src", "Hexalith.Works.Contracts"));
            string projectName = testConsumer ? "Hexalith.Timesheets.Works.Tests" : "Hexalith.Timesheets.Works";
            string projectPath = Path.Combine(consumer, projectName + ".csproj");
            File.Copy(RepositoryRoot.PathTo(testConsumer ? "tests" : "src", projectName, projectName + ".csproj"), projectPath);
            File.Copy(RepositoryRoot.PathTo("global.json"), Path.Combine(temporary.FullName, "global.json"));
            XElement properties = new("Project", new XElement("PropertyGroup",
                new XElement("TargetFramework", "net10.0"),
                new XElement("HexalithWorksRoot", works),
                new XElement("HexalithEventStoreVersion", new XAttribute("Condition", "'$(HexalithEventStoreVersion)' == ''"), "4.5.6"),
                new XElement("UseHexalithProjectReferences", new XAttribute("Condition", "'$(Configuration)' == 'Debug'"), "true"),
                new XElement("UseHexalithProjectReferences", new XAttribute("Condition", "'$(UseHexalithProjectReferences)' == ''"), "false")));
            if (testConsumer)
            {
                // Feed the SDK's real IncludeTransitiveProjectReferences target without restoring
                // packages. This is the second reference owner exposed by the actual solution build.
                properties.Add(new XElement("ItemGroup", new XAttribute("Condition", "'$(Configuration)' == 'Debug'"),
                    new XElement("_TransitiveProjectReferences", new XAttribute("Include",
                        Path.Combine(works, "src", "Hexalith.Works.Contracts", "Hexalith.Works.Contracts.csproj")))));
            }
            new XDocument(properties).Save(Path.Combine(consumer, "Directory.Build.props"));
            File.Copy(RepositoryRoot.PathTo("Directory.Build.targets"), Path.Combine(consumer, "Directory.Build.targets"));
            // Mirror the referenced module's pre-SDK defaults without restoring packages or
            // depending on a populated sibling checkout. Its default intentionally differs from the consumer.
            File.WriteAllText(Path.Combine(works, "Directory.Build.props"), """
                <Project>
                  <PropertyGroup>
                    <UseHexalithProjectReferences Condition="'$(Configuration)' == 'Debug'">true</UseHexalithProjectReferences>
                    <UseHexalithProjectReferences Condition="'$(UseHexalithProjectReferences)' == ''">false</UseHexalithProjectReferences>
                    <HexalithEventStoreVersion Condition="'$(HexalithEventStoreVersion)' == ''">1.2.3</HexalithEventStoreVersion>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(works, "src", "Hexalith.Works.Contracts", "Hexalith.Works.Contracts.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            string targets = Path.Combine(temporary.FullName, "Probe.targets");
            File.WriteAllText(targets, """
                <Project>
                  <Target Name="ProbeParent" DependsOnTargets="AssignProjectConfiguration">
                    <ItemGroup>
                      <_WorksEdge Include="@(ProjectReferenceWithConfiguration)" Condition="'%(Filename)' == 'Hexalith.Works.Contracts'" />
                      <_WorksPackage Include="@(PackageReference)" Condition="'%(Identity)' == 'Hexalith.Works.Contracts'" />
                    </ItemGroup>
                    <Error Condition="'$(Configuration)' == 'Release' and ('@(_WorksEdge)' != '' or ('$(MSBuildProjectName)' == 'Hexalith.Timesheets.Works' and '@(_WorksPackage)' == ''))" Text="Release must not select the external Works project." />
                    <Error Condition="'$(Configuration)' == 'Debug' and '@(_WorksEdge)' == ''" Text="Debug must select the external Works project." />
                    <MSBuild Projects="@(_WorksEdge)" Targets="ProbeChild"
                             Properties="%(_WorksEdge.SetConfiguration);%(_WorksEdge.SetPlatform);%(_WorksEdge.AdditionalProperties)"
                             RemoveProperties="%(_WorksEdge.GlobalPropertiesToRemove)" />
                  </Target>
                  <Target Name="ProbeChild">
                    <WriteLinesToFile File="$(ProbeOutput)" Lines="$(Configuration);$(UseHexalithProjectReferences);$(HexalithEventStoreVersion)" Overwrite="true" />
                  </Target>
                </Project>
                """);
            string result = Path.Combine(temporary.FullName, "selection.txt");
            using Process process = new();
            ProcessStartInfo start = process.StartInfo;
            start.FileName = "dotnet";
            start.WorkingDirectory = consumer;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.UseShellExecute = false;
            start.Environment["DOTNET_CLI_HOME"] = Path.Combine(Path.GetTempPath(), "dotnet-cli-home");
            foreach (string name in new[] { "UseHexalithProjectReferences", "HexalithEventStoreVersion", "DirectoryBuildPropsPath", "DirectoryBuildTargetsPath" })
            {
                start.Environment.Remove(name);
            }
            foreach (string argument in new[] { "msbuild", projectPath, "-nologo", "-t:ProbeParent",
                "-p:Configuration=" + configuration, "-p:BuildingSolutionFile=true",
                "-p:SkipResolvePackageAssets=true",
                "-p:CustomAfterMicrosoftCommonTargets=" + targets, "-p:ProbeOutput=" + result, "-m:1", "/nr:false" })
            {
                start.ArgumentList.Add(argument);
            }
            if (suppliedVersion is not null) start.ArgumentList.Add("-p:HexalithEventStoreVersion=" + suppliedVersion);
            process.Start().ShouldBeTrue();
            Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
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
            process.ExitCode.ShouldBe(0, (await output) + (await error));
            if (configuration == "Debug")
            {
                File.ReadAllLines(result).ShouldBe(["Debug", "true", suppliedVersion ?? "4.5.6"]);
            }
            else
            {
                File.Exists(result).ShouldBeFalse();
            }
        }
        finally
        {
            temporary.Delete(recursive: true);
        }
    }
}
