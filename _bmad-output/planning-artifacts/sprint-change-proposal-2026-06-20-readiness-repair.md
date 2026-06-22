---
project: timesheets
date: 2026-06-20
workflow: bmad-correct-course
status: approved-and-applied
mode: batch
trigger: "_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-20.md"
sourceProposalPreserved: "_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-20.md"
scope_classification: "Major planning correction; no MVP reduction; code rollback not recommended"
outputLanguage: English
approved: 2026-06-20
applied: 2026-06-20
applied_edits:
  - "PRD Phase 5 reframed as release-readiness verification"
  - "Architecture launch-readiness notes reassigned to owning feature epics"
  - "Epics restructured so Epic 5 is verification-only"
  - "Owning-epic follow-up stories added under Epics 1, 3, and 4"
  - "Sprint status reopened Epics 1, 3, and 4 and replaced Epic 5 feature backlog with final gate"
---

# Sprint Change Proposal: Readiness Repair and Epic Dependency Correction

## 1. Issue Summary

The implementation-readiness assessment completed on 2026-06-20 with status
`NOT READY`. The report found 14 issues:

- 2 document discovery / traceability blockers.
- 4 critical epic dependency violations.
- 5 major story readiness issues.
- 3 minor structure and documentation concerns.

The trigger is not a product pivot. The PRD, architecture, UX spine, and feature
scope are still coherent. The issue is planning discipline: the latest planning
state added Epic 5 as a launch-readiness bucket, but several Epic 5 stories are
not final verification work. They are prerequisite implementation work required
for earlier feature epics to honestly satisfy their own user-facing claims.

The existing file
`_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-20.md` is
marked `approved-and-applied` and records the previous correction that created
Epic 5. This proposal preserves that audit trail and creates a follow-up repair
instead of overwriting the applied record.

### Evidence

| Finding | Evidence |
| --- | --- |
| Discovery missed source documents | Readiness report frontmatter has `documentsIncluded.prd: []` and `documentsIncluded.ux: []`, then later discovers `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md` and UX files under `ux-designs/ux-timesheets-2026-06-18/`. |
| PRD traceability can be repaired | The PRD contains FR-1 through FR-23; `epics.md` contains a 23-FR inventory and coverage map. The failed extraction is a discovery-path problem, not absent requirements. |
| Epic 5 is a hardening bucket | `epics.md` defines Epic 5 as "Launch Readiness & Integration Hardening" and covers FR2, FR14, FR17, FR18, FR19, FR21, FR22, FR23, which are already owned by Epics 1, 3, and 4. |
| Magic-link live behavior depends on later work | Architecture states the registered `IMagicLinkConfirmationCapabilityStateLoader` is still `UnavailableMagicLinkConfirmationCapabilityStateLoader`; Epic 5 Story 5.1 is the first concrete loader work, but Epic 3 Story 3.3 already claims live confirmation. |
| Work reference/reporting depends on later work | Story 1.7 and Story 4.3 depend on the Works consumer-query/adapter decision; Epic 5 Story 5.3 is the first concrete adapter implementation story. |
| Export preview is undecided after export story | Story 4.5 requires preview before export; Epic 5 Story 5.4 still decides whether preview is a dedicated public query contract or ledger-query/readiness behavior. |
| Performance evidence is concentrated too late | Epic 5 Story 5.5 owns NFR10/NFR11 evidence instead of the stories that introduce command/report/export/dashboard paths. |

## 2. Impact Analysis

### Epic Impact

| Epic | Impact |
| --- | --- |
| Epic 1: Trusted Time Capture & Activity Governance | Needs a concrete Work-reference validation slice or Story 1.7 must be explicitly narrowed to Project-only until the Work adapter exists. |
| Epic 2: Submission, Approval, Period Review & Corrections | No structural dependency violation found. Keep as complete after policy wording is checked against PRD open questions. |
| Epic 3: External Contributor Confirmation | Must own the EventStore-backed magic-link state loader and HTTP-boundary no-disclosure proof before it claims launch-ready external confirmation. |
| Epic 4: Approved Time Ledger, Reporting & Finance Export | Must own Works planned-effort/report adapter maturity and export-preview semantics before export/report stories are considered launch-ready. |
| Epic 5: Launch Readiness & Integration Hardening | Must be removed as a feature-completion bucket or reframed as release verification only. It should not contain first implementation of FR-scoped behavior. |

### Story Impact

Current backlog artifacts show Epics 1-4 and all 29 original stories as `done`.
Because implementation artifacts already exist, the least disruptive repair is
additive:

- Preserve existing completed story files and history.
- Add follow-up stories inside the owning epics for prerequisite work.
- Reopen the owning epic status where follow-up stories are added.
- Reframe Epic 5 as a release-readiness verification gate with only final
  evidence aggregation, waiver classification, and documentation sync.

For a future greenfield replay, the canonical order should place those same
stories before their dependent feature stories. For this repository, additive
follow-up story IDs avoid renumbering dozens of implementation artifacts.

### Artifact Conflicts

| Artifact | Conflict | Required correction |
| --- | --- | --- |
| Readiness report | The report extracted 0 PRD FRs because discovery missed nested PRD/UX documents. | Repair or rerun readiness with the actual PRD/UX inventory. |
| PRD | Phase 5 now reads as feature hardening, which encourages a technical bucket. | Change Phase 5 to release-readiness verification only; move concrete feature-completion work back to owning phases. |
| Epics | Epic 5 contains FR-scoped behavior required by Epics 1, 3, and 4. | Move or duplicate follow-up stories into owning epics; remove feature FR coverage from Epic 5. |
| Architecture | Notes currently say Epic 5 promotes caveats to implementation scope. | Update notes so loader/Works/export preview implementation belongs to owning epic stories; release gate only verifies. |
| Sprint status | Epic 5 backlog keys now hide dependencies for done epics. | Reopen affected epics and add owning-epic follow-up keys; reduce Epic 5 to final gate. |
| UX | No substantive UX conflict found. | Keep existing UX; ensure magic-link and export follow-up stories preserve UX requirements. |

### Technical Impact

No code rollback is recommended. The implementation records indicate Epics 1-4
are story-complete with explicit launch-readiness caveats. The correction should
make those caveats first-class backlog under the owning epics before further
implementation claims are made.

Expected technical work after approval:

- EventStore-backed magic-link capability/time-entry/catalog state loading.
- HTTP-route no-disclosure equivalence tests for magic-link invalid states.
- Concrete Works reference/planned-effort adapter or explicit unavailable launch policy.
- Export preview decision and selected implementation or contract cleanup.
- Feature-owned performance evidence for command/report/export/dashboard paths.

## 3. Recommended Approach

Selected path: **Hybrid Direct Adjustment + Backlog Reorganization**.

| Option | Verdict | Rationale |
| --- | --- | --- |
| Direct Adjustment | Partially selected | Most fixes are planning-artifact and sprint-status corrections. |
| Potential Rollback | Not selected | Completed code/story work is useful; rollback does not simplify the missing adapter/test work. |
| MVP Review | Not selected | MVP scope still holds. The problem is sequencing and launch-readiness honesty, not scope size. |
| Hybrid | Selected | Requires direct artifact edits plus reopening/re-homing backlog items into owning epics. |

Effort estimate: **Medium** for planning artifacts and sprint-status repair;
**Medium-High** for subsequent implementation of the rehomed follow-up stories.

Risk level: **Medium**. The highest-risk implementation items are the live
magic-link loader and Works adapter because they cross security and sibling
module boundaries. The planning repair itself is straightforward but touches
completed epic status.

Timeline impact: implementation should not proceed from Epic 5 as currently
structured. The next implementation handoff should start with the owning-epic
follow-up stories listed below.

## 4. Detailed Change Proposals

### 4A. Readiness Discovery and Traceability

Artifact: `_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-20.md`

Change type: repair or rerun, not hand-edit only.

OLD:

```yaml
documentsIncluded:
  prd: []
  ux: []
documentWarnings:
  - PRD document not found under required discovery patterns.
  - UX design document not found under required discovery patterns.
```

NEW:

```yaml
documentsIncluded:
  prd:
    - _bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md
  ux:
    - _bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md
    - _bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/DESIGN.md
    - _bmad-output/planning-artifacts/ux-designs/ux-timesheets-2026-06-18/.decision-log.md
documentWarnings: []
```

Required validation:

- Extract PRD FR-1 through FR-23 from the actual PRD.
- Compare PRD FRs to the epics FR inventory and coverage map.
- Keep any true FR mismatch as a new finding; do not keep the "0 PRD FRs" result.

Rationale: readiness cannot be used for implementation sign-off while its source
inventory excludes the canonical PRD and UX documents.

### 4B. PRD Rollout Language

Artifact: `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`

Section: `13. Rollout and Launch Readiness`

OLD:

```markdown
- **Phase 5 - Launch-readiness hardening.** Replace deferred fail-closed integration defaults with concrete launch-scope adapters and executable evidence: Magic-Link state loading, Works planned-effort/reference integration, export preview API handling, HTTP no-disclosure proof, and realistic performance fixtures.

Launch readiness requires: passing tenant-isolation tests, approval/correction evidence tests, projection rebuild tests, export contract tests, privacy/logging scans, a documented owns-vs-references boundary decision, concrete launch-scope integration adapters, and performance evidence for the stated command/report targets.
```

NEW:

```markdown
- **Phase 5 - Release-readiness verification.** Aggregate evidence, waivers, documentation sync, and launch decision records after feature behavior has already been implemented in the phase that introduced it. Phase 5 must not be the first implementation location for Magic-Link state loading, Work-reference validation, planned-effort reporting, export preview behavior, or performance evidence required by earlier phases.

Launch readiness requires: passing tenant-isolation tests, approval/correction evidence tests, projection rebuild tests, export contract tests, privacy/logging scans, a documented owns-vs-references boundary decision, concrete launch-scope integration adapters implemented in their owning feature phases, and performance evidence for the stated command/report targets.
```

Rationale: this keeps launch verification explicit without turning Phase 5 into a
technical bucket that completes earlier feature promises late.

### 4C. Epic 5 Reframe

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: `Epic List`

OLD:

```markdown
### Epic 5: Launch Readiness & Integration Hardening

The completed module is hardened for launch by replacing deferred fail-closed integration defaults with concrete launch-scope adapters and executable quality evidence.

**FRs covered:** FR2, FR14, FR17, FR18, FR19, FR21, FR22, FR23
```

NEW:

```markdown
### Epic 5: Release Readiness Verification

The release owner verifies launch evidence, waivers, documentation consistency, and final gate status after feature-completion work has been implemented in the owning epics.

**FRs covered:** none directly. Epic 5 verifies evidence for FR/NFR coverage already delivered by Epics 1-4.
```

Rationale: Epic 5 may remain as a release-owner validation container, but it must
not claim FR coverage or contain the first implementation of feature behavior.

### 4D. Rehome Epic 5 Feature-Completion Stories

Artifact: `_bmad-output/planning-artifacts/epics.md`

Current Epic 5 stories should be rehomed as follows.

| Current story | Proposed owning epic | Proposed additive story ID for this repository | Canonical greenfield placement |
| --- | --- | --- | --- |
| 5.1 Implement EventStore-Backed Magic-Link State Loading | Epic 3 | 3.6 Implement EventStore-Backed Magic-Link State Loading | Before live confirm/adjust stories. |
| 5.2 Prove Magic-Link No-Disclosure at the HTTP Boundary | Epic 3 | 3.7 Prove Magic-Link No-Disclosure at the HTTP Boundary | With or immediately after invalid-link behavior. |
| 5.3 Implement the Works Reference and Planned-Effort Adapter Path | Split across Epic 1 and Epic 4 | 1.10 Implement Work Reference Validation Adapter; 4.8 Implement Works Planned-Effort Reporting Adapter | Work validation before trust-bearing Work capture; planned effort before Work reports. |
| 5.4 Implement the Approved Export Preview API Decision | Epic 4 | 4.9 Resolve and Implement Approved Export Preview Behavior | Before finance export implementation. |
| 5.5 Activate Realistic Performance Evidence | Owning feature epics, plus final gate | 1.11 Add Command Performance Evidence; 4.10 Add Report Export Dashboard Performance Evidence | Attach minimal evidence to hot command/query/report/export stories; aggregate later. |
| 5.6 Final Launch-Readiness Gate and Documentation Sync | Epic 5 | 5.1 Final Launch-Readiness Gate and Documentation Sync | Final release verification only. |

Rationale: this preserves current implementation artifacts without renumbering
done story files, while making the backlog honest about which epic owns each
remaining launch-scope behavior.

### 4E. Story 1.1 Trim

Artifact: `_bmad-output/planning-artifacts/epics.md`

Story: `1.1 Set Up Initial Timesheets Project from Hexalith Module Scaffold`

OLD includes acceptance criteria for scaffold plus EventStore integration
points, authorization/reference-validation abstractions, UI/metadata entry
points, privacy-safe logging, performance harness placeholders, build, and
baseline tests.

NEW:

```markdown
Story 1.1 remains the scaffold/build/package/test foundation story. It creates the solution, project shell, Central Package Management, build props, architecture tests, initial EventStore package boundary, and test lane shape.

Move feature behavior out of Story 1.1:

- Tenant/resource authorization implementation belongs in Story 1.2 and later path-specific stories.
- FrontComposer metadata belongs in Story 1.3 and UI-bearing stories.
- Retention/comment/logging policy belongs in Story 1.4.
- Performance evidence placeholders may remain as test-lane scaffolding, but measurable NFR10/NFR11 evidence belongs to the stories that introduce the measured paths.
```

Rationale: the scaffold story should establish the module foundation without
pretending to prove future feature paths.

### 4F. Story 1.2 Current-Path Scope

Artifact: `_bmad-output/planning-artifacts/epics.md`

Story: `1.2 Enforce Tenant and Resource Authorization Gates`

OLD:

```markdown
Given a Timesheets command, query, projection read, export request, or confirmation request enters the host
When tenant, user, Project, Work, or Party authority cannot be resolved
Then the request fails closed before aggregate load, command dispatch, projection disclosure, export, or magic-link disclosure
```

NEW:

```markdown
Given an executable Timesheets path exists in the current story scope
When tenant, user, Project, Work, or Party authority cannot be resolved for that path
Then the request fails closed before aggregate load, command dispatch, projection disclosure, export, or magic-link disclosure
And every later story that introduces a new command, query, projection read, export, or confirmation path must add path-specific authorization tests for that path.
```

Rationale: Story 1.2 should establish the reusable gate and prove it on current
executable paths. Future paths must carry their own tests when introduced.

### 4G. Works Reference Split

Artifact: `_bmad-output/planning-artifacts/epics.md`

Stories: `1.7`, `4.3`, current `5.3`

OLD:

```markdown
Story 1.7 records draft time against Project or Work, with the Work path gated on a later Works consumer query/adapter decision.
Story 4.3 produces Project and Work actual-time reports, with planned-vs-actual depending on a later Works adapter decision.
Story 5.3 implements the Works reference and planned-effort adapter path.
```

NEW:

```markdown
Add Story 1.10: Implement Work Reference Validation Adapter

As a contributor or system integrator,
I want Work references validated through a concrete Works-owned query or approved adapter bridge,
So that trust-bearing Work capture does not depend on unavailable defaults.

Acceptance criteria:
- Trust-bearing Work submission, approval, correction, export, and magic-link confirmation fail closed when Works authority is missing, stale, unauthorized, ambiguous, or unavailable.
- Timesheets stores stable Work references and source/freshness metadata only.
- No Work lifecycle state, names, descriptions, or ownership details are copied into durable Timesheets events.

Add Story 4.8: Implement Works Planned-Effort Reporting Adapter

As a work reviewer,
I want planned-vs-actual comparison backed by a concrete Works adapter or explicit unavailable launch policy,
So that Work reports do not imply planned-effort integration that does not exist.

Acceptance criteria:
- Work actual-time reports either display source-attributed planned effort from Works with freshness metadata or explicitly mark planned effort unavailable.
- Planned-vs-actual report tests cover available, unavailable, stale, unauthorized, and cross-tenant Works states.
```

Rationale: Work validation and Work planned effort are related but not identical.
Splitting them keeps Epic 1 capture and Epic 4 reporting independent.

### 4H. Magic-Link Loader and HTTP Boundary Proof

Artifact: `_bmad-output/planning-artifacts/epics.md`

Stories: `3.3`, `3.4`, `3.5`, current `5.1`, current `5.2`

OLD:

```markdown
Story 3.3 confirms time through a magic link.
Story 3.4 adjusts time through a magic link.
Story 3.5 rejects invalid confirmation links without disclosure.
Story 5.1 later implements EventStore-backed magic-link state loading.
Story 5.2 later proves no-disclosure at the HTTP boundary.
```

NEW:

```markdown
Add Story 3.6: Implement EventStore-Backed Magic-Link State Loading

As an external contributor,
I want valid magic links to load scoped confirmation state in the live host,
So that confirmation and adjustment work outside service-only tests without weakening no-disclosure behavior.

Add Story 3.7: Prove Magic-Link No-Disclosure at the HTTP Boundary

As a security reviewer,
I want executable HTTP-boundary tests for magic-link invalid states,
So that service-level no-disclosure behavior is proven at the routes external contributors actually hit.
```

Add dependency notes to Stories 3.3 and 3.4:

```markdown
Launch-readiness dependency: live host confirmation/adjustment requires Story 3.6. Before that story is complete, service-layer behavior may be story-complete but external live endpoints remain fail-closed/unavailable by default.
```

Rationale: FR14 and UX-DR31 require live valid-link detail before confirmation.
That cannot be deferred to a later technical bucket if Epic 3 claims launch-ready
external confirmation.

### 4I. Export Preview Decision Before Export

Artifact: `_bmad-output/planning-artifacts/epics.md`

Stories: `4.5`, current `5.4`

OLD:

```markdown
Story 4.5 previews export scope before export.
Story 5.4 later decides whether `PreviewApprovedTimeExport` is a dedicated handler or ledger-query/readiness behavior.
```

NEW:

```markdown
Add Story 4.9: Resolve and Implement Approved Export Preview Behavior

As a finance consumer,
I want export preview behavior to match the public contract,
So that downstream users know whether preview is a first-class API operation or ledger-query readiness semantics.

Acceptance criteria:
- The team chooses exactly one preview behavior before implementation starts.
- If `PreviewApprovedTimeExport` remains public, a dedicated handler is implemented and tested.
- If preview is ledger-query driven, contracts, metadata, OpenAPI, README, and UX copy do not imply a separate endpoint.
- Unauthorized, cross-tenant, stale, unavailable, no-row, corrected/superseded, billable-filter, and comment-redaction cases are tested for the selected behavior.
```

Rationale: a decision story with mutually exclusive branches is not
implementation-ready. The selected branch must be known before Story 4.5's
export surface can be treated as launch-ready.

### 4J. Performance Evidence Ownership

Artifact: `_bmad-output/planning-artifacts/epics.md`

Current story: `5.5 Activate Realistic Performance Evidence`

OLD:

```markdown
Story 5.5 centralizes command acknowledgement and report query performance evidence after feature stories are complete.
```

NEW:

```markdown
Move NFR10/NFR11 evidence into feature-owned stories:

- Story 1.11: Add Command Performance Evidence for Capture and Governance Paths.
- Story 4.10: Add Report, Export, and Dashboard Performance Evidence.
- Epic 5 final gate aggregates evidence and records waivers; it does not create the first measurement path.
```

Rationale: feature stories should not be marked launch-ready while their
launch-relevant NFR evidence is first introduced later.

### 4K. Policy Inputs Before Affected Stories

Artifacts: PRD, epics, architecture

Open PRD questions still affecting implementation:

- Q6 Activity Type governance: who can define project-level Activity Types, and can a project restrict tenant-level defaults?
- Q7 Export format: CSV-only v1 or structured API/webhook at launch?
- Q8 Comment sensitivity: allowed comment content, classification, redaction, display, and export policy.

Proposal:

```markdown
Resolve Q6 before finalizing Stories 1.5 and 1.6 launch claims.
Resolve Q7 before Story 4.9 and Story 4.5 launch claims.
Resolve Q8 before Story 1.4, Story 4.2, Story 4.5, and Story 4.6 launch claims.

If a policy cannot be fully resolved, the owning story must say it delivers policy scaffolding only and must name the launch gate or post-v1 deferral.
```

Rationale: `where policy allows` is acceptable scaffolding language only when
the story is explicit about whether the policy itself is launch-ready.

### 4L. Sprint Status Reorganization

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

OLD:

```yaml
  # Epic 5: Launch Readiness & Integration Hardening
  epic-5: backlog
  5-1-implement-eventstore-backed-magic-link-state-loading: backlog
  5-2-prove-magic-link-no-disclosure-at-the-http-boundary: backlog
  5-3-implement-the-works-reference-and-planned-effort-adapter-path: backlog
  5-4-implement-the-approved-export-preview-api-decision: backlog
  5-5-activate-realistic-performance-evidence: backlog
  5-6-final-launch-readiness-gate-and-documentation-sync: backlog
  epic-5-retrospective: optional
```

NEW:

```yaml
  # Epic 1: Trusted Time Capture & Activity Governance
  epic-1: in-progress
  1-10-implement-work-reference-validation-adapter: backlog
  1-11-add-command-performance-evidence-for-capture-and-governance-paths: backlog

  # Epic 3: External Contributor Confirmation
  epic-3: in-progress
  3-6-implement-eventstore-backed-magic-link-state-loading: backlog
  3-7-prove-magic-link-no-disclosure-at-the-http-boundary: backlog

  # Epic 4: Approved Time Ledger, Reporting & Finance Export
  epic-4: in-progress
  4-8-implement-works-planned-effort-reporting-adapter: backlog
  4-9-resolve-and-implement-approved-export-preview-behavior: backlog
  4-10-add-report-export-and-dashboard-performance-evidence: backlog

  # Epic 5: Release Readiness Verification
  epic-5: backlog
  5-1-final-launch-readiness-gate-and-documentation-sync: backlog
  epic-5-retrospective: optional
```

Rationale: sprint status should reveal which owning epic still has launch-scope
work instead of hiding all prerequisites under a final technical bucket.

## 5. Checklist Execution Summary

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story | [x] | Trigger is the 2026-06-20 readiness report, especially Epic 5 follow-up stories blocking earlier epics. |
| 1.2 Core problem | [x] | Misunderstanding of readiness sequencing and failed document discovery; not a product pivot. |
| 1.3 Supporting evidence | [x] | Evidence from readiness report, PRD, UX, architecture, epics, sprint status, phase continuation notes, and retrospectives. |
| 2.1 Current epic viability | [!] | Epics 1, 3, and 4 need reopened follow-up scope or explicit narrowed launch claims. |
| 2.2 Epic-level changes | [!] | Reframe Epic 5; rehome feature-completion work to Epics 1, 3, and 4. |
| 2.3 Remaining epic review | [x] | Epic 2 remains structurally sound. |
| 2.4 New/obsolete epics | [x] | No new product epic; Epic 5 becomes verification-only. |
| 2.5 Priority/order | [!] | Follow-up stories must run before any launch-ready claim for affected feature paths. |
| 3.1 PRD conflicts | [!] | Phase 5 wording should be changed to verification-only. |
| 3.2 Architecture conflicts | [!] | Architecture notes should assign concrete implementation to owning epics, not Epic 5. |
| 3.3 UX conflicts | [x] | UX is coherent; follow-up stories must preserve it. |
| 3.4 Other artifacts | [!] | Sprint status and readiness report need correction. |
| 4.1 Direct adjustment | [x] | Viable for document and sprint-status changes. |
| 4.2 Rollback | [N/A] | Code rollback not useful. |
| 4.3 MVP review | [N/A] | MVP unchanged. |
| 4.4 Recommended path | [x] | Hybrid direct adjustment plus backlog reorganization. |
| 5.1 Issue summary | [x] | Included. |
| 5.2 Impact and artifact needs | [x] | Included. |
| 5.3 Recommended path | [x] | Included. |
| 5.4 MVP/action plan | [x] | No MVP reduction; action plan is rehomed follow-up stories. |
| 5.5 Handoff plan | [x] | Defined below. |
| 6.1 Checklist completion | [x] | Applicable sections addressed and approved. |
| 6.2 Proposal accuracy | [x] | Applied state matches the approved proposal. |
| 6.3 User approval | [x] | Approved by Jerome on 2026-06-20 with `yes`. |
| 6.4 Sprint status update | [x] | Applied in `_bmad-output/implementation-artifacts/sprint-status.yaml`. |
| 6.5 Handoff plan | [x] | Defined below. |

## 6. Implementation Handoff

Scope classification: **Major** for planning/backlog governance, **Moderate**
for artifact editing, and **Medium-High** for subsequent implementation.

Route to:

- Product Manager / Product Owner: approval recorded for the rehoming strategy and reopening affected completed epics in sprint status.
- Architect: confirm Works adapter strategy, magic-link loader architecture, export preview shape, and performance evidence ownership.
- Developer agent: planning edits are applied; next implementation step is to run `bmad-create-story` for the first rehomed backlog item.
- Test Architect: define HTTP no-disclosure and performance evidence criteria before the affected implementation stories begin.

Success criteria:

1. A corrected readiness run includes the real PRD and UX documents and validates FR-1 through FR-23 against the epics.
2. Epic 5 no longer contains first implementation of FR-scoped feature behavior.
3. Work-reference, magic-link, export-preview, and performance evidence work is visible under the owning epics.
4. Sprint status no longer reports Epics 1, 3, and 4 as fully launch-ready while their rehomed launch-scope follow-ups remain backlog.
5. PRD, architecture, epics, sprint status, and implementation-story records use the same distinction between story-complete and launch-ready.

## 7. Approval Request

Approved by Jerome on 2026-06-20 and applied to the planning and sprint-status artifacts.
