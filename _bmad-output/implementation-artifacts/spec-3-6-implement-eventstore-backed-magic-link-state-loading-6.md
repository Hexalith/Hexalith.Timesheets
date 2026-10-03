---
title: 'Close Story 3.6 confirmation display and host verification patches'
type: 'bugfix'
created: '2026-10-03'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6's latest implementation review leaves four accepted patches open. A valid confirmation link can be submitted after Activity Type deactivation but cannot display its recorded type; configured internal-port delivery, the kernel's concrete loader registration, and the host's HTTP claim accessor lack executable evidence.

**Approach:** Apply the recorded confirm-as-is decision: confirmation describe may display a uniquely resolved tenant Activity Type in a fresh catalog regardless of capture availability, while adjustment describe and issuance retain availability checks. Leave the loader unchanged. Add focused server and HTTP tests that prove this distinction, resolve the registered loader with its dependencies, exercise configuration-bound internal-port projection delivery and rejection on another port with persisted read-model assertions, and load admin state through the host's actual claim accessor. Preserve opaque denial, server-side authorization, EventStore persistence, fail-closed defaults, and all existing deferred ownership. Reconcile the four latest review checkboxes and current verification evidence without closing historical deferred work, altering topology, updating packages, or changing submodule pointers.

</frozen-after-approval>

## Implementation Notes

- Investigation confirmed all four latest patches remain open. Their intent is already settled by the 2026-09-18 review decision; no human-only choice or irreversible operation is required. The production change is limited to the command service's private display-label resolution, with existing test fixtures reused for verification.
- Canonical build baseline: `3f602aa285ee31ff936bc38ac8177cd2f563c6c7`. Actual `global.json` and installed SDK both select `10.0.401`; the managed agent-context SDK value is historical.
- Implemented a private capture-availability switch for label resolution: confirmation keeps the unique tenant-owned/Fresh catalog gate and accepts the recorded label after deactivation; adjustment retains both availability flags. The loader and issuance checks remain unchanged.
- Reused existing server fixtures and HTTP factory/filter/store doubles. Concrete HTTP journeys now retain the kernel's loader registration. New tests exercise canonical deactivation delivery, matching and mismatching configured/local ports, and admin catalog/capability reads selected by actual HTTP claims. Local-port simulation is explicitly in-process evidence.
- Bare solution build and the explicit-Debug retry failed with `HXW0002` plus missing Works references (176 errors). Restore/build with `Configuration=Debug` and `UseHexalithProjectReferences=true` passed; the latter build needed a matching restore to resolve Roslynator.Analyzers 4.15.0. No dependency configuration was edited.
- The initial HTTP configuration callback failed with a disposed ConfigurationManager; configuring the existing WebHost setting instead allowed the real host options binding to execute. All 20 focused HTTP cases then passed.
- Full individual test inventory: ArchitectureTests 55; Contracts.Tests 90; IntegrationTests 109 (105 passed, four existing skips); Projections.Tests 146; Server.Tests 478; Works.Tests 76. Total 954, 950 passed, zero failures. The first architecture run exposed pre-existing dated catalog drift; launch readiness now records current source values as an observation without claiming renewed package audits.
- Recorded the failed current Aspire smoke separately: first start timed out, no-build retry started the AppHost, Timesheets readiness exited 18, security was Unhealthy, and stop succeeded. Durable submission, concurrent single-use, and real network topology remain deferred as before.
- Reconciled the four latest review checkboxes, cumulative File List, change log, and current test summary. Story/sprint remain in-progress until the independent review completes.
- Applied all six independent-review findings: assert no downstream AdjustmentResult; revoke an existing capability through the HTTP admin path; compare the actual authorization context with HTTP claims/accessor values; replay an event-bearing dispatch with the admitted fingerprint on both ports and prove persisted-state outcomes; exercise all four external routes on the public port after internal projection delivery; and replace the current Build row with the source-reference commands and canonical October baseline while retaining the bare-build blocker.

- Final solution build passed with zero warnings/errors after the review patches. ArchitectureTests, IntegrationTests, and Server.Tests were rerun and passed; the complete inventory remains 954 total / 950 passed / four existing skips. All six findings were patched, with no new review deferrals. Canonical story and sprint now agree on review status; release readiness remains FAIL for the existing durable-write and infrastructure gaps.

## Review Triage Log

- **medium / patch:** The availability test inspected TimeEntryResult although adjustment work uses AdjustmentResult. The assertion now checks AdjustmentResult is null, so downstream adjustment cannot hide behind a non-dispatched combined result.
- **medium / patch:** Issuing a new capability proves stream addressing but cannot detect discarded existing events. The same administrator theory now revokes an issued capability: matching claims require folded state and yield Accepted; foreign claims read only their own missing stream and yield Forbidden.
- **medium / patch:** Accessor observations alone did not prove the context passed to authorization. The test now captures actual authorization requests for both issue and revoke and compares tenant, actor, and correlation with the HTTP-established context.
- **medium / patch:** The public-port rejection request carried no events and an invented fingerprint. The replacement uses a real admitted fingerprint and an issuance event; public refusal preserves both stored read models, and admitting the identical dispatch internally completes and stores its candidate.
- **medium / patch:** Configured-port valid-link evidence fetched on the internal port. The same host now projects on 8081 and describes/submits confirmation and adjustment on 8080, preserving its store and configured guard.
- **medium / patch:** The current Build row cited the historical bare command despite a current failure, and two neighboring rows had old integration counts. Current evidence now records October source-reference commands/baseline, the bare-build failure, and 109/105 integration counts. Fitness assertions pin the new evidence without weakening the warning/error gate.
