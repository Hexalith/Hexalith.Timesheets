using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
var projectRoot = args[0];
using var changes = JsonDocument.Parse(File.ReadAllText(args[1]));
var names = changes.RootElement.EnumerateArray().GroupBy(item => item.GetProperty("Old").GetString()!)
    .ToDictionary(group => group.Key, group => group.First().GetProperty("New").GetString()!);
// These source-fitness assertions intentionally reference renamed test methods as text.
var literalReferences = new Dictionary<(string File, string Method, string Old), string>
{
    [("tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/PerformanceEvidenceTests.cs",
      "IntegrationTestsReserveRuntimeAndPerformanceLanesWithoutEnteringFastBaseline",
      "Runtime_integration_lane_is_reserved_for_eventstore_state_store_evidence")] =
        "RuntimeIntegrationLaneIsReservedForEventstoreStateStoreEvidence",
    [("tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/PerformanceEvidenceTests.cs",
      "IntegrationTestsReserveRuntimeAndPerformanceLanesWithoutEnteringFastBaseline",
      "Performance_lane_is_reserved_for_launch_latency_evidence")] =
        "PerformanceLaneIsReservedForLaunchLatencyEvidence"
};
var seenLiteralReferences = new HashSet<(string File, string Method, string Old)>();
var commentReferences = new Dictionary<(string File, string Method, string Old), string>
{
    [("tests/Hexalith.Timesheets.Projections.Tests/MagicLinkStateProjectionHandlerTests.cs",
      "IndexRebuildDropsAHashAnotherTenantAlreadyOwns",
      "// rather than letting either side win. Index_rebuild_replaces_only_target_tenant_slice never")] =
        "// rather than letting either side win. IndexRebuildReplacesOnlyTargetTenantSlice never",
    [("tests/Hexalith.Timesheets.Server.Tests/AuthorityBoundaryTests.cs",
      "ServerAuthorityContextIsDefinedOnlyInTheServerKernel",
      "// type by Public_contracts_and_client_do_not_expose_server_authority_context. The host's")] =
        "// type by PublicContractsAndClientDoNotExposeServerAuthorityContext. The host's"
};
var seenCommentReferences = new HashSet<(string File, string Method, string Old)>();
int count = 0;
foreach (var group in changes.RootElement.EnumerateArray().GroupBy(item => item.GetProperty("File").GetString()!))
{
    var info = new ProcessStartInfo("git") { WorkingDirectory = projectRoot, RedirectStandardOutput = true };
    info.ArgumentList.Add("show"); info.ArgumentList.Add(args[2] + ":" + group.Key);
    using var process = Process.Start(info)!;
    string baseline = process.StandardOutput.ReadToEnd(); process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidOperationException("Baseline read failed");
    var before = CSharpSyntaxTree.ParseText(baseline).GetRoot();
    var after = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(projectRoot, group.Key))).GetRoot();
    foreach (var change in group)
    {
        string oldName = change.GetProperty("Old").GetString()!, newName = change.GetProperty("New").GetString()!;
        var oldMethod = before.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == oldName);
        string owner = string.Join(".", oldMethod.Ancestors().OfType<TypeDeclarationSyntax>().Select(type => type.Identifier.ValueText));
        var newMethod = after.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == newName
            && string.Join(".", method.Ancestors().OfType<TypeDeclarationSyntax>().Select(type => type.Identifier.ValueText)) == owner);
        var renamed = oldMethod.ReplaceTokens(oldMethod.DescendantTokens().Where(token =>
            token.IsKind(SyntaxKind.IdentifierToken) && names.ContainsKey(token.ValueText)),
            (original, _) => SyntaxFactory.Identifier(names[original.ValueText]).WithTriviaFrom(original));
        renamed = renamed.ReplaceTokens(renamed.DescendantTokens().Where(token =>
            token.IsKind(SyntaxKind.StringLiteralToken) && literalReferences.ContainsKey((group.Key, newName, token.ValueText))),
            (original, _) =>
            {
                var key = (group.Key, newName, original.ValueText);
                if (!seenLiteralReferences.Add(key)) throw new InvalidOperationException("Repeated literal reference exception.");
                string value = literalReferences[key];
                return SyntaxFactory.Literal(JsonSerializer.Serialize(value), value).WithTriviaFrom(original);
            });
        renamed = renamed.ReplaceTrivia(renamed.DescendantTrivia().Where(trivia =>
            commentReferences.ContainsKey((group.Key, newName, trivia.ToFullString()))),
            (original, _) =>
            {
                var key = (group.Key, newName, original.ToFullString());
                if (!seenCommentReferences.Add(key)) throw new InvalidOperationException("Repeated comment reference exception.");
                return SyntaxFactory.Comment(commentReferences[key]);
            });
        if (renamed.ToFullString() != newMethod.ToFullString()) throw new InvalidOperationException($"Test content changed: {group.Key} {oldName}");
        count++;
    }
}
if (seenCommentReferences.Count != commentReferences.Count) throw new InvalidOperationException("Missing comment reference exception.");
if (seenLiteralReferences.Count != literalReferences.Count) throw new InvalidOperationException("Missing literal reference exception.");
Console.WriteLine($"All {count} renamed attributed methods preserve exact attributes, signature, data, assertions and trivia after identifier-token substitution and {seenLiteralReferences.Count} exact allowlisted source-fitness literal references plus {seenCommentReferences.Count} exact comment references; all other tokens/literals/comments/trivia are unchanged.");
