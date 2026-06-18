using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

public sealed class PerformanceEvidenceTests
{
    [Fact]
    public void Performance_evidence_lane_documents_launch_latency_targets()
    {
        string evidence = File.ReadAllText(RepositoryRoot.PathTo("docs", "performance-evidence.md"));

        evidence.ShouldContain("500 ms p95");
        evidence.ShouldContain("2s p95");
        evidence.ShouldContain("EventStore-backed write path");
        evidence.ShouldContain("read models");
        evidence.ShouldContain("fast unit baseline");
    }

    [Fact]
    public void Integration_tests_reserve_runtime_and_performance_lanes_without_entering_fast_baseline()
    {
        string[] integrationTests = Directory.GetFiles(
            RepositoryRoot.PathTo("tests", "Hexalith.Timesheets.IntegrationTests"),
            "*.cs",
            SearchOption.AllDirectories);

        integrationTests.ShouldNotBeEmpty();

        string combinedSource = string.Join(Environment.NewLine, integrationTests.Select(File.ReadAllText));
        combinedSource.ShouldContain("Runtime_integration_lane_is_reserved_for_eventstore_state_store_evidence");
        combinedSource.ShouldContain("Performance_lane_is_reserved_for_launch_latency_evidence");
        combinedSource.ShouldContain("Skip =");
    }
}
