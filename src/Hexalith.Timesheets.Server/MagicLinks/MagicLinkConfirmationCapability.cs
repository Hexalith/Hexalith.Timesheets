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

    private static TimesheetsDomainResult Reject(
        string message,
        IReadOnlyList<TimesheetsFieldError> errors)
        => TimesheetsDomainResult.Rejection([
            new(TimesheetsRejectionCode.ValidationFailed, message, errors)
        ]);
}
