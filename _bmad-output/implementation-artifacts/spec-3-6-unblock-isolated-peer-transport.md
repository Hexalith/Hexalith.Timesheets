---
title: 'Unblock Story 3.6 isolated Dapr peer transport'
type: 'feature'
created: '2026-11-03'
status: 'in-review'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '53786d00b31459b41f620b5b15f085ec03c6204f'
story_key: '3-6-implement-eventstore-backed-magic-link-state-loading'
context:
  - 'AGENTS.md'
  - '_bmad-output/implementation-artifacts/handoff-3-6-isolated-dapr-peer-identity.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6's catalog and writer bootstrap succeed, but its first metadata request fails with `403 / ERR_DIRECT_INVOKE`. Recorded diagnosis identifies missing authenticated Dapr peer transport. EventStore has no public transport integration covering Timesheets actors.

**Approach:** Qualify a Development transport capability in EventStore, then consume it through the existing Timesheets fixture. Require authenticated peer and actor/control-plane evidence before resuming persisted qualification.

## Boundaries & Constraints

**Always:** Keep trust/transport plumbing in EventStore. Use owned temporary trust and pinned Sentry; qualify the isolated placement/scheduler services. Preserve eight owner routes, gateway technical composite, binding-v2 checks, four gateway/seven Timesheets grants, default deny, application/channel/workload gates, admission, source checks and historical custody/evidence.

**Never:** Borrow global credentials, use shared infrastructure, relax ACLs, manufacture authority headers, add bypass endpoints or a second host. Production enrollment, deployed acceptance, package upgrades and Git mutations are excluded. Signed-service/catalog-context production remains a separate unqualified gate.

**User decision, 2026-11-03:** Authorize implementation in the owning EventStore repository, including one owned local Sentry and compatible isolated control-plane configuration, then consume the qualified capability in Timesheets. Continue implementation in this session under this approved scope.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Development fixture | Owned trust and exact peers | Authenticated invocation and working actors/control plane | Refuse incomplete identity before writes |
| Untrusted peer | Wrong app, namespace, trust or certificate | Denial; stored state unchanged | Opaque external failure |
| Unavailable trust | Missing/expired credentials or Sentry | Refusal without unauthenticated fallback | Sanitized failure and custody receipt |
| Interrupted startup | Partial owned resources | Bounded exact cleanup | Retain protected material without complete custody |

</frozen-after-approval>

## Code Map

Owner paths are relative to `references/Hexalith.EventStore/`; edit from that repository after scope approval.

- `src/Hexalith.EventStore.Aspire/DevelopmentRouteCatalogAspireExtensions.cs` — `Compose` replaces ACLs; no transport provisioner.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/RouteCatalogDevelopmentSentry.cs` and `RouteCatalogDevelopmentSidecar.cs` — internal starting points. `ConfigureMutualTls` disables placement/scheduler; reuse requires actor coverage.
- `src/Hexalith.Timesheets.AppHost/Program.cs` — existing private catalog selection.
- `scripts/run_isolated_magic_link_dapr.py` — infrastructure, catalog preparation, runtime verification and cleanup.
- `scripts/local_aspire_magic_link_journey.py` — preserve persisted assertions and distinguish the unqualified producer/context path.

## Tasks & Acceptance

**Execution:**
- [x] Owner `src/Hexalith.EventStore.Aspire/DevelopmentRouteCatalogTransportExtensions.cs` — expose opt-in Development lifecycle ownership for Sentry, temporary trust, native sidecars, compatible control plane and cleanup; separate supporting types.
- [x] Owner `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/RouteCatalogHostedLiveTests.cs` — qualify peer identity, actor persistence/restart and negative/cleanup cases through the public seam.
- [x] `src/Hexalith.Timesheets.AppHost/Program.cs` and `scripts/run_isolated_magic_link_dapr.py` — consume the qualified seam; bind source/custody/runtime evidence.
- [x] `scripts/test_isolated_magic_link_dapr.py` — cover the matrix, ordinary mode and cleanup failures.
- [x] `_bmad-output/implementation-artifacts/handoff-3-6-isolated-dapr-peer-identity.md` — append transport proof and the next observed gate.

**Acceptance Criteria:**
- Given qualified transport, when metadata is called, then evidence distinguishes peer delivery from application authorization; producer/context refusal stays open.
- Given transport passes, when Story 3.6 is assessed, then persisted effects, retry/single-use, restart/rebuild and deployed gates require independent verification.

## Implementation Notes

- EventStore owns the opt-in Linux/Development lifecycle, existing immutable Sentry/placement/scheduler 1.18.7 pins, temporary trust, native flags, private custody and bounded exact cleanup. The CLI runtime adapter only replaces the pinned CLI's fixed Sentry address; all ACL/application/workload/catalog gates remain intact.
- The owning live witnesses exercise real actor state, persisted readback, reminder delivery, native restart, wrong app/namespace/trust, incomplete credentials/Sentry, malformed flags, partial startup and cleanup refusal. A separate focused witness renders the public Aspire API and launches its actual pinned CLI with secret parameters.
- The first consumer attempt stopped before catalog/writer/metadata qualification because Dapr CLI 1.18.2 resolves `--runtime-path` as an installation root containing `.dapr/bin/daprd`. Its missing-owned-executable failure and cleanup receipts are retained in `evidence/3-6/20261103-owned-peer-consumer/run-9g26dgfh`. The owner adapter layout was repaired and the focused public-API/CLI witness passed before retry.
- Timesheets uses a thin Debug/source build bridge for the unpublished owner API. Ordinary published-package startup is unchanged; selecting the new fixture in a published-package build refuses clearly rather than upgrading packages.
- The supported retry `evidence/3-6/20261103-owned-peer-consumer/run-tvhk9h3o` qualified actual native/control trust, unchanged catalog/readbacks and writer bootstrap. Metadata advanced to HTTP 503/empty receiver response; signed-service/catalog-context producer qualification remains open. Matching source snapshots and complete exact cleanup are retained. Persisted/retry/single-use/restart/rebuild/deployed gates and broad-test limits remain independent.
- Independent review patches are recorded in `evidence/3-6/20261103-owned-transport-review-fixes`: owner build, six native cases, six CI tooling cases and metadata/privacy coverage pass. Parent verification passes all 234 Python tests and checks the retained hashes/XML. Its final consumer attempt is admission-refused before root build/startup/resources, and later checks remain refused. Current-source consumer revalidation is blocked; this spec remains in-review. Earlier runtime proof retains its exact dated source scope.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| BH-1: CI control images | high | New live tests use pull-never placement/scheduler images; the existing CI preparation pulls only Sentry/Redis. Patch the existing isolated tool preparation, preserving ordinary job pins. |
| BH-2: CI CLI pin | high | The new CLI witness requires the recorded 1.18.2 checksum; owner CI installs 1.18.0 and prepares only a separate daprd. Patch isolated CLI preparation/selection without changing the ordinary installation. |
| BH-3: enrollment XML order | medium | Public XML says after composition, while the actual consumer and configuration capture require before composition. Direct documentation patch. |
| BH-4: nullable sidecar identity | medium | A process can exit after the initial checks; storing null makes the later absent-process comparison null == null and prevents cleanup. Patch refusal of a vanished identity before recording it. |
| BH-5: runtime symlink | medium | Startup hashes a lexical path but observation compares it with the resolved native executable; an accepted symlink can fail only after launch. Patch canonical-path validation before resource creation. |
| BH-6: interpreter prerequisite | medium | The generated adapter requires /usr/bin/python3, but startup currently creates controls without checking this prerequisite. Patch early validation and document the dependency. |
| BH-7: incremental startup custody | high | Custody receives PID/start identities only after all controls are ready. Termination during startup loses the durable ownership inventory. Patch durable directory/launch intents and captured identities as each existing process is started. |
| BH-8: qualification setup cleanup | medium | The real actor test acquires transport before setup that can throw, and enters finally only later. Patch immediate asynchronous ownership disposal. |
| BH-9: denied persisted writes | medium | Denied peer probes target read-only config; unchanged actor state cannot establish rejection of attempted writes. Patch the technical fixture with an allowed persisted-write control and denied service-invocation writes plus independent stored readback; production routes/grants stay unchanged. |
| BH-10: 503 classifier | low | The observed empty 503 remains machine-classified unqualified while the receipt describes a source-derived receiver response. Patch the narrow classification and coverage, retaining uncertainty about the exact receiver predicate. |
| EC-1: premature startup qualification | medium | _ready is set before final validation/adapter/ready-receipt writes; their failure can report qualified successful cleanup. Patch failed-start state before disposal and add a final-publication failure regression. |
| EC-2: setup scope | medium | Same verified setup cleanup defect as BH-8; grouped with that patch. |
| EC-3: PID reuse receipt collision | low | A long-lived owner could encounter PID reuse and the create-only PID receipt would refuse restart. Rejected under the low-finding rule: current bounded fixtures are unlikely to encounter it, and a safe repair needs composite identity bookkeeping rather than only a filename change. Failure remains closed. |
| VG-1: expired unchanged credential | medium | Pre-verified gap: replacing a certificate triggers hash refusal before date validation, so removing the date guard leaves that test green. Patch an internal clock seam and unchanged-byte expiry coverage without adding a public API. |
| VG-2: owner CI prerequisites | high | Source verification confirms both absent image preparation and the CLI checksum mismatch. Grouped with BH-1/BH-2; patch existing isolated preparation and its tests. |

All retained patch entries were corrected and checked in the review-fix packet; the parent read the resulting source delta and independently verified the six passing native XML cases. EC-3 remains rejected as explained above. Final current-source consumer verification is blocked by canonical admission, not waived; no completion or broad-gate pass is claimed.

## Design Notes

Two independent read-only investigations found no Timesheets-only repair. Pinned EventStore `7c93a1234d14c66f5ee85ed48caaee1d30a671f3` and recorded `origin/main` `0e1173bb4b01348430910bade3ab7ee0dce95d97` lack the seam. No fetch, dependency change, build, test or live run occurred. Story remains in progress; launch remains FAIL.

## Verification

- Run owner-prescribed build/live tests, including actor/control-plane end-state assertions.
- Run root AGENTS.md's Debug/source restore/build, Python fixture regressions and relevant individual xUnit executables. Preserve earlier failures; passing broad gates remain required for story completion.
- Resume the supported runner after owner qualification and idle admission; retain source/custody/cleanup receipts and stop at unsupported producer/context requirements.
- Draft checks: whitespace, existing references and Git status; no runtime qualification.
