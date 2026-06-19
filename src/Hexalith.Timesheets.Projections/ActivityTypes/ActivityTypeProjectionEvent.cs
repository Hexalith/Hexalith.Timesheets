namespace Hexalith.Timesheets.Projections.ActivityTypes;

public sealed record ActivityTypeProjectionEvent(
    string MessageId,
    long SequenceNumber,
    object Payload);
