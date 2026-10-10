using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Server.TimeEntries.Commands;

/// <summary>Internal recording intent; the owner actor rechecks all authority before appending.</summary>
/// <param name="Command">The proposed external entry.</param>
/// <param name="Tenant">The tenant bound to the workload request.</param>
/// <param name="Actor">The authenticated contributor bound to the workload request.</param>
/// <param name="SubmittedAtUtc">The proposed submission audit instant.</param>
public sealed record CommitTimeEntryRecord(
    SubmitExternalTimeEntry Command,
    TenantReference Tenant,
    PartyReference Actor,
    DateTimeOffset SubmittedAtUtc);
