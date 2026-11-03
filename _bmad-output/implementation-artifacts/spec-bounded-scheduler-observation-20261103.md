---
title: 'Bound scheduler preservation observations'
type: 'bugfix'
created: '2026-11-03'
status: 'done'
baseline_commit: '53d112c59a3827cf14e2c4565f048832de88467e'
route: 'dispatch'
review_loop_iteration: 1
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The scheduler preservation reader can lose its sanitized failure
receipt on deeply nested JSON, consumes an unbounded HTTP body, and has only a
socket timeout. These are three findings across two unresolved ledger entries.
An incomplete observation must never become evidence that shared state was
preserved.

**Approach:** Make scheduler observation bounded and retain safe refusal
metadata for parser/resource failures. Verify complete successful observations
and refusals through offline fixtures and owned loopback transports. Record
exact outcomes, then commit and push the verified changes to `main` as requested.
The user approved completing these three local findings across two ledger entries,
including verification and publication to main.

## Boundaries & Constraints

**Always:** Retain the exact scheduler range request and protected direct
transport; accept only complete ranges; preserve strict Redis/scheduler
comparison and existing sanitized phase/category/timestamp receipts. Release
all owned transport resources on success, refusal, interruption, and timeout.
Report actual closure counts and qualification limits.

**Never:** Access shared services for these checks, terminate foreign sessions,
rewrite history, change dependency source/gitlinks, weaken a scanner or
preservation gate, or claim deployed/runtime acceptance from offline results.
Keep Story 3.6 and launch FAIL unchanged.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Complete range | Valid empty/nonempty range within supported bounds | Existing complete fingerprint and comparison behavior | Response resources close |
| Parser depth | JSON deeper than the parser supports | No successful observation | Sanitized scheduler-phase refusal receipt |
| Oversized body | Declared or streamed response exceeds the byte budget | Refuse before retaining an unbounded body | No payload in receipt or traceback |
| Trickle/stall | Connection, headers, or body exceed the total deadline | Stop the observation with bounded cleanup | Sanitized refusal; no surviving owned observer |
| Truncation/redirect | Incomplete body or redirected endpoint | Existing refusal behavior | No alternate endpoint contact |
| Interruption | Caller interrupts an active observation | Preserve interruption semantics and receipt | Release owned resources |

</frozen-after-approval>

## Code Map

- `scripts/run_isolated_magic_link_dapr.py`: `read_only_shared_snapshot` uses
  `json.load` on the scheduler response and omits `RecursionError` from its safe
  refusal path. `SharedObservationError`, `SharedSnapshot`, and
  `scheduler_fingerprint` own sanitized outcomes and complete-range validation.
  Reuse `owned_http_open`, which disables proxies and redirects.
- `scripts/test_isolated_magic_link_dapr.py`: reuse `RedisSnapshotConnection`,
  the existing scheduler protocol/redirect tests, and owned HTTP-server helpers.
  Assert safe receipts, transport cleanup, complete comparisons, and absence of
  payload details. Redis remains a transport double.
- `_bmad-output/implementation-artifacts/deferred-work.md`: the final two
  unnumbered entries originate in
  `spec-five-scheduler-and-secret-findings-20261103.md`; its R1/R2/R3 review rows
  establish the three findings. Earlier scheduler and secret-scan findings are
  explicitly resolved and must not be counted again.
- `docs/boundary-decision-record.md` and planning `architecture.md`: preserve
  privacy, platform boundaries, and the separation between local evidence and
  deployment acceptance.

## Tasks & Acceptance

**Execution:**

- [x] `scripts/run_isolated_magic_link_dapr.py` — add safe depth refusal and
  bounded complete scheduler collection, including opening/headers/body and
  deterministic cleanup; document conservative local byte/time budgets.
- [x] `scripts/test_isolated_magic_link_dapr.py` — exercise every matrix case,
  exact byte boundaries, real trickled headers/body, response closure, and
  safe on-disk receipts. Retain existing proxy/redirect/protocol regressions.
- [x] `_bmad-output/implementation-artifacts/tests/bounded-scheduler-observation-20261103.md`
  — record exact commands, source identity, results and environmental blockers.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` — append supported
  resolutions to the two selected entries only after verification.

**Acceptance Criteria:**

- Given an unsupported or incomplete scheduler response, when observation
  refuses, then no snapshot can pass preservation and only safe failure
  metadata is retained.
- Given a complete supported range, when observed within bounds, then its
  fingerprint and preservation comparison remain equivalent to existing behavior.
- Given owned failure fixtures, when execution ends, then owned connections and
  observers are released and shared services remain untouched.
- Given verified changes, when publication completes, then `origin/main`
  contains the validated Conventional Commit without unrelated changes.

## Implementation Notes

- Planning baseline: clean `main` at
  `53d112c59a3827cf14e2c4565f048832de88467e`.
- Independent read-only ledger audit confirms three findings/two entries;
  additional unresolved items require operational/platform scope.
- Irreversible action: the explicitly requested commit/push. No runtime or
  domain-state mutation is needed for the local increment.

## Verification

- `python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py -v`
  — complete offline fixture suite passes. Keep any canonical-admission
  contention separate from focused evidence; never disturb the holder.
- Focused scheduler regressions — exact boundary success/refusal, total deadline,
  depth/protocol failure, cleanup, and safe receipt assertions pass.
- `git diff --check` — no whitespace errors.
- Validate the proposed message with the repository's commitlint before commit;
  fetch and inspect divergence before pushing `main`.

- User approval 2026-11-03: option 1 approves the three-finding local scope and continuation through verification and push.

## First-Pass Parent Verification

- The staged diff was read against the full baseline. All four implementation tasks and all six matrix rows are covered by passing tests in the retained full-suite log.
- Independently verified both command receipts, the two current source hashes, the decompressed output hashes, 18/249 discovered test records, and successful summaries. Publication is the remaining authorized completion step.

## Review Triage Log

| Finding | Verdict and evidence | Route |
| --- | --- | --- |
| B1: negative native chunk size bypasses sized reads | high; parent reproduced 8,388,608 retained bytes from a -1 chunk under the 4 MiB ceiling; installed HTTPResponse calls raw read(-1) | bad_spec: native chunk decoding cannot establish the required bound |
| B2: invalid chunk CRLF passes | medium; parent reproduced a valid fingerprint after XX replaces the data separator; also pre-existing on the baseline | Group with B1: strict bounded framing must precede consumption |
| B3: missing final trailer terminator passes | medium; parent reproduced acceptance after the final 0 CRLF without a terminating blank line; baseline also accepts it | Group with B1: complete bounded chunk decoding |
| B4: installation interruption leaks handler | medium; parent reproduced KeyboardInterrupt immediately after real handler installation, leaving the handler replaced | bad_spec: protect signal setup and cleanup |
| B5: cancellation interruption leaves timer armed | high; parent reproduced an active timer under the restored handler after interruption before disarm; eventual default handling can abort cleanup | Group with B4: interruption-safe lifecycle |
| B6: cleanup timeout masks caller interruption | medium; parent reproduced TimeoutError replacing an active KeyboardInterrupt during cleanup | Group with B4: preserve the original caller interruption |
| B7: owned HTTP helper joins only accept loop | low; ThreadingHTTPServer uses daemon request threads, so the new observer assertion cannot certify handler completion | patch: directly join owned request handlers and keep fixture cleanup bounded |
| B8: near-limit success fixtures mostly whitespace | low; code inspection confirms tiny logical ranges at byte boundaries omit real near-limit decode/fingerprint work | patch: add one near-limit actual row/value range |
| B9: opening cleanup has no real transport proof | false; existing trickled-header test times out inside HTTPResponse.begin before open returns and asserts peer closure; its native getresponse exception path closes the resource | reject |
| E1: interruption after handler installation | medium; duplicate independently reproduced B4 location and outcome | Group with B4 |
| E2: interruption before disarming | high; duplicate independently reproduced B5 location and outcome | Group with B4 |
| E3: malformed and truncated chunk framing completes | medium; independently reproduced B2/B3, including baseline acceptance | Group with B1 |

The verification-gap layer reported no gaps. All findings were classified before
grouping. Two bad-spec groups require re-derivation; the two direct test
corrections remain required. There is no intent gap or new user decision.

## Spec Change Log

- Review loop 1: Native HTTPResponse chunk decoding can turn a negative chunk
  size into an unbounded read and accepts invalid separators/truncated trailers.
  Signal installation and disarm also have interruption windows, and cleanup
  can replace an active caller interruption. Re-derive with explicit bounded
  chunk framing and a signal lifecycle that protects setup/teardown and preserves
  caller interruption. Avoid the reproduced unbounded read, successful truncated
  framing, leaked signal state and masked cancellation.
- KEEP: exact scheduler POST and protected opener; existing private fingerprints,
  complete range validation and strict comparisons; 4 MiB body and 20-second
  whole-operation ceilings; bounded declared/close-delimited collection; safe
  depth/resource receipts; all real HTTP boundary/trickle/SIGINT tests; active
  timer, blocked alarm and unsupported-context refusal; caller signal restoration;
  unchanged Redis logic and no shared-service/dependency changes.

## Design Notes for Review Loop 1

- Do not rely on sized HTTPResponse.read for chunked bodies. Decode chunk framing
  through bounded raw reads, validating nonnegative hexadecimal sizes and CRLF
  before any data consumption. Enforce the remaining body budget before reading
  each chunk; validate complete terminal framing/trailers. Refuse malformed
  framing with safe errors and release the response. Retain valid chunked support.
- Protect signal installation itself and cancellation/restoration from SIGINT
  and deadline delivery. Preserve pre-existing signal masks, handlers and timer
  ownership. Cleanup must not replace an active KeyboardInterrupt with a timeout.
  Test interruptions immediately after installation and before disarm, including
  the original signal/exception behavior and zero remaining owned timer.
- Test the actual native negative-chunk path with a guarded raw transport that
  fails on any unbounded read, plus real malformed separators/missing trailer
  terminators. Retain all successful bounded chunked coverage. Directly wait for
  owned request handlers, and include an actual near-limit encoded value or range.
- Preserve the existing first-pass command receipts/logs as pre-review evidence.
  Add separately named final receipts/logs bound to the re-derived source and
  update the report/ledger to cite the final results accurately.

- During loop-1 implementation, a parent worker-delivery probe reproduced the
  same signal-cleanup group despite main-thread pthread masking: a live worker
  calls signal.raise_signal(SIGINT) while a trace hook pauses the main thread at
  cleanup's `if claimed:` line. Python delivers its callback in the main thread,
  leaking the active timer, deadline handler and protected mask. Cleanup must
  defer Python-level caller-interruption callbacks throughout transitions, with
  a real worker-delivery regression, before this group is resolved.

## Final Parent Verification Before Review

- Read the re-derived changes and matrix tests, independently verified both final command receipts and all current source/decompressed log hashes, and counted 27 focused and 258 full passing test records. All four implementation tasks and six matrix rows are complete; publication remains the final authorized step.

## Final Review Triage (Round 2)

| Finding | Verdict and evidence | Route |
| --- | --- | --- |
| B2-1: interruption before cleanup critical flag | high; parent independently reproduced worker SIGINT at cleanup entry leaving the timer active and handler replaced | patch: include entry in the existing protected cleanup retry |
| B2-2: retry loops on persistent native API failure | maybe-false, medium if a supported Linux syscall fails persistently; loops are unbounded, but no actual valid-argument native failure was demonstrated | defer operational qualification; do not hide failure by abandoning owned state |
| B2-3: malformed native headers hide length | medium; native header defects can omit the declared length from get_all before framing selection | patch: reject recorded native header defects before reading |
| B2-4: duplicate JSON names discard rows | medium; parent reproduced a nonempty kvs overwritten by a later empty kvs/count; json.load on baseline has the same last-name behavior | defer pre-existing duplicate-name validation |
| B2-5: 204 with JSON chunk body is accepted | low; the unusual response still undergoes complete bounded range validation; no different stored-state fingerprint or bound bypass was shown | reject: unsupported server metadata case; adding a status guard is disproportionate to demonstrated harm |
| B2-6: plaintext JSON labelled gzip is accepted | low; no decompression occurs, actual gzip bytes fail JSON parsing and both bytes and semantics remain validated | reject: mislabeled plaintext does not demonstrate lost state or a resource-bound failure |
| B2-7: deferred SystemExit can lose to prior failure | low; retain prioritizes KeyboardInterrupt only, so a custom caller termination request can be masked | patch: preserve caller termination precedence with direct coverage |
| B2-8: cumulative framing boundaries lack tests | medium; existing cases test one oversized line rather than cumulative metadata | patch: exact cumulative boundaries |
| B2-9: custom chunk waits lack deadline cases | low; existing real deadline cases do not individually cover size/data/separator/trailer waits | patch: extend owned transport deadline cases |
| B2-10: native/ignored dispatch branches untested | low; SIG_DFL/SIG_IGN forwarding has no direct subprocess coverage | patch: owned child checks for default termination and ignored callbacks |
| E2-1: acquired response interrupted before context entry | medium; parent reproduced real SIGALRM at with-response entry, leaving the response open | patch: protect acquisition and close in a finally |
| E2-2: claimed cleanup omits acquisition window | medium; same independent reproduction and root cause as E2-1 | group with E2-1 |
| V2-1: aggregate framing budget can regress undetected | medium; verification reviewer mutated cumulative into per-line accounting and all 27 tests passed while 84,677 framing bytes were accepted | group with B2-8; patch requested regression |

These are direct corrections within existing mechanisms and tests; no public
surface, new user decision or further re-derivation is required. The two deferred
items do not reopen the three selected local findings after their fixes pass.

## Final Completion Evidence

- All round-1 re-derivation and round-2 direct corrections are implemented and
  verified. The round-2 deferred native-API qualification and pre-existing JSON
  duplicate-name issue remain explicitly open; low protocol-label concerns were
  rejected on their documented evidence.
- Parent final commands pass 33/33 focused scheduler tests (3.236 seconds) and
  264/264 complete offline tests (84.051 seconds), with zero failures/errors/skips
  and byte-identical script hashes before and after each command. Both compressed
  and decompressed logs were checked independently. All matrix rows pass.
- Final source-bound evidence is in `evidence/bounded-scheduler-observation-20261103/published-focused.json`
  and `published-fixture-suite.json`; older receipts remain historical.
- The user's explicit authorization includes committing and pushing the verified
  result to main; publication follows these final checks.
