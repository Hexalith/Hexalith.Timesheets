---
title: 'Resolve five current deferred work items'
type: 'bugfix'
created: '2026-11-02'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'aca14b73311509ad5b88ca2a2862535728dda36d'
context:
  - 'AGENTS.md'
  - 'references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The five newest deferrals concern protected command dispatch, a shared-state safety reader in test tooling that can block clients, an unrun launch test, missing packet logs, and a stale launch summary.

**Approach:** Resolve the final five open bullets in `deferred-work.md` with EventStore admission, bounded test-only observation, focused verification, and corrected records. Push the verified result to `main`.

**Decision (2026-11-02):** Use bounded refusal for oversized shared Redis; large-DB qualification remains a separate open gate. The user selected the recommended option.

## Boundaries & Constraints

**Always:** Use EventStore/Dapr abstractions for module state. Preserve fail-closed authorization, gateway-origin checks, complete privacy-safe preservation evidence, historical receipts, foreign resources, Story 3.6 in-progress, and launch **FAIL**. Validate Conventional Commits before pushing.

**Never:** Add direct Redis access to module code, a permissive test facet, or weaker SDK guards; clear shared stores, stop foreign processes, fabricate logs, rewrite SHA256SUMS, infer deployed acceptance, or update nested submodules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Protected command | Declared Timesheets payload with valid gateway authority and idempotency intent | Catalog admits the exact command and original effects are persisted once | Conflicting or forged intent fails closed before submission |
| Shared Redis | Large or changing DB0 | Observation avoids a long blocking Lua script and never reports incomplete preservation as success | Unsafe or uncertified capture produces a sanitized refusal receipt |
| Historical evidence | Attempt 8 manifest names ignored logs absent from Git | Readers can identify which hashes cannot be verified in a clean checkout | Retain the original manifest and do not invent bytes |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.Timesheets.IntegrationTests/HttpRouteCatalogFixture.cs` — commits projections with empty admissions; reuse prepare/ready/commit.
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs` and `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs` — four internal payloads, keyed processor, and gateway-origin enforcement.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/DomainRouteCatalogGuard.cs` — SDK authority contract.
- `scripts/run_isolated_magic_link_dapr.py:1315` and `scripts/test_isolated_magic_link_dapr.py` — atomic full-DB Redis Lua snapshot, comparison and offline RESP tests; preserve complete maps, expiry handling and sanitized failure receipts.
- `src/Hexalith.Timesheets/Program.cs` and `src/Hexalith.Timesheets.AppHost/DaprComponents/` — module uses EventStore/Dapr; development components select Redis. The runner inspects shared DB0/13/14.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` and `docs/launch-readiness.md` — pinned assertions and current assessment.
- `_bmad-output/implementation-artifacts/evidence/3-6/20261101-current-source-live-proof-8/README.md` and `SHA256SUMS` — append provenance correction to README; preserve original manifest bytes.

## Tasks & Acceptance

**Execution:**

- [x] `src/Hexalith.Timesheets.Server/Runtime/TimesheetsRouteCatalogDeclarations.cs`, `MagicLinkEventStoreDomainProcessor.cs`, `Runtime/ServiceCollectionExtensions.cs`, and `tests/Hexalith.Timesheets.IntegrationTests/HttpRouteCatalogFixture.cs` — add exact declarations, processor inventory, semantic adapters and committed admission; extend `MagicLinkConfirmationHttpBoundaryTests.Retry.cs` for HTTP/state outcomes.
- [x] `scripts/run_isolated_magic_link_dapr.py` and its offline tests — implement the selected complete-or-refuse Redis observation path with bounded client impact and sanitized receipts.
- [x] `docs/launch-readiness.md` — update Current Assessment for attempts 7–9 while retaining pinned language, then run the built ArchitectureTests `-class '*LaunchReadinessTests' -noColor` once.
- [x] `_bmad-output/implementation-artifacts/evidence/3-6/20261101-current-source-live-proof-8/README.md` — identify three absent ignored runner logs and failing clean-checkout manifest rows; retain historical hashes.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` and `tests/five-current-deferred-work-20261102.md` — record dispositions and limits; verify the implementation. Review, commit, and push follow this implementation gate.

**Acceptance Criteria:**

- Given a declared valid command and committed matching catalog, when protected `/process` receives it, then the HTTP route reaches the processor and persisted original effects are idempotent; undeclared, forged, or conflicting commands remain denied.
- Given large or unstable shared Redis state, when the reader cannot certify a complete comparison within its safety bound, then it refuses before fixture startup and retains a sanitized receipt without claiming preservation.
- Given the updated assessment and provenance, when the pinned launch tests and packet checks run, then the test passes, the current timeout blocker is visible, and missing historical logs are explicit without altering original receipts.
- Given unfinished deployed gates, when this batch is reviewed and pushed, then Story 3.6 stays in-progress and overall launch readiness remains **FAIL**.

## Implementation Notes

Implemented locally on 2026-11-02. The route fixture checks installed processor payloads and registered owner adapters before binding the Development admissions. The Redis reader refuses oversized DB0 before EVAL, repeats the bound inside Lua and preserves the existing complete fingerprint/expiry comparison for admitted stores. See [verification](tests/five-current-deferred-work-20261102.md). Commit/push remain for the final sequential handoff.

## Spec Change Log

## Review Triage Log

Review round 1, 2026-11-02. Each layer's finding is listed separately. Related patch groups are B1+E3 and B9+E1; B6+E5 are rejected by the direct `/process` test. All other survivors stand alone.

| ID | Finding | Verdict | Evidence and route |
|---|---|---|---|
| B1 | Compiled owner source lacks `Operations`. | medium | `CompiledRouteCatalogDeclarations.ReadOperations` throws for this attributed source. Patch the schema inventory. |
| B2 | No adapter-source factory attribute. | medium | `RunScopedRouteCatalogGateway` discovers factory attributes, while this assembly has none. Patch the existing factory surface. |
| B3 | HTTP host does not enroll Server declarations for deployed catalog. | medium | `Program.cs` registers Contracts/Projections and does not call Development enrollment. Pre-existing deployed Story 3.6 gate; defer. |
| B4 | Fixture creates targets without checking mapped endpoint and authorization. | medium | `Observe` maps route strings from declarations; actual `EndpointDataSource` is consulted only for auxiliary routes. Patch endpoint observation. |
| B5 | Fixture filters out undeclared `IIdempotencyIntentAdapter` implementations. | medium | `OfType<IDeclaredIdempotencyIntentAdapter>` discards additional installed adapters before comparison. Patch full inventory check. |
| B6 | Direct `/process` response does not persist returned effects. | false | The cited test explicitly asserts no `StoredEvents` and separately checks the returned event. Gateway/actor persistence has separate tests; the response is not claimed to write state. Reject. |
| B7 | Redis `MEMORY USAGE` default sampling can undercount nested values. | medium | Redis samples nested values by default, so a large collection may pass the 64 KiB check. Patch exact measurement. |
| B8 | `KEYS '*'` runs before the elapsed check on potentially long names. | medium | DBSIZE counts keys, not their byte length; 1024 very long keys can still make `KEYS` expensive. Patch aggregate preflight and document cooperative timing. |
| B9 | Lua can return success after the final key exceeds the time budget. | medium | The loop checks only before `DUMP`; no check precedes return. Patch final timer check. |
| B10 | Current Assessment retains stale current SDK and source revisions. | medium | Its 2026-11-02 heading precedes a paragraph still calling SDK 10.0.402 and AppHost 13.6.1 current. Patch current identity paragraph only. |
| V1 | Only one of four adapters has semantic mutation coverage. | medium | The sole `InternalCommandCatalogUsesExactPayloadsAndRejectsChangedIntent` mutation is `CommitMagicLinkUse`; patch focused coverage for other payloads. |
| V2 | No real Redis check of elapsed-time refusal. | medium | The owned Redis probe covers cardinality/value limits but not elapsed refusal. Patch a disposable delayed-read check if reliable. |
| E1 | Final key can exceed the Lua time budget. | medium | Same verified missing final timer check as B9; patch with B9. |
| E2 | Expiry deadline uses a time read before `DUMP`. | medium | `now` is sampled before `MEMORY USAGE` and `DUMP`, then added to later PTTL. Patch `TIME` adjacent to PTTL. |
| E3 | Compiled source omits `Operations`. | medium | Same verified `ReadOperations` failure as B1; patch with B1. |
| E4 | Named projections are absent from compiled source. | medium | Development enrollment rejects when actual named handlers lack declarations, but the host was not enrolled before this change. Defer with the existing deployed Story 3.6 composition gate. |
| E5 | Direct `/process` response is claimed as persisted effect. | false | The direct HTTP test explicitly expects no stored event and separately verifies its returned event; no persisted write is asserted. Reject with B6. |

## Design Notes

The SDK route catalog joins exact payload declarations, semantic idempotency, observed targets, and committed admission. A fixture-only facet bypasses owner policy. The runner's direct Redis read checks shared infrastructure: Dapr cannot inventory foreign DB0/pubsub keys and expiry. `SCAN` cannot provide an atomic snapshot under concurrent writes. A keeps the existing comparison for small stores and safely refuses the current large DB; later live proof can use a quiet, isolated environment. B may require admin snapshot commands or control of foreign writers, so it is deferred unless explicitly needed.

## Verification

**Commands:**

- Run the matching Debug/source restore and warnings-as-errors build from `AGENTS.md` — expected: zero warnings/errors.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py` — expected: Redis and preservation regressions pass.
- `tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor` — expected: pinned readiness checks pass after the edit.
- `git diff --check` — expected: no whitespace errors.
