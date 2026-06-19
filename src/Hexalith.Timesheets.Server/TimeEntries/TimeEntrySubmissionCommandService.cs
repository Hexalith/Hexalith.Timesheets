using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed class TimeEntrySubmissionCommandService
{
    private readonly ITimesheetsAccessGuard _accessGuard;

    public TimeEntrySubmissionCommandService(ITimesheetsAccessGuard accessGuard)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        _accessGuard = accessGuard;
    }

    public async ValueTask<TimeEntrySubmissionCommandResult> SubmitAsync(
        TimesheetsRequestContext context,
        SubmitTimeEntriesForApproval command,
        IReadOnlyDictionary<TimeEntryId, TimeEntryState?> states,
        ActivityTypeCatalogReadModel activityTypeCatalog,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(activityTypeCatalog);

        List<TimeEntrySubmissionEntryResult> results = [];
        HashSet<TimeEntryId> processed = [];

        foreach (TimeEntryId timeEntryId in command.TimeEntryIds)
        {
            // A single submission command must produce at most one transition per entry.
            // Skip repeated identifiers so a duplicated id cannot emit two TimeEntrySubmitted events.
            if (!processed.Add(timeEntryId))
            {
                continue;
            }

            states.TryGetValue(timeEntryId, out TimeEntryState? state);
            results.Add(await SubmitOneAsync(
                context,
                command,
                timeEntryId,
                state,
                activityTypeCatalog,
                submittedAtUtc,
                cancellationToken).ConfigureAwait(false));
        }

        return new(results);
    }

    private async ValueTask<TimeEntrySubmissionEntryResult> SubmitOneAsync(
        TimesheetsRequestContext context,
        SubmitTimeEntriesForApproval command,
        TimeEntryId timeEntryId,
        TimeEntryState? state,
        ActivityTypeCatalogReadModel activityTypeCatalog,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken)
    {
        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            CreateAuthorizationRequest(context, state),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            return new(timeEntryId, authorization, null, false);
        }

        if (!TryResolveActivityTypeScope(state, activityTypeCatalog, timeEntryId, out ActivityTypeScope scope, out TimesheetsDomainResult? rejection))
        {
            return new(timeEntryId, authorization, rejection, false);
        }

        TimesheetsDomainResult result = TimeEntry.Handle(
            command,
            timeEntryId,
            state,
            context.Actor,
            context.Tenant,
            submittedAtUtc,
            scope);

        return new(timeEntryId, authorization, result, true);
    }

    private static TimesheetsAuthorizationRequest CreateAuthorizationRequest(
        TimesheetsRequestContext context,
        TimeEntryState? state)
    {
        TimesheetsAuthorizationRequest request = new(context, TimesheetsOperation.Command)
        {
            Contributor = state?.Contributor
        };

        if (state?.Target?.TargetKind == TimeEntryTargetKind.Project)
        {
            return request with { Project = new ProjectReference(state.Target.TargetId) };
        }

        if (state?.Target?.TargetKind == TimeEntryTargetKind.Work)
        {
            return request with { Work = new WorkReference(state.Target.TargetId) };
        }

        return request;
    }

    private static bool TryResolveActivityTypeScope(
        TimeEntryState? state,
        ActivityTypeCatalogReadModel catalog,
        TimeEntryId timeEntryId,
        out ActivityTypeScope scope,
        out TimesheetsDomainResult? rejection)
    {
        scope = ActivityTypeScope.Unknown;
        rejection = null;

        if (state?.IsRecorded != true || state.ActivityTypeId is null)
        {
            return true;
        }

        if (catalog.ProjectionFreshness.State != ProjectionFreshnessState.Fresh)
        {
            rejection = Reject(
                TimesheetsRejectionCode.ProjectionUnavailable,
                "Activity Type catalog is not fresh enough for submission.",
                EntryField(timeEntryId, "activityTypeCatalog"),
                "not-fresh");
            return false;
        }

        ActivityTypeCatalogItem? selected = catalog.Items
            .SingleOrDefault(item => item.ActivityTypeId == state.ActivityTypeId);

        if (selected is null)
        {
            rejection = Reject(
                TimesheetsRejectionCode.ActivityTypeNotFound,
                "Activity Type was not found for submission.",
                EntryField(timeEntryId, "activityTypeId"),
                "not-found");
            return false;
        }

        if (!selected.IsActive || !selected.IsAvailableForCapture)
        {
            rejection = Reject(
                TimesheetsRejectionCode.ActivityTypeInactive,
                "Activity Type is not available for submission.",
                EntryField(timeEntryId, "activityTypeId"),
                "unavailable");
            return false;
        }

        if (state.Target?.TargetKind == TimeEntryTargetKind.Project
            && selected.Scope == ActivityTypeScope.Project
            && selected.Project != new ProjectReference(state.Target.TargetId))
        {
            rejection = Reject(
                TimesheetsRejectionCode.ActivityTypeScopeMismatch,
                "Project Activity Type does not belong to the submission Project.",
                EntryField(timeEntryId, "activityTypeId"),
                "scope-mismatch");
            return false;
        }

        if (state.Target?.TargetKind == TimeEntryTargetKind.Work
            && selected.Scope == ActivityTypeScope.Project)
        {
            rejection = Reject(
                TimesheetsRejectionCode.AuthorityCannotBeResolved,
                "Work Activity Type submission requires a governing Project adapter.",
                EntryField(timeEntryId, "target"),
                "work-project-unresolved");
            return false;
        }

        scope = selected.Scope;
        return true;
    }

    private static TimesheetsDomainResult Reject(
        TimesheetsRejectionCode code,
        string message,
        string field,
        string fieldCode)
        => TimesheetsDomainResult.Rejection([
            new(
                code,
                message,
                [
                    new(field, fieldCode, message)
                ])
        ]);

    private static string EntryField(TimeEntryId timeEntryId, string field)
        => $"entries[{timeEntryId.Value}].{field}";
}
