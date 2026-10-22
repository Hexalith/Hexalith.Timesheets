# Five historical host integration dispositions — 2026-10-22

Five historical [ledger](../deferred-work.md) entries now have explicit
dispositions across three already-delivered concerns. This is a documentation
reconciliation; no runtime implementation changed. The two repeated freshness
observations and two repeated missing-resource observations are identified as
carry-forwards rather than counted as separate runtime fixes.

Inspection baseline: Timesheets `63f89f96be8089f42bd37bcb4dca006454ca9549`.
Its committed gitlinks are EventStore
`5ce5f1d84b88c808cc8a526b9decf149de977e55`, Builds
`359fee24aeca4d9eabed4b425d4cfa2520b2adda`, and Works
`d60c29569de975bd280098e2e2f37406a30cf705`. The current verification below uses
this source tree with only the three reconciliation documents changed.

| Original entry at baseline | Disposition and evidence |
| --- | --- |
| Line 76, September 13 non-Fresh collapse | Closed as stale. The [loader](../../../src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs) validates catalog shape and retains only defined non-Fresh status internally. Items, cursor, timestamp and detail are cleared, and capability/Time Entry state is withheld. Missing or malformed catalogs stay unavailable. |
| Line 78, September 13 missing EventStore resource | Superseded missing-resource diagnosis. The [AppHost](../../../src/Hexalith.Timesheets.AppHost/Program.cs) declares the gateway and domain-module sidecar registration in run mode, with the internal app port and generated credentials. This observation does not establish publish-mode topology. |
| Line 81, September 13 anonymous internal routes | Closed as stale. The [HTTP host](../../../src/Hexalith.Timesheets/Program.cs) registers the SDK and invokes authentication/authorization plus the [internal listener guard](../../../src/Hexalith.Timesheets/Runtime/InternalSurfaceGuard.cs). Credential refusal, public-listener refusal and authenticated mapped delivery have in-process HTTP proof, including stored-model assertions. |
| Lines 192–194, September 15 wire/prove EventStore resource | Duplicate missing-resource diagnosis superseded by delivered run-mode wiring and historical isolated persistence proof. The surviving runtime-proof requirement remains with the canonical open AppHost smoke and NuGet-backed orchestration entries; it is not closed by this reconciliation. |
| Line 210, September 15 repeated non-Fresh collapse | Closed as stale, like line 76. Both are superseded by the already-resolved canonical “Preserve explicit non-Fresh catalog state only when the no-disclosure bundle can represent it safely” entry. Existing HTTP tests retain opaque denial and reveal no freshness category publicly. |

The [canonical freshness disposition](../deferred-work.md#deferred-from-code-review-of-3-6-implement-eventstore-backed-magic-link-state-loading-2026-09-15)
has its completed [spec](../spec-close-five-runtime-correctness-carry-forwards.md)
and [verification report](five-runtime-correctness-carry-forwards.md).

The route-wide security conclusion also uses source evidence from the pinned
EventStore revision above. Its [operational route catalog](../../../references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceRoutes.cs)
binds process, replay, query, projection, rebuild and operational metadata to
workload operations/policies. The [SDK endpoint mappings](../../../references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs)
call `RequireEventStoreDomainServicePolicy`, including their rebuild helpers;
the [security extension](../../../references/Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceSecurityExtensions.cs)
resolves those policies to `RequireAuthorization`, registers the workload and
sidecar schemes, and installs startup validation. These three files match the
committed EventStore gitlink. The selected HTTP credential test exercises
`/project/v2`; the guard suite checks public/unconfigured-listener refusal for
multiple protected routes. It does not individually exercise authenticated
credential admission for every route in the SDK catalog.

The source-history provenance is explicit: commit
`aa57e591780404f779f87ed72806c6acf41aee40` already contains status-only catalog
handling and the withheld-authority token bundle; commit
`95804f1cb486c7cb6709fbb81ccdea10368be995` already declares the local gateway,
domain module and generated app-channel tokens; commit
`b5000b02e061e5cd8b85a1d2bba4cb393c58a060` introduced the internal surface
guard; commit `d6b337b423367927b2ea7b212214c335fb477171` added the host's explicit
authentication/authorization calls while restoring the public capability routes.
Inspect these full identifiers with `git show` and the paths linked above.

The completed [local topology spec](../spec-3-6-local-aspire-dapr-topology.md)
and retained [October 18 final receipt](../evidence/3-6/20261018-direct-eventstore-local-proof/post-review/verification.json)
record historical local persistence proof: 1 passed, zero failures/errors/skips,
exit 0, 131.536 seconds, direct DCP ownership, replaced identity, shared-state
preservation and complete fixture cleanup. That receipt belongs to the earlier
AppHost SDK `13.5.3` CLI-bundle graph, with an explicit EventStore `3.125.1`
source-build override. Current verification uses AppHost SDK `13.6.1` with
NuGet-backed orchestration and EventStore `3.126.0`. The tested historical
worktree was based on Timesheets
`72d9fdaa813daf9d39d296e268fd2e1086a8dd87` and EventStore
`d55c6f7c11e9bce6f17d490a84c83f1561979d63`, with implementation changes then
uncommitted; later publication identifiers do not replace those tested source
identities, as recorded in its [source-context README](../evidence/3-6/20261018-direct-eventstore-local-proof/README.md).
No new live journey was run for this documentation correction.

The ledger's canonical [automated AppHost runtime smoke](../deferred-work.md#deferred-from-build-review-of-story-36-review-evidence-increment-2026-09-17)
and [NuGet-backed current-graph qualification](../deferred-work.md#deferred-from-code-review-of-spec-5-2-reconcile-package-currency-and-platform-dependency-versions-2026-10-18)
entries remain open. Production tenant/Party identity, operator authority,
deployed component inventory/migration, subscriber delivery, and certified
shared-rebuild acceptance also remain open. The [launch decision](../../../docs/launch-readiness.md)
remains **FAIL**; local and in-process proof does not establish deployment
acceptance or authorize changing fail-closed defaults.

Run the existing verification from the repository root:

```bash
set -e
export DOTNET_CLI_HOME=/tmp/dotnet-cli-home
dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests -class 'Hexalith.Timesheets.Server.Tests.EventStoreMagicLinkConfirmationCapabilityStateLoaderTests'
tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class 'Hexalith.Timesheets.IntegrationTests.InternalSurfaceGuardTests'
tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method '*InternalProjectionRouteRejectsMissingAndForgedCredentials' -method '*ConfiguredInternalPortRefusesProjectionWritesOnThePublicPort' -method '*ConfiguredInternalPortDeliversProjectionsAndResolvesAValidLink' -method '*Confirm_submit_reports_one_indistinguishable_category_for_every_non_fresh_catalog'
tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests
```

The imported catalog and Works default both select EventStore `3.126.0`, so
these commands need no version override. Test selections are focused evidence,
not a rerun of all six default suites. All six commands exited 0. The build
reported zero warnings/errors in 36.50 seconds. The four executable invocations
passed **265 distinct cases**, with no failures/errors/skips/not-run. Portable
runner summaries follow, in the same order as the executable commands above:

```text
Hexalith.Timesheets.Server.Tests  Total: 112, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 0.923s
Hexalith.Timesheets.IntegrationTests  Total: 39, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 11.509s
Hexalith.Timesheets.IntegrationTests  Total: 5, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 3.454s
Hexalith.Timesheets.ArchitectureTests  Total: 109, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 17.244s
```

The five HTTP cases include the two public-port theory cases. The HTTP
freshness selection compares scripted stale and unresolved confirmation-submit
denials; it does not establish every status/route combination. All five
non-Fresh statuses through both loader APIs are covered at the loader layer,
as distinguished in the canonical verification report linked above. The loader class
includes all five defined non-Fresh statuses through both catalog and token
APIs, malformed controls and missing-catalog refusal. The guard class exercises
closed listeners and public capability reachability. The credential test
asserts 401/403 and legacy direct-index key absence; it does not fingerprint all
storage keys. The two public-listener cases compare selected index/catalog
contents before and after refusal, then admit the same dispatch internally.
Authenticated mapped delivery populates read models before valid public
resolution. These tests use
in-process substitutes and cannot prove live sidecar credentials, deployed
authorization or resource readiness. Build/test logs are also available for
this checkout under `/tmp/timesheets-five-historical-host-integration/`;
the retained summaries above do not depend on those temporary files.

The preservation/link/scope command below passed. Its first run caught an
incorrect report link to the guard implementation; the path was corrected and
the exact command reran successfully. Independent Blind Hunter review returned
five evidence/traceability findings, all corrected: credential-test scope,
HTTP freshness scope, SDK policy source support, historical package versions,
and navigable canonical handoffs. No runtime change or new deferred work was
required; the existing test results remain applicable to these documentation
corrections.

The following read-only check proves placement, exactly five added notes,
preservation of every original ledger byte and agent entry point, valid local
links, and the three-document change scope. It also works after commit:

```bash
set -e
python3 - <<'PY'
from pathlib import Path
import re
import subprocess

base = '63f89f96be8089f42bd37bcb4dca006454ca9549'
folder = '_bmad-output/implementation-artifacts/'
ledger = folder + 'deferred-work.md'
spec = folder + 'spec-close-five-historical-host-integration-deferrals.md'
report = folder + 'tests/five-historical-host-integration-deferrals.md'
marker = b'`spec-close-five-historical-host-integration-deferrals.md`'
original = subprocess.check_output(['git', 'show', f'{base}:{ledger}'])
current = Path(ledger).read_bytes()
lines = current.splitlines(keepends=True)
notes = [i for i, line in enumerate(lines)
         if line.startswith(b'  resolution_note:') and marker in line]
assert len(notes) == 5
original_lines = original.splitlines(keepends=True)
expected_previous = [original_lines[i - 1] for i in (76, 78, 81, 194, 210)]
assert [lines[i - 1] for i in notes] == expected_previous
assert b''.join(line for i, line in enumerate(lines) if i not in notes) == original
entrypoints = ['AGENTS.md', 'CLAUDE.md', '.github/copilot-instructions.md']
for path in entrypoints:
    assert Path(path).read_bytes() == subprocess.check_output(
        ['git', 'show', f'{base}:{path}'])
assert len({Path(path).read_bytes() for path in entrypoints}) == 1
changed = set(subprocess.check_output(
    ['git', 'diff', '--name-only', base], text=True).splitlines())
changed.update(subprocess.check_output(
    ['git', 'ls-files', '--others', '--exclude-standard'], text=True).splitlines())
assert changed == {ledger, spec, report}, changed
for path in changed:
    raw = Path(path).read_bytes()
    assert raw.endswith(b'\n')
    assert all(line == line.rstrip(b' \t') for line in raw.splitlines())
for target in re.findall(r'\]\(([^)]+)\)', Path(report).read_text()):
    assert (Path(report).parent / target.split('#', 1)[0]).resolve().is_file(), target
print('Five adjacent notes; original ledger and entry points preserved; links and scope pass.')
PY
git diff --check 63f89f96be8089f42bd37bcb4dca006454ca9549
```
