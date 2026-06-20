---
project: timesheets
phase: 2
phaseName: External-party surface
date: 2026-06-20
owner: John
status: continuation-ready
sourceArtifacts:
  - _bmad-output/planning-artifacts/prds/prd-timesheets-2026-06-18/prd.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - _bmad-output/implementation-artifacts/epic-3-retro-2026-06-19.md
---

# Phase 2 Continuation: External-party Surface

## Phase Definition

Phase 2 in the Timesheets PRD is the external-party surface:

- API-only external submission.
- Magic-Link Confirmation.
- Security and audit gates for external confirmation and adjustment.

This maps to Epic 3, "External Contributor Confirmation," and stories 3.1 through 3.5.

## Current State

Phase 2 implementation artifacts already show all Epic 3 stories as `done`:

- 3.1 Expose External Contributor Confirmation API.
- 3.2 Issue Scoped Magic-Link Confirmation Capabilities.
- 3.3 Confirm Time Through Magic Link.
- 3.4 Adjust Time Through Magic Link.
- 3.5 Reject Invalid Confirmation Links Without Resource Disclosure.

The Epic 3 retrospective is complete and confirms the delivered external contributor capability set.

## Product Readiness Position

Phase 2 can be treated as story-complete, but not launch-complete.

The delivered product position is:

- External submissions reuse the same Time Entry workflow as internal submissions.
- Confirmation is contributor evidence, not approval.
- Magic-link capabilities are scoped, single-use, expiring, and token-hash based.
- Invalid-link behavior is no-disclosure at the service boundary.
- FrontComposer metadata and OpenAPI artifacts were extended without creating a full external portal.

## Launch-readiness Caveats

The following caveats should not reopen Phase 2 stories, but they should be carried as launch-readiness work:

1. Implement a concrete EventStore-backed `IMagicLinkConfirmationCapabilityStateLoader`.
2. Add HTTP-boundary tests proving equivalent no-disclosure responses for invalid magic-link states.
3. Wire `MagicLinkInvalidLinkOutcomeCategory` into correlation-safe diagnostics or audit once abuse detection/rate limiting is in scope.
4. Decide whether Phase 4 ledger/export rows include external source and magic-link evidence metadata beyond the FR18 minimum.
5. Keep external confirmation out of approval, locking, ledger eligibility, and finance export eligibility unless a later approved story changes that rule.

## Recommended Continuation

Proceed with Phase 2 launch-readiness before claiming production external confirmation:

1. Create or promote a launch-readiness story for the EventStore-backed magic-link state loader.
2. Follow with HTTP-boundary no-disclosure tests for confirm and adjust routes.
3. Update release/readiness notes to distinguish story-complete from launch-complete.

No sprint-status transition is needed in the current workspace because Epic 3 and all Phase 2 story keys are already marked `done`.
