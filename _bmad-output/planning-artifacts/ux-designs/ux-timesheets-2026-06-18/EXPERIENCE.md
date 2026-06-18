---
name: Hexalith.Timesheets
project: timesheets
document: EXPERIENCE.md
status: draft
created: 2026-06-18
updated: 2026-06-18
sources:
  - ../../prds/prd-timesheets-2026-06-18/prd.md
  - ../../prds/prd-timesheets-2026-06-18/addendum.md
  - ../../briefs/brief-timesheets-2026-06-18/brief.md
  - ../../briefs/brief-timesheets-2026-06-18/addendum.md
---

# Hexalith.Timesheets - Experience Spine

This spine owns how the product works. `DESIGN.md` owns how it looks. The spines win on conflict with mockups, generated UI, or imported references.

## Foundation

Form factor: responsive web.

Primary surface: internal Hexalith web shell using `FrontComposerShell`, FrontComposer generated command/projection surfaces, and Blazor Fluent UI components.

Secondary surface: external Magic-Link Confirmation web page for a single contribution confirmation. [ASSUMPTION] This page may be hosted outside the full internal navigation shell but must still use the same Fluent UI component family and no-disclosure security behavior.

UI system: Hexalith FrontComposer first, then Blazor Fluent UI V5. Use FrontComposer or Fluent UI components whenever available. Do not use raw HTML, custom CSS, JavaScript, or third-party components when an equivalent FrontComposer, Fluent UI, or Blazor component exists.

Stakes: regulated-adjacent internal/business software. Timesheets is not payroll or invoicing, but approval, correction, export, tenant isolation, and billing evidence make auditability and no-disclosure behavior load-bearing.

`DESIGN.md` is the visual identity reference. It inherits Fluent UI professional themes and defines only Timesheets-specific usage rules.

## Information Architecture

| Surface | Reached from | Purpose |
|---|---|---|
| Timesheets dashboard | Module nav / shell landing | My current period, pending actions, approval workload, report shortcuts |
| Record Time Entry | Project/Work context action, dashboard action, command surface | Create a Time Entry against exactly one Project or Work reference |
| My Timesheet Period | Dashboard, contributor nav | Review Draft/Submitted/Rejected entries for a weekly or monthly period and submit period |
| Time Entry Detail | Grid row, period row, report row, ledger row | Evidence view for one entry, including approval state and correction lineage |
| Correction Flow | Time Entry Detail, rejection notice | Additive correction or resubmission without silently rewriting history |
| Approvals Queue | Approver nav, dashboard workload | Filter submitted entries and periods awaiting review |
| Period Approval Detail | Approvals Queue row | Approve/reject period and individual entries with reasons where required |
| Activity Type Catalog | Admin nav, settings | Manage tenant and project Activity Types, active/inactive state, billable defaults |
| Operational Reports | Reports nav, Project/Work context | Actual time by Contributor, Project, Work, Activity Type, Billable Flag, Approval State, period, contributor category |
| AI Effort Report | Reports nav, Work context | AI wall-clock, model/tool runtime, billable effort, and token metrics beside human/external effort |
| Approved-Time Ledger | Finance/report nav | Approved evidence query and export with correction lineage |
| Export Review Dialog | Approved-Time Ledger action | Confirm filters, scope, and evidence fields before export |
| Magic-Link Confirmation | Scoped external link | External Contributor confirms or adjusts one proposed entry |
| Magic-Link Invalid State | Invalid/expired/used external link | No-disclosure failure surface with no tenant, Project, Work, Party, or Time Entry details |

Navigation model:

- Internal users use the existing `FrontComposerShell` module navigation.
- Project and Work contexts may deep-link into `Record Time Entry`, reports, or filtered Time Entry views.
- Global navigation must not expose raw EventStore, Party profile, Project lifecycle, Work lifecycle, invoicing, payroll, or rate-card ownership.

IA closes when every stated need maps to a surface:

- Personal capture: `Record Time Entry`, `My Timesheet Period`.
- Approval evidence: `Approvals Queue`, `Period Approval Detail`, `Time Entry Detail`.
- External confirmation: `Magic-Link Confirmation`, `Magic-Link Invalid State`.
- AI evidence: `Record Time Entry`, `AI Effort Report`, `Time Entry Detail`.
- Finance evidence: `Approved-Time Ledger`, `Export Review Dialog`.
- Activity governance: `Activity Type Catalog`.

## Voice and Tone

Microcopy is factual, short, and consequence-aware. Brand posture lives in `DESIGN.md`.

| Do | Don't |
|---|---|
| `Submit period` | `Send my amazing timesheet!` |
| `Rejected entry needs correction.` | `Oops, something went wrong.` |
| `Approved entries are locked. Add a correction to change evidence.` | `Edit approved entry` |
| `Projection is rebuilding. Results may be incomplete.` | `Loading data...` for stale/rebuild states |
| `This link is expired or unavailable.` | `Your link for {Project} expired.` |
| `Token metrics unavailable from provider.` | `0 tokens` when metrics are missing |
| `Export approved ledger` | `Create invoice` |

Rules:

- Never imply Timesheets owns Party, Project, Work, invoice, payroll, or rate data.
- Avoid celebratory copy, gamification, productivity coaching, and timer-app language.
- State the consequence before destructive or evidence-changing actions.
- Rejection, correction, export, and magic-link failure copy must be precise enough for audit and support without leaking protected details.

## Component Patterns

Visual specs live in `DESIGN.md.Components`.

| Component | Use | Behavioral rules |
|---|---|---|
| FrontComposerShell | Internal app frame | Owns module nav and page chrome. Timesheets does not add a parallel shell. |
| FrontComposerProjectionView | Generated query/projection surfaces | Default for time entry lists, periods, catalogs, reports, and ledgers. Must surface projection freshness when stale/rebuilding/unavailable. |
| FrontComposerGeneratedForm | Generated command forms | Default for record, submit, approve, reject, correct, manage catalog, and export commands. Validation messages stay beside fields. |
| FluentDataGrid | Queues, reports, ledgers, catalogs | Sortable/filterable where needed. Row click opens detail. Multi-select only for batch approval/export actions with clear selected count. Keyboard traversal required. |
| FluentAccordion | Multi-section detail pages/dialogs/panels | Use one accordion for two or more sibling titled sections. Expand the primary section by default. Do not hide the only primary content. |
| FluentAccordionItem | Sections inside FluentAccordion | Use for Evidence, Approval, Correction Lineage, AI Metrics, Export Scope, Audit Metadata, and Policy sections. |
| FluentTabs | Closely related subviews | Use for Entries / Periods / Ledger or Human / External / AI report partitions. Do not use for unrelated navigation. |
| FluentDialog | Focused decisions | Use for approve, reject with reason, submit period, correct approved entry, export review, and external confirmation adjustment. One dialog layer only. |
| FluentButton | Explicit commands | Button text is a verb phrase. Primary action appears once per command context. Destructive/evidence-changing actions require confirmation when irreversible by direct edit. |
| FluentMessageBar | Persistent surface state | Use for stale projections, permission denied, invalid/expired magic link, comments policy, export scope warning, or approval authority ambiguity. |
| FluentToast | Transient feedback | Use after successful save/submit/approve/reject/correct/export request. Do not use for audit-critical messages. |
| FilterBar | Dense filters above grids | Include only filters relevant to the current surface. Preserve filters during drill-in and back navigation. |
| StatusBadge | Semantic state display | Always include text. Used for Approval State, period state, Billable Flag, contributor category, correction state, projection freshness, and magic-link state. |

## State Patterns

| State | Surface | Treatment |
|---|---|---|
| Cold load | All internal grid/report surfaces | Use Fluent/FrontComposer loading skeleton or progress pattern sized to expected layout. |
| Empty current period | My Timesheet Period | Show the period boundary and a single action: `Record time`. |
| Invalid entry at submission | My Timesheet Period | Keep valid entries selectable/submittable where policy allows; mark only blocking entries with field-level correction needed. |
| Submitted period with mixed entries | My Timesheet Period, Period Approval Detail | Show period state separately from entry states. Do not flatten mixed entry states into one badge. |
| Rejected entry | My Timesheet Period, Time Entry Detail | Show rejection reason and `Correct entry`; preserve original values and rejection metadata. |
| Approved entry | Time Entry Detail, Ledger | Direct edit disabled. Show `Add correction` as the mutation path. |
| Corrected/superseded entry | Detail, Ledger, Reports | Show current value plus lineage link. Reports can include/exclude superseded entries through filters. |
| Projection stale | Reports, Ledger, Approvals Queue | Persistent `FluentMessageBar` with freshness text and refresh/retry action if available. |
| Projection rebuilding | Reports, Ledger | Show partial/unavailable state explicitly; never present as fresh data. |
| Permission denied | Any internal surface | No raw identifiers in error copy. Route to safe empty/denied state. |
| Approver authority unresolved | Approval surfaces | Block approval and explain authority cannot be resolved. Do not allow optimistic approval. |
| Export no results | Approved-Time Ledger | Show filters and `No approved entries match these filters.` No export action. |
| Export requested | Export Review Dialog, Ledger | Confirm requested scope, then toast for request accepted; export audit remains visible if surfaced. |
| Magic link valid | Magic-Link Confirmation | Show only the scoped proposed entry details needed to confirm or adjust. |
| Magic link expired/used/invalid | Magic-Link Invalid State | No tenant, Project, Work, Party, Time Entry, or duration details. Provide one safe recovery path. |
| AI token metrics unavailable | AI Effort Report, Time Entry Detail | Show `Unavailable` or `Not reported by provider`; never show zero unless provider reported zero. |
| Offline/interrupted command | Command forms | Preserve entered values. Show retry path. Do not claim persistence until command accepted. |

## Interaction Primitives

- Click/tap rows in `FluentDataGrid` to open detail.
- Use explicit submit/approve/reject/correct/export commands; do not autosubmit evidence-changing actions.
- Batch actions are allowed only after visible selection and selected-count confirmation.
- `Esc` closes the topmost dialog or popover.
- Back navigation preserves grid filters, sort, page, and selected report period.
- Approval/rejection dialogs focus the first required field or primary decision control.
- Correction flow starts from the prior entry values but saves as additive correction, not direct mutation.
- Filter changes update query results explicitly or through the established FrontComposer pattern; stale/rebuilding states remain visible.
- Hover-only controls are not permitted. Any row action exposed on hover must also be reachable by keyboard and touch.
- Drag/drop, kanban movement, stopwatch-style live timers, desktop activity capture, screenshots, and gamified streaks are out of scope for v1.

## Accessibility Floor

Behavioral accessibility. Visual contrast is governed by `DESIGN.md` and the active Fluent theme.

- Target WCAG 2.2 AA for internal and external web surfaces.
- All actions reachable by keyboard, including grid navigation, filter changes, dialog decisions, and magic-link confirmation.
- Focus order follows reading order: page title, message bars, filters, grid/list, details/actions.
- `FluentDialog` traps focus while open and returns focus to the invoking control when closed.
- `StatusBadge` text must be screen-reader readable and not color-only.
- Field validation is tied to the field and summarized where FrontComposer supports it.
- Projection freshness changes should be announced through an accessible status region when they materially change available data.
- Magic-link invalid states must be accessible without exposing protected details.
- Time duration inputs must have clear units. AI metric fields must distinguish minutes, runtime, billable effort, and token counts.
- Touch targets follow Fluent UI defaults; any custom unavoidable command target must meet 44px minimum.

## Responsive & Platform

| Viewport | Behavior |
|---|---|
| Desktop/laptop | Primary internal experience. Full `FrontComposerShell`, filter bars, `FluentDataGrid`, detail panels/dialogs, report and export workflows. |
| Tablet/narrow desktop | Internal shell remains usable. Grids reduce columns by priority; details move below or into dialogs according to FrontComposer patterns. |
| Phone | External Magic-Link Confirmation must be fully usable. Internal surfaces should support review/simple actions where FrontComposer allows, but v1 is not a native mobile app. |

No native mobile app is in v1. No separate external-party portal is in v1.

## Inspiration & Anti-patterns

- Lifted from the market baseline: contextual work-item time logging, weekly/monthly approval, locking approved time from direct edit, and exportable approved evidence.
- Lifted from Hexalith module patterns: generated FrontComposer command/projection surfaces, EventStore evidence semantics, tenant isolation, and Party references.
- Rejected: generic timer-app positioning. Timesheets is an evidence ledger, not a stopwatch product.
- Rejected: desktop surveillance patterns such as screenshots, idle detection, app monitoring, and automatic activity tracking.
- Rejected: invoice/payroll language. Approved billable evidence is not an invoice, payroll run, rate calculation, or revenue decision.
- Rejected: full external supplier portal in v1. Magic-Link Confirmation is scoped and single-purpose.
- Rejected: token-to-hours conversion as a default. AI metrics are related but not interchangeable.

## Evidence And Audit Semantics

- A Time Entry is an attributable fact, not a mutable spreadsheet row.
- Approval State and Timesheet Period state are distinct and must remain visible where both matter.
- Approved entries are locked from direct edit. Corrections are additive and show lineage.
- Contributor display data is hydrated from Parties at read time. Timesheets persists Party references, not Party personal data.
- Project and Work names may be displayed when supplied by source modules, but Timesheets must not imply ownership of Project/Work state.
- Exports are evidence exports. The UI must not calculate rates, invoice totals, taxes, payroll values, or revenue recognition.

## Open Assumptions

- [ASSUMPTION] Internal UI is responsive web in `FrontComposerShell`; external magic-link confirmation is a minimal responsive web page.
- [ASSUMPTION] Product stakes are regulated-adjacent because approval, correction, and billing evidence require audit-quality behavior.
- [ASSUMPTION] Tenant time zone controls period boundaries until a more specific policy is provided.
- [ASSUMPTION] Self-approval is denied unless policy explicitly allows it.
- [ASSUMPTION] CSV export is sufficient as a v1 UI affordance unless architecture selects API/webhook export at launch.
- [ASSUMPTION] Corrections are shown as superseding lineage by default; offset entries can be added later if architecture supports both.

## Key Flows

### UJ-1. Camille records her project time before submitting the week.

1. Camille opens Hexalith.Timesheets from the internal shell or from a Project/Work context.
2. She selects `Record time`.
3. `FrontComposerGeneratedForm` opens with date, duration, Target Reference, Activity Type, comment, Billable Flag, and Contributor Party reference.
4. She saves the Time Entry in Draft state.
5. On Friday, Camille opens `My Timesheet Period`.
6. She reviews the weekly period in `FluentDataGrid`, with entry Approval State and required-field status visible.
7. She selects `Submit period`.
8. **Climax:** the period shows Submitted, and each included entry remains traceable to its Project or Work reference.

Failure path: one entry is missing required Activity Type. Submission blocks that entry, shows the field-level correction needed, and leaves the rest of the period review context intact.

### UJ-2. Nadia approves the week and rejects one entry.

1. Nadia opens `Approvals Queue`.
2. She filters by Project, period, and Submitted state.
3. She opens Camille's `Period Approval Detail`.
4. The detail shows period state, entry states, billable flags, comments, contributor, and projection freshness.
5. Nadia approves valid entries.
6. She opens `Reject entry` for a vague billable entry and enters a reason.
7. **Climax:** approved entries become locked into the Approved-Time Ledger, while the rejected entry returns to Camille with the reason and correction path visible.

Failure path: Camille later corrects the rejected entry after period submission. The period shows a pending correction/mixed state instead of silently changing the approved evidence.

### UJ-3. Simon confirms supplier time through Magic-Link Confirmation.

1. Simon opens a scoped magic link from email or another tenant-approved delivery channel.
2. The Magic-Link Confirmation page validates the token before showing details.
3. If valid, Simon sees only the proposed date, duration, Activity Type, comment, Billable Flag, and target context needed for this confirmation.
4. Simon chooses `Confirm time` or `Adjust`.
5. If adjusting, a focused `FluentDialog` shows editable allowed fields.
6. **Climax:** the tenant receives a Party-attributed Time Entry that can enter the same approval workflow as internal entries, without Simon receiving broader access.

Failure path: the link is expired, invalid, or already used. Simon sees `This link is expired or unavailable.` No tenant, Project, Work, Party, Time Entry, or duration details are shown.

### UJ-4. Ada the AI agent records execution evidence.

1. Ada completes a Work item step through Hexalith.Works.
2. Ada submits a Time Entry through the API/automation path with Work Reference and Contributor Party ID.
3. The entry includes wall-clock execution time, model/tool runtime, billable effort, and provider token metrics where available.
4. An operator opens the Work-linked Time Entry Detail.
5. The detail separates human/external duration from AI runtime and token counts.
6. **Climax:** the Work report shows AI effort beside human effort without converting tokens into human hours.

Failure path: provider token metrics are unavailable. The entry shows `Unavailable` for token fields and keeps the rest of the evidence intact.

### UJ-5. Marc exports approved billable evidence.

1. Marc opens `Approved-Time Ledger`.
2. He filters by tenant, Project, period, Activity Type, Contributor category, and Billable Flag.
3. `FluentDataGrid` shows approved entries with stable IDs, approval metadata, and correction lineage.
4. Marc selects `Export approved ledger`.
5. `Export Review Dialog` summarizes filters, output scope, included evidence fields, and what Timesheets will not calculate.
6. Marc confirms export.
7. **Climax:** downstream finance receives approved billable evidence with entry IDs, Contributor Party references, Approval State, correction lineage, and comments, without Timesheets becoming an invoicing system.

Failure path: no approved entries match the filters. Export is disabled and the ledger shows `No approved entries match these filters.`
