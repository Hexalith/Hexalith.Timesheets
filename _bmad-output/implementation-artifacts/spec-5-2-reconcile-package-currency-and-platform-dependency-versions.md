---
title: 'Story 5.2: Reconcile Package Currency and Platform Dependency Versions'
type: 'chore'
created: '2026-09-12'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '72be616918e0ae80fadf56fa152dbd561955e22d'
context:
  - '_bmad-output/implementation-artifacts/epic-5-context.md'
  - '_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 5.2 was closed against an obsolete SDK and AppHost baseline. Timesheets still pins .NET SDK `10.0.400` and `Aspire.AppHost.Sdk/13.4.6`, while the approved release baseline is SDK `10.0.401`, AppHost SDK `13.5.3`, and the latest stable direct packages compatible with `net10.0`.

**Approach:** Update the two Timesheets-owned pins, regenerate restore assets, rerun package and full test evidence, and reconcile the current architecture and launch-readiness package claims without rewriting dated historical evidence.

## Boundaries & Constraints

**Always:** Keep NuGet versions under Central Package Management with no inline `PackageReference` versions; prefer stable `net10.0`-compatible direct packages; audit direct, transitive, vulnerable, and deprecated packages after a clean SDK `10.0.401` restore; retain root npm as not applicable while no root manifest exists; preserve Story 5.3's dated SDK `10.0.400` evidence; record unavailable tooling and any remaining platform exception with owner, risk, and revisit condition.

**Never:** Edit `references/*`, sibling package manifests, or gitlinks; add a transitive pin without a compatibility, security, or deterministic-build reason; adopt a new prerelease package without explicit approval; add a `.sln`, inline version, npm manifest, UI, topology, persistence, or product behavior; fold Story 5.1's final launch-verdict and synchronized agent-guidance reconciliation into this story.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Approved baseline | SDK `10.0.400`; AppHost SDK `13.4.6` | Pins become `10.0.401` and `13.5.3`; clean restore removes mixed Aspire assets | Build/test failure blocks completion and remains recorded |
| Direct update found | New stable, compatible direct version | Apply only through Timesheets-owned CPM configuration | Do not edit the imported catalog or use an inline version |
| Transitive-only drift | Closest direct packages are current and audit-clean | Record drift and add no pin | Pin only with documented compatibility/security/determinism evidence |
| Audit-tool failure | Package listing cannot evaluate a graph | Preserve exact failure and run per-project fallback checks | Never report the failed lane as clean |

</frozen-after-approval>

## Code Map

- `global.json` -- executable SDK source of truth; update `10.0.400` to `10.0.401`, retaining `latestPatch` roll-forward.
- `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj` -- Timesheets-owned `Aspire.AppHost.Sdk` pin; align `13.4.6` to imported stable `13.5.3` without changing topology.
- `Directory.Packages.props` -- root CPM/import policy; currently needs no edit because the `Hexalith.Builds` catalog already supplies the approved direct matrix.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/ScaffoldGovernanceTests.cs` -- reuse the existing AppHost guard and add an exact `global.json` SDK/roll-forward guard.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/BuildConfigurationTests.cs` -- existing `.slnx`, CPM, and no-inline-version guards; do not weaken them.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` -- existing four-dimension package-evidence guard; change only if current evidence needs a durable new assertion.
- `docs/launch-readiness.md` -- append SDK `10.0.401` package-audit/build/test evidence and replace stale current-package claims while preserving the dated Story 5.3 run.
- `_bmad-output/planning-artifacts/architecture.md` -- reconcile current SDK, Aspire, Dapr, and Fluent UI catalog claims; leave historical observations explicitly dated.
- `_bmad-output/implementation-artifacts/5-2-reconcile-package-currency-and-platform-dependency-versions.md` -- preserve the June record and append the reopened-story work, exact commands, counts, and file list.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- use the resolved `5-2-reconcile-package-currency-and-platform-dependency-versions` key for workflow status transitions.

## Tasks & Acceptance

**Execution:**
- [x] `global.json`, `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj` -- apply the approved SDK/AppHost pins, then perform a clean restore before judging resolved packages.
- [x] `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/ScaffoldGovernanceTests.cs` -- update the AppHost expectation and guard SDK `10.0.401` plus `latestPatch`.
- [x] `docs/launch-readiness.md`, `_bmad-output/planning-artifacts/architecture.md` -- record the current package matrix and honest direct/npm/transitive/platform verdicts, preserving historical evidence and explicit remaining exceptions.
- [x] `_bmad-output/implementation-artifacts/5-2-reconcile-package-currency-and-platform-dependency-versions.md` -- append reopened implementation evidence, exact test counts, decisions, and a `git diff --name-only` file list without erasing the June record.
- [x] Run restore, warnings-as-errors build, ArchitectureTests, every project under `tests/`, and all four package-audit dimensions; record fallbacks and failures honestly.

**Acceptance Criteria:**
- Given the approved release baseline, when configuration and fitness tests are inspected, then SDK `10.0.401`, `rollForward: latestPatch`, and `Aspire.AppHost.Sdk/13.5.3` agree.
- Given restored package assets, when direct, transitive, vulnerable, and deprecated audits run, then compatible direct updates use CPM only, unjustified transitive pins are absent, and unavailable audit lanes are visible.
- Given package evidence is published, when launch-readiness and architecture are compared with current files, then direct currency, npm applicability, transitive drift, platform alignment, and dated historical runs are non-contradictory.
- Given repository boundaries, when the final diff is inspected, then no sibling content/gitlink, inline version, legacy solution, UI, topology, or runtime behavior changed.
- Given verification completes, when every test executable is run, then all non-skipped tests pass with warnings as errors and exact totals are recorded.

## Implementation Notes

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

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet --version` -- expected: `10.0.401`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` -- expected: success with regenerated assets.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` -- expected: zero warnings and errors.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --outdated` plus transitive, vulnerable, and deprecated variants -- expected: direct/vulnerability/deprecation clean; any graph-tool failure recorded with per-project fallback evidence.
- Run each built executable under `tests/*/bin/Debug/net10.0/` -- expected: all non-skipped tests pass and exact project totals are captured.
- `git diff --check` and `git diff --name-only` -- expected: clean patch and an in-scope file list.

**Results:**

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
