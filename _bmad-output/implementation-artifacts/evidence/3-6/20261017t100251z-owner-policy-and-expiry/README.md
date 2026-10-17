# Story 3.6 owner policy and expiry reconciliation — 2026-10-17

Story 3.6 remains in progress. New Platform documentation simplifies owner-side
document review; it supplies no deployed acceptance evidence and cannot revive
expired candidate `20261015t162756z`.

## New public evidence

This planning resume starts from clean Timesheets `main` at
`148498fc6167211a8e86f2396445723666d490d3`, observing Platform
`d186ad667639b9d629f7de51921e9600e7f7f992`.

- Platform `f81b864e17012bb75c60374f4efaab87c20d63e1` includes the
  [reviewed offline production plan](../../../../../references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261017t070848z-offline-owner-evidence-production-plan/PLAN-v3.md)
  and [document review](../../../../../references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261017t070848z-offline-owner-evidence-production-plan/FINAL-REVIEW.md).
  G1–G9 identify proposed Platform tooling; the plan commissions no implementation
  or operational action.
- Platform `c31721650589222471746b08d902bf8d14e0ce5c` adds the
  [A0 draft manifest](../../../../../references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261017t083905z-a0-owner-authority-json-drafts/package-manifest.json).
  Its payloads retain `DRAFT_BLOCKED`, null owner observations and unperformed
  review. Signature files are present; this resume neither verifies them nor
  treats a document signature as proof of the missing operational facts.
- The same revision adds the
  [sole-owner documentary policy](../../../../../references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261017t093206z-sole-owner-documentary-policy/sole-owner-policy.json).
  The owner prepares and reviews their own project documents. A distinct human
  reviewer, external authority registry, new personal certificate and new signing
  key are not required for that documentary scope. Earlier requirements for
  those steps remain historical; they are not the next owner-document task.
  The policy explicitly leaves deployed authorization and acceptance rules
  unchanged. Actual PKI, grants, custody and runtime behavior still require
  factual evidence; key revocation remains un-attested and compromise unknown.

## Remaining work

At **2026-10-17T10:02:51Z**, the clock is past the unchanged exclusive ceiling
**2026-10-16T16:27:56Z**. Preparation remains **2026-10-15T16:27:56Z**.
The candidate is expired; this reconciliation extends no time and allocates
no replacement. A fresh prerequisite needs the owner's separately scoped
process; an approval of this expired candidate cannot make it executable.

Current factual server/client/TLS identity, native grants and complete revocation
and current-rights evidence, protected delivery, actual runtime/HTTP qualification,
and preservation-drift disposition are still missing in the inspected handoff.
The broader [deployed acceptance requirements](../../../../../docs/deployed-magic-link-acceptance.md#platform-source-findings-and-outstanding-prerequisites)
also remain: an enrolled Timesheets deployment, production resource-binding
issuer, certified legacy coverage/continuity and maintenance authority, and
installed subscriber/shared-rebuild proof. Platform's documentary policy does
not replace those facts. No separate Timesheets runtime fix was established by
the scoped source/history investigation or independent investigator.

The [historical exact-verifier refusal](../20261016t112503z-offline-owner-evidence-intake/exact-verifier-result.json)
remains **exit 1**, `offline-revision-integrity-or-custody-refusal`. It was not
rerun, waived or changed. No completed implementation, synthetic suite, live
journey, signature verification or unchanged packet/runtime hash sweep was
repeated. This is public repository intake, not a fresh cluster census or an
exhaustive search of externally held owner artifacts.

Intent gaps: none established. Footprint: Timesheets evidence/tracking text
only, no callable surface. Deployment, maintenance and domain writes remain
outside this reconciliation. Frozen intent and prior checkpoint bytes are
preserved. No private credential/configuration access or operational action
occurred; Platform sources, packets, failures and gitlinks received no writes.

Spec-23 remains **draft**; Story **3.6/Epic 3/4.27 in progress**;
**4.1 operational gate closed**; release **FAIL**. The build workflow cannot
advance this draft to implementation while its factual prerequisites are missing.

## Verification of this reconciliation

```text
tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor
```

Exit **0**: **13 passed**, zero errors/failures/skips, **0.155s**, using existing
built assets. This checks the changed readiness documents; it is not a new build
or deployed/runtime test. A local Python check exits 0 for all six evidence links,
unchanged frozen intent and the exact 60,536-byte prior checkpoint prefix, with
draft/in-progress tracking retained. `git diff --check` exits 0. These checks
establish document consistency only and close no external acceptance gate.
