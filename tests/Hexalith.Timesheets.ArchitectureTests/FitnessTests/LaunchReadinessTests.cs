using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

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
    public void Launch_readiness_record_keeps_open_story_and_launch_work_explicit()
    {
        // AC1/AC2: incomplete story work cannot be reframed as a launch waiver.
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        readiness.ShouldContain("neither story-complete nor launch-complete");
        readiness.ShouldContain("Story 3.6 remains in progress");
        readiness.ShouldContain("Story 5.2 remains in progress");
        readiness.ShouldContain("final Story 5.1 reconciliation remains ready for development");
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
        readiness.ShouldContain("Works checkout ownership");
        // Assert the shape of the evidence, not its arithmetic. Pinning exact suite totals reddens
        // this lane on every test added anywhere in the solution — including one added to this very
        // project — and the cheapest way back to green is hand-editing the document, which inverts
        // the evidence discipline these assertions exist to enforce.
        readiness.ShouldMatch(@"\b\d{4}-\d{2}-\d{2}\b");
        readiness.ShouldContain("ArchitectureTests");
        readiness.ShouldContain("Projections.Tests");
        readiness.ShouldContain("Server.Tests");
        readiness.ShouldMatch(@"Final total: \d+ tests, \d+ pass, \d+ intentional skips, 0 failures");
    }

    [Fact]
    public void Launch_readiness_overall_decision_is_an_honest_verdict_not_a_vanity_pass()
    {
        // Open core stories and unfinished atomic magic-link persistence require FAIL, not a waiver.
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        int decisionStart = readiness.LastIndexOf("Overall release decision", StringComparison.Ordinal);
        decisionStart.ShouldBeGreaterThanOrEqualTo(0, "Launch-readiness record must declare an overall release decision.");

        string overall = readiness[decisionStart..];
        overall.ShouldContain("Overall release decision: **FAIL**");
        overall.ShouldContain("Story 3.6 and Story 5.2 remain in progress");
        overall.ShouldContain("final Story 5.1 reconciliation remains ready for development");
        overall.ShouldContain("durable atomic magic-link confirm/adjust submission is unfinished");
        overall.ShouldNotContain("decision: **PASS**");
        overall.ShouldNotContain("decision: PASS");

        string[] persistenceGate = ReadReleaseGateRow(readiness, "Magic-link durable confirm/adjust persistence");
        persistenceGate[1].ShouldBe("FAIL");
        persistenceGate[2].ShouldContain("not submitted to EventStore");
        persistenceGate[2].ShouldContain("do not prove durable capability use or Time Entry writes");
        persistenceGate[3].ShouldContain("concurrent requests can reuse the same unpersisted capability state");
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
    public void Launch_readiness_record_keeps_remaining_deferrals_honest_and_records_magic_link_wiring()
    {
        // Remaining launch deferrals stay visible, while the Story 3.6 projection delivery and valid
        // HTTP journey are no longer described by the obsolete unwired-index waiver.
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        readiness.ShouldContain("Live Works reference validation in host");
        readiness.ShouldContain("Magic-link live end-to-end resolution");
        readiness.ShouldContain("Export preview");
        readiness.ShouldContain("canonical token-hash index and tenant Activity Type catalog projection handlers");
        readiness.ShouldContain("all four valid confirm/adjust HTTP routes reach their expected in-process responses without direct index or catalog seeding");
        readiness.ShouldContain("Successful confirm/adjust POST results are not durably submitted to EventStore");
        readiness.ShouldContain("they are not persistence or single-use evidence");
        readiness.ShouldNotContain("no projection-host wiring");
        readiness.ShouldNotContain("Valid links do not resolve");
        readiness.ShouldContain("no dedicated HTTP route");

        string readme = File.ReadAllText(RepositoryRoot.PathTo("README.md"));
        readme.ShouldContain("discovers the canonical token-hash index and tenant catalog projection handlers");
        readme.ShouldContain("reaches all four confirm/adjust routes");
        readme.ShouldContain("neither read model was seeded directly");
        readme.ShouldNotContain("has no live projection-host wiring");
        readme.ShouldNotContain("A valid magic link still does not resolve");
    }

    [Fact]
    public void LaunchReadinessRecordsStructuredMagicLinkOwnershipAndTimingWaivers()
    {
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        readiness.ShouldContain("compound labels such as `implemented / waived`");

        string[] ownership = ReadClassificationRow(readiness, "Magic-link Activity Type ownership inventory");
        ownership[1].ShouldContain("issuance validates the catalog-selected tenant ownership");
        ownership[1].ShouldContain("does not pre-load the target Time Entry");
        ownership[1].ShouldContain("folded Time Entry scope is the authoritative use-time ownership gate");
        ownership[1].ShouldContain("duplicate and ownership-shape catalog checks are defence in depth");
        ownership[1].ShouldContain("zero-match presence gate remains required");
        ownership[2].ShouldBe("implemented / waived");
        ownership[3].ShouldBe("Story 3.6 / release owner");
        ownership[4].ShouldContain("project-owned capability");
        ownership[4].ShouldContain("reissuable");
        ownership[4].ShouldContain("only after an authorized correction");
        ownership[4].ShouldContain("followed by capability revocation and reissuance");
        ownership[4].ShouldContain("An Activity-Type-changing correction");
        ownership[4].ShouldContain("without revocation at the first Activity-Type change");
        ownership[4].ShouldContain("A-to-B-to-A");
        ownership[4].ShouldContain("ID reuse can revalidate an unused old link");
        ownership[4].ShouldContain("legacy scope-less Project-to-Tenant correction can retain Project scope");
        ownership[4].ShouldContain("legacy scope-less Tenant-to-Project correction can retain Tenant scope");
        ownership[4].ShouldContain("project-owned Activity Type ID absent from the tenant catalog");
        ownership[4].ShouldContain("token reissue alone");
        ownership[5].ShouldContain("Before rollout");
        ownership[5].ShouldContain("inventories deployed capability and TimeEntry histories");
        ownership[5].ShouldContain("authorizes correction");
        ownership[5].ShouldContain("then revokes and reissues");
        ownership[5].ShouldContain("revokes affected capabilities at the first Activity-Type change");
        ownership[5].ShouldContain("before any later ID reuse can revalidate an old link");
        ownership[5].ShouldContain("both legacy scope/Activity-Type mismatch shapes");

        string[] timing = ReadClassificationRow(readiness, "Magic-link invalid-token timing");
        timing[2].ShouldBe("implemented / waived");
        timing[3].ShouldBe("Story 3.6 / security");
        timing[1].ShouldContain("raw UTF-8 response length");
        timing[1].ShouldContain("zero read-model and EventStore I/O");
        timing[1].ShouldContain("one token-index lookup");
        timing[4].ShouldContain("not constant");
        timing[5].ShouldContain("public exposure without suitable abuse controls");
        timing[5].ShouldContain("token entropy is weakened");
        timing[5].ShouldContain("practically classified by timing");

        string[] liveResolution = ReadClassificationRow(readiness, "Magic-link live end-to-end resolution");
        liveResolution[1].ShouldContain("Successful confirm/adjust POST results are not durably submitted to EventStore");
        liveResolution[1].ShouldContain("not persistence or single-use evidence");
        liveResolution[4].ShouldContain("concurrent requests can reuse the same unpersisted capability state");
        liveResolution[5].ShouldContain("atomically submits capability-use and Time Entry events through EventStore");
        liveResolution[5].ShouldContain("concurrent reuse is rejected from authoritative persisted state");
    }

    [Fact]
    public void Launch_readiness_record_captures_package_currency_verdict_dimensions()
    {
        // Story 5.2: package evidence must distinguish direct package currency, root npm applicability,
        // transitive drift, and platform/submodule alignment so release readiness cannot overstate currency.
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));
        string packageVerdict = ReadSection(readiness, "## Package-Currency Verdict", "## Works Checkout Ownership");
        using JsonDocument globalJson = JsonDocument.Parse(File.ReadAllText(RepositoryRoot.PathTo("global.json")));
        JsonElement sdk = globalJson.RootElement.GetProperty("sdk");
        string sdkVersion = sdk.GetProperty("version").GetString().ShouldNotBeNull();
        string rollForward = sdk.GetProperty("rollForward").GetString().ShouldNotBeNull();

        XElement appHost = XDocument.Load(RepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets.AppHost",
            "Hexalith.Timesheets.AppHost.csproj")).Root.ShouldNotBeNull();
        string appHostSdk = appHost.Attribute("Sdk").ShouldNotBeNull().Value;
        const string appHostSdkPrefix = "Aspire.AppHost.Sdk/";
        appHostSdk.ShouldStartWith(appHostSdkPrefix);
        string appHostVersion = appHostSdk[appHostSdkPrefix.Length..];

        XDocument centralCatalog = XDocument.Load(RepositoryRoot.PathTo(
            "references",
            "Hexalith.Builds",
            "Props",
            "Directory.Packages.props"));
        string aspireVersion = GetPackageVersion(centralCatalog, "Aspire.Hosting");
        string daprVersion = GetPackageVersion(centralCatalog, "Dapr.Client");
        string keycloakVersion = GetPackageVersion(centralCatalog, "Aspire.Hosting.Keycloak");
        string communityToolkitDaprVersion = GetPackageVersion(centralCatalog, "CommunityToolkit.Aspire.Hosting.Dapr");
        string fluentUiVersion = GetPackageVersion(centralCatalog, "Microsoft.FluentUI.AspNetCore.Components");

        packageVerdict.ShouldContain("Package-Currency Verdict");
        packageVerdict.ShouldContain("Direct Timesheets NuGet package currency");
        packageVerdict.ShouldContain("Root npm applicability");
        packageVerdict.ShouldContain("Transitive drift");
        packageVerdict.ShouldContain("Platform and submodule alignment");
        packageVerdict.ShouldContain("no direct package updates");
        packageVerdict.ShouldContain("not applicable");
        packageVerdict.ShouldContain("reviewed, no pin");
        packageVerdict.ShouldContain("waived");
        packageVerdict.ShouldContain($"SDK `{sdkVersion}`");
        packageVerdict.ShouldContain($"`rollForward: {rollForward}`");
        packageVerdict.ShouldContain($"`Aspire.AppHost.Sdk` `{appHostVersion}`");
        packageVerdict.ShouldContain($"Aspire packages at stable `{aspireVersion}`");
        packageVerdict.ShouldContain($"Dapr family at stable `{daprVersion}`");
        packageVerdict.ShouldContain($"`Aspire.Hosting.Keycloak` `{keycloakVersion}`");
        packageVerdict.ShouldContain($"`CommunityToolkit.Aspire.Hosting.Dapr` `{communityToolkitDaprVersion}`");
        packageVerdict.ShouldContain($"Fluent UI V5 policy-surface catalog entry is `{fluentUiVersion}`");
        packageVerdict.ShouldContain("sprint-change-proposal-2026-09-12.md");
        packageVerdict.ShouldContain("`aspire start`");
        packageVerdict.ShouldContain("`security` resource reported `Healthy`");
        packageVerdict.ShouldContain("`aspire stop`");
        packageVerdict.ShouldContain("error: Sequence contains no matching element");
        packageVerdict.ShouldContain("this lane is not reported clean");
        packageVerdict.ShouldContain("without a compatibility, security, or deterministic-build reason");
    }

    [Fact]
    public void Launch_readiness_record_captures_works_checkout_ownership_evidence()
    {
        // Story 5.3: checkout evidence must name the umbrella references/ pin, forbid a root
        // gitlink, and stay honest that this is not live Works host integration.
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        readiness.ShouldContain("Works Checkout Ownership");
        readiness.ShouldContain("Default Works checkout");
        readiness.ShouldContain("Repository-root Works gitlink or probe");
        readiness.ShouldContain("references/Hexalith.Works");
        readiness.ShouldContain("<workspace>/references/Hexalith.Works");
        readiness.ShouldContain("forbidden");
        readiness.ShouldContain("does not claim live Works host integration");
        readiness.ShouldContain("Do not initialize `Hexalith.Timesheets/Hexalith.Works`");
    }

    private static string GetPackageVersion(XDocument centralCatalog, string packageId)
    {
        return centralCatalog.Descendants("PackageVersion")
            .Single(element => string.Equals(element.Attribute("Include")?.Value, packageId, StringComparison.Ordinal))
            .Attribute("Version")
            .ShouldNotBeNull()
            .Value;
    }

    private static string ReadSection(string document, string heading, string nextHeading)
    {
        int start = document.IndexOf(heading, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Missing current section '{heading}'.");

        int end = document.IndexOf(nextHeading, start + heading.Length, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, $"Missing section boundary '{nextHeading}'.");

        return document[start..end];
    }

    private static string[] ReadClassificationRow(string document, string item)
    {
        string row = document.Split('\n')
            .Single(line => line.StartsWith($"| {item} |", StringComparison.Ordinal));
        string[] delimitedCells = row.Split('|', StringSplitOptions.TrimEntries);
        delimitedCells[0].ShouldBeEmpty($"The '{item}' classification row must start with a table delimiter.");
        delimitedCells[^1].ShouldBeEmpty($"The '{item}' classification row must end with a table delimiter.");
        string[] cells = delimitedCells[1..^1];

        cells.Length.ShouldBe(7, $"The '{item}' classification must contain exactly seven structured columns, including empty cells.");
        cells[0].ShouldBe(item);
        return cells;
    }

    private static string[] ReadReleaseGateRow(string document, string gate)
    {
        string row = document.Split('\n')
            .Single(line => line.StartsWith($"| {gate} |", StringComparison.Ordinal));
        string[] delimitedCells = row.Split('|', StringSplitOptions.TrimEntries);
        delimitedCells[0].ShouldBeEmpty($"The '{gate}' release-gate row must start with a table delimiter.");
        delimitedCells[^1].ShouldBeEmpty($"The '{gate}' release-gate row must end with a table delimiter.");
        string[] cells = delimitedCells[1..^1];

        cells.Length.ShouldBe(4, $"The '{gate}' release gate must contain exactly four structured columns.");
        cells[0].ShouldBe(gate);
        return cells;
    }
}
