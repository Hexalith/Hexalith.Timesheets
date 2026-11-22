using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.MagicLinks;

public sealed class MagicLinkCapabilityState
{
    /// <summary>Applies an EventStore issuance payload.</summary>
    public void Apply(StoredMagicLinkIssued issued) => Apply(issued.Event);

    /// <summary>Applies an EventStore use payload.</summary>
    public void Apply(StoredMagicLinkUsed used) => Apply(used.Event);

    /// <summary>Applies an EventStore revocation payload.</summary>
    public void Apply(StoredMagicLinkRevoked revoked) => Apply(revoked.Event);

    /// <summary>Applies an EventStore expiry payload.</summary>
    public void Apply(StoredMagicLinkExpired expired) => Apply(expired.Event);

    /// <summary>Ignores a rejected command retained in the EventStore stream.</summary>
    public void Apply(MagicLinkCommitRejected rejected)
        => ArgumentNullException.ThrowIfNull(rejected);

    public bool Exists { get; private set; }

    public MagicLinkCapabilityId? CapabilityId { get; private set; }

    public TenantReference? Tenant { get; private set; }

    public PartyReference? Contributor { get; private set; }

    public TimeEntryTargetReference? Target { get; private set; }

    public ActivityTypeId? ActivityTypeId { get; private set; }

    public TimeEntryId? TimeEntryId { get; private set; }

    public MagicLinkTargetKind TargetKind { get; private set; }

    public MagicLinkAllowedAction AllowedAction { get; private set; }

    public Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState State { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public PartyReference? Issuer { get; private set; }

    public DateTimeOffset? IssuedAtUtc { get; private set; }

    public MagicLinkTokenHash? TokenHash { get; private set; }

    public DateTimeOffset? UsedAtUtc { get; private set; }

    public MagicLinkAuditMetadata? UseMetadata { get; private set; }

    public bool IsTerminal => State is Contracts.ValueObjects.MagicLinkCapabilityState.Revoked
        or Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Expired
        or Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used;

    public void Apply(MagicLinkConfirmationCapabilityIssued issued)
    {
        ArgumentNullException.ThrowIfNull(issued);
        if (Exists)
        {
            throw new InvalidOperationException("A capability cannot be issued more than once.");
        }

        Exists = true;
        CapabilityId = issued.CapabilityId;
        Tenant = issued.Tenant;
        Contributor = issued.Contributor;
        Target = issued.Target;
        ActivityTypeId = issued.ActivityTypeId;
        TimeEntryId = issued.TimeEntryId;
        TargetKind = issued.TargetKind;
        AllowedAction = issued.AllowedAction;
        State = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Issued;
        ExpiresAtUtc = issued.ExpiresAtUtc;
        Issuer = issued.Issuer;
        IssuedAtUtc = issued.IssuedAtUtc;
        TokenHash = issued.TokenHash;
    }

    public void Apply(MagicLinkConfirmationCapabilityRevoked revoked)
    {
        ArgumentNullException.ThrowIfNull(revoked);
        RequireIssued(revoked.Tenant, revoked.CapabilityId, revoked.TimeEntryId ?? TimeEntryId);
        State = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Revoked;
    }

    public void Apply(MagicLinkConfirmationCapabilityExpired expired)
    {
        ArgumentNullException.ThrowIfNull(expired);
        RequireIssued(expired.Tenant, expired.CapabilityId, expired.TimeEntryId ?? TimeEntryId);
        if (expired.ExpiredAtUtc < ExpiresAtUtc)
        {
            throw new InvalidOperationException("A capability expiry precedes its expiration time.");
        }

        State = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Expired;
    }

    public void Apply(MagicLinkConfirmationCapabilityUsed used)
    {
        ArgumentNullException.ThrowIfNull(used);
        RequireIssued(used.Tenant, used.CapabilityId, used.TimeEntryId);
        bool actionAllowed = used.OutcomeCategory switch
        {
            "confirmed" => AllowedAction is MagicLinkAllowedAction.Confirm or MagicLinkAllowedAction.ConfirmOrAdjust,
            "adjusted" => AllowedAction is MagicLinkAllowedAction.Adjust or MagicLinkAllowedAction.ConfirmOrAdjust,
            _ => false
        };
        if (used.Contributor is null || used.Contributor != Contributor || !actionAllowed
            || used.UsedAtUtc < IssuedAtUtc || used.UsedAtUtc >= ExpiresAtUtc)
        {
            throw new InvalidOperationException("A capability use does not match its issuance.");
        }

        State = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used;
        UsedAtUtc = used.UsedAtUtc;
        UseMetadata = used.Source;
    }

    private void RequireIssued(TenantReference tenant, MagicLinkCapabilityId capabilityId, TimeEntryId? timeEntryId)
    {
        if (!Exists || State != Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Issued)
        {
            throw new InvalidOperationException("A capability terminal transition requires an issued capability.");
        }

        if (tenant != Tenant || capabilityId != CapabilityId || timeEntryId is null || timeEntryId != TimeEntryId)
        {
            throw new InvalidOperationException("A terminal event must match the issued capability and Time Entry.");
        }
    }
}
