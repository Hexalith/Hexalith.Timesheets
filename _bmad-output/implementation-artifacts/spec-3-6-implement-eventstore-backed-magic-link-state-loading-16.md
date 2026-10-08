---
title: 'Close Story 3.6 privacy-helper coverage and current-evidence review patches'
type: 'bugfix'
created: '2026-10-09'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 'bb7fe1fb93681a0c77eb8eab76a19e2232326def'
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6 has twelve accepted review patches. The corrected privacy assertions have no coverage in a passing test lane and omit the exact fixture comment outside Timesheets log categories. Current story and release summaries still overstate historical HTTP evidence despite the SDK authorization regression.

**Approach:** Exercise the privacy helpers directly with synthetic log records in the existing IntegrationTests lane, restore the exact fixture-comment exclusion on every category, and close all twelve record and coverage patches. Preserve the settled decision to defer the live-host authorization regression to the EventStore domain-service contract: since `da172a8effe484be96c17aaef1c007e1ecb8f27c`, every external magic-link request and `/metadata/timesheets` returns 401, regressing AC1/AC3. Keep release FAIL and remaining acceptance gaps explicit.

</frozen-after-approval>

## Implementation Notes

- No unresolved intent choice or irreversible action was found. This increment changes one test source and six existing records, plus this spec; it adds no production API or dependency. Follow the twelve patches in the story's latest review, preserving original finding text and earlier decisions.
- In `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs`, add non-HTTP parameterized cases calling `AssertSensitiveDiagnosticsAbsent` and `AssertSensitiveMaterialAbsent`. Accept harmless content-root vocabulary under Microsoft.Hosting.Lifetime, Microsoft.AspNetCore.Hosting.Diagnostics and a third-party category. Reject raw token, hash, six fixture identifiers and the exact sensitive fixture comment in category, message and structured state. Reject generic vocabulary in Timesheets logs and response bodies. Add the exact fixture comment to `AssertProtectedMaterialAbsent`.
- Update `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`: current disposition, individual closures, spec inventory, spec-15 closure row and a dated correction to the prior review's HEAD-coverage claim. Retain historical finding text.
- Update `docs/launch-readiness.md` and `README.md`: date the formerly passing HTTP/guard statements as 2026-10-05 evidence, state the current 401 blocker, define blocked classification, explain all-category protected versus Timesheets-only vocabulary checks, and restore the static-scan source path. Preserve fitness-pinned release-decision text and historical zero-failure inventory.
- Update `_bmad-output/implementation-artifacts/deferred-work.md`: the spec-15 SDK entry must name the supported public-capability route opt-in accepted by EventStoreDomainServiceEndpointInventory, current live-host AC1/AC3 regression, and completion checks: all three HTTP privacy methods (four cases) passing, including a scratch-root run containing the generic vocabulary.
- Update `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md`: use the portable `$PWD` verifier path, label the four October 4 bullets as prior, and record current focused helper and six-lane results separately from historical passing HTTP evidence. Update sprint tracking to review only after independent review; acceptance and release remain incomplete.
- Verify the required Aspire baseline before editing code, then stop it. Run the README source-reference Debug restore/build with SDK 10.0.401, the new focused direct-xUnit helper cases, and all six test projects individually. Keep performance lanes skipped. Record exact existing failures without weakening authorization or changing SDK/submodule pointers. Review the finished diff through the workflow's Blind Hunter subagent and resolve findings before finalization.

- Implemented all twelve patches and closed their individual story boxes with a per-patch evidence table. The new 189-case theory passes in the normal IntegrationTests lane and checks case-insensitive protected values, including the exact fixture comment. No production source changed.
- README source-reference Debug restore/build passed with zero warnings/errors. All six direct executables ran individually: Architecture 55/56; Contracts 90/90; Server 482/482; Projections 150/150; Integration 311 total / 265 pass / 42 fail / four skips; Works 76/76. Total: 1165 / 1118 pass / 43 fail / four skips. Exact full commands are in README and focused/scratch commands in the test summary. Logs use `/tmp/timesheets-story-3-6-spec-16-*.log`. Every HTTP failure is an unexpected Unauthorized response; the sole architecture failure is the pre-existing package-verdict mismatch.
- The exact scratch-content-root HTTP privacy command ran all four cases and failed on Unauthorized before the log loops. The SDK ledger now explicitly requires a passing rerun after the platform contract fix.
- Required Aspire baseline: explicit-AppHost no-build start exited 0; wait exited 18; describe reported Timesheets Finished and security Healthy; resource logs identify missing APP_API_TOKEN and JWT Authority/SigningKey configuration. Stop exited 0. These failures do not prove a live magic-link or persisted EventStore journey.

- Independent Blind Hunter returned nine findings. Eight received simple test/record corrections: blank/whitespace-token protected-value cases, harmless vocabulary in category/state, the real default ProblemDetails safe control, a second token/hash, response-only tenant exclusions, full concrete-loader/guard completion criteria, blocked classifications with matching architecture assertions, and the README's overall FAIL verdict. This adds two focused assertion updates in `LaunchReadinessTests.cs` to keep the classification record and its existing fitness test aligned. The production surface is unchanged.
- Deferred the pre-existing substring `60` diagnostic false-positive risk to a field-aware privacy assertion follow-up; the production logger emits legitimate timestamp/correlation data that can contain those digits. This increment retains the accepted generic-vocabulary exclusions. The final helper matrix has 420 cases; final verification follows below.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| Generic `60` exclusion rejects permitted diagnostics | medium | defer | Real pre-existing helper false positive: the production boundary logger records TimestampUtc and TraceIdentifier, and a correlation string containing `60` is rejected. Ledgered a field-aware assertion follow-up without changing the accepted vocabulary policy. |
| Blank tokens only have acceptance cases | medium | patch | Added rejection cases for both empty and whitespace tokens across protected category/message/state values and response bodies; skipping all checks on blank tokens now fails the passing lane. |
| Framework vocabulary acceptance omits category/state | medium | patch | Added category and structured-state controls for all three framework/third-party categories and populated/empty/whitespace tokens. |
| Safe response control does not use production denial | medium | patch | Replaced invented copy with serialized ProblemDetails using the production default title/detail and forbidden status, so accidental rejection of the permitted `expired` copy is detected. |
| Only one request token is tested | medium | patch | Added a distinct second token and its derived hash in every logger field/category and response bodies, preventing fixed probe material from satisfying the token-argument contract. |
| Response helper omits fixture tenant identifiers | medium | patch | Added tenant-1/tenant-2 response-only exclusions and cases; legitimate tenant identifiers remain permitted in diagnostics. |
| SDK completion checks omit concrete loader and guards | medium | patch | The integration ledger now explicitly requires both complete HTTP classes, projection-delivered valid journeys, configured internal/public-port checks and all fourteen guard/reachability cases passing. |
| Historical timing/live-resolution rows still imply proven current controls | medium | patch | Both rows now use implemented / blocked / waived, name the SDK blocker and preserve their distinct residual waivers; the existing architecture assertions pin the updated classifications. |
| README introductory posture conflicts with release FAIL | low | patch | The intro now states overall release FAIL and identifies CONCERNS as the separate package-currency verdict. |

- Final post-review solution Debug build passed with zero warnings/errors. All 420 focused helper cases passed; all six final lanes ran individually: Architecture 55/56 (the same package-verdict failure), Contracts 90/90, Server 482/482, Projections 150/150, Integration 542 total / 496 pass / 42 unchanged Unauthorized failures / four existing skips, Works 76/76. Overall: 1396 total / 1349 pass / 43 fail / four skips. Final logs use `/tmp/timesheets-story-3-6-spec-16-*-final.log`. The earlier 189-case/1165-total notes describe the initial pre-review run.
- All nine independent findings have an evidence-backed disposition: eight patched, one pre-existing helper false-positive risk deferred. This spec's patch work is done; story/sprint move to review under the settled finalization precedent, while live-host AC1/AC3, live persisted topology, durable writes and release remain incomplete. No new production API or dependency was introduced.

- Final documentation/status check: ArchitectureTests reran after final records and status synchronization at 55/56, failing only the unchanged package-verdict case; no added fitness failure. `git diff --check` passed and the conventional commit message passed `references/Hexalith.Builds/commitlint.config.mjs`.
