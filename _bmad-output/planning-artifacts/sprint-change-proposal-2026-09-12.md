---
title: "Sprint Change Proposal: Reconcile Implementation Readiness After Sprint Closure"
project: timesheets
date: 2026-09-12
status: approved
mode: batch
scope: moderate
---

# Sprint Change Proposal: Reconcile Implementation Readiness After Sprint Closure

## 1. Issue Summary

### Trigger

The post-sprint implementation-readiness review found four contradictions between the completed sprint ledger and the repository evidence:

1. `sprint-status.yaml` marks Epic 3 and Story 3.6 done, but the running host cannot resolve a valid magic link because `MagicLinkTokenHashCapabilityIndexProjection` is a pure fold/read model with no projection-host registration or event-delivery path that populates its `IReadModelStore` entry.
2. Epic and story acceptance criteria claim rendered dashboard/UI component behavior and WCAG evidence, while the repository contains no Timesheets UI project and repository guidance expressly forbids adding one. The implementation proves contracts, metadata, server composition, and in-process behavior—not rendered component behavior.
3. PRD questions Q6–Q8 remain textually open even though the completed implementation already embodies bounded v1 choices for Activity Type governance, CSV export, and sensitive-comment defaults.
4. The synchronized agent entry points name SDK `10.0.302`, while `global.json`, architecture, build evidence, and launch-readiness evidence use `10.0.400`. The approved correction now supersedes both values with SDK `10.0.401` and the latest stable, .NET 10-compatible direct packages.

These are not equivalent issues. The magic-link gap is unfinished behavior against an unchanged v1 functional requirement. The UI issue is a repository ownership/scope mismatch. Q6–Q8 are documentation decisions. The SDK/package direction is a bounded release-baseline correction owned by the existing package-currency story, followed by documentation synchronization.

### Evidence

| Concern | Repository evidence | Finding |
|---|---|---|
| Sprint closure | `_bmad-output/implementation-artifacts/sprint-status.yaml` | Every epic and story is marked `done`; the file has a pre-existing user edit that must be merged, not replaced. |
| Magic-link v1 requirement | PRD FR14 and MVP §7.1; `epics.md` Epic 3 and Story 3.6 AC1 | Valid, single-use, scoped links are still an in-scope v1 behavior, and Story 3.6 explicitly requires live-host describe/confirm/adjust to resolve token state. |
| Magic-link runtime | `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs`; `MagicLinkTokenHashCapabilityIndexProjection.cs`; `src/Hexalith.Timesheets/Program.cs` | The loader reads the fixed read-model key, but no Timesheets projection handler or host registration writes it. A missing entry returns the opaque unavailable state, so valid tokens fail closed. |
| Magic-link story record | `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md` | The completion record claims live-host functionality while its scope note and review observations acknowledge that projection-host wiring is absent. |
| Launch classification | `docs/launch-readiness.md` | Valid-link end-to-end resolution is labeled `waived`, even though FR14 and the Story 3.6 live-host acceptance criterion remain unchanged. |
| UI implementation boundary | `AGENTS.md`; `README.md`; current `src/` and `tests/` project lists | No `Hexalith.Timesheets.UI` or UI test project exists. Future UI must be composed through FrontComposer and Fluent UI V5; a Timesheets UI project must not be added. |
| UI acceptance overclaim | `epics.md` NFR13/UX-DR coverage and Story 4.7; `_bmad-output/implementation-artifacts/4-7-surface-timesheets-dashboard-overview.md` | Story 4.7 AC7 claims component, focus, keyboard, and WCAG verification, while completion evidence is metadata, contract, server, and in-process integration testing. |
| Q6 | PRD §14 Q6; Stories 1.5/1.6; `ProjectActivityTypeCommandService`; `ConfigureProjectActivityTypeCatalogRestriction` | Project changes are gated by tenant/project server-side authorization; restriction is explicit and opt-in; the default is tenant plus project types. |
| Q7 | PRD §14 Q7; Stories 4.5/4.6; `ApprovedTimeExportFormat`; `ApprovedTimeExportCsvWriter`; export golden file | CSV v1 is the only supported and verified export. No structured API/webhook export is implemented. |
| Q8 | PRD §14 Q8; Story 1.4; `TimeEntryCommentPolicy.SensitiveDefault`; `TimesheetsEvidencePolicyOptions`; policy guidance | Comments are sensitive unstructured evidence; diagnostics and export are excluded by default, external disclosure requires redaction/policy, and missing policy fails trust-bearing actions closed. |
| SDK | installed SDK inventory; `global.json`; `architecture.md`; `docs/launch-readiness.md`; `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md` | SDK `10.0.401` is installed and is now the approved target. `global.json` still says `10.0.400`, while the three normalized instruction files say `10.0.302`. |
| Direct package currency | root project files; imported `references/Hexalith.Builds/Props/Directory.Packages.props`; NuGet v3 package registrations queried 2026-09-12 | All direct package versions requested through the shared catalog match the latest stable releases found today. The Timesheets-owned `Aspire.AppHost.Sdk` pin is the exception: `13.4.6` must move to latest stable `13.5.3`. Existing no-restore assets resolve several older versions and must be regenerated before verification. |

## 2. Impact Analysis

### Epic Impact

| Epic | Impact | Proposed disposition |
|---|---|---|
| Epic 1 — Trusted Time Capture & Activity Governance | Q6 and the baseline Q8 policy are already implemented. No domain work is invalidated. | Keep done. Resolve PRD wording from the completed evidence. |
| Epic 2 — Submission, Approval, Period Review & Corrections | No direct impact. | Keep done. |
| Epic 3 — External Contributor Confirmation | FR14 is not live for valid links because Story 3.6 AC1 is incomplete. Invalid-link no-disclosure from Stories 3.5/3.7 remains valid. | Reopen Epic 3 and Story 3.6 for the residual projection-host wiring and valid-link HTTP proof. |
| Epic 4 — Approved Time Ledger, Reporting & Finance Export | CSV v1, dashboard contracts, metadata, query composition, and service tests remain valid. Rendered UI/WCAG was not proven. | Keep done. Correct Story 4.7 and the UX coverage language to state the evidence boundary. |
| Epic 5 — Release Readiness Verification | Story 5.1 did not reconcile `epics.md`, historical story claims, Q6–Q8, or agent guidance. Its classification model also conflates an unfinished FR with a waiver. The newly approved SDK/package target also reactivates Story 5.2's package-currency acceptance boundary. | Reopen Epic 5 and Stories 5.1/5.2. Story 5.2 establishes the SDK/package baseline; Story 5.1 performs the final artifact and gate reconciliation after Stories 3.6 and 5.2. |

No new epic is needed. No completed domain, projection, export, dashboard-service, or no-disclosure work is rolled back.

### Story Impact

#### Stories to reopen

| Story | Why it must reopen | Work that remains accepted |
|---|---|---|
| 3.6 — Implement EventStore-Backed Magic-Link State Loading | AC1 says the live host resolves valid tokens, but the candidate index is never populated in the running projection topology. | Loader, authoritative aggregate folds, trusted tenant context, token hashing, opaque failure behavior, deterministic fold tests, and privacy checks. |
| 5.1 — Final Launch-Readiness Gate and Documentation Sync | AC2 requires documentation and sprint artifacts to match code without UI/live-link overclaims. Q6–Q8, the UI acceptance boundary, the SDK pin, and the waiver-vs-unfinished distinction remain inconsistent. | Existing launch-readiness record, gate evidence, fitness tests, build/test evidence, and all unrelated waiver classifications. |
| 5.2 — Reconcile Package Currency and Platform Dependency Versions | The user approved SDK `10.0.401` and latest stable packages after the story was closed. The Timesheets AppHost remains on `Aspire.AppHost.Sdk/13.4.6` while both the imported platform catalog and NuGet latest stable are `13.5.3`. | Existing Central Package Management policy, prior audit method, npm-not-applicable finding, no-inline-version guard, submodule boundary, and historical package evidence. |

#### Stories that remain done

- Stories 1.4, 1.5, and 1.6 remain done. Their behavior supplies the proposed Q6/Q8 decisions.
- Story 3.7 remains done. Its invalid-link HTTP no-disclosure proof is independent of valid-link index population.
- Story 3.3 remains done after its display wording is made policy-conditional; the current confirmation response already omits the default-sensitive comment, and the adjustment response includes it only when `ExternalConfirmationDisplay` is explicitly allowed.
- Stories 4.5 and 4.6 remain done. Their deterministic CSV v1 evidence supplies the Q7 decision.
- Story 4.7 remains done for dashboard contracts, metadata, authorization/freshness-aware server composition, and in-process tests. Its artifact receives an evidence-boundary correction; it does not gain unperformed rendered-UI claims.
- Story 5.3 remains done; its SDK `10.0.400` build evidence remains valid historical evidence and is not rewritten as if it ran on `10.0.401`.
- Completed retrospectives remain historical records and are not reset.

#### Stories to add

None. The missing work falls directly inside existing Story 3.6 AC1, Story 5.1 AC2, and Story 5.2's package-currency acceptance criteria. Adding 3.8, 5.4, or another maintenance story would preserve misleading `done` claims and duplicate existing ownership.

### Artifact Conflicts

| Artifact | Conflict | Required adjustment |
|---|---|---|
| PRD | Q6–Q8 remain open; FR22/NFR13 can be read as a locally shipped rendered UI. | Resolve Q6–Q8 and state the FrontComposer consumer-host boundary. |
| Epics | UI acceptance language and NFR13 coverage overstate rendered evidence; Story 3.6 lacks an explicit host-population proof. | Add an evidence-boundary rule, amend Story 4.7 AC7, and strengthen Story 3.6 residual ACs. |
| Architecture | Target tree and component boundaries still describe a Timesheets-owned `UI`/`UI.Tests`; the magic-link gap is called a waiver. | Remove the local UI-project target and classify live valid-link resolution as unfinished. |
| UX DESIGN/EXPERIENCE | Documents read as implemented component specifications without identifying the consuming-host owner or current evidence status. | Mark them as downstream FrontComposer design contracts and separate desired behavior from evidence produced here. |
| Launch readiness | Vocabulary has no `unfinished` state; valid-link failure is grouped with accepted waivers; UI revisit says to scaffold a forbidden UI project. | Add `unfinished`, move magic-link resolution into it, correct the UI owner/revisit condition, and change the interim release verdict. |
| Sprint status | All work is `done`. | Reopen only 3.6, 5.1, and 5.2 and their epics, preserving the user's current edits. |
| Toolchain/package configuration | `global.json` is `10.0.400`; the AppHost SDK is `13.4.6`; existing restored assets contain older resolutions; the imported catalog requests the current direct-package releases. | Set `global.json` to `10.0.401`, set `Aspire.AppHost.Sdk` to `13.5.3`, regenerate restore assets, re-audit direct/transitive/vulnerable/deprecated packages, and update the AppHost fitness expectation. Do not edit a shared submodule merely to duplicate versions it already supplies. |
| Repository guidance | Three synchronized entry points name a superseded SDK. | Change `10.0.302` to `10.0.401` in all three normalized files together. |
| Deferred-work ledger | The SDK/context drift is recorded as deferred. | Mark the SDK portion resolved when the synchronized guidance change is applied; retain unrelated deferred findings. |

### Technical Impact

This proposal does not implement code. After approval, Story 3.6 will require a bounded EventStore projection integration:

- adapt the token-hash index to the platform projection handler contract used by the current EventStore SDK;
- register/discover that handler in the Timesheets domain host without adding a second host or topology;
- persist the rebuildable, non-authoritative index through `IReadModelStore` at the exact address the loader reads;
- prove an issued valid token reaches describe/confirm/adjust through the HTTP host;
- retain authoritative single-use/revoke/expire checks in the capability aggregate fold;
- retain identical opaque responses for every invalid/failure path;
- avoid raw token persistence/logging and avoid direct projection mutation outside the EventStore projection mechanism.

No Timesheets UI project, new topology, prerelease-by-default package adoption, or sibling-submodule edit is authorized by this proposal. The only currently identified package edit is the Timesheets-owned AppHost SDK pin; any newly reported direct update must remain root-owned, stable, .NET 10-compatible, and pass Story 5.2 verification.

## 3. Recommended Approach

### Selected path: Direct Adjustment plus narrow MVP/UI scope clarification

Use the existing epics and stories. Reopen the two records whose own acceptance criteria are not fully satisfied, then synchronize planning and repository guidance around the implementation that already exists.

This is preferable to rollback because the completed loader, domain, policy, export, dashboard, and security work remains correct and reusable. It is preferable to adding stories because Story 3.6 already owns live valid-link loading, Story 5.2 owns package currency, and Story 5.1 owns final documentation/status reconciliation. It needs a narrow MVP clarification only for UI ownership: Timesheets v1 supplies contracts and FrontComposer-compatible metadata; rendered component behavior belongs to a consuming FrontComposer host and is not claimed by this repository.

### Waiver and requirement semantics

The launch record should use these distinct dispositions:

| Disposition | Meaning |
|---|---|
| `implemented` | The requirement is satisfied by executable or documentary evidence at the claimed boundary. |
| `unfinished` | The requirement remains in v1 scope and its acceptance evidence is incomplete. It cannot be converted to a waiver merely because the implementation fails closed. |
| `waived` | The unmet launch concern has an explicit owner, risk, revisit condition, and prior approval to launch with that limitation. |
| `post-v1` | The PRD or an approved course correction removes the item from v1 scope and assigns a future owner/revisit trigger. |

Applied to this correction:

- Valid magic-link end-to-end resolution is `unfinished`, because FR14 and Story 3.6 AC1 remain in scope.
- Rendered Timesheets dashboard/UI components and component-level WCAG evidence are `post-v1`/consumer-host integration, not unfinished work for this repository and not permission to add a Timesheets UI project.
- Project Activity Type governance is `implemented`; Q6 is resolved from Stories 1.5/1.6.
- CSV v1 export is `implemented`; a structured API/webhook is `post-v1`; Q7 is resolved accordingly.
- The sensitive-comment baseline is `implemented`; richer tenant classification/redaction schemes are `post-v1`; Q8 is resolved accordingly. Legal-hold and tenant policy configuration remain separately classified as already recorded.
- Existing unrelated owner/risk/revisit waivers in `docs/launch-readiness.md` carry forward unchanged. This proposal does not silently revoke or broaden them.

While Stories 3.6, 5.2, and 5.1 are open, the overall release verdict should be `FAIL` (an in-scope functional requirement and the approved release baseline are incomplete), not `CONCERNS`. After the residual Story 3.6 work and Story 5.2 verification pass and Story 5.1 completes the evidence sync, the verdict may return to `CONCERNS` for the carried-forward accepted waivers.

### Effort, risk, and timeline

| Work | Estimate | Risk |
|---|---:|---:|
| Reconcile PRD, epics, architecture, UX, launch readiness, guidance, and sprint status | Low, roughly 0.5–1 day | Low; main risk is overwriting the user's current sprint-status edit or leaving synchronized entry points inconsistent. |
| Reopen Story 5.2; apply SDK `10.0.401` and latest stable direct-package baseline; rerun audits/build/tests | Low–Medium, roughly 0.5–1 day | Medium; package and SDK changes can expose restore, analyzer, generated-assets, or runtime compatibility issues. |
| Complete Story 3.6 projection-host wiring and valid-link HTTP proof | Medium, roughly 1–3 days | Medium–High; it touches security-sensitive token resolution and EventStore projection routing. |
| Final Story 5.1 gate rerun and evidence closure | Low, roughly 0.5 day | Low–Medium; depends on stable runtime fixtures and exact evidence reporting. |

Expected timeline impact: one focused implementation/review cycle. No completed feature is reimplemented.

## 4. Detailed Change Proposals

### 4.1 Sprint tracking

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

OLD:

```yaml
epic-3: done
3-6-implement-eventstore-backed-magic-link-state-loading: done

epic-5: done
5-1-final-launch-readiness-gate-and-documentation-sync: done
5-2-reconcile-package-currency-and-platform-dependency-versions: done
```

NEW:

```yaml
epic-3: in-progress
3-6-implement-eventstore-backed-magic-link-state-loading: ready-for-dev

epic-5: in-progress
5-1-final-launch-readiness-gate-and-documentation-sync: ready-for-dev
5-2-reconcile-package-currency-and-platform-dependency-versions: ready-for-dev
```

Rationale: These statuses reflect remaining work already owned by the three stories. All other keys, the completed 5.3 status, timestamps/formatting not directly changed by the correction, and the user's existing modifications must be preserved.

### 4.2 Story 3.6 — live projection population

Artifacts: `epics.md` Story 3.6 and the Story 3.6 implementation record.

Story record status: `Status: done` → `Status: ready-for-dev`.

OLD acceptance boundary:

> When the live host describes, confirms, or adjusts through the magic-link endpoints, then the loader resolves the token hash, folds EventStore-backed capability state, folds scoped Time Entry state, and loads a fresh Activity Type catalog.

The text is correct, but the completed implementation only proves the loader when an index read model is injected/populated in tests.

NEW clarification and additional acceptance criteria:

```text
Reopened 2026-09-12: AC1 is incomplete until the token-hash candidate index is populated by the running EventStore projection path. The existing loader, folds, privacy behavior, and invalid-link evidence remain accepted.

Given MagicLinkConfirmationCapabilityIssued is delivered through the configured Timesheets projection path
When the projection host processes or rebuilds that event
Then the token-hash candidate index is written through IReadModelStore at the state-store name and key consumed by EventStoreMagicLinkConfirmationCapabilityStateLoader
And the stored candidate contains only the token hash, TenantReference, and MagicLinkCapabilityId.

Given capabilities and scoped Time Entries were issued through the live host and the index projection has processed the issuance events
When correctly scoped valid tokens are used against the describe/confirm and describe-adjust/adjust HTTP routes
Then the host resolves each candidate and reaches the existing authoritative capability/Time Entry/catalog validation path
And both allowed action families succeed without test-only index injection.

Given invalid, expired, used, revoked, cross-tenant, wrong-action, missing-state, or projection-unavailable input
When the same routes are exercised
Then the existing no-disclosure response equivalence and no-dispatch guarantees remain unchanged.
```

Rationale: This closes the exact gap without moving authority into the index or reopening Story 3.7.

Related Story 3.3 AC1 wording correction (story remains done):

```text
OLD: shows only the proposed date, duration, Activity Type, comment, Billable Flag, and minimal target context needed for this confirmation.

NEW: shows only the proposed date, duration, Activity Type, Billable Flag, minimal target context, and comment text only when the comment's external-confirmation policy explicitly allows it; otherwise the comment is omitted or represented by safe policy state.
```

Rationale: The current implementation already enforces this policy boundary; the planning text must not require disclosure forbidden by the resolved Q8 baseline.

### 4.3 Story 4.7 and epic-wide UI evidence boundary

Artifact: `epics.md`

Add one evidence rule before the story breakdown:

```text
UI evidence boundary (2026-09-12): This repository does not ship a Timesheets UI project. UI-oriented acceptance text in Stories 1–4 is a contract and FrontComposer-metadata requirement unless a rendered consumer-host test is explicitly named. Metadata, read-model, action, and status semantics may be complete here; rendered Fluent components, browser interaction, focus behavior, responsive layout, and WCAG conformance are not claimed until proven in a consuming FrontComposer host.
```

Story 4.7 AC7 OLD:

```text
Given dashboard UI is tested
When accessibility and conformance checks run
Then FrontComposer/Fluent UI V5 components, status badges with text, message bars, focus order, and WCAG 2.2 AA behavior are verified
And hover-only controls or color-only statuses are not introduced.
```

Story 4.7 AC7 NEW:

```text
Given dashboard contracts and FrontComposer metadata are tested in this repository
When contract, metadata, authorization, freshness, and conformance checks run
Then the descriptor requires text-bearing statuses, persistent message-bar semantics, action/focus intent, and no hover-only or color-only interaction requirement
And no Timesheets UI project or runtime Fluent UI dependency is introduced.

Given a consuming FrontComposer host renders the dashboard
When browser-level accessibility and component checks run in that owning repository
Then Fluent UI V5 component use, keyboard behavior, focus order, responsive behavior, and WCAG 2.2 AA are verified there
And this Story does not claim that rendered evidence before it exists.
```

Append, rather than erase, an evidence correction to the Story 4.7 implementation record:

```text
Evidence correction (2026-09-12): the completed evidence covers dashboard contracts, metadata, policy/freshness-aware server composition, and in-process integration tests. It does not include rendered FrontComposer/Fluent components, browser focus/keyboard tests, or a WCAG audit. Story 4.7 remains done at the Timesheets-owned metadata/service boundary.
```

Rationale: Completed work remains accepted while the unsupported component-level claim is removed.

### 4.4 Story 5.1 — classification and synchronization

Artifacts: `epics.md` Story 5.1 and the Story 5.1 implementation record.

Story record status: `Status: done` → `Status: ready-for-dev`.

AC1 OLD:

```text
marks each item as exactly one of implemented, waived, or post-v1
```

AC1 NEW:

```text
marks each item as implemented, unfinished, waived, or post-v1; an unfinished v1 requirement cannot be labeled waived without an approved PRD scope change, and every waiver records its approval evidence in addition to owner, risk, and revisit condition.
```

AC2 NEW scope additions:

- Include `epics.md`, affected story implementation records, UX artifacts, `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, and `sprint-status.yaml` in the reconciliation set.
- Resolve Q6–Q8 against implemented behavior.
- State that no Timesheets UI project exists or may be added, and that component/WCAG verification belongs to a consuming FrontComposer host.
- Synchronize SDK/package claims to the baseline established by Story 5.2: SDK `10.0.401`, AppHost SDK `13.5.3`, and the verified direct-package versions.
- Preserve all pre-existing user changes while applying the three story status transitions.

Rationale: This is the existing release-documentation story’s unfinished acceptance work; no new documentation story is needed.

### 4.5 Story 5.2 — SDK and direct-package currency

Artifacts: `global.json`, `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj`, `Directory.Packages.props` if an implementation-day direct update is required, `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/ScaffoldGovernanceTests.cs`, `docs/launch-readiness.md`, `architecture.md`, and the Story 5.2 implementation record.

Story record status: `Status: done` → `Status: ready-for-dev`.

Approved implementation target:

```text
.NET SDK: 10.0.400 -> 10.0.401
Aspire.AppHost.Sdk: 13.4.6 -> 13.5.3
Package policy: latest stable releases compatible with net10.0; prerelease packages require a separately documented platform constraint or explicit approval.
```

The official NuGet v3 registrations were checked on 2026-09-12 for every package directly referenced by a Timesheets project. The current approved version matrix is:

| Direct package group | Latest stable version |
|---|---:|
| `Aspire.AppHost.Sdk` | `13.5.3` |
| `MinVer` | `8.0.0` |
| `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.12` |
| `Microsoft.Extensions.Http.Resilience`, `Microsoft.Extensions.ServiceDiscovery` | `10.10.0` |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `.Http`, `.Runtime` | `1.18.0` |
| `coverlet.collector` | `10.0.1` |
| `Microsoft.NET.Test.Sdk` | `18.10.0` |
| `NSubstitute` | `6.2.0` |
| `Shouldly` | `4.3.0` |
| `xunit.v3`, `xunit.runner.visualstudio` | `4.0.0` |

All direct NuGet versions in this table are already requested through the imported `Hexalith.Builds` central catalog; do not edit its submodule or gitlink simply to restate them. Update the Timesheets-owned AppHost SDK pin and its architecture fitness expectation. Re-run the direct, transitive, vulnerability, and deprecation audits on the implementation day. If a newer compatible stable direct version is then available, apply it through Timesheets Central Package Management without inline `Version` attributes or submodule edits.

Regenerate restore assets under SDK `10.0.401` before judging resolved versions. The existing `--no-restore` package output contains stale resolutions such as `10.0.11`, `10.9.0`, `18.9.0`, and `MinVer 8.0.0-rc.1`; it is evidence that a clean restore is required, not an approved downgrade.

Verification must include restore, warnings-as-errors solution build, ArchitectureTests, every project under `tests/`, direct/transitive outdated checks, vulnerable/deprecated checks, and an honest record of any package-tooling failure. Historical Story 5.3 results remain labeled as SDK `10.0.400`; new Story 5.2 evidence is recorded separately under `10.0.401`.

Rationale: Story 5.2 already owns package currency, SDK/package alignment, fitness coverage, and release evidence. Reopening it is smaller and more truthful than adding Story 5.4 or folding dependency changes into the documentation-only portion of Story 5.1.

### 4.6 PRD decisions and UI ownership

Artifact: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`

FR22 OLD:

> v1 ships REST/SDK command/query contracts plus FrontComposer-compatible metadata for admin/internal surfaces; it does not ship a bespoke mobile app.

FR22 NEW:

> v1 ships REST/SDK command/query contracts plus FrontComposer-compatible metadata for admin/internal surfaces. This repository does not ship a Timesheets UI project; rendered internal or external web experiences are composed and verified by a consuming FrontComposer host using Fluent UI V5. It does not ship a bespoke mobile app.

NFR13 OLD:

> Any internal UI surface follows the Hexalith/FrontComposer/Fluent UI rules and targets WCAG 2.2 AA where applicable.

NFR13 NEW:

> Any rendered Timesheets surface in a consuming FrontComposer host follows the Hexalith/FrontComposer/Fluent UI V5 rules and targets WCAG 2.2 AA. Timesheets-owned contracts and metadata must express accessible semantics, but this repository does not claim component-level or browser-level conformance without consumer-host evidence.

PRD Q6 OLD:

> Activity Type governance — Who can define project-level Activity Types, and can a project restrict tenant-level defaults?

PRD Q6 NEW:

> **[RESOLVED 2026-09-12]** Tenant Activity Types require tenant-authorized command access. Project Activity Types and catalog restriction changes require both tenant and Project authority resolved through server-side gates; role names remain provider policy, not caller input. Projects may explicitly restrict the selectable tenant/project IDs. The default is unrestricted active tenant types plus active types for the selected Project. Implemented by Stories 1.5 and 1.6.

PRD Q7 OLD:

> Export format — Is CSV sufficient for v1, or does downstream billing need a structured API/webhook contract at launch?

PRD Q7 NEW:

> **[RESOLVED 2026-09-12]** Deterministic approved-time CSV v1 is the v1 launch export. A structured API/webhook export is post-v1 and requires a named downstream consumer and contract story. Implemented by Stories 4.5 and 4.6.

PRD Q8 OLD:

> Comment sensitivity — Are comments allowed to contain customer/private data, and do they need classification/redaction rules?

PRD Q8 NEW:

> **[RESOLVED 2026-09-12]** Comments are allowed as sensitive unstructured evidence. Authorized internal display is policy-controlled; external confirmation display, diagnostics, and export are excluded by default; external disclosure requires explicit policy/redaction; missing comment policy fails trust-bearing actions closed. Richer tenant classification/redaction schemes are post-v1. Implemented baseline: Story 1.4. Legal-hold and tenant policy configuration remain separately tracked launch concerns.

UJ3 OLD:

> Simon reviews the proposed date, duration, Activity Type, comment, and Billable Flag.

UJ3 NEW:

> Simon reviews the proposed date, duration, Activity Type, Billable Flag, minimal target context, and comment only when external-confirmation policy explicitly allows it; otherwise comment text is omitted.

Also update the PRD assumptions index, rollout/launch-readiness text, document `updated` date, and decision log so none of Q6–Q8 remains listed as open or deferred.

### 4.7 Architecture

Artifact: `_bmad-output/planning-artifacts/architecture.md`

OLD structure/ownership direction:

```text
Internal UI, when implemented, lives in a Timesheets UI project.
UI owns FrontComposer metadata and Blazor Fluent UI V5 component composition.
UI projects are scaffolded with the first UI-bearing story.
```

NEW:

```text
Timesheets owns UI-neutral contracts, read models, action/status semantics, and FrontComposer-compatible metadata in Contracts/Client/Server. It does not own or add Hexalith.Timesheets.UI or Hexalith.Timesheets.UI.Tests. A consuming FrontComposer host owns rendered Fluent UI V5 composition and browser/accessibility evidence.
```

Required architecture edits:

- Remove `Hexalith.Timesheets.UI`, `UI.Tests`, `UI/Composition`, and `UI/wwwroot` from the target repository tree and asset/package lists.
- Replace the “first UI-bearing story” timing note with the consumer-host ownership rule.
- Keep the UX component rules as downstream integration constraints, not proof of current implementation.
- Change the magic-link status note from a launch waiver to an unfinished Story 3.6 acceptance item until a real projection handler is registered and a valid token succeeds without a test-injected index.
- Change the current SDK target from `10.0.400` to `10.0.401` and the AppHost SDK from `13.4.6` to `13.5.3`; retain older build observations as dated historical evidence.
- Reconcile package tables with the actual imported central catalog and the Story 5.2 post-restore audit instead of repeating obsolete Dapr, Aspire, or Fluent UI values.
- Retain the existing no-disclosure, non-authoritative-index, EventStore-only, AppHost, and fail-closed constraints.

Rationale: Repository guidance is authoritative over the older architecture target tree, and the new boundary matches the current codebase.

### 4.8 UX DESIGN and EXPERIENCE

Artifacts: `DESIGN.md`, `EXPERIENCE.md`, and the UX decision log.

Add an implementation-boundary section near the beginning of both UX documents:

```text
Implementation boundary (2026-09-12): This document is the rendering contract for a consuming FrontComposer host. Hexalith.Timesheets publishes contracts and FrontComposer-compatible metadata but does not ship a Timesheets UI project. Component names, interaction behavior, responsive rules, and WCAG targets are requirements for that consuming host; they are not evidence that rendered components exist in this repository.
```

Update the accessibility and component sections to distinguish:

- Timesheets-owned evidence: metadata fields, action/status semantics, safe copy, freshness and authorization states, and contract tests.
- Consumer-host evidence: actual Fluent UI V5 components, keyboard traversal, focus management, responsive layout, screen-reader behavior, contrast, touch targets, and browser-level WCAG verification.

Replace open UX assumptions for Activity Type governance, export format, and comment sensitivity with the resolved PRD decisions in §4.6. Preserve the design specifications as the future FrontComposer acceptance contract.

Where `EXPERIENCE.md` currently requires the magic-link page to show a comment, change the requirement to policy-conditional display: sensitive-default comments are omitted, and only explicitly allowed/redacted comment text may be rendered. This keeps UJ3, Story 3.3, UX, and the implemented response models consistent.

### 4.9 Launch readiness

Artifact: `docs/launch-readiness.md`

Classification vocabulary OLD:

```text
implemented, waived, post-v1
```

NEW:

```text
implemented, unfinished, waived, post-v1
```

Magic-link row OLD:

```text
Magic-link live end-to-end resolution | ... no projection-host wiring ... | waived
```

NEW:

```text
Magic-link live end-to-end resolution | Loader is concrete, but no running projection path populates the candidate index; valid links fail closed. | unfinished | Story 3.6 reopened | FR14/Story 3.6 AC1 is not satisfied until valid-link HTTP evidence passes without test-only index injection.
```

UI row OLD revisit condition:

> A UI-bearing story scaffolds `Hexalith.Timesheets.UI` and `Hexalith.Timesheets.UI.Tests`.

UI row NEW:

> Component-level UI and WCAG evidence is post-v1 consumer-host integration owned by FrontComposer/the composing application. Revisit when that host consumes Timesheets metadata and adds browser/accessibility tests. Do not scaffold a Timesheets UI project.

Q6–Q8 disposition updates:

- Add Activity Type governance as implemented/resolved.
- Record CSV v1 as implemented and structured API/webhook as post-v1.
- Record the sensitive-comment baseline as implemented and richer classification as post-v1; keep legal-hold/tenant configuration separate.

SDK/package evidence updates:

- Record SDK `10.0.401` and `Aspire.AppHost.Sdk` `13.5.3` as the current baseline only after Story 5.2 verification passes.
- Replace the stale platform-alignment package claims with values read from the actual imported catalog and Timesheets AppHost project.
- Preserve the SDK `10.0.400` Story 5.3 build entry as dated historical evidence; append the new `10.0.401` audit/build/test evidence rather than rewriting history.
- Keep platform/submodule alignment waived only for limitations that remain after the root-owned latest-stable audit; do not use that waiver for an available Timesheets-owned update.

Interim release decision NEW:

> **FAIL** while Stories 3.6 and 5.2 and the final Story 5.1 reconciliation are open. After valid-link evidence, the SDK/package audit, and Story 5.1 gate closure pass, reassess; the expected residual posture is `CONCERNS` because the previously accepted waivers remain.

Update `LaunchReadinessTests` during Story 5.1 so the document cannot omit `unfinished`, cannot classify unchanged in-scope FR14 as waived, and cannot direct creation of a Timesheets UI project.

### 4.10 Repository guidance and deferred-work record

Artifacts: `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, and `_bmad-output/implementation-artifacts/deferred-work.md`.

OLD synchronized line:

```text
Use the SDK in `global.json` (`10.0.302`, `rollForward: latestPatch`); do not trust the machine SDK.
```

NEW synchronized line:

```text
Use the SDK in `global.json` (`10.0.401`, `rollForward: latestPatch`); do not trust the machine SDK.
```

Apply the same normalized edit to all three entry points in one change. Because the line is inside the managed `bmad:context` block, refresh it through the repository context workflow so future refreshes preserve the value. Mark only the SDK-staleness portion of the deferred-work finding resolved; retain its unrelated findings.

Rationale: after Story 5.2 changes the executable source of truth to `10.0.401`, the three shared entry points must remain byte-for-byte synchronized with it.

## 5. Implementation Handoff

### Scope classification

**Moderate.** Backlog/status reorganization and cross-artifact product clarification are required, followed by a bounded SDK/package refresh and one security-sensitive implementation remainder. No fundamental re-architecture or rollback is needed.

### Sequence and recipients

1. **Product Owner / PM**
   - Approve the proposal, including the Q6–Q8 decisions and UI consumer-host boundary.
   - Apply the `epics.md`, PRD, UX decision-log, and sprint-status changes without overwriting the user's existing `sprint-status.yaml` edits.
   - Reopen Stories 3.6, 5.1, and 5.2; add no new story.

2. **Release maintainer for Story 5.2**
   - Pin SDK `10.0.401` and `Aspire.AppHost.Sdk` `13.5.3`, update the exact architecture fitness expectation, and regenerate restore assets.
   - Re-run direct/transitive outdated, vulnerable, and deprecated audits; apply any implementation-day stable compatible direct update through Timesheets Central Package Management only.
   - Do not edit shared submodules or gitlinks. Record failures and compatibility exceptions honestly.

3. **Developer / Architect for Story 3.6**
   - Confirm the current EventStore projection-handler API and implement only the missing index population/registration path.
   - Add valid-link HTTP evidence while preserving the existing no-disclosure matrix and EventStore boundary.
   - Do not add topology, a second host, direct state writes, a UI project, or package changes outside approved Story 5.2 scope.

4. **Release owner for Story 5.1**
   - Reconcile architecture, UX, launch readiness, README if needed, story correction notes, normalized repository guidance, and deferred-work disposition.
   - Rerun the narrowest relevant documentation fitness tests plus the full release gates required by Story 5.1 after Stories 3.6 and 5.2 close.
   - Mark Epics 3 and 5 done only after the evidence and documentation agree.

5. **FrontComposer / consuming-host owner (post-v1 or separate repository plan)**
   - Render Timesheets metadata using Fluent UI V5 and prove component/browser accessibility there.
   - This is not a Timesheets repository implementation task and does not authorize a Timesheets UI project.

### Success criteria

- A valid issued magic link populates the candidate index through the running EventStore projection path and succeeds through the applicable HTTP journey without test-only index seeding.
- Invalid/expired/used/revoked/cross-tenant/wrong-action/unavailable cases remain externally indistinguishable and emit no unauthorized state change.
- Stories 3.6, 5.1, and 5.2 plus Epics 3 and 5 are the only reopened tracking entries; all other completed work remains done.
- PRD Q6–Q8 are marked resolved with decisions matching current implementation; post-v1 portions are explicit.
- No artifact claims a Timesheets-owned rendered UI or component-level WCAG evidence, and no artifact instructs creation of a Timesheets UI project.
- Architecture and UX identify FrontComposer/the consuming host as the owner of rendered components and accessibility proof.
- `global.json` and all three normalized agent entry points name SDK `10.0.401`; the Timesheets AppHost uses `Aspire.AppHost.Sdk/13.5.3`.
- A clean SDK `10.0.401` restore/build/test run resolves the approved direct-package versions, and direct/transitive/vulnerable/deprecated package audit results are recorded without hiding unavailable tooling.
- `docs/launch-readiness.md` distinguishes `unfinished` from accepted `waived` and `post-v1` items. The release cannot return to `CONCERNS` until the unfinished valid-link requirement closes.
- The pre-existing user edit in `sprint-status.yaml` is preserved and merged rather than overwritten.

## 6. Change Navigation Checklist Record

### Section 1 — Understand the Trigger and Context

- [x] 1.1 Triggering stories identified: 3.6, 4.7, 5.1, and 5.2, discovered by a post-sprint implementation-readiness review plus the approved SDK/package direction.
- [x] 1.2 Problem categorized: incomplete runtime integration, acceptance-evidence mismatch, unresolved documented product decisions, toolchain/package currency, and repository-guidance drift.
- [x] 1.3 Evidence gathered from current code, PRD, epics, architecture, UX, launch readiness, story records, repository guidance, sprint status, installed SDKs, resolved package assets, the imported central catalog, and official NuGet registrations.

### Section 2 — Epic Impact Assessment

- [!] 2.1 Epic 3 cannot remain done while Story 3.6 AC1 and FR14 are incomplete; Epic 5 cannot remain done while Stories 5.1 and 5.2 have reopened release-baseline work.
- [x] 2.2 Modify existing Epic 3/Epic 5 status and story scope; no new epic.
- [x] 2.3 Epics 1, 2, and 4 reviewed; completed implementation remains valid.
- [N/A] 2.4 No future epic is invalidated and no new epic is required.
- [x] 2.5 Sequence: planning/status correction, Story 5.2 SDK/package baseline, Story 3.6 residual implementation, then Story 5.1 final reconciliation and gate closure.

### Section 3 — Artifact Conflict and Impact Analysis

- [!] 3.1 PRD requires Q6–Q8 resolution and a precise UI ownership/evidence boundary.
- [!] 3.2 Architecture requires UI project-tree/ownership corrections, magic-link reclassification, and current SDK/package claims.
- [!] 3.3 UX requires a consumer-host implementation boundary; its design content remains useful.
- [!] 3.4 Launch readiness, sprint status, SDK/AppHost pins, package fitness coverage, three normalized agent entry points, and the deferred-work record require synchronization.

### Section 4 — Path Forward Evaluation

- [x] 4.1 Direct Adjustment: viable; medium effort, medium overall risk.
- [x] 4.2 Potential Rollback: not viable; it would discard valid implementation and does not solve the evidence gap.
- [x] 4.3 MVP Review: narrowly viable only to clarify that rendered UI is consumer-host scope; FR14 remains v1.
- [x] 4.4 Selected approach: Direct Adjustment plus narrow MVP/UI scope clarification.

### Section 5 — Proposal Components

- [x] 5.1 Issue summary completed.
- [x] 5.2 Epic/story/artifact impact documented.
- [x] 5.3 Recommended path and rejected alternatives documented.
- [x] 5.4 MVP impact, sequencing, and success criteria documented.
- [x] 5.5 PO/PM, package maintainer, Developer/Architect, Release Owner, and downstream FrontComposer responsibilities identified.

### Section 6 — Final Review and Handoff

- [x] 6.1 Applicable checklist sections addressed; action-needed items are represented in the proposal.
- [x] 6.2 Proposal cross-checked against the requested primary evidence and current worktree state.
- [!] 6.3 Explicit user approval pending.
- [!] 6.4 Sprint-status changes intentionally not applied before approval; the user-owned file remains untouched by this workflow.
- [!] 6.5 Implementation handoff begins only after approval.

## 7. Approval Gate

No code or proposed artifact edits beyond this proposal have been applied.

Approval authorizes planning/status/document reconciliation, the bounded root-owned Story 5.2 SDK/package updates, and the later Story 3.6 implementation handoff. It does not authorize adding a Timesheets UI project, changing topology, adopting prerelease packages without a recorded exception, modifying sibling submodules or gitlinks, or discarding existing user changes.

**Decision:** Approved by Jerome on 2026-09-12.

**Handoff status:** Ready for the Product Owner / Scrum Master to apply the approved backlog and planning-artifact changes, then route Stories 5.2 and 3.6 for implementation and Story 5.1 for final reconciliation. Approval of this proposal does not itself apply those downstream changes.
