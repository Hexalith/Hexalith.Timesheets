# Five historical runtime correctness carry-forwards

2026-10-22 inspection baseline: Timesheets `7533cbd95557d0d02943a619a115fc85552d25c4`, clean `main`; committed EventStore gitlink `5ce5f1d84b88c808cc8a526b9decf149de977e55`. This is a documentation reconciliation of delivered code. Exactly five original [ledger](../deferred-work.md) entries receive additive dispositions; their original evidence remains intact. The [build spec](../spec-close-five-runtime-correctness-carry-forwards.md) records scope and review.

## Dispositions

| Historical request | Verified delivery and scope |
| --- | --- |
| Projection/loader correctness | [Rebuild concurrency](../../../src/Hexalith.Timesheets.Projections/ReadModelRebuildConcurrency.cs) selects CreateOnly only for absence, Match for usable ETags, and refuses existing rows without one. [ProjectionEventReader](../../../src/Hexalith.Timesheets.Projections/ProjectionEventReader.cs) and the [loader](../../../src/Hexalith.Timesheets.Server/MagicLinks/EventStoreMagicLinkConfirmationCapabilityStateLoader.cs) compare the historical event type, payload, serialization, MessageId, timestamp, user and correlation fields; the loader also compares causation, metadata version and protection metadata. This verifies the original duplicate-event request and does not certify parity with every field subsequently added to EventStore stream contracts. Null guards, both EventStoreDomain attributes, named slot and logger wiring are present. [Runtime registration](../../../src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs) preserves caller-owned gateway descriptors and supporting services. |
| Regression-detecting fixtures | [Handler tests](../../../tests/Hexalith.Timesheets.Projections.Tests/MagicLinkStateProjectionHandlerTests.cs) assert rebuild StoreName, key, operation Kind and Concurrency. [Operability tests](../../../tests/Hexalith.Timesheets.Projections.Tests/MagicLinkStateProjectionHandlerTests.Operability.cs) exercise component/key isolation, unambiguous per-key ETags and unchanged contents/versions after rejected writes. [HTTP tests](../../../tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs) project a real cross-tenant candidate and compare its denial with an unknown token on all four external routes. Their store delegates to the platform InMemoryReadModelStore. |
| Safe non-Fresh catalog state | The concrete loader validates catalog shape, keeps only the internal defined status, clears items/cursor/time/detail, and withholds capability/Time Entry state. [Loader tests](../../../tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs) cover Stale, Rebuilding, Degraded, Unknown and Unavailable, with separate malformed controls. The focused HTTP diagnostics check compares stale and unresolved denial; it does not alone cover every status/route combination. |
| Checkpoint tenant isolation | [ValidateTenant](../../../src/Hexalith.Timesheets.Projections/TimesheetsProjectionCheckpoint.cs) uses ordinal equality. Ten call sites cover full/incremental evidence, lists, periods, ledgers, both report targets, both catalogs and capability issuance. [Checkpoint tests](../../../tests/Hexalith.Timesheets.Projections.Tests/ProjectionCheckpointTenantScopeTests.cs) verify rejection before history enumeration and identifier-free errors. The original [implementation spec](../spec-enforce-projection-checkpoint-tenant-scope.md) and ledger Resolution already record the broader matching-scope, incremental and capability controls. This note reconciles the older request with that delivery. |
| Committed AppPort/metadata handoff | EventStore `86d8847616b1f56064fa2dddf3d0ca07837b188f` committed the optional AppPort seam, sidecar forwarding and capability-bound keyed-domain metadata changes. Git ancestry confirms that revision is contained in the recorded `5ce5f1d84b88c808cc8a526b9decf149de977e55` gitlink. The current Timesheets AppHost already consumes `appPort: InternalPort`. This closes the old uncommitted-source/pin request without changing any pointer. |

Correctness provenance: Timesheets `90744b40ed72f9decd0b2a57b0505bdd4fe2ae9e` delivered rebuild concurrency/fold guards; `87d8acef1dbab47ca11714907af57040800dfff0` hardened loader integrity and cross-tenant tests; `aa57e591780404f779f87ed72806c6acf41aee40` introduced sanitized catalog status; `171ae12cd3f19e925c8d6418125fe1b5a2146626` implemented checkpoint scope. Later [rebuild hardening](../spec-close-five-projection-rebuild-deferrals.md), [operability](../spec-close-five-magic-link-operability-deferrals.md), and [integration handoffs](../spec-close-five-integration-handoffs.md) completed missing-ETag guards, strict doubles, transport diagnostics, gateway support and helper documentation. These are existing deliveries, not code changes made by this reconciliation.

The public static Rebuild helper is retained as a supported pure composition helper with explicit limits; no removal is claimed. Logging.Abstractions is actively used. The separate broad eight-goal re-verification entry includes obsolete domain-wide loader fixture setup that remains in historical test arrangements; this reconciliation does not close that entry or claim those inert clauses were removed. Shape-layering and configurable gateway options also remain independent open work.

## Verification

Use SDK `10.0.402`. Restore and build the same Debug/source graph:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false -v:q
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false -v:q
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests -class '*EventStoreMagicLinkConfirmationCapabilityStateLoaderTests' -class '*RuntimeRegistrationTests'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -class '*MagicLinkStateProjectionHandlerTests' -class '*ProjectionCheckpointTenantScopeTests'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -method '*MismatchedCheckpointTenant*' -method '*CapabilityRejectsCheckpointTenantDifferentFromIssuance'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -method '*IncrementalEvidenceValidatesCheckpointTenantAndPreservesVerifiedPrefix' -method '*CapabilityWithoutIssuanceStillReturnsNoModel' -method '*CapabilityValidatesCheckpointAgainstSelectedIssuance'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method '*Concrete_loader_cross_tenant_candidate_is_indistinguishable_from_an_unknown_token_on_every_route' -method '*Confirm_submit_reports_one_indistinguishable_category_for_every_non_fresh_catalog' -method '*ConfiguredInternalPortDeliversProjectionsAndResolvesAValidLink'
```

Restore exited 0. The solution build passed with zero warnings/errors in 11.46 s. Each executable invocation exited 0:

| Selection | Passed | Time |
| --- | ---: | ---: |
| Complete architecture suite | 109 | 16.278 s |
| Loader and runtime registration classes | 125 | 1.270 s |
| Handler and early-checkpoint classes | 201 | 1.042 s |
| Cross-tenant, opaque diagnostics and internal-port HTTP methods | 3 | 2.803 s |
| Checkpoint mismatch methods | 36 | 0.157 s |
| Incremental-prefix, selected-issuance and absent-issuance controls | 4 | 0.170 s |

Every selection had zero failures, errors, skips and not-run tests. Times are from this parallel verification session, not performance measurements. Local logs are `/tmp/timesheets-runtime-carry-forwards-{restore,build,architecture,server,projections,http,checkpoints,checkpoint-controls}.log`. This is focused verification of existing code for a ledger-only change; no new tests or runtime behavior are introduced. No full-suite, live AppHost, platform test-run or deployed acceptance is claimed. Launch readiness remains FAIL and package currency remains CONCERNS. Automatic subscriber delivery, visibility latency, migration/inventory/authority and current-graph runtime qualification remain open under their existing owners.

Portable sanitized runner receipt (commands above, only documentation differs from the recorded source baseline):

```text
Timesheets source: 7533cbd95557d0d02943a619a115fc85552d25c4
EventStore gitlink: 5ce5f1d84b88c808cc8a526b9decf149de977e55
dotnet --version: 10.0.402
restore exit: 0
build exit: 0; Build succeeded; 0 Warning(s); 0 Error(s)
architecture exit: 0
Hexalith.Timesheets.ArchitectureTests  Total: 109, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 16.278s
server exit: 0
Hexalith.Timesheets.Server.Tests  Total: 125, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 1.270s
projections exit: 0
Hexalith.Timesheets.Projections.Tests  Total: 201, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 1.042s
http exit: 0
Hexalith.Timesheets.IntegrationTests  Total: 3, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 2.803s
checkpoints exit: 0
Hexalith.Timesheets.Projections.Tests  Total: 36, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 0.157s
checkpoint-controls exit: 0
Hexalith.Timesheets.Projections.Tests  Total: 4, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 0.170s
```

Reproduce the committed-source observation from the repository root:

```bash
git ls-tree 7533cbd95557d0d02943a619a115fc85552d25c4 references/Hexalith.EventStore
git -C references/Hexalith.EventStore merge-base --is-ancestor 86d8847616b1f56064fa2dddf3d0ca07837b188f 5ce5f1d84b88c808cc8a526b9decf149de977e55
git -C references/Hexalith.EventStore show 5ce5f1d84b88c808cc8a526b9decf149de977e55:src/Hexalith.EventStore.Aspire/HexalithEventStoreDomainModuleExtensions.cs
git -C references/Hexalith.EventStore show 5ce5f1d84b88c808cc8a526b9decf149de977e55:src/Hexalith.EventStore.Aspire/AspireDaprDomainModuleOptions.cs
git -C references/Hexalith.EventStore show 5ce5f1d84b88c808cc8a526b9decf149de977e55:src/Hexalith.EventStore.Aspire/AspireDaprDomainModuleAspireExtensions.cs
git -C references/Hexalith.EventStore show 5ce5f1d84b88c808cc8a526b9decf149de977e55:src/Hexalith.EventStore.DomainService/AdminOperationalIndexMetadata.cs
```

Verify that the five additive notes preserve all original ledger bytes, including after this reconciliation is committed:

```bash
python3 - <<'PY_CHECK'
from pathlib import Path
import subprocess
baseline = '7533cbd95557d0d02943a619a115fc85552d25c4'
ledger = '_bmad-output/implementation-artifacts/deferred-work.md'
original = subprocess.check_output(['git', 'show', f'{baseline}:{ledger}'])
current = Path(ledger).read_bytes().splitlines(keepends=True)
marker = b'  resolution_note: Closed 2026-10-22 by verification in `spec-close-five-runtime-correctness-carry-forwards.md`.'
added = [line for line in current if line.startswith(marker)]
expected = {
    b'Harden projection and loader correctness across the Story 3.6 review patch items that are not part of live-host catalog freshness.',
    b'Strengthen the Story 3.6 test suite where assertions cannot currently catch the regressions they imply.',
    b'Preserve explicit non-Fresh catalog state only when the no-disclosure bundle can represent it safely.',
    b'Enforce checkpoint tenant scope consistently in Timesheets read-model projections.',
    b'Commit and pin the EventStore-owned AppPort and metadata changes in a later authorized repository update.',
}
noted = []
for index, line in enumerate(current):
    if line.startswith(marker):
        assert index > 0 and current[index - 1].startswith(b'  summary: ')
        noted.append(current[index - 1].removeprefix(b'  summary: ').rstrip(b'\r\n'))
assert len(added) == len(noted) == len(set(noted)) == 5
assert set(noted) == expected
assert b''.join(line for line in current if not line.startswith(marker)) == original
print('Five additive dispositions; original ledger bytes preserved.')
PY_CHECK
```
