using System.Diagnostics;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>Profiles logical IReadModelStore reads, excluding the SDK's underlying raw batch IO.</summary>
internal sealed class BootstrapObservingReadModelStore(IReadModelStore inner) : IReadModelStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _reads = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _active = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _maximum = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _indexKeys = new(StringComparer.Ordinal);
    private readonly Stopwatch _elapsed = new();
    private CancellationToken _foregroundToken;
    private bool _frozen;
    private BootstrapReadMeasurement? _measurement;
    private int _independentMetadataReads;
    private int _allActive;

    /// <summary>Gets or sets latency applied at each logical GetAsync invocation.</summary>
    public TimeSpan ReadDelay { get; set; }

    /// <summary>Gets or sets the inventory phase held beyond caller cancellation.</summary>
    public string? BlockPhase { get; set; }

    /// <summary>Starts counters and clock together, before setup uses the same caller token.</summary>
    public void Start(CancellationToken foregroundToken)
    {
        lock (_gate)
        {
            _foregroundToken = foregroundToken;
            _elapsed.Start();
        }
    }

    /// <summary>Freezes a coherent foreground snapshot; independent-token SDK cleanup is reported separately.</summary>
    public BootstrapReadMeasurement Freeze()
    {
        lock (_gate)
        {
            if (_measurement is not null) return _measurement;
            _frozen = true;
            _elapsed.Stop();
            return _measurement = new(_elapsed.Elapsed, new Dictionary<string, int>(_reads), new Dictionary<string, int>(_maximum),
                _indexKeys.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)[.. pair.Value]),
                _allActive, _active.GetValueOrDefault("direct") + _active.GetValueOrDefault("selected"),
                _independentMetadataReads);
        }
    }

    /// <inheritdoc />
    public async Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key,
        CancellationToken cancellationToken = default) where TValue : class
    {
        string phase = typeof(TValue) == typeof(MagicLinkTokenHashCapabilityIndexReadModel)
            ? key.StartsWith("shared-generation:", StringComparison.Ordinal) ? "selected" : "direct" : "metadata";
        bool block = false;
        bool measured;
        lock (_gate)
        {
            _allActive++;
            measured = !_frozen && cancellationToken == _foregroundToken;
            if (!_frozen && !measured && phase == "metadata") _independentMetadataReads++;
            if (measured)
            {
                int count = _reads.GetValueOrDefault(phase) + 1;
                _reads[phase] = count;
                _active[phase] = _active.GetValueOrDefault(phase) + 1;
                _maximum[phase] = Math.Max(_maximum.GetValueOrDefault(phase), _active[phase]);
                if (phase != "metadata")
                {
                    if (!_indexKeys.TryGetValue(phase, out HashSet<string>? keys))
                        _indexKeys[phase] = keys = new(StringComparer.Ordinal);
                    keys.Add(key);
                }
                block = phase == BlockPhase && count > 1
                    && (phase != "selected" || _reads.GetValueOrDefault("direct") == 257);
            }
        }
        try
        {
            if (measured) await Task.Delay(block ? TimeSpan.FromSeconds(30) : ReadDelay, cancellationToken);
            return await inner.GetAsync<TValue>(storeName, key, cancellationToken);
        }
        finally
        {
            lock (_gate)
            {
                _allActive--;
                if (measured) _active[phase]--;
            }
        }
    }

    /// <inheritdoc />
    public Task SaveAsync<TValue>(string storeName, string key, TValue value,
        CancellationToken cancellationToken = default) where TValue : class
        => inner.SaveAsync(storeName, key, value, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag,
        CancellationToken cancellationToken = default) where TValue : class
        => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
}
