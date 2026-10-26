namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>One immutable logical foreground profile frozen with its clock and counters.</summary>
/// <param name="Elapsed">Elapsed time including setup.</param>
/// <param name="Reads">Logical GetAsync invocations by phase.</param>
/// <param name="Maximum">Peak logical invocation concurrency by phase.</param>
/// <param name="IndexKeys">Exact distinct inventory addresses.</param>
/// <param name="Active">All logical reads still in flight, including independent cleanup.</param>
/// <param name="ActiveIndexReads">Inventory reads still in flight.</param>
/// <param name="IndependentMetadataReads">Independent-token metadata reads observed, including SDK cleanup.</param>
internal sealed record BootstrapReadMeasurement(TimeSpan Elapsed, IReadOnlyDictionary<string, int> Reads,
    IReadOnlyDictionary<string, int> Maximum, IReadOnlyDictionary<string, IReadOnlyList<string>> IndexKeys,
    int Active, int ActiveIndexReads, int IndependentMetadataReads);
