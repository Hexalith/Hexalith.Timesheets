- source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
  summary: Codex hooks hard-code this machine path and still call deleted bmad-story-automator.
  evidence: `.codex/hooks.json` Stop/SessionStart commands use `/home/administrator/projects/hexalith/timesheets/...`; the first Stop entry still execs `.agents/skills/bmad-story-automator/scripts/story-automator`, which is not on disk.

- source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
  summary: BMAD party-mode, forge-idea, and customization-merge tests do not pin the 6.12 override behavior.
  evidence: Override tests check only name/source; party-mode has no non-string member case; `--project-root` tests stub the resolver so `structural_merge` never runs.

- source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
  summary: bmad-loop hook can crash on non-UTF-8 stdin.
  evidence: `json.load(sys.stdin)` in `.bmad-loop/bmad_loop_hook.py` catches JSONDecodeError and ValueError only, not UnicodeDecodeError.

- source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
  summary: bmad-loop event filenames are not sanitized for path separators.
  evidence: `_write_event` uses `{ts}-{task_id}-{event_name}.json` with POSIX dir_fd open; a `task_id` containing `..` can walk out of the events directory.

- source_spec: _bmad-output/implementation-artifacts/spec-move-submodules-to-references.md
  summary: Copilot agent wrappers do not cover every installed BMAD skill.
  evidence: Updated skills such as bmad-agent-tech-writer and bmad-architecture have no matching `.github/agents` entry after the BMAD installer refresh.

- source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
  summary: Historical Epic 5 planning artifacts still describe sibling-only Works checkout ownership.
  evidence: `epics.md` Story 5.3, `epic-5-context.md`, and `sprint-change-proposal-2026-07-20.md` still require no Timesheets Works gitlink and default `HexalithWorksRoot` to `../Hexalith.Works` only. Live docs now pin `references/Hexalith.Works`.

- source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
  summary: The move-spec review log still describes Story 5.3 as draft or backlog.
  evidence: `spec-move-submodules-to-references.md` BH3/BH4/BH8/BH14 and VG1 were written before this story’s tests and doc sync landed.

- source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
  summary: Move-spec `DependencyDirectionTests` assertions miss some slash and quote forms of a root Exists probe.
  evidence: The added `Exists('$(MSBuildThisFileDirectory)Hexalith.` substring does not catch `/Hexalith.` or `\\Hexalith.` variants. Those assertions came from the parallel move-spec review.

- source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
  summary: The managed bmad:context block is stale relative to the Story 5.3 Works checkout pin.
  evidence: `AGENTS.md`, `CLAUDE.md`, and `.github/copilot-instructions.md` never state the `references/Hexalith.Works` rule, still say README omits Works.Tests, pin the superseded SDK `10.0.302`, and rewrote the shared-baseline sentence. Fixes that edit those agent-context files are deferred.

- source_spec: _bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md
  summary: Move-spec ScaffoldGovernanceTests treat any path starting with `references/` as under references/.
  evidence: `references/../Hexalith.Works` would pass `StartsWith("references/")`. That assertion is a parallel move-spec review edit, not this story’s Works checkout class.

## Deferred from: code review of spec-5-3-consume-umbrella-owned-hexalith-works-checkout (2026-09-12)

- The `bmad:context` block was added to `AGENTS.md`, `CLAUDE.md`, and `.github/copilot-instructions.md` although this story deferred editing those files, and it ships stale: it never states the `references/Hexalith.Works` rule the story exists to pin, says "README omits Works.Tests", pins the superseded SDK `10.0.302`, and rewrites a sentence the shared baseline header declares normalized across the Codex, Claude, and Copilot entry points.
- The `references/Hexalith.EventStore` pin bump changes the AppHost's composed security topology (`AddHexalithEventStoreSecurity` now creates generated secret parameters and injects `HEXALITH_EVENTSTORE_CLIENT_USERNAME`/`_PASSWORD` into Keycloak), guarded only by a text read of `Program.cs` in `ScaffoldGovernanceTests`. An Aspire hosting test belongs to a later infrastructure story, since the AppHost is deliberately a minimal scaffold.
- `docs/launch-readiness.md:21` misstates the imported Builds catalog: it claims Dapr `1.18.4`, Aspire Hosting `13.4.6`, `Microsoft.FluentUI.Components` `4.11.6`, while the catalog at both the old (`35c3d1e`) and new (`a32cb42`) pin has `Dapr.AspNetCore` `1.18.5`, `Aspire.Hosting` `13.5.3`, `Microsoft.FluentUI.AspNetCore.Components` `5.0.0-rc.5-26219.1`. Story 5.2 evidence, already wrong at baseline.
- `src/Hexalith.Timesheets.AppHost/KeycloakRealms/hexalith-realm.json` hardcodes `admin-pass`, `tenant-a-pass`, `tenant-b-pass`, `readonly-pass`, and `no-tenant-pass`, where the upstream file it was copied from uses `__HEXALITH_*_PASSWORD__` substitution placeholders. Pre-existing and outside the diff; the EventStore pin bump widens the drift.

- source_spec: `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md`
  summary: Reconcile the overall launch-readiness verdict while Stories 3.6 and 5.1 remain open.
  evidence: The approved 2026-09-12 correction requires `FAIL` until valid-link Magic-Link resolution and final Story 5.1 documentation synchronization complete, while the pre-existing launch record still says `CONCERNS`; final release classification belongs to Story 5.1 rather than this package-baseline story.
