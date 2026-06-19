using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed class TimeEntryCommandService
{
    private readonly ITimesheetsAccessGuard _accessGuard;

    public TimeEntryCommandService(ITimesheetsAccessGuard accessGuard)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        _accessGuard = accessGuard;
    }

    public async ValueTask<TimeEntryCommandResult> RecordAsync(
        TimesheetsRequestContext context,
        RecordTimeEntry command,
        TimeEntryState? state,
        ActivityTypeCatalogReadModel activityTypeCatalog,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(activityTypeCatalog);

        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            CreateAuthorizationRequest(context, command),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            return new(authorization, null);
        }

        if (!TryResolveActivityTypeScope(command, activityTypeCatalog, out ActivityTypeScope scope, out TimesheetsDomainResult? rejection))
        {
            return new(authorization, rejection);
        }

        return new(authorization, TimeEntry.Handle(command, state, scope), true);
    }

    private static TimesheetsAuthorizationRequest CreateAuthorizationRequest(
        TimesheetsRequestContext context,
        RecordTimeEntry command)
    {
        TimesheetsAuthorizationRequest request = new(context, TimesheetsOperation.Command)
        {
            Contributor = command.Contributor
        };

        if (command.Target?.TargetKind == TimeEntryTargetKind.Project)
        {
            return request with { Project = new ProjectReference(command.Target.TargetId) };
        }

        if (command.Target?.TargetKind == TimeEntryTargetKind.Work)
        {
            return request with { Work = new WorkReference(command.Target.TargetId) };
        }

        return request;
    }

    private static bool TryResolveActivityTypeScope(
        RecordTimeEntry command,
        ActivityTypeCatalogReadModel catalog,
        out ActivityTypeScope scope,
        out TimesheetsDomainResult? rejection)
    {
        scope = ActivityTypeScope.Unknown;
        rejection = null;

        if (catalog.ProjectionFreshness.State != ProjectionFreshnessState.Fresh)
        {
            rejection = Reject(
                TimesheetsRejectionCode.ProjectionUnavailable,
                "Activity Type catalog is not fresh enough for capture.",
                "activityTypeCatalog",
                "not-fresh");
            return false;
        }

        ActivityTypeCatalogItem? selected = catalog.Items
            .SingleOrDefault(item => item.ActivityTypeId == command.ActivityTypeId);

        if (selected is null)
        {
            rejection = Reject(
                TimesheetsRejectionCode.ActivityTypeNotFound,
                "Activity Type was not found for capture.",
                "activityTypeId",
                "not-found");
            return false;
        }

        if (!selected.IsActive || !selected.IsAvailableForCapture)
        {
            rejection = Reject(
                TimesheetsRejectionCode.ActivityTypeInactive,
                "Activity Type is not available for capture.",
                "activityTypeId",
                "unavailable");
            return false;
        }

        if (command.Target?.TargetKind == TimeEntryTargetKind.Project
            && selected.Scope == ActivityTypeScope.Project
            && selected.Project != new ProjectReference(command.Target.TargetId))
        {
            rejection = Reject(
                TimesheetsRejectionCode.ActivityTypeScopeMismatch,
                "Project Activity Type does not belong to the capture Project.",
                "activityTypeId",
                "scope-mismatch");
            return false;
        }

        if (command.Target?.TargetKind == TimeEntryTargetKind.Work
            && selected.Scope == ActivityTypeScope.Project)
        {
            rejection = Reject(
                TimesheetsRejectionCode.AuthorityCannotBeResolved,
                "Work Activity Type selection requires a governing Project adapter.",
                "target",
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
}
