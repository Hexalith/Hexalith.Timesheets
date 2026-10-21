using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

public sealed class LaunchReadinessTests
{
    /// <summary>
    /// Verifies every current classification has a supported disposition and traceable approval for any waiver.
    /// </summary>
    [Fact]
    public void Launch_readiness_record_uses_only_approved_structured_classifications()
    {
        string readiness = ReadReadiness();
        string classification = ReadMarkdownSection(readiness, "## Launch-Scope Classification");
        string[] allowed = ["implemented", "unfinished", "waived", "post-v1"];
        string[] items = ["Unavailable defaults", "Skipped lanes", "Deferred integrations", "Legal-hold policy",
            "Tenant policy configuration", "Comment sensitivity", "Export format", "Secondary magic-link identity verification", "Performance evidence",
            "Live Works reference validation in host", "Live Works planned-effort reporting", "Magic-link HTTP no-disclosure",
            "Magic-link Activity Type ownership inventory", "Magic-link invalid-token timing", "Magic-link live end-to-end resolution",
            "Export preview", "Time-zone and period policy", "NFR13 UI behavior", "Production deployment evidence",
            "External stakeholder acceptance", "Works staleness detection", "Platform prerelease dependency exception",
            "Doc-vs-test-project divergence"];
        string[] actual = classification.Split('\n')
            .Where(line => line.StartsWith("| ", StringComparison.Ordinal) && !line.StartsWith("| Item |", StringComparison.Ordinal))
            .Select(line => line.Split('|')[1].Trim()).ToArray();
        actual.Order().ShouldBe(items.Order());
        foreach (string item in items)
        {
            string[] row = ReadClassificationRow(readiness, item);
            foreach (string cell in row)
            {
                cell.ShouldNotBeNullOrWhiteSpace($"'{item}' must populate every classification field.");
            }

            string[] dispositions = row[2].Split(" / ", StringSplitOptions.TrimEntries);
            dispositions.Distinct().Count().ShouldBe(dispositions.Length);
            foreach (string disposition in dispositions)
            {
                allowed.ShouldContain(disposition);
            }

            if (dispositions.Contains("waived", StringComparer.Ordinal))
            {
                ValidateExistingWaiver(item, row);
            }
        }
    }

    /// <summary>
    /// Keeps unfinished v1 and deployed acceptance visible independently of implemented local controls.
    /// </summary>
    [Fact]
    public void Unfinished_v1_acceptance_cannot_be_relabelled_as_a_waiver()
    {
        string readiness = ReadReadiness();
        string[] live = ReadClassificationRow(readiness, "Magic-link live end-to-end resolution");
        live[2].ShouldBe("unfinished");
        live[1].ShouldContain("FR14 remains in v1 scope");
        live[1].ShouldContain("canonical token-hash index and tenant Activity Type catalog projection handlers");
        live[1].ShouldContain("valid confirm/adjust POSTs return 202 only after Completed EventStore status and exact stored-event readback");
        live[4].ShouldContain("does not establish deployed Dapr persistence");
        live[4].ShouldContain("Current NuGet-orchestration runtime qualification is unfinished");
        live[5].ShouldContain("Story 3.6");
        ReadClassificationRow(readiness, "Legal-hold policy")[2].ShouldBe("unfinished");
        ReadClassificationRow(readiness, "Tenant policy configuration")[2].ShouldBe("unfinished");
        ReadClassificationRow(readiness, "Production deployment evidence")[2].ShouldBe("unfinished");
        ReadClassificationRow(readiness, "External stakeholder acceptance")[2].ShouldBe("unfinished");
        ReadClassificationRow(readiness, "Export preview")[1].ShouldContain("no dedicated HTTP route");
        ReadClassificationRow(readiness, "Live Works reference validation in host")[1].ShouldContain("DenyAllWorkReferenceValidator");
        ReadClassificationRow(readiness, "Live Works planned-effort reporting")[1].ShouldContain("UnavailableWorkPlannedEffortProvider");
        string current = ReadCurrentAssessment(readiness);
        current.ShouldContain("neither story-complete nor launch-complete");
        current.ShouldContain("Overall release decision: **FAIL**");
        current.ShouldContain("historical local proof");
        current.ShouldContain("current NuGet-orchestration AppHost graph has no passing runtime qualification");
    }

    /// <summary>
    /// Verifies dated source scope, complete gates, and exact arithmetic for all six recorded suites.
    /// </summary>
    [Fact]
    public void Current_evidence_has_dated_full_revisions_and_all_release_gates()
    {
        string readiness = ReadReadiness();
        string current = ReadCurrentAssessment(readiness);
        current.ShouldMatch(@"\b20\d{2}-\d{2}-\d{2}\b");
        Regex.Matches(current, @"\b[0-9a-f]{40}\b").Count.ShouldBeGreaterThanOrEqualTo(4);
        current.ShouldContain("performance-evidence.md");
        current.ShouldContain("boundary-decision-record.md");
        string[] gates = ["Build", "Package currency", "Tests (full suite)", "Privacy/logging scans",
            "Projection rebuild/idempotency", "Export golden files", "Magic-link HTTP no-disclosure",
            "Magic-link durable confirm/adjust persistence", "Performance evidence", "Legal-hold sign-off", "Tenant-policy configuration", "Tenant-isolation/security", "Works checkout ownership"];
        foreach (string gate in gates)
        {
            string[] row = ReadReleaseGateRow(readiness, gate);
            foreach (string cell in row)
            {
                cell.ShouldNotBeNullOrWhiteSpace();
            }

            foreach (string verdict in row[1].Split(" / ", StringSplitOptions.TrimEntries))
            {
                new[] { "PASS", "CONCERNS", "FAIL", "WAIVED" }.ShouldContain(verdict);
            }
        }

        string summary = File.ReadAllText(RepositoryRoot.PathTo("_bmad-output/implementation-artifacts/tests/5-1-test-summary.md"));
        using JsonDocument counts = ReadEvidenceJson("counts.json");
        ValidatePublishedCounts(readiness, summary, counts.RootElement, ReadExecutionRecords());
        ValidateFailedAttempts();
        foreach (string gate in new[] { "Legal-hold sign-off", "Tenant-policy configuration" })
        {
            string[] row = ReadReleaseGateRow(readiness, gate);
            row[1].ShouldBe("FAIL");
            row[2].ShouldContain("Owner:");
            row[3].ShouldContain("Acceptance evidence required:");
        }

        string[] persistence = ReadReleaseGateRow(readiness, "Magic-link durable confirm/adjust persistence");
        persistence[1].ShouldBe("FAIL");
        persistence[2].ShouldContain("exact stored use/effect batches");
        persistence[2].ShouldContain("actor recreation");
        persistence[3].ShouldContain("Deployed Dapr persistence");
    }

    /// <summary>
    /// Enforces consumer-host rendering and accessibility ownership without Timesheets UI projects.
    /// </summary>
    [Fact]
    public void Consumer_host_owns_rendering_and_accessibility_without_timesheets_ui_projects()
    {
        string[] paths = ["README.md", "_bmad-output/planning-artifacts/architecture.md",
            "_bmad-output/planning-artifacts/epics.md", "_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md",
            "_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md",
            "_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md"];
        foreach (string path in paths)
        {
            string document = File.ReadAllText(RepositoryRoot.PathTo(path));
            document.ShouldContain("consuming FrontComposer host");
            document.ShouldContain("Fluent UI V5");
            document.ShouldContain("WCAG");
            document.ShouldNotContain("first UI-bearing story");
            document.ShouldNotContain("UI/Composition");
            document.ShouldNotContain("Hexalith.Timesheets.UI/wwwroot");
        }

        Directory.EnumerateFiles(RepositoryRoot.PathTo("src"), "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(RepositoryRoot.PathTo("tests"), "*.csproj", SearchOption.AllDirectories))
            .Select(Path.GetFileNameWithoutExtension).ShouldNotContain(name => name!.StartsWith("Hexalith.Timesheets.UI", StringComparison.Ordinal));
        string[] ui = ReadClassificationRow(ReadReadiness(), "NFR13 UI behavior");
        ui[2].ShouldBe("post-v1");
        ui[3].ShouldContain("FrontComposer host");
        ui[5].ShouldContain("do not scaffold Timesheets UI projects");
    }

    /// <summary>
    /// Verifies resolved product decisions and policy-conditional external comment disclosure.
    /// </summary>
    [Fact]
    public void Prd_decisions_and_external_comment_disclosure_match_the_approved_scope()
    {
        string prd = File.ReadAllText(RepositoryRoot.PathTo("_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md"));
        string questions = ReadMarkdownSection(prd, "## 14. Open Questions");
        foreach (string question in new[] { "Q6", "Q7", "Q8" })
        {
            questions.Split('\n').Single(line => line.Contains($"({question})", StringComparison.Ordinal))
                .ShouldContain("[RESOLVED 2026-09-12]");
        }

        ReadClassificationRow(ReadReadiness(), "Comment sensitivity")[1].ShouldContain("missing trust-bearing policy fails closed");
        ReadClassificationRow(ReadReadiness(), "Export format")[1].ShouldContain("CSV v1");
        string epics = File.ReadAllText(RepositoryRoot.PathTo("_bmad-output/planning-artifacts/epics.md"));
        string story = File.ReadAllText(RepositoryRoot.PathTo("_bmad-output/implementation-artifacts/3-3-confirm-time-through-magic-link.md"));
        string experience = File.ReadAllText(RepositoryRoot.PathTo("_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md"));
        string design = File.ReadAllText(RepositoryRoot.PathTo("_bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md"));
        // The condition must be in each active disclosure criterion/journey, not elsewhere in the document.
        foreach (string criterion in new[]
        {
            epics.Split('\n').Single(line => line.StartsWith("UX-DR31:", StringComparison.Ordinal)),
            ReadMarkdownSection(epics, "### Story 3.3: Confirm Time Through Magic Link").Split('\n')
                .Single(line => line.StartsWith("**And** shows only", StringComparison.Ordinal)),
            ReadMarkdownSection(prd, "### 3.3 Key User Journeys").Split('\n')
                .Single(line => line.StartsWith("- **UJ-3.", StringComparison.Ordinal)),
            ReadMarkdownSection(prd, "#### FR-14: Support Magic-Link Confirmation").Split('\n')
                .Single(line => line.StartsWith("- Valid-link details", StringComparison.Ordinal)),
            ReadMarkdownSection(story, "## Acceptance Criteria").Split('\n')
                .Single(line => line.StartsWith("1.", StringComparison.Ordinal)),
            ReadMarkdownSection(experience, "### UJ-3. Simon confirms supplier time through Magic-Link Confirmation.").Split('\n')
                .Single(line => line.StartsWith("3.", StringComparison.Ordinal)),
            experience.Split('\n').Single(line => line.StartsWith("| Magic link valid |", StringComparison.Ordinal)),
            design.Split('\n').Single(line => line.StartsWith("- External magic-link confirmation is", StringComparison.Ordinal)),
        })
        {
            ValidateDisclosureCriterion(criterion);
        }

    }

    /// <summary>
    /// Keeps active story records and readiness tracking consistent without closing unfinished prerequisites.
    /// </summary>
    [Fact]
    public void Current_story_records_match_sprint_tracking_and_leave_prerequisites_open()
    {
        string sprint = File.ReadAllText(RepositoryRoot.PathTo("_bmad-output/implementation-artifacts/sprint-status.yaml"));
        string readiness = ReadReadiness();
        string[] keys = ["3-3-confirm-time-through-magic-link", "3-6-implement-eventstore-backed-magic-link-state-loading",
            "3-7-prove-magic-link-no-disclosure-at-the-http-boundary", "4-7-surface-timesheets-dashboard-overview",
            "5-1-final-launch-readiness-gate-and-documentation-sync", "5-2-reconcile-package-currency-and-platform-dependency-versions"];
        foreach (string key in keys)
        {
            string status = ReadTrackingStatus(sprint, key);
            string story = File.ReadAllText(RepositoryRoot.PathTo($"_bmad-output/implementation-artifacts/{key}.md"));
            ReadStoryStatus(story).ShouldBe(status);
            ReadFourColumnRow(ReadCurrentAssessment(readiness), key, "story-tracking")[1].ShouldBe(status);
        }

        using JsonDocument acceptance = ReadEvidenceJson("story-acceptance.json");
        ValidatePrerequisiteStatuses(sprint, ReadClassificationRow(readiness, "Magic-link live end-to-end resolution")[2] == "unfinished", acceptance.RootElement);

    }

    /// <summary>
    /// Verifies both line endings, scoped human acceptance, and independent package completion while protecting unfinished FR14.
    /// </summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Tracking_readers_accept_both_line_endings_and_independent_package_completion(string newline)
    {
        const string package = "5-2-reconcile-package-currency-and-platform-dependency-versions";
        string sprint = File.ReadAllText(RepositoryRoot.PathTo("_bmad-output/implementation-artifacts/sprint-status.yaml"))
            .Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", newline, StringComparison.Ordinal);
        const string reconciliation = "5-1-final-launch-readiness-gate-and-documentation-sync";
        sprint = Regex.Replace(sprint, $@"(?m)^  {reconciliation}: [a-z-]+(?=\r?$)", $"  {reconciliation}: review");
        string story = File.ReadAllText(RepositoryRoot.PathTo($"_bmad-output/implementation-artifacts/{package}.md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", newline, StringComparison.Ordinal);
        ReadStoryStatus(story).ShouldBe(ReadTrackingStatus(sprint, package));
        string completedPackage = Regex.Replace(sprint, $@"(?m)^  {package}: [a-z-]+(?=\r?$)", $"  {package}: done");
        ReadTrackingStatus(completedPackage, package).ShouldBe("done");
        ValidatePrerequisiteStatuses(completedPackage, unfinishedFr14: true);
        string acceptedReconciliation = Regex.Replace(completedPackage, $@"(?m)^  {reconciliation}: review(?=\r?$)", $"  {reconciliation}: done");
        ReadTrackingStatus(acceptedReconciliation, reconciliation).ShouldBe("done");
        Should.Throw<ShouldAssertException>(() => ValidatePrerequisiteStatuses(acceptedReconciliation, unfinishedFr14: true));
        using JsonDocument acceptance = ReadEvidenceJson("story-acceptance.json");
        ValidatePrerequisiteStatuses(acceptedReconciliation, unfinishedFr14: true, acceptance.RootElement);
        JsonNode broadened = JsonNode.Parse(acceptance.RootElement.GetRawText()).ShouldNotBeNull();
        broadened["release_authorized"] = true;
        using JsonDocument releaseApproval = JsonDocument.Parse(broadened.ToJsonString());
        Should.Throw<ShouldAssertException>(() => ValidatePrerequisiteStatuses(acceptedReconciliation, unfinishedFr14: true, releaseApproval.RootElement));
        broadened["release_authorized"] = false;
        broadened["scope"] = "product-release";
        using JsonDocument wrongScope = JsonDocument.Parse(broadened.ToJsonString());
        Should.Throw<ShouldAssertException>(() => ValidatePrerequisiteStatuses(acceptedReconciliation, unfinishedFr14: true, wrongScope.RootElement));
        string closedFr14Owner = Regex.Replace(completedPackage,
            @"(?m)^  3-6-implement-eventstore-backed-magic-link-state-loading: [a-z-]+(?=\r?$)",
            "  3-6-implement-eventstore-backed-magic-link-state-loading: done");
        Should.Throw<ShouldAssertException>(() => ValidatePrerequisiteStatuses(closedFr14Owner, unfinishedFr14: true));
        Should.Throw<ShouldAssertException>(() => ValidatePrerequisiteStatuses(closedFr14Owner, unfinishedFr14: true, acceptance.RootElement));
    }

    /// <summary>
    /// Verifies evidence guards reject changed counts, inconsistent outcomes, and unrelated approved records.
    /// </summary>
    [Fact]
    public void Evidence_checks_reject_invented_counts_mismatched_outcomes_and_unrelated_approvals()
    {
        string readiness = ReadReadiness();
        string summary = File.ReadAllText(RepositoryRoot.PathTo("_bmad-output/implementation-artifacts/tests/5-1-test-summary.md"));
        using JsonDocument counts = ReadEvidenceJson("counts.json");
        Dictionary<string, JsonElement> executions = ReadExecutionRecords();
        const string project = "Hexalith.Timesheets.Contracts.Tests";
        var suiteCounts = ReadCounts(counts.RootElement.GetProperty("suites").GetProperty(project));
        var aggregate = ReadCounts(counts.RootElement.GetProperty("totals"));
        string suiteRow = ReadMarkdownSection(summary, "## Current Suite Results").Split('\n')
            .Single(line => line.StartsWith($"| {project} |", StringComparison.Ordinal));
        string invented = MutateText(summary, suiteRow,
            $"| {project} | {suiteCounts.Passed + 1} | {suiteCounts.Failed} | {suiteCounts.Skipped} | {suiteCounts.Total + 1} |");
        Should.Throw<ShouldAssertException>(() => ValidatePublishedCounts(readiness, invented, counts.RootElement, executions));
        string incorrectReadiness = MutateText(readiness,
            $"Contracts.Tests {suiteCounts.Passed}/{suiteCounts.Failed}/{suiteCounts.Skipped}",
            $"Contracts.Tests {suiteCounts.Passed + 1}/{suiteCounts.Failed}/{suiteCounts.Skipped}");
        Should.Throw<ShouldAssertException>(() => ValidatePublishedCounts(incorrectReadiness, summary, counts.RootElement, executions));
        string totalRow = ReadMarkdownSection(summary, "## Current Suite Results").Split('\n')
            .Single(line => line.StartsWith("| **Total** |", StringComparison.Ordinal));
        string incorrectAggregate = MutateText(summary, totalRow,
            $"| **Total** | **{aggregate.Passed + 1}** | **{aggregate.Failed}** | **{aggregate.Skipped}** | **{aggregate.Total + 1}** |");
        Should.Throw<ShouldAssertException>(() => ValidatePublishedCounts(readiness, incorrectAggregate, counts.RootElement, executions));
        string incorrectReadinessAggregate = MutateText(readiness,
            $"Total: {aggregate.Passed} passed, {aggregate.Failed} failed, {aggregate.Skipped} skipped ({aggregate.Total} cases)",
            $"Total: {aggregate.Passed + 1} passed, {aggregate.Failed} failed, {aggregate.Skipped} skipped ({aggregate.Total + 1} cases)");
        Should.Throw<ShouldAssertException>(() => ValidatePublishedCounts(incorrectReadinessAggregate, summary, counts.RootElement, executions));
        JsonNode wrongCounts = JsonNode.Parse(counts.RootElement.GetRawText()).ShouldNotBeNull();
        wrongCounts["totals"]!["passed"] = aggregate.Passed + 1;
        wrongCounts.ToJsonString().ShouldNotBe(JsonNode.Parse(counts.RootElement.GetRawText()).ShouldNotBeNull().ToJsonString());
        using JsonDocument wrongTotals = JsonDocument.Parse(wrongCounts.ToJsonString());
        Should.Throw<ShouldAssertException>(() => ValidatePublishedCounts(readiness, summary, wrongTotals.RootElement, executions));
        string report = counts.RootElement.GetProperty("suites").GetProperty(project)
            .GetProperty("compressed_receipt").GetString().ShouldNotBeNull();
        JsonNode wrongOutcome = JsonNode.Parse(executions[report].GetRawText()).ShouldNotBeNull();
        wrongOutcome["exit_code"] = executions[report].GetProperty("exit_code").GetInt32() == 0 ? 1 : 0;
        wrongOutcome.ToJsonString().ShouldNotBe(JsonNode.Parse(executions[report].GetRawText()).ShouldNotBeNull().ToJsonString());
        using JsonDocument wrongExit = JsonDocument.Parse(wrongOutcome.ToJsonString());
        executions[report] = wrongExit.RootElement;
        Should.Throw<ShouldAssertException>(() => ValidatePublishedCounts(readiness, summary, counts.RootElement, executions));
        string[] waiver = ReadClassificationRow(readiness, "Unavailable defaults");
        string unrelatedApproval = File.ReadAllText(RepositoryRoot.PathTo("_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-26.md"));
        Regex.IsMatch(unrelatedApproval, @"(?m)^status: approved\r?$").ShouldBeTrue();
        waiver[6] = "Approval: [approved unrelated record](../_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-26.md#3-recommended-approach), carrying forward the existing named waiver only.";
        Should.Throw<ShouldAssertException>(() => ValidateExistingWaiver("Unavailable defaults", waiver));
        Should.Throw<ShouldAssertException>(() => ValidateDisclosureCriterion("Display date, duration, Activity Type and comment."));
    }

    /// <summary>
    /// Verifies synchronized guidance reflects executable configuration and registered projection delivery.
    /// </summary>
    [Fact]
    public void Managed_entry_points_match_configuration_and_projection_wiring()
    {
        string[] paths = ["AGENTS.md", "CLAUDE.md", ".github/copilot-instructions.md"];
        string[] documents = paths.Select(path => File.ReadAllText(RepositoryRoot.PathTo(path))).ToArray();
        string normalized = Regex.Replace(documents[0], @"\s+", " ").Trim();
        using JsonDocument globalJson = JsonDocument.Parse(File.ReadAllText(RepositoryRoot.PathTo("global.json")));
        foreach (string document in documents)
        {
            Regex.Replace(document, @"\s+", " ").Trim().ShouldBe(normalized);
            string managed = document[document.IndexOf("<!-- bmad:context -->", StringComparison.Ordinal)..];
            managed.ShouldContain($"`{globalJson.RootElement.GetProperty("sdk").GetProperty("version").GetString()}`");
            managed.ShouldContain(".github/workflows/");
            managed.ShouldContain("all six built xUnit v3 executables individually");
            managed.ShouldContain("**FAIL**");
            managed.ShouldContain("HTTP host registers the projection assembly and EventStore processing");
            managed.ShouldNotContain("has no projection-host wiring");
            managed.ShouldContain("`references/Hexalith.Works` is the sole default checkout");
            managed.ShouldContain("preserves an explicit `HexalithWorksRoot`");
            managed.ShouldContain("`references/Hexalith.Works` first and `../Hexalith.Works` second");
            managed.ShouldContain("Never initialize a repository-root `Hexalith.Works` checkout");
        }
    }

    /// <summary>
    /// Preserves ownership and timing risks while distinguishing controls from unapproved acceptance.
    /// </summary>
    [Fact]
    public void LaunchReadinessRetainsMagicLinkOwnershipAndTimingRisksWithoutInventingApproval()
    {
        string readiness = ReadReadiness();
        string[] ownership = ReadClassificationRow(readiness, "Magic-link Activity Type ownership inventory");
        ownership[1].ShouldContain("issuance validates the catalog-selected tenant ownership");
        ownership[1].ShouldContain("does not pre-load the target Time Entry");
        ownership[1].ShouldContain("folded Time Entry scope is the authoritative use-time ownership gate");
        ownership[1].ShouldContain("duplicate and ownership-shape catalog checks are defence in depth");
        ownership[1].ShouldContain("zero-match presence gate remains required");
        ownership[2].ShouldBe("implemented / unfinished");
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
        timing[2].ShouldBe("implemented / unfinished");
        timing[3].ShouldBe("Story 3.6 / security");
        timing[1].ShouldContain("raw UTF-8 response length");
        timing[1].ShouldContain("zero read-model and EventStore I/O");
        timing[1].ShouldContain("one token-index lookup");
        timing[4].ShouldContain("not constant");
        timing[5].ShouldContain("public exposure without suitable abuse controls");
        timing[5].ShouldContain("token entropy is weakened");
        timing[5].ShouldContain("practically classified by timing");

        string current = ReadCurrentAssessment(readiness);
        current.ShouldNotContain("no projection-host wiring");
        string readme = File.ReadAllText(RepositoryRoot.PathTo("README.md"));
        readme.ShouldContain("discovers the canonical token-hash index and tenant catalog projection handlers");
        readme.ShouldContain("reaches all four confirm/adjust routes");
        readme.ShouldContain("neither read model was seeded directly");
    }

    [Fact]
    public void Launch_readiness_record_captures_package_currency_verdict_dimensions()
    {
        // Story 5.2: package evidence must distinguish direct package currency, root npm applicability,
        // transitive drift, and platform/submodule alignment so release readiness cannot overstate currency.
        // Assertions are scoped to the current verdict paragraph and its table rows; dated historical
        // paragraphs in the same section cannot satisfy them.
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));
        string packageVerdict = ReadMarkdownSection(readiness, "## Package-Currency Verdict");
        string currentVerdict = ReadParagraph(packageVerdict, "Current package-currency verdict");
        using JsonDocument globalJson = JsonDocument.Parse(File.ReadAllText(RepositoryRoot.PathTo("global.json")));
        JsonElement sdk = globalJson.RootElement.GetProperty("sdk");
        string sdkVersion = sdk.GetProperty("version").GetString().ShouldNotBeNull();
        string rollForward = sdk.GetProperty("rollForward").GetString().ShouldNotBeNull();
        string appHostVersion = ReadAppHostSdkVersion();

        XDocument centralCatalog = LoadImportedPackageCatalog();
        string aspireVersion = GetPackageVersion(centralCatalog, "Aspire.Hosting");
        string daprVersion = GetPackageVersion(centralCatalog, "Dapr.Client");
        string keycloakVersion = GetPackageVersion(centralCatalog, "Aspire.Hosting.Keycloak");
        string communityToolkitDaprVersion = GetPackageVersion(centralCatalog, "CommunityToolkit.Aspire.Hosting.Dapr");
        string fluentUiVersion = GetPackageVersion(centralCatalog, "Microsoft.FluentUI.AspNetCore.Components");

        // Anchored to the baseline sentence: the approval-evidence sentence also names both versions.
        currentVerdict.ShouldContain(
            $"baseline is .NET SDK `{sdkVersion}` (`rollForward: {rollForward}`) and `Aspire.AppHost.Sdk` `{appHostVersion}`");
        currentVerdict.ShouldContain("`AspireUseCliBundle=false`");
        currentVerdict.ShouldContain("`ASPIRE010`");
        currentVerdict.ShouldContain("sprint-change-proposal-2026-09-12.md");
        currentVerdict.ShouldContain("so it does not validate the current restored graph");
        currentVerdict.ShouldContain("Current runtime-smoke evidence remains deferred");

        string[] direct = ReadPackageVerdictRow(packageVerdict, "Direct Timesheets NuGet package currency");
        direct[1].ShouldBe("PASS");
        direct[2].ShouldContain("no Timesheets-owned direct package updates");
        direct[2].ShouldContain($"`{appHostVersion}`");

        string[] vulnerable = ReadPackageVerdictRow(packageVerdict, "Vulnerable and deprecated NuGet packages");
        vulnerable[1].ShouldBe("PASS");
        vulnerable[2].ShouldContain("--vulnerable --include-transitive");
        vulnerable[2].ShouldContain("--deprecated --include-transitive");

        // Root npm is applicable exactly when the Timesheets root carries an npm manifest.
        string[] npm = ReadPackageVerdictRow(packageVerdict, "Root npm applicability");
        if (File.Exists(RepositoryRoot.PathTo("package.json")))
        {
            npm[1].ShouldStartWith("applicable");
            npm[2].ShouldContain("`npm outdated`");
            npm[2].ShouldContain("`npm audit`");
            npm[3].ShouldContain("Owner:");
            npm[3].ShouldContain("Risk:");
            npm[3].ShouldContain("Revisit condition:");
        }
        else
        {
            npm[1].ShouldBe("not applicable");
        }

        string[] transitive = ReadPackageVerdictRow(packageVerdict, "Transitive drift");
        transitive[1].ShouldStartWith("reviewed, no pin");
        transitive[2].ShouldContain("error: Sequence contains no matching element");
        transitive[2].ShouldContain("this lane is not reported clean");
        transitive[3].ShouldContain("without a compatibility, security, or deterministic-build reason");

        string[] platform = ReadPackageVerdictRow(packageVerdict, "Platform and submodule alignment");
        platform[1].ShouldBe("waived / open divergence");
        platform[2].ShouldContain($"SDK `{sdkVersion}`");
        platform[2].ShouldContain($"AppHost SDK `{appHostVersion}`");
        platform[2].ShouldContain($"Aspire packages at stable `{aspireVersion}`");
        platform[2].ShouldContain($"Dapr family at stable `{daprVersion}`");
        platform[2].ShouldContain($"`Aspire.Hosting.Keycloak` `{keycloakVersion}`");
        platform[2].ShouldContain($"`CommunityToolkit.Aspire.Hosting.Dapr` `{communityToolkitDaprVersion}`");
        platform[2].ShouldContain($"Fluent UI V5 policy-surface catalog entry is `{fluentUiVersion}`");
        platform[3].ShouldContain("Owner:");
        platform[3].ShouldContain("sprint-change-proposal-2026-09-12.md");
        platform[3].ShouldContain("Risk:");
        platform[3].ShouldContain("Revisit condition:");

        // The launch-scope waiver row must name the same current prerelease lines as the verdict table.
        string[] prerelease = ReadClassificationRow(readiness, "Platform prerelease dependency exception");
        prerelease[1].ShouldContain($"Aspire `{aspireVersion}`");
        prerelease[1].ShouldContain($"Dapr `{daprVersion}`");
        prerelease[1].ShouldContain($"Keycloak `{keycloakVersion}`");
        prerelease[1].ShouldContain($"CommunityToolkit Aspire Dapr `{communityToolkitDaprVersion}`");
        prerelease[1].ShouldContain($"Fluent UI V5 policy-surface catalog entry is `{fluentUiVersion}`");
        prerelease[2].ShouldBe("waived");

        string[] packageGate = ReadReleaseGateRow(readiness, "Package currency");
        packageGate[2].ShouldContain($"SDK `{sdkVersion}`");
        packageGate[2].ShouldContain($"`Aspire.AppHost.Sdk` `{appHostVersion}`");

        string historicalSmoke = ReadSection(
            readiness,
            "**Historical live `aspire start` evidence",
            "**Residual risk:**");
        historicalSmoke.ShouldContain("predates the current Builds catalog");
        historicalSmoke.ShouldContain("The split\nwas therefore verified end to end for that dependency graph");
        historicalSmoke.ShouldContain("not current Aspire/Keycloak runtime compatibility evidence");

        string[] buildGate = ReadReleaseGateRow(readiness, "Build");
        buildGate[1].ShouldBe("PASS");
        buildGate[2].ShouldContain("--configuration Debug --no-restore -p:UseHexalithProjectReferences=true");
        buildGate[2].ShouldContain("0 warnings and 0 errors");
        buildGate[3].ShouldContain("not current-graph runtime evidence");
        buildGate[3].ShouldContain("current automated AppHost smoke remains deferred");
    }

    /// <summary>
    /// Verifies the documented invariant that the Timesheets-owned AppHost SDK follows the imported catalog's
    /// canonical AppHost SDK contract and, per the platform's consumer rule, its <c>Aspire.Hosting</c> version, so
    /// the SDK-added AppHost, orchestration, and dashboard packages cannot mix with a second Aspire hosting line.
    /// </summary>
    [Fact]
    public void AppHostSdkTracksTheImportedStableAspireCatalogLine()
    {
        string appHostVersion = ReadAppHostSdkVersion();
        XDocument centralCatalog = LoadImportedPackageCatalog();
        string catalogAppHostSdkVersion = GetCatalogProperty(centralCatalog, "HexalithAspireAppHostSdkVersion");

        appHostVersion.ShouldBe(
            catalogAppHostSdkVersion,
            "Align Aspire.AppHost.Sdk with the imported catalog's HexalithAspireAppHostSdkVersion in the same change.");
        appHostVersion.ShouldBe(
            GetPackageVersion(centralCatalog, "Aspire.Hosting"),
            "Aspire.AppHost.Sdk must equal the imported Aspire.Hosting version, or the restored AppHost graph mixes two Aspire lines.");
    }

    /// <summary>
    /// Verifies that architecture version notes and the restated version lines listed here match the executable
    /// SDK, AppHost, and imported-catalog pins.
    /// </summary>
    [Fact]
    public void ArchitectureVersionNotesMatchExecutableConfiguration()
    {
        string architecture = File.ReadAllText(RepositoryRoot.PathTo(
            "_bmad-output",
            "planning-artifacts",
            "architecture.md"));
        string versionNotes = ReadSection(
            ReadMarkdownSection(architecture, "### Data Architecture"),
            "**Version notes:**",
            "**Aggregate boundaries:**");
        using JsonDocument globalJson = JsonDocument.Parse(File.ReadAllText(RepositoryRoot.PathTo("global.json")));
        JsonElement sdk = globalJson.RootElement.GetProperty("sdk");
        string appHostVersion = ReadAppHostSdkVersion();
        XDocument centralCatalog = LoadImportedPackageCatalog();

        versionNotes.ShouldContain($"pinned to `{sdk.GetProperty("version").GetString()}`");
        versionNotes.ShouldContain($"`rollForward: {sdk.GetProperty("rollForward").GetString()}`");
        versionNotes.ShouldContain($"aligned at stable `{appHostVersion}`");
        versionNotes.ShouldContain("`AspireUseCliBundle=false`");
        versionNotes.ShouldContain("`ASPIRE010`");
        versionNotes.ShouldContain($"family at stable `{GetPackageVersion(centralCatalog, "Dapr.Client")}`");
        versionNotes.ShouldContain(
            $"V5 package family at `{GetPackageVersion(centralCatalog, "Microsoft.FluentUI.AspNetCore.Components")}`");
        versionNotes.ShouldContain($"`Aspire.Hosting.Keycloak` `{GetPackageVersion(centralCatalog, "Aspire.Hosting.Keycloak")}`");
        versionNotes.ShouldContain(
            $"`CommunityToolkit.Aspire.Hosting.Dapr` `{GetPackageVersion(centralCatalog, "CommunityToolkit.Aspire.Hosting.Dapr")}`");
        versionNotes.ShouldNotContain("AspireUseCliBundle=true");

        string sdkVersion = sdk.GetProperty("version").GetString().ShouldNotBeNull();
        string daprVersion = GetPackageVersion(centralCatalog, "Dapr.Client");
        (string Prefix, string[] Expected)[] restatedLines =
        [
            ("- Infrastructure: Aspire AppHost ", [$"Aspire AppHost `{appHostVersion}`", $"Dapr SDK policy target `{daprVersion}`"]),
            ("- ASP.NET Core auth/data-protection package line ",
                [$"currently `{GetPackageVersion(centralCatalog, "Microsoft.AspNetCore.Authentication.JwtBearer")}`"]),
            ("- Dapr SDK target remains the imported stable ", [$"stable `{daprVersion}` family"]),
            ("- The imported catalog pins `Microsoft.AspNetCore.OpenApi` ",
            [
                $"`Microsoft.AspNetCore.OpenApi` at `{GetPackageVersion(centralCatalog, "Microsoft.AspNetCore.OpenApi")}`",
                $"`Swashbuckle.AspNetCore` at `{GetPackageVersion(centralCatalog, "Swashbuckle.AspNetCore")}`",
            ]),
            ("- The imported catalog pins `ModelContextProtocol.AspNetCore` ",
                [$"`ModelContextProtocol.AspNetCore` at `{GetPackageVersion(centralCatalog, "ModelContextProtocol.AspNetCore")}`"]),
            ("The intended Timesheets UI policy is Fluent UI V5 components only. ",
            [
                $"`Microsoft.FluentUI.AspNetCore.Components` and its Icons package at stable `{GetPackageVersion(centralCatalog, "Microsoft.FluentUI.AspNetCore.Components")}`",
                $"Icons package at stable `{GetPackageVersion(centralCatalog, "Microsoft.FluentUI.AspNetCore.Components.Icons")}`",
            ]),
            ("The imported catalog's Dapr package family is stable ", [$"family is stable `{daprVersion}`", $"`Dapr.Client` `{daprVersion}`"]),
            ("- Use the imported Dapr SDK ", [$"Dapr SDK `{daprVersion}` family"]),
            ("The technology choices work together: ",
                [$".NET SDK `{sdkVersion}`", $"Dapr SDK `{daprVersion}` family", $"Aspire AppHost SDK `{appHostVersion}`"]),
            ("Critical decisions are documented with versions ",
                [$"SDK `{sdkVersion}`", $"Dapr SDK family `{daprVersion}`", $"Aspire AppHost SDK `{appHostVersion}`"]),
            ("- Dapr version policy ambiguity is resolved ", [$"package family `{daprVersion}`"]),
        ];

        string[] architectureLines = architecture.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach ((string prefix, string[] expected) in restatedLines)
        {
            string line = architectureLines
                .Where(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal))
                .ShouldHaveSingleItem($"Expected exactly one architecture line starting with '{prefix}'.");
            foreach (string value in expected)
            {
                line.ShouldContain(value);
            }
        }
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

    /// <summary>
    /// Verifies that catalog defaults are resolved without selecting conditional overrides.
    /// </summary>
    [Fact]
    public void PackageCatalogReaderUsesUnconditionalDefaultsWithConditionalOverrides()
    {
        XDocument catalog = XDocument.Parse(
            """
            <Project>
              <PropertyGroup Condition="'$(MSBuildProjectName)' == 'Other'">
                <SharedVersion>9.0.0</SharedVersion>
              </PropertyGroup>
              <PropertyGroup>
                <SharedVersion>1.2.3</SharedVersion>
                <SharedVersion Condition="'$(MSBuildProjectName)' == 'Other'">8.0.0</SharedVersion>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Shared" Version="$(SharedVersion)" />
                <PackageVersion Include="Literal" Version="4.5.6" />
              </ItemGroup>
            </Project>
            """);

        GetPackageVersion(catalog, "Shared").ShouldBe("1.2.3");
        GetPackageVersion(catalog, "Literal").ShouldBe("4.5.6");
    }

    private static string MutateText(string document, string original, string replacement)
    {
        document.Contains(original, StringComparison.Ordinal).ShouldBeTrue("A regression mutation must target an existing published value.");
        string mutated = document.Replace(original, replacement, StringComparison.Ordinal);
        mutated.ShouldNotBe(document, "A regression mutation must change its input.");
        return mutated;
    }

    private static void ValidateExistingWaiver(string item, string[] row)
    {
        // Only the existing named scopes carried forward in the approved correction are waivers.
        Dictionary<string, string> scopes = new(StringComparer.Ordinal)
        {
            ["Unavailable defaults"] = "fail-closed defaults for live Works validation and planned-effort reporting",
            ["Skipped lanes"] = "Five default skips",
            ["Deferred integrations"] = "Live Works host channels remain waived",
            ["Performance evidence"] = "EventStore-backed persistence/Dapr wire path is waived",
            ["Live Works reference validation in host"] = "no live `IWorksQueryChannel` is bound",
            ["Live Works planned-effort reporting"] = "host keeps `UnavailableWorkPlannedEffortProvider`",
            ["Works staleness detection"] = "detect stale Works state fail-closed",
            ["Platform prerelease dependency exception"] = "Keycloak",
        };
        scopes.ContainsKey(item).ShouldBeTrue($"'{item}' has no approved carried-forward waiver scope.");
        row[1].ShouldContain(scopes[item]);
        Match approval = Regex.Match(row[6], @"Approval:.*?\]\((?<source>[^)]+)\)");
        approval.Success.ShouldBeTrue();
        const string source = "../_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md#3-recommended-approach";
        approval.Groups["source"].Value.ShouldBe(source, "An unrelated approved file or section cannot authorize this waiver.");
        row[6].ShouldContain("carrying forward the existing named waiver only");
        string correction = File.ReadAllText(RepositoryRoot.PathTo("_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md"));
        Regex.IsMatch(correction, @"(?m)^status: approved\r?$").ShouldBeTrue();
        ReadMarkdownSection(correction, "## 7. Approval Gate").ShouldContain("**Decision:** Approved by Jerome on 2026-09-12.");
        string scope = ReadMarkdownSection(correction, "## 3. Recommended Approach");
        scope.ShouldContain("Existing unrelated owner/risk/revisit waivers in `docs/launch-readiness.md` carry forward unchanged.");
        scope.ShouldContain("does not silently revoke or broaden them");
        if (item == "Platform prerelease dependency exception")
        {
            row[1].ShouldContain("only the Keycloak and CommunityToolkit Dapr integrations remain prerelease");
            row[4].ShouldContain("Prerelease");
            row[5].ShouldContain("stable");
            ReadMarkdownSection(correction, "## 7. Approval Gate").ShouldContain("adopting prerelease packages without a recorded exception");
        }
    }

    private static void ValidateDisclosureCriterion(string criterion)
    {
        criterion.ShouldMatch(@"(?i)\bcomment(?:s| text)?\b");
        criterion.ShouldMatch(@"(?i)only when.{0,35}(?:explicit external-confirmation policy|external-confirmation policy explicitly).{0,20}allows");
        criterion.ShouldMatch(@"(?i)otherwise.{0,30}omitted");
    }

    private static string ReadStoryStatus(string story)
        => Regex.Matches(story, @"^Status: (?<status>[a-z-]+)\r?$", RegexOptions.Multiline)
            .Cast<Match>().ShouldHaveSingleItem("Expected one story status.").Groups["status"].Value;

    private static void ValidatePrerequisiteStatuses(string sprint, bool unfinishedFr14, JsonElement? reconciliationAcceptance = null)
    {
        string[] prerequisites = ["3-6-implement-eventstore-backed-magic-link-state-loading",
            "3-7-prove-magic-link-no-disclosure-at-the-http-boundary"];
        if (unfinishedFr14)
        {
            foreach (string prerequisite in prerequisites)
            {
                ReadTrackingStatus(sprint, prerequisite).ShouldNotBe("done", "Unfinished FR14 acceptance cannot close its owning prerequisites.");
            }
        }

        if (unfinishedFr14 || prerequisites.Any(key => ReadTrackingStatus(sprint, key) != "done")
            || ReadTrackingStatus(sprint, "5-2-reconcile-package-currency-and-platform-dependency-versions") != "done")
        {
            if (ReadTrackingStatus(sprint, "5-1-final-launch-readiness-gate-and-documentation-sync") == "done")
            {
                reconciliationAcceptance.HasValue.ShouldBeTrue("Closing the reconciliation before its release prerequisites requires explicit scoped human acceptance.");
                JsonElement acceptance = reconciliationAcceptance.GetValueOrDefault();
                acceptance.GetProperty("story_key").GetString().ShouldBe("5-1-final-launch-readiness-gate-and-documentation-sync");
                acceptance.GetProperty("decision").GetString().ShouldBe("accepted");
                acceptance.GetProperty("scope").GetString().ShouldBe("story-5.1-reconciliation");
                acceptance.GetProperty("user_instruction").GetString().ShouldBe("accept and sign-off");
                acceptance.GetProperty("release_authorized").GetBoolean().ShouldBeFalse();
                acceptance.GetProperty("release_decision").GetString().ShouldBe("FAIL");
                acceptance.GetProperty("remaining_gates").EnumerateArray().Select(gate => gate.GetString()).Order()
                    .ShouldBe(new[] { "story-3.6-deployed-acceptance", "story-3.7-deployed-privacy", "story-5.2-review",
                        "current-graph-runtime-qualification", "tenant-legal-sign-off", "tenant-policy-configuration", "product-stakeholder-uat" }.Order());
            }
        }
        // Package-currency completion is independent of the FR14/runtime gates.
    }

    private static string EvidencePath(string file)
        => RepositoryRoot.PathTo("_bmad-output/implementation-artifacts/evidence/5-1/20261020-final-gate", file);

    private static JsonDocument ReadEvidenceJson(string file)
        => JsonDocument.Parse(File.ReadAllText(EvidencePath(file)));

    private static Dictionary<string, JsonElement> ReadExecutionRecords()
    {
        Dictionary<string, JsonElement> records = new(StringComparer.Ordinal);
        foreach (string manifest in new[] { "execution.json", "projection-followup.json", "final-verification.json", "history-correction-verification.json" })
        {
            using JsonDocument document = ReadEvidenceJson(manifest);
            foreach (JsonElement execution in document.RootElement.EnumerateArray())
            {
                if (execution.TryGetProperty("compressed_xml_receipt", out JsonElement receipt))
                {
                    records.Add(receipt.GetString().ShouldNotBeNull(), execution.Clone());
                }
            }
        }

        return records;
    }

    private static XElement ReadRetainedReport(string receipt)
    {
        Path.GetFileName(receipt).ShouldBe(receipt, "Retained reports must reside in the evidence packet.");
        using JsonDocument manifest = ReadEvidenceJson("receipt-hashes.json");
        JsonElement hash = manifest.RootElement.GetProperty("receipts").EnumerateArray()
            .Single(entry => entry.GetProperty("compressed_file").GetString() == receipt);
        byte[] compressed = File.ReadAllBytes(EvidencePath(receipt));
        Convert.ToHexStringLower(SHA256.HashData(compressed)).ShouldBe(hash.GetProperty("compressed_sha256").GetString());
        using GZipStream stream = new(new MemoryStream(compressed), CompressionMode.Decompress);
        using MemoryStream raw = new();
        stream.CopyTo(raw);
        byte[] bytes = raw.ToArray();
        Convert.ToHexStringLower(SHA256.HashData(bytes)).ShouldBe(hash.GetProperty("raw_sha256").GetString());
        using MemoryStream xml = new(bytes);
        return XDocument.Load(xml).Root.ShouldNotBeNull().Elements("assembly").ShouldHaveSingleItem();
    }

    private static (int Total, int Passed, int Failed, int Skipped) ReadCounts(JsonElement counts)
        => (counts.GetProperty("total").GetInt32(), counts.GetProperty("passed").GetInt32(),
            counts.GetProperty("failed").GetInt32(), counts.GetProperty("skipped").GetInt32());

    private static (int Total, int Passed, int Failed, int Skipped) ReadCounts(XElement assembly)
        => ((int)assembly.Attribute("total").ShouldNotBeNull(), (int)assembly.Attribute("passed").ShouldNotBeNull(),
            (int)assembly.Attribute("failed").ShouldNotBeNull(), (int)assembly.Attribute("skipped").ShouldNotBeNull());

    private static (int Total, int Passed, int Failed, int Skipped) ReadPublishedCounts(string results, string project)
    {
        string[] cells = results.Split('\n').Single(line => line.StartsWith($"| {project} |", StringComparison.Ordinal))
            .TrimEnd('\r').Split('|', StringSplitOptions.TrimEntries)[1..^1];
        cells.Length.ShouldBe(5);
        int[] counts = cells[1..].Select(cell => int.Parse(cell.Trim('*'), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return (counts[3], counts[0], counts[1], counts[2]);
    }

    private static void ValidatePublishedCounts(string readiness, string summary, JsonElement counts, Dictionary<string, JsonElement> executions)
    {
        string results = ReadMarkdownSection(summary, "## Current Suite Results");
        string[] gate = ReadReleaseGateRow(readiness, "Tests (full suite)");
        string[] projects = ["Hexalith.Timesheets.ArchitectureTests", "Hexalith.Timesheets.Contracts.Tests",
            "Hexalith.Timesheets.Server.Tests", "Hexalith.Timesheets.Works.Tests", "Hexalith.Timesheets.Projections.Tests",
            "Hexalith.Timesheets.IntegrationTests"];
        counts.GetProperty("suites").EnumerateObject().Select(suite => suite.Name).Order().ShouldBe(projects.Order());
        results.Split('\n').Where(line => line.StartsWith("| ", StringComparison.Ordinal) && !line.StartsWith("| Suite |", StringComparison.Ordinal))
            .Select(line => line.Split('|')[1].Trim()).Order().ShouldBe(projects.Append("**Total**").Order());
        (int Total, int Passed, int Failed, int Skipped) aggregate = (0, 0, 0, 0);
        foreach (string project in projects)
        {
            JsonElement suite = counts.GetProperty("suites").GetProperty(project);
            string receipt = suite.GetProperty("compressed_receipt").GetString().ShouldNotBeNull();
            XElement assembly = ReadRetainedReport(receipt);
            Path.GetFileNameWithoutExtension(assembly.Attribute("name").ShouldNotBeNull().Value).ShouldBe(project);
            var actual = ReadCounts(assembly);
            actual.Total.ShouldBe(actual.Passed + actual.Failed + actual.Skipped);
            ((int?)assembly.Attribute("errors") ?? 0).ShouldBe(0);
            ((int?)assembly.Attribute("not-run") ?? 0).ShouldBe(0);
            ReadCounts(suite).ShouldBe(actual, $"counts.json must match selected report '{receipt}'.");
            ReadPublishedCounts(results, project).ShouldBe(actual, "Published arithmetic must match actual retained XML.");
            string shortName = project["Hexalith.Timesheets.".Length..];
            Match published = Regex.Matches(gate[2], $@"(?<![\w.]){Regex.Escape(shortName)} (?<passed>\d+)/(?<failed>\d+)/(?<skipped>\d+)(?!\d)")
                .Cast<Match>().ShouldHaveSingleItem();
            int.Parse(published.Groups["passed"].Value, System.Globalization.CultureInfo.InvariantCulture).ShouldBe(actual.Passed);
            int.Parse(published.Groups["failed"].Value, System.Globalization.CultureInfo.InvariantCulture).ShouldBe(actual.Failed);
            int.Parse(published.Groups["skipped"].Value, System.Globalization.CultureInfo.InvariantCulture).ShouldBe(actual.Skipped);
            executions.ContainsKey(receipt).ShouldBeTrue($"Selected report '{receipt}' requires a recorded execution outcome.");
            JsonElement execution = executions[receipt];
            string[] argv = execution.GetProperty("argv").EnumerateArray().Select(arg => arg.GetString().ShouldNotBeNull()).ToArray();
            Path.GetFileName(argv[0]).ShouldBe(project);
            int xmlArgument = Array.IndexOf(argv, "-xml");
            xmlArgument.ShouldBeGreaterThan(0);
            (Path.GetFileName(argv[xmlArgument + 1]) + ".gz").ShouldBe(receipt);
            argv.Length.ShouldBe(3, "Selected full-suite execution must run the executable with only its XML output argument.");
            execution.GetProperty("exit_code").GetInt32().ShouldBe(actual.Failed == 0 ? 0 : 1,
                "Successful and failed selected reports must agree with their execution outcome.");
            DateTimeOffset.Parse(execution.GetProperty("end_utc").GetString().ShouldNotBeNull(), System.Globalization.CultureInfo.InvariantCulture)
                .ShouldBeGreaterThanOrEqualTo(DateTimeOffset.Parse(execution.GetProperty("start_utc").GetString().ShouldNotBeNull(), System.Globalization.CultureInfo.InvariantCulture));
            if (project == "Hexalith.Timesheets.Projections.Tests")
            {
                string[] projectionGate = ReadReleaseGateRow(readiness, "Projection rebuild/idempotency");
                projectionGate[1].ShouldBe(actual.Failed == 0 ? "PASS" : "FAIL");
                projectionGate[2].ShouldContain($"Projections.Tests: {actual.Passed} passed, {actual.Failed} failed, {actual.Skipped} skipped");
            }

            aggregate = (aggregate.Total + actual.Total, aggregate.Passed + actual.Passed, aggregate.Failed + actual.Failed, aggregate.Skipped + actual.Skipped);
        }

        ReadCounts(counts.GetProperty("totals")).ShouldBe(aggregate);
        ReadPublishedCounts(results, "**Total**").ShouldBe(aggregate);
        gate[2].ShouldContain($"Total: {aggregate.Passed} passed, {aggregate.Failed} failed, {aggregate.Skipped} skipped ({aggregate.Total} cases)");
        gate[1].ShouldBe(aggregate.Failed == 0 ? "PASS" : "FAIL");
        results.ShouldContain($"Tests gate: **{gate[1]}**");
        gate[3].ShouldContain("Local proof does not replace a deployed persistence test");
    }

    private static void ValidateFailedAttempts()
    {
        using JsonDocument failures = ReadEvidenceJson("failed-attempts.json");
        JsonElement[] attempts = failures.RootElement.GetProperty("attempts").EnumerateArray().ToArray();
        string[] failedReports = Directory.EnumerateFiles(Path.GetDirectoryName(EvidencePath("counts.json")).ShouldNotBeNull(), "*.xml.gz")
            .Select(path => Path.GetFileName(path)).Where(receipt =>
            {
                XElement assembly = ReadRetainedReport(receipt);
                return ReadCounts(assembly).Failed > 0 || ((int?)assembly.Attribute("errors") ?? 0) > 0;
            }).ToArray();
        attempts.Select(attempt => attempt.GetProperty("compressed_receipt").GetString()).Order().ShouldBe(failedReports.Order());
        // The superseded broad failure and failed guard attempt remain retained across retries.
        failedReports.ShouldContain("Hexalith.Timesheets.Projections.Tests.xml.gz");
        failedReports.ShouldContain("launch-readiness-initial.xml.gz");
        foreach (JsonElement attempt in attempts)
        {
            string receipt = attempt.GetProperty("compressed_receipt").GetString().ShouldNotBeNull();
            ReadCounts(attempt).ShouldBe(ReadCounts(ReadRetainedReport(receipt)));
            attempt.GetProperty("original_outcome").GetString().ShouldBe("failed");
            if (attempt.GetProperty("execution_manifest").ValueKind == JsonValueKind.String)
            {
                ReadExecutionRecords()[receipt].GetProperty("exit_code").GetInt32().ShouldBe(1);
            }
        }
    }

    private static string ReadReadiness()
        => File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

    private static string ReadCurrentAssessment(string document)
    {
        string heading = document.Split('\n').Single(line => line.StartsWith("## Current Assessment — ", StringComparison.Ordinal)).TrimEnd('\r');
        heading.ShouldMatch(@"^## Current Assessment — 20\d{2}-\d{2}-\d{2}$");
        return ReadMarkdownSection(document, heading);
    }

    private static string ReadTrackingStatus(string sprint, string key)
    {
        Match status = Regex.Matches(sprint, $@"^  {Regex.Escape(key)}: (?<status>[a-z-]+)\r?$", RegexOptions.Multiline)
            .Cast<Match>().ShouldHaveSingleItem($"Expected one sprint tracking entry '{key}'.");
        return status.Groups["status"].Value;
    }

    private static string GetPackageVersion(XDocument centralCatalog, string packageId)
    {
        string version = centralCatalog.Descendants("PackageVersion")
            .Single(element => string.Equals(element.Attribute("Include")?.Value, packageId, StringComparison.Ordinal))
            .Attribute("Version")
            .ShouldNotBeNull()
            .Value;

        Match propertyReference = Regex.Match(version, @"^\$\((?<name>[^)]+)\)$");
        if (!propertyReference.Success)
        {
            return version;
        }

        return GetCatalogProperty(centralCatalog, propertyReference.Groups["name"].Value);
    }

    private static string GetCatalogProperty(XDocument centralCatalog, string propertyName)
        // Read the shared default; the catalog's conditional Folders override does not apply to Timesheets.
        => centralCatalog.Descendants(propertyName)
            .Single(element => element.AncestorsAndSelf().All(ancestor => ancestor.Attribute("Condition") is null))
            .Value
            .Trim();

    /// <summary>
    /// Loads the first existing catalog among the root <c>Directory.Packages.props</c> import candidates, in
    /// document order, after substituting that file's <c>PropertyGroup</c> values (last assignment wins) and the
    /// repository directory for <c>$(MSBuildThisFileDirectory)</c>. Import and property conditions and global properties are
    /// not evaluated; this approximates, rather than proves, the catalog MSBuild imports.
    /// </summary>
    private static XDocument LoadImportedPackageCatalog()
    {
        XElement rootProps = XDocument.Load(RepositoryRoot.PathTo("Directory.Packages.props")).Root.ShouldNotBeNull();
        string repositoryDirectory = RepositoryRoot.Find().FullName + Path.DirectorySeparatorChar;
        Dictionary<string, string> properties = rootProps.Elements("PropertyGroup")
            .Elements()
            .GroupBy(static element => element.Name.LocalName, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Last().Value.Trim(), StringComparer.Ordinal);

        string[] candidates = rootProps.Elements("Import")
            .Select(static import => import.Attribute("Project")?.Value ?? string.Empty)
            .Select(project => Regex.Replace(
                project,
                @"\$\((?<name>[^)]+)\)",
                match => properties.TryGetValue(match.Groups["name"].Value, out string? value) ? value : match.Value))
            .Select(path => path.Replace("$(MSBuildThisFileDirectory)", repositoryDirectory, StringComparison.Ordinal))
            .ToArray();
        candidates.ShouldNotBeEmpty("Directory.Packages.props must import the shared central package catalog.");

        string catalogPath = candidates.FirstOrDefault(File.Exists)
            .ShouldNotBeNull($"None of the imported catalog candidates exists: {string.Join(", ", candidates)}");
        return XDocument.Load(catalogPath);
    }

    private static string ReadAppHostSdkVersion()
    {
        const string appHostSdkPrefix = "Aspire.AppHost.Sdk/";
        XElement appHost = XDocument.Load(RepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets.AppHost",
            "Hexalith.Timesheets.AppHost.csproj")).Root.ShouldNotBeNull();
        string appHostSdk = appHost.Attribute("Sdk").ShouldNotBeNull().Value;
        appHostSdk.ShouldStartWith(appHostSdkPrefix);
        return appHostSdk[appHostSdkPrefix.Length..];
    }

    /// <summary>
    /// Reads a Markdown section from its exact heading line to the next heading of the same or higher level.
    /// </summary>
    private static string ReadMarkdownSection(string document, string heading)
    {
        string[] lines = document.Split('\n');
        int start = Array.FindIndex(lines, line => string.Equals(line.TrimEnd('\r'), heading, StringComparison.Ordinal));
        start.ShouldBeGreaterThanOrEqualTo(0, $"Missing heading line '{heading}'.");

        int level = heading.TakeWhile(static character => character == '#').Count();
        level.ShouldBeGreaterThan(0, $"'{heading}' is not a Markdown heading.");
        int end = Array.FindIndex(lines, start + 1, line =>
        {
            int lineLevel = line.TakeWhile(static character => character == '#').Count();
            return lineLevel > 0 && lineLevel <= level && line.Length > lineLevel && line[lineLevel] == ' ';
        });

        return string.Join('\n', lines[start..(end < 0 ? lines.Length : end)]);
    }

    /// <summary>
    /// Reads the single paragraph that starts with <paramref name="prefix"/>.
    /// </summary>
    private static string ReadParagraph(string document, string prefix)
    {
        string[] paragraphs = document.Replace("\r\n", "\n", StringComparison.Ordinal).Split("\n\n")
            .Select(static paragraph => paragraph.Trim())
            .Where(paragraph => paragraph.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();

        return paragraphs.ShouldHaveSingleItem($"Expected exactly one paragraph starting with '{prefix}'.");
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
        string row = ReadMarkdownSection(document, "## Launch-Scope Classification").Split('\n')
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
        => ReadFourColumnRow(ReadMarkdownSection(document, "## Release-Gate Decision Table"), gate, "release-gate");

    private static string[] ReadPackageVerdictRow(string packageVerdictSection, string dimension)
        => ReadFourColumnRow(packageVerdictSection, dimension, "package-verdict");

    private static string[] ReadFourColumnRow(string document, string key, string table)
    {
        string row = document.Split('\n')
            .Single(line => line.StartsWith($"| {key} |", StringComparison.Ordinal));
        string[] delimitedCells = row.TrimEnd('\r').Split('|', StringSplitOptions.TrimEntries);
        delimitedCells[0].ShouldBeEmpty($"The '{key}' {table} row must start with a table delimiter.");
        delimitedCells[^1].ShouldBeEmpty($"The '{key}' {table} row must end with a table delimiter.");
        string[] cells = delimitedCells[1..^1];

        cells.Length.ShouldBe(4, $"The '{key}' {table} row must contain exactly four structured columns.");
        cells[0].ShouldBe(key);
        return cells;
    }
}
