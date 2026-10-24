# Five build and hosting deferrals — 2026-10-24

Original baselines: Timesheets `c712954a9e0d768d78c968c85d0b788ac6f09de6`; EventStore `e1d8b17160202761ab5c9606cafbcf4d1a4b92c3`. Publication fetch found ten incoming EventStore commits. The reviewed patch was restored byte-for-byte after a fast-forward to `6e927633d920eafcefa2635a124810b541264489`; no nested submodule was updated. Final verification below uses that integrated candidate. The [approved spec](../spec-five-build-and-hosting-deferrals.md) retains frozen intent and all review triage. [Dated receipts](../evidence/five-build-and-hosting-deferrals/20261024/) retain commands, counts, fingerprints and controls.

## Delivered behavior

- The evaluated consumer version reaches solution/project NuGet traversal and direct/SDK-added source build edges. Explicit overrides win, Release selects packages, and Works remains read-only. Default Debug/source commands need no version workaround.
- AppHost uses the SDK gateway credential and retains Timesheets credential generation. The opt-in probe builds/disposes the full graph without Start/Run and reports credential counts only. Runtime children discard probe settings and aliases.
- Shared defaults constrain unsafe hosting Information records after all caller post-configuration and before validation. The lowest-precedence fallback preserves the global minimum, stricter provider/category thresholds and existing predicates. Real Console JSON and OpenTelemetry captures inspect formatted/structured values, exceptions and scopes, including later provider rules. Enabled Warning and unrelated logs remain available.
- Legacy metadata includes explicitly requested, server-confirmed keyed domains for both processor forms. Invalid/unknown/orphan names cannot become domains through an unkeyed processor; null/empty lists retain aggregate discovery. Casing aliases retain one query-bearing domain. Legacy responses advertise no v2 capability; v2 fingerprints and workload policies remain intact.
- [GitHub withdrew the braces advisory on 2026-10-23](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm). The dated audit exits 1 with three unrelated vulnerable packages (two moderate, one high), and no braces finding. No dependency pin, manifest, lockfile or SDK selection changed for withdrawal. The unrelated advisories remain separately tracked.

## Final verification

Default Debug/source restore and warnings-as-errors build pass without a version override, with zero warnings/errors. The consumer selects 3.127.0; supplied Works still defaults to 3.126.0.

| Timesheets suite | Passed | Skipped |
| --- | ---: | ---: |
| Hexalith.Timesheets.ArchitectureTests | 117 | 0 |
| Hexalith.Timesheets.Contracts.Tests | 92 | 0 |
| Hexalith.Timesheets.Server.Tests | 693 | 0 |
| Hexalith.Timesheets.Works.Tests | 76 | 0 |
| Hexalith.Timesheets.Projections.Tests | 697 | 0 |
| Hexalith.Timesheets.IntegrationTests | 918 | 5 |

Total: **2,593 passed, 5 skipped, zero failed/errors/not-run**. Projections took 886.431 s; Integration 752.214 s. These two complete final suites ran sequentially. [Final suite receipt](../evidence/five-build-and-hosting-deferrals/20261024/final-timesheets-suites.json) and [all final consuming checks](../evidence/five-build-and-hosting-deferrals/20261024/review-consuming-checks.json) bind the commands and retained logs.

Both affected upstream projects build with zero warnings/errors. Metadata passes **99/99** and the final telemetry namespace **49/49**, including all **10** privacy/filter cases. The complete upstream Server run finishes in **239.35 s**: **5,731 passed, 25 skipped, one failed guard**, zero errors/not-run. The remaining guard findings are described below; this broad gate is not green.

Compiled configuration/model/construction checks pass **63/63**. A compiled duplicate-registration mutant exits -6 before its success marker. Eight restore/build/runtime cases cover solution/standalone, default/explicit versions and lowercase environment pollution; assertions inspect consumer and Works assets, Works compiled references, copied assembly and runtime identity. The earlier restore-forwarding mutant fails exactly two default cases while preserving two explicit overrides; original configuration was restored byte-for-byte. Python fixture/diagnostic discovery passes **146** tests; release/BMAD tooling **88**.

The [matrix audit](../evidence/five-build-and-hosting-deferrals/20261024/matrix-audit.json) maps all five approved rows to evidence. The original ledger lines are preserved with exactly five additive resolution notes. Input manifests, synchronized entry points, frozen intent and unchanged Works are verified separately.

## Broader blockers and retained failures

The final upstream tracked-secret guard reports exactly **15** fixture lines in Admin UI, PayloadProtection and Sample tests. All complete files match fetched baseline `6e927633d920eafcefa2635a124810b541264489` byte-for-byte. The new privacy test initially added a sixteenth finding; its fixed marker was replaced with a generated per-test value, all privacy assertions remain, and it is absent from the final scan. The final completed run has no other failures. [Incoming finding fingerprints](../evidence/five-build-and-hosting-deferrals/20261024/incoming-secret-scanner-findings.json) and [complete final Server receipt](../evidence/five-build-and-hosting-deferrals/20261024/review-serverfinal-checks.json) document the blocker. Scanner rules and unrelated fixtures were not changed; reconciliation is deferred to the upstream test/security owner.

DomainService broader verification still requires the absent nested `references/Hexalith.EventStore/references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj`. The current integrated guard rerun fails on that exact missing subject. No nested checkout or weakened guard was used. Affected metadata passes 99/99; this is focused evidence, not a broad DomainService pass. [Current guard receipt](../evidence/five-build-and-hosting-deferrals/20261024/review-domainblocker-checks.json) retains the exact command/result.

Historical receipts remain identified as such. The first build-only forwarding patch failed 52/76 Works cases because NuGet chose 3.126.0 while Works compiled against 3.127.0; restore traversal was then corrected. An initial upstream restore hit NU5037 in the temporary codecoverage cache; selecting intact `NUGET_PACKAGES=/home/administrator/.nuget/packages` restored without dependency/audit-policy changes. Original broad attempts were incomplete. Later old-baseline runs were interrupted specifically to integrate incoming commits; a 500 in an interrupted HTTP rebuild was inconclusive and the final complete Integration suite passes. Those interrupted runs are not green gates. Raw completed broad Server logs are retained compressed, with readable failure/summary excerpts and separate raw/archive/retained hashes.

## Scope and publication

Launch remains **FAIL**. Construction does not qualify DCP/dashboard/resource startup. Current-graph runtime, deployed authority/persistence, certified inventory/rebuild and browser acceptance stay open. No foreign application was started/stopped. Shared fixes qualify the source checkout; no published dependency version changed and Timesheets retains its interim logging control. No package publication or deployment was invoked in this session.

<!-- publication-results -->
Independent review and triage are complete. Pushed EventStore `dcd3dce42b67a305b608f03c4dab2b812791abae`, Timesheets content `231e413bc607d8fecd74f8c0d99b070dc57ce982`, and separate gitlink `f759f688e085277fa1f262903c3ea66deaf4a30a` to main. Both implementation remote heads were verified and the parent gitlink matches the pushed SDK commit. [Publication receipt](../evidence/five-build-and-hosting-deferrals/20261024/publication.json) records the operations. This completion record follows publication.

[Timesheets CI](https://github.com/Hexalith/Hexalith.Timesheets/actions/runs/40063665856) and [EventStore CI](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/40063600875) were queued/in progress when inspected. Upstream Commitlint had passed; other hosted results were not awaited or claimed green.

## Reproduction

Use README Debug/source restore/build and run each built executable individually. Run `python3 scripts/check_magic_link_apphost_guards.py --configuration-only`, `python3 -m unittest discover -s scripts -p 'test_*.py'`, and `python3 -m unittest discover -s scripts/tests -p 'test_*.py'`.

In EventStore, restore/build affected projects in matching Debug/source mode with the intact cache. Run the DomainService executable with `-class '*EventStoreDomainServiceExtensionsTests'` and Server with `-class 'Hexalith.EventStore.Server.Tests.Telemetry.*'`. The full Server command uses `-longRunning 60` and an outer 3,600-second bound, with no silence cutoff. Current commands/environments, exit codes and retention policies are in the linked final receipts.
