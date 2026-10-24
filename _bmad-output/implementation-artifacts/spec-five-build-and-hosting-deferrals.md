---
title: 'Close five build and hosting deferrals'
type: 'bugfix'
created: '2026-10-24'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'c712954a9e0d768d78c968c85d0b788ac6f09de6'
eventstore_baseline_commit: 'e1d8b17160202761ab5c9606cafbcf4d1a4b92c3'
eventstore_integrated_baseline_commit: '6e927633d920eafcefa2635a124810b541264489'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/docs/boundary-decision-record.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferrals concern Works version propagation, duplicate AppHost credentials, shared request-log privacy, legacy keyed-domain metadata, and an obsolete npm advisory.

**Approach:** Complete these five independent items as the requested batch. Make shared fixes in EventStore, verify/review both repositories, push validated Conventional Commits to their main branches, then separately commit/push the owned EventStore gitlink update in Timesheets.

## Boundaries & Constraints

**Always:** Preserve explicit version overrides, Debug/source and Release/package separation, workload authentication, Timesheets credentials, metadata version boundaries, historical evidence, and launch FAIL. The AppHost edit target is `src/Hexalith.Timesheets.AppHost`. Works stays read-only.

**Never:** Add topology, change domain persistence/product authorization, initialize nested submodules, stop foreign apps, publish packages, or deploy. Construction tests do not close runtime-smoke entries.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Works edges | Differing defaults; explicit override; direct/transitive references | Consumer version reaches child; Release uses packages | Incorrect propagation fails regression |
| Full AppHost construction | Run-mode graph | One gateway credential; Timesheets credential retained | Duplicate mutant fails without starting resources |
| Shared request logging | Query secret; provider-specific Information override | Console/OTel export no request query values | Retain Warning and unrelated logs |
| Legacy metadata | Registered keyed domain; unknown/orphan | Registered domain appears without v2 advertisement | Unknown/orphan absent; auth unchanged |
| Advisory | Official withdrawal; fresh audit | Close original finding with dated evidence | Track unrelated findings separately |

</frozen-after-approval>

## Code Map

- `Directory.Solution.props`, `Directory.Solution.targets`, `Directory.Build.targets` and `FitnessTests/SourceVersionGraphTests.cs`: root audit correction aligns the actual NuGet restore walk, source build and runtime identities for solutions/standalone projects and explicit overrides.

- `src/Hexalith.Timesheets.Works/Hexalith.Timesheets.Works.csproj`, `tests/Hexalith.Timesheets.Works.Tests/Hexalith.Timesheets.Works.Tests.csproj`, `Directory.Build.targets`: direct/SDK-added Works edges. Architecture `WorksReferencePropagationTests.cs` exercises real MSBuild with distinct consumer/child defaults and overrides.
- AppHost `Program.cs`: SDK gateway credential generation is reused; the full-construction probe reaches Build/disposal. `scripts/check_magic_link_apphost_guards.py` owns compiled probes.
- EventStore `src/Hexalith.EventStore.ServiceDefaults/Extensions.cs`: console/OTel setup uses an internal options factory to apply the shared request diagnostic guard after every caller post-configuration and before validation.
- EventStore `src/Hexalith.EventStore.DomainService/{AdminOperationalIndexMetadata,EventStoreDomainServiceExtensions}.cs`: legacy overload receives server-confirmed registrations while preserving the old API and unchanged v2 behavior.
- EventStore `tests/Hexalith.EventStore.{DomainService,Server}.Tests`: metadata and shared telemetry regression owners.

## Tasks & Acceptance

**Execution:**

- [x] Works edge files, `Directory.Build.targets` and `FitnessTests/WorksReferencePropagationTests.cs`: propagate evaluated consumer version; exercise differing defaults and explicit overrides through direct/transitive solution traversal.
- [x] AppHost `Program.cs` and guard script: remove redundant gateway generation; add opt-in full-construction/disposal probe without Run/Start. Preserve endpoint probes and sanitized output.
- [x] EventStore shared defaults and new `Server.Tests/Telemetry/RequestLogPrivacyTests.cs`: centrally prevent unsafe hosting Information records reaching either provider despite provider-specific overrides. Capture real request logs, formatted/structured values and scopes; retain Warning behavior.
- [x] EventStore metadata files and `DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs`: pass server-confirmed keyed domains into legacy requests. Verify both processor forms, aggregate compatibility, orphan refusal and unchanged v2 fingerprints/capabilities.
- [x] `docs/ci-cd.md`, `docs/launch-readiness.md`: replace the obsolete current braces verdict with authoritative withdrawal/fresh audit evidence; retain history and separately track unrelated advisories. No dependency change is needed merely for withdrawal.
- [x] `deferred-work.md`, this spec and `tests/five-build-and-hosting-deferrals.md`: add exactly five evidence-backed resolutions and retain verification/matrix evidence.

**Acceptance Criteria:**

- Given differing Works defaults, when documented Debug/source verification runs without a version override, then all six Timesheets suites load consistently and pass.
- Given shared hosting, when secret-bearing requests and legacy metadata queries execute, then no query values escape request logging and only registered domains appear.


## Implementation Notes

- Step-03 implementation is sequential and permits no remote operations or push. The implementation agent reports its patch and verification; the root agent audits tasks/matrix, runs workflow review, then performs authorized publication. Leave publication-dependent task/acceptance checks pending until that phase.

- User approved and continued on 2026-10-24. Re-read the unchanged spec from disk before acceptance; frozen intent is locked. Implementation handoff is sequential; publication follows verification and review.

- Both repositories were clean on main at the recorded revisions. No product-policy decision or migration is required. Cross-repository footprint and independent deliverables select dispatch; the batch/spec checkpoint was approved by the user.
- Foreign Tenants/Dapr applications block the fixture's idle-runtime prerequisite. Leave them untouched; full construction qualifies only duplicate registration.
- Fresh npm audit reports two moderate/one high unrelated findings. GHSA-vfj7-8cjw-p6xm was withdrawn 2026-10-23: https://github.com/advisories/GHSA-vfj7-8cjw-p6xm. Revalidate during implementation.

- Root acceptance caught 52 Works runtime failures after the first patch: NuGet assets selected Contracts 3.126.0 while Works referenced 3.127.0. Reference metadata alone did not reach NuGet's path walk. The consumer version is now promoted through solution/project restore traversal as well as source build edges, without a CLI workaround or Works edit.
- Four new restore/build/runtime graph regressions cover solution and standalone entry points with differing defaults and explicit overrides. A mutant removing only restore forwarding fails both default cases while preserving the two explicit overrides. Original configuration was restored byte-for-byte. Corrected Works passes 76/76 and Architecture 113/113; the subsequent complete suite results are recorded below.

- Root completed the full affected Server telemetry namespace as the broad-run fallback: 41 passed, including privacy and existing telemetry behavior. Metadata remains 95 passed. Monolithic upstream attempts remain explicitly incomplete. Final compiled AppHost guards pass 63/63 on the corrected source build.

- Root diff audit also found that the new legacy registry enumeration dereferenced a null Domains list, unlike the preexisting legacy factory. The enumeration now treats null as empty, with two aggregate-discovery compatibility cases included in the final metadata class run. V2 behavior remains unchanged.

- Invalid legacy names are filtered before keyed-service lookup and before publication: a null service key means an unkeyed processor in DI and must not fabricate a null metadata domain. A non-keyed-processor regression joins the two null/empty-list compatibility cases.

- The shared request-log guard now constrains existing rules and inserts its default fallback at lowest precedence, preserving stricter Error/None thresholds and existing global predicates. Five configuration regressions supplement the real Console/OTel captures; final upstream results are recorded below.

- All code tasks/matrix rows are verified: six suites 2,589 passed/five skips, upstream metadata 98 and telemetry 48, final consuming build zero warnings/errors, HTTP filter 3 and Works 76. Publication bookkeeping moves below so the no-remote implementation phase does not mark review/push complete.

- Review patches completed through the original implementation agent: final-order privacy guarding, legacy casing, isolated fixture property cleanup/child compile identities, current platform disclosure, and precise retained-file/hash receipts. Focused privacy 10, metadata 99, architecture/readiness 24 pass. Root final verification is running, including an adequately bounded full upstream Server retry.

- Publication fetch found ten newer EventStore commits. The reviewed patch has no direct file overlap and was preserved byte-for-byte across a fast-forward to `6e927633d920eafcefa2635a124810b541264489`. Old-baseline in-flight tests were interrupted for this integration, not diagnosed as hangs or accepted as passing. Final builds and suites rerun on the integrated candidate; no nested submodule update occurred.

- Final complete upstream Server run: 5,731 passed, 25 skipped, one failed tracked-secret guard on exactly 15 byte-identical incoming fixture lines; no owned marker remains. Metadata 99 and telemetry 49 pass. The incoming guard reconciliation is deferred separately and this broad gate remains non-green.

- The final integrated Timesheets solution build has zero warnings/errors and all six suites pass: Architecture 117, Contracts 92, Server 693, Projections 697, Integration 918 plus five intentional skips, Works 76. Total 2,593 passed/five skipped. Projections 886.431 s and Integration 752.214 s ran sequentially.

- Final AppHost 63/63, fixture/diagnostic Python 146 and release/BMAD tooling 88 pass. Retained JSON/raw/archive fingerprints, all original ledger lines, five additive resolutions, frozen intent, synchronized entry points and unchanged Works pass integrity checks. The user explicitly requested main pushes and approved continuation; publication follows that existing authorization.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Checked evidence and route |
| --- | --- | --- |
| B1 — late logging post-configuration bypass | high | A later provider Information rule is appended after the current PostConfigure wrapper and re-enables Hosting.Diagnostics; the reproduced state exposes request queries. Patch: apply the existing guard after all post-configuration, before options validation, with a late-rule regression. |
| B2 — legacy domain casing duplicates | medium | The legacy query path groups names with OrdinalIgnoreCase, but the new registry merge uses Ordinal and admits SAMPLE plus sample. Patch: reuse the legacy comparison and retain query metadata in a regression. |
| B3 — construction probe does not check token secrecy/wiring | false | The retained SDK helper creates a secret parameter and passes the same tokenResource to the sidecar callback and project APP_API_TOKEN. Dummy parameters are not produced by the traced path. The probe qualifies duplicate registration only; runtime acceptance remains explicitly open. Reject. |
| B4 — case-sensitive fixture environment cleanup | medium | MSBuild accepts case-insensitive property names, but the child-process fixture removes exact spellings only; a lowercase inherited version changes its expected graph. Patch: remove the existing property list case-insensitively and verify a polluted environment. |
| B5 — child compile identity omitted from graph assertions | low | The new test checks consumer assets, copied DLL and runtime reflection, so runtime unification can conceal the Works compile reference. Patch: directly assert Works assets and compiled assembly-reference identity using existing platform metadata APIs. |
| B6 — absent Release construction receipt | false | The report explicitly qualifies Debug/source checks and source-only SDK fixes, and does not claim a Release/package run. The existing CI Release build invokes the same construction driver. No both-mode verification claim exists to correct; package runtime qualification is retained as open. Reject. |
| B7 — effective platform version row stale | medium | Root assets resolve Hosting 13.6.3 and Keycloak 13.6.3-preview.1.26521.2, while the current row says restored 13.6.1. Patch: distinguish unchanged imported defaults from effective root overrides and align the existing document assertion. |
| B8 — manifest lists ignored logs absent from patch | medium | changed-files.json contains ignored .log paths not in the retained Git change set. Patch: enumerate actual retained changed files rather than all filesystem descendants. |
| B9 — raw and retained evidence hashes ambiguous | low | upstream-final-checks.json hashes raw temporary logs but names normalized excerpts as retained_log. Patch: name raw and retained hashes separately and record the excerpt policy. |
| B10 — broad Server verification stopped prematurely | medium | The 215-second attempt stopped before its 300-second bound with no failure or proven blocker. Patch validation: root will rerun the built full Server executable with an adequate explicit bound and retain its actual result. |

| B11 — fixed privacy marker violates tracked-secret guard | medium | The integrated full Server run reports RequestLogPrivacyTests.cs:106 as one of 16 scanner violations. This new test stores a fixed secret-like literal. Patch: generate the per-test query marker without weakening the scanner; root will verify the 15 incoming paths against the fetched baseline and retain the complete broad result. |

| B12 — incoming tracked-secret guard findings | medium | The scanner reports 15 unrelated fixture lines whose complete files are byte-identical to integrated baseline `6e927633d920eafcefa2635a124810b541264489`; after generating our marker, focused scanning retains exactly those 15. Defer: incoming pre-existing fixture/guard reconciliation, without weakening the scanner or claiming a green broad suite. |

Edge-case and verification-gap reviewers returned no findings. All findings above were classified individually before grouping; the surviving entries have distinct causes. No intent or public API change is needed.

## Verification

- Matching Timesheets Debug/source solution restore/build with warnings as errors and serialized MSBuild; each of six built test executables individually. Verify default graph without the workaround plus explicit-override regressions.
- Compiled configuration/construction probes; Python fixture-driver/release-tooling suites.
- Matching EventStore Debug/source builds and individual affected DomainService/Server suites, focused privacy/metadata first. Record exact broad blockers and focused fallback evidence without weakening gates.
- Retain audit JSON/withdrawal evidence, inspect diff/ledger preservation, validate each message with owning pinned commitlint, fetch/inspect divergence before ordinary pushes.

### Implementation receipt (2026-10-24)

- The five local implementation/advisory dispositions and original-ledger preservation are recorded in [the verification report](tests/five-build-and-hosting-deferrals.md); exact commands, results, hashes and audit/withdrawal JSON are in [the dated evidence directory](evidence/five-build-and-hosting-deferrals/20261024/).
- Final Timesheets Debug/source restore/build passes without a version override (consumer `3.127.0`, read-only Works default `3.126.0`), with zero warnings/errors. Individual suite/probe results are recorded in the report and receipt as they complete.
- EventStore affected project builds and completed focused metadata/privacy checks pass. The initial temporary-cache NU5037 is retained alongside the successful intact-user-cache restore. Both broad upstream attempts remain incomplete; the nested-Tenants guard failure and exact elapsed/termination evidence are retained. They are not demonstrated regressions or passing broad gates. Root review completed an adequately bounded Server retry; its final result and incoming guard blocker are recorded below.
- Exactly five additive ledger resolutions preserve all original evidence; a separate open entry tracks the three unrelated npm packages. Launch FAIL, historical/runtime obligations and frozen intent are retained. Root completed independent review and publication after this implementation receipt; see Publication Gate. No commit, fetch, push, package publication or deployment was performed by the implementation agent.

## Publication Gate

- [x] Complete independent review and triage findings.
- [x] Validate exact Conventional Commits, inspect remote divergence, and push EventStore changes to main.
- [x] Commit Timesheets content and the owned EventStore gitlink separately; push main and verify both remote heads.

Ordinary main pushes completed and both implementation remote heads were verified. EventStore: `dcd3dce42b67a305b608f03c4dab2b812791abae`; Timesheets content: `231e413bc607d8fecd74f8c0d99b070dc57ce982`; separate pointer: `f759f688e085277fa1f262903c3ea66deaf4a30a`. The parent pointer matches the pushed SDK commit. [Publication receipt](evidence/five-build-and-hosting-deferrals/20261024/publication.json) records exact heads and observed CI runs; hosted CI remains pending. This completion record is committed after implementation publication.

CI runs observed after publication: [Timesheets CI](https://github.com/Hexalith/Hexalith.Timesheets/actions/runs/40063665856) and [EventStore CI](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/40063600875). These links record queued/in-progress runs, not passing results.
