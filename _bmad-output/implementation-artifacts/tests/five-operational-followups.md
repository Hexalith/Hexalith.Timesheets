# Five duplicate operational follow-ups

Inspected on 2026-10-23 against Timesheets main
`5a82cdc7f457347b9cd89f1947678e53dea58404`.
The [build spec](../spec-consolidate-five-operational-followups.md) owns this
documentation-only increment. Five duplicate ledger entries receive supersession
notes, and their canonical open inventory task receives a scope addendum. These dispositions consolidate duplicate tracking; the
underlying requirements remain unfinished. No runtime fix, deployed acceptance,
new inventory, migration execution or release authorization is claimed.

| Superseded entry | Canonical open successor and retained requirement |
| --- | --- |
| [October 5 pre-handler history/bootstrap question](../deferred-work.md#deferred-from-code-review-of-3-6-implement-eventstore-backed-magic-link-state-loadingmd-2026-10-05-2), summary “Establish how the token-hash index and tenant catalog are populated for histories that predate handler deployment.” | [October 11 deployed inventory/rebuild task](../deferred-work.md#deferred-from-local-aspiredapr-topology-review-2026-10-11), summary “Inventory deployed read-model components and rebuild or migrate prior magic-link index and catalog data before deployment.” Its additive followup_note retains histories predating handler deployment even when no read-model row exists. Populate and verify **both** the token-hash index and tenant catalog before admitting traffic, and document the chosen replay/rebuild/migration procedure and its verification in the deployment runbook. Automatic historical replay is not established. This consolidates the question into that still-open operational requirement, without declaring bootstrap or the runbook delivered. |
| [October 11 persisted-journey cross-request retry finding](../deferred-work.md#deferred-from-story-36-local-persisted-journey-review-2026-10-11) | [October 10 spec-19 canonical task](../deferred-work.md#deferred-from-story-36-review-of-polling-and-projection-scope-2026-10-10), same summary “Reconcile an uncertain committed magic-link use across separate HTTP retries.” A new HTTP trace identity can yield a new command identity after a committed opaque outcome. Define safe idempotency/status reconciliation without revealing capability state; within-request retries and replay denial do not settle this. |
| [October 11 persisted-journey deployed wiring finding](../deferred-work.md#deferred-from-story-36-local-persisted-journey-review-2026-10-11), summary “Verify deployed read-model component wiring and migration independently of the opt-in local journey.” | The same [October 11 deployed inventory/rebuild task](../deferred-work.md#deferred-from-local-aspiredapr-topology-review-2026-10-11). Inspect actual deployed component wiring and prior `statestore` content in each applicable hosting mode. Local `timesheets-readmodels` wiring and a disposable journey do not prove installed wiring or migration. |
| [October 18 diagnostic-redaction scope-selection wrapper](../deferred-work.md#deferred-from-diagnostic-credential-redaction-scope-selection-2026-10-18), summary “Address deferred projection storage and migration hardening in separate increments.” | The specific open tasks in the coverage list below replace this generic scheduling wrapper. Its source spec explicitly leaves projection/migration hardening separate and adds no extra acceptance criteria. |
| [October 18 restart-timeout scope-selection wrapper](../deferred-work.md#deferred-from-eventstore-restart-timeout-scope-selection-2026-10-18), summary “Address projection storage and migration hardening in separate increments.” | The same specific open tasks below retain deployed inventory, authorization, storage cleanup and their own evidence requirements. Neither completed restart repair nor this consolidation implements them. |

## Coverage retained by the generic wrappers

The following are explicit open requirements, not completed work. Each row gives
the ledger's exact source_spec value and summary; the first predates that format
and is identified by its original bullet instead. Section links locate those
identities without changing the original ledger headings.

| Source identity | Exact canonical summary or original bullet | Retained requirement |
| --- | --- | --- |
| [September 13 raw observation](../deferred-work.md#deferred-from-code-review-of-3-6-implement-eventstore-backed-magic-link-state-loading-2026-09-13), no source_spec field | Original bullet begins “The tenant-wide Activity Type catalog is persisted under the magic-link-scoped key” | Persisted catalog-key migration concern. |
| [October 5](../deferred-work.md#deferred-from-code-review-of-3-6-implement-eventstore-backed-magic-link-state-loadingmd-2026-10-05-2), `_bmad-output/implementation-artifacts/3-6-implement-eventstore-backed-magic-link-state-loading.md` | Confirm that unfenced shared rebuilds cannot drop live index or catalog writes made between aggregate enumeration and FinalizeAsync. | Catalog concurrency safety; index epochs do not settle the catalog's non-epoch path. |
| [October 11 topology](../deferred-work.md#deferred-from-local-aspiredapr-topology-review-2026-10-11), `_bmad-output/implementation-artifacts/spec-3-6-local-aspire-dapr-topology.md` | Inventory deployed read-model components and rebuild or migrate prior magic-link index and catalog data before deployment. | Installed wiring, prior data, full pre-handler-history coverage, both projections before traffic, and documented initialization/verification procedure. |
| [October 11 continuity](../deferred-work.md#deferred-from-story-36-local-persisted-journey-review-2026-10-11), `spec-3-6-migrate-v1-magic-link-index-to-v7.md` | Establish independently verified legacy owner delivery continuity and pending-journal recovery before enabling the v1 migration in a real environment. | Verified owner provenance, source coverage and pending-journal recovery; candidate rows cannot manufacture owner checkpoints. |
| [October 13 materialization](../deferred-work.md#deferred-from-final-review-of-story-36-materialization-2026-10-13), `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md` | Verify whether any deployed direct legacy shard must be rejected before v7 bootstrap. | Inspect deployed layouts or historical writers before deciding admission. |
| [October 13 materialization](../deferred-work.md#deferred-from-final-review-of-story-36-materialization-2026-10-13), `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md` | Verify sealed v6 migration resume against a changed source or damaged staged root. | Source/staged-root fault verification, preserving sealed-stage safety. |
| [October 13 materialization](../deferred-work.md#deferred-from-final-review-of-story-36-materialization-2026-10-13), `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md` | Bind deployed migration authorization to the intended operator as well as its certified operation. | Qualified provider and ingress operator policy before enabling migration. |
| [October 13 materialization](../deferred-work.md#deferred-from-final-review-of-story-36-materialization-2026-10-13), `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md` | Retire obsolete same-generation capability projection fragments after repeated large-view updates. | Reclaim unreachable prior-digest fragments with appropriate storage evidence. |
| [October 13 materialization](../deferred-work.md#deferred-from-final-review-of-story-36-materialization-2026-10-13), `_bmad-output/implementation-artifacts/spec-3-6-materialize-magic-link-read-models.md` | Establish subscriber-driven projection delivery independently of manual mapped-route dispatch. | Installed subscriber delivery/shared-rebuild proof; manual local delivery is insufficient. |
| [October 22 diagnostic](../deferred-work.md#deferred-from-review-of-five-magic-link-operability-closures-2026-10-22), `_bmad-output/implementation-artifacts/spec-close-five-magic-link-operability-deferrals.md` | Assess complete SDK typed epoch and scope-boundary validation for the local selected-index diagnostic. | Authoritative validation assessment; JSON checks do not certify concurrent admission or production HTTP resolution. |

## Evidence boundary

The [deployment prerequisite checklist](../../../docs/deployed-magic-link-acceptance.md#platform-source-findings-and-outstanding-prerequisites)
and [later production/legacy prerequisites](../../../docs/deployed-magic-link-2026-10-17.md#production-resource-binding-capability-and-legacy-prerequisites)
require qualified installed wiring, immutable full historical coverage, authority,
enforced maintenance, owner continuity and deployed subscriber/shared-rebuild
proof. They are prerequisite checklists, not a qualified executable deployment
runbook. Read the preserved [October 22 continuation](../spec-3-6-deployment-authority-runtime-continuation.md#local-resumption--2026-10-22)
with the older observations: the prepared handoff remains blocked and earlier
authority qualification was withdrawn. No native or remote deployment observation
was repeated for this increment.

Current [index handler](../../../src/Hexalith.Timesheets.Projections/MagicLinks/MagicLinkTokenHashCapabilityIndexProjectionHandler.cs)
legacy-root checks and streamed rebuild admission refuse unqualified legacy
state. The [catalog handler](../../../src/Hexalith.Timesheets.Projections/ActivityTypes/TenantActivityTypeCatalogProjectionHandler.cs)
has full-tenant candidate/rebuild seams; their existence does not establish
automatic historical replay or installed rebuild execution.
The [verified inventory contract](../../../src/Hexalith.Timesheets.Projections/MagicLinks/IMagicLinkV1VerifiedInventoryProvider.cs)
requires independent complete coverage and enforced quiescence; its
[unavailable default](../../../src/Hexalith.Timesheets.Projections/MagicLinks/UnavailableMagicLinkV1VerifiedInventoryProvider.cs)
provides no admission. [Local proof](../../../docs/local-magic-link-dapr-proof.md)
expressly retains manual projection delivery and unproved deployed acceptance.

[Launch readiness](../../../docs/launch-readiness.md) retains release **FAIL**
and separate package **CONCERNS**. Story/sprint status, fail-closed defaults,
source/configuration, evidence and dependency gitlinks are unchanged.

## Verification

Use the existing architecture checks for launch classification, unfinished
acceptance, tracking and managed guidance. This change adds no executable
behavior, so no new tests or AppHost lifecycle operation is needed.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests
git diff --check
```

The preservation check below works against the investigation baseline before or
after commit. It requires exactly the three owned documents, five supersession
notes, one open-task followup_note, unchanged original ledger bytes and unchanged
user content. The selected source/summary pairs ensure the earlier canonical
retry task stays open. Local link targets and unique heading anchors are checked
below. After staging, invoke this check with `VERIFY_STAGED=1` to require all three
staged files and their exact equality with the reviewed working-tree bytes.

```bash
python3 - <<'PY'
from pathlib import Path
import hashlib, os, re, subprocess

baseline = '5a82cdc7f457347b9cd89f1947678e53dea58404'
ledger = '_bmad-output/implementation-artifacts/deferred-work.md'
spec = '_bmad-output/implementation-artifacts/spec-consolidate-five-operational-followups.md'
report = '_bmad-output/implementation-artifacts/tests/five-operational-followups.md'
user_path = '_bmad-output/implementation-artifacts/spec-3-6-deployment-authority-runtime-continuation.md'
owned = {ledger, spec, report}
def original(path):
    return subprocess.check_output(['git', 'show', f'{baseline}:{path}'])
changed = set(subprocess.check_output(['git', 'diff', '--name-only', baseline, '--']).decode().splitlines())
untracked = set(subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard']).decode().splitlines())
assert changed | untracked == owned, changed | untracked
staged = set(subprocess.check_output(['git', 'diff', '--cached', '--name-only']).decode().splitlines())
assert staged <= owned and user_path not in staged, staged
if os.environ.get('VERIFY_STAGED') == '1':
    assert staged == owned, staged
    for path in owned:
        assert subprocess.check_output(['git', 'show', f':{path}']) == Path(path).read_bytes(), path
assert Path(user_path).read_bytes() == original(user_path)
assert hashlib.sha256(Path(user_path).read_bytes()).hexdigest() == '44781ce44c014f4de846211a65a3b01f3b4be63d9db11505659fbd315f037d06'
prefix = b'  resolution_note: Superseded 2026-10-23 by tracking consolidation in `spec-consolidate-five-operational-followups.md`.'
lines = Path(ledger).read_bytes().splitlines(keepends=True)
assert sum(line.startswith(prefix) for line in lines) == 5
followup = b'  followup_note: Open acceptance retained 2026-10-23 by `spec-consolidate-five-operational-followups.md`.'
assert sum(line.startswith(followup) for line in lines) == 1
assert b''.join(line for line in lines if not line.startswith((prefix, followup))) == original(ledger)
sources = '_bmad-output/implementation-artifacts/'
expected = [
    (sources + '3-6-implement-eventstore-backed-magic-link-state-loading.md',
     'Establish how the token-hash index and tenant catalog are populated for histories that predate handler deployment.'),
    (sources + 'spec-3-6-implement-eventstore-backed-magic-link-state-loading-20.md',
     'Reconcile an uncertain committed magic-link use across separate HTTP retries.'),
    (sources + 'spec-3-6-implement-eventstore-backed-magic-link-state-loading-20.md',
     'Verify deployed read-model component wiring and migration independently of the opt-in local journey.'),
    ('none', 'Address deferred projection storage and migration hardening in separate increments.'),
    ('none', 'Address projection storage and migration hardening in separate increments.'),
]
selected = []
for block in Path(ledger).read_text().split('- source_spec:')[1:]:
    if followup.decode() in block:
        assert block.splitlines()[0].strip().strip('`') == sources + 'spec-3-6-local-aspire-dapr-topology.md'
        assert '  summary: Inventory deployed read-model components and rebuild or migrate prior magic-link index and catalog data before deployment.\n' in block
        assert 'resolution_note:' not in block.split('\n## ')[0]
    if prefix.decode() in block:
        summary = next(line.strip().removeprefix('summary: ') for line in block.splitlines() if line.strip().startswith('summary:'))
        source = block.splitlines()[0].strip().strip('`')
        selected.append((source, summary))
assert sorted(selected) == sorted(expected), selected
for path in owned:
    raw = Path(path).read_bytes()
    assert raw.endswith(b'\n') and b'\r' not in raw, path
    assert all(line == line.rstrip(b' \t') for line in raw.splitlines()), path
for document in [spec, report]:
    for target in re.findall(r'\]\(([^)]+)\)', Path(document).read_text()):
        path, _, anchor = target.partition('#')
        linked = (Path(document).parent / path).resolve() if path else Path(document).resolve()
        assert linked.is_file(), target
        if anchor:
            counts, slugs = {}, []
            for heading in re.findall(r'(?m)^#{1,6} (.+)$', linked.read_text()):
                slug = re.sub(r'\s', '-', re.sub(r'[^\w\s-]', '', heading.lower()))
                n = counts.get(slug, 0)
                counts[slug] = n + 1
                slugs.append(slug + (f'-{n}' if n else ''))
            assert anchor in slugs, target
print('PASS: five duplicate dispositions; originals preserved; scope and local links valid')
PY
```

Restore and build exited 0; the build completed with zero warnings/errors in
1.93 s. The complete architecture executable passed **109/109**, with zero
errors, failures, skips or not-run tests, in **16.876 s**. The exact preservation,
scope and link check above and `git diff --check` also passed. No full solution
build, other suite, package audit, AppHost operation or deployed operation was
run for this documentation-only increment. A [compact receipt](#verification-receipt)
retains the observed outputs and source identity.

## Verification receipt

The retained command output below records the observed
source revision `5a82cdc7f457347b9cd89f1947678e53dea58404` plus the three owned
documentation changes, under SDK 10.0.402. The source/configuration tested by the
architecture executable is unchanged by review corrections. Exit codes for
restore, build, suite, preservation/link verification and whitespace were all 0.

```text
restore:
  Determining projects to restore...
  All projects are up-to-date for restore.

build:
  Hexalith.Timesheets.ArchitectureTests -> /home/administrator/projects/hexalith/timesheets/tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.93

suite:
=== TEST EXECUTION SUMMARY ===
   Hexalith.Timesheets.ArchitectureTests  Total: 109, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 16.876s

preservation/link verification:
PASS: five duplicate dispositions; originals preserved; scope and local links valid
```
