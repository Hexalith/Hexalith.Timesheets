using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Server.TimeEntries.Commands;

/// <summary>Internal confirmation intent; the owner actor rechecks all authority before appending.</summary>
/// <param name="Command">The proposed external confirmation.</param>
/// <param name="Tenant">The tenant bound to the workload request.</param>
/// <param name="Actor">The authenticated actor bound to the workload request.</param>
/// <param name="ConfirmedAtUtc">The proposed confirmation audit instant.</param>
public sealed record CommitTimeEntryConfirmation(
    ConfirmExternalTimeEntry Command,
    TenantReference Tenant,
    PartyReference Actor,
    DateTimeOffset ConfirmedAtUtc);
