---
title: 'Close Story 3.6 alternate configuration and literal parsing patches'
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

**Problem:** The latest Story 3.6 review leaves four accepted patches open. Alternate configuration keys and command-line arguments evade the single-export count, host configuration can enable AllowOnAnyPort undetected, the retained AppHost inline-comment mutation does not exercise end-of-line stripping, and interpolated verbatim strings can hide an overriding Port export.

**Approach:** Close those four patches in the existing configuration fitness test and retained verifier. Count InternalSurface irrespective of separator or casing, reject AllowOnAnyPort in the host source, preserve interpolated verbatim strings during comment removal, and exercise genuine end-of-line comments. Retain exact unsafe substitutions, harmless literal controls, and byte-for-byte source restoration. Verify the focused mutation matrix and individual test lanes, then reconcile this increment's evidence and tracking. Existing launch and durable-write deferrals keep their recorded ownership; this increment prepares Story 3.6 for review without declaring the whole story complete.

</frozen-after-approval>

## Implementation Notes

- Planning found no unresolved user choice, irreversible action, new API, or dependency change. The clean main worktree baseline is `30fc6d3092cbb376c500176ceeef351109e3fa7f`. The cached Epic 3 context is current; Story 3.5 and completed spec-8 provide continuity. Actual global.json selects SDK `10.0.401`; the older AGENTS managed-block SDK observation is stale.
- Change `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationCapabilityEndpointTests.cs` and `_bmad-output/implementation-artifacts/tests/3-6-configuration-mutations.py`. Reuse the existing source checks and literal-first regex; broaden the verbatim alternative to include `@$"`. The regex remains a bounded source check with known nested-interpolation limits, not a C# parser. The existing production host/AppHost wiring remains the verification input.
- Add colon-key and WithArgs Port overrides, host Configure and in-memory AllowOnAnyPort overrides, and interpolated-verbatim URL-hidden Port mutations. For the corrected inline-comment mutation, place the fake export after the internal WithHttpEndpoint call and put the actual chain semicolon on the next line, so neither a line-start comment stripper nor an early chain terminator can reject it accidentally. Add harmless interpolated-verbatim controls for both prefix orders.
- Evidence/tracking changes target only the latest four story checkboxes, its cumulative File List/change log, sprint-status.yaml, tests/3-6-test-summary.md, and docs/launch-readiness.md. Preserve historical review records and deferred ownership. Finish with story/sprint review status and this increment spec done.
- Required pre-edit runtime check: explicit-path `DOTNET_CLI_HOME=/tmp/dotnet-cli-home aspire start --no-build --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive --format Json` succeeded. The matching `aspire wait timesheets --timeout 30` exited 18; describe showed Timesheets Finished (exit 134), and `aspire logs timesheets --tail 18` reported `System.IO.IOException: Failed to bind to address http://127.0.0.1:8080: address already in use`. The explicit-path `aspire stop` succeeded. This reproduces the current workspace conflict and supplies no passing runtime journey evidence.

- The retained verifier failed against the previous built test at the new colon-key Port mutation (one selected test ran and incorrectly passed), reproducing that bypass. After the minimal fitness changes, the matching full-solution Debug restore/build passed with zero warnings/errors. The verifier passed baseline, five harmless literal variants and restored-source checks, rejected all 19 unsafe mutations, and confirmed exact production-byte restoration.
- The first full inventory run passed Contracts (90), Integration (106 pass / four existing skips), Projections (146), Server (478), and Works (76), but ArchitectureTests had one failure of 55: Launch_readiness_record_captures_package_currency_verdict_dimensions expected the raw `$(HexalithAspireHostingDaprVersion)` text. The current shared catalog uses that property and defines a separate conditional Folders-only override. Adapted only the existing GetPackageVersion helper in `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` to resolve an exact property reference to its unconditional default; this keeps the documented concrete version assertion and changes no package configuration.
- The focused ArchitectureTests rerun passed 55/55 after the catalog-reader adaptation. Final individual-lane inventory is 955 total / 951 pass / four declared skips / zero failures. Updated current evidence to the canonical baseline, the 19-mutation matrix, both interpolated-verbatim controls, actual end-of-line comment substitution and the captured runtime blocker. Closed only the latest four patch checkboxes and added this spec to the cumulative File List.

- Independent Blind Hunter review returned six findings. Narrowed the separator-agnostic occurrence count to the Timesheets InternalSurface configuration-key prefix so unrelated InternalSurface URLs remain valid; the original four override cases remain covered. Preserved the AppHost resource semicolon outside block comments and disabled directives, added both prefix orders with ordinary interpolation holes/doubled quotes/comment markers, and added an unconditional-default catalog regression covering both element and ancestor conditions. The post-review full solution Debug build again passed with zero warnings/errors.
- Post-review verification caught an extra colon in the refined regex before any commit; corrected it and rebuilt the affected IntegrationTests project with zero warnings/errors. The final retained verifier passed baseline, eight harmless literal/URL controls and restored-source checks, rejected all 21 unsafe mutations, and restored both production files exactly. A separate focused reproduction confirmed that escaped keys, concatenated keys and an escaped host property identifier still pass the lexical check; those pre-existing semantic-analysis limits are documented and grouped into one deferred item.

- Final affected-lane reruns passed ArchitectureTests 56/56 and IntegrationTests 110 total / 106 pass / four existing skips. The four other individually passing projects were unchanged by the review patches. Final inventory is 956 total / 952 pass / four skips / zero failures; the 21-mutation matrix and eight harmless controls passed. Story/sprint transition to review at presentation; overall launch posture and durable-write/infrastructure ownership remain unfinished. Shared Builds commitlint accepted the Conventional Commit message.

## Review Triage Log

- **low / patch — unrelated InternalSurface URL:** The broad marker count would reject a harmless Endpoint URL containing InternalSurface. Restricted the count to the separator-agnostic Timesheets InternalSurface configuration-key prefix and retained a harmless URL control; all literal override cases still fail.
- **medium / defer — escaped and concatenated keys:** Verified that `WithArgs("--Timesheets:Internal\u0053urface:Port=8080")` and `WithArgs("--Timesheets:Internal" + "Surface:Port=8080")` pass the selected source test. This is the existing semantic limitation of lexical source analysis, beyond the four literal-wiring patches. Documented the examples and grouped them with identifier escapes in the deferred-work ledger.
- **medium / defer — escaped property identifier:** Verified that `Configure<InternalSurfaceOptions>(o => o.AllowOnAny\u0050ort = true)` passes the selected source test. Grouped with the same raw-source-versus-C#-semantics limitation above; the current production source uses no such identifier. Semantic configuration validation needs separate ownership.
- **low / patch — ancestor conditions:** The catalog helper could select a property under a conditional PropertyGroup. Excluded conditions on both the element and its ancestors, and added one regression proving element/ancestor overrides are ignored while property defaults and literal versions resolve.
- **low / patch — disabled-export semicolon:** The retained AppHost block-comment and directive mutations removed the chain semicolon. Moved the actual semicolon outside each disabled region; the resulting omission remains valid C# and is rejected by the source test.
- **low / patch — ordinary interpolation controls:** Added both interpolated-verbatim prefix orders with a PublicPort interpolation hole, doubled quotes and embedded comment markers as harmless controls and as URL-before-override variants. All controls pass and unsafe variants fail. Nested interpolation retains its separately documented limit.
