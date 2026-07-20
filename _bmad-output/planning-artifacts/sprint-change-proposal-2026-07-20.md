---
title: "Sprint Change Proposal — Consume the Umbrella-Owned Hexalith.Works Checkout"
status: approved-and-applied
created: 2026-07-20
approved: 2026-07-20
project: timesheets
mode: batch
change_scope: moderate
recommended_path: direct-adjustment
---

# Sprint Change Proposal: Consume the Umbrella-Owned Hexalith.Works Checkout

## 1. Issue Summary

### Trigger

The umbrella workspace now owns `Hexalith.Works` as the root-declared submodule at
`references/Hexalith.Works`. `Hexalith.Timesheets` still declares a second
`Hexalith.Works` submodule at its own repository root and tracks a corresponding
gitlink. Timesheets must not initialize, maintain, or prefer that repository-local
checkout.

This change was triggered by the umbrella dependency-topology update rather than by
a failed Timesheets product story. It affects the completed scaffold and Works
adapter work from Stories 1.1, 1.10, and 4.8, but it does not invalidate their
business behavior. The correction should be delivered as a new release-readiness
follow-up story instead of rewriting the history of those completed stories.

### Core Problem

Issue type: **new workspace dependency-ownership requirement discovered after
implementation**.

Timesheets currently has two possible physical sources for Works contracts:

1. A Timesheets-owned root path, `Hexalith.Works`, declared in the Timesheets
   `.gitmodules` file and tracked as a gitlink.
2. The umbrella-owned sibling path, `../Hexalith.Works`, corresponding to
   `<workspace>/references/Hexalith.Works`.

`Directory.Build.props` probes the Timesheets-owned path first. Although that path
is presently uninitialized and MSBuild therefore resolves the sibling checkout, a
future nested submodule initialization would silently change dependency ownership
and could select a different Works commit from the umbrella workspace.

### Evidence

| Evidence | Current result |
|---|---|
| Umbrella `.gitmodules` | Declares `references/Hexalith.Works` and `references/Hexalith.Timesheets` as sibling root submodules. |
| Umbrella Git index | Tracks `references/Hexalith.Works` at commit `27388cb6174034645fc5c0d068d668cfc4c9570a`. |
| Timesheets `.gitmodules` | Declares `[submodule "Hexalith.Works"]` with `path = Hexalith.Works`. |
| Timesheets Git index | Tracks a `160000` gitlink at `Hexalith.Works` (`f2259daab922096113262fc9e0a5588182918e0a`). |
| Current local state | Timesheets' `Hexalith.Works` directory is uninitialized; the umbrella sibling is initialized. |
| MSBuild evaluation | `HexalithWorksRoot` currently resolves to `.../Hexalith.Timesheets/../Hexalith.Works`. |
| Resolution risk | `Directory.Build.props` probes `$(MSBuildThisFileDirectory)Hexalith.Works` before the umbrella sibling, so initializing the Timesheets gitlink would override the intended checkout. |

## 2. Impact Analysis

### Epic Impact

- **Epic 1 — Trusted Time Capture & Activity Governance:** product behavior remains
  complete. Stories 1.1 and 1.10 established the scaffold and Works adapter, but
  their dependency-checkout assumption is superseded by the umbrella ownership
  rule. Do not reopen or rewrite those completed stories.
- **Epics 2 and 3:** no impact.
- **Epic 4 — Approved Time Ledger, Reporting & Finance Export:** Story 4.8's planned
  effort adapter remains valid. Only the physical source path for
  `Hexalith.Works.Contracts` is constrained.
- **Epic 5 — Release Readiness Verification:** add Story 5.3 to correct checkout
  ownership, strengthen build-governance tests, and synchronize documentation.
  Epic order and MVP sequencing remain unchanged.

No epic becomes obsolete, no new product epic is needed, and no prior feature work
needs rollback.

### Story Impact

- Add **Story 5.3: Consume the Umbrella-Owned Hexalith.Works Checkout**.
- Leave Stories 1.1, 1.10, and 4.8 marked `done`; reference them as historical
  context in Story 5.3.
- Add Story 5.3 to `sprint-status.yaml` as `backlog` until implementation begins.

### Artifact Conflicts

| Artifact | Impact |
|---|---|
| PRD | No change. FR2, FR17, FR20, and FR23 still require integration with Works; physical checkout ownership is not product scope. |
| Epics | Add the checkout-ownership requirement and Story 5.3; clarify Epic 5's FR23 boundary-verification role. |
| Architecture | Clarify the workspace-owned Works invariant, `HexalithWorksRoot` resolution, standalone override behavior, and forbidden Timesheets-local gitlink. |
| UX Design | No change. No user surface, flow, component, accessibility, or copy behavior changes. |
| Sprint status | Add Story 5.3 as `backlog`; keep Epic 5 `in-progress`. |
| Repository configuration | Remove the Timesheets Works submodule declaration and gitlink; remove the root-local Works probe from `Directory.Build.props`. |
| Tests | Add fitness coverage preventing a Timesheets-owned Works declaration/path and preserving sibling or explicit-property resolution. |
| Documentation | Update boundary/build guidance to identify the umbrella checkout as the sole default Works source. |

### Technical Impact

The Timesheets Works adapter and its `ProjectReference` remain unchanged:

```xml
<ProjectReference Include="$(HexalithWorksRoot)\src\Hexalith.Works.Contracts\Hexalith.Works.Contracts.csproj" />
```

Only the source of `HexalithWorksRoot` changes. An explicitly supplied
`HexalithWorksRoot` remains valid for controlled standalone or CI builds. Without
an explicit property, the default must resolve only to the umbrella sibling
`../Hexalith.Works`. Timesheets must not create, initialize, or prefer
`./Hexalith.Works`.

No domain contracts, commands, events, projections, APIs, runtime adapters, or UI
surfaces require behavioral changes.

## 3. Recommended Approach

### Selected Path: Direct Adjustment

Implement a bounded build-governance correction in Epic 5:

1. Remove the Timesheets `.gitmodules` entry for Works and the tracked
   `Hexalith.Works` gitlink.
2. Remove the Timesheets-root Works probe from `Directory.Build.props` while
   preserving an explicitly supplied `HexalithWorksRoot` and the umbrella-sibling
   fallback.
3. Add architecture fitness tests that prevent reintroduction.
4. Synchronize architecture, epics, sprint status, README, and boundary guidance.
5. Validate the Works adapter and the full solution against the umbrella-owned
   checkout.

### Rationale

- The umbrella already owns and pins the required Works checkout.
- The current build already resolves that sibling checkout because the nested
  Timesheets gitlink is uninitialized, proving that product code does not need a
  second source tree.
- Removing the duplicate declaration eliminates commit skew and prevents accidental
  nested initialization without changing domain behavior.
- A new follow-up story preserves the audit history of completed Stories 1.1, 1.10,
  and 4.8.

### Effort, Risk, and Timeline

- **Effort:** Low; approximately one focused developer change plus review.
- **Risk:** Low after the proposed fitness/build checks. The primary risk is breaking
  standalone clones that relied on the nested Works gitlink; the explicit
  `HexalithWorksRoot` override is the supported mitigation.
- **Timeline impact:** No product milestone or MVP scope change. The correction
  should complete before the next Timesheets build/release claim.

### Alternatives Considered

1. **Potential rollback — not viable or necessary.** Reverting Stories 1.10 or 4.8
   would remove required Works validation/reporting behavior while doing nothing to
   improve checkout ownership.
2. **MVP review — not necessary.** The PRD remains achievable and no user-facing
   capability changes.
3. **Keep both checkouts and document precedence — rejected.** This retains the
   possibility of silent commit skew and violates the explicit requirement that
   Timesheets may not initialize its own Works checkout.

## 4. Detailed Change Proposals

### 4.1 PRD

**No edit proposed.** The PRD correctly describes Works as an external bounded
context. It does not prescribe Git checkout ownership.

### 4.2 Epics

#### Additional Requirement

**Current:**

> Preserve root-level submodule rules and never introduce recursive submodule initialization.

**Proposed:**

> Preserve umbrella root-declared submodule rules and never introduce recursive submodule initialization. `Hexalith.Timesheets` must consume `Hexalith.Works` from the umbrella-owned `references/Hexalith.Works` checkout, exposed as sibling `../Hexalith.Works`, or from an explicitly supplied `HexalithWorksRoot`. Timesheets must not declare, initialize, track, or prefer a repository-root `Hexalith.Works` checkout.

**Rationale:** The current wording does not distinguish umbrella-owned root
dependencies from nested submodules declared by Timesheets.

#### Epic 5 Coverage

**Current:**

> **FRs covered:** none directly. Epic 5 verifies evidence for FR/NFR coverage already delivered by Epics 1-4.

**Proposed:**

> **FRs covered:** FR23 boundary verification only. Epic 5 otherwise verifies evidence for FR/NFR coverage delivered by Epics 1-4.

**Rationale:** Story 5.3 directly enforces the sibling-module ownership boundary.

#### New Story 5.3

```markdown
### Story 5.3: Consume the Umbrella-Owned Hexalith.Works Checkout

**Requirements:** FR23 and the workspace dependency-ownership requirement

As a Hexalith build maintainer,
I want Timesheets to consume the umbrella-owned Hexalith.Works checkout,
So that one workspace-pinned Works source supplies validation and reporting contracts without nested submodule initialization or commit skew.

**Acceptance Criteria:**

**Given** the umbrella workspace declares `references/Hexalith.Works`
**When** Timesheets repository metadata is inspected
**Then** Timesheets `.gitmodules` does not declare `Hexalith.Works`
**And** the Timesheets Git index has no `Hexalith.Works` gitlink.

**Given** `HexalithWorksRoot` is not supplied explicitly
**When** Timesheets projects are evaluated inside the umbrella workspace
**Then** `HexalithWorksRoot` resolves to sibling `../Hexalith.Works`
**And** no Timesheets-root `./Hexalith.Works` probe or fallback exists.

**Given** a controlled standalone or CI build supplies `HexalithWorksRoot`
**When** Timesheets projects are evaluated
**Then** the supplied path is preserved
**And** Timesheets does not initialize or mutate any Works checkout.

**Given** checkout-governance fitness tests run
**When** `.gitmodules` and `Directory.Build.props` are inspected
**Then** tests fail if a Timesheets-owned Works declaration, gitlink assumption, or root-local path probe is reintroduced
**And** tests confirm the explicit-property and umbrella-sibling resolution contract.

**Given** the umbrella-owned Works checkout is available
**When** restore, build, ArchitectureTests, and Works.Tests run
**Then** the Works adapter and full Timesheets solution build with warnings as errors
**And** Stories 1.10 and 4.8 behavior remains green without a nested Works checkout.

**Given** boundary and build documentation is reviewed
**When** Works setup guidance is read
**Then** it identifies `<workspace>/references/Hexalith.Works` as the sole default checkout
**And** it does not instruct users or agents to initialize `Hexalith.Timesheets/Hexalith.Works`.
```

### 4.3 Architecture

#### Technical Constraint

**Current:**

> Root-level submodule rules are preserved; no recursive submodule initialization is introduced.

**Proposed:**

> The umbrella workspace owns the root-declared `references/Hexalith.Works` checkout. Timesheets consumes it through `HexalithWorksRoot`, defaulting to sibling `../Hexalith.Works`, and must not declare, initialize, track, or probe a Timesheets-root `Hexalith.Works` checkout. An explicitly supplied `HexalithWorksRoot` is allowed for controlled standalone/CI builds and must be treated as read-only dependency input.

#### Build Resolution Invariant

Add the following architecture invariant:

> `Hexalith.Timesheets.Works` continues to reference `$(HexalithWorksRoot)/src/Hexalith.Works.Contracts/Hexalith.Works.Contracts.csproj`. `Directory.Build.props` preserves a caller-supplied `HexalithWorksRoot`; otherwise it resolves only the umbrella sibling. Build governance tests must reject a root-local Works probe or Timesheets-owned Works gitlink.

#### Project Structure

Clarify that `Hexalith.Works/` is not part of the Timesheets repository tree. The
external dependency is located at `<workspace>/references/Hexalith.Works` beside
`<workspace>/references/Hexalith.Timesheets`.

### 4.4 UI/UX

**No edit proposed.** The checkout correction has no user-interface impact.

### 4.5 Sprint Status

**Current:**

```yaml
epic-5: in-progress
5-1-final-launch-readiness-gate-and-documentation-sync: done
5-2-reconcile-package-currency-and-platform-dependency-versions: done
epic-5-retrospective: done
```

**Proposed after approval:**

```yaml
epic-5: in-progress
5-1-final-launch-readiness-gate-and-documentation-sync: done
5-2-reconcile-package-currency-and-platform-dependency-versions: done
5-3-consume-umbrella-owned-hexalith-works-checkout: backlog
epic-5-retrospective: done
```

### 4.6 Repository Configuration

#### `.gitmodules`

**Remove:**

```ini
[submodule "Hexalith.Works"]
	path = Hexalith.Works
	url = https://github.com/Hexalith/Hexalith.Works.git
```

Also remove the tracked `Hexalith.Works` gitlink from the Timesheets repository.
Do not change or remove the umbrella's `references/Hexalith.Works` declaration.

#### `Directory.Build.props`

**Remove:**

```xml
<HexalithWorksRoot Condition="'$(HexalithWorksRoot)' == '' and Exists('$(MSBuildThisFileDirectory)Hexalith.Works\src\Hexalith.Works.Contracts')">$(MSBuildThisFileDirectory)Hexalith.Works</HexalithWorksRoot>
```

**Retain:**

```xml
<HexalithWorksRoot Condition="'$(HexalithWorksRoot)' == '' and Exists('$(MSBuildThisFileDirectory)..\Hexalith.Works\src\Hexalith.Works.Contracts')">$(MSBuildThisFileDirectory)..\Hexalith.Works</HexalithWorksRoot>
```

The condition already preserves an explicitly supplied `HexalithWorksRoot`.

### 4.7 Tests and Documentation

- Extend `ScaffoldGovernanceTests` to reject a Timesheets `.gitmodules` Works
  declaration.
- Extend `DependencyDirectionTests` to require the sibling Works fallback and reject
  the Timesheets-root Works probe.
- Keep Works adapter unit/composition tests unchanged; they validate behavior through
  the resolved contracts reference.
- Update `docs/boundary-decision-record.md` and `README.md` with the umbrella checkout
  ownership and explicit override rule.
- Update architecture, epics, and sprint status as proposed above.

### 4.8 Validation Commands

```bash
git ls-files --stage -- Hexalith.Works
dotnet msbuild src/Hexalith.Timesheets.Works/Hexalith.Timesheets.Works.csproj -nologo -getProperty:HexalithWorksRoot
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --no-build
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.Works.Tests/Hexalith.Timesheets.Works.Tests.csproj --no-build
```

Expected governance evidence:

- `git ls-files --stage -- Hexalith.Works` produces no output.
- `HexalithWorksRoot` evaluates to the umbrella sibling unless explicitly supplied.
- Restore, build, ArchitectureTests, and Works.Tests pass without initializing a
  Timesheets-local Works checkout.

## 5. Implementation Handoff

### Scope Classification

**Moderate.** The implementation is technically small, but it adds a backlog story
and synchronizes architecture, epics, sprint status, repository configuration,
tests, and developer documentation.

### Recipients and Responsibilities

| Recipient | Responsibility |
|---|---|
| Product Owner | Approve Story 5.3 placement and the no-MVP-impact classification. |
| Developer | Remove the Timesheets Works declaration/gitlink, constrain path resolution, add fitness tests, update documents, and run validation. |
| Reviewer | Verify no umbrella dependency was removed, no nested Works initialization is required, and the full build consumes the workspace-pinned Works commit. |

No Product Manager or Solution Architect escalation is required because product
scope, domain contracts, and runtime architecture remain unchanged.

### Success Criteria

1. Timesheets no longer declares or tracks `Hexalith.Works` as its own submodule.
2. The umbrella continues to track `references/Hexalith.Works`.
3. Default `HexalithWorksRoot` resolves only to the umbrella sibling; an explicit
   override remains supported.
4. Architecture fitness tests prevent the local declaration/probe from returning.
5. The Timesheets Works adapter and full solution build/test successfully against the
   umbrella-owned checkout.
6. Planning and developer documentation consistently describe checkout ownership.

## 6. Change Navigation Checklist Record

### Section 1 — Trigger and Context

- [x] 1.1 Trigger identified: umbrella commit `83ae767` added the root-declared
  `references/Hexalith.Works`; completed Timesheets Stories 1.1/1.10/4.8 carry the
  affected prior assumption.
- [x] 1.2 Core problem categorized and stated.
- [x] 1.3 Concrete Git, `.gitmodules`, MSBuild, and filesystem evidence collected.

### Section 2 — Epic Impact

- [x] 2.1 Existing epics remain completable; behavior is unchanged.
- [x] 2.2 Add Story 5.3 within Epic 5.
- [x] 2.3 Remaining/future work reviewed; only release/build governance is affected.
- [N/A] 2.4 No epic is invalidated and no product epic is required.
- [N/A] 2.5 No epic resequencing or product-priority change is required.

### Section 3 — Artifact Impact

- [x] 3.1 PRD reviewed; no conflict or MVP edit.
- [x] 3.2 Architecture sections requiring clarification identified.
- [N/A] 3.3 UX reviewed; no UI/flow/accessibility impact.
- [x] 3.4 Repository configuration, tests, docs, and sprint status identified.

### Section 4 — Path Forward

- [x] 4.1 Direct adjustment is viable: low effort, low residual risk.
- [N/A] 4.2 Rollback is unnecessary and would remove valid behavior.
- [N/A] 4.3 MVP review is unnecessary.
- [x] 4.4 Direct adjustment selected with rationale.

### Section 5 — Proposal Components

- [x] 5.1 Issue summary completed.
- [x] 5.2 Epic and artifact impact documented.
- [x] 5.3 Recommended path and alternatives documented.
- [x] 5.4 No MVP impact; implementation sequence defined.
- [x] 5.5 Product Owner/Developer/Reviewer handoff defined.

### Section 6 — Final Review and Handoff

- [x] 6.1 Applicable checklist sections addressed.
- [x] 6.2 Proposal checked against repository evidence.
- [x] 6.3 Approved by Jerome on 2026-07-20 with `approve`.
- [x] 6.4 Story 5.3 added to `sprint-status.yaml` as `backlog`.
- [x] 6.5 Product Owner/Developer/Reviewer handoff confirmed below.

## 7. Approval and Handoff Log

- **Approval:** Jerome approved the proposal on 2026-07-20.
- **Planning changes applied:** `epics.md` now contains the workspace ownership
  requirement and Story 5.3; `architecture.md` contains the Works checkout and
  `HexalithWorksRoot` invariants; `sprint-status.yaml` lists Story 5.3 as
  `backlog`.
- **Product scope:** unchanged; PRD and UX artifacts require no edits.
- **Implementation route:** Product Owner / Developer, with review focused on
  repository-boundary and dependency-resolution evidence.
- **Developer next step:** create/implement Story 5.3, removing only the
  Timesheets-owned Works declaration/gitlink while preserving the umbrella
  `references/Hexalith.Works` checkout.
- **Success gate:** architecture fitness tests, Works adapter tests, and the
  warnings-as-errors solution build pass against the umbrella-owned checkout.
