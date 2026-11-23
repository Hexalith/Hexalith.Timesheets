---
title: 'Recover one durable receipt for each finance export'
type: 'feature'
created: '2026-11-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '124dc5b5241163b30ad6ad45a029f7d96e05cdb4'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-6-context.md'
---

<frozen-after-approval reason="User selected the recommended fallback while the installed Platform handoff is unavailable">

## Intent

Release each authorized approved-time export only after reading back its matching durable EventStore receipt. Recover a committed receipt after a lost acknowledgement or restart, including when an attempt has a later clock time. Preserve the existing deterministic CSV, supported filters, preview semantics and fail-closed delivery guard. This is the remaining local implementation of [Story 6.2](../planning-artifacts/epics.md#story-62-require-a-durable-audit-before-delivering-finance-output).

## Boundaries

Use the existing server recorder extension point and the Timesheets EventStore domain receiver, workload gateway and authoritative stream read facilities. Scope idempotency to the request and tenant. Bind requester, original request and generation instants in UTC, correlation, normalized filters and output scope, freshness, row count, format/version and final CSV SHA-256. A receipt carries only audit metadata. Keep preview side-effect free. Existing callers and custom recorders must retain their supported contract. An unconfigured gateway or missing trust must withhold output.

</frozen-after-approval>

## Code Map

- The service and recorder in `src/Hexalith.Timesheets.Server/Exports` already check the audit acknowledgement before delivery. The default recorder cannot acknowledge. Extend the contracts only as needed for a stable request identity and preserve existing caller signatures.
- `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs` demonstrates durable submission, status verification and stream readback. `MagicLinkDurableSubmissionService.cs` already implements workload retries and status handling.
- Integrate the command through `MagicLinkEventStoreDomainProcessor.cs`, `TimesheetsRouteCatalogDeclarations.cs`, the public discovery surfaces and `ServiceCollectionExtensions.cs`. Verify exact route counts and catalog consumers including the isolated fixture and package inventory. Preserve existing command policies and receiver authorization. Distinguish authenticated workload identity from the finance requester.
- The server and integration export suites cover authorization, uncertainty, metadata, cancellation and exact output. `MagicLinkEventStoreActorPersistenceTests.cs` shows how to exercise the real EventStore actor and inspect committed envelopes. Use the .NET 11 SDK from `global.json`.

## Tasks & Acceptance

- [x] Implement idempotent EventStore submission, receipt readback and normal server registration. Demonstrate that the reader verifies the authoritative owner, persisted event identity and payload.
- [x] Given a successful export, compare the persisted metadata and hash against the returned file. Check that raw rows and comments are absent from persistence and that preview does not create a receipt.
- [x] Given an acknowledgement lost after commit, retry from a fresh service/receiver with the same stable request and verify one persisted receipt, its original timestamps and matching delivered bytes. Verify concurrent or repeated retries cannot append a second receipt. Reject a conflicting requester, scope or content under the same request. A malformed, unavailable or unverifiable readback returns zero rows and bytes. Honor caller cancellation.
- [x] Update affected route admission/inventory assertions and operational documentation. Run focused tests and the Debug source solution suites. Preserve existing committed dependency pointers and leave the five deployed magic-link entries open.

## Verification

The integration tests exercise competing first writes and lost acknowledgements through a real EventStore aggregate actor. They inspect the stored event and recreate the service provider, receiver, gateway, command-status store and actor over retained in-memory actor storage. The recovered CSV and redacted rows match the digests in the single receipt. A live host restart remains a deployment qualification item.

The warnings-as-errors Release package build passed. All six isolated NuGet consumers passed against the candidate packages. The package-mode finance, preview and receiver tests passed all 163 cases. The 270 fixture checks and the opt-in report/export/dashboard performance test also passed.

The Debug solution built with warnings as errors against the committed dependency revisions. Across all six source test suites, 2,956 tests passed, five were skipped and zero failed.

## Review triage

| Finding | Assessment and disposition |
| --- | --- |
| Receiver denial could occupy the request stream | High. EventStore persists domain rejection events. A refused finance command must complete with zero owner events so that an authorized retry can proceed. |
| Recovery could miss scope-specific export revocation | High. The initial receiver bound the project, work and contributor. Both new and recovered deliveries need that same authorization. |
| Gateway canonicalizes the storage tenant | Medium. Use the EventStore tenant contract for the stream and retain the domain tenant for policy and receipt comparisons. |
| Public record changes could alter old command identities | Medium. Pin the v1 derivation and test a known vector. |
| A different well-formed event ID can pass syntactic validation | The authoritative read supplies an opaque EventStore-issued event ID. The verifiable evidence is the sole owner event, its type, command lineage, requester and matching contents. Document this trust boundary. |
| Blocked output omitted the stable request ID | Medium. Return the supplied valid ID for reconciliation while withholding the rows and bytes. |
| The persisted request key had no bound | Medium. Enforce the same 256-character limit and control-character exclusions used for retry keys elsewhere in Timesheets. |
| Changed traversal page size can stop recovery | Low. The first receipt binds normalized filters. Document the requirement to retain the normalized page size and cover equivalent inputs. |
| The fixture did not distinguish tenant streams | Medium. Verify independent tenants sharing a request ID against separately addressed actor state. |
| Snapshot-only drift was untested | Medium. Change snapshot metadata while holding the CSV constant and verify the retry withholds delivery. |
| A second review independently identified scoped authorization on recovery | High. Cover revocation after a committed receipt through the normal export service. |
| A second review independently identified rejection-event persistence | High. Exercise a refusal followed by a legitimate first write and read the actor stream. |
| A null ledger query could fault before authorization | Medium. Validate after authorization and propagate cancellation. Cover generation and preview. |
| The event identifier is opaque | Low. The workload readback and command linkage establish attribution. An additional identity service would add no independent witness. |
| A single-page CSV can omit parts of the returned correction history | High. Bind the redacted row representation as well as the CSV in the receipt. |
| Receiver-side export revocation lacked a test | Medium. Exercise a correctly attributed request with denied scope and inspect the owner stream. |
| Independent requests could accidentally share one owner | Medium. Exercise two explicit request IDs and the correlation fallback against one tenant. |
| Equivalent filter representations lacked a recovery case | Medium. Check normalized period and page size on retry. |

### Review Findings

- [x] [Review][Patch] Recover an existing receipt when the retry host clock precedes the new request timestamp. Verify timestamp ordering for persisted receipts and new writes.
- [x] [Review][Patch] Accept a qualified empty EventStore owner for first write and isolate finance owners from unrelated receiver commands.
- [x] [Review][Patch] Propagate cancellation before early returns, and recheck the export grant after row reauthorization.
- [x] [Review][Patch] Validate lossless request IDs and preserve legacy custom acknowledgements for calls that omit a request ID.
- [x] [Review][Patch] Verify request and receipt JSON fields, cursor-only and as-of-only drift, and update the default recorder documentation.
- [x] [Review][Defer] Verify requester attribution and receipt recovery through the installed gateway and receiver. The local actor test substitutes the workload transport. Installed-host qualification is tracked in [launch readiness](../../docs/launch-readiness.md).
- [x] [Review][Defer] Carry the existing ledger scope and traversal issues into Story 6.3. The [finance export deferrals](spec-finance-export-local-deferrals.md) track effective superseded scope and the distinction between empty and withheld pages.

The review build passed with warnings as errors on both the refreshed references and the committed dependency revisions. The six .NET suites on the refreshed references reported 2,970 passed, five skipped and zero failed. All 133 focused finance cases passed against the committed revisions. All 273 Python Dapr fixture checks passed when run on their own after the .NET suites exited. The earlier concurrent fixture run encountered `/proc` process-inspection permission errors.

#### Rejected

- Explicit IDs and fallback correlations share the tenant-scoped identity namespace. The supported retry preserves its original identity form. The existing owner derivation preserves receipts and refuses reuse across forms.
- The row digest is stable for the current v1 contract. The README now requires preserving the v1 digest input when changing DTOs or serialization.
- The recorder extension continues to accept the original exact-event acknowledgement. Only the built-in recorder attests the original timestamps of a recovered EventStore receipt.
- A separate per-row finance grant would change the existing authorization contract. Export authorizes the requested scope and projection reads authorize each returned current and historical scope.
