---
title: 'Close Story 3.6 loader policy and evidence gaps'
type: 'bugfix'
created: '2026-09-16'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.6 still permits inconsistent capability/Time Entry Activity Type evidence, and its command service accepts project-owned Activity Types although the production magic-link catalog is tenant-only. Several fail-closed and stream-integrity guards lack falsifying tests, early-exit I/O claims are incomplete, and the accepted timing risk is absent from launch readiness.

**Approach:** Enforce the resolved tenant-owned Activity Type boundary across loading, issuance, display, confirmation, and adjustment while preserving Project and Work targets. Complete loader integrity tests, no-disclosure I/O evidence, public-code conventions, and release-risk documentation without changing persistence, endpoint response shapes, or topology.

## Boundaries & Constraints

**Always:** Keep the token-hash index non-authoritative; fold capability and Time Entry authority from EventStore; require matching Activity Type IDs and a tenant-scoped Time Entry/catalog item; preserve opaque external denials, genuine cancellation, and successful Project/Work targets using tenant-owned Activity Types. Treat existing project-scoped links as fail-closed until operators inventory and reissue them.

**Never:** Add dummy reads, response padding, latency thresholds, a project-catalog writer, schema/data migration, direct Dapr state access, new topology, UI, package upgrades, sibling-submodule edits, or changes to deferred platform/agent-context work.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Supported target | Project or Work target with one tenant-owned Activity Type | Issue, describe, confirm, and adjust retain existing valid behavior | No new disclosure or persistence path |
| Disallowed type | Project-owned Activity Type during issue/adjust or in existing folded state | Reject before token generation or dispatch; existing links return the canonical opaque denial | No capability-use or Time Entry event |
| Inconsistent bundle | Capability and Time Entry differ by Activity Type ID/scope or a later event has another tenant | Loader returns the unavailable bundle | Same external denial as unknown token |
| Read/integrity failure | Index read throws or page `EventCount` disagrees with events | Fail closed without EventStore command work | Caller cancellation still propagates |
| Early rejection | Blank or hash-derivation-rejected token | No read-model or EventStore I/O | Response remains envelope-equivalent; latency is not guaranteed constant |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs` -- validate bundle Activity Type identity/scope/catalog resolution; add XML docs; rename the JSON options field.
- `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkConfirmationCapabilityCommandService.cs` -- enforce tenant-owned Activity Types in issuance, display, confirmation, and adjustment without rejecting Project/Work targets.
- `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs` -- add throwing/counted read-model behavior, submitted-history and page-integrity proofs, full fold comparison, and PascalCase test names.
- `tests/Hexalith.Timesheets.Server.Tests/MagicLinkConfirmationCapabilityCommandServiceTests.cs` -- prove project-owned types reject before issue/adjust dispatch while Project targets with tenant types remain valid.
- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` -- prove an existing project-scoped link is denied identically on all four public routes with zero trusted command work.
- `docs/launch-readiness.md` and `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` -- record and pin the bounded timing guarantee, migration inventory concern, and approved revisit triggers.
- `_bmad-output/implementation-artifacts/3-4-adjust-time-through-magic-link.md` and `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md` -- append contract clarification and completion evidence; do not rewrite historical acceptance or review text.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs` -- enforce Activity Type bundle identity/scope/catalog resolution and apply the XML/naming conventions.
- [ ] `src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkConfirmationCapabilityCommandService.cs` -- reject non-tenant Activity Types in issue/display/confirm/adjust while retaining Project and Work targets.
- [ ] `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs` -- falsify every loader edge case, count read-model I/O, compare the complete submitted fold, and rename its tests to PascalCase.
- [ ] `tests/Hexalith.Timesheets.Server.Tests/MagicLinkConfirmationCapabilityCommandServiceTests.cs` and `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` -- prove tenant-only policy, opaque four-route denial, and zero dispatch.
- [ ] `docs/launch-readiness.md` and `tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs` -- record and pin the bounded timing and deployed-link inventory risks.
- [ ] `_bmad-output/implementation-artifacts/3-4-adjust-time-through-magic-link.md`, `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md`, and `_bmad-output/implementation-artifacts/sprint-status.yaml` -- append contract/evidence clarification and synchronize story tracking.

**Acceptance Criteria:**
- Given a Project or Work target with a tenant-owned Activity Type, when a valid magic link is issued or used, then the existing successful behavior remains available.
- Given a project-owned or mismatched Activity Type, when any magic-link path evaluates it, then no token, Time Entry change, or capability-use event is emitted and public routes return the canonical opaque denial.
- Given submitted/replayed history, index failure, wrong-tenant later events, or malformed page counts, when the loader folds state, then it deterministically succeeds only for complete consistent history and otherwise fails closed.
- Given blank or hash-derivation-rejected input, when the loader evaluates it, then it performs zero EventStore and read-model I/O while launch readiness states that v1 does not guarantee constant latency.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -m:1 /nr:false` -- restore succeeds with the SDK pinned by `global.json`.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror -m:1 /nr:false` -- succeeds with zero warnings and errors.
- Run each built xUnit v3 executable under `tests/*/bin/Debug/net10.0/` individually, including Works.Tests -- all non-skipped tests pass; only documented infrastructure/performance skips remain.
