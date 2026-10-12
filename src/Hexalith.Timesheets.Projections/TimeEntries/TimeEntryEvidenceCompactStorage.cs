using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Timesheets.Contracts.Models;

namespace Hexalith.Timesheets.Projections.TimeEntries;

/// <summary>Builds the lossless compact representation of a selected Time Entry evidence row.</summary>
internal static class TimeEntryEvidenceCompactStorage
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    /// <summary>Packs a validated evidence row and signs its canonical storage shape.</summary>
    internal static JsonObject Pack(TimeEntryEvidenceReadModel row,
        string? effectiveValuesDigest = null)
    {
        ArgumentNullException.ThrowIfNull(row);
        ValidateLineage(row);
        TimeEntryEvidenceReadModel packed = row with
        {
            PackedEventLineage = TimeEntryEvidenceLineageCodec.PackEvents(row.EventLineage),
            PackedPayloadLineage = TimeEntryEvidenceLineageCodec.PackPayloads(row.EventPayloadLineage)
        };
        JsonObject json = JsonSerializer.SerializeToNode(packed, s_json)?.AsObject()
            ?? throw new InvalidOperationException("Time Entry evidence did not serialize.");
        _ = json.Remove("eventLineage");
        _ = json.Remove("eventPayloadLineage");
        if (effectiveValuesDigest is not null)
        {
            if (effectiveValuesDigest.Length != 64
                || effectiveValuesDigest.Any(static character => !char.IsAsciiHexDigitUpper(character)))
                throw new InvalidOperationException("The effective Time Entry values digest is malformed.");
            json["effectiveValuesDigest"] = effectiveValuesDigest;
        }
        json["materializedDigest"] = Digest(json);
        return json;
    }

    /// <summary>Restores a selected row only when its compact lineage and integrity digest agree.</summary>
    internal static TimeEntryEvidenceReadModel Unpack(JsonObject json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.ContainsKey("eventLineage") && json["eventLineage"] is null
            || json.ContainsKey("eventPayloadLineage") && json["eventPayloadLineage"] is null)
        {
            throw new InvalidOperationException("Stored Time Entry lineage is null.");
        }

        bool legacyExplicit = !json.ContainsKey("materializedDigest")
            && json["packedEventLineage"] is null
            && json["packedPayloadLineage"] is null
            && json.ContainsKey("eventLineage")
            && json.ContainsKey("eventPayloadLineage");
        if (!legacyExplicit)
        {
            if (json["materializedDigest"]?.GetValue<string>() is not { Length: 64 } digest)
            {
                throw new InvalidOperationException("Stored Time Entry materialized evidence has no integrity digest.");
            }

            JsonObject unsigned = (JsonObject)json.DeepClone();
            _ = unsigned.Remove("materializedDigest");
            if (!string.Equals(digest, Digest(unsigned), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Stored Time Entry materialized evidence is inconsistent.");
            }
        }

        TimeEntryEvidenceReadModel row = json.Deserialize<TimeEntryEvidenceReadModel>(s_json)
            ?? throw new InvalidOperationException("Stored Time Entry evidence is empty.");
        if (row.PackedEventLineage is not null || row.PackedPayloadLineage is not null)
        {
            if (row.PackedEventLineage is null || row.PackedPayloadLineage is null)
            {
                throw new InvalidOperationException("Stored Time Entry compact lineage is incomplete.");
            }

            if (json.ContainsKey("eventLineage")
                && !(json["eventLineage"]!.Deserialize<TimeEntryEventLineageItem[]>(s_json)
                    ?? throw new InvalidOperationException("Stored Time Entry explicit event lineage is null."))
                    .SequenceEqual(TimeEntryEvidenceLineageCodec.UnpackEvents(row.PackedEventLineage))
                || json.ContainsKey("eventPayloadLineage")
                && !(json["eventPayloadLineage"]!.Deserialize<string[]>(s_json)
                    ?? throw new InvalidOperationException("Stored Time Entry explicit payload lineage is null."))
                    .SequenceEqual(TimeEntryEvidenceLineageCodec.UnpackPayloads(row.PackedPayloadLineage), StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Stored Time Entry explicit and compact lineage disagree.");
            }
        }
        else if (!json.ContainsKey("eventLineage") || !json.ContainsKey("eventPayloadLineage"))
        {
            throw new InvalidOperationException("Stored Time Entry lineage is missing.");
        }

        ValidateLineage(row);

        return row;
    }

    /// <summary>Checks the exact digest format written before envelope-bound compact storage existed.</summary>
    internal static void ValidateLegacyPayloadLineage(
        TimeEntryEvidenceReadModel row, IReadOnlyList<ProjectionEventDto> ownerPrefix)
    {
        if (ownerPrefix.Count != row.EventPayloadLineage.Count)
        {
            throw new InvalidOperationException("Legacy Time Entry row has no complete owner prefix.");
        }

        for (int index = 0; index < ownerPrefix.Count; index++)
        {
            ProjectionEventDto item = ownerPrefix[index];
            byte[] name = Encoding.UTF8.GetBytes(item.EventTypeName + "\0");
            byte[] input = new byte[name.Length + item.Payload.Length];
            name.CopyTo(input, 0);
            item.Payload.CopyTo(input, name.Length);
            string digest = Convert.ToHexString(SHA256.HashData(input));
            if (!StringComparer.Ordinal.Equals(row.EventPayloadLineage[index], digest))
            {
                throw new InvalidOperationException("Legacy Time Entry payload lineage differs from its owner history.");
            }
        }
    }

    private static void ValidateLineage(TimeEntryEvidenceReadModel row)
    {
        if (row.EventLineage is null || row.EventPayloadLineage is null
            || row.EventLineage.Count == 0
            || row.EventPayloadLineage.Count < row.EventLineage.Count)
        {
            throw new InvalidOperationException("Stored Time Entry lineage exceeds its owner prefix.");
        }

        _ = TimeEntryEvidenceLineageCodec.PackEvents(row.EventLineage);
        _ = TimeEntryEvidenceLineageCodec.PackPayloads(row.EventPayloadLineage);

        IReadOnlyList<string> payloadLineage = row.EventPayloadLineage;
        long priorOrdinal = 0;
        foreach (TimeEntryEventLineageItem item in row.EventLineage)
        {
            if (item.Ordinal <= priorOrdinal || item.Ordinal > payloadLineage.Count)
            {
                throw new InvalidOperationException("Stored Time Entry event ordinal is outside its owner prefix.");
            }

            priorOrdinal = item.Ordinal;
        }
    }

    private static string Digest(JsonObject json)
    {
        // ReadModelBatch persists object properties in ordinal order. Hash the same
        // canonical shape so a staged write can be verified after selected readback.
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(json, s_json));
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCanonical(document.RootElement, writer);
        }

        return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan));
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject()
                    .OrderBy(static property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    WriteCanonical(item, writer);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
