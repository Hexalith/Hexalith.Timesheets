namespace Hexalith.Timesheets.Projections.TimesheetPeriods;

public sealed record TimesheetPeriodProjectionEvent(
    string MessageId,
    long SequenceNumber,
    object Payload);
