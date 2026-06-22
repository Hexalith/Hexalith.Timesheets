# Approved Export Preview — Behavior Note (Story 4.9)

Status: implemented 2026-06-22. Resolves the epic's mutually-exclusive export-preview branch by making
`PreviewApprovedTimeExport` a first-class, side-effect-free query handler that shares one readiness/disclosure
core with `GenerateApprovedTimeExport`. No file is produced and no `ApprovedTimeExported` audit event is emitted
by preview.

## Chosen path (Path A — dedicated handler)

**Implement `PreviewApprovedTimeExport` as a dedicated server handler (`ApprovedTimeExportService.PreviewAsync`)
that shares one `EvaluateAsync` readiness core with `GenerateAsync`.** Preview evaluates ledger filters,
per-row authorization, projection freshness, comment policy, billable-filter state, deterministic output scope,
and the `Ready`/`Blocked` reason, then returns that readiness as a rows-free `ApprovedTimeExportPreviewReadModel`
— **without** producing a CSV file and **without** emitting `ApprovedTimeExported` audit evidence.

Why Path A (not Path B):

1. `PreviewApprovedTimeExport` already exists as a public contract. Giving it a handler makes the public surface
   honest; deleting it would be a non-additive public-surface removal that cuts against NFR12/Compatibility
   (additive, serialization-tolerant evolution).
2. The metadata catalog already advertised a `review-export-readiness` action
   (`Timesheets.ReviewExportReadiness`) with no contract/handler behind it — a phantom. Path A binds that
   advertised action to a real query; Path B would have had to scrub it.
3. Preview is a genuinely distinct capability from the `GenerateApprovedTimeExport` command: a dry run that
   produces no file and emits no audit event. Before this story the only way to get full-scope export
   evaluation (row count, `Blocked` reasons, deterministic scope) was `GenerateAsync`, which writes a CSV and
   records audit evidence — so a no-side-effect preview was a real gap.
4. Sharing one readiness core between preview and generate is the strongest guarantee that "readiness output is
   consistent with export-generation policy" (epic AC4): the two cannot drift because generation gates on the
   same evaluation preview returns.
5. Implementation is a contained, low-risk extraction of already-tested gating logic, not a greenfield build.

## Rejected path (Path B — ledger-query-driven, no dedicated endpoint) and how to flip to it later

**Rejected:** "preview is served by the existing ledger query (`CanUseForExport` + `ExportReadinessDetail` +
freshness) plus `GenerateApprovedTimeExport`; retire the `PreviewApprovedTimeExport` contract." Rejected because
it requires a non-additive removal of a published public contract (NFR12) and a scrub of the already-advertised
`review-export-readiness` metadata action.

Path B remains a **one-step, bounded fallback** if a policy owner later wants zero preview surface:
1. Delete `src/Hexalith.Timesheets.Contracts/Queries/Exports/PreviewApprovedTimeExport.cs` and
   `ApprovedTimeExportPreviewReadModel.cs` + `ApprovedTimePreviewResult.cs`.
2. Remove `ApprovedTimeExportService.PreviewAsync` (keep the shared `EvaluateAsync` core — `GenerateAsync` still
   needs it).
3. Drop the `review-export-readiness` metadata action and the `timesheets.query.approved-ledger-export-preview`
   descriptor; reword "export preview" copy to "export readiness on the ledger query".
4. Delete the preview tests.

Because the readiness vocabulary and the shared core stay, flipping to Path B is documented and reversible
without re-discovery.

## Authorization choice (Q-A default applied)

Preview's authorization gate is `TimesheetsOperation.Export` (parity with `GenerateAsync`): you cannot preview an
export you have no authority to perform, and this keeps fail-closed parity for AC4. The ledger query inside
`LoadDisclosedLedgerAsync` still re-runs `ProjectionRead` + per-row authorization, so preview inherits the full
layered gate. Alternative (`TimesheetsOperation.ProjectionRead`, to expose readiness to ledger-readers without
export authority) is a one-line `AuthorizeAsync(...)` argument change plus the auth tests — recorded but not
applied.

## Side-effect-free guarantee (no file, no audit event)

`PreviewAsync` calls the shared `EvaluateAsync` core and maps the result to `ApprovedTimeExportPreviewReadModel`.
It **never** calls `_auditRecorder`, **never** builds CSV, and **never** computes an output content hash. The
preview read model is structurally rows-free: it carries no `ApprovedTimeExportRowReadModel` evidence and no
`CsvContent`, so a preview can never be mistaken for, substituted for, or leak the contents of a generated file.
The `Audit` metadata it carries sets `GeneratedAtUtc = null` and `OutputContentHashSha256 = null` — nothing is
generated.

Emitting `ApprovedTimeExported` (or any domain event) from preview would be a correctness bug — it is the precise
behavior that distinguishes preview from the generate command. This is asserted: every preview test uses a
fake `IApprovedTimeExportAuditRecorder` and asserts it is **never** called.

## Shared readiness core (AC3)

The single source of truth is one private `EvaluateAsync(context, ledgerQuery, format, formatVersion,
requestedAtUtc, ct)` that performs, in order:

1. `AuthorizeAsync(TimesheetsOperation.Export)` — denial ⇒ terminal `NotFoundOrDenied`.
2. `context.Tenant` null check — ⇒ `MissingTenant` terminal denial.
3. `ValidateContract(format, version, BillableState.Billable)` — ⇒ `Blocked` (no ledger lookup, no audit).
4. `LoadDisclosedLedgerAsync` (full cursor-paged disclosed scope; fails closed on any non-fresh page) —
   terminal denial ⇒ `NotFoundOrDenied`.
5. `ValidateReadiness(ledger)` — `Blocked(reason)` | `Ready`.

`GenerateAsync` calls `EvaluateAsync`, then **only when `Ready`** continues to order rows → `CsvWriter.Write` →
build `ApprovedTimeExported` → `RecordAcceptedExportAsync`. `PreviewAsync` calls the same `EvaluateAsync` and maps
to the preview read model, discarding the disclosed `Items` (it returns only the row **count**, never the rows).
The refactor is behavior-preserving for `GenerateAsync`; its 17 existing tests stay green and inherit the
single-page-truncation fix (cursor loop) and the descending-sort fix from Story 4.5.

Disclosing the scope row **count** to an `Export`-authorized caller is not a leak: the ledger query already
discloses the same authorized rows to that authority.

## Comment policy surface (Q-C default applied — dedicated rows-free read model)

`ApprovedTimeExportPreviewReadModel` carries `CommentExportPolicy` (a `TimesheetsCommentPolicyDecision`) set from
the effective export comment policy. The source is the already-registered `TimesheetsEvidencePolicyOptions`
(`FailClosedDefault` singleton): `ExportCommentsAllowed` ⇒ `Allowed`, else `Excluded`. The options are injected
into `ApprovedTimeExportService` as an **optional** constructor parameter defaulting to
`TimesheetsEvidencePolicyOptions.FailClosedDefault`, so the generate constructor stays additive and its 17 tests
stay green. On the fail-closed default the preview reports `Excluded`. When comment policy is unresolved,
authorization already fails closed (`CommentPolicyMissing` ⇒ `Blocked`) before a `Ready` preview is reached, so
an unresolved policy never surfaces on a `Ready` preview.

The dedicated read model was chosen over reusing `ApprovedTimeExportReadModel` with `Rows=[]`/`CsvContent=null`
because the latter conflates "preview" with "blocked export output" and risks a future change leaking rows
through the preview surface.

## Result wrapper

`ApprovedTimePreviewResult(TimesheetsAuthorizationDecision Authorization, ApprovedTimeExportPreviewReadModel?
Preview)` mirrors `ApprovedTimeExportResult`. Terminal auth denials return `NotFoundOrDenied` (null preview);
contract/readiness blocks return an `Evaluated` `Blocked` preview (parity with how `GenerateAsync` returns
`Blocked` read models rather than denials). `WasDisclosed` is true only when authorized and a preview is present.

## Metadata reconciliation (AC1)

- The already-advertised `review-export-readiness` → `Timesheets.ReviewExportReadiness` action on the
  `timesheets.projection.approved-time-ledger` descriptor is no longer a phantom: it now corresponds to the
  implemented `PreviewApprovedTimeExport` query. The stable action key/intent is preserved (no metadata key
  churn — Q-B default).
- A focused query descriptor `timesheets.query.approved-ledger-export-preview`
  (`TimesheetsSurfaceKind.Query`, `FrontComposerProjectionView`) was added, mirroring the existing
  `timesheets.command.approved-ledger-export` descriptor: selected filters (`QueryApprovedTimeLedger`), output
  scope (`ApprovedTimeExportScope`), projection freshness, export readiness
  (`ApprovedTimeExportReadinessState`), comment policy, and billable filter. Its copy states preview **returns
  readiness without producing a file** and the `review-export-readiness` action is its review intent.
- `TimesheetsSurfaceKind.Query` (value 3) was added — an additive, serialization-tolerant enum extension
  (NFR12) — because preview is genuinely a query surface, not a command or a projection. No exhaustive switch
  or membership test depends on the prior set.
- Copy implies no separate file-producing preview endpoint and avoids invoice/payroll/rate/tax/revenue-
  recognition language (UX-DR30), consistent with the existing export descriptors.

## Scope boundary

This story does **not** wire preview to an HTTP/REST endpoint. The export surface is contract + service +
FrontComposer metadata today (`ApprovedTimeExportService` is registered but not HTTP-mapped); preview matches
that wiring status (registered via `AddTimesheetsServerKernel`, callable, tested, metadata-described). HTTP
dispatch is a separate platform/host concern. Performance evidence (NFR11) is owned by Story 4.10, not here.

## Test evidence

- Contracts: `ApprovedTimeExportContractTests` — preview read-model round-trip (readiness/detail/scope/comment
  policy/freshness/audit-without-generated-fields/format) and `PreviewApprovedTimeExport` omits server-controlled
  authority.
- Server: `ApprovedTimeExportPreviewServiceTests` — mirrors the 17 generate cases; each asserts no `CsvContent`,
  no evidence rows, and zero audit-recorder calls; fail-closed paths (export-denied, missing tenant,
  cross-tenant, stale, empty ledger, non-billable, insufficient-role-filtered, comment-redacted) yield
  `Blocked`/`NotFoundOrDenied` with no leaked rows or comment text; deterministic scope across repeated runs.
- Consistency (AC3): preview and generate agree on `Readiness`, freshness `State`, scope `RowCount`, and block
  reason; on `Ready`, generate additionally produces CSV+audit while preview produces neither.
- Metadata: `ApprovedTimeLedgerContractTests` — the `Timesheets.ReviewExportReadiness` action and the new
  preview descriptor expose export-readiness/freshness/comment-policy/billable vocabularies, imply no
  file-producing endpoint, and carry no EventStore/invoice/payroll/rate/tax/revenue language.
- Integration: `ApprovedTimeExportPreviewIntegrationTests` — seeded billable Project+Work rows ⇒ `Ready` +
  correct `Scope.RowCount` + `Fresh`, no file; no-results filter ⇒ `Blocked`, no file; preview never triggers an
  audit event end-to-end.
- Privacy/fitness: `DiagnosticsPrivacyTests` export scan covers the preview path (preview output, metadata, and
  blocked results disclose no denied Party/Project/Work/Time Entry/comment/CSV details).

## Open Questions — resolved defaults applied

- **Q-A — preview authorization gate = `TimesheetsOperation.Export`** (parity with generate). One-line switch to
  `ProjectionRead` + auth tests if a policy owner wants readiness visible to ledger-readers without export
  authority.
- **Q-B — keep the stable `review-export-readiness` / `Timesheets.ReviewExportReadiness` action key**, bind it to
  the real query (no rename — renaming a published action is non-additive).
- **Q-C — dedicated rows-free `ApprovedTimeExportPreviewReadModel`** (cannot structurally carry evidence/CSV).

Each is a one-line switch arm + matching test to change if a policy owner decides otherwise.
