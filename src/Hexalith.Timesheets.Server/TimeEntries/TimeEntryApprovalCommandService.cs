using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed class TimeEntryApprovalCommandService
{
    private const string AccessDenied = "Access denied for this action.";
    private const string AuthorityUnresolved = "Authority cannot be resolved.";

    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly ITimesheetsApprovalAuthorityResolver _authorityResolver;

    public TimeEntryApprovalCommandService(
        ITimesheetsAccessGuard accessGuard,
        ITimesheetsApprovalAuthorityResolver authorityResolver)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(authorityResolver);

        _accessGuard = accessGuard;
        _authorityResolver = authorityResolver;
    }

    public async ValueTask<TimeEntryApprovalCommandResult> ApproveAsync(
        TimesheetsRequestContext context,
        ApproveTimeEntry command,
        TimeEntryState? state,
        DateTimeOffset decidedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        TimesheetsAuthorizationRequest authorizationRequest = CreateAuthorizationRequest(context, state);
        TimesheetsAuthorizationDecision authorization = await _accessGuard
            .AuthorizeAsync(authorizationRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            return new(command.TimeEntryId, SafeAuthorization(authorization), null, null, false);
        }

        ApprovalAuthorityResolutionResult authority = await ResolveAuthorityAsync(
            authorizationRequest,
            ApprovalAuthorityAction.EntryApproval,
            state?.Contributor,
            cancellationToken).ConfigureAwait(false);

        if (!authority.IsAllowed)
        {
            return new(command.TimeEntryId, authorization, SafeAuthority(authority), null, false);
        }

        TimesheetsDomainResult domainResult = TimeEntry.Handle(
            command,
            command.TimeEntryId,
            state,
            context.Actor,
            context.Tenant,
            decidedAtUtc,
            authority.SourceAttribution,
            TimeEntryApprovalScope.IndividualEntry);

        return new(command.TimeEntryId, authorization, authority, domainResult, true);
    }

    public async ValueTask<TimeEntryApprovalCommandResult> RejectAsync(
        TimesheetsRequestContext context,
        RejectTimeEntry command,
        TimeEntryState? state,
        DateTimeOffset decidedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        TimesheetsAuthorizationRequest authorizationRequest = CreateAuthorizationRequest(context, state);
        TimesheetsAuthorizationDecision authorization = await _accessGuard
            .AuthorizeAsync(authorizationRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            return new(command.TimeEntryId, SafeAuthorization(authorization), null, null, false);
        }

        ApprovalAuthorityResolutionResult authority = await ResolveAuthorityAsync(
            authorizationRequest,
            ApprovalAuthorityAction.EntryRejection,
            state?.Contributor,
            cancellationToken).ConfigureAwait(false);

        if (!authority.IsAllowed)
        {
            return new(command.TimeEntryId, authorization, SafeAuthority(authority), null, false);
        }

        TimesheetsDomainResult domainResult = TimeEntry.Handle(
            command,
            command.TimeEntryId,
            state,
            context.Actor,
            context.Tenant,
            decidedAtUtc,
            authority.SourceAttribution,
            TimeEntryApprovalScope.IndividualEntry);

        return new(command.TimeEntryId, authorization, authority, domainResult, true);
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

    private static string SafeReason(TimesheetsDenialCategory category)
    {
        return category is TimesheetsDenialCategory.NonMember
            or TimesheetsDenialCategory.InsufficientRole
            ? AccessDenied
            : AuthorityUnresolved;
    }
}
