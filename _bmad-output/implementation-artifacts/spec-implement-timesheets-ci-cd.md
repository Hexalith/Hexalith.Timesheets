---
title: 'Implement Timesheets CI/CD and NuGet publication verification'
type: 'feature'
created: '2026-09-18'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '7b169cfab577b6455aacb6ac097c8167b5578519'
context:
  - 'references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '_bmad-output/planning-artifacts/architecture.md'
  - 'docs/launch-readiness.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Timesheets has no CI, release, dependency-update, commit-message, package inventory, or package-consumer validation configuration, and none of its five intended NuGet packages is currently present on nuget.org. Its Release graph also cannot yet be package-only because `Hexalith.Works.Contracts` is unpublished.

**Approach:** First complete the separately owned Works CI/CD prerequisite and verify its five published packages; then apply the common Tenants/EventStore/FrontComposer model to Timesheets: thin pinned Hexalith.Builds workflow callers, Debug source references and Release NuGet references, an authoritative five-package manifest, isolated package validation, a manual protected semantic release, and explicit registry verification.

## Boundaries & Constraints

**Always:** Require the exact published `Hexalith.Works.Contracts` version before activating Timesheets CI. Build Release with SDK `10.0.401`, warnings as errors, NuGet audit enabled, Central Package Management, package-only cross-repository dependencies, and all six test projects run individually. Publish only Contracts, Client, Server, Projections, and Testing; require exact green `main`, a protected `production` environment, `NUGET_API_KEY`, an immutable Builds SHA, and `HEXALITH_RELEASE_PUBLISH_ENABLED=true`. Review Timesheets implementation locally before any separately authorized commit, push, or release dispatch.

**Never:** Add a temporary Works source-reference exception, omit Works.Tests, merge a knowingly red Timesheets workflow, or implement the separately owned Works prerequisite in this spec. Do not publish AppHost, host, ServiceDefaults, Works adapter, or tests; use recursive submodule updates, inline package versions, `--skip-duplicate`, mutable release workflow refs, automatic release-on-push, copied product-specific FrontComposer/EventStore governance, or silently treat a `404` registry response as published.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| CI | Push or PR to `main` | Restore/build Release, run every Timesheets test project, pack exactly five packages, validate archives and isolated consumers | Any missing test result, dependency leak, package mismatch, warning, or failing test fails CI |
| Release | Manual dispatch at exact green `main` | Semantic version, validated packages, NuGet publication, tag and GitHub Release | Stale source, missing approval/secret, occupied version, manifest drift, or frozen publication fails closed or explicitly skips before side effects |
| Registry check | Five manifest IDs and expected release version | Every ID resolves from the official NuGet v3 feed at the exact version | Missing/partial/mismatched publication is reported and the verification job fails |

</frozen-after-approval>

## Code Map

- `Directory.Build.props`, `Directory.Build.targets`, `Directory.Solution.props`, `Directory.Packages.props`, `global.json`, `src/*/*.csproj` -- introduce Debug/source versus Release/package dependency mode; replace unconditional EventStore/Works project references and enable compliant NuGet auditing.
- `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj` -- preserve the existing AppHost and topology while making its external compile dependencies conditional source/package references; retain executable resource paths without bringing sibling source projects into the Release compile graph.
- `Hexalith.Timesheets.slnx`, `tests/*/*.csproj` -- mandatory 15-project build and six-project xUnit v3/Microsoft.Testing.Platform inventory; Works.Tests cannot be omitted.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DependencyDirectionTests.cs` -- stale rule currently forbids all sibling package references; preserve layer boundaries while allowing conditional Release dependencies.
- `references/Hexalith.Builds/.github/workflows/domain-{ci,release}.yml` -- shared execution contract; use the reviewed root-pinned Builds commit, not copied workflow logic.
- `references/Hexalith.Works/_bmad-output/implementation-artifacts/spec-implement-works-ci-cd.md` -- separate ready-for-development prerequisite; its package publication must complete before Timesheets Release validation, but its implementation is outside this spec.
- `references/Hexalith.{Tenants,EventStore,FrontComposer}/.github/workflows/{ci,release}.yml` -- reference callers for exact-source gating, test tiers, protected release, and publication checks; omit their module-specific lanes and containers.
- `references/Hexalith.Tenants/{tools,scripts}` -- closest five-package manifest/packer/archive/consumer pattern; do not copy its broad `.ServiceDefaults` rejection or historical 4.x floor.
- `.github/`, `.releaserc.json`, `package*.json`, `commitlint.config.mjs`, `tools/`, `scripts/` -- absent Timesheets CI/CD surface to create.
- `docs/ci-cd.md` -- record the selected shared model, required repository settings, package inventory, and dated official-registry evidence.

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Build.props`, `Directory.Packages.props`, affected `src/*/*.csproj`, architecture tests -- implement and prove Debug/source and Release/package modes without weakening ownership boundaries.
- [x] `tools/release-packages.json`, `scripts/pack-release-packages.py`, `scripts/validate-nuget-packages.py`, `scripts/validate-consumer-package-references.py` -- make one fail-closed five-package contract and isolated package-only consumer gate.
- [x] `.github/workflows/ci.yml`, security/commitlint callers, `.github/dependabot.yml` -- add pinned shared CI, every test lane, consumer validation, CodeQL, dependency review, and Conventional Commit enforcement.
- [x] `.github/workflows/release.yml`, `.releaserc.json`, release preflight/verification scripts, `package*.json` -- add manual exact-source protected NuGet release and exact-version post-publication verification.
- [x] `docs/ci-cd.md` -- document operation and the official NuGet findings for Timesheets plus its release-blocking dependencies.

**Acceptance Criteria:**
- Given the separately released Works package is visible from the official NuGet feed, when CI-equivalent Release validation runs from a clean checkout, then restore/build and all six test executables pass with zero failures and package validation produces exactly five consumable packages.
- Given Debug source mode and Release package mode, when MSBuild evaluates cross-repository dependencies, then only Debug uses root-declared submodule projects and Release uses NuGet packages.
- Given a release dispatch, when source, CI, environment, manifest, version, secret, or publication-freeze checks are unsatisfied, then no tag or NuGet write occurs.
- Given a completed release, when the official NuGet v3 indexes are queried, then all five manifest IDs expose the exact released version or verification fails with the missing IDs.

## Implementation Notes

### Prerequisite resolved and implementation resumed — 2026-10-18

The user reported Works publication and resumed this task. A fresh manifest-driven
query of the official NuGet v3 flat-container indexes returned HTTP 200 with
version `1.0.1` for **all five** Works packages: Contracts, Server, Projections,
Reactor, and Testing. Pin the required Works Contracts dependency to this exact
published version through Central Package Management. The 2026-10-16 blocker
below is historical and no longer blocks implementation.

Resume HEAD is `edcbba9219b20f0c9cf18db9637467065fc5144e`; preserve the original
`baseline_commit` frontmatter. The reviewed root Builds pin is now
`544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9`; consume its actual workflow input
contract. User authorization to proceed alongside unrelated working-tree changes
persists: preserve the modified deferred-work ledger and untracked EventStore
restart-timeout investigation. No commit, push, publication, release dispatch,
submodule update, or repository-setting mutation is authorized.

### Local implementation checkpoint — 2026-10-18

Implemented the conditional Debug/source and Release/package graph, exact published
Works Contracts `1.0.1` CPM entry, enabled NuGet auditing, MTP runner selection,
conditional AppHost compile metadata, five-package contract and final-version
packer, restore-backed archive validation, isolated package consumers, pinned
CI/security/commitlint callers, root-only dependency surveillance, manual protected
release, pre-publication identity/checksum gates, and exact-version registry
verification. `Directory.Solution.props` is necessary because the shared workflow's
bare solution restore otherwise supplies Debug before project props can choose a
Release graph. The original baseline and frozen intent remain unchanged.

A current-source startup check after dependency metadata edits (not a pristine
pre-edit baseline) used the exact Timesheets AppHost: `aspire start`, `aspire
describe`, and `aspire stop`, each with `--apphost
src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj`, with
`--non-interactive` for start/stop and
`DOTNET_CLI_HOME=/tmp/dotnet-cli-home`. Timesheets, EventStore and both Dapr CLI
resources were Running/Healthy; stop succeeded. `Program.cs` and topology are
unchanged. No further live journey is claimed.

The initial package-only Release Integration run completed 732 cases in
7m 56.871s: 726 passed, one failed and five declared skips. Its race fixture
started two HTTP tasks without ensuring both reached commit submission before
either stored terminal state; a valid earlier loader denial could leave zero
rejected commit events. A bounded test-only pre-submit barrier now forces the
intended overlap while preserving every exact persisted-event assertion and the
stalled-response scenario. The focused rebuilt fact passes; the full rerun is
recorded separately below when complete. No production logic changed for this fix.

The Node lock was initialized from the sibling release toolchain and refreshed
within compatible upstream constraints. Signature/attestation and commit policy
checks pass. Compatible bundled npm refresh to `11.21.1` removed the critical
Handlebars and bundled-dependency findings. `npm audit` retains eight high findings,
all from the unpatched braces/micromatch chain and semantic-release dependents.
This is a real activation/merge blocker under the unchanged high-severity
dependency-review gate. The registry's latest braces `3.0.3` and micromatch `4.0.8`
have no compatible fixed release. No forced downgrade or broad override was used.
`npm run release:dry-run` currently refuses with ENOGHTOKEN
because this local execution supplies no GitHub token. Hosted production settings,
secrets and publication enablement are still external prerequisites, documented in
`docs/ci-cd.md`; no hosted activation or publication was attempted.

### Read-only prerequisite investigation — 2026-10-16

Implementation is blocked by the approved Works-publication prerequisite. No CI/CD
implementation assets, dependency configuration, workflow activation, commit, push,
release dispatch, repository setting, or submodule pointer was changed by this
investigation. Preparing dependent local assets before Works publication would
change the approved execution order and remains pending an explicit user decision.
The frozen intent, baseline commit, status, and acceptance checkboxes remain intact.

Observed source identities:

| Repository | Exact revision |
|---|---|
| Timesheets investigation HEAD | `ee95f90f529812c744761090691a23196c971c78` |
| Root-pinned Hexalith.Builds | `0e3775b8b00685d8bc87b09135b2475ae867e131` |
| Root-pinned Hexalith.Works | `8679d7d700925a870e9931b3c43c745c063e459b` |
| Root-pinned Hexalith.EventStore | `746b843a24f18b17a7572be25b76b735fa8e4ca6` |

The current Works manifest, read from
`references/Hexalith.Works/tools/release-packages.json`, declares **Contracts,
Server, Projections, Reactor, and Testing**. It does not declare a Client package.
At **2026-10-16T12:53:51Z**, every manifest ID returned **HTTP 404** from its
official NuGet v3 flat-container index. In particular, no exact published
`Hexalith.Works.Contracts` version can currently be selected. All five intended
Timesheets IDs also returned HTTP 404 during the initial registry probe; absence
is recorded as unpublished, never successful publication.

At **2026-10-16T12:55:11Z**, the official indexes for
`Hexalith.EventStore.{Aspire,Client,Contracts,DomainService,Server,ServiceDefaults,Testing}`
all returned HTTP 200 and contained exact version **3.122.0**, matching the pinned
Builds catalog. API inspection of published DomainService 3.122.0 found
`IAsyncDomainSharedProjectionStreamedOwnerHandler` and
`IAsyncDomainSharedProjectionRebuildHandler`. This proves those named types are
published; it does not prove whole-solution compilation or runtime compatibility.

Read-only MSBuild evaluation of all 15 solution projects with
`Configuration=Release` and `UseHexalithProjectReferences=false` succeeded, but
still selected **19 cross-repository ProjectReference edges** and reported
`NuGetAudit=false` in every project. Exactly Contracts, Client, Server,
Projections, and Testing are already packable. The SDK resolves to **10.0.401**;
the local Node/npm versions are **26.4.0 / 11.18.0**. No restore, build, or test
acceptance result is claimed by these evaluation-only checks.

Proposed adjustments after the execution order permits implementation:

- Define one validated Debug/source versus Release/package dependency mode in
  `Directory.Build.props`, including consistent configuration-free restore
  behavior, and enable NuGet auditing. Convert every affected source and test
  project edge to a conditional ProjectReference/PackageReference pair. Preserve
  local Timesheets layer edges and the existing Contracts/Works ownership rules.
  Add the eventual exact published Works Contracts version through Central
  Package Management; the current imported catalog has no Works entry. Do not
  invent a version or use a temporary Works source exception.
- Explicitly include
  `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj` in any
  approved Code Map adjustment. Its two current unconditional EventStore source
  edges target the Aspire library and gateway executable. Package-mode compilation
  needs a published Aspire PackageReference and must avoid the executable source
  edge. Preserve `Program.cs`, declared resources, and topology. Verify the
  published Aspire integration's runtime expectations before claiming Release
  AppHost compatibility. No AppHost edit is authorized by these notes alone.
- Select `Microsoft.Testing.Platform` in `global.json` before using the shared
  `test-platform: microsoft-testing-platform` contract; the current root
  `global.json` pins only the SDK. Keep all six projects explicit, including
  Works.Tests. The current default IntegrationTests inventory contains **five**
  declared skips: two static reservations, two `TIMESHEETS_PERF` opt-ins, and the
  newer `TIMESHEETS_LIVE_DAPR` opt-in. Reconcile the Verification section's older
  four-skip expectation with the human-owned acceptance intent before completion;
  do not remove or hide a skip to match the stale count.
- Call the shared CI and release workflows at immutable Builds revision
  `0e3775b8b00685d8bc87b09135b2475ae867e131`. Its CI contract supports individual
  MTP project lists, missing-result failure checks, and the three package scripts.
  Its release contract supports `production`, an explicit five-package count,
  `NUGET_API_KEY`, and the exact-string publication freeze. Use
  `require-publication-authority: false` for this spec's protected-environment
  minimum; the default `true` requires additional issue-bound reservation
  governance outside this spec. A NuGet-only caller must supply its own source,
  inventory, destination-collision, and publication verification scripts: the
  shared container action installs its preflight helper only when containers are
  enabled, and containers are outside this spec.

## Spec Change Log

## Review Triage Log

All three independent review layers completed. B3–B10, E1–E2 and V1–V3
were patched and verified; B2 was refuted against the actual caller/shared-workflow
freeze, and B1 was recorded in the deferred-work ledger as an external upstream
blocker. This completion covers the reviewed local implementation. Hosted
activation/publication, the authenticated dry-run and resolution of the upstream
advisory remain prerequisites; no commit, push, settings change or release was
authorized. The explicit spec restriction on separately authorized Git operations
takes precedence over the build workflow's generic local-commit step.

Post-review checks: the parent reran all **67 Python tests**, archive validation,
all-six TRX validation, actionlint, Python compilation and all five isolated
candidate consumers. Consumer evidence is retained at
`/tmp/timesheets-ci-parent-consumers.log`, and Python evidence at
`/tmp/timesheets-ci-parent-python.log`. The affected Architecture project rebuilt
without warnings/errors and its two dependency-mode facts passed at
`/tmp/timesheets-ci-review-dependency-mode.log`. The prior complete six-suite run
remains applicable: no production C# or domain behavior changed during review.
Release identity/checksum logs are flushed before writes; consumer cache archives
and local feed metadata match the candidate; no blanket framework reference is
supplied. All local patch checks passed, with the eight-high upstream advisory
retained as the explicit activation/merge blocker.

| ID | Verdict / route | Evidence and decision |
|---|---|---|
| B1 | high / defer | The new lock exposes the existing unpatched upstream braces advisory through eight metavulnerabilities. Compatible updates were exhausted; the unchanged dependency-review gate correctly blocks it. Retain as an external merge/activation blocker. |
| B2 | false / reject | The real caller requires its exact-string publication output to be true, and the pinned shared release checks the vars-context flag immediately before invoking semantic-release. Frozen runs never invoke these hooks. Hooks do not receive that flag in the shared execution contract; a standalone call is not evidence of a frozen workflow publishing. |
| B3 | medium / patch | check_source proves only ci.yml, although Commitlint and CodeQL are independent configured workflows. Reuse the existing exact-source run proof for those workflows so their red or pending results cannot accompany a green release decision. |
| B4 | medium / patch | inventory only requires the expected DLL and admits an injected AppHost DLL under another TFM. Reject extra payload assemblies and unexpected framework assets. |
| B5 | medium / patch | Archive parsing discards dependency include/exclude restrictions. Preserve the normal generated Build,Analyzers exclusion while rejecting restrictions that remove runtime/compile assets. |
| B6 | medium / patch | Both feeds are eligible for the same candidate identity. A fresh cache proves isolation from previous restores, but not which feed won. Map all five candidate IDs exclusively to the local feed and verify restored candidate bytes. |
| B7 | medium / patch | typeof(marker) checks assembly identity without compiling representative public signatures or exercising registration. Use small side-effect-free public API examples in each isolated consumer. |
| B8 | medium / patch | Every consumer supplies AspNetCore independently. Remove the blanket framework reference so published metadata supplies any needed framework. |
| B9 | medium / patch | The NuGet-only reusable job omits the container evidence uploader; identity and checksum ledger are otherwise ephemeral on failure. Preserve safe identity/checksum evidence in run logs before writes and include them as successful-release assets. No recovery automation is needed. |
| B10 | medium / patch | Negative archive tests target release_common rather than all production callers. Add focused packer/consumer rejection coverage and the specific restore-backed validator case below; positive real packing/consumer runs already pass. |
| E1 | medium / patch | http.client.IncompleteRead escapes the registry helper after one attempt, bypassing per-package JSON retention. Retry bounded HTTP protocol/body failures and test exhausted publication evidence. |
| E2 | medium / patch | Cancelling WaitForExitAsync disposes the Process handle but does not terminate its owned MSBuild child. Terminate the owned process tree on timeout/cancellation while preserving the original error. |
| V1 | medium / patch | Pre-verified: deleting the incomplete-result CLI failure guard leaves 49 tests green. Exercise the tagged main path and assert failure plus retained JSON. |
| V2 | medium / patch | Pre-verified: deleting the publisher's per-write source proof leaves 49 tests green. Change eligibility after one push and require no second write. |
| V3 | medium / patch | Pre-verified: an underdeclared dependency fails the real validator but no unittest imports that module. Test actual archive/assets version mismatch and its rejection. |

Review scope is the resumed CI/CD delta against
`edcbba9219b20f0c9cf18db9637467065fc5144e`. The original baseline is preserved
for provenance; its complete historical diff is retained separately at
`/tmp/timesheets-ci-review-y7eozz8s/full-provenance.patch`. Previously committed
domain changes and the user's existing investigation/ledger changes are not part
of this CI/CD implementation review. Review input is
`/tmp/timesheets-ci-review-y7eozz8s/ci-cd.patch`, including new files and the locked
Node dependency graph. No index changes were made to create either patch.

## Design Notes

Tenants supplies the closest domain-module shape, EventStore demonstrates larger package-boundary validation, and FrontComposer supplies NuGet-only publication plus independent evidence. Timesheets should use their common minimum, with five packages and no container, rather than clone repository-specific governance. Repository order is Works implementation, Works publication verification, Timesheets implementation, then separately authorized Timesheets publication. Before publication, dry-run semantic-release, validate the exact pinned Builds input contract, verify repository settings, and poll NuGet with bounded retries. Detect and report partial publication; do not build recovery automation unless it occurs.

## Verification

### Final local implementation verification — 2026-10-18

All locally feasible implementation and verification is complete on the resumed
working tree. Original `baseline_commit`, review iteration and frozen intent are preserved;
status subsequently advanced through review to local implementation completion. This is local uncommitted-tree evidence, not a
claim of a fresh hosted checkout, green dependency review, publication, or launch
readiness. The remaining Node advisory, protected-environment/secret/branch
settings and authenticated semantic-release dry-run prerequisites remain explicit.
Do not merge or activate a knowingly red dependency-review configuration.

| Check | Exact command / result | Retained local evidence |
|---|---|---|
| Package-only Release restore | `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:UseHexalithProjectReferences=false -m:1 /nr:false` passed with audit enabled; bare shared-workflow restore also passed | `/tmp/timesheets-ci-release-restore-final.log`, `/tmp/timesheets-ci-bare-restore.log` |
| Release build | `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Release --no-restore -warnaserror -m:1 /nr:false` passed, zero warnings/errors, 13.40s | `/tmp/timesheets-ci-release-build-final.log` |
| Debug/source restore and build | Matching `-p:Configuration=Debug -p:UseHexalithProjectReferences=true` restore and `--configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` build passed, zero warnings/errors, build 11.19s | `/tmp/timesheets-ci-debug-restore.log`, `/tmp/timesheets-ci-debug-build.log` |
| Architecture | 59 passed, no skips; final rerun after the targets-scope correction passed | `/tmp/timesheets-ci-architecture-final.log` |
| Contracts | 92 passed, no skips | `/tmp/timesheets-ci-Hexalith.Timesheets.Contracts.Tests.log` |
| Server | 558 passed, no skips | `/tmp/timesheets-ci-Hexalith.Timesheets.Server.Tests.log` |
| Projections | 437 passed, no skips, 8m20.840s | `/tmp/timesheets-ci-Hexalith.Timesheets.Projections.Tests.log` |
| Works | 76 passed, no skips | `/tmp/timesheets-ci-Hexalith.Timesheets.Works.Tests.log` |
| Final Integration | 727 passed, zero failures, five declared skips; 732 cases in 9m03.451s | `/tmp/timesheets-ci-integration-final.log` |
| Focused race regression | One rebuilt fact passed, then five consecutive reruns passed with no skips | `/tmp/timesheets-ci-race-focused.log`, `/tmp/timesheets-ci-race-repeated.log` |
| TRX completeness gate | `python3 scripts/validate-test-results.py /tmp/timesheets-ci-tests-final` passed for all six projects | `/tmp/timesheets-ci-tests-final/<Project>/<Project>.trx` |
| Final-version packaging | Packer and archive validator passed exactly five `.nupkg`, five `.snupkg`, one checksum ledger at `0.0.0-ci-test` | `/tmp/timesheets-ci-pack-final.log`, `/tmp/timesheets-ci-packages-20261018-final/` |
| Isolated consumers | All five private-cache CPM/PackageReference consumers built with zero warnings/errors, ran, and matched assembly informational version | `/tmp/timesheets-ci-consumers-final.log` |
| Python failure-path suite | `python3 -m unittest discover -s scripts/tests -v` passed 49/49, including malformed archives, source/protection/freeze, release identity/collision, five-push publisher/no-write refusals, CLI tag resolution and missing/failing/empty TRX | `/tmp/timesheets-ci-python-tests.log` |
| Node provenance / commit policy | `npm ci`, `npm audit signatures`, `npx commitlint --from HEAD~1 --to HEAD` passed; 504 registry signatures and 127 attestations verified; representative Dependabot prefixes passed | `/tmp/timesheets-ci-npm-bundled-refresh.log` |
| Static checks | Python compilation, `actionlint .github/workflows/*.yml`, current shared-workflow input comparison, and `git diff --check` passed | Local command output |
| Node vulnerability audit | `npm audit --json` still exits nonzero with eight high findings, all the unpatched braces/micromatch chain; upstream latest and compatible npm refresh were checked | `/tmp/timesheets-ci-npm-audit-latest.json` |
| Semantic-release dry-run | `npm run release:dry-run` exited 1 at `ENOGHTOKEN No GitHub token specified`; no publication occurred | `/tmp/timesheets-ci-semantic-dry-run.log` |

Each full suite used the actual shared-workflow MTP contract individually:
`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/<Project> --no-build
--configuration Release --results-directory <directory> --report-xunit-trx
--report-xunit-trx-filename <Project>.trx`. The final six reports total **1,954
cases: 1,949 passed, five skipped, zero failed**. Their measured combined suite
time is about 17m40s; the shared CI job retains a 30-minute cap. Hosted cold
checkout/restore overhead has not been measured.

The initial red Integration run is retained separately: 732 cases, 726 passed,
one race-fixture failure and five skips in 7m56.871s, with log
`/tmp/timesheets-ci-Hexalith.Timesheets.IntegrationTests.log` and TRX
`/tmp/timesheets-ci-tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.trx`.
The successful final rerun is in the `timesheets-ci-tests-final` directory and
does not overwrite that failure. Debug validation also exposed inherited module
policy on a sibling build-tool project; the Timesheets target now scopes itself
to Timesheets-owned project names, and the final Debug and Release builds pass.
No dependency repository was edited to fix that issue.

Official NuGet observations: all five Works manifest indexes expose `1.0.1`;
at **2026-10-18T12:17:05Z**, EventStore Aspire, Client, Contracts, DomainService,
Server, ServiceDefaults and Testing indexes all returned 200 and included exact
`3.124.0`; at **2026-10-18T12:06:37Z**, all five Timesheets indexes still returned
404. The 2026-10-16 unpublished-Works evidence below is retained as history.

**Read-only prerequisite commands and results (2026-10-16):**

The following manifest-driven registry probe was run at
`2026-10-16T12:53:51.673886+00:00`; it reported HTTP 404 for all five current Works
manifest IDs. A 404 means unpublished and does not satisfy the prerequisite.

```bash
python3 - <<'PY'
import datetime, json, urllib.error, urllib.request
from pathlib import Path

print('checkedAtUtc=' + datetime.datetime.now(datetime.UTC).isoformat())
manifest = json.loads(Path('references/Hexalith.Works/tools/release-packages.json').read_text())
for package in manifest['packages']:
    url = 'https://api.nuget.org/v3-flatcontainer/' + package['id'].lower() + '/index.json'
    try:
        with urllib.request.urlopen(url, timeout=30) as response:
            data = json.load(response)
            print(package['id'], response.status, data['versions'])
    except urllib.error.HTTPError as error:
        print(package['id'], error.code)
PY
```

The exact-version EventStore probe below returned HTTP 200 and `True` for each
of the seven IDs at `2026-10-16T12:55:11.437218+00:00`:

```bash
python3 - <<'PY'
import json, urllib.request

for area in ['Aspire', 'Client', 'Contracts', 'DomainService', 'Server', 'ServiceDefaults', 'Testing']:
    package_id = 'Hexalith.EventStore.' + area
    url = 'https://api.nuget.org/v3-flatcontainer/' + package_id.lower() + '/index.json'
    with urllib.request.urlopen(url, timeout=30) as response:
        print(package_id, response.status, '3.122.0' in json.load(response)['versions'])
PY
```

- `dotnet --version` → `10.0.401`; `node --version` → `v26.4.0`;
  `npm --version` → `11.18.0`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet msbuild <each of the 15 solution project paths> -p:Configuration=Release -p:UseHexalithProjectReferences=false -getProperty:NuGetAudit,IsPackable -getItem:ProjectReference`
  → all evaluations exited 0; audit remained false everywhere and 19 sibling
  ProjectReference edges remained. This is existing-state evidence, not an
  implementation pass.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dnx dotnet-inspect -y -- find '*SharedProjection*' --package Hexalith.EventStore.DomainService@3.122.0 -n 35`
  → exited 0 and listed the streamed-owner and rebuild interfaces above. The
  initial invocation with `--oneline` exited 1 with
  `Error: Unrecognized command or argument '--oneline'.`; the supported command
  above supplied the evidence.
- All three frontmatter context files were read fully. `.editorconfig`, root
  build/package configuration, the 15-project solution/test inventory, current
  architecture fitness rules, and pinned reusable-workflow contracts were
  inspected. Root `.gitattributes` and `Directory.Build.targets` do not exist.
- Required Release restore/build, six-suite execution, packing, isolated
  consumers, semantic-release dry-run, and hosted repository-settings validation
  have **not run**. The separately owned Works publication prerequisite is the
  current blocker, and the implementation task remains incomplete.

**Commands:**
- `npm ci && npm audit signatures && npx commitlint --from HEAD~1 --to HEAD` -- Node release tooling and commit policy are reproducible.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:UseHexalithProjectReferences=false -m:1 /nr:false` -- package-only Release graph restores with audit enabled.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Release --no-restore -warnaserror -m:1 /nr:false` -- zero warnings/errors.
- Run each built `tests/*/bin/Release/net10.0/*Tests` executable -- all six projects pass; the five existing performance/infrastructure/live-Dapr skips remain (the earlier four-skip count predates the live-Dapr fact).
- Run the packer and both validators against a synthetic version, then query every manifest ID through `https://api.nuget.org/v3-flatcontainer/<lower-id>/index.json` -- exact inventory and publication state are proven.
