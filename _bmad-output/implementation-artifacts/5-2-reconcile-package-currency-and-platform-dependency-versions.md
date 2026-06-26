---
baseline_commit: b0c41aaa3ad899ca13d49cd66e4f26f3b8e3ae5f
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

- [ ] **Task 1 - Re-run and record the root package audits (AC: 1, 2, 3, 6, 7)**
  - [ ] Run direct NuGet currency over the Timesheets solution:
    `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --outdated`.
  - [ ] Run vulnerability and deprecation checks:
    `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --vulnerable --include-transitive`
    and `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list Hexalith.Timesheets.slnx package --deprecated`.
  - [ ] Confirm root npm status from the Timesheets root only. Root-level `package.json`, lockfiles, `pnpm-lock.yaml`,
    and `yarn.lock` are absent today; manifests under `Hexalith.*` are submodule-owned and out of scope.
  - [ ] If a direct package update exists, update only `Directory.Packages.props`; never add `Version=` to `.csproj`.
  - [ ] Record the direct NuGet, vulnerable/deprecated, and root npm verdicts in `docs/launch-readiness.md`.

- [ ] **Task 2 - Review transitive drift without blind pinning (AC: 3, 6, 7)**
  - [ ] Re-run transitive drift per affected project because the solution-level command currently fails under SDK
    `10.0.301` with `error: Sequence contains no matching element`.
  - [ ] Start with:
    `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet list tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj package --outdated --include-transitive`.
  - [ ] For each drift item, identify whether it is caused by a direct root package, a sibling project reference, a
    test runner package, or an AppHost/platform dependency before adding any pin.
  - [ ] Add an explicit `PackageVersion` pin only when there is a documented compatibility, security, or deterministic
    build reason. Prefer updating the closest direct package first when a package vulnerability exists.
  - [ ] Document either the chosen pin rationale or the "reviewed, no pin" rationale in `docs/launch-readiness.md`.

- [ ] **Task 3 - Reconcile platform version claims (AC: 4, 5, 7)**
  - [ ] Compare current root and platform pins:
    `global.json`, `Directory.Packages.props`, `src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj`,
    and `Hexalith.Builds/Props/Directory.Packages.props`.
  - [ ] Resolve or explicitly waive the known divergence:
    architecture targets Dapr `1.18.4` and Fluent UI V5; `Hexalith.Builds` currently pins base `Dapr` `1.17.9`,
    `Dapr.AspNetCore`/Actors/Workflow `1.18.4`, `Aspire.Hosting` `13.4.6`, and `Microsoft.FluentUI.Components`
    `4.11.6`.
  - [ ] Do not edit `Hexalith.Builds`, sibling package manifests, or submodule pointers unless Jerome explicitly
    approves that scope during implementation.
  - [ ] If the outcome is a waiver, include owner, risk, and revisit condition in `docs/launch-readiness.md`.
  - [ ] If the outcome changes architecture wording, update `_bmad-output/planning-artifacts/architecture.md` so it
    distinguishes intended platform policy from actual pinned package state.

- [ ] **Task 4 - Add or update fitness coverage for package-currency evidence (AC: 1, 2, 3, 4, 7)**
  - [ ] Extend `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` or add a focused
    package-currency fitness test.
  - [ ] Guard that `docs/launch-readiness.md` contains package-currency evidence for direct NuGet, root npm
    applicability, transitive drift, and platform/submodule alignment.
  - [ ] Keep `BuildConfigurationTests.Project_files_do_not_use_inline_package_versions` green; do not weaken existing
    `.slnx`, Central Package Management, no-inline-version, or no-Hexalith-NuGet-package guards.

- [ ] **Task 5 - Verify and report exact results (AC: 6, 7)**
  - [ ] Run restore and build:
    `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false`
    and `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false`.
  - [ ] Run ArchitectureTests after doc/fitness changes. If any package pin, AppHost SDK, or test-stack dependency
    changes, run all affected built xUnit v3 executables and record exact counts.
  - [ ] Re-run package audit commands after edits and copy the important verdicts into the Dev Agent Record.
  - [ ] Generate the File List from `git diff --name-only`; do not include unrelated user/submodule changes.

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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
