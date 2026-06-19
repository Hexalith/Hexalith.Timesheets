using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimeEntryApprovalDecisionEvidence(
    TimeEntryId TimeEntryId,
    TimeEntryApprovalDecisionId TimeEntryApprovalDecisionId,
    PartyReference Approver,
    TenantReference Tenant,
    DateTimeOffset DecidedAtUtc,
    TimeEntryApprovalState ApprovalState,
    TimeEntryApprovalScope ApprovalScope,
    ApprovalAuthoritySourceAttribution AuthoritySource,
    TimeEntryRejectionReason? Reason);
