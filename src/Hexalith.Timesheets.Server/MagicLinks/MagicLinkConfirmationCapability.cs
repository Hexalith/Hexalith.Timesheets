using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;

namespace Hexalith.Timesheets.Server.MagicLinks;

public static class MagicLinkConfirmationCapability
{
    public static TimesheetsDomainResult HandleIssue(
        IssueMagicLinkConfirmationCapability command,
        MagicLinkCapabilityState? state,
        TenantReference? tenant,
        PartyReference? issuer,
        MagicLinkTokenHash tokenHash,
        DateTimeOffset issuedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tokenHash);

        List<TimesheetsFieldError> errors = [];
        ValidateIssue(command, tenant, issuer, tokenHash, issuedAtUtc, errors);

        if (state?.Exists == true)
        {
            errors.Add(new("capabilityId", "duplicate", "Magic-link capability already exists."));
        }

        if (errors.Count > 0)
        {
            return Reject("Magic-link capability issuance failed validation.", errors);
        }

        return TimesheetsDomainResult.Success([
            new MagicLinkConfirmationCapabilityIssued(
                command.CapabilityId,
                tenant!,
                command.Scope.Contributor,
                command.Scope.Target,
                command.Scope.ActivityTypeId,
                command.Scope.TimeEntryId,
                command.Scope.TargetKind,
                command.AllowedAction,
                tokenHash,
                command.ExpiresAtUtc.ToUniversalTime(),
                issuer!,
                issuedAtUtc.ToUniversalTime(),
                command.Source,
                true)
        ]);
    }

    public static TimesheetsDomainResult HandleRevoke(
        RevokeMagicLinkConfirmationCapability command,
        MagicLinkCapabilityState? state,
        TenantReference? tenant,
        PartyReference? revokedBy,
        DateTimeOffset revokedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(command);

        List<TimesheetsFieldError> errors = [];
        ValidateTransition(command.CapabilityId, state, tenant, revokedBy, revokedAtUtc, true, errors);

        if (errors.Count > 0)
        {
            return Reject("Magic-link capability revocation failed validation.", errors);
        }

        return TimesheetsDomainResult.Success([
            new MagicLinkConfirmationCapabilityRevoked(
                command.CapabilityId,
                tenant!,
                revokedBy!,
                revokedAtUtc.ToUniversalTime(),
                command.Source)
        ]);
    }

    public static TimesheetsDomainResult HandleExpire(
        ExpireMagicLinkConfirmationCapability command,
        MagicLinkCapabilityState? state,
        TenantReference? tenant,
        DateTimeOffset expiredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(command);

        List<TimesheetsFieldError> errors = [];
        ValidateTransition(command.CapabilityId, state, tenant, null, expiredAtUtc, false, errors);

        if (state?.Exists == true && expiredAtUtc.ToUniversalTime() < state.ExpiresAtUtc)
        {
            errors.Add(new("expiresAtUtc", "not-expired", "Magic-link capability has not reached its expiry instant."));
        }

        if (errors.Count > 0)
        {
            return Reject("Magic-link capability expiry failed validation.", errors);
        }

        return TimesheetsDomainResult.Success([
            new MagicLinkConfirmationCapabilityExpired(
                command.CapabilityId,
                tenant!,
                expiredAtUtc.ToUniversalTime(),
                command.Source)
        ]);
    }

    public static TimesheetsDomainResult HandleUse(
        ConfirmTimeThroughMagicLink command,
        MagicLinkCapabilityState? state,
        TenantReference? tenant,
        MagicLinkTokenHash tokenHash,
        DateTimeOffset usedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tokenHash);

        List<TimesheetsFieldError> errors = [];
        ValidateUse(
            state,
            tenant,
            tokenHash,
            usedAtUtc,
            static action => action is MagicLinkAllowedAction.Confirm or MagicLinkAllowedAction.ConfirmOrAdjust,
            errors);

        if (errors.Count > 0)
        {
            return Reject(MagicLinkInvalidLinkDenial.Default.Title, errors);
        }

        return TimesheetsDomainResult.Success([
            new MagicLinkConfirmationCapabilityUsed(
                state!.CapabilityId!,
                tenant!,
                state.Contributor!,
                state.TimeEntryId!,
                usedAtUtc.ToUniversalTime(),
                ServerDerivedSource(state))
            {
                OutcomeCategory = "confirmed"
            }
        ]);
    }

    public static TimesheetsDomainResult HandleUse(
        AdjustTimeThroughMagicLink command,
        MagicLinkCapabilityState? state,
        TenantReference? tenant,
        MagicLinkTokenHash tokenHash,
        DateTimeOffset usedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(tokenHash);

        List<TimesheetsFieldError> errors = [];
        ValidateUse(
            state,
            tenant,
            tokenHash,
            usedAtUtc,
            static action => action is MagicLinkAllowedAction.Adjust or MagicLinkAllowedAction.ConfirmOrAdjust,
            errors);

        if (errors.Count > 0)
        {
            return Reject(MagicLinkInvalidLinkDenial.Default.Title, errors);
        }

        return TimesheetsDomainResult.Success([
            new MagicLinkConfirmationCapabilityUsed(
                state!.CapabilityId!,
                tenant!,
                state.Contributor!,
                state.TimeEntryId!,
                usedAtUtc.ToUniversalTime(),
                ServerDerivedSource(state))
            {
                OutcomeCategory = "adjusted"
            }
        ]);
    }

    /// <summary>
    /// Determines whether a capability can currently back a confirmation or its read-only display,
    /// applying the exact same fail-closed checks as <see cref="HandleUse"/> without emitting an event.
    /// </summary>
    /// <param name="state">The folded capability state.</param>
    /// <param name="tenant">The trusted tenant authority.</param>
    /// <param name="tokenHash">The server-derived token hash.</param>
    /// <param name="atUtc">The evaluation instant in UTC.</param>
    /// <returns><see langword="true"/> when the capability is valid, unexpired, unused, and confirm-scoped.</returns>
    public static bool IsValidForUse(
        MagicLinkCapabilityState? state,
        TenantReference? tenant,
        MagicLinkTokenHash tokenHash,
        DateTimeOffset atUtc)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        List<TimesheetsFieldError> errors = [];
        ValidateUse(
            state,
            tenant,
            tokenHash,
            atUtc,
            static action => action is MagicLinkAllowedAction.Confirm or MagicLinkAllowedAction.ConfirmOrAdjust,
            errors);
        return errors.Count == 0;
    }

    public static bool IsValidForAdjustment(
        MagicLinkCapabilityState? state,
        TenantReference? tenant,
        MagicLinkTokenHash tokenHash,
        DateTimeOffset atUtc)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        List<TimesheetsFieldError> errors = [];
        ValidateUse(
            state,
            tenant,
            tokenHash,
            atUtc,
            static action => action is MagicLinkAllowedAction.Adjust or MagicLinkAllowedAction.ConfirmOrAdjust,
            errors);
        return errors.Count == 0;
    }

    /// <summary>
    /// Builds the audit source from validated server-side capability state only, never from caller input,
    /// so external request bodies cannot inject audit/source metadata into persisted events.
    /// </summary>
    /// <param name="state">The validated capability state.</param>
    /// <returns>The server-derived magic-link audit metadata.</returns>
    private static MagicLinkAuditMetadata ServerDerivedSource(MagicLinkCapabilityState state)
        => new("magic-link", state.CapabilityId!.Value);

    private static void ValidateIssue(
        IssueMagicLinkConfirmationCapability command,
        TenantReference? tenant,
        PartyReference? issuer,
        MagicLinkTokenHash tokenHash,
        DateTimeOffset issuedAtUtc,
        List<TimesheetsFieldError> errors)
    {
        if (tenant is null)
        {
            errors.Add(new("tenant", "required", "Tenant authority is required."));
        }

        if (issuer is null)
        {
            errors.Add(new("issuer", "required", "Issuer authority is required."));
        }

        if (command.Scope is null)
        {
            errors.Add(new("scope", "required", "Magic-link scope is required."));
            return;
        }

        if (command.Scope.Target is null || command.Scope.Target.TargetKind == TimeEntryTargetKind.Unknown)
        {
            errors.Add(new("target", "required", "Magic-link target is required."));
        }

        if (command.Scope.TargetKind == MagicLinkTargetKind.Unknown)
        {
            errors.Add(new("targetKind", "required", "Magic-link target kind is required."));
        }

        if (command.AllowedAction == MagicLinkAllowedAction.Unknown)
        {
            errors.Add(new("allowedAction", "required", "Magic-link allowed action is required."));
        }

        if (issuedAtUtc.Offset != TimeSpan.Zero || command.ExpiresAtUtc.Offset != TimeSpan.Zero)
        {
            errors.Add(new("expiresAtUtc", "utc-required", "Magic-link issue and expiry instants must be UTC."));
        }

        if (command.ExpiresAtUtc <= issuedAtUtc)
        {
            errors.Add(new("expiresAtUtc", "expired-at-issue", "Magic-link expiry must be in the future."));
        }

        if (string.IsNullOrWhiteSpace(tokenHash.Value))
        {
            errors.Add(new("tokenHash", "required", "Magic-link token hash is required."));
        }
    }

    private static void ValidateTransition(
        MagicLinkCapabilityId capabilityId,
        MagicLinkCapabilityState? state,
        TenantReference? tenant,
        PartyReference? actor,
        DateTimeOffset transitionAtUtc,
        bool requireActor,
        List<TimesheetsFieldError> errors)
    {
        if (tenant is null)
        {
            errors.Add(new("tenant", "required", "Tenant authority is required."));
        }

        if (requireActor && actor is null)
        {
            errors.Add(new("actor", "required", "Actor authority is required."));
        }

        if (transitionAtUtc.Offset != TimeSpan.Zero)
        {
            errors.Add(new("transitionAtUtc", "utc-required", "Magic-link transition instant must be UTC."));
        }

        if (state?.Exists != true)
        {
            errors.Add(new("capabilityId", "unknown", "Magic-link capability cannot be resolved."));
            return;
        }

        if (state.CapabilityId != capabilityId)
        {
            errors.Add(new("capabilityId", "mismatch", "Magic-link capability state does not match the command."));
        }

        if (state.IsTerminal)
        {
            errors.Add(new("state", "terminal", "Magic-link capability is already terminal."));
        }
    }

    private static void ValidateUse(
        MagicLinkCapabilityState? state,
        TenantReference? tenant,
        MagicLinkTokenHash tokenHash,
        DateTimeOffset usedAtUtc,
        Func<MagicLinkAllowedAction, bool> isAllowedAction,
        List<TimesheetsFieldError> errors)
    {
        if (tenant is null)
        {
            errors.Add(InvalidLink("tenant"));
        }

        if (usedAtUtc.Offset != TimeSpan.Zero)
        {
            errors.Add(InvalidLink("usedAtUtc"));
        }

        if (state?.Exists != true)
        {
            errors.Add(InvalidLink("capability"));
            return;
        }

        if (state.Tenant != tenant
            || state.CapabilityId is null
            || state.Contributor is null
            || state.TimeEntryId is null
            || state.Target is null
            || state.Target.TargetKind == TimeEntryTargetKind.Unknown
            || state.TargetKind != MagicLinkTargetKind.ProposedTimeEntry
            || state.TokenHash != tokenHash)
        {
            errors.Add(InvalidLink("capability"));
        }

        if (state.IsTerminal || state.State != Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Issued)
        {
            errors.Add(InvalidLink("capability"));
        }

        if (state.ExpiresAtUtc <= usedAtUtc.ToUniversalTime())
        {
            errors.Add(InvalidLink("capability"));
        }

        if (!isAllowedAction(state.AllowedAction))
        {
            errors.Add(InvalidLink("capability"));
        }
    }

    private static TimesheetsFieldError InvalidLink(string field)
        => new(field, "invalid-link", MagicLinkInvalidLinkDenial.Default.Title);

    private static TimesheetsDomainResult Reject(
        string message,
        IReadOnlyList<TimesheetsFieldError> errors)
        => TimesheetsDomainResult.Rejection([
            new(TimesheetsRejectionCode.ValidationFailed, message, errors)
        ]);
}
