---
title: "Hexalith.Timesheets — Sprint Change Proposal"
date: 2026-06-19
trigger: "_bmad-output/planning-artifacts/implementation-readiness-report-2026-06-18.md"
mode: batch
scope_classification: "Minor → Moderate (planning-artifact edits + 3 policy ratifications; no code, no epic restructure)"
status: approved-and-applied
approved: 2026-06-19
policy_decisions:
  time_zone_period: "tenant time zone (canonical v1)"
  retention_legal_hold: "v1 default retention + legal-hold override as launch gate"
  magic_link_secondary_verification: "deferred to post-v1"
applied_edits:
  - "readiness report: submodule inventory corrected; story count 25→29"
  - "architecture: UI-project timing decided (first UI story, not 1.1); Works reference-validation note added"
  - "epics: NFR→story + UX-DR→story tables added; Works dependency notes on 1.7/4.3; policy notes on 1.4/2.7/3.2/4.6"
  - "prd: §9 retention + §10 time-zone resolved; §14 Q1/Q4/Q5 resolved; updated 2026-06-19"
author: "Correct Course workflow (Developer role) — /bmad-correct-course"
artifacts_assessed:
  - prds/prd-timesheets-2026-06-18/prd.md (+ addendum.md)
  - architecture.md
  - epics.md
  - ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md + DESIGN.md
  - implementation-artifacts/sprint-status.yaml
  - .gitmodules / git submodule status
  - Hexalith.Projects/src/** + Hexalith.Works/src/** (contract-surface scan)
---

# Sprint Change Proposal — Hexalith.Timesheets

**Date:** 2026-06-19
**Trigger:** Implementation Readiness Assessment Report (`implementation-readiness-report-2026-06-18.md`)
**Mode:** Batch
**Navigation result:** Direct Adjustment (artifact corrections + policy ratifications). No epic/story add, remove, or renumber. No code change.

---

## Section 1 — Issue Summary

The implementation-readiness review concluded **"✅ READY FOR IMPLEMENTATION"** with 0 critical, 0 major, and 6 minor/non-blocking items. That overall verdict still holds. However, validating the report against the actual repository surfaced **two factual errors in the report itself** and confirmed that the remaining items are documentation polish and open policy decisions — not scope, architecture, or structural defects.

### What triggered this correction

1. **The report's highest-attention finding rests on a wrong submodule inventory.** It claims `Hexalith.Works` and a domain-level `Hexalith.Projects` are *not* root submodules of this umbrella and that their "presence/maturity is unconfirmed." The umbrella's `.gitmodules` and `git submodule status` show **both are real, initialized, checked-out root submodules at `heads/main`**, each with a full `src/` tree and `.slnx`. The report's sibling list (`…Folders…Memories…`) names two modules that are **not** in this umbrella and omits `Projects`, `Works`, `Commons`, and `PolymorphicSerializations` — it was evidently read from a different umbrella's context, not this one.

2. **The report's story count is internally inconsistent.** It states "4 epics, 25 stories" while its own per-epic breakdown (9 + 8 + 5 + 7) sums to **29**, which matches `epics.md` and `sprint-status.yaml`. The correct count is **29 stories**.

3. **A precise (and much smaller) residual replaces the false dependency alarm.** A source-level scan of the two siblings shows the real picture:
   - **`Hexalith.Projects`** exposes a mature, consumer-facing contract surface (`GetProjectAsync`, `ListProjectsAsync`, generated Client SDK + OpenAPI, `ProjectLifecycle`). **FR-2 Project-reference validation is readily supportable.**
   - **`Hexalith.Works`** has the *data model* needed (`WorkItemEffort` → FR-17; `ExecutorBinding`/`Channel` → FR-15/FR-20) but **no consumer-facing read/validate query** (no `Hexalith.Works.Client`, no `GetWorkItem`, only an internal `WhatsNext` queue handler and an unimplemented `IExpectationResolver` port). **FR-2 *Work*-reference validation therefore has a genuine, narrow contract gap.**

4. **Five genuine (non-blocking) items from the report remain valid:** the Works-query gap above (#1 residual), three open policy values (#2 time-zone/period, #3 retention/legal-hold, #4 magic-link secondary verification), and two traceability/clarity edits (#5 NFR/UX-DR coverage tables, #6 UI-project scaffold ambiguity).

### Evidence

| Claim | Evidence |
|---|---|
| Projects + Works are root submodules | `.gitmodules` (paths `Hexalith.Projects`, `Hexalith.Works`); `git submodule status` → both at `heads/main`; on-disk `src/` + `.slnx` |
| Report's sibling list is wrong | Report line 198 / 291 list `Folders`, `Memories` (absent here) and omit `Projects`, `Works`, `Commons`, `PolymorphicSerializations` |
| Story count is 29, not 25 | `epics.md` (1.1–1.9, 2.1–2.8, 3.1–3.5, 4.1–4.7); `sprint-status.yaml` (29 entries) |
| Projects FR-2 supportable | `Hexalith.Projects.Client/Generated/HexalithProjectsClient.g.cs` → `GetProjectAsync`; `…Contracts/openapi/hexalith.projects.v1.yaml` |
| Works FR-17 / FR-15 / FR-20 data present | `…Works.Contracts/ValueObjects/WorkItemEffort.cs`, `ExecutorBinding.cs`, `Channel.cs` |
| Works FR-2 query gap | No `Hexalith.Works.Client`; `WhatsNextQueryHandler` is an internal EventStore query handler (queue, not addressable by Work ID); `IExpectationResolver` has no remote implementation |
| UI ambiguity | `architecture.md` project tree includes `…UI`/`UI.Tests` (lines 834/868) vs "Deferred Decisions" (324) vs "Nice-to-Have Gaps" (1072) vs scaffold cmd (184–197) and epics Story 1.1 (no UI project) |
| Open policy values | PRD §9 retention `[NOTE FOR PM]` (369), §10 time-zone `[NOTE FOR PM]` (379), §14 Q4 magic-link (424) |

---

## Section 2 — Impact Analysis

### Epic Impact — none (no structural change)
- No epic is in flight; `sprint-status.yaml` shows all 4 epics and all 29 stories at `backlog`.
- All four epics remain completable as planned. **No epic is added, removed, redefined, or resequenced.**
- The Epics 3–4 soft-warnings the report attached to FR-13/FR-14 (magic-link) and FR-17/FR-15/FR-20 (Works) **downgrade**: the modules exist; only the Works **query surface** for FR-2/FR-17 is a real dependency action.

### Story Impact — annotations only (no story added/removed)
- **Story 1.7** (Record draft entry against Project **or Work**) and **Story 4.3** (planned-vs-actual from Works) are the stories whose Work-reference paths depend on the missing Works query surface → add an explicit dependency/gating note.
- **Stories 1.4 / 2.7 / 4.6 / 3.2** already *own* the three open policy values; they need launch-gate annotations once the values are ratified.
- No story's acceptance criteria change in substance.

### Artifact Conflicts
| Artifact | Conflict? | Needed change |
|---|---|---|
| **Readiness report** | Yes — 2 factual errors | Correct submodule inventory + dependency framing (#1); correct story count 25→29 |
| **Architecture** | Yes — intra-doc contradiction | Resolve UI-project ambiguity (#6); add a note that the Works reference-validation adapter currently has no Works-owned consumer query (the Projects one exists) |
| **Epics** | No conflict — additions | Add NFR→story + UX-DR→story tables (#5); add Works-query dependency note to 1.7/4.3; add launch-gate notes to 1.4/2.7/3.2/4.6 |
| **PRD** | No conflict — already correct on deps | Optionally record ratified policy values for §14 Q1/Q4/Q5 once decided |
| **UX (EXPERIENCE/DESIGN)** | No conflict | None |
| **sprint-status.yaml** | No | None (no epic/story structural change) |

### Technical Impact
- **No code, infrastructure, or deployment change** is proposed here.
- The one technical reality to record: the architecture assumes "Work Reference validation uses a Works-owned API/projection/adapter" (architecture line ~101), but a **Works-owned consumer query does not yet exist**. The Timesheets fail-closed adapter pattern still holds; the implementation choice (add a Works `GetWorkItem` query upstream **vs.** bridge to the Works EventStore projection from a Timesheets adapter) should be made when **Story 1.7 / 4.3** are picked up. This is a cross-module coordination note, not a blocker for Epic 1's foundation stories (1.1–1.6, 1.8).

---

## Section 3 — Recommended Approach

**Selected path: Option 1 — Direct Adjustment** (with a Hybrid element for the three policy values).

| Path | Verdict | Rationale |
|---|---|---|
| **1. Direct Adjustment** | ✅ Selected | All six items resolve through targeted edits to planning artifacts + three policy ratifications. Nothing implemented yet, so no rework. |
| 2. Potential Rollback | N/A | No completed work to revert. |
| 3. PRD MVP Review | Not needed | MVP scope, goals, and FR set are unaffected; every item is polish or an already-owned policy value. |

- **Effort:** Low–Medium (documentation edits + 3 decisions).
- **Risk:** Low. The only item with downstream teeth is the Works-query gap, and it is contained to two stories and already covered by the fail-closed adapter design.
- **Timeline impact:** None to Epic 1 start. Epic 1 foundation (Stories 1.1–1.6, 1.8) can begin immediately. The Works-query coordination must be settled before the **Work-reference** paths of Story 1.7 and before Story 4.3.

---

## Section 4 — Detailed Change Proposals

Grouped by artifact. `OLD → NEW` shown where a precise replacement applies; additive content shown as insert blocks.

### 4A. Readiness report — correct two factual errors

**Edit R1 — submodule inventory & dependency framing**
*File:* `implementation-readiness-report-2026-06-18.md` (Step 4 ⚠️ bullet, ~line 198; Summary item 1, ~line 291)

> **OLD (Step 4):** "⚠️ … `Hexalith.Works` and a domain-level `Hexalith.Projects` are heavily referenced but their presence/maturity in this ecosystem is unconfirmed. The umbrella's root submodules are AI.Tools, Builds, Commons, Conversations, EventStore, Folders, FrontComposer, Memories, Parties, Tenants — no `Hexalith.Works`, and the 'Hexalith.Projects' project-context is actually the umbrella root context…"
>
> **NEW:** "✅ **Corrected 2026-06-19.** `Hexalith.Projects` and `Hexalith.Works` are both root-level submodules of this umbrella (`.gitmodules`; `git submodule status` → both at `heads/main`, each with `src/` + `.slnx`). Full root set: AI.Tools, Builds, Commons, Conversations, EventStore, FrontComposer, Parties, PolymorphicSerializations, Projects, Tenants, Works. Residual (narrowed): **Projects exposes a mature consumer query surface (`GetProjectAsync`/`ListProjectsAsync`) so FR-2 Project validation is supportable; Works exposes the needed data model (`WorkItemEffort`→FR-17, `ExecutorBinding`→FR-15/FR-20) but no consumer-facing read/validate query, so FR-2 Work-reference validation needs a Works query contract or a Timesheets adapter bridge.**"

> **OLD (Summary item 1):** "Neither appears as a root submodule in this umbrella (siblings present: AI.Tools, Builds, Commons, Conversations, EventStore, Folders, FrontComposer, Memories, Parties, Tenants)."
>
> **NEW:** "Both appear as root submodules. The genuine residual is the **Works consumer-query gap** for FR-2 (Work) and FR-17; gate Story 1.7 (Work path) and Story 4.3 accordingly."

**Edit R2 — story count**
*File:* same report (Step 3, ~line 130; Coverage Statistics, ~line 168)

> **OLD:** "Source: `epics.md` — 4 epics, 25 stories." / "**Stories:** 25 across 4 epics (Epic 1: 9 · Epic 2: 8 · Epic 3: 5 · Epic 4: 7…)"
>
> **NEW:** "Source: `epics.md` — 4 epics, **29** stories." / "**Stories:** **29** across 4 epics (Epic 1: 9 · Epic 2: 8 · Epic 3: 5 · Epic 4: 7…)"

*Rationale:* the report is a launch-gate artifact; its headline finding and counts must be accurate.

### 4B. Architecture — resolve UI-project ambiguity + record the Works-query reality

**Edit A1 — UI-project scaffold decision (#6)**
*File:* `architecture.md` (Deferred Decisions ~324; Nice-to-Have Gaps ~1072)

Make one explicit decision and remove the contradiction. **Recommended:** *the `Hexalith.Timesheets.UI` / `UI.Tests` projects are **created with the first UI-bearing story, not in scaffold Story 1.1.*** The documented project tree (lines 758–869) stays as the **target** structure; Story 1.1 deliberately omits UI.

> **NEW (replace the deferred/nice-to-have UI lines with a single decision):** "**UI project timing (decided 2026-06-19):** `Hexalith.Timesheets.UI` and `Hexalith.Timesheets.UI.Tests` are part of the **target** project tree but are **scaffolded with the first UI-bearing story, not in Story 1.1**. Story 1.1 creates host/Contracts/Client/Server/Projections/Testing/ServiceDefaults/AppHost only. When added, UI must follow the documented `UI/` structure and Fluent UI V5-only rule."

**Edit A2 — Works reference-validation note (#1 residual)**
*File:* `architecture.md` (API & Communication / Integration Points, ~line 893–962)

> **INSERT note:** "**Reference-validation adapter maturity (2026-06-19):** `Hexalith.Projects` exposes a consumer query (`GetProjectAsync`) suitable for FR-2 Project validation. `Hexalith.Works` currently exposes **no consumer-facing read/validate query** (only an internal `WhatsNext` queue handler and an unimplemented `IExpectationResolver`). The Works reference-validation adapter must therefore either (a) consume a new Works-owned `GetWorkItem` query, or (b) read the Works EventStore projection through a Timesheets adapter. Decision required before Story 1.7 (Work path) and Story 4.3. `WorkItemEffort` (FR-17) and `ExecutorBinding` (FR-15/FR-20) are already stable Works Contracts."

### 4C. Epics — add traceability tables + dependency/launch-gate notes (#5, #1, #2–4)

**Edit E1 — add NFR→story coverage table** (insert after the FR Coverage Map, ~line 252). Full table in **Appendix A** below.

**Edit E2 — add UX-DR→story coverage table** (insert after the new NFR table). Full table in **Appendix B** below.

**Edit E3 — Works dependency note on Story 1.7 & Story 4.3**
> **Story 1.7 — add under Requirements:** "*Dependency:* Work-reference validation requires a Works consumer query (see architecture Edit A2). The Project path (via `GetProjectAsync`) is unblocked; the Work path is gated on that decision."
> **Story 4.3 — add:** "*Dependency:* planned-vs-actual consumes `WorkItemEffort` from Works; actual read access requires the Works consumer-query decision (architecture Edit A2)."

**Edit E4 — launch-gate annotations on policy-owning stories** (see Section 4D for the values)
> **Story 1.4:** mark retention/legal-hold default + "legal-hold override = launch-readiness gate (NFR7/GOV-7)."
> **Story 2.7 / 4.6:** mark "tenant time-zone/period policy ratified = canonical; DST/period-boundary cases are launch gates (NFR15)."
> **Story 3.2:** mark the magic-link secondary-verification decision (Section 4D-#4).

### 4D. PRD — record the three policy ratifications (#2–4)

These are the only items needing **your decision**. Recommended defaults below (each consistent with the architecture's existing defaults and the PRD §15 assumptions). On approval, I'll close the matching PRD Open Questions (§14) and update the owning stories.

| # | Open question (PRD §14) | Recommended v1 resolution | Owner story |
|---|---|---|---|
| **#2 Time-zone / period** | Q1 | **Tenant time zone is the canonical policy.** Store UTC audit instants + tenant-local period keys; DST/period-boundary handling proven by golden-file tests. (Already the architecture default.) | 2.7, 4.6 |
| **#3 Retention / legal-hold** | Q5 | **Default:** Time Entry & approval events retained as indefinite audit evidence; export records + magic-link audit metadata retained per tenant policy with a documented default; **legal-hold override remains an explicit launch-readiness gate** (needs tenant/legal sign-off). | 1.4 |
| **#4 Magic-link secondary verification** | Q4 | **v1 baseline = single-use scoped expiring links (FR-14).** Secondary identity verification for high-value/billable entries is **deferred to post-v1**, recorded as an explicit assumption rather than silently assumed closed. | 3.2 |

*If you prefer different values, tell me at approval and I'll substitute them before any edits land.*

---

## Section 5 — Implementation Handoff

### Scope classification: **Minor → Moderate**
- **Minor (direct implementation):** Edits R1–R2, A1–A2, E1–E4 are deterministic artifact edits a Developer/Tech-Writer agent can apply directly.
- **Moderate (needs a decision-maker):** the three policy ratifications (4D #2–4) need your (PM/stakeholder) sign-off — #3 in particular may need legal input. The Works-query gap (A2/E3) needs a cross-module coordination decision with the `Hexalith.Works` owner.

### Recipients & responsibilities
| Recipient | Deliverable |
|---|---|
| **Developer / Tech-Writer agent** | Apply R1–R2, A1, E1–E2 (and E3–E4, A2 text) once approved |
| **PM (you) / stakeholders** | Ratify or adjust policy values 4D #2–4; sign off legal-hold gate |
| **Architect + `Hexalith.Works` owner** | Decide Works `GetWorkItem` query vs. Timesheets adapter bridge before Story 1.7 (Work path) / 4.3 |

### Success criteria
1. Readiness report shows the corrected submodule set and story count (29); finding #1 reframed as the Works-query residual.
2. `architecture.md` has a single, non-contradictory UI-project decision and the Works reference-validation note.
3. `epics.md` carries the NFR→story and UX-DR→story tables (Appendix A/B) and the dependency/launch-gate notes.
4. PRD §14 Q1/Q4/Q5 resolved (or explicitly deferred with rationale); owning stories annotated.
5. Epic 1 foundation stories (1.1–1.6, 1.8) start with no open dependency.

### What does **not** change
- No epic/story add, remove, or renumber → **`sprint-status.yaml` unchanged**.
- No FR/NFR/UX-DR scope change; no architecture decision reversal; no code.

---

## Appendix A — NFR → Story coverage map (proposed `epics.md` insert)

All 15 NFRs trace to ≥1 story (by acceptance-criteria substance). "Primary" = the story that most directly establishes it.

| NFR | Theme | Primary | Also covered in |
|-----|-------|---------|-----------------|
| NFR1 | Tenant isolation (adversarial fail-closed) | 1.2 | 1.1, 1.7, 1.8, 2.2, 2.3, 2.6, 2.8, 3.1, 3.5, 4.1, 4.2, 4.4 |
| NFR2 | Append-only, no silent overwrite | 2.5 | 1.3, 2.4, 2.6, 4.2 |
| NFR3 | Store references only, no personal/sibling data | 1.7 | 1.2, 1.6, 1.8, 1.9, 3.1, 4.3 |
| NFR4 | Correction provenance | 2.6 | 2.4, 4.2 |
| NFR5 | Export auditability | 4.6 | 4.5 |
| NFR6 | Magic-link no-disclosure | 3.5 | 3.2, 3.3 |
| NFR7 | Retention policy (launch gate) | 1.4 | — |
| NFR8 | Tenant+resource gates on all paths | 1.2 | 1.5, 1.7, 2.3, 3.1, 4.1, 4.2, 4.5 |
| NFR9 | Projection at-least-once / replay / freshness | 1.8 | 2.6, 2.8, 4.1, 4.2, 4.3 |
| NFR10 | Command ack ≤500 ms p95 | 1.1 | (evidence harness; later stories add data) |
| NFR11 | Report query ≤2 s p95 | 4.3 | 1.1 |
| NFR12 | Privacy-safe logging | 1.4 | 1.1, 1.7, 1.9, 3.1, 3.4, 3.5 |
| NFR13 | WCAG 2.2 AA / FrontComposer+Fluent UI | 4.7 | 1.5, 1.6, 1.7, 1.8, 2.1, 2.3, 2.4, 3.3, 3.4, 4.1–4.5 |
| NFR14 | Additive, serialization-tolerant evolution | 1.3 | 1.9, 4.6 |
| NFR15 | Time-zone / period policy (launch gate) | 2.7 | 4.6 |

## Appendix B — UX-DR → Story coverage map (proposed `epics.md` insert)

All 37 UX-DRs trace to ≥1 story.

| UX-DR | Theme | Primary | Also in |
|-------|-------|---------|---------|
| UX-DR1 | FrontComposerShell, no parallel shell | 4.7 | 1.1 |
| UX-DR2 | External magic-link minimal page | 3.3 | 3.4, 3.5 |
| UX-DR3 | FrontComposer-first, Fluent UI V5 | 1.3 | 1.1, all UI stories |
| UX-DR4 | Inherit Hexalith themes, no bespoke brand | 1.3 | 4.7 |
| UX-DR5 | Spacing scale | 1.3 | UI stories |
| UX-DR6 | Restrained radii | 1.3 | UI stories |
| UX-DR7 | ProjectionView default + freshness | 4.1 | 1.5, 1.8, 4.2, 4.3 |
| UX-DR8 | GeneratedForm for commands | 1.7 | 2.1, 2.3, 2.4, 4.5 |
| UX-DR9 | FluentDataGrid for queues/reports/ledgers | 4.1 | 1.5, 2.3, 4.3 |
| UX-DR10 | Accordion for 2+ titled sections | 1.8 | 2.6 |
| UX-DR11 | Primary content not hidden in accordion | 1.8 | 4.7 |
| UX-DR12 | Accordion sections (Evidence/Approval/…) | 1.8 | 2.6, 4.5 |
| UX-DR13 | FluentTabs for related subviews only | 4.3 | 4.1, 4.4 |
| UX-DR14 | FluentDialog for focused decisions | 2.3 | 2.6, 2.7, 3.4, 4.5 |
| UX-DR15 | FluentButton verb phrase + confirm | 2.3 | 2.5, 4.5 |
| UX-DR16 | MessageBar for persistent state/policy | 1.4 | 2.1, 2.2, 4.5 |
| UX-DR17 | Toast transient only | 2.1 | 2.3 |
| UX-DR18 | Dense FilterBar, preserved on drill-in | 4.1 | 1.6, 4.3 |
| UX-DR19 | StatusBadge text not color | 1.8 | 1.5, 2.3, 2.7, 3.2, 4.x |
| UX-DR20 | Operational dashboard, not marketing | 4.7 | — |
| UX-DR21 | Record Time Entry reachable + fields | 1.7 | — |
| UX-DR22 | My Timesheet Period states | 2.7 | — |
| UX-DR23 | Time Entry Detail, not mutable row | 1.8 | 2.6 |
| UX-DR24 | Correction flow additive | 2.4 | 2.6 |
| UX-DR25 | Approvals Queue / Period Detail | 2.8 | 2.3 |
| UX-DR26 | Activity Type Catalog | 1.5 | 1.6 |
| UX-DR27 | Operational Reports dimensions | 4.3 | — |
| UX-DR28 | AI Effort Report separation | 4.4 | 1.9 |
| UX-DR29 | Approved-Time Ledger + disable empty export | 4.2 | 4.5 |
| UX-DR30 | Export Review Dialog, no finance language | 4.5 | 4.6 |
| UX-DR31 | Magic-link validate-before-detail | 3.3 | 3.4 |
| UX-DR32 | Magic-link no-disclosure failure | 3.5 | 3.2 |
| UX-DR33 | Factual, non-celebratory copy | 4.5 | 1.4, 4.7 |
| UX-DR34 | Freshness visible + accessible status region | 4.1 | 4.2, 4.3, 4.7 |
| UX-DR35 | Keyboard-reachable, no hover-only | 1.7 | 2.3, 3.3, 4.1 (all UI) |
| UX-DR36 | Duration/AI units; missing tokens "Unavailable" | 1.9 | 1.7, 4.4 |
| UX-DR37 | Responsive desktop/tablet/phone | 3.3 | 1.7, 3.4 |

## Appendix C — Change-navigation checklist (final status)

| Item | Status | Note |
|------|--------|------|
| 1.1 Triggering story | [N/A] | Trigger is the readiness report, not a story |
| 1.2 Core problem | [Done] | Doc defect + open policy decisions |
| 1.3 Evidence | [Done] | See Section 1 evidence table |
| 2.1–2.5 Epic impact | [Done] | No structural epic change |
| 3.1 PRD | [Action-needed] | Policy ratifications (4D) |
| 3.2 Architecture | [Action-needed] | UI ambiguity + Works-query note |
| 3.3 UX | [N/A] | No conflict |
| 3.4 Other artifacts | [Action-needed] | Correct the readiness report |
| 4.1 Direct Adjustment | [Viable] | Selected |
| 4.2 Rollback | [Not viable] | Nothing implemented |
| 4.3 MVP Review | [Not viable] | MVP unaffected |
| 4.4 Path selected | [Done] | Option 1 (Hybrid for policy) |
| 5.1–5.5 Proposal components | [Done] | This document |
| 6.4 sprint-status.yaml | [N/A] | No epic/story structural change |

---

*Prepared by the Correct Course workflow (Developer role). Awaiting approval before any artifact edits are applied.*
