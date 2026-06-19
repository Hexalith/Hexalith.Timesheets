using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.Policies;

public sealed class TimesheetsEvidencePolicyEvaluator : ITimesheetsPolicyEvaluator
{
    private readonly TimesheetsEvidencePolicyOptions _options;

    public TimesheetsEvidencePolicyEvaluator(TimesheetsEvidencePolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
    }

    public ValueTask<TimesheetsPolicyEvaluationResult> EvaluateAsync(
        TimesheetsAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Operation == TimesheetsOperation.Unknown)
        {
            return ValueTask.FromResult(TimesheetsPolicyEvaluationResult.Denied(
                TimesheetsDenialCategory.UnconfiguredPolicy,
                "Authority cannot be resolved."));
        }

        if (!IsTrustBearingOperation(request.Operation))
        {
            return ValueTask.FromResult(TimesheetsPolicyEvaluationResult.Allowed());
        }

        if (!_options.LegalHoldPolicyConfigured || !_options.TenantRetentionOverridePolicyConfigured)
        {
            return ValueTask.FromResult(TimesheetsPolicyEvaluationResult.Denied(
                TimesheetsDenialCategory.RetentionPolicyMissing,
                "Retention policy is unresolved for this action."));
        }

        if (!_options.CommentSensitivityPolicyConfigured)
        {
            return ValueTask.FromResult(TimesheetsPolicyEvaluationResult.Denied(
                TimesheetsDenialCategory.CommentPolicyMissing,
                "Comment policy is unresolved for this action."));
        }

        return ValueTask.FromResult(TimesheetsPolicyEvaluationResult.Allowed());
    }

    private static bool IsTrustBearingOperation(TimesheetsOperation operation)
    {
        return operation is TimesheetsOperation.Command
            or TimesheetsOperation.Export
            or TimesheetsOperation.Confirmation
            or TimesheetsOperation.UiActionVisibility;
    }
}
