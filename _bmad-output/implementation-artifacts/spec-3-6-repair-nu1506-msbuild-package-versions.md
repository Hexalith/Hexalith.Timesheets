---
title: 'Repair Story 3.6 attempt 7 duplicate MSBuild package versions'
type: 'bugfix'
created: '2026-11-01'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Repair the NU1506 failure retained in Story 3.6 live-proof attempt 7. Trace evaluated imports and PackageVersion definitions, then make the smallest durable change in the owning build configuration. Preserve Microsoft.Build and Microsoft.Build.Framework 18.9.6, Microsoft.Build.Locator 1.11.2, explicit caller overrides, Debug/source and Release/package separation, all user changes, and the supplied Works checkout's read-only status. Do not suppress validation or update dependencies.

Run the exact root AGENTS.md Debug/source solution restore and build with DOTNET_CLI_HOME=/tmp/dotnet-cli-home. Retain exact commands, UTC timing, exits, and sanitized diagnostics, plus focused evaluation checks justified by this repair. Link the separate repair evidence from the Story 3.6 record and test summary. Preserve attempt 7 and every earlier receipt unchanged. Do not start Aspire, inspect runtime availability, invoke the isolated runner, touch stores, or change AppHost, ServiceDefaults, or topology. Leave changes uncommitted. Story 3.6 stays in-progress and release FAIL while deployed acceptance remains unfinished; build verification grants no runtime qualification.

</frozen-after-approval>

## Implementation Notes

- No unresolved intent gaps or irreversible actions. Small configuration repair plus evidence and append-only story/test-summary updates; proceed under the user's explicit repair authorization with the existing dirty documents preserved.
- Evaluated Works.Contracts items each originate once in `references/Hexalith.Builds/Props/Directory.Packages.props` and once in Works' own `Directory.Packages.props`. The latter imports the former before its unconditional declarations. The shared catalog is the writable owner; Works remains untouched. Preserve shared entries for other consumers and defer to Works' declarations in both modes.
- Keep root version forwarding and source/package guards unchanged. Verify item uniqueness, defining paths and exact pins for Works.Contracts and its architecture-test consumer in both modes, non-Works catalog coverage, and explicit EventStore/Works-root overrides. Capture the exact solution commands independently of historical live-proof receipts.

- Final implementation identifies the normal Works package-wrapper owner by its adjacent solution marker and compares a normalized selected wrapper path. Independent review exposed equivalent-path and arbitrary-project-name cases; both are now covered without changing Works. Remove this compatibility deferral when the Works owner migrates its three local declarations to the shared catalog.
- Added `references/Hexalith.Builds/Tools/test_works_package_versions.py` and its command in `Tools/README.md`. Nine temporary-wrapper subcases pass, covering both modes, renamed checkouts, arbitrary names, equivalent paths, alternate catalogs, non-Works defaults and an explicit EventStore version. No restore or application startup occurs in this regression.
- Final required Debug/source solution restore and warning-as-error build pass with the exact authorized arguments and environment prefix. Fifteen final actual-project evaluations and the owning catalog/consumer/exception validators pass. Configuration-free Release/package selection and evaluated override package versions were also checked. [Repair evidence](evidence/3-6/20261101-nu1506-repair/README.md) retains commands, UTC intervals, exits, sanitized diagnostics and earlier repair diagnostics.
- Story and sprint status remain in-progress; release remains FAIL. No runtime qualification is claimed. The user explicitly required leaving the repair uncommitted; no commit or gitlink update was performed.

## Review Triage Log

| Finding | Verdict and disposition | Evidence |
|---|---|---|
| Equivalent wrapper paths bypassed ownership | medium, patched | Normalize the selected path; `/./` and `/../` actual-project and synthetic probes pass. |
| Non-prefixed projects under Works inherited duplicates | medium, patched | Determine the wrapper's module owner instead of project naming; both unchanged historical probe projects and synthetic Consumer pass. |
| Works-root probe repeated the default | low, patched | Final distinct `/references/./Hexalith.Works` spelling is retained in both property and edge. |
| Retained harness requires private temporary setup | low, patched | Packet README documents reconstruction with fresh private/output directories; the durable regression needs no capture state. |
| Owning catalog lacked a durable regression | medium, patched | New Builds regression executes evaluated synthetic wrappers, not textual condition matching. Nine subcases pass. |
| Configuration-free CI graph was not probed | low, patched | Focused evaluation selects Release/package with audit enabled and Works package edges. |
| Evaluations do not run contradictory-mode validation targets | false, rejected | The repair changes only three package-item conditions and private ownership helpers. Existing mode validation targets and root forwarding are byte-unchanged; the required build executes the valid source-mode gates. No validation rule was removed or weakened. An additional negative-mode lane is outside this narrow collision repair. |
| Correct override properties could hide stale package-item versions | low, patched | Final evidence assertions and durable regression require evaluated EventStore item versions to match the explicit override. |
| Preprocess receipts did not bind XML output | medium, patched | Import-output provenance retains original XML sizes/hashes and excerpt hashes for before, intermediate and final traces. |
| Repair spec lacked incoming story/summary links | low, patched | Both appended records and the packet README link this spec directly. |
