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

    [Fact]
    public void Performance_evidence_documents_capture_and_governance_command_verdict()
    {
        string evidence = File.ReadAllText(RepositoryRoot.PathTo("docs", "performance-evidence.md"));

        // NFR10 command-acknowledgement evidence must carry an explicit verdict, not regress to a pure placeholder.
        evidence.ShouldContain("NFR10");
        evidence.ShouldContain("command acknowledgement");
        evidence.ShouldContain("pass / concern / fail / waived");
        evidence.ShouldContain("Verdict: pass");
        evidence.ShouldContain("waived");

        // The measured capture/governance command performance lane must exist in the integration source.
        string[] integrationTests = Directory.GetFiles(
            RepositoryRoot.PathTo("tests", "Hexalith.Timesheets.IntegrationTests"),
            "*.cs",
            SearchOption.AllDirectories);

        string combinedSource = string.Join(Environment.NewLine, integrationTests.Select(File.ReadAllText));
        combinedSource.ShouldContain("CaptureAndGovernanceCommandPerformanceLaneTests");
    }

    [Fact]
    public void Command_performance_lane_stays_opt_in_and_out_of_the_fast_baseline()
    {
        string[] integrationTests = Directory.GetFiles(
            RepositoryRoot.PathTo("tests", "Hexalith.Timesheets.IntegrationTests"),
            "*.cs",
            SearchOption.AllDirectories);

        string combinedSource = string.Join(Environment.NewLine, integrationTests.Select(File.ReadAllText));

        // AC2: the measured timing lane must stay gated behind the TIMESHEETS_PERF env-var opt-in and
        // dynamically skip (Assert.Skip), so it cannot silently start running inside the fast
        // unit/architecture baseline. The existing "Skip =" check is satisfied by the static reserved
        // placeholders and does NOT protect this dynamic lane's gate.
        combinedSource.ShouldContain("TIMESHEETS_PERF");
        combinedSource.ShouldContain("Assert.Skip");
    }

    [Fact]
    public void Performance_evidence_documents_report_export_dashboard_nfr11_verdicts()
    {
        string evidence = File.ReadAllText(RepositoryRoot.PathTo("docs", "performance-evidence.md"));

        evidence.ShouldContain("NFR11");
        evidence.ShouldContain("Report, export, and dashboard query latency evidence");
        evidence.ShouldContain("report/export/dashboard");
        evidence.ShouldContain("Verdict:");
        evidence.ShouldContain("waived (deferred)");

        string[] integrationTests = Directory.GetFiles(
            RepositoryRoot.PathTo("tests", "Hexalith.Timesheets.IntegrationTests"),
            "*.cs",
            SearchOption.AllDirectories);

        string combinedSource = string.Join(Environment.NewLine, integrationTests.Select(File.ReadAllText));
        combinedSource.ShouldContain("ReportExportDashboardQueryPerformanceLaneTests");
    }

    [Fact]
    public void Report_export_dashboard_performance_lane_stays_opt_in()
    {
        string source = File.ReadAllText(RepositoryRoot.PathTo(
            "tests",
            "Hexalith.Timesheets.IntegrationTests",
            "ReportExportDashboardQueryPerformanceLaneTests.cs"));

        source.ShouldContain("private const string OptInVariable = \"TIMESHEETS_PERF\";");
        source.ShouldContain("Set {OptInVariable}=1 to run the report/export/dashboard performance lane.");
        source.ShouldContain("Assert.Skip");
        source.ShouldContain("Nfr11TargetMilliseconds = 2000.0");
    }

    [Fact]
    public void Performance_evidence_documents_how_to_run_the_opt_in_lane()
    {
        string evidence = File.ReadAllText(RepositoryRoot.PathTo("docs", "performance-evidence.md"));

        // AC2: the docs must explain how to run the performance lane and that it is skipped by default,
        // so the run instructions cannot silently regress away.
        evidence.ShouldContain("How to run the performance lane");
        evidence.ShouldContain("TIMESHEETS_PERF=1");
        evidence.ShouldContain("skipped by default");
    }

    [Fact]
    public void Report_export_dashboard_lane_emits_privacy_safe_nfr11_aggregates_only()
    {
        // AC5 / NFR12: the lane and its recorded evidence must surface timing aggregates and scenario
        // names only - never report rows, ledger rows, CSV content, comments, identifiers, or PII.
        // Guard the privacy commitment in the evidence doc so it cannot silently regress.
        string evidence = File.ReadAllText(RepositoryRoot.PathTo("docs", "performance-evidence.md"));

        evidence.ShouldContain("NFR12");
        evidence.ShouldContain("does not emit report rows");
        evidence.ShouldContain("personal data");

        // The lane only ever emits the aggregate-shaped table header (scenario name + p95/min/median/max),
        // so no per-row or payload content can reach ITestOutputHelper or the captured -xml report.
        string laneSource = File.ReadAllText(RepositoryRoot.PathTo(
            "tests",
            "Hexalith.Timesheets.IntegrationTests",
            "ReportExportDashboardQueryPerformanceLaneTests.cs"));

        laneSource.ShouldContain("Scenario | p95 (ms) | min (ms) | median (ms) | max (ms)");
    }

    [Fact]
    public void Report_export_dashboard_read_journey_has_fast_baseline_functional_coverage()
    {
        // AC3: the report/export/dashboard read paths the opt-in lane measures must also have
        // fast-baseline functional coverage so their composed correctness is guarded in the default
        // suite (not only behind TIMESHEETS_PERF=1) and cannot silently be deleted.
        string[] integrationTests = Directory.GetFiles(
            RepositoryRoot.PathTo("tests", "Hexalith.Timesheets.IntegrationTests"),
            "*.cs",
            SearchOption.AllDirectories);

        string combinedSource = string.Join(Environment.NewLine, integrationTests.Select(File.ReadAllText));
        combinedSource.ShouldContain("ReportExportDashboardReadJourneyE2ETests");
    }
}
