---
title: "Sprint Change Proposal - Package Currency and Platform Dependency Alignment"
status: draft
created: 2026-06-26
project: timesheets
change_trigger: "update packages"
mode: incremental
---

# Sprint Change Proposal: Package Currency and Platform Dependency Alignment

## 1. Issue Summary

Jerome requested `bmad-correct-course update packages` on 2026-06-26. The change was clarified as: update all NuGet/npm packages to the latest compatible versions.

The package audit found that the root Timesheets solution has no direct NuGet package updates available:

```text
dotnet list Hexalith.Timesheets.slnx package --outdated
=> every Timesheets project reports no direct updates.
```

There is no root `package.json`, `package-lock.json`, `pnpm-lock.yaml`, or `yarn.lock`, so npm package updates are not applicable to the Timesheets root today.

The same audit found transitive drift in restored packages, including `Google.Protobuf` `3.35.0 -> 3.35.1`, `Polly.*` `8.4.2 -> 8.7.0`, and test-stack transitives such as `Microsoft.Testing.Platform` `1.9.1 -> 2.2.3`, `DiffEngine` `11.3.0 -> 19.2.0`, `EmptyFiles` `4.4.0 -> 8.18.1`, and `Newtonsoft.Json` `13.0.3 -> 13.0.4`.

Launch readiness already records a related dependency concern:

```text
Doc-vs-dependency version divergence:
Root architecture targets Dapr 1.18.4 and Fluent UI V5, while build props include base Dapr 1.17.9, Aspire Hosting 13.4.6, and Fluent UI Components 4.11.6.
```

This means the change is not a simple root package bump. The Timesheets-owned package pins are already current, while the remaining package/version work is either transitive dependency pinning, documentation/readiness reconciliation, or platform/submodule coordination.

## 2. Impact Analysis

### Epic Impact

Epic 5 is affected because the request concerns release readiness, package currency, and dependency evidence. Epics 1-4 remain feature-complete and do not require scope changes.

Affected epic:

- Epic 5: Release Readiness Verification

Required change:

- Add Story 5.2: Reconcile Package Currency and Platform Dependency Versions.
- Reopen Epic 5 to `in-progress` until Story 5.2 is resolved.

### Story Impact

Existing stories remain valid. Story 5.1 stays done, but a new follow-up story is needed because package currency was not a first-class release gate in the current sprint status.

No completed feature story needs rollback.

### Artifact Conflicts

PRD:

- No PRD requirement changes are needed. The MVP scope and functional requirements remain achievable.

Epics:

- Add a new Epic 5 follow-up story for package currency and dependency alignment.

Architecture:

- No architecture rewrite is needed before implementation.
- Implementation may update architecture/readiness wording if the package decision changes platform dependency policy, especially around Dapr and Fluent UI.

UX Design:

- No UX flow or visual design changes are needed because there is no Timesheets UI project yet.
- If a future UI story adds package updates, it must preserve FrontComposer and Fluent UI V5 requirements.

Launch Readiness:

- `docs/launch-readiness.md` should be updated by the follow-up story with the final package-currency verdict.

Sprint Status:

- `sprint-status.yaml` should be updated after this proposal is approved.

### Technical Impact

Timesheets root:

- Direct package pins are already current.
- No root npm package manifests exist.
- Transitive drift should not be pinned blindly. Explicit transitive pins should be added only for compatibility, security, or deterministic build reasons.

Platform/submodules:

- Sibling submodules have independent package manifests and dirty or non-recorded submodule states.
- Whole-workspace package updates would require separate submodule-owned changes and should not be mixed into a Timesheets root change without explicit scope approval.

## 3. Recommended Approach

Recommended path: Direct Adjustment.

Add a focused Epic 5 follow-up story and keep implementation scoped:

1. Audit direct package pins in root `Directory.Packages.props`.
2. Record root npm as not applicable unless a root manifest is introduced.
3. Review transitive drift and avoid explicit transitive pins unless justified.
4. Reconcile the existing launch-readiness package/version divergence for Dapr, Fluent UI, Aspire, and Hexalith.Builds.
5. Keep submodule package updates split into owned submodule changes unless explicitly approved.
6. Run restore, build, architecture tests, and targeted affected tests with warnings as errors.
7. Update launch-readiness evidence with the final verdict.

Effort estimate: Low to Medium.

Risk level: Medium.

Rationale:

- Direct root package updates appear unnecessary because direct pins are already current.
- Transitive pinning can increase maintenance burden and conflict with sibling module package graphs.
- Platform dependency alignment touches shared `Hexalith.Builds` and sibling modules, so it should be handled deliberately.
- Reopening Epic 5 for a focused follow-up preserves the completed feature stories while making the dependency work visible.

Alternatives considered:

- Potential Rollback: Not viable. No feature implementation needs to be reverted to update packages.
- PRD MVP Review: Not viable. Package currency does not change the product MVP.
- Immediate package edits without a story: Not recommended because the known issue is partly a release-readiness/platform coordination concern.

## 4. Detailed Change Proposals

### Epics

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: Epic 5

OLD:

```md
### Story 5.1: Final Launch-Readiness Gate and Documentation Sync
```

NEW:

```md
### Story 5.2: Reconcile Package Currency and Platform Dependency Versions

As a release maintainer,
I want Timesheets package currency and platform dependency alignment verified,
So that launch-readiness does not overstate dependency versions or hide transitive/package drift.

Acceptance Criteria:
- Direct Timesheets package pins are audited and updated only when compatible.
- Root npm status is recorded as not applicable unless a root manifest is added.
- Transitive drift is reviewed before adding explicit transitive pins.
- Dapr, Fluent UI, Aspire, and Hexalith.Builds version divergence is resolved or kept as an explicit waiver.
- Submodule package updates are split into owned submodule changes unless explicitly approved.
- Restore, build, and relevant tests pass with warnings as errors.
- `docs/launch-readiness.md` is updated with the final package-currency verdict.
```

Rationale:

The request is package/readiness maintenance after Epic 5 completion. A focused follow-up story keeps the dependency decision visible without reopening feature scope.

### Sprint Status

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

Section: `development_status`

OLD:

```yaml
epic-5: done
5-1-final-launch-readiness-gate-and-documentation-sync: done
epic-5-retrospective: done
```

NEW:

```yaml
epic-5: in-progress
5-1-final-launch-readiness-gate-and-documentation-sync: done
5-2-reconcile-package-currency-and-platform-dependency-versions: backlog
epic-5-retrospective: done
```

Rationale:

The new story reopens Epic 5 for a bounded release-maintenance task. The completed retrospective remains recorded as done; if the team wants a follow-up retrospective after Story 5.2, that should be added separately.

### PRD

No PRD edits proposed.

Rationale:

The package update request does not change the product problem, MVP scope, functional requirements, non-goals, or success metrics.

### Architecture

No immediate architecture edits proposed.

Rationale:

The existing architecture already records centralized package management, Dapr `1.18.4` expectations, Fluent UI V5 policy, and package/version enforcement rules. Story 5.2 should update architecture only if implementation changes those decisions.

### UX Design

No UX edits proposed.

Rationale:

No Timesheets UI project exists today, and the request does not change UX flows or component behavior.

### Launch Readiness

Implementation story should update `docs/launch-readiness.md`.

Expected update:

- Add package-currency verdict.
- Record direct NuGet status.
- Record root npm status.
- Record transitive drift decision.
- Resolve or carry forward Dapr/Fluent/Aspire/Hexalith.Builds divergence.

## 5. Implementation Handoff

Scope classification: Moderate.

Reason:

The code/package edit may be small, but the change affects release-readiness evidence and potentially shared platform dependency policy. It should be tracked through backlog/status before implementation.

Handoff recipients:

- Product Owner / Developer: update backlog artifacts after proposal approval.
- Developer agent: implement Story 5.2 after backlog update.
- Platform/Architecture owner: approve any `Hexalith.Builds` or sibling-submodule package changes before they are made.

Success criteria:

- Direct root package update audit is recorded.
- No unsupported npm update claim is made for the root.
- Any explicit transitive pin has a documented reason.
- Platform dependency divergence is resolved or retained as an explicit waiver.
- Restore/build/tests pass with warnings as errors.
- Launch readiness reflects the package-currency outcome.

## Checklist Summary

- [x] 1.1 Triggering story identified: N/A; post-Epic-5 release-maintenance request.
- [x] 1.2 Core problem defined: package-currency/readiness and platform dependency alignment.
- [x] 1.3 Evidence collected: direct package audit, transitive audit, root npm absence, launch-readiness divergence, sprint status.
- [x] 2.1 Current epic impact assessed: Epic 5 affected.
- [x] 2.2 Epic-level changes identified: add Story 5.2.
- [x] 2.3 Remaining epics reviewed: Epics 1-4 unaffected.
- [x] 2.4 Future epic invalidation checked: no new epic needed.
- [x] 2.5 Epic order reviewed: no resequencing beyond reopening Epic 5.
- [x] 3.1 PRD conflicts checked: none.
- [x] 3.2 Architecture conflicts checked: existing package/version divergence remains relevant.
- [x] 3.3 UX conflicts checked: none.
- [x] 3.4 Secondary artifacts checked: launch readiness and sprint status need updates.
- [x] 4.1 Direct Adjustment evaluated: viable.
- [N/A] 4.2 Potential Rollback evaluated: not viable.
- [N/A] 4.3 PRD MVP Review evaluated: not needed.
- [x] 4.4 Recommended path selected: Direct Adjustment.
- [x] 5.1 Issue summary created.
- [x] 5.2 Epic and artifact impacts documented.
- [x] 5.3 Recommended path documented.
- [x] 5.4 MVP impact and action plan documented.
- [x] 5.5 Handoff plan documented.
- [x] 6.1 Checklist reviewed.
- [x] 6.2 Proposal drafted.
- [!] 6.3 User approval pending.
- [!] 6.4 Sprint status update pending approval.
- [!] 6.5 Final handoff pending approval.
