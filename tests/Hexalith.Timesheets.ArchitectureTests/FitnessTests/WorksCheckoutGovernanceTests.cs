using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

public sealed class WorksCheckoutGovernanceTests
{
    private const string MsBuildThisFileDirectory = "$(MSBuildThisFileDirectory)";
    private const string ReferencesWorksPath = "references/Hexalith.Works";
    private const string SiblingWorksPath = "../Hexalith.Works";

    private static readonly Regex MsBuildDirectoryWorksPath = new(
        @"\$\(\s*MSBuildThisFileDirectory\s*\)(?<relative>[^'""<>\r\n)]*?Hexalith\.Works)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SubmodulePath = new(
        @"^\s*path\s*=\s*(?<path>.+?)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public void Gitmodules_declares_works_only_under_references()
    {
        string gitmodules = File.ReadAllText(RepositoryRoot.PathTo(".gitmodules"));
        string[] worksPaths = SubmodulePath.Matches(gitmodules)
            .Select(match => NormalizeRepositoryPath(match.Groups["path"].Value))
            .Where(IsWorksPath)
            .ToArray();

        worksPaths.Length.ShouldBe(1, "Exactly one Hexalith.Works submodule must be declared.");
        worksPaths[0].ShouldBe(ReferencesWorksPath);
    }

    [Fact]
    public async Task Git_index_has_references_works_gitlink_and_no_other_works_gitlink()
    {
        IReadOnlyList<(string Mode, string Path)> staged = await ReadStagedPathsAsync();
        (string Mode, string Path)[] worksGitlinks = staged
            .Where(static entry => string.Equals(entry.Mode, "160000", StringComparison.Ordinal))
            .Select(entry => (entry.Mode, NormalizeRepositoryPath(entry.Path)))
            .Where(entry => IsWorksPath(entry.Item2))
            .ToArray();

        worksGitlinks.Length.ShouldBe(1, "Exactly one Hexalith.Works gitlink must be tracked.");
        worksGitlinks[0].Mode.ShouldBe("160000");
        worksGitlinks[0].Path.ShouldBe(ReferencesWorksPath);
    }

    [Fact]
    public async Task Directory_build_props_preserves_caller_supplied_hexalith_works_root()
    {
        XElement[] worksRoots = LoadWorksRootAssignments();

        foreach (XElement worksRoot in worksRoots)
        {
            string? condition = worksRoot.Attribute("Condition")?.Value;
            condition.ShouldNotBeNull("HexalithWorksRoot assignments must be conditional.");
            condition.ShouldContain("'$(HexalithWorksRoot)' == ''");
        }

        string unset = await EvaluateHexalithWorksRootAsync();
        NormalizeFullPath(unset).ShouldBe(NormalizeFullPath(RepositoryRoot.PathTo("references", "Hexalith.Works")));

        string supplied = Path.Combine(Path.GetTempPath(), "explicit-hexalith-works-root");
        (await EvaluateHexalithWorksRootAsync(supplied)).ShouldBe(supplied);

        DirectoryInfo environmentSupplied = Directory.CreateTempSubdirectory("explicit-hexalith-works-root-");
        try
        {
            (await EvaluateHexalithWorksRootAsync(environmentSupplied.FullName, supplyViaEnvironment: true))
                .ShouldBe(environmentSupplied.FullName);
        }
        finally
        {
            environmentSupplied.Delete(recursive: true);
        }
    }

    [Fact]
    public void Directory_build_props_resolves_references_then_sibling_and_rejects_root_local_probe()
    {
        string propsPath = RepositoryRoot.PathTo("Directory.Build.props");
        string props = File.ReadAllText(propsPath);
        XElement[] worksRoots = LoadWorksRootAssignments();

        worksRoots.Length.ShouldBe(2, "Only the references/ default and sibling fallback may assign HexalithWorksRoot.");
        NormalizeMsBuildPath(worksRoots[0].Value).ShouldBe(ReferencesWorksPath);
        NormalizeMsBuildPath(worksRoots[1].Value).ShouldBe(SiblingWorksPath);

        string[] worksPaths = FindMsBuildDirectoryWorksPaths(props).ToArray();
        worksPaths.ShouldContain(ReferencesWorksPath);
        worksPaths.ShouldContain(SiblingWorksPath);
        worksPaths.ShouldNotContain("Hexalith.Works");

        FindMsBuildDirectoryWorksPaths("$(MSBuildThisFileDirectory)/Hexalith.Works").Single()
            .ShouldBe("Hexalith.Works");
        FindMsBuildDirectoryWorksPaths(@"$(MSBuildThisFileDirectory)references\..\Hexalith.Works").Single()
            .ShouldBe("Hexalith.Works");
    }

    [Fact]
    public async Task Directory_build_props_resolves_sibling_then_prefers_references_when_both_exist()
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "timesheets-works-governance-" + Guid.NewGuid().ToString("N"));
        string shadowRepository = Path.Combine(temporaryRoot, "Hexalith.Timesheets");
        string projectPath = Path.Combine(shadowRepository, "WorksRootProbe.csproj");

        try
        {
            Directory.CreateDirectory(shadowRepository);
            Directory.CreateDirectory(Path.Combine(temporaryRoot, "Hexalith.Works", "src", "Hexalith.Works.Contracts"));
            File.Copy(RepositoryRoot.PathTo("Directory.Build.props"), Path.Combine(shadowRepository, "Directory.Build.props"));
            File.Copy(RepositoryRoot.PathTo("global.json"), Path.Combine(shadowRepository, "global.json"));
            File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

            string evaluated = await EvaluateHexalithWorksRootAsync(shadowRepository, projectPath);
            NormalizeFullPath(evaluated).ShouldBe(
                NormalizeFullPath(Path.Combine(temporaryRoot, "Hexalith.Works")));

            string referencesWorks = Path.Combine(
                shadowRepository,
                "references",
                "Hexalith.Works",
                "src",
                "Hexalith.Works.Contracts");
            Directory.CreateDirectory(referencesWorks);

            string preferred = await EvaluateHexalithWorksRootAsync(shadowRepository, projectPath);
            NormalizeFullPath(preferred).ShouldBe(
                NormalizeFullPath(Path.Combine(shadowRepository, "references", "Hexalith.Works")));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Repository_root_does_not_contain_a_works_checkout()
    {
        Directory.Exists(RepositoryRoot.PathTo("Hexalith.Works"))
            .ShouldBeFalse("A repository-root Hexalith.Works checkout must not return.");
    }

    private static async Task<string> EvaluateHexalithWorksRootAsync(
        string? suppliedRoot = null,
        bool supplyViaEnvironment = false)
    {
        string projectPath = Path.Combine(
            RepositoryRoot.Find().FullName,
            "src",
            "Hexalith.Timesheets.Works",
            "Hexalith.Timesheets.Works.csproj");

        return await EvaluateHexalithWorksRootAsync(
            RepositoryRoot.Find().FullName,
            projectPath,
            suppliedRoot,
            supplyViaEnvironment);
    }

    private static async Task<string> EvaluateHexalithWorksRootAsync(
        string workingDirectory,
        string projectPath,
        string? suppliedRoot = null,
        bool supplyViaEnvironment = false)
    {
        (int exitCode, string output, string error) = await RunProcessAsync(
            "dotnet",
            workingDirectory,
            startInfo =>
            {
                startInfo.Environment["DOTNET_CLI_HOME"] = Path.Combine(Path.GetTempPath(), "dotnet-cli-home");
                startInfo.Environment.Remove("HexalithWorksRoot");
                startInfo.ArgumentList.Add("msbuild");
                startInfo.ArgumentList.Add(projectPath);
                startInfo.ArgumentList.Add("-nologo");
                startInfo.ArgumentList.Add("-getProperty:HexalithWorksRoot");

                if (suppliedRoot is null)
                {
                    return;
                }

                if (supplyViaEnvironment)
                {
                    startInfo.Environment["HexalithWorksRoot"] = suppliedRoot;
                }
                else
                {
                    startInfo.ArgumentList.Add("-p:HexalithWorksRoot=" + suppliedRoot);
                }
            });

        exitCode.ShouldBe(0, error);
        return output.Trim();
    }

    private static IEnumerable<string> FindMsBuildDirectoryWorksPaths(string text)
    {
        return MsBuildDirectoryWorksPath.Matches(text)
            .Select(match => NormalizeRepositoryPath(match.Groups["relative"].Value.TrimStart('/', '\\')));
    }

    private static bool IsWorksPath(string path)
    {
        return path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(static segment => string.Equals(segment, "Hexalith.Works", StringComparison.OrdinalIgnoreCase));
    }

    private static XElement[] LoadWorksRootAssignments()
    {
        return XDocument.Load(RepositoryRoot.PathTo("Directory.Build.props"))
            .Descendants("HexalithWorksRoot")
            .ToArray();
    }

    private static string NormalizeFullPath(string path)
    {
        return Path.GetFullPath(path.Replace('\\', Path.DirectorySeparatorChar))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string NormalizeMsBuildPath(string path)
    {
        path.ShouldStartWith(MsBuildThisFileDirectory);
        return NormalizeRepositoryPath(path[MsBuildThisFileDirectory.Length..]);
    }

    private static string NormalizeRepositoryPath(string path)
    {
        string trimmed = path.Trim().Trim('"', '\'');
        string platformPath = trimmed
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(RepositoryRoot.Find().FullName, platformPath));

        return Path.GetRelativePath(RepositoryRoot.Find().FullName, fullPath).Replace('\\', '/');
    }

    private static async Task<IReadOnlyList<(string Mode, string Path)>> ReadStagedPathsAsync()
    {
        (int exitCode, string output, string error) = await RunProcessAsync(
            "git",
            RepositoryRoot.Find().FullName,
            static startInfo =>
            {
                startInfo.ArgumentList.Add("ls-files");
                startInfo.ArgumentList.Add("--stage");
            });

        exitCode.ShouldBe(0, error);

        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static line =>
            {
                string[] metadataAndPath = line.Split('\t', 2);
                metadataAndPath.Length.ShouldBe(2, line);
                string mode = metadataAndPath[0].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
                return (mode, metadataAndPath[1]);
            })
            .ToArray();
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(
        string fileName,
        string workingDirectory,
        Action<ProcessStartInfo> configure)
    {
        using Process process = new();
        process.StartInfo.FileName = fileName;
        process.StartInfo.WorkingDirectory = workingDirectory;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        configure(process.StartInfo);

        process.Start().ShouldBeTrue($"Could not start {fileName}.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(ProcessTimeout);

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
            throw new TimeoutException($"{fileName} did not exit within {ProcessTimeout}.");
        }

        return (process.ExitCode, await output, await error);
    }
}
