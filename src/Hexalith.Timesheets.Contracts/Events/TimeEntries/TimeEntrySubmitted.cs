using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Events.TimeEntries;

public sealed record TimeEntrySubmitted(
    TimeEntryId TimeEntryId,
    PartyReference Submitter,
    TenantReference Tenant,
    DateTimeOffset SubmittedAtUtc,
    TimeEntrySubmissionId TimeEntrySubmissionId,
    TimeEntrySubmissionScope SubmissionScope,
    TimeEntryApprovalState ApprovalState);
