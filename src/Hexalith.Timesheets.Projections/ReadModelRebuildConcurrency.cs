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
/// A non-blank ETag is proof the row exists: the shipped stores return an existing row as its
/// deserialized value paired with its ETag. A JSON-null payload still leaves the key and its ETag
/// in place, so that state takes <see cref="ReadModelBatchConcurrency.Match(string)"/> and heals
/// the row. Malformed JSON throws during the store read before this policy can be selected.
/// </para>
/// <para>
/// Only an entry carrying nothing that indicates an existing row takes
/// <see cref="ReadModelBatchConcurrency.CreateOnly"/>. An existing row a store returned without an
/// ETag is rejected: a tenant-slice rebuild can preserve other tenants' mutable contributions,
/// so replay alone cannot make an unconditional write safe across the read-to-commit window.
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
        return !string.IsNullOrWhiteSpace(current.ETag) && current.ETag is { } etag
            ? ReadModelBatchConcurrency.Match(etag)
            : current.Value is null && current.ETag is null
                ? ReadModelBatchConcurrency.CreateOnly
                : throw new InvalidOperationException("An existing rebuild row has no concurrency token.");
    }
}
