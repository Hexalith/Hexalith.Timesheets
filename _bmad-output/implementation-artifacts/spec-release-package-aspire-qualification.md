---
title: 'Qualify published Aspire alignment on the Release package graph'
type: 'chore'
created: '2026-10-28'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Qualify Timesheets commit `94d0a6cd1182b6c9f3ee3392e016cab4004eb3b8` on its Release/published-package graph. Inspect CI results and artifacts first, reuse successful source-bound verification, and investigate missing or failed lanes in an independent checkout at all published root gitlinks. Inspect evaluated package references, imported catalog and AppHost SDK before changing anything. Where CI evidence is insufficient, execute the requested matching Release/package solution restore and warnings-as-errors build. Obtain successful results from all six suites individually and serially and existing AppHost checks with `--configuration-only --configuration Release`, without starting resources.

Fix only demonstrated package/build alignment defects; root package floors and AppHost configuration are authorized where necessary. Preserve HTTP claims-read regressions, warnings/assertions, topology, gitlinks, existing workspace, dependency checkouts and foreign runtime sessions. Do not initialize nested submodules, broadly upgrade dependencies or mix Release/package and Debug/source assets. Update the report and ledger with exact commands, complete source identities, results and limits. Preserve historical failures, launch FAIL, deployed/native gates and the unclassified earlier foreign-process disappearance; do not claim continuous runtime preservation. Complete independent review, Conventional Commit validation and push the resulting changes to main, as expressly approved by the user.

</frozen-after-approval>

## Implementation Notes

- No intent gaps. Scope is qualification evidence plus only any demonstrated alignment defect. Existing approval explicitly covers commit/push. Use the oneshot route as in the preceding build qualification.
- Original workspace main is clean at the target SHA. Implementation uses `/tmp/timesheets-release-qualification-20261028`; evidence staging is `/tmp/timesheets-release-evidence-20261028`. Thirteen independent dependency clones check out exact published gitlinks; no nested gitfiles are present. Setup, original-workspace identities and runtime observations are recorded before qualification.
- CI inspected before local graph/build work. AppHost job `122756179985` of CI run `40670628561` succeeds with all 67 configuration-only Release probes and no resources. Downloaded artifact archive matches its published SHA-256. Reuse it. Main CI restore/build/consumer-validation steps succeed but tests are still running and their artifact/logs are unavailable at initial inspection. CodeQL and commitlint pass. Final report must distinguish completed evidence from the initial in-progress observation.
- Relevant sources: `Directory.Packages.props`, `Directory.Build.props`/`.targets`, `global.json`, AppHost project, imported `references/Hexalith.Builds/Props/Directory.Packages.props`, root CI and pinned reusable workflow. Reuse existing six suite executables and configuration script. No tests added for evidence-only work.

- Evaluated Release graph contains 16 Timesheets nodes and no sibling dependency source-project nodes. Actual catalog hash is `f9c97805fe540f17277f0d96f16a92b5f595f24c0f059c7a2c864abe8a6c1fb5`; SDK/catalog/Hosting/AppHost/Dashboard/DCP align at 13.6.4. Published EventStore.Aspire is 3.128.2. Required isolated restore passes in 4.016 seconds and build in 6.701 seconds with zero warnings/errors. No demonstrated alignment defect; no configuration/package/source changes are justified.
- Source manifests and all 2,021 Release outputs are retained. A package hash audit initially compared signed archive and original content digests; corrected comparisons pass independently for all eight AppHost package identities, matching NuGet's documented distinction. This was an audit assumption error, not a package defect. No package cache was changed by that audit.
- Locked `npm ci` in the isolated checkout and `npm run commitlint -- --edit /tmp/timesheets-release-evidence-20261028/commit-message.txt` succeed; tracked lock/manifest inputs remain identical. Install output retains two tooling advisories; no dependency upgrade or new audit verdict is made.
- Original workspace and all thirteen dependency states still match baseline. Current read-only runtime samples match 17 of 22 baseline numeric PID/start/name identities; five dotnet identities are absent. Their ownership, disappearance cause and timing remain unclassified. Earlier 51/66 disappearance stays historical and unresolved. No lifecycle/state command was issued; continuous foreign runtime preservation is not established.
- CI build-and-test succeeds at 2026-10-28T10:55:18Z. Complete test artifact inspection is in progress. The configured coverage gate defaults false and is not overridden; the initial audit's contrary coverage inference is preserved with an explicit correction. No suite was rerun locally while source-bound CI was running.
- Preservation observation at 2026-10-28T10:55:49Z matches 4/22 baseline runtime identities with 18 absent, superseding the initial 17/22 (five absent) count for that sample only. Cause/timing/ownership remain unclassified. Original workspace/dependency states, build inputs and all 2,021 Release outputs still match. No continuous-runtime claim is made.
- Final CI artifact `12809709991` digest matches GitHub metadata. All six TRX suites run individually without interval overlap and pass 2,785/2,790 tests with five existing skips and zero failed/error/timeout/aborted; all nine HTTP claims cases pass. Actual CI coverage flag is false. Successful tests/configuration/consumer verification are reused without local duplication. Report and ledger append the exact commands, source identities, raw artifacts and remaining limits; historical contents remain byte prefixes.
- Source CI completes successfully at run update 10:57:30Z, including evidence-verifier job 122774108074. Timing clarification: main job API completes 10:55:17Z, check-run 10:55:18Z. Existing repository TRX validator also passes locally against downloaded results without test execution. CI provides Release assembly paths but no compiled-assembly hash manifest; source binding relies on workflow/checkout/artifact provenance. No internal test scheduling assertion is made.

- Review corrections supersede the earlier shorthand: the retained outputs are the 2,021-file **hash manifest**, not compiled binaries in the bundle. Main job and check-run both complete at 10:55:17Z; 10:55:18.766795Z is the observation time. Five skips are three opt-in facts and two unconditional placeholder facts, not five opt-in facts. The central Aspire.Hosting.Testing pin is evaluated at 13.6.4 but is absent from restored libraries. No package/build/test change is needed.
- Review adds explicit staging of ignored evidence logs, an aggregate failure gate to the command reproduction, a portable read-only ZIP/metadata/variant verifier and a map reconstructing derived core fields. Historical helper scripts remain unchanged as execution transcripts with their prerequisites documented. Final preservation receipts will be refreshed after document finalization.

## Review Triage Log

- **BH-1 — medium, patched:** Twelve manifest-listed log files are ignored by Git and ordinary staging omits them. Explicitly force-stage only the evidence bundle and compare every manifest entry to Git index bytes before commit; no ignore-rule change.
- **BH-2 — low, patched:** Check-run completion 10:55:18 is observation time; raw completion is 10:55:17. Correct report and append explicit spec clarification with raw check completion and observation timestamps.
- **BH-3 — medium, patched:** Five skips were all described as opt-in, but two are unconditional placeholders. Report/ledger classify three opt-in facts and two unconditional placeholder facts; spec appends correction. No skip assertion or runtime gate changes.
- **BH-4 — medium, patched:** Preservation receipt hashes predate the completed report and ledger. Refresh preservation after final report/ledger content; preserve interim/pre-review observations separately and verify document hashes in final bundle check.
- **BH-5 — low, patched:** Spec calls 2,021 Release outputs retained, while only their hash manifest is in the evidence bundle. Append explicit correction to agent-owned implementation notes, preserving chronology; report and bundle README say binaries remain in isolated checkout and hashes are retained.
- **BH-6 — low, patched:** Reproduction loop can hide an earlier failed suite when the final suite passes. Aggregate failures and return nonzero after all six invocations; actual source CI already has a blocking gate and remains unchanged.
- **BH-7 — low, patched:** Recorded parser requires a hardcoded temporary extraction directory absent from exported bundle. Document exact original prerequisite and historical-helper scope; add portable read-only verifier consuming retained ZIP directly with an evidence-directory argument.
- **BH-8 — low, patched:** Historical parser assigns source/run constants without checking archive metadata/digest. Retain historical transcript, document that it alone is not source attestation; portable verifier obtains source/run from raw API and checks checkout/artifact metadata plus original ZIP digest before parsing.
- **BH-9 — low, patched:** Historical parser accepts nine matching claims rows without exact unique expected variants. Portable verifier requires the exact five accessor/two cleanup/two issuance names, unique test/execution identities and Passed outcomes. Raw artifact already contains all expected variants.
- **BH-10 — low, patched:** Construction of derived provenance/final summaries is not retained as a reproducible helper. Add input-to-field reconstruction map and portable independent comparison/rederivation of core fields from raw metadata/log/TRX inputs; authored explanatory fields remain explicitly interpretation.
- **EV-1 — low, patched:** Report describes Aspire.Hosting.Testing as restored; only its central PackageVersion evaluates to 13.6.4. Distinguish central Testing pin from actual restored packages; no dependency change or retest warranted.

No review finding is deferred. All corrections concern qualification records and evidence delivery; no production code, test assertion, package pin, AppHost configuration or gitlink changed. Final raw-artifact/manifest/index and preservation checks follow these document corrections.

- Both independent final rechecks are clean with no finding deferred. The final portable verifier also passes in a shallow exported directory outside the repository; safe ancestor discovery avoids path-depth assumptions. Final staging includes every manifest entry, including raw ignored logs, with byte-for-byte index verification and no gitlink changes.
