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
<!-- Verified 2026-09-08 against e3ea9ce2629e01745d7b9cdb451e302fd5162f6c. Managed by bmad-project-context; edits inside this block are replaced on refresh. Keep anything you want preserved outside the markers. -->

## timesheets

Hexalith Timesheets is a tenant-scoped effort-evidence module: time capture, approval, confirmation, reporting, and finance export. Persistence and hosting start from `hexalith-llm-instructions.md` and `hexalith-state-instructions.md`. Planning lives in `_bmad-output/planning-artifacts/`. Boundaries are in `docs/boundary-decision-record.md`; launch posture is `docs/launch-readiness.md` (CONCERNS, not PASS).

## Policy

- Never persist Timesheets domain state through SQL, EF, Redis, Dapr `SaveStateAsync`/`GetStateAsync`, local JSON, or direct projection mutation; add write paths through Hexalith.EventStore.
- Store sibling Tenant, Party, Project, and Work identifiers only; never copy their owned data into events or read models.
- Treat JWT claims and caller-submitted context as evidence; authorize through server-side gates.
- Do not add a Timesheets UI project; future UI goes through FrontComposer and Fluent UI V5.
- This repo ships `Hexalith.Timesheets.AppHost` and `Hexalith.Timesheets.ServiceDefaults` (architecture tests require them). Do not delete them, do not add topology or a second host, and change them only when a story names those projects.

## Where things are

- Aggregates and command services: `src/Hexalith.Timesheets.Server` (`src/Hexalith.Timesheets` is the HTTP host).
- Works ports and opt-in DI: `src/Hexalith.Timesheets.Works`
- Rebuildable projections: `src/Hexalith.Timesheets.Projections`
- Launch waivers and deferred wiring: `docs/launch-readiness.md`
- Perf measurements: `docs/performance-evidence.md`

## Running and verifying

- Use the SDK in `global.json` (`10.0.302`, `rollForward: latestPatch`); do not trust the machine SDK.
- Restore and build `Hexalith.Timesheets.slnx`; test each project under `tests/` individually, including `Hexalith.Timesheets.Works.Tests` (README omits it). There is no `.github/workflows`.
- In restricted environments, prefix `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`. If `dotnet test` fails on VSTest sockets, run the built `tests/<Project>/bin/Debug/net10.0/<Project>` executable.
- Perf lanes stay skipped unless `TIMESHEETS_PERF=1` is set on the IntegrationTests invocation.

## Conventions that differ from defaults

- Leave `AddTimesheetsServerKernel` fail-closed (`DenyAll*` / `Unavailable*`). Bind Works validation or planned-effort only via `AddTimesheetsWorksReferenceValidation` / `AddTimesheetsWorksPlannedEffortReporting` after an `IWorksQueryChannel` is registered.
- Keep aggregates pure (`Handle`/`Apply`); I/O stays in services and the host.

## Known pitfalls

- Do not stage `references/` submodule gitlinks unless the change owns that pointer.
- Empty magic-link resolution is not a loader bug: `MagicLinkTokenHashCapabilityIndexProjection` has no projection-host wiring; valid links fail closed until a story wires it.

<!-- /bmad:context -->

