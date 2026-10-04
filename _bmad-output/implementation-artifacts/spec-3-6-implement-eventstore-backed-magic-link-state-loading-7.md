---
title: 'Close Story 3.6 build guidance and verification evidence patches'
type: 'chore'
created: '2026-10-04'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The latest Story 3.6 review leaves six accepted patches open. Contributor build instructions still invoke the known failing bare solution build; configured-port tests share the production section constant and cannot detect configuration drift; cumulative coverage, administrator test limitations, the settled confirmation-availability decision, and privacy verification provenance are incomplete or stale.

**Approach:** Apply the recorded Build decision (a): retain PASS for the verified source-reference Debug build, publish its matching restore/build commands, and ledger the bare-build failure with ownership, root cause, and a narrow configuration-propagation follow-up. Bind the HTTP fixture with the literal `Timesheets:InternalSurface:Port` key and add a fitness assertion tying the AppHost's `Timesheets__InternalSurface__Port` value to `InternalPort` and the host options section. Restore cumulative test coverage and explicitly state that injected claims and substituted authorization prove accessor mapping only. Annotate the existing availability ledger entry with the implemented confirm-as-is decision without closing it. Refresh privacy evidence and reconcile only the six latest review checkboxes and this increment's tracking. Preserve current production behavior, EventStore persistence, fail-closed defaults, existing deferred ownership, dependencies, topology, and submodule pointers; Story 3.6 remains unfinished beyond this review increment.

</frozen-after-approval>

## Implementation Notes

- Investigation found no unresolved intent choice or irreversible action. The six latest patches form one small review-closure increment; the 2026-10-03 decisions are already recorded. Worktree was clean on `main`, with canonical baseline `57770d380c724c94233b78ac021a8f10cd5d6f39`.
- SDK selection is `10.0.401`. Reuse the existing configured-port HTTP cases and persisted read-model assertions. Add the source fitness assertion in `MagicLinkConfirmationCapabilityEndpointTests.cs`, which already references the host; match complete whitespace-tolerant AppHost invocations so changing the exported port to `PublicPort` is detected.
- Evidence files: `README.md`, `docs/launch-readiness.md`, `deferred-work.md`, `tests/3-6-test-summary.md`, the canonical story and sprint status. The bare-build cause is independently visible in Works dependency-mode validation and SDK `ShouldUnsetParentConfigurationAndPlatform` behavior. Ledger a per-reference `Configuration=Debug` propagation fix first, with `ShouldUnsetParentConfigurationAndPlatform=false` as a fallback requiring verification; do not implement build configuration changes here.
- Required pre-edit runtime check: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home aspire start --no-build --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive --format Json` started the AppHost; `aspire wait timesheets --timeout 30 --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive` exited 18. Describe reported Timesheets Finished, exit 134, and security Healthy; resource logs identify `System.IO.IOException: Failed to bind to address http://127.0.0.1:8080: address already in use`. `aspire stop` succeeded. This does not establish the cause of the historical October 3 attempt.

- Implemented all six latest patches. The ledger proposes preserving `Configuration=$(Configuration)` per reference, rather than hard-pinning Debug, and explicitly requires validating Release package mode before accepting a candidate build fix. Existing confirmation-availability evidence is retained as historical with an implementation annotation, not closed.
- Matching README restore and full solution Debug build passed with zero warnings/errors. All six individual direct xUnit v3 lanes passed: ArchitectureTests 55; Contracts.Tests 90; IntegrationTests 110 total / 106 pass / four existing skips; Projections.Tests 146; Server.Tests 478; Works.Tests 76. Total 955 / 951 pass / zero failures; performance lanes remained disabled. Evidence records now cite the October 4 worktree baseline.
- The new configuration fitness assertion also discards commented-out source wiring before matching complete invocations, so a disabled export cannot satisfy it. The cumulative story File List and six latest checkboxes are reconciled; historical tracking drift and production persistence/authentication gaps remain separately deferred.

- Final changed lanes were rerun after refreshing evidence and comment handling: ArchitectureTests 55/55 and the two affected HTTP/endpoint classes 24/24 passed. Mutation checks against the built source fitness test rejected a renamed environment key, exporting PublicPort, and commenting out the export (each one expected failure), then passed on the restored unchanged AppHost source.

- Independent review exposed the README test-runner incompatibility: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.Works.Tests/Hexalith.Timesheets.Works.Tests.csproj --no-build` exited 1 with `Microsoft.Testing.Platform.MSBuild.targets(355,5): Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later.` README now presents the verified direct executables as the default, including the opt-in performance commands, without changing runner configuration.
- Confirmed the reviewer’s three source-fitness holes by mutation: moving the export to another resource, placing it under `#if false`, and assigning InternalPort = PublicPort all passed the initial assertion. The test now scopes both listeners/export to the Timesheets resource chain, rejects conditional AppHost wiring, and pins the listener values used by the HTTP fixture. Final sprint/story tracking now agrees on review.

## Review Triage Log

- **medium / patch:** README runner guidance exposed an everyday contributor failure after the corrected build. Reproduced the Works test command exit 1 on SDK 10.0.401; promoted the already verified direct xUnit v3 commands and documented the actual unsupported-VSTest-target error. No runner/dependency changes.
- **medium / patch:** Independent whole-file listener/export matches allowed the export to move to another resource. The mutation passed before remediation; the test now matches both listeners and the export inside the Timesheets AddProject chain.
- **low / patch:** Comment removal did not reject inactive preprocessor wiring. The `#if false` export mutation passed before remediation; a small assertion now requires unconditional AppHost wiring, without a new syntax-parser dependency.
- **medium / patch:** The port name assertions allowed both listeners to use the same value while HTTP fixtures retained separate ports. The shared-port mutation passed before remediation; source assertions now pin 8080/8081, matching the executable HTTP evidence.
- **low / patch:** The sprint timestamp comment still described the latest six patches as open while review was pending. Finalization now updates that comment and synchronizes canonical story/sprint review status; historical deferred work stays unfinished.

- Final review patches passed the full solution Debug build (zero warnings/errors), ArchitectureTests 55/55, and IntegrationTests 110 total / 106 pass / four existing skips. The other four previously passing lanes were unchanged; final inventory remains 955 total / 951 pass / zero failures. All six configuration mutations failed as intended and the restored AppHost source passed. All five independent-review findings were patched with no new review deferrals. Conventional commit message passed the shared Builds commitlint configuration; story and sprint remain review, and release readiness remains FAIL for existing unfinished work.
