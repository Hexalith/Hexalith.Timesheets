using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimesheetPeriodBlockingEntryGuidance(
    TimeEntryId TimeEntryId,
    string Field,
    string Code,
    string Guidance);
