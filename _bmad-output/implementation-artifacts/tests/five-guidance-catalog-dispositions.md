# Five completed guidance and catalog dispositions

Inspected on 2026-10-22 at Timesheets
`dd0b6796efe0935f94a2c1feb9570d2af8948ec8` on main. The user's existing
deployment-authority spec edit is preserved and excluded from this change.
The [build spec](../spec-close-five-guidance-catalog-dispositions.md) records this
increment's scope. This increment adds five explicit `resolution_note` dispositions to completed
work; it delivers no new runtime implementation. Three entries already had
prose or inline resolutions. The two scope-selection carry-forwards overlap
the same delivered guidance.

| Entry in the deferred-work ledger | Existing completion and current evidence |
| --- | --- |
| [September 12 Story 5.3 review: stale `bmad:context` block](../deferred-work.md#deferred-from-code-review-of-spec-5-3-consume-umbrella-owned-hexalith-works-checkout-2026-09-12) | Already prose-resolved by [checkout governance](../spec-close-five-submodule-governance-deferrals.md). The three entry points are identical and name the current SDK, six individual test executables, explicit Works-root preservation, references-first/sibling-second resolution and root-checkout prohibition. |
| [September 12 Story 5.3 review: incorrect imported-catalog claims](../deferred-work.md#deferred-from-code-review-of-spec-5-3-consume-umbrella-owned-hexalith-works-checkout-2026-09-12) | Already inline-resolved by [Story 5.2](../spec-5-2-reconcile-package-currency-and-platform-dependency-versions.md). Current readiness platform/waiver rows and architecture version notes agree with the actual imported catalog: Dapr 1.18.10, Aspire Hosting 13.6.1 and Fluent UI V5 5.0.1. These are inspected catalog pins, not a new restored-package audit. |
| [September 12 Story 5.2 review: entry-point SDK 10.0.302](../deferred-work.md#deferred-from-code-review-of-spec-5-2-reconcile-package-currency-and-platform-dependency-versions-2026-09-12) | Already prose-resolved. All three entry points agree with global.json SDK 10.0.402, latestPatch, and README's individual test-run instructions. |
| [Diagnostic-redaction scope selection: refresh guidance and reconcile launch documentation](../deferred-work.md#deferred-from-diagnostic-credential-redaction-scope-selection-2026-10-18) | [Runtime guidance](../spec-close-five-runtime-guidance-deferrals.md), [agent-guidance reconciliation](../spec-close-five-agent-guidance-carry-forwards.md) and Story 5.1 already supply this correction. Host registration corroborates projection wiring; the current launch record and entry points agree that acceptance is unfinished and overall release is FAIL. |
| [Restart-timeout scope selection: refresh synchronized guidance](../deferred-work.md#deferred-from-eventstore-restart-timeout-scope-selection-2026-10-18) | The [earlier guidance report](five-agent-guidance-carry-forwards.md) explicitly covers this same SDK/wiring/posture carry-forward. The added note makes its disposition explicit at the original entry. Runtime qualification remains separate. |

The original ledger observations and all three earlier prose/inline resolutions
remain intact in [deferred work](../deferred-work.md). These are five ledger
entries covering overlapping completed corrections, not five independent fixes.
Distinct layering, gateway configuration, product-policy, migration, current-graph
runtime and deployed authority/shared-rebuild work remains open. The current
[launch assessment](../../../docs/launch-readiness.md) retains overall **FAIL**
and its separate package **CONCERNS**.

Checked sources: [AGENTS.md](../../../AGENTS.md),
[CLAUDE.md](../../../CLAUDE.md),
[Copilot instructions](../../../.github/copilot-instructions.md),
[global.json](../../../global.json), [README](../../../README.md),
[HTTP Program.cs](../../../src/Hexalith.Timesheets/Program.cs),
[architecture](../../planning-artifacts/architecture.md), and the
[imported Builds catalog](../../../references/Hexalith.Builds/Props/Directory.Packages.props).
The catalog checkout and committed gitlink both equal
`359fee24aeca4d9eabed4b425d4cfa2520b2adda`.

Delivery provenance can be inspected without changing any checkout:

```bash
git show f82c446f62d9645735989c8fa2c12257dff4a8c9 -- AGENTS.md CLAUDE.md .github/copilot-instructions.md docs/launch-readiness.md
git show eff123eac31c4708689682b222ae6133b6d5bea0 -- AGENTS.md _bmad-output/implementation-artifacts/deferred-work.md
git show b85e81093475f55ff0f2b7bcc3c7bea9caa81d73 -- docs/launch-readiness.md _bmad-output/planning-artifacts/architecture.md
```

The first is Story 5.1's SDK/posture/wiring guidance correction; the second
records Works governance and the two prose resolutions; the third records the
October 18 Story 5.2 package reconciliation. Later refinements remain recorded
in the linked completed specs. Current architecture fitness checks read the
imported catalog rather than treating these historical versions as current.

Verification uses the existing
[architecture suite](../../../tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs),
including `Managed_entry_points_match_configuration_and_projection_wiring`,
`Launch_readiness_record_captures_package_currency_verdict_dimensions`,
`ArchitectureVersionNotesMatchExecutableConfiguration` and
`Unfinished_v1_acceptance_cannot_be_relabelled_as_a_waiver`.
The managed-entry-point test checks guidance strings. Actual host registration
source is checked separately by
[`DependencyDirectionTests.Timesheets_host_discovers_projection_handlers_and_maps_canonical_sdk_routes`](../../../tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/DependencyDirectionTests.cs).
[`WorksCheckoutGovernanceTests`](../../../tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/WorksCheckoutGovernanceTests.cs)
evaluates actual MSBuild resolution for caller-supplied roots and
references-first/sibling-second fallback. All these cases ran in the same
complete architecture executable; source/configuration checks supply no live
host or deployed integration proof.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests
git diff --check
```

All four commands exited 0. The matching Debug/source architecture build
completed with zero warnings/errors in 2.82 s. The complete existing executable
reported:

```text
Hexalith.Timesheets.ArchitectureTests  Total: 109, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 9.372s
```

These are current focused documentation/configuration checks. The other five
test executables were not rerun for this ledger-only change. No AppHost was
started or stopped, and no new runtime or deployed acceptance is supplied.

The reconciliation's preservation check is reproducible after commit from
the repository root. Timesheets-owned files must retain the original LF
checkout convention (`core.autocrlf=false` or equivalent effective attributes).
The byte comparisons intentionally refuse a checkout rewritten to CRLF;
they verify preservation in this run, while the architecture tests verify
configuration and guidance content. The imported dependency's existing
CRLF/LF conversion is handled separately below.

```bash
python3 - <<'PY'
from pathlib import Path
import json, re, subprocess

baseline = 'dd0b6796efe0935f94a2c1feb9570d2af8948ec8'
def original(path):
    return subprocess.check_output(['git', 'show', f'{baseline}:{path}'])

ledger = Path('_bmad-output/implementation-artifacts/deferred-work.md')
owned = {str(ledger),
         '_bmad-output/implementation-artifacts/spec-close-five-guidance-catalog-dispositions.md',
         '_bmad-output/implementation-artifacts/tests/five-guidance-catalog-dispositions.md'}
user_path = '_bmad-output/implementation-artifacts/spec-3-6-deployment-authority-runtime-continuation.md'
changed = set(subprocess.check_output(['git', 'diff', '--name-only', baseline, '--']).decode().splitlines())
untracked = set(subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard']).decode().splitlines())
assert (changed | untracked) - {user_path} == owned, changed | untracked
for path in owned - {str(ledger)}:
    raw = Path(path).read_bytes()
    assert raw.endswith(b'\n') and b'\r' not in raw, path
    assert all(line == line.rstrip(b' \t') for line in raw.splitlines()), path
lines = ledger.read_bytes().splitlines(keepends=True)
prefix = b'  resolution_note: Closed 2026-10-22 by verification in `spec-close-five-guidance-catalog-dispositions.md`'
needles = [
    b'- The `bmad:context` block was added to',
    b'- `docs/launch-readiness.md:21` misstates the imported Builds catalog:',
    b'- `AGENTS.md:92`, `CLAUDE.md:92`,',
    b'  summary: Refresh stale repository guidance and reconcile launch documentation separately.',
    b'  summary: Refresh synchronized repository guidance in a separate increment.',
]
assert sum(line.startswith(prefix) for line in lines) == 5
for needle in needles:
    matches = [i for i, line in enumerate(lines) if line.startswith(needle)]
    assert len(matches) == 1, needle
    assert lines[matches[0] + 1].startswith(prefix), needle
assert b''.join(line for line in lines if not line.startswith(prefix)) == original(ledger)
paths = ['AGENTS.md', 'CLAUDE.md', '.github/copilot-instructions.md']
documents = [Path(path).read_bytes() for path in paths]
assert all(document == documents[0] for document in documents)
assert all(document == original(path) for path, document in zip(paths, documents))
for path in ['global.json', 'README.md', 'src/Hexalith.Timesheets/Program.cs',
             'docs/launch-readiness.md', '_bmad-output/planning-artifacts/architecture.md',
             'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props']:
    assert Path(path).read_bytes() == original(path), path
catalog_revision = '359fee24aeca4d9eabed4b425d4cfa2520b2adda'
catalog_repo = 'references/Hexalith.Builds'
assert subprocess.check_output(['git', 'rev-parse', f'HEAD:{catalog_repo}']).decode().strip() == catalog_revision
assert subprocess.check_output(['git', '-C', catalog_repo, 'rev-parse', 'HEAD']).decode().strip() == catalog_revision
# This dependency checks out CRLF while its Git object stores LF.
assert Path(catalog_repo, 'Props/Directory.Packages.props').read_text() == subprocess.check_output(
    ['git', '-C', catalog_repo, 'show', f'{catalog_revision}:Props/Directory.Packages.props']).decode().replace('\r\n', '\n')
sdk = json.loads(Path('global.json').read_text())['sdk']
assert f"`{sdk['version']}`".encode() in documents[0]
assert f"rollForward: {sdk['rollForward']}".encode() in documents[0]
report = Path('_bmad-output/implementation-artifacts/tests/five-guidance-catalog-dispositions.md')
for target in re.findall(r'\]\(([^)]+)\)', report.read_text()):
    path, _, anchor = target.partition('#')
    linked = (report.parent / path).resolve()
    assert linked.is_file(), target
    if anchor:
        headings = re.findall(r'(?m)^#{1,6} (.+)$', linked.read_text())
        slugs = [re.sub(r'\s', '-', re.sub(r'[^\w\s-]', '', heading.lower())) for heading in headings]
        assert anchor in slugs, target
print('PASS: exactly five adjacent dispositions; original ledger, guidance and sources preserved; links resolve')
PY
```

The separate local preservation check records the user's uncommitted
deployment-authority spec at SHA-256
`44781ce44c014f4de846211a65a3b01f3b4be63d9db11505659fbd315f037d06`.
This is a local snapshot, not a requirement for future clones; that edit is
excluded from staging and the commit. Only the ledger, this report and the
new build spec belong to this increment.

The scope check above includes tracked and untracked changes relative to the
fixed baseline and permits only the three owned files plus that separately
preserved user path. New-artifact whitespace is checked explicitly because
`git diff --check` alone omits untracked files. Before commit,
`git diff --cached --check` also validates the complete staged change.

Independent Blind Hunter review returned five findings: the byte check's LF
prerequisite, untracked-document whitespace, scope-check reproduction, precise
test attribution and navigation. All five were verified and addressed in this
report and its executable check; nothing new is deferred. The expanded check
passes, and the 109-case architecture result remains applicable because only
documentation changed during review.
