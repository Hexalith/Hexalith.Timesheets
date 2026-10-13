---
title: 'Story 3.6: isolate storage for the live Dapr acceptance fixture'
type: 'chore'
created: '2026-10-12'
status: 'done'
route: 'dispatch'
review_loop_iteration: 2
baseline_commit: '2af8a543229f6427d4b07d1bc592add9f87aa3b2'
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-resume-checkpoint.md'
  - '_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The live journey stops at the direct-v1 index. Migration is implemented, but verified inventory, authority, quiescence and pending-delivery recovery remain absent.

**Approach:** Obtain current-tree local acceptance evidence in a completely isolated fixture environment, preserving the existing legacy environment and its migration prerequisites. Report live results separately from production certification.

**Approved decision (2026-10-12):** The user authorized the isolated fixture and continued implementation. Permit the scoped AppHost component-directory override and fixture-owned disposable Redis described below as an exception to the earlier no-AppHost-change boundary.

## Boundaries & Constraints

**Always:** Persist through EventStore. Use the existing Timesheets AppHost, resource graph, ports, component names and component scopes. Isolate actor/command state and read models together. Require explicit fixture opt-in and unique `eventstore`/`timesheets` Dapr IDs. Retain exact stored-event, selected-candidate, effective-value, one-winner race, opaque denial and restart assertions. Keep credentials and tokens out of output.

**Never:** Clear or migrate existing Redis data; manufacture inventory, authority, owner checkpoints or journal identities; register simulated production authority; seed domain events or index rows directly; add another AppHost or change ServiceDefaults/dependencies/gitlinks. Local proof cannot mark Story 3.6 or release PASS.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Behavior | Error Handling |
|---|---|---|---|
| Isolated journey | Fixture-owned fresh actor/read-model stores | Real record/issue/delivery/GET/use/replay/race/restart readbacks | Record exact failure if any assertion fails |
| Ordinary start | No component override | Existing checked-in components | Existing fail-closed behavior |
| Invalid override | Missing component, wrong scope, or fixture opt-in absent | Refuse startup before interacting with storage | Safe configuration error |
| Foreign runtime | Existing matching Dapr app ID or occupied required port | Start no conflicting AppHost | Preserve foreign resources |
| Interruption | Failed start, failed journey or cancellation | Stop only the exact fixture AppHost; retain diagnostic evidence | Dispose only artifacts created by this fixture |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.AppHost/Program.cs` — currently fixes `componentsDirectory` to `DaprComponents` in run mode. Existing SDK calls already accept actor component and isolated Timesheets component paths; reuse those parameters without changing the graph.
- `src/Hexalith.Timesheets.AppHost/DaprComponents/statestore.yaml` and `Timesheets/timesheets-readmodels.yaml` — existing stores use localhost Redis databases 13 and 14, `keyPrefix: none`, and separate `eventstore`/`timesheets` scopes. Preserve these tracked defaults.
- `scripts/local_aspire_magic_link_journey.py` — existing real EventStore journey; retain selected v7 candidate, independent evidence lineage, comment-policy checks and bounded actor retries.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkLiveDaprTests.cs` — existing opt-in journey; preserve the default skip.
- `MagicLinkTokenHashCapabilityIndexV1Migration` and `AddTimesheetsIndexMigration` — reviewed migration and default-deny/unavailable registration; require no changes.

## Tasks & Acceptance

**Execution:**

- [x] `src/Hexalith.Timesheets.AppHost/Program.cs` — retain the guarded component-directory override and default behavior. Extend fixture validation to require an isolated `pubsub` component and explicit private placement/scheduler endpoints before resources are constructed. Reuse existing `pubSubComponentPath`, `Dapr:PlacementHostAddress` and `Dapr:SchedulerHostAddress` SDK seams; preserve the existing graph/application ports. No SDK edits or new dependencies.
- [x] `scripts/run_isolated_magic_link_dapr.py` — retain the installed immutable Redis image, owned loopback endpoint, exact lifecycle and pre-command proof. Include pub/sub on that private Redis and isolate placement/scheduler through fixture-owned disposable services from the already installed immutable `dapr_placement`/`dapr_scheduler` images, using the existing configurable endpoints. Ensure scheduler advertised addresses also point to the fixture; never fall back to shared services. Refuse any running AppHost before launch, as the frozen acceptance criterion states.
- [x] `scripts/run_isolated_magic_link_dapr.py` — terminate and reap journey subprocess descendants on timeout/interruption before AppHost cleanup; use Aspire lifecycle for the AppHost itself. Prove fixture-bound sidecars/resource processes are gone before removing storage/components. An unavailable Docker inspect is unknown ownership, not confirmed absence. Convert component-removal failures into incomplete cleanup while preserving original exit status. Give each invocation a unique evidence directory, retain sanitized stdout and stderr on timeout, redact structured credential fields, and accept only exactly one executed passing live test with zero skips/errors.
- [x] `scripts/test_isolated_magic_link_dapr.py` — retain all positive/failure-injected checks. Add real IPv4 and supported IPv6 occupied-socket tests, actual component endpoint mismatches for each sidecar, subprocess-tree interruption/timeout checks, unknown Docker recovery, component-deletion failures, evidence-directory reuse, zero/skipped test rejection and JSON credential/timeout-stderr checks. Verify the isolated broker/control-plane settings and rejection of shared/missing settings.
- [x] `scripts/test_isolated_magic_link_dapr.py` or a colocated checked-in guard-test script — preserve the real compiled-AppHost refusal checks as repeatable repository tests included in fixture verification. Test missing opt-in, wrong scope and missing component, plus unsafe broker/control-plane configuration, against owned infrastructure so a regressed guard cannot activate legacy storage.
- [x] `scripts/local_aspire_magic_link_journey.py` — verify the actual actor/read-model/pubsub components and placement/scheduler targets before commands. Restore the verified `scripts/magic_link_index_selection.py` contract-order fix, token-free diagnostic categories and sorted-persistence regression; retain all original journey assertions and live-test endpoint marker.
- [x] `docs/local-magic-link-dapr-proof.md`, `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md`, `3-6-resume-checkpoint.md`, and the materialization task boxes — preserve the first three attempts as prior state-store-only evidence. Redis DB13/14 fingerprints did not establish scheduler or broker preservation; do not claim they did, and do not attempt to repair/clear earlier shared effects. Record a latest fully isolated passing run and preservation/cleanup evidence before checking the live tasks again. Story 3.6/release stay FAIL.

- [x] `scripts/run_isolated_magic_link_dapr.py` and focused tests — enumerate every manifest extension Dapr loads (`.yaml`/`.yml`, case-insensitive) in the actual sidecar resource directories. Redact complete multiline JSON/YAML credential values and URL userinfo before saving or printing command output. Refuse nullable/malformed Docker ownership and published-port responses as safe fixture failures. Preserve a completed journey's original nonzero status and captured stdout/stderr if descendant shutdown fails; retain infrastructure when shutdown is unproved. Evidence-file write failures must report incomplete cleanup without escaping and replacing the original nonzero status. Validate the scheduler range envelope/rows/count before hashing it, rejecting error or unknown responses while supporting a genuine empty range.
- [x] `scripts/test_isolated_magic_link_dapr.py` and `scripts/check_magic_link_apphost_guards.py` — add demonstrated regressions for the above paths, a real bound surviving subprocess through the production resource scanner (blocking dependency deletion), and harmless compiled shared-Redis and per-component mismatched-endpoint refusals. Keep the probe exit before resources. Update the procedural run instructions and prerequisites to the three-service/three-component runner and its read-only retained-service snapshot endpoints.

**Acceptance Criteria:**

- Given the authorized isolated fixture, when the latest-tree live journey runs, then record, issue, selected index, confirm, adjust, race and post-restart evidence pass against stored events without touching the existing stores.
- Given invalid isolation or an unrelated running AppHost, when fixture preflight runs, then no domain write or foreign stop occurs.
- Given only isolated acceptance evidence, when readiness is recorded, then legacy migration, production authority and deployed certification remain open.

## Implementation Notes

The user approved this spec and continuation on 2026-10-12. The existing untracked spec is this workflow's artifact; other working-tree changes were absent at approval. Do not stage, commit or push. Pre-edit AppHost execution with the old fixed components would activate legacy storage; preserve that environment and obtain the runtime baseline only after isolated components are supported.

Round 1 source is backed up at `/tmp/timesheets-3-6-isolation-round1-source` with its original relative paths plus `guard-checks.py`. Only this increment's owned source was restored to the recorded baseline for review-driven re-derivation; all evidence/documents are retained. Reapply the positive KEEP behavior from that backup and finish the amended tasks. Earlier live evidence is a functional state-store-only pass, not proof of untouched shared scheduler/pubsub state. No earlier control-plane baseline exists; record that limitation honestly and do not repair or delete shared state.

Invalid configuration probes must remain harmless even if the guard under test regresses. Keep private infrastructure addresses supplied for actual startup probes, and exercise missing/shared control-plane rejection without constructing resources (for example, a fixture-only validation probe that exits before resource construction). Never test a bad guard by allowing fallback to the shared scheduler/broker. This also permits a repeatable ordinary-default selection check without activating retained storage.

## Spec Change Log

### Review round 1 — complete isolation and lifecycle evidence

Blind review verified that the plan omitted shared pub/sub and scheduler state. `AddHexalithEventStore` defaults pub/sub to localhost:6379, while `AggregateActor.DrainUnpublishedEventsAsync` unregisters a reminder when its record is absent; reusing scheduler identity with fresh actor storage can affect legacy jobs. Amend only the non-frozen Code Map, Tasks and Verification to isolate existing external services and test ownership/failure paths. This avoids certifying a database-only fixture as a completely isolated environment.

KEEP: the existing resource graph and public/internal ports, two tracked state component defaults and scopes, explicit run-mode guard, immutable installed images/no pulls, exact-target Aspire routing, actual endpoint checks before commands, server/SDK defaults, v7 canonical Row/Candidate order and sorted-persistence proof, all record/issue/use/race/rebuild/restart assertions, and all historical logs. Preserve source/workspace changes outside this increment. The owned implementation backup is `/tmp/timesheets-3-6-isolation-round1-source`; use it for selective reuse while re-deriving these files. Do not weaken assertions or invent legacy recovery/certification.

Other confirmed round-1 findings amend the same tasks: subprocess and sidecar shutdown, unknown Docker recovery, original exit preservation, unique evidence, exact live-test count, JSON redaction, timeout stderr, durable AppHost refusals, real sockets and actual endpoint-mismatch tests. These have no unresolved product choice; they implement the approved isolation and evidence requirements.

Groups after individual verdicts: scheduler (Blind 1); broker (Blind 2); resource exit (Blind 3/Edge 3); journey descendants (Blind 4/Edge 1); Docker recovery (Blind 5/Edge 2); component cleanup (Blind 6/Edge 4/Edge 6); live-test execution (Blind 7); redaction (Blind 8); timeout stderr (Blind 10); unique evidence (Edge 5); foreign preflight (Edge 7); durable guards (Verification 1); real sockets (Verification 2); loaded endpoints (Verification 3). Every surviving group routes to bad_spec and the same re-derivation. Blind 9 is rejected as described above.

### Review round 2 — complete manifest and evidence failure paths

The independently reviewed source is backed up at `/tmp/timesheets-3-6-isolation-round2-source` with all original relative source paths. Only this increment's five tracked and three newly created source files were restored/removed for re-derivation; all documents, passing and failed evidence, and unrelated work remain. Reuse the positive implementation from this backup, applying the two added tasks with the smallest coherent corrections. The frozen intent is unchanged.

KEEP: private immutable Redis/pubsub/placement/scheduler services, scheduler private advertisement, unchanged graph/ports/default components/SDK/dependencies, guarded run-mode configuration and harmless validation-only probes, exact owned Aspire lifecycle, default process-group shutdown/reaping, actual components/targets before commands, unique sanitized evidence, Docker unknown-ownership retention, original failure preservation, TTL-aware read-only preservation, v7 contract-order correction and all unchanged real journey assertions. Preserve 43 passing Python checks, 12 compiled probes, the 56/56 architecture pass, and the latest 1/1 live result as historical evidence for its tested source. Do not add generalized detached-session descendant tracking: that low, uncommon extension is rejected below; the configured xUnit/Python journey uses ordinary children, and Aspire resources retain separate lifecycle ownership.

Amend the non-frozen tasks to avoid accepting unexamined Dapr manifests or invalid scheduler proof, leaking multiline/userinfo credentials, or losing captured failure evidence. Add the two preverified regression gaps and compiled endpoint mismatch probes during re-derivation. The original groups rated patch are moot in this loopback and are included in the amended verification task so their demonstrated gaps are resolved. Record the next fully isolated pass and preservation/cleanup results without erasing earlier attempts. No additional permission is needed within the approved fixture scope.

Groups after individual verdicts: manifest enumeration (Round2 Blind 1/Edge 1); multiline credentials (Blind 3); URL credentials (Blind 4); nullable ownership (Blind 5); cleanup evidence writes (Blind 6); scheduler response validation (Blind 7); journey shutdown failure evidence/status (Edge 3/Edge 4); nullable port binding (Edge 2); compiled endpoint mismatches (Blind 8); production scanner coverage (Blind 9/Verification 1); shared-Redis compiled coverage (Verification 2); procedural instructions (Blind 10). The nontrivial evidence-handling groups route to bad_spec; the remaining direct corrections/tests route to patch but are moot on re-derivation. Blind 2 is rejected as a low, uncommon generalization requiring extra process tracking.

## Review Triage Log

| Finding | Verdict | Evidence and route |
|---|---|---|
| Blind 1: shared scheduler identity | high | Existing default namespace/fixed app ID can receive legacy reminders; AggregateActor unregisters absent drain records. Database fingerprints exclude scheduler state. bad_spec: isolate scheduler/placement endpoints and verify them. |
| Blind 2: shared pub/sub | high | SDK defaults pubsub.redis to localhost:6379; Completed real publications reach that broker. bad_spec: use its existing pubSubComponentPath seam with fixture Redis. |
| Blind 3: sidecars after AppHost disappearance | medium | Cleanup checks Aspire AppHost absence only; resource quiescence is not established before deletion. bad_spec: require bound resource/sidecar exit evidence. |
| Blind 4: journey descendants | high | subprocess.run kills the direct xUnit child on interruption/timeout; a surviving Python journey can issue commands or resource operations after cleanup. bad_spec: reap the journey process group before lifecycle cleanup. |
| Blind 5: failed Docker recovery | medium | Injected daemon failure returns nonzero inspect and cleanup reports success despite unknown container status. bad_spec: distinguish confirmed absence from unavailable ownership. |
| Blind 6: component deletion replaces exit | medium | rmtree is outside cleanup error handling; PermissionError escapes finally and loses the original failure/evidence message. bad_spec: retain exit and report incomplete cleanup. |
| Blind 7: zero or skipped live tests | medium | Any zero process exit is accepted, including zero discovered/executed tests. bad_spec: require total 1, failed/errors/skipped/not-run 0. |
| Blind 8: JSON credential redaction | medium | Both ordinary token and quoted Authorization JSON fields retain SECRET in a direct reproduction. bad_spec: redact structured credential fields and test them. |
| Blind 9: Unicode cross-runtime digest oracle | low | Non-ASCII escaping was pre-existing; generated fixture identities are ASCII and the actual passing C# live digest provides an independent oracle for those inputs. Reject rare general-serializer expansion; retain sorted-order regression and live C# proof. |
| Blind 10: timeout stderr lost | medium | Timeout handling reads stdout only, dropping the captured failure stream. bad_spec: sanitize and retain both streams. |
| Edge 1: interrupted journey descendants | high | Same reproduced process-tree leak as Blind 4. bad_spec: same complete journey shutdown. |
| Edge 2: unavailable recovery inspect | medium | Same deterministic unknown-container cleanup-success path as Blind 5. bad_spec: same recovery correction. |
| Edge 3: surviving sidecars | medium | Same absence-only cleanup proof as Blind 3. bad_spec: verify bound resources are gone before deleting dependencies. |
| Edge 4: removal OSError | medium | Same original-exit loss as Blind 6. bad_spec: cleanup errors must remain cleanup outcomes. |
| Edge 5: evidence directory reuse | medium | Ordinal filenames are overwritten when the documented log directory is reused. bad_spec: unique per-invocation evidence storage. |
| Edge 6: preserve-exit claim | medium | Independent claim of the same demonstrated rmtree exception path. bad_spec: original nonzero exit survives cleanup failure. |
| Edge 7: unrelated AppHost refusal | medium | Injected unrelated AppHost with clear IDs/ports is accepted, contrary to the frozen acceptance criterion. bad_spec: refuse any pre-existing AppHost. |
| Verification 1: durable C# refusal checks | medium | Actual compiled guard checks ran, but the driver exists only in /tmp and separate Python validation cannot catch C# guard regression. bad_spec: check in and execute the behavioral refusal tests. |
| Verification 2: real port check untested | medium | All tests replace the production socket check; a no-op mutation passes. bad_spec: occupied real IPv4/IPv6 listeners must reject through production code. |
| Verification 3: actual endpoint mismatch untested | medium | Existing negatives use missing files/foreign components; accepting a sole wrong-endpoint file survives all tests. bad_spec: test each actual loaded legacy endpoint against the expected private endpoint. |
| Round2 Blind 1: ignored Dapr manifest extensions | medium | Actual verification globs only lowercase .yaml. Dapr's loader accepts .yml and case-insensitive YAML names; an additional foreign manifest escapes the count/content check. bad_spec: enumerate the same files Dapr loads. |
| Round2 Blind 2: detached descendant sessions | low | An injected child starting a new session survives the group shutdown. The fixed current journey and its Python/CLI calls do not detach journey descendants; generalized ancestry/session tracking adds complexity for an uncommon extension. Reject low finding under the workflow's everyday-use rule; retain ordinary-child process-group coverage and separate Aspire resource cleanup. |
| Round2 Blind 3: multiline credentials | medium | Direct reproduction retains SECRET from valid multiline token JSON and a YAML block. bad_spec: sanitize the complete credential value before line-based output retention. |
| Round2 Blind 4: URL userinfo credentials | medium | Direct reproduction retains SECRET in https://user:SECRET@example.test/status. patch: redact URL userinfo (or full URLs); moot on loopback and included in amended task. |
| Round2 Blind 5: nullable Docker labels | medium | A valid Config.Labels:null response raises AttributeError through recover_container and cleanup instead of an ownership refusal. patch: normalize/reject absent ownership metadata safely; moot on loopback. |
| Round2 Blind 6: cleanup evidence write | medium | Injected PermissionError from cleanup.json escapes execute after journey exit 42. bad_spec: evidence-write failure is incomplete cleanup and must preserve the original nonzero status. |
| Round2 Blind 7: invalid scheduler proof | medium | Two mocked HTTP-200 error objects without kvs are hashed as identical empty successful ranges. bad_spec: validate a real range envelope and rows/count before certifying preservation. |
| Round2 Blind 8: compiled endpoint equality gap | medium | Compiled probes never alter Redis endpoint equality; Python negatives exercise separate validation and would survive removal of the C# equality condition. patch: harmless compiled per-component mismatch probes; moot on loopback. |
| Round2 Blind 9: production scanner gap | medium | Survivor coverage injects its own lambda; no test detects a real survivor through bound_resource_processes. patch: real bound process must block cleanup with the default scanner; moot on loopback. |
| Round2 Blind 10: obsolete runner instructions | low | Procedural section describes two components and one Redis container despite three services/files and required retained snapshot endpoints. patch: direct instructions/prerequisites correction; moot on loopback. |
| Round2 Edge 1: ignored manifest extensions | medium | Same actual-file enumeration omission as Blind 1. bad_spec: same Dapr-compatible enumeration. |
| Round2 Edge 2: null port binding | low | A valid {"6379/tcp":null} Docker response raises TypeError at len(bindings); outer finally still cleans owned artifacts but prints an unexpected traceback. patch: treat null binding as unavailable and refuse safely; moot on loopback. |
| Round2 Edge 3: shutdown replaces journey exit | medium | Reproduced captured exit 42 followed by stop_process_group failure becomes FixtureError exit 1 before command logging. bad_spec: retain original completed status and output while reporting unproved shutdown. |
| Round2 Edge 4: shutdown loses timeout streams | medium | Reproduced shutdown failure escapes before TimeoutExpired reaches run_command's stream-saving handler; no command log is created. bad_spec: same shutdown-failure evidence correction. |
| Round2 Verification 1: production scanner gap | medium | Preverified mutation to return [] passes all 43 Python tests; a real bound sleeper is detected only by the original scanner. patch: default-scanner survivor test; moot on loopback. |
| Round2 Verification 2: compiled shared Redis gap | medium | Preverified removal of port == 6379 survives existing probes; exact copies using 127.0.0.1:6379 are never tested against the compiled AppHost. patch: harmless shared-Redis refusal probe; moot on loopback. |
| Round3 Blind 1: unindented YAML flow credentials | low | A synthetic unquoted YAML flow list retains its values, but configured CLI output uses JSON and scalar YAML credential forms; credential lists do not occur in this fixed journey. Reject uncommon flow-parser expansion under the everyday-use/complexity rule. |
| Round3 Blind 2: relative/uppercase token URLs | medium | Direct reproductions retain SECRET in HTTPS://example.test/confirm?t=SECRET and /confirm?t=SECRET. patch: case-insensitive absolute and relative query redaction with regressions. |
| Round3 Blind 3: cancellation during preservation | medium | Injected KeyboardInterrupt after completed exit 42 escapes the final snapshot handler. patch: handle final cancellation in both runners, preserving an existing nonzero status and reporting interruption otherwise. |
| Round3 Blind 4: cleanup completion precedes preservation | false | cleanup.json records the completed resource-cleanup stage (process/container/component removal), not the whole invocation. Late evidence failure already forces overall nonzero status and emits an incomplete-evidence diagnostic; the passing regression explicitly checks that separate stage result. No overall success is certified by that file alone. |
| Round3 Blind 5: bare fixture opt-in/shared defaults | medium | The same fixture-authorization branch and default component selection exist at baseline 2af8a543, before this increment. The new runner supplies all isolation settings and checks actual targets before commands. defer: pre-existing direct-start configuration concern; retain ordinary defaults within this authorized increment. |
| Round3 Blind 6: C# Unicode escaping | low | carried: same serializer/claim as Blind 9 from round 1; json.dumps remains unchanged and the configured fixture generates ASCII identifiers. Keep the prior rejection of uncommon general serializer expansion, with the actual C# live digest as the fixture oracle. |
| Round3 Blind 7: scheduler cluster identity | medium | Valid empty ranges from different cluster IDs currently produce identical fingerprints in a direct reproduction. patch: include normalized cluster identity and retain snapshot revision for audit; compare identity plus contents without treating leader/member changes as data changes. |
| Round3 Blind 8: recorded PID proof not repeatable | medium | runtime-isolation.json captures PID/start records, but neither checked-in runner emits the separately obtained recorded-process-exit.json or checks that captured set individually. patch: retain the existing captured identities, check their exit before dependency deletion and emit repeatable evidence, alongside the current scanner. |
| Round3 Blind 9: binary/source binding | low | The supported procedure explicitly requires a current Debug build and this run recorded successful pinned-SDK restore/build before guards/live execution. Running old binaries would skip that prerequisite; hash-based build attestation is an uncommon extra mechanism beyond this local proof. Reject low finding requiring additional build/identity infrastructure. |
| Round3 Blind 10: spec probe count | low | The historical verification bullet still says 12 while the round-3 evidence records 16. Reject a finding whose only fix edits this build's spec, as required by the review workflow; executed 16-probe evidence remains authoritative. |
| Round3 Edge 1: detached descendant session | low | carried: same location/claim and unchanged process-group implementation as Round2 Blind 2. Keep its low/complexity rejection; fixed current journey children use ordinary groups and Aspire resources have separate ownership cleanup. |
| Round3 Edge 2: uppercase HTTP URL query | medium | Same directly reproduced URL redaction defect as Round3 Blind 2. patch: shared URL correction. |
| Round3 Edge 3: invalid UTF-8 output | low | Both ordinary and journey commands raise UnicodeDecodeError for injected invalid UTF-8, losing command evidence. Existing cleanup remains in finally. patch: decode with replacement in both subprocess capture paths and retain the original result in a focused regression. |
| Round3 Edge 4: detached bound Python claim | low | carried: same escaped-session cause and unchanged group/scanner code as Round2 Blind 2. Keep its existing rejection; no detached Python descendant is created by the configured journey. |
| Round3 Verification 1: required broker DB coverage | medium | Preverified omission of DB0 passes all 55 Python tests; reader tests use empty databases and never assert all three required identities. patch: populated reader coverage for DB0/13/14 and changed broker content rejection. |
| Round3 Verification 2: scheduler difference coverage | medium | Preverified unconditional scheduler success passes all 55 tests, which compare identical scheduler fingerprints only. patch: valid changed scheduler contents must reject preservation. |

## Design Notes

The isolation uses private Redis for actor/read-model/pubsub state and private existing Dapr placement/scheduler services outside the unchanged Aspire graph. Resolve installed immutable image IDs, never pull. The SDK already exposes all needed component/control-plane path/endpoint seams; inspected built APIs confirm pubSubComponentPath. Ensure scheduler peers advertise the private address so its client cannot reconnect to the shared endpoint. Dapr scheduling is independent of Redis state; prior Redis fingerprints alone are limited evidence. Ordinary starts retain their defaults.

## Verification

- `python3 -m unittest discover -s scripts -p 'test_*magic_link*.py' -v` — all focused Python checks pass; current existing checks passed 9/9 during investigation.
- SDK `10.0.401`: source-reference Debug restore/build of `Hexalith.Timesheets.slnx`, then each of the six test projects individually — zero errors; existing opt-in skips remain expected.
- `python3 scripts/check_magic_link_apphost_guards.py` — 12 compiled configuration checks pass on owned private infrastructure; all probes exit before resources.
- `python3 scripts/run_isolated_magic_link_dapr.py` — one latest-tree isolated live test passes, sanitized logs identify the isolated mode, and cleanup leaves unrelated resources untouched.

Re-derivation checks: build the source-reference Debug solution, run all focused Python and checked-in actual AppHost refusal tests, run ArchitectureTests and the latest fully isolated journey, and verify legacy broker/state/scheduler preservation plus absence of bound processes/containers/components. The six unchanged domain/test suites already passed 1,867/1,872 in round 1; repeat a lane only if its sources or an unresolved failure make repetition necessary. Keep exact old and new commands/results distinct.

Round-1 evidence remains historical: six default suites passed 1,867 cases
with five expected skips, Python passed 25/25, and state-store-only live
attempts failed twice before passing 1/1 in 118.546 seconds. DB13/14
fingerprints matched then, but pub/sub and scheduler remained shared. No
earlier control-plane baseline exists; their preservation was not proved,
and earlier shared effects have not been repaired or cleared.

Latest amended implementation verification (2026-10-12): SDK 10.0.401
source-reference restore exited 0; Debug solution build passed with zero
warnings/errors in 7.30 seconds. Python passed 43/43, Architecture 56/56 in
1.931 seconds, and checked-in compiled guards 12/12 with harmless validation
probes before resource construction. The latest fully isolated live journey
passed 1/1 in 140.368 seconds, zero errors/failures/skips/not-run; runner
exit 0. Evidence is `/tmp/timesheets-isolation-round2-live/run-wovpme49`.
All 16 captured fixture processes, three owned containers and components
are gone. Before/after retained DB0/13/14 permanent and unexpired TTL
values/deadlines and all 51 scheduler KV entries match, with zero natural
expirations. Prior guard/snapshot failures remain recorded. Saved evidence
is sanitized; a temporary manual dashboard-query output caveat and exact
commands/results are in tests/3-6-test-summary.md. The six unchanged domain
suites were not repeated. Independent review is pending; Story 3.6 and
release remain FAIL.

Parent task and matrix audit: all seven tasks match the complete baseline-relative diff, including untracked scripts. Every frozen matrix row has passing executed coverage: isolated journey (live 1/1); ordinary selection (compiled ordinary-default probe plus unchanged fail-closed domain suites); invalid override (compiled refusal probes); foreign runtime (matching-ID, unrelated/exact-AppHost and actual IPv4/IPv6 socket checks); interruption (failed/interrupted start and journey, real descendant timeout/interruption, ownership and incomplete-cleanup checks). Final Python ran 43/43 with no skips. After the evidence edits, LaunchReadinessTests passed 13/13 in 0.115 seconds. Original Story 3.6 and Epic 3 remain in-progress in sprint-status.yaml.

Round-3 verification: use the preserved source-reference build, focused Python checks, expanded checked-in compiled guards, Architecture/LaunchReadiness and one latest fully isolated live journey with preservation/cleanup proof. Rebuild the solution if C# sources change; repeat unchanged domain suites only if their sources change or a failure warrants it. Preserve all previous logs and qualify old counts as historical.


Round-3 implementation verification (2026-10-12): all nine amended tasks
are checked for this locally verified source. SDK 10.0.401 supported
source-reference restore/build exited 0, zero warnings/errors, build 21.11s.
Focused Python 55/55 in 6.907s, Architecture 56/56 in 2.874s and harmless
compiled guards 16/16 passed. The latest fully isolated live journey passed
1/1 in 124.583s with zero errors/failures/skips/not-run; runner exit 0.
Evidence is `/tmp/timesheets-isolation-round3-live/run-liwouxcy`; guard proof
is `/tmp/timesheets-isolation-round3-guards/run-wjgil617`. All 16 recorded
PID/start identities, three owned containers and component copies are gone;
final Aspire/Dapr/owned-container inventories are empty. Retained DB0/13/14
permanent and unexpired TTL values/deadlines plus all 54 scheduler entries
match before/after, zero natural expirations. Current pre-run aggregates
differed from round 2; cross-run changes are not attributed or repaired.
The 43/43 Python, 12/12 guards, 140.368s live and 51 scheduler counts above
are historical round-2 evidence. Untouched domain suites were not repeated.
Independent review is pending; legacy/deployed gates and Story 3.6/release
remain FAIL. Exact commands/results are in the test summary and local proof.

Parent round-3 audit: all nine tasks match the full baseline-relative diff, including all three untracked source scripts. The five frozen matrix rows retain executed passing coverage: live journey 1/1, compiled default selection and refusal probes 16/16, foreign-ID/AppHost and real socket tests, and failed/interrupted start/journey plus real subprocess/resource-survivor and evidence-failure checks within Python 55/55. LaunchReadiness passed 13/13 after the evidence edits. Original Story 3.6 and Epic 3 remain in-progress; the scoped fixture is entering independent review only.

Round-3 groups after individual verdicts: URL redaction (Blind 2/Edge 2); final preservation cancellation (Blind 3); scheduler identity (Blind 7); recorded process evidence (Blind 8); output decoding (Edge 3); broker reader coverage (Verification 1); scheduler comparator coverage (Verification 2). These are focused patches to demonstrated inputs, with no new public API. Blind 5 is deferred as pre-existing. The remaining findings are rejected with the recorded individual evidence. Re-engage the same round-3 implementation agent for the patches, then verify the current tree.

Final reviewed verification: all retained round-3 patches are implemented and independently audited against the rewritten full diff. Python 63/63 (7.170s), Architecture 56/56 (1.792s), compiled guards 16/16, and latest isolated live 1/1 (135.607s), runner exit 0, passed. Every frozen matrix row and all nine tasks retain executed passing coverage. The runner emits and checks all 16 captured PID/start exits; three containers/components are removed. Retained DB0/13/14 and 57 scheduler entries/cluster identity match, zero natural expirations. Final two failed attempts are preserved with their actual port-publication and shared-state-preservation outcomes; only the fresh-baseline passing run certifies this current fixture. The pre-existing bare fixture-flag concern is appended to deferred-work.md. The scoped fixture spec is done and has no independent sprint story key; original Story 3.6/Epic 3 remain in-progress and release FAIL for the still-open legacy/deployed gates. No staging/commit/push was performed within the approved scope. Exact commands/results are in [the test summary](tests/3-6-test-summary.md).

Final presentation audit: LaunchReadiness passed 13/13 in 0.116s after evidence edits; git diff --check passed. Approved frozen intent is unchanged, no scoped tasks remain unchecked, and the Git index is untouched. Final Dapr/Aspire, fixture-container and temporary-component inventories are empty.
