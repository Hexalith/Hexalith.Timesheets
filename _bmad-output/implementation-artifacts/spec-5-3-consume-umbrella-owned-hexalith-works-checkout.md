---
title: 'Consume the umbrella-owned Hexalith.Works checkout'
type: 'refactor'
created: '2026-09-08'
status: 'done'
baseline_commit: 'e3ea9ce2629e01745d7b9cdb451e302fd5162f6c'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-git-instructions.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-5-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture.md'
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-20.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 5.3 requires Timesheets to consume one umbrella-owned Hexalith.Works source so Stories 1.10 and 4.8 cannot silently bind a second commit. The July 2026 story assumed a nested Timesheets clone with a root `Hexalith.Works` gitlink. This clone later moved every dependency under `references/`, still gitlinks `references/Hexalith.Works`, and has no enclosing superproject.

**Approach:** Treat this Timesheets clone as the umbrella. Keep `references/Hexalith.Works` as the sole default checkout. Add fitness tests and docs that reject a repository-root Works declaration, gitlink, or probe. Keep the Works adapter and its Contracts-only project reference unchanged. Do not initialize or mutate any Works checkout.

**Decision:** Option B — this repo is the umbrella. Keep the `references/Hexalith.Works` submodule. Reject only a repository-root `Hexalith.Works` path, gitlink, or `$(MSBuildThisFileDirectory)Hexalith.Works` probe. Keep the current `references/` then `../Hexalith.Works` probes. Do not remove the Works gitlink.

## Boundaries & Constraints

**Always:** Preserve a caller-supplied `HexalithWorksRoot`. When unset, resolve `references/Hexalith.Works` first and `../Hexalith.Works` second. Keep `Hexalith.Timesheets.Works` pointing at `$(HexalithWorksRoot)\src\Hexalith.Works.Contracts\Hexalith.Works.Contracts.csproj` only. Keep Stories 1.10 and 4.8 green. Use `.slnx`, Central Package Management, and warnings-as-errors. One C# type per file. Read `hexalith-git-instructions.md` before any gitlink or `.gitmodules` edit.

**Never:** Change domain, host wiring, EventStore persistence, or UI. Do not reintroduce a repository-root `./Hexalith.Works` probe, gitlink, or `.gitmodules` path. Do not remove `references/Hexalith.Works`. Do not instruct anyone to initialize `Hexalith.Timesheets/Hexalith.Works`. Do not edit files inside `references/Hexalith.Works`. Do not bump other submodule pointers. Do not add `Hexalith.*` NuGet package references.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Default evaluation | `HexalithWorksRoot` unset; `references/Hexalith.Works` present | Property resolves to `references/Hexalith.Works` | Restore fails closed if that checkout is missing; do not create or init a checkout |
| Sibling fallback | `HexalithWorksRoot` unset; `references/Hexalith.Works` absent; `../Hexalith.Works` present | Property resolves to `../Hexalith.Works` | Same fail-closed rule; do not init Works |
| Explicit override | Caller sets `HexalithWorksRoot` | The supplied path is preserved | Treat as read-only; do not init or mutate Works |
| Root-local regression | `.gitmodules`, git index, or `Directory.Build.props` reintroduces repo-root `Hexalith.Works` | Fitness tests fail | N/A |
| Adapter continuity | `Hexalith.Timesheets.Works.Tests` after alignment | Existing 1.10 and 4.8 tests stay green | Report the exact command and counts |

</frozen-after-approval>

## Code Map

- `.gitmodules` lines 28–30 and git index -- `[submodule "Hexalith.Works"]` at `references/Hexalith.Works` (`160000` @ `42c43180df201883512369c680db1d091f57b02d`). No root `Hexalith.Works` gitlink.
- `Directory.Build.props` lines 7 and 14 -- caller-supplied `HexalithWorksRoot` wins; else `references\Hexalith.Works` then `..\Hexalith.Works`. No `.\Hexalith.Works` probe. Change Works only; leave other `Hexalith*Root` probes.
- `src/Hexalith.Timesheets.Works/Hexalith.Timesheets.Works.csproj` line 17 -- keep the Contracts-only `$(HexalithWorksRoot)` `ProjectReference`.
- `src/Hexalith.Timesheets.Works/WorksQueryWorkReferenceValidator.cs`, `WorksQueryWorkPlannedEffortProvider.cs`, and `tests/Hexalith.Timesheets.Works.Tests/` -- 1.10 / 4.8 behavior; re-run, do not rewrite.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/` -- `DependencyDirectionTests` is name-presence only; `ScaffoldGovernanceTests` is nested-`.git` only; `LaunchReadinessTests` is the Story 5.2 doc-guard pattern; `RepositoryRoot` locates the repo. Add `WorksCheckoutGovernanceTests.cs`.
- `docs/launch-readiness.md`, `docs/boundary-decision-record.md` line 32, `README.md`, and `_bmad-output/planning-artifacts/architecture.md` lines 262–275 and 890–892 -- stale or missing checkout guidance; do not flip overall launch `CONCERNS` to `PASS`.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- `5-3-consume-umbrella-owned-hexalith-works-checkout: backlog`.
- `_bmad-output/implementation-artifacts/spec-move-submodules-to-references.md` -- later relocate that pinned Works under `references/`; keep that pin.
- Do not change -- files under `references/Hexalith.Works/`, adapter mapping, host deny/unavailable defaults, or other module probes.

## Tasks & Acceptance

**Execution:**
- [x] `.gitmodules` and the Works gitlink -- keep `path = references/Hexalith.Works`; add no root `Hexalith.Works` path or gitlink -- this repo stays the umbrella pin.
- [x] `Directory.Build.props` -- keep caller-supplied `HexalithWorksRoot` and the `references/` then `../Hexalith.Works` probes; never restore `$(MSBuildThisFileDirectory)Hexalith.Works` -- change Works only.
- [x] `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/WorksCheckoutGovernanceTests.cs` -- new fitness class asserting the `references/` declaration is allowed, the gitlink is not at repo root, no root-local probe exists, and an explicit `HexalithWorksRoot` is preserved -- fail if a root Works checkout returns.
- [x] `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` -- guard launch-readiness checkout-ownership evidence -- same honesty pattern as package currency.
- [x] `docs/launch-readiness.md` -- record that `references/Hexalith.Works` is the default checkout and a root gitlink is forbidden, without claiming live Works host integration -- Story 5.1 / 5.2 evidence shape.
- [x] `docs/boundary-decision-record.md` -- replace the root-level submodule claim with `references/Hexalith.Works` -- stop stale ownership language.
- [x] `README.md` -- name `<workspace>/references/Hexalith.Works` as the sole default checkout; forbid `Hexalith.Timesheets/Hexalith.Works` init; include `Works.Tests` in the test command list -- docs AC.
- [x] `_bmad-output/planning-artifacts/architecture.md` -- rewrite Workspace Dependency Ownership so this repo owns `references/Hexalith.Works`, sibling `../Hexalith.Works` is fallback, and a Timesheets-root Works path is forbidden -- remove the nested-Timesheets contradiction.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- move `5-3-consume-umbrella-owned-hexalith-works-checkout` through in-progress then done -- keep sprint status honest.

**Acceptance Criteria:**
- Given Timesheets Git metadata is inspected, when `.gitmodules` and the index are read, then Works is declared only at `references/Hexalith.Works` and there is no repository-root `Hexalith.Works` gitlink.
- Given `HexalithWorksRoot` is unset and `references/Hexalith.Works` exists, when Timesheets projects evaluate, then the property resolves to that checkout and no Timesheets-root `./Hexalith.Works` probe exists.
- Given a controlled build supplies `HexalithWorksRoot`, when projects evaluate, then the supplied path is preserved and Timesheets does not initialize or mutate Works.
- Given checkout-governance fitness tests run, when `.gitmodules`, the Git index, and `Directory.Build.props` are inspected, then they fail on a reintroduced repository-root Works declaration, gitlink, or root-local path probe, and they allow the `references/Hexalith.Works` pin.
- Given `references/Hexalith.Works` is available, when restore, build, ArchitectureTests, and Works.Tests run with warnings as errors, then they pass and Stories 1.10 and 4.8 stay green.
- Given README, architecture, boundary, and launch-readiness docs are read, when Works setup is described, then they name `<workspace>/references/Hexalith.Works` as the sole default checkout and do not tell anyone to initialize `Hexalith.Timesheets/Hexalith.Works`.

## Implementation Notes

- 2026-09-08 — `.gitmodules` and `Directory.Build.props` already matched Option B; no gitlink or probe edits.
- 2026-09-08 — Added `WorksCheckoutGovernanceTests` and a launch-readiness checkout-ownership guard. Synced README, architecture, boundary, and launch-readiness docs. Overall launch verdict remains `CONCERNS`.
- 2026-09-08 — Verification: `git ls-files --stage` shows one `160000` gitlink at `references/Hexalith.Works`. Unset `HexalithWorksRoot` evaluates to `.../references/Hexalith.Works`; `-p:HexalithWorksRoot=/tmp/explicit-works-root` is preserved. ArchitectureTests 50/50. Works.Tests 76/76. Restore succeeded. SDK `10.0.302` solution/Works build hits pre-existing CS9057 (PolymorphicSerializations analyzer wants compiler 5.9.0; 10.0.302 has 5.6.0). Works.Tests rebuild with SDK `10.0.400` MSBuild: 0 warnings, 0 errors.
- 2026-09-08 — Left parallel uncommitted move-spec review artifacts untouched (`spec-move-submodules-to-references.md`, `deferred-work.md`, extra `DependencyDirectionTests` / `ScaffoldGovernanceTests` assertions). Kept this spec and sprint 5-3 at `in-progress` until review; did not mark epic-5 done.
- 2026-09-08 — Review patches: `WorksCheckoutGovernanceTests` now evaluates `dotnet msbuild -getProperty:HexalithWorksRoot` for the default `references/` path and a caller override, and the root-probe regex rejects `/Hexalith.Works` and `\Hexalith.Works` without matching `..\Hexalith.Works`. ArchitectureTests 50/50 after the patch.
- 2026-09-08 — Re-verified Option B in this clone. Left `epic-5` `in-progress`. Left parallel uncommitted move-spec review artifacts untouched. Kept this spec and sprint 5-3 at `in-progress` until review.
- 2026-09-08 — Review patch: `WorksCheckoutGovernanceTests` also evaluates `HexalithWorksRoot` from the child environment with no `-p:` flag so a Condition rewrite from `and` to `or` cannot stay green.

## Spec Change Log

## Review Triage Log

- BH1 / epics.md, epic-5-context.md, and the July change proposal still describe sibling-only Works — **defer** — carried those planning artifacts still say Timesheets must not gitlink Works. This story updated the live docs listed in the frozen AC (README, architecture ownership, BDR, launch-readiness). Rewriting the historical proposal or compiled epic cache is not caused by the checkout tests.
- BH2 / deferred-work.md still lists 5.3 as unfinished — **defer** — carried that ledger entry was written by the parallel move-spec review. This workflow must not rewrite existing deferred-work rows.
- BH3 / spec-move-submodules review log still calls 5.3 draft/backlog — **defer** — carried that log belongs to the other spec and was left untouched on purpose.
- BH4 / ECH6 / sprint-status is `in-progress` while a task is checked through `done` — **false** — carried the story is `in-review`; `in-progress` in sprint-status is the honest mid-workflow state. Marking `done` before this review would hide an open story.
- BH5 / spec Code Map still says sprint-status is backlog — rejected — carried the requested fix is an edit to this build's spec.
- BH6 / ECH7 / docs AC half-applied / architecture “Use root-level submodules only” / BDR omits `<workspace>/` — **low** — carried README, architecture ownership, and launch-readiness already name `<workspace>/references/Hexalith.Works` and forbid `Hexalith.Timesheets/Hexalith.Works`. BDR names `references/Hexalith.Works` and forbids a root checkout. Architecture line 228 still means non-nested root-declared modules. Everyday readers will not miss the pin. Rejected as low.
- BH7 / VG1 / HexalithWorksRoot is only a Condition substring — **medium** — carried the first-pass claim that MSBuild was never evaluated. The suite now runs `-getProperty:HexalithWorksRoot` with and without `-p:`. The remaining env-vs-global gap is logged as VG1 below.
- BH8 / RootLocalWorksProbe slash forms — **low** — carried the regex now matches `/Hexalith.Works` and `\Hexalith.Works` and rejects `..\Hexalith.Works`. Not re-opened.
- BH9 / ECH1 / ECH8 / gitmodules and gitlink checks use exact `Hexalith.Works` — **low** — carried `.gitmodules` and `git ls-files --stage -- Hexalith.Works` use that exact path. `./Hexalith.Works` is not how this repo declares submodules. Rejected as low.
- BH10 / no Contracts-only ProjectReference fitness test — **low** — carried `DependencyDirectionTests` already keeps Works out of Contracts/Server. Rejected as low.
- BH11a / launch-readiness baseline date unchanged — **false** — carried the record is cumulative (same pattern as Story 5.2). New checkout evidence is in its own section.
- BH11b / ownership table uses verdict `forbidden` — **low** — carried that cell names the forbidden state, not a release-gate vocabulary miss. Rejected as low.
- BH11c / live-integration waived row omits owner/risk/revisit — **false** — carried the same waiver already has owner, risk, and revisit in the Launch-Scope table.
- BH12 / ECH5 / process helpers have no timeout — **low** — carried `git ls-files --stage` and a short `-getProperty` evaluation are not everyday hang locks. Rejected as low.
- ECH2 / index files under `Hexalith.Works/` stay green — **low** — carried the real regression is a `160000` gitlink at `Hexalith.Works`, which the stage check rejects. Rejected as low.
- ECH4 / BH12 / DependencyDirectionTests slash/quote gaps — **defer** — carried those extra assertions came from the parallel move-spec review, not from this story’s Works checkout class.
- BH-context-1 / `bmad:context` never states the Story 5.3 checkout rule — **defer** — the block is in `AGENTS.md`, `CLAUDE.md`, and `.github/copilot-instructions.md`. Fixes that edit agent-context files are deferred.
- BH-context-2 / VG-other / `bmad:context` says README omits Works.Tests — **defer** — README now lists Works.Tests; the stale sentence is in the same agent-context block. Defer rather than edit AGENTS/CLAUDE/copilot-instructions.
- BH-context-3 / shared-baseline sentence rewritten into a Timesheets `bmad:context` process rule — **defer** — same agent-context files.
- BH-env-unset / unset `EvaluateHexalithWorksRoot` inherits the parent environment — **false** — `ProcessStartInfo.Environment` is inherited, but this tree has no `HexalithWorksRoot`. The unset evaluation returned `.../references/Hexalith.Works` for the empty-property case, not a pre-set parent value.
- BH-sibling / frozen sibling-fallback row is never MSBuild-evaluated — **false** — Design Notes require text and git metadata only and forbid requiring a live sibling clone. `Directory_build_props_resolves_references_then_sibling_and_rejects_root_local_probe` pins probe order. Removing `references/Hexalith.Works` to force sibling evaluation is excluded by intent.
- BH-docs-tests / only launch-readiness has a new honesty test — **low** — adding string-contains guards for README, BDR, and architecture is extra fitness surface. Everyday readers already have the pin on those pages. Rejected as low.
- BH-cs9057 / spec Verification and launch-readiness Build still claim `dotnet build -warnaserror` is clean — **defer** — pinned SDK `10.0.302` CS9057 on Works.Contracts is pre-existing analyzer/compiler skew, not introduced by this checkout work.
- BH-nested-init / docs forbid `Hexalith.Timesheets/Hexalith.Works` but not `./Hexalith.Works` — **false** — the frozen docs AC names that exact init string. README, launch-readiness, and architecture already forbid a repository-root checkout and name `<workspace>/references/Hexalith.Works`.
- BH-move-method / move-spec verification uses vstest-style `-method` filters — **defer** — that line belongs to `spec-move-submodules-to-references.md`, not this story’s verification list.
- ECH-second-gitlink / `ReadStagedPaths` only inspects two pathspecs — **low** — a second Works gitlink outside `Hexalith.Works` and `references/Hexalith.Works` would stay green, but default probes would not bind it. Scanning every `160000` path adds complexity. Rejected as low.
- ECH-scaffold-dotdot / `ScaffoldGovernanceTests` accepts `references/../Hexalith.Works` via `StartsWith("references/")` — **defer** — that `StartsWith` assertion is a parallel move-spec review edit, not this story’s Works checkout class.
- VG1 / `-p:HexalithWorksRoot` cannot observe a Condition overwrite of an environment-supplied root — **medium** — verification-gap pre-verified. Isolated MSBuild showed `-p:` always wins, while `HexalithWorksRoot` in the environment is preserved under `== '' and Exists(...)` and overwritten under `== '' or Exists(...)`. The current test would stay green after that Condition typo.

## Design Notes

July 2026 Story 5.3 assumed a nested Timesheets clone. Decision B keeps this clone as the umbrella: `references/Hexalith.Works` is the pin, `../Hexalith.Works` is fallback, and a repository-root Works checkout stays forbidden. `.gitmodules` and `Directory.Build.props` already match that layout; the remaining work is fitness coverage and doc sync.

Fitness tests should read text and git metadata only. Do not shell out to `git submodule update`. For gitlink absence, parse `git ls-files --stage` output or an equivalent checked-in index read; do not require a live sibling clone.

Example of the forbidden root probe (must not return):

```xml
<HexalithWorksRoot Condition="'$(HexalithWorksRoot)' == '' and Exists('$(MSBuildThisFileDirectory)Hexalith.Works\src\Hexalith.Works.Contracts')">$(MSBuildThisFileDirectory)Hexalith.Works</HexalithWorksRoot>
```

## Verification

**Commands:**
- `git ls-files --stage -- Hexalith.Works references/Hexalith.Works` -- expected: one gitlink at `references/Hexalith.Works`; no root `Hexalith.Works` gitlink.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet msbuild src/Hexalith.Timesheets.Works/Hexalith.Timesheets.Works.csproj -nologo -getProperty:HexalithWorksRoot` -- expected: a `references/Hexalith.Works` path when unset; caller value when `-p:HexalithWorksRoot=` is passed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` -- expected: restore succeeds if the approved checkout exists.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` -- expected: 0 warnings, 0 errors.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` -- expected: all pass after the new fitness tests.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Works.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Works.Tests` -- expected: existing 1.10 / 4.8 tests stay green.
