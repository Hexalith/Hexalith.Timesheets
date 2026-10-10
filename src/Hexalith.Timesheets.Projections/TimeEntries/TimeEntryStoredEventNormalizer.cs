using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.Projections.TimeEntries;

/// <summary>Normalizes EventStore-owned payloads before Time Entry read-model folding.</summary>
public static class TimeEntryStoredEventNormalizer
{
    /// <summary>Unwraps stored effects and rejects missing or conflicting duplicate deliveries.</summary>
    public static IReadOnlyList<TimeEntryProjectionEvent> Normalize(IEnumerable<TimeEntryProjectionEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var seen = new Dictionary<(TimeEntryId Owner, long Sequence), object>();
        var normalized = new List<TimeEntryProjectionEvent>();
        foreach (TimeEntryProjectionEvent item in events.OrderBy(static value => value.SequenceNumber))
        {
            object payload = item.Payload switch
            {
                StoredTimeEntryRecorded stored => stored.Event,
                StoredTimeEntryConfirmed stored => stored.Event,
                StoredTimeEntryAdjusted stored => stored.Event,
                StoredMagicLinkUsed stored => stored.Event,
                StoredMagicLinkRevoked stored => stored.Event,
                StoredMagicLinkExpired stored => stored.Event,
                _ => item.Payload
            } ?? throw new InvalidOperationException("Stored Time Entry evidence is missing.");

            // Sequences belong to one Time Entry stream. Different entries may both have
            // sequence 1; only contradictory deliveries within one owner are ambiguous.
            TimeEntryId? owner = OwnerOf(payload);
            if (owner is null)
            {
                normalized.Add(item with { Payload = payload });
                continue;
            }

            var key = (owner, item.SequenceNumber);
            if (seen.TryGetValue(key, out object? existing))
            {
                if (existing.GetType() != payload.GetType() || !Equals(existing, payload))
                {
                    throw new InvalidOperationException("Time Entry delivery has contradictory evidence.");
                }

                continue;
            }

            seen.Add(key, payload);
            normalized.Add(item with { Payload = payload });
        }

        return normalized;
    }

    private static TimeEntryId? OwnerOf(object payload) => payload switch
    {
        TimeEntryRecorded item => item.TimeEntryId,
        TimeEntrySubmitted item => item.TimeEntryId,
        TimeEntryContributorConfirmed item => item.TimeEntryId,
        TimeEntryAdjustedThroughMagicLink item => item.TimeEntryId,
        TimeEntryApproved item => item.TimeEntryId,
        TimeEntryRejected item => item.TimeEntryId,
        TimeEntryCorrected item => item.TimeEntryId,
        TimeEntryApprovedCorrected item => item.TimeEntryId,
        MagicLinkConfirmationCapabilityUsed item => item.TimeEntryId,
        MagicLinkConfirmationCapabilityRevoked item => item.TimeEntryId,
        MagicLinkConfirmationCapabilityExpired item => item.TimeEntryId,
        _ => null
    };
}
