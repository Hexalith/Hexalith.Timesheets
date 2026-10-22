# Five local recovery carry-forwards — 2026-10-22

Inspection baseline: clean `main` at
`5f3a83926500600f7cf32ee7828f5056afd99b85`. These are five **ledger disposition
corrections for existing implementations**, not five new runtime fixes. The
selected entries already said their fixes were delivered, but lacked
`resolution_note`. Original summaries, evidence and receipts are retained.

| Selected ledger summary | Existing implementation and provenance | Closure boundary |
| --- | --- | --- |
| The deferred bare TIMESHEETS_LIVE_DAPR_FIXTURE=1 startup gap is closed by a separate scoped increment. | [Startup-hardening spec](../spec-3-6-harden-live-dapr-fixture-startup.md); delivered in `6866353684e795cba122b86875071091325ff362`. [AppHost Program.cs](../../../src/Hexalith.Timesheets.AppHost/Program.cs) requires run mode, explicit validated components and distinct loopback control-plane endpoints with allowed port numbers before resource construction. | AppHost endpoint validation establishes address shape, not process ownership. The isolated runner separately provisions labeled private containers, validates their identities/ports and checks ownership for cleanup. Configuration-only probes create no control planes, DCP, dashboard or application resources. |
| The local recording lost-response follow-up from the final materialization review is implemented. | Delivered in `11976c2d8000bfc83f1d816a085836beecf8f6d1`. [Durable submission](../../../src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkDurableSubmissionService.cs) observes the same command after failed submit response within its existing polling deadline, without resubmission. [Recording verification](../../../src/Hexalith.Timesheets.Server/TimeEntries/TimeEntryRecordSubmissionService.cs) requires Completed status and exact stored recording/submission events. | Existing service tests cover failed responses, malformed evidence, terminal failure, transient status, timeout and cancellation. The actor-backed HTTP test checks Draft/Submitted stored end state and a retry retaining one submission. It is an in-process fixture. |
| The deferred local OTLP/API-key diagnostic sanitization gap is closed. | [Redaction spec](../spec-redact-diagnostic-telemetry-credentials.md); delivered in `95d5955f58c5fc9fb566375dfb2cda0c339650fd`. The [isolated runner](../../../scripts/run_isolated_magic_link_dapr.py) sanitizer handles plain, structured, escaped and multiline credential evidence. | Existing synthetic tests cover command arguments, stdout/stderr and timeout streams. This local diagnostic scope supplies no production logging or deployed privacy qualification. |
| Shared-state verification now refuses active baselines and reports the specific changed services without exposing state keys or values. | [Preservation spec](../spec-fix-shared-state-preservation-checks.md); delivered in `edcbba9219b20f0c9cf18db9637467065fc5144e`. The isolated runner and [guard driver](../../../scripts/check_magic_link_apphost_guards.py) repeat runtime admission around two stable baseline observations, require scheduler revision equality during baseline admission, retain service-specific diagnostics and enforce final preservation/owned cleanup. | Final preservation compares scheduler cluster identity and retained contents; it permits revision-only changes. Tests exercise unstable baselines, late arrivals, real listeners/sidecars, service changes, baseline revision-only activity, privacy and original failure retention. This is no cross-workspace atomic reservation or new live preservation proof; the historical observations do not identify a writer. |
| The selected local restart timeout now has a reviewed and verified direct-execution correction. | [Native-launch adapter](../../../src/Hexalith.Timesheets.AppHost/IsolatedGatewayLaunch.cs) delegates to EventStore. The runner checks native command, fixture-bound DCP parent, replacement PID/start identity and old-process exit. Published as Timesheets `9523d94983a346d866246a2c45c3515b1bc54580`, following EventStore implementation `9b66153515687463fc1b1e929f8b0e73d8145e2c`. | The [October 18 receipt](../evidence/3-6/20261018-direct-eventstore-local-proof/post-review/verification.json) records 1/1 live journey in 131.536 s, shared preservation and complete cleanup. Publication revisions are not the tested source identities; current driver tests do not requalify the later NuGet orchestration graph. |

The earlier [five local fixture handoffs](five-local-fixture-handoffs.md) resolved
four original implementation requests shared with this set: startup admission,
recording response-loss recovery, credential redaction and native restart
recovery. Its fifth item added routine compiled CI probes. This set instead
includes the independently completed [shared-state preservation work](../spec-fix-shared-state-preservation-checks.md).
This increment resolves five separate historical completion carry-forwards. It
changes only the ledger, its [spec](../spec-close-five-local-recovery-carry-forwards.md)
and this report. Runtime code, tests, configuration, dependencies, all three agent
entry points, story tracking, launch classifications and archived receipts stay
unchanged. No new tests are needed for the additive documentation correction.

The inspected root pins EventStore
`5ce5f1d84b88c808cc8a526b9decf149de977e55`, Builds
`359fee24aeca4d9eabed4b425d4cfa2520b2adda` and Works
`d60c29569de975bd280098e2e2f37406a30cf705`. Catalog and Works forwarding defaults
both select EventStore `3.126.0`; the current commands do not force the historical
`3.125.1` override. The unresolved structural forwarding issue is separate.

The October 18 journey ran on modified source based on Timesheets
`72d9fdaa813daf9d39d296e268fd2e1086a8dd87` and EventStore
`d55c6f7c11e9bce6f17d490a84c83f1561979d63`. Its [retained proof packet](../evidence/3-6/20261018-direct-eventstore-local-proof/README.md#final-corrected-source-verification)
distinguishes source snapshots, intermediate failures and later publication.
Its historical SDK override and CLI-bundle DCP do not qualify the current graph.
No live topology is started in this increment. Current-graph runtime, deployed
authority, inventory/continuity, migration and shared rebuild acceptance remain
open; the overall launch verdict remains **FAIL**.

Current focused verification used SDK `10.0.402`, Python `3.14.4` and the baseline
sources above. All commands exited **0**. Restore and build used matching
Debug/source graphs:

```bash
set -e
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py -v
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests -class '*TimeEntryRecordSubmissionServiceTests' -class '*MagicLinkDurableSubmissionServiceTests' -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method '*ExternalRecordingRecoversCommittedBatchAfterSubmitResponseFault' -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/check_magic_link_apphost_guards.py --configuration-only --configuration Debug --log-directory /tmp/timesheets-local-recovery-guards
```

Portable result summaries from this run:

```text
Build succeeded. 0 Warning(s), 0 Error(s). Time Elapsed 00:00:12.90
Ran 97 tests in 22.306s — OK
Hexalith.Timesheets.Server.Tests Total: 49, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 1.763s
Hexalith.Timesheets.IntegrationTests Total: 2, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 1.672s
Hexalith.Timesheets.ArchitectureTests Total: 109, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 13.308s
Compiled guards: 35 cases, all passed; expected invalid-configuration process exits are refusal evidence.
guards-mode: configuration=Debug, configuration_only=true, resources_enabled=false
cleanup: complete=true, journey_descendants_gone=true, failure_reasons=[], components_created=false, components_removed=false, containers_removed=0
```

This is **160 focused .NET cases**, 97 Python cases and 35 compiled configuration
probes. The full default .NET suites and opt-in live/performance lanes were not
rerun for this documentation change. Temporary raw logs use the prefix
`/tmp/timesheets-local-recovery-`; guard receipts are under
`/tmp/timesheets-local-recovery-guards/run-68njf3hg/`. The commands and result
summaries above are retained here so the disposition does not depend on those
temporary paths surviving.

Run this read-only preservation check from the repository root. It remains valid
after commit because its comparison revision is pinned:

```bash
set -e
python3 - <<'PY'
from pathlib import Path
import re
import subprocess

base = '5f3a83926500600f7cf32ee7828f5056afd99b85'
ledger = Path('_bmad-output/implementation-artifacts/deferred-work.md')
spec = '_bmad-output/implementation-artifacts/spec-close-five-local-recovery-carry-forwards.md'
report = Path('_bmad-output/implementation-artifacts/tests/five-local-recovery-carry-forwards.md')
selected = {
    'The deferred bare TIMESHEETS_LIVE_DAPR_FIXTURE=1 startup gap is closed by a separate scoped increment.',
    'The local recording lost-response follow-up from the final materialization review is implemented.',
    'The deferred local OTLP/API-key diagnostic sanitization gap is closed.',
    'Shared-state verification now refuses active baselines and reports the specific changed services without exposing state keys or values.',
    'The selected local restart timeout now has a reviewed and verified direct-execution correction.',
}
original = subprocess.check_output(['git', 'show', base + ':' + str(ledger)])
current = ledger.read_bytes()
marker = ('  resolution_note: Resolved 2026-10-22 by `' + Path(spec).name + '`').encode()
lines = current.splitlines(keepends=True)
added = [line for line in lines if line.startswith(marker)]
assert len(added) == 5
assert b''.join(line for line in lines if not line.startswith(marker)) == original
seen = set()
for entry in current.decode().split('- source_spec:')[1:]:
    summary = next(line.removeprefix('  summary: ') for line in entry.splitlines()
                   if line.startswith('  summary: '))
    notes = [line for line in entry.splitlines() if line.startswith('  resolution_note:')]
    if any(line.encode().startswith(marker) for line in notes):
        assert summary in selected and summary not in seen
        assert len(notes) == 1
        entry_lines = entry.splitlines()
        assert entry_lines[entry_lines.index(notes[0]) - 1].startswith('  evidence: ')
        seen.add(summary)
assert seen == selected
for name in ['AGENTS.md', 'CLAUDE.md', '.github/copilot-instructions.md']:
    assert Path(name).read_bytes() == subprocess.check_output(['git', 'show', base + ':' + name])
for target in re.findall(r'\]\(([^)]+)\)', report.read_text()):
    assert (report.parent / target.split('#')[0]).resolve().exists(), target
changed = set(subprocess.check_output(['git', 'diff', '--name-only', base]).decode().splitlines())
changed.update(subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard']).decode().splitlines())
assert changed == {str(ledger), spec, str(report)}, changed
for name in changed:
    content = Path(name).read_bytes()
    assert content.endswith(b'\n'), name
    assert all(line == line.rstrip(b' \t') for line in content.splitlines()), name
print('PASS: five unique adjacent dispositions; original ledger bytes and entry points preserved; links, whitespace and three-file scope verified.')
PY
git diff --check 5f3a83926500600f7cf32ee7828f5056afd99b85
```

The Python checks include all three files even before the new documents are
staged. The base-relative Git whitespace check covers their committed diff after
publication. `set -e` propagates a failing preservation assertion immediately.

The independent Blind Hunter review returned five documentation improvements:
failure-status propagation, whitespace coverage for new files, precise endpoint
validation/ownership boundaries, baseline versus final scheduler checks, and
mapping the earlier five-item set. All five were verified and corrected. The
final preservation command passed; a synthetic assertion failure returned exit
1 as expected. No runtime change or additional deferral was needed. The existing
build and focused test results above remain applicable after these prose-only
corrections.
