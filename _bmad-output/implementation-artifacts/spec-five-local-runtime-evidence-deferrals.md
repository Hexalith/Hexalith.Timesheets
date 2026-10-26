---
title: 'Resolve five local runtime evidence deferrals'
type: 'bugfix'
created: '2026-10-26'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'c940b5a1784bbc80014ace494aef6255e6157080'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/docs/boundary-decision-record.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Local evidence has three transport/provenance findings, unmeasured bootstrap cost (DW-190), and an unreconciled startup failure (DW-75).

**Approach:** Qualify the existing journey, measure bootstrap cost/cancellation, and reconcile five entries. Review, verify, commit and push to main as requested.

## Boundaries & Constraints

**Always:** Preserve admission, foreign-runtime refusal, private infrastructure, secret redaction, cleanup and shared preservation. Keep launch FAIL. Reconcile DW-75 as superseded: October 3's cause remains unknown; October 4's collision and later fix are separate evidence. Qualify DW-190 locally; retain deployed latency acceptance as an explicit open obligation.

**Never:** Change dependencies, domain behavior, topology, shared data, or foreign processes; weaken integrity/cancellation. Ordinary root overrides remain supported; this fixture refuses evaluated dependency roots outside its recorded default graph before resource startup.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Failure handling |
| --- | --- | --- | --- |
| Dashboard | PID/start and loopback listener | Verify socket ownership before/after health request | Refuse missing, foreign, ambiguous or changed ownership |
| Provenance | Evaluated default graph | Identify graph and executed assets | Refuse alternate roots/changed inputs |
| HTTP | Response, redirect, poisoned proxy | Preserve response contract; use intended origin | Never forward credentials |
| Bootstrap | Delayed direct/selected reads | Measure cost and durable v7 selection | Cancellation retains pending work; retry succeeds |
| History | October 3 and later receipts | Distinguish unknown cause from later collision/fix | No retrospective causal claim |

</frozen-after-approval>

## Code Map

- `scripts/run_isolated_magic_link_dapr.py`: dashboard verification, source capture, gateway resolution and reusable direct opener; evaluated root overrides are unchecked.
- `scripts/local_aspire_magic_link_journey.py`: `call` uses the default opener; `project` retries stable identities.
- `scripts/test_isolated_magic_link_dapr.py`: real servers, AST extraction, process/source/MSBuild fixtures.
- `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkGlobalIndexEpochTests.cs`: ordinary/streamed bootstrap seams; existing observer misses selected reads.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs`: bounded direct scan plus selected legacy scan; read-only target.
- `_bmad-output/implementation-artifacts/deferred-work-archive.md`: DW-82 collision evidence; adjacent `tests/five-apphost-security-runtime-deferrals.md`: October 26 success.

## Tasks & Acceptance

**Execution:**

- [x] Runner above — verify Linux TCP/TCP6 socket ownership and process identity; validate evaluated dependency roots before startup and record graph identity.
- [x] Journey above — use the direct opener for credential/metadata calls; record projection elapsed time/attempts without changing completion policy.
- [x] Python tests above — cover matrix failures, real owned/foreign listeners, redirects/proxies, and actual MSBuild override refusal before resource startup.
- [x] `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkBootstrapLatencyTests.cs` and adjacent `BootstrapObservingReadModelStore.cs` — measure all direct/selected reads in both bootstrap paths, persisted selection, cancellation in each phase, and recovery. One new C# type per file.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/InternalSurfaceConfigurationSyntax.cs` and its tests — admit only the existing fixture gateway-wait replacement; continue rejecting listener/environment annotation mutations.
- [x] `_bmad-output/implementation-artifacts/tests/five-local-runtime-evidence-deferrals.md`, adjacent `../deferred-work.md`, and `docs/launch-readiness.md` — record receipts/dispositions; assign IDs to appended findings, reconcile DW-75, and retain DW-190's deployed remainder explicitly.

**Acceptance Criteria:**

- Given final sources, when the security journey runs, then ownership, HTTP, persisted confirm/adjust/restart, source identity, cleanup and preservation pass without skips.
- Given controlled delays, when both bootstrap paths run/cancel, then counts/concurrency/elapsed time and persisted recovery establish the local envelope.
- Given reviewed dispositions and passing checks, when publishing, then a commitlint-validated Conventional Commit reaches origin/main without force or unrelated gitlinks.

## Implementation Notes

- User approved and continued on 2026-10-26. The frozen block is unchanged; commit/push to main remains authorized after verification/review.
- Sequential implementation handoff produced the socket/HTTP/provenance changes and six bootstrap cases. All 204 Python cases passed locally and without dependency catalogs, the full Debug/source build and 67 compiled probes passed, and focused bootstrap cases passed 6/6. The observed local budget is the installed ten-second HTTP attempt default; the manual socket timeout is separate.
- Provenance audit added the imported Builds repository to source capture even though CPM imports it without a project-reference edge. Existing source-capture regression now verifies changed catalog diffs and untracked catalog files; all 204 Python cases pass after this correction.
- Initial broad IntegrationTests ran 942 cases: 934 passed, three failed, five default skips. All failures came from the unchanged AppHost's existing fixture-only gateway WaitAnnotation removal being treated as a forbidden listener mutation. The test-only checker now permits exactly that loop under the fixture condition; new mutations of annotation type, removal operation, resource predicate and fixture guard must still fail. No AppHost/domain behavior changed. Original failing receipts are retained separately from correction results.

- The initial broad Projections run passed 743 cases and failed the streamed timing case because promoted scratch cleanup can read metadata after CommitAsync returns. The successful-completion assertion now requires drained inventory reads while allowing that independent SDK cleanup; cancellation still requires every physical read to drain. The focused six-case rerun passes. Original failed receipts remain separate from corrected full reruns.
- Socket audit includes IPv4-mapped IPv6 listeners alongside native IPv4/IPv6, with real-kernel and foreign-listener regressions.

- Corrected full suites pass: 2,707 passed / five existing skips / zero failures/errors/not-run, 2,712 total. All 206 Python regressions and 67 compiled probes pass. Imported Builds changes, IPv4-mapped listeners, pinned numeric dashboard connections and the isolated root-guard deletion mutation are covered.
- Runtime attempt run-npij1wb2 refused before resources while reading the preservation baseline. A subsequent complete read-only fingerprint succeeded in 17.087 seconds. Retry run-vfdac814 verified matching shared baselines, then refused because the foreign FrontComposer AppHost started; no Timesheets infrastructure/AppHost was created and preservation matched. The approved no-foreign-process boundary requires the user to clear or authorize stopping that exact AppHost before the remaining runtime gate.

- FrontComposer and then the McpCli test AppHost exited independently; the stop question became obsolete. No foreign process was stopped. Current-source run-2jlljrdv passed one actual live test with zero failures/errors/skips/not-run in 70.611 seconds. All 315 hashes and source/graph identities match, native gateway replacement is verified, all captured processes exited, owned cleanup completed and shared preservation matched. Five selected local dispositions are complete; DW-194 retains deployed latency acceptance.

## Spec Change Log

## Review Triage Log

All three independent layers reviewed `/tmp/timesheets-five-local-runtime-review-fu6vbjo5.patch`. Findings were graded individually before grouping. No intent change is required; all corrections extend the existing private verification seams without changing dependencies or domain behavior.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| B1 — inherited globals disappear | medium | The queue replaces inherited globals with each edge's AdditionalProperties; a three-project conditional-root graph is admitted differently from MSBuild. Patch P1: inherit globals with edge overrides/removals. |
| B2 — relative root base | medium | A real MSBuild probe keeps ../../references/Hexalith.Works relative and resolves its reference from the project directory; Python resolves it from the workspace cwd instead. Patch P2: normalize at the evaluated project. |
| B3 — imported build inputs absent | medium | Graph/source capture records project roots and repository diffs, but an external imported props file can change build inputs without entering either digest. Patch P3: fingerprint actual evaluated import files without retaining their source text. |
| B4 — incomplete output closure | medium | Current output trees contain configuration and runtime-subdirectory files outside the flat DLL/manifest list; these can affect runtime behavior without changing the receipt. Patch P4: include the complete static execution output trees. |
| B5 — fixture else branch | medium | The ancestor test accepts the exact loop beneath the else branch of if(fixtureEnabled). Patch P5: require containment in its true branch and cover the mutation. |
| B6 — inaccessible additional holder | maybe-false | Ownership of the required process's descriptors is verified, but exclusivity against inaccessible additional holders is not established. An actual restored dashboard transferring a listening FD to such a holder would establish the claim; no supported lifecycle doing this is demonstrated. Defer D1 as unverified medium; describe observable ownership precisely. |
| B7 — unrelated IPv6 wildcard | medium | The reviewer reproduced refusal with IPV6_V6ONLY=1. Follow-up probes with both bind orders and v6only 0/1 routed all 1,000 IPv4 requests to the explicit owned listener; a wildcard alone cannot replace the required explicit listener. Patch P6: remove the blanket wildcard rejection, retaining before/after exact listener ownership and real coexistence regressions. |
| B8 — count/timing interval mismatch | low | PrepareAsync performs observed reads before the clock starts. Patch P7: align counters and clock boundaries; this is a direct measurement correction. |
| B9 — cleanup races measurement | medium | Promoted SDK cleanup can read metadata after commit; independently captured counters can describe different instants. Patch P8: freeze one coherent foreground measurement and distinguish subsequent cleanup. |
| B10 — composed HTTP deadline proof | medium | Tests establish the installed default and cooperative local cancellation, not effective composed HTTP options/cancellation. This underlying gateway verification was not introduced by the change and no deployed acceptance is claimed. Defer D2 for an actual composed client/endpoint proof, retaining the current local reference-budget scope. |
| B11 — selected key inventory | low | A count of 257 distinct keys does not establish the selected scope/generation. Patch P9: compare exact expected selected keys, as already done for direct keys. |
| E1 — transitive property admission | medium | Same demonstrated inherited-global defect as B1. Patch P1. |
| E2 — ordinary-mode wait removal | medium | Same demonstrated else-branch defect as B5. Patch P5. |
| E3 — raw protocol reads bypass delay/counts | medium | InMemoryReadModelStore resolves marker envelopes internally and the unwrapped batch argument reads manifests/markers outside the observer. Patch P10: add raw SDK transport measurement using existing public test seams; label logical API measurements accurately. |
| G1 — traversal deletion undetected | medium | Trust the verification-gap layer's in-memory deletion: removing child scheduling leaves four graph/source tests passing. Patch P11: verify a successful recursive graph with inherited/overridden properties, repository inclusion, and child-only root refusal. |

Patch entries P1–P11 remain separate root causes except the explicit B1/E1 and B5/E2 duplicates. D1/D2 are retained separately; no finding was silently discarded.

## Design Notes

Use the installed standard-resilience attempt default (ten seconds) as a local reference budget. Direct coordinator tests verify cooperative cancellation; effective composed HTTP options and cancellation into a blocked raw read remain D2. Measure the manual journey separately: its 45-second socket timeout and the loader timeout are separate contracts.

## Verification

- Run the matching Debug/source restore and warnings-as-errors build from AGENTS.md, then each of the six xUnit executables individually.
- `PYTHONPATH=scripts python3 -m unittest discover -s scripts -p 'test_*.py'` — sequential execution; all cases pass.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/check_magic_link_apphost_guards.py --configuration-only` — all compiled probes pass.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --security-smoke` — runtime, cleanup and preservation pass; retain existing scoped certificate configuration.
- `git diff --check`; validate the exact commit message with commitlint; fetch and inspect divergence before the authorized push.

## Final verification

All review patches P1–P11 are applied; D1/D2 remain explicit, unchanged deferrals. Transitive traversal deletion is now detected with its healthy control passing. Final solution build passes with zero warnings/errors, 208 Python cases pass locally and source-only, 67 compiled guards pass, and the nine bootstrap cases pass. The final six-executable gate is 2,711 passed / five existing skips / zero failures/errors/not-run (2,716 cases).

Final implementation run-5b2g61ov passes one live test without skips in 68.585 seconds. Nineteen evaluated project/property combinations, 278 unique imported files and 926 execution/restore fingerprints match before/after. Native replacement, complete owned cleanup, all captured process exits and shared preservation pass. Deployed acceptance/DW-194 and review D1/D2 remain open; launch remains FAIL. The frozen approved block is unchanged.
