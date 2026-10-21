---
title: 'Close five deferred developer-tooling and diagnostic items'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Close five deferred items: validate the entire selected v6 index shard before a local diagnostic claims a unique candidate; distinguish missing v7 format from a failed read; run installed BMAD party-mode/forge-idea suites and verify Claude mirrors in CI; reconcile Copilot persona-wrapper coverage against installed personas; and name standalone kernel read dependencies in the three synchronized agent entry points. Preserve opaque diagnostics, supported v7 behavior, domain state, runtime topology, dependency pins, and existing launch classifications. Commit and push the verified changes to main as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation baseline: clean main at `ebf544bbb8c8c2618b5422696404aac5b5b47906`. The user explicitly requested a batch of five deferred items and publication to main. No additional product decisions or irreversible implementation operations are needed. This is a bounded Python/CI/guidance change with no new product API; use the oneshot route.
- Selected ledger summaries are “Validate the complete selected v6 shard in the local Python candidate diagnostic”, “Distinguish selected-format read failure from an absent v7 format in the local index diagnostic”, “Run the installed BMAD party-mode and forge-idea regression suites in CI”, “Copilot agent wrappers do not cover every installed BMAD skill”, and “Name the standalone kernel read dependencies explicitly in the synchronized managed agent guidance”.
- `scripts/magic_link_index_selection.py` currently trusts inline v6 candidates without reading unrelated manifest pages. Reuse the contracts in `MagicLinkTokenHashCapabilityIndexProjection.HasValidPageManifest`, `HasValidDirectoryPage`, `HasValidCandidatePage`, and the production loader's complete-shard traversal; reject invalid manifests, missing/corrupt pages and overlapping/unsorted rows before candidate selection. Retain the generation/root recheck. Add regressions in `scripts/test_magic_link_index_selection.py`.
- `scripts/local_aspire_magic_link_journey.py` catches every RuntimeError reading the selected v7 format. Its read seam already distinguishes absence using `allow_missing=True`; use that seam and propagate failures. Extract only the selected-index snapshot into the pure helper module for direct tests without starting the journey. Make valid-GET diagnostics use the verified candidate path for v6 too.
- `.github/workflows/ci.yml` currently discovers only `scripts/tests`. Add a Python 3.11+ verification path for the two installed BMAD suites and check their `.claude` copies. Keep installer files unchanged. Test persona wrapper inventory against `[agent]` customization metadata rather than treating workflow skills as personas: all 13 installed personas already have wrappers, technical-writer is absent, and architecture is a workflow. Record this historical finding as refuted rather than generating extra agents.
- `RuntimeRegistrationTests.ServerKernelResolvesConcreteMagicLinkLoaderWithoutLoaderOverride` supplies gateway plus `IReadModelStore` and `IReadModelBatchStore` seams. Default EventStore store registration requires `DaprClient`, supplied by the composed host. Name those read dependencies in AGENTS.md, CLAUDE.md and .github/copilot-instructions.md without changing registrations or defaults.
- Runtime baseline admission: `aspire ps --format Json` and `dapr list --output json` show a running foreign Tenants AppHost with app ID `eventstore`. Starting Timesheets would collide; no application was started or stopped. Runtime acceptance remains unfinished. Verify changes using resource-free Python suites and relevant architecture/registration checks.
- Verify the installed BMAD suites, their mirrors, local diagnostic and fixture Python suites, entry-point normalization and the targeted .NET checks. Review independently before finalizing. Mark exactly the five selected ledger entries with source-backed resolutions, preserving their original evidence and every other entry.
- Implemented complete v6 validation and extracted the selected snapshot reader into the resource-free helper. The live journey now uses the optional marker read and the same verified candidate path for v6 diagnostics. Updated all three entry points with the concrete DaprClient and standalone test seams; their normalized contents match. Added the CI-discovered BMAD wrapper/mirror/subprocess checks, Python version preflight, explicit index-suite invocation and local CI/CD commands.
- Review patches retain SDK 10.0.402 golden digests for punctuation, controls, accented and supplementary identifiers; canonical v6 encoding now matches default System.Text.Json, and row/range order uses UTF-16 ordinal comparison. Required Entries, explicit Boolean flag values, Int32 page metadata and integer storage versions are checked. CI subprocess verification rejects an entirely skipped suite as well as empty/failing suites. No product code, dependency pin, skill installer file or gitlink changed.
- Independent review recheck confirmed all seven patched findings resolved, accurate triage of the separate epoch lifecycle gap, passing 16 index and six BMAD tooling checks, and rejection of malformed scalars and empty/all-skipped suites. No concrete defect remained within that recheck. The configured Blind Hunter layer ran; no configured review layer was skipped.

## Review Triage Log

- Medium, patch: Python JSON escaping differed from System.Text.Json, newly rejecting valid unrelated collision pages. Confirmed with temporary SDK 10.0.402 serialization; corrected canonical encoding and retained nine golden collision fixtures plus a directory fixture in regression coverage.
- Medium, patch: Python Unicode sorting differed from .NET UTF-16 ordinal ordering for supplementary/private-use identifiers. Confirmed String.CompareOrdinal orders U+1F600 before U+E000; corrected comparisons and added a full traversal regression with a server-generated digest.
- Medium, patch: missing required Entries could pass a directory-only manifest. Confirmed the record's missing Entries becomes null and production HasValidPageManifest rejects it; now require the member and test both valid-empty and absent cases.
- Medium, patch: falsey malformed legacyContributionsUnverified values such as null and zero passed. Production's Boolean JSON contract rejects them; the diagnostic now accepts only false or the omitted default, and tests reject malformed flags.
- Medium, patch: manifest page metadata outside Int32 range passed. Confirmed production page identifiers and NextCandidatePageId are Int32; bounded the integer validator and added the reproduced 2**40 counter regression.
- Medium, patch: floating storage versions 6.0/7.0 compared equal to integers. Confirmed production integer JSON properties reject those representations; both snapshot selection and candidate dispatch now require integer types, with regression coverage.
- Medium, patch: a subprocess suite with every test skipped passed the new CI gate. Reproduced the summary shape; the gate now requires at least one non-skipped test. Temporary all-skipped and mixed active/skipped suite probes verify refusal and acceptance respectively.
- Medium, defer: the preexisting snapshot diagnostic ignores selected epoch lifecycle, journal and offboarding evidence. Inspected SharedProjectionEpochCoordinator.ReadAsync: offboarding and CatchingUp/Aborting are unavailable; journal/Capturing/Building are stale, with separate conflict degradation. Generation/root equality alone does not cover these states. Added a separate ledger follow-up; candidate shape validation remains distinct from production GET and deployment acceptance.

- Final verification: matching whole-solution Debug/source restore/build passed with zero warnings/errors. All six executables passed individually: **2198 passed, 5 intentional skips, zero failures** across 2203 tests. Final changed-lane Python checks passed **88/88** release/tooling checks and **16/16** index checks, with four BMAD subprocess suites included; local fixture/lineage coverage passed earlier. Full commands, exact suite counts/timings, serializer fixtures and runtime-admission limits are recorded in `tests/five-tooling-diagnostic-handoffs.md`. Exactly five existing ledger entries were resolved; one preexisting epoch-lifecycle diagnostic follow-up was appended. Final fetch showed no divergence.
