---
title: 'Story 5.2: Reconcile Package Currency and Platform Dependency Versions'
type: 'chore'
created: '2026-09-12'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '72be616918e0ae80fadf56fa152dbd561955e22d'
rebaseline_commit: '180d9fd9a635ed44ba896688d6a06e38a33e9f28'
context:
  - '_bmad-output/implementation-artifacts/epic-5-context.md'
  - '_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

<!-- Renegotiated 2026-10-18 by Jerome (option A): re-baseline to SDK 10.0.402 and Aspire 13.6.1; root npm now applicable. -->

## Intent

**Problem:** Story 5.2 still records the September baseline. Since then `global.json` moved to SDK `10.0.402`, the imported `Hexalith.Builds` catalog moved Aspire to stable `13.6.1` (sibling AppHosts pin `Aspire.AppHost.Sdk/13.6.1`), and release tooling added a root `package.json` + `package-lock.json`. The Timesheets AppHost still pins `Aspire.AppHost.Sdk/13.5.3` with `AspireUseCliBundle=true`, so its restored graph mixes `Aspire.Hosting.AppHost/13.5.3` with `Aspire.Hosting/13.6.1` and omits NuGet orchestration packages. Launch readiness still calls root npm not applicable and cites 2026-09-17 audits of an earlier graph. Fifteen 2026-09-12 review patches remain open.

**Approach:** Keep SDK `10.0.402`. Align the AppHost SDK to the imported stable `13.6.1` with NuGet-backed orchestration (`AspireUseCliBundle=false` plus `NoWarn` `ASPIRE010`, the decided Option 2 shape used by `Hexalith.EventStore`). Rerun all four NuGet audit dimensions and root npm outdated/audit on the current graph. Re-validate each open review patch against HEAD and apply those still valid. Reconcile current package claims without rewriting dated historical evidence.

## Boundaries & Constraints

**Always:** Keep NuGet versions under Central Package Management with no inline `PackageReference` versions; prefer stable `net10.0`-compatible direct packages; audit direct, transitive, vulnerable, and deprecated NuGet packages after a clean SDK `10.0.402` restore; record root npm as applicable with `npm outdated` and `npm audit` results, keeping findings visible with owner, risk, and revisit condition; keep historical evidence (Story 5.3 SDK `10.0.400` run, 2026-09-17 audits, 2026-10-03 catalog observation) explicitly dated; record unavailable tooling and remaining platform exceptions with owner, risk, and revisit condition; rerun AppHost/DCP-starting lanes when their prerequisites exist and record exactly what could not run.

**Never:** Edit `references/*`, sibling manifests, or gitlinks; change `global.json`, `package.json`, `package-lock.json`, or npm dependency versions (CI/CD release tooling owns them); suppress any Aspire diagnostic other than `ASPIRE010`; add a transitive pin without a compatibility, security, or deterministic-build reason; adopt a new prerelease package without explicit approval; add a `.sln`, inline version, UI, topology, persistence, or product behavior; fold Story 5.1's final launch verdict or `AGENTS.md`/`CLAUDE.md`/Copilot guidance sync into this story.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Approved baseline | SDK `10.0.402`; AppHost SDK `13.5.3` with CLI bundle | SDK unchanged; AppHost becomes `13.6.1`, `AspireUseCliBundle=false`, `NoWarn` adds only `ASPIRE010`; restored AppHost graph has one Aspire `13.6.1` line including orchestration and dashboard packages | Build/test failure or a new Aspire diagnostic blocks completion and remains recorded |
| Direct update found | New stable, compatible direct version | Apply only through Timesheets-owned CPM configuration | Do not edit the imported catalog or use an inline version |
| Transitive-only drift | Closest direct packages are current and audit-clean | Record drift and add no pin | Pin only with documented compatibility/security/determinism evidence |
| Root npm manifest present | Root `package.json` + `package-lock.json` | npm recorded applicable; `npm outdated` and `npm audit` results recorded with counts | High/critical findings stay visible as CONCERNS with owner, risk, revisit; no npm edits |
| Audit-tool failure | Package listing cannot evaluate a graph | Preserve exact failure and run per-project fallback checks | Never report the failed lane as clean |
| Stale review patch | 2026-09-12 patch whose target moved or no longer exists | Re-validated against HEAD: applied if still valid, otherwise marked obsolete with reason | Never apply a patch by its original line numbers alone |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj` -- SDK `13.5.3` → `13.6.1`; `AspireUseCliBundle` `true` → `false`; add unconditional `<NoWarn>$(NoWarn);ASPIRE010</NoWarn>`. Leave the conditional compiled-gateway constant and the project/package references untouched. Precedent: `references/Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` (read-only).
- `global.json` -- already `10.0.402` + `latestPatch`, guarded by `Repository_pins_approved_dotnet_sdk`; read-only.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/ScaffoldGovernanceTests.cs` -- `AppHost_initializes_security_through_eventstore_aspire_helper` asserts `13.5.3` and CLI bundle `true`; flip to `13.6.1`/`false` plus an `ASPIRE010`-only suppression, and split the unrelated rules per the open patch.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` -- package-verdict guard reads `references/Hexalith.Builds/Props/Directory.Packages.props` and searches the whole `## Package-Currency Verdict` section; the open patches on hard-bound catalog path, section scoping, and AppHost/catalog alignment land here.
- `docs/launch-readiness.md` -- `## Package-Currency Verdict` (npm row says no `package.json`) and the release-gate `Package currency` row: new dated evidence, npm applicable, AppHost `13.6.1`. The old preamble patch targets text that has since moved; re-validate.
- `_bmad-output/planning-artifacts/architecture.md` -- version notes and the two later summaries still say SDK `10.0.401`, Aspire `13.5.3`, `AspireUseCliBundle=true`, Dapr `1.18.7`; reconcile to current, keep dated observations dated.
- `docs/ci-cd.md` -- current SDK claim says `10.0.401`; correct that claim only.
- `_bmad-output/implementation-artifacts/epic-5-context.md` -- two open patches (Works checkout rule, dropped constraints).
- `_bmad-output/implementation-artifacts/deferred-work.md` -- resolve or refresh Story 5.2-owned entries: the 5.3-review catalog misstatement, the spec-6 waiver-row/fitness-scope entry, the spec-15 catalog-observation entry, the "re-run audits against the current 13.6.1 catalog graph" entry, and the two open ledger-convention patches. Leave the full-suite fitness entry and Story 5.1-deferred items open.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` -- nondeterministic privacy-assertion patch; the file changed since review, re-validate.
- `_bmad-output/implementation-artifacts/5-2-reconcile-package-currency-and-platform-dependency-versions.md` -- append the re-baselined iteration without erasing earlier records.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- key `5-2-reconcile-package-currency-and-platform-dependency-versions`.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj` -- apply SDK `13.6.1` with NuGet-backed orchestration, then a forced no-cache restore; confirm the AppHost assets hold one Aspire `13.6.1` line including `Aspire.Hosting.Orchestration.*` and `Aspire.Dashboard.Sdk.*`.
- [x] `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/ScaffoldGovernanceTests.cs` -- update and split the AppHost guard for `13.6.1`, CLI bundle `false`, and an `ASPIRE010`-only suppression.
- [x] `### Review Findings` below -- re-validate each open patch against HEAD; apply valid ones; mark each `[x]` as "applied" or "obsolete: <reason>".
- [x] `docs/launch-readiness.md`, `_bmad-output/planning-artifacts/architecture.md`, `docs/ci-cd.md` -- record the current package matrix and verdicts; keep historical runs dated.
- [x] `deferred-work.md`, `epic-5-context.md`, story record -- resolve or refresh the Story 5.2-owned entries; append iteration evidence, exact counts, and a `git diff --name-only` file list.
- [x] Run restore, warnings-as-errors build, every test-project executable, all four NuGet audit dimensions, `npm ci` + `npm outdated` + `npm audit`, and AppHost/DCP-starting lanes when available; record exact commands, counts, and failures.

**Acceptance Criteria:**
- Given current configuration, when `global.json`, the AppHost project, and fitness tests are inspected, then SDK `10.0.402`/`latestPatch`, `Aspire.AppHost.Sdk/13.6.1`, `AspireUseCliBundle=false`, and an `ASPIRE010`-only suppression agree, and the restored AppHost graph contains no Aspire `13.5.x` package.
- Given restored assets, when NuGet direct/transitive/vulnerable/deprecated audits and root npm outdated/audit run, then compatible direct NuGet updates use CPM only, unjustified transitive pins are absent, and every finding or unavailable lane is recorded verbatim.
- Given evidence is published, when launch readiness, architecture, CI/CD docs, and Epic 5 context are compared with current files, then SDK, Aspire, Dapr, npm applicability, transitive drift, platform waivers, and dated historical runs are non-contradictory.
- Given the 2026-09-12 review patches, when the Review Findings section is inspected, then each is marked applied or obsolete with a reason.
- Given repository boundaries, when the final diff is inspected, then no sibling content/gitlink, `global.json`, npm manifest/lockfile, inline version, legacy solution, UI, topology, or product behavior changed.
- Given verification completes, when every test executable runs, then all non-skipped tests pass with warnings as errors and exact totals are recorded.

### Review Findings

Code review 2026-09-12 (layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor).

**Decision needed**

- [x] [Review][Decision] `AspireUseCliBundle=true` turns the Aspire CLI into a hard build prerequisite and drops the orchestration packages from restore — The ASPIRE010 fix chose the CLI bundle over the suppression path the diagnostic itself offers. `Aspire.Hosting.AppHost.targets:433` raises `<Error Code="ASPIRE009">` when the bundle cannot be resolved, inside targets gated on `AspireUseCliBundle == 'true'`; reproduced as `error ASPIRE009 ... Build FAILED` with `aspire` hidden from `PATH`. `src/Hexalith.Timesheets.AppHost/obj/project.assets.json` now holds only `Aspire.Hosting`, `Aspire.Hosting.AppHost`, `Aspire.Hosting.Redis`, `Aspire.Hosting.Keycloak` — `Aspire.Hosting.Orchestration` and `Aspire.Dashboard.Sdk` are gone, so restore + build is no longer self-contained. The recorded "0 warnings, 0 errors" holds only because this workstation has `aspire` `13.5.3` on `PATH`. Nothing in `README.md`, `docs/launch-readiness.md`, or a fitness test records the prerequisite, and the agent guidance explicitly targets restricted environments. `Aspire.Hosting.AppHost.targets:158` offers the alternative verbatim: "or suppress ASPIRE010 to continue without the bundle", which keeps the build self-contained. This also bears on the frozen AC4 ("no ... runtime behavior changed") and approved proposal §3 ("The only currently identified package edit is the Timesheets-owned AppHost SDK pin") — the CLI-bundle property is a second AppHost change that was reinterpreted in prose (Implementation Notes, triage EH5) rather than renegotiated. [src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj:4]
  **Resolved 2026-09-13 (Jerome): Option 2** — suppress `ASPIRE010` and keep NuGet-backed Aspire orchestration, matching the reviewed shape already adopted by `Hexalith.EventStore` (`AspireUseCliBundle=false` + `NoWarn=$(NoWarn);ASPIRE010`, guarded by `AppHostProjectExplicitlyRetainsNuGetOrchestrationAndSuppressesOnlyAspire010`). Rationale: `AddReferenceToDashboardAndDCP` (`Aspire.AppHost.Sdk/13.5.3/Sdk/Sdk.targets:76`) is byte-identical to 13.4.6 and re-adds `Aspire.Dashboard.Sdk.<RID>` + `Aspire.Hosting.Orchestration.<RID>`, so restore/build returns to being self-contained and AC4's "no runtime behavior changed" becomes literally true instead of reinterpreted. Converted to the first patch below.

**Patch**

- [x] [Review][Patch] Revert to NuGet-backed Aspire orchestration: `AspireUseCliBundle=false` + `NoWarn=$(NoWarn);ASPIRE010`, flip the fitness guard, and correct the `ASPIRE010` narrative in architecture/launch-readiness/story record [src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj:4]
  2026-10-18 re-validation against HEAD `180d9fd`: applied — the AppHost now declares `Aspire.AppHost.Sdk/13.6.1`, an unconditional `AspireUseCliBundle=false`, and an unconditional `NoWarn` of exactly `$(NoWarn);ASPIRE010`; the split `ScaffoldGovernanceTests` guards assert that shape; the architecture version notes, the launch-readiness current verdict, and the story record's 2026-10-18 section describe NuGet-backed orchestration, while the 2026-09-12 CLI-bundle narrative stays as dated history.
- [x] [Review][Patch] Regenerated Epic 5 context outlaws the repository's own test-enforced Works checkout [_bmad-output/implementation-artifacts/epic-5-context.md:27]
  2026-10-18 re-validation against HEAD `180d9fd`: applied — the Works paragraph now states the `references/Hexalith.Works` umbrella pin enforced by `WorksCheckoutGovernanceTests`, forbids only a repository-root checkout, gitlink, or `$(MSBuildThisFileDirectory)Hexalith.Works` probe, and resolves `references/` first and `../Hexalith.Works` second.
- [x] [Review][Patch] Regenerating Epic 5 context dropped ratified constraints and prerequisite traceability [_bmad-output/implementation-artifacts/epic-5-context.md]
  2026-10-18 re-validation against HEAD `180d9fd`: applied — restored the ratified launch classifications (including the legal-hold v1 default and post-v1 secondary verification for high-value or billable entries), story-complete versus launch-complete, evidence-only export language, factual copy, the V4-icon fallback, the no-pointer-mixing rule, Docker/Dapr/Aspire lanes in the gate, and story-numbered prerequisites (3.6, 1.10, 4.8, 4.9, 1.11/4.10, 1.4, 2.7/4.6). The logging and time-zone invariants (BH10/BH11) were already present at HEAD.
- [x] [Review][Patch] Classification vocabulary contradicts itself across two files changed in the same commit [docs/launch-readiness.md:36]
  2026-10-18 re-validation against HEAD `180d9fd`: applied — the record's vocabulary has since moved (now `implemented`, `waived`, `post-v1`, `blocked` plus compound labels). `epic-5-context.md` now names both the approved `implemented/unfinished/waived/post-v1` dispositions and the record's actual labels, defines `blocked` as never a waiver, and leaves vocabulary reconciliation in `docs/launch-readiness.md` to Story 5.1.
- [x] [Review][Patch] New doc guard hard-binds ArchitectureTests to submodule-owned catalog state by hardcoded path [tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs:159-169]
  2026-10-18 re-validation against HEAD `180d9fd`: applied — `LoadImportedPackageCatalog` follows the root `Directory.Packages.props` import candidates in order (`references/`, `../Hexalith.Builds`, `../../references/`) and loads the first existing catalog, so the guard reads whichever catalog the build imports instead of a hard-coded path.
- [x] [Review][Patch] Section guard can pass on the wrong text, and one assertion can never fail [tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs:171]
  2026-10-18 re-validation against HEAD `180d9fd`: applied — removed the always-true `ShouldContain("Package-Currency Verdict")`; the section is read from its exact heading line to the next same-level heading, and assertions now target only the single `Current package-currency verdict` paragraph, the named verdict rows, the prerelease waiver row, and the `Package currency` gate row, so dated paragraphs cannot satisfy them.
- [x] [Review][Patch] Nondeterministic privacy assertion accepted with retry-to-green, no fix and no ledger entry [tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs:370]
  2026-10-18 re-validation against HEAD `180d9fd`: applied (record), with the ledger part obsolete. The helper moved (the generic `"60"` term is now in `AssertSensitiveMaterialAbsent`, about line 1519) and still matches substrings. The "no ledger entry" part is obsolete: the 2026-10-09 spec-16 entry ("Replace substring-based numeric privacy exclusions...") owns the field-aware fix with a named owner, and the launch-readiness privacy and no-disclosure gates keep the false-positive risk visible. Applied here: the 2026-09-12 story record now cross-references that entry and states that the focused retries did not establish determinism. This iteration did not retry to green; the full IntegrationTests executable passed once (727 passed, 5 skipped, 732 total). The test-design fix stays with the spec-16 owner because it changes HTTP privacy assertion semantics outside package currency.
- [x] [Review][Patch] `architecture.md` version claims remain the only unguarded ones [_bmad-output/planning-artifacts/architecture.md:357-362]
  2026-10-18 re-validation against HEAD `180d9fd`: applied — new `ArchitectureVersionNotesMatchExecutableConfiguration` derives the SDK, roll-forward, AppHost, Dapr, Fluent UI, Keycloak, and CommunityToolkit values from `global.json`, the AppHost project, and the imported catalog, and rejects a CLI-bundle opt-in in the Data Architecture version notes.
- [x] [Review][Patch] Launch-readiness preamble still scopes the whole record to the June baseline [docs/launch-readiness.md:3]
  2026-10-18 re-validation against HEAD `180d9fd`: applied at the moved target — the sentence now sits after the dated 2026-10-15 to 2026-10-17 entries; it describes `b0c41aa` as the original 2026-06-26 anchor, states that later sections carry their own baselines, and cites the 2026-10-18 package-verdict anchor while keeping the pinned open-story wording.
- [x] [Review][Patch] Story record's reopened file list is wrong three ways [_bmad-output/implementation-artifacts/5-2-reconcile-package-currency-and-platform-dependency-versions.md:372-388]
  2026-10-18 re-validation against HEAD `180d9fd`: applied — a dated correction under that list records the actual `git show --name-status ee76a92` set: `deferred-work.md` was omitted, `epic-5-context.md` and `sprint-status.yaml` were changed by that commit rather than pre-existing, and the spec was added (`A`) by it rather than left untracked.
- [x] [Review][Patch] New deferred-work entry breaks ledger conventions and is filed under the wrong story [_bmad-output/implementation-artifacts/deferred-work.md:48]
  2026-10-18 re-validation against HEAD `180d9fd`: applied — the entry moved from the Story 5.3 review section into the Story 5.2 review section with a repository-relative `source_spec`, names Story 5.1 as owner, and stays open for Story 5.1's final classification.
- [x] [Review][Patch] A deferred-work entry this change resolves is left standing [_bmad-output/implementation-artifacts/deferred-work.md:45]
  2026-10-18 re-validation against HEAD `180d9fd`: applied — the Story 5.3 review's catalog-misstatement bullet now carries a 2026-10-18 resolution note.
- [x] [Review][Patch] Nothing asserts the documented AppHost-SDK/catalog `13.5.3` alignment invariant [tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs:165]
  2026-10-18 re-validation against HEAD `180d9fd`: applied — new `AppHostSdkTracksTheImportedStableAspireCatalogLine` asserts that the AppHost SDK version equals the imported `Aspire.Hosting` version (`13.6.1` today).
- [x] [Review][Patch] Security-helper test now asserts three unrelated rules, with two conventions for one version [tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/ScaffoldGovernanceTests.cs:54-80]
  2026-10-18 re-validation against HEAD `180d9fd`: applied — split into `AppHost_initializes_security_through_eventstore_aspire_helper`, `AppHost_pins_approved_aspire_apphost_sdk`, and `AppHost_retains_nuget_orchestration_and_suppresses_only_aspire010`. The approved pin is one literal constant, like the `global.json` guard, while documentation guards derive the version from the project. Project references are checked through XML rather than raw text.
- [x] [Review][Patch] Architecture version note points "below" at content that is above it [_bmad-output/planning-artifacts/architecture.md:362]
  2026-10-18 re-validation against HEAD `180d9fd`: applied — the historical template note now points "above" to the current AppHost baseline.

**Deferred**

- [x] [Review][Defer] Agent entry points still pin SDK `10.0.302`, now two steps behind `global.json` [AGENTS.md:92, CLAUDE.md:92, .github/copilot-instructions.md:92] — deferred: fix edits agent-context files; the spec's Never clause assigns agent-guidance sync to Story 5.1, and existing ledger entries predate the approved baseline.
- [x] [Review][Defer] Overall release decision stays `CONCERNS` while the approved correction and the new epic rule require `FAIL` [docs/launch-readiness.md:78] — deferred: final release classification belongs to Story 5.1; already ledgered by this change, though the same diff cites proposal §7 as waiver approval while not honoring §3/§4.
- [x] [Review][Defer] AppHost CLI-bundle resolution has no executing test [tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/ScaffoldGovernanceTests.cs:54-80] — deferred: an `Aspire.Hosting.Testing` lane pulls Docker/Keycloak into the default ArchitectureTests path; the existing ledger entry covers only the security topology and should be widened to CLI-bundle resolution.
- [x] [Review][Defer] Commit message understates the change [commit ee76a92] — deferred: "build: align SDK and Aspire package baseline" while rewriting ~60 lines of Epic 5 governance text; fixing it requires history rewrite.
- [x] [Review][Defer] Work was committed directly to `main` [commit ee76a92] — deferred: agent guidance says branch first; already committed, so the correction is procedural.

**Rejected**

- Spec frontmatter `status: 'done'` and `review_loop_iteration: 0` contradict `Status: review` in the story and `review` in sprint status (raised by all four layers) — real, but the fix edits the spec under review.
- Spec Verification commands omit the `--force --no-cache` and `--no-restore`/`--include-transitive` flags actually used — real, but the fix edits the spec under review (the build's own triage rejected this as BH9 for the same reason).
- `global.json` missing-key guards: `GetProperty("sdk"/"rollForward")` throws `KeyNotFoundException` — low; `global.json` is the artifact under guard and a malformed one should fail loudly, and the fix adds branches.
- Multi-SDK `Sdk` attribute (`"Aspire.AppHost.Sdk/13.5.3;Microsoft.NET.Sdk"`) would corrupt the derived version — low and speculative; no such csproj exists and the fix adds parsing.
- `AspireUseCliBundle` inside `Choose`/`Otherwise` or a `Target` escapes the unconditional check — low and speculative; the fix adds further assertions to a guard added in this change.
- `Directory.Build.props` or `-p:` could override `AspireUseCliBundle` outside the csproj — low; detecting it needs an evaluated-property probe, and the substantive concern is carried by the decision-needed item above.

Code review 2026-10-18 of `180d9fd..b85e810` (layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor; 46 raw findings).

**Patch**

- [x] [Review][Patch] Alignment guard does not enforce the platform consumer rule `Aspire.AppHost.Sdk` == catalog `Aspire.Hosting` [tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs:350]
  `AppHostSdkTracksTheImportedStableAspireCatalogLine` compares the SDK only with `HexalithAspireAppHostSdkVersion`, yet its doc comment claims the restored graph "cannot mix two Aspire hosting versions". The SDK adds `Aspire.Hosting.AppHost`, orchestration, and dashboard at the SDK version while `Aspire.Hosting`/`Redis` come from the catalog. The two catalog values really diverged: Builds `9e6b6ca` and `1bde4d9` (both pinned by Timesheets) carried `HexalithAspireAppHostSdkVersion` `13.6.0` with `Aspire.Hosting` `13.6.1`, where this guard would have demanded SDK `13.6.0` and the mixed graph this story removes. The platform's consumer rule is SDK == `Aspire.Hosting` (`references/Hexalith.Builds/Tools/validate-package-version-exceptions.ps1:368-375`); the `HexalithAspireAppHostSdkVersion` comparison applies only to Builds' own projects. The architecture guard's "aligned at stable `{appHostVersion}`" check also reads the AppHost value rather than the catalog entries it describes. Fix: keep the existing comparison and add `appHostVersion.ShouldBe(GetPackageVersion(catalog, "Aspire.Hosting"), ...)`. [blind-hunter+edge-case-hunter+verification-gap+acceptance-auditor]
  Applied 2026-10-18: `AppHostSdkTracksTheImportedStableAspireCatalogLine` keeps the `HexalithAspireAppHostSdkVersion` comparison and also asserts SDK == imported `Aspire.Hosting`; the doc comment names the consumer rule.
- [x] [Review][Patch] Platform-alignment verdict cell says `waived` while the row holds an open, unwaived Works divergence, and the test pins that label [docs/launch-readiness.md:687; tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs:298]
  The rationale cell says "Waived (platform prereleases only)" and "Open, unwaived divergence: the Works EventStore-version forwarding", but the Verdict column reads plain `waived`, so a reader of the verdict column (Story 5.1's final classification) sees the dimension as fully waived. R2 P1 corrected the prose but left the cell, and `platform[1].ShouldBe("waived")` rejects an honest label. Fix: use a compound label in the table's existing style (e.g. `waived / open divergence`, like `applicable / CONCERNS`) and assert it. [blind-hunter+edge-case-hunter+acceptance-auditor]
  Applied 2026-10-18: the verdict cell is `waived / open divergence` and `platform[1]` asserts that label.
- [x] [Review][Patch] Re-baselined file list omits `sprint-status.yaml`, which the reviewed commit changes [_bmad-output/implementation-artifacts/5-2-reconcile-package-currency-and-platform-dependency-versions.md:500]
  `git diff --name-only 180d9fd b85e810` lists 11 files; the story record lists 10 because the finalization status sync (`in-progress` -> `review`) ran after the list was generated. Fix: add the file with a finalization-sync note. (The matching sentence in this spec's Verification section is not patched: spec-only edit.) [blind-hunter+edge-case-hunter+acceptance-auditor]
  Applied 2026-10-18: the story record's re-baselined file list now includes `sprint-status.yaml` with a finalization-sync note.
- [x] [Review][Patch] Restated-version guard misses `architecture.md:496` and `:533` although its doc comment claims "every restated version line" [tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs:361]
  `ModelContextProtocol.AspNetCore` `2.2.0` (`:496`) and the Fluent UI components/Icons `5.0.1` note (`:533`) are not in `restatedLines`, so a catalog bump leaves them stale with the test green. Fix: add both prefixes to `restatedLines` (catalog-derived values), or narrow the doc comment. [blind-hunter+edge-case-hunter]
  Applied 2026-10-18: `restatedLines` now covers the `ModelContextProtocol.AspNetCore` line and the Fluent UI components/Icons note; the doc comment says "the restated version lines listed here". A temporary `2.2.0` -> `2.1.0` mutation failed the test and was reverted byte-identical.
- [x] [Review][Patch] `docs/ci-cd.md` SDK claim has no fitness guard and had already drifted to `10.0.401` [docs/ci-cd.md:10]
  `global.json` has moved repeatedly; this diff hand-corrected the claim but nothing keeps it aligned. Fix: assert `docs/ci-cd.md` contains `` SDK `{sdkVersion}` `` derived from `global.json`. [edge-case-hunter]
  Applied 2026-10-18: new `ScaffoldGovernanceTests.CiCdGuideNamesThePinnedDotnetSdk` derives the SDK from `global.json`. A temporary `10.0.401` mutation failed the test and was reverted byte-identical.
- [x] [Review][Patch] Transitive tooling-gap owner is "Story 5.2 release maintainer", a role that disappears when the story closes [docs/launch-readiness.md:686]
  New in this diff (the 2026-09-17 row named no owner). Fix: name a durable owner (e.g. Timesheets release maintainer, with the failing .NET SDK `dotnet list` evaluation). [blind-hunter]
  Applied 2026-10-18: the owner is now "Timesheets release maintainer".

**Deferred**

- [x] [Review][Defer] No default lane starts the `13.6.1` AppHost with NuGet `Aspire.Hosting.Orchestration`/`Aspire.Dashboard.Sdk` [src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj:4] — deferred: already ledgered as the 2026-10-18 Story 5.2 DCP-proof entry (D1); needs a free shared Dapr placement. [verification-gap]
- [x] [Review][Defer] Default README source-reference test mode fails 735 cases while release-gate `Build`/`Tests (full suite)` rows say PASS [docs/launch-readiness.md:828-830] — deferred: pre-existing Works `HexalithEventStoreVersion` forwarding mismatch; already ledgered (D2 and the Works forwarding entry). [acceptance-auditor]
- [x] [Review][Defer] Story 5.1's handoff still names superseded version ground truth [_bmad-output/implementation-artifacts/5-1-final-launch-readiness-gate-and-documentation-sync.md:295; _bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md:21,39-40,64] — deferred: the fix edits another story spec and the approved proposal; Story 5.1 still lists SDK `10.0.301`, Aspire `13.4.6`, Fluent `4.11.6`, and the proposal names `10.0.401`/`13.5.3` and npm not applicable, with no addendum pointing 5.1 at `10.0.402`/`13.6.1`/npm applicable. [blind-hunter]
- [x] [Review][Defer] D2 omits the agent entry points' verification instructions [AGENTS.md; CLAUDE.md; .github/copilot-instructions.md "Running and verifying"] — deferred: agent-context edit; unverified whether their bare restore/build/Debug-executable flow reproduces the 735 failures (settle by running those exact steps). If it does, medium. [blind-hunter]

**Rejected** (27)

- Story 5.2 status differs across spec (`done`), sprint (`review`), story record and launch readiness (`in-progress`) (BH1, VG-other, AA4, EH11) — `bmad-build` finalization sets spec `done` and sprint `review` (`step-05-present.md:15-17`); settled precedent (2026-10-05), and the spec half edits the spec under review.
- No R2 `[Review]` checklist, `review_loop_iteration` 0, "(review patch P1)" label (BH3) — fix edits the spec under review.
- Stale round-1 resolution notes for the alignment and catalog-loader patches (BH4, AA8) — real, but the fix edits the spec under review.
- Change Log "applied all fifteen", privacy patch marked applied, R2 rejections, double blank line (BH5, AA9) — fix edits the spec under review.
- Audits and tests read different restore graphs (BH9) — low: the spec prescribes Debug executables, the record names each lane's graph, CI covers Release; adding a Release test lane is more than a direct correction.
- No mutation evidence for the R2 guards (BH10) — low: guards are present and readable; rerunning mutations is evidence work, not a correction.
- Current-verdict test does not assert the option-A citation (BH11) — low: deletion is unlikely and the fix adds a guard (same reasoning as R2-BH10).
- Superseded ledger bullet still reads open (BH14) — false: it is explicitly marked superseded, and `bmad-loop-sweep` verifies each entry against the code with a superseded/skip route.
- `epic-5-context.md` Works checkout "only `references/`" contradicts fallbacks (BH16, EH13) — false: "green with only the `references/` checkout" is a sufficiency requirement; the prohibition is limited to repository-root and nested checkouts, matching the Works paragraph.
- 2026-09-17 table condensed into a dated "Superseded" paragraph (AA5) — low: still dated with its commands, results, and drift, and the SDK correction is annotated inline; restoring the table also needs test-helper changes.
- AppHost manual fallback names 5 of 22 drifted packages; group names in project drift (AA6) — low: non-packable AppHost; listing all requires rerunning the registry comparison.
- Package-mode AppHost transitive graph not compared (AA7) — low: disclosed in the row; closing it is more evidence work.
- Waiver rows cite only §7 for `13.6.1` prereleases (AA10) — false: §7 forbids prerelease adoption "without a recorded exception"; the rows are that recorded exception and carry the current versions.
- Catalog prerelease would be labelled "stable" (EH3) — low and speculative; adds a guard (same as R2-EH2).
- Conditional `HexalithAspireAppHostSdkVersion` makes `Single()` throw (EH4) — low: the catalog declares it unconditionally, and the failure would be loud.
- Unresolved import token silently skipped by `LoadImportedPackageCatalog` (EH5) — low and speculative: current candidates use only `$(MSBuildThisFileDirectory)`; the comment already says it approximates.
- Blanket `#pragma warning disable` / `TreatWarningsAsErrors=false` evades the `ASPIRE010`-only guard (EH7) — low and speculative; adds guards.
- Aspire suppression in `Directory.Packages.props` or nested `src/Directory.Build.*` not scanned (EH8) — low and speculative: no nested build files exist; adds guards.
- A future non-Aspire `NoWarn` would fail the guard (EH9) — false: a loud failure on an unreached state is correct.
- `Choose`/`When` placement escapes the unconditional check (EH10) — low and speculative; rejected on the same grounds in the 2026-09-12 review.
- Commit message says the AppHost "keeps" NuGet-backed orchestration though it switches from the CLI bundle (EH14) — low: needs a history rewrite; the story record, spec, and D1 entry describe the switch.

Code review 2026-10-18 (round 3) of `180d9fd..62ec574`, including the separate CI commit `55e2955` (layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor; 29 raw findings). ArchitectureTests at `62ec574`: 64/64.

**Patch**

- [x] [Review][Patch] Fluent UI v4 package scan still selects projects by the `Hexalith.Timesheets` name match that `55e2955` replaced [tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/ScaffoldGovernanceTests.cs:49]
  `No_fluent_ui_v4_component_package_is_introduced` keeps `path.Contains("Hexalith.Timesheets")`. In the Actions checkout (`.../Hexalith.Timesheets/Hexalith.Timesheets/`) every `references/**/*.csproj` matches, so a sibling adding `Microsoft.Fast.Components.FluentUI` fails Timesheets CI, the failure `55e2955` fixed for the inline-version scan. Locally it skips the tracked `_bmad-output/.../3-6-privacy-assertion-verifier/Verifier.csproj`. Fix: one shared Timesheets-owned project enumeration (for example in `RepositoryRoot`) used by both scans. [verification-gap+blind-hunter]
  Applied 2026-10-19: `RepositoryRoot.TimesheetsProjectFiles()` now feeds both scans. A temporary `_bmad-output` probe project carrying the v4 package failed the v4 test and was removed.
- [x] [Review][Patch] Inline-version scan excludes only the top-level `references/`, so submodule projects in in-repo worktrees are scanned [tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/BuildConfigurationTests.cs:56-60]
  Worktrees under the gitignored `.bmad-loop/runs/*/worktrees/` or `.claude/worktrees/` with initialized submodules expose sibling evidence projects that carry inline versions (for example `references/Hexalith.EventStore/_bmad-output/.../Consumer.csproj`). The pre-`55e2955` `/Hexalith.` name filter excluded them locally, and the root-prefix filter does not. Fix: exclude any root-relative path with a `references` segment, in the same shared helper as the item above. [edge-case-hunter]
  Applied 2026-10-19: the shared list drops any root-relative path with a `references` segment. A temporary `.claude/worktrees/.../references/` probe with an inline version and the v4 package passed both scans and was removed.
- [x] [Review][Patch] Fluent UI restated-line guard checks the Components version against the Icons phrase [tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs:415]
  Both expectations are `Icons package at stable `{version}``, so the `Microsoft.FluentUI.AspNetCore.Components` version is never anchored to its own name: a wrong Components version beside a correct Icons version passes. Fix: expect ``Microsoft.FluentUI.AspNetCore.Components` and its Icons package at stable `{components}``, matching `architecture.md:533`. [blind-hunter+edge-case-hunter]
  Applied 2026-10-19: the Components version is anchored to its own name. A temporary `5.0.0` Components claim at `architecture.md:533` failed the test and was restored byte-identical.
- [x] [Review][Patch] AppHost source-suppression regexes match the word "Aspire" in comments and justifications [tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/ScaffoldGovernanceTests.cs:138-141]
  `#pragma\s+warning\s+disable[^\r\n]*\bASPIRE` and `SuppressMessage(...)\bASPIRE` are case-insensitive, so `#pragma warning disable CA2000 // Aspire owns disposal` or `Justification = "Aspire ..."` fails as an Aspire suppression. Fix: match the existing `AspireDiagnosticCode` pattern (`\bASPIRE[A-Z]*\d{3}\b`) instead of the bare word. [edge-case-hunter]
  Applied 2026-10-19: both regexes require an Aspire diagnostic ID, and the pragma match stops at a `//` comment. Temporary AppHost probes: a CA2000 pragma and justification that mention Aspire passed; `#pragma warning disable ASPIRE001` and a `SuppressMessage` naming `ASPIREHOSTINGPYTHON001` each failed; the probe file was removed.
- [x] [Review][Patch] Story record test totals predate the review-patch commit [_bmad-output/implementation-artifacts/5-2-reconcile-package-currency-and-platform-dependency-versions.md:487-489]
  The record says ArchitectureTests 63/63 and 1,958 total from `b85e810`. `62ec574` added `CiCdGuideNamesThePinnedDotnetSdk`, and the review-time run at `62ec574` gives 64/64 (1,959 total). Fix: append a dated post-review line; keep the `b85e810` totals as recorded. [blind-hunter+acceptance-auditor]
  Applied 2026-10-19: the story record adds a dated post-review totals line (64/64, 1,959 total) and lists `BuildConfigurationTests.cs` and `RepositoryRoot.cs` in the re-baselined file list.
- [x] [Review][Patch] Epic 5 context omits the open, unwaived Works EventStore-version divergence [_bmad-output/implementation-artifacts/epic-5-context.md:34]
  The divergence bullet names Dapr, Fluent UI, Aspire, and Hexalith.Builds only. Story 5.1 classifies divergences from this context, while launch readiness records the Works `HexalithEventStoreVersion` forwarding (`3.124.0` versus catalog `3.125.1`) as open and unwaived. Fix: name it in that bullet and point at the existing Works forwarding ledger entry. [blind-hunter]
  Applied 2026-10-19: the divergence bullet names the Works `HexalithEventStoreVersion` forwarding (`3.124.0` versus `3.125.1`), the `-p:HexalithEventStoreVersion=3.125.1` lane parameter, and the Works forwarding ledger entry.
- [x] [Review][Patch] D1 ledger entry cites its evidence path relative to the ledger, not the repository [_bmad-output/implementation-artifacts/deferred-work.md:739]
  `evidence/3-6/20261018-direct-eventstore-local-proof/...` does not resolve from the repository root; the file is under `_bmad-output/implementation-artifacts/`. Fix: use the repository-relative path, like `source_spec`. (The D1/D2/P1 labels exist only in this spec; adding them would edit the spec under review.) [blind-hunter]
  Applied 2026-10-19: the evidence path is repository-relative and resolves.

**Deferred**

- [x] [Review][Defer] No default lane starts the `13.6.1` AppHost with NuGet `Aspire.Hosting.Orchestration`/`Aspire.Dashboard.Sdk` [src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj:4] — deferred: repeat of the round-2 deferral; already ledgered as the 2026-10-18 Story 5.2 DCP-proof entry (D1), so no new ledger entry. [verification-gap]

**Rejected** (18)

- Prerelease waiver row must name stable Aspire/Dapr/Fluent versions (BH3) — false: the row deliberately lists the restored graph's lines to explain why only Keycloak and CommunityToolkit Dapr remain prerelease; the guard keeps those values current.
- `README.md:16` SDK claim unguarded (BH5) — low: currently `10.0.402` and updated alongside `global.json` in `c4695a5`; the fix adds a guard.
- Works EventStore divergence versions hard-coded rather than derived (BH6) — low: the text is dated 2026-10-18 evidence, and a guard needs a new helper for the conditional catalog property.
- Architecture "aligned at stable" note and Dapr/Aspire family claims guarded through one representative package (BH7, EH4) — low: Aspire and Dapr ship each family in lockstep, the round-2 patch now ties the representative to `Aspire.Hosting`, and the fix adds per-member assertions.
- Rejected round-2 spec defects have no ledger entry (BH8) — fix edits the spec under review; already rejected on that ground.
- Regenerating Epic 5 context would revert corrections from stale sources (BH10) — already ledgered: the Story 5.3 historical-planning entry covers `epics.md` and the 2026-07-20 proposal, and the Story 5.1 ground-truth entry covers the 2026-09-12 proposal.
- Source-reference graph not audited for vulnerable/deprecated packages (BH12) — low: CI/CD ships the package-mode graph, which was audited, and each row names its graph; rerunning audits is evidence work.
- Current-verdict paragraph is overloaded and fragile to reflow (BH13) — low: a reflow fails loudly through `ReadParagraph`, which is the guard's intent; no other harm named.
- Spec `done` while the README test mode fails 735 cases (AA1) — settled: sprint status stays `review` per the 2026-10-05 precedent, and the 735 failures are the round-2 D2 deferral; the spec half edits the spec under review.
- `55e2955` absent from the story record (AA3) — false: it is a separate CI increment, not a Story 5.2 change, so the story file list correctly omits it; the "lists only" Verification sentence is spec-only.
- Privacy and vocabulary patches marked "applied" though partly deferred (AA4) — fix edits the spec under review; rejected as R2-BH16.
- Inconsistent catalog makes both SDK assertions unsatisfiable (EH2) — low: the test fails loudly and the second assertion names the conflict; the fix adds an assertion for wording.
- `ReadParagraph` misses whitespace-only separator lines (EH5) — false: `.editorconfig` `[*]` trims trailing whitespace and `docs/launch-readiness.md` has no such line.
- Fenced `# ` line inside the section truncates it (EH6) — false: the result is a loud lookup failure, and the section has no fenced block.
- `LoadImportedPackageCatalog` relative or unresolved token paths (EH7) — low and speculative; rejected as R2-EH5.
- `Directory.Build.rsp` not scanned for Aspire suppressions (EH9) — low and speculative: no such file exists; adds a guard (same as R2-EH8).
- Redis-only catalog bump mixes Aspire lines with a green test (EH10) — false: with `CentralPackageTransitivePinningEnabled`, `Aspire.Hosting.Redis` above the pinned `Aspire.Hosting` fails restore with a downgrade error, and the reverse bump is compatible.

Code review 2026-10-19 (round 4) of `180d9fd..de3d57c`, including the separate CI commit `55e2955` and excluding the unrelated CI commit `aa49fda` (`.github/workflows/release.yml`) (layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor; 21 raw findings). This round's IDs carry an `R4-` prefix. Release ArchitectureTests at `de3d57c`: 64/64 (acceptance-auditor run).

**Patch**

- [x] [Review][Patch] `docs/ci-cd.md` says the imported catalog selects EventStore `3.124.0`; the catalog has selected `3.125.1` since before `180d9fd` [docs/ci-cd.md:163]
  The sentence was true at Builds `544fd2e` (`c4695a5`), but Builds `332b2c1`, pinned at both `180d9fd` and `de3d57c`, sets `HexalithEventStoreVersion` `3.125.1`. The launch-readiness platform row and `epic-5-context.md` say the catalog is `3.125.1` and only Works forwards `3.124.0`. So two documents dated 2026-10-18 contradict each other, and the CI/CD guide hides the open divergence. AC3 requires the CI/CD docs to agree on platform alignment. The Code Map's "correct that claim only" limited the earlier SDK edit; the D2 entry covers only the guide's test instructions. Fix: keep the dated observation and annotate it: `3.124.0` at Builds `544fd2e`; the current pin `332b2c1` selects `3.125.1`; Works still forwards `3.124.0` into source-reference builds (see the launch-readiness platform row). [R4-AA1, acceptance-auditor]
  Applied 2026-10-19: the dated sentence now reads "then selected EventStore `3.124.0` (Builds `544fd2e`)", followed by a dated correction naming the current pin `332b2c1` (`3.125.1`), the Works `3.124.0` forwarding, and the launch-readiness record.
- [x] [Review][Patch] The `Vulnerable and deprecated packages` verdict row says PASS without scoping it to NuGet, while root npm carries 8 high advisories [docs/launch-readiness.md:684; tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs:270]
  The row's evidence is NuGet-only, and the npm advisories (braces `GHSA-vfj7-8cjw-p6xm`) appear only in the `Root npm applicability` row. A reader of the verdict column, such as Story 5.1's final classification, sees "vulnerabilities: PASS". Round 2 fixed the same kind of overstated verdict cell in the platform row. Fix: rename the row `Vulnerable and deprecated NuGet packages`, say in its evidence cell that npm advisories are recorded in the `Root npm applicability` row, and update the `ReadPackageVerdictRow` key. [R4-AA2, R4-BH4, acceptance-auditor+blind-hunter]
  Applied 2026-10-19: the row is `Vulnerable and deprecated NuGet packages`, its evidence cell points npm advisories to the `Root npm applicability` row, and the test reads the renamed row.
- [x] [Review][Patch] The ledger entry for the agent entry points asks a question the build defaults already answer, and leaves the real defect untracked [_bmad-output/implementation-artifacts/deferred-work.md:753-755]
  The entry says "Unverified whether that flow reproduces the 735 source-reference failures". `Directory.Build.props:19-22` settles it: a bare restore/build defaults to `Release` with `UseHexalithProjectReferences=false`, which is the package graph, and the Works forwarding mismatch cannot apply there. The real defect is that the bare flow never writes the `tests/<Project>/bin/Debug/net10.0` executables the entry points tell agents to run, so agents run stale or missing builds. That already produced a stale-dll result in the round-3 review. Fix: restate the entry around the stale Debug executables, and note that the Debug source-reference mode also needs `-p:HexalithEventStoreVersion=3.125.1`. [R4-BH7, blind-hunter]
  Applied 2026-10-19: the entry is restated around the `bin/Debug` executables the bare flow never produces, cites `Directory.Build.props:20-22`, answers the original question (the package graph cannot reproduce the 735 failures), and names `owner: Story 5.1`.
- [x] [Review][Patch] The current-verdict guard accepts the SDK and AppHost versions from the approval-evidence sentence, so a stale or deleted baseline sentence passes [tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs:255-257]
  `` SDK `{sdkVersion}` `` and `` `Aspire.AppHost.Sdk` `{appHostVersion}` `` are searched in the whole paragraph. The approval sentence added by round-2 P1 ("approves the SDK `10.0.402` and `Aspire.AppHost.Sdk` `13.6.1` baseline") contains both. So the recorded "stale current-verdict AppHost version" mutation is no longer caught. Fix: replace the three separate assertions with the anchored phrase ``baseline is .NET SDK `{sdkVersion}` (`rollForward: {rollForward}`) and `Aspire.AppHost.Sdk` `{appHostVersion}` ``. [R4-EH3, edge-case-hunter]
  Applied 2026-10-19: one anchored assertion replaces the three. A temporary `13.6.0` AppHost version in the baseline sentence failed the test and was restored byte-identical.
- [x] [Review][Patch] The package-verdict anchor names a commit that does not contain the audited configuration [docs/launch-readiness.md:53, :679]
  The preamble says the verdict "is anchored at `180d9fd…`", but at `180d9fd` the AppHost still pins `13.5.3` with `AspireUseCliBundle=true`. The verdict paragraph's "worktree based on `180d9fd`" follows the record's convention for uncommitted work, but neither place names the commit that holds the audited AppHost configuration. Fix: in both places, name the worktree based on `180d9fd…` and add "committed as `b85e810`". [R4-BH3, blind-hunter]
  Applied 2026-10-19: the preamble and the verdict paragraph both name the worktree based on `180d9fd…`, committed as `b85e810`.
- [x] [Review][Patch] The D1 rerun instruction leaves out the EventStore-version property the cited proof needed [_bmad-output/implementation-artifacts/deferred-work.md:738]
  `scripts/run_isolated_magic_link_dapr.py:684-685` forces the Debug source graph and passes `HexalithEventStoreVersion` only from the caller's environment. The `9523d94` proof that D1 cites ran with `HexalithEventStoreVersion=3.125.1` (`evidence/3-6/20261018-direct-eventstore-local-proof/README.md:62-65`). A rerun without the property can fail on the Works forwarding mismatch, and that failure could be misread as the NuGet-orchestration failure D1 is meant to rule out. Fix: add the build and environment property to the rerun instruction until the Works forwarding entry closes. [R4-BH6, blind-hunter]
  Applied 2026-10-19: the D1 rerun instruction sets `HexalithEventStoreVersion=3.125.1` for the Debug source-reference build and the script's environment until the Works forwarding entry closes.
- [x] [Review][Patch] The historical SDK chain in the architecture stops at `10.0.401` [_bmad-output/planning-artifacts/architecture.md:156]
  The line ends with the Story 5.2 reopening's `10.0.401`, while the version notes at `:360` state `10.0.402` without saying where it came from. Fix: append that the 2026-10-18 CI/CD commit `c4695a5` raised the pin to `10.0.402`. [R4-BH11, blind-hunter]
  Applied 2026-10-19: the chain now ends with the 2026-10-18 CI/CD commit `c4695a5` raising the pin to `10.0.402`.

**Deferred**

- [x] [Review][Defer] No default lane starts the `13.6.1` AppHost with NuGet `Aspire.Hosting.Orchestration`/`Aspire.Dashboard.Sdk` [src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj:4] — deferred: this repeats the round-2 and round-3 deferral and is already ledgered (D1), so no new ledger entry. [R4-VG1, verification-gap]
- [x] [Review][Defer] Agent entry points and a ledger rationale still say the repository has no `.github/workflows` [CLAUDE.md:93; AGENTS.md:93; .github/copilot-instructions.md:93; _bmad-output/implementation-artifacts/deferred-work.md:312] — deferred: pre-existing since `c4695a5` added `ci.yml`, which runs the solution build and the six test projects under Microsoft.Testing.Platform; agent-context edits belong to Story 5.1's guidance sync. [R4-VG3, verification-gap]
- [x] [Review][Defer] Release-gate `Build` and `Tests (full suite)` rows still call the round-20 runs "latest" [docs/launch-readiness.md:828, :830] — deferred: pre-existing, because the 2026-10-18 `9523d94` proof ran newer lanes before `180d9fd`; the rows belong to Story 5.1's final reconciliation and are already the subject of the D2 entry. [R4-BH8, blind-hunter]

**Rejected** (10)

- Aspire-suppression regexes have no positive fixture cases (R4-VG2) — low: the gap matters only when someone edits these two patterns, the round-3 probes verified both directions, and the fix adds a test guard. Round 2 rejected missing mutation evidence on the same ground (BH10).
- The `references` segment exclusion is case-insensitive, so a project under `src/*/References` would be skipped (R4-EH1) — low and speculative: those folders hold only `.cs` files, a project there would break the one-project-per-folder layout, and the fix adds path anchoring.
- In-repo worktree copies and `node_modules` are scanned (R4-EH2, R4-BH9) — low: scanning extra files can only add failures and never hides a violation. This predates `55e2955`, whose name filter also matched worktree projects. No worktree exists now, and the fix adds worktree detection or a `git` call.
- The alignment guard ignores `Aspire.Hosting.Redis`/`Docker` entries below `Aspire.Hosting` (R4-EH4) — low: settled in round 3 (EH10, BH7). Aspire ships the family in lockstep, and per-member assertions would add guards.
- Round-3 rejections cite `R2-EH5`/`R2-EH8` instead of the round-2 EH5/EH8 (R4-BH1) — the fix edits the spec under review. This round uses `R4-` IDs to avoid that collision.
- The spec's Verification totals, file-list sentence, Change Log, and `review_loop_iteration` are stale (R4-BH2) — the fix edits the spec under review; rejected on the same ground in rounds 2 and 3.
- The Works divergence has no approved disposition, and the Epic 5 divergence rule was narrowed (R4-BH5) — false: the narrowed rule names AC4's four targets, and the same bullet names the Works divergence as open and unwaived with its ledger entry. Launch readiness carries its owner, risk, and revisit condition, and `waived / open divergence` is the round-2 P2 label. Final classification belongs to Story 5.1, and the vocabulary entry is already ledgered.
- Epic 5 context says Story 5.2 "establishes the current SDK/package baseline" (R4-BH10) — false: the sentence was already there at `180d9fd`, and under option A Story 5.2 does establish the approved baseline. It assigns no ownership of `global.json`.
- The Aspire ID pattern is repeated in three regexes, and a comment in `GetCatalogProperty` sits between the signature and `=>` (R4-BH12) — low: drift needs a future edit to one copy, the comment compiles and reads as intended, and the fix is a refactor.

Code review 2026-10-19 (round 5) of `180d9fd..419bc7c`, including the separate CI commit `55e2955` and excluding the unrelated commits `aa49fda` (`.github/workflows/release.yml`) and `969d32a` (`epic-3-context.md`) (layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor; 23 raw findings). This round's IDs carry an `R5-` prefix. Release ArchitectureTests at `419bc7c`: 64/64 (acceptance-auditor run, and `dotnet test --project` during triage).

**Patch**

- [x] [Review][Patch] The round-4 ledger entry corrects only the "no `.github/workflows`" sentence of the `dotnet test` runner entry, whose premise `c4695a5` already removed [_bmad-output/implementation-artifacts/deferred-work.md:311-312, :759-761]
  The spec-3-6 entry "Restore a working `dotnet test` path…" rests on "`global.json` lacks the `"test": { "runner": "Microsoft.Testing.Platform" }` opt-in". `c4695a5` added that opt-in, and during this review `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test --project tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj -c Release --no-build` passed 64/64. The round-4 entry added in `419bc7c` hands Story 5.1 only the workflows sentence, so Story 5.1 inherits a runner problem that no longer exists. Fix: in the round-4 entry, record that `c4695a5` also added the runner opt-in and that `dotnet test --project` now works; add a dated update to the runner entry saying its premise no longer holds. [R5-BH2, blind-hunter]
  Applied 2026-10-20: the runner entry carries an update saying its premise no longer holds and pointing to the round-4 entry; the round-4 entry records the `c4695a5` runner opt-in, the 64/64 `dotnet test --project` run, and the VSTest-socket fallback wording Story 5.1 should replace.
- [x] [Review][Patch] The historical SDK chain still skips the `10.0.302` step [_bmad-output/planning-artifacts/architecture.md:156]
  The round-4 patch completed the chain at its end, but it still reads `10.0.301` -> `10.0.400`. `git log -- global.json` shows `937be13` (2026-07-17) raised the pin to `10.0.302` before Story 5.3's `10.0.400`; the ledger's "three steps behind the current pin" for the agent entry points (`10.0.302`) counts that step. Fix: add that `937be13` raised the pin to `10.0.302` on 2026-07-17. [R5-BH11, blind-hunter]
  Applied 2026-10-20: the chain now runs `10.0.301` -> `937be13` `10.0.302` (2026-07-17) -> `10.0.400` -> `10.0.401` -> `10.0.402`.
- [x] [Review][Patch] The 2026-10-03 catalog observation still says "Story 5.2 owns their refresh" with no pointer to the refresh [docs/launch-readiness.md:691]
  The refresh is the 2026-10-18 current verdict above it. The superseded 2026-09-17 paragraph carries a note pointing to the 2026-10-18 evidence; this paragraph does not, so it still reads as an open Story 5.2 action for Story 5.1's final classification. Fix: append a dated note that the 2026-10-18 current verdict above refreshed those audits. [R5-BH13, blind-hunter]
  Applied 2026-10-20: the paragraph keeps its 2026-10-03 text and ends that sentence with "(Update 2026-10-20: the 2026-10-18 current package-currency verdict above is that refresh.)".

**Deferred**

- [x] [Review][Defer] No default lane starts the `13.6.1` AppHost with NuGet `Aspire.Hosting.Orchestration`/`Aspire.Dashboard.Sdk` [src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj:4] — deferred: this repeats the round-2 to round-4 deferral and is already ledgered (D1), so no new ledger entry. At triage a foreign Works AppHost with six Dapr sidecars was running, so the isolated journey's precondition still does not hold. [R5-VG1, verification-gap]

**Rejected** (19 findings)

- `dotnet test` claim and command list in this spec's Verification section are stale (R5-BH1, R5-BH3) — the fix edits the spec under review. The `dotnet test` half is real and verified (64/64); its ledger side is patch R5-BH2.
- The ASPIRE010-only guard does not scan `Directory.Packages.props` or a nested `src/Directory.Build.*` (R5-VG2) — low: no nested build file exists and nobody puts `NoWarn` in the package catalog; the fix adds a directory-walk guard. Rejected on the same ground in round 2 (EH8).
- NuGet orchestration bakes `/tmp/dotnet-cli-home` DCP and dashboard paths into the AppHost dll (R5-BH4) — low: confirmed in both builds, but this is standard NuGet-backed orchestration (the `Hexalith.EventStore` shape), an evicted DCP fails loudly at `aspire start` naming the missing path, and D1's rerun already starts with a Debug build.
- The "AppHost/DCP lanes" record bullet is mislabeled and omits the dll metadata evidence (R5-BH5) — false: the bullet says "compiled no-resource probes" and "Current runtime smoke therefore remains deferred"; it claims no DCP evidence.
- D1 names no owner while every other new entry does (R5-BH6) — false: D2 ("Qualify the release-gate…") and the vocabulary entry name none either; the ledger does not require an owner (18 of about 150 entries carry one), and D1 names its rerun trigger and the infrastructure-owned automated lane.
- Passing CI runs are not cited (R5-BH7) — low: the record makes no false claim, the stale gate rows are the round-4 Story 5.1 entry, and citing run IDs is evidence work.
- `docs/ci-cd.md` "the current Builds pin `332b2c1`" will go stale (R5-BH8, R5-EH5) — false: the sentence is a dated "Correction (2026-10-19)", the record's convention for observations; R4-AA1's defect was a same-date contradiction between documents, which this correction removed.
- `epic-5-context.md` states unguarded present-tense versions (R5-BH9) — low: the values carry their 2026-10-18 renegotiation, and the Story 5.1 ground-truth entry already says to refresh from `docs/launch-readiness.md` before it runs; the fix adds a guard.
- The platform-row test lets the Works divergence's owner, risk, and revisit condition be deleted (R5-BH10) — low: deletion is unlikely and the fix adds assertions; rejected on the same ground in round 2 (R2-BH10, BH11).
- The spec says the gate rows are covered by D2 while the ledger adds a second entry (R5-BH12) — false: the round-4 entry tracks a different defect (the stale "latest round-20" citation) and tells Story 5.1 to handle it with D2; aligning the spec wording edits the spec under review.
- Fluent UI guard cannot pass if Components and Icons get different versions (R5-EH1) — low: the catalog holds both at `5.0.1`, the architecture line itself states one shared version, so a divergence needs a document rewrite anyway, and the failure is loud; the fix adds a branch.
- Npm verdict cell could read PASS beside eight high advisories (R5-EH2) — false: the row reads `applicable / CONCERNS`; the looser `ShouldStartWith("applicable")` assertion would need a condition tied to audit counts.
- Direct-row and gate-row version assertions are unanchored (R5-EH3) — false: neither cell holds a second occurrence of the asserted token (`13.6.1` appears once in the direct cell; the gate cell's other SDK is `10.0.401`), so a stale claim fails. Round 4 anchored the verdict paragraph because its approval sentence held a real duplicate.
- `SuppressMessage` regex stops at the first `)` (R5-EH4) — low: diagnostic categories never contain parentheses and no AppHost source uses `SuppressMessage`; a bracket-aware pattern adds its own multi-line edge cases.
- `b85e810` says the AppHost "keeps" NuGet orchestration (R5-EH6) — low: needs a history rewrite; rejected in round 2 (EH14).
- `Aspire.Hosting.Redis` can sit below `Aspire.Hosting` with a green alignment test (R5-EH7) — settled in rounds 3 and 4 (EH10, BH7, R4-EH4).
- `de3d57c` (`fix: story 5.2`) will make the next release dispatch cut `v1.0.1` with unchanged package code (R5-AA1) — low: verified (`.releaserc.json` uses the default commit analyzer, `v1.0.0..HEAD` holds only this `fix`, and `release.yml` is `workflow_dispatch` only), but the release owner controls when to dispatch, the effect is one redundant patch version, and correcting it rewrites published `main`; commit-message findings were rejected or deferred on that ground in round 2 (EH14) and in 2026-09-12 (`ee76a92`).

Code review 2026-10-20 (round 6) of `180d9fd..62de12f`, including the separate CI commit `55e2955` and excluding the unrelated commits `aa49fda` (`.github/workflows/release.yml`) and `969d32a` (`epic-3-context.md`) (layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor; 17 raw findings). This round's IDs carry an `R6-` prefix. Release ArchitectureTests at `62de12f`: 64/64 (acceptance-auditor run, and `dotnet test <project> -c Release --no-build` during triage).

**Patch**

- [x] [Review][Patch] The npm row's risk cell says "the shared dependency-review gate blocks activation", but that gate runs only on pull requests and CI/CD is already active on `main` [docs/launch-readiness.md:685]
  `.github/workflows/dependency-review.yml` triggers only on `pull_request` to `main`. `ci.yml`, `codeql.yml`, and `commitlint.yml` run on every push to `main` (CI run `39359226330` for `62de12f`), the repository has no pull requests (`gh pr list --state all` is empty), and every Story 5.2 commit was pushed directly. No gate has therefore evaluated the lockfile's eight high braces findings, and the row, new in this diff, offers Story 5.1's final classification a control that does not apply. The claim came from the pre-existing `docs/ci-cd.md:196-199` ("activation and merge blocker … Do not merge or activate this configuration as green") and the CI/CD ledger entry "Resolve the upstream braces/micromatch semantic-release advisory chain before merging or activating Timesheets CI/CD", which the activation has overtaken. Fix: replace the clause with a statement that the PR-only gate has not evaluated these findings because the workflows have run on direct pushes to `main` since `c4695a5`; add a dated correction after the `docs/ci-cd.md` sentence and a dated update to the CI/CD ledger entry. No npm file changes. [R6-BH2, blind-hunter]
  Applied 2026-10-20: the row's risk cell says the PR-only dependency-review gate has not evaluated these findings because the workflows have run on direct pushes to `main` since `c4695a5` (checked 2026-10-20); `docs/ci-cd.md` carries a dated correction after the blocker sentence, and the CI/CD ledger entry a dated update. No npm file changed.
- [x] [Review][Patch] The current verdict says AppHost smoke "last passed on 2026-09-15 … before the current Builds catalog", but the `9523d94` journey passed on 2026-10-18 on the current catalog [docs/launch-readiness.md:679; tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs:261]
  `9523d94` (2026-10-18, an ancestor of `180d9fd`, Builds pin `332b2c1` as at HEAD) ran `scripts/run_isolated_magic_link_dapr.py`, which starts the AppHost through `aspire start`; its evidence packet records the live journey 1/1 in 131.536 s. It ran on AppHost `13.5.3` with the CLI-bundle DCP, so it still does not validate the current graph, as the D1 ledger entry says. The sentence predates this story, but this diff re-dated the paragraph as the 2026-10-18 current verdict and kept it, and `docs/launch-readiness.md` never mentions `9523d94`. Fix: name the 2026-10-18 `9523d94` isolated journey as the latest passing AppHost-starting run, on the pre-change `13.5.3` CLI-bundle graph; keep "it does not validate the current restored graph"; move the pinned assertion to the new text. [R6-BH6, R6-AA1, blind-hunter+acceptance-auditor]
  Applied 2026-10-20: the current verdict names the 2026-10-18 `9523d94` isolated journey (1/1 in 131.536 s) as the latest passing AppHost-starting run, on the current Builds catalog with the pre-change `13.5.3` CLI-bundle DCP, keeps the 2026-09-15 smoke as earlier history, and says the evidence does not validate the current graph. `LaunchReadinessTests` asserts the new run, the pre-change graph, and the conclusion. Temporary `72d9fda` and `13.6.1` mutations each failed the test, and the document was restored byte-identical.
- [x] [Review][Patch] The prerelease waiver row says "Timesheets directly pins only its AppHost SDK", while root `Directory.Packages.props:14` pins `Hexalith.Works.Contracts` `1.0.1` [docs/launch-readiness.md:735]
  The direct verdict row rewritten in this diff names "the root-pinned `Hexalith.Works.Contracts` `1.0.1`", a pin that predates `180d9fd` (`c4695a5`). This diff re-dated the waiver row to 2026-10-18 and kept the sentence, so two rows of the same record contradict each other (AC3). No test pins the sentence. Fix: "Timesheets directly pins only its AppHost SDK and the separately published `Hexalith.Works.Contracts` `1.0.1`, and does not override the platform-owned prereleases." [R6-AA3, acceptance-auditor]
  Applied 2026-10-20: the row says Timesheets directly pins only its AppHost SDK and the separately published `Hexalith.Works.Contracts` `1.0.1`.
- [x] [Review][Patch] `README.md:14` and `:16` describe build and test failures that `c4695a5` removed, and no ledger entry hands them to Story 5.1 [_bmad-output/implementation-artifacts/deferred-work.md:303, :761]
  `README.md:16` says `dotnet test … --no-build` fails with the VSTest message; at `62de12f`, `dotnet test tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj -c Release --no-build` passed 64/64 in both the positional and the `--project` forms. `README.md:14` says the bare solution build "fails with `HXW0002`"; since `c4695a5`, `Directory.Build.props:20-22` makes a bare restore/build Release package mode, and Story 5.2's forced no-cache bare restore and `-warnaserror` build passed (15 projects, 0 warnings, 0 errors). The round-5 update tells Story 5.1 to fix only the agent entry points' runner wording, the D2 entry covers only the README's missing `HexalithEventStoreVersion` qualification, and the Works build-configuration entry ("Preserve the external Works reference's Configuration…") still states the bare failure. Fix: name `README.md:14` and `:16` with this evidence in the round-4 Story 5.1 entry; add a dated update to the Works build-configuration entry saying the bare half no longer reproduces, while the explicit `-c Debug` solution build without `-p:UseHexalithProjectReferences=true` was not rerun. The README edit stays with Story 5.1's documentation sync. [R6-BH3, R6-AA2, R6-BH4, blind-hunter+acceptance-auditor]
  Applied 2026-10-20: the round-4 Story 5.1 entry's summary names the stale README build and test lines, and a dated update records the `README.md:14` and `:16` evidence; the Works build-configuration entry records that the bare half no longer reproduces and keeps the explicit-Debug half and its follow-up open. `README.md` is unchanged.

**Deferred**

- [x] [Review][Defer] No default lane starts the `13.6.1` AppHost with NuGet `Aspire.Hosting.Orchestration`/`Aspire.Dashboard.Sdk` [src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj:4] — deferred: this repeats the round-2 to round-5 deferral and is already ledgered (D1), so no new ledger entry. During this review a foreign `eventstore` Dapr application was running, so the isolated journey's precondition still does not hold. [R6-VG1, verification-gap]

**Rejected** (9 findings)

- The AppHost Keycloak guard reads only `Program.cs` (R6-EH1) — low: the `Program.cs`-only `ShouldNotContain("AddKeycloak(")` predates `180d9fd` and this diff kept it unchanged, `IsolatedGatewayLaunch.cs` holds only the gateway build-mode refusal and SDK delegation, and the fix adds a directory scan.
- The runner entry's closure sits in `evidence:` rather than a `resolution_note:`, and `62de12f` says it marks the entry resolved (R6-EH2, R6-BH5) — false: the approved R5-BH2 fix prescribed exactly this dated update; the entry stays open on purpose because stale wording remains, and it points to the round-4 entry; `bmad-loop-sweep` verifies each open entry against the code (round-2 BH14 precedent). Correcting the commit message needs a history rewrite.
- The "no Fluent UI v4" scan looks for the v1–v3 package ID `Microsoft.Fast.Components.FluentUI` (R6-BH1) — low: true, because V4 and V5 share `Microsoft.FluentUI.AspNetCore.Components`, but the check predates `180d9fd`, no Timesheets project or root props references any Fluent UI package, the repository may not add a UI project, and catching V4 needs a version-aware guard rather than an ID string.
- The `.sln` guard checks only `Hexalith.Timesheets.sln` (R6-BH7) — low: unchanged since before `180d9fd`, a differently named `.sln` is unlikely, and the fix adds a repository scan.
- Hexalith Commons and PolymorphicSerializations drift is catalog lag with no Builds owner (R6-BH8) — false: the row already attributes the drift to "the catalog-owned Hexalith package versions"; the spec's transitive-only row requires recording the drift with no pin, a root override would be the unjustified transitive pin the Never clause forbids, and the catalog belongs to Hexalith.Builds.
- The story record's Change Log stops at 2026-06-26, and AC2/Task 1 say no npm manifest exists today (R6-BH9) — false: the record carries the reopen and the re-baseline in dated sections (`## Reopened … (2026-09-12)` and `## Re-baselined … (2026-10-18)`, which names option A and the post-review totals); AC2 already says "unless a root manifest is added", and Task 1 is the checked June task.
- New ledger entries cite line pointers after the line-pointer entry was resolved (R6-BH10) — false: every cited pointer resolves at HEAD (`launch-readiness.md:828` Build, `:830` Tests, ledger line 312, `AGENTS.md:93`/`CLAUDE.md:93`, `5-1-…md:295`, proposal lines 21/39-40/64); each sits in an evidence field dated to its review, and the resolved entry corrected one spec-6 entry, not a ledger-wide rule.
- The catalog's Dapr runtime pins (`HexalithDaprRuntimeVersion` `1.18.2`, CLI `1.18.0`) are not recorded (R6-BH11) — low: no Timesheets document claims a Dapr runtime version (`architecture.md:362` and `:581` name the SDK package family only), no DCP lane ran in this story, and recording runtime pins is new evidence work.

Code review 2026-10-20 (round 7) of `180d9fd..a6cfe69`, including the separate CI commit `55e2955` and excluding the unrelated commits `aa49fda` (`.github/workflows/release.yml`) and `969d32a` (`epic-3-context.md`) (layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor; 21 raw findings). This round's IDs carry an `R7-` prefix. Release ArchitectureTests at `a6cfe69`: 64/64 (acceptance-auditor run).

**Decision needed**

- [x] [Review][Decision] A Timesheets-owned direct update and a catalog-owned EventStore update were published after the 2026-10-18 audit, so the current direct row's `PASS` / "no direct package updates" no longer holds [docs/launch-readiness.md:683] — `dotnet list Hexalith.Timesheets.slnx package --outdated --no-restore` now reports the root-pinned `Hexalith.Works.Contracts` `1.0.1 -> 1.0.2` (`Directory.Packages.props:14`) and the catalog-owned `Hexalith.EventStore.*` `3.125.1 -> 3.126.0`. NuGet records `1.0.2` as published 2026-10-18T21:01Z and EventStore `3.126.0` at 21:00Z; the audit commit `b85e810` is 18:28Z, so the audit was correct when it ran. The `1.0.2` nuspec has the same dependencies as `1.0.1` (`Hexalith.EventStore.Contracts` `3.124.0`, PolymorphicSerializations `1.19.3`, Commons.UniqueIds `2.30.1`, Ulid `1.4.2`, DI Abstractions `10.0.13`). It was built from Works `1ab960c`, two commits after the Timesheets Works submodule pin `9a7a71b`; `1.0.1` came from `640c8b5`, twelve commits before that pin. The spec's "Direct update found" row says to apply a compatible direct update through Timesheets-owned CPM. Whether to adopt it now, record it for Story 5.1, or leave the dated verdict alone is a scope call. [R7-AA1, acceptance-auditor]
  **Resolved 2026-10-20 (Jerome): Option 1** — adopt `Hexalith.Works.Contracts` `1.0.2` through root CPM now and record the catalog-owned EventStore `3.126.0` as a dated note. Rationale: the last commit touching `src/Hexalith.Works.Contracts` (`afadfaa`, 2026-10-16) precedes `v1.0.1` (`640c8b5`), so `v1.0.1`, the Works source pin `9a7a71b`, and `v1.0.2` carry identical Contracts source; the nuspec dependencies are identical; only `Hexalith.Timesheets.Works` (consumed by `Works.Tests`) references the package. The EventStore line is set by the Builds-owned `HexalithEventStoreVersion` and cannot move without editing `references/`. Rejected: recording and handing off to Story 5.1 (moves the same bump and verification to a later story), and leaving the dated verdict unchanged (the verdict column would keep `PASS` beside a reported update). Converted to the first patch below.

**Patch**

- [x] [Review][Patch] Adopt `Hexalith.Works.Contracts` `1.0.2` through root CPM and record the catalog-owned EventStore `3.126.0` as post-audit drift [Directory.Packages.props:14; docs/launch-readiness.md:683, :735; docs/ci-cd.md:162; tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs:268]
  From the R7-AA1 decision. Change the pin, restore the package-mode graph, run the `-warnaserror` build, the `Works.Tests` and ArchitectureTests Release executables, and the four NuGet audits. Update the direct row (`1.0.2`, plus a dated note that the only remaining reported update is the catalog-owned `Hexalith.EventStore.*` `3.126.0`), the waiver row, and `docs/ci-cd.md` (dated update); keep `PASS` and assert "no Timesheets-owned direct package updates". [R7-AA1, acceptance-auditor]
  Applied 2026-10-20: root CPM now pins Works Contracts `1.0.2`; forced package-mode restore and Release `-warnaserror` build passed, Works.Tests passed 76/76 and ArchitectureTests passed 64/64. All four NuGet audits were rerun: only catalog-owned EventStore `3.125.1 -> 3.126.0` remains as direct drift, vulnerability/deprecation checks are clean, and the existing transitive-tool failure plus the 14 successful project fallbacks (26 distinct transitive drift IDs) are recorded. The current paragraph, direct/waiver rows and dated CI/CD note agree; the direct guard now asserts no Timesheets-owned direct package updates.
- [x] [Review][Patch] The current verdict names the 2026-10-18 journey as the latest AppHost-starting run, while four rows still call the 2026-10-12 round-19 journey the latest, and no ledger entry covers them [docs/launch-readiness.md:679, :728, :830, :835, :840; _bmad-output/implementation-artifacts/deferred-work.md:763-765]
  The round-6 patch (R6-BH6) correctly named the 2026-10-18 isolated journey (1/1 in 131.536 s) as the latest passing run. The Magic-link live-resolution row (`:728`), the `Tests (full suite)` row (`:830`), the durable confirm/adjust row (`:835`), and the `Overall release decision` paragraph (`:840`) still say "the latest round-19 isolated … journey passed 1/1 … in 106.313 seconds" (2026-10-12). So the same record now gives two different "latest" journeys, which AC3 forbids. The round-4 Story 5.1 entry "Refresh the release-gate `Build` and `Tests (full suite)` rows…" covers only the "latest round-20" build and executable wording. The rows belong to Story 5.1's final reconciliation (round-4 R4-BH8 precedent). Fix: add a dated update to that entry naming these four round-19 "latest" statements and the 2026-10-18 journey that superseded them. Leave the rows themselves unchanged. [R7-BH2, R7-EH1, R7-AA3, blind-hunter+edge-case-hunter+acceptance-auditor]
  Applied 2026-10-20: the existing Story 5.1 release-gate refresh entry now explicitly names all four stale round-19 "latest" statements, the 2026-10-18 131.536 s journey, and its post-review receipt. The Story 5.1-owned rows remain unchanged.
- [x] [Review][Patch] The npm risk cell and the `docs/ci-cd.md` correction imply a future pull request would evaluate the braces findings, but the dependency-review action checks only dependencies a pull request adds or changes [docs/launch-readiness.md:685; docs/ci-cd.md:199-203; _bmad-output/implementation-artifacts/deferred-work.md:720-722]
  Builds `dependency-review.yml@544fd2e` runs `actions/dependency-review-action@v5.0.0`, which compares the base and head dependency graphs. The eight high braces findings are already in `main`'s `package-lock.json`, so a pull request that does not change those packages will never be blocked. No workflow runs `npm audit`: `grep -l npm .github/workflows/*.yml` and Builds `domain-ci.yml@544fd2e` both have no match. The round-6 wording ("the gate has not evaluated these findings because the workflows have run on direct pushes") is true, but it implies that the missing pull request is the only gap. Fix: in all three places, say the gate evaluates only dependency changes a pull request introduces, so these lockfile findings stay outside it until a pull request changes the affected packages, and no workflow runs `npm audit`. No npm file changes. [R7-BH3, R7-EH3, blind-hunter+edge-case-hunter]
  Applied 2026-10-20: the npm risk cell, CI/CD correction and advisory-ledger update state that dependency review checks PR-introduced dependency changes, so unchanged existing lockfile findings are outside it; no workflow runs an npm vulnerability audit (commitlint runs `npm audit signatures`). The current action documentation is linked; npm files and workflows are unchanged.
- [x] [Review][Patch] The 2026-10-18 journey is cited as the "`9523d94`" run, but its receipts record the worktree based on `72d9fda` with EventStore `d55c6f7c`, both with uncommitted changes [docs/launch-readiness.md:679; tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs:261; _bmad-output/implementation-artifacts/deferred-work.md:739, :765]
  `evidence/3-6/20261018-direct-eventstore-local-proof/README.md:5` names those base identities. Its publication section says the changes were published afterwards (EventStore fast-forwarded three remote commits to `9b66153`, recorded by Timesheets `88f4ae0`, then `9523d94`). It also says that "the prior qualification receipts retain their original source identities and are not relabeled as these later revisions". At `9523d94` the EventStore pin is `9b66153`, which the journey never ran. The Builds catalog `332b2c1` is the same at `72d9fda` and HEAD, so the "current Builds catalog" clause holds. Fix: in launch readiness, cite the journey as run on the worktree based on `72d9fda` (EventStore `d55c6f7c`, both with uncommitted changes) and published in `9523d94`, and move the pinned assertion to the new wording. Give the ledger's D1 and round-4 entries the same "published in `9523d94`" wording. D1's `historical-live/` pointer is the earlier 128.350 s run, so add the 131.536 s `post-review/` pointer beside it. [R7-BH1, blind-hunter]
  Applied 2026-10-20: the current verdict and fitness assertions distinguish the original modified source worktree from later publication, using full canonical source/publication identifiers. Both affected ledger entries use the same provenance; D1 now links the final `post-review/verification.json` 131.536 s run beside the historical 128.350 s receipt.
- [x] [Review][Patch] "The current Builds pin `332b2c1`" can be read as the workflow pin, which the same guide states as `544fd2e` [docs/ci-cd.md:165]
  `docs/ci-cd.md:3-5` says the domain workflows run at Builds `544fd2e`. `ci.yml` and `dependency-review.yml` still call `@544fd2e…`. Only the `references/Hexalith.Builds` submodule (the imported catalog) is at `332b2c1`. Fix: write "the current `references/Hexalith.Builds` catalog pin `332b2c1`" and add "(the workflows still call Builds `544fd2e`)". [R7-BH5, blind-hunter]
  Applied 2026-10-20: the CI/CD catalog correction explicitly names the `references/Hexalith.Builds` catalog pin and separately identifies the reusable-workflow pin, with full canonical identifiers.
- [x] [Review][Patch] The re-baselined file list does not mention the two unrelated commits that the same `git diff --name-only 180d9fd…` command now also reports [_bmad-output/implementation-artifacts/5-2-reconcile-package-currency-and-platform-dependency-versions.md:517]
  Today the command lists 15 files, adding `.github/workflows/release.yml` (`aa49fda`) and `epic-3-context.md` (`969d32a`). The list already notes the separate CI commit `55e2955` and the review-added files, so a reader comparing it with the command sees two unexplained files. Fix: add a note that `aa49fda` and `969d32a` are unrelated commits whose files are outside Story 5.2. [R7-BH6, blind-hunter]
  Applied 2026-10-20: a dated note before the historical file list identifies the unrelated release-workflow and Epic 3 context commits and explains why their files are outside Story 5.2.
- [x] [Review][Patch] An open ledger entry names Story 5.2 as owner of a defect that `d6b337b` already fixed [_bmad-output/implementation-artifacts/deferred-work.md:455-457]
  The entry "Make the launch-readiness full-suite fitness assertion check current evidence…" describes a `Final total:` regex. `d6b337b` (2026-10-09, an ancestor of `180d9fd`) replaced that regex with `ReadReleaseGateRow(readiness, "Tests (full suite)")`, and HEAD has no `Final total` match in `LaunchReadinessTests.cs`. The entry ends "Owner: launch-readiness fitness (Story 5.2 reconciliation)". The Code Map's "leave the full-suite fitness entry open" and round-2 R2-BH3's "still accurate" both assumed it was still open. Fix: add a dated resolution note citing `d6b337b`. [R7-AA2, acceptance-auditor]
  Applied 2026-10-20: the full-suite fitness ledger entry carries a dated resolution note citing `d6b337b423367927b2ea7b212214c335fb477171`; both that commit and current code use `ReadReleaseGateRow`, and no `Final total:` regex remains. Story 5.1 still owns the row content refresh.


**Deferred**

- [x] [Review][Defer] The durable confirm/adjust row and the overall decision still say "Current Timesheets HEAD pins EventStore `3099cb27`; the SDK changes in this worktree are uncommitted" [docs/launch-readiness.md:835, :840] — deferred: pre-existing (both sentences are present at `180d9fd`); HEAD pins EventStore `9b66153`; the rows belong to Story 5.1's final reconciliation. [R7-BH2, blind-hunter]
- [x] [Review][Defer] No default lane starts the `13.6.1` AppHost with NuGet `Aspire.Hosting.Orchestration`/`Aspire.Dashboard.Sdk` [src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj:4] — deferred: this repeats the round-2 to round-6 deferral and is already ledgered (D1), so no new ledger entry. During this review the acceptance auditor saw nine foreign Dapr applications start within two minutes, so the isolated journey's precondition still does not hold. [R7-VG2, verification-gap]

**Rejected** (10 findings)

- CI/CD was activated despite the documented blocker, with no recorded approval (R7-BH4) — low: activation is pre-existing since `c4695a5` and belongs to the CI/CD release-tooling owner. The round-6 correction already states that the configuration is active and that the advisory chain stays open. Recording an approval needs that owner's decision, not a direct correction.
- The post-review bullet elides the ArchitectureTests project path in `dotnet test --project …` and gives the build only as flags (R7-BH7) — low: the sentence names the single ArchitectureTests project, the ledger's round-5 update carries the full `dotnet test --project tests/Hexalith.Timesheets.ArchitectureTests/…` form, and rebuilding the earlier command lines after the fact would record commands nobody verified.
- The post-review bullet is a long paragraph with a repeated "not rerun" clause (R7-BH8) — false: every round's sentence names its date, its changes, its result, and the lanes it did not rerun; the complaint is style with no wrong outcome.
- No document lists the features that `AspireUseCliBundle=false` gives up (R7-BH9) — low: the `_AspireCheckCliVersion`/`_AspireComputeRunArguments` targets are indeed gated on the bundle. But no Timesheets document or script runs the AppHost with `dotnet run` (the scripts use `aspire start`), the trade-off is the 2026-09-13 Option 2 decision, and listing the features needs research beyond a direct correction.
- The orchestration guard does not reject `SkipAddAspireDefaultReferences=true` (R7-BH10) — low and speculative: nothing sets it, and the fix adds a guard (same ground as the 2026-09-12 and round-2 suppression-surface rejections).
- The option-A approval rests on an HTML comment and a Spec Change Log line (R7-BH11) — the fix edits the spec under review or the approved proposal; round 2 (R2-BH5) accepted the Spec Change Log citation as approval evidence.
- The Spec Change Log says the 2026-09-12 Implementation Notes are "below" (R7-BH12) — the fix edits the spec under review.
- `Hexalith.Works.Contracts` `1.0.1` is restated in three places with no derived assertion (R7-BH13) — low: the fix adds a guard, the same ground as R5-BH9; whichever option R7-AA1 takes decides whether those three mentions change now.
- The waiver row's "Timesheets directly pins only its AppHost SDK and `Hexalith.Works.Contracts`" ignores the exact-pinned npm devDependencies (R7-EH2) — false: the row's subject is the restored NuGet package graph and its platform prereleases; the npm pins have their own row and are not platform prereleases.
- The ASPIRE010-only guard does not scan `Directory.Packages.props` or a nested `Directory.Build.*` (R7-VG1) — low: settled in round 2 (EH8) and round 5 (R5-VG2); the only Timesheets build files are the three root files, nobody puts `NoWarn` in the package catalog, and the fix adds a directory-walk guard.

Code review 2026-10-20 (round 8) of `180d9fd..c5ac600`, excluding the unrelated commits `aa49fda` (`.github/workflows/release.yml`) and `969d32a` (`epic-3-context.md`) (layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor; 22 raw findings). This round's IDs carry an `R8-` prefix. During the review HEAD moved to the workspace sync commit `929ea22` (2026-10-20 09:50, outside the range; it owns its pointer moves). HEAD re-verification on the Release package-mode graph: forced restore and `-warnaserror` build with 0 warnings and 0 errors (blind-hunter run); all six suites passed 1,959 tests with 0 failures and 5 intentional skips (ArchitectureTests 64, Contracts.Tests 92, Works.Tests 76, Server.Tests 558, Projections.Tests 437, IntegrationTests 732).

**Decision needed**

- [x] [Review][Decision] The current package claims are stale at HEAD `929ea22` [docs/launch-readiness.md:679, :686, :689, :690, :738, :832; docs/ci-cd.md:165-175; _bmad-output/implementation-artifacts/epic-5-context.md:34; _bmad-output/implementation-artifacts/deferred-work.md:727, :777] — After the round-7 audit, `929ea22` moved all 12 `references/*` gitlinks.
  - **What HEAD now pins.** The `references/Hexalith.Builds` catalog `6a38cb0` sets `HexalithEventStoreVersion` `3.126.0`, Commons `2.30.3` and PolymorphicSerializations `1.19.5`. Works `6460257` defaults and forwards `HexalithEventStoreVersion` `3.126.0`.
  - **Review audits at HEAD** (package-mode graph):
    - Direct, vulnerable and deprecated audits are clean across all 15 projects; no direct update remains.
    - The solution-level and AppHost transitive-outdated runs still fail with `Sequence contains no matching element`.
    - The 14 project fallbacks report 19 transitive IDs: the earlier 26 minus the five EventStore IDs, `Hexalith.Commons.UniqueIds` and `Hexalith.PolymorphicSerializations`.
    - The six suites pass (counts above). The README source-reference default lane was not rerun.
  - **Stale current claims:**
    - The verdict paragraph and direct row (`:679`, `:686`) say only catalog-owned EventStore `3.125.1 -> 3.126.0` remains.
    - The transitive row update (`:689`) says 26 IDs.
    - The platform and gate rows (`:690`, `:832`) call the Works `3.124.0` vs `3.125.1` divergence open and pass `-p:HexalithEventStoreVersion=3.125.1`.
    - The waiver row (`:738`) says the catalog still selects `3.125.1`.
    - `docs/ci-cd.md` says catalog pin `332b2c1` selects `3.125.1` while Works forwards `3.124.0`.
    - `epic-5-context.md:34` keeps the divergence and the `-p` instruction. That instruction now forces an older EventStore than both HEAD defaults.
  - **Stale ledger entries:**
    - The round-7 Builds handoff (`:777`) names `332b2c1`, and its revisit condition has fired (catalog update, then a Timesheets rerun).
    - The Works forwarding entry (`:727`) still describes `3.124.0` vs `3.125.1`. Works still hard-codes its default, so the structural defect remains, although the values now coincide.
  - **Not stale:** AC4 forbids contradictory version claims, and at HEAD the claims above are contradictory. The Epic 5 context's "latest stable direct packages" claim does hold at HEAD.
  - **The choice:** refresh the current evidence at HEAD from these reruns now; add dated "superseded at `929ea22`" notes and hand the refresh to Story 5.1; or leave the round-7 evidence as dated and out of range.

  [R8-AA6, R8-BH1, R8-BH2, R8-BH3, R8-EH4, R8-AA4, acceptance-auditor+blind-hunter+edge-case-hunter]
  **Resolved 2026-10-20 (Jerome): Option 1** — refresh the current package-currency evidence at HEAD `929ea22` from this review's reruns now. Rationale: the verification is already done (same ground as the R7-AA1 rejection of moving a finished verification to a later story), and AC4 then holds at HEAD. Rejected: dated "superseded" notes with a Story 5.1 handoff (stale lines and the `-p` instruction stay current; Story 5.1 redoes the edits), and leaving round 7 as dated evidence (the contradiction stays hidden and a fired ledger condition stays open). Converted to the first patch below.
- [x] [Review][Decision] No fitness guard derives the catalog EventStore line, the Works forwarding default or the root `Hexalith.Works.Contracts` pin that the current rows restate [tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs:267-270, :314-320] — `LaunchReadinessTests` reads Aspire, Dapr, Keycloak, CommunityToolkit and Fluent UI versions from the catalog, but none of these three values.
  - **The concrete case:** after `929ea22`, five documents carry stale `3.125.1`, `332b2c1` or `3.124.0` claims while ArchitectureTests pass 64/64 at HEAD. R3-BH6, R5-BH9 and R7-BH13 rejected similar guards as unlikely; this sync is that case.
  - **The cost of a guard:** an EventStore guard would fail ArchitectureTests on every sync that moves that line, and the line moved `3.124.0` -> `3.125.1` -> `3.126.0` within three days.
  - **The choice** is a policy call: guard the values, date them instead of guarding, or keep the precedent.

  [R8-EH1, R8-EH2, R8-BH8, edge-case-hunter+blind-hunter]
  **Resolved 2026-10-20 (Jerome): date the values, no new guard** — the refresh states catalog-owned EventStore and Works-forwarding values as dated observations tied to the commit they were observed at, following the record's dating convention; no test change. Rationale: an EventStore guard would fail ArchitectureTests on most workspace syncs, and a `Works.Contracts`-only guard would not cover the catalog drift that occurred. Rejected: three derived guards, a `Works.Contracts`-only guard, and keeping the R3/R5/R7 precedent unchanged. Folded into the first patch below.

**Patch**

- [x] [Review][Patch] Refresh the current package-currency evidence at HEAD `929ea22` and date the catalog-owned values [docs/launch-readiness.md:53, :679, :686, :689, :690, :738, :832; docs/ci-cd.md:165-175; _bmad-output/implementation-artifacts/epic-5-context.md:34; _bmad-output/implementation-artifacts/deferred-work.md:727, :777; tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs:300]
  This patch implements both R8 decisions. Add a dated 2026-10-20 HEAD update anchored at `929ea22`; it states the catalog-owned EventStore and Works-forwarding values as observations at that commit. Content:
  - catalog `6a38cb0` selects EventStore `3.126.0`, Commons `2.30.3` and PolymorphicSerializations `1.19.5`, and Works `6460257` forwards `3.126.0`;
  - direct, vulnerable and deprecated audits are clean across 15 projects;
  - the solution/AppHost transitive failure persists, and the 14 fallbacks report 19 IDs;
  - six suites pass 1,959 tests with 0 failures and 5 intentional skips.

  Keep the round-7 and 2026-10-18 results as dated history. Retain the review receipts as a round-8 evidence packet.

  Ledger and context changes:
  - Resolve the round-7 Builds handoff entry (`:777`).
  - Update the Works forwarding entry (`:727`): the values coincide at `3.126.0`, but Works still hard-codes its default, and the README source-reference default lane was not rerun.
  - Update the platform row, the gate row, `docs/ci-cd.md` and `epic-5-context.md` the same way. Replace the `-p:HexalithEventStoreVersion=3.125.1` instruction.

  Change `LaunchReadinessTests.cs:300` only if the platform row's verdict label changes. No package, gitlink or test-guard change. [R8-AA6, R8-BH1, R8-BH2, R8-BH3, R8-EH4, R8-AA4, R8-EH1, R8-EH2, R8-BH8]
  Applied 2026-10-20 in the review. Evidence: a forced no-cache restore, a non-incremental `-warnaserror` Release build (0 warnings, 0 errors), all six Release suites (1,959 tests, 0 failures, 5 intentional skips; ArchitectureTests 64/64 after the final document edits) and the four NuGet audits (no direct update; vulnerable and deprecated clean; solution/AppHost transitive failure unchanged; 19 transitive IDs) were rerun at `929ea22` and retained in `evidence/5-2/20261020-round8/`. Changes:
  - The current verdict, the direct, vulnerable/deprecated, transitive and platform rows, the gate row, `docs/ci-cd.md` and `epic-5-context.md` carry the dated HEAD observations.
  - The `-p` instruction now names the catalog version generically.
  - The ledger resolves the round-7 Builds handoff and adds a HEAD update to the still-open Works forwarding entry.
  - The direct row's resolved-version list now cites the round-8 package listing.
  - The platform verdict label stays `waived / open divergence`, so no test changed.
- [x] [Review][Patch] The `Package currency` release-gate row was not updated in round 7 [docs/launch-readiness.md:832]
  The row's evidence cell cites only the 2026-10-18 audits, and its notes say "Direct NuGet, vulnerable, and deprecated audits are clean". The current verdict and the direct row (`:679`, `:686`) record two things this row omits: the round-7 Works Contracts `1.0.2` adoption, and a 2026-10-20 direct rerun that reported catalog-owned EventStore drift. R7-AA1's patch scope named the direct row, the waiver row and `docs/ci-cd.md`, and missed this row. Its test checks only the SDK and AppHost tokens. Fix: add the dated 2026-10-20 round-7 rerun (Works Contracts `1.0.2`; no Timesheets-owned direct update) to the evidence cell, and qualify "clean" in the notes. If the first R8 decision refreshes the evidence at HEAD, this row takes the HEAD evidence instead. [R8-AA1, R8-BH4, acceptance-auditor+blind-hunter]
  Applied 2026-10-20: the evidence cell adds the dated round-7 (`c5ac600`) and round-8 (`929ea22`) reruns, and the notes say the audits are clean at HEAD `929ea22`.
- [x] [Review][Patch] The EventStore drift sentence sits in the `waived` platform-prerelease row without saying it is outside the waiver [docs/launch-readiness.md:738]
  The row (verdict `waived`, with prerelease risk and revisit cells) ends "The catalog still selects EventStore `3.125.1`; the newly reported `3.126.0` update belongs to the Builds catalog owner." EventStore is not a prerelease and is not covered by this waiver. The verdict column still makes the sentence read as waived; round-2 P2 fixed the same defect in the platform row. The direct row and the ledger already carry the sentence. Fix: delete it from this row. [R8-AA2, acceptance-auditor]
  Applied 2026-10-20: the sentence was deleted; the row ends with the Timesheets direct-pin sentence.
- [x] [Review][Patch] The round-7 evidence is anchored only to the modified worktree based on `2d6b689`, which still pins Works Contracts `1.0.1` [docs/launch-readiness.md:53, :679; _bmad-output/implementation-artifacts/evidence/5-2/20261020-round7/README.md:9]
  The preamble still anchors the package-currency verdict at the worktree based on `180d9fd…`, committed as `b85e810`. Yet the direct, vulnerable/deprecated and transitive rows now rest on 2026-10-20 evidence. The receipt's input hashes match the `c5ac600` files, but no document names that commit. Round 4 (R4-BH3) patched the same gap for `b85e810`. Fix: add "committed as `c5ac6005a48d973d96f7eff6496c72d0728432f0`" to the round-7 update sentence and to the packet README, and name the round-7 rerun in the preamble's anchor example. [R8-AA3, R8-BH5, acceptance-auditor+blind-hunter]
  Applied 2026-10-20: the preamble names both dated 2026-10-20 updates with their commits, and the round-7 update sentence and packet README say `committed as c5ac600…`.
- [x] [Review][Patch] Only the commitlint workflow is named as running `npm audit signatures`, but the release workflow runs it too [docs/launch-readiness.md:688; docs/ci-cd.md:212; _bmad-output/implementation-artifacts/deferred-work.md:723]
  `release.yml` calls Builds `domain-release.yml@544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9`, which runs `npm ci --ignore-scripts` and then `npm audit signatures` (its lines 334-336 and 746-749). "No workflow runs an npm vulnerability audit" stays true. Fix: name both pinned workflows in all three places. [R8-AA5, acceptance-auditor]
  Applied 2026-10-20: all three places name the commitlint workflow and the shared release workflow `domain-release.yml@544fd2e…` (called by `release.yml`).
- [x] [Review][Patch] `sprint-status.yaml` carries two different update times [_bmad-output/implementation-artifacts/sprint-status.yaml:38]
  `c5ac600` set the header comment to `10-20-2026 09:40` but left the `last_updated` field at `10-20-2026 09:22`. Fix: make the field match the header. This review's status sync rewrites both. [R8-BH7, blind-hunter]
  Applied 2026-10-20: the review's status sync set both to the same time.

**Deferred**

- [x] [Review][Defer] No normally run lane starts the `13.6.1` AppHost with NuGet `Aspire.Hosting.Orchestration`/`Aspire.Dashboard.Sdk` [src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj:4] — deferred: this repeats the round-2 to round-7 deferral and is already ledgered (D1, `deferred-work.md:739`), so no new ledger entry. The only test that starts the host is skipped unless `TIMESHEETS_LIVE_DAPR=1`. [R8-VG1, verification-gap]

**Rejected** (5 findings)

- `GetCatalogProperty` does not resolve a catalog property whose value is itself `$(…)` (R8-EH3) — low and speculative: at HEAD every property the tests read is a literal (64/64 pass). A nested reference would fail loudly rather than pass a wrong value, and the fix adds recursion.
- The Option 1 basis (identical nuspec dependencies and Contracts source) has no retained receipt (R8-BH6) — low: nuget.org nuspecs are immutable, and the source claim can be reproduced from the cited commits (the verification-gap layer reran `git diff 640c8b5..1ab960c -- src/Hexalith.Works.Contracts` and got an empty diff). The fix would be new evidence work.
- The packet keeps derived counts but no derivation script (R8-BH9) — low: the raw stdout is retained, and the blind hunter recomputed every count from it (15 projects, 6 direct IDs in 17 occurrences, 26 transitive IDs). The overstated "all count calculations" wording is in the completed spec-2.
- The spec-2 Review Triage Log puts severities in its Verdict column and has no IDs (R8-BH10) — false on the format: a severity in the verdict column is the triage format this spec's own log uses (`| R2-BH11 | low | …`). The missing IDs are cosmetic, and the fix edits a completed spec.
- Superseded 2026-10-18 decisions carry no forward pointer to the round-7 `1.0.2` adoption (R8-BH11) — the story record is append-only with dated sections, and its round-7 section names the `Directory.Packages.props` change (same ground as R6-BH9). The spec parts would edit the spec under review.

Code review 2026-10-20 (round 9) of `180d9fd..5258598`, excluding the unrelated commits `aa49fda` (`.github/workflows/release.yml`) and `969d32a` (`epic-3-context.md`) and the gitlink-only workspace sync `929ea22` (layers: blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor; 27 raw findings). This round's IDs carry an `R9-` prefix. Release ArchitectureTests at `5258598`: 64/64 (verification-gap run). The round-8 receipt's `docs/launch-readiness.md` and `docs/ci-cd.md` hashes match `5258598`, not `929ea22`; its package and test inputs match both.

**Patch**

- [x] [Review][Patch] The round-8 refresh missed four statements that `929ea22` made stale [docs/launch-readiness.md:679; _bmad-output/implementation-artifacts/deferred-work.md:740, :756, :772]
  - The current verdict says the 2026-10-18 isolated journey "ran on the current Builds catalog". Its source worktree `72d9fda` and publication commit `9523d94` both pin Builds `332b2c1`, while HEAD pins `6a38cb0`, and the same paragraph's round-8 update names `6a38cb0`. Round 7 kept the clause only because `332b2c1` was then also the HEAD catalog (R7-BH1).
  - The D1 entry (`:740`) tells the isolated-journey rerun to set `HexalithEventStoreVersion=3.125.1`. The Story 5.1 agent entry-point entry (`:756`) says to build the Debug source-reference mode with `-p:HexalithEventStoreVersion=3.125.1`. Round 8 found that this value "now forces an older EventStore than both HEAD defaults" and replaced it in launch readiness, `docs/ci-cd.md` and `epic-5-context.md`, but not in these two entries.
  - The round-7 Story 5.1 handoff (`:772`) says "HEAD pins `references/Hexalith.EventStore` at `9b66153`". Since `929ea22`, the pin is `6c56c27`.

  Fix:
  - Reword the verdict to "It ran on the then-current Builds catalog `332b2c1`, but with the pre-change …". The asserted substring after "but with" is unchanged.
  - Append dated round-9 updates to `:740` and `:756`. They replace `3.125.1` with the catalog's EventStore version, passed only when the Works default differs (as `-p:HexalithEventStoreVersion=<catalog version>`).
  - Append a dated update to `:772` naming `6c56c27` as the pin since `929ea22`.
  - Leave D2's dated 2026-10-18 sentence (`:744`, "only `-p:HexalithEventStoreVersion=3.125.1` passes") unchanged.

  [R9-AA1, R9-BH1, R9-AA2, R9-BH2, R9-AA5, R9-BH3, acceptance-auditor+blind-hunter]
  Applied 2026-10-20: the verdict now says "the then-current Builds catalog `332b2c1`". Dated round-9 updates on the D1 and agent entry-point entries replace `3.125.1` with the catalog version, passed only when the Works default differs. The D1 update also records that a foreign `eventstore` Dapr app was running at this review. The round-7 handoff names `6c56c27` as the pin since `929ea22`. D2's dated sentence is unchanged.
- [x] [Review][Patch] The round-8 evidence anchor names "HEAD `929ea22`" and not the commit that published the round-8 documents [docs/launch-readiness.md:53, :679, :683, :688, :689, :691, :692, :834; docs/ci-cd.md:176; _bmad-output/implementation-artifacts/epic-5-context.md:34; _bmad-output/implementation-artifacts/deferred-work.md:728; _bmad-output/implementation-artifacts/evidence/5-2/20261020-round8/README.md:9]
  - Twelve durable statements say "HEAD `929ea22`", but HEAD is already `5258598`. This is the same pattern as the "Current Timesheets HEAD pins EventStore `3099cb27`" defect handed to Story 5.1.
  - The receipt's documentation hashes match `5258598`. R8-AA3 fixed the same gap for round 7 with "committed as `c5ac600`".
  - Fix: anchor round 8 as "the worktree based on `929ea22c977385590d77d95f73af86e8b051c4fc` (documentation and ledger edits only), committed as `5258598…`" in the preamble, the verdict's round-8 update and the packet README, following the round-7 form. Replace the remaining "HEAD `929ea22`" with "`929ea22`".

  [R9-BH4, R9-BH5, blind-hunter]
  Applied 2026-10-20: the preamble, the verdict's round-8 update and the packet README anchor round 8 as the worktree based on `929ea22…`, committed as `525859841e3dc329f65970b7067d3ef3bd1a91fa`. The gate row adds "committed as `5258598`". No "HEAD `929ea22`" wording remains in launch readiness, `docs/ci-cd.md`, `epic-5-context.md`, the ledger or the packet.
- [x] [Review][Patch] "1,959 passing Release tests" counts the 5 skips as passes [docs/launch-readiness.md:679, :834; _bmad-output/implementation-artifacts/deferred-work.md:779; _bmad-output/implementation-artifacts/evidence/5-2/20261020-round8/README.md:23]
  - The receipt records `tests_total` 1959, `tests_failed` 0 and `tests_skipped` 5, so 1,954 passed.
  - The gate row says "1,959 passing Release tests with 5 intentional skips". The verdict, the ledger resolution note and the README say "passed 1,959 tests with 0 failures and 5 intentional skips".
  - The story record's "1,959 tests, 0 failures and 5 intentional skips" is correct.
  - Fix: "ran 1,959 tests: 1,954 passed, 5 intentional skips, 0 failures".

  [R9-AA3, acceptance-auditor]
  Applied 2026-10-20: the verdict, the gate row, the ledger resolution note and the packet README say "1,959 tests: 1,954 passed, 5 intentional skips, 0 failures".
- [x] [Review][Patch] The Direct row states the round-7 drift in the present tense with no round label [docs/launch-readiness.md:688]
  - "On 2026-10-20 … The only reported direct update is catalog-owned `Hexalith.EventStore.*` `3.125.1 -> 3.126.0`" is followed by a second "On 2026-10-20" run at `929ea22` that reported no direct update. Only the cell's last clause identifies the first sentence as round-7 evidence.
  - Fix: label the first run "round 7, committed as `c5ac600`" and use the past tense. Keep the phrase "no Timesheets-owned direct package updates", which `LaunchReadinessTests.cs:269` asserts.

  [R9-AA4, R9-BH7, acceptance-auditor+blind-hunter]
  Applied 2026-10-20: the first run is labelled "in round 7 (committed as `c5ac600`)" and the second "in round 8 at `929ea22`". The drift sentence is in the past tense, and the asserted phrase is unchanged.
- [x] [Review][Patch] The Direct row's resolved-version list omits the six direct EventStore packages [docs/launch-readiness.md:688]
  - The round-8 package listing resolves `Hexalith.EventStore.Aspire`, `.Client`, `.Contracts`, `.DomainService`, `.Server` and `.Testing` at `3.126.0`. These are the packages that drove the round-7 and round-8 direct results.
  - The row lists every other direct package, and the Aspire line is covered by "AppHost-generated direct assets resolve at `13.6.1`".
  - Fix: add "the six catalog-owned `Hexalith.EventStore.*` direct packages `3.126.0` (dated observation at `929ea22`)".

  [R9-BH6, blind-hunter]
  Applied 2026-10-20: the list adds the six catalog-owned `Hexalith.EventStore.*` direct packages at `3.126.0` as a dated observation at `929ea22`.
- [x] [Review][Patch] The transitive row does not say that the manual AppHost registry fallback was not rerun after 2026-10-18 [docs/launch-readiness.md:691]
  - The risk cell says AppHost-only drift "depends on the manual registry fallback". That fallback last ran on 2026-10-18, on the source-reference graph with EventStore `3.125.1`.
  - The round-7 and round-8 updates report only the failing solution/AppHost lane and the 14 project fallbacks. The "Not rerun" lists do not mention the manual fallback.
  - Fix: add one sentence to the round-8 update: the manual AppHost registry fallback was not rerun in rounds 7 or 8, and its 2026-10-18 result remains the latest AppHost-only drift evidence.

  [R9-BH9, blind-hunter]
  Applied 2026-10-20: the round-8 update says the manual AppHost registry fallback was not rerun in round 7 or round 8, so its 2026-10-18 result remains the latest AppHost-only drift evidence.
- [x] [Review][Patch] `TimesheetsProjectFiles()` skips any `References` path segment case-insensitively [tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/RepositoryRoot.cs:39]
  - `StringComparer.OrdinalIgnoreCase` also matches the Timesheets-owned `src/Hexalith.Timesheets.Server/References` and `src/Hexalith.Timesheets.Contracts/References` folders.
  - A project file added under either folder would be silently skipped by the inline-version and Fluent UI V4 guards.
  - The submodule and worktree directories are lowercase `references`.
  - Fix: `StringComparer.Ordinal`, then rebuild and rerun the ArchitectureTests.

  [R9-VG2, verification-gap]
  Applied 2026-10-20: the helper uses `StringComparer.Ordinal`. It selects the same 16 project files as before on the current tree. A non-incremental `-warnaserror` Release ArchitectureTests build passed with 0 warnings and 0 errors, and the executable passed 64/64 (exit 0).

**Deferred**

- [x] [Review][Defer] Round 8 reran only the package-mode graph, not the source-reference or live lanes, and did not record whether live-lane prerequisites existed [_bmad-output/implementation-artifacts/5-2-reconcile-package-currency-and-platform-dependency-versions.md:657] — deferred: disclosed. The round-8 record and the Works forwarding entry say the README source-reference default lane and the live AppHost lanes were not rerun, and D1, D2 and the Works forwarding entry own those lanes, so no new ledger entry. At this review (2026-10-20, about 13:00 local), a foreign Projects AppHost holding Dapr app ID `eventstore` and five other Dapr apps were running, so the isolated journey's preflight would refuse. [R9-AA6, R9-BH8, acceptance-auditor+blind-hunter]
- [x] [Review][Defer] No normally run lane starts the `13.6.1` AppHost with NuGet `Aspire.Hosting.Orchestration`/`Aspire.Dashboard.Sdk` [src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj:4] — deferred: this repeats R8-VG1 and is already ledgered (D1 at `deferred-work.md:739` and the automated AppHost runtime smoke entry at `:233`), so no new ledger entry. [R9-VG1, verification-gap]
- [x] [Review][Defer] `CLAUDE.md:93` still says "There is no `.github/workflows`" [CLAUDE.md:93] — deferred: outside this diff. It is already ledgered for Story 5.1's agent entry-point sync (round-4 deferral), and agent-context edits belong there, so no new ledger entry. [R9-VG3, verification-gap]

**Rejected** (9 findings)

- `architecture.md:365` points to the launch-readiness verdict "(2026-10-18)" (R9-AA7) — false: the verdict is still introduced as "audited on 2026-10-18" and carries its dated 2026-10-20 updates, so the pointer reaches the current state.
- The round-8 test receipts have no `exit_code` (R9-BH10) — low: each summary records 0 failed and 0 errors, and the packet README claims "test summaries", not test exit codes. Adding exit codes would mean running the suites again.
- `R7-BH2` names two findings (R9-BH11) — the fix edits the spec under review.
- The spec Change Log stops at 2026-10-18 (R9-BH12) — the fix edits the spec under review. The story record carries the round-7 and round-8 decisions.
- The dated prerelease-waiver row is checked against the live catalog (R9-BH13) — false: this is the intended tripwire (R2-BH9). A catalog move fails loudly and prompts a new dated observation. The round-8 dating decision covered the unguarded EventStore and Works values only.
- The round-7 Builds handoff was resolved with its checks after the catalog adoption (R9-BH14) — false: the resolution note says the sync moved the catalog and the review "then ran" the checks, so it records the order. The entry required only package-mode checks, and all of them ran and passed.
- The Direct row's phrase assertion can be satisfied by an older dated sentence (R9-EH1) — low: it needs a contradicting PASS update that nobody has written, and the fix adds segment parsing to a prose guard. R2-BH8 and R2-EH4 already scoped this guard to its section.
- An XML comment naming `ASPIRE010` in the AppHost project breaks the suppression guard (R9-EH2) — low: it fails loudly with a clear message, the project has no such comment, and the fix adds comment stripping.
- A backslash `ProjectReference` to the EventStore Aspire helper fails the security-helper guard (R9-EH3) — low: the same ground as R2-EH7. It fails loudly, and that reference uses forward slashes.


## Implementation Notes

_2026-10-18 re-baselined iteration:_

- The AppHost now pins `Aspire.AppHost.Sdk/13.6.1` with `AspireUseCliBundle=false` and an unconditional
  `NoWarn` of `$(NoWarn);ASPIRE010`. The forced no-cache restore yields one Aspire `13.6.1` line, including orchestration
  and dashboard packages, in both the package-mode and the source-reference graphs. The build raised no new Aspire diagnostic.
- No direct NuGet update was available, so `Directory.Packages.props` is unchanged and no transitive pin was added. Root
  npm is recorded as applicable, with eight high `npm audit` findings visible as CONCERNS; no npm file changed.
- The default README source-reference test run still hits the deferred Works `HexalithEventStoreVersion` forwarding
  mismatch (`3.124.0` versus catalog `3.125.1`). Verification therefore used the established
  `-p:HexalithEventStoreVersion=3.125.1` mode, and launch readiness records the divergence as open and unwaived, with owner, risk, and revisit
  condition (review patch P1).
- The isolated DCP journey refused at preflight because foreign AppHosts and Dapr applications were running. The 35
  compiled no-resource AppHost probes passed. Current runtime smoke stays deferred.
- Story status stays `in-progress`: the review transition needs the launch-readiness open-story sentences (pinned by
  `LaunchReadinessTests`, with the overall decision owned by Story 5.1) to change in the same step.

_2026-09-12 iteration (historical; the CLI-bundle choice was reversed by the Option 2 decision):_


- The Aspire 13.5.3 update introduced warnings-as-errors diagnostic `ASPIRE010`. The AppHost now opts into the
  version-aligned CLI bundle with `AspireUseCliBundle=true`; resource topology is unchanged.
- The imported central catalog already supplies every current stable direct package, so `Directory.Packages.props`
  remains unchanged and no transitive package was promoted to a root dependency.
- The platform-owned Keycloak, CommunityToolkit Aspire Dapr, and Fluent UI V5 prerelease entries remain explicit
  exceptions with owner, risk, and revisit conditions in launch readiness.
- The June Story 5.2 record and dated Story 5.3 SDK 10.0.400 evidence remain intact; reopened results were appended.

## Spec Change Log

- 2026-09-12 -- Implemented the approved SDK/AppHost baseline, reconciled package evidence, completed every task, and
  recorded verification plus remaining tooling/platform risks.
- 2026-10-18 -- Human renegotiated the frozen intent (option A): the SDK baseline is `10.0.402` (moved in `c4695a5`),
  the AppHost SDK follows the imported stable Aspire `13.6.1` with the decided Option 2 orchestration shape, and root npm
  is applicable since release tooling added `package.json`. Tasks were reset for the re-baselined iteration; this
  iteration's diff starts at `rebaseline_commit`. The 2026-09-12 Implementation Notes and Results below are historical.
- 2026-10-18 -- Implemented the re-baselined tasks: AppHost `13.6.1` with NuGet-backed orchestration, split fitness guards,
  re-validated and applied all fifteen 2026-09-12 review patches, reconciled package evidence, and recorded verification,
  the open Works EventStore-version divergence, and the unavailable DCP lane. The frozen intent is unchanged.

- 2026-10-20 -- Human acceptance: the user instructed “apply recommended” after the final walkthrough. The
  [scoped acceptance](evidence/5-2/20261020-final-acceptance/story-acceptance.json) accepts the approved baseline,
  audit reconciliation, and completed round-9 patches and closes Story 5.2 with package currency still CONCERNS.
  Stories 3.6/3.7, current-graph runtime qualification, and the independent legal/policy/UAT release gates remain
  open; release remains FAIL. Existing review dispositions, dated evidence, residual owners, and frozen intent
  are unchanged. No release, deployment, commit, or push is authorized.

## Review Triage Log

| Finding | Verdict | Evidence | Route |
|---|---|---|---|
| BH1 | medium | `epic-5-context.md` still names Dapr `1.18.4`, while the implemented architecture and imported catalog use stable `1.18.7`; future story planning can select the obsolete value. | patch |
| BH2 | low | The legacy Story 5.2 body still says `done`; the spec-to-sprint `in-review`/`in-progress` difference is an expected workflow transition, but the legacy status and reopened baseline remain misleading. | patch |
| BH3 | medium | The approved correction requires an overall `FAIL` while Stories 3.6 and 5.1 remain open, whereas launch readiness already said `CONCERNS` before this package story. The final verdict is pre-existing Story 5.1 work, not package-baseline behavior. | defer |
| BH4 | medium | The new platform waiver has owner, risk, and revisit data but omits the approved course-correction artifact required by the regenerated Epic 5 waiver rule. | patch |
| BH5 | low | Other prereleases exist in the broad shared catalog, but restored Timesheets assets contain only Keycloak and CommunityToolkit prereleases; the wording nevertheless overstates its three entries as the complete catalog inventory. | patch |
| BH6 | medium | Build and source-text checks did not execute the upgraded AppHost. A review-time `aspire start` now proves the security resource reaches healthy state and can be recorded without adding a slow default infrastructure test. | patch |
| BH7 | medium | Raw substring checks can pass for commented, conditional, or conflicting AppHost XML and therefore do not durably guard the effective local SDK/property values. | patch |
| BH8 | medium | The package-evidence fitness test searches prose globally and can pass on historical strings without comparing the current section to `global.json`, AppHost XML, or the imported catalog. | patch |
| BH9 | low | The spec command omits `--force --no-cache`, although the recorded run used both flags. The only direct correction edits this build's spec, so the review protocol rejects it. | rejected (spec-only) |
| BH10 | medium | Regenerating Epic 5 context dropped the explicit prohibition on logging payloads, comments, personal data, tokens, secrets, and command bodies; `privacy-safe` is insufficient future implementation guidance. | patch |
| BH11 | medium | Regenerating Epic 5 context dropped the tenant-time-zone, UTC-audit, tenant-local-period-key, and DST/boundary evidence invariants needed by later Epic 5 work. | patch |
| BH12 | low | The reopened file list mentions the spec only as transiently untracked instead of listing it as an intended delivered artifact. | patch |
| VG1 | medium | The AppHost SDK/CLI-bundle change lacked runtime startup evidence; review-time Aspire lifecycle verification demonstrated successful startup and healthy composed security resource. | patch |
| EH1 | medium | Independent edge tracing confirms the Epic 5 Dapr `1.18.4` statement conflicts with the current `1.18.7` package baseline. | patch |
| EH2 | false | Installed Aspire CLI is `13.5.3`, matching the AppHost SDK; official Aspire 13.5 guidance states default `Path` mode selects a compatible CLI and falls back to the SDK-paired DNX package, so `DnxPinned` is not required. | rejected |
| EH3 | medium | Independent edge tracing confirms inactive/commented XML can satisfy the new AppHost substring assertions. | patch |
| EH4 | medium | Independent edge tracing confirms historical prose can satisfy global string assertions even when the current package-verdict section regresses. | patch |
| EH5 | medium | CLI-bundle opt-in deliberately changes orchestration dependency resolution, so an unqualified no-runtime-change claim is too broad; topology and domain/product behavior are what remain unchanged. | patch |
| R2-BH1 | false | Spec `in-review` vs story/sprint `in-progress` is the workflow's own step-04 transition; finalization syncs status (accepted precedent), and the fix would edit this spec. | rejected |
| R2-BH2 | low | `deferred-work.md` Story 5.2-review bullets still say "approved `10.0.401`" and "`docs/launch-readiness.md:78` still records `CONCERNS`" although `global.json` is `10.0.402` and the record says `FAIL`; the second now duplicates the moved Story 5.1 entry. | patch (P7) |
| R2-BH3 | false | The full-suite fitness entry is pre-existing, still accurate, and outside the package-currency intent; leaving it open produces no wrong outcome. | rejected |
| R2-BH4 | medium | Release-gate `Build`/`Tests (full suite)` rows say PASS citing round-20 runs, while the default README source-reference test mode fails 735 cases without `-p:HexalithEventStoreVersion=3.125.1`; the Works forwarding mismatch predates this story and was already ledgered. | defer (D2) |
| R2-BH5 | medium | The platform row, current verdict, and gate notes present the Works EventStore-version divergence as waived under proposal §7, but §7 approves only bounded Story 5.2 SDK/package updates and recorded prerelease exceptions; the 2026-10-18 option-A renegotiation is not cited as approval for the `13.6.1` baseline. | patch (P1) |
| R2-BH6 | medium | `architecture.md:402` says ASP.NET Core auth/data-protection "currently `10.0.12`" and `:495` says OpenApi `10.0.12`/Swashbuckle `10.2.3`; the imported catalog has `10.0.13`/`10.3.0`, contradicting the new launch-readiness direct row. | patch (P2) |
| R2-BH7 | medium | `ArchitectureVersionNotesMatchExecutableConfiguration` guards only the Data Architecture notes; the hand-edited restatements at `architecture.md:331`, `:402-403`, `:581`, `:756`, `:1077`, `:1099`, `:1121` are unguarded, and `:402` has already drifted. | patch (P3) |
| R2-BH8 | low | `AppHostSdkTracksTheImportedStableAspireCatalogLine` compares with `Aspire.Hosting` instead of the catalog's canonical `HexalithAspireAppHostSdkVersion` (`Directory.Packages.props:20`), which platform tests treat as the AppHost SDK contract; a hosting-only hotfix would force the wrong SDK. | patch (P4) |
| R2-BH9 | low | A Builds catalog bump fails several guards at once, but each fails loudly with a remediation message; this is the tripwire the 2026-09-12 alignment patch requested, and documenting a cross-repo procedure is more than a direct correction. | rejected |
| R2-BH10 | low | The `Package currency` gate verdict cell and the paragraph's `**CONCERNS**` are not asserted; flipping them is unlikely in everyday use and the fix adds assertions. | rejected |
| R2-BH11 | low | `ReadParagraph` splits on `"\n\n"` without normalizing `\r`, so a CRLF checkout (no `.gitattributes`) fails the package-verdict test on a correct document; sibling helpers in the same diff already trim `\r`. | patch (P5) |
| R2-BH12 | low | The security-helper guard checks only the source-mode `ProjectReference`, as the pre-split test did; package-mode coverage is a pre-existing gap and the fix adds assertions. | rejected |
| R2-BH13 | medium | `AppHost_retains_nuget_orchestration_and_suppresses_only_aspire010` reads only the csproj, so an Aspire suppression in root `Directory.Build.props`/`.targets`, `.editorconfig`, or an AppHost `#pragma`/`SuppressMessage` passes despite the test's "No other Aspire diagnostic may be suppressed" message. | patch (P6) |
| R2-BH14 | low | Audit rows do not say which restore graph each `--no-restore` audit read (package-mode vs source-reference), and the package-mode AppHost transitive graph was not separately compared, so "audits are clean" overstates graph coverage. | patch (P8) |
| R2-BH15 | low | The README-mode "ArchitectureTests 61 total / 1 failed" run matches neither HEAD (59) nor the final tree (63) and the record does not say it was an intermediate tree. | patch (P9) |
| R2-BH16 | low | The privacy patch's `[x] applied` mark and Change Log "applied all fifteen" overstate a record-only correction, but the fix edits this spec. | rejected |
| R2-BH17 | low | The vocabulary reconciliation handed to Story 5.1 in `epic-5-context.md` has no ledger entry; the record/approved vocabulary contradiction is pre-existing. | defer (D3) |
| R2-BH18 | low | `LoadImportedPackageCatalog`'s comment claims it reads "the catalog the build actually imports" but it ignores `Import`/property conditions and global properties and picks the first existing candidate. | patch (P10) |
| R2-BH19 | low | Double blank line in the spec's Implementation Notes; the fix edits this spec. | rejected |
| R2-VG1 | medium | Pre-verified gap: no automated or manual run starts the `13.6.1` AppHost with NuGet `Aspire.Hosting.Orchestration`/`Aspire.Dashboard.Sdk`; the 9523d94 DCP-parent proof ran on the CLI-bundle DCP, and the isolated journey refused at preflight because foreign runtimes were active. | defer (D1) |
| R2-VG2a | medium | Pre-verified gap: same root cause as R2-BH13 — the suppression guard does not scan root build props/targets, `.editorconfig`, or AppHost sources. | patch (P6) |
| R2-VG2b | low | Pre-verified gap: no test reads `obj/project.assets.json` for the restored orchestration line; such a test depends on build mode and RID, and `AppHostSdkTracksTheImportedStableAspireCatalogLine` already enforces the version invariant, so the guard is not worth its brittleness. | rejected |
| R2-VG3 | low | Same root cause as R2-BH8 (alignment compared with `Aspire.Hosting`, not `HexalithAspireAppHostSdkVersion`). | patch (P4) |
| R2-EH1 | low | Same root cause as R2-BH11 (`ReadParagraph` CRLF). | patch (P5) |
| R2-EH2 | low | The alignment test does not reject a prerelease `Aspire.Hosting`; a prerelease stable catalog line is unlikely and the fix adds a guard. | rejected |
| R2-EH3 | medium | Same root cause as R2-BH7 (unguarded architecture restatements). | patch (P3) |
| R2-EH4 | low | A root lockfile without `package.json` would demand `not applicable`; that layout is unlikely and the fix adds a condition. | rejected |
| R2-EH5 | low | An escaped or code-span pipe would split a row into more than four cells; the guard fails loudly, no current row contains one, and the fix adds regex complexity. | rejected |
| R2-EH6 | medium | Same root cause as R2-BH13 (suppression surfaces outside the csproj). | patch (P6) |
| R2-EH7 | false | A backslash `ProjectReference` would fail loudly, which is correct behavior; the AppHost uses forward slashes and nothing shows the backslash form is reachable. | rejected |
| R2-EH8 | medium | Same root cause as R2-BH4 (unqualified `Tests (full suite)` PASS row). | defer (D2) |
| R2-EH9 | low | Same root cause as R2-BH14 (restore graph per audit unnamed). | patch (P8) |
| R2-EH10 | medium | Same root cause as R2-BH6 (`architecture.md:402` stale ASP.NET Core line). | patch (P2) |
| R2-EH11 | medium | `epic-5-context.md:35` still says fitness tests reject "a Timesheets-owned Works declaration/gitlink", contradicting the corrected Works paragraph and `WorksCheckoutGovernanceTests`, which require the `references/Hexalith.Works` declaration. | patch (P11) |
| R2-EH12 | low | Same root cause as R2-BH2 (stale Story 5.2-review ledger bullets). | patch (P7) |
| R2-EH13 | low | Same root cause as R2-BH18 (`LoadImportedPackageCatalog` overclaims build-equivalent evaluation). | patch (P10) |

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet --version` -- expected: `10.0.402`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx --force --no-cache -m:1 /nr:false` -- expected: success with regenerated assets.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` -- expected: zero warnings and errors.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --outdated --no-restore` plus `--include-transitive`, `--vulnerable --include-transitive`, and `--deprecated --include-transitive` variants -- expected: direct/vulnerability/deprecation clean; any graph-tool failure recorded with per-project fallback evidence.
- `npm ci`, `npm outdated`, `npm audit` at the repository root -- expected: results recorded verbatim; nonzero exits are evidence, not blockers.
- Run each built executable under `tests/*/bin/Debug/net10.0/` (`dotnet test` fails under Microsoft.Testing.Platform, see `README.md`) -- expected: all non-skipped tests pass and exact project totals are captured.
- `git diff --check` and `git diff --name-only 180d9fd9a635ed44ba896688d6a06e38a33e9f28` -- expected: clean patch and an in-scope file list.

**2026-10-18 results (SDK 10.0.402 / AppHost 13.6.1):**

- `dotnet --version` returned `10.0.402`. The forced no-cache bare restore passed for 15 projects, and the bare
  warnings-as-errors Release build passed with 0 warnings and 0 errors; both were rerun on the final tree. The AppHost
  assets hold one Aspire `13.6.1` line, including `Aspire.Hosting.Orchestration.linux-x64` and
  `Aspire.Dashboard.Sdk.linux-x64`, and no `13.5.x` package.
- The direct, vulnerable, and deprecated solution audits covered 15 projects and were clean. The solution-level and
  AppHost transitive lanes fail with `error: Sequence contains no matching element`. The 14 project fallbacks (21 drift
  items) and a manual AppHost registry fallback (22 stable items behind latest, plus 2 waived prereleases) were reviewed
  with no pin.
- `npm ci` exit 0. `npm outdated` exit 1 (2 exact-pinned `@commitlint` packages, `21.2.2` -> `21.2.4`). `npm audit`
  exit 1 (8 high, 0 critical, braces `GHSA-vfj7-8cjw-p6xm`).
- The README source-reference build passed with 0 warnings and 0 errors, but its tests failed on the deferred Works
  EventStore-version forwarding mismatch (Server 135, Projections 351, and Integration 249 failures). With
  `-p:HexalithEventStoreVersion=3.125.1` (Rebuild, then a final incremental build, each with 0 warnings and 0 errors):
  ArchitectureTests 63/63; Contracts.Tests 92/92; Server.Tests 558/558; Projections.Tests 437/437; IntegrationTests
  727 passed / 5 skipped / 732 total; Works.Tests 76/76. Total: 1,953 passed, 5 intentional skips, 0 failed, 1,958 total.
- The compiled AppHost guard probes passed 35/35. The isolated DCP journey refused at preflight (`A Dapr application is
  already running or inventory is unsafe; no AppHost started`) because foreign runtimes were active; no foreign runtime
  was touched.
- Six temporary mutations of the AppHost project, launch readiness, and architecture were each caught by the new guards
  and then restored byte-identical.
- `git diff --check` passed. `git diff --name-only 180d9fd9a635ed44ba896688d6a06e38a33e9f28` lists only the story
  record, `deferred-work.md`, `epic-5-context.md`, this spec, `architecture.md`, `docs/ci-cd.md`,
  `docs/launch-readiness.md`, the AppHost project, and the two ArchitectureTests fitness files.

**2026-09-12 results (historical, SDK 10.0.401 / AppHost 13.5.3):**

- SDK `10.0.401`; forced/no-cache restore succeeded; warnings-as-errors build succeeded with 0 warnings and 0 errors.
- Direct, vulnerable, and deprecated solution audits covered all 15 projects and were clean. Solution/AppHost
  transitive-outdated evaluation remains unavailable with exact error `Sequence contains no matching element`; 14
  project fallbacks succeeded and were reviewed with no pin.
- ArchitectureTests 52/52; Contracts.Tests 88/88; IntegrationTests 83 passed / 4 skipped / 87 total;
  Projections.Tests 77/77; Server.Tests 420/420; Works.Tests 76/76. Total: 796 passed, 4 intentional skips, 0 failed,
  800 total.
- Matrix audit: approved baseline is covered by `Repository_pins_approved_dotnet_sdk` and the AppHost scaffold guard;
  CPM/no-inline update behavior is covered by `BuildConfigurationTests`; transitive no-pin and audit-tool failure
  evidence are covered by the package-currency launch-readiness guard. All covering ArchitectureTests ran and passed.
- `git diff --check` passed. The tracked diff contains no `references/` path or gitlink, inline package version,
  legacy `.sln`, npm manifest, UI, topology, persistence, or product-behavior change.
