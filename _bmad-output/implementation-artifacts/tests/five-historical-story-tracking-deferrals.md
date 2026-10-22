# Five historical Story 3.6 tracking dispositions — 2026-10-22

Inspection baseline: Timesheets `ffb169221ba4bdd85d9e60b2ab5e914f36f0e9c5`,
clean `main`; EventStore gitlink and checkout
`5ce5f1d84b88c808cc8a526b9decf149de977e55`.
The [build spec](../spec-close-five-historical-story-tracking-deferrals.md)
and [ledger](../deferred-work.md) record this documentation reconciliation.

Before commit, a fresh fetch found two upstream commits ending at
`c315b6992fb48a68fa7d187282f497df092762da`. They remove the Works package
override from Directory.Packages.props and do not touch this reconciliation's
files or cited host/tracking sources. Main was fast-forwarded while preserving
the staged documentation. Final integration checks use that upstream revision
as the change-scope base; original-ledger preservation remains pinned to the
initial inspection revision above.

Five historical ledger records receive additive dispositions. Four tracking
requests are closed or superseded by existing corrections; the fifth retires
only the obsolete no-authentication diagnosis and explicitly hands off the
still-open production identity requirement. These are overlapping historical
records, not five new runtime implementations or five acceptance gates passed.

| Original record (baseline line) | Evidence and disposition |
| --- | --- |
| Story artifact and agent context are stale (82) | The story labels its Completion Notes as historical and expressly supersedes the unwired-index and domain-wide catalog-read observations. Its cumulative File List includes every one of the ten source/test additions in the original implementation commit below. Managed guidance already records host projection registration and unfinished acceptance. Close the historical drift request. |
| Sprint-status header count disagrees with the story (124) | The sprint header no longer publishes the obsolete 2/5/3 count. The story's dated October 4 reconciliation records 37 historical dispositions: 28 implemented/superseded and nine deferred. Checked Defer rows mean triage, not implementation completion. Close this obsolete count finding without treating 37 as the current backlog size. |
| Synchronize Story 3.6 tracking and three entry points (142–144) | Historical labels, explicit supersession, the cumulative inventory, reconciled review dispositions, corrected sprint header and synchronized guidance already address the listed drift. Close the tracking request. Its old claim that synchronization alone permits story completion is superseded: story and sprint remain in-progress for current acceptance requirements. |
| Rewrite historical Completion Notes (293–295) | The existing historical label and September 16 supersession explicitly preserve the original review record while correcting the implementation diagnoses. Supersede this duplicate rewrite request; preserve provenance instead of rewriting old notes. |
| Register authentication for the trusted-context accessor (322–324) | SDK registration now supplies workload authentication, and the HTTP host runs authentication and authorization middleware. Supersede only the no-scheme/no-middleware diagnosis. The existing [canonical production-identity request](../deferred-work.md#deferred-from-public-route-compatibility-review-2026-10-09), “Supply trusted tenant and actor identity to production magic-link management routes,” remains open. |

The tracking evidence is in [Story 3.6](../3-6-implement-eventstore-backed-magic-link-state-loading.md):
“Current Review Disposition,” the 37-finding reconciliation, “Completion Notes
List,” “File List,” and “2026-09-16 Supersession and Policy Clarification.”
The [sprint record](../sprint-status.yaml) agrees with the story's in-progress
status. The supersession notice's old “ready for review” sentence is dated
September 16 history, not the current status source.

The inventory check is deliberately bounded to the original implementation
commit `c2f68e5ae34638f87c0e0c26ab9771f65d4cb81c`. It added ten source/test
files, each listed in the current cumulative File List; the original review's
“eight” wording is preserved. This does not certify the entire inventory of
all subsequent Story 3.6 changes. The existing
[agent-guidance reconciliation](../spec-close-five-agent-guidance-carry-forwards.md)
records the delivered guidance correction, originally made in Story 5.1 commit
`f82c446f62d9645735989c8fa2c12257dff4a8c9`. The current
[AGENTS.md](../../../AGENTS.md), [CLAUDE.md](../../../CLAUDE.md) and
[Copilot entry point](../../../.github/copilot-instructions.md) agree;
`LaunchReadinessTests.Managed_entry_points_match_configuration_and_projection_wiring`
in the existing [fitness suite](../../../tests/Hexalith.Timesheets.ArchitectureTests/FitnessTests/LaunchReadinessTests.cs)
checks normalized guidance equality, the configured SDK version, and required
wording. Actual projection registration was verified separately by inspecting
HTTP Program.cs; that fitness test does not inspect its source.

The authentication evidence is [HTTP Program.cs](../../../src/Hexalith.Timesheets/Program.cs),
the pinned SDK's
[registration extension](../../../references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs)
and [security extension](../../../references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceSecurityExtensions.cs).
For readers without initialized submodules, the same committed sources are
available through pinned upstream links:
[registration at the EventStore gitlink](https://github.com/Hexalith/Hexalith.EventStore/blob/5ce5f1d84b88c808cc8a526b9decf149de977e55/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs)
and [security at the EventStore gitlink](https://github.com/Hexalith/Hexalith.EventStore/blob/5ce5f1d84b88c808cc8a526b9decf149de977e55/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceSecurityExtensions.cs).
`AddEventStoreDomainService` calls `AddEventStoreDomainServiceSecurity`, which
registers workload/sidecar schemes, route policies and the workload fallback;
the host calls `UseAuthentication` and `UseAuthorization`.
The existing [HTTP boundary tests](../../../tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs)
reject missing/forged projection credentials and exercise the real trusted
context accessor with fixture-provided claims. The selected
[internal-surface theory](../../../tests/Hexalith.Timesheets.IntegrationTests/InternalSurfaceGuardTests.cs)
requires workload credentials on all three management routes.

The accessor success cases use `MagicLinkFixtureClaimsTransformation`, a
scripted access guard and gateway; they establish in-process claim mapping,
not production tenant/Party issuance or authorization. The host's opt-in
Development fixture likewise supplies no deployed identity evidence.
[Launch readiness](../../../docs/launch-readiness.md) retains overall **FAIL**;
deployed authority, inventory/continuity/shared-rebuild acceptance and current
NuGet-orchestration runtime qualification remain open. The existing open
shape-layering and configurable gateway/options work also remains unchanged.
No Aspire/Dapr topology was started or stopped for this documentation-only
verification. The HTTP tests start in-process WebApplicationFactory hosts.

Verification uses SDK 10.0.402, matching Debug/source restore and build graphs,
and individual executable test selections:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method '*InternalProjectionRouteRejectsMissingAndForgedCredentials' -method '*AdminStateLoadsUseTheHostsHttpClaimsAccessor' -method '*Magic_link_management_routes_require_workload_credentials'
git diff --check c315b6992fb48a68fa7d187282f497df092762da
```

Run the whitespace command after staging exactly the three intended documents
so it includes the two new files; this fixed-baseline form also works after
commit. Only those documentation files are staged for this task.

At the initial inspection revision, all commands exited 0. Restore passed;
the solution build completed in
15.51 seconds with zero warnings/errors. Architecture passed 109/109 and
the selected HTTP cases passed 9/9: 118 distinct passing cases, no failures,
errors, skips or not-run cases. This is focused verification, not a full
six-suite run or runtime/deployed acceptance. Temporary captures are
`/tmp/timesheets-story-tracking-{restore,build,architecture,http}.log`;
the portable runner summaries are retained here:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:15.51
Hexalith.Timesheets.ArchitectureTests  Total: 109, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 39.506s
Hexalith.Timesheets.IntegrationTests  Total: 9, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 9.161s
```

At integration revision `c315b6992fb48a68fa7d187282f497df092762da`, the same
restore/build and executable selections passed again, all exits 0. The build
completed in 34.98 seconds with zero warnings/errors; the same 118 distinct
cases passed with no failures/errors/skips/not-run. Captures are
`/tmp/timesheets-story-tracking-integrated-{restore,build,architecture,http}.log`.
Portable integrated summaries:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:34.98
Hexalith.Timesheets.ArchitectureTests  Total: 109, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 28.234s
Hexalith.Timesheets.IntegrationTests  Total: 9, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 4.526s
```

The preservation/source/link/scope check below also passed. Independent
review and commit/push checks are recorded in the build spec on completion.

Run this read-only source/preservation check from the repository root. Its
fixed baseline supports reproduction after commit. It verifies exactly one
note at each intended record, preservation of every original ledger byte,
unchanged tracking/guidance/source, the original added-file inventory and
local report links:

```bash
python3 - <<'PY'
from pathlib import Path
import re, subprocess

baseline = 'ffb169221ba4bdd85d9e60b2ab5e914f36f0e9c5'
integration = 'c315b6992fb48a68fa7d187282f497df092762da'
def original(path):
    return subprocess.check_output(['git', 'show', f'{baseline}:{path}'])

root = Path('_bmad-output/implementation-artifacts')
spec = 'spec-close-five-historical-story-tracking-deferrals.md'
ledger = root / 'deferred-work.md'
lines = ledger.read_bytes().splitlines(keepends=True)
def added(line):
    return line.startswith(b'  resolution_note: ') and spec.encode() in line
assert sum(map(added, lines)) == 5
assert b''.join(line for line in lines if not added(line)) == original(ledger)
anchors = [
    (b'- Story artifact and agent context are stale after this change:', 1),
    (b'- **Sprint-status header count disagrees with the story.**', 1),
    (b'  summary: Synchronize Story 3.6 tracking artifacts and the three agent-context entry points with the delivered implementation.', 2),
    (b'  summary: Rewrite historical Story 3.6 Completion Notes that still describe the token-hash index as unwired.', 2),
    (b"  summary: Register an authentication scheme so the host's trusted-context accessor can receive tenant and actor claims in a deployment.", 2),
]
for anchor, offset in anchors:
    matches = [i for i, line in enumerate(lines) if line.startswith(anchor)]
    assert len(matches) == 1, anchor
    assert added(lines[matches[0] + offset]), anchor

story = root / '3-6-implement-eventstore-backed-magic-link-state-loading.md'
sprint = root / 'sprint-status.yaml'
entry_points = ['AGENTS.md', 'CLAUDE.md', '.github/copilot-instructions.md']
for path in [story, sprint, 'docs/launch-readiness.md', 'src/Hexalith.Timesheets/Program.cs', *entry_points]:
    assert Path(path).read_bytes() == original(path), path
assert len({Path(path).read_bytes() for path in entry_points}) == 1
text = story.read_text()
assert 'Status: in-progress' in text
assert '3-6-implement-eventstore-backed-magic-link-state-loading: in-progress' in sprint.read_text()
assert '2 decisions resolved and applied, 5 patch action items open, 3 deferred' not in sprint.read_text()
assert 'Historical initial-delivery notes.' in text
assert '37 historical unchecked review patches below have been reconciled individually: 28' in text
assert not re.search(r'^\s*- \[ \] \[Review\]', text, re.M)
inventory = text.split('### File List\n', 1)[1].split('\n## Change Log', 1)[0]
implementation = 'c2f68e5ae34638f87c0e0c26ab9771f65d4cb81c'
files = subprocess.check_output(['git', 'show', '--format=', '--diff-filter=A', '--name-only', implementation, '--', 'src', 'tests'], text=True).splitlines()
assert len(files) == 10 and all(path in inventory for path in files)

pin = '5ce5f1d84b88c808cc8a526b9decf149de977e55'
for name in ['EventStoreDomainServiceExtensions.cs', 'EventStoreDomainServiceSecurityExtensions.cs']:
    path = f'src/Hexalith.EventStore.DomainService/{name}'
    committed = subprocess.check_output(['git', '-C', 'references/Hexalith.EventStore', 'show', f'{pin}:{path}'])
    assert (Path('references/Hexalith.EventStore') / path).read_bytes() == committed
report = root / 'tests/five-historical-story-tracking-deferrals.md'
for target in re.findall(r'\]\(([^)]+)\)', report.read_text()):
    if target.startswith('https://'):
        assert target.startswith(f'https://github.com/Hexalith/Hexalith.EventStore/blob/{pin}/')
        source = target.split(f'/blob/{pin}/', 1)[1]
        assert (Path('references/Hexalith.EventStore') / source).is_file(), target
        continue
    assert (report.parent / target.split('#', 1)[0]).resolve().is_file(), target
section = '\n## Deferred from: public-route compatibility review (2026-10-09)\n'
canonical = ledger.read_text().split(section, 1)[1].split('\n## ', 1)[0]
identity = canonical.split('  summary: Supply trusted tenant and actor identity to production magic-link management routes.\n', 1)[1].split('\n- source_spec:', 1)[0]
assert 'resolution_note:' not in identity
assert '../deferred-work.md#deferred-from-public-route-compatibility-review-2026-10-09' in report.read_text()
allowed = {str(ledger), str(root / spec), str(report)}
changed = set(subprocess.check_output(['git', 'diff', '--name-only', integration], text=True).splitlines())
changed.update(subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard'], text=True).splitlines())
assert changed == allowed, changed
print('PASS: five adjacent dispositions; original ledger/tracking/guidance preserved; inventory, SDK source, links and scope verified')
PY
```
