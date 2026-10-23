---
title: 'Close five local AppHost boundary deferrals'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '40eb8590153e0585b419f5c29e956354d1725a5b'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/docs/boundary-decision-record.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open deferrals concern local AppHost listener exposure, the fixed public port, configuration-verification bypasses, misleading ingress comments, and concurrent probe admission.

**Approach:** Complete these five as the requested batch: qualify the actual endpoint model, make the local public port configurable, recognize escaped/computed sensitive configuration, correct ingress guidance, and serialize cooperating runtime probes. Verify and independently review the changes, then commit and push to main as requested.

## Boundaries & Constraints

**Always:** Preserve default ports 8080/8081/8082, the internal listener guard, opaque public failures, fail-closed configuration, private fixture ownership, and original ledger evidence. Keep each C# type in its own file. Name `src/Hexalith.Timesheets.AppHost` explicitly as the AppHost changed by this increment.

**Never:** Add resources or a second host; change publish-mode exposure, internal authentication, product policy, domain persistence, sibling checkouts/gitlinks, managed guidance, or historical receipts. Configuration/model probes launch no resources. Locks cover cooperating tools only. Keep launch FAIL and deployed acceptance unfinished.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Endpoint qualification | Actual public/internal AppHost endpoints | Internal IsExternal is false; report current public exposure accurately | Model changes fail verification |
| Local public port | Absent or valid unused override | Use 8080 or the explicit port throughout host and journey | Reject malformed, reserved/colliding listener ports; reject override in publish mode |
| Source fitness | Unicode escapes, constant concatenation, unsafe property assignment | Detect the three recorded bypasses plus ordinary spellings | Refuse unevaluable configuration constructs within the bounded checked wiring |
| Probe contention | Two cooperating workspaces | Only one runtime probe passes admission | Immediate privacy-safe refusal; lock releases after cleanup or process exit |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.AppHost/Program.cs`: endpoints, port export and validation-only branch. Aspire 13.6.3 exposes external status through `EndpointAnnotation.IsExternal`; this property does not prove cloud networking.
- `scripts/check_magic_link_apphost_guards.py`: compiled no-resource probes and runtime `IsolatedFixture` reuse.
- `scripts/run_isolated_magic_link_dapr.py`: `REQUIRED_PORTS`, preflight, execute, cleanup and preservation checks.
- `scripts/local_aspire_magic_link_journey.py`: hard-coded public origin in management/display/submit calls; preserve internal and EventStore ports.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityEndpointTests.cs`: raw-regex fitness; HTTP fixture option overrides mask production wiring.
- `scripts/test_isolated_magic_link_dapr.py`: resource-free lifecycle/guard tests and subprocess support.
- `references/Hexalith.Builds/Props/Directory.Packages.props`: read-only `Microsoft.CodeAnalysis.CSharp` 5.9.0 pin.

## Tasks & Acceptance

**Execution:**

- [x] `AppHost/Program.cs` and `scripts/check_magic_link_apphost_guards.py` — add a compiled model-only probe before Build/Run; assert actual endpoint names, ports and external flags. Emit only allowlisted nonsensitive fields. Qualify omitted external exposure through the current API, without publishing anything.
- [x] Same AppHost file, both runner/journey scripts, and their tests — add validated run-mode `Timesheets:LocalPublicPort` with default 8080. Add runner `--public-port`, propagate its value explicitly, and check the selected port. Reject nonintegers, values outside 1024–65535 and internal/gateway/sidecar collisions. Publish mode rejects the override. Preserve the existing idle-runtime prerequisite.
- [x] Endpoint fitness test file, new sibling syntax-check helper/test files, and `IntegrationTests.csproj` — use the catalog-pinned test-only Roslyn package to inspect decoded identifiers and constant string expressions. Test both recorded WithArgs mutants and escaped AllowOnAnyPort assignment, harmless comments, and healthy wiring. Fail closed on unsupported expressions in the checked configuration chain; make no arbitrary-program semantic-completeness claim.
- [x] `AppHost/Program.cs` comments and `README.md` — distinguish local anonymous public routes from deployment ingress/external exposure; document optional local port and probe admission. Change no endpoint exposure flags.
- [x] Both runner/guard scripts, new `scripts/local_runtime_admission.py`, and fixture tests — hold one canonical `/tmp/hexalith-timesheets-local-runtime.lock` OS advisory lock across admission, runtime execution, cleanup and preservation checks, independent of checkout/TMPDIR. Refuse unavailable locking or unsafe lock paths. Do not unlink a held lock. Configuration-only guards need no runtime lock. Verify cross-process contention, graceful/abrupt release and failure cleanup without launching services.
- [x] `deferred-work.md`, this spec and `tests/five-local-apphost-boundary-deferrals.md` — close exactly the five distinct summaries above only after evidence passes; preserve historical text and report all limitations. Leave the duplicated layering entries open.

**Acceptance Criteria:**

- Given the real compiled AppHost, when the model probe runs, then internal exposure remains false and no runtime resource starts.
- Given an explicit alternate public port, when local configuration/journey probes run, then every public request uses it while the internal guard remains bound to 8081.
- Given each recorded escaped/computed mutation, when configuration fitness runs, then it fails; healthy production wiring passes.
- Given concurrent cooperating workspaces, when one holds runtime admission, then the other refuses before infrastructure creation and can enter after release.
- Given passing checks and completed review, when publication runs, then a validated Conventional Commit reaches origin/main without unrelated changes.

## Implementation Notes

- Clean main at the baseline above; repository/build/test/architecture and permitted LLM/state/Git guidance read. No tracked .gitattributes. No intent gaps or migrations; cross-file footprint selects dispatch. User authorized this five-item batch and eventual push. Implementation and ledger closure await the spec checkpoint.
- User approved and continued on 2026-10-23. Re-read the unchanged spec from disk before accepting approval; frozen intent is now locked. Implementation handoff precedes root task/acceptance verification, independent review and final publication.
- Implementation returned with matching Debug/source restore/build (zero warnings/errors), 16 focused integration cases, 108 Python fixture tests and 53 compiled probes passing. Root read the entire staged baseline-relative diff and audited all four matrix rows against these executed tests. Full default suites run sequentially and remain a final publication gate.
- Added exactly five evidence-backed ledger notes with original-byte preservation and the [verification report](tests/five-local-apphost-boundary-deferrals.md). The endpoint probe returns after endpoint wiring and before unchanged duplicate app-channel token generation; source tracing confirms this pre-existing runtime limitation, separately from the successful model checks. No runtime or deployed acceptance is claimed.

- Review corrections are complete. Root inspected the patch diff: default/trimmed/actual-component collision checks, explicit private allocation exclusions, second probe origin, scrubbed child probe settings, resource/constant/binding/annotation syntax guards, explicit unproxied model fields, and executed real journey initialization. Final affected builds passed with zero warnings/errors; root focused integration passed 24/24, Python passed 112/112, and compiled probes passed 62/62.
- Independent targeted rechecks confirmed V1's original hardcoded initialization mutant is caught and E1's proxy-mode guarantee is restored. Review rows B1–B11/E1/V1 are patched; R1 remains the separately recorded pre-existing runtime follow-up. Evidence is retained in the report-linked repository directory. Full default Integration/Works completion and final publication checks remain outstanding.

- Final unfiltered receipts: Architecture 109, Contracts 92, Server 693, Projections 697, Integration 910 passed/five expected skips, Works 76. All six exited zero: 2,577 passed, five skips, zero failures/errors/not-run. The broad run began before review corrections; final focused C# 24/24 and repeated affected builds/Python 112/112/compiled probes 62/62 qualify the corrected verification paths separately. Full logs and source fingerprints are retained.
- Final commitlint, ledger-byte/frozen-intent preservation and source-hash checks pass. Root-only fetch found no divergence from origin/main at baseline. Review is complete, all five selected entries are resolved, and the one independently established pre-existing duplicate-token runtime follow-up remains open. The authorized Conventional Commit/push is the final publication operation.

## Spec Change Log

- Review retains the approved frozen intent. Apply the thirteen triaged findings as direct corrections to existing validation and verification paths, preserving public behavior/defaults and the no-resource probe boundary. No intent gap or new domain API is needed.

## Review Triage Log

| Finding | Verdict and checked evidence | Route |
| --- | --- | --- |
| B1: whitespace-normalized control endpoint | medium: the SDK resolver trims endpoints, while the new collision parser used NumberStyles.None on an untrimmed suffix. A trailing-space address can evade the public-port check. | patch normalization and regression |
| B2: default skips collision validation | medium: the new resolver returns 8080 immediately when the override is absent, so its infrastructure collision check is skipped for a supported default input. | patch resolved default through the same check |
| B3: fixture Redis endpoint source | medium: the validated component files own the selected Redis address; the optional diagnostic environment key is not that authority. A valid component endpoint can collide with the public listener without that key. | patch admission against validated component endpoint |
| B4: infrastructure allocation consumes public port | medium: available_control_port excludes only fixed defaults and Redis/placement use automatic Docker ports. An alternate public port in the ephemeral range can be selected by the fixture itself. | patch private allocation exclusions and deterministic regression |
| B5: second projection probe still uses 8080 | medium: local_aspire_projection_probe.py still posts a workload assertion and synthetic payload to fixed public port 8080. This is a direct alternate-port consumer missed by the initial change. | patch probe origin selection and refusal tests |
| B6: inherited model-only setting | medium: IsolatedFixture currently retains MODEL_ONLY and its provider aliases, so runtime startup can intentionally exit after infrastructure allocation. | patch scrub of probe-only settings and aliases |
| B7: canonical export on wrong resource | medium: the new syntax checker counts the export anywhere, while the removed regex pinned it to the Timesheets initializer. Moving it to eventStore passes the new check. | patch resource ownership assertion |
| B8: mutable internal-port declaration | medium: the checker validates use of the identifier but no longer requires const int InternalPort = 8081. Conditional configuration can pass default-only model cases. | patch declaration assertion and mutation cases |
| B9: additional options binding forms | medium: the helper counts only unqualified Configure<T>; PostConfigure<T> and qualified InternalSurfaceOptions permit a second guarded-port override while its new canonical-only assertion passes. | patch the demonstrated binding forms |
| B10: direct post-initializer annotation mutation | medium: unsupported calls are checked only in the initializer. A direct Timesheets annotation mutation with an unevaluable key is another reachable override inside the checked entry point. | patch explicit mutation refusal without claiming arbitrary-program analysis |
| B11: removed proxy assertion | medium: the old test required isProxied:false, but both new syntax and model fields omit it. A proxied internal endpoint can listen on a target port different from the exported 8081. | patch compiled proxy-mode qualification |
| E1: lost unproxied endpoint guarantee | medium: the independent edge reviewer reproduced the same internal isProxied:true mutation passing syntax checks; Aspire documents separately allocated target ports for proxies. | group with B11; one fix |
| V1: journey initialization bypassed by test | medium: the gap reviewer demonstrated that hardcoding the initialization lookup to 8080 leaves the current AST URL test passing because it injects public_origin. | patch execution of real initialization for matching/mismatched environments |
| R1: duplicate gateway app-channel resource | medium: baseline AppHost and the current SDK both generate the same gateway parameter. The model failure and source trace establish an existing full-graph construction issue; this change keeps authentication wiring untouched. | defer as a separate owning composition/runtime qualification item |

## Verification

- Matching Debug/source solution restore and warnings-as-errors build with `-p:HexalithEventStoreVersion=3.127.0`, serialized `-m:1 /nr:false` and writable CLI home.
- Run each of the six built test executables individually; use focused new configuration/endpoint tests first. Run `python3 -m unittest discover -s scripts -p 'test_isolated_magic_link_dapr.py' -v` and compiled `scripts/check_magic_link_apphost_guards.py --configuration-only`.
- Verify actual model/default/alternate-port probes and real subprocess locking tests. Inspect runtime state before any authorized runtime attempt; never stop foreign apps. An idle-runtime blocker is recorded separately and is not deployed acceptance.
- Complete workflow review, five-note/link/whitespace checks, commitlint and divergence inspection before commit/push without force.
