# Five completed implementation follow-ups — 2026-10-22

Investigation and source-verification baseline: clean `main` at
`622ee40e0df72404f60ee57da06e2963e665749d`. This change reconciles exactly five
ledger entries with implementations already delivered. It adds no runtime fix.
The three tooling carry-forwards overlap the same canonical fixes; the five
closures do not represent five new implementations.

| Selected deferred summary | Current disposition and evidence |
| --- | --- |
| Implement durable, atomic magic-link confirmation and adjustment persistence for unfinished Story 3.3/3.4 behavior | Delivered by [the durable submission spec](../spec-3-6-durable-magic-link-submission.md). `MagicLinkEventStoreDomainProcessor` emits use plus matching Time Entry effect in one owner-stream result. `MagicLinkDurableSubmissionService` requires `Completed` and exact stored-batch identity, sequence and command attribution. Current HTTP tests inspect stored confirm/adjust events, enforce one accepted concurrent confirmation and one persisted effect, reject replay and incomplete/incorrect receipts, and recover an exact batch after response loss. The real `AggregateActor` is recreated over `InMemoryStateManager` in four confirm/adjust/rejection cases; event history survives and replay cannot append another use/effect. Completed status and its two-event count are asserted before the final recreation; status-store restart durability is not tested. The existing `resolution_2026_10_10` field is preserved and supplemented with a standard resolution note. |
| The deferred cross-tenant checkpoint freshness gap is closed across Timesheets projection folds | Already delivered by [checkpoint tenant isolation](../spec-enforce-projection-checkpoint-tenant-scope.md). `TimesheetsProjectionCheckpoint.ValidateTenant` uses ordinal equality at all ten affected fold entry points. Tests cover missing/blank/foreign/case-different checkpoint tenants and all freshness states; explicit-tenant paths reject before enumerating history, errors omit identifiers, incremental refusal preserves the verified prefix, and capability checks use the selected issuance. Matching-tenant freshness and export behavior remain positive controls. |
| Address deferred BMAD hook and test-coverage maintenance in separate increments | Delivered by the canonical hook fixes below and [override regression coverage](../spec-pin-bmad-customization-override-behavior.md). [Tooling diagnostic follow-ups](../spec-close-five-tooling-diagnostic-deferrals.md) already connected the installed persona suites and both Claude mirrors to CI discovery. Current checks enforce mirror equality, installed Copilot persona wrappers, nonempty/non-all-skipped successful suites, and actual resolver behavior. |
| Harden the deferred BMAD hooks in a separate increment | [Filename confinement](../spec-confine-bmad-loop-event-filenames.md) rejects both separator forms, drive/stream syntax, NUL and empty/dot names before filesystem access. Existing regressions demonstrate traversal refusal, outside-file preservation, redirected-directory refusal, temporary-file conflict handling and complete normal events on the POSIX and forced path-based writers. [Local generated Codex hooks](../spec-make-codex-hooks-local.md) removed tracked machine-specific and deleted-automator commands. |
| Harden BMAD hooks and remove obsolete hook references in a separate increment | The same filename and local-hook fixes apply. [Malformed-input regression coverage](../spec-pin-bmad-loop-malformed-input-behavior.md) disproved the original crash diagnosis: `UnicodeDecodeError` is already caught through `ValueError`. Tests exercise strict UTF-8, surrogateescape, complete metadata, inactive sessions without stdin EOF and both event-directory channels. README documents per-checkout generation, adapter selection, trust setup and removal of the deleted automator registration. |

Current verification passed with SDK `10.0.402`. Both the imported catalog and
Works forwarding default select EventStore `3.126.0`; no historical version
override was applied. Restore and build used the same Debug/source graph:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

The build had zero warnings/errors. All six built test executables ran
individually; the two larger suites used the relevant filters recorded in the
receipts. These are selected regression results, not a full default-suite run.

| Current lane | Passed | Scope |
| --- | ---: | --- |
| ArchitectureTests | 109 | Complete suite |
| Contracts.Tests | 92 | Complete suite |
| Server.Tests | 624 | Complete suite, including durable submission |
| Projections.Tests | 174 | Seven affected checkpoint projection classes |
| Works.Tests | 76 | Complete suite |
| IntegrationTests | 24 | Nine persisted HTTP/actor methods, including theory cases |
| IntegrationTests receipt negatives | 61 | Five additional methods reject tampered use/effect fields, prior values and command attribution |
| Python tooling discovery | 88 | Complete `scripts/tests` discovery, including 15 hook tests and six tooling checks |

The .NET selections total **1,160 passed**, zero failures/errors/skips/not-run.
The Python tooling checks also execute 86 nested persona tests: party-mode 23 and
forge-idea 20 in each of `.agents` and `.claude`. Those nested executions are not
added to the 88-test discovery count. Each of the four persona suites also ran
directly after review, with its own retained output and 23/20 count, confirming
all 86 cases independently. The receipt records Python 3.14.4 and the Linux/WSL2
platform; forced path-based hook tests do not qualify native Windows execution.
Static checks confirm generated
`.codex/hooks.json` is untracked and ignored, `.codex/config.toml` is tracked,
README describes local setup/obsolete-hook removal, and CI requires Python 3.11+
before running the same tooling discovery.

[Retained receipts](../evidence/completed-followups/20261022-five-entries/verification.json)
record canonical source and gitlink identities, relevant input SHA-256 values,
exact argument vectors/environment settings, exit codes, counts and durations.
Adjacent compressed logs and xUnit XML retain the executed results. The original
`-xml` invocations remain exact historical receipts; the review follow-up and
reproduction use the runner's supported `-result-xml` option. To reproduce all
recorded lanes from the repository root, first require the recorded inputs and
dependency checkouts, then compare counts and outcomes as well as exit codes:

```bash
python3 - <<'PY'
import json
import hashlib
import os
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

receipt = Path('_bmad-output/implementation-artifacts/evidence/completed-followups/20261022-five-entries/verification.json')
evidence = json.loads(receipt.read_text())
assert sys.version_info >= (3, 11), 'Python 3.11+ is required'
subprocess.run(['git', 'merge-base', '--is-ancestor', evidence['baseline_commit'], 'HEAD'], check=True)
gitlinks = subprocess.check_output(['git', 'ls-tree', 'HEAD', 'references/'], text=True).splitlines()
assert gitlinks == evidence['root_gitlinks'], 'Root dependency pins differ'
for path, revision in evidence['dependency_checkouts'].items():
    actual = subprocess.check_output(['git', '-C', path, 'rev-parse', 'HEAD'], text=True).strip()
    assert actual == revision, 'Dependency checkout differs: ' + path
    assert not subprocess.check_output(['git', '-C', path, 'status', '--porcelain', '--untracked-files=no'], text=True).strip(), path
for path, expected in evidence['inputs_sha256'].items():
    assert hashlib.sha256(Path(path).read_bytes()).hexdigest() == expected, 'Input differs: ' + path
with tempfile.TemporaryDirectory(prefix='timesheets-followup-recheck-') as output:
    for check in evidence['checks']:
        argv = list(check['argv'])
        if '-xml' in argv:
            argv[argv.index('-xml')] = '-result-xml'
        if '-result-xml' in argv:
            xml = Path(output) / (check['name'] + '.xml')
            argv[argv.index('-result-xml') + 1] = str(xml)
        environment = {**os.environ, **check['environment']}
        environment.pop('TIMESHEETS_LIVE_DAPR', None)
        environment.pop('TIMESHEETS_PERF', None)
        result = subprocess.run(argv, env=environment, capture_output=True, text=True, check=True)
        if 'results' in check:
            assemblies = list(ET.parse(xml).getroot().iter('assembly'))
            actual = {key: sum(int(a.get(key, '0')) for a in assemblies) for key in check['results']}
            assert actual == check['results'], (check['name'], actual)
        if 'tests' in check:
            text = result.stdout + result.stderr
            count = re.search(r'Ran (\d+) tests? in ', text)
            outcome = re.search(r'(?m)^OK(?: \([^\n]+\))?$', text)
            assert count and int(count[1]) == check['tests'], check['name']
            assert outcome and outcome[0] == check['test_summary'], check['name']
        print(check['name'], 'passed with the recorded results')
PY
```

The verbatim reproduction block passed all 15 recorded commands after review,
including exact outcome/count comparisons and executable static/preservation
checks. Its completion status and compressed log are retained alongside the
original results. Independent review confirmed all ten corrections addressed,
with no concrete remaining finding and nothing newly deferred.

This is local regression evidence. The HTTP concurrency fixture serializes
commands in process; the actor fixture uses an in-memory state manager. Neither
proves distributed concurrency or deployed Dapr durability. No runtime was
started/stopped, no installed hook was regenerated, and no domain state,
dependency, gitlink, agent guidance or historical frozen spec changed.

Deployed authority/history inventory, shared-rebuild acceptance and current-graph
AppHost runtime qualification remain open. Cross-request uncertain-commit
reconciliation, readback-budget policy, direct legacy-shard admission, sealed
migration retry validation and obsolete-fragment cleanup retain their existing
open entries in [the deferred ledger](../deferred-work.md). In particular,
[the durable-submission review follow-up](../spec-3-6-implement-eventstore-backed-magic-link-state-loading-19.md)
owns the retained cross-request uncertain-commit and readback-budget requests;
[read-model materialization](../spec-3-6-materialize-magic-link-read-models.md)
owns the unfinished legacy-shard/migration/fragment-storage requests.
[Launch readiness](../../../docs/launch-readiness.md) retains deployed and
current-graph runtime gates. The independent PascalCase test naming and future-spec lifecycle
wording requests also remain open; the tooling closures reconcile the named
canonical hook/override-test work. Story 3.6 remains in-progress and launch
readiness remains **FAIL**.
