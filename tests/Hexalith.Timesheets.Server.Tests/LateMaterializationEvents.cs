using System.Collections;

using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.Timesheets.Server.Tests;

/// <summary>Triggers a late boundary after transport charging and response validation, as buffering begins.</summary>
/// <param name="events">The valid response events.</param>
/// <param name="onMaterializing">The cancellation or expiry boundary to exercise.</param>
internal sealed class LateMaterializationEvents(IReadOnlyList<StreamReadEvent> events, Action onMaterializing)
    : IReadOnlyList<StreamReadEvent>
{
    private int _enumerations;

    /// <inheritdoc/>
    public int Count => events.Count;

    /// <inheritdoc/>
    public StreamReadEvent this[int index] => events[index];

    /// <inheritdoc/>
    public IEnumerator<StreamReadEvent> GetEnumerator()
    {
        // A valid non-empty page is traversed for charging, cursor validation and maximum sequence
        // before its fourth enumeration materializes the authoritative history buffer.
        if (++_enumerations == 4) onMaterializing();
        return events.GetEnumerator();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
