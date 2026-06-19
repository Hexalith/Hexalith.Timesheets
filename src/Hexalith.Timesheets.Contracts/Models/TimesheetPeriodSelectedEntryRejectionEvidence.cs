using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimesheetPeriodSelectedEntryRejectionEvidence(
    TimeEntryId TimeEntryId,
    TimeEntryRejectionReason Reason);
