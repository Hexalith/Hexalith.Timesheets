using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed class ExternalContributionCommandService
{
    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly ExternalContributionPolicyOptions _policyOptions;
    private readonly TimeEntryCommandService _recordService;
    private readonly TimeEntrySubmissionCommandService _submissionService;

    public ExternalContributionCommandService(
        TimeEntryCommandService recordService,
        TimeEntrySubmissionCommandService submissionService,
        ITimesheetsAccessGuard accessGuard,
        ExternalContributionPolicyOptions policyOptions)
    {
        ArgumentNullException.ThrowIfNull(recordService);
        ArgumentNullException.ThrowIfNull(submissionService);
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(policyOptions);

        _recordService = recordService;
        _submissionService = submissionService;
        _accessGuard = accessGuard;
        _policyOptions = policyOptions;
    }

    public async ValueTask<ExternalContributionCommandResult> SubmitAsync(
        TimesheetsRequestContext context,
        SubmitExternalTimeEntry command,
        TimeEntryState? state,
        ActivityTypeCatalogReadModel activityTypeCatalog,
        DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(activityTypeCatalog);

        RecordTimeEntry recordCommand = ToRecordCommand(command);

        TimeEntryCommandResult recordResult = state?.IsRecorded == true && state.ExternalSource == command.Source
            ? await AuthorizeNoOpRecordAsync(context, recordCommand, cancellationToken).ConfigureAwait(false)
            : await _recordService.RecordAsync(
                context,
                recordCommand,
                state,
                activityTypeCatalog,
                cancellationToken).ConfigureAwait(false);

        if (!recordResult.WasDispatched || recordResult.DomainResult?.IsRejection == true)
        {
            return new(recordResult, null);
        }

        if (_policyOptions.InitialApprovalState != TimeEntryApprovalState.Submitted)
        {
            return new(recordResult, null);
        }

        TimeEntryState submissionState = state ?? ApplyRecordedEvent(recordResult);
        TimeEntrySubmissionCommandResult submissionResult = await _submissionService.SubmitAsync(
            context,
            new SubmitTimeEntriesForApproval(
                new TimeEntrySubmissionId(command.Source.ExternalRequestId),
                [command.TimeEntryId],
                TimeEntrySubmissionScope.SelectedEntries),
            new Dictionary<TimeEntryId, TimeEntryState?> { [command.TimeEntryId] = submissionState },
            activityTypeCatalog,
            submittedAtUtc,
            cancellationToken).ConfigureAwait(false);

        return new(recordResult, submissionResult);
    }

    public async ValueTask<TimeEntryConfirmationCommandResult> ConfirmAsync(
        TimesheetsRequestContext context,
        ConfirmExternalTimeEntry command,
        TimeEntryState? state,
        DateTimeOffset confirmedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            CreateConfirmationAuthorizationRequest(context, command, state),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            return new(authorization, null, false);
        }

        TimesheetsDomainResult result = TimeEntry.Handle(
            command,
            state,
            context.Tenant,
            confirmedAtUtc);

        return new(authorization, result, true);
    }

    public async ValueTask<TimeEntryAdjustmentCommandResult> AdjustAsync(
        TimesheetsRequestContext context,
        AdjustTimeThroughMagicLink command,
        TimeEntryState? state,
        TenantReference? tenant,
        PartyReference? contributor,
        TimeEntryId? timeEntryId,
        TimeEntryTargetReference? target,
        ActivityTypeScope activityTypeScope,
        ExternalContributionSource source,
        DateTimeOffset adjustedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(source);

        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            CreateAdjustmentAuthorizationRequest(context, contributor, state),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            return new(authorization, null, false);
        }

        TimesheetsDomainResult result = TimeEntry.Handle(
            command,
            state,
            tenant,
            contributor,
            timeEntryId,
            target,
            adjustedAtUtc,
            activityTypeScope,
            source);

        return new(authorization, result, true);
    }

    private static RecordTimeEntry ToRecordCommand(SubmitExternalTimeEntry command)
        => new(
            command.TimeEntryId,
            command.Target,
            command.Contributor,
            command.ActivityTypeId,
            command.ServiceDate,
            command.DurationMinutes,
            command.BillableState,
            ContributorCategory.ExternalContributor,
            null)
        {
            Comment = command.Comment,
            ExternalSource = command.Source
        };

    private async ValueTask<TimeEntryCommandResult> AuthorizeNoOpRecordAsync(
        TimesheetsRequestContext context,
        RecordTimeEntry command,
        CancellationToken cancellationToken)
    {
        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            CreateRecordAuthorizationRequest(context, command),
            cancellationToken).ConfigureAwait(false);

        return authorization.IsAuthorized
            ? new(authorization, TimesheetsDomainResult.NoOp(), true)
            : new(authorization, null, false);
    }

    private static TimesheetsAuthorizationRequest CreateRecordAuthorizationRequest(
        TimesheetsRequestContext context,
        RecordTimeEntry command)
    {
        TimesheetsAuthorizationRequest request = new(context, TimesheetsOperation.Command)
        {
            Contributor = command.Contributor
        };

        if (command.Target.TargetKind == TimeEntryTargetKind.Project)
        {
            return request with { Project = new ProjectReference(command.Target.TargetId) };
        }

        return request with { Work = new WorkReference(command.Target.TargetId) };
    }

    private static TimesheetsAuthorizationRequest CreateConfirmationAuthorizationRequest(
        TimesheetsRequestContext context,
        ConfirmExternalTimeEntry command,
        TimeEntryState? state)
    {
        TimesheetsAuthorizationRequest request = new(context, TimesheetsOperation.Confirmation)
        {
            Contributor = command.Contributor
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

    private static TimesheetsAuthorizationRequest CreateAdjustmentAuthorizationRequest(
        TimesheetsRequestContext context,
        PartyReference? contributor,
        TimeEntryState? state)
    {
        TimesheetsAuthorizationRequest request = new(context, TimesheetsOperation.Confirmation)
        {
            Contributor = contributor
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

    private static TimeEntryState ApplyRecordedEvent(TimeEntryCommandResult result)
    {
        // Only reached after a successful record dispatch (WasDispatched, not a rejection or
        // no-op), so the dispatched events always contain exactly one TimeEntryRecorded carrying
        // the catalog-resolved activity-type scope. Reuse that event rather than fabricating state.
        TimeEntryState state = new();
        state.Apply(result.DomainResult!.Events.OfType<TimeEntryRecorded>().Single());
        return state;
    }
}
