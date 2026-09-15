---
title: 'Correct Story 3.6 projection write concurrency and delivery guards'
type: 'bugfix'
created: '2026-09-15'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'fe82c7d13b1d0c7ae3ceaf0b6fd37792079f563b'
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6's two live projection handlers carry three defects no test can catch. They choose write concurrency from the ETag string rather than from whether the row exists, so an existing row returned without an ETag commits `CreateOnly` and is rejected permanently. Neither handler is discoverable by `GetHandlerDomainNames`, so the host registers no domain telemetry. And on the rebuild path a persisted read model whose `ProjectionFreshness` deserializes to null escapes the guarded mapping as an unhandled `NullReferenceException` instead of the declared failed result. Tests assert only each plan's canonical value, so inverting both concurrency choices leaves the suite green.

**Approach:** Fix all three in the Projections layer and add, in the same change, the assertions that falsify them — the concurrency, kind and store name of the returned plans, a store double with per-key ETags, and malformed-input deliveries.

## Boundaries & Constraints

**Always:** Handlers stay pure folds holding no state between calls, writing only through `IReadModelStore` / `ReadModelWritePolicy`. Malformed or unavailable input maps to the declared failed or retryable result with no unsafe write and no swallowed cancellation. Every new guard must be independently falsifiable: removing it alone must fail a named test.

**Never:** Do not touch `EventStoreMagicLinkConfirmationCapabilityStateLoader`, `ServiceCollectionExtensions`, or the HTTP boundary tests — separate ledger goals own the loader's same-sequence equivalence and the kernel's gateway registration. Do not move `MagicLinkTokenHashCapabilityIndexProjection` or `MagicLinkActivityTypeCatalogReadModelAddress` between projects, and leave the unused public `Rebuild` alone. Do not change persisted state keys, topology, the freshness promotion arm, or the launch-readiness verdict.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Key absent | `ReadModelEntry(null, null)` | Operation carries `CreateOnly` | N/A |
| Key present, ETag known | `ReadModelEntry(x, "7")` | Operation carries `Match("7")` | N/A |
| Key present, no ETag | `ReadModelEntry(x, null or "")` | Operation carries `LastWrite`, never `CreateOnly` | Write applies instead of being rejected forever |
| Telemetry discovery | Host scans the Projections assembly | Both handlers yield domain `timesheets` | N/A |
| Null persisted freshness | Read model with JSON-null `ProjectionFreshness` | Declared failed or retryable result, no write | No unhandled `NullReferenceException` |
| Null or blank event type | `ProjectionEventDto` with JSON-null `EventTypeName` | Treated as an unknown event | Declared result preserved |

</frozen-after-approval>

## Code Map

- `Projections/ActivityTypes/TenantActivityTypeCatalogProjectionHandler.cs` -- `:171-173` the concurrency ternary; `:135-139` and `:158-162` dereference `ProjectionFreshness` outside the `Guarded` wrapper at `:297-308` (which does already catch `NullReferenceException`); `:33`/`:80` show the named-constant slot convention. Leave `:278-284` alone — that freshness arm is correct.
- `Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs` -- `:161-163` the same ternary; `:36` and `:81` use the bare literal `"index"`.
- `Projections/ProjectionEventReader.cs` -- `:92-94` `Matches<T>` splits `EventTypeName`, declared non-nullable but null when the wire payload carries JSON `null`; only call ordering keeps that safe today, and `Deserialize` catches `JsonException`/`ArgumentException` only.
- `references/Hexalith.EventStore/.../EventStoreDomainServiceExtensions.cs:479-506` -- `GetHandlerDomainNames`: `[EventStoreDomain]` short-circuits at `:486-490`, otherwise a parameterless constructor is required at `:492-494` — both handlers fail that gate silently. Attribute takes one string; use the constant at `Server/Runtime/TimesheetsEventStoreIntegration.cs:5`.
- `references/Hexalith.EventStore/.../Projections/ReadModelBatchProtocol.cs:1014` -- `CreateOnly` succeeds only when the key is absent. `ReadModelBatchConcurrency.cs:19-38` offers `LastWrite`, `CreateOnly`, `IdempotentAbsent`, `Match`. In `ReadModelEntry<TValue>(TValue? Value, string? ETag)`, the two are independently nullable.
- `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkStateProjectionHandlerTests.cs` -- `:296`, `:341`, `:420`, `:657` are the four `FinalizeAsync` tests, all asserting only `CanonicalValue`; `ScriptedReadModelStore` at `:834` hands every key one global version as its ETag; `:470` already covers already-fresh delivery.

## Tasks & Acceptance

**Execution:**
- [x] `TenantActivityTypeCatalogProjectionHandler.cs` -- choose concurrency from row existence, falling back to `LastWrite` when an existing row has no ETag; bring both rebuild-path freshness dereferences under the guarded mapping; add the domain attribute.
- [x] `MagicLinkTokenHashCapabilityIndexProjectionHandler.cs` -- same concurrency selection and domain attribute; replace both `"index"` literals with a constant declared in this project, not in Server.
- [x] `ProjectionEventReader.cs` -- treat a null or blank `EventTypeName` as an unknown event inside `Matches<T>`, so safety no longer depends on call ordering.
- [x] `MagicLinkStateProjectionHandlerTests.cs` -- assert concurrency, kind and store name on both handlers' returned plans across all three row/ETag states; give `ScriptedReadModelStore` per-key ETags so the match case is real; add the null-freshness and null-event-type deliveries; assert both handlers resolve to the `timesheets` domain.

**Acceptance Criteria:**
- Given any guard added here is reverted alone, when the Projections test project runs, then a named test fails identifying that guard.
- Given the host scans the Projections assembly, when domain telemetry is registered, then `timesheets` is among the discovered domains.
- Given the solution builds with warnings as errors, when the six test projects run individually, then all pass with only the declared skips.

## Implementation Notes

The three-arm concurrency rule landed once, in a new internal
`Projections/ReadModelRebuildConcurrency.cs` (`For<TValue>(ReadModelEntry<TValue>)`), rather than being
re-typed in each handler: the shipped defect was the same wrong two-arm ternary duplicated across both
`FinalizeAsync` bodies, so a single owner for the rule is what stops it recurring. Both handlers now call
it; neither computes concurrency itself.

The rule tests the ETag first — `Match(etag)` for any non-empty ETag, then `CreateOnly` only for an entry
carrying neither an ETag nor a value, and `LastWrite` for an existing row a store returned without an
ETag. A non-empty ETag, not a non-null value, is what proves the row exists: `DaprReadModelStore` and
`InMemoryReadModelStore` both return an existing row as `(Deserialize<TValue>(bytes), visible.ETag)`, so
a null value under a live ETag is an existing row whose bytes did not materialize as the requested type.
Deciding existence from the value would plan `CreateOnly` there and strand the row for as long as the key
lives, which is the same failure class the two-arm ternary produced.

`Guarded` in the catalog handler became generic (`Guarded<TResult>`) so `FinalizeAsync` can wrap the
`FromCandidate` + cursor block, which returns the read model, and `AccumulateAsync` can wrap its `Merge`
call. A side effect worth knowing: a malformed catalog rebuild candidate now surfaces from those two
methods as the handler's deterministic fold failure instead of the raw `InvalidOperationException`
`FromCandidate` raises. No test or caller depended on the old type. The index handler's `Guarded` is
unchanged - that handler holds no freshness metadata.

`[EventStoreDomain("timesheets")]` is on both handler classes. The SDK's scan only instantiates handlers
that expose a parameterless constructor; both take an injected `IReadModelStore`, so the attribute is the
only route by which they become visible to domain telemetry.

The test project was granted `InternalsVisibleTo` from the Projections project. Two of the new guards are
not reachable through the public handler surface: `ProjectionEventReader.Matches<T>` is only ever called
after `Normalize` has already rejected a null event-type name, and `ProjectionFoldException` is the type
the rebuild-path freshness guard raises. Without internals access neither guard could be falsified by a
named test, which the acceptance criteria require.

`ScriptedReadModelStore` now versions each key separately and formats its ETag as `<key>#<version>`. The
single global counter it used before meant a plan that read some other key's ETag would still satisfy an
expected-ETag assertion. It also gained `SuppressETag` / `ETagWhenSuppressed`, which is how the
"row exists, store returned no ETag" state - the arm that was silently committing `CreateOnly` - is
reproduced, and the `unreadable-value` row state seeds the key with a row that materializes as null for
the requested read-model type, covering the live-ETag-without-a-value state the same way.

## Spec Change Log

## Review Triage Log

- **medium / patch** -- `ReadModelRebuildConcurrency.For` keys row existence off `current.Value`, so a row that exists but whose bytes deserialize to null gets `CreateOnly`. Verified reachable: both shipped stores return `(Deserialize<TValue>(bytes), visible.ETag)` for an existing row (`DaprReadModelStore.cs:62-67`, `InMemoryReadModelStore.cs:83-85`), so JSON-null bytes yield `(null, non-empty ETag)`. `SatisfiesConcurrency` maps a create-only write to `!current.Exists` (`ReadModelBatchProtocol.cs:1014`), so that plan conflicts on every attempt, where the pre-change ternary produced `Match(etag)` and healed the row. A regression in the exact failure class this spec exists to remove. Raised by all three layers.
- **maybe-false / defer** -- `LastWrite` in the third arm is said to risk a lost update on the cross-tenant index row (`ReplaceTenant` does carry other tenants' entries, confirmed at `MagicLinkTokenHashCapabilityIndexProjection.cs:75-88`) and to contradict the SDK remark "A missing ETag is never silently translated into last-write behavior". The concrete clobbering path is refuted: staging rejects `foreignEnvelope || !SatisfiesConcurrency` (`ReadModelBatchProtocol.cs:471-472`), which blocks envelope-wrapped rows for *every* mode including `Unconditional`; and the only way either shipped store yields an existing row with an empty ETag is that same envelope path (`ResolveVisibleAsync` lines 315-327). What would settle it: a store implementation that returns an existing, non-envelope row with an empty ETag.
- **low / reject** -- `ProjectionFoldException` is indistinguishable to the rebuild dispatcher, which catches `Exception` and returns `Indeterminate`/`HandlerFailure` either way, so the two rebuild tests pin an internal CLR type rather than a coordinator-visible outcome. Accurate, but the dispatcher behavior is unchanged by this diff and a real fix needs an SDK-visible failure contract. The live-path mapping it mirrors *is* load-bearing and is covered.
- **low / defer** -- Wrapping the catalog rebuild path in `Guarded` reclassifies a malformed-candidate `InvalidOperationException` as `ProjectionFoldException`, diverging from the sibling index handler, whose equivalent rejection is still asserted as `InvalidOperationException` (test file line 378). No caller depends on either type today.
- **medium / defer** -- The index handler's rebuild path has the same null-deserialization exposure on a different field: `ReplaceTenant` reads `pair.Value.Tenant.TenantId` and `CanonicalCandidate` reads the same chain, neither under `Guarded`. Real, but pre-existing and untouched by this change.
- **medium / defer** -- A rebuild candidate with JSON-null `items` passes through `FinalizeAsync` untouched (the guarded block only rewrites `ProjectionFreshness`), so the plan would publish a `Fresh` catalog with null `Items`. Same defect class as the null-freshness row the spec named; pre-existing.
- **low / defer** -- `ProjectionEventReader` still trusts `SerializationFormat` and `Payload` from the same wire payload, and `Equivalent` compares a JSON-null payload as an empty span equal to another empty payload. Pre-existing; outside the one field the spec named.
- **false** -- "The new `Matches<T>` guard is unreachable from production callers." True that `Normalize` throws first at every call site, but an unreachable defensive branch that returns the correct answer is not a bad outcome, and the spec asked for it precisely so safety stops depending on call ordering.
- **false** -- "A blank `EventTypeName` should throw rather than be read as an unknown event." `Deserialize` returning null means "not this event type"; callers treat unknown events as no-ops and `Normalize` already rejects malformed envelopes upstream, so no malformed history is silently accepted.
- **low / reject** -- The telemetry test mirrors only `IAsyncDomainProjectionHandler`, not `IDomainProjectionHandler`/`IDomainQueryHandler`. Verified that nothing in `src/` implements the other two, and the test does fail when either attribute is removed; broadening it guards a hypothetical future handler at the cost of added complexity.
- **low / reject** -- Nothing pins the literal value of `SlotName`, so changing the constant silently changes the declared slot. Changing a constant's value is a deliberate act, and the catalog side has the same shape already.
- **low / reject** -- Slot-constant ownership is now split (catalog's on the Server address type, index's on the handler). No named harm; the spec explicitly forbade the Server home for the index constant.
- **low / reject** -- No test drives a plan through `ReadModelBatchProtocol` to prove the third arm commits. The handler's entire contribution is the concurrency value on the returned plan; protocol enforcement is SDK-owned and tested there. The one quadrant that mattered is covered by the patch above.
- **low / reject** -- New reader and catalog tests landed in `MagicLinkStateProjectionHandlerTests.cs`, helpers dispatch on `handlerName` magic strings, and both handlers' `RebuildStoreName` is `"statestore"` so that assertion cannot distinguish them. Real weaknesses, no product defect; restructuring exceeds a direct correction.
- **low / reject** -- `SuppressETag`/`ETagWhenSuppressed` apply store-wide so the double cannot express per-key ETag absence, and `ETagFor` throws for an unseeded key. Test-double limitation with no current caller that needs either.

## Design Notes

`CreateOnly` is not a neutral default — it is accepted only while the key is absent, so choosing it for a row that exists strands that write for good. The three states each need their own arm, which a two-arm ternary on `ETag` cannot express:

```csharp
current.ETag is { Length: > 0 } etag
    ? ReadModelBatchConcurrency.Match(etag)
    : current.Value is null
        ? ReadModelBatchConcurrency.CreateOnly
        : ReadModelBatchConcurrency.LastWrite;
```

The ETag is tested first because it, not the value, is what proves the row exists. Both shipped stores return an existing row as `(Deserialize<TValue>(bytes), visible.ETag)`, so a payload that is empty, JSON-null, or of another shape yields a null value under a live ETag while the key stays present. Leading with `current.Value is null` would plan `CreateOnly` for exactly that row and strand it — the failure class this change exists to remove.

`LastWrite` is safe in the last arm because both read models are deterministic rebuilds of the same history: overwriting re-derives identical bytes, while a permanently rejected write silently strands the projection.

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` -- expected: restore succeeds on SDK `10.0.401`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` -- expected: 0 warnings, 0 errors.
- Run each project under `tests/` individually; if VSTest sockets are blocked, run the built `tests/<Project>/bin/Debug/net10.0/<Project>` executables per `README.md` -- expected: all lanes pass with only the declared skips.
- Falsification sweep: revert each new guard one at a time and confirm a named test fails.
