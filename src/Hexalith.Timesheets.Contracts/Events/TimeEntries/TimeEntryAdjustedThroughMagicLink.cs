using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.TimeEntries;

public sealed record TimeEntryAdjustedThroughMagicLink(
    TimeEntryId TimeEntryId,
    TenantReference Tenant,
    PartyReference Contributor,
    DateTimeOffset AdjustedAtUtc,
    ActivityTypeScope ActivityTypeScope,
    TimeEntryCorrectionValues PreviousValues,
    TimeEntryCorrectionValues AdjustedValues,
    ExternalContributionSource Source);
