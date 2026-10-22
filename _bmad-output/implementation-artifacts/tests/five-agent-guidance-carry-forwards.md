# Five historical agent-guidance carry-forwards

Inspection date: 2026-10-22. Clean starting revision: `2acdce4ec2d347bdb8be9e170b176c8d85a72937`.

Five ledger requests are satisfied by previously delivered guidance. This increment adds their missing dispositions; it implements no new runtime behavior and performs no full managed-context refresh.

| Selected deferred summary | Verified disposition |
|---|---|
| Refresh the agent-context files that still describe the token-hash index as unwired. | All three entry points describe registered projection assembly/EventStore processing. HTTP Program.cs corroborates both registrations. |
| Refresh the CLAUDE.md `timesheets` block's SDK pin. | All three entry points match global.json: SDK 10.0.402, latestPatch. |
| Refresh the launch posture in the agent entry points' `timesheets` block. | All three entry points agree with the current launch assessment: overall FAIL, deployed acceptance unfinished; package currency remains separately CONCERNS. |
| Refresh the managed repository agent context for the current launch posture and magic-link projection wiring. | Combined carry-forward of the posture and wiring requests above; already satisfied. |
| Refresh stale repository guidance in a separate increment (projection checkpoint scope selection). | SDK, wiring, launch-status, and synchronized-entry-point requirements are already satisfied. |

The checked sources are [AGENTS.md](../../../AGENTS.md), [CLAUDE.md](../../../CLAUDE.md), [Copilot instructions](../../../.github/copilot-instructions.md), [global.json](../../../global.json), [HTTP Program.cs](../../../src/Hexalith.Timesheets/Program.cs), and the current assessment in [launch readiness](../../../docs/launch-readiness.md). The existing `LaunchReadinessTests.Managed_entry_points_match_configuration_and_projection_wiring` fitness check covers normalized guidance equality, current SDK, overall FAIL, and wiring guidance.

The SDK, launch verdict, and projection-wiring guidance was corrected by Story 5.1 commit `f82c446f62d9645735989c8fa2c12257dff4a8c9`; `git show f82c446f62d9645735989c8fa2c12257dff4a8c9 -- AGENTS.md CLAUDE.md .github/copilot-instructions.md` verifies that delivery directly. Later refinements are recorded in [runtime guidance](../spec-close-five-runtime-guidance-deferrals.md) and [tooling/guidance follow-up](../spec-close-five-tooling-diagnostic-deferrals.md).

Older ledger references calling the 2026-09-16 wiring-guidance entry “canonical open” are superseded by its new disposition. The matching guidance carry-forward under EventStore restart timeout scope selection refers to the same satisfied SDK/wiring/posture facts and supplies no separate open refresh goal. Original ledger evidence, managed-context generation provenance, historical story notes, and archived runtime receipts are retained. Distinct Story 3.6 tracking reconciliation and deployed/current-graph acceptance remain outside these five dispositions.

Verification (2026-10-22):

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests
git diff --check
```

All commands exited 0. The build had zero warnings/errors; the existing ArchitectureTests suite passed 109/109, with zero failures, errors, skips, or not-run tests (11.273 s). The runtime and domain suites were not rerun for this ledger-only change.

An additional read-only check compared all three entry points byte-for-byte with each other and HEAD, checked the SDK and host registrations, scoped launch claims to the current assessment before Historical Receipts, and resolved every report link. Removing exactly the five new resolution-note lines reproduces the original ledger bytes. All checks passed. The only changes are the ledger, this evidence summary, and the build spec.

Run this preservation/source check from the repository root; its fixed baseline also permits reproduction after committing:

```bash
python3 - <<'PY'
from pathlib import Path
import json, re, subprocess

baseline = '2acdce4ec2d347bdb8be9e170b176c8d85a72937'
def original(path):
    return subprocess.check_output(['git', 'show', f'{baseline}:{path}'])

paths = ['AGENTS.md', 'CLAUDE.md', '.github/copilot-instructions.md']
documents = [Path(path).read_bytes() for path in paths]
assert all(document == documents[0] for document in documents)
sdk = json.loads(Path('global.json').read_text())['sdk']
for path, document in zip(paths, documents):
    assert document == original(path)
    managed = document.decode().split('<!-- bmad:context -->', 1)[1]
    assert f"`{sdk['version']}`" in managed
    assert f"rollForward: {sdk['rollForward']}" in managed
    assert '**FAIL**' in managed
    assert 'HTTP host registers the projection assembly and EventStore processing' in managed
    assert 'has no projection-host wiring' not in managed
    assert 'deployed authority/shared-rebuild acceptance and current-graph AppHost runtime qualification remain unfinished' in managed

host = Path('src/Hexalith.Timesheets/Program.cs').read_text()
assert 'typeof(TimesheetsProjectionsMarker).Assembly' in host
assert 'builder.Services.AddTimesheetsMagicLinkEventStoreProcessing();' in host
current = Path('docs/launch-readiness.md').read_text().split('## Historical Receipts', 1)[0]
assert 'Overall release decision: **FAIL**' in current
assert re.search(r'Package currency[^\n]*CONCERNS', current)
ledger = Path('_bmad-output/implementation-artifacts/deferred-work.md')
lines = ledger.read_bytes().splitlines(keepends=True)
prefix = b'  resolution_note: Closed 2026-10-22 by verification in `spec-close-five-agent-guidance-carry-forwards.md`'
assert sum(line.startswith(prefix) for line in lines) == 5
assert b''.join(line for line in lines if not line.startswith(prefix)) == original(ledger)
report = Path('_bmad-output/implementation-artifacts/tests/five-agent-guidance-carry-forwards.md')
for target in re.findall(r'\]\(([^)]+)\)', report.read_text()):
    assert (report.parent / target).resolve().is_file(), target
print('PASS: five dispositions, original evidence preserved, guidance and source checks passed')
PY
```

Independent Blind Hunter review raised four traceability findings: historical open references, the matching restart-timeout carry-forward, missing original-delivery provenance, and preservation-check reproducibility. All four were addressed in the five added notes and this report; nothing newly deferred.

`aspire ps --format Json` identified foreign AppHosts during preflight. No runtime was started or stopped. These documentation checks supply no new runtime/deployed acceptance, and the overall launch verdict remains FAIL.
