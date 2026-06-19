namespace Hexalith.Timesheets.Projections.MagicLinks;

public sealed record MagicLinkProjectionEvent(
    string MessageId,
    long SequenceNumber,
    object Payload);
