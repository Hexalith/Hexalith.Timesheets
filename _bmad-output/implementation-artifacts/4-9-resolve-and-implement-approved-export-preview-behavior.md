---
baseline_commit: ab4a012a5846e9758ab8de15824ca4ff18c19aa8
---

# Story 4.9: Resolve and Implement Approved Export Preview Behavior

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a finance consumer,
I want export preview behavior to match the public contract,
so that downstream users know whether preview is a first-class API operation or ledger-query readiness semantics.

## Decision (RESOLVED — implement this; do not re-open)

**Chosen behavior: Path A — `PreviewApprovedTimeExport` is a first-class, dedicated query handler.**

The epic AC offers two mutually exclusive branches. This story **resolves** the decision so it is
implementation-ready: implement a dedicated, side-effect-free `PreviewApprovedTimeExport` query handler that
shares **one** readiness/disclosure evaluation path with `GenerateApprovedTimeExport`, and make the public
surface consistent with that choice.

**Why Path A (not Path B — "preview is just the ledger query, retire the contract"):**

1. `PreviewApprovedTimeExport` **already exists as a public contract** ([src/Hexalith.Timesheets.Contracts/Queries/Exports/PreviewApprovedTimeExport.cs]). Giving it a handler makes the public surface honest; removing it would be a non-additive public-surface change that cuts against **NFR12/Compatibility (additive, serialization-tolerant evolution)**.
2. The metadata catalog **already advertises** a `review-export-readiness` action → `Timesheets.ReviewExportReadiness` ([TimesheetsMetadataCatalog.cs:485]) that has **no contract or handler behind it** — a phantom surface. Path A binds that advertised action to a real query; Path B would have to scrub it.
3. Preview is a genuinely distinct capability from the `GenerateApprovedTimeExport` command: a **dry run that produces no file and emits no `ApprovedTimeExported` audit event**. Today the only way to get full-scope export evaluation (row count, Blocked reasons, deterministic scope) is `GenerateAsync`, which writes a CSV **and** records audit evidence — so a no-side-effect preview is a real gap.
4. Sharing one readiness core between preview and generate is the **strongest guarantee** that "readiness output is … consistent with export generation policy" (epic AC4): the two cannot drift because generation gates on the same evaluation preview returns.
5. Implementation is a contained, low-risk **extraction** of already-tested gating logic — not a greenfield build.

Path B (ledger-query-driven, no dedicated endpoint) is recorded as the **considered-and-rejected** alternative
in the behavior note; flipping to it later is a documented, bounded change (delete the contract, scrub the
`ReviewExportReadiness` metadata action and "preview" wording). See **Decision Record & Rejected Alternative**
in Dev Notes.

## Acceptance Criteria

1. **Decision recorded; public surface made consistent (resolves epic AC1/AC2 — FR22).**
   **Given** the export-preview behavior is decided as a dedicated handler
   **When** contracts, the metadata catalog, README/OpenAPI guidance, and the behavior note are reviewed
   **Then** the decision and rationale are recorded in a behavior note, the advertised `Timesheets.ReviewExportReadiness` metadata action maps to the real `PreviewApprovedTimeExport` query, and **no** public surface advertises a preview operation that has no implementation or implies a separate file-producing endpoint.

2. **Dedicated, side-effect-free preview handler (epic AC1 — FR18, FR19, FR22).**
   **Given** `PreviewApprovedTimeExport` remains a public query contract
   **When** an authorized caller previews an export scope
   **Then** a dedicated server handler evaluates ledger filters, per-row authorization, projection freshness, comment policy, billable filter state, deterministic output scope, and disabled/exportable (`Ready`/`Blocked`) reasons, and returns that readiness **without producing an export file and without emitting `ApprovedTimeExported` audit evidence**.

3. **Single readiness core; preview matches generation (epic AC4 — NFR9, FR19).**
   **Given** both preview and `GenerateApprovedTimeExport` evaluate readiness over the same ledger scope
   **When** each runs
   **Then** they share one readiness/disclosure evaluation path, so preview's `Ready`/`Blocked` verdict, freshness state, output scope (row count, lineage options), and block reason are identical to what generation gates on — with no duplicated or divergent readiness logic.

4. **Fail closed, no disclosure (epic AC3 — NFR8, NFR12).**
   **Given** preview is requested by an unauthorized, missing-tenant, cross-tenant, stale, unavailable, no-row, or policy-denied caller
   **When** the request is handled
   **Then** it fails closed without leaking protected contributor, target, comment, ledger, or export details (no evidence rows, no CSV content, no excluded/redacted comment text, no denied-row labels in result, logs, or diagnostics).

5. **Deterministic, scenario-tested readiness (epic AC4 — NFR9).**
   **Given** preview tests run
   **When** seeded ledger scenarios include no rows, stale/non-fresh rows, denied and insufficient-role rows, comment-redacted rows, corrected/superseded rows, and billable-filter states
   **Then** readiness output (`Ready`/`Blocked` + scope row count + freshness state + reason) is deterministic across repeated runs and consistent with export-generation policy.

## Tasks / Subtasks

- [x] **Task 1 — Record the decision and keep the contract additive (AC: 1)**
  - [x] Write `_bmad-output/implementation-artifacts/4-9-approved-export-preview-behavior.md` (mirror the structure of `4-8-works-planned-effort-reporting-adapter-behavior.md`): chosen path (dedicated handler), the rejected Path B alternative and how to flip to it later, authorization choice, the side-effect-free guarantee (no file, no audit event), and the metadata reconciliation performed.
  - [x] Keep `PreviewApprovedTimeExport` a public contract (do **not** delete or rename it — additive evolution per NFR12/Compatibility). Verify it accepts **no** caller-supplied tenant/user/correlation/authority fields (it currently exposes only `LedgerQuery`, `Format`, `FormatVersion`).

- [x] **Task 2 — Add the preview read model (AC: 2, 4)**
  - [x] Add `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportPreviewReadModel.cs` carrying: `Readiness` (`ApprovedTimeExportReadinessState`), `ReadinessDetail` (string), `Scope` (`ApprovedTimeExportScope` — surfaces the **billable filter state** and lineage options via `Scope.Filters`), `CommentExportPolicy` (`TimesheetsCommentPolicyDecision` — the effective export comment decision, see next bullet), `ProjectionFreshness` (`ProjectionFreshnessMetadata`), `Audit` (reuse `ApprovedTimeExportAuditMetadata` with `GeneratedAtUtc = null` and `OutputContentHashSha256 = null` — nothing is generated), `Format` (`ApprovedTimeExportFormat`), `FormatVersion` (string).
  - [x] Surface **comment policy** explicitly (epic AC lists "comment policy" among what preview evaluates, and the Export Review Dialog metadata exposes a `commentPolicy` field): set `CommentExportPolicy` from the effective export comment policy — recommended source is the already-registered `TimesheetsEvidencePolicyOptions` (`FailClosedDefault` singleton; `ExportCommentsAllowed` → `Allowed`, else `Excluded`). Inject `TimesheetsEvidencePolicyOptions` into `ApprovedTimeExportService` as an **optional** ctor parameter defaulting to `TimesheetsEvidencePolicyOptions.FailClosedDefault` so the generate ctor stays additive and its 17 tests stay green. (When comment policy is unresolved, authorization already fails closed with `CommentPolicyMissing` → `Blocked`, so an unresolved policy never reaches a `Ready` preview.)
  - [x] The preview model **must not** carry export evidence rows or CSV content. It returns **scope** (row count + filters + lineage options) and the aggregate comment-policy/billable/freshness signals — never the `ApprovedTimeExportRowReadModel` evidence or `CsvContent`. This is the structural guarantee that a preview can never be mistaken for, or substituted for, a generated file or leak per-row evidence.
  - [x] XML docs on every public member (`<param>` tags for the primary-constructor record per the C# standards).

- [x] **Task 3 — Extract one shared readiness core and add `PreviewAsync` (AC: 2, 3, 4)**
  - [x] Refactor `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs`: extract the existing `GenerateAsync` prelude — `AuthorizeAsync(TimesheetsOperation.Export)` → tenant null-check → `ValidateContract` → `LoadDisclosedLedgerAsync` (full cursor-paged disclosed scope) → `ValidateReadiness` → `Scope(...)` — into a private `EvaluateAsync(...)` that returns an internal evaluation value (authorization decision, optional denial, `Ready`/`Blocked` + reason, aggregated `ProjectionFreshnessMetadata`, `ApprovedTimeExportScope`, disclosed row count, and the disclosed `ledger.Items` for the generate path only).
  - [x] Rewrite `GenerateAsync` to call `EvaluateAsync`, then **only when `Ready`** continue to `ApplyExportOrdering` → `ApprovedTimeExportCsvWriter.Write` → build `ApprovedTimeExported` → `_auditRecorder.RecordAcceptedExportAsync`. This must be **behavior-preserving** — all 17 existing `ApprovedTimeExportServiceTests` stay green.
  - [x] Add `public async ValueTask<ApprovedTimePreviewResult> PreviewAsync(TimesheetsRequestContext context, PreviewApprovedTimeExport query, DateTimeOffset requestedAtUtc, CancellationToken cancellationToken)` that calls `EvaluateAsync` and maps the result to `ApprovedTimeExportPreviewReadModel`. It **must never** call `_auditRecorder`, build CSV, or compute an output content hash.
  - [x] Add `src/Hexalith.Timesheets.Server/Exports/ApprovedTimePreviewResult.cs` (mirror `ApprovedTimeExportResult`): `(TimesheetsAuthorizationDecision Authorization, ApprovedTimeExportPreviewReadModel? Preview)` with `WasDisclosed`/`Evaluated` semantics and `NotFoundOrDenied` / `Evaluated` factories. Terminal auth denials return `NotFoundOrDenied` (null preview); contract/readiness blocks return an `Evaluated` `Blocked` preview (parity with how `GenerateAsync` returns `Blocked` read models rather than denials).
  - [x] Reuse the existing readiness messages already present in the service ("Projection freshness does not allow export preview.", "No approved ledger rows are available for export preview.", etc.) — do not invent a second readiness vocabulary.
  - [x] Authorization gate for preview = `TimesheetsOperation.Export` (parity with `GenerateAsync`). See **Open Question Q-A** before changing this to `ProjectionRead`.

- [x] **Task 4 — Reconcile FrontComposer metadata (AC: 1, 2)**
  - [x] Update `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs` so the already-advertised `review-export-readiness` action (`Timesheets.ReviewExportReadiness`, on the `timesheets.projection.approved-time-ledger` descriptor) corresponds to the now-implemented `PreviewApprovedTimeExport` query — the advertised action must no longer be a phantom.
  - [x] Add a focused query descriptor (e.g. `timesheets.query.approved-ledger-export-preview`, `TimesheetsSurfaceKind.Query`) mirroring the existing `timesheets.command.approved-ledger-export` descriptor: fields for selected filters (`QueryApprovedTimeLedger`), output scope (`ApprovedTimeExportScope`), projection freshness, export readiness (`ApprovedTimeExportReadinessState`), comment policy, and billable filter — with copy stating preview **returns readiness without producing a file**.
  - [x] Metadata copy must imply **no** separate file-producing preview endpoint and must avoid invoice/payroll/rate/tax/revenue-recognition language (consistent with UX-DR30 and the existing export descriptors).

- [x] **Task 5 — Tests (AC: 2, 3, 4, 5)**
  - [x] **Contracts** (`tests/Hexalith.Timesheets.Contracts.Tests/ApprovedTimeExportContractTests.cs`): add a round-trip test for `ApprovedTimeExportPreviewReadModel` (readiness/detail/scope/freshness/audit-without-generated-fields/format) and assert `PreviewApprovedTimeExport` JSON omits server-controlled authority (already partially covered — extend, do not duplicate).
  - [x] **Server** — add `tests/Hexalith.Timesheets.Server.Tests/ApprovedTimeExportPreviewServiceTests.cs` mirroring the 17 generate cases, asserting for each: (a) **no `CsvContent` and no evidence rows** on the preview, (b) **zero** audit-recorder calls / no `ApprovedTimeExported` emitted (NSubstitute/fake `IApprovedTimeExportAuditRecorder`, assert never called), (c) fail-closed paths — export-denied, missing tenant, cross-tenant, stale/non-fresh, empty ledger, non-billable, insufficient-role filtered, comment-redacted — yield `Blocked`/`NotFoundOrDenied` with no leaked rows or comment text, (d) deterministic scope row count including superseded/corrected rows per query options.
  - [x] **Consistency** (AC3): one test seeds a scope and asserts `PreviewAsync` and `GenerateAsync` agree on `Readiness`, freshness `State`, scope `RowCount`, and block reason — and that on `Ready`, generate additionally produces CSV+audit while preview produces neither.
  - [x] **Metadata** (`tests/Hexalith.Timesheets.Contracts.Tests/` metadata tests): assert the `Timesheets.ReviewExportReadiness` action / new preview descriptor maps to the real contract, contains export-readiness/freshness/comment-policy/billable vocabularies, implies no file-producing endpoint, and carries no EventStore-stream/invoice/payroll/rate/tax/revenue language.
  - [x] **Integration** (`tests/Hexalith.Timesheets.IntegrationTests/ApprovedTimeExportIntegrationTests.cs` or a new `...PreviewIntegrationTests.cs`): seeded approved billable Project + Work rows → preview returns `Ready` + correct `Scope.RowCount` + `Fresh`, no file; no-results filter → `Blocked`, no file; stale projection → `Blocked`, no file. Assert preview never triggers an audit event end-to-end.
  - [x] **Privacy/fitness** (`tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs` or the existing export privacy test): preview output, metadata, logs, diagnostics, and blocked results disclose no denied Party/Project/Work/Time Entry/comment/CSV details. If an existing test scans the `Server/Exports` folder for export-leak tokens, extend it to cover the preview path rather than adding a parallel scan.

- [x] **Task 6 — Build, test, verify, report (AC: 2, 3, 4, 5)**
  - [x] `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` then `... dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` (expect 0 warnings / 0 errors).
  - [x] Run affected suites via the **built xUnit v3 executables** (VSTest socket is blocked in the sandbox: `SocketException (13): Permission denied`) — Contracts, Server, Integration, Architecture (and Projections only if touched). Record the reason and **exact** counts.
  - [x] Generate the File List from the actual `git` diff and report exact test counts (no estimates — this is an enforced gate; Stories 1.10/1.11/4.8 were caught reporting stale counts). Budget for ≥1 review-found patch.

## Dev Notes

### TL;DR — this is a "resolve a decision + thin extraction", not a greenfield build

Every type this story plugs into already exists and is tested. `ApprovedTimeExportService.GenerateAsync` already
performs the **entire** readiness/disclosure evaluation a preview needs (auth → tenant → contract → full
cursor-paged disclosed ledger → freshness/readiness/billable gate → scope). Your deliverable is: **(1)** record
the decision, **(2)** add one read model + one result wrapper, **(3)** extract that evaluation into a shared core
and expose it as a `PreviewAsync` that stops before CSV/audit, **(4)** make the metadata name a real preview, and
**(5)** test it. Do **not** build a parallel readiness evaluator, a second ledger reader, or a new export pipeline.

### Decision Record & Rejected Alternative (read before coding)

- **Chosen (Path A):** dedicated `PreviewApprovedTimeExport` handler, sharing one readiness core with generate,
  producing **no file and no `ApprovedTimeExported` audit event**.
- **Rejected (Path B):** "preview is served by the ledger query (`CanUseForExport` + `ExportReadinessDetail` +
  freshness) and `GenerateApprovedTimeExport` results; retire the contract." Rejected because it requires a
  non-additive removal of a public contract (NFR12/Compatibility), and because the metadata already advertises a
  preview action that Path B would have to scrub. Path B remains a one-step fallback if a policy owner later wants
  zero preview surface: delete `PreviewApprovedTimeExport`, drop the `review-export-readiness` metadata action,
  and reword the "export preview" copy to "export readiness on the ledger query". Document this in the behavior
  note so the decision is reversible without re-discovery.

### Current code state to extend (verified at baseline `ab4a012`)

- **`PreviewApprovedTimeExport`** ([src/Hexalith.Timesheets.Contracts/Queries/Exports/PreviewApprovedTimeExport.cs]) is a plain public record `{ QueryApprovedTimeLedger LedgerQuery (default BillableState=Billable); ApprovedTimeExportFormat Format=Csv; string FormatVersion="approved-time-export.csv.v1" }` — **structurally identical to `GenerateApprovedTimeExport`**, **no handler**, **not** polymorphic-registered, referenced only by one serialization round-trip test today. (Not polymorphic-registered is correct and matches `GenerateApprovedTimeExport`; do **not** add `[PolymorphicSerialization]` — these export contracts round-trip as plain JSON in tests and are not routed through the polymorphic dispatcher in the current wiring.)
- **`ApprovedTimeExportService`** ([src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs]) is `TryAddSingleton`-registered ([Server/Runtime/ServiceCollectionExtensions.cs:56]) with one public method `GenerateAsync(context, GenerateApprovedTimeExport, DateTimeOffset requestedAtUtc, CancellationToken)`. Ctor deps: `ITimesheetsAccessGuard`, `ApprovedTimeLedgerQueryService`, optional `IApprovedTimeExportAuditRecorder` (defaults to `DomainEventApprovedTimeExportAuditRecorder`). It already: authorizes `TimesheetsOperation.Export` → checks `context.Tenant` → `ValidateContract` (Csv + version + `BillableState.Billable`) → `LoadDisclosedLedgerAsync` (loops the ledger cursor to accumulate the **full** disclosed scope, fails closed on any non-fresh page) → `ValidateReadiness` (Fresh + `CanUseForExport` + ≥1 row + all-billable) → orders rows → writes CSV → records `ApprovedTimeExported`. `RecordAcceptedExportAsync` is called **only** on the accepted/`Ready` path.
- **`ApprovedTimeExportResult`** = `(TimesheetsAuthorizationDecision Authorization, ApprovedTimeExportReadModel? Export, TimesheetsDomainResult? AuditResult)`; factories `Generated` / `Blocked` / `NotFoundOrDenied`. `Blocked` returns `Allowed()` auth + a `Blocked` read model with **no** CSV. Mirror this shape for `ApprovedTimePreviewResult`.
- **`ApprovedTimeExportReadModel`** is an *output* model (it has `Rows` + `CsvContent` + `HasOutput`). **Do not reuse it for preview** — define the dedicated, rows-free `ApprovedTimeExportPreviewReadModel` so a preview cannot structurally carry evidence rows or a file.
- **`ApprovedTimeExportScope`** = `(QueryApprovedTimeLedger Filters, int RowCount, bool IncludesSupersededRows, bool CurrentRowsOnly)` — exactly the "deterministic output scope" preview must return.
- **`ApprovedTimeExportAuditMetadata`** = `(PartyReference? Requester, QueryApprovedTimeLedger Filters, DateTimeOffset RequestedAtUtc, DateTimeOffset? GeneratedAtUtc, string CorrelationId, ApprovedTimeExportScope OutputScope, ApprovedTimeExportFormat Format, string FormatVersion, ProjectionFreshnessState FreshnessState, int RowCount, string? BlockedReason) { TenantReference? Tenant; string? OutputContentHashSha256 }`. For preview, set `GeneratedAtUtc=null`, `OutputContentHashSha256=null`.
- **`ApprovedTimeExportReadinessState`** = `{ Unknown=0, Ready=1, Blocked=2 }`. **`ApprovedTimeExportFormat`** = `{ Unknown=0, Csv=1 }`.
- **`QueryApprovedTimeLedger`** filters: `Project`, `Work`, `Contributor`, `ActivityTypeId`, `TenantLocalPeriodKey`, `ServiceDateFrom/To`, `BillableState?`, `CurrentRowsOnly=true`, `IncludeSupersededRows`, `SortBy`, `SortDirection`, `PageSize=50`, `Cursor`.
- **`ApprovedTimeLedgerReadModel`** = `(IReadOnlyList<ApprovedTimeLedgerRowReadModel> Items, string? NextCursor, ProjectionFreshnessMetadata ProjectionFreshness, bool CanUseForExport, string ExportReadinessDetail)`.
- **Metadata phantom to fix:** `TimesheetsMetadataCatalog.cs:485` advertises action `("review-export-readiness", "Review export readiness", "Timesheets.ReviewExportReadiness")` on the approved-time-ledger projection descriptor, with **no contract/handler behind it**. Bind it to `PreviewApprovedTimeExport`. The command descriptor `timesheets.command.approved-ledger-export` ([:499-526]) is the template for the new query descriptor.

### Shared readiness core — the design that guarantees AC3

The single source of truth for "is this scope exportable, and at what scope/freshness/reason" must be one method
both paths call. Sketch:

```
private async ValueTask<ExportEvaluation> EvaluateAsync(context, ledgerQuery, format, formatVersion, requestedAtUtc, ct)
  // 1. Authorize TimesheetsOperation.Export  -> denial => NotFoundOrDenied
  // 2. context.Tenant null check             -> MissingTenant denial
  // 3. ValidateContract(format, version, BillableState.Billable) -> Blocked(reason), no audit
  // 4. LoadDisclosedLedgerAsync (full cursor-paged disclosed scope; fails closed on non-fresh page) -> denial or aggregate ledger
  // 5. ValidateReadiness(ledger) -> Blocked(reason) | Ready
  // returns: { Authorization, Denial?, Readiness(Ready/Blocked), Reason?, Freshness, Scope, RowCount, DisclosedItems(for generate only) }

GenerateAsync: EvaluateAsync -> if Ready { order rows -> CSV -> ApprovedTimeExported -> RecordAcceptedExportAsync -> Generated } else map to Generated/Blocked/NotFoundOrDenied read model (unchanged outward behavior)
PreviewAsync : EvaluateAsync -> map to ApprovedTimeExportPreviewReadModel (Ready/Blocked); NEVER order rows, NEVER CSV, NEVER RecordAcceptedExportAsync
```

Keep the refactor behavior-preserving for `GenerateAsync` (its 17 tests are your regression net). The
`DisclosedItems` only flow to the generate branch; preview discards them and returns only `Scope` (the row
**count**, not the rows). Disclosing the count to an `Export`-authorized caller is not a leak — the ledger query
already discloses the same authorized rows to that authority.

### Fail-closed & no-disclosure (AC4 — NFR8, NFR12)

- Preview inherits the layered gate: `Export` authority at the top, then `ApprovedTimeLedgerQueryService` re-runs `ProjectionRead` + **per-row** authorization inside `LoadDisclosedLedgerAsync`. Only `InsufficientRole` rows are silently filtered; every other denial (`MissingTenant`, `CrossTenantTarget`, `StaleProjection`, `UnavailableSiblingAuthority`, policy-missing, etc.) is **terminal** → preview returns `NotFoundOrDenied` with no page. (Pattern: `ApprovedTimeLedgerQueryService.QueryAsync`; `CanFilterRow` only allows `InsufficientRole`.)
- Preview must **never** surface evidence rows, CSV content, excluded/redacted comment text, or denied-row identifiers — not in the result, not in logs, not in diagnostics. Mirror the no-disclosure assertions in `TimesheetsDashboardOverviewQueryServiceTests` (deny → `WasDisclosed=false`, projection-reader call count `0`, no rows leaked).
- Comment redaction is already enforced upstream: ledger rows carry `CommentProjectionState`/`Comment` sanitized by projection policy, and the export row mapper drops comment text unless `TimeEntryCommentPolicy.ExportInclusion == Allowed`. Preview returns **no rows at all**, so it cannot reintroduce comment text — but assert it (comment-redacted scenario in AC5) so a future change can't regress it.
- Billable filter: `ValidateContract` already requires `BillableState.Billable`; a non-billable filter must yield `Blocked` with the existing "Approved billable ledger evidence is required for export." reason, not a leak.

### Architecture & boundary constraints

- EventStore stays the only authoritative boundary. Preview is a **read-only query**: no SQL/Redis/Dapr-state/local-file/projection-mutation, and **no domain event** — emitting `ApprovedTimeExported` (or any event) from preview would be a correctness bug (it is the precise behavior that distinguishes preview from the generate command).
- Public contracts hide EventStore envelopes, aggregate internals, projection rebuild mechanics, raw stream names, and sequence internals. Preview returns only typed read models.
- Contract evolution is additive: keep `PreviewApprovedTimeExport`, `GenerateApprovedTimeExport`, and all ledger/export model fields; do not rename. Add the new preview read model and result type alongside.
- Performance: preview reuses generate's full cursor-paged `LoadDisclosedLedgerAsync` to count the true exportable scope (not a single page) — same traversal cost as generate. Report/export/dashboard performance evidence (NFR11, ≤2 s p95) is owned by **Story 4.10**, not this story; do not add a performance lane here.
- Scope boundary: this story does **not** wire export or preview to an HTTP/REST endpoint. The export surface is contract + service + FrontComposer metadata today (`ApprovedTimeExportService` is registered but not HTTP-mapped); preview must match that same wiring status (registered, callable, tested, metadata-described). HTTP/dispatch wiring is a separate platform/host concern — do not expand scope into it.
- Logs/traces: structured metadata + correlation IDs only — no comments, event payloads, command bodies, personal data, tokens, secrets, full rows, raw claims, or protected identifiers (NFR12).

### UX / metadata constraints (UX-DR29, UX-DR30, UX-DR34)

- Preview is the readiness step **before** the Export Review Dialog; it stays metadata-described (no new UI shell, no raw Blazor/HTML/CSS, no Timesheets UI project in this story).
- Keep export/preview copy factual and free of invoice/payroll/rate/tax/revenue-recognition ownership language. Use "Review export readiness" / "export readiness" / "output scope"; never imply Timesheets calculates finance values or that preview produces a file.
- Freshness must remain visible: preview surfaces `ProjectionFreshnessMetadata` so a stale/rebuilding/unavailable projection is shown as non-exportable, never as fresh authority.

### Testing standards

- xUnit v3 · Shouldly · NSubstitute. Test method names PascalCase. Run projects individually (not solution-level `dotnet test`).
- Templates to mirror: `ApprovedTimeExportServiceTests` (the 17 generate cases — clone the seed fixtures), `TimesheetsDashboardOverviewQueryServiceTests` (no-leak/deny-before-read), `ApprovedTimeLedgerQueryServiceIntegrationTests` (seed events → project → query → assert state-store end-state), `ApprovedTimeExportIntegrationTests` (seeded billable Project+Work export fixtures).
- Every claimed readiness state, freshness state, scope row count, authorization decision, comment-policy outcome, and the no-file/no-audit guarantee needs **executable** proof. Integration tests must assert end-state (preview verdict + scope + no emitted audit event), not just return codes.
- Sandbox: VSTest socket blocked (`SocketException (13): Permission denied`). Build first, then run the built executable directly, e.g. `./tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests`. Record reason + exact counts.

### Previous story intelligence

- **Story 4.5 (Generate Finance Export)** created `PreviewApprovedTimeExport`, `ApprovedTimeExportService`, the export read/scope/audit models, and the metadata. Its review explicitly logged: *"`PreviewApprovedTimeExport` contract has no server handler. Preview/readiness is served today by the existing ledger query (`CanUseForExport` + `ExportReadinessDetail` + freshness). The contract is forward-looking for Story 4.6 and is covered by a serialization round-trip test; left in place by design."* This story discharges that forward-looking marker. 4.5's review also fixed a **single-page export truncation** bug (the cursor loop in `LoadDisclosedLedgerAsync`) and a **descending-sort ordering** bug — your shared core inherits both fixes; do not regress them.
- **Story 4.6 (Verify Finance Export Evidence)** owns export audit/golden verification and recorded the CSV `\n` line-ending and formula-injection follow-ups. Preview produces no file, so it does not touch CSV golden files — but the consistency test (AC3) must confirm that when `Ready`, generate still produces the same audited CSV it does today.
- **Story 4.8 (Works planned-effort, just merged at `ab4a012`)** establishes the house gates this story inherits: exact test counts from built executables (not estimates), File List generated from `git diff`, a behavior note for any decision, and "fail closed; never fabricate state." Budget for ≥1 review-found patch.
- **Story 4.2 (Approved-Time Ledger)** + **4.1 (operational queries)** established `ProjectionFreshnessMetadataMapper`, the `CanUseForExport`/`ExportReadinessDetail` signals, deterministic ordering, and result-level authorization. Reuse them via `ApprovedTimeLedgerQueryService`; do not recompute freshness or re-authorize rows in preview.

### Git intelligence

- Recent cadence: `ab4a012 feat(story-4.8): Implement Works Planned-Effort Reporting Adapter`, `c4a9183 docs(epic-3): complete closure retrospective`, `6606239 feat(story-3.7): Prove Magic-Link No-Disclosure at the HTTP Boundary`. Use a `feat(story-4.9):` Conventional Commit; branch `feat/<desc>`.
- The export contracts/service/metadata all landed in `feat(story-4.5)`; this story extends that same `Exports` capability folder. No new package families, no dependency upgrades, no submodule changes required.
- Working tree at story creation has an unrelated modified file (`_bmad-output/story-automator/orchestration-1-20260622-124648.md`). Do not revert or bundle it.

### Latest tech information

- .NET 10 / C# 14, `.slnx` only, Central Package Management (no inline `<PackageReference Version>`), nullable + implicit usings, file-scoped namespaces, Allman braces, `_camelCase` private fields, `Async` suffix, XML docs on public/internal members, `ConfigureAwait(false)` on awaited production calls, `-warnaserror`.
- No external/library research required: no new framework, API, or dependency selection — the architecture and repo pin the stack and every consumed type already ships.

### Project context reference

Follow `Hexalith.AI.Tools/hexalith-llm-instructions.md` and the root `CLAUDE.md`. Domain-centric, read-only story:
persist nothing (no `Hexalith.EventStore` writes — preview emits no event), reuse platform/server services instead
of duplicating. Sibling `project-context.md` files exist for EventStore/Tenants/Parties/Conversations/Projects/
FrontComposer; do not initialize nested submodules.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-4.9] — ACs; requirements FR18, FR19, FR22, NFR5, NFR8, NFR12.
- [Source: _bmad-output/planning-artifacts/epics.md#Story-4.5] — owning export story; "export preview semantics owned by Story 4.9".
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-20-readiness-repair.md#4I-Export-Preview-Decision-Before-Export] — decision-story genesis and the two branches.
- [Source: _bmad-output/planning-artifacts/architecture.md#Query-API-model] (lines 462-464) — "`PreviewApprovedTimeExport` is a contract shape … a dedicated server preview handler requires an explicit later decision"; readiness-repair ownership by Story 4.9.
- [Source: _bmad-output/planning-artifacts/architecture.md#Reporting-Approved-Time-Ledger-And-Exports] (export audit implementation note) — `IApprovedTimeExportAuditRecorder` / `ApprovedTimeExported` evidence shape.
- [Source: _bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md#FR-18] · [#FR-19] · [#FR-22] · [#10-Cross-Cutting-NFRs] (security/observability/compatibility) · [#9-Data-Governance-and-Audit-Requirements] (export accountability).
- [Source: src/Hexalith.Timesheets.Contracts/Queries/Exports/PreviewApprovedTimeExport.cs] · [Commands/Exports/GenerateApprovedTimeExport.cs]
- [Source: src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs] · [Exports/ApprovedTimeExportResult.cs] · [Server/Runtime/ServiceCollectionExtensions.cs#L56]
- [Source: src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportReadModel.cs] · [ApprovedTimeExportScope.cs] · [ApprovedTimeExportAuditMetadata.cs] · [ApprovedTimeLedgerReadModel.cs] · [ProjectionFreshnessMetadata.cs]
- [Source: src/Hexalith.Timesheets.Contracts/ValueObjects/TimesheetsEnums.cs#ApprovedTimeExportReadinessState,#ApprovedTimeExportFormat] · [Queries/Reporting/QueryApprovedTimeLedger.cs]
- [Source: src/Hexalith.Timesheets.Server/ApprovedTimeLedger/ApprovedTimeLedgerQueryService.cs] · [ApprovedTimeLedgerQueryResult.cs]
- [Source: src/Hexalith.Timesheets.Server/Authorization/TimesheetsOperation.cs#Export] · [TimesheetsAccessGuard.cs] · [TimesheetsDenialCategory.cs]
- [Source: src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs#L485 (review-export-readiness phantom),#L499-526 (export command descriptor template)]
- [Source: tests/Hexalith.Timesheets.Server.Tests/ApprovedTimeExportServiceTests.cs] · [TimesheetsDashboardOverviewQueryServiceTests.cs] · [tests/Hexalith.Timesheets.Contracts.Tests/ApprovedTimeExportContractTests.cs] · [tests/Hexalith.Timesheets.IntegrationTests/ApprovedTimeExportIntegrationTests.cs] · [ApprovedTimeLedgerQueryServiceIntegrationTests.cs]
- [Source: _bmad-output/implementation-artifacts/4-5-generate-finance-export-from-approved-ledger.md#Senior-Developer-Review] · [4-8-works-planned-effort-reporting-adapter-behavior.md] (behavior-note template)

### Open Questions (recommended defaults applied; a change is a one-line switch + matching test)

- **Q-A — Preview authorization gate.** Default: `TimesheetsOperation.Export` (parity with `GenerateAsync`; you cannot preview an export you have no authority to perform; keeps fail-closed parity for AC4). Alternative: `TimesheetsOperation.ProjectionRead` if the policy owner wants export readiness visible to ledger-readers who lack export authority (the ledger query already exposes `CanUseForExport` to `ProjectionRead`, so this is defensible). Switching is one `AuthorizeAsync(...)` argument + the auth tests.
- **Q-B — Metadata action naming.** Default: bind the **existing** `review-export-readiness` → `Timesheets.ReviewExportReadiness` action to `PreviewApprovedTimeExport` (no metadata key churn). Alternative: rename it to `Timesheets.PreviewApprovedTimeExport` for literal contract-name parity — but renaming a published metadata action is a non-additive surface change, so keep the stable key unless the policy owner wants the rename.
- **Q-C — Preview read-model shape.** Default: dedicated rows-free `ApprovedTimeExportPreviewReadModel` (cannot structurally carry evidence/CSV). Alternative: reuse `ApprovedTimeExportReadModel` with `Rows=[]`/`CsvContent=null` — rejected because it conflates "preview" with "blocked export output" and risks a future change leaking rows through the preview surface.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8[1m] (Claude Opus 4.8, 1M context) — BMAD dev-story workflow.

### Debug Log References

- Restore: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` → up-to-date.
- Build: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` → **Build succeeded, 0 Warning(s), 0 Error(s)**.
  - A first full-rebuild pass surfaced transient `Hexalith.PolymorphicSerializations` submodule pack/style errors (NU5118 README collision, IDE0065) — unrelated to story 4.9 and self-cleared on the next build (0 such errors; no error outside that submodule at any point). All Timesheets projects compiled clean with `-warnaserror` throughout.
- Tests run via the **built xUnit v3 executables** (VSTest socket blocked in sandbox: `SocketException (13): Permission denied`):
  - `Hexalith.Timesheets.Contracts.Tests` — Total **88**, Failed 0, Skipped 0.
  - `Hexalith.Timesheets.Server.Tests` — Total **420**, Failed 0, Skipped 0.
  - `Hexalith.Timesheets.IntegrationTests` — Total **80**, Failed 0, Skipped 3 (77 passed; 3 skips are pre-existing perf/infra lanes gated by `TIMESHEETS_PERF` / runtime fixtures). (Corrected during review: the Dev Agent Record previously reported 78 before the two AC3/AC5 integration tests were added; the test-summary already recorded 80.)
  - `Hexalith.Timesheets.ArchitectureTests` — Total **29**, Failed 0, Skipped 0.
  - `Hexalith.Timesheets.Projections.Tests` (regression, not touched) — Total **77**, Failed 0, Skipped 0.

### Completion Notes List

- **Decision resolved (Path A).** `PreviewApprovedTimeExport` is now a first-class, side-effect-free query handler (`ApprovedTimeExportService.PreviewAsync`) sharing one `EvaluateAsync` readiness core with `GenerateAsync`. Behavior note written; Path B recorded as the reversible fallback.
- **Shared readiness core (AC3).** Extracted the `GenerateAsync` prelude (auth → tenant → contract → full cursor-paged disclosed ledger → readiness → scope) into a private `EvaluateAsync` returning an internal `ExportEvaluation`. `GenerateAsync` was rewritten to call it and is behavior-preserving — all 17 existing `ApprovedTimeExportServiceTests` stay green (part of the 420 Server total).
- **Side-effect-free guarantee (AC2/AC4).** `PreviewAsync` never calls `_auditRecorder`, never builds CSV, never computes a content hash. `ApprovedTimeExportPreviewReadModel` is structurally rows-free (no `Rows`, no `CsvContent`), so a preview can never carry evidence or a file. Every preview test asserts a fake `IApprovedTimeExportAuditRecorder` is never called.
- **Comment policy (AC2).** `CommentExportPolicy` is set from the registered `TimesheetsEvidencePolicyOptions` (`FailClosedDefault` → `Excluded`; `ExportCommentsAllowed` → `Allowed`), injected as an optional ctor parameter so the generate ctor stays additive.
- **Metadata reconciliation (AC1).** The advertised `Timesheets.ReviewExportReadiness` action is no longer a phantom — it maps to the real query and is carried by a new `timesheets.query.approved-ledger-export-preview` descriptor (added `TimesheetsSurfaceKind.Query = 3`, an additive enum extension). Copy implies no file-producing endpoint and carries no finance-ownership language.
- **Fail-closed (AC4).** Preview inherits the layered gate: terminal denials (export-denied, missing tenant, cross-tenant, stale, policy-missing) → `NotFoundOrDenied` with no disclosed page; only `InsufficientRole` rows are silently filtered. Contract/readiness blocks → `Evaluated` `Blocked` preview with no leaked rows or comment text (asserted by serializing the preview and scanning).
- **Scope boundary honored.** No HTTP/REST wiring added (preview matches the export service's registered-but-not-HTTP-mapped status); no new dependencies; no submodule changes; no performance lane (NFR11 owned by Story 4.10).
- **Test count discipline.** All counts above are exact, taken from the built xUnit v3 executables (no estimates).

### File List

**Added — production**
- `src/Hexalith.Timesheets.Contracts/Models/ApprovedTimeExportPreviewReadModel.cs`
- `src/Hexalith.Timesheets.Server/Exports/ApprovedTimePreviewResult.cs`

**Modified — production**
- `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs` (extract `EvaluateAsync` shared core; add `PreviewAsync`; optional `TimesheetsEvidencePolicyOptions` ctor param; behavior-preserving `GenerateAsync`)
- `src/Hexalith.Timesheets.Contracts/Ui/TimesheetsSurfaceKind.cs` (add `Query = 3`)
- `src/Hexalith.Timesheets.Contracts/TimesheetsMetadataCatalog.cs` (add `timesheets.query.approved-ledger-export-preview` descriptor)

**Added — tests**
- `tests/Hexalith.Timesheets.Server.Tests/ApprovedTimeExportPreviewServiceTests.cs`
- `tests/Hexalith.Timesheets.IntegrationTests/ApprovedTimeExportPreviewIntegrationTests.cs`

**Modified — tests**
- `tests/Hexalith.Timesheets.Contracts.Tests/ApprovedTimeExportContractTests.cs` (preview read-model round-trip)
- `tests/Hexalith.Timesheets.Contracts.Tests/ApprovedTimeLedgerContractTests.cs` (preview descriptor metadata test)
- `tests/Hexalith.Timesheets.Contracts.Tests/ReferenceContractTests.cs` (descriptor count 23 → 24 + new name)
- `tests/Hexalith.Timesheets.Contracts.Tests/TimeCaptureContractTests.cs` (ordered descriptor-name list + new name)
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DiagnosticsPrivacyTests.cs` (preview read-model rows-free fitness test)

**Added — docs**
- `_bmad-output/implementation-artifacts/4-9-approved-export-preview-behavior.md` (behavior note)

## Senior Developer Review (AI)

**Reviewer:** Jerome · **Date:** 2026-06-22 · **Outcome:** Approve (auto-fixed) · **Status:** review → done

### Scope & method

Adversarial validation of every Acceptance Criterion and every `[x]` task against the actual git diff and the real
implementation. Build re-run with `-warnaserror` (0/0) and all affected suites re-run from the built xUnit v3
executables (VSTest socket blocked in sandbox). All five ACs are implemented and all six tasks are genuinely done:
the shared `EvaluateAsync` core is real and behavior-preserving for `GenerateAsync`; `PreviewAsync` is side-effect-free
(no CSV, no content hash, `_auditRecorder` never called — asserted); the metadata phantom is reconciled; File List
matches git exactly. No CRITICAL or HIGH findings.

### Findings & fixes applied (auto-fix mode)

1. **[MEDIUM — count discipline] Stale integration test count in the story file.** The Dev Agent Record (Debug Log
   References) and Change Log reported `IntegrationTests` Total = **78**, but the verified actual is **80** (77 passed,
   3 pre-existing perf/infra skips). Two AC3/AC5 integration tests were added after the count was first recorded; the
   `test-summary.md` was updated to 80 ("was 78; +2 added") but the story file was not synced. This is exactly the
   enforced count-discipline gate Task 6 calls out (Stories 1.10/1.11/4.8 were caught the same way). *Fix:* corrected
   the story's reported integration count to 80 (Debug Log + this Change Log).

2. **[LOW — AC3 consistency] Preview blocked-path `Audit.RowCount` drifted from generation.** `PreviewAsync` set
   `Audit.RowCount = evaluation.Scope.RowCount` on **all** paths, so a blocked-readiness preview (e.g. stale projection
   or non-billable rows present) reported a non-zero audit row count, whereas `GenerateAsync`'s blocked path reports
   `0` (no output rows). AC3 requires preview and generation not to drift. *Fix:* `ApprovedTimeExportService.cs` now
   uses `ready ? evaluation.Scope.RowCount : 0` for the preview audit row count, exactly mirroring generation. The
   top-level `Scope.RowCount` (disclosed scope) is unchanged. *Test:* strengthened
   `Preview_and_generate_share_one_readiness_core_on_blocked_reason` to assert preview and generate agree on both
   `Scope.RowCount` and `Audit.RowCount` (= 0) on a blocked result — closing a real coverage gap (the test previously
   never asserted scope/audit row-count parity on the blocked path).

### Verification after fixes

- Build: `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` → **0 Warning(s), 0 Error(s)**.
- Suites (built executables): Contracts **88**/0/0 · Server **420**/0/0 · Integration **80**/0/**3-skip** · Architecture **29**/0/0 · Projections (regression) **77**/0/0. All green.

### Observations (no action — by design)

- Preview's `CommentExportPolicy` is sourced from the tenant-wide `TimesheetsEvidencePolicyOptions` (DI singleton,
  fail-closed default), not per-row outcomes — the resolved Q-C design; verified the option is registered and injected.
- Shared readiness messages use "export preview" wording even on the generate path; pre-existing (Story 4.5) and
  explicitly reused per Task 3 — not changed.

## Change Log

| Date | Version | Description |
|------|---------|-------------|
| 2026-06-22 | 0.1 | Story 4.9 implemented: dedicated side-effect-free `PreviewApprovedTimeExport` handler sharing one readiness core with generate; preview read model + result wrapper; metadata reconciliation (`review-export-readiness` bound to a real query; new preview descriptor + `TimesheetsSurfaceKind.Query`); contracts/server/integration/architecture tests. Build 0/0; suites green (88/420/80·3-skip/29/77). Status → review. |
| 2026-06-22 | 0.2 | Senior Developer Review (AI): auto-fixed 2 findings — corrected stale integration count (78 → 80) and aligned preview blocked-path `Audit.RowCount` with generation (0 when blocked) + strengthened blocked consistency test. Re-verified build 0/0 and suites green (88/420/80·3-skip/29/77). No CRITICAL issues. Status → done. |
