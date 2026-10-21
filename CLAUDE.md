# AI Assistant Instructions

This is a location-independent baseline. Its normalized text is intentionally
shared by Codex, Claude, and GitHub Copilot entry points in the superproject
and its root-declared submodules. It contains shared safeguards only; repository
documentation and configuration remain authoritative for repository-specific
rules.

## Required Hexalith LLM Baseline

Before working in a Hexalith repository, locate, read, and follow
`hexalith-llm-instructions.md`.

- If the current repository contains that file at its root, read that copy.
- Otherwise, use `git rev-parse --show-superproject-working-tree` to locate an
  enclosing superproject. When it returns no path, use the current repository
  root as the workspace. Then read
  `<workspace>/references/Hexalith.AI.Tools/hexalith-llm-instructions.md`.
- Before using that workspace copy, confirm its root `.gitmodules` declares
  `references/Hexalith.AI.Tools` as a root submodule.
- Do not initialize or update a nested submodule to locate this file. If no
  permitted location exists, stop and report the missing baseline as a blocker.

## Working in a Repository

- Work from the repository that owns the change.
- Before changing code, configuration, data, or documentation, inspect the
  relevant tracked repository guidance and configuration, including build files,
  `.editorconfig`, `.gitattributes`, tests, and architecture documentation.
- Preserve user changes. Do not revert, overwrite, clean, stage, commit, push,
  branch, or update dependencies unless the task explicitly requires it.
- Validate changes with the narrowest relevant checks and report any blocker
  with the exact command and result.

## Agent Skills

- A repository-local agent skill is a `SKILL.md` manifest and its supporting
  files.
- Never discover, load, or execute an agent skill located in a repository's
  `references/` directory. This restriction does not prevent reading ordinary
  source files or documentation in that directory when the task requires it.
- If a requested skill is available only from `references/`, explain that it is
  unavailable and use an allowed alternative.

## Git and Submodules

- Before Git work, inspect the current repository's branch, working tree,
  remotes, and recent history.
- Use Conventional Commits whenever a commit is requested. Never bypass commit
  validation.
- In an umbrella workspace, initialize or update only dependencies declared by
  the top-level workspace `.gitmodules` file.
- Never initialize or update a submodule's nested submodules unless the user
  explicitly requests that nested work. Never use recursive or remote submodule
  updates by default.
- If nested submodules were initialized accidentally, deinitialize them before
  continuing.

## Shared Entry Points

- Keep `AGENTS.md`, `CLAUDE.md`, and `.github/copilot-instructions.md`
  synchronized as normalized text when intentionally updating this shared
  baseline.
- Keep repository-specific instructions in the managed `bmad:context` block
  below, not in the shared baseline text above the markers.

<!-- bmad:context -->
<!-- Verified 2026-10-20 against d72899b4489aaca0b9a9b96d0ac267165920caf8. Managed by bmad-project-context; edits inside this block are replaced on refresh. Keep anything you want preserved outside the markers. -->

## timesheets

Hexalith Timesheets is a tenant-scoped effort-evidence module: time capture, approval, confirmation, reporting, and finance export. Persistence and hosting start from `hexalith-llm-instructions.md` and `hexalith-state-instructions.md`. Planning lives in `_bmad-output/planning-artifacts/`. Boundaries are in `docs/boundary-decision-record.md`; the overall launch verdict in `docs/launch-readiness.md` is **FAIL** while deployed acceptance remains unfinished.

## Policy

- Never persist Timesheets domain state through SQL, EF, Redis, Dapr `SaveStateAsync`/`GetStateAsync`, local JSON, or direct projection mutation; add write paths through Hexalith.EventStore.
- Store sibling Tenant, Party, Project, and Work identifiers only; never copy their owned data into events or read models.
- Treat JWT claims and caller-submitted context as evidence; authorize through server-side gates.
- Do not add a Timesheets UI project. Timesheets supplies contracts and FrontComposer metadata; a consuming FrontComposer host owns rendered Fluent UI V5 behavior and browser/WCAG evidence.
- This repo ships `Hexalith.Timesheets.AppHost` and `Hexalith.Timesheets.ServiceDefaults` (architecture tests require them). Do not delete them, do not add topology or a second host, and change them only when a story names those projects.

## Where things are

- Aggregates and command services: `src/Hexalith.Timesheets.Server` (`src/Hexalith.Timesheets` is the HTTP host).
- Works ports and opt-in DI: `src/Hexalith.Timesheets.Works`
- `references/Hexalith.Works` is the sole default checkout. MSBuild preserves an explicit `HexalithWorksRoot`; otherwise it resolves `references/Hexalith.Works` first and `../Hexalith.Works` second. Treat the supplied checkout as read-only. Never initialize a repository-root `Hexalith.Works` checkout.
- Rebuildable projections: `src/Hexalith.Timesheets.Projections`
- Launch classifications, approval sources, and gates: `docs/launch-readiness.md`
- Historical performance measurements and wire-path waiver: `docs/performance-evidence.md`

## Running and verifying

- Use the SDK in `global.json` (`10.0.402`, `rollForward: latestPatch`). The AppHost SDK is `13.6.1`; package pins come from the imported Builds catalog plus root CPM overrides.
- Restore/build `Hexalith.Timesheets.slnx` with matching local Debug/source-reference mode: `dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`, then `dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`.
- CI workflows exist under `.github/workflows/`; CI/CD uses Release/published packages. Never mix these restore/build graphs. Works forwards its own EventStore default; when it differs from the consuming catalog, pass the catalog version as `-p:HexalithEventStoreVersion=<catalog version>` to both local commands until the owned build follow-up closes.
- Prefix `DOTNET_CLI_HOME=/tmp/dotnet-cli-home` in restricted environments. Run all six built xUnit v3 executables individually under `tests/<Project>/bin/Debug/net10.0/<Project>`, including `Hexalith.Timesheets.Works.Tests`. SDK 10.0.402 rejects this repository's `dotnet test` VSTest target; see README commands.
- Performance lanes require `TIMESHEETS_PERF=1`; the local Dapr journey requires `TIMESHEETS_LIVE_DAPR=1` and its isolated fixture. Default skips are not runtime acceptance.

## Conventions that differ from defaults

- Leave `AddTimesheetsServerKernel` fail-closed (`DenyAll*` / `Unavailable*`). Bind Works validation or planned effort only via `AddTimesheetsWorksReferenceValidation` / `AddTimesheetsWorksPlannedEffortReporting` after an `IWorksQueryChannel` is registered.
- Keep aggregates pure (`Handle`/`Apply`); I/O stays in services and the host.

## Known pitfalls

- Do not stage `references/` submodule gitlinks unless the change owns that pointer.
- The HTTP host registers the projection assembly and EventStore processing. Capability-index, Time Entry evidence, and tenant Activity Type catalog handlers have composed and historical isolated local persistence proof. Missing state still fails closed; deployed authority/shared-rebuild acceptance and current-graph AppHost runtime qualification remain unfinished (Story 3.6). Do not restore the obsolete unwired-index diagnosis or infer deployed acceptance from local proof.

<!-- /bmad:context -->
