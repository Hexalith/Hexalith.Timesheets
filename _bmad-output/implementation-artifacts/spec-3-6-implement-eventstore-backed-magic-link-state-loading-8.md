---
title: 'Close Story 3.6 configuration fitness and contributor guidance patches'
type: 'bugfix'
created: '2026-10-04'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The latest Story 3.6 review leaves five accepted patches open. The configuration fitness test accepts overriding, commented-out, or conditional internal-surface wiring; the performance guide still gives failing build/test commands; the packaged README contains an unusable relative ledger link; and the new test name violates the baseline convention.

**Approach:** Close those five patches as one review increment. Require exactly one internal-surface environment export and reject AllowOnAnyPort in the comment-stripped AppHost, strip inline comments, and reject conditional wiring in both AppHost and host. Rename the new test to PascalCase. Align performance instructions with the verified Debug source-reference restore/build and direct xUnit v3 executables, and express the README ledger reference as a code-formatted path plus heading. Verify the configuration checks with mutations, run the existing individual test lanes, and reconcile this increment's evidence and tracking. Keep production behavior, dependencies, topology, submodule pointers, and existing deferred ownership unchanged; Story 3.6 remains unfinished beyond this increment.

</frozen-after-approval>

## Implementation Notes

- Planning found no unresolved intent choice or irreversible action. The working tree was clean on main, with canonical baseline `c825bffec491ae82160d26906bc4004fa1b1062d`. The cached Epic 3 context is current; Story 3.5 and the completed spec-7 provide continuity. SDK selection from global.json is `10.0.401`.
- Change `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityEndpointTests.cs`, `docs/performance-evidence.md`, and `README.md`. Reuse the existing resource-chain and options-section checks. Require exactly one `Timesheets__InternalSurface__` occurrence in the stripped AppHost and no `AllowOnAnyPort`; remove the line-start restriction on comment stripping and allow whitespace after # for conditional directives in both sources.
- Mutations must reject an added AllowOnAnyPort export, a later Port export to PublicPort, an inline-comment substitute for the real export, spaced conditional AppHost wiring, and conditional host options binding. Restore exact original production source bytes after each check. No new public API or test framework is needed.
- Evidence/tracking files are the canonical Story 3.6, sprint-status.yaml, tests/3-6-test-summary.md, and docs/launch-readiness.md. Reconcile the five latest checkboxes only; historical tracking drift and durable-write/authentication/infrastructure gaps retain their existing deferred ownership. Do not mark the entire story done.
- Required pre-edit runtime check: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home aspire start --no-build --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive --format Json` succeeded. `DOTNET_CLI_HOME=/tmp/dotnet-cli-home aspire wait timesheets --timeout 30 --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive` exited 18. Describe showed Timesheets Finished (exit 134), security Healthy; resource logs reported `System.IO.IOException: Failed to bind to address http://127.0.0.1:8080: address already in use`. Explicit-path `aspire stop` succeeded. This reproduces the existing workspace conflict and does not prove live magic-link operation.

- Implemented all five latest patches and reconciled their checkboxes and cumulative File List. README and performance restore/build commands match exactly; the performance bash examples parse, and the packaged README text names an existing ledger heading. Existing performance lanes remained disabled.
- The matching source-reference restore and full solution Debug build passed with zero warnings/errors. All six individual xUnit v3 executables passed: ArchitectureTests 55; Contracts.Tests 90; IntegrationTests 110 total / 106 pass / four declared skips; Projections.Tests 146; Server.Tests 478; Works.Tests 76. Total 955 / 951 pass / zero failures.
- Eight mutations were rejected by the filtered `-method Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationCapabilityEndpointTests.AppHostExportsTheInternalListenerPortToTheHostOptionsSection` invocation: AllowOnAnyPort export, later Port override to PublicPort, inline-comment export, block-comment export, spaced AppHost conditional, ordinary AppHost conditional, spaced host conditional binding, and inline-comment host binding. Baseline and restored-source invocations each passed; both production files were restored byte-for-byte.

- Independent review identified a reproducible URL-string comment-stripping hole. Literal-first comment removal now preserves ordinary, verbatim, raw, and character literals. The export-prefix count also ignores key casing, and the bypass-name check explicitly uses Shouldly's case-insensitive comparison.
- Retained the exact mutation substitutions and runner in `tests/3-6-configuration-mutations.py` alongside this spec's evidence. `python3 _bmad-output/implementation-artifacts/tests/3-6-configuration-mutations.py` passed on the rebuilt IntegrationTests executable: baseline, three harmless URL literal variants, and restored-source checks passed; all 13 unsafe variants failed as expected; production bytes were restored exactly. The two performance report paths are now distinct.

## Review Triage Log

- **medium / patch — URL-string comment stripping:** Reproduced the reviewer's ordinary URL plus overriding Port export falsely passing the initial check. Literal-first matching preserves the URL and exposes the duplicate export. The retained matrix rejects ordinary, verbatim, and raw URL-hidden overrides and accepts harmless URL settings.
- **false — lowercase AllowOnAnyPort bypass:** The exact lowercase bypass mutation failed the initial built test with Shouldly's `should not contain (case insensitive comparison)` assertion. The claimed bypass does not occur. Separately made the prefix count case-insensitive to reject duplicate Port exports with differing key casing, and retained both casing cases in the matrix.
- **low / patch — sprint header:** The stale "five patches left" header described the prior review. Finalization updates it and last_updated, synchronizes story/sprint to review, and keeps the entire story unfinished rather than done.
- **medium / patch — mutation reproducibility:** The temporary verification had no retained executable matrix. Added the focused Python verifier with all exact substitutions, single-method execution checks, positive literal controls, and finally-based byte restoration; its invocation is recorded in the test summary.
- **medium / patch — performance report overwrite:** The revised primary commands exposed the existing shared XML path when both lanes run in sequence. Separate NFR10 and NFR11 report paths preserve both outputs.

- Final verification after review patches: the focused IntegrationTests Debug build passed with zero warnings/errors; IntegrationTests 110 total / 106 pass / four existing skips and ArchitectureTests 55/55 passed again. The other four previously passing lanes were unchanged; final inventory remains 955 total / 951 pass / zero failures. Guide bash syntax, distinct report paths, five closed latest patch checkboxes, and story/sprint review status passed consistency checks. Shared Builds commitlint accepted the Conventional Commit message. All verified review findings were patched, the exact lowercase-bypass claim was rejected, and no new review deferrals were added.
