using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.MagicLinks;

public sealed class MagicLinkConfirmationCapabilityCommandService
{
    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly IMagicLinkTokenGenerator _tokenGenerator;

    public MagicLinkConfirmationCapabilityCommandService(
        ITimesheetsAccessGuard accessGuard,
        IMagicLinkTokenGenerator tokenGenerator)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(tokenGenerator);

        _accessGuard = accessGuard;
        _tokenGenerator = tokenGenerator;
    }

    public async ValueTask<MagicLinkCapabilityCommandResult> IssueAsync(
        TimesheetsRequestContext context,
        IssueMagicLinkConfirmationCapability command,
        MagicLinkCapabilityState? state,
        ActivityTypeCatalogReadModel activityTypeCatalog,
        DateTimeOffset issuedAtUtc,
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

        if (!TryResolveActivityTypeScope(command, activityTypeCatalog, out TimesheetsDomainResult? rejection))
        {
            return new(authorization, rejection);
        }

        if (state?.Exists == true)
        {
            TimesheetsDomainResult duplicate = MagicLinkConfirmationCapability.HandleIssue(
                command,
                state,
                context.Tenant,
                context.Actor,
                new MagicLinkTokenHash("duplicate-state-placeholder"),
                issuedAtUtc);
            return new(authorization, duplicate);
        }

        MagicLinkTokenMaterial token = _tokenGenerator.Generate();
        TimesheetsDomainResult result = MagicLinkConfirmationCapability.HandleIssue(
            command,
            state,
            context.Tenant,
            context.Actor,
            token.TokenHash,
            issuedAtUtc);

        MagicLinkIssueResponse? response = result.IsSuccess
            ? new MagicLinkIssueResponse(command.CapabilityId, token.OneTimeToken, command.ExpiresAtUtc.ToUniversalTime())
            : null;

        return new(authorization, result, response);
    }

    public async ValueTask<MagicLinkCapabilityCommandResult> RevokeAsync(
        TimesheetsRequestContext context,
        RevokeMagicLinkConfirmationCapability command,
        MagicLinkCapabilityState? state,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            CreateManagementAuthorizationRequest(context, state),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            return new(authorization, null);
        }

        return new(
            authorization,
            MagicLinkConfirmationCapability.HandleRevoke(command, state, context.Tenant, context.Actor, revokedAtUtc));
    }

    public TimesheetsDomainResult Expire(
        ExpireMagicLinkConfirmationCapability command,
        MagicLinkCapabilityState? state,
        TimesheetsRequestContext context,
        DateTimeOffset expiredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);

        return MagicLinkConfirmationCapability.HandleExpire(command, state, context.Tenant, expiredAtUtc);
    }

    private static TimesheetsAuthorizationRequest CreateAuthorizationRequest(
        TimesheetsRequestContext context,
        IssueMagicLinkConfirmationCapability command)
    {
        TimesheetsAuthorizationRequest request = new(context, TimesheetsOperation.MagicLinkIssuance)
        {
            Contributor = command.Scope?.Contributor
        };

        if (command.Scope?.Target?.TargetKind == TimeEntryTargetKind.Project)
        {
            return request with { Project = new ProjectReference(command.Scope.Target.TargetId) };
        }

        if (command.Scope?.Target?.TargetKind == TimeEntryTargetKind.Work)
        {
            return request with { Work = new WorkReference(command.Scope.Target.TargetId) };
        }

        return request;
    }

    private static TimesheetsAuthorizationRequest CreateManagementAuthorizationRequest(
        TimesheetsRequestContext context,
        MagicLinkCapabilityState? state)
    {
        TimesheetsAuthorizationRequest request = new(context, TimesheetsOperation.MagicLinkIssuance)
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
        IssueMagicLinkConfirmationCapability command,
        ActivityTypeCatalogReadModel catalog,
        out TimesheetsDomainResult? rejection)
    {
        rejection = null;

        if (command.Scope is null)
        {
            rejection = Reject(
                TimesheetsRejectionCode.ValidationFailed,
                "Magic-link scope is required.",
                "scope",
                "required");
            return false;
        }

        if (command.Scope.Target is null || command.Scope.Target.TargetKind == TimeEntryTargetKind.Unknown)
        {
            rejection = Reject(
                TimesheetsRejectionCode.ValidationFailed,
                "Magic-link target is required.",
                "target",
                "required");
            return false;
        }

        if (catalog.ProjectionFreshness.State != ProjectionFreshnessState.Fresh)
        {
            rejection = Reject(
                TimesheetsRejectionCode.ProjectionUnavailable,
                "Activity Type catalog is not fresh enough for magic-link issuance.",
                "activityTypeCatalog",
                "not-fresh");
            return false;
        }

        ActivityTypeCatalogItem[] matches = catalog.Items
            .Where(item => item.ActivityTypeId == command.Scope.ActivityTypeId)
            .Take(2)
            .ToArray();
        ActivityTypeCatalogItem? selected = matches.Length == 1 ? matches[0] : null;

        if (selected is null)
        {
            if (matches.Length > 1)
            {
                rejection = Reject(
                    TimesheetsRejectionCode.AuthorityCannotBeResolved,
                    "Activity Type catalog returned ambiguous data for magic-link issuance.",
                    "activityTypeId",
                    "ambiguous");
                return false;
            }

            rejection = Reject(
                TimesheetsRejectionCode.ActivityTypeNotFound,
                "Activity Type was not found for magic-link issuance.",
                "activityTypeId",
                "not-found");
            return false;
        }

        if (!selected.IsActive || !selected.IsAvailableForCapture)
        {
            rejection = Reject(
                TimesheetsRejectionCode.ActivityTypeInactive,
                "Activity Type is not available for magic-link issuance.",
                "activityTypeId",
                "unavailable");
            return false;
        }

        if (command.Scope.Target.TargetKind == TimeEntryTargetKind.Project
            && selected.Scope == ActivityTypeScope.Project
            && selected.Project != new ProjectReference(command.Scope.Target.TargetId))
        {
            rejection = Reject(
                TimesheetsRejectionCode.ActivityTypeScopeMismatch,
                "Project Activity Type does not belong to the magic-link target Project.",
                "activityTypeId",
                "scope-mismatch");
            return false;
        }

        if (command.Scope.Target.TargetKind == TimeEntryTargetKind.Work
            && selected.Scope == ActivityTypeScope.Project)
        {
            rejection = Reject(
                TimesheetsRejectionCode.AuthorityCannotBeResolved,
                "Work Activity Type selection requires a governing Project adapter.",
                "target",
                "work-project-unresolved");
            return false;
        }

        return true;
    }

    private static TimesheetsDomainResult Reject(
        TimesheetsRejectionCode code,
        string message,
        string field,
        string fieldCode)
        => TimesheetsDomainResult.Rejection([
            new(code, message, [new TimesheetsFieldError(field, fieldCode, message)])
        ]);
}
