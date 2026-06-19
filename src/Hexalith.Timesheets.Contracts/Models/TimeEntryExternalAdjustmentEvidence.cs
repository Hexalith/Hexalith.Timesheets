using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimeEntryExternalAdjustmentEvidence(
    TimeEntryId TimeEntryId,
    PartyReference Contributor,
    TenantReference Tenant,
    DateTimeOffset AdjustedAtUtc,
    TimeEntryCorrectionValues PreviousValues,
    TimeEntryCorrectionValues AdjustedValues,
    ExternalContributionSource Source);
