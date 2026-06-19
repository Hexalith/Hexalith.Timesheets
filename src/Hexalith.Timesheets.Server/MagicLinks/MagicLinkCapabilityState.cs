using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.MagicLinks;

public sealed class MagicLinkCapabilityState
{
    public bool Exists { get; private set; }

    public MagicLinkCapabilityId? CapabilityId { get; private set; }

    public TenantReference? Tenant { get; private set; }

    public PartyReference? Contributor { get; private set; }

    public TimeEntryTargetReference? Target { get; private set; }

    public ActivityTypeId? ActivityTypeId { get; private set; }

    public TimeEntryId? TimeEntryId { get; private set; }

    public MagicLinkAllowedAction AllowedAction { get; private set; }

    public Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState State { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public PartyReference? Issuer { get; private set; }

    public DateTimeOffset? IssuedAtUtc { get; private set; }

    public MagicLinkTokenHash? TokenHash { get; private set; }

    public bool IsTerminal => State is Contracts.ValueObjects.MagicLinkCapabilityState.Revoked
        or Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Expired
        or Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Used;

    public void Apply(MagicLinkConfirmationCapabilityIssued issued)
    {
        ArgumentNullException.ThrowIfNull(issued);

        Exists = true;
        CapabilityId = issued.CapabilityId;
        Tenant = issued.Tenant;
        Contributor = issued.Contributor;
        Target = issued.Target;
        ActivityTypeId = issued.ActivityTypeId;
        TimeEntryId = issued.TimeEntryId;
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
        State = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Revoked;
    }

    public void Apply(MagicLinkConfirmationCapabilityExpired expired)
    {
        ArgumentNullException.ThrowIfNull(expired);
        State = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState.Expired;
    }
}
