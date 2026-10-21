# Five launch-evidence handoffs — verification, 2026-10-21

Scope: documentation changes based on Timesheets
`f62eb4a8404f7f497c57e0189f81b7f7fbeb565b`. The selected October 20 release
gates, their suite counts, and all historical receipts retain their original scope.
These checks do not qualify the current AppHost graph or deployed acceptance.

Run from the repository root:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests
```

Restore passed. Build passed with zero warnings/errors (0.97 s). Before review,
the focused selection passed 16/16 (0.386 s), and the full Architecture executable
passed 103/103 (10.360 s), with zero errors, failures, skips or not-run cases.
Post-review results are appended below after the corrected documents are checked.
Other executables were not rerun for this documentation-only change.

The following read-only probe reproduces the five ledger additions, preserved
historical bodies, scope restrictions, new local links, and configuration/Git
observations. “No agent-entry-point changes” refers specifically to `AGENTS.md`,
`CLAUDE.md`, and `.github/copilot-instructions.md`; CI/CD and Epic 5 guidance
intentionally changed. It works before staging and after the closure commit.

```bash
python3 - <<'PY'
import json
import re
import subprocess
from pathlib import Path
from xml.etree import ElementTree as ET

baseline = 'f62eb4a8404f7f497c57e0189f81b7f7fbeb565b'
def git(*args):
    return subprocess.check_output(['git', *args]).decode()
def before(path):
    result = subprocess.run(['git', 'show', f'{baseline}:{path}'], capture_output=True)
    return result.stdout.decode() if result.returncode == 0 else ''

ledger = '_bmad-output/implementation-artifacts/deferred-work.md'
lines = Path(ledger).read_text().splitlines()
tag = 'resolution_note: Resolved 2026-10-21 by `spec-close-five-launch-evidence-deferrals.md`'
added = [line for line in lines if tag in line]
assert len(added) == 5
assert '\n'.join(line for line in lines if line not in added) + '\n' == before(ledger)

for path, marker in [
    ('docs/launch-readiness.md', '### Deployment and authority implementation — 2026-10-17'),
    ('_bmad-output/implementation-artifacts/5-1-final-launch-readiness-gate-and-documentation-sync.md', '## Historical Tasks / Subtasks (original execution)'),
    ('_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md', '## 1. Issue Summary'),
]:
    assert before(path).split(marker, 1)[1] == Path(path).read_text().split(marker, 1)[1]

changed = set(git('diff', '--name-only', baseline).splitlines())
changed.update(git('ls-files', '--others', '--exclude-standard').splitlines())
expected = {
    ledger, 'docs/ci-cd.md', 'docs/launch-readiness.md',
    '_bmad-output/implementation-artifacts/5-1-final-launch-readiness-gate-and-documentation-sync.md',
    '_bmad-output/implementation-artifacts/epic-5-context.md',
    '_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-12.md',
    '_bmad-output/implementation-artifacts/spec-close-five-launch-evidence-deferrals.md',
    '_bmad-output/implementation-artifacts/tests/five-launch-evidence-handoffs.md',
}
assert changed == expected, changed ^ expected
links = 0
for path in changed:
    pattern = r'\[[^\]]+\]\(([^)]+)\)'
    for target in set(re.findall(pattern, Path(path).read_text())) - set(re.findall(pattern, before(path))):
        if '://' in target:
            continue
        dest, _, fragment = target.partition('#')
        resolved = (Path(path).parent / dest).resolve()
        assert resolved.exists(), (path, target)
        if fragment:
            headings = re.findall(r'^#+ (.+)$', resolved.read_text(), re.M)
            slugs = [re.sub(r'[^\w -]', '', h.lower()).replace(' ', '-') for h in headings]
            assert fragment in slugs, (path, target)
        links += 1

assert json.loads(Path('global.json').read_text())['sdk']['version'] == '10.0.402'
apphost = ET.parse('src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj').getroot()
assert apphost.attrib['Sdk'] == 'Aspire.AppHost.Sdk/13.6.1'
for path in ['references/Hexalith.Builds/Props/Directory.Packages.props', 'references/Hexalith.Works/Directory.Build.props']:
    assert ET.parse(path).getroot().find('.//HexalithEventStoreVersion').text == '3.126.0'
observation = Path('docs/launch-readiness.md').read_text().split('**Record reconciliation — 2026-10-21:**', 1)[1].split('\n\n', 1)[0]
assert baseline in observation
assert git('ls-tree', baseline, 'references/Hexalith.EventStore').split()[2] in observation
print(f'PASS: five ledger additions; historical bodies and scope preserved; {links} new local links resolve; configuration/Git observations match.')
PY
git diff --check
```

Post-review verification: the full Architecture executable passed **103/103**
(12.514 s), zero errors, failures, skips or not-run cases, including all 16
launch-readiness checks. The retained probe passed with exactly five ledger
additions, unchanged historical bodies, the expected eight-document scope,
12 resolving local links, and matching configuration/Git observations.
`git diff --check` passed. The Conventional Commit message passed
`npm run commitlint -- --edit /tmp/timesheets-launch-handoffs-commit-message.txt`.
