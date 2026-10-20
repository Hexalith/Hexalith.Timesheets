---
title: 'Story 5.2: Apply Round-7 Package-Currency Review Patches'
type: 'chore'
created: '2026-10-20'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 5.2 has seven open round-7 review patches: an approved Works Contracts update and inaccurate package-currency, CI advisory-gate, runtime-proof provenance, file-list, and deferred-work claims.

**Approach:** Apply the seven patches recorded in the original Story 5.2 spec, including Jerome's approved 2026-10-20 Option 1: update only the root CPM pin for `Hexalith.Works.Contracts` to `1.0.2`, rerun package-mode restore/build, Works.Tests, ArchitectureTests and all four NuGet audits, and record catalog-owned EventStore drift separately. Correct current evidence and ledger handoffs while preserving dated historical results and Story 5.1's ownership of final launch reconciliation. Leave sibling files/gitlinks, SDK, npm manifests/lockfile, runtime behavior, and workflow configuration untouched.

</frozen-after-approval>

## Implementation Notes

- Baseline: `2d6b689ec45fd1f73fb7a3ff09e8950c90a2fd07`; working tree was clean when implementation began. The initial status inspection preceded publication of that review-record commit; the original three documentation edits were committed externally and were not modified by this run.
- Scope: seven round-7 patches in the original Story 5.2 spec; no intent gaps, irreversible actions, new APIs, or product behavior. The existing Epic 5 context was checked against newer planning documents by a context-compilation subagent and remains semantically current without edits. No earlier done spec exists for Story 5.1.
- Root CPM now requests Works Contracts `1.0.2`. Release/package-mode verification is required because Debug/source mode would not exercise this package update. The SDK, AppHost and imported catalog stay unchanged.
- All seven original round-7 patch dispositions are marked applied with evidence. Updated `docs/launch-readiness.md`, `docs/ci-cd.md`, `LaunchReadinessTests.cs`, the deferred ledger and Story 5.2 record. The original frozen intent was preserved.
- Verification: forced no-cache solution restore passed (15 projects); Release solution build with `-warnaserror` passed (0 warnings/errors); Release Works.Tests 76/76 and ArchitectureTests 64/64 passed. Works `1.0.1`/`1.0.2` nuspec dependency lists match, and package assets resolve `1.0.2`. Exact commands and counts are appended to the story record.
- All four NuGet audits were rerun without restore against that package graph. Direct audit: only catalog-owned EventStore `3.125.1 -> 3.126.0`; vulnerable/deprecated: clean across 15 projects. Transitive-outdated: solution and AppHost fallback exit 1 with `error: Sequence contains no matching element`; 14 project fallbacks pass, with 26 distinct transitive drift IDs and no new pin. Existing npm findings and unavailable runtime proof remain open; those lanes were not rerun.
- Independent blind review returned seven findings. Corrections distinguish npm vulnerability scanning from the existing signature check, date the historical 21-ID result, anchor the new audit to its actual baseline, retain audit/build/test receipts, distinguish both repositories' implementation/evidence publication, and check both source identities. A new ledger handoff assigns the remaining EventStore drift to the Builds/platform dependency owner.

- Final verification: post-review ArchitectureTests Release build passed with 0 warnings/errors and its executable passed 64/64; Works.Tests remains 76/76. The verification packet retains every audit/build/test result and all count calculations. `git diff --check` is clean. Original Story 5.2 frozen intent and all excluded repository/dependency files are unchanged.

## Review Triage Log

| Finding | Verdict | Evidence and disposition |
|---|---|---|
| Blanket claim that no workflow runs `npm audit` | low | Confirmed the pinned Builds commitlint workflow runs `npm audit signatures`; this verifies provenance rather than vulnerabilities. Patched current prose and resolution notes to state that distinction. |
| Historical 21-ID/no-direct-update result follows current 26-ID drift | low | Both runs exist, but the transition could mislead maintainers. Patched the older sentence to explicitly name 2026-10-18 and qualify its no-update observation to that date. |
| New audit lacks source baseline in the current verdict | low | The story record names the correct source; the package paragraph previously named only the earlier baseline. Patched the dated update to identify the modified worktree based on `2d6b689ec45fd1f73fb7a3ff09e8950c90a2fd07`. |
| Audit totals lack durable raw outputs | medium | Registry results change over time, making totals hard to check after the run. Patched by retaining compressed raw solution and project audit output, build/test logs and graph-input hashes with reading instructions. |
| EventStore drift lacks a durable owner handoff | low | The drift is real and catalog-owned; adoption requires a separate owning-repository change. Deferred to a new ledger entry naming the catalog pin, owner, compatibility risk and reassessment condition. |
| Runtime source pair has only a Timesheets publication identity | low | The evidence README records EventStore implementation publication separately. Patched current evidence and ledger wording to identify EventStore publication and Timesheets proof publication independently. |
| Provenance guard omits the EventStore source identity | low | The changed guard could accept a paragraph with the EventStore source removed. Patched its existing assertion to include both source identities together and both later publication identities. |
