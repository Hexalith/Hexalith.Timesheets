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
        string opening = ReadSection(readiness, "# Launch Readiness Record", "Related evidence:");

        opening.ShouldContain("neither story-complete nor launch-complete");
        opening.ShouldContain("Story 3.6 remains in progress");
        opening.ShouldContain("Story 5.2 remains in progress");
        opening.ShouldContain("final Story 5.1 reconciliation remains ready for development");
        opening.ShouldNotContain("Story 3.6 is complete");
        opening.ShouldNotContain("Story 3.6 is in review");
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
        // this lane on every test added anywhere in the solution.
        readiness.ShouldMatch(@"\b\d{4}-\d{2}-\d{2}\b");
        readiness.ShouldContain("ArchitectureTests");
        readiness.ShouldContain("Projections.Tests");
        readiness.ShouldContain("Server.Tests");
        string[] fullSuite = ReadReleaseGateRow(readiness, "Tests (full suite)");
        fullSuite[1].ShouldBe("PASS");
        fullSuite[2].ShouldContain("Architecture");
        fullSuite[2].ShouldContain("Integration");
        fullSuite[3].ShouldMatch(@"All six executables ran on the round-\d+ implementation tree");
        fullSuite[3].ShouldContain("local proof does not replace a deployed persistence test");
    }

    [Fact]
    public void Launch_readiness_overall_decision_is_an_honest_verdict_not_a_vanity_pass()
    {
        // Open core stories and missing deployed persistence proof require FAIL, not a waiver.
        string readiness = File.ReadAllText(RepositoryRoot.PathTo("docs", "launch-readiness.md"));

        int decisionStart = readiness.LastIndexOf("Overall release decision", StringComparison.Ordinal);
        decisionStart.ShouldBeGreaterThanOrEqualTo(0, "Launch-readiness record must declare an overall release decision.");

        string overall = readiness[decisionStart..];
        overall.ShouldContain("Overall release decision: **FAIL**");
        overall.ShouldContain("Stories 3.6 and 3.7 remain in progress, Story 5.2 remains in progress");
        overall.ShouldContain("final Story 5.1 reconciliation remains ready for development");
        overall.ShouldContain("Atomic magic-link submission, EventStore-backed recording, local actor commit/reload");
        overall.ShouldMatch(@"round-\d+ 2026-10-12 isolated opt-in local Dapr/EventStore journey passed");
        overall.ShouldContain("shared-rebuild Begin producer now exists in code");
        overall.ShouldContain("no independent installed coverage authority");
        overall.ShouldContain("certified legacy owner census");
        overall.ShouldContain("Deployed acceptance remains open");
        overall.ShouldNotContain("Story 3.6 is complete");
        overall.ShouldNotContain("Story 3.6 is in review");
        overall.ShouldNotContain("decision: **PASS**");
        overall.ShouldNotContain("decision: PASS");

        string[] persistenceGate = ReadReleaseGateRow(readiness, "Magic-link durable confirm/adjust persistence");
        persistenceGate[1].ShouldBe("FAIL");
        persistenceGate[2].ShouldContain("exact stored use/effect batches");
        persistenceGate[2].ShouldContain("actor recreation");
        persistenceGate[3].ShouldContain("Deployed Dapr persistence");
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
        readiness.ShouldContain("valid Fresh-catalog GET displays return 200 and valid confirm/adjust POSTs return 202 only after Completed EventStore status and exact stored-event readback");
        readiness.ShouldMatch(@"round-\d+ isolated local Dapr/EventStore journey passed 1/1");
        readiness.ShouldContain("does not establish deployed Dapr persistence");
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
        liveResolution[2].ShouldBe("blocked");
        liveResolution[1].ShouldContain("valid confirm/adjust POSTs return 202 only after Completed EventStore status and exact stored-event readback");
        liveResolution[1].ShouldContain("The run-only AppHost declares an EventStore resource");
        liveResolution[4].ShouldContain("does not establish deployed Dapr persistence");
        liveResolution[5].ShouldContain("Inventory deployed streams read-only");
        liveResolution[5].ShouldContain("concurrent single-use and restart checks");
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

        currentVerdict.ShouldContain($"SDK `{sdkVersion}`");
        currentVerdict.ShouldContain($"`rollForward: {rollForward}`");
        currentVerdict.ShouldContain($"`Aspire.AppHost.Sdk` `{appHostVersion}`");
        currentVerdict.ShouldContain("`AspireUseCliBundle=false`");
        currentVerdict.ShouldContain("`ASPIRE010`");
        currentVerdict.ShouldContain("sprint-change-proposal-2026-09-12.md");
        currentVerdict.ShouldContain("Historical AppHost smoke evidence last passed on 2026-09-15");
        currentVerdict.ShouldContain("it does not validate the current restored graph");
        currentVerdict.ShouldContain("Current runtime-smoke evidence remains deferred");

        string[] direct = ReadPackageVerdictRow(packageVerdict, "Direct Timesheets NuGet package currency");
        direct[1].ShouldBe("PASS");
        direct[2].ShouldContain("no direct package updates");
        direct[2].ShouldContain($"`{appHostVersion}`");

        string[] vulnerable = ReadPackageVerdictRow(packageVerdict, "Vulnerable and deprecated packages");
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
        buildGate[2].ShouldMatch(@"round-\d+ Timesheets source-reference Debug solution build passed");
        buildGate[2].ShouldContain("the changed worktree based on");
        buildGate[2].ShouldContain("57770d380c724c94233b78ac021a8f10cd5d6f39");
        buildGate[2].ShouldContain("--configuration Debug --no-restore -p:UseHexalithProjectReferences=true");
        buildGate[2].ShouldContain("0 warnings and 0 errors");
        buildGate[2].ShouldContain("MSB3243/CS1704");
        buildGate[3].ShouldContain("HXW0002");
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
        => ReadFourColumnRow(document, gate, "release-gate");

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
