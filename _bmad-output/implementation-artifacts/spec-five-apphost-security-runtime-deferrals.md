---
title: 'Qualify five AppHost security and runtime deferrals'
type: 'bugfix'
created: '2026-10-25'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '39e1455250206d6a59476f88c685d03f64887f81'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/docs/boundary-decision-record.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferrals lack verified AppHost security construction, safe realm credentials and current-graph runtime evidence.

**Approach:** Address DW-12, DW-14, DW-67, DW-106 and DW-166 through the existing AppHost and isolated runner. Review, verify, commit and push to main after completion.

## Boundaries & Constraints

**Always:** This increment names `Hexalith.Timesheets.AppHost`. Preserve ordinary development authentication, listener separation, generated credentials, admission, private infrastructure, ownership checks and cleanup. Reuse platform helpers. Keep launch FAIL and deployed acceptance open.

**Never:** Add topology or another host, copy a realm renderer, embed passwords, report secrets, change dependencies/submodules, stop foreign resources, or bypass preflight. Model probes and skipped tests cannot close runtime items.

**Approved exception (2026-10-25):** The user explicitly authorized one normal, non-forced stop of `/home/administrator/projects/hexalith/frontcomposer/src/Hexalith.FrontComposer.AppHost/Hexalith.FrontComposer.AppHost.csproj` to clear local admission and continue verification, review and publication. All other foreign resources remain outside that authorization.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Behavior | Failure |
| --- | --- | --- | --- |
| Security construction | Existing security branch | Generated secret references reach the intended resource | Reject missing/duplicate/non-secret bindings |
| Realm import | Generated-client environment placeholders | Replace fixed-password accounts; preserve required scope claims | Refuse unresolved credentials |
| Ordinary run | Default settings | Preserve development signing/app-channel behavior | Fail closed |
| Security smoke | Explicit validated fixture opt-in | Exercise existing Keycloak resource | Refuse outside the fixture |
| Admission | Foreign runtime/unsafe inventory | Start nothing | Retain blocked receipt |
| Current graph | Idle runtime and matching build | Ready DCP/dashboard/resources, DCP-owned gateway, persisted journey | Sanitized failure and owned cleanup |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.AppHost/Program.cs`: run mode disables Keycloak; existing construction/model probes return before startup; development credentials are already generated.
- `src/Hexalith.Timesheets.AppHost/KeycloakRealms/hexalith-realm.json`: five fixed-password sample accounts; use the security helper's generated-client environment contract without copying EventStore's private renderer.
- `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj`: SDK 13.6.1, NuGet-backed orchestration, Debug/source-only compiled gateway.
- `scripts/check_magic_link_apphost_guards.py`: compiled configuration/model/construction probes.
- `scripts/run_isolated_magic_link_dapr.py`: admission, isolated infrastructure, exact lifecycle, ownership and preservation.
- `scripts/test_isolated_magic_link_dapr.py`: runner regressions.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkLiveDaprTests.cs` and existing fixture: opt-in persisted journey.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/ScaffoldGovernanceTests.cs`: existing security source checks.

## Tasks & Acceptance

**Execution:**

- [x] AppHost program and realm above: replace fixed-password accounts with generated-client placeholders and necessary scope claims; enable the existing security branch only for explicit validated smoke fixtures. Preserve ordinary defaults.
- [x] Compiled guard script above: verify actual secret parameter/environment references without resolving values; retain construction/disposal and duplicate-credential checks for DW-12/DW-14.
- [x] Runner and Python tests above: automate security startup, readiness, current runtime identity, dashboard/DCP ownership, credential handoff and failure cleanup for DW-67/DW-166.
- [x] Existing live test/fixture above: verify generated-credential startup and persisted record/issue/index/use/replay/restart outcomes for DW-106.
- [x] `docs/launch-readiness.md`, `_bmad-output/implementation-artifacts/deferred-work.md`, this spec and adjacent `tests/five-apphost-security-runtime-deferrals.md`: map closures to current receipts; preserve historical evidence and external blockers.

**Acceptance Criteria:**

- Given valid inputs, when compiled probes run, then actual security references pass without disclosed values.
- Given an idle runtime, when smoke/journey execute, then required resources are ready, stored outcomes pass, and cleanup/preservation are verified.
- Given foreign activity, when preflight runs, then nothing starts and runtime entries remain open.
- Given five verified outcomes and independent review, when publication runs, then a commitlint-validated Conventional Commit reaches origin/main without force.

## Implementation Notes

- User approved this spec and continuation on 2026-10-25. The frozen block is locked. Commit/push to main remains authorized after verification.

## Spec Change Log

## Review Triage Log

All three independent layers completed against `/tmp/timesheets-five-final-review-_5iqdnkt.patch`. The edge-case layer returned no findings. Each finding was graded separately before grouping; no intent or specification loopback was required.

| Finding | Verdict | Evidence and disposition |
| --- | --- | --- |
| Gap G1 — source refusal test can fail for another reason | medium | The verification-gap layer demonstrated that deleting the source-identity guard leaves all 163 runner tests passing: every defect injects an extra sidecar. Trust this pre-verified gap; patch the test to isolate each defect, satisfy downstream prerequisites, and assert its specific refusal. |
| Other O1 — source-only dependency breaks CI checkout | medium | `release-tooling` checks out no submodules and runs this Python suite; the new reason-code test unconditionally reads two EventStore files. Patch the test so its privacy assertions always run and source alignment runs when the source checkout exists; verify a checkout without that source. |
| Blind B1 — stale integration executable can be qualified | medium | The runner documents a prior full build, but its security lane itself refreshes only AppHost and then executes the existing IntegrationTests binary. A changed test can therefore be omitted if the prior build is missed; trivially patch the admitted build target to the matching solution before capturing identities. |
| Blind B2 — default HTTP opener remains in readiness and journey | medium | New `readiness_observation.read_response` uses the default redirect/proxy handlers despite the isolated lane's exact-origin checks. Patch that new observation to reuse `owned_http_open`; the journey's credential-bearing `call` is unchanged from the baseline and is separately deferred below. |
| Blind B3 — malformed token shape can pass claim checks | medium | `jwt_three_parts` is recorded without being required, and dictionary membership can satisfy the audience/tenant/permission comparisons. Patch exact three-part and claim-container types with value-free diagnostics; this endpoint-response check does not claim JWT signature/expiry authorization. |
| Blind B4 — successful OAuth body has no size bound | low | `json.load` has no byte cap, but this is the owned Keycloak token endpoint for one fixed synthetic profile, with a 20-second transport timeout. Oversized or continuously streaming success is not an everyday fixture state; reject adding response/deadline guards for this negligible demonstrated-use risk. |
| Blind B5 — dashboard listener is not independently mapped to a socket inode | maybe-false | PID/start, restored binary ancestry, intended loopback URL and a direct health response are checked, but no mismatched recipient was demonstrated through the real DCP launcher. Defer as medium, unverified: settle with a real launch in which that same live dashboard fails to own the responding port, or socket-inode ownership evidence. |
| Blind B6 — untracked source absent from source identity | medium | A disposable Git reproduction confirmed that changing an untracked `src/Untracked.cs` leaves the recorded Git diff digest identical. Patch private source receipts to include hashes for untracked build inputs under the existing source/test/script trees and root build files, excluding evidence outputs; verify that changing only such a file changes identity. |
| Blind B7 — alternate MSBuild roots may differ from recorded repository roots | maybe-false | Gateway resolution explicitly evaluates the default source project and launches its exact native binary; the parent build permits root overrides, but no alternate-root qualification was exercised. Defer as medium, unverified: evaluate an actual overridden AppHost graph and compare its project/assembly inputs with recorded roots before deciding whether receipts misidentify executed code. |
| Blind B8 — concurrent byte-store test need not observe nonterminal contention | medium | The fake yields but has no deterministic contention condition or assertion that recovery ran; a serial schedule can bypass the retry branch. The retained real SDK runs already demonstrate optimistic batch conflicts, so patch the private test to force one such conflict and require eventual success with both owners preserved. |
| Blind B9 — compiled negative cases use driver-output mutations | low | These negatives test the Python driver's refusal while actual compiled positive probes inspect resource references and secret parameters. Adding mutable invalid C# graph hooks would add test-only branches for a speculative regression; reject this low-impact coverage enhancement under the review's complexity rule. |
| Blind B10 — active launch row calls October 18 the latest local journey | low | The active table's first evidence cell still says “latest” for the historical October 18 run while its next cell records October 26 qualification. Patch that direct wording and link to the current receipt, preserving deployed classification and dated historical evidence. |

Survivors have distinct causes and remain separate patch entries (G1, O1, B1, B2's new readiness observation, B3, B6, B8, B10). B5 and B7 are separate unverified deferrals. The baseline journey HTTP opener is a separate pre-existing deferral from B2; no production authorization policy changes are implied.

## Verification

Use root AGENTS.md's matching Debug/source restore/build; run focused Python/compiled probes and all six xUnit executables individually. Run live acceptance only after unchanged admission passes. Retain source identities and sanitized runtime/ownership receipts. Check ledger dispositions and whitespace, validate the commit, fetch/inspect divergence, then push main.

## External Blocker

On 2026-10-25, read-only `aspire ps --non-interactive --format Json` and `dapr list --output json` reported foreign AppHosts and Dapr applications, including `eventstore`. Runner preflight refuses either nonempty inventory. Construction work can proceed after spec approval; runtime closures require an idle environment. No foreign resources were stopped, Timesheets runtime launched, or ledger entries closed.

## Implementation and Verification — 2026-10-25

The existing AppHost now enables its platform-owned Keycloak resource only for `TIMESHEETS_LIVE_DAPR_SECURITY_SMOKE=1` with the already validated run-mode fixture. Ordinary development signing and app-channel wiring remain. The realm consumes the platform-generated client environment placeholders, and the compiled construction probe checks actual generated secret references without resolving values. The isolated runner's `--security-smoke` lane adds current-source/build receipts, NuGet DCP/dashboard ownership and readiness, generated-credential authentication, existing persisted journey checks, and cleanup even when security readiness fails before inspection.

Matching Debug/source restore and full warnings-as-errors build passed (zero warnings/errors); 67 compiled probes and 123 Python regressions passed. All six built xUnit executables passed: 2,693 passed, zero failed and five intentional default skips (2,698 cases). [Verification details and matrix coverage](tests/five-apphost-security-runtime-deferrals.md) distinguish executed local tests from the blocked runtime lane. DW-12 and DW-14 close for construction/configuration; DW-67, DW-106 and DW-166 remain open.

The actual `--security-smoke` attempt exited 1 at unchanged preflight: foreign Dapr application `parties-five-erasure-20261025` remained present. The [admission receipt](evidence/five-apphost-security-runtime-deferrals/runtime/run-fprxrqbh/admission.json) records `admitted=false` and `resources_started=false`; [cleanup](evidence/five-apphost-security-runtime-deferrals/runtime/run-fprxrqbh/cleanup.json) records no components or containers created. No foreign resource was stopped. Live resource readiness, generated-realm authentication, current NuGet DCP/dashboard qualification and persisted journey/restart outcomes remain unverified on this graph. The live task checkbox remains open, and publication is not yet eligible under the five-verified-outcomes acceptance criterion. Launch FAIL and deployed acceptance remain open.

- Parent final audit verified all six compressed XML receipts (2,693 passed, five declared skips, zero failures/errors/not-run), the matching source hashes, unchanged frozen intent, all local evidence links, and preservation of every unrelated ledger entry. Root inspected the staged implementation and corrected the early-readiness cleanup, late-admission receipt and restored-package provenance checks; their Python regressions pass. The live matrix rows remain unverified, so this spec stays in-progress and the workflow cannot advance to independent review or publication.

- User-requested retry on 2026-10-25 at 14:10:03 UTC exited 1 at unchanged admission. Read-only inventory reported a foreign Projects AppHost and seven Dapr applications, including `eventstore` and `parties-five-erasure-20261025`. [Retry admission](evidence/five-apphost-security-runtime-deferrals/runtime/run-61vx8cev/admission.json) and [cleanup](evidence/five-apphost-security-runtime-deferrals/runtime/run-61vx8cev/cleanup.json) confirm zero Timesheets resources started or created. Source hashes are unchanged; existing passing local receipts remain applicable. DW-67/DW-106/DW-166, independent review and publication remain pending.

- User-requested retry on 2026-10-25 at 17:25:58 UTC exited 1 at the AppHost admission check. Dapr inventory is now empty, but the foreign FrontComposer AppHost remained running on recheck at 17:26:57 UTC. [Retry admission](evidence/five-apphost-security-runtime-deferrals/runtime/run-km7k5zm8/admission.json) and [cleanup](evidence/five-apphost-security-runtime-deferrals/runtime/run-km7k5zm8/cleanup.json) confirm no Timesheets resources started or created. Source hashes are unchanged. The live task, independent review and publication remain pending.

- User-requested retry on 2026-10-25 first refused a transient Dapr application at 20:39:29 UTC ([receipt](evidence/five-apphost-security-runtime-deferrals/runtime/run-v9qxd608/admission.json)). Dapr inventory then cleared; the second retry at 20:40:08 UTC refused the still-running foreign FrontComposer AppHost ([admission](evidence/five-apphost-security-runtime-deferrals/runtime/run-624nvq02/admission.json), [cleanup](evidence/five-apphost-security-runtime-deferrals/runtime/run-624nvq02/cleanup.json)). Both attempts created no Timesheets resources. Source hashes remain unchanged. Permission to stop only that exact foreign AppHost using normal Aspire stop was requested; no authorization has yet been received and no foreign resource was stopped. Live acceptance, independent review and publication remain pending.

- User approved the exact FrontComposer normal-stop request. The specific exception above records that authorization; no other foreign stop or forced stop is authorized.

- After the authorized normal FrontComposer stop, runtime admission passed in `run-w768ikht`. Source capture then failed before resource creation because the dotted native executable name was incorrectly reduced to `Hexalith.dll`. The companion path now appends `.dll`; the direct capture regression reproduced the original failure and verifies file hashes, a decoy path and missing-assembly refusal. All 124 Python regressions pass (22.477 s). Failed-run cleanup and shared preservation passed. The live lane will be retried against this corrected source.

- Retry `run-4ql8an7j` reached healthy security/EventStore/Timesheets resources, then refused DCP/dashboard provenance. Cleanup removed the three private infrastructure containers and shared preservation matched. A failure-only diagnostic now records allowlisted process/package/ancestor metadata without changing acceptance or retaining environment values/raw arguments. All 125 Python regressions pass. The next live run will retain the exact ownership mismatch if it repeats.

- Actual diagnostics in `run-fdnsjij5` established 13 fixture-bound instances of the exact restored DCP binary in one daemon/controller tree, with a reparented daemon. The validator now accepts that verified tree and keeps required daemon/controller/dashboard identities, canonical package paths and direct gateway proof strict; only proven exited noncritical workers may disappear. Regressions include the observed graph and disconnected/wrong-package/stale/unbound cases. All 128 Python regressions pass (23.036 s). The next live run uses this corrected provenance model.

- Retry `run-bhv6vfe3` passed healthy resources, actual NuGet DCP/dashboard tree and dashboard HTTPS readiness, then refused generated credential/claim handoff. Cleanup and shared preservation passed. The next run has failure-only stage/shape, allowlisted OAuth status/category and fixed-label claim booleans; no token response, arbitrary description or credential values enter evidence. Realm and acceptance predicates remain unchanged. All 131 Python regressions pass (23.688 s).

- Actual credential diagnostics in `run-6ogm519u` identified port selection failure before authentication: generated credential shapes were valid. Both callers now discover the security HTTP endpoint from the installed Aspire CLI contract, match its full container ID and loopback published port to exactly one owned container TCP binding, and keep raw describe output in memory. No fixed container port or claim relaxation remains. All 134 Python regressions pass (22.761 s); the realm remains unchanged pending actual authentication evidence.

- Retry `run-hobyr7o7` matched the actual CLI resource and full owned container ID, then refused a URL predicate before port mapping or token exchange. Failure-only URL diagnostics now expose fixed scheme/hostname categories, parse/path/query/fragment/userinfo and bounded port predicates without retaining raw URL data. Guards and realm remain unchanged. All 135 Python regressions pass (22.049 s).

- Three retained Docker publication failures selected ports 51393, 50237 and 50711; each failed closed and cleanup/preservation passed. Private port allocation now samples randomized available ports in 20000–29999, retaining exclusions, loopback probing, uniqueness, actual Docker validation and exhaustion refusal. All 138 Python regressions pass (22.116 s). The next live retry retains the unchanged URL/credential diagnostics and acceptance predicates.

- Actual URL predicates in `run-l0cj2cld` confirmed the SDK upgrades the named-http security endpoint to HTTPS on localhost:8180; all other URL predicates pass. The shared discovery/handoff now supports HTTP or HTTPS and retains normal chain/hostname verification. All 139 Python regressions pass (22.743 s). The public cached Aspire certificate was verified for localhost; the next invocation scopes SSL_CERT_FILE to that certificate without modifying any trust store or disabling verification. Its hash is retained in verification.json. Realm remains unchanged.

- Retry `run-kjd67qlo` passed verified HTTPS, actual endpoint/container binding and generated credential shape, then Keycloak returned invalid_grant/profile_setup_required. The technical account now has the required complete synthetic profile (Synthetic / Timesheets Fixture, local.invalid email, verified seed metadata) without changing generated credentials, claims or authorization/profile policy. All 140 Python regressions pass (22.469 s); JSON and whitespace checks pass.

- Retry `run-32lc7tjz` used the actual gateway `/ready` health check. It exited 17 at readiness; exact owned cleanup and shared-service preservation matched. Bounded observations and sanitized gateway logs show healthy actor placement, sidecar, state store and app-channel checks, but the fresh isolated store lacks the mandatory projection delivery v2 writer marker. The domain-module helper also waits for gateway health, so startup ordering must allow the module to start while the gateway completes metadata discovery. No health check or protocol requirement is bypassed; fixture-only bootstrap will use the supported authenticated admin route. Matching build, 67 compiled probes, 178 Python regressions and 118 architecture tests pass before that correction.

- Retry `run-5eqfygiz` passed authenticated fixture-only writer-protocol activation (HTTP 200), full gateway readiness (HTTP 200; required actor/sidecar/store/channel checks Healthy, optional configstore Degraded), generated Keycloak login/scope claims, negative tenant assertion without an owner event, persisted Time Entry rebuild and the complete confirmation race/replay path. The adjustment path then refused candidate-index delivery; its generic reason was obscured by intentional credential-line redaction. The run exited 1; source identities were unchanged, exact owned cleanup completed and shared preservation matched. All three runtime deferrals remain open pending complete adjustment/restart outcomes. The next diagnostic uses only fixed support-safe outcome enums.

- Diagnostic retry `run-uhhg9y9f` reproduced the adjustment index refusal after complete confirmation proof. The SDK returned HTTP 200 with one Indeterminate outcome and fixed reason `projection_handler_failure`; legacy direct index is absent. No retry policy or acceptance expectation changed. Source identities, exact owned cleanup and shared preservation all matched. The uncovered scenario is multiple ordinary owner contributions in one active index generation, unlike maintenance-epoch insertion tests. A focused ordinary-delivery regression will reproduce the handler exception before any production correction.

- A focused regression in `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkGlobalIndexEpochTests.cs` exercises two ordinary owners in one active generation with real canonical mutations. It passes (1/1, 0.313 s), and the installed serializer accepts nonempty character-key dictionaries. Neither hypothesis explains the live indeterminate outcome. Temporary, fixture-only exception-category/source-line observation in the owning HTTP host will identify the swallowed fault; it is diagnostic only and must be removed before final acceptance. No production projection correction or policy relaxation is justified yet.

- One diagnostic attempt (`run-kn6_4hak`) stopped before resources because a retained Redis DB0 TTL deadline changed during the double baseline; the guard was unchanged. Diagnostic retry `run-vgnib6xx` then passed admission, bootstrap and confirmation, and reproduced adjustment failure with exact cleanup/preservation matched. The temporary observer emitted only `operation/coordinator/2122`; the five-member SDK batch enum proves this location represents Conflict. The observer is now removed. The SDK dispatcher retains valid Retryable/Indeterminate projection outcomes in its pending ledger with stable dispatch identity, while the fixture retries only one reason code. The fixture will honor those documented nonterminal classifications with bounded identical requests, continue accepting only durable completion, and immediately refuse terminal/identity/malformed results. A byte-store concurrency regression must prove resumed selected state; no SDK/projection policy change is made.

- Final consumer-contract correction (2026-10-26 local date): valid matching Retryable/Indeterminate results now use bounded identical dispatch requests. Only integer Completed/AlreadyCompleted outcomes count as success; terminal, identity-conflicting, mismatched or malformed results fail. The actual Dapr read-model store over a yielding per-key-ETag byte backend reproduced optimistic conflicts, then same-journal retries selected both owners; immutable batch identity conflict still preserves the original value (3/3 tests passed, 0.526 s). The ordinary-generation regression (1/1), privacy checks (12/12), full Debug/source warnings-as-errors build and 192 Python cases pass. Source capture now includes native/DLL hashes for Timesheets and IntegrationTests. Temporary host instrumentation is absent. The next live run must still prove the complete persisted journey and restart; no SDK or projection policy change is claimed.

- First full live success: `run-ob02opgh` exited 0 with one actual xUnit acceptance test passed, zero failures/errors/skips/not-run, 70.017 s. The test requires generated credentials, negative tenant denial, persisted record/issue/index, confirmation race, adjustment, replay denial, native gateway restart and fresh post-restart link success. [Source snapshots](evidence/five-apphost-security-runtime-deferrals/runtime/run-ob02opgh/source-before.json) match after execution across all 12 file hashes. [Native replacement](evidence/five-apphost-security-runtime-deferrals/runtime/run-ob02opgh/gateway-process-replacement.json) proves a new PID/start under the same NuGet DCP controller and prior identity exit. [Cleanup](evidence/five-apphost-security-runtime-deferrals/runtime/run-ob02opgh/cleanup.json) is complete; all captured processes exited and [shared preservation](evidence/five-apphost-security-runtime-deferrals/runtime/run-ob02opgh/shared-preservation.json) matched. A subsequent source-audit guard fix will refuse owned-HTTP redirects/proxies; that final source still requires its own live qualification before review/publication.

- Source-audit HTTP ownership correction is complete: the shared direct opener refuses redirects and environment proxies while preserving each caller's TLS context. Real 302/307 credential-POST/dashboard-GET tests prove no request reaches the redirect destination; proxy poisoning cannot reroute requests. All 195 Python cases pass (23.309 s), including six focused HTTP/privacy/TLS checks. No C# or dependency changed. Final exact-source live qualification follows.

- The hardened browser-root dashboard probe in `run-jnj3vsqf` failed after authenticated bootstrap/readiness; owned cleanup and shared preservation matched. The installed dashboard 13.6.1 package identifies source commit `3751e615ad660621f2a7aa4c390ef48e08a0eed8`, whose endpoint mapping explicitly allows anonymous `/health`. The probe now uses that direct route on its strict validated loopback origin, records fixed path `/health`, and retains redirect/proxy refusal plus the existing TLS conventions. A real local-server test distinguishes browser-root login redirect from direct health success. All 196 Python cases pass (23.276 s); five focused checks pass. Final source qualification follows.

- Hardened retry `run-lucaupok` passed direct dashboard `/health`, generated Keycloak authentication, full initial readiness and both persisted confirm/adjust paths. It then hit the pre-existing 60-second healthy-wait limit after native gateway restart; source snapshots, native replacement, owned cleanup and shared preservation matched. Gateway startup performs awaited metadata discovery, and the fixture already allows 180 seconds for initial HTTP startup. The restart now uses the same bounded 180-second healthy-wait allowance; the eight-minute journey deadline and full-health requirement are unchanged. Python syntax and whitespace checks pass.

- Final current-source live run `run-fr5m7reh` exited 0: one actual test passed, zero failures/errors/skips/not-run, 64.649 s. Direct dashboard `/health`, generated credential handoff, all persisted journey and restart assertions passed. Source snapshots including all 12 hashes matched; exact native replacement, full owned process/container/component cleanup and shared-service preservation passed. All five selected ledger entries now have local closure evidence. Current readiness documentation and synchronized repository entry points distinguish this completed local qualification from unfinished deployed acceptance. Independent review and authorized publication remain next.

- Final verification reconciliation: the complete Python command is `PYTHONPATH=scripts python3 -m unittest discover -s scripts -p 'test_*.py'` and passes 196 cases (23.243 s): 163 isolated-runner tests, 30 index-selection tests and three evidence-lineage tests. The separate runner-only command passes 163 cases. The final full Debug/source build passes with zero warnings/errors and unchanged qualified source/build hashes; all 16 reconciled readiness-document checks pass. All implementation tasks and matrix rows have executed passing evidence; independent review and the preauthorized commit/push are the remaining workflow steps.

- Independent review completed across all three layers. Twelve findings were individually triaged; eight small patch entries passed verification, two low-impact enhancements were rejected, and three follow-ups were recorded (two unverified provenance claims and one pre-existing HTTP helper). The source-guard deletion mutation is now detected and the no-submodule checkout passes all 166 runner tests. Final complete Python regressions pass 199/199 (23.353 s); all 67 compiled probes, three deterministic byte-store cases, 16 readiness-document checks and the matching solution build pass with zero warnings/errors. The initial parallel independent Python attempts contended on the shared admission lock; sequential checks pass, with the failed attempts preserved separately.

- Post-review current-source live qualification `run-y2ktjsim` exited 0: one passed, zero failures/errors/skips/not-run, 61.718 s. Source snapshots (including untracked build inputs and all 12 file hashes) match. Generated credentials, stored confirm/adjust/replay, native restart, exact cleanup and shared preservation passed. All five selected deferrals are locally complete; deployed acceptance and launch FAIL remain open. Conventional Commit validation passed; the already authorized commit and push to main follow this evidence.
