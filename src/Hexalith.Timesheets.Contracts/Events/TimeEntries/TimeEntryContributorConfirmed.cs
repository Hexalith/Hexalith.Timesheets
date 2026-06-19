using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.TimeEntries;

public sealed record TimeEntryContributorConfirmed(
    TimeEntryId TimeEntryId,
    PartyReference Contributor,
    TenantReference Tenant,
    DateTimeOffset ConfirmedAtUtc,
    ExternalContributionSource Source);
