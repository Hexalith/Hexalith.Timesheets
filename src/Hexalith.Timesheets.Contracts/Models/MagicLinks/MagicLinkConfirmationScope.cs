using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models.MagicLinks;

public sealed record MagicLinkConfirmationScope(
    PartyReference Contributor,
    TimeEntryTargetReference Target,
    ActivityTypeId ActivityTypeId,
    TimeEntryId TimeEntryId,
    MagicLinkTargetKind TargetKind);
