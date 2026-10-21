---
title: 'Pin BMAD customization override behavior'
type: 'chore'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The next unresolved deferred-work entry reports that party-mode and forge-idea tests do not protect BMAD 6.12 override semantics. Omitted installed persona fields, malformed party member tokens, and real three-layer customization merging can regress while the existing tests pass.

**Approach:** Strengthen both installed test suites and their matching `.claude` copies. Verify omitted installed fields survive persona overrides, malformed tokens remain unresolved without disrupting valid members, and each wrapper uses the real customization resolver with an explicit project root. Pin default/team/user scalar precedence, keyed member/group replacement and order, and ordinary array appending. Record resolution in the deferred-work ledger after verification. Commit and push this increment on main as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation: `build_collective` and `build_pool` already copy installed fields correctly; party `resolve_members` already reports malformed tokens. The gap is regression coverage, so runtime resolver behavior needs no change.
- Change the two `scripts/tests/test_resolve_*.py` files under `.agents/skills/bmad-party-mode` and `.agents/skills/bmad-forge-idea`, and mirror them under `.claude/skills`. Use the existing stdlib unittest conventions and UTF-8/LF formatting.
- For integration coverage, copy the real `_bmad/scripts/resolve_customization.py` and `config_utils.py` into a temporary project, write base/team/user TOMLs, and run each wrapper loader with a different working directory containing decoy overrides. Do not mock the subprocess or merge result.
- Scope: four test files, this spec, and the selected ledger entry. No new runtime API, dependency, domain state, or AppHost change; no unresolved intent decisions or migrations.
- Baseline at `e42c05380b55f5d6baad41399f7f40fa2b077dd9`: party-mode 19 tests and forge-idea 17 tests pass. Verify all four changed suites, mirrored file equality, and representative temporary mutations that remove the protected behavior. Review before committing.
- Implemented: full installed-entry assertions, omitted-name preservation, explicit empty icon/title overrides, mixed malformed/valid party-member resolution, and real subprocess merge integration in both wrappers. Each test fixture copies the repository's actual resolver and merge helper; no merge result or subprocess is mocked in the integration cases.
- Verification: `python3 -B .agents/skills/bmad-party-mode/scripts/tests/test_resolve_party.py` passes 23 tests; `python3 -B .agents/skills/bmad-forge-idea/scripts/tests/test_resolve_personas.py` passes 20. The matching `.claude/skills/` commands pass the same counts, for 86 total executions. `git diff --check` passes.
- Mutation verification in disposable copies caught 17/17 faults: for each wrapper, dropping installed fields, ignoring empty overrides, omitting the explicit root, appending keyed arrays, recursively merging keyed records, replacing ordinary arrays, reversing scalar precedence, and ignoring group IDs; party-mode also rejected removal of its non-string member guard. Each targeted new test ran once and failed as expected. Temporary copies were removed automatically.
- Review patches use nonalphabetical member codes and ordinary-array values, repeat a fact across layers, assert renamed-member lookup, cover full codes/mixed-case aliases/display names, and clear all supported override fields after setting custom persona/model/capabilities. The four suites still pass 86 executions and remain byte-identical across install roots.
- Expanded mutation verification caught 35/35 faults, including all initial 17 plus sorting ordinary/keyed arrays, deduplicating ordinary arrays, skipping custom-name registration, removing case-insensitive override matching, and individually ignoring empty name/persona/model/capabilities in each wrapper.
- Final independent patch verification confirmed all six in-scope findings fixed, reran 86 test executions and the reviewer's six previously surviving mutations successfully, checked mirror equality and whitespace, and found no remaining concrete issue. CI discovery stays separately deferred. The Conventional Commit message passed local commitlint.

## Review Triage Log

- BH1 — medium, defer: `.github/workflows/ci.yml` discovers only `scripts/tests` for Python tooling; neither installed skill suite is invoked there. This pre-existing automation gap is outside the selected test-suite increment and has a new deferred entry.
- BH2 — medium, patch: alphabetically ordered facts let sorting pass. Fixtures now use deliberately unsorted facts; sorting mutations fail in both wrappers.
- BH3 — medium, patch: unique facts let deduplication pass. A shared fact now occurs in base and team layers and is expected twice; deduplication mutations fail.
- BH4 — medium, patch: alphabetical member codes hid keyed-array sorting. Fixtures now append `z-base`, `a-team`, and `m-user` in that order after the replaced analyst slot; sorting mutations fail.
- BH5 — medium, patch: override tests discarded the index. They now assert `mary-custom` resolves to the installed canonical slot; skipping custom-name registration fails.
- BH6 — medium, patch: lowercase-only override inputs missed case-insensitive matching regressions. Subtests cover alias, mixed-case alias, full code, display name, and mixed-case display name; removing the lowercase fallback fails.
- BH7 — medium, patch: empty-field tests covered only icon/title. They now set nonempty custom persona/model/capabilities before clearing every supported field, including empty name and capabilities. Each corresponding individual mutation fails.
