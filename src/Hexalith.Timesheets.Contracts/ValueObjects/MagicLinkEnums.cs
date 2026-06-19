using System.Text.Json.Serialization;

namespace Hexalith.Timesheets.Contracts.ValueObjects;

[JsonConverter(typeof(JsonStringEnumConverter<MagicLinkAllowedAction>))]
public enum MagicLinkAllowedAction
{
    Unknown = 0,
    Confirm = 1,
    Adjust = 2,
    ConfirmOrAdjust = 3
}

[JsonConverter(typeof(JsonStringEnumConverter<MagicLinkCapabilityState>))]
public enum MagicLinkCapabilityState
{
    Unknown = 0,
    Issued = 1,
    Revoked = 2,
    Expired = 3,
    Used = 4
}

[JsonConverter(typeof(JsonStringEnumConverter<MagicLinkTargetKind>))]
public enum MagicLinkTargetKind
{
    Unknown = 0,
    ProposedTimeEntry = 1,
    ExistingTimeEntry = 2
}

[JsonConverter(typeof(JsonStringEnumConverter<MagicLinkExpiryState>))]
public enum MagicLinkExpiryState
{
    Unknown = 0,
    Active = 1,
    ExpiringSoon = 2,
    Expired = 3
}
