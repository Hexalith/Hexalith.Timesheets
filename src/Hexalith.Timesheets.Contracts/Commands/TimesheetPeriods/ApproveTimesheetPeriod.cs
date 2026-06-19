using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.TimesheetPeriods;

public sealed record ApproveTimesheetPeriod(
    TimesheetPeriodId TimesheetPeriodId,
    TimesheetPeriodApprovalDecisionId TimesheetPeriodApprovalDecisionId);
