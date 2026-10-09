---
title: 'Close current Story 3.6 public-route review patches'
type: 'bugfix'
created: '2026-10-09'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Story 3.6 route repair passes in process, but its EventStore SDK commit is absent from remote branches, a GET-only canonical route fails startup inventory, and seven accepted review patches leave evidence inconsistent.

**Approach:** Correct the SDK inventory, integrate its commit with EventStore upstream, update the Timesheets gitlink, prove the credentialed HTTP boundary, and reconcile current records with reproducible results. Keep Story 3.6 open for live topology, catalog freshness distinctions, and durable writes.

## Boundaries & Constraints

**Always:** Work in each owning repository. Preserve canonical POST and sidecar authorization, the explicit public marker contract, the Timesheets port guard, and opaque denials. Follow the recorded EventStore rebase decision; commit SDK content and the parent gitlink separately with validated Conventional Commit messages. Report remote reachability accurately.

**Never:** Push without separate explicit approval; weaken security or tests; seed read models directly; add host topology; mark Story 3.6 or the release complete from in-process tests.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| SDK canonical route | GET-only `/project` under a denying fallback | Startup inventory accepts it without the POST policy | Anonymous or public-marker variants fail startup |
| Protected projection | Identical signed dispatch on public and internal Timesheets ports | Public port returns 404; internal port reaches projection | Invalid credentials remain denied |
| Public capability | Four anonymous routes and metadata | Existing in-process reachability and opaque failures remain | No unexpected 401 or disclosure |

</frozen-after-approval>

## Code Map

- `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceEndpointInventory.cs` — restore POST-only catalog-policy requirement; retain all-method public denial.
- `references/Hexalith.EventStore/tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs` — add GET-only canonical startup cases.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` — `PostProjectionAsync` omits public-port credentials.
- `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, `README.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml` — stale 401 claims and story inventory.
- `docs/launch-readiness.md`, `_bmad-output/implementation-artifacts/deferred-work.md` — waiver, blocker ownership, ledger convention.
- `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md` — final commands, counts, capture provenance.

## Tasks & Acceptance

**Execution:**
- [ ] `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceEndpointInventory.cs` and `references/Hexalith.EventStore/tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs` — repair the GET-only rule, add positive and negative startup cases, then rebase the owned SDK commit onto current upstream.
- [ ] `references/Hexalith.EventStore` gitlink — point Timesheets at the integrated SDK commit as a separate owned change; report remote reachability.
- [ ] `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` — send the same signed projection request on both ports and assert public 404/internal persistence.
- [ ] `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md` — run and record final SDK, source-reference build, two HTTP classes, four scratch-root cases and six test lanes; label preliminary captures.
- [ ] `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, `README.md`, and `_bmad-output/implementation-artifacts/sprint-status.yaml` — date the old 401 claims, close seven patch records, update file/pointer inventory and current status.
- [ ] `docs/launch-readiness.md` and `_bmad-output/implementation-artifacts/deferred-work.md` — align waivers, live blocker ownership, ledger key and management identity references; retain dated history.

**Acceptance Criteria:**
- Given a GET-only canonical route with a denying fallback, when SDK startup inventory runs, then it accepts the route; given anonymous metadata or a public marker on that route, then startup fails.
- Given the same authenticated projection request, when sent to the public and internal ports, then the public port refuses it with 404 and the internal port persists the projection read model.
- Given final verification, when current records are reviewed, then claims, commands, gitlink ownership, waivers and live limitations agree without implying Story 3.6 completion.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` — restore source references.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` — zero warnings/errors.
- Run the SDK DomainService test executable and each `tests/<Project>/bin/Debug/net10.0/<Project>` executable separately; record selections and counts.
