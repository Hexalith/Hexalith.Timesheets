---
title: 'Close current Story 3.6 public-route review patches'
type: 'bugfix'
created: '2026-10-09'
status: 'done'
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
- [x] `references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceEndpointInventory.cs` and `references/Hexalith.EventStore/tests/Hexalith.EventStore.DomainService.Tests/EventStoreDomainServiceExtensionsTests.cs` — repair the GET-only rule, add positive and negative startup cases, then rebase the owned SDK commit onto current upstream.
- [x] `references/Hexalith.EventStore` gitlink — point Timesheets at the integrated SDK commit as a separate owned change; report remote reachability.
- [x] `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` — send the same signed projection request on both ports and assert public 404/internal persistence.
- [x] `_bmad-output/implementation-artifacts/tests/3-6-test-summary.md` — run and record final SDK, source-reference build, two HTTP classes, four scratch-root cases and six test lanes; label preliminary captures.
- [x] `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, `README.md`, and `_bmad-output/implementation-artifacts/sprint-status.yaml` — date the old 401 claims, close seven patch records, update file/pointer inventory and current status.
- [x] `docs/launch-readiness.md` and `_bmad-output/implementation-artifacts/deferred-work.md` — align waivers, live blocker ownership, ledger key and management identity references; retain dated history.

**Acceptance Criteria:**
- Given a GET-only canonical route with a denying fallback, when SDK startup inventory runs, then it accepts the route; given anonymous metadata or a public marker on that route, then startup fails.
- Given the same authenticated projection request, when sent to the public and internal ports, then the public port refuses it with 404 and the internal port persists the projection read model.
- Given final verification, when current records are reviewed, then claims, commands, gitlink ownership, waivers and live limitations agree without implying Story 3.6 completion.

## Implementation Notes

The SDK GET-only inventory fix and three positive/negative cases were first committed by an external `/pushall` run as `dc140b0f26bec0ad9b523c16cbcd1201ac14bb1c` while implementation was in progress. A second external `/pushall` commit, `2a3c21b99e83e7fd64ab4cd79cac0318cfdbc8b3`, captured the Timesheets pointer together with unrelated gitlinks and content. These commits were preserved. A diagnostics fixture correction was committed separately as `0b1b1455ca9afe93217ee4deaf3b36b67d973697` after the SDK class run exposed its stale domain list.

To integrate without rewriting externally created main history, `fix/story-3-6-public-routes` was created from the locally available EventStore `origin/main` (`f463442cca19e4199982a23a08bae4a490767d4a`). The SDK public-route, inventory, and diagnostics changes were applied there and committed as `bd976eff37e80fa404ee47d4031ecf6c2545b541` after commitlint validation. Timesheets commit `298899bfbdaeaa6df7528d32309b826f5b63f95d` changes only the EventStore gitlink to that integrated commit and was validated before commit. This differs from the recorded rebase decision but preserves the same upstream base and the external commits. No local remote-tracking branch contains the integrated commit. No fetch or push was performed in this run; the pointer remains unproven for a fresh clone until EventStore publishes it.

The public/internal port test reuses the exact signed assertion and projection dispatch on both ports. The SDK inventory accepts a GET-only canonical route under a denying fallback and rejects anonymous/public-marker variants. The two HTTP classes and scratch-root privacy cases pass. The Story 3.6 review record marks six of seven patches closed; the first remains open for upstream publication and the recorded rebase decision. Live-topology and durable-write acceptance also remain open.

**Upstream reconciliation (2026-10-09).** EventStore `fe4e7559f17ed53eea005b0ae5a4f6a358221780` now contains the integrated SDK changes. All five owned SDK files are byte-identical to local `bd976eff37e80fa404ee47d4031ecf6c2545b541`, and local `origin/main` contains `fe4e7559`. Timesheets `46265ba71138617ac387bd68e63aa0d7e5685947` points its EventStore gitlink there. That later commit also changes other files and gitlinks, so it is not the separate gitlink-only commit required by the original plan; `298899bfbdaeaa6df7528d32309b826f5b63f95d` remains the earlier separate owned gitlink commit. The upstream integration closes the remaining functional and reachability patch without rewriting existing history. Local remote-tracking reachability was checked without a new fetch or push. Current verification on the pinned pointer retains the one SDK nested-Tenants guardrail failure and one Timesheets Aspire package-verdict failure; Story 3.6 and release remain open.

**Post-review guard (2026-10-09).** Review found that a GET-only canonical route with explicit weak policy metadata could suppress the denying fallback. EventStore `f87affaefef5693fd80452eea02999e46fa1888b` now requires the catalog policy whenever explicit authorization metadata exists; a new three-case theory covers default, wrong, and correct policies. Timesheets `61dc18bdb3e28ed8bbee64488954b8f42b90c4e7` changes only the EventStore gitlink. Both commit messages were validated with commitlint; neither commit was pushed. EventStore `origin/main` still points to `fe4e7559`, so the current pointer is locally valid but not proven fetchable by a fresh clone. The earlier upstream reconciliation describes the prior pointer. Live topology and durable-write acceptance remain open.

## Spec Change Log

- 2026-10-09: Applied the review-found GET-only authorization guard in EventStore, pinned it in a separate Timesheets gitlink commit, strengthened launch-readiness exit criteria and story-status assertions, and recorded the new pointer's publication limit.
- 2026-10-09: Reconciled the later upstream EventStore integration and Timesheets pointer, closed the seven accepted review patches, and reran verification on the pinned upstream commit. Retained the separate-commit deviation and live acceptance limits explicitly.
- 2026-10-09: Recorded local upstream integration, separate Timesheets gitlink ownership, final verification, and the remaining remote-reachability limit without changing the frozen intent.

## Review Triage Log

- **high · patch · blind-hunter · EventStoreDomainServiceEndpointInventory.cs:79:** A GET-only canonical endpoint with a weak explicit policy passes because any authorization metadata suffices, and that metadata suppresses the fallback at runtime. Require the canonical policy whenever such metadata is present; keep the denying-fallback-only GET case valid.
- **false · rejected · blind-hunter · EventStoreDomainServiceExtensionsTests.cs:1056:** The test invokes the same `Validate` method that `EventStoreDomainServiceSecurityStartupValidator.StartingAsync` calls; separate tests verify the SDK fallback registration. A second host-start test would add coverage, but the claimed startup behavior is not untested at the decision point.
- **false · rejected · blind-hunter · MagicLinkConfirmationHttpBoundaryTests.cs:426:** `InternalSurfaceGuardTests.Domain_service_routes_are_refused_when_no_internal_port_is_configured` still sends unauthenticated POSTs to `/project/v2` and `/PROJECT/v2` and asserts 404. The changed test specifically proves the credentialed request is also refused.
- **false · rejected · blind-hunter · Builds gitlink:** The verification claims a source-reference graph, while the release record remains `FAIL` and package currency remains `CONCERNS`; it does not claim that the package-backed graph is release validated.
- **false · rejected · blind-hunter · Story 3.6 pointer inventory:** The inventory identifies EventStore moves owned by Story 3.6 and explicitly notes that `46265ba` mixed those with other content and gitlinks. The other pointers are separately committed workspace changes and are not claimed as Story 3.6 deliverables.
- **false · rejected · blind-hunter · launch-readiness.md:14:** “This Story 3.6 increment changed no dependencies or submodule pointers” is part of the dated 2026-10-03 catalog observation, before the later pointer changes. The current pinned-pointer section names the later EventStore move.
- **low · rejected · blind-hunter · spec-18 task 1:** The task verb says “rebase,” while the implementation notes disclose that the SDK changes were reapplied on upstream instead. The upstream result is present; changing this build's spec task wording is excluded by review routing.
- **low · rejected · blind-hunter · spec-18 task 2:** The final pointer move was mixed, while an earlier owned pointer move was separate. The spec's upstream reconciliation records that deviation; changing this build's spec task wording is excluded by review routing.
- **false · rejected · blind-hunter · spec-18 upstream reconciliation:** The claim is limited to local `origin/main` containment and explicitly says no fresh fetch or push was performed. It does not assert a fresh remote clone was tested.
- **medium · patch · blind-hunter · launch-readiness.md:56:** The no-disclosure row omits the known header scan and cache/referrer gaps from its risk and completion cells, so a reader could close the waiver after logging evidence alone. Add both gaps and their exit conditions to that row.
- **medium · defer · blind-hunter · AGENTS.md:72,105:** The managed agent context still says launch posture is `CONCERNS` and the token-hash projection lacks host wiring, which can misdirect future work. Refreshing agent-context files is deferred under this review's routing rule.
- **low · patch · edge-case-hunter · LaunchReadinessTests.cs:61,124:** Both assertions accept a record containing the current in-progress wording plus obsolete “Story 3.6 is in review” wording. Add a direct negative assertion in each section.
- **low · rejected · edge-case-hunter · spec-18 task 1:** The checked task uses “rebase” even though the notes document reapplication on upstream. This duplicates the verified spec-wording issue above, and editing this build's spec task is excluded by review routing.

## Verification

Post-review local pointer `f87affaefef5693fd80452eea02999e46fa1888b`: SDK build zero warnings/errors, focused inventory/public-route selection 22/22, ExtensionsTests 81/81, full SDK 548/549 with the same absent nested-Tenants guardrail failure. Timesheets source-reference restore/build passed with zero warnings/errors; HTTP classes 482/482; scratch-root privacy 4/4; six lanes 1415 total, 1410 passed, one existing Aspire package-verdict failure, four skips. Exact commands and captures are in [the test summary](tests/3-6-test-summary.md). EventStore publication is still required before the current gitlink is fetchable in a fresh clone.

Prior upstream-pointer verification:

On prior EventStore pointer `fe4e7559f17ed53eea005b0ae5a4f6a358221780`: SDK build zero warnings/errors, focused selection 19/19, ExtensionsTests 78/78, full SDK 545/546 with the same absent nested-Tenants guardrail failure. Timesheets source-reference restore/build passed with zero warnings/errors; HTTP classes 482/482; scratch-root privacy 4/4; six lanes 1415 total, 1410 passed, one existing Aspire package-verdict failure, four skips. Exact commands and captures are in [the test summary](tests/3-6-test-summary.md).

Prior local-integration verification:

Integrated SDK focused selection: 19/19; `EventStoreDomainServiceExtensionsTests` class: 78/78. The full SDK DomainService executable ran 546 tests, with 545 pass and one guardrail failure because the subject nested Tenants submodule is absent; repository policy forbids initializing it. Timesheets source-reference restore/build passed with zero warnings/errors. Two HTTP classes: 482/482. Scratch-root privacy: 4/4. Six Timesheets lanes: 1415 total, 1410 passed, one existing Aspire package-verdict failure, four intentional skips. Exact commands and captures are in [the test summary](tests/3-6-test-summary.md).

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` — restore source references.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` — zero warnings/errors.
- Run the SDK DomainService test executable and each `tests/<Project>/bin/Debug/net10.0/<Project>` executable separately; record selections and counts.
