using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Commands.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Server.TimesheetPeriods;

public sealed class TimesheetPeriodApprovalCommandService
{
    private const string AccessDenied = "Access denied for this action.";
    private const string AuthorityUnresolved = "Authority cannot be resolved.";
    private const string EntryNeedsCorrection = "Entry needs correction.";
    private const string ProjectionRebuilding = "Projection is rebuilding.";

    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly ITimesheetsApprovalAuthorityResolver _authorityResolver;

    public TimesheetPeriodApprovalCommandService(
        ITimesheetsAccessGuard accessGuard,
        ITimesheetsApprovalAuthorityResolver authorityResolver)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(authorityResolver);

        _accessGuard = accessGuard;
        _authorityResolver = authorityResolver;
    }

    public async ValueTask<TimesheetPeriodApprovalCommandResult> ApproveAsync(
        TimesheetsRequestContext context,
        ApproveTimesheetPeriod command,
        TimesheetPeriodState? periodState,
        IReadOnlyDictionary<TimeEntryId, TimeEntryState?> entryStates,
        TimesheetPeriodSummaryReadModel? periodProjection,
        DateTimeOffset decidedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(entryStates);

        TimesheetsAuthorizationRequest authorizationRequest = CreatePeriodAuthorizationRequest(context, periodState);
        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            authorizationRequest,
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            return new(SafeAuthorization(authorization), null, null, [], []);
        }

        ApprovalAuthorityResolutionResult authority = await ResolveAuthorityAsync(
            authorizationRequest,
            ApprovalAuthorityAction.PeriodApproval,
            periodState?.Contributor,
            cancellationToken).ConfigureAwait(false);

        if (!authority.IsAllowed)
        {
            return new(authorization, SafeAuthority(authority), null, [], []);
        }

        List<TimesheetPeriodBlockingEntryGuidance> blocking = [];
        if (!HasFreshProjection(periodProjection))
        {
            AddPeriodProjectionBlock(periodState, blocking);
        }

        IReadOnlyList<TimeEntryId> affectedEntryIds = periodState?.IncludedTimeEntryIds ?? [];
        foreach (TimeEntryId timeEntryId in affectedEntryIds)
        {
            entryStates.TryGetValue(timeEntryId, out TimeEntryState? entryState);
            await ValidateApprovalEntryAsync(
                context,
                timeEntryId,
                entryState,
                periodState,
                blocking,
                cancellationToken).ConfigureAwait(false);
        }

        if (blocking.Count > 0)
        {
            return new(authorization, authority, RejectPeriod("Timesheet Period approval blocked by one or more entries.", blocking), [], blocking);
        }

        List<TimeEntryApprovalCommandResult> entryResults = [];
        foreach (TimeEntryId timeEntryId in affectedEntryIds)
        {
            TimeEntryState? entryState = entryStates[timeEntryId];
            TimesheetsDomainResult entryResult = TimeEntry.Handle(
                new ApproveTimeEntry(timeEntryId, new TimeEntryApprovalDecisionId(command.TimesheetPeriodApprovalDecisionId.Value)),
                timeEntryId,
                entryState,
                context.Actor,
                context.Tenant,
                decidedAtUtc,
                authority.SourceAttribution,
                TimeEntryApprovalScope.TimesheetPeriod);

            entryResults.Add(new(timeEntryId, authorization, authority, entryResult, true));
            if (!entryResult.IsSuccess && !entryResult.IsNoOp)
            {
                blocking.Add(Guidance(timeEntryId, "approvalState", "invalid-transition", EntryNeedsCorrection));
            }
        }

        if (blocking.Count > 0)
        {
            return new(authorization, authority, RejectPeriod("Timesheet Period approval blocked by one or more entries.", blocking), entryResults, blocking);
        }

        TimesheetsDomainResult periodResult = TimesheetPeriod.Handle(
            command,
            periodState,
            context.Actor,
            context.Tenant,
            decidedAtUtc,
            authority.SourceAttribution);

        return new(authorization, authority, periodResult, entryResults, []);
    }

    public async ValueTask<TimesheetPeriodApprovalCommandResult> RejectAsync(
        TimesheetsRequestContext context,
        RejectTimesheetPeriod command,
        TimesheetPeriodState? periodState,
        IReadOnlyDictionary<TimeEntryId, TimeEntryState?> entryStates,
        TimesheetPeriodSummaryReadModel? periodProjection,
        DateTimeOffset decidedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(entryStates);

        TimesheetsAuthorizationRequest authorizationRequest = CreatePeriodAuthorizationRequest(context, periodState);
        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            authorizationRequest,
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            return new(SafeAuthorization(authorization), null, null, [], []);
        }

        ApprovalAuthorityResolutionResult authority = await ResolveAuthorityAsync(
            authorizationRequest,
            ApprovalAuthorityAction.PeriodRejection,
            periodState?.Contributor,
            cancellationToken).ConfigureAwait(false);

        if (!authority.IsAllowed)
        {
            return new(authorization, SafeAuthority(authority), null, [], []);
        }

        List<TimesheetPeriodBlockingEntryGuidance> blocking = [];
        if (!HasFreshProjection(periodProjection))
        {
            AddPeriodProjectionBlock(periodState, blocking);
        }

        IReadOnlyList<TimesheetPeriodSelectedEntryRejectionEvidence> rejectedEntries = command.RejectedEntries ?? [];
        foreach (TimesheetPeriodSelectedEntryRejectionEvidence rejectedEntry in rejectedEntries)
        {
            entryStates.TryGetValue(rejectedEntry.TimeEntryId, out TimeEntryState? entryState);
            await ValidateRejectionEntryAsync(
                context,
                rejectedEntry.TimeEntryId,
                entryState,
                periodState,
                blocking,
                cancellationToken).ConfigureAwait(false);
        }

        if (blocking.Count > 0)
        {
            return new(authorization, authority, RejectPeriod("Timesheet Period rejection blocked by one or more entries.", blocking), [], blocking);
        }

        List<TimeEntryApprovalCommandResult> entryResults = [];
        foreach (TimesheetPeriodSelectedEntryRejectionEvidence rejectedEntry in rejectedEntries)
        {
            TimeEntryState? entryState = entryStates[rejectedEntry.TimeEntryId];
            TimesheetsDomainResult entryResult = TimeEntry.Handle(
                new RejectTimeEntry(
                    rejectedEntry.TimeEntryId,
                    new TimeEntryApprovalDecisionId(command.TimesheetPeriodApprovalDecisionId.Value),
                    rejectedEntry.Reason),
                rejectedEntry.TimeEntryId,
                entryState,
                context.Actor,
                context.Tenant,
                decidedAtUtc,
                authority.SourceAttribution,
                TimeEntryApprovalScope.TimesheetPeriod);

            entryResults.Add(new(rejectedEntry.TimeEntryId, authorization, authority, entryResult, true));
            if (!entryResult.IsSuccess && !entryResult.IsNoOp)
            {
                blocking.Add(Guidance(rejectedEntry.TimeEntryId, "approvalState", "invalid-transition", EntryNeedsCorrection));
            }
        }

        if (blocking.Count > 0)
        {
            return new(authorization, authority, RejectPeriod("Timesheet Period rejection blocked by one or more entries.", blocking), entryResults, blocking);
        }

        TimesheetsDomainResult periodResult = TimesheetPeriod.Handle(
            command,
            periodState,
            context.Actor,
            context.Tenant,
            decidedAtUtc,
            authority.SourceAttribution);

        return new(authorization, authority, periodResult, entryResults, []);
    }

    private async ValueTask ValidateApprovalEntryAsync(
        TimesheetsRequestContext context,
        TimeEntryId timeEntryId,
        TimeEntryState? state,
        TimesheetPeriodState? periodState,
        List<TimesheetPeriodBlockingEntryGuidance> blocking,
        CancellationToken cancellationToken)
    {
        if (!ValidateCommonEntry(timeEntryId, state, periodState, blocking))
        {
            return;
        }

        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            CreateEntryAuthorizationRequest(context, state!),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            blocking.Add(Guidance(timeEntryId, "authorization", authorization.DenialCategory.ToString(), SafeDenialCopy(authorization)));
            return;
        }

        if (state!.ApprovalState != TimeEntryApprovalState.Submitted)
        {
            blocking.Add(Guidance(timeEntryId, "approvalState", "invalid-transition", EntryNeedsCorrection));
        }
    }

    private async ValueTask ValidateRejectionEntryAsync(
        TimesheetsRequestContext context,
        TimeEntryId timeEntryId,
        TimeEntryState? state,
        TimesheetPeriodState? periodState,
        List<TimesheetPeriodBlockingEntryGuidance> blocking,
        CancellationToken cancellationToken)
    {
        if (!ValidateCommonEntry(timeEntryId, state, periodState, blocking))
        {
            return;
        }

        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            CreateEntryAuthorizationRequest(context, state!),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            blocking.Add(Guidance(timeEntryId, "authorization", authorization.DenialCategory.ToString(), SafeDenialCopy(authorization)));
            return;
        }

        if (state!.ApprovalState != TimeEntryApprovalState.Submitted)
        {
            blocking.Add(Guidance(timeEntryId, "approvalState", "invalid-transition", EntryNeedsCorrection));
        }
    }

    private static bool ValidateCommonEntry(
        TimeEntryId timeEntryId,
        TimeEntryState? state,
        TimesheetPeriodState? periodState,
        List<TimesheetPeriodBlockingEntryGuidance> blocking)
    {
        if (state?.IsRecorded != true)
        {
            blocking.Add(Guidance(timeEntryId, "timeEntryId", "missing", EntryNeedsCorrection));
            return false;
        }

        if (periodState?.IncludedTimeEntryIds.Contains(timeEntryId) != true)
        {
            blocking.Add(Guidance(timeEntryId, "timeEntryId", "not-in-period", EntryNeedsCorrection));
            return false;
        }

        if (state.Contributor != periodState.Contributor)
        {
            blocking.Add(Guidance(timeEntryId, "contributor", "mismatch", EntryNeedsCorrection));
        }

        if (state.CorrectionState == TimeEntryCorrectionState.Superseded
            || state.LockState == TimeEntryLockState.SupersededLocked)
        {
            blocking.Add(Guidance(timeEntryId, "lockState", "superseded", EntryNeedsCorrection));
        }

        return true;
    }

    private ValueTask<ApprovalAuthorityResolutionResult> ResolveAuthorityAsync(
        TimesheetsAuthorizationRequest authorizationRequest,
        ApprovalAuthorityAction action,
        PartyReference? contributor,
        CancellationToken cancellationToken)
    {
        return _authorityResolver.ResolveAsync(
            new ApprovalAuthorityResolutionRequest(
                authorizationRequest with { ApprovalAction = action },
                action,
                contributor),
            cancellationToken);
    }

    private static TimesheetsAuthorizationRequest CreatePeriodAuthorizationRequest(
        TimesheetsRequestContext context,
        TimesheetPeriodState? state)
        => new(context, TimesheetsOperation.Command)
        {
            Contributor = state?.Contributor
        };

    private static TimesheetsAuthorizationRequest CreateEntryAuthorizationRequest(
        TimesheetsRequestContext context,
        TimeEntryState state)
    {
        TimesheetsAuthorizationRequest request = new(context, TimesheetsOperation.Command)
        {
            Contributor = state.Contributor
        };

        if (state.Target?.TargetKind == TimeEntryTargetKind.Project)
        {
            return request with { Project = new ProjectReference(state.Target.TargetId) };
        }

        if (state.Target?.TargetKind == TimeEntryTargetKind.Work)
        {
            return request with { Work = new WorkReference(state.Target.TargetId) };
        }

        return request;
    }

    private static bool HasFreshProjection(TimesheetPeriodSummaryReadModel? projection)
        => projection?.ProjectionFreshness.State == ProjectionFreshnessState.Fresh
            && projection.IncompleteEntryEvidenceIds.Count == 0
            && projection.EntrySummaries.All(static entry =>
                entry.ProjectionFreshness.State == ProjectionFreshnessState.Fresh);

    private static void AddPeriodProjectionBlock(
        TimesheetPeriodState? periodState,
        List<TimesheetPeriodBlockingEntryGuidance> blocking)
    {
        IReadOnlyList<TimeEntryId> ids = periodState?.IncludedTimeEntryIds ?? [];
        if (ids.Count == 0)
        {
            blocking.Add(Guidance(new TimeEntryId("period-evidence"), "projectionFreshness", "not-fresh", ProjectionRebuilding));
            return;
        }

        foreach (TimeEntryId id in ids)
        {
            blocking.Add(Guidance(id, "projectionFreshness", "not-fresh", ProjectionRebuilding));
        }
    }

    private static TimesheetsDomainResult RejectPeriod(
        string message,
        IReadOnlyList<TimesheetPeriodBlockingEntryGuidance> blocking)
        => TimesheetsDomainResult.Rejection([
            new(
                TimesheetsRejectionCode.ValidationFailed,
                message,
                blocking.Select(static item => new TimesheetsFieldError(
                    $"entries[{item.TimeEntryId.Value}].{item.Field}",
                    item.Code,
                    item.Guidance)).ToArray())
        ]);

    private static TimesheetsAuthorizationDecision SafeAuthorization(TimesheetsAuthorizationDecision decision)
    {
        return TimesheetsAuthorizationDecision.Denied(
            decision.DenialCategory,
            SafeReason(decision.DenialCategory));
    }

    private static ApprovalAuthorityResolutionResult SafeAuthority(ApprovalAuthorityResolutionResult authority)
    {
        return ApprovalAuthorityResolutionResult.Denied(
            authority.DenialCategory,
            SafeReason(authority.DenialCategory),
            authority.SourceAttribution);
    }

    private static string SafeDenialCopy(TimesheetsAuthorizationDecision authorization)
        => SafeReason(authorization.DenialCategory);

    private static string SafeReason(TimesheetsDenialCategory category)
        => category is TimesheetsDenialCategory.NonMember
            or TimesheetsDenialCategory.InsufficientRole
                ? AccessDenied
                : AuthorityUnresolved;

    private static TimesheetPeriodBlockingEntryGuidance Guidance(
        TimeEntryId timeEntryId,
        string field,
        string code,
        string guidance)
        => new(timeEntryId, field, code, guidance);
}
