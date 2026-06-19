using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

/// <summary>
/// Composes the fail-closed gates for correcting rejected or approved Time Entries.
/// <para>
/// Correction-policy boundary: correction permission is resolved through the existing
/// <see cref="ITimesheetsApprovalAuthorityResolver"/> using
/// <see cref="ApprovalAuthorityAction.CorrectionAuthorization"/>. Correction is therefore
/// authority-provider resolved, NOT contributor-self-owned: the resolver's self-approval
/// shortcut deliberately applies only to entry/period approval, so a contributor correcting
/// their own entry must still obtain provider/policy authority and fails closed when
/// none is available. No ad hoc role checks are performed here. Both the current entry
/// context and the corrected target/contributor context are authorized before aggregate dispatch
/// so cross-tenant, stale, ambiguous, unavailable, or invalid references fail closed with safe
/// denial copy.
/// </para>
/// </summary>
public sealed class TimeEntryCorrectionCommandService
{
    private const string AccessDenied = "Access denied for this action.";
    private const string AuthorityUnresolved = "Authority cannot be resolved.";

    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly ITimesheetsApprovalAuthorityResolver _authorityResolver;

    public TimeEntryCorrectionCommandService(
        ITimesheetsAccessGuard accessGuard,
        ITimesheetsApprovalAuthorityResolver authorityResolver)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(authorityResolver);

        _accessGuard = accessGuard;
        _authorityResolver = authorityResolver;
    }

    public async ValueTask<TimeEntryCorrectionCommandResult> CorrectAsync(
        TimesheetsRequestContext context,
        CorrectRejectedTimeEntry command,
        TimeEntryState? state,
        ActivityTypeCatalogReadModel activityTypeCatalog,
        DateTimeOffset correctedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(activityTypeCatalog);

        TimesheetsAuthorizationRequest currentAuthorizationRequest = CreateCurrentAuthorizationRequest(context, state);
        TimesheetsAuthorizationDecision currentAuthorization = await _accessGuard
            .AuthorizeAsync(currentAuthorizationRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!currentAuthorization.IsAuthorized)
        {
            return new(command.TimeEntryId, SafeAuthorization(currentAuthorization), null, null, null, false);
        }

        TimesheetsAuthorizationRequest correctedAuthorizationRequest = CreateCorrectedAuthorizationRequest(context, command);
        TimesheetsAuthorizationDecision correctedAuthorization = await _accessGuard
            .AuthorizeAsync(correctedAuthorizationRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!correctedAuthorization.IsAuthorized)
        {
            return new(
                command.TimeEntryId,
                currentAuthorization,
                SafeAuthorization(correctedAuthorization),
                null,
                null,
                false);
        }

        ApprovalAuthorityResolutionResult authority = await _authorityResolver
            .ResolveAsync(
                new ApprovalAuthorityResolutionRequest(
                    currentAuthorizationRequest with { ApprovalAction = ApprovalAuthorityAction.CorrectionAuthorization },
                    ApprovalAuthorityAction.CorrectionAuthorization,
                    state?.Contributor),
                cancellationToken)
            .ConfigureAwait(false);

        if (!authority.IsAllowed)
        {
            return new(
                command.TimeEntryId,
                currentAuthorization,
                correctedAuthorization,
                SafeAuthority(authority),
                null,
                false);
        }

        if (!TryResolveActivityTypeScope(command, activityTypeCatalog, out ActivityTypeScope scope, out TimesheetsDomainResult? rejection))
        {
            return new(
                command.TimeEntryId,
                currentAuthorization,
                correctedAuthorization,
                authority,
                rejection,
                false);
        }

        TimesheetsDomainResult result = TimeEntry.Handle(
            command,
            command.TimeEntryId,
            state,
            context.Actor,
            context.Tenant,
            correctedAtUtc,
            scope);

        return new(
            command.TimeEntryId,
            currentAuthorization,
            correctedAuthorization,
            authority,
            result,
            true);
    }

    public async ValueTask<TimeEntryCorrectionCommandResult> CorrectAsync(
        TimesheetsRequestContext context,
        CorrectApprovedTimeEntry command,
        TimeEntryState? state,
        ActivityTypeCatalogReadModel activityTypeCatalog,
        DateTimeOffset correctedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(activityTypeCatalog);

        TimesheetsAuthorizationRequest currentAuthorizationRequest = CreateCurrentAuthorizationRequest(context, state);
        TimesheetsAuthorizationDecision currentAuthorization = await _accessGuard
            .AuthorizeAsync(currentAuthorizationRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!currentAuthorization.IsAuthorized)
        {
            return new(command.TimeEntryId, SafeAuthorization(currentAuthorization), null, null, null, false);
        }

        TimesheetsAuthorizationRequest correctedAuthorizationRequest = CreateCorrectedAuthorizationRequest(context, command);
        TimesheetsAuthorizationDecision correctedAuthorization = await _accessGuard
            .AuthorizeAsync(correctedAuthorizationRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!correctedAuthorization.IsAuthorized)
        {
            return new(
                command.TimeEntryId,
                currentAuthorization,
                SafeAuthorization(correctedAuthorization),
                null,
                null,
                false);
        }

        ApprovalAuthorityResolutionResult authority = await _authorityResolver
            .ResolveAsync(
                new ApprovalAuthorityResolutionRequest(
                    currentAuthorizationRequest with { ApprovalAction = ApprovalAuthorityAction.CorrectionAuthorization },
                    ApprovalAuthorityAction.CorrectionAuthorization,
                    state?.Contributor),
                cancellationToken)
            .ConfigureAwait(false);

        if (!authority.IsAllowed)
        {
            return new(
                command.TimeEntryId,
                currentAuthorization,
                correctedAuthorization,
                SafeAuthority(authority),
                null,
                false);
        }

        if (!TryResolveActivityTypeScope(command, activityTypeCatalog, out ActivityTypeScope scope, out TimesheetsDomainResult? rejection))
        {
            return new(
                command.TimeEntryId,
                currentAuthorization,
                correctedAuthorization,
                authority,
                rejection,
                false);
        }

        TimesheetsDomainResult result = TimeEntry.Handle(
            command,
            command.TimeEntryId,
            state,
            context.Actor,
            context.Tenant,
            correctedAtUtc,
            scope);

        return new(
            command.TimeEntryId,
            currentAuthorization,
            correctedAuthorization,
            authority,
            result,
            true);
    }

    private static TimesheetsAuthorizationRequest CreateCurrentAuthorizationRequest(
        TimesheetsRequestContext context,
        TimeEntryState? state)
        => AddTarget(new(context, TimesheetsOperation.Command)
        {
            Contributor = state?.Contributor
        }, state?.Target);

    private static TimesheetsAuthorizationRequest CreateCorrectedAuthorizationRequest(
        TimesheetsRequestContext context,
        CorrectRejectedTimeEntry command)
        => AddTarget(new(context, TimesheetsOperation.Command)
        {
            Contributor = command.Contributor
        }, command.Target);

    private static TimesheetsAuthorizationRequest CreateCorrectedAuthorizationRequest(
        TimesheetsRequestContext context,
        CorrectApprovedTimeEntry command)
        => AddTarget(new(context, TimesheetsOperation.Command)
        {
            Contributor = command.Contributor
        }, command.Target);

    private static TimesheetsAuthorizationRequest AddTarget(
        TimesheetsAuthorizationRequest request,
        TimeEntryTargetReference? target)
    {
        if (target?.TargetKind == TimeEntryTargetKind.Project)
        {
            return request with { Project = new ProjectReference(target.TargetId) };
        }

        if (target?.TargetKind == TimeEntryTargetKind.Work)
        {
            return request with { Work = new WorkReference(target.TargetId) };
        }

        return request;
    }

    private static bool TryResolveActivityTypeScope(
        CorrectRejectedTimeEntry command,
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
                "Activity Type catalog is not fresh enough for correction.",
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
                "Activity Type was not found for correction.",
                "activityTypeId",
                "not-found");
            return false;
        }

        if (!selected.IsActive || !selected.IsAvailableForCapture)
        {
            rejection = Reject(
                TimesheetsRejectionCode.ActivityTypeInactive,
                "Activity Type is not available for correction.",
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
                "Project Activity Type does not belong to the correction Project.",
                "activityTypeId",
                "scope-mismatch");
            return false;
        }

        if (command.Target?.TargetKind == TimeEntryTargetKind.Work
            && selected.Scope == ActivityTypeScope.Project)
        {
            rejection = Reject(
                TimesheetsRejectionCode.AuthorityCannotBeResolved,
                "Work Activity Type correction requires a governing Project adapter.",
                "target",
                "work-project-unresolved");
            return false;
        }

        scope = selected.Scope;
        return true;
    }

    private static bool TryResolveActivityTypeScope(
        CorrectApprovedTimeEntry command,
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
                "Activity Type catalog is not fresh enough for correction.",
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
                "Activity Type was not found for correction.",
                "activityTypeId",
                "not-found");
            return false;
        }

        if (!selected.IsActive || !selected.IsAvailableForCapture)
        {
            rejection = Reject(
                TimesheetsRejectionCode.ActivityTypeInactive,
                "Activity Type is not available for correction.",
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
                "Project Activity Type does not belong to the correction Project.",
                "activityTypeId",
                "scope-mismatch");
            return false;
        }

        if (command.Target?.TargetKind == TimeEntryTargetKind.Work
            && selected.Scope == ActivityTypeScope.Project)
        {
            rejection = Reject(
                TimesheetsRejectionCode.AuthorityCannotBeResolved,
                "Work Activity Type correction requires a governing Project adapter.",
                "target",
                "work-project-unresolved");
            return false;
        }

        scope = selected.Scope;
        return true;
    }

    private static TimesheetsAuthorizationDecision SafeAuthorization(TimesheetsAuthorizationDecision decision)
        => TimesheetsAuthorizationDecision.Denied(
            decision.DenialCategory,
            SafeReason(decision.DenialCategory));

    private static ApprovalAuthorityResolutionResult SafeAuthority(ApprovalAuthorityResolutionResult authority)
        => ApprovalAuthorityResolutionResult.Denied(
            authority.DenialCategory,
            SafeReason(authority.DenialCategory),
            authority.SourceAttribution);

    private static string SafeReason(TimesheetsDenialCategory category)
        => category is TimesheetsDenialCategory.NonMember
            or TimesheetsDenialCategory.InsufficientRole
            ? AccessDenied
            : AuthorityUnresolved;

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
