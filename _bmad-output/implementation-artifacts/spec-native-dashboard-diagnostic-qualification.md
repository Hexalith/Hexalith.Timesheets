---
title: 'Qualify native dashboard ownership diagnostics'
type: 'chore'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The ownership failure diagnostics introduced by `509c6341e306309cf9f57ed49450e8d7934670e7` have synthetic regression evidence, but their native qualification attempt was refused by runtime admission.

**Approach:** Qualify the actual restored native Dashboard with the supported isolated security-smoke journey when admission permits. Retain source identity, native PID/start and listener inodes, before/after-health and post-journey observations, visibility limits, owned cleanup and shared preservation. If native ownership refuses, verify privacy-safe failure evidence survives cleanup. Investigate demonstrated failures and make only evidence-supported diagnostic fixes. Separate native observations from synthetic regressions, leaving inaccessible holders, unobserved transfers, system-wide exclusivity and every unfinished deployed acceptance gate open. Preserve existing AppHosts, unrelated changes, gitlinks and launch FAIL. The user authorizes this narrow work, independent review, Conventional Commit validation and push to main.

</frozen-after-approval>

## Implementation Notes

- Starting tree is clean on main at `69f3622b17de70bf3e9043f96577f2b6d1a4cbd9`, a descendant of the requested diagnostic commit. The later SDK alignment is retained. No intent gaps or unapproved irreversible operations; runtime uses disposable owned infrastructure. Expected footprint is this spec, narrow receipts, existing report and ledger; Python diagnostics/tests change only for demonstrated faults.
- Reuse `IsolatedFixture` in `scripts/run_isolated_magic_link_dapr.py`: canonical admission, evaluated source closure, build, native observations, failure receipts, execute/finally cleanup and shared preservation. Run `scripts/test_isolated_magic_link_dapr.py` serially with native execution because both exercise admission. Do not change AppHost topology or production policies.
- Read baseline/git instructions, root build/format configuration, current report/ledger, boundary record and relevant architecture constraints. No root .gitattributes exists. Aspire lifecycle editor tools are unavailable; the supported runner uses exact-path CLI operations and ownership guards. User-approved diagnostic process inspection supersedes the generic manual-inspection restriction for this qualification.
- Read-only investigation found no AppHost in `aspire ps`; existing assets contain native Dashboard/DCP 13.6.1 although the project now declares SDK 13.6.3. Restore the matching checked-in Debug/source graph, retaining actual selected package versions/hashes rather than attributing new evidence to historical 13.6.1 assets.
- Native execution: matching restore passes in 10.367 seconds; supported security-smoke run `run-4y_0l_yg` passes in 282.586 seconds, live journey 1/1 in 74.349 seconds. Actual restored Dashboard/DCP are 13.6.3. Four native PID/start/inode observations match around health/journey/restart; source/asset equality, gateway replacement, all 22 captured exits, owned cleanup and shared preservation pass. No native ownership failure or demonstrated diagnostic bug; no runtime code changed.
- Retained 103 lossless native command/JSON receipts, native-package metadata, offline audit and invocation/restore/inventory outcomes under `evidence/dashboard-native-qualification-20261028/`. The report and ledger keep inaccessible holders, unobserved transfers, exclusivity, actual failed-native tracing and deployed gates open. A separate post-run convenience inventory assertion failed without retaining the offending response; the subsequent captured read-only commands report zero rows. Its first cause remains unclassified; no stop or repair followed.
- Serialized Python verification passes 193 tests in 47.476 seconds, including six synthetic failure/cleanup subcases. Six default compiled suites are being run individually from the native run's successful Debug/source build; Architecture, Contracts and Server have passed when independent artifact review begins. Remaining results will be recorded before commit/publication.
- Two independent reviews completed. The native receipt reviewer verified 103/103 lossless copies, 926 file hashes, 276 unique evaluated imports across 19 projects, all root gitlinks, native identities/timing, owned exits and preservation; bounded privacy scanning found no JWT/private-key/URL-userinfo patterns. The blind reviewer demonstrated permissive checks in the newly added offline audit using synthetic receipt substitutions and optimized Python. Corrections strengthen evidence consistency only; runtime diagnostics stay unchanged. Native package metadata now distinguishes assets/metadata content hashes from physical local archive/sidecar hashes, whose observed values differ. No upstream archive provenance assertion is made.
- Final broad verification is not green: all six built suites complete with 2,782 passed, one failed and five skipped. Five suites pass; IntegrationTests has an existing claims-accessor synthetic SDK read-set failure at line 1264. Its isolated five-case method run passes; no cause or dashboard connection is demonstrated. This is retained as a separate open integration-test obligation, without a C# change or broad-gate closure. All 926 recorded static/build hashes still match after the suites.
- A later boundary check detects external checkout-head changes in 11 root submodules after the native run and initial independent review. Preserve them untouched and exclude them from staging; committed gitlinks, module runtime/build sources and launch-readiness hash remain unchanged. Native qualification applies to the retained source snapshot, not subsequent dependency imports/source. Do not restore/build the external graph. Conventional Commit validation passes; final review/publication must explicitly retain this scope and the failed broad gate.
- Narrow qualification and independent final scope review are complete. This is not completion of Story 3.6 or the broad default test/deployed gates. Final independent review confirms exact six-suite/focused totals, 103 native receipts, 926 unchanged static/build files, unchanged committed/index gitlinks and launch-readiness. Builds advanced again after the boundary receipt's timestamp; that receipt remains a historical observation and makes no current-graph claim. Publication is limited to these evidence artifacts and documentation, preserving unrelated checkout changes and every unresolved limit.

## Review Triage Log

- Blind B1 — **medium, patch**: Python optimization could remove audit assertions. The offline checker now explicitly refuses optimized execution before reading/qualifying receipts.
- Blind B2 — **medium, patch**: the new offline checker omitted the separate manifest. It now requires exact coverage of all 103 fixture files, stored/decompressed hashes and lengths.
- Blind B3 — **medium, patch**: equal source snapshots could contradict initial identity. Cross-check initial Timesheets revision, empty tracked/untracked inputs for every captured source root, and initial versus verification runner/test hashes.
- Blind B4 — **medium, patch**: the new checker verified only the Dashboard package. It now matches all DCP versions/executable hashes against orchestration metadata and cross-checks duplicated controller/root records and parent identities.
- Blind B5 — **medium, patch**: empty socket sets could satisfy equality checks. Require nonempty unique positive numeric listener inodes and complete owner coverage.
- Blind B6 — **medium, patch**: retained owner/bracketing identities were not cross-checked. Require exact expected/visible holder PID/start and both live-process bracket samples.
- Blind B7 — **medium, patch**: recorded health endpoint predicates were unchecked. Require allowed scheme, numeric loopback connection/address coverage, health path and consistent bounded port.
- Blind B8 — **medium, patch**: gateway summary booleans could contradict raw identities. Require separate before/after receipt equality, distinct native PID/start and matching actual DCP parent.
- Blind B9 — **medium, patch**: a shortened exit list could omit captured resources. Require exact coverage of captured runtime identities plus replacement gateway, including every DCP/Dashboard identity.
- Blind B10 — **medium, patch**: shared-preservation booleans could contradict retained fingerprints. Cross-check Redis persistent fingerprints/counts and scheduler identity/revision/counts. Individual expiring values/deadlines deliberately remain absent; offline detail relies on retained runner predicates, now stated explicitly.
- Native R1 — **low, patch**: ambiguous `package_sha512` named an assets content hash. Rename it and retain matching metadata content hash plus separately computed local archive/sidecar hashes. The native reviewer independently confirmed all four named sources and unchanged raw native evidence after correction.
- All blind substitutions are synthetic evidence-consistency checks; none is an observed native ownership failure or native transfer. No review finding authorizes a runtime policy change, native-failure closure or deployed acceptance.
