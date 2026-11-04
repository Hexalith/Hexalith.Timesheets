# Five deferred-work dispositions — 2026-11-04

One confirmed local parser defect is fixed. Four other selected open obligations
remain open after current-source investigation; they require evidence or platform
work beyond a local maintenance patch. Previously resolved ledger entries are not
counted again. Launch remains **FAIL**, and Story 3.6 remains in progress.

The user selected completion of locally actionable work even when fewer than
five ledger entries can close. The single parser closure completes that scope;
the other four dispositions document remaining qualification requirements.

The starting Timesheets revision is
`d91d22e3409858d7c156428c55fdb05a5b8f15a0`; the unchanged EventStore checkout is
`0e5ecebc13e8d11afe6d46b6e6de5c877c58acc7`.

| Selected open obligation | Result and evidence | Remaining closure requirements |
| --- | --- | --- |
| Duplicate scheduler JSON names | Fixed locally. An object-pairs hook rejects duplicate decoded names at every object level before `scheduler_fingerprint`. The regression first exposed five accepted malformed ranges and a sixth case that reached fingerprinting. Tests cover duplicate range/header/identity/row/private names and escaped equivalents, response closure, six-key sanitized receipts and restored signal handlers/mask/timer. Real owned HTTP tests cover declared, close-delimited and chunked bodies, plus unchanged nonempty fingerprints. | No deployed scheduler or current runtime qualification follows from these offline checks. |
| Certified abort repair beyond its bounded manifest capacity | Open. `MagicLinkTokenHashCapabilityIndexV6Migration.CaptureRepairSourceGuardsAsync` gathers all 256 shard guards and candidate/directory-page guards into one array. `SharedProjectionEpochCoordinator.AbortRepair.cs` still validates at most 4,096 guards and 262,144 serialized bytes. | Measure the complete independently certified deployment inventory. If it exceeds either limit, the EventStore owner must provide a complete immutable chunked-manifest contract, reservation/restart recovery, source-drift refusal and journal catch-up proof before consumer adoption. Locally splitting requests or raising limits would not establish those guarantees. |
| Full-manifest construction memory and scan cost | Open. Guard collection, sorted digest serialization, SDK serialization/cloning and verification all process the complete inventory. No agreed operational resource budget or certified representative deployed inventory is available to this task. | Define memory/allocation, elapsed-time and store-read budgets; measure full certified representative inventories through the actual backing store. If necessary, design bounded construction/verification with EventStore while retaining complete source-integrity checks. |
| Persistent native signal API failures during deadline cleanup | Open, unverified. `scheduler_observation_deadline.transition` retries until the requested state is observed; the outer cleanup also retries interrupted restoration. Existing temporary interruption and real-signal fixtures do not reproduce persistently failing supported native calls with valid arguments. | Reproduce such a supported-environment failure, then verify an escalation design that disarms the owned timer and restores caller handlers/masks. A permanently failing Python mock alone cannot supply native qualification; simply bounding retries could leave process signal state altered. |
| Complete preservation for large/actively changing shared Redis | Open. The current reader refuses above 32 MiB server memory or 1,024 database keys before Lua, repeats those guards atomically, and checks 64 KiB per-key memory and a cooperative 250 ms script budget. The comparison still requires complete DB0/13/14 content and expiry evidence. | Approve a complete observation design or supply a quiet isolated inventory, then establish complete pre/post consistency, natural expiry handling and foreign-client latency. Bounded refusal and disposable small-store checks do not prove complete large/active shared-store preservation. |

Default production repair certification remains denied by
`DenyAllMagicLinkV6AbortRepairCertification`. Historical capacity tests in
`MagicLinkV6MigrationTests.CertifiedRepairHonorsSdkManifestBoundsWithoutLosingPersistedEvidence`
qualified 1,191 guards at exactly 262,144 bytes; one additional byte refused.
Their 4,096/4,097-guard fixtures already exceed the byte ceiling. These are prior
results, not fresh runs or independent guard-count-ceiling acceptance.

## Verification

Only the scheduler parser and its Python tests change executable behavior. No
domain, AppHost, topology, dependency, certification or deployment changes are
made, and no shared Redis or scheduler is accessed. HTTP servers use owned
ephemeral loopback ports and Redis uses the existing transport double.

- `python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py -k duplicate_json_names -v`
  initially fails on all six malformed cases; after the parser change it passes.
- The first complete fixture run executes 266 tests with two failures in the
  existing total-deadline test. Its JSON mock lacked keyword forwarding and
  therefore raised TypeError when given `object_pairs_hook`. The corrected mock
  forwards the real hook; the focused total-deadline test passes all three phases.
- The final complete command `python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py -v`
  passes **266/266**, zero failures/errors/skips, in 63.035 test seconds.
  [Final command receipt](../evidence/strict-scheduler-json-20261104/fixture-suite.json)
  and compressed text bind the run to identical before/after Python source
  hashes and CPython 3.14.4, with both compressed/plain output digests. The initial failed run is retained separately with its original hashes.
- The final [baseline reproduction](../evidence/strict-scheduler-json-20261104/baseline-regression.json)
  evaluates the exact baseline runner against the final regression and records
  seven expected failures with unchanged test bytes.
- `git diff --check` and Conventional Commit message validation pass.
- Independent blind review found seven test/evidence improvements, all patched.
  No parser implementation defect or new deferred review item remains. The final
  matrix has seven direct duplicate cases and 21 real HTTP cases, including
  nested duplicates, reordered valid JSON and a valid unique escaped name.
  [Triage](../spec-strict-scheduler-json-20261104.md) records every finding.

This report distinguishes five investigated obligations from one completed fix;
it does not claim five closures. No full solution build, .NET suite, performance
lane, live journey or deployed qualification is claimed for this Python change.

Independent final recheck confirms all seven findings are resolved, final source
equality, both output digests, all eleven manifest entries and six source-audit
hashes. No new tests or service calls ran during the read-only recheck.
