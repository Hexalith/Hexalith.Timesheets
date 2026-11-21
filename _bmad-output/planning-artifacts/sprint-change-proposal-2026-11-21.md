---
title: Close gaps in the Timesheets sprint plan
project: timesheets
date: 2026-11-21
status: applied-to-backlog
scope: moderate
---

# Close gaps in the Timesheets sprint plan

## What triggered the correction

The November 21 sprint refresh accounted for every story ID. A comparison of the stories with the architecture at Timesheets `88363d5` found that a developer could satisfy the written finance criteria and still return the wrong disclosure summary. There was also no open story for AD-3 recovery. The period submission coordinator returns early on an unsuccessful aggregate precheck, including a period no-op. The review coordinator checks the freshness of the summaries it receives without comparing their IDs to the full membership. Neither result establishes completion across the period and entry streams. [Architecture decisions](architecture.md) · [Submission coordinator](../../src/Hexalith.Timesheets.Server/TimesheetPeriods/TimesheetPeriodSubmissionCommandService.cs) · [Review coordinator](../../src/Hexalith.Timesheets.Server/TimesheetPeriods/TimesheetPeriodApprovalCommandService.cs)

The tracker also told developers to wait for Platform 4.14 and Epics 1–5. At the inspected Platform revision `0aacf1324`, Story 4.14 was in progress, staging deployment Story 4.22 was in backlog, and the Timesheets binding required Development/source mode. Those milestones alone cannot establish that Timesheets has deployed authority, continuity or privacy evidence. [Platform tracker](../../references/Hexalith.Platform/_bmad-output/implementation-artifacts/sprint-status.yaml) · [Platform binding](../../references/Hexalith.Platform/src/Hexalith.Platform.Composition/PlatformTimesheetsBinding.cs)

## Changes to the handoff

| Work | Gap in the previous plan | Correction and acceptance evidence |
| --- | --- | --- |
| 3.6 and 3.7, magic links | The tracker named upstream milestones. The epic described the in-process HTTP checks without a deployed privacy criterion. | Give each story its own outstanding evidence. For 3.6, identify the authorized deployment and verify owner histories, restart and shared rebuild. For 3.7, compare responses and check ingress, sidecar and application diagnostics. Record observed boundaries in the launch register. |
| 6.1, finance disclosure | The mixed-policy case had no expected preview result. Export tests could miss an earlier disclosure in a rebuilt ledger or historical value. | Exercise direct and rebuilt publication before export. Assert the existing v1 mixed summary and text, then check permitted and withheld values in the generated outputs. The export review journey now describes that summary. |
| 6.2 and 6.3, finance delivery | Receipt fields and pagination edge cases were listed without all their expected results. | Verify the stored receipt against the exact bytes and tenant scope. Continue past an empty authorized page with an advancing cursor. Withhold delivery on drift, a stalled cursor, lost disclosure or failed eligibility. |
| 6.4 and 6.5, external recording | Confirmation covered restart. The recording timeout case did not specify whether the retry must survive process loss. | Exercise the recording retry after restart with the same command identity. The shorter title for 6.5 produces a complete BMAD key and preserves its backlog status and previous heading anchor. |
| 6.6, package discovery | FR22 in the PRD describes both package consumer shapes. The coverage map omitted the open qualification story. | Map FR22 to 6.6. Its existing two packed-consumer tests define the evidence. |
| 6.7–6.9, integration and reporting | The November 20 proposal already gives these stories owners, predecessors and measurable outcomes. | Platform and Timesheets compose one Project path in 6.7. The release owner qualifies that path in 6.8. Reporting supplies the representative volume and budget for 6.9. |
| 6.10, period recovery | AD-3 added durable intent, full-roster admission and recovery requirements after the original period stories were completed. | Add a backlog story for FR7 and FR8. Exercise a two-entry interruption, restart and concurrent retries. Verify each required owner effect once, preserve attribution and show a pending period until completion is proven. |

The changes are recorded in the epic criteria, the FR7 user outcome, the export and period experience, the two active magic-link story files and the tracker. The November 21 architecture supplies the implementation contract. [Epics](epics.md) · [PRD](prds/prd-timesheets-2026-06-18/prd.md) · [Experience](ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md) · [Sprint tracker](../implementation-artifacts/sprint-status.yaml)

## Delivery order and ownership

1. **Timesheets delivery owner** starts with 6.1. Use one synthetic marker across current, original and corrected values. The finance increment requires all criteria in 6.1–6.3 before delivery. The November 20 estimate was 1–3 engineer-days for the first disclosure and rejection checks, then 3–6 days for durable audit and snapshot handling. [Finance sequencing](sprint-change-proposal-2026-11-20.md)
2. **Timesheets and EventStore owners** take 6.10 as a separate backlog item. Size it after identifying the persistence entry point that can save admitted intent and read back individual effects. An early completion acknowledgement is the principal risk. The restart case is the acceptance test. The product owner can schedule this work independently of the finance increment. [Recovery criteria](epics.md#story-610-recover-interrupted-period-submissions-and-reviews)
3. **Platform and Timesheets owners** resume deployed 3.6 and 3.7 when they can identify the authorized environment and observe each required boundary. The release owner records those results and tracks the separate 6.8 Project qualification, tenant/legal decisions and product UAT. The release verdict is **FAIL**. [Launch register](../../docs/launch-readiness.md)

This correction assigns work within the adopted product scope. Reopening completed stories would mix older acceptance evidence with new recovery obligations. A separate backlog item gives that work an owner and a verifiable outcome.
