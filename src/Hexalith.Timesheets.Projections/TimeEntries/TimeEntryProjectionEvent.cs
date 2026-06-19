namespace Hexalith.Timesheets.Projections.TimeEntries;

public sealed record TimeEntryProjectionEvent(
    string MessageId,
    long SequenceNumber,
    object Payload);
