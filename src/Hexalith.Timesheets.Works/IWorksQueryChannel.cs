using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.Timesheets.Works;

/// <summary>
/// Timesheets-owned port over the EventStore domain-service query channel used to consume Hexalith.Works
/// consumer queries (for example <c>get-work-item</c>). Its signature mirrors the EventStore gateway's
/// <c>Hexalith.EventStore.Queries.IDomainQueryInvoker</c> exactly, so the composed host supplies the
/// implementation with a small pass-through adapter. That adapter is defined in the host (not in this
/// library) because <c>IDomainQueryInvoker</c> lives in the EventStore web host, which this light library
/// deliberately does not reference:
/// <code>
/// internal sealed class DomainQueryInvokerWorksQueryChannel(IDomainQueryInvoker invoker) : IWorksQueryChannel
/// {
///     public Task&lt;QueryResult&gt; InvokeAsync(QueryEnvelope query, CancellationToken cancellationToken)
///         =&gt; invoker.InvokeAsync(query, cancellationToken);
/// }
///
/// services.AddSingleton&lt;IWorksQueryChannel, DomainQueryInvokerWorksQueryChannel&gt;();
/// </code>
/// <para>
/// Depending on this port (which needs only <c>Hexalith.EventStore.Contracts</c>) instead of the gateway
/// keeps this domain adapter a light, conflict-free library while still consuming the real Works query over
/// the real <see cref="QueryEnvelope"/>/<see cref="QueryResult"/> wire contract. It is reusable by Story 4.8's
/// planned-effort provider.
/// </para>
/// </summary>
public interface IWorksQueryChannel
{
    /// <summary>
    /// Invokes the Works domain service that owns the query and returns its result.
    /// </summary>
    /// <param name="query">The query envelope to send to the Works <c>/query</c> endpoint.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The Works domain service's <see cref="QueryResult"/>.</returns>
    Task<QueryResult> InvokeAsync(QueryEnvelope query, CancellationToken cancellationToken);
}
