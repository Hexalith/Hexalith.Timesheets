using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.TimeEntries;

public sealed record TimeEntryApproved(
    TimeEntryId TimeEntryId,
    PartyReference Approver,
    TenantReference Tenant,
    DateTimeOffset DecidedAtUtc,
    TimeEntryApprovalDecisionId TimeEntryApprovalDecisionId,
    TimeEntryApprovalState ApprovalState,
    ApprovalAuthoritySourceAttribution AuthoritySource,
    TimeEntryApprovalScope ApprovalScope);
