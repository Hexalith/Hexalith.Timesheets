using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
var rootPath = args[0];
var changes = new List<object>();
string Git(params string[] arguments)
{
    var info = new ProcessStartInfo("git") { WorkingDirectory = rootPath, RedirectStandardOutput = true };
    foreach (string argument in arguments) info.ArgumentList.Add(argument);
    using var process = Process.Start(info)!;
    string result = process.StandardOutput.ReadToEnd(); process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidOperationException("Git source read failed.");
    return result;
}
string baseline = args.Length > 1 ? args[1] : null;
IEnumerable<string> files = baseline is null
    ? Directory.EnumerateFiles(Path.Combine(rootPath, "tests"), "*.cs", SearchOption.AllDirectories)
        .Where(file => !file.Contains("/obj/") && !file.Contains("/bin/"))
    : Git("ls-tree", "-r", "--name-only", baseline, "--", "tests").Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Where(file => file.EndsWith(".cs", StringComparison.Ordinal)).Select(file => Path.Combine(rootPath, file));
foreach (string file in files)
{
    var syntax = CSharpSyntaxTree.ParseText(baseline is null ? File.ReadAllText(file) : Git("show", baseline + ":" + Path.GetRelativePath(rootPath, file))).GetRoot();
    foreach (var method in syntax.DescendantNodes().OfType<MethodDeclarationSyntax>())
    {
        string name = method.Identifier.ValueText;
        bool test = method.AttributeLists.SelectMany(list => list.Attributes).Any(attribute =>
            attribute.Name.ToString().Split('.').Last() is "Fact" or "Theory" or "FactAttribute" or "TheoryAttribute");
        if (!test || !name.Contains('_')) continue;
        string renamed = string.Concat(name.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        var owner = method.Parent!;
        if (owner.ChildNodes().OfType<MethodDeclarationSyntax>().Any(other => other != method && other.Identifier.ValueText == renamed))
            throw new InvalidOperationException($"Naming collision: {file}: {name} -> {renamed}");
        changes.Add(new { File = Path.GetRelativePath(rootPath, file), Old = name, New = renamed,
            Start = method.Identifier.Span.Start, Length = method.Identifier.Span.Length });
    }
}
Console.Write(JsonSerializer.Serialize(changes, new JsonSerializerOptions { WriteIndented = true }));
