---
title: 'Close five deferred projection rebuild integrity gaps'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five related deferred items allow malformed projection rebuild state to escape as inconsistent exceptions, publish unusable catalog contents, or produce an unconditional write without an existing row's concurrency token. Malformed event payload envelopes can also be normalized as equivalent.

**Approach:** Complete the ledger's missing-ETag, null index fields, null catalog Items, event format/payload, and rebuild exception consistency items together. Reject malformed serialized candidates and persisted merge inputs through the existing deterministic fold exception, validate required event-envelope fields before equivalence, and refuse to finalize an existing row without a usable ETag. Keep absent-row CreateOnly and existing-row Match behavior, valid canonical output, tenant preservation, and transport/cancellation behavior. Add falsifying regression tests, annotate exactly five canonical ledger resolutions, independently review, and verify the matching Debug/source graph and all six test executables before committing and pushing to main. No dependency, AppHost, agent-guidance, domain-authority, or deployed-acceptance changes are needed.

</frozen-after-approval>

## Implementation Notes

- Baseline: clean `main` at `5454741826e8525d965007f1f7c2919c51e72556`; root Hexalith baseline/state/Git instructions, editor/build/package/test configuration, architecture projection/privacy constraints and boundary record were read. Builds and Works both select EventStore `3.126.0`; no version override is required.
- Route facts: no unresolved user-visible choice, migration or domain-data operation; four existing production helpers/handlers, focused projection tests, this spec and five ledger annotations form one bounded fail-closed correction. Commit and push are explicitly authorized.
- Code map: `ReadModelRebuildConcurrency.For` selects rebuild operation policy; both shared handlers call it. `ProjectionEventReader.Normalize` checks envelopes before duplicate equivalence; `Deserialize` is also called directly. Index `AccumulateAsync`/`FinalizeAsync`, `FromCandidate`/`CanonicalCandidate`, and catalog `FromCandidate`/`Merge`/`FinalizeAsync` own candidate validation. Reuse `ProjectionFoldException`, keep store I/O outside fold guards, and retain SDK interfaces and persisted shapes.
- Tests: extend `MagicLinkStateProjectionHandlerTests` through a same-class partial file to reuse real handlers, serialized candidates, seeded read-model snapshots and per-key ETags. Update existing no-ETag assertions to require refusal; preserve valid absence/Match, foreign-tenant and collision controls. New test names use PascalCase.
- Required Aspire baseline: exact-target `aspire start --non-interactive --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --format Json` succeeded. `aspire describe` observed EventStore and both Dapr resources Running/Healthy, but Timesheets Finished. Exact-target `aspire stop --non-interactive --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj` stopped successfully before edits. This observation does not establish runtime qualification or deployed acceptance.

- Implemented explicit malformed-candidate validation and consistent `ProjectionFoldException` mapping, event wire-field checks before equivalence, and refusal of existing rows without usable ETags. Store reads remain outside fold guards. Added serialized candidate, unchanged-state/ETag, live malformed-envelope, persisted-input, direct-reader and transport controls. The first focused run exposed an invalid test setup (seeding a blank TenantReference already fails in its constructor); that unreachable setup was removed while the serialized candidate case remains covered.
- Focused projection restore/build and matching whole-solution Debug/source restore/build passed with zero warnings/errors. The focused `MagicLinkStateProjectionHandlerTests` selection passed 169/169 with zero skips/failures. Exactly five canonical ledger entries now carry dated resolution notes. Full individual executable suites and independent review are underway.

- Independent Blind Hunter review returned six findings. Patched empty/blank ETags with null-materialized values, the missing root Contributions-null control, precise direct-reader envelope diagnostics, and transport controls using the exact exception types caught by fold guards. Added valid-empty-candidate, cancellation, and derived-cache repair controls. The pre-review full projection and integration runs were stopped as superseded; their incomplete runs are not acceptance evidence.
- Scope clarification: these five legacy shared-candidate items cover `AccumulateAsync`/`FinalizeAsync` and their `FromCandidate` decoders. The separate pre-existing streamed partition decoder remains outside this closure; its verified malformed-partition exception gap is recorded as one new deferred follow-up. No known protected-data disclosure or write-before-failure was observed in that path.

## Review Triage Log

- BH1 — **medium / patch**: an empty or whitespace ETag paired with a null-materialized value selected an unusable CreateOnly plan. Only `(Value=null, ETag=null)` now signifies absence; both handlers have combined unreadable/empty/blank controls. A null/null entry cannot distinguish an unreadable row from absence; CreateOnly still cannot overwrite a row.
- BH2 — **false**: `Expand` deliberately uses canonical Contributions when nonempty and re-derives Entries. A null member in that unused derived cache never enters ReplaceTenant or the finalized output. A new positive control with `Entries={"bad":null}` and a valid other-tenant contribution proves final output repairs the cache while preserving the other tenant. Required collection-null checks remain explicit.
- BH3 — **medium / defer**: streamed partition JSON/token validation is a separate pre-existing path, outside both FromCandidate decoders. It fails before yielding writes, with raw JSON/argument exceptions. Qualified the five closure notes and recorded one separate follow-up rather than claiming streamed rebuild qualification.
- BH4 — **low / patch**: add a serialized root `contributions:null` regression; removing the collection guard must no longer leave coverage green via Expand fallback.
- BH5 — **low / patch**: direct malformed-wire tests now assert the envelope-specific diagnostic and absence of an inner exception, separately from unsupported format; the existing fallback errors cannot satisfy the assertion.
- BH6 — **low / patch**: store read failures now include InvalidOperationException and JsonException, checking exact exception identity so incorrectly moving await into a fold guard is observable.

- Post-review whole-solution Debug/source build passed with zero warnings/errors; expanded focused projection selection passed **183/183**, zero skips/failures. The independent reviewer rechecked all six findings as resolved or accurately triaged, with no concrete introduced regression. Final full executable suites were started from this reviewed build. The configured Blind Hunter layer ran; no configured review layer was skipped.

- Final verification completed from the post-review Debug/source build: ArchitectureTests **103 passed / 0 skipped** (11.383 s); Contracts.Tests **92 passed / 0 skipped** (0.607 s); Server.Tests **558 passed / 0 skipped** (2.080 s); Projections.Tests **549 passed / 0 skipped** (621.309 s); Works.Tests **76 passed / 0 skipped** (0.374 s); IntegrationTests **820 passed / 5 skipped** (595.724 s). Aggregate: **2203 tests, 2198 passed, 5 intentional skips, zero failures/errors/not-run**. Every executable ran individually with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home` and `-longRunning 20`. The five skips remain the default infrastructure, performance and live-Dapr opt-ins; this is not live runtime/deployed acceptance.
- Reproduction: restore with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`, then build with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`. Run each `tests/<Project>/bin/Debug/net10.0/<Project> -longRunning 20` executable. All six final suite process exits were zero.
- Final diff checks passed. Exactly five canonical ledger entries were resolved; one separate pre-existing streamed-decoder follow-up was added. No submodule gitlink, dependency, AppHost, domain-authority, agent entry point or launch classification changed. The Conventional Commit message passed the repository commitlint command before staging.
