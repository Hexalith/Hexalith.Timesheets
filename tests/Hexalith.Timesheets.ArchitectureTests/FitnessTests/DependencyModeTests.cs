using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

/// <summary>Verifies the evaluated source/package boundary across every solution project.</summary>
public sealed class DependencyModeTests
{
    /// <summary>Requires sibling source edges only in Debug and auditing in both configurations.</summary>
    /// <param name="configuration">The solution build configuration.</param>
    /// <param name="sourceMode">The expected evaluated sibling-source switch.</param>
    [Theory]
    [InlineData("Debug", "true")]
    [InlineData("Release", "false")]
    public async Task Evaluated_solution_projects_keep_source_references_in_debug_only(string configuration, string sourceMode)
    {
        string root = RepositoryRoot.Find().FullName;
        XDocument solution = XDocument.Load(Path.Combine(root, "Hexalith.Timesheets.slnx"));
        string[] projects = solution.Descendants("Project").Select(static row => row.Attribute("Path")!.Value).ToArray();
        projects.Length.ShouldBe(15);
        int siblingEdges = 0;
        foreach (string project in projects)
        {
            ProcessStartInfo start = new("dotnet")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.Environment["DOTNET_CLI_HOME"] = Path.Combine(Path.GetTempPath(), "dotnet-cli-home");
            start.Environment.Remove("UseHexalithProjectReferences");
            foreach (string argument in new[] { "msbuild", project, "-nologo", $"-p:Configuration={configuration}",
                "-getProperty:UseHexalithProjectReferences,NuGetAudit", "-getItem:ProjectReference,PackageReference" })
            {
                start.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(start)!;
            Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception cleanupError) when (cleanupError is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Preserve the original cancellation if the owned child already exited.
                }

                throw;
            }
            process.ExitCode.ShouldBe(0, await error);
            using JsonDocument evaluation = JsonDocument.Parse(await output);
            JsonElement properties = evaluation.RootElement.GetProperty("Properties");
            properties.GetProperty("UseHexalithProjectReferences").GetString().ShouldBe(sourceMode, project);
            properties.GetProperty("NuGetAudit").GetString().ShouldBe("true", project);
            foreach (JsonElement reference in evaluation.RootElement.GetProperty("Items").GetProperty("ProjectReference").EnumerateArray())
            {
                string path = Path.GetFullPath(reference.GetProperty("FullPath").GetString()!);
                if (!path.StartsWith(Path.Combine(root, "src") + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && !path.StartsWith(Path.Combine(root, "tests") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    configuration.ShouldBe("Debug", $"Release source leak: {project} -> {path}");
                    siblingEdges++;
                }
            }
        }

        if (configuration == "Debug")
        {
            siblingEdges.ShouldBeGreaterThan(0);
        }
    }
}
