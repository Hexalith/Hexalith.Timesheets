---
title: "Qualify Project time and finance evidence after the v2.1.1 audit"
project: timesheets
date: 2026-11-20
status: applied-to-backlog
mode: batch
scope: moderate
---

# Qualify Project time and finance evidence after the v2.1.1 audit

## 1. Issue summary

The November 20 audit tested release v2.1.1 at `b72eff51895982cec2de7746e8f06c9da3c5f401`. Six .NET suites reported 2,874 passes and five default skips. The audit also exercised published-package discovery and failure cases that those suites missed. For example, the compiled export service reported both audit rejection and successful generation for the same request. A separate run produced two current rows for one entry when the checkpoint changed between pages.

The correction specifies acceptance tests for finance, external contributors and Project approvers at their actual entry points. One onboarding defect is corrected in this change. Functional repairs are queued in the sprint tracker. The overall launch decision is **FAIL**. [Launch readiness](../../docs/launch-readiness.md)

## 2. Impact on existing work

| Audit finding at v2.1.1 | Evidence at the audited revision | Follow-up |
|---|---|---|
| 1. Production Project path | The kernel defaults to deny/unavailable providers. The owner declares four commands, covering recording and magic links. The pinned Platform binding requires Development/source mode. `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs:110`, `src/Hexalith.Timesheets.Server/Runtime/TimesheetsRouteCatalogDeclarations.cs:16`, `references/Hexalith.Platform/src/Hexalith.Platform.Composition/PlatformTimesheetsBinding.cs:22` | 6.7 wires the journey across Epics 1, 2 and 4. Story 6.8 qualifies it in deployment. |
| 2. Export comment disclosure | Preview checks the tenant option. Generation copies row and correction evidence. `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs:187` and `:309` | 6.1 tests tenant and per-row decisions across preview, CSV and returned rows. |
| 3. External confirmation | The HTTP handler passes null for the recorded state. The aggregate returned `not-recorded` with that input. `src/Hexalith.Timesheets/Endpoints/ExternalContributionEndpoints.cs:58`, `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs:1008` | 6.4 follows up Story 3.1 through workload-authenticated HTTP. |
| 4. Export after audit rejection | The default recorder returns an event in memory. A rejecting recorder still yielded one export row. `src/Hexalith.Timesheets.Server/Exports/DomainEventApprovedTimeExportAuditRecorder.cs:8`, `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs:140` | 6.2 follows up Story 4.6 with rejection, uncertainty, retry and persisted evidence. |
| 5. Export from different checkpoints | The service starts at the supplied cursor. Fresh pages at checkpoints 100 and 101 with an overlapping row produced a file. `src/Hexalith.Timesheets.Server/Exports/ApprovedTimeExportService.cs:342` | 6.3 follows up Stories 4.2/4.5 with mutation between pages and cursor progress tests. |
| 6. Repeated projection work | For 60, 240 and 960 entries, one page took 5.9, 132.8 and 962.5 ms and allocated 1.9, 31.2 and 517.3 MiB. Five warmed Debug calls per size measured calling-thread allocations under concurrent sandbox load. `src/Hexalith.Timesheets.Projections/ApprovedTimeLedger/ApprovedTimeLedgerProjection.cs:36` | 6.9 compares the same fold and output before considering materialization. |
| 7. Standalone package discovery | A Projections-only NuGet consumer enumerated four projections. Accessing the combined routes threw `FileNotFoundException` for Server. `src/Hexalith.Timesheets.Projections/TimesheetsProjectionRouteCatalogDeclarations.cs:38`, `tools/package-consumers/Projections.cs:1` | 6.6 exercises both clean package consumer shapes. |
| 8. Unbounded recording readback | External recording uses caller cancellation during readback and maps an uncertain read to the same refusal as a policy denial. `src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs:70` and `:131` | 6.5 follows up the recording path with a deadline, diagnostics and retry proof. |
| 9. Stale onboarding | Build instructions used SDK 10.0.403, Aspire 13.6.4 and net10.0 paths. `README.md:30`, `global.json:3`, `Directory.Build.props:23` | README corrected in this change. |

The follow-up is grouped in Epic 6 so the team can schedule new work while retaining the original story receipts. Stories 3.6 and 3.7 continue to own deployed magic-link evidence. The Works host and wire-performance waivers retain their named owners and revisit conditions. The consuming FrontComposer host owns rendered accessibility. Preview HTTP and structured API/webhook export are post-v1. [Scope classifications](../../docs/launch-readiness.md#launch-scope-classification)

## 3. Recommended approach and order

Direct adjustment fits the existing EventStore command path, authorization ports, package-validation lane and AppHost. Rolling back a released version would remove unrelated fixes and would still leave the missing bindings and export cases. Reducing MVP scope would withdraw promised confirmation or CSV behavior. The first Project slice limits coordination while exercising the approval-to-ledger boundary.

| When | Work, owner and dependencies |
|---|---|
| First 1–3 engineer-days | Timesheets delivery owner takes 6.1 first and the rejection guard from 6.2 in the same finance increment. An unverified recorder must withhold delivery. The README correction is applied. In parallel, the integration owner inventories existing Platform bindings for 6.7. |
| Next 1–2 weeks | Timesheets and the EventStore owner settle replay identity and snapshot semantics. Finish durable audit and pagination (6.2–6.3, 3–6 engineer-days together). Retrying on drift is simpler than retaining snapshots and can repeat work on a busy ledger. Schedule 6.4 (2–4 days) alongside 6.5 (1–2 days), sharing readback behavior where appropriate. The package owner can deliver 6.6 independently (1–2 days) and coordinate discovery compatibility before new receivers land. |
| Over 2–4 weeks across owners | Timesheets and Platform deliver 6.7, then qualify 6.8 with one authorized tenant and a second tenant for negative cases. Begin 6.9 (2–4 engineer-days) once the export and correction golden cases are fixed. Obtain representative tenant volumes and time/allocation budgets from reporting before proposing new storage. |

Estimates assume familiarity with EventStore. Environment access, tenant/legal decisions and stakeholder availability can extend elapsed time. The production binding owner should first check for an independently maintained composition that already satisfies some of 6.7.

## 4. Edits applied

| Artifact | Previous behavior or wording | Change and reason |
|---|---|---|
| PRD §§5.4, 5.5, 13 | FR13 required scoped external access. FR19 required stable IDs and lineage. The rollout named generic integration tests. | Added retry-safe recorded confirmation, complete snapshot, tenant-plus-row disclosure, audit-before-delivery and one deployed Project acceptance journey. These clarify v1 acceptance. [PRD](prds/prd-timesheets-2026-06-18/prd.md) |
| Architecture | The architecture identified EventStore and rebuildable read models but left the commit/readback and delivery boundaries implicit. Current tooling notes still cited the old Aspire graph. | Specified the owning command path, trusted policy, audit acknowledgement, snapshot continuity, package ownership and measured projection change. Updated active build guidance. The existing topology serves this workflow. [Architecture](architecture.md#qualification-of-the-project-and-finance-workflows) |
| UX experience, Export Review Dialog and UJ-5 | The flow acknowledged an export request and proceeded to finance delivery. | Distinguished request acceptance from file availability. An unverified export offers a neutral retry. The existing dialog, visual design and host ownership suffice. [Experience](ux-designs/ux-timesheets-2026-06-18/EXPERIENCE.md#state-patterns) |
| Epics and sprint tracker | Epics 1–5 tracked original story acceptance, with no follow-up criteria for the nine findings. | Added Epic 6 and nine backlog stories with observable acceptance criteria. The production finding has separate implementation and qualification stories. The onboarding finding is addressed directly. [Epics](epics.md#epic-6-qualify-project-time-and-finance-evidence) · [Sprint status](../implementation-artifacts/sprint-status.yaml) |
| Launch register and README | The register cited a CSV golden case for default comment exclusion. The README gave stale build prerequisites and executable paths. | Recorded the open finance, FR13 and Project cases. Aligned onboarding with the SDK, target framework, Aspire bundle and root package overrides, including a fresh-checkout smoke command. [Register](../../docs/launch-readiness.md) · [Build and test](../../README.md#build-and-test) |

## 5. Implementation handoff

**Scope is moderate.** The product owner sequences Epic 6. Timesheets owns the export, external command, projection and package changes. The Platform integration owner supplies or locates the release composition. The EventStore owner confirms supported durable acknowledgement and snapshot capabilities. The tenant/platform and legal owners approve deployed policy and retention. The product owner collects UAT. These role assignments identify who can produce evidence. They do not record anyone’s sign-off.

**First developer story is 6.1.** Use one synthetic comment in an allowed ledger row with tenant export disabled. Assert absence in preview disclosure, CSV and every nested returned value before exercising the allowed cases. The finance increment can release output only after 6.1–6.3 meet their acceptance criteria. The separate deployed Project receipt belongs in 6.8. [Story acceptance criteria](epics.md#epic-6-qualify-project-time-and-finance-evidence)

### Verification of this correction

A fresh Linux checkout used the 14 pinned direct submodules from local Git mirrors, the existing NuGet cache, SDK 11.0.100 and the Aspire 17.0.0 CLI bundle. The README’s Debug/source restore and warnings-as-errors build passed with zero warnings and errors. Its `dotnet test --project` ArchitectureTests smoke command passed all 95 cases. This checks the checkout and build instructions with installed tools. The earlier audit’s live Dapr fixture stopped at the missing Dapr CLI. Production acceptance still requires the evidence in Story 6.8 and the separate release gates.

### Change navigation record

| Checklist | Disposition |
|---|---|
| 1.1–1.3 Trigger and evidence | [x] Release audit across several original stories. Evidence and affected IDs are in §2. |
| 2.1–2.5 Epic impact and sequencing | [x] Direct adjustment through one new epic. Prior story receipts are retained. |
| 3.1–3.4 Artifact impact | [x] PRD, architecture, UX behavior, tracking, readiness and onboarding reconciled. [N/A] Visual redesign and new topology. |
| 4.1–4.4 Alternatives and decision | [x] Direct adjustment chosen. Rollback and scope reduction assessed in §3. |
| 5.1–5.5 Proposal and handoff | [x] Evidence, edits, order, roles, effort and acceptance recorded in §§1–5. |
| 6.1–6.5 Review and routing | [x] Applied the requested audit recommendations to the backlog in batch. [!] Developer delivery, Platform/environment qualification and tenant/legal/product approvals remain with the named owners. |
