using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Server.MagicLinks;

public sealed class MagicLinkConfirmationCapabilityCommandService
{
    private readonly ITimesheetsAccessGuard _accessGuard;
    private readonly ExternalContributionCommandService _externalContributionService;
    private readonly IMagicLinkTokenGenerator _tokenGenerator;

    public MagicLinkConfirmationCapabilityCommandService(
        ITimesheetsAccessGuard accessGuard,
        IMagicLinkTokenGenerator tokenGenerator,
        ExternalContributionCommandService externalContributionService)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        ArgumentNullException.ThrowIfNull(tokenGenerator);
        ArgumentNullException.ThrowIfNull(externalContributionService);

        _accessGuard = accessGuard;
        _tokenGenerator = tokenGenerator;
        _externalContributionService = externalContributionService;
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

    public async ValueTask<MagicLinkConfirmationUseResult> ConfirmAsync(
        TimesheetsRequestContext context,
        string oneTimeToken,
        ConfirmTimeThroughMagicLink command,
        MagicLinkCapabilityState? capabilityState,
        TimeEntryState? timeEntryState,
        DateTimeOffset confirmedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            CreateDisclosureAuthorizationRequest(context, capabilityState),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            return new(null, null);
        }

        if (string.IsNullOrWhiteSpace(oneTimeToken))
        {
            return new(InvalidLinkRejection(), null);
        }

        MagicLinkTokenHash tokenHash;
        try
        {
            tokenHash = _tokenGenerator.DeriveHash(oneTimeToken);
        }
        catch (ArgumentException)
        {
            return new(InvalidLinkRejection(), null);
        }

        TimesheetsDomainResult scopeResult = ValidateConfirmationScope(capabilityState, timeEntryState);
        if (scopeResult.IsRejection)
        {
            return new(scopeResult, null);
        }

        TimesheetsDomainResult capabilityResult = MagicLinkConfirmationCapability.HandleUse(
            command,
            capabilityState,
            context.Tenant,
            tokenHash,
            confirmedAtUtc);

        if (!capabilityResult.IsSuccess)
        {
            return new(capabilityResult, null);
        }

        TimeEntryConfirmationCommandResult timeEntryResult = await _externalContributionService.ConfirmAsync(
            context,
            new ConfirmExternalTimeEntry(
                capabilityState!.TimeEntryId!,
                capabilityState.Contributor!,
                ServerDerivedSource(capabilityState)),
            timeEntryState,
            confirmedAtUtc,
            cancellationToken).ConfigureAwait(false);

        return new(capabilityResult, timeEntryResult);
    }

    public async ValueTask<MagicLinkConfirmationUseResult> AdjustAsync(
        TimesheetsRequestContext context,
        string oneTimeToken,
        AdjustTimeThroughMagicLink command,
        MagicLinkCapabilityState? capabilityState,
        TimeEntryState? timeEntryState,
        ActivityTypeCatalogReadModel activityTypeCatalog,
        DateTimeOffset adjustedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(activityTypeCatalog);

        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            CreateDisclosureAuthorizationRequest(context, capabilityState),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            return new(null, null);
        }

        if (string.IsNullOrWhiteSpace(oneTimeToken))
        {
            return new(InvalidLinkRejection("Magic-link adjustment request was not accepted."), null);
        }

        MagicLinkTokenHash tokenHash;
        try
        {
            tokenHash = _tokenGenerator.DeriveHash(oneTimeToken);
        }
        catch (ArgumentException)
        {
            return new(InvalidLinkRejection("Magic-link adjustment request was not accepted."), null);
        }

        TimesheetsDomainResult scopeResult = ValidateAdjustmentScope(capabilityState, timeEntryState);
        if (scopeResult.IsRejection)
        {
            return new(scopeResult, null);
        }

        if (!MagicLinkConfirmationCapability.IsValidForAdjustment(capabilityState, context.Tenant, tokenHash, adjustedAtUtc))
        {
            return new(InvalidLinkRejection("Magic-link adjustment request was not accepted."), null);
        }

        if (!TryResolveActivityTypeScope(command, capabilityState!, activityTypeCatalog, out ActivityTypeScope activityTypeScope, out TimesheetsDomainResult? rejection))
        {
            return new(rejection, null);
        }

        TimeEntryAdjustmentCommandResult adjustmentResult = await _externalContributionService.AdjustAsync(
            context,
            command,
            timeEntryState,
            context.Tenant,
            capabilityState!.Contributor,
            capabilityState.TimeEntryId,
            capabilityState.Target,
            activityTypeScope,
            ServerDerivedSource(capabilityState),
            adjustedAtUtc,
            cancellationToken).ConfigureAwait(false);

        if (!adjustmentResult.WasDispatched || adjustmentResult.DomainResult?.IsSuccess != true)
        {
            return new(null, null, adjustmentResult);
        }

        TimesheetsDomainResult capabilityResult = MagicLinkConfirmationCapability.HandleUse(
            command,
            capabilityState,
            context.Tenant,
            tokenHash,
            adjustedAtUtc);

        return new(capabilityResult, null, adjustmentResult);
    }

    /// <summary>
    /// Validates a one-time token against the scoped capability and proposed Time Entry, then returns only the
    /// minimal, no-disclosure confirmation details. Every invalid, expired, used, revoked, wrong-action,
    /// wrong-scope, hash-mismatch, tenant-mismatch, or unavailable-state path returns <see langword="null"/> so
    /// callers emit the single opaque invalid-link response without revealing whether anything exists.
    /// </summary>
    /// <param name="context">The trusted request context.</param>
    /// <param name="oneTimeToken">The opaque one-time token presented by the external contributor.</param>
    /// <param name="capabilityState">The folded magic-link capability state, or <see langword="null"/>.</param>
    /// <param name="timeEntryState">The folded proposed Time Entry state, or <see langword="null"/>.</param>
    /// <param name="activityTypeCatalog">The Activity Type catalog used to resolve the display label.</param>
    /// <param name="observedAtUtc">The evaluation instant in UTC.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The safe display response, or <see langword="null"/> for any invalid-link state.</returns>
    public async ValueTask<MagicLinkConfirmationDisplayResponse?> DescribeAsync(
        TimesheetsRequestContext context,
        string oneTimeToken,
        MagicLinkCapabilityState? capabilityState,
        TimeEntryState? timeEntryState,
        ActivityTypeCatalogReadModel activityTypeCatalog,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(activityTypeCatalog);

        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            CreateDisclosureAuthorizationRequest(context, capabilityState),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized || string.IsNullOrWhiteSpace(oneTimeToken))
        {
            return null;
        }

        MagicLinkTokenHash tokenHash;
        try
        {
            tokenHash = _tokenGenerator.DeriveHash(oneTimeToken);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (ValidateConfirmationScope(capabilityState, timeEntryState).IsRejection
            || !MagicLinkConfirmationCapability.IsValidForUse(capabilityState, context.Tenant, tokenHash, observedAtUtc))
        {
            return null;
        }

        return TryResolveDisplayLabel(capabilityState!, activityTypeCatalog, out string? activityTypeLabel)
            ? new MagicLinkConfirmationDisplayResponse(
                timeEntryState!.ServiceDate,
                timeEntryState.DurationMinutes,
                "minutes",
                capabilityState!.ActivityTypeId!,
                activityTypeLabel!,
                timeEntryState.BillableState,
                capabilityState.Target!.TargetKind.ToString())
            : null;
    }

    public async ValueTask<MagicLinkAdjustmentDisplayResponse?> DescribeAdjustmentAsync(
        TimesheetsRequestContext context,
        string oneTimeToken,
        MagicLinkCapabilityState? capabilityState,
        TimeEntryState? timeEntryState,
        ActivityTypeCatalogReadModel activityTypeCatalog,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(activityTypeCatalog);

        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            CreateDisclosureAuthorizationRequest(context, capabilityState),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized || string.IsNullOrWhiteSpace(oneTimeToken))
        {
            return null;
        }

        MagicLinkTokenHash tokenHash;
        try
        {
            tokenHash = _tokenGenerator.DeriveHash(oneTimeToken);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (ValidateAdjustmentScope(capabilityState, timeEntryState).IsRejection
            || !MagicLinkConfirmationCapability.IsValidForAdjustment(capabilityState, context.Tenant, tokenHash, observedAtUtc))
        {
            return null;
        }

        return TryResolveDisplayLabel(capabilityState!, activityTypeCatalog, out string? activityTypeLabel)
            ? new MagicLinkAdjustmentDisplayResponse(
                timeEntryState!.ServiceDate,
                timeEntryState.DurationMinutes,
                "minutes",
                capabilityState!.ActivityTypeId!,
                activityTypeLabel!,
                timeEntryState.BillableState,
                capabilityState.Target!.TargetKind.ToString(),
                ["serviceDate", "durationMinutes", "activityTypeId", "billableState", "comment"],
                ["target", "contributor", "tenant", "timeEntryId", "approvalState"])
            {
                Comment = timeEntryState.Comment?.Policy.ExternalConfirmationDisplay == TimesheetsCommentPolicyDecision.Allowed
                    ? timeEntryState.Comment.Text
                    : null
            }
            : null;
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

    private static TimesheetsAuthorizationRequest CreateDisclosureAuthorizationRequest(
        TimesheetsRequestContext context,
        MagicLinkCapabilityState? state)
    {
        TimesheetsAuthorizationRequest request = new(context, TimesheetsOperation.MagicLinkDisclosure)
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

    private static TimesheetsDomainResult ValidateConfirmationScope(
        MagicLinkCapabilityState? capabilityState,
        TimeEntryState? timeEntryState)
    {
        if (capabilityState?.Exists != true || timeEntryState?.IsRecorded != true)
        {
            return InvalidLinkRejection();
        }

        return timeEntryState.TimeEntryId == capabilityState.TimeEntryId
            && timeEntryState.Contributor == capabilityState.Contributor
            && timeEntryState.Target == capabilityState.Target
            ? TimesheetsDomainResult.NoOp()
            : InvalidLinkRejection();
    }

    private static TimesheetsDomainResult ValidateAdjustmentScope(
        MagicLinkCapabilityState? capabilityState,
        TimeEntryState? timeEntryState)
    {
        if (capabilityState?.Exists != true || timeEntryState?.IsRecorded != true)
        {
            return InvalidLinkRejection("Magic-link adjustment request was not accepted.");
        }

        return timeEntryState.TimeEntryId == capabilityState.TimeEntryId
            && timeEntryState.Contributor == capabilityState.Contributor
            && timeEntryState.Target == capabilityState.Target
            && timeEntryState.ApprovalState == TimeEntryApprovalState.Draft
            && timeEntryState.ContributorCategory == ContributorCategory.ExternalContributor
            && !timeEntryState.IsLockedFromDirectEdit
            && timeEntryState.CorrectionState == TimeEntryCorrectionState.None
            ? TimesheetsDomainResult.NoOp()
            : InvalidLinkRejection("Magic-link adjustment request was not accepted.");
    }

    private static ExternalContributionSource ServerDerivedSource(MagicLinkCapabilityState state)
        => new("magic-link", state.CapabilityId!.Value);

    private static bool TryResolveDisplayLabel(
        MagicLinkCapabilityState state,
        ActivityTypeCatalogReadModel catalog,
        out string? label)
    {
        label = null;

        if (catalog.ProjectionFreshness.State != ProjectionFreshnessState.Fresh)
        {
            return false;
        }

        ActivityTypeCatalogItem[] matches = catalog.Items
            .Where(item => item.ActivityTypeId == state.ActivityTypeId)
            .Take(2)
            .ToArray();

        if (matches.Length != 1 || !matches[0].IsActive || !matches[0].IsAvailableForCapture)
        {
            return false;
        }

        label = matches[0].Label;
        return true;
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

    private static bool TryResolveActivityTypeScope(
        AdjustTimeThroughMagicLink command,
        MagicLinkCapabilityState capabilityState,
        ActivityTypeCatalogReadModel catalog,
        out ActivityTypeScope activityTypeScope,
        out TimesheetsDomainResult? rejection)
    {
        activityTypeScope = ActivityTypeScope.Unknown;
        rejection = null;

        if (capabilityState.Target is null || capabilityState.Target.TargetKind == TimeEntryTargetKind.Unknown)
        {
            rejection = InvalidLinkRejection("Magic-link adjustment request was not accepted.");
            return false;
        }

        if (catalog.ProjectionFreshness.State != ProjectionFreshnessState.Fresh)
        {
            rejection = Reject(
                TimesheetsRejectionCode.ProjectionUnavailable,
                "Activity Type catalog is not fresh enough for magic-link adjustment.",
                "activityTypeCatalog",
                "not-fresh");
            return false;
        }

        ActivityTypeCatalogItem[] matches = catalog.Items
            .Where(item => item.ActivityTypeId == command.ActivityTypeId)
            .Take(2)
            .ToArray();
        ActivityTypeCatalogItem? selected = matches.Length == 1 ? matches[0] : null;

        if (selected is null)
        {
            rejection = Reject(
                matches.Length > 1 ? TimesheetsRejectionCode.AuthorityCannotBeResolved : TimesheetsRejectionCode.ActivityTypeNotFound,
                matches.Length > 1
                    ? "Activity Type catalog returned ambiguous data for magic-link adjustment."
                    : "Activity Type was not found for magic-link adjustment.",
                "activityTypeId",
                matches.Length > 1 ? "ambiguous" : "not-found");
            return false;
        }

        if (!selected.IsActive || !selected.IsAvailableForCapture)
        {
            rejection = Reject(
                TimesheetsRejectionCode.ActivityTypeInactive,
                "Activity Type is not available for magic-link adjustment.",
                "activityTypeId",
                "unavailable");
            return false;
        }

        if (capabilityState.Target.TargetKind == TimeEntryTargetKind.Project
            && selected.Scope == ActivityTypeScope.Project
            && selected.Project != new ProjectReference(capabilityState.Target.TargetId))
        {
            rejection = Reject(
                TimesheetsRejectionCode.ActivityTypeScopeMismatch,
                "Project Activity Type does not belong to the magic-link target Project.",
                "activityTypeId",
                "scope-mismatch");
            return false;
        }

        if (capabilityState.Target.TargetKind == TimeEntryTargetKind.Work
            && selected.Scope == ActivityTypeScope.Project)
        {
            rejection = Reject(
                TimesheetsRejectionCode.AuthorityCannotBeResolved,
                "Work Activity Type selection requires a governing Project adapter.",
                "target",
                "work-project-unresolved");
            return false;
        }

        activityTypeScope = selected.Scope;
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

    private static TimesheetsDomainResult InvalidLinkRejection()
        => InvalidLinkRejection("Magic-link confirmation request was not accepted.");

    private static TimesheetsDomainResult InvalidLinkRejection(string message)
        => Reject(
            TimesheetsRejectionCode.ValidationFailed,
            message,
            "capability",
            "invalid-link");
}
