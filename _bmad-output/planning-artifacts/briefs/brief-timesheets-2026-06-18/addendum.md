# Addendum: Hexalith.Timesheets Product Brief

## Discovery Notes

- User stated the module records time spent on projects or work items.
- User confirmed the module serves personal capture, billing/accounting evidence, and operational reporting.
- User confirmed internal users, external parties, and AI agents can record time.
- User confirmed time entries include date, duration, project/work reference, activity type, comment, billable flag, and approval.
- Fast path selected; initial `[ASSUMPTION]` tags were resolved by the user's follow-up decisions.
- User resolved approval scope: both per-entry approval and weekly/monthly timesheet-period approval.
- User resolved activity type ownership: activity types are scoped by tenant and project.
- User resolved billing boundary: Timesheets owns the billable flag and approved-time ledger; rates, invoicing, and revenue recognition are downstream.
- User resolved AI-agent metrics: wall-clock execution time, model/tool runtime, billable effort, and token consumption.
- User resolved external-party v1 surface: API-only integration and magic-link confirmation.

## Resolved Product Decisions

### Approval Model

Timesheets must support approval at two levels:

- Individual time entries can be approved or rejected.
- Weekly or monthly timesheet periods can be approved as a grouped review artifact.

This implies the product should distinguish entry state from period state. A rejected entry inside an otherwise submitted period should remain traceable, and period approval should not silently rewrite entry history.

### Activity Catalog Scope

Activity types are scoped by tenant and project. Tenant-level activity types provide a shared vocabulary; project-level activity types allow local specificity where the project needs more precise categorization.

### Billing Boundary

Timesheets owns:

- The billable flag on recorded time.
- The approved-time ledger.
- Approved-time reporting/export surfaces.

Timesheets does not own billable rates, invoice generation, payment tracking, or revenue recognition in v1.

### AI Agent Time Evidence

AI-agent entries can carry multiple effort/economics dimensions:

- Wall-clock execution time.
- Model/tool runtime.
- Billable effort.
- Token consumption.

These are related but not interchangeable. Downstream PRD/architecture should model them explicitly rather than collapsing every AI contribution into one `duration` field.

### External Party Surface

The minimum v1 external-party experience is API-only integration plus magic-link confirmation. A full external-party portal is out of scope.

## Landscape Digest

The current market baseline is broad. Existing products already support many standard timesheet capabilities:

- Harvest positions time tracking around billing, profitability, clients, projects, tasks, and invoicing.
- Asana's timesheets and budgets feature supports time by task or project, billable/non-billable separation, approvals, dashboards, and cost reporting.
- Clockify supports billable tracking, invoicing, formal timesheet approvals, locking historical time, manager roles, and task rates.
- Tempo/Jira supports logging time directly in Jira work items, viewing worklogs by permission, reports, and suggested activity logging from integrations.
- Microsoft Work Trend Index material frames AI agents as becoming part of work execution, which strengthens the need for time evidence that can cover both human and AI contributors.

Implication for Hexalith.Timesheets: the product should not be framed as a generic time tracker. Its stronger position is an event-sourced, tenant-isolated, actor-neutral time evidence module for the Hexalith ecosystem.

## Source Links

- Harvest: https://www.getharvest.com/
- Asana Timesheets and Budgets: https://asana.com/features/resource-management/timesheets-budgets
- Clockify pricing/features: https://clockify.me/pricing
- Tempo Timesheets for Jira work items: https://help.tempo.io/timesheets/latest/logging-time-in-jira-issues-using-tempo-timesheets
- Microsoft 2026 Work Trend Index: https://www.microsoft.com/en-us/worklab/work-trend-index/agents-human-agency-and-the-opportunity-for-every-organization
- Microsoft 2025 Work Trend Index: https://www.microsoft.com/en-us/worklab/work-trend-index/2025-the-year-the-frontier-firm-is-born
