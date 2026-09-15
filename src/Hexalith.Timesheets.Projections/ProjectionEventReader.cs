using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.Timesheets.Projections;

internal static class ProjectionEventReader
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    internal static IReadOnlyList<ProjectionEventDto> Normalize(IEnumerable<ProjectionEventDto> events)
    {
        if (events is null)
        {
            throw new InvalidOperationException("The projection event collection is missing.");
        }

        List<ProjectionEventDto> received = [];
        foreach (ProjectionEventDto? projectionEvent in events)
        {
            if (projectionEvent is null || string.IsNullOrWhiteSpace(projectionEvent.EventTypeName))
            {
                throw new InvalidOperationException("A projection event envelope is malformed.");
            }

            received.Add(projectionEvent);
        }

        List<ProjectionEventDto> normalized = [];
        foreach (IGrouping<long, ProjectionEventDto> group in received.GroupBy(static item => item.SequenceNumber))
        {
            ProjectionEventDto first = group.First();
            if (group.Any(item => !Equivalent(first, item)))
            {
                throw new InvalidOperationException("Conflicting projection events share one aggregate sequence.");
            }

            normalized.Add(first);
        }

        normalized.Sort(static (left, right) => left.SequenceNumber.CompareTo(right.SequenceNumber));
        long expectedSequence = 1;
        HashSet<string> messageIds = new(StringComparer.Ordinal);
        if (normalized.Any(item => item.SequenceNumber != expectedSequence++
                || (!string.IsNullOrWhiteSpace(item.MessageId) && !messageIds.Add(item.MessageId))))
        {
            throw new InvalidOperationException("The projection event history is incomplete or ambiguous.");
        }

        return normalized;
    }

    internal static T? Deserialize<T>(ProjectionEventDto projectionEvent)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(projectionEvent);
        if (!Matches<T>(projectionEvent.EventTypeName))
        {
            return null;
        }

        if (!string.Equals(projectionEvent.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A recognized projection event does not use JSON serialization.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(projectionEvent.Payload, s_jsonOptions)
                ?? throw new InvalidOperationException("A recognized projection event has an empty payload.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("A recognized projection event has a malformed payload.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("A recognized projection event has an invalid payload.", exception);
        }
    }

    private static bool Equivalent(ProjectionEventDto left, ProjectionEventDto right)
        => string.Equals(left.EventTypeName, right.EventTypeName, StringComparison.Ordinal)
            && string.Equals(left.SerializationFormat, right.SerializationFormat, StringComparison.OrdinalIgnoreCase)
            && left.Payload.AsSpan().SequenceEqual(right.Payload)
            && left.Timestamp == right.Timestamp
            && string.Equals(left.CorrelationId, right.CorrelationId, StringComparison.Ordinal)
            && string.Equals(left.MessageId, right.MessageId, StringComparison.Ordinal)
            && string.Equals(left.UserId, right.UserId, StringComparison.Ordinal)
            && left.GlobalPosition == right.GlobalPosition;

    private static bool Matches<T>(string? eventTypeName)
    {
        // The DTO declares this member non-nullable, but a wire payload carrying JSON null deserializes
        // it to null anyway. An absent type name identifies no event, so it reads as an unknown event
        // here rather than relying on Normalize having rejected the envelope first.
        if (string.IsNullOrWhiteSpace(eventTypeName))
        {
            return false;
        }

        string unqualified = eventTypeName.Split(',', 2)[0];
        return string.Equals(unqualified, typeof(T).Name, StringComparison.Ordinal)
            || string.Equals(unqualified, typeof(T).FullName, StringComparison.Ordinal);
    }
}
