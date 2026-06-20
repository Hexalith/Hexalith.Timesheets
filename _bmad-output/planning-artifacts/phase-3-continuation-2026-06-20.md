---
project: timesheets
phase: 3
phaseName: AI-agent evidence
date: 2026-06-20
owner: John
status: continuation-ready
sourceArtifacts:
  - _bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/implementation-artifacts/1-9-project-ai-assisted-time-capture-metrics.md
  - _bmad-output/implementation-artifacts/4-4-surface-ai-effort-reporting.md
  - _bmad-output/implementation-artifacts/epic-1-retro-2026-06-19.md
  - _bmad-output/implementation-artifacts/epic-4-retro-2026-06-19.md
---

# Phase 3 Continuation: AI-agent Evidence

## Phase Definition

Phase 3 in the Timesheets PRD is AI-agent evidence:

- Add AI Effort Metrics capture.
- Report AI effort separately from human and external effort.
- Tie AI evidence to Works execution context through stable references and source metadata.

The implemented scope maps primarily to Story 1.9 and Story 4.4:

- Story 1.9: Project AI-Assisted Time Capture Metrics.
- Story 4.4: Surface AI Effort Reporting.

## Current State

Phase 3 implementation artifacts already show the relevant AI evidence stories as `done`.

Delivered behavior includes:

- AI-agent Time Entries can carry wall-clock runtime, model/tool runtime, billable effort, provider token counts, token availability, and source metadata.
- Missing provider token metrics are represented as unavailable or not reported, never as synthetic zero.
- Human/external duration, AI wall-clock runtime, AI model/tool runtime, AI billable effort, and token counts remain separate units.
- AI capture reuses the existing Time Entry command path and fail-closed tenant, Party, Project, Work, Activity Type, policy, and audit gates.
- AI effort reporting extends actual-time reports with explicit AI units, AI source metadata, result-level authorization, freshness, and FrontComposer metadata.
- AI metrics are not treated as approval, payroll, invoice, rate, tax, revenue, or finance authority.

## Product Readiness Position

Phase 3 can be treated as story-complete.

The product claim should stay precise:

- Timesheets supports AI-agent effort evidence as multi-unit operational evidence.
- Timesheets can report AI effort beside human and external effort without converting units by default.
- Timesheets stores stable references and compact AI source metadata, not prompts, responses, provider payloads, secrets, or sibling-owned state.

Do not claim full live Works execution integration unless the Works adapter/query path is explicitly made concrete.

## Launch-readiness Caveats

The following caveats should be carried as launch-readiness or future-planning work:

1. Define or implement the concrete Works adapter path for planned/execution context where product claims require live Works data.
2. Keep `IWorkPlannedEffortProvider` fail-closed or unavailable unless backed by a Works-owned query/projection adapter.
3. Add realistic EventStore-backed performance evidence for AI capture/reporting once persisted fixtures are available.
4. Keep dashboard and report claims clear when counts come from bounded pages or unavailable external providers.
5. Keep provider token metrics out of finance semantics unless a later approved story defines a governed conversion or cost model.

## Recommended Continuation

Proceed with Phase 3 launch-readiness before making production claims about AI/Works integration:

1. Promote a Works adapter readiness story if live Works execution/planned-effort context is in launch scope.
2. Add persisted-fixture performance evidence for AI capture and AI report queries.
3. Keep AI evidence documentation explicit about separate units, unavailable token metrics, and no token-to-hours conversion.

No sprint-status transition is needed in the current workspace because Story 1.9, Story 4.4, and their containing epics are already marked `done`.
