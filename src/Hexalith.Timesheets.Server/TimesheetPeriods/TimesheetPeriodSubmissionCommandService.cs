using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Commands.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimesheetPeriods;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Server.TimesheetPeriods;

public sealed class TimesheetPeriodSubmissionCommandService
{
    private readonly ITimesheetsAccessGuard _accessGuard;

    public TimesheetPeriodSubmissionCommandService(ITimesheetsAccessGuard accessGuard)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        _accessGuard = accessGuard;
    }

    public async ValueTask<TimesheetPeriodSubmissionCommandResult> SubmitAsync(
        TimesheetsRequestContext context,
        SubmitTimesheetPeriod command,
        TimesheetPeriodState? periodState,
        IReadOnlyDictionary<TimeEntryId, TimeEntryState?> entryStates,
        ActivityTypeCatalogReadModel activityTypeCatalog,
        TenantTimesheetPeriodPolicy periodPolicy,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(entryStates);
        ArgumentNullException.ThrowIfNull(activityTypeCatalog);
        ArgumentNullException.ThrowIfNull(periodPolicy);

        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            new TimesheetsAuthorizationRequest(context, TimesheetsOperation.Command)
            {
                Contributor = command.Contributor
            },
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            return new(authorization, null, [], []);
        }

        TimesheetsDomainResult aggregatePrecheck = TimesheetPeriod.Handle(
            command,
            periodState,
            context.Tenant,
            context.Actor,
            submittedAtUtc,
            periodPolicy);

        if (!aggregatePrecheck.IsSuccess)
        {
            return new(authorization, aggregatePrecheck, [], []);
        }

        // Derive the period boundary from the accepted aggregate event so entry
        // validation uses the exact boundary that will be recorded, and so a second
        // tenant time-zone resolution can never diverge from the emitted evidence.
        TimesheetPeriodSubmitted submittedEvent = aggregatePrecheck.Events
            .OfType<TimesheetPeriodSubmitted>()
            .Single();
        TenantLocalPeriodBoundary boundary = new(
            submittedEvent.PeriodKind,
            submittedEvent.PeriodKey,
            submittedEvent.LocalStartDate,
            submittedEvent.LocalEndDate,
            submittedEvent.TenantTimeZoneId);

        List<TimeEntryId> orderedEntryIds = DistinctInOrder(command.TimeEntryIds);
        List<TimesheetPeriodBlockingEntryGuidance> blocking = [];
        List<TimeEntrySubmissionEntryResult> entryResults = [];

        foreach (TimeEntryId timeEntryId in orderedEntryIds)
        {
            entryStates.TryGetValue(timeEntryId, out TimeEntryState? entryState);
            await ValidateEntryAsync(
                context,
                command,
                timeEntryId,
                entryState,
                boundary,
                activityTypeCatalog,
                blocking,
                cancellationToken).ConfigureAwait(false);
        }

        if (blocking.Count > 0)
        {
            return new(
                authorization,
                RejectPeriod(blocking),
                entryResults,
                blocking)
            {
                ValidTimeEntryIds = ValidEntryIds(orderedEntryIds, blocking)
            };
        }

        SubmitTimeEntriesForApproval entryCommand = new(
            new TimeEntrySubmissionId(command.TimesheetPeriodId.Value),
            orderedEntryIds,
            TimeEntrySubmissionScope.TimesheetPeriod);

        foreach (TimeEntryId timeEntryId in orderedEntryIds)
        {
            TimeEntryState? entryState = entryStates[timeEntryId];
            if (entryState?.ApprovalState == TimeEntryApprovalState.Submitted)
            {
                continue;
            }

            TimesheetsDomainResult result = TimeEntry.Handle(
                entryCommand,
                timeEntryId,
                entryState,
                context.Actor,
                context.Tenant,
                submittedAtUtc,
                entryState!.ActivityTypeScope);

            entryResults.Add(new(
                timeEntryId,
                authorization,
                result,
                true));
        }

        return new(
            authorization,
            aggregatePrecheck,
            entryResults,
            [])
        {
            ValidTimeEntryIds = orderedEntryIds
        };
    }

    private async ValueTask ValidateEntryAsync(
        TimesheetsRequestContext context,
        SubmitTimesheetPeriod command,
        TimeEntryId timeEntryId,
        TimeEntryState? state,
        TenantLocalPeriodBoundary boundary,
        ActivityTypeCatalogReadModel catalog,
        List<TimesheetPeriodBlockingEntryGuidance> blocking,
        CancellationToken cancellationToken)
    {
        if (state?.IsRecorded != true)
        {
            blocking.Add(Guidance(timeEntryId, "timeEntryId", "missing", "Entry needs correction."));
            return;
        }

        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            CreateAuthorizationRequest(context, state),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            blocking.Add(Guidance(
                timeEntryId,
                "authorization",
                authorization.DenialCategory.ToString(),
                SafeDenialCopy(authorization)));
            return;
        }

        if (state.Contributor != command.Contributor)
        {
            blocking.Add(Guidance(timeEntryId, "contributor", "mismatch", "Entry needs correction."));
        }

        if (state.ServiceDate < boundary.LocalStartDate || state.ServiceDate > boundary.LocalEndDate)
        {
            blocking.Add(Guidance(timeEntryId, "serviceDate", "outside-period", "Entry needs correction."));
        }

        if (state.CorrectionState == TimeEntryCorrectionState.Superseded
            || state.LockState == TimeEntryLockState.SupersededLocked)
        {
            blocking.Add(Guidance(timeEntryId, "lockState", "superseded", "Entry needs correction."));
        }
        else if (state.ApprovalState is not (TimeEntryApprovalState.Draft or TimeEntryApprovalState.Submitted))
        {
            blocking.Add(Guidance(timeEntryId, "approvalState", "invalid-transition", "Entry needs correction."));
        }

        if (!TryResolveActivityTypeScope(state, catalog, timeEntryId, out TimesheetPeriodBlockingEntryGuidance? activityGuidance))
        {
            blocking.Add(activityGuidance);
        }
    }

    private static TimesheetsAuthorizationRequest CreateAuthorizationRequest(
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

    private static bool TryResolveActivityTypeScope(
        TimeEntryState state,
        ActivityTypeCatalogReadModel catalog,
        TimeEntryId timeEntryId,
        out TimesheetPeriodBlockingEntryGuidance guidance)
    {
        guidance = Guidance(timeEntryId, "activityTypeId", "unknown", "Entry needs correction.");

        if (catalog.ProjectionFreshness.State != ProjectionFreshnessState.Fresh)
        {
            guidance = Guidance(timeEntryId, "activityTypeCatalog", "not-fresh", "Projection is rebuilding.");
            return false;
        }

        ActivityTypeCatalogItem? selected = catalog.Items
            .SingleOrDefault(item => item.ActivityTypeId == state.ActivityTypeId);

        if (selected is null)
        {
            guidance = Guidance(timeEntryId, "activityTypeId", "not-found", "Entry needs correction.");
            return false;
        }

        if (!selected.IsActive || !selected.IsAvailableForCapture)
        {
            guidance = Guidance(timeEntryId, "activityTypeId", "unavailable", "Entry needs correction.");
            return false;
        }

        if (state.Target?.TargetKind == TimeEntryTargetKind.Project
            && selected.Scope == ActivityTypeScope.Project
            && selected.Project != new ProjectReference(state.Target.TargetId))
        {
            guidance = Guidance(timeEntryId, "activityTypeId", "scope-mismatch", "Entry needs correction.");
            return false;
        }

        if (state.Target?.TargetKind == TimeEntryTargetKind.Work
            && selected.Scope == ActivityTypeScope.Project)
        {
            guidance = Guidance(timeEntryId, "target", "work-project-unresolved", "Authority cannot be resolved.");
            return false;
        }

        return true;
    }

    private static TimesheetsDomainResult RejectPeriod(
        IReadOnlyList<TimesheetPeriodBlockingEntryGuidance> blocking)
        => TimesheetsDomainResult.Rejection([
            new(
                TimesheetsRejectionCode.ValidationFailed,
                "Timesheet Period submission blocked by one or more entries.",
                blocking.Select(static item => new TimesheetsFieldError(
                    $"entries[{item.TimeEntryId.Value}].{item.Field}",
                    item.Code,
                    item.Guidance)).ToArray())
        ]);

    private static List<TimeEntryId> DistinctInOrder(IEnumerable<TimeEntryId> ids)
    {
        List<TimeEntryId> ordered = [];
        HashSet<TimeEntryId> seen = [];

        foreach (TimeEntryId id in ids)
        {
            if (seen.Add(id))
            {
                ordered.Add(id);
            }
        }

        return ordered;
    }

    private static IReadOnlyList<TimeEntryId> ValidEntryIds(
        IEnumerable<TimeEntryId> processed,
        IReadOnlyList<TimesheetPeriodBlockingEntryGuidance> blocking)
    {
        HashSet<TimeEntryId> blocked = blocking
            .Select(static item => item.TimeEntryId)
            .ToHashSet();

        return processed
            .Where(id => !blocked.Contains(id))
            .ToArray();
    }

    private static TimesheetPeriodBlockingEntryGuidance Guidance(
        TimeEntryId timeEntryId,
        string field,
        string code,
        string guidance)
        => new(timeEntryId, field, code, guidance);

    private static string SafeDenialCopy(TimesheetsAuthorizationDecision authorization)
        => authorization.DenialCategory is TimesheetsDenialCategory.StaleProjection
            or TimesheetsDenialCategory.AmbiguousAuthority
            or TimesheetsDenialCategory.UnavailableSiblingAuthority
            or TimesheetsDenialCategory.UnconfiguredPolicy
                ? "Authority cannot be resolved."
                : "Access denied for this action.";
}
