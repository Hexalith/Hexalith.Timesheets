# Works Planned-Effort Reporting Adapter — Behavior Note (Story 4.8)

Status: implemented 2026-06-22. Makes the planned-vs-actual seam concrete behind a fail-closed default,
without claiming launch-time planned-effort integration that the host has not composed.

## Chosen integration path

**Consume the Hexalith.Works `get-work-item` consumer query** (domain `work`, query type `get-work-item`)
over the EventStore domain-service query channel, reusing the **existing** `Hexalith.Timesheets.Works`
adapter project and its `IWorksQueryChannel` light-port (built by Story 1.10 explicitly "reusable by Story
4.8's planned-effort provider"). No parallel Works client, no new project, and no new freshness type were
created. The provider reads the consumer view's planned/estimated effort — `Estimated`/`Done`/`Remaining`/
`Unit` — which is the legitimate payload of this read-reporting story (unlike the Story 1.10 authority gate,
which must not read effort).

## Where the code lives (no new project, no new references)

All new production code is in the existing `src/Hexalith.Timesheets.Works` project (namespace
`Hexalith.Timesheets.Works`), which already references `.Server` (the seam + `TimesheetsRequestContext`),
`Hexalith.Works.Contracts` (`WorkItemView`, `Unit`), and `Hexalith.EventStore.Contracts` (`QueryEnvelope`,
`QueryResult`) — **no new project references**.

- `WorksQueryWorkPlannedEffortProvider` — the concrete `IWorkPlannedEffortProvider`, cloned from
  `WorksQueryWorkReferenceValidator` (same `IWorksQueryChannel` injection, hardcoded
  `WorkDomainName = "work"` / `GetWorkItemQueryType = "get-work-item"`, `BuildEnvelope`, the
  `result.Success` / undefined-payload / `Deserialize<WorkItemView>` flow, the
  `catch (OperationCanceledException) throw;` + `CA1031` fail-closed catch, and the defensive cross-tenant
  re-check).
- `WorksPlannedEffortReportingServiceCollectionExtensions.AddTimesheetsWorksPlannedEffortReporting()`.

The seam (`IWorkPlannedEffortProvider`) and the fail-closed kernel default
(`UnavailableWorkPlannedEffortProvider`, from Story 4.3) stay in
`src/Hexalith.Timesheets.Server/OperationalReports/` — **unchanged**. Because all new code stays in `.Works`,
no `DependencyDirectionTests` change was needed: contracts stay infrastructure-free and `.Server` takes no
Works reference.

### Why `IWorksQueryChannel` instead of injecting `IDomainQueryInvoker` directly

`Hexalith.EventStore.Queries.IDomainQueryInvoker` lives in the EventStore web host; referencing it from this
light adapter library drags in the full ASP.NET/Dapr/Redis/JWT closure and produces unresolvable transitive
assembly-version conflicts (MSB3277) under `-warnaserror`. The provider therefore depends on the narrow port
`IWorksQueryChannel` (signature mirrors `IDomainQueryInvoker` exactly:
`Task<QueryResult> InvokeAsync(QueryEnvelope, CancellationToken)`), and the composed host supplies the
implementation with a small `DomainQueryInvokerWorksQueryChannel` pass-through defined in the host. This is the
same binding Story 1.10's validator uses; both adapters share the one port.

## Registration (fail-closed by default)

`AddTimesheetsServerKernel` keeps `TryAddSingleton<IWorkPlannedEffortProvider, UnavailableWorkPlannedEffortProvider>()`
(unchanged). `AddTimesheetsWorksPlannedEffortReporting()` uses `services.Replace(...)` so the concrete provider
wins regardless of call order relative to `AddTimesheetsServerKernel`. When the extension is **not** invoked,
the kernel default keeps reporting Work planned effort as `Unavailable`, so no report implies a planned-effort
integration that does not exist.

## State mapping (fail-closed)

Tenant authority is taken from `TimesheetsRequestContext.Tenant` (request authority), never from a
caller-supplied field. The `QueryEnvelope` is built with `TenantId = context.Tenant`, `Domain = "work"`,
`QueryType = "get-work-item"`, `AggregateId = work.WorkId`, and `UserId = actor PartyId ?? correlationId`.

| `WorkItemView` / call condition | `WorkPlannedEffortReadModel` |
|---|---|
| `context.Tenant is null` | `Unavailable()` *(no Works call is issued)* |
| non-success `QueryResult`, undefined payload, deserialize failure, `view is null` | `Unavailable()` (never throw except `OperationCanceledException`) |
| `view.TenantId` ≠ request tenant | `Unauthorized()` |
| `Found == false` | `NotSupplied()` *(Q-A default; the row is already authorized, so an absent estimate is "not supplied", not an authority failure)* |
| `Found && (Estimated is null OR Unit is null)` | `NotSupplied()` |
| `Found && Estimated is not null && Unit is not null` | `Supplied(Estimated, Done, Remaining, Unit.Value, Fresh@cursor)` |
| `OperationCanceledException` | propagate (caller intent; never converted to a result) |

`Supplied` freshness is `new ProjectionFreshnessMetadata(Fresh, cursor: LatestAcceptedSourceSequence, asOfUtc: null, detail: null)`,
recording the monotonic Works source sequence as the cursor. `SourceModuleName` is hardcoded `"Works"` by the
factories; both `plannedSourceReferenceState` and `plannedSourceFreshness` are populated by the read model.

Unlike the validator, there is **no lifecycle-status write-gate** (Q-B default): a completed or cancelled Work
can still carry a planned estimate worth comparing against logged time, so the estimate is supplied for any
`Found` work that has one, regardless of `Status`.

## No unit conversion (FR20)

The Works `Unit.Value` string is passed through unchanged. The provider never converts Works effort into
minutes/hours or any Timesheets unit, never collapses it with human/external duration, AI runtime, or token
metrics, and never derives a finance/duration value from it. A unit-pass-through test asserts a non-time unit
(`"story-points"`) survives verbatim.

## Source attribution & no-leak (AC2 — NFR3, FR23)

Planned values surface **only** through the typed `WorkPlannedEffortReadModel` (`SourceModuleName == "Works"`).
The provider never logs, never serializes Works state (or the raw `QueryResult` payload) outward, and never
copies Works `Status`, names, descriptions, ownership, `Parent`, or internal roll-up (`OwnEffort`) into
Timesheets events, state, projections, contracts, metadata, or logs. Only the stable `WorkId` (already captured
on the Time Entry) crosses the boundary inbound.

### Privacy fitness-test update (Task 4)

`DiagnosticsPrivacyTests.Works_reference_adapter_does_not_log_serialize_or_copy_works_owned_state` was
refactored: the `.Estimated`/`.Done`/`.Remaining`/`.Unit` prohibition now applies to the
**validator file only** (`WorksQueryWorkReferenceValidator.cs` — an authority gate that must not read effort),
while `ILogger`/`logger`/`Log*`/`JsonSerializer.Serialize`/`SerializeToElement`/`SerializeToUtf8Bytes` and
`.OwnEffort`/`.Parent` stay forbidden across the **entire** `.Works` project. A positive assertion confirms the
new provider exposes effort only via its typed `ValueTask<WorkPlannedEffortReadModel>` return.

## Orthogonal freshness (Epic 4 retro)

Planned-effort source state (`WorkPlannedEffort.SourceFreshness` / `SourceReferenceState`) is separate from the
report's actual-time projection freshness. An unavailable/not-supplied Works call does not degrade the row's
actual-time freshness, and vice-versa (the read model carries its own freshness; the consumer overwrites only
`row.WorkPlannedEffort`). The existing
`ActualTimeReportAuthorizationTests.Work_report_preserves_planned_effort_state_separately_from_actual_projection_freshness`
keeps this locked.

## Freshness limitation (Q-C)

`WorkItemView` exposes only `LatestAcceptedSourceSequence` (a monotonic sequence) and **no** degraded/rebuilding
flag or as-of timestamp. The provider therefore cannot positively detect a stale/rebuilding Works projection
from the consumer view, so it never fabricates a `Stale` result; it returns `Supplied` with `Fresh` freshness
carrying the source sequence as the cursor, and fails closed to `Unavailable` only on transport/deserialize
failure. Positive staleness would require a Works-side consumer-view extension (out of scope, needs Works
approval). The `WorkPlannedEffortReadModel.Stale(...)` factory stays unused by this adapter. (Mirrors Story 1.10
Q3.)

## Launch posture (AC4 — Q-D)

**Default applied: ship the tested adapter + `AddTimesheetsWorksPlannedEffortReporting()` extension, but keep
`src/Hexalith.Timesheets/Program.cs` on the kernel fail-closed default for v1** (consistent with Story 1.10
leaving the validator unwired). Until a host composes **both** `IWorksQueryChannel` (bound to the EventStore
domain-service query channel via the host's `DomainQueryInvokerWorksQueryChannel` pass-through) **and** the new
extension, every Work actual-time report renders planned effort as **unavailable**.

Planned-vs-actual is therefore **post-v1 / unavailable-until-composed**, not waived away and not falsely claimed.
The report metadata already represents this honestly and needs no copy change: the Work actual-time report row
exposes `plannedEffortAvailability` typed as `WorkPlannedEffortAvailability`
(`Unknown`/`Supplied`/`NotSupplied`/`Unavailable`/`Unauthorized`/`Stale`), the catalog copy attributes Works as
the source **only** "when planned values are supplied," and contains **no** finance-ownership language
(no invoice/payroll/rate/tax/revenue). On the kernel default the availability renders `Unavailable`; once the
host composes the adapter it renders `Supplied`/`NotSupplied` from real Works data.

### Host-binding recipe (to go live for v1, if the launch owner approves)

```csharp
// In the Timesheets host composition:
services.AddTimesheetsServerKernel();
services.AddTimesheetsWorksPlannedEffortReporting();              // wins via Replace, either order
services.AddSingleton<IWorksQueryChannel, DomainQueryInvokerWorksQueryChannel>(); // host pass-through

internal sealed class DomainQueryInvokerWorksQueryChannel(IDomainQueryInvoker invoker) : IWorksQueryChannel
{
    public Task<QueryResult> InvokeAsync(QueryEnvelope query, CancellationToken cancellationToken)
        => invoker.InvokeAsync(query, cancellationToken);
}
```

This is the same pass-through Story 1.10 documented for the validator; the validator and provider share the one
`IWorksQueryChannel` binding.

## Open Questions — resolved defaults applied

- **Q-A `Found == false` → `NotSupplied()`.** The row is already authorized; absent estimate is "not supplied".
- **Q-B no lifecycle-status gate.** Supply the estimate for any `Found` work that has one, regardless of `Status`.
- **Q-C never fabricate `Stale`.** Documented limitation above.
- **Q-D ship adapter + extension, host stays on kernel default for v1.** Planned-vs-actual is unavailable until
  the host composes `IWorksQueryChannel` + the extension.

Each is a one-line switch arm + matching test to change if a policy owner decides otherwise.

## Test evidence

- `tests/Hexalith.Timesheets.Works.Tests/WorksQueryWorkPlannedEffortProviderTests.cs` — supplied-with-estimate
  (values + `Works` attribution), source-sequence-as-freshness-cursor, unit pass-through (no conversion),
  found-without-estimate → `NotSupplied`, found-with-estimate-but-no-unit → `NotSupplied`, not-found →
  `NotSupplied`, supply-regardless-of-lifecycle-status (Created/Completed/Cancelled/Expired), cross-tenant →
  `Unauthorized`, non-success `QueryResult` → `Unavailable`, and repeated-invocation determinism.
- `tests/Hexalith.Timesheets.Works.Tests/WorksQueryWorkPlannedEffortProviderEdgeCaseTests.cs` — missing tenant →
  `Unavailable` with no Works call, undefined payload → `Unavailable`, deserialize failure → `Unavailable`,
  `OperationCanceledException` propagation, tenant-scoped `get-work-item` envelope assertion, correlation-id
  fallback for `UserId` when the actor is missing, case-insensitive tenant match, and the null-argument
  fast-fail contract (null channel/context/work).
- `tests/Hexalith.Timesheets.Works.Tests/WorksPlannedEffortReportingCompositionTests.cs` — composed-service
  proof: a DI-resolved `ActualTimeReportQueryService` (kernel + extension + faked `IWorksQueryChannel`) discloses
  a Work row whose `WorkPlannedEffort.Availability == Supplied` and `SourceModuleName == "Works"`, and the
  channel is invoked; **without** the extension the same row keeps the kernel default's `Unavailable`. Adapter
  registration wins in both call orders relative to `AddTimesheetsServerKernel`.
- `tests/Hexalith.Timesheets.Works.Tests/WorksQueryTestData.cs` — `FoundView(...)` extended to populate
  `Estimated`/`Done`/`Remaining`/`Unit`.
- Full solution builds with `-warnaserror` (0 warnings, 0 errors). Suites run green via the built xUnit v3
  executables (VSTest socket is blocked in the sandbox, per Stories 1.1–1.10).
