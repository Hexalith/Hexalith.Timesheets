---
title: 'Move repository submodules under references'
type: 'refactor'
created: '2026-07-20'
status: 'in-review'
baseline_commit: 'd4dd622'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/CLAUDE.md'
  - '{project-root}/.gitmodules'
  - '{project-root}/Directory.Build.props'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Timesheets repository declares most Hexalith dependencies at
repository-root paths, while the shared workspace conventions and agent
baseline expect root-declared dependencies under `references/`. The checkout
also contains a stale duplicate `Hexalith.Builds` gitlink at the root even
though its active declaration already points to `references/Hexalith.Builds`.

**Approach:** Relocate every Timesheets-owned root submodule checkout to
`references/<module>`, remove the stale root `Hexalith.Builds` gitlink, update
`.gitmodules` and the parent MSBuild dependency probes, and preserve each
submodule's checked-out commit and repository content.

## Boundaries & Constraints

**Always:** Keep the same remote URLs and submodule commits; use only the
existing root-declared submodules; keep the operation non-recursive; make
parent build properties resolve local dependencies from `references/` while
preserving supported sibling fallbacks; leave submodule content unchanged.

**Ask First:** Any request to change a submodule commit, remote URL, nested
submodule, dependency version, or product source code is outside this intent.

**Never:** Do not initialize or update submodules from remote; do not edit
files inside a submodule; do not leave root-level Hexalith gitlinks or stale
root path declarations; do not commit or push.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Existing checkout | Initialized root submodules and the existing `references/Hexalith.Builds` checkout | All unique submodules are available at `references/<module>` with their original commits | Stop if a target path is occupied by unrelated content |
| Duplicate Builds gitlink | Root `Hexalith.Builds` and `references/Hexalith.Builds` both tracked | The stale root gitlink is removed and the existing references checkout remains | Stop if the two pointers cannot be distinguished without changing content |
| Fresh clone metadata | Updated `.gitmodules` and gitlink paths | `git submodule status` and non-recursive initialization metadata address only `references/` paths | Report the exact failing Git command |
| Build resolution | Projects evaluate root dependency properties | Local dependency roots resolve under `references/`, and supported sibling fallbacks remain available | Report the exact MSBuild or restore failure |

</frozen-after-approval>

## Code Map

- `.gitmodules` -- declares the parent repository's submodule names, paths, and remotes.
- `Directory.Build.props` -- resolves local and sibling Hexalith project roots for MSBuild.
- `Directory.Packages.props` -- imports centralized package versions from the Builds reference and must retain a valid references-first import.
- `Hexalith.*` and `references/Hexalith.*` -- gitlink worktrees whose tracked paths are being normalized.

## Tasks & Acceptance

**Execution:**
- [x] `.gitmodules` -- change every declared submodule path to `references/<module>` and remove no remote or URL -- align metadata with workspace ownership.
- [x] `Hexalith.*` gitlinks -- move the ten root worktrees and remove the stale root `Hexalith.Builds` duplicate while preserving the existing references pointer -- normalize the tracked dependency layout.
- [x] `Directory.Build.props` -- change local dependency probes and values from root paths to `references/` paths while retaining explicit-property and sibling fallbacks -- keep project evaluation valid after the move.
- [x] `Directory.Packages.props` -- verify and adjust only if needed so the references-first Builds props import remains valid -- preserve centralized package resolution.

**Acceptance Criteria:**
- Given the parent repository metadata is inspected, when all declared submodule paths are listed, then every path is under `references/` and all original remotes remain unchanged.
- Given the parent Git index is inspected, when gitlinks are listed, then there is exactly one gitlink per declared module, every gitlink is under `references/`, and no root `Hexalith.*` gitlink remains.
- Given the moved worktrees are checked, when each submodule HEAD is compared with its pre-move commit, then every commit is unchanged and each worktree remains a valid Git submodule.
- Given a local Timesheets project is evaluated, when its dependency root properties are queried, then local dependencies resolve from `references/` and the Builds package props import succeeds.
- Given repository validation runs, when Git consistency, whitespace checks, restore, and the focused architecture/build checks execute, then they pass without recursive submodule initialization or submodule content changes.

## Verification

**Commands:**
- `git diff --check` -- expected: no whitespace or conflict-marker errors.
- `git submodule status` -- expected: one valid entry per `references/` submodule.
- `git ls-files -s | awk '$1 == "160000"'` -- expected: all gitlinks are under `references/`.
- `dotnet msbuild src/Hexalith.Timesheets.Works/Hexalith.Timesheets.Works.csproj -nologo -getProperty:HexalithWorksRoot` -- expected: a valid resolved dependency path.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` -- expected: restore succeeds using the relocated references.
