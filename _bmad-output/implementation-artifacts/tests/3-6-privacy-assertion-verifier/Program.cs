// Run against the rebuilt IntegrationTests output; this verifies assertion behavior only.
using System.Reflection;
using System.Runtime.Loader;

ArgumentException.ThrowIfNullOrWhiteSpace(args.Single());
string assemblyDirectory = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    string dependency = Path.Combine(assemblyDirectory, name.Name + ".dll");
    return File.Exists(dependency) ? context.LoadFromAssemblyPath(dependency) : null;
};
Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(assemblyDirectory, "Hexalith.Timesheets.IntegrationTests.dll"));
Type boundary = assembly.GetType("Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests", true)!;
Type recordType = boundary.GetNestedType("LogRecord", BindingFlags.NonPublic)!;
MethodInfo diagnostics = boundary.GetMethod("AssertSensitiveDiagnosticsAbsent", BindingFlags.NonPublic | BindingFlags.Static)!;
MethodInfo response = boundary.GetMethod("AssertSensitiveMaterialAbsent", BindingFlags.NonPublic | BindingFlags.Static)!;
MethodInfo hashMethod = boundary.GetMethod("Hash", BindingFlags.NonPublic | BindingFlags.Static)!;
const string token = "query-secret-probe";
string hash = (string)hashMethod.Invoke(null, [token])!;
string[] protectedValues = [token, hash, "party-1", "party-2", "project-1", "work-1", "time-entry-1", "time-entry-2"];
string[] genericValues = ["comment", "token", "Delivery", "durationMinutes", "60", "Draft", "RecoveryPath", "revoked", "used", "unauthorized", "cross-tenant", "wrong-recipient", "wrong-action", "stale-catalog", "project-owned", "repeated-token"];
int checks = 0;
void Check(MethodInfo method, object?[] values, bool mustReject)
{
    bool rejected = false;
    try
    {
        method.Invoke(null, values);
    }
    catch (TargetInvocationException exception) when (exception.InnerException?.GetType().FullName == "Shouldly.ShouldAssertException")
    {
        rejected = true;
    }

    if (rejected != mustReject)
    {
        throw new InvalidOperationException($"Unexpected assertion outcome: expected rejection {mustReject}.");
    }
    checks++;
}
object Record(string category, string message, params string[] state) => Activator.CreateInstance(recordType, [category, message, state])!;
string genericPath = "/tmp/timesheets-privacy-" + string.Join('-', genericValues);
foreach (string category in new[] { "Microsoft.Hosting.Lifetime", "Microsoft.AspNetCore.Hosting.Diagnostics", "ThirdParty.Diagnostics" })
{
    Check(diagnostics, [Record(category, "Content root path: " + genericPath), token], false);
    Check(diagnostics, [Record(category, "Content root path: " + genericPath), string.Empty], false);
    foreach (string value in protectedValues)
    {
        Check(diagnostics, [Record(category, "probe " + value), token], true);
        Check(diagnostics, [Record(category, "probe", "value=" + value), token], true);
        Check(diagnostics, [Record(category + "." + value, "probe"), token], true);
    }
}
foreach (string value in genericValues)
{
    Check(diagnostics, [Record("Hexalith.Timesheets.Tests", "probe " + value), token], true);
    Check(diagnostics, [Record("Hexalith.Timesheets.Tests", "probe", "value=" + value), token], true);
    Check(response, ["response " + value, token], true);
}
foreach (string value in protectedValues)
{
    Check(diagnostics, [Record("Hexalith.Timesheets.Tests", "probe " + value), token], true);
    Check(response, ["response " + value, token], true);
}
Check(diagnostics, [Record("Hexalith.Timesheets.Tests", "Magic-link denied", "Category=Unknown"), token], false);
Check(response, ["Magic-link confirmation request was not accepted.", token], false);
Console.WriteLine($"Privacy assertion verification: {checks} checks passed; harmless framework vocabulary accepted, protected values rejected across categories/message/state, Timesheets and response vocabulary checks retained.");
