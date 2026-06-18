---
name: Hexalith.Timesheets
description: Actor-neutral effort evidence UX for Hexalith, implemented through FrontComposer and Blazor Fluent UI.
project: timesheets
document: DESIGN.md
status: draft
created: 2026-06-18
updated: 2026-06-18
sources:
  - ../../prds/prd-timesheets-2026-06-18/prd.md
  - ../../prds/prd-timesheets-2026-06-18/addendum.md
  - ../../briefs/brief-timesheets-2026-06-18/brief.md
  - ../../briefs/brief-timesheets-2026-06-18/addendum.md
colors: {}
typography:
  inherited:
    note: 'Inherited from Hexalith FrontComposer and the active Blazor Fluent UI theme. Do not introduce Timesheets-specific typefaces or custom font ramps.'
  table:
    note: 'Use Fluent UI table/grid typography through FluentDataGrid and FrontComposer projection defaults.'
  form:
    note: 'Use Fluent UI form typography through FrontComposer generated command forms.'
rounded:
  sm: 4px
  md: 6px
  lg: 8px
  full: 9999px
spacing:
  '1': 4px
  '2': 8px
  '3': 12px
  '4': 16px
  '5': 24px
  '6': 32px
  page-gutter: 24px
  dense-row-gap: 8px
  section-gap: 16px
components:
  FrontComposerShell:
    visual-source: 'Hexalith.FrontComposer shell default'
    navigation: 'Use existing shell navigation and page chrome.'
  FrontComposerProjectionView:
    visual-source: 'FrontComposer projection defaults'
    density: 'Operational, scan-first projection layout.'
  FrontComposerGeneratedForm:
    visual-source: 'FrontComposer command form defaults'
    sectioning: 'Use FluentAccordion when two or more sibling titled sections exist.'
  FluentDataGrid:
    visual-source: 'Blazor Fluent UI component default'
    use: 'Primary table/report/list surface.'
  FluentAccordion:
    visual-source: 'Blazor Fluent UI component default'
    use: 'Required for multi-section pages, dialogs, and detail panels.'
  FluentAccordionItem:
    visual-source: 'Blazor Fluent UI component default'
    use: 'One titled section inside a FluentAccordion.'
  FluentTabs:
    visual-source: 'Blazor Fluent UI component default'
    use: 'Switch between closely related views inside one surface.'
  FluentDialog:
    visual-source: 'Blazor Fluent UI component default'
    use: 'Focused approval, rejection, correction, export, and confirmation actions.'
  FluentButton:
    visual-source: 'Blazor Fluent UI component default'
    use: 'All explicit actions.'
  FluentMessageBar:
    visual-source: 'Blazor Fluent UI component default'
    use: 'Persistent state or policy communication on the current surface.'
  FluentToast:
    visual-source: 'Blazor Fluent UI component default'
    use: 'Transient save, submit, export, and refresh feedback.'
  FilterBar:
    visual-source: 'Composed from Fluent UI inputs/selects through FrontComposer patterns'
    use: 'Dense operational filtering above grids and reports.'
  StatusBadge:
    visual-source: 'Use Fluent UI badge/status affordances where available; otherwise FrontComposer semantic display.'
    use: 'Approval, projection freshness, billable, contributor category, and correction state.'
---

## Brand & Style

Hexalith.Timesheets is a professional evidence tool inside the Hexalith ecosystem. It should read as operational software for capture, approval, audit, and reporting, not as a consumer timer app or finance product.

The visual identity inherits from Hexalith FrontComposer and Blazor Fluent UI. Timesheets does not introduce a bespoke visual brand, custom palette, custom typography, or custom component skin. Its design distinction is semantic clarity: time evidence, approval state, correction lineage, contributor type, billable status, and projection freshness must be visible and scannable.

The product should feel calm, factual, and work-focused. Dense information is expected; decorative layouts, marketing hero sections, custom cards, ornamental color, and gamified tracking are out of place.

## Colors

No Timesheets-specific color tokens are defined. Use the active Fluent UI theme and FrontComposer theme integration for surfaces, text, borders, focus, hover, selected rows, destructive actions, and disabled states.

Color is semantic, not decorative:

- Approval and rejection states use the existing Fluent/FrontComposer status treatment.
- Billable and non-billable must remain legible without relying on color alone.
- Projection freshness and magic-link validity states must pair color treatment with clear text.
- AI effort metrics must not use a special branded color that makes agent effort look promotional.

Do not create custom CSS variables or one-off colors for Timesheets unless FrontComposer and Fluent UI have no suitable token or component state.

## Typography

Typography is inherited from FrontComposer and Fluent UI. Use the default heading, label, grid, body, and form text roles supplied by the shell and components.

Timesheets should not use editorial display type, custom font families, large hero text, or decorative labels. Page titles and section headings should be short and operational. Grid cells, field labels, status text, and report summaries should prioritize scan accuracy over expressive tone.

## Layout & Spacing

Use the FrontComposer shell as the page frame. Primary internal surfaces are desktop/laptop-first responsive web surfaces with dense grids, filters, detail panels, and command forms.

Use the spacing scale in frontmatter only where a Timesheets-specific layout decision is needed. Otherwise inherit component spacing from FrontComposer and Fluent UI.

Rules:

- Keep one primary grid or form visible; do not hide the only primary content in an accordion.
- When a page, dialog, or panel has two or more sibling titled content sections, group them in one `FluentAccordion` with one `FluentAccordionItem` per section and expand the primary section by default.
- Filter-heavy report pages use a `FilterBar` above `FluentDataGrid`; the grid remains the main visual object.
- Detail pages can use a summary header followed by accordion sections for evidence, approval, correction lineage, and audit metadata.
- External magic-link confirmation is a single-purpose surface with the proposed entry details and confirm/adjust choices visible without broad navigation.

## Elevation & Depth

Use the depth and layering already supplied by FrontComposer and Fluent UI. Do not add custom shadows or floating cards to create hierarchy.

Hierarchy comes from shell navigation, page titles, grid grouping, tabs, accordions, dialogs, and message bars. Dialog overlays should be used for focused decisions: reject with reason, approve, correct, export, confirm magic-link entry.

## Shapes

Use Fluent UI component shapes and the FrontComposer defaults. If a Timesheets-specific custom component is unavoidable, keep corners restrained: `{rounded.sm}` for small controls, `{rounded.md}` for compact surfaces, and `{rounded.lg}` for dialogs or panels. Avoid large rounded cards and pill-heavy layouts except where Fluent uses them for status affordances.

## Components

- **FrontComposerShell** - Owns application frame, navigation, page chrome, authentication context, and module placement. Timesheets should not introduce a parallel shell.
- **FrontComposerProjectionView** - Default for generated query/projection surfaces such as time entries, periods, activity types, and ledgers. Prefer projection-driven views over hand-built tables.
- **FrontComposerGeneratedForm** - Default for command surfaces such as record time, submit period, approve/reject, correct entry, manage activity type, and export evidence.
- **FluentDataGrid** - Primary component for entry queues, timesheet period rows, activity type catalogs, operational reports, and approved ledger results. It should support sorting, pagination, selection where needed, and keyboard traversal.
- **FluentAccordion** - Required for pages/dialogs/panels with multiple sibling titled sections. Use for entry details, correction lineage, export evidence details, and activity type governance details.
- **FluentAccordionItem** - Titled section inside a `FluentAccordion`. Use for Evidence, Approval, Correction Lineage, AI Metrics, Export Scope, Audit Metadata, and Policy sections.
- **FluentTabs** - Use only when switching between closely related views on the same object, such as Entries / Periods / Ledger or Human / External / AI metrics. Do not use tabs as global navigation.
- **FluentDialog** - Use for bounded decisions with explicit consequences: reject with reason, approve selected entries, correct approved entry, submit period, generate export, confirm external entry.
- **FluentButton** - Use for every explicit command. Button text must be action-specific: `Submit period`, `Reject entry`, `Confirm time`, `Export ledger`.
- **FluentMessageBar** - Use for persistent surface-level information: projection is stale, link expired, permission denied, comments policy, or export scope warning.
- **FluentToast** - Use for transient feedback after successful save, submission, approval, rejection, correction, or export request. Do not use toast for policy or audit information that must remain visible.
- **FilterBar** - Composed from Fluent UI inputs/selects through FrontComposer patterns. Used above grids for Project, Work, Contributor, Activity Type, Billable Flag, Approval State, period, source type, and freshness filters.
- **StatusBadge** - Represents approval state, billable state, correction state, contributor category, magic-link status, and projection freshness. Must pair visual state with text.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Use FrontComposer and Blazor Fluent UI components first. | Build raw HTML/CSS versions of existing components. |
| Inherit Fluent UI professional themes. | Create a Timesheets-only visual theme or custom palette. |
| Make evidence states visible in grids, details, and reports. | Hide approval/correction state behind hover-only UI. |
| Use `FluentDataGrid` for dense operational lists and reports. | Replace grid-first surfaces with decorative cards. |
| Use `FluentAccordion` for multi-section details. | Collapse the only primary content on a page. |
| Pair status color with text and accessible names. | Depend on color alone for approval, rejection, billable, or freshness states. |
| Keep external magic-link pages minimal and no-disclosure on invalid links. | Show tenant, Project, Work, Party, or Time Entry details after invalid/expired link access. |
