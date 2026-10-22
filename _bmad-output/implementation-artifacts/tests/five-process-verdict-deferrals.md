# Five historical process and verdict deferrals

Inspected on 2026-10-22 at Timesheets
`61ccd95e7ad3b0fdab9bdc9abae98b1847bdeb34` on main. The
[build spec](../spec-close-five-process-verdict-deferrals.md) owns this increment.
It adds explicit dispositions to five historical ledger entries. Four reconcile
delivered or superseded work; the fifth explicitly documents the authoring
convention illustrated by later specs. Two entries overlap the same verdict
correction; these are five ledger dispositions, with no new runtime implementation.

| Ledger entry | Disposition and supporting evidence |
| --- | --- |
| [September 12 Story 5.2 canonical overall-verdict reconciliation](../deferred-work.md#deferred-from-code-review-of-spec-5-2-reconcile-package-currency-and-platform-dependency-versions-2026-09-12) | Delivered. [Launch readiness](../../../docs/launch-readiness.md) records overall FAIL and separate package CONCERNS. [Story 5.1 acceptance](../evidence/5-1/20261020-final-gate/story-acceptance.json) accepts reconciliation independently of remaining release prerequisites and explicitly withholds release authorization. |
| [Same section's raw CONCERNS-verdict bullet](../deferred-work.md#deferred-from-code-review-of-spec-5-2-reconcile-package-currency-and-platform-dependency-versions-2026-09-12) | Superseded duplicate of the canonical correction above. Its existing October 18 update remains intact. This closes the stale correction request, preserving current FAIL and unfinished gates. |
| [Same section's direct-main/branch-first bullet](../deferred-work.md#deferred-from-code-review-of-spec-5-2-reconcile-package-currency-and-platform-dependency-versions-2026-09-12) | Superseded procedure. The current [Hexalith Git instructions](../../../references/Hexalith.AI.Tools/hexalith-git-instructions.md#branches) say to work on main by default. This is no claim of historical compliance and does not rewrite the old commit or close the separate commit-message follow-up. |
| [September 14 repeated agent-context consolidation](../deferred-work.md#deferred-from-code-review-of-story-36-2026-09-14) | Superseded duplicate. The [canonical September 16 unwired-index request](../deferred-work.md#deferred-from-code-review-of-3-6-implement-eventstore-backed-magic-link-state-loading-2026-09-16) is already closed by [agent-guidance reconciliation](../spec-close-five-agent-guidance-carry-forwards.md), whose [report](five-agent-guidance-carry-forwards.md) explicitly supersedes earlier references calling it open. [Historical tracking reconciliation](../spec-close-five-historical-story-tracking-deferrals.md) separately closes the combined tracking/guidance request. |
| [October 5 future build-spec finalization wording](../deferred-work.md#deferred-from-code-review-of-3-6-implement-eventstore-backed-magic-link-state-loadingmd-2026-10-05) | This increment documents the [future-authoring convention](#future-build-spec-status-wording) explicitly. [Spec-13](../spec-3-6-implement-eventstore-backed-magic-link-state-loading-13.md) and [later guidance reconciliation](../spec-close-five-agent-guidance-carry-forwards.md) illustrate the wording; those examples alone were not an earlier closure. Story 3.6 remains in-progress for its unfinished acceptance. |

The canonical launch-verdict entry's old premise that Story 5.1 is still open
is historical: its scoped reconciliation was accepted on October 20. Current
[story tracking](../sprint-status.yaml) and the active launch assessment retain
Story 3.6/3.7 in-progress and Stories 5.1/5.2 done. No status changes are made here.
Runtime qualification, deployed authority/inventory/shared-rebuild acceptance,
legal and tenant policy, product acceptance and package concerns retain their
existing owners and classifications.

## Future build-spec status wording

For subsequent build specs that mention a temporary story or spec status,
qualify that status with **until workflow finalization**. Name the transition
from the applicable workflow separately; acceptance prerequisites remain
independent. For example:

> Keep the story in-progress until workflow finalization. At finalization,
> apply the workflow's status transition while preserving open acceptance gates.

This is the explicit authoring convention recorded by this increment. It
supersedes the earlier interpretation that manual spec-12/spec-13 examples
alone left the request open, retained in the
[Story 3.6 review record](../3-6-implement-eventstore-backed-magic-link-state-loading.md).
The October 18 hook/test-maintenance carry-forward in the
[ledger](../deferred-work.md#deferred-from-diagnostic-credential-redaction-scope-selection-2026-10-18)
and [completed-implementation report](five-completed-implementation-followups.md)
are superseded only where they still call this wording request open. Their
original text remains preserved. PascalCase naming remains open, as do all
separately owned runtime and product requests. This is documented authoring
guidance, without an automatic skill-template change or a universal final
story status; authors must apply the qualification to future specs.

## Provenance and verification

Delivery provenance: Timesheets commit
`f82c446f62d9645735989c8fa2c12257dff4a8c9` records Story 5.1's reconciliation
and scoped sign-off; `91fcb500f8a24ab4fc6d5233e58e13a8e29d9020` records
spec-13's explicit finalization wording. Current Git procedure is read from
the root-declared AI.Tools dependency, whose committed gitlink and checkout both
equal `3f194e17174994d308ec84af9ee2b5aa68674d0d`. These observations supply no new
runtime or deployed evidence. All earlier observations, including the adjacent
historical commit-message follow-up, remain unchanged.

Verification uses the existing
[LaunchReadinessTests](../../../tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs),
particularly `Unfinished_v1_acceptance_cannot_be_relabelled_as_a_waiver`,
`Current_story_records_match_sprint_tracking_and_leave_prerequisites_open`, and
`Managed_entry_points_match_configuration_and_projection_wiring`. Those tests
check classification/tracking/guidance content. The Git procedure and later
spec wording are inspected directly; these are no runtime behavior assertions.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests
git diff --check
```

All four commands exited 0. The build completed with zero warnings/errors in
2.15 s. The complete existing executable passed 109/109, with zero errors,
failures, skips or not-run tests (18.152 s). The exact preservation/scope/link
check below also passed, and the user's existing edit retained SHA-256
`44781ce44c014f4de846211a65a3b01f3b4be63d9db11505659fbd315f037d06`.
No new tests, full solution build, other suite, package audit, AppHost operation
or deployed operation was run for this ledger-only change.

The following read-only check works before and after commit from this original
workspace while the preserved user edit remains present. Its hash intentionally
does not describe a fresh clone or a later user edit. Byte preservation assumes
the Timesheets LF checkout convention; the dependency's existing CRLF checkout
is read as normalized text. The check also rejects staging of the user path.

```bash
python3 - <<'PY'
from pathlib import Path
import hashlib, json, re, subprocess

baseline = '61ccd95e7ad3b0fdab9bdc9abae98b1847bdeb34'
ledger = '_bmad-output/implementation-artifacts/deferred-work.md'
spec = '_bmad-output/implementation-artifacts/spec-close-five-process-verdict-deferrals.md'
report = '_bmad-output/implementation-artifacts/tests/five-process-verdict-deferrals.md'
user_path = '_bmad-output/implementation-artifacts/spec-3-6-deployment-authority-runtime-continuation.md'
owned = {ledger, spec, report}
def original(path):
    return subprocess.check_output(['git', 'show', f'{baseline}:{path}'])

changed = set(subprocess.check_output(['git', 'diff', '--name-only', baseline, '--']).decode().splitlines())
untracked = set(subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard']).decode().splitlines())
assert (changed | untracked) - {user_path} == owned, changed | untracked
assert hashlib.sha256(Path(user_path).read_bytes()).hexdigest() == '44781ce44c014f4de846211a65a3b01f3b4be63d9db11505659fbd315f037d06'
staged = set(subprocess.check_output(['git', 'diff', '--cached', '--name-only']).decode().splitlines())
assert user_path not in staged and staged <= owned, staged
for path in owned:
    raw = Path(path).read_bytes()
    assert raw.endswith(b'\n') and b'\r' not in raw, path
    assert all(line == line.rstrip(b' \t') for line in raw.splitlines()), path
lines = Path(ledger).read_bytes().splitlines(keepends=True)
prefix = b'  resolution_note: Closed 2026-10-22 by verification in `spec-close-five-process-verdict-deferrals.md`'
needles = [
    b'  summary: Reconcile the overall launch-readiness verdict while Stories 3.6 and 5.1 remain open',
    b'- `docs/launch-readiness.md:78` still records the overall release decision',
    b'- Commit `ee76a92` was made directly on `main`;',
    b'- The repeated agent-context finding is consolidated into the canonical open',
    b'  summary: State the status intent of future build specs as "in-progress until finalization"',
]
assert sum(line.startswith(prefix) for line in lines) == 5
for needle in needles:
    matches = [i for i, line in enumerate(lines) if line.startswith(needle)]
    assert len(matches) == 1, needle
    end = next((i for i in range(matches[0] + 1, len(lines))
                if lines[i].startswith(b'- ') or lines[i].startswith(b'## ')), len(lines))
    assert sum(line.startswith(prefix) for line in lines[matches[0]:end]) == 1, needle
assert b''.join(line for line in lines if not line.startswith(prefix)) == original(ledger)
for path in ['AGENTS.md', 'CLAUDE.md', '.github/copilot-instructions.md',
             'docs/launch-readiness.md', '_bmad-output/implementation-artifacts/sprint-status.yaml']:
    assert Path(path).read_bytes() == original(path), path
assert Path('AGENTS.md').read_bytes() == Path('CLAUDE.md').read_bytes() == Path('.github/copilot-instructions.md').read_bytes()
readiness = Path('docs/launch-readiness.md').read_text().split('## Historical Receipts')[0]
assert 'Overall release decision: **FAIL**' in readiness
assert 'Package currency separately remains **CONCERNS**' in readiness
acceptance = json.loads(Path('_bmad-output/implementation-artifacts/evidence/5-1/20261020-final-gate/story-acceptance.json').read_text())
assert acceptance['decision'] == 'accepted' and acceptance['release_authorized'] is False
assert acceptance['release_decision'] == 'FAIL'
dependency = 'references/Hexalith.AI.Tools'
revision = '3f194e17174994d308ec84af9ee2b5aa68674d0d'
for tree in [baseline, 'HEAD']:
    assert subprocess.check_output(['git', 'rev-parse', f'{tree}:{dependency}']).decode().strip() == revision
assert subprocess.check_output(['git', '-C', dependency, 'rev-parse', 'HEAD']).decode().strip() == revision
git_rule = Path(dependency, 'hexalith-git-instructions.md').read_text()
committed_rule = subprocess.check_output(['git', '-C', dependency, 'show', f'{revision}:hexalith-git-instructions.md']).decode().replace('\r\n', '\n')
assert git_rule == committed_rule
assert 'Work on the **`main`** branch by default.' in git_rule
for document in [spec, report]:
    parent = Path(document).parent
    for target in re.findall(r'\]\(([^)]+)\)', Path(document).read_text()):
        path, _, anchor = target.partition('#')
        linked = (parent / path).resolve() if path else Path(document).resolve()
        assert linked.is_file(), target
        if anchor:
            headings = re.findall(r'(?m)^#{1,6} (.+)$', linked.read_text())
            slugs = [re.sub(r'\s', '-', re.sub(r'[^\w\s-]', '', heading.lower())) for heading in headings]
            assert anchor in slugs, target
print('PASS: five dispositions; original ledger, guidance, tracking and verdict preserved; scope and links valid')
PY
```

Independent Blind Hunter review returned five findings. The review exposed
the distinction between manual wording examples and an explicit authoring
convention; the section above now records the convention and the earlier open
handoffs it supersedes. The other corrections name the actual canonical guidance
disposition, verify the local user-edit hash and staged exclusion, and reproduce
the AI.Tools gitlink/checkout/committed-text comparison. No new runtime work is
deferred by this increment.
