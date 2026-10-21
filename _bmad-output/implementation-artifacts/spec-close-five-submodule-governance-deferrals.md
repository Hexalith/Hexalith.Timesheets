---
title: 'Close five deferred submodule-governance gaps'
type: 'chore'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferred entries from the Story 5.3 review still undermine the approved checkout rule: historical Epic 5 planning requires sibling-only Works ownership, the move-spec review notes describe Story 5.3 as unfinished, the dependency guard misses alternate root-probe spellings, agent guidance omits Works checkout ownership, and the submodule guard accepts traversal out of `references/`.

**Approach:** Close these five related governance entries together as requested. Align the current epic and all three managed agent-context blocks with Story 5.3's approved Option B: preserve explicit `HexalithWorksRoot`, prefer `references/Hexalith.Works`, retain the sibling fallback, and reject a repository-root Works checkout. Label the July proposal and original move-spec findings as historical and add dated current dispositions. Strengthen the architecture guards with separator/quote variants and lexical path normalization, including regression cases for escaping `references/`. Preserve historical evidence, frozen story intent, submodule pointers, build configuration, and runtime source. Verify with a focused Debug/source architecture build and its executable, record five ledger resolutions, review, then commit and push to `main` as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation baseline: `d2dca9533791b860bc041dd6ef982e24f2329619`, clean `main`. Story 5.3 spec and sprint status are already done; `epic-5-context.md` and architecture ownership are already correct.
- Route facts: no unresolved intent gaps or data operations; small, reversible changes confined to three architecture test files, synchronized managed guidance, two planning documents, the move-spec history, and deferred-work evidence. No public API or runtime behavior changes.
- Code map: `DependencyDirectionTests.Directory_build_props_detects_required_root_level_sibling_modules` has the literal probe check; `ScaffoldGovernanceTests.Nested_submodules_are_not_initialized_inside_root_level_submodules` has the raw prefix check; `LaunchReadinessTests.Managed_entry_points_match_configuration_and_projection_wiring` already verifies synchronized guidance. `WorksCheckoutGovernanceTests` already executes explicit, environment, references-first and sibling-fallback MSBuild evaluations.
- Validation scope: architecture tests own these changes; no domain or AppHost code is changed. Existing foreign Aspire sessions were observed through `aspire ps`; this test/documentation increment does not launch or stop shared resources.
- Implemented XML-condition probe inspection and lexical containment guards with 27 regression cases; added Works checkout assertions to the existing synchronized-guidance check. Reconciled the epic requirement and Story 5.3 criteria, prefaced the July proposal and move-spec findings with dated current dispositions, added the Works rule to all three managed contexts, and recorded five ledger resolutions.
- Verification: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` passed. The matching `dotnet build` with `--configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` passed with zero warnings/errors. The built `tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` executable passed all 92 tests, zero skips/failures, including existing MSBuild checkout evaluations.
- Independent guard experiments copied the built test executable and repository inputs into a temporary shadow root. Both original guards passed unchanged inputs; all 12 mutated configurations failed the actual repository-reading facts (seven quote/separator/dot-path probes and five escaping/root/prefix-lookalike submodule paths). No tracked build inputs or submodule content were mutated. Temporary copies were removed.
- `git diff --check` and byte comparisons of all three entry points passed. The pre-context shared baseline remains unchanged. `git fetch origin main` found no divergence at the investigation baseline.
- Independent review exposed additional literal-path and declaration syntax variants. The guard now asks Git to parse submodule paths, so compact, tab-separated, quoted and commented assignments cannot skip containment. MSBuild literal escapes are decoded once, opposite quote characters are accepted inside arguments, and quoted condition strings are skipped. These are private test helpers; the scope and runtime API remain unchanged.
- Final verification after review patches: the same warnings-as-errors Debug/source build passed with zero warnings/errors; the complete ArchitectureTests executable passed **103/103**, zero skips/failures, in 9.586 seconds. Shadow-root experiments rejected **20/20** forbidden configurations and accepted **5/5** allowed/baseline inputs. The independent reviewer rechecked all five findings as addressed and separately passed the 23 probe/config syntax cases. No review findings remain deferred.
- Commit validation: the repository excludes `chore` from commit types, so the initial proposed message was rejected. The corrected `test(governance): close five deferred checkout gaps` message passed `node_modules/.bin/commitlint --edit` before staging or committing.

## Review Triage Log

- BH1 — **medium / patch**: confirmed `git config` reads `path=references/../Hexalith.Commons` while the previous raw-line parser skipped it. Use Git's native parser with bounded process execution and cover compact, tabbed, concatenated-quote and comment forms.
- BH2 — **medium / patch**: a temporary real MSBuild project resolved `Hexalith%2eCommons/src`, `%2e/Hexalith.Commons/src` and `references%2f..%2fHexalith.Commons/src` to a forbidden existing root. Decode literal `%xx` once; keep `%252e` double escaping distinct. This is bounded literal decoding rather than computed-property evaluation.
- BH3 — **medium / patch**: the opposite quote is legal inside a quoted argument on supported filesystems; excluding both quote characters skipped an executable probe. Match the argument's own closing delimiter and add the demonstrated case.
- BH4 — **low / patch**: an `Exists(...)` example inside a quoted condition string is data rather than an executed probe. Skip condition string literals and test both delimiter forms to avoid false rejection.
- BH5 — **low / patch**: two historical duplicate ledger bullets still presented the selected guidance drift as current. Add explicit supersession notes under those bullets, preserving their original evidence. This annotates the selected entry's duplicates rather than closing an additional independent goal.
