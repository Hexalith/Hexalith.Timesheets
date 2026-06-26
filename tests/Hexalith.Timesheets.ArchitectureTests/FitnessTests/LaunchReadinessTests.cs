using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

public sealed class LaunchReadinessTests
{
    [Fact]
    public void Launch_readiness_record_exists_and_declares_required_verdict_vocabulary()
    {
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        readiness.ShouldContain("implemented");
        readiness.ShouldContain("waived");
        readiness.ShouldContain("post-v1");
        readiness.ShouldContain("PASS");
        readiness.ShouldContain("CONCERNS");
        readiness.ShouldContain("FAIL");
        readiness.ShouldContain("WAIVED");
    }

    [Fact]
    public void Launch_readiness_record_requires_owner_risk_and_revisit_for_waivers()
    {
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        readiness.ShouldContain("Owner");
        readiness.ShouldContain("Risk");
        readiness.ShouldContain("Revisit condition");
    }

    [Fact]
    public void Launch_readiness_record_classifies_epic_named_launch_scope_items()
    {
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        readiness.ShouldContain("Unavailable defaults");
        readiness.ShouldContain("Skipped lanes");
        readiness.ShouldContain("Deferred integrations");
        readiness.ShouldContain("Legal-hold policy");
        readiness.ShouldContain("Comment sensitivity");
        readiness.ShouldContain("Export format");
        readiness.ShouldContain("Secondary magic-link identity verification");
        readiness.ShouldContain("Performance evidence");
    }

    [Fact]
    public void Launch_readiness_record_distinguishes_story_complete_from_launch_complete()
    {
        // AC1/AC2: the record's reason for being is to separate "the feature story is done" from
        // "the system is launch-ready". Guard the framing so an edit cannot collapse the two.
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        readiness.ShouldContain("story-complete");
        readiness.ShouldContain("launch-complete");
    }

    [Fact]
    public void Launch_readiness_record_anchors_evidence_to_a_baseline_commit_and_date()
    {
        // AC3: every verdict must be backed by traceable evidence (commit). The record must pin the
        // baseline commit SHA and the assessment date so the evidence set is anchored in time, not floating.
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        readiness.ShouldContain("baseline");
        Regex.IsMatch(readiness, "\\b[0-9a-f]{40}\\b")
            .ShouldBeTrue("Launch-readiness record must pin a 40-character baseline commit SHA.");
        Regex.IsMatch(readiness, "\\b20\\d{2}-\\d{2}-\\d{2}\\b")
            .ShouldBeTrue("Launch-readiness record must record the assessment date (ISO yyyy-MM-dd).");
    }

    [Fact]
    public void Launch_readiness_record_publishes_a_per_gate_release_decision_table()
    {
        // AC3: the record must carry a per-gate decision table covering every release gate, so a gate
        // cannot be quietly dropped from the readiness verdict.
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        readiness.ShouldContain("Release-Gate Decision Table");
        readiness.ShouldContain("Build");
        readiness.ShouldContain("Tests (full suite)");
        readiness.ShouldContain("Privacy/logging scans");
        readiness.ShouldContain("Projection rebuild/idempotency");
        readiness.ShouldContain("Export golden files");
        readiness.ShouldContain("Magic-link HTTP no-disclosure");
        readiness.ShouldContain("Tenant-isolation/security");
    }

    [Fact]
    public void Launch_readiness_overall_decision_is_an_honest_verdict_not_a_vanity_pass()
    {
        // The project's #1 recurring failure is overstatement. The whole point of Story 5.1 is to render
        // the honest verdict: with real launch-scope items waived, the overall decision MUST be CONCERNS
        // (or WAIVED if formally accepted) and must never silently flip to a vanity PASS.
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        int decisionStart = readiness.LastIndexOf("Overall release decision", StringComparison.Ordinal);
        decisionStart.ShouldBeGreaterThanOrEqualTo(0, "Launch-readiness record must declare an overall release decision.");

        string overall = readiness[decisionStart..];
        (overall.Contains("CONCERNS") || overall.Contains("WAIVED"))
            .ShouldBeTrue("Overall launch-readiness decision must be the honest CONCERNS/WAIVED verdict.");
        overall.ShouldNotContain("decision: **PASS**");
        overall.ShouldNotContain("decision: PASS");
        overall.ShouldNotContain("decision: **FAIL**");
        overall.ShouldNotContain("decision: FAIL");
    }

    [Fact]
    public void Launch_readiness_record_cross_links_related_evidence_documents()
    {
        // Task 2: the record must cross-link the sibling evidence docs so the release evidence set is
        // navigable and the performance/boundary evidence cannot be orphaned from the gate verdict.
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        readiness.ShouldContain("performance-evidence.md");
        readiness.ShouldContain("boundary-decision-record.md");
    }

    [Fact]
    public void Launch_readiness_record_keeps_deferred_integrations_marked_not_launch_active()
    {
        // AC2: the record must not let the host be described as launch-active for integrations that are
        // only story-complete. Guard the honest "not wired / not resolving / no route" caveats so a doc
        // edit cannot overstate live Works, valid magic-link end-to-end, or an export-preview HTTP route.
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        readiness.ShouldContain("Live Works reference validation in host");
        readiness.ShouldContain("Magic-link live end-to-end resolution");
        readiness.ShouldContain("Export preview");
        readiness.ShouldContain("no projection-host wiring"); // magic-link index not host-wired
        readiness.ShouldContain("no dedicated HTTP route"); // export preview has no HTTP endpoint
    }

    [Fact]
    public void Launch_readiness_record_captures_package_currency_verdict_dimensions()
    {
        // Story 5.2: package evidence must distinguish direct package currency, root npm applicability,
        // transitive drift, and platform/submodule alignment so release readiness cannot overstate currency.
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        readiness.ShouldContain("Package-Currency Verdict");
        readiness.ShouldContain("Direct Timesheets NuGet package currency");
        readiness.ShouldContain("Root npm applicability");
        readiness.ShouldContain("Transitive drift");
        readiness.ShouldContain("Platform and submodule alignment");
        readiness.ShouldContain("no direct package updates");
        readiness.ShouldContain("not applicable");
        readiness.ShouldContain("reviewed, no pin");
        readiness.ShouldContain("waived");
    }
}
