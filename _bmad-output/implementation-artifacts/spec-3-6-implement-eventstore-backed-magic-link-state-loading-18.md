---
title: 'Close current Story 3.6 public-route review patches'
type: 'bugfix'
created: '2026-10-09'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'a59575113470d37d8dac4086dda6509562790e46'
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
- [x] `references/Hexalith.EventStore` gitlink — point Timesheets at the integrated SDK commit as a separate owned change; report remote reachability.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` — send the same signed projection request on both ports and assert public 404/internal persistence.
- [x] `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md` — run and record final SDK, source-reference build, two HTTP classes, four scratch-root cases and six test lanes; label preliminary captures.
- [ ] `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, `README.md`, and `_bmad-output/implementation-artifacts/sprint-status.yaml` — date the old 401 claims, close seven patch records, update file/pointer inventory and current status.
- [x] `docs/launch-readiness.md` and `_bmad-output/implementation-artifacts/deferred-work.md` — align waivers, live blocker ownership, ledger key and management identity references; retain dated history.

**Acceptance Criteria:**
- Given a GET-only canonical route with a denying fallback, when SDK startup inventory runs, then it accepts the route; given anonymous metadata or a public marker on that route, then startup fails.
- Given the same authenticated projection request, when sent to the public and internal ports, then the public port refuses it with 404 and the internal port persists the projection read model.
- Given final verification, when current records are reviewed, then claims, commands, gitlink ownership, waivers and live limitations agree without implying Story 3.6 completion.

## Implementation Notes

The SDK GET-only inventory fix and three positive/negative cases were first committed by an external `/pushall` run as `dc140b0f26bec0ad9b523c16cbcd1201ac14bb1c` while implementation was in progress. A second external `/pushall` commit, `2a3c21b99e83e7fd64ab4cd79cac0318cfdbc8b3`, captured the Timesheets pointer together with unrelated gitlinks and content. These commits were preserved. A diagnostics fixture correction was committed separately as `0b1b1455ca9afe93217ee4deaf3b36b67d973697` after the SDK class run exposed its stale domain list.

To integrate without rewriting externally created main history, `fix/story-3-6-public-routes` was created from the locally available EventStore `origin/main` (`f463442cca19e4199982a23a08bae4a490767d4a`). The SDK public-route, inventory, and diagnostics changes were applied there and committed as `bd976eff37e80fa404ee47d4031ecf6c2545b541` after commitlint validation. Timesheets commit `298899bfbdaeaa6df7528d32309b826f5b63f95d` changes only the EventStore gitlink to that integrated commit and was validated before commit. This differs from the recorded rebase decision but preserves the same upstream base and the external commits. No local remote-tracking branch contains the integrated commit. No fetch or push was performed in this run; the pointer remains unproven for a fresh clone until EventStore publishes it.

The public/internal port test reuses the exact signed assertion and projection dispatch on both ports. The SDK inventory accepts a GET-only canonical route under a denying fallback and rejects anonymous/public-marker variants. The two HTTP classes and scratch-root privacy cases pass. The Story 3.6 review record marks six of seven patches closed; the first remains open for upstream publication and the recorded rebase decision. Live-topology and durable-write acceptance also remain open.

## Spec Change Log

- 2026-10-09: Recorded local upstream integration, separate Timesheets gitlink ownership, final verification, and the remaining remote-reachability limit without changing the frozen intent.

## Review Triage Log

## Verification

Integrated SDK focused selection: 19/19; `EventStoreDomainServiceExtensionsTests` class: 78/78. The full SDK DomainService executable ran 546 tests, with 545 pass and one guardrail failure because the subject nested Tenants submodule is absent; repository policy forbids initializing it. Timesheets source-reference restore/build passed with zero warnings/errors. Two HTTP classes: 482/482. Scratch-root privacy: 4/4. Six Timesheets lanes: 1415 total, 1410 passed, one existing Aspire package-verdict failure, four intentional skips. Exact commands and captures are in [the test summary](tests/3-6-test-summary.md).

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` — restore source references.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` — zero warnings/errors.
- Run the SDK DomainService test executable and each `tests/<Project>/bin/Debug/net10.0/<Project>` executable separately; record selections and counts.
