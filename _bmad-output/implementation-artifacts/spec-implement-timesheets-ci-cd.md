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

## Spec Change Log

## Review Triage Log

## Design Notes

Tenants supplies the closest domain-module shape, EventStore demonstrates larger package-boundary validation, and FrontComposer supplies NuGet-only publication plus independent evidence. Timesheets should use their common minimum, with five packages and no container, rather than clone repository-specific governance. Repository order is Works implementation, Works publication verification, Timesheets implementation, then separately authorized Timesheets publication. Before publication, dry-run semantic-release, validate the exact pinned Builds input contract, verify repository settings, and poll NuGet with bounded retries. Detect and report partial publication; do not build recovery automation unless it occurs.

## Verification

**Commands:**
- `npm ci && npm audit signatures && npx commitlint --from HEAD~1 --to HEAD` -- Node release tooling and commit policy are reproducible.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:UseHexalithProjectReferences=false -m:1 /nr:false` -- package-only Release graph restores with audit enabled.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Release --no-restore -warnaserror -m:1 /nr:false` -- zero warnings/errors.
- Run each built `tests/*/bin/Release/net10.0/*Tests` executable -- all six projects pass; only the four documented performance/infrastructure skips remain.
- Run the packer and both validators against a synthetic version, then query every manifest ID through `https://api.nuget.org/v3-flatcontainer/<lower-id>/index.json` -- exact inventory and publication state are proven.
