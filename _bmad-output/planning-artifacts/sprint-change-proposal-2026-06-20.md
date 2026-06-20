---
project: timesheets
date: 2026-06-20
workflow: bmad-correct-course
status: approved-and-applied
mode: batch
trigger: implement missing or deferred launch-readiness work
outputLanguage: English
---

# Sprint Change Proposal: Launch-Readiness Integration Hardening

## 1. Issue Summary

The completed Timesheets implementation has story-complete Epics 1-4, but retrospectives and continuation notes identify deferred launch-readiness work that should no longer remain hidden caveats:

- Live magic-link confirm/adjust endpoints still depend on a fail-closed `UnavailableMagicLinkConfirmationCapabilityStateLoader`.
- Magic-link no-disclosure behavior is strongly covered at service/workflow level, but HTTP-boundary equivalence tests remain deferred.
- Work planned-effort and Work validation still depend on a concrete Works adapter/query decision.
- `PreviewApprovedTimeExport` exists as a contract, while the implemented behavior is currently ledger-query/readiness driven rather than a dedicated server preview handler.
- Performance evidence remains reserved, not measured against realistic EventStore-backed persisted fixtures.

The trigger was the user instruction: `$bmad-correct-course implement missing or deferred work`. I treated this as approval to apply batch planning/backlog changes for the deferred work.

## 2. Impact Analysis

### Epic Impact

| Epic | Impact |
| --- | --- |
| Epic 1 | No reopening. Its AI metric and performance harness work remains valid. NFR10 now receives concrete follow-up coverage in Epic 5. |
| Epic 2 | No reopening. Approval/correction behavior remains valid. Launch-readiness review must keep policy/default assumptions explicit. |
| Epic 3 | No reopening. External contribution and magic-link stories remain done, but live magic-link loading and HTTP-boundary proof move into Epic 5. |
| Epic 4 | No reopening. Reporting/export/dashboard remain done, but Works adapter, export preview API clarity, and performance evidence move into Epic 5. |
| Epic 5 | Added: Launch Readiness & Integration Hardening. |

### Story Impact

No completed story is rolled back or marked incomplete.

New backlog stories added:

- 5.1 Implement EventStore-Backed Magic-Link State Loading.
- 5.2 Prove Magic-Link No-Disclosure at the HTTP Boundary.
- 5.3 Implement the Works Reference and Planned-Effort Adapter Path.
- 5.4 Implement the Approved Export Preview API Decision.
- 5.5 Activate Realistic Performance Evidence.
- 5.6 Final Launch-Readiness Gate and Documentation Sync.

### Artifact Conflicts

| Artifact | Adjustment |
| --- | --- |
| PRD | Add Phase 5 launch-readiness hardening to rollout plan and launch-readiness criteria. |
| Epics | Add Epic 5 and update NFR coverage rows for launch-readiness evidence. |
| Architecture | Add launch-readiness notes for magic-link loader, Works adapter, export preview, and performance evidence. |
| UX | No UX rewrite required. Existing no-disclosure, Fluent UI, dashboard, and report guidance remains valid. |
| Sprint Status | Add Epic 5 and story backlog keys. |

### Technical Impact

The change is implementation-heavy but architecturally narrow:

- EventStore-backed token-hash/capability fold and scoped Time Entry state loading.
- HTTP test infrastructure for magic-link route equivalence.
- Works adapter integration without copying Work state.
- Export preview handler or explicit contract/metadata clarification.
- Performance fixtures and evidence lane activation.

No dependency upgrade, rollback, submodule update, or PRD MVP reduction is required.

## 3. Recommended Approach

Recommended path: **Direct Adjustment**.

Rationale:

- The deferred items are launch-readiness gaps, not invalid completed stories.
- Rollback would discard working domain/service/projection behavior and does not simplify the missing adapter/test work.
- MVP scope still holds; this change clarifies what must be concrete before launch claims are made.

Effort estimate: **Medium-High**.

Risk level: **Medium**. The highest-risk items are the EventStore-backed magic-link loader and Works adapter because they touch cross-module integration and no-disclosure security. The work is still bounded by existing seams.

## 4. Detailed Change Proposals

### PRD Change

Section: `13. Rollout and Launch Readiness`

OLD:

```markdown
- **Phase 4 — Finance evidence.** Harden Approved-Time Ledger export and reconciliation flows for downstream billing/accounting consumers.

Launch readiness requires: passing tenant-isolation tests, approval/correction evidence tests, projection rebuild tests, export contract tests, privacy/logging scans, and a documented owns-vs-references boundary decision.
```

NEW:

```markdown
- **Phase 4 — Finance evidence.** Harden Approved-Time Ledger export and reconciliation flows for downstream billing/accounting consumers.
- **Phase 5 — Launch-readiness hardening.** Replace deferred fail-closed integration defaults with concrete launch-scope adapters and executable evidence: Magic-Link state loading, Works planned-effort/reference integration, export preview API handling, HTTP no-disclosure proof, and realistic performance fixtures.

Launch readiness requires: passing tenant-isolation tests, approval/correction evidence tests, projection rebuild tests, export contract tests, privacy/logging scans, a documented owns-vs-references boundary decision, concrete launch-scope integration adapters, and performance evidence for the stated command/report targets.
```

Rationale: Phase 5 makes deferred readiness work explicit without changing v1 product scope.

### Epics Change

Section: `Epic List`

OLD:

```markdown
### Epic 4: Approved Time Ledger, Reporting & Finance Export

Authorized users can query time entries, report actual time by Project and Work, inspect AI effort separately from human/external effort, use a rebuildable Approved-Time Ledger, and export approved billable evidence with stable IDs and correction lineage.

**FRs covered:** FR16, FR17, FR18, FR19, FR20
```

NEW:

```markdown
### Epic 4: Approved Time Ledger, Reporting & Finance Export

Authorized users can query time entries, report actual time by Project and Work, inspect AI effort separately from human/external effort, use a rebuildable Approved-Time Ledger, and export approved billable evidence with stable IDs and correction lineage.

**FRs covered:** FR16, FR17, FR18, FR19, FR20

### Epic 5: Launch Readiness & Integration Hardening

The completed module is hardened for launch by replacing deferred fail-closed integration defaults with concrete launch-scope adapters and executable quality evidence.

**FRs covered:** FR2, FR14, FR17, FR18, FR19, FR21, FR22, FR23
```

Rationale: Adds a clear implementation container for deferred work instead of silently reopening completed epics.

### New Story Changes

Story: `5.1 Implement EventStore-Backed Magic-Link State Loading`

NEW:

```markdown
Live magic-link endpoints must resolve token hash, fold EventStore-backed capability state, fold scoped Time Entry state, and load a fresh Activity Type catalog while preserving no-disclosure for all invalid states.
```

Rationale: Replaces the fail-closed unavailable loader where live external confirmation is launch scope.

Story: `5.2 Prove Magic-Link No-Disclosure at the HTTP Boundary`

NEW:

```markdown
Route-level GET/POST confirm/adjust tests must prove equivalent status, content type, ProblemDetails shape, headers, and sensitive-field absence for malformed, unknown, expired, used, revoked, unauthorized, cross-tenant, wrong-recipient, and replay cases.
```

Rationale: Moves no-disclosure evidence from service-only confidence to external HTTP behavior.

Story: `5.3 Implement the Works Reference and Planned-Effort Adapter Path`

NEW:

```markdown
Work validation and planned-vs-actual reporting must use either a Works-owned consumer query or a Timesheets adapter over a Works EventStore projection; unavailable/stale/unauthorized Work data fails closed or reports planned effort as unavailable.
```

Rationale: Turns the Works query/adapter gap into concrete implementation scope.

Story: `5.4 Implement the Approved Export Preview API Decision`

NEW:

```markdown
Either implement a dedicated `PreviewApprovedTimeExport` server handler or make contracts/metadata/docs explicit that preview readiness is served by Approved-Time Ledger query output and export generation results.
```

Rationale: Removes ambiguity between public contract shape and implemented behavior.

Story: `5.5 Activate Realistic Performance Evidence`

NEW:

```markdown
Performance tests must run against realistic persisted fixtures in an isolated lane and report p95 evidence for command acknowledgement, report query, export, dashboard, and launch-scope integration paths.
```

Rationale: Converts reserved NFR10/NFR11 placeholders into launch evidence.

Story: `5.6 Final Launch-Readiness Gate and Documentation Sync`

NEW:

```markdown
Final readiness must classify unavailable defaults, skipped lanes, deferred integrations, legal hold, comment sensitivity, export format, secondary magic-link identity verification, and performance evidence as implemented, waived, or post-v1.
```

Rationale: Prevents release claims from overstating implemented integration or quality evidence.

### Architecture Change

Sections: `Magic-Link Confirmation model`, `API documentation`, `Integration Points`

Applied changes:

- Added a 2026-06-20 launch-readiness note requiring concrete EventStore-backed magic-link loading.
- Added a 2026-06-20 launch-readiness note making the export preview decision explicit.
- Added a 2026-06-20 launch-readiness note promoting Works adapter implementation.
- Added a 2026-06-20 launch-readiness note for realistic performance evidence.

Rationale: Architecture already described the caveats; this change converts caveats into implementation commitments.

### Sprint Status Change

OLD:

```yaml
  # Epic 4: Approved Time Ledger, Reporting & Finance Export
  epic-4: done
  ...
  epic-4-retrospective: done
```

NEW:

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

Rationale: Gives the Developer agent executable backlog keys without changing completed Epic 1-4 status.

## 5. Checklist Execution Summary

| Item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story | [x] | Trigger spans Epic 3 and Epic 4 retrospectives plus Phase 2/3 continuation notes. |
| 1.2 Core problem | [x] | Deferred launch-readiness integrations and evidence are documented but not in active backlog. |
| 1.3 Supporting evidence | [x] | Evidence from README, architecture, Epic 3/4 retros, Story 3.5, Story 4.6, and performance docs. |
| 2.1 Current epic viability | [x] | Completed epics remain viable. |
| 2.2 Epic-level changes | [x] | Add Epic 5. |
| 2.3 Remaining epic review | [x] | No planned future epics existed before this change. |
| 2.4 New epic needed | [x] | Epic 5 required. |
| 2.5 Priority/order | [x] | Epic 5 follows Epic 4 before launch claims. |
| 3.1 PRD conflict | [x] | No conflict; rollout/readiness section needed update. |
| 3.2 Architecture conflict | [x] | Caveats existed; implementation commitment needed. |
| 3.3 UX conflict | [N/A] | Existing UX remains valid. |
| 3.4 Other artifacts | [x] | README/performance docs referenced as later sync in Story 5.6. |
| 4.1 Direct adjustment | [x] | Viable, selected. |
| 4.2 Rollback | [N/A] | Not useful; completed behavior is valid. |
| 4.3 MVP review | [N/A] | MVP remains achievable. |
| 4.4 Path forward | [x] | Direct Adjustment via Epic 5. |
| 5.1 Issue summary | [x] | Included above. |
| 5.2 Epic/artifact impact | [x] | Included above. |
| 5.3 Recommended path | [x] | Included above. |
| 5.4 MVP/action plan | [x] | No MVP reduction; action plan is Epic 5. |
| 5.5 Handoff plan | [x] | Developer agent for Epic 5 stories; Architect/Product Owner for Works/export decisions. |
| 6.1 Checklist completion | [x] | All applicable items addressed. |
| 6.2 Proposal accuracy | [x] | Proposal matches applied artifact changes. |
| 6.3 User approval | [x] | User requested implementation in batch form. |
| 6.4 Sprint status update | [x] | Epic 5 backlog keys added. |
| 6.5 Handoff plan | [x] | See below. |

## 6. Implementation Handoff

Scope classification: **Moderate**.

Routed to:

- Developer agent: implement Epic 5 stories in order.
- Architect: decide/approve Works adapter strategy and export preview API shape before or during Stories 5.3/5.4.
- Test Architect: own HTTP-boundary no-disclosure and performance evidence design.
- PM/PO: approve launch-readiness waivers or post-v1 deferrals in Story 5.6.

Recommended sequence:

1. Story 5.1 Magic-link state loader.
2. Story 5.2 HTTP no-disclosure proof.
3. Story 5.3 Works adapter path.
4. Story 5.4 export preview decision.
5. Story 5.5 performance evidence.
6. Story 5.6 final launch-readiness gate.

Success criteria:

- No launch-scope path relies on unavailable defaults without an explicit waiver.
- External magic-link behavior works in live host paths without existence disclosure.
- Work validation/planned effort is concrete or explicitly unavailable by launch decision.
- Export preview behavior is contract-consistent.
- Performance evidence exists for NFR10/NFR11 or carries a documented waiver.
- README, architecture, performance docs, and sprint artifacts match code.

## 7. Applied Artifact Changes

Modified:

- `_bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md`
- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

Created:

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-20.md`

## 8. Next Step

Run `bmad-create-story` for `5.1-implement-eventstore-backed-magic-link-state-loading`, then hand it to the Developer agent for implementation and review.
