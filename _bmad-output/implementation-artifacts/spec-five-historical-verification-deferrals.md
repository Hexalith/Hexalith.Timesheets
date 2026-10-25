---
title: 'Reconcile five historical verification deferrals'
type: 'chore'
created: '2026-10-25'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five historical ledger observations remain undisposed after their implementation or evidence wording changed: unbounded authority paging, process-wide registration-test environment mutation, ignored unknown authority events, an overstated live-host HTTP claim, and obsolete CLI-bundle resolution concerns.

**Approach:** Verify the current code and focused tests, then add a disposition to each original entry and retain a concise evidence report. Three duplicate defect reports can close against existing fixes. The HTTP and CLI-bundle observations can close only as historical diagnoses superseded by accurately scoped evidence and the existing production-identity/current-graph runtime follow-ups. Those acceptance obligations stay open. Preserve all original ledger text, runtime code, package selections, fail-closed defaults, guidance entry points and launch verdicts. Commit and push the reviewed documentation to main as requested.

</frozen-after-approval>

## Implementation Notes

- Selected five original prose entries: September 13 paging and live-host claim; September 14 RuntimeRegistrationTests mutation; September 15 rerun unknown-event tolerance; September 12 AppHost SDK/CLI-bundle runtime observation.
- No intent gap or unapproved external action remains. The implementation is three documentation files, adds no callable API, and performs no runtime or persistence change. The user's explicit five-item batch and main push are retained.
- Reuse the current loading budget/admission code, in-memory gateway configuration tests, composed HTTP fixture tests and AppHost package-governance checks. The investigation found that shared-shape layering, stale-snapshot consistency and current-graph startup remain substantive separate work; they are excluded.
- Run the matching Debug/source solution restore/build, focused loader/registration/HTTP and AppHost-governance checks. Record exact commands, source identities and test counts; passing fixtures supply no deployed authority or current-graph startup acceptance.
- Implemented exactly five additive resolution notes and the retained evidence report; stripping those notes reproduces the baseline ledger byte-for-byte. Runtime/configuration/package files and dependency gitlinks are unchanged.
- Restore passed; solution build passed with zero warnings/errors (56.05 s). Fresh executable selections passed Server 174/174, scaffold governance 28/28 and composed HTTP 2/2: 204 total, zero failures/errors/skips/not-run. Full command lines and source identities are retained in `tests/five-historical-verification-deferrals.md`.
- Independent Blind Hunter review completed. Root verified every finding and patched the report to distinguish XML fitness from actual restored package presence, explicitly retain production tenant/contributor validator wiring, and name the deadline regressions already included in the successful selection. Fresh AppHost assets contain the Linux x64 orchestration and dashboard packages at 13.6.1. No runtime source changed and no test rerun was needed for these prose clarifications.

## Review Triage Log

The reviewer measured 12.589 kB and a finding floor of four. All four findings were checked individually:

| ID | Finding | Verdict and evidence | Disposition |
| --- | --- | --- | --- |
| B1 | Raw XML/build receipts are only in temporary storage | low: raw files are explicitly supplemental; the tracked report retains full source identities, exact commands, selections, counts, timestamps and build outcome. The reviewer independently inspected the existing XML/logs and confirmed them. No new runtime change or full-suite claim relies on unavailable raw evidence. | Rejected as a low-value expansion into additional artifact retention for this documentation reconciliation. |
| B2 | XML governance does not verify restored orchestration/dashboard package presence | medium: the named test checks the CLI-bundle setting and suppression only; package retention needs separate evidence. Inspection of freshly generated AppHost project.assets.json confirms both Linux x64 packages at 13.6.1. | Patched report with the exact identities and inspection command, and narrowed the test's described coverage. |
| B3 | Management identity follow-up does not fully name tenant/contributor validator wiring | low: the report already retains fail-closed defaults and production access/authority, but claims alone cannot supply those validators. | Patched report to name both validator interfaces and retain their production wiring/verification obligation explicitly. |
| B4 | Awaited-read claim omits deadline regression names | low: the successful loader-class selection includes cumulative and non-cooperative stream/read-model deadline cases; the table named only count/byte cases. | Patched report with all three supporting methods. |

All accepted corrections are complete. No new deferred work was introduced by this review; existing production/runtime and consistency obligations remain open.
