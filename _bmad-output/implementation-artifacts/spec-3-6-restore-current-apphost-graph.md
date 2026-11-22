---
title: 'Restore the current AppHost graph for Story 3.6'
type: 'bugfix'
created: '2026-11-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '151fd37308eb08f942040c283209e7886ad954a6'
---

<frozen-after-approval reason="human-owned intent">

## Intent

Restore the Debug source-reference solution after updating the direct repositories to main. Use the shared Builds version of the Aspire Dapr integration so EventStore and its consumer resolve a compatible graph. Exercise the Story 3.6 checks and record the observed runtime and deployment boundaries in the launch register.

</frozen-after-approval>

## Implementation Notes

- The original restore failed with NU1109 in the AppHost. Both the committed Builds revision and the updated checkout select the version required by EventStore.
- Removed the Timesheets override. The restored Dapr hosting package is `17.0.0-preview.1.261120-0454`.
- The owner-history check exposed two reproducible loader failures. The terminal fold now verifies the Time Entry, contributor, permitted outcome and issued time window before changing state. A legacy revocation or expiry may omit the entry ID. The regression tests exercise both the capability stream and the Time Entry stream.

## Verification

```sh
dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
dotnet build Hexalith.Timesheets.slnx -c Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net11.0/Hexalith.Timesheets.Server.Tests -class '*EventStoreMagicLinkConfirmationCapabilityStateLoaderTests'
python3 scripts/check_magic_link_apphost_guards.py --configuration-only
TMPDIR=/var/tmp python3 scripts/run_isolated_magic_link_dapr.py --route-catalog --public-port 18080
```

The solution built with zero warnings and errors. The focused loader suite passed 157/157. All 64 compiled AppHost configuration, model and construction probes passed. The live invocation reached the configured catalog and writer, then failed at the metadata request. [Runtime observations and deployed gates](../../docs/launch-readiness.md#story-36-qualification-on-2026-11-22)

| Default test executable | Passed | Skipped |
| --- | ---: | ---: |
| ArchitectureTests | 95 | 0 |
| Contracts.Tests | 108 | 0 |
| Server.Tests | 750 | 0 |
| Projections.Tests | 792 | 0 |
| IntegrationTests | 1,057 | 5 |
| Works.Tests | 76 | 0 |
| **Total** | **2,878** | **5** |

All six executables finished with zero failures. The separate opt-in Dapr run failed one case. This closes the restore and local verification increment. Story 3.6 remains in progress.

After the live run, the HTTP test fixture was corrected to record an adjustment outcome for a used adjustment link. The full invalid-link response matrix then passed. A separate regression checks that a legacy revocation and a use on the Time Entry stream deny the same capability.

Two earlier startup attempts stopped at Aspire's read-only socket directory and at the SDK's refusal to store private transport material beneath a directory containing `.git`. Each retained its protected catalog profile. The final attempt used writable Aspire sockets and `/var/tmp`. The four prerequisite containers installed for the test were removed afterward. The fixture retained its own Redis container, components and private profile.

## Review

Review identified premature expiry in a test fixture, omitted timestamp and action validation in the use fold, and a stale Dapr version in the launch register. The fixes align the fold with the persisted projection's checks, record valid historical outcomes in fixtures, and document the version selected by restore.
