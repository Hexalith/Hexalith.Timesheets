using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

public sealed class WorksCheckoutGovernanceTests
{
    private static readonly Regex RootLocalWorksProbe = new(
        @"\$\(MSBuildThisFileDirectory\)(?:\.[\\/]|[\\/])?Hexalith\.Works\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void Gitmodules_declares_works_only_under_references()
    {
        string[] submodulePaths = File.ReadAllLines(RepositoryRoot.PathTo(".gitmodules"))
            .Select(static line => line.Trim())
            .Where(static line => line.StartsWith("path = ", StringComparison.Ordinal))
            .Select(static line => line["path = ".Length..])
            .ToArray();

        submodulePaths.ShouldContain("references/Hexalith.Works");
        submodulePaths.ShouldNotContain("Hexalith.Works");
    }

    [Fact]
    public void Git_index_has_references_works_gitlink_and_no_repository_root_works_path()
    {
        IReadOnlyList<(string Mode, string Path)> staged = ReadStagedPaths(
            "Hexalith.Works",
            "references/Hexalith.Works");

        staged.ShouldNotContain(entry => string.Equals(entry.Path, "Hexalith.Works", StringComparison.Ordinal));
        staged.ShouldContain(entry =>
            string.Equals(entry.Path, "references/Hexalith.Works", StringComparison.Ordinal)
            && string.Equals(entry.Mode, "160000", StringComparison.Ordinal));
    }

    [Fact]
    public void Directory_build_props_preserves_caller_supplied_hexalith_works_root()
    {
        XElement[] worksRoots = XDocument.Load(RepositoryRoot.PathTo("Directory.Build.props"))
            .Descendants("HexalithWorksRoot")
            .ToArray();

        worksRoots.ShouldNotBeEmpty();

        foreach (XElement worksRoot in worksRoots)
        {
            string? condition = worksRoot.Attribute("Condition")?.Value;
            condition.ShouldNotBeNull("HexalithWorksRoot assignments must be conditional.");
            condition.ShouldContain("'$(HexalithWorksRoot)' == ''");
        }

        string unset = EvaluateHexalithWorksRoot();
        unset.Replace('\\', '/').ShouldContain("references/Hexalith.Works");

        string supplied = Path.Combine(Path.GetTempPath(), "explicit-hexalith-works-root");
        EvaluateHexalithWorksRoot(supplied).ShouldBe(supplied);

        string environmentSupplied = Path.Combine(Path.GetTempPath(), "explicit-hexalith-works-root-from-environment");
        EvaluateHexalithWorksRoot(environmentSupplied, supplyViaEnvironment: true).ShouldBe(environmentSupplied);
    }

    [Fact]
    public void Directory_build_props_resolves_references_then_sibling_and_rejects_root_local_probe()
    {
        string props = File.ReadAllText(RepositoryRoot.PathTo("Directory.Build.props"));

        int referencesProbe = props.IndexOf(
            @"$(MSBuildThisFileDirectory)references\Hexalith.Works",
            StringComparison.Ordinal);
        int siblingProbe = props.IndexOf(
            @"$(MSBuildThisFileDirectory)..\Hexalith.Works",
            StringComparison.Ordinal);

        referencesProbe.ShouldBeGreaterThanOrEqualTo(0, "Default HexalithWorksRoot must probe references/Hexalith.Works.");
        siblingProbe.ShouldBeGreaterThanOrEqualTo(0, "Fallback HexalithWorksRoot must probe ../Hexalith.Works.");
        referencesProbe.ShouldBeLessThan(siblingProbe, "references/Hexalith.Works must be probed before ../Hexalith.Works.");

        RootLocalWorksProbe.IsMatch("$(MSBuildThisFileDirectory)/Hexalith.Works").ShouldBeTrue();
        RootLocalWorksProbe.IsMatch(@"$(MSBuildThisFileDirectory)\Hexalith.Works").ShouldBeTrue();
        RootLocalWorksProbe.IsMatch(@"$(MSBuildThisFileDirectory)..\Hexalith.Works").ShouldBeFalse();

        RootLocalWorksProbe.IsMatch(props)
            .ShouldBeFalse("Directory.Build.props must not restore a repository-root Hexalith.Works probe.");
    }

    [Fact]
    public void Repository_root_does_not_contain_a_works_checkout()
    {
        Directory.Exists(RepositoryRoot.PathTo("Hexalith.Works"))
            .ShouldBeFalse("A repository-root Hexalith.Works checkout must not return.");
    }

    private static string EvaluateHexalithWorksRoot(string? suppliedRoot = null, bool supplyViaEnvironment = false)
    {
        using Process process = new();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.WorkingDirectory = RepositoryRoot.Find().FullName;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.Environment["DOTNET_CLI_HOME"] = "/tmp/dotnet-cli-home";
        process.StartInfo.ArgumentList.Add("msbuild");
        process.StartInfo.ArgumentList.Add(
            Path.Combine("src", "Hexalith.Timesheets.Works", "Hexalith.Timesheets.Works.csproj"));
        process.StartInfo.ArgumentList.Add("-nologo");
        process.StartInfo.ArgumentList.Add("-getProperty:HexalithWorksRoot");
        if (suppliedRoot is not null)
        {
            if (supplyViaEnvironment)
            {
                process.StartInfo.Environment["HexalithWorksRoot"] = suppliedRoot;
            }
            else
            {
                process.StartInfo.ArgumentList.Add("-p:HexalithWorksRoot=" + suppliedRoot);
            }
        }

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, error);

        return output.Trim();
    }

    private static IReadOnlyList<(string Mode, string Path)> ReadStagedPaths(params string[] pathspecs)
    {
        using Process process = new();
        process.StartInfo.FileName = "git";
        process.StartInfo.WorkingDirectory = RepositoryRoot.Find().FullName;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.ArgumentList.Add("ls-files");
        process.StartInfo.ArgumentList.Add("--stage");
        process.StartInfo.ArgumentList.Add("--");
        foreach (string pathspec in pathspecs)
        {
            process.StartInfo.ArgumentList.Add(pathspec);
        }

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, error);

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
}
