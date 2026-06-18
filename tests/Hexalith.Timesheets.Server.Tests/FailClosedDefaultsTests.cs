using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class FailClosedDefaultsTests
{
    [Fact]
    public void Default_authorization_gate_is_fail_closed()
    {
        string source = File.ReadAllText(ServerSourcePath(
            "Authorization",
            "DenyAllTimesheetsAuthorizationGate.cs"));

        source.ShouldContain("TimesheetsAuthorizationDecision.Denied");
        source.ShouldNotContain("TimesheetsAuthorizationDecision.Allowed");
    }

    [Fact]
    public void Default_reference_validators_reject_unverified_sibling_references()
    {
        string[] validatorFiles =
        [
            ServerSourcePath("References", "DenyAllProjectReferenceValidator.cs"),
            ServerSourcePath("References", "DenyAllWorkReferenceValidator.cs"),
            ServerSourcePath("References", "DenyAllContributorPartyValidator.cs")
        ];

        foreach (string validatorFile in validatorFiles)
        {
            string source = File.ReadAllText(validatorFile);
            source.ShouldContain("ReferenceValidationResult.Invalid");
            source.ShouldNotContain("ReferenceValidationResult.Valid");
        }
    }

    private static string ServerSourcePath(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "src", "Hexalith.Timesheets.Server");
            if (Directory.Exists(candidate))
            {
                return Path.Combine([candidate, .. segments]);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the Timesheets server source.");
    }
}
