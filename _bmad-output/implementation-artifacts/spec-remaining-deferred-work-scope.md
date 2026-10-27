---
title: 'Resolve the remaining deferred-work scope'
type: 'bugfix'
created: '2026-10-27'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '1d34143c0d11b8ab1367319be06bd691b98adf75'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/docs/boundary-decision-record.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Migration endpoints lack caller-bound authorization, and the native
dashboard listener handoff has not been investigated. Local preparation cannot
substitute for deployed acceptance.

**Approach:** Implement caller binding at both migration endpoints and investigate
the actual dashboard listener handoff, correcting demonstrated defects. The user
approved these two local follow-ups on 2026-10-27, including independent review,
verification, Conventional Commit validation and push to main. Five closures are
not required; preserve unrelated changes, gitlinks and unfinished deployed gates.

## Boundaries & Constraints

**Always:** Require caller authorization and migration certification. Preserve
opaque denials, pending journals, histories, public signatures and fail-closed
defaults. Retain launch FAIL and the local/deployed evidence distinction.

**Never:** Fabricate five closures, rewrite published history, enable production
providers, operate a deployment, install unpublished SDK dependencies, modify
Works or initialize nested submodules. Do not turn a simulated inaccessible
descriptor into proof about the actual native dashboard lifecycle.

## I/O & Edge-Case Matrix

| Scenario | Input / state | Expected behavior | Failure handling |
| --- | --- | --- | --- |
| Caller binding | Authenticated principal and exact operation/receipt | Server gate evaluates their binding before migration IO | Missing provider denies |
| Replay by another caller | Same operation/receipt, different principal | Refuse without migration or inventory IO | Opaque denial |
| Binding accepted | Caller permitted, certification rejects | Certification still refuses | Preserve stored state |
| Listener handoff | Actual dashboard/socket-inode trace | Report observed lifecycle | Unobserved transfer remains unresolved |
| Incomplete visibility | Inaccessible descriptors | No exclusivity claim | Retain unresolved obligation |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkIndexMigrationEndpoints.cs`
  lacks a caller-aware operation-binding gate.
- New `IMagicLinkIndexMigrationCallerAuthorizer.cs` and separate deny-all type
  in the endpoint directory accept ClaimsPrincipal and exact operation/evidence;
  register via TryAddSingleton and evaluate before MigrateAsync. Move the existing
  v6 request record into its own file when editing its containing endpoint.
- `src/Hexalith.Timesheets.Projections/MagicLinks/IMagicLinkIndexMigrationAuthority.cs`
  checks operation/inventory fingerprint; preserve its contract and default denial.
- `tests/Hexalith.Timesheets.IntegrationTests/InternalSurfaceGuardTests.cs`:
  existing mapped-route fixtures. Explicitly bind callers in positive and legacy
  validation tests so certification, inventory and retry coverage survives. Use
  raw store snapshots and a counting IO seam to prove refusal preserves state.
- `scripts/run_isolated_magic_link_dapr.py`, function
  `dashboard_listener_identity`: PID/start and observable descriptor checks;
  PermissionError leaves inaccessible holders unknown. Retain pre/post-health
  identities, aggregate visibility limits and observable scope; compare identity
  separately from counts affected by unrelated process churn. Retain another
  actual native observation after the journey/gateway restart. Real inherited
  listener and transferred-listener tests must refuse changed/visible shared
  owners; these are synthetic regressions, not proof of native handoff.
- `scripts/test_isolated_magic_link_dapr.py` has real listener and owner probes.
- `docs/deployed-magic-link-2026-10-17.md` records Platform ownership and missing
  deployment prerequisites. Historical access supplies no current authority.

## Tasks & Acceptance

**Execution:**

- [x] In the endpoint directory above, add a caller-aware, default-deny gate with
  separately named types. Bind principal and exact v1 receipt/v6 inventory
  operation before migration; preserve certification, ingress guards and privacy.
- [x] Extend `InternalSurfaceGuardTests.cs` for both routes and the matrix;
  assert refusals precede inventory/storage IO and preserve stored contents.
- [x] Investigate the restored native dashboard through the isolated runner;
  retain sanitized PID/start/socket-inode handoff evidence. Make only justified
  corrections, with real process coverage in the adjacent test file.
- [x] Record results and unresolved limits in a new
  `tests/remaining-local-deferred-followups.md` under implementation artifacts;
  update `deferred-work.md` with non-closing local evidence. Retain deployed DW-143.
- [ ] Independently review, validate a Conventional Commit, fetch and inspect
  main divergence, commit only owned changes and push without force.

**Acceptance Criteria:**

- Given an unbound caller, when either migration route is invoked, then it
  refuses before migration IO without weakening existing certification.
- Given dashboard evidence, when recorded, then distinguish demonstrated
  lifecycle from synthetic possibility; incomplete evidence leaves it open.
- Given verified corrections, when publishing, then main receives the reviewed
  commit without five-closure claims or unrelated gitlink changes.

## Implementation Notes

- Pre-change isolated baseline reached actual NuGet DCP/dashboard and ready
  Timesheets/EventStore resources; dashboard PID/start and two listener inodes
  verified. Security credential handoff then refused with tls_verification_failed
  because Python lacked scoped dev-certificate trust. Owned cleanup completed.
  Retain this refusal as history, and set SSL_CERT_FILE for final qualification.

- Investigation: pinned Aspire dashboard 13.6.1 native entrypoint and Kestrel
  configuration show in-process listeners, with no handoff in those inspected
  paths. No native transfer defect is demonstrated. Improved retained snapshots
  address the evidence gap; inaccessible holders and unobserved transfers stay
  unresolved. Runtime qualification must use the fixture and scoped development
  certificate trust, and complete owned cleanup before builds or code changes.

- Planning: no remaining intent gaps. Small host authorization seam plus local
  process diagnostics/tests; no runtime migration or deployment. Publication is
  explicitly authorized. Scope and spec approval were supplied in the invocation.

- Implementation: both mapped migration routes now evaluate the authenticated
  principal and exact operation/receipt or inventory fingerprint through the
  separately registered default-deny caller authorizer before calling migration.
  Existing certification contracts and migration services are unchanged. The v6
  request record moved to its own file without changing its public signature.
- Endpoint regressions cover missing caller providers, another principal,
  operation/evidence mismatch, independent certification refusal, and replay of
  both successfully persisted migrations by another caller. Counting store and
  inventory seams prove caller refusal precedes request I/O; raw byte snapshots
  retain histories, pending journals and selected/legacy state. Existing malformed
  input, inventory and retry tests explicitly install their caller bindings.
- Listener diagnostics retain separate pre/post-health identities and aggregate
  visibility limits, comparing listener identity independently of process churn.
  The runner also captures an actual post-journey dashboard observation. Real
  inherited TCP and SCM_RIGHTS shared/transferred descriptor tests are synthetic
  process regressions; they demonstrate no native dashboard transfer or deployed
  acceptance. Inaccessible holders, exclusivity and unobserved transfers remain
  unresolved.

## Spec Change Log

## Review Triage Log

| ID | Verdict / route | Evidence and disposition |
| --- | --- | --- |
| B1 | medium / patch | Modified native runtime receipts were absent in the review snapshot. Parent will complete the planned native qualification and retain actual lifecycle, journey and cleanup receipts before publication. |
| B2 | medium / patch | Only four suite receipts existed at snapshot time. Integration and Works remain required; the sequential six-suite command is running and publication waits for both. |
| B3 | medium / patch | The new caller provider can throw TimeoutException/HttpRequestException: v1 returns opaque 503 while v6 currently escapes. Add identical bounded opaque outage handling and faulting-provider HTTP tests proving no migration IO. |
| B4 | medium / defer | Existing dashboard_listener_identity/verify_dashboard_ready discard conflicting observations on failure; the new failure receipt retains only a generic message. This is a pre-existing diagnostic/privacy limitation, not evidence of an actual native handoff. Keep richer failure-time ownership tracing unresolved alongside the dashboard obligation. |
| B5 | medium / patch | New real-process handshake readline calls have no deadline; a stalled live child can hang unittest/CI. Bound handshakes and verify the stalled-child refusal. |
| B6 | medium / patch | New fixture cleanup stops at the first terminate/wait failure, leaving later children/streams unhandled. Ensure all owned children are killed/reaped and streams closed on failed termination. |
| B7 | medium / patch | Current inaccessible-holder tests do not prove a later accessible descriptor is inspected. Add the mixed-descriptor visible-sharing regression. |
| B8 | medium / patch | Both test authorizer methods use identical permissions/counters, so swapping route dispatch is undetectable. Grouped with V1; test permission for exactly one migration version. |
| B9 | medium / patch | Direct helper coverage does not execute the new finally-path capture call or failure gate. Grouped with V2; exercise invocation, nonzero failure result and cleanup through execute. |
| B10 | low / patch | Lifecycle capture runs after failed journeys too; the standalone receipt lacks outcome/timing context. Add explicit journey outcome and observation timing without implying overall qualification. |
| V1 | medium / patch | Pre-verified regression gap: swapped v1/v6 authorization calls leave existing tests unchanged. Add mapped-route version-specific permissions, opaque refusal and unchanged bytes; preserve successful permitted migrations. |
| V2 | medium / patch | Pre-verified mutation: deleting the actual capture invocation still passes all 180 tests. Add execution-path receipt and failure/cleanup assertions. |

Edge-case hunter returned no findings. No layer was skipped. Each finding above retains its own verdict before grouping.

Review patches completed: B3/B5/B6/B7/B8/B9/B10/V1/V2 are covered by 55 passing endpoint cases and 184 passing Python tests. B1/B2 are parent verification gates; B2 completed with 2,777 passed/five pre-existing skips across all six initial suites. B4 remains a non-closing diagnostic limitation in the ledger. Independent V1/V2 recheck confirms both gaps closed; a targeted in-memory deletion mutation now fails all four execute scenarios instead of passing unnoticed.

## Verification

- Final parent gates: solution build passed with zero warnings/errors; final
  native run `run-pjnjhza6` exited zero in 273.482 seconds, with one passing live
  journey and no skips. Four dashboard observations retain identical PID/start/
  listener inodes; source/build snapshots match. Owned cleanup, process exits,
  gateway replacement and shared preservation pass. This closes review B1/B2;
  B4 and all deployed acceptance remain unresolved. The prior pre-resource
  refusal and unchanged-timeout read-only diagnostic are retained. See
  [complete results and limits](tests/remaining-local-deferred-followups.md).
  Publication is authorized and follows this verified implementation commit.

- Review-correction checks: 55 endpoint cases and 184 Python cases pass, zero
  skips; focused Debug/source build has zero warnings/errors. All six initial
  compiled suites passed 2,777 cases with five existing skips and no failures;
  this broad phase precedes the review corrections. Final focused checks and the
  final solution build cover those corrections; native qualification follows.

- Implementation focused checks on 2026-10-27: Debug/source IntegrationTests
  project build passed with zero warnings/errors; all 49
  `InternalSurfaceGuardTests` cases passed without skips; isolated runner unittest
  suite passed all 180 tests; `git diff --check` passed. Logs are retained locally
  at `/tmp/timesheets-remaining-focused-build.log`,
  `/tmp/timesheets-remaining-focused-endpoints.log` and
  `/tmp/timesheets-remaining-focused-runner.log`. Full solution/six-suite validation,
  final native qualification, independent review and publication remain with the
  dispatch parent; these focused checks supply no deployed proof.

- Investigation on 2026-10-27 found ten numbered open ledger entries:
  DW-86/113/119/132/137/143/145/194 require deployed evidence; DW-19 requires a
  historical commit rewrite; DW-184 requires SDK publication. Two unresolved
  unnumbered follow-ups concern dashboard ownership visibility and repair
  manifest capacity. Capacity extension also needs SDK work/publication.
- Initial pre-approval investigation: NuGet Client ended at 3.128.2; the held patch needs unpublished SDK source
  `c613e55aefd0650c1e64ed0580c8495657585a89`. Upstream CI 40492684889 failed;
  baseline Timesheets CI 40389784548 passed. No module tests ran during that initial investigation; implementation results are recorded above.
- If implementing, run the matching Debug/source restore/build commands in
  AGENTS.md, followed by focused InternalSurfaceGuardTests and all six individual
  executable suites. For runner changes use `python3 -m unittest discover -s
  scripts -p 'test_isolated_magic_link_dapr.py'`. Load Aspire orchestration before
  runtime work; preserve admission refusals. Check Markdown links and
  `git diff --check`. Local tests supply no deployed proof.
