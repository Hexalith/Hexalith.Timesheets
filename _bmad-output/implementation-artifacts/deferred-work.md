- source_spec: `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/spec-move-submodules-to-references.md`
  summary: Story 5.3 still needs Works-checkout fitness coverage and doc sync after the references layout move.
  evidence: Draft spec-5-3 requires WorksCheckoutGovernanceTests plus README, architecture, boundary-decision, launch-readiness, and sprint-status updates; those artifacts still describe root or sibling-only Works and list 5-3 as backlog.

- source_spec: `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/spec-move-submodules-to-references.md`
  summary: Hexalith submodule pins at HEAD differ from the pre-move commits.
  evidence: Relocate cfd9e62 kept most SHAs; HEAD pins (Works 42c4318, Commons 6da79ae, Builds 35c3d1e, and the other references/ gitlinks) come from later 830e44e / 9a799f2 / e3ea9ce updates.

- source_spec: `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/spec-move-submodules-to-references.md`
  summary: Codex hooks hard-code this machine path and still call deleted bmad-story-automator.
  evidence: `.codex/hooks.json` Stop/SessionStart commands use `/home/administrator/projects/hexalith/timesheets/...`; the first Stop entry still execs `.agents/skills/bmad-story-automator/scripts/story-automator`, which is not on disk.

- source_spec: `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/spec-move-submodules-to-references.md`
  summary: BMAD party-mode, forge-idea, and customization-merge tests do not pin the 6.12 override behavior.
  evidence: Override tests check only name/source; party-mode has no non-string member case; `--project-root` tests stub the resolver so `structural_merge` never runs.

- source_spec: `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/spec-move-submodules-to-references.md`
  summary: bmad-loop hook can crash on non-UTF-8 stdin.
  evidence: `json.load(sys.stdin)` in `.bmad-loop/bmad_loop_hook.py` catches JSONDecodeError and ValueError only, not UnicodeDecodeError.

- source_spec: `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/spec-move-submodules-to-references.md`
  summary: bmad-loop event filenames are not sanitized for path separators.
  evidence: `_write_event` uses `{ts}-{task_id}-{event_name}.json` with POSIX dir_fd open; a `task_id` containing `..` can walk out of the events directory.

- source_spec: `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/spec-move-submodules-to-references.md`
  summary: Copilot agent wrappers do not cover every installed BMAD skill.
  evidence: Updated skills such as bmad-agent-tech-writer and bmad-architecture have no matching `.github/agents` entry after the BMAD installer refresh.

- source_spec: `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md`
  summary: Historical Epic 5 planning artifacts still describe sibling-only Works checkout ownership.
  evidence: `epics.md` Story 5.3, `epic-5-context.md`, and `sprint-change-proposal-2026-07-20.md` still require no Timesheets Works gitlink and default `HexalithWorksRoot` to `../Hexalith.Works` only. Live docs now pin `references/Hexalith.Works`.

- source_spec: `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md`
  summary: An earlier deferred-work row still lists Story 5.3 fitness and doc sync as unfinished.
  evidence: The move-spec ledger entry says `WorksCheckoutGovernanceTests` and the 5.3 docs are missing; those artifacts now exist. This workflow cannot rewrite that existing row.

- source_spec: `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md`
  summary: The move-spec review log still describes Story 5.3 as draft or backlog.
  evidence: `spec-move-submodules-to-references.md` BH3/BH4/BH8/BH14 and VG1 were written before this story’s tests and doc sync landed.

- source_spec: `/home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/spec-5-3-consume-umbrella-owned-hexalith-works-checkout.md`
  summary: Move-spec `DependencyDirectionTests` assertions miss some slash and quote forms of a root Exists probe.
  evidence: The added `Exists('$(MSBuildThisFileDirectory)Hexalith.` substring does not catch `/Hexalith.` or `\\Hexalith.` variants. Those assertions came from the parallel move-spec review.
