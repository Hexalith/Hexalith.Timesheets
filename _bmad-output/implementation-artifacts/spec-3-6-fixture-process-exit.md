---
title: 'Recognize exited processes during Story 3.6 fixture cleanup'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
---

<frozen-after-approval reason="human-owned intent">

## Intent

The isolated magic-link runner retains its infrastructure while a captured process still appears active. Linux keeps an exited process in `/proc` until its parent reaps it, causing a matching zombie to block cleanup. Record that exit from the kernel state while continuing to retain the fixture for live processes or unverified shutdown. This increment addresses local fixture cleanup. Story 3.6 still requires the deployed Platform handoff and acceptance evidence.

</frozen-after-approval>

## Implementation Notes

- `recorded_process_exits` reads the current kernel state, thread count, and start time together. A matching zombie with no surviving threads is recorded as exited with `observed_state: Z`. A matching live identity or surviving worker still blocks cleanup. Malformed observations refuse cleanup.
- The regression waits for a real child to exit without reaping it, then checks that the runner removes the simulated fixture's owned resources. The existing live-process regression checks that the runner retains them.
- The November 22 run observed three zombie PIDs after finalization. Their state at the original cleanup check is unknown. The run remains unqualified, and the metadata HTTP 500 has no established cause.
- Platform `b975a77e5f37c763f332e90e16b82e45c6bc2ba6` still records `BLOCKED_NOT_FOR_EXECUTION`. GitHub deployment metadata identifies the same package-publication records listed in the [qualification handoff](../../docs/deployed-magic-link-qualification-handoff.md). Story 3.6 remains in progress pending a digest-bound installed release, approved provider authority, certified history and continuity, and deployed delivery and rebuild evidence.

## Review Triage Log

- High, fixed. A zombie thread-group leader can outlive active workers. Require a thread count of one and exercise the multi-threaded case.
- Medium, fixed. Linux task names can contain non-UTF-8 bytes. Parse the stat record as bytes and exercise a non-UTF-8 name.
- Medium, fixed. An incorrect PID header or unknown state must fail inspection before the start time is used as exit evidence. Both cases now have regression coverage.

## Verification

- Debug/source restore and warning-as-error solution build passed on Timesheets base `85d15b5337622d21e10a838451fce5738a3f218f` with the refreshed reference checkouts.
- `python3 -m unittest discover -s scripts -p 'test_*.py'` passed 306 tests on rerun. The preceding configured run failed an existing scheduler alarm timing assertion. The new cleanup regression also reproduced the failure with the original exit check.
- `python3 scripts/check_magic_link_apphost_guards.py --configuration-only` passed 64 compiled probes. `npm run test:release-tooling` passed 30 tests.
- `python3 scripts/run_isolated_magic_link_dapr.py --route-catalog --public-port 18080` stopped at `dapr --version`. The environment has no Dapr CLI or `dapr_redis` container. The run started no fixture resources.
