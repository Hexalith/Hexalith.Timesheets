# Work Reference Validation Adapter — Behavior Note (Story 1.10)

Status: implemented 2026-06-22. Unblocks the Work-reference path for Story 1.7 launch-readiness.

## Chosen integration path

**Consume the Hexalith.Works `get-work-item` consumer query** (domain `work`, query type
`get-work-item`) over the EventStore domain-service query channel. The architecture note claiming Works
exposes no consumer query was outdated; `GetWorkItemQueryHandler` + `WorkItemView` ship today, are
tenant-scoped, and are already fail-closed (`WorkItemView.NotFound`). The heavier
"Timesheets-owned Works EventStore projection" alternative was **not** chosen — it is unnecessary given
the consumer query, and would duplicate Works-owned state.

## Where the code lives (project placement — Open Question Q4)

- Seam + fail-closed default stay in the pure kernel: `src/Hexalith.Timesheets.Server/References/`
  (`IWorkReferenceValidator`, `DenyAllWorkReferenceValidator`). **Unchanged.**
- Concrete adapter lives in a **new** `src/Hexalith.Timesheets.Works` project (default Q4 choice;
  reusable by Story 4.8's planned-effort provider):
  - `WorksQueryWorkReferenceValidator` — the concrete `IWorkReferenceValidator`.
  - `IWorksQueryChannel` — a thin Timesheets-owned port over the EventStore query channel.
  - `WorksReferenceValidationServiceCollectionExtensions.AddTimesheetsWorksReferenceValidation()`.

### Why `IWorksQueryChannel` instead of injecting `IDomainQueryInvoker` directly

`Hexalith.EventStore.Queries.IDomainQueryInvoker` is defined in the EventStore **web host** project.
Referencing that host from a clean domain-adapter library drags in the full ASP.NET/Dapr/Redis/JWT
closure and produces unresolvable transitive assembly-version conflicts (MSB3277:
`Microsoft.IdentityModel.Tokens`, `StackExchange.Redis`) under warnings-as-errors. The adapter therefore
depends on a narrow port `IWorksQueryChannel` whose signature mirrors `IDomainQueryInvoker` exactly
(`Task<QueryResult> InvokeAsync(QueryEnvelope, CancellationToken)`) and needs only the light
`Hexalith.EventStore.Contracts`. The composed host supplies the implementation with a small pass-through
adapter. That adapter is defined **in the host** (not in this library) because `IDomainQueryInvoker` lives
in the EventStore web host that this library deliberately does not reference:

```csharp
internal sealed class DomainQueryInvokerWorksQueryChannel(IDomainQueryInvoker invoker) : IWorksQueryChannel
{
    public Task<QueryResult> InvokeAsync(QueryEnvelope query, CancellationToken cancellationToken)
        => invoker.InvokeAsync(query, cancellationToken);
}

// in the host's service registration:
services.AddSingleton<IWorksQueryChannel, DomainQueryInvokerWorksQueryChannel>();
```

This keeps the kernel boundary intact, keeps the adapter a light/conflict-free library, still consumes
the real Works query over the real `QueryEnvelope`/`QueryResult` wire contract, and is reusable by 4.8.

## Registration (fail-closed by default)

`AddTimesheetsServerKernel` keeps `TryAddSingleton<IWorkReferenceValidator, DenyAllWorkReferenceValidator>()`.
`AddTimesheetsWorksReferenceValidation()` uses `services.Replace(...)` so the concrete validator wins
regardless of call order. When the extension is **not** invoked, the kernel default keeps denying Work
writes — preserving the Story 1.7 guarantee. `TimesheetsAccessGuard` gate order and the
`ReferenceValidationState → TimesheetsDenialCategory` mapping are **unchanged**; only the injected
implementation becomes concrete.

## State mapping (fail-closed)

Tenant authority is taken from `TimesheetsRequestContext.Tenant` (request authority), never from a
caller-supplied field. The `QueryEnvelope` is built with `TenantId = context.Tenant`, `Domain = "work"`,
`QueryType = "get-work-item"`, `AggregateId = work.WorkId`.

| `WorkItemView` condition | `ReferenceValidationState` | Trust-bearing write |
|---|---|---|
| Missing tenant context | `Unavailable` | Deny (authority cannot be consulted) |
| Transport/timeout/non-success `QueryResult`, empty/garbled payload | `Unavailable` | Deny (never `Valid`) |
| `Found == false` (missing or not-yet-projected) | `InvalidReference` *(Q1 default)* | Deny |
| `view.TenantId` ≠ request tenant | `TenantMismatch` | Deny (defensive; cross-tenant id already resolves NotFound) |
| `Status ∈ {Created, Assigned, Queued, InProgress}` | `Valid` | Allow |
| `Status == Completed` | `Valid` *(Q2 default: historical capture)* | Allow |
| `Status == Suspended` | `DisabledOrArchived` *(Q2 default)* | Deny |
| `Status ∈ {Cancelled, Rejected, Expired}` | `DisabledOrArchived` | Deny |
| `Status == Unknown` while `Found` | `Ambiguous` | Deny (should not occur) |

`OperationCanceledException` propagates (caller intent), never converted to a denial. All other
exceptions fail closed to `Unavailable`.

## Open Questions — resolved defaults applied

- **Q1 NotFound → `InvalidReference`.** Both NotFound meanings deny; `InvalidReference` is the surfaced category.
- **Q2 `Completed → Valid`, `Suspended → DisabledOrArchived`.** Confirm with the time-capture policy owner;
  changing them is a one-line switch arm + the matching `[InlineData]` test rows.
- **Q3 Freshness limitation accepted, documented (below).**
- **Q4 New `src/Hexalith.Timesheets.Works` project** (reusable by 4.8).

## Freshness limitation (Q3)

`WorkItemView` exposes only `LatestAcceptedSourceSequence` (a monotonic sequence) and **no**
degraded/rebuilding flag (that lives on the internal `WorkItemRollUp.Degraded`, outside the consumer
view). The adapter therefore cannot positively detect a stale/rebuilding Works projection from
`WorkItemView` alone, so it never returns `Stale`; it fails closed to `Unavailable` only on transport
failure. If trust-bearing writes must additionally fail closed on Works projection staleness, that
requires either a Works-side consumer-view extension (out of scope, needs Works approval) or the
EventStore-projection path where Timesheets owns freshness. The consumer-query freshness contract is
accepted for this story with this documented limitation.

## References-only / privacy

The adapter returns only a typed `ReferenceValidationResult` (state + generic, existence-non-disclosing
reason). It never logs, never serializes outward, and never copies Works lifecycle status, names,
descriptions, ownership, effort (`Estimated`/`Done`/`Remaining`/`Unit`), or `Parent` into Timesheets
events, state, projections, contracts, metadata, or logs. Only the stable `WorkId` (already captured by
the Time Entry path) crosses the boundary. A fitness test
(`DiagnosticsPrivacyTests.Works_reference_adapter_does_not_log_serialize_or_copy_works_owned_state`)
enforces this, and a boundary test
(`DependencyDirectionTests.Server_and_contracts_take_no_works_or_eventstore_reference`) keeps the kernel
and contracts free of Works/EventStore references.

## Test evidence

- `tests/Hexalith.Timesheets.Works.Tests` — 39 tests: every Works state → expected state (incl. the
  active set, `Completed`, `Suspended`, `Cancelled/Rejected/Expired`, `Unknown`), NotFound, tenant
  mismatch, transport failure, empty payload, missing tenant (no Works call), cancellation propagation,
  tenant-scoped envelope assertion, and duplicate/replayed-read determinism. Edge-case suite
  (`WorksQueryWorkReferenceValidatorEdgeCaseTests`) additionally covers envelope audit propagation
  (actor → `UserId`, correlation-id fallback when the actor is missing), case-insensitive tenant
  matching, and the fail-fast null-argument contract (null channel/context/work).
- **AC4 state coverage note.** AC4 enumerates "stale" and "unauthorized" Works states. The consumer
  `WorkItemView` exposes neither as a positive signal, so both are covered by their observable
  manifestations under this adapter's fail-closed contract: a *stale/rebuilding* Works projection is not
  detectable from the view (see Freshness limitation) and any transport-level staleness fails closed to
  `Unavailable`; an *unauthorized/cross-tenant* Work resolves to `NotFound` (→ `InvalidReference`) via the
  tenant-scoped roll-up key, or to `TenantMismatch` via the defensive tenant re-assertion — both denied and
  both exercised by tests. The adapter therefore never returns `Stale` or `Unauthorized`.
- Composed-service proof (Epic 1 retro requirement): the DI-resolved `TimesheetsAccessGuard` invokes the
  concrete adapter when registered (allow path + deny-on-NotFound path), and still fails closed via
  `DenyAllWorkReferenceValidator` when the adapter is not registered. Registration wins in both call
  orders relative to `AddTimesheetsServerKernel`.
- Full solution builds with `-warnaserror` (0 warnings, 0 errors). Suites run green via the built
  xUnit v3 executables (VSTest socket is blocked in the sandbox, per Stories 1.1–1.9).
