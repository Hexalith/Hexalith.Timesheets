---
title: 'Close five deferred HTTP privacy verification gaps'
type: 'chore'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open ledger entries weaken the magic-link HTTP no-disclosure evidence: the logger omits scopes and exception text, malformed and throwing requests lack diagnostic privacy checks, a numeric substring check rejects safe correlation/timestamp data, response headers are not scanned, and unexpected statuses abort before privacy checks.

**Approach:** Complete these five related verification items together. Capture external log scopes and full exception text, retain the existing category-specific vocabulary policy, distinguish permitted diagnostic fields and protocol headers from duration evidence, scan response bodies and all headers before status/shape assertions, and exercise malformed JSON and throwing dependencies through the composed HTTP host. Add positive/negative sentinel controls proving each assertion detects actual protected material without rejecting safe timestamps, correlation identifiers or numeric protocol metadata. Preserve runtime behavior, dependency pins, domain state, agent guidance and deployed acceptance classifications. Verify the focused HTTP class and full integration executable in Debug/source-reference mode, annotate exactly five ledger resolutions, independently review, then commit and push to main as requested.

</frozen-after-approval>

## Implementation Notes

- Baseline: clean `main` at `eff123eac31c4708689682b222ae6133b6d5bea0`. The five canonical entries are the scope/exception and malformed/throwing diagnostic entries from spec-14, numeric assertion entry from spec-16, and header/status-order entries from spec-17.
- Route facts: no unresolved intent gaps or domain data operations; private test helpers, regression coverage, this spec and five ledger annotations form a small reversible change. The user explicitly authorized commit and push.
- Code map: `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` owns `CaptureFailureAsync`, `AssertSensitiveDiagnosticsAbsent`, `AssertSensitiveMaterialAbsent`, the malformed-body test and `CapturingLoggerProvider`/`CapturingLogger`/`LogRecord`. Add controls in a partial file for that same test class. Reuse the current factory's service replacement and real middleware; preserve its configured workload security and fail-closed defaults.
- Guidance reviewed: root agent instructions, Hexalith LLM/state/Git baselines, editor/build/package/test configuration, architecture privacy rules and boundary record. Works and Builds both currently select EventStore `3.126.0`, so no version override is needed.
- Required Aspire baseline: exact-target `aspire start --non-interactive --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --format Json` succeeded; `aspire describe` reported EventStore/Timesheets and sidecars Running/Healthy. The exact AppHost was stopped successfully before test builds. This is a startup observation, not deployed persistence/authority acceptance.
- Implemented external scope/exception capture in the existing logger fixture and added same-class partial-file controls for scalar and structured scopes, exception chains, asynchronous isolation, duration/metadata discrimination, protected response/content header names and values, and privacy-first 401/500 assertions. The malformed-body probe now inspects actual exception diagnostics; a substituted throwing loader reaches all four real exception middleware paths with no submission.
- The focused Debug/source-reference test-project restore and warnings-as-errors build passed. The focused privacy/control selection passed 510/510, zero skips/failures, in 1.380 s. The first control run exposed Shouldly's async assertion helper rethrowing the intentionally captured assertion exception; these controls now use xUnit `Assert.ThrowsAsync`, with no change to the privacy assertions under test.
- The matching root `dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` and `dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` passed with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`; the build reported zero warnings/errors. All six executable suites are being checked individually.
- Independent review identified five concrete edge cases; all were patched with regressions. Raw protected response scans now precede numeric JSON preprocessing; duplicate/malformed JSON falls back safely. Duration detection retains unit-bearing forms and checks JSON numeric values, including exponents. Diagnostic numeric exemptions inspect each complete field and the anchored denial message rather than deleting metadata-like text inside ordinary values. Header checks include business-state sentinels while permitting numeric protocol fields. Framework error-body checks retain response-specific tenant exclusions.
- Post-review focused rebuild passed with zero warnings/errors; the expanded privacy selection passed **526/526**, zero skips/failures, in 1.561 s. The independent reviewer rechecked every finding as resolved, with no concrete remaining regression or new deferral. The pre-edit HTTP baseline and pre-review integration run were stopped as superseded, and one final full integration run was started from the reviewed build; their incomplete results are not acceptance evidence.
- Final verification: all six Debug/source-reference xUnit executables passed individually with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home` and `-longRunning 20`. ArchitectureTests **103/103**, Contracts.Tests **92/92**, Server.Tests **558/558**, Projections.Tests **437/437**, Works.Tests **76/76**, IntegrationTests **820 passed / 5 intentional skips**. Aggregate: **2,091 tests, 2,086 passed, 5 intentional skips, zero failures/errors/not-run**. The final integration run used the post-review build and completed in 531.219 s; projection capacity coverage completed in 568.723 s. Opt-in performance and live-Dapr lanes remain outside this default evidence.
- The configured one-shot Blind Hunter review ran independently; no configured review layer was skipped. All five findings were patched and rechecked. Exactly five canonical ledger entries were annotated; no additional work was deferred. The runtime source, submodule pointers and launch verdict remain unchanged. The Conventional Commit message passed the repository's commitlint command before staging.

## Review Triage Log

- BH1 — **medium / patch**: duplicate JSON properties could throw during numeric preprocessing before raw protected-string and header scans. Raw response checks now run first; malformed/duplicate JSON has a raw numeric fallback, with both body and header sentinel controls.
- BH2 — **medium / patch**: whole-word numeric matching missed unit-bearing `60min`/`PT60M`; JSON exponent representation `6e1` also expressed the known duration sentinel. Retain digit-boundary checks outside allowed metadata and inspect parsed JSON numbers, with independent controls for all reported forms.
- BH3 — **medium / patch**: global metadata replacement could erase `60` inside an ordinary field such as `Evidence=CorrelationId=60`. Inspect each complete field's actual key and validated value, use anchored denial-message positions, and never exempt exception text; mixed and metadata-like controls now reject those disclosures.
- BH4 — **low / patch**: equivalent custom headers could disclose `60`, `Draft` or `Delivery` without a protected identifier. Add direct business-header controls and assertions while explicitly retaining legitimate protocol lengths and correlation headers.
- BH5 — **low / patch**: malformed/throwing framework response bodies used a diagnostic identifier subset that omits tenants. Both paths now use shared response identifier exclusions, with lower/upper-case tenant controls and retained framework type vocabulary.
