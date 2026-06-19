using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Policies;

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

    [Fact]
    public void Default_tenant_validator_rejects_unconfigured_authority()
    {
        string source = File.ReadAllText(ServerSourcePath(
            "Authorization",
            "DenyAllTimesheetsTenantAccessValidator.cs"));

        source.ShouldContain("Denied");
        source.ShouldNotContain("Allowed()");
        source.ShouldNotContain("Authorized()");
    }

    [Fact]
    public async Task Default_policy_evaluator_blocks_trust_bearing_operations_until_policy_is_configured()
    {
        // The registered default (TimesheetsEvidencePolicyEvaluator + FailClosedDefault options)
        // must deny trust-bearing operations until retention/comment policy is configured.
        TimesheetsEvidencePolicyEvaluator evaluator = new(TimesheetsEvidencePolicyOptions.FailClosedDefault);

        TimesheetsPolicyEvaluationResult result = await evaluator.EvaluateAsync(
            new TimesheetsAuthorizationRequest(
                new TimesheetsRequestContext(
                    new TenantReference("tenant_01"),
                    new PartyReference("party_01"),
                    "correlation_01"),
                TimesheetsOperation.Command),
            TestContext.Current.CancellationToken);

        result.IsAllowed.ShouldBeFalse();
        result.DenialCategory.ShouldBe(TimesheetsDenialCategory.RetentionPolicyMissing);
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
