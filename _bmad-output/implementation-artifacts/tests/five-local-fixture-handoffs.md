# Five local fixture safety handoffs — 2026-10-21

Baseline: clean `main` at `87350775d56d7dceef9943630b9acf9c7b9cae53`.
The user requested five deferred items together and publication to main. Exactly
five original entries receive resolution notes; historical evidence is retained.

| Deferred item | Delivered behavior and evidence |
| --- | --- |
| Bare/incomplete fixture startup | Existing AppHost admission refuses missing components, unsafe private stores/control planes, and unsupported contexts before resource construction. Current real Debug/source compiled probes cover the complete 35-case driver. |
| Recording submit-response loss | The recording service observes the same command without resubmission, then requires Completed status and exact stored events. `MagicLinkEventStoreActorPersistenceTests.ExternalRecordingRecoversCommittedBatchAfterSubmitResponseFault` covers Draft/Submitted batches, complete persisted event values and an HTTP retry with one submission. |
| Routine compiled guard probes | The new blocking `apphost-configuration` CI job builds Release/package AppHost assets with warnings as errors, then runs the driver in explicit Release configuration-only mode and uploads sanitized receipts even on failure. Package builds must refuse private compiled-gateway settings. Debug remains the local default; Release infrastructure mode and missing Release assets refuse without fallback. |
| Monitoring credential redaction | Existing plain/structured/escaped/multiline OTLP and API-key redaction is covered by the 97-test fixture module, now also executed routinely in CI. Command arguments, stdout/stderr and retained evidence are covered. |
| Historical restart timeout | The delivered EventStore-owned native launch adapter and checked-in process checks verify direct DCP ownership and old/new PID/start replacement around normal restart. Current Python regressions preserve these checks. The October 18 corrected-source journey passed 1/1 in 131.536 s, preserving shared state and cleaning all owned resources. It is dated historical proof, not qualification of the later graph. |

The CI change reuses existing immutable action pins, initializes only the
root-declared Builds checkout, and preserves the Release/package dependency
policy. Local restore/build use Debug/source references. The fixture implementation changes no dependency, gitlink,
AppHost C# source, authorization default or release disposition; the separate
external dependency update is identified below.

Initial focused checks passed:

- Matching Debug/source solution restore and warning-as-error build: zero warnings/errors.
- `python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py -v`: 97/97.
- `python3 -m unittest discover -s scripts/tests -v`: 88/88, including the installed persona suites and their mirrors.
- `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/check_magic_link_apphost_guards.py --configuration-only`: 35/35 real compiled Debug probes.
- `actionlint .github/workflows/ci.yml` and `git diff --check`: clean.

The independent context-free Blind Hunter review found no confirmed additional
defects. Its independent fixture-module execution also passed 97/97. No review
finding was deferred.

The exact local restore/build commands were:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

Each default executable runs separately as
`DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/<Project>/bin/Debug/net10.0/<Project> -noColor`.
Both the Works forwarding default and consuming catalog are `3.126.0`; no
historical EventStore version override is applied.

[Retained current local receipts](../evidence/local-fixture/20261021-five-handoffs/)
contain input hashes, compressed restore/build/Python logs, per-probe results,
explicit build configuration and cleanup status. Cleanup confirms no components
or containers were created. The complete default-suite results are recorded below.

Local policy permits Debug/source builds; actual Release/package probe execution
belongs to the new hosted CI lane. Python Release tests validate selection,
expected package refusals, missing assets, incompatible adapter success and
infrastructure-mode rejection; they are not a real Release execution claim.
No live topology was started during this change. The previous corrected restart
receipts remain in [the October 18 proof packet](../evidence/3-6/20261018-direct-eventstore-local-proof/README.md#final-corrected-source-verification).
Current-graph runtime and deployed inventory/authority/shared-rebuild acceptance
remain open, and the overall launch decision remains **FAIL**.


## Completed default suites and shared-checkout publication

All six executables built at the original baseline passed: **2,252 passed, five
intentional skips, zero failures/errors/not-run**. Exact commands, durations and
per-suite compressed logs are retained in `default-suites.json` and its adjacent
receipts. Projection ran 569/569 in 581.732 s; Integration ran 848 passing plus five
skips in 849.002 s.

While those executables were running, another process committed and pushed the
prepared fixture changes as `433055f0a5364eb77a67343237a569c88398ee8e`, then advanced
ten root-declared gitlinks in `8c42a8ae153e3a11061ff1bd45f5f9373c70c78c`. Those
commits and pointers were preserved. The original full-suite receipts identify
the original build and do not requalify the advanced dependency graph.

The later graph was restored/rebuilt in matching Debug/source mode with zero
warnings/errors. It passed **1,020 focused cases**: Architecture 103, Contracts 92,
Server 564, projection handler 183, Works 76, and both Draft/Submitted recording
response-loss cases. All 35 actual compiled Debug guards passed again. The
current commands, filters, counts, build receipts and configuration/cleanup
records are retained separately in `current-focused.json` and `current-*` files.
The current EventStore catalog and Works forwarding default remain `3.126.0`.

## Hosted qualification and CI correction

[The hosted Release/package guard job](https://github.com/Hexalith/Hexalith.Timesheets/actions/runs/39656896437/job/119419445055)
passed all compiled probes on `8c42a8ae153e3a11061ff1bd45f5f9373c70c78c`.
`hosted-release-configuration.json` records the job identity and successful
conclusion. This is configuration admission evidence, not a running AppHost.

The newly routine Python job exposed one existing test's dependence on a built
EventStore sibling. Its failed run is retained in `hosted-tooling-failure.log.gz`.
The test now queries the real SDK against a temporary standalone project and a
non-executed native file, retaining all poisoned-environment assertions without
restore/build or sibling checkout. The job explicitly installs the pinned SDK
using the existing setup-dotnet pin. Both real SDK tests pass with the sibling
project path deliberately unavailable, and the complete fixture module passes
97/97 after correction. An intermediate local correction omitted the working
directory required by metadata admission; its failed result is retained separately
from the final passing run. Final source hashes are in `followup-inputs.json`.

Independent review of the correction found no concrete issues; the reviewer also
ran both focused SDK tests successfully. No new deferred item was added. The
corrected source will receive its own hosted run after publication; no successful
result is inferred from the earlier failed Python job.
