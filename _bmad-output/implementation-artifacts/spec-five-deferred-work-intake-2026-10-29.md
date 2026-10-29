---
title: 'Select the remaining five deferred-work items'
type: 'chore'
created: '2026-10-29'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'bc97acea37435fea73cc2fa818d27a1d053933fa'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/docs/boundary-decision-record.md'
---

<frozen-after-approval reason="proposed scope awaiting the owner's decision">

## Intent

**Problem:** Five deferrals and publication to main were requested. Nine numbered
entries remain open: DW-19 needs published-history rewriting; eight need deployed
evidence or production authority. No five local closures remain.

**Proposed approach:** Work toward deployed magic-link readiness,
through DW-113/119/143/145/194: trusted management identity, operator binding,
issue-to-use readiness, automatic projection delivery and effective bootstrap
budgets. Begin with Platform-owned prerequisites. This proposal grants no
operational authority or five-closure guarantee. Alternatively, narrow to the
local repair-manifest construction-cost qualification; that would leave all
five deployed items open.

## Boundaries & Constraints

**Always:** Preserve fail-closed authorization and certification, EventStore-only
domain writes, exact source/evidence identities, historical refusals and launch
FAIL. Keep local preparation distinct from deployed acceptance. The owner
authorized preserving and excluding the pre-existing Story 3.6 continuation spec
changes and `evidence/3-6/20261029t092023z-continuation-intake/` on 2026-10-29.
Commit only this run's completed, reviewed work with Conventional Commit
validation and push main without force after checking divergence.

**Never:** Invent closures, adopt fixture claims as production authority, turn
manual projection dispatch into automatic-delivery proof, rewrite published
history, update dependency gitlinks, modify the supplied Works checkout, or
initialize nested submodules. Do not deploy, enroll authority, grant permissions,
run migrations or mutate live domain data on this planning approval alone.
Prepare exact target-bound commands, effects and recovery for any later required
operational approval. Preserve all unrelated changes.

## I/O & Edge-Case Matrix

| Scenario | Evidence or state | Required result | Refusal behavior |
| --- | --- | --- | --- |
| Missing production identity | Workload authentication without trusted tenant/Party binding | DW-113 stays open | Preserve default denial |
| Unbound migration caller | Authenticated principal without certified caller/operation binding | DW-143 stays open | No migration I/O |
| Issuance visibility | Committed issuance and independently observed selected index/readback | Measure DW-119 against the exact release | Local manual delivery grants no deployed pass |
| Automatic delivery | Installed SDK subscriber/dispatch path without manual `/project/v2` | Qualify DW-145 with persisted end state | Missing subscription evidence stays open |
| Delivery budget | Effective client options plus complete certified inventory | Measure DW-194 with timeout/cancellation/recovery | No inferred production budget |

</frozen-after-approval>

## Open Questions

- Choose the execution scope: pursue the five deployed items through
  Platform-owned prerequisites, or narrow this run to local repair construction
  memory/scan-cost qualification. The first requires missing environment,
  authority and certification evidence; the second is partial preparation and
  cannot close those five items. Neither option authorizes live mutation.

## Code Map

- `_bmad-output/implementation-artifacts/deferred-work.md`: open entries are
  DW-19/86/113/119/132/137/143/145/194. Repair capacity/construction cost and native
  dashboard failure visibility also retain unresolved, unnumbered limits.
- `docs/deployed-magic-link-2026-10-17.md`: deployment handoff, production issuer,
  certified continuity and operational approval requirements. Its observations
  are dated evidence, not a fresh cluster census.
- `references/Hexalith.Platform/eng/timesheets-deployment/platform-managed-operations.json`
  and adjacent `.md`: existing Platform-owned proposal remains
  `BLOCKED_NOT_FOR_EXECUTION`, with no granted approval or executable dispatcher.
  Both named approved staging values files are absent at this intake. Consult
  owning repository instructions before any future Platform changes.
- `src/Hexalith.Timesheets/Program.cs`: production composition keeps the kernel
  defaults; fixture access/claim replacements require Development plus opt-in.
- `src/Hexalith.Timesheets/Runtime/HttpContextTimesheetsTrustedContextAccessor.cs`
  and `EventStoreGatewayWorkloadAssertionSource.cs`: trusted request context and
  outgoing tenant/domain/actor bindings; preserve their trust boundaries.
- `src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkIndexMigrationEndpoints.cs`:
  default-deny caller/operation authorizer, separate migration authority and
  unavailable verified-inventory provider. Local caller binding already exists.
- `tests/Hexalith.Timesheets.IntegrationTests/InternalSurfaceGuardTests.cs` and
  `MagicLinkConfirmationHttpBoundaryTests.Management.cs`: existing local
  authorization regressions; no deployed qualification follows from mocks.
- `src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexV6Migration.cs`
  and `tests/Hexalith.Timesheets.Projections.Tests/MagicLinkV6MigrationTests.cs`:
  alternative local measurement scope; SDK request limits are already qualified
  locally and must not be widened in Timesheets.

## Tasks & Acceptance

**Execution:**

- [x] Audit the ledger and existing code without changing user-owned work;
  distinguish actual unresolved obligations from completed local fixes.
- [x] Check the maintained Platform proposal and existence of its required
  values files; retain the exact preparation blocker without operating services.
- [ ] Resolve the scope question, then replace proposed execution tasks with
  file-specific implementation steps supported by the selected scope's evidence.
- [ ] Complete the selected implementation, independent review and appropriate
  checks; record precise dispositions in the ledger and verification report.
- [ ] Validate the commit, fetch and inspect divergence, commit owned completed
  changes and push main; verify the exact published revision.

**Acceptance Criteria:**

- Given the current ledger, when selecting work, then no resolved item is
  relabeled open and no five-closure claim is made from local preparation.
- Given absent production prerequisites, when planning deployed acceptance,
  then the affected items remain open and no fixture or stale receipt enables
  production authority.
- Given completed selected work, when publishing, then unrelated Story 3.6
  changes and dependency pointers are excluded and remote main is verified.

## Implementation Notes

- Intake only. No code, runtime, deployment, authority, dependency, ledger status,
  commit or push changed. The draft cannot become ready-for-dev until the scope
  question and resulting implementation/dependency gaps are resolved.
- Independent read-only investigation confirms the scope. Configuration and
  boundary guidance were inspected; this planning draft requires no code tests.

## Spec Change Log

## Review Triage Log

## Verification

- Parse the ledger's numbered sections and confirm exactly nine `status: open`
  entries with the identifiers above.
- Confirm both Platform-approved staging values files are absent; inspect the
  maintained proposal without treating its dated target facts as current access.
- Run `git diff --check`; verify that no pre-existing file was changed and that
  this draft alone was added by the intake.
