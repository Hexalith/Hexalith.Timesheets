---
title: "Hexalith.Timesheets - Product Brief"
status: draft
created: 2026-06-18
updated: 2026-06-18
---

# Product Brief: Hexalith.Timesheets

## Executive Summary

Hexalith.Timesheets is the Hexalith module for recording time spent against either a Project or a Work item. It serves three purposes at once: personal time capture, billing/accounting evidence, and operational reporting. A time entry is not just a note in a weekly sheet; it is an attributable, tenant-scoped fact that says who spent time, when, for how long, on what, why, whether it is billable, and whether it has been approved.

The module matters because Hexalith work is not done only by internal employees. Internal users, external parties, and AI agents can all contribute effort, and all three need a common time-evidence model. Most timesheet products assume a human user logging against a project or task. Hexalith.Timesheets should treat every contributor as a Party and every recorded duration as an auditable event, whether it came from a staff member, a supplier, a customer, or an AI agent running work through Hexalith.Works.

Why now: Hexalith already has Projects for project context and Works for work coordination. Without Timesheets, there is no durable answer to "where did the time go?", "what can we bill?", "what did this work actually cost in effort?", or "which AI/human/external contribution should be approved?" The module closes that gap while staying thin: Projects and Works own their own domain objects; Timesheets owns the time records and their approval state.

## The Problem

Time is currently scattered across personal memory, comments, work-item progress, external supplier updates, AI execution traces, and accounting exports. That creates three overlapping problems.

First, people need a low-friction way to record time on the thing they actually worked on: a Project or a Work item. If time entry is detached from the work context, it becomes after-the-fact administration and the data decays quickly.

Second, billable and accounting workflows need trusted evidence, not rough recollection. A duration that may become invoiceable or cost evidence needs actor identity, date, activity type, comment, billable classification, and an approval path. Corrections must be traceable rather than silently overwriting history.

Third, operational reporting needs actual effort by project and work item. Hexalith.Works can estimate and burn down work, but Timesheets is the complementary "actual time spent" ledger. Without it, teams cannot compare planned effort to actual effort, understand capacity, or see how much human, external, and AI time a project consumed.

## The Solution

Hexalith.Timesheets provides a tenant-scoped time-entry ledger for Project and Work references.

A time entry contains:

- Date and duration.
- A target reference: Project or Work item.
- Activity type.
- Comment.
- Billable flag.
- Approval state.
- Actor identity, resolved through the Hexalith Party model.

The core experience is simple: a contributor records time against a Project or Work item, optionally marks it billable, adds context, and submits it for approval when required. Approvers can approve or reject individual entries and can also approve a weekly or monthly timesheet period. Reports aggregate time by actor, Project, Work item, activity type, billable status, approval state, and period.

The technical shape should follow Hexalith's domain-module rules. Timesheets persists domain state through Hexalith.EventStore, emits append-only events for recorded, submitted, approved, rejected, and corrected time, and derives read models through projections. It references Projects and Works by stable IDs; it does not copy project/work content or become the system of record for project structure or work lifecycle.

## What Makes This Different

The market baseline is mature: tools such as Harvest, Clockify, Asana, Jira/Tempo, and Toggl already handle time tracking, billable hours, approvals, reports, and invoicing workflows. Hexalith.Timesheets should not compete as a generic timer app.

The difference is the Hexalith context:

- **Actor-neutral time evidence.** Internal users, external parties, and AI agents record time through one model. There is no separate "human timesheet" versus "agent runtime log" product surface.
- **Project and Work native.** Time can attach directly to Hexalith.Projects or Hexalith.Works references, letting actual effort roll up by both project structure and work execution.
- **Event-sourced auditability.** Time records, approvals, rejections, and corrections are append-only facts. Approved time can be trusted for billing/accounting because the path to approval is visible.
- **Operational feedback loop.** Works tracks planned/remaining effort; Timesheets tracks actual spent effort. Together they give a real planned-vs-actual view.
- **Thin module boundary.** Timesheets owns time evidence and approval state only. It references Tenants, Parties, Projects, Works, and EventStore rather than duplicating their responsibilities.

## Who This Serves

**Internal users** record their own time against projects and work items, review what they have submitted, and correct mistakes through traceable adjustments.

**External parties** record or confirm time spent for a tenant, project, or work item through API-only integration or magic-link confirmation without becoming full internal users.

**AI agents** record wall-clock execution time, model/tool runtime, billable effort, and token consumption against work items or projects so agent effort is visible beside human and external effort.

**Approvers and project managers** validate submitted time, resolve rejected entries, and use approved time for project reporting, utilization, and operational control.

**Finance/accounting consumers** use approved billable time as evidence for downstream billing, export, or invoicing. Timesheets owns the billable flag and approved-time ledger; full invoicing, rates, and revenue recognition remain downstream concerns.

## Success Criteria

- Internal users, external parties, and AI agents can record time against a Project or Work item through the same domain model.
- A time entry captures date, duration, target reference, activity type, comment, billable flag, actor identity, and approval state.
- Submitted entries can be approved or rejected individually; weekly or monthly timesheet periods can also be approved.
- Approved entries become locked from silent mutation. Corrections are additive and auditable.
- Project and Work reports show actual time by actor, period, activity type, billable status, and approval state.
- Work-item reporting can compare planned/estimated effort from Works with actual time from Timesheets.
- AI-agent reporting shows wall-clock time, model/tool runtime, billable effort, and token consumption.
- The module stays event-sourced, tenant-isolated, and reference-based: no direct database persistence, no copied Project/Work/Party data, and no hand-written infrastructure store.

## Scope

**In for the first version:**

- Time entry recording against Project or Work references.
- Actor-neutral contributor model for internal users, external parties, and AI agents.
- Entry fields: date, duration, target reference, activity type, comment, billable flag, approval state.
- Activity type catalogs scoped by tenant and project.
- Entry-level submit, approve, reject, and additive correction flows.
- Weekly and monthly timesheet-period approval.
- Query/report read models by actor, project, work item, period, activity type, billable status, and approval state.
- AI-agent metrics: wall-clock execution time, model/tool runtime, billable effort, and token consumption.
- External-party v1 surface through API-only integration and magic-link confirmation.
- EventStore-backed persistence and replayable projections.
- Tenant isolation and Party-based actor references.

**Explicitly out of the first version:**

- Payroll processing.
- Invoice generation and payment tracking.
- Rate-card ownership and revenue recognition.
- Automatic desktop/app activity tracking.
- Calendar import and meeting inference.
- AI interpretation of natural-language time notes.
- Full external-party portal UX beyond API-only integration and magic-link confirmation.
- Replacing Project or Work ownership of their own state.

## Vision

Hexalith.Timesheets becomes the trusted effort ledger of the Hexalith ecosystem. Projects show where time is going. Works shows what work consumed that time. Finance sees which time is billable and approved. Operators see planned versus actual effort. AI agents become accountable contributors rather than invisible automation.

In the longer term, Timesheets can power richer economics: project profitability, utilization, agent-vs-human effort comparison, budget burn, and billing exports. The core bet remains small and durable: every unit of spent time is recorded as an attributable, approved, tenant-scoped fact.

## Resolved Decisions

- Approval happens both per entry and per weekly/monthly timesheet period.
- Activity types are scoped by tenant and project.
- Timesheets owns the billable flag and approved-time ledger; billable rates and invoicing remain outside the module.
- AI-agent entries can include wall-clock execution time, model/tool runtime, billable effort, and token consumption.
- The minimum external-party experience for v1 is API-only integration plus magic-link confirmation.
