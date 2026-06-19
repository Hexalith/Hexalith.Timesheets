using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.TimesheetPeriods;

public sealed record SubmitTimesheetPeriod(
    TimesheetPeriodId TimesheetPeriodId,
    PartyReference Contributor,
    TimesheetPeriodRequest Period,
    IReadOnlyList<TimeEntryId> TimeEntryIds);
