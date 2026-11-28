using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Policies;

namespace Hexalith.Timesheets.Runtime.ProjectWorkflow;

/// <summary>Applies configured evidence policy and limits ledger rows to their contributor or Project approver.</summary>
internal sealed class ProjectWorkflowEvidencePolicy(ProjectWorkflowOptions options) : ITimesheetsPolicyEvaluator
{
    private readonly TimesheetsEvidencePolicyEvaluator _evidence = new(options.EvidencePolicy);

    /// <inheritdoc />
    public ValueTask<TimesheetsPolicyEvaluationResult> EvaluateAsync(
        TimesheetsAuthorizationRequest request, CancellationToken cancellationToken)
    {
        if (!request.Context.IsProjectWorkflow || request.Operation != TimesheetsOperation.ProjectionRead)
        {
            return _evidence.EvaluateAsync(request, cancellationToken);
        }

        if (request.Contributor is { } contributor && contributor != request.Context.Actor
            && !options.ApproverPartyIds.Contains(request.Context.Actor?.PartyId, StringComparer.Ordinal))
        {
            return ValueTask.FromResult(TimesheetsPolicyEvaluationResult.Denied(
                TimesheetsDenialCategory.InsufficientRole, "The workflow scope is unavailable."));
        }

        return _evidence.EvaluateAsync(request with { Operation = TimesheetsOperation.Command }, cancellationToken);
    }
}
