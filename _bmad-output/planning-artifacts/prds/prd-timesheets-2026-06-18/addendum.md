# Addendum: Hexalith.Timesheets PRD

This addendum preserves context that is useful for architecture, UX, and story creation but should not crowd the PRD body.

## Source Inputs

- Product brief: `D:\Hexalith.Timesheets\_bmad-output\planning-artifacts\briefs\brief-timesheets-2026-06-18\brief.md`
- Product brief addendum: `D:\Hexalith.Timesheets\_bmad-output\planning-artifacts\briefs\brief-timesheets-2026-06-18\addendum.md`

## Current Landscape Notes

The market baseline is mature and should be treated as table stakes:

- Harvest presents time tracking, reporting, invoicing, budgets, team management, and profitability as an integrated business workflow.
- Asana's Timesheets and Budgets add-on includes centralized timesheet management, approvals, budget/cost tracking, billable and non-billable rates, reporting, and exports.
- Clockify supports weekly/monthly timesheet approvals, locking after approval, manager review, and audit history around changes.
- Tempo Timesheets for Jira centers time logging directly inside Jira work items and closes approval periods after timesheets are approved.

Implication: Hexalith.Timesheets should avoid generic time-tracker positioning. Its launch-grade thesis is actor-neutral, event-sourced effort evidence inside the Hexalith ecosystem.

## Source Links

- Harvest: https://www.getharvest.com/
- Harvest pricing/features: https://www.getharvest.com/pricing
- Asana Timesheets and Budgets: https://asana.com/features/resource-management/timesheets-budgets
- Clockify timesheet submission/approval: https://clockify.me/help/track-time-and-expenses/submit-time-expenses-for-approval
- Clockify Standard Plan features: https://clockify.me/standard-plan/features
- Tempo Timesheets Jira work-item logging: https://help.tempo.io/timesheets/latest/logging-time-in-jira-issues-using-tempo-timesheets
- Tempo Jira time tracking product page: https://www.tempo.io/products/jira-time-tracking
- Microsoft 2026 Work Trend Index: https://www.microsoft.com/en-us/worklab/work-trend-index/agents-human-agency-and-the-opportunity-for-every-organization
- Microsoft 2025 Work Trend Index: https://www.microsoft.com/en-us/worklab/work-trend-index/2025-the-year-the-frontier-firm-is-born

## Candidate Event Catalog for Architecture

Names are not final requirements, but the PRD implies these event families:

- `TimeEntryRecorded`
- `TimeEntrySubmitted`
- `TimeEntryApproved`
- `TimeEntryRejected`
- `TimeEntryCorrected`
- `TimeEntrySuperseded`
- `TimesheetPeriodSubmitted`
- `TimesheetPeriodApproved`
- `TimesheetPeriodRejected`
- `ActivityTypeCreated`
- `ActivityTypeRenamed`
- `ActivityTypeDeactivated`
- `ExternalTimeEntryConfirmed`
- `AiEffortMetricsRecorded`
- `ApprovedTimeExported`

Architecture should align naming with the module's C# contract rules and existing Hexalith EventStore conventions.

## Architecture Handoff Notes

- Define the aggregate boundary explicitly. A launch-grade design likely needs at least Time Entry, Timesheet Period, and Activity Type aggregate or state boundaries, but the PRD deliberately leaves exact aggregate partitioning to architecture.
- Decide whether a correction is represented as a new Time Entry linked to the original, a correction event on the original aggregate, or both.
- Avoid a direct dependency from contracts to Projects/Works/Parties infrastructure. Public contracts should carry stable IDs and validation should sit behind server-side adapters/projections.
- Treat magic-link confirmation as a security-sensitive capability, not just a UX shortcut.
- Treat AI Effort Metrics as multi-unit evidence. Do not collapse token counts, model runtime, wall-clock time, and billable effort into one duration field.
- Plan projection freshness and rebuild behavior before story slicing. Approved-Time Ledger correctness is launch-grade evidence, not a convenience report.
