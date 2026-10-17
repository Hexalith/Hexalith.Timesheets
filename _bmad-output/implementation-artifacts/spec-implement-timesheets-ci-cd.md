---
title: 'Implement Timesheets CI/CD and NuGet publication verification'
type: 'feature'
created: '2026-09-18'
status: 'in-progress'
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

- `Directory.Build.props`, `Directory.Packages.props`, `src/*/*.csproj` -- introduce Debug/source versus Release/package dependency mode; replace unconditional EventStore/Works project references and enable compliant NuGet auditing.
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
- [ ] `Directory.Build.props`, `Directory.Packages.props`, affected `src/*/*.csproj`, architecture tests -- implement and prove Debug/source and Release/package modes without weakening ownership boundaries.
- [ ] `tools/release-packages.json`, `scripts/pack-release-packages.py`, `scripts/validate-nuget-packages.py`, `scripts/validate-consumer-package-references.py` -- make one fail-closed five-package contract and isolated package-only consumer gate.
- [ ] `.github/workflows/ci.yml`, security/commitlint callers, `.github/dependabot.yml` -- add pinned shared CI, every test lane, consumer validation, CodeQL, dependency review, and Conventional Commit enforcement.
- [ ] `.github/workflows/release.yml`, `.releaserc.json`, release preflight/verification scripts, `package*.json` -- add manual exact-source protected NuGet release and exact-version post-publication verification.
- [ ] `docs/ci-cd.md` -- document operation and the official NuGet findings for Timesheets plus its release-blocking dependencies.

**Acceptance Criteria:**
- Given the separately released Works package is visible from the official NuGet feed, when CI-equivalent Release validation runs from a clean checkout, then restore/build and all six test executables pass with zero failures and package validation produces exactly five consumable packages.
- Given Debug source mode and Release package mode, when MSBuild evaluates cross-repository dependencies, then only Debug uses root-declared submodule projects and Release uses NuGet packages.
- Given a release dispatch, when source, CI, environment, manifest, version, secret, or publication-freeze checks are unsatisfied, then no tag or NuGet write occurs.
- Given a completed release, when the official NuGet v3 indexes are queried, then all five manifest IDs expose the exact released version or verification fails with the missing IDs.

## Implementation Notes

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

## Design Notes

Tenants supplies the closest domain-module shape, EventStore demonstrates larger package-boundary validation, and FrontComposer supplies NuGet-only publication plus independent evidence. Timesheets should use their common minimum, with five packages and no container, rather than clone repository-specific governance. Repository order is Works implementation, Works publication verification, Timesheets implementation, then separately authorized Timesheets publication. Before publication, dry-run semantic-release, validate the exact pinned Builds input contract, verify repository settings, and poll NuGet with bounded retries. Detect and report partial publication; do not build recovery automation unless it occurs.

## Verification

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
- Run each built `tests/*/bin/Release/net10.0/*Tests` executable -- all six projects pass; only the four documented performance/infrastructure skips remain.
- Run the packer and both validators against a synthetic version, then query every manifest ID through `https://api.nuget.org/v3-flatcontainer/<lower-id>/index.json` -- exact inventory and publication state are proven.
