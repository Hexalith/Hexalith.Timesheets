---
title: 'Close five deferred local fixture safety handoffs'
type: 'bugfix'
created: '2026-10-21'
status: 'in-progress'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Close five deferred local-fixture safety items: incomplete fixture startup admission, recording submit-response loss, routine compiled configuration probes, monitoring credential redaction, and the EventStore restart timeout. Revalidate the four delivered implementations and add the missing routine CI guard lane. Preserve the Debug/source local journey and Release/package CI policy. Run compiled probes without creating resources, using the correct build's expected behavior, and retain failure evidence. Resolve only these five original ledger entries after verification, then commit and push to main as requested. Local configuration checks do not establish current-graph runtime, deployed acceptance, or release readiness.

</frozen-after-approval>

## Implementation Notes

- Investigation baseline: clean main at `87350775d56d7dceef9943630b9acf9c7b9cae53`. The user requested five deferred items together and publication to main. No unresolved product choice, data migration, new runtime API or dependency update is required. The small implementation touches the existing Python guard driver/test module, CI workflow, verification documentation, this spec and five ledger annotations; use the oneshot route.
- The driver `scripts/check_magic_link_apphost_guards.py` hardcodes Debug and expects its source-only compiled launch adapter to succeed. Add an explicit Debug/Release selection, defaulting to Debug. Release is allowed only for configuration-only probes; compiled-launch cases must exercise the existing package-build refusal while ordinary/default and unsafe fixture probes retain their current outcomes. Select the matching executable and child environment without modifying the caller environment. Keep every probe's command-line validation-only setting.
- Add a separate blocking CI job in `.github/workflows/ci.yml`: root checkout, initialize only the declared Builds submodule, use the already-pinned setup-dotnet action/global.json, restore/build the AppHost in Release/package mode, run configuration-only probes with explicit Release, and retain sanitized evidence even on failure. Reuse existing action pins. No live infrastructure, Dapr/Aspire CLI installation, topology change, publish or release trigger is needed.
- Four handoffs already delivered: pre-resource fixture/control-plane admission (`Program.cs`); `TimeEntryRecordSubmissionService` response-loss status/readback recovery with actor-backed HTTP persistence cases; structured/plain telemetry/API-key sanitizer regression coverage; and the native EventStore launch/restart fix with dated October 18 persisted journey receipts. Revalidate current available tests without turning historical runtime proof into current runtime qualification.
- Exact ledger summaries: “Direct AppHost starts with bare TIMESHEETS_LIVE_DAPR_FIXTURE=1 enable development fixture authorization while retaining shared component defaults”; “Recover a committed external recording when its submit response is lost”; “Put compiled AppHost fixture-control refusal probes in a routine verification lane”; “Extend the local diagnostic sanitizer to redact OTLP/API-key headers before retaining or displaying monitoring output”; “Diagnose the EventStore process-stop timeout that prevents the isolated journey's restart assertion”. Preserve original evidence and separate deployment/restart qualification follow-ups.
- Verify Python guard/runner/sanitizer regressions and routine tooling discovery; restore/build matching Debug/source solution with warnings as errors and run the six default suites individually. Run real no-resource Debug guards, and verify Release selection/refusals through focused driver regressions and the new CI lane. Local policy prohibits producing Release assets. Record exact results and limits in `tests/five-local-fixture-handoffs.md`. Keep status in-progress until review and verification finish.

- Implementation: added explicit guard configuration and package-build refusal expectations; all 35 Debug probes retain their behavior. Release refuses infrastructure mode before fixture creation. The new blocking CI job initializes only Builds, builds the Release/package AppHost with warnings as errors, runs the no-resource guard driver, and retains sanitized receipts. The existing Python CI job now also executes the fixture/sanitizer regression module. README and CI/CD guidance document both modes and their evidence limits.
- Verification so far: matching Debug/source restore/build passed (zero warnings/errors), fixture Python 97/97, routine tooling Python 88/88, real compiled Debug guards 35/35 and actionlint/diff checks passed. The six individual default suites are running; actual Release execution belongs to hosted CI. No local Release build or live topology was started.
- The configured context-free Blind Hunter review found no confirmed additional defects after checking code, workflow, tests and scope claims. Its independent Python execution also passed 97/97. No additional review layers are configured, and no review deferral was added.
