---
title: 'Qualify five deployed magic-link readiness deferrals'
type: 'chore'
created: '2026-11-15'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '02a69b58f41c2780b4e4fdc14069af2220613e95'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/docs/boundary-decision-record.md'
---

<frozen-after-approval reason="proposed scope; deployment inputs remain unresolved">

## Intent

**Problem:** Five outstanding magic-link obligations require deployed identity,
inventory and delivery evidence. Historical local fixtures cannot establish that
the managed release safely issues links or makes them available through real
projection delivery.

**Approach:** Qualify one exact Platform-managed Timesheets release against
DW-86, DW-113, DW-119, DW-132 and DW-145. Preserve partial results and distinguish
each satisfied gate from every remaining blocker. Commit reviewed Timesheets
changes with a validated Conventional Commit and push to `main`, as requested.
These are five selected obligations, not five claimed closures.

## Boundaries & Constraints

**Always:** Bind sanitized evidence and persisted outcomes to actual release,
target and authority identities. Preserve server-side authorization, fail-closed
defaults, historical evidence and unrelated gitlinks. Keep Story 3.6 in progress
and launch FAIL until their full requirements pass. Use `run-with-temp` for
disposable verification workspaces; retain useful results outside them.

**Never:** Infer deployed authority from fixture claims, manual projection calls,
cluster-admin rights or Development enrollment. Invent historical inventories,
enable permissive providers, mutate read models directly or reuse expired
authority. Platform owns provisioning and release operations. This draft grants
no live mutation authorization or permission to rewrite published history.

## I/O & Edge-Case Matrix

| Scenario | Input / state | Required result | Failure handling |
| --- | --- | --- | --- |
| Installed target | Current Platform release and target binding | Verify installed artifact and private/public routes | Missing or stale identity blocks qualification |
| Management identity | Supported tenant/Party source and server gates | Authorized scope succeeds; forged/cross-tenant scope refuses | Default-deny remains intact |
| Historical ownership | Complete capability/Time Entry inventory | Identify affected Activity Type ownership and approved disposition | Incomplete coverage leaves DW-86 open |
| Read-model inventory | Current components and historical layouts | Prove installed address and separately authorized rebuild/migration | No inference from an absent local store |
| Automatic delivery | Authorized issuance through actual dispatch | Observe persisted index readiness without manual `/project` calls | Retain measured failure/delay and leave gate open |

</frozen-after-approval>

## Open Questions

1. Which current Platform release and authority/inventory handoff should this
   batch consume? The checked-in proposal is `BLOCKED_NOT_FOR_EXECUTION`; both
   proposed approved values files are absent. Options: supply the current handoff;
   prepare prerequisites in Hexalith.Platform instead; or select different backlog
   work. Prerequisite preparation alone cannot close deployed obligations.

## Code Map

- `_bmad-output/implementation-artifacts/deferred-work.md`: open DW-86 (Activity
  Type ownership), DW-113 (management identity), DW-119 (index visibility),
  DW-132 (component/history inventory), DW-145 (automatic delivery).
- `docs/deployed-magic-link-2026-10-17.md`: historical target census and exact
  missing authority, legacy continuity and Platform operational prerequisites.
  Its October observations do not establish November deployment state.
- `references/Hexalith.Platform/eng/timesheets-deployment/platform-managed-operations.json`
  and adjacent `.md`: blocked handoff, missing dispatcher/values; read-only
  dependency documents, not skills. Platform owns environment preparation.
- `src/Hexalith.Timesheets/Program.cs`: production composition retains server
  gates; fixture claims transformation is Development-only. Preserve that boundary.
- `src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs`:
  authorization, tenant and reference defaults remain deny-all/unavailable.
- `src/Hexalith.Timesheets.Projections/MagicLinks/`: compiled projection handlers
  and authority/inventory seams; not proof of installed delivery or coverage.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkLiveDaprTests.cs` and
  `scripts/run_isolated_magic_link_dapr.py`: reuse local acceptance semantics,
  never fixture target/credentials against a deployment.

## Tasks & Acceptance

**Execution:**

- [ ] Resolve the Open Question and revise this draft around the actual supplied
  handoff or explicitly selected replacement scope before implementation.
- [ ] `docs/deployed-magic-link-acceptance.md` — assess the protocol against actual
  release/authority evidence; prepare bound operations before requesting live action.
- [ ] `_bmad-output/implementation-artifacts/tests/five-deployed-magic-link-deferrals-20261115.md`
  — retain the five per-entry outcomes, source identities, exact commands,
  persisted-state evidence and unsatisfied requirements after execution.
- [ ] `_bmad-output/implementation-artifacts/deferred-work.md` — record only
  substantiated outcomes; update `docs/launch-readiness.md` only where evidence
  changes current assertions. Keep every unsatisfied gate open.
- [ ] Independently review, verify the actual diff, validate the commit message,
  inspect submodules and remote divergence, then push completed work to `main`.

**Acceptance Criteria:**

- Given an unbound deployment or missing authority, when assessing a gate, then
  record the exact missing evidence and retain its open status.
- Given supported identity and authorization, when management runs, then accept
  authorized tenant/Party scope and refuse forged context.
- Given certified historical coverage, when assessing ownership/read-model
  readiness, then identify affected histories and require approved remediation.
- Given actual automatic delivery, when a link is issued, then measure time to
  persisted index visibility and successful resolution without manual dispatch.
- Given verified changes, when publishing, then push completed authorized scope
  and distinguish closures from blockers.

## Implementation Notes

Investigation only. Independent searches found prior resolutions for gateway
timeouts, dashboard diagnostics and duplicate scheduler names. Remaining native
signal/dashboard, repair capacity/resource and large shared-store obligations
need evidence/platform contracts; no new local defect was demonstrated. No code,
deployment, shared service or dependency checkout changed.

## Spec Change Log

## Review Triage Log

## Verification

- Before approval: verify every selected ledger entry remains open, every Code
  Map path exists, the Platform proposal remains blocked and the two proposed
  approved values files remain absent. `git diff --check` must pass.
- After input resolution: specify narrow implementation checks. This draft
  claims no build, suite, runtime or deployment pass.
