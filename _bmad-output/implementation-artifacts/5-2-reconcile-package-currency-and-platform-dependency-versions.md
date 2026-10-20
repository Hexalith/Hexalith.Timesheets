---
baseline_commit: b0c41aaa3ad899ca13d49cd66e4f26f3b8e3ae5f
reopened_baseline_commit: 72be616918e0ae80fadf56fa152dbd561955e22d
rebaseline_commit: 180d9fd9a635ed44ba896688d6a06e38a33e9f28
---

# Story 5.2: Reconcile Package Currency and Platform Dependency Versions

Status: in-progress

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a release maintainer,
I want Timesheets package currency and platform dependency alignment verified,
so that launch-readiness does not overstate dependency versions or hide transitive/package drift.

## Acceptance Criteria

1. **Direct Timesheets NuGet package currency is audited and compatible updates are applied only through Central Package Management.**
   **Given** Timesheets root package versions are managed through Central Package Management
   **When** direct NuGet package currency is audited
   **Then** direct Timesheets package pins are updated only when compatible
   **And** no inline package versions or `.sln` files are introduced.

2. **Root npm status is recorded honestly.**
   **Given** the Timesheets root has no npm manifest today
   **When** package currency is recorded
   **Then** root npm status is documented as not applicable unless a root manifest is added
   **And** package-update evidence does not imply npm work that cannot be performed.

3. **Transitive drift is reviewed before any explicit transitive pin is added.**
   **Given** transitive dependency drift is reported by package tooling
   **When** update options are evaluated
   **Then** explicit transitive pins are added only for compatibility, security, or deterministic build reasons
   **And** the rationale is documented before pinning.

4. **Dapr, Fluent UI, Aspire, and Hexalith.Builds version divergence is resolved or retained as an explicit waiver.**
   **Given** launch readiness records Dapr, Fluent UI, Aspire, or Hexalith.Builds version divergence
   **When** dependency alignment is reviewed
   **Then** the divergence is resolved or retained as an explicit waiver with owner, risk, and revisit condition
   **And** architecture, launch-readiness, and package files do not make contradictory version claims.

5. **Sibling submodule package updates are not mixed into this Timesheets root change without explicit approval.**
   **Given** sibling submodules have independent package manifests and repository state
   **When** package updates would touch submodules
   **Then** those updates are split into owned submodule changes unless explicitly approved for this Timesheets change
   **And** root submodule pointer changes are not mixed into unrelated package edits.

6. **Verification runs with warnings as errors and records any unavailable audit tooling honestly.**
   **Given** package-currency changes are implemented
   **When** verification runs
   **Then** restore, build, architecture tests, and relevant affected tests pass with warnings as errors
   **And** any skipped, failing, or unavailable runtime/package-audit evidence remains visible.

7. **Launch-readiness evidence records the final package-currency verdict.**
   **Given** the package-currency audit is complete
   **When** launch-readiness evidence is updated
   **Then** `docs/launch-readiness.md` records the final package-currency verdict
   **And** the verdict distinguishes direct package currency, root npm applicability, transitive drift, and platform/submodule alignment.

## Tasks / Subtasks

- [x] **Task 1 - Re-run and record the root package audits (AC: 1, 2, 3, 6, 7)**
  - [x] Run direct NuGet currency over the Timesheets solution:
    `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --outdated`.
  - [x] Run vulnerability and deprecation checks:
    `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --vulnerable --include-transitive`
    and `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --deprecated`.
  - [x] Confirm root npm status from the Timesheets root only. Root-level `package.json`, lockfiles, `pnpm-lock.yaml`,
    and `yarn.lock` are absent today; manifests under `Hexalith.*` are submodule-owned and out of scope.
  - [x] If a direct package update exists, update only `Directory.Packages.props`; never add `Version=` to `.csproj`.
  - [x] Record the direct NuGet, vulnerable/deprecated, and root npm verdicts in `docs/launch-readiness.md`.

- [x] **Task 2 - Review transitive drift without blind pinning (AC: 3, 6, 7)**
  - [x] Re-run transitive drift per affected project because the solution-level command currently fails under SDK
    `10.0.301` with `error: Sequence contains no matching element`.
  - [x] Start with:
    `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj package --outdated --include-transitive`.
  - [x] For each drift item, identify whether it is caused by a direct root package, a sibling project reference, a
    test runner package, or an AppHost/platform dependency before adding any pin.
  - [x] Add an explicit `PackageVersion` pin only when there is a documented compatibility, security, or deterministic
    build reason. Prefer updating the closest direct package first when a package vulnerability exists.
  - [x] Document either the chosen pin rationale or the "reviewed, no pin" rationale in `docs/launch-readiness.md`.

- [x] **Task 3 - Reconcile platform version claims (AC: 4, 5, 7)**
  - [x] Compare current root and platform pins:
    `global.json`, `Directory.Packages.props`, `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj`,
    and `Hexalith.Builds/Props/Directory.Packages.props`.
  - [x] Resolve or explicitly waive the known divergence:
    architecture targets Dapr `1.18.4` and Fluent UI V5; `Hexalith.Builds` currently pins base `Dapr` `1.17.9`,
    `Dapr.AspNetCore`/Actors/Workflow `1.18.4`, `Aspire.Hosting` `13.4.6`, and `Microsoft.FluentUI.Components`
    `4.11.6`.
  - [x] Do not edit `Hexalith.Builds`, sibling package manifests, or submodule pointers unless Jerome explicitly
    approves that scope during implementation.
  - [x] If the outcome is a waiver, include owner, risk, and revisit condition in `docs/launch-readiness.md`.
  - [x] If the outcome changes architecture wording, update `_bmad-output/planning-artifacts/architecture.md` so it
    distinguishes intended platform policy from actual pinned package state.

- [x] **Task 4 - Add or update fitness coverage for package-currency evidence (AC: 1, 2, 3, 4, 7)**
  - [x] Extend `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` or add a focused
    package-currency fitness test.
  - [x] Guard that `docs/launch-readiness.md` contains package-currency evidence for direct NuGet, root npm
    applicability, transitive drift, and platform/submodule alignment.
  - [x] Keep `BuildConfigurationTests.Project_files_do_not_use_inline_package_versions` green; do not weaken existing
    `.slnx`, Central Package Management, no-inline-version, or no-Hexalith-NuGet-package guards.

- [x] **Task 5 - Verify and report exact results (AC: 6, 7)**
  - [x] Run restore and build:
    `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`
    and `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [x] Run ArchitectureTests after doc/fitness changes. If any package pin, AppHost SDK, or test-stack dependency
    changes, run all affected built xUnit v3 executables and record exact counts.
  - [x] Re-run package audit commands after edits and copy the important verdicts into the Dev Agent Record.
  - [x] Generate the File List from `git diff --name-only`; do not include unrelated user/submodule changes.

### Review Findings

- [x] [Review][Patch] Remove unapproved submodule pointer changes and reconcile story record [Hexalith.Projects:1; Hexalith.Tenants:1]

## Dev Notes

### Scope boundary

Story 5.2 is release-readiness maintenance. It may update root package pins, package/readiness documentation, and
architecture/fitness tests that keep package claims honest. It must not introduce product behavior, runtime wiring,
new UI surface area, EventStore persistence changes, or broad submodule package upgrades.

### Current package state verified during story creation

- `global.json` pins SDK `10.0.301` with `rollForward: latestPatch`; installed SDKs include `10.0.300` and `10.0.301`.
- Root `Directory.Packages.props` enables `ManagePackageVersionsCentrally=true` and
  `CentralPackageTransitivePinningEnabled=true`.
- Root direct pins currently include:
  - Aspire `13.4.6` (`Aspire.Hosting`, `Aspire.Hosting.Docker`, `Aspire.Hosting.Redis`)
  - `CommunityToolkit.Aspire.Hosting.Dapr` `13.4.0-preview.1.260602-0230`
  - `Microsoft.Extensions.*` `10.0.9` / `10.7.0`
  - OpenTelemetry `1.16.0` with `OpenTelemetry.Instrumentation.Runtime` `1.15.1`
  - `Microsoft.NET.Test.Sdk` `18.7.0`, `xunit.v3` `3.2.2`, `xunit.runner.visualstudio` `3.1.5`,
    `Shouldly` `4.3.0`, `NSubstitute` `6.0.0-rc.1`, `coverlet.collector` `10.0.1`
- `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj` uses `Aspire.AppHost.Sdk/13.4.6` and references
  `$(HexalithEventStoreRoot)/src/Hexalith.EventStore.Aspire/Hexalith.EventStore.Aspire.csproj`.
- There is no Timesheets root npm manifest or lockfile. The manifests found by `rg --files` are under sibling
  submodules such as `Hexalith.Builds`, `Hexalith.FrontComposer`, `Hexalith.Tenants`, `Hexalith.EventStore`,
  `Hexalith.Parties`, and `Hexalith.Projects`; those are not root npm work for this story.

### Current package audit evidence

Commands run from the Timesheets root on 2026-06-26:

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --outdated`
  completed successfully against `https://api.nuget.org/v3/index.json`; every Timesheets project reported no direct
  package updates.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --vulnerable --include-transitive`
  completed successfully; every Timesheets project reported no vulnerable packages from the current sources.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --deprecated`
  completed successfully; every Timesheets project reported no deprecated packages from the current sources.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --outdated --include-transitive`
  failed with `error: Sequence contains no matching element`. The same failure occurs for the AppHost project, likely
  due to the AppHost/project-reference graph. Record this as tooling evidence if it remains reproducible; do not treat
  it as a clean transitive audit.
- Per-project transitive audit for `tests/Hexalith.Timesheets.IntegrationTests` succeeded and reported drift:
  `DiffEngine` `11.3.0 -> 19.2.0`, `EmptyFiles` `4.4.0 -> 8.18.1`, `Google.Protobuf` `3.35.0 -> 3.35.1`,
  `Microsoft.ApplicationInsights` `2.23.0 -> 3.1.2`, `Microsoft.Bcl.AsyncInterfaces` `6.0.0 -> 10.0.9`,
  `Microsoft.Testing.*` `1.9.1 -> 2.2.3`, `Newtonsoft.Json` `13.0.3 -> 13.0.4`, `Polly.*` `8.4.2 -> 8.7.0`,
  `System.CodeDom` `6.0.0 -> 10.0.9`, `System.Management` `6.0.1 -> 10.0.9`, and
  `System.Threading.RateLimiting` `8.0.0 -> 10.0.9`.

### Files likely touched

Read these before editing and preserve their existing responsibilities:

- `Directory.Packages.props`: root Timesheets package versions only. Add/update `PackageVersion` entries here if and
  only if the story needs a direct or justified transitive pin.
- `global.json`: SDK pin. Do not bump casually; if changed, record why and verify local/CI compatibility.
- `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj`: AppHost SDK pin is in the `Sdk` attribute,
  not in `Directory.Packages.props`.
- `docs/launch-readiness.md`: final package-currency verdict belongs here. Keep overall launch posture honest;
  existing waivers mean this should not silently become `PASS`.
- `_bmad-output/planning-artifacts/architecture.md`: update only Timesheets-owned package/version claims that would
  otherwise contradict actual pins or launch-readiness evidence.
- `README.md`: update only if build/package audit instructions or package status claims change.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/BuildConfigurationTests.cs`: already guards `.slnx`,
  Central Package Management, and no inline package versions.
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs`: currently guards
  `docs/launch-readiness.md` vocabulary, owner/risk/revisit markers, release gate table, honest `CONCERNS/WAIVED`
  verdict, and deferred-integration caveats. Extend this pattern for package-currency evidence.

### Architecture guardrails

- Use `Hexalith.Timesheets.slnx`; never create or use `.sln`.
- Keep package versions centralized. Project files must use `<PackageReference Include="..." />` without `Version=`.
- Root `Directory.Packages.props` can define transitive pins because transitive pinning is enabled, but NuGet will
  promote transitively pinned dependencies into package dependencies when packing. Evaluate this carefully for
  packable projects before adding pins.
- Do not convert sibling submodule project references into `Hexalith.*` NuGet packages. `DependencyDirectionTests`
  explicitly blocks `Hexalith.*` package references in Timesheets projects.
- `Directory.Build.props` sets `NuGetAudit=false` today. Because this story explicitly audits vulnerabilities using
  CLI commands, document the command output and do not silently reinterpret normal restore/build as a full audit gate.
- Submodule package manifests and `Hexalith.Builds/Props/Directory.Packages.props` are reference evidence only unless
  Jerome explicitly approves editing submodules for this story.

### Previous story intelligence

Story 5.1 is the direct precedent. It added `docs/launch-readiness.md`, synchronized readiness docs, and added
fitness tests so release evidence cannot drift. Its review found the same recurring failure pattern seen throughout
the project: overstated docs, stale evidence paths, stale test counts, and File List omissions. Apply these controls:

- Classify package evidence honestly. "No direct updates" is not the same as "no transitive drift."
- Keep exact command outputs and exact test counts in the Dev Agent Record.
- Generate the File List from actual `git diff --name-only`.
- Do not report a clean `PASS` release posture while existing launch waivers remain.
- Do not edit or bundle sibling submodule changes unless explicitly approved.

### Git intelligence

Recent relevant commits:

- `b0c41aa fix: update sprint status and change proposal to reflect epic 5 reopening and package currency alignment`
  added Story 5.2 to the backlog and captured the package-currency change proposal.
- `76455cd feat: update project references and add Keycloak realm configuration for security integration` changed many
  submodule pointers plus AppHost security wiring; do not mix similar broad pointer changes into this story by default.
- `527bdb9 fix: update OpenTelemetry and Microsoft.NET.Test.Sdk package versions` updated root
  `Directory.Packages.props`; use this as the local pattern for direct root package pin edits.

### Latest technical information

- .NET 10 supports `dotnet package list`; the existing `dotnet list ... package` form still works locally. The package
  list command can audit outdated, deprecated, vulnerable, and transitive packages, and .NET 10 restores automatically
  before listing unless `--no-restore` is supplied.
- NuGet Central Package Management requires `Directory.Packages.props`, `ManagePackageVersionsCentrally=true`,
  `PackageVersion` entries in the props file, and project `PackageReference` items without `Version` attributes.
- NuGet audit guidance recommends using `--include-transitive` for vulnerable package checks and preferring updates to
  the closest direct package before pinning a transitive package directly.

External references:

- Microsoft Learn, `dotnet package list`: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list
- Microsoft Learn, NuGet Central Package Management: https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management
- Microsoft Learn, NuGet package auditing: https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages

### Project Context Reference

Follow `Hexalith.AI.Tools/hexalith-llm-instructions.md`: C# files stay one type per file, domain persistence must use
Hexalith.EventStore, and submodules must never be initialized recursively. Persistent project-context facts from
EventStore, Tenants, Parties, Projects, Conversations, and FrontComposer reinforce the same boundaries: warnings as
errors, centralized versions, no inline versions, no recursive submodules, no copied sibling state, and no casual
shared-platform package upgrades.

### Open Questions / Default Decisions

- **Default for submodule package drift:** record and waive/split out; do not edit submodules in this story unless
  Jerome explicitly approves.
- **Default for transitive drift:** document and avoid pins unless compatibility, security, or deterministic build risk
  is proven.
- **Default final launch posture:** keep `CONCERNS` unless the release owner formally accepts every remaining waiver as
  `WAIVED`; do not change to `PASS`.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-26 - Red phase: added `Launch_readiness_record_captures_package_currency_verdict_dimensions`; ArchitectureTests failed as expected, 43 passed / 1 failed / 44 total, missing `Package-Currency Verdict`.
- 2026-06-26 - Root audit: `dotnet list Hexalith.Timesheets.slnx package --outdated` passed; every Timesheets project reported no direct package updates.
- 2026-06-26 - Root audit: `dotnet list Hexalith.Timesheets.slnx package --vulnerable --include-transitive` passed; no vulnerable packages reported.
- 2026-06-26 - Root audit: `dotnet list Hexalith.Timesheets.slnx package --deprecated` passed; no deprecated packages reported.
- 2026-06-26 - Root npm check: no root `package.json`, `package-lock.json`, `npm-shrinkwrap.json`, `pnpm-lock.yaml`, or `yarn.lock` found; sibling `Hexalith.*` manifests remain out of scope.
- 2026-06-26 - Transitive audit: solution-level and AppHost `--outdated --include-transitive` commands still fail with `error: Sequence contains no matching element`.
- 2026-06-26 - Project transitive audits classified drift as test-stack, ServiceDefaults/Polly, or sibling EventStore/Dapr related; no direct transitive pin was added.
- 2026-06-26 - Restore/build validation passed: `dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`; `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` with 0 warnings and 0 errors.
- 2026-06-26 - Full test validation passed: ArchitectureTests 44/44; Contracts.Tests 88/88; IntegrationTests 87 total / 83 passed / 4 skipped; Projections.Tests 77/77; Server.Tests 420/420; Works.Tests 76/76. Total 792, 788 passed, 4 skipped, 0 failed.
- 2026-06-26 - Review fix: reverted accidental `Hexalith.Projects` and `Hexalith.Tenants` gitlink drift back to the story baseline commits (`339414e` and `cd6319e`) so unrelated sibling submodule changes are not bundled into the package-currency story. Revalidated with `git diff --check` clean, `dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`, `dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` (0 warnings, 0 errors), and ArchitectureTests 44/44.

### Completion Notes List

- Added launch-readiness package-currency evidence that separates direct NuGet currency, vulnerable/deprecated audit results, root npm applicability, transitive drift, and platform/submodule alignment.
- Retained the final package-currency verdict as `CONCERNS / WAIVED`; direct packages are current, root npm is not applicable, transitive drift is reviewed with no pin, and platform/submodule version divergence remains explicitly waived with owner, risk, and revisit condition.
- Updated architecture wording so Dapr `1.18.4` and Fluent UI V5 are clear Timesheets policy targets, not proof that every root/submodule package pin already matches that policy.
- Added ArchitectureTests fitness coverage to keep package-currency evidence present in `docs/launch-readiness.md`.
- No package pins, AppHost SDK changes, `Directory.Packages.props` edits, or sibling package manifest edits were made. Review removed the accidental sibling submodule pointer advances by rewinding `Hexalith.Projects` and `Hexalith.Tenants` to the story baseline commits.

### File List

- `_bmad-output/implementation-artifacts/5-2-reconcile-package-currency-and-platform-dependency-versions.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/architecture.md`
- `docs/launch-readiness.md`
- `Hexalith.Projects` (review fix: gitlink rewound to baseline `339414e`)
- `Hexalith.Tenants` (review fix: gitlink rewound to baseline `cd6319e`)
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs`

### Change Log

- 2026-06-26 - Code review fixed the AC5/File List mismatch by reverting the accidental `Hexalith.Projects` and `Hexalith.Tenants` submodule gitlink drift from the story diff; status set to done.
- 2026-06-26 - Implemented Story 5.2 package-currency evidence, platform-version waiver wording, fitness coverage, and verification record; status set to review.

## Reopened Story 5.2 Implementation Evidence (2026-09-12)

The June implementation record above is retained as dated historical evidence. The approved reopened baseline was
applied and verified separately on 2026-09-12 from worktree baseline
`72be616918e0ae80fadf56fa152dbd561955e22d`.

### Reopened implementation decisions

- Updated the executable SDK source of truth from `10.0.400` to `10.0.401` while retaining
  `rollForward: latestPatch`.
- Updated the Timesheets-owned AppHost SDK from `13.4.6` to stable `13.5.3`. The first warnings-as-errors build
  exposed `ASPIRE010`; per the Aspire 13.5 diagnostic guidance, the AppHost now sets
  `<AspireUseCliBundle>true</AspireUseCliBundle>`. This changes orchestration-runtime dependency resolution from
  SDK-supplied bundle assets to the version-aligned CLI bundle; the declared resource topology and domain/product
  behavior are unchanged.
- Added architecture fitness coverage for the exact SDK version, roll-forward policy, AppHost SDK, and Aspire CLI
  bundle opt-in.
- Left `Directory.Packages.props` unchanged. After the clean restore, all stable direct packages resolved at the
  imported central-catalog versions and the direct outdated audit reported no direct package updates. No project
  gained an inline `PackageReference` version.
- Added no transitive pin. The solution/AppHost transitive-outdated tooling failure remains visible, the other 14
  project fallbacks completed, and all reported drift is platform-transitive or test-stack drift without a
  vulnerability, deprecation, compatibility, security, or deterministic-build reason to promote it into root
  dependencies.
- Reconciled architecture and launch-readiness claims to SDK `10.0.401`, Aspire/AppHost `13.5.3`, imported Dapr
  `1.18.7`, and the actual Fluent UI V5 catalog entry. The platform-owned prerelease entries remain explicitly
  waived: `Aspire.Hosting.Keycloak` `13.5.3-preview.1.26425.3`,
  `CommunityToolkit.Aspire.Hosting.Dapr` `13.5.1-beta.751`, and Fluent UI V5
  `5.0.0-rc.5-26219.1`. Owner: Hexalith.Builds / Hexalith.EventStore. Risk: AppHost platform integrations remain on
  prerelease packages and can be misstated as stable direct Timesheets dependencies. Revisit condition: stable,
  validated platform replacements ship or release ownership explicitly approves continued prerelease use.
- Confirmed root npm remains not applicable: no root `package.json`, `package-lock.json`, `npm-shrinkwrap.json`,
  `pnpm-lock.yaml`, or `yarn.lock` exists.
- Preserved the dated Story 5.3 SDK `10.0.400` evidence and made no edit under `references/`, no gitlink change, and no
  UI, declared resource-topology, persistence, or domain/product-behavior change. Orchestration-runtime dependency
  resolution changed to the version-aligned CLI bundle as recorded above.

### Reopened verification commands and results

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet --version` -> `10.0.401`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false --force --no-cache`
  -> success; restore assets regenerated for every solution project. With the CLI bundle enabled, the AppHost assets
  no longer contain the prior SDK-supplied `Aspire.Dashboard.Sdk` or `Aspire.Hosting.Orchestration` bundle packages;
  the remaining Aspire hosting packages resolve on the `13.5.3` line.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`
  -> final result: success, 0 warnings, 0 errors. The first run failed with `ASPIRE010`; after the documented
  `AspireUseCliBundle=true` compatibility correction, the exact command passed.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --outdated --no-restore`
  -> success; all 15 projects reported no direct package updates.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --vulnerable --include-transitive --no-restore`
  -> success; all 15 projects reported no vulnerable packages from the current sources.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --deprecated --include-transitive --no-restore`
  -> success; all 15 projects reported no deprecated packages from the current sources.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --outdated --include-transitive --no-restore`
  -> failed exactly with `error: Sequence contains no matching element`; this lane is not reported clean.
- Per-project `dotnet list <project> package --outdated --include-transitive --no-restore --format json` fallbacks
  -> 14 succeeded and the AppHost fallback failed with the same exact error. Reported drift: `Google.Protobuf`
  `3.35.0 -> 3.36.1`; gRPC packages `2.80.0 -> 2.83.0`; Polly packages `8.4.2 -> 8.7.0`; `Castle.Core`
  `5.1.1 -> 5.2.1`; `DiffEngine` `11.3.0 -> 20.3.1`; `EmptyFiles` `4.4.0 -> 8.18.2`;
  `Microsoft.Testing.*` `2.3.3 -> 2.4.0`; `Microsoft.ApplicationInsights` `2.23.0 -> 3.1.2`;
  `Microsoft.Bcl.AsyncInterfaces` `6.0.0 -> 10.0.12`; `System.CodeDom` `6.0.0 -> 10.0.12`;
  `System.Diagnostics.EventLog` `6.0.0 -> 10.0.12`; `System.Management` `6.0.1 -> 10.0.12`; and
  `System.Threading.RateLimiting` `8.0.0 -> 10.0.12`.
- Built xUnit v3 executable results: ArchitectureTests 52 total / 52 passed; Contracts.Tests 88 / 88;
  IntegrationTests 87 total / 83 passed / 4 skipped; Projections.Tests 77 / 77; Server.Tests 420 / 420;
  Works.Tests 76 / 76. Final total: 800 tests, 796 passed, 4 intentional skips, 0 failed.
- The first IntegrationTests invocation had two `AssertSensitiveMaterialAbsent` failures because its broad forbidden
  substring `60` collided with rendered diagnostic content. Both focused retries passed (2/2), then the complete
  IntegrationTests executable rerun passed (87 total / 83 passed / 4 skipped). No unrelated test/runtime behavior
  was changed; the intermittent substring collision remains visible as a test-quality risk.
  Correction (2026-10-18): the focused retries did not establish determinism and were not a fix. The false positive is
  owned by the 2026-10-09 spec-16 deferred-work entry "Replace substring-based numeric privacy exclusions with
  assertions that distinguish business values from permitted diagnostic correlation and timestamp fields".

### Reopened Intended File List

`git diff --name-only` after implementation reported the tracked entries below; the intended artifact list also
includes the delivered untracked workflow spec:

- `_bmad-output/implementation-artifacts/5-2-reconcile-package-currency-and-platform-dependency-versions.md`
- `_bmad-output/implementation-artifacts/epic-5-context.md` (pre-existing reopened-story context change)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (pre-existing reopened-story status change)
- `_bmad-output/planning-artifacts/architecture.md`
- `docs/launch-readiness.md`
- `global.json`
- `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj`
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs`
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/ScaffoldGovernanceTests.cs`
- `_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md`
  (delivered workflow spec; intentionally included even while untracked and therefore absent from
  `git diff --name-only`)

Correction (2026-10-18): the list above is wrong in three ways. `git show --name-status ee76a92` shows that the
commit changed 11 files: (1) it also modified `_bmad-output/implementation-artifacts/deferred-work.md`, which the list
omits; (2) it modified `epic-5-context.md` and `sprint-status.yaml` itself, so they were not pre-existing changes; and
(3) it added (`A`) the spec rather than leaving it untracked. The 2026-09-12 list is otherwise retained as written.

## Re-baselined Story 5.2 Implementation Evidence (2026-10-18)

The June and 2026-09-12 records above are retained as dated history. On 2026-10-18 Jerome renegotiated the frozen
intent (option A): SDK `10.0.402`, AppHost SDK aligned with the imported stable Aspire `13.6.1` using the decided Option 2
orchestration shape, and root npm applicable. This iteration starts at `180d9fd9a635ed44ba896688d6a06e38a33e9f28`.
Story status remains `in-progress`. `docs/launch-readiness.md` (pinned by `LaunchReadinessTests`) still says "Story 5.2
remains in progress", and Story 5.1 owns the overall-decision text, so the review/done transition is left to the
workflow owner.

### Re-baselined implementation decisions

- `global.json` stays at SDK `10.0.402` with `rollForward: latestPatch`. CI/CD owns it, and this iteration does not edit it.
- `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj`: `Aspire.AppHost.Sdk/13.5.3` -> `13.6.1`,
  `AspireUseCliBundle` `true` -> `false`, and an unconditional `<NoWarn>$(NoWarn);ASPIRE010</NoWarn>`. This is the
  2026-09-13 Option 2 decision, matching `Hexalith.EventStore`. The compiled-gateway constant, project references, and
  package references are unchanged. Before the change, the AppHost assets mixed `Aspire.Hosting.AppHost` `13.5.3` with
  `Aspire.Hosting` `13.6.1` and had no orchestration or dashboard package. After the forced no-cache restore, both the
  package-mode and the source-reference graphs hold `Aspire.Hosting`, `Aspire.Hosting.AppHost`, `Aspire.Hosting.Redis`,
  `Aspire.Hosting.Orchestration.linux-x64`, and `Aspire.Dashboard.Sdk.linux-x64` at `13.6.1`, plus the waived
  `Aspire.Hosting.Keycloak` `13.6.1-preview.1.26506.6` and `CommunityToolkit.Aspire.Hosting.Dapr` `13.6.1-beta.935`.
  They hold no Aspire `13.5.x` package. The build raised no Aspire diagnostic.
- `Directory.Packages.props` is unchanged: the direct audit found no update, so no CPM edit was needed. No inline version
  was added, and no transitive pin was added.
- Root npm is now applicable. `package.json`, `package-lock.json`, and npm versions are unchanged.
- Fitness tests: the AppHost guard is split into security-helper, approved-SDK-pin, and NuGet-orchestration/`ASPIRE010`-only
  tests. `LaunchReadinessTests` now resolves the imported catalog through the root import candidates, scopes the
  package assertions to the current-verdict paragraph and named rows, and derives npm applicability from the presence of
  a root `package.json`. It also adds an AppHost-SDK/catalog alignment test and an architecture version-notes guard.
  Six temporary mutations (CLI bundle `true`, an extra `ASPIRE004` suppression, AppHost SDK `13.6.0`, a stale
  current-verdict AppHost version, a stale architecture Dapr version, and the npm row reverted to `not applicable`) were
  each caught by the intended tests. The files were restored byte-identical afterwards.
- Documents: the launch-readiness package section now leads with the 2026-10-18 verdict and a five-row table. The
  2026-10-03/04 paragraphs and a superseded 2026-09-17 verdict paragraph follow, still dated. The prerelease waiver row and
  the `Package currency` gate row were refreshed, and the preamble no longer scopes the whole record to the June baseline.
  `architecture.md` version notes and summaries now say SDK `10.0.402`, AppHost `13.6.1`, Dapr `1.18.10`, and Fluent UI V5
  `5.0.1`; the earlier values are kept as dated history. `docs/ci-cd.md` now says SDK `10.0.402`. `epic-5-context.md`
  received the Works-checkout, dropped-constraint, vocabulary, npm, and baseline updates.
- The Works-owned `HexalithEventStoreVersion` default (`3.124.0`) still diverges from the catalog's EventStore `3.125.1`.
  It is recorded in launch readiness as an open, unwaived divergence with owner (Hexalith.Works), risk, and revisit
  condition, tracked by the deferred-work entry "Align the existing Works EventStore-version forwarding with the
  consuming source-build catalog in a separate build increment". Only the platform prereleases are waived (§7 recorded
  exceptions). The SDK `10.0.402` and AppHost `13.6.1` baseline is approved by the 2026-10-18 option-A renegotiation in
  the Story 5.2 spec's Spec Change Log. No submodule or Timesheets build file was changed for the divergence.
- All fifteen 2026-09-12 review patches were re-validated against HEAD and marked in the spec's Review Findings; the
  Story 5.2-owned deferred-work entries were resolved or refreshed.

### Re-baselined verification commands and results

- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet --version` -> `10.0.402`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx --force --no-cache -m:1 /nr:false` ->
  exit 0, 15 projects restored (configuration-free package mode). It was rerun on the final tree with the same result.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`
  -> exit 0, 0 warnings, 0 errors (Release package mode). It was rerun on the final tree with the same result.
- The four solution audits and the 14 project fallbacks below read the package-mode graph produced by the forced no-cache
  configuration-free restore above.
- `dotnet list Hexalith.Timesheets.slnx package --outdated --no-restore` -> exit 0; all 15 projects report no updates.
- `dotnet list Hexalith.Timesheets.slnx package --vulnerable --include-transitive --no-restore` -> exit 0; 15 projects,
  no vulnerable packages.
- `dotnet list Hexalith.Timesheets.slnx package --deprecated --include-transitive --no-restore` -> exit 0; 15 projects,
  no deprecated packages.
- `dotnet list Hexalith.Timesheets.slnx package --outdated --include-transitive --no-restore` -> exit 1,
  `error: Sequence contains no matching element`. The per-project `--format json` fallbacks: AppHost exit 1 with the
  same error, and 14 exit 0 with 21 distinct transitive drift items and no direct update.
  On the source-reference graph (after the Debug `-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
  -p:HexalithEventStoreVersion=3.125.1` restore), `dotnet package list --project <AppHost> --outdated
  --include-transitive --no-restore` fails with the same error. A manual AppHost fallback on that same source-reference
  graph (`dotnet list <AppHost> package --include-transitive --no-restore --format json`, then a nuget.org flat-container
  comparison) checked 82 packages and found 22 stable packages behind latest, all Aspire hosting or orchestration
  dependencies, plus the two waived prereleases. The package-mode AppHost transitive graph was not separately compared.
  No pin was added.
- `npm ci` -> exit 0 (added 361 packages, audited 505). `npm outdated` -> exit 1: `@commitlint/cli` and
  `@commitlint/config-conventional` at current/wanted `21.2.2`, latest `21.2.4`. `npm audit` -> exit 1: 8 high,
  0 critical, 0 moderate, 0 low, all from braces `GHSA-vfj7-8cjw-p6xm`. The only offered fix is `npm audit fix --force`,
  which is breaking. No npm file changed.
- README source-reference mode (`dotnet restore ... -p:Configuration=Debug -p:UseHexalithProjectReferences=true --force
  --no-cache` plus the README Debug build) -> restore and build passed with 0 warnings and 0 errors. The test executables
  then failed on the existing Works EventStore-version forwarding mismatch: ArchitectureTests 61 total / 1 failed (an
  intermediate tree: the split AppHost guards were in place, but the later LaunchReadinessTests and document edits
  were not, so the count matches neither HEAD's 59 nor the final 63, and the failure was the then-stale package-verdict
  document), Contracts.Tests 92/92, Server.Tests 558 / 135 failed,
  Projections.Tests 437 / 351 failed, IntegrationTests 732 / 249 failed / 5 skipped, and Works.Tests 76/76. Every failure in
  the three lanes was `FileNotFoundException` for `Hexalith.EventStore.Contracts, Version=3.125.1.0`, beside a copied
  `3.124.0.0` assembly.
- Established verification mode, from the 2026-10-18 direct-execution test summary:
  `dotnet restore ... -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false
  -p:HexalithEventStoreVersion=3.125.1 -m:1 /nr:false`, then `dotnet build ... --configuration Debug --no-restore
  -t:Rebuild` with the same properties and `-warnaserror` -> 0 warnings, 0 errors. A final incremental build of the
  finished tree also had 0 warnings and 0 errors.
- Built executables, run individually with `HexalithEventStoreVersion=3.125.1`: ArchitectureTests 63/63 (final tree);
  Contracts.Tests 92/92; Server.Tests 558/558; Projections.Tests 437/437 (606.8 s); IntegrationTests 732 total /
  727 passed / 5 skipped (558.4 s); Works.Tests 76/76. Total: 1,958 tests, 1,953 passed, 5 intentional skips, 0 failed.
  The five non-ArchitectureTests lanes ran on the Rebuild output; their sources did not change afterwards.
- Post-review totals: on 2026-10-18 the Release ArchitectureTests executable built from `62ec574` ran 64/64, because
  the round-2 review patch added `CiCdGuideNamesThePinnedDotnetSdk`; the suite total is therefore 1,959 tests. On
  2026-10-19, after the round-3 review patches, the Release ArchitectureTests build (`--no-restore -warnaserror`) had
  0 warnings and 0 errors and its executable ran 64/64. The other five lanes were not rerun; their sources did not
  change. Later on 2026-10-19, after the round-4 review patches (one `LaunchReadinessTests` assertion and one row key
  changed), a package-mode solution restore (needed to bring back the evicted `xunit.analyzers` `2.2.0`), then the
  Release ArchitectureTests build (`--no-restore -warnaserror`) had 0 warnings and 0 errors and its executable ran
  64/64. The other five lanes were not rerun; their sources did not change. On 2026-10-20, after the round-5 review
  patches (documentation and ledger text only), the Release ArchitectureTests build (`--no-restore -warnaserror`) had
  0 warnings and 0 errors and `dotnet test --project … -c Release --no-build` ran 64/64. The other five lanes were not
  rerun; their sources did not change. Later on 2026-10-20, after the round-6 review patches (documentation and ledger
  text plus one `LaunchReadinessTests` assertion change), the Release ArchitectureTests build (`--no-restore -warnaserror`)
  had 0 warnings and 0 errors and `dotnet test --project … -c Release --no-build` ran 64/64. Temporary `72d9fda` and
  `13.6.1` mutations of the new current-verdict sentence each failed the package-verdict test, and the document was
  restored byte-identical. The other five lanes were not rerun; their sources did not change.
- AppHost/DCP lanes: `python3 scripts/check_magic_link_apphost_guards.py --configuration-only` -> exit 0; all 35 compiled
  no-resource probes passed. `python3 scripts/run_isolated_magic_link_dapr.py` -> exit 1 at preflight with
  `A Dapr application is already running or inventory is unsafe; no AppHost started`. At that point a foreign Tenants
  AppHost and 13 foreign Dapr applications (including two `eventstore` IDs) were running. An ordinary `aspire start` was not
  attempted because it would add a duplicate `eventstore` ID on the shared placement. No foreign runtime was started or
  stopped. Current runtime smoke therefore remains deferred.
- `git diff --check` -> clean. No `references/` path or gitlink, `global.json`, npm manifest or lockfile, inline version,
  `.sln`, UI, topology, persistence, or product-behavior change.

### Re-baselined file list

`git diff --name-only 180d9fd9a635ed44ba896688d6a06e38a33e9f28` after implementation:

Correction (2026-10-20, round-7 patch): the same command now also lists
`.github/workflows/release.yml` from unrelated commit
`aa49fda2e62749e8ff3cc4eda8596fb7e1a9fd9a` and `epic-3-context.md` from unrelated
commit `969d32a77d62c833182270349d87ec7bd0200606`. Those files are outside Story 5.2;
the list below records the earlier implementation, not every subsequent workspace change.

- `_bmad-output/implementation-artifacts/5-2-reconcile-package-currency-and-platform-dependency-versions.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/epic-5-context.md`
- `_bmad-output/implementation-artifacts/spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md`
  (also carries Jerome's pre-existing 2026-10-18 renegotiation edits)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (added by the 2026-10-18 code review: the finalization
  status sync `in-progress` -> `review` ran after this list was generated and is part of commit `b85e810`)
- `_bmad-output/planning-artifacts/architecture.md`
- `docs/ci-cd.md`
- `docs/launch-readiness.md`
- `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj`
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs`
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/ScaffoldGovernanceTests.cs`
- `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/BuildConfigurationTests.cs` and
  `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/RepositoryRoot.cs` (added by the round-3 code review
  patches, which moved both project scans to one shared Timesheets-owned project list; the earlier
  `BuildConfigurationTests.cs` change in `55e2955` is a separate CI commit)

## Round-7 Review Patches (2026-10-20)

Applied all seven round-7 patches from the original Story 5.2 spec, starting from
`2d6b689ec45fd1f73fb7a3ff09e8950c90a2fd07`. Jerome's approved Option 1 raises only
the root CPM Works Contracts pin from `1.0.1` to `1.0.2`; the two nuspec dependency
lists are identical. Restored assets resolve Works Contracts `1.0.2` alongside
catalog-selected EventStore Contracts `3.125.1`.

The package verdict now distinguishes Timesheets-owned direct currency from
post-audit catalog-owned EventStore `3.126.0` drift. CI/CD documentation separates
the Builds catalog pin from the reusable-workflow pin. Advisory wording explicitly
states that dependency review checks PR-introduced dependency changes and no workflow
runs an npm vulnerability audit; commitlint runs `npm audit signatures` for provenance,
and unchanged braces findings remain open. Runtime evidence now names
the modified source worktree separately from its later publication commit. The
ledger records the superseded round-19 journey statements for Story 5.1 and resolves
the already-fixed full-suite fitness regex entry. The historical file list now
explains the two unrelated commits it also reports.

Verification used SDK `10.0.402` and the Release package graph throughout. Exact
commands (each `dotnet` command used `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`):

- `dotnet restore Hexalith.Timesheets.slnx --force --no-cache -m:1 /nr:false`:
  exit 0, all 15 projects restored.
- `dotnet build Hexalith.Timesheets.slnx --configuration Release --no-restore -warnaserror -m:1 /nr:false`:
  exit 0, 0 warnings and 0 errors.
- `tests/Hexalith.Timesheets.Works.Tests/bin/Release/net10.0/Hexalith.Timesheets.Works.Tests -noColor`:
  exit 0, 76/76 passed, 0 skipped, 0 failed.
- `tests/Hexalith.Timesheets.ArchitectureTests/bin/Release/net10.0/Hexalith.Timesheets.ArchitectureTests -noColor`:
  exit 0, 64/64 passed, 0 skipped, 0 failed; repeated against the final evidence
  text with the same result.
- `dotnet list Hexalith.Timesheets.slnx package --outdated --no-restore --format json`:
  exit 0, all 15 projects covered; the only reported direct updates are six
  distinct catalog-owned EventStore package IDs, `3.125.1 -> 3.126.0` (17 project/package
  occurrences). No Timesheets-owned direct update remains.
- `dotnet list Hexalith.Timesheets.slnx package --vulnerable --include-transitive --no-restore --format json`:
  exit 0, all 15 projects covered, no vulnerable packages.
- `dotnet list Hexalith.Timesheets.slnx package --deprecated --include-transitive --no-restore --format json`:
  exit 0, all 15 projects covered, no deprecated packages.
- `dotnet list Hexalith.Timesheets.slnx package --outdated --include-transitive --no-restore --format json`:
  exit 1, exactly `error: Sequence contains no matching element`.
  Fallback: `dotnet list <project.csproj> package --outdated --include-transitive --no-restore --format json`
  for each of the 15 Timesheets-owned projects listed in `Hexalith.Timesheets.slnx`.
  The AppHost fails with the same error; the other 14 exit 0 and report 26 distinct
  transitive drift IDs, the earlier 21 plus five EventStore IDs. No transitive pin
  was added; this unavailable solution/AppHost lane remains explicit.

The other four test projects were not rerun: only Works.Tests consumes the changed
Works Contracts package, and ArchitectureTests covers the edited evidence guards.
Their earlier suite results remain dated to their original runs. Root npm and live
AppHost lanes were not rerun; their prior findings and deferred runtime proof remain
visible. No sibling, gitlink, SDK, npm manifest/lockfile, or workflow changed.

Changed files for this round: `Directory.Packages.props`, `docs/launch-readiness.md`,
`docs/ci-cd.md`, `LaunchReadinessTests.cs`, this record, `deferred-work.md`, the original
Story 5.2 spec (patch dispositions), and the new round-7 continuation spec. Workflow
finalization also synchronizes Story 5.2's sprint key to `review`.

The independent blind review produced seven findings. Six evidence/provenance
corrections were applied, including the distinction between vulnerability scanning
and `npm audit signatures`, and a durable Builds/platform handoff was added for
EventStore drift. The post-review ArchitectureTests project build used
`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --configuration Release --no-restore -warnaserror -m:1 /nr:false`
and passed with 0 warnings/errors; its Release executable again passed 64/64.
Works.Tests and the NuGet audits were not repeated because the review corrections
changed only documentation, evidence and the ArchitectureTests assertions.

The [round-7 verification packet](evidence/5-2/20261020-round7/README.md) retains
raw JSON output for all four solution audits and all 15 project fallbacks, exact
commands/exits, restore/build/test logs, dependency-input hashes and derived counts.
It is an additional artifact of this round, alongside the files listed above.

## Round-8 Review Patches (2026-10-20)

The round-8 code review of `180d9fd..c5ac600` (excluding the unrelated `aa49fda`
and `969d32a`) found that the workspace sync commit
`929ea22c977385590d77d95f73af86e8b051c4fc` had moved every `references/*` gitlink
after the round-7 audit. That commit owns the pointer moves. At it, the Builds
catalog `6a38cb0` selected EventStore `3.126.0`, and Works `6460257` forwarded
`3.126.0`, so several current round-7 claims were stale.

Jerome chose two options:

- Refresh the current package-currency evidence at HEAD.
- Record the catalog-owned values as dated observations, without a new fitness guard.

The review then applied all six round-8 patches; their dispositions are in the
original Story 5.2 spec.

Verification at HEAD `929ea22` on the Release package-mode graph:

- **Restore:** `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx --force --no-cache -m:1 /nr:false`
  restored 15 projects.
- **Build:** `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore --no-incremental -warnaserror -m:1 /nr:false`
  passed with 0 warnings and 0 errors.
- **Tests:** the six Release executables passed:

  | Suite | Total | Notes |
  |---|---|---|
  | ArchitectureTests | 64 | run after the final documentation edits |
  | Contracts.Tests | 92 | |
  | Works.Tests | 76 | |
  | Server.Tests | 558 | |
  | Projections.Tests | 437 | |
  | IntegrationTests | 732 | 5 intentional skips |

  That is 1,959 tests, 0 failures and 5 intentional skips.
- **NuGet audits:**
  - Direct: no update across 15 projects.
  - Vulnerable and deprecated: clean across 15 projects.
  - Transitive, solution and AppHost: still exit 1 with `error: Sequence contains no matching element`.
  - Transitive, other 14 project fallbacks: exit 0 and report 19 distinct transitive IDs behind latest.

  No transitive pin was added.
- **Not rerun:** the README source-reference default lane, npm, and live AppHost lanes.

Changed files for this round:

- `docs/launch-readiness.md`
- `docs/ci-cd.md`
- `epic-5-context.md`
- `deferred-work.md`: the round-7 Builds handoff is resolved; the Works forwarding entry stays open with a HEAD update.
- the round-7 packet README (commit anchor)
- the original Story 5.2 spec (round-8 findings and dispositions)
- `sprint-status.yaml`
- this record

The [round-8 verification packet](evidence/5-2/20261020-round8/README.md) retains the
raw audit, package-listing and fallback output, restore/build logs, test summaries,
input and assets hashes, and derived counts. No package, project, test, gitlink, SDK,
npm manifest/lockfile, or workflow file changed.

## Round-9 Review Patches (2026-10-20)

The round-9 code review covered `180d9fd..5258598`. It excluded `aa49fda`, `969d32a`,
and the gitlink-only workspace sync `929ea22`. Its main finding was that the round-8
refresh missed several statements that `929ea22` had made stale.

Jerome chose to apply all seven patches; their dispositions are in the original Story
5.2 spec.

- **Stale statements:**
  - The current verdict now names the 2026-10-18 journey's catalog as the then-current `332b2c1`.
  - The ledger's D1 and agent entry-point instructions no longer force `HexalithEventStoreVersion=3.125.1`.
  - The round-7 Story 5.1 handoff now names the current EventStore pin, `6c56c27`.
- **Round-8 anchor:** round 8 is anchored as the worktree based on `929ea22`, committed as
  `525859841e3dc329f65970b7067d3ef3bd1a91fa`. The receipt's documentation hashes match that commit.
- **Test count:** "1,959 tests" now reads 1,954 passed, 5 intentional skips and 0 failures.
- **Direct row:**
  - The round-7 result is labelled and in the past tense.
  - The row lists the six direct `Hexalith.EventStore.*` packages at `3.126.0`.
- **Transitive row:** the row says the manual AppHost registry fallback was last run on 2026-10-18.
- **Project filter:** `RepositoryRoot.TimesheetsProjectFiles()` now excludes only lowercase `references`
  segments. On the current tree it selects the same 16 project files as before.

Verification (SDK `10.0.402`, Release package-mode assets restored in round 8):

- **Build:** `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --configuration Release --no-restore --no-incremental -warnaserror -m:1 /nr:false`
  passed with 0 warnings and 0 errors.
- **Tests:** `tests/Hexalith.Timesheets.ArchitectureTests/bin/Release/net10.0/Hexalith.Timesheets.ArchitectureTests -noColor`
  exited 0, with 64/64 passed after the final edits.
- **Not rerun:** the other five suites, the NuGet audits and npm. No package, production source or other test
  source changed.
- **Live lanes:** the README source-reference and live AppHost lanes were not rerun. At this review a foreign
  Projects AppHost holding Dapr app ID `eventstore` and five other Dapr applications were running, so the
  isolated journey's preflight would refuse.

Changed files for this round:

- `docs/launch-readiness.md`
- `docs/ci-cd.md`
- `epic-5-context.md`
- `deferred-work.md`
- the round-8 packet README
- `RepositoryRoot.cs`
- the original Story 5.2 spec (round-9 findings and dispositions)
- `sprint-status.yaml` (timestamp only; the status stays `review`)
- this record

No package, gitlink, SDK, npm manifest/lockfile, or workflow file changed.
