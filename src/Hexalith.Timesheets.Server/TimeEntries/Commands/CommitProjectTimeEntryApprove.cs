using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Server.TimeEntries.Commands;

/// <summary>Carries a Project Time Entry approve intent to the EventStore owner.</summary>
/// <param name="Command">The requested lifecycle change.</param>
/// <param name="Tenant">The tenant bound to the authenticated envelope.</param>
/// <param name="Actor">The Party bound to the authenticated envelope.</param>
public sealed record CommitProjectTimeEntryApprove(ApproveTimeEntry Command, TenantReference Tenant, PartyReference Actor);
