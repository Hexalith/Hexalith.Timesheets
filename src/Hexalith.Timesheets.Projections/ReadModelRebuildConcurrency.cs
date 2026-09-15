using Hexalith.EventStore.Client.Projections;

namespace Hexalith.Timesheets.Projections;

/// <summary>
/// Chooses the optimistic-concurrency policy a rebuild plan must carry for one read-model row.
/// </summary>
/// <remarks>
/// <para>
/// The choice is driven by whether the row exists, never by whether the row's bytes happened to
/// materialize as <typeparamref name="TValue"/>. <see cref="ReadModelBatchConcurrency.CreateOnly"/>
/// is accepted only while the key is absent, so selecting it for a row that already exists strands
/// that rebuild write permanently instead of publishing it.
/// </para>
/// <para>
/// A non-empty ETag is proof the row exists: the shipped stores return an existing row as its
/// deserialized value paired with its ETag, and a value that deserializes to null - an empty or
/// JSON-null payload, or a payload of another shape - still leaves the key, and its ETag, in place.
/// That state takes <see cref="ReadModelBatchConcurrency.Match(string)"/> and heals the row.
/// </para>
/// <para>
/// Only an entry carrying nothing that indicates an existing row takes
/// <see cref="ReadModelBatchConcurrency.CreateOnly"/>. An existing row a store returned without an
/// ETag falls back to <see cref="ReadModelBatchConcurrency.LastWrite"/>, which is safe here because
/// every plan produced in this project is a deterministic replay of the same history: overwriting
/// re-derives identical bytes, while a permanently rejected write silently strands the projection.
/// </para>
/// </remarks>
internal static class ReadModelRebuildConcurrency
{
    /// <summary>Selects the concurrency policy for a read-model row that was just read.</summary>
    /// <typeparam name="TValue">The read-model type.</typeparam>
    /// <param name="current">The entry returned by the read-model store.</param>
    /// <returns>The concurrency policy the plan's write operation must carry.</returns>
    internal static ReadModelBatchConcurrency For<TValue>(ReadModelEntry<TValue> current)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(current);
        return current.ETag is { Length: > 0 } etag
            ? ReadModelBatchConcurrency.Match(etag)
            : current.Value is null
                ? ReadModelBatchConcurrency.CreateOnly
                : ReadModelBatchConcurrency.LastWrite;
    }
}
