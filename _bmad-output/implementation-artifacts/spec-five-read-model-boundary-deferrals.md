---
title: 'Resolve five read-model boundary deferrals'
type: 'refactor'
created: '2026-10-25'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '6fe2d21c132a4dcc286fdeb0d6b6e84db50c3c77'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** DW-26, DW-30, DW-35, DW-39 and DW-140 leave shared read-model ownership coupled to Server and allow fresh v7 bootstrap to overlook direct legacy shards. DW-30/35/39 overlap; this batch resolves five ledger entries through one boundary correction.

**Approach:** Separate pure shared contracts from SDK-dependent read-model code, remove Projections' Server dependency, centralize tenant-catalog addressing, and reject direct legacy shards before bootstrap. Review, verify, commit and push the completed changes to main as requested.

## Boundaries & Constraints

**Always:** Keep Contracts infrastructure-free. Use existing EventStore SDK seams. Preserve public CLR type names through assembly forwarding, stored payload type names, JSON fields, keys, projection identities, policy defaults and authorization behavior. Keep the catalog's legacy physical key as a compatibility contract.

**Never:** Change deployed data, perform migrations, initialize submodules, change dependency versions, modify AppHost/topology, or claim deployed inventory or runtime acceptance. Launch remains FAIL. Direct legacy writers must still be inventoried before deployment.

## I/O & Edge-Case Matrix

| State | Expected behavior |
| --- | --- |
| Existing catalog address | Canonical owner and compatibility facade return identical key/store/slot. |
| No selected v7 index and any direct legacy shard exists | Ordinary and streamed bootstrap refuse; preserve legacy data and recovery state. |
| No legacy data | Existing fresh bootstrap succeeds. |
| Direct-shard read fails or is cancelled | No new v7 selection; preserve retry/recovery semantics. |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkTokenHashCapabilityIndex*.cs`: fourteen pure DTOs plus SDK-dependent Projection, TreeReader and TreeBuilder. The old four-shape proposal predates these dependencies; the SDK-bound helper cannot enter Contracts.
- `src/Hexalith.Timesheets.Server/MagicLinks/Stored*.cs`: eight payload wrappers implement SDK IEventPayload; persisted full names must survive relocation.
- `src/Hexalith.Timesheets.Server/Policies/TimesheetsEvidencePolicyOptions.cs`, `TimeEntryCommentPublicationIssuer.cs`, and `Runtime/TimesheetsEventStoreIntegration.cs`: projection dependencies on options and constants; authority and registration stay Server-owned.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs`: HasSelectedLegacyIndexAsync and StreamFinalizedOperationsAsync omit direct shards. Existing selected-shard concurrency is bounded at sixteen.
- `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkGlobalIndexEpochTests.cs`: existing fresh-bootstrap and legacy-refusal fixtures.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Timesheets.Contracts/Models/MagicLinks/`, `Policies/`, and `TimesheetsDomain.cs` -- move pure index DTOs and policy options; centralize domain/publication constants. Preserve existing full type names where forwarding requires them.
- [x] `src/Hexalith.Timesheets.Contracts/Models/ActivityTypes/TenantActivityTypeCatalogReadModelAddress.cs` -- own catalog store, slot, projection name and exact existing v1 key. Delegate the old Server address facade; update writer and loader to canonical ownership (DW-26).
- [x] `src/Hexalith.Timesheets.ReadModels/Hexalith.Timesheets.ReadModels.csproj` -- add a shared module library referencing Contracts and conditional EventStore Client/Contracts only. Relocate the SDK-bound index helper, reader/builder and eight wrappers here with unchanged namespaces; no generic platform plumbing.
- [x] `src/Hexalith.Timesheets.Server/Compatibility/ReadModelTypeForwards.cs`, both existing Server/Projections `.csproj` files, and `Hexalith.Timesheets.slnx` -- forward moved public types, consume ReadModels, and remove Projections-to-Server. Alias existing Server constants to canonical contracts (DW-30/39).
- [x] `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DependencyDirectionTests.cs` -- assert no Server dependency in the owned Projections/ReadModels closure. Explicitly retain Projections' required SDK DomainService composition; do not mistake its intentional transitive runtime dependencies for forbidden module edges (DW-35).
- [x] `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs` -- check all 256 direct shard keys before both fresh-bootstrap paths, with at most sixteen concurrent reads, before any selected-shard early return (DW-140). Treat persisted empty/corrupt rows as requiring explicit recovery; never erase them.
- [x] `tests/Hexalith.Timesheets.Contracts.Tests/ReadModelContractCompatibilityTests.cs` and `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkGlobalIndexEpochTests.cs` -- verify address/serialization compatibility, forwarding, first/last shards, empty/corrupt rows, failures/cancellation, concurrency, preserved legacy data and absent v7 publication; retain success/replay coverage.
- [x] `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/{DependencyModeTests,BuildConfigurationTests,DiagnosticsPrivacyTests}.cs` -- update project inventories and relocated source checks without weakening guards.
- [x] `docs/boundary-decision-record.md`, relevant architecture/project documentation, `docs/launch-readiness.md`, and `_bmad-output/implementation-artifacts/deferred-work.md` -- update ownership and links; close exactly the five selected entries only after verification. Retain a command/result report under `implementation-artifacts/tests/`.
**Acceptance Criteria:**
- Given either dependency mode, when references are evaluated, then Contracts has no SDK reference and Projections/ReadModels cannot reach Server.
- Given existing persisted shapes and catalog keys, when readers/writers use the new assemblies, then identities and behavior remain compatible.
- Given any direct legacy shard, when either bootstrap path runs, then it refuses without publishing v7 or discarding recovery evidence.

## Implementation Notes

- No human-only intent gap or data operation remains. The broad source refactor requires dispatch; existing namespaces preserve compatibility.
- Foreign AppHost/Dapr processes prevent live qualification; this scope requires no lifecycle operations. Planning changed only this draft.

- Implemented the shared library, canonical contracts, 26 Server type forwards and bounded direct-shard refusal in both bootstrap paths. Root inspected the staged unified diff; 25 relocated files match their original contents exactly, and the SDK index helper only aliases the canonical domain constant.
- Focused compatibility 16/16, bootstrap/replay 63/63 and architecture 118/118 passed. The matching Debug/source build passed with zero warnings/errors. All four approved matrix rows have passing covering cases; full-suite Projections completion remains pending before ledger closure.
- Runtime wrapper discovery was traced through explicit processor construction, runtime-type wire serialization and typed Apply replay. Integration passed 918 cases with five declared skips, including actor recreation, protected processing and composed stored payload paths.

- Root independently parsed all retained XML receipts: six suites passed 2640 cases with five declared skips and zero failures/errors. Each approved matrix row has executed passing cases, including 20 persisted-shard shapes, two selected-shard conflicts, two complete-empty scans and six failure/cancellation retry cases.
- Updated README, architecture, boundary ownership and the launch policy source link. Closed exactly DW-26/30/35/39/140 with preserved historical evidence and scoped resolution notes. Deployed inventory/runtime/shared-rebuild obligations and the launch FAIL remain intact. Evidence is retained in [the verification report](tests/five-read-model-boundaries/README.md).

- All accepted review corrections are complete: six-package release integration, drained synchronous read faults, deterministic concurrency assertions, direct single-root refusal/fault coverage, and byte/ETag/recovery-list preservation assertions. The implementation agent passed 72 release-tooling tests and 40 affected cases. Root then passed the full Debug/source solution build (zero warnings/errors) and all 73 bootstrap/replay cases.
- Root independently packed and validated six primary/symbol packages and compiled/executed all six NuGet-only consumers in an isolated Release/package worktree. The tested source/configuration manifest matches 491 current files; Debug assets were preserved. No registry write or deployment occurred.

## Spec Change Log

## Review Triage Log

| Finding | Verdict and evidence | Route |
| --- | --- | --- |
| B1: ReadModels absent from release inventory | high: the manifest and release_common enumerate five packages; pack/validate omit or reject the newly required ReadModels dependency. This directly breaks release consumers. | patch: add the already-introduced package to existing release lists, consumer surface and count expectations. |
| B2: synchronous read exception drops earlier tasks | medium: IReadModelStore permits a synchronous throw; collection construction invokes GetAsync directly before WhenAll, so earlier reads can outlive refusal/retry. | patch: capture invocation faults inside async tasks and prove the batch drains. |
| B3: concurrency lower bound is scheduling-dependent | medium: Task.Delay(1) does not guarantee overlap; a valid peak of one violates the test's minimum of two. | patch: assert the required upper bound without a minimum of two. |
| B4: direct single-root null/ETag path untested | medium: newly added root ETag predicate is not exercised by shard-only null cases or the populated-root fixture. | patch: cover root null/fault cases on both bootstrap paths. |
| B5: recovery evidence assertions require empty lists | low: assertions do not compare these two fields to the captured status, weakening preservation verification when fixtures evolve. | patch: compare both lists to the captured status. |
| B6: source ETag preservation unasserted | low: byte comparison cannot detect an identical-content rewrite that changes a witness ETag. | patch: compare the direct row's ETag as well as its bytes. |
| B7: populated payload compatibility missing | false: the copied wrapper definitions are byte-identical, and passing composed actor tests deserialize populated stored recording/submission/use/terminal payloads and assert their fields and recreated state. ExternalRecordingRecoversCommittedBatchAfterSubmitResponseFault explicitly checks populated recording and submission payloads. The null-wrapper contract check is only one part of that coverage. | reject. |
| B8: populated index relationships untested | false: passing OrdinaryDirectoryInsertionUsesFullOwnerOrderAcrossSameHashPages persists populated integer-keyed digests/pages; MagicLinkNumericIndexTests exercise child trees, rosters, replacements and exact candidate lookup through SDK storage. All fourteen moved DTO definitions are byte-identical to baseline. | reject. |
| B9: pre-move compiled consumer fixture absent | low: all historical Server-qualified names resolve through verified exported forwarders, with unchanged type signatures. A separate retained binary would strengthen proof but no broken consumer is demonstrated; adding a fixture/compiler harness exceeds a direct correction. | reject under the low-value complexity rule. |
| B10: combined bootstrap latency unmeasured | maybe-false (medium, unverified): the extra bounded direct reads are real, but no current deployed deadline failure is established. Controlled store/actual delivery-budget qualification would settle whether the combined direct and selected reads need optimization. | defer: retain a current-bootstrap latency qualification follow-up without claiming runtime acceptance. |
| E1: concurrency assertion can flake | medium: same scheduling-dependent lower bound as B3; the cited snippet differs, but the actual assertions at the cited tests require 2..16. | patch with B3. |
| V1: null direct single-root regression gap | medium: pre-verified mutation demonstrates the missing root ETag-specific covering case. | patch with B4. |
| V2: release cannot distribute ReadModels | high: confirmed against manifest, packer, release_common and validator; same release integration defect as B1. | patch with B1. |

Final dispositions: B1/V2, B2, B3/E1, B4/V1, B5 and B6 are fixed and verified. B7/B8 were refuted by existing populated persistence coverage; B9 was rejected under the low-value complexity rule. B10 is retained as unverified medium follow-up DW-190. No accepted correction remains open.

All findings were classified individually before grouping. Surviving groups are direct corrections with no new public API or newly guarded production state. Release inventory changes distribute the library already approved in the frozen intent. No intent/spec loopback is required.

## Verification

- Restore `Hexalith.Timesheets.slnx` with `-p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`; build with matching Debug/source properties, `--no-restore -warnaserror -m:1 /nr:false`.
- Run all six built xUnit v3 executables individually; start with affected Contracts, Projections and architecture selections. Record expected perf/live skips separately from failures.
- Inspect the evaluated package-mode reference graph without replacing the Debug restore graph. Validate the Conventional Commit with commitlint; fetch and inspect divergence before pushing main. Preserve unrelated changes and submodule gitlinks.
