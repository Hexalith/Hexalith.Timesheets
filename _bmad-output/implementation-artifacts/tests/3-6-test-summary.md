# Test Automation Summary — Story 3.6 (EventStore-Backed Magic-Link State Loading)

> **Superseded on 2026-09-17.** The 2026-06-22 generation record below is retained as historical
> evidence only. Its framework versions, server count, and claims that HTTP/projection-host coverage
> did not exist no longer describe the repository.

## Current Verification

### Expired recovery; reconciled reviewed offline packet (2026-10-15)

Recovery `20261014t112353z` expired at **11:40:42 UTC**. Independent offline
reconciliation at **14:12:04 UTC** authenticates all eight unchanged original
signatures and matches 16 original/21 supplemental preparation checksums,
13 archived inputs and four historical October 1 scripts. At **14:43:26 UTC**,
all 162 historical preservation files, eight signed original pairs and the
failed OIDC original still match. Historical 4,809-check validation, 4,714
manifest readbacks and 16 independent signed-record readbacks are preserved;
no current provider/source freshness or operational acceptance is claimed.

Three independent reviews identified metadata/coverage gaps. The final guard
requires current identities and authority lineage/custody, complete source and
tool bindings, both-target confinement, a separate pre-run execution plan and
scope approval, retained-window containment, exact immutable destination and
30-day retention, dependent-record timing and named cleanup absence. Metadata
size/depth are bounded; diagnostics are sanitized and tool hashes stream.
The helper pins its interpreter/source/inputs and uses **`-I -S -B`**. Original
concurrent helpers/bindings are preserved; frozen source matches the repository.

The exact final command passed **26/26 in 1.142s**, no failures or skips:

```bash
/usr/bin/python3.14 -I -S -B -m unittest discover -s /home/administrator/hexalith-retirement-investigation-evidence/20261015t140709z-s427-expired-recovery-preparation/timesheets-reviewed-source -p test_recovery_readiness.py -v
```

Source hashes matched before/after. The reviewed helper's Bash syntax passed;
an actual execution with synthetic malicious `PYTHONPATH`/`sitecustomize` and
`PYTHONSTARTUP` inputs created no startup marker. Actual readiness at
**14:41:10 UTC** exits **1**, `operationalGate=closed`, with
`workload recovery expired or future` and unchanged earliest expiry
**2026-10-15T11:40:42Z**. It made no remote request, loaded no credential and
performed no cryptographic verification. Synthetic signatures/provider versions
prove metadata refusals only. All **30 scoped private checksum entries** match.

The existing document fitness lane passed **13/13 in 0.126s**, zero errors,
failures, skips or not-run; SDK **10.0.401** matches current `global.json`:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet --version
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor
```

The original audit dictionary-key failure, preliminary refusal-text/order
assertion failures and concurrent **19-test / 14-failure** intermediate
schema/fixture stop remain preserved, followed by the independent passing
frozen-source run. No historical executable or completed operational stage was
replayed. No broad build/suite, AppHost or local/deployed journey ran.

[Exact final receipt](../evidence/3-6/20261015t141204z-expired-recovery-preparation/verification.json),
[triage](../evidence/3-6/20261015t141204z-expired-recovery-preparation/review-triage.json)
and [remaining prerequisite](../evidence/3-6/20261015t141204z-expired-recovery-preparation/NEXT-PREREQUISITE.md)
retain hashes, results and attribution. The next input is separate exact
authorization for fresh **4.0 preparation**, before current signed recovery and
separately covered operational **4.1** external-etcd/node recovery and recovered
independent access. No replacement bundle, capture, restore, decryption,
infrastructure mutation, signing, publication or Git mutation occurred.
4.27/Story 3.6/Epic 3 remain in progress, 4.1's operational gate closed,
spec-23 draft and Timesheets release **FAIL**.

### Fresh signed recovery accepted within deadline (2026-10-15 11:29 UTC)

Concurrent final validation signing/publication completed **11:25:38 UTC**.
The independent final read-only command below exited **0**, finished
**2026-10-15T11:28:43.801049Z**: all **eight** original signatures authenticate,
including the Administrator's actual final M1/M2 attestation; all **16** exact
immutable versions freshly match hashes/lengths/AES256/COMPLIANCE retention.
All 117 final bound files plus the precisely progressed ledger, complete
policy/manifest/proof/cleanup/gate/validation bindings, RPO/cutoff/expiry,
73 local manifest files and current 15 protected source bindings pass.
Non-dry validation remains **4,809/4,809**, zero failures/skips, with all
**4,714** versions reread/matching by the concurrent validator. This resume
replayed none of those stages and issued no remote writes or signatures.

```bash
uv run --quiet --no-project --with boto3 python -B /home/administrator/hexalith-retirement-investigation-evidence/20261015t112105z-s427-timesheets-signed-records-reconcile/reconcile_final_signature.py
```

Final validation provider version `1792063538323774` (2,955,092 bytes) and
signature version `1792063538589066` (314 bytes) freshly match at **11:28:38 UTC**.
The [unsigned final receipt](../evidence/3-6/20261015t112105z-signed-recovery-reconcile/final-signed-acceptance.json)
records every exact original signature/version and owner-acceptance lineage.
At **11:29:22 UTC**, further bounded native reads again passed isolated target
name/UID/storage/claim and temporary-copy absence with intake **2/2** ready.
Fresh 4.0 is accepted at **11:28:43 UTC**, **only until 11:40:42 UTC**;
policy age **85,681.800962s < 86,400s**. Expiry requires separately authorized
fresh recovery, preserving this RID and timestamps, without automatic replay.

The [current checkpoint](../../../docs/deployed-magic-link-platform-candidate-recheck.md#fresh-signed-recovery-accepted-within-deadline--2026-10-15-1129-utc)
names fresh 4.1 external-etcd/node/configuration recovery and independent
recovered-native access as the next separately scoped prerequisite. Concurrent
offline preparation has zero remote requests and a closed gate. Retirement,
upgrades and deployment retain separate exact approvals. Spec-23 stays draft,
4.27/Story 3.6/Epic 3 stay in progress, 4.1 stays closed and release stays **FAIL**.
Earlier barrier receipts and all historical failures remain unchanged.

After recording final acceptance, the same existing LaunchReadiness executable
command passed **13/13 in 0.122s**, zero errors/failures/skips/not-run; final
`git diff --check` passed. Prior sealed checksum sets remain valid. The
[final local verification](../evidence/3-6/20261015t112105z-signed-recovery-reconcile/final-local-verification.json)
records preservation and custody checks. No broad build/suite/journey ran.
The first final local custody audit exited **1** on the new unsigned
`preservation-after.json` summary at mode `0644`; its content was preserved and
only that file's mode corrected to `0600` before the final audit. No signed
original or operational gate input changed, and the failed check is retained.

### Signed recovery and final attestation barrier (2026-10-15 11:24 UTC)

Independent reconciliation finished **2026-10-15T11:22:08.728445Z**, exit **0**:
seven original input signatures authenticate, all eight requested manifest/proof
objects freshly match their original immutable versions, and all 14 objects
including policy/cleanup/gate match exact bytes, AES256 and COMPLIANCE retention.
Approved original bindings (98 files), 73 manifest local files, proof/RPO,
freshness, all 15 protected storage bindings, source/controller identities,
target absence and Memories 2/2 readiness pass. Policy age was **85,286.728436s**;
the first policy/gate deadline remains **2026-10-15T11:40:42Z**.

Concurrent executor results are reconciled, without replay: the four signatures
were published and their temporary agent stopped at **11:01:09 UTC**; the gate
was signed/published; non-dry validation at **11:12:03 UTC** passed
**4,809/4,809**, zero failures/skips, with **4,714/4,714** manifest versions
reread/matching. Current M1/M2 receipts pass but explicitly leave final manual
attestation incomplete. At **11:23:40 UTC**, the final signature/preflight are
absent and both final provider keys return **404**. The existing final guard
passes local-only mode, and its existing Bash action passes syntax validation.
Both M1 source pods still match their UIDs with ready containers/zero restarts.

Exact read-only commands (both exited **0**):

```bash
uv run --quiet --no-project --with boto3 python -B /home/administrator/hexalith-retirement-investigation-evidence/20261015t112105z-s427-timesheets-signed-records-reconcile/reconcile.py
uv run --quiet --no-project --with boto3 python -B /home/administrator/hexalith-retirement-investigation-evidence/20261015t112105z-s427-timesheets-signed-records-reconcile/check_final_prerequisite.py
```

The [unsigned receipt](../evidence/3-6/20261015t112105z-signed-recovery-reconcile/verification.json)
and [current checkpoint](../../../docs/deployed-magic-link-platform-candidate-recheck.md#signed-recovery-reconciled-final-attestation-pending--2026-10-15-1124-utc)
record exact versions, hashes, current action and expiry prerequisite. Stop for
the honest Administrator M1/M2 attestation/final signature, then authenticate
the original and obtain both exact immutable final readbacks before accepting
recovery. No competing signer, remote write or completed-stage replay ran.
If expired, preserve this RID and require separately authorized fresh recovery;
never change timestamps or replace it automatically. Existing decisions,
changes and failures remain preserved. Spec-23 stays draft; 4.27/Story 3.6/Epic 3
stay in progress, 4.1 stays closed and release stays **FAIL**. No broad suite,
solution build, capture/restore or local/deployed journey is claimed here.

Focused document checks passed: SDK **10.0.401** matches `global.json`;
the existing built **13/13 LaunchReadinessTests** passed in **0.124s**, zero
errors/failures/skips/not-run. Whitespace validation passed. Exact commands:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet --version
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor
git diff --check
```

Preservation checks confirm insertions only over all five existing Timesheets
documents, unchanged frozen spec-23/pinned acceptance bytes, unchanged bound
Platform execution/procedure/maintenance snapshots, preserved prior untracked
receipts and unchanged Git index. The [local verification receipt](../evidence/3-6/20261015t112105z-signed-recovery-reconcile/local-verification.json)
records these checks. No dependency, runtime/test source or gitlink changed.

### Current unsigned/unpublished recovery barrier (2026-10-15 10:20 UTC)

The independent read-only check finished at **2026-10-15T10:20:44.986852Z**:
the manifest and three restore proofs remain unsigned and unpublished (all
eight provider HEADs **404**, unchanged four-entry ledger). Policy/cleanup
signatures authenticate; their four exact immutable versions freshly match.
All 15 protected storage bindings, source/controller identities, target absence
and Memories 2/2 readiness pass. Existing records were inspected without replay.
The next input is the Administrator's existing guarded protected-terminal
signing/publication action; reconcile the concurrent replacement-agent unlock
checkpoint before any signer. Gate creation/signing, non-dry validation and
honest current Administrator M1/M2 attestation/signature remain pending.
Policy freshness still ends **2026-10-15T11:40:42Z**; preserve expired evidence
and obtain separately authorized fresh recovery prerequisites if it lapses.
Existing scope/incident/window/roles, changes and historical failures are
preserved. Retirement, upgrades and deployment retain separate exact approvals.
Spec-23 stays draft, 4.27/Story 3.6/Epic 3 stay in progress, 4.1 stays closed and
release remains **FAIL**.

Read-only reconciliation exited **0**; exact command:

```bash
env -u PYTHONOPTIMIZE PYTHONDONTWRITEBYTECODE=1 uv run --offline --quiet --no-project --with boto3 python /home/administrator/hexalith-retirement-investigation-evidence/20261015t101936z-s427-timesheets-readonly-resume/reconcile.py
```

The [current recheck](../../../docs/deployed-magic-link-platform-candidate-recheck.md#current-signature-and-publication-barrier--2026-10-15-1020-utc)
records four exact HEAD/GET/retention readbacks (10:20:38–39 UTC), eight
prepared-pair HEAD **404** results, 73 local evidence-file matches and the
protected native guards. The private reconciliation SHA-256 is
`6d96d06f015ade9da3579473662ce24e3fcf4621cfa59167167619e80b334886`;
the [sanitized unsigned receipt](../evidence/3-6/20261015t101936z-recovery-readonly-resume/verification.json)
has SHA-256 `9ebda6f92e60e1387d89d72a3ed47a8bbc21eb1150fa8d83ae577135cf5201ce`. No broad suite, fixture, solution build or
local/deployed journey is claimed for this documentation-only continuation.
No later operational gate was dispatched past the missing protected signatures.

At **10:24:21 UTC**, four bounded read-only `ListObjectVersions` prefix scans
confirmed **zero immutable versions and zero delete markers** for the eight
exact manifest/proof/signature keys. No provider write occurred. The [separate
publication-history receipt](../evidence/3-6/20261015t101936z-recovery-readonly-resume/publication-version-presence.json)
has SHA-256 `a5aacbe38563e395918db9767dc2d2143454f1a825a5bb44e677e0ea0bba999b`; full responses remain encrypted in private custody.

Focused document verification passed using the existing built test executable:
**13/13 LaunchReadinessTests**, zero errors/failures/skips/not-run, **0.118s**.
SDK **10.0.401** matches current `global.json`. No rebuild was required for
these tests, which read current repository documents. Exact commands:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet --version
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor
git diff --check
```

All three commands passed; whitespace validation exited **0**. Existing
runtime/test source, frozen spec-23 intent and production tasks are unchanged.

### Signed cleanup and concurrent recovery reconciliation (2026-10-15)

Timesheets starts at `c9f71a9fc8db778ab364a955d0b30afde3104204`; the separate
owning Platform checkout is `55bee680e6901a45946dd8bf1e4a4060952bd7ea` with
local continuation evidence. Its unchanged Timesheets gitlink was not staged
or updated. SDK **10.0.401** matches the current `global.json`.

Original policy/cleanup signatures verify against the pinned Administrator
identity. At **08:49:13.225649 UTC**, the separate read-only reconciliation
returned **allPass=true**, exit **0**: four exact immutable validator versions
matched hashes/lengths, AES256 and COMPLIANCE retention; policy age
**76,105.019278s < 86,400s**; protected source/cleanup/controller checks passed.
The manifest's 4,714-version readback and three pass proofs belong to concurrent
executor `20261015t084001z`, whose output appeared during this run. This resume
independently matched its manifest/proof hashes, 73 local evidence files, RPO,
isolation and accepted correction provenance; it did not replay those stages.
All four record signatures, gate and final validation were absent at that
observation. Exact versions/hashes, retained private receipt identity and the
[guarded signing action](../../../docs/deployed-magic-link-platform-candidate-recheck.md#signed-cleanup-and-concurrent-records-reconciled--2026-10-15)
are recorded in the checkpoint. Policy freshness ends **2026-10-15T11:40:42Z**.

The failed bounded prerequisite wrapper is retained separately: child exit
**1**, **0.136s**, no timeout/stdout, at **08:46:02 UTC**, with encrypted
diagnostics and no later dependent dispatch. Its original encrypted error is
not asserted. A later offline diagnosis, with provider access replaced by
unconditional refusal, returned diagnostic `Refusal: refusing to overwrite an
existing proof`, driver exit **0**. This later observation cannot establish
the earlier error. The successful independent reconciliation is distinct from
a retry of manifest/proof creation. Exact executed interfaces were:

```bash
env -u PYTHONOPTIMIZE PYTHONDONTWRITEBYTECODE=1 python3 /home/administrator/hexalith-retirement-investigation-evidence/20261015t084153z-s427-signed-cleanup-resume/continuation.py prerequisite-readback
# Its bounded child, exit 1:
uv run --quiet --no-project --with boto3 python /home/administrator/hexalith-retirement-investigation-evidence/20261015t084153z-s427-signed-cleanup-resume/continuation.py --preread
# Separate independent reconciliation, exit 0:
env -u PYTHONOPTIMIZE PYTHONDONTWRITEBYTECODE=1 uv run --quiet --no-project --with boto3 python /home/administrator/hexalith-retirement-investigation-evidence/20261015t084153z-s427-signed-cleanup-resume/reconcile_readonly.py
```

The offline diagnostic used `env -u PYTHONOPTIMIZE PYTHONDONTWRITEBYTECODE=1
uv run --quiet --no-project --with boto3 python -` with a local heredoc that
loaded `continuation.py` via `runpy` and replaced `s40lib.object_evidence` with
unconditional refusal before `preread`. Its retained diagnostic SHA-256 is
`9783fb0017c3208da65ceb747152a2eca0c9f4f4b177874100beda8278f684d8`.
The independent reconciliation receipt SHA-256 is
`1a30ac7073f35747fdf09c617fcfff1addffcb021c4894df9a376d80b6be8731`.
No raw provider/native output or credentials were published.

Focused document verification passed: ArchitectureTests build, exit **0**,
**0 warnings/errors**, **0.98s**; rebuilt `LaunchReadinessTests`, exit **0**,
**13/13 in 0.117s**, zero errors/failures/skips/not-run; `git diff --check`,
exit **0**. Exact commands:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet --version
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor
git diff --check
```

The six Timesheets changes are evidence/tracking additions. Runtime/test
source, frozen intent, original qualification, retained plan/declaration and
historical failures remain preserved. No broad suite, fixture, solution build,
local/deployed journey, signing, gate or non-dry final validation is claimed.
This resume made zero remote writes and replayed no completed recovery stage.
Next is Administrator signing/publication of the existing manifest and three
proofs, then authentic fresh readbacks before later gate/validation signatures
and actual final M1/M2. Existing scope/incident/window/roles persist. Retirement,
upgrades and deployment retain their separate exact approvals. Spec-23 stays
draft; Story 3.6/Epic 3 stay in progress and release stays **FAIL**.

### Authorized dirty-tree prerequisite resume (2026-10-14)

The user approved resuming over existing evidence edits and changed root
submodule checkouts. Timesheets HEAD remained
`9fa572c6afb0782bf434e98b6e1b30674eb265d2`; observed Platform was
`4b9cb5624159de0782dd3329cdcc3b2d66010509`, and EventStore was
`210b90da172cc7ca946fbb06cfd44390bffa707b`. `dotnet --version` returned
**10.0.401**, matching the current `global.json`. No clean-tree or released
artifact claim is made for these checkout identities.

Eight native reads at **19:26:22 UTC** repeated the exact retained-client
commands under [the staging recheck](../../../docs/deployed-magic-link-platform-candidate-recheck.md#latest-staging-dependency-recheck).
The first six exited **0**, with client v1.34.12, server v1.34.9, unchanged
cluster UID, 23 namespaces, 100 workload/service objects, 111 pods and no
matching names/images. Both staging namespaces were NotFound, exit **1**;
the Gateway resource type was unavailable, exit **1**. Responses were reduced
in memory; raw responses, credentials, Secrets and domain histories were not
printed or retained. Exact source/provider findings and bounded-discovery
limits are in [the current recheck](../../../docs/deployed-magic-link-platform-candidate-recheck.md#latest-authorized-working-tree-recheck).

A fresh focused ArchitectureTests build passed with **zero warnings/errors
in 1.27s**, exit **0**. The rebuilt readiness class passed **13/13 in
0.127s**, zero errors/failures/skips/not-run. Whitespace validation passed:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor
git diff --check
```

Preservation checks retain all pre-existing evidence edits, frozen spec-23
intent at SHA-256 `3bb249ed0d9b617bb92e06aa0a725ad0727a460293cbee1e1e9c99f3d63b922a`,
the pinned original qualification report, retained plan/declaration and prior
unsigned preparation receipt. The canonical plan digest remains
`80aaf70967771c671e9245982a906ea9b77d56bdb6b568ee60015ad4e33305c7`.
Concurrent synchronization advanced Timesheets HEAD to
`e2a9e0898f5e30f2b9f3457ece6ddd55d4c999ba`, Platform to
`55bee680e6901a45946dd8bf1e4a4060952bd7ea` and Works to
`ceef28532550061dd361dfe7df9c2a93582b2aa5`. The initial Platform handoff/
prerequisite hashes changed externally; both input snapshots are retained,
and the preservation check reported that drift rather than claiming unchanged
inputs. No checkout, index or commit mutation was issued by this run.
[The concurrent owner checkpoint](../../../docs/deployed-magic-link-platform-candidate-recheck.md#latest-concurrent-owner-checkpoint)
records approved partial recovery/cleanup and its unsigned receipt, without
promoting it to accepted recovery or Timesheets deployment. This run adds evidence to six
Timesheets documents; runtime/test source is unchanged. No solution build,
broad suite or local/deployed journey is claimed by these focused checks.
No signing, retirement, upgrade, deployment, domain/maintenance operation,
dependency update, nested initialization or Git mutation was issued.
The new Party JWT reader supplies no Timesheets production binding/certified
provider, and no independent runtime fix was established. External prerequisites
keep spec-23 draft, Story 3.6/Epic 3 in progress and release **FAIL**.

After the concurrent owner evidence was reconciled, the rebuilt readiness
class reran with the same exact command and passed **13/13 in 0.117s**, exit
**0**, with zero errors/failures/skips/not-run. Final preservation checks
passed for the six pre-existing document contents, frozen intent, pinned
report/plan/declaration, prior receipt and newly captured owner inputs.
Checkout changes matched only the recorded external Platform/Works moves;
Timesheets source/tests stayed unchanged across that synchronization.
`git diff --check` passed. Both handoff/prerequisite snapshots remain retained;
the earlier detected drift is preserved as external activity, not hidden.

### Offline retirement preparation and native prerequisite recheck (2026-10-14)

Initial clean Timesheets `main`: `9fa572c6afb0782bf434e98b6e1b30674eb265d2`;
Platform: `2bd78603e164265e3b6c00af9950429cff5510f9`. `dotnet --version`
returned **10.0.401**, matching `global.json`. The new offline retirement
preparation packet records no production source observation, plan, approval
or accepted retirement. Platform's recorded 284/284 tests (including 31
preparation tests) and historical integrity checks were inspected as retained
evidence; neither software checks nor private preparation were rerun here.
Independent source investigation established no separate Timesheets runtime fix.

Eight native reads at **10:57:18 UTC** used the same exact commands as
[the staging recheck](../../../docs/deployed-magic-link-platform-candidate-recheck.md#latest-staging-dependency-recheck).
The first six exited **0**: client v1.34.12, server v1.34.9, unchanged
cluster UID, 23 namespaces, 100 workload/service objects and 111 pods, with
no matching names/images. Both staging namespaces were NotFound, exit **1**;
the Gateway resource type was unavailable, exit **1**. Responses were projected
in memory; raw JSON, credentials, Secrets and domain histories were not printed
or retained. These bounded observations do not certify domain history or
exclude renamed applications. Receipt/source identities and all qualification
limits are in [the current recheck](../../../docs/deployed-magic-link-platform-candidate-recheck.md#latest-offline-retirement-preparation-recheck).

After reconciliation, the existing executable passed **13/13 in 0.163s**,
zero errors/failures/skips/not-run, exit **0**:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor
git diff --check
```

Whitespace validation passed. This uses existing build assets to check current
documents; no fresh solution build, broad suite or local/deployed journey is
claimed. Frozen spec-23 intent remains verbatim at SHA-256
`3bb249ed0d9b617bb92e06aa0a725ad0727a460293cbee1e1e9c99f3d63b922a`.
The pinned original qualification report and inspected Platform inputs remain
unchanged; canonical plan hash verification passed at
`80aaf70967771c671e9245982a906ea9b77d56bdb6b568ee60015ad4e33305c7`,
and the candidate declaration retains its recorded hash. Spec-23 remains draft.
Only six Timesheets evidence/tracking documents changed. No runtime startup,
signing, fixture, retirement, upgrade, deployment, domain or maintenance
operation was issued; no dependency, nested submodule or Git mutation occurred.
Spec-23 stays draft, Story 3.6/Epic 3 stay in progress and release stays **FAIL**.

### Staging dependency handoff and native prerequisite recheck (2026-10-14)

Initial clean Timesheets `main`: `9ecda28dc7ff540d83fad7ba1e5d585bdc33d0ae`;
Platform: `92a02924894f6f2f219801a90e5bf25716abd028`. `dotnet --version`
returned **10.0.401**, matching `global.json`. Independent source investigation
confirmed the new staging dependency handoff and missing deployed providers;
no independent Timesheets runtime fix was established. Platform's retained
preparation tests/receipts were read as dated evidence and were not rerun or
regenerated by this Timesheets investigation.

Eight native reads at **07:32:39 UTC** used the retained v1.34.12 client;
the first six exited **0**, reporting server v1.34.9, unchanged cluster UID,
23 namespaces, 100 workload/service objects and 111 pods, with no matching
names/images. The staging namespace command exited **1** with both names
NotFound; the Gateway command exited **1** because the resource type is
unavailable. Exact full commands, response minimization and census limits are
in [the current recheck](../../../docs/deployed-magic-link-platform-candidate-recheck.md#latest-staging-dependency-recheck).

After the evidence/tracking reconciliation, the existing executable passed
**13/13 in 0.170s**, zero errors/failures/skips/not-run, exit **0**:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor
git diff --check
```

Whitespace validation passed. This runner uses existing build assets to check
current documents; it does not establish a fresh dependency-graph build.
Frozen spec-23 intent remains verbatim at SHA-256
`3bb249ed0d9b617bb92e06aa0a725ad0727a460293cbee1e1e9c99f3d63b922a`.
The pinned original report, retained Platform plan/declaration and new handoff
bytes are preserved. The plan's canonical preparation hash verifies as
`80aaf70967771c671e9245982a906ea9b77d56bdb6b568ee60015ad4e33305c7`.

Only seven Timesheets evidence/tracking documents changed. No runtime startup,
solution build, broad suite, local journey, deployment or domain/maintenance
operation ran; no dependency, nested submodule or Git mutation occurred.
Prior local evidence retains its original source identity. Hosted release,
production providers, certified inventory/maintenance/continuity and deployed
subscriber/shared-rebuild proof remain external prerequisites. Spec-23 stays
draft; Story 3.6/Epic 3 stay in progress and release stays **FAIL**.

### Development host binding and deployed prerequisite recheck (2026-10-13)

Initial clean Timesheets `main`: `b9f968221a014c95869f3add8e16eda398b989c8`;
Platform: `416878a74975b1023b3a00ab5d35aad4ce6365d9`. `dotnet --version`
returned **10.0.401**, matching `global.json`. Independent source investigations
confirmed the new explicit Development/source host binding and unchanged missing
production/certification prerequisites. Platform's recorded focused/full/Python
checks were read as historical evidence, not executed again. No separate
Timesheets runtime defect was established.

At 18:58 UTC the following bounded native reads were executed; the first four
exited **0** and the last exited **1**, with both requested namespaces NotFound:

```text
kubectl --context jpiquot@local --request-timeout=15s get namespace kube-system -o json
kubectl --context jpiquot@local --request-timeout=15s get namespaces -o json
kubectl --context jpiquot@local --request-timeout=15s get deployments,statefulsets,services -A -o json
kubectl --context jpiquot@local --request-timeout=15s get pods -A -o json
kubectl --context jpiquot@local --request-timeout=15s get namespaces hexalith-staging hexalith-staging-data -o json
```

Observed cluster UID remained `b5860674-1d1e-4a3b-b413-e1bb3b9a4557`,
with **23 namespaces**, **100 workload/service objects** and **111 pods**.
The name/namespace and pod container/init-container image scan found no
`timesheet`, `eventstore` or `platform` matches. Responses were reduced in memory
to these fields; raw JSON, credentials, Secrets and domain histories were neither
printed nor retained. Bounded discovery cannot certify complete history or exclude
arbitrarily renamed applications. Exact new binding/retained-plan limits are in
[the candidate recheck](../../../docs/deployed-magic-link-platform-candidate-recheck.md#latest-development-binding-recheck).

After the evidence/tracking reconciliation, the existing executable passed
**13/13 in 0.154s**, zero errors/failures/skips/not-run, exit **0**:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor
git diff --check
```

Whitespace validation passed. Frozen spec-23 intent remains verbatim at SHA-256
`3bb249ed0d9b617bb92e06aa0a725ad0727a460293cbee1e1e9c99f3d63b922a`.
The pinned original qualification report, retained Platform plan and declaration
retain their hashes recorded in the candidate recheck. Offline preparation was
not repeated; absent nested inputs were not initialized.

Only seven Timesheets evidence/tracking files changed. This run used existing
readiness build assets to check current repository documents; it does not prove
a fresh dependency-graph build. No new solution build, broad suite, local journey,
live Platform startup, deployment or domain/maintenance operation ran. Prior
local results retain their original source identities. Spec-23 stays draft,
Story 3.6/Epic 3 stay in progress and release remains **FAIL** pending hosted
release, real production providers, certified inventory/maintenance/continuity
and deployed subscriber/shared-rebuild proof.

### Latest Platform candidate and native target recheck (2026-10-13)

Initial clean Timesheets `main`:
`aefd76772e84018442eb21b36549b8499a488721`; Platform:
`ce6f2d34398803644b29d02ba57625898190ce20`. SDK `dotnet --version`
returned **10.0.401**, matching `global.json`. Platform now retains an offline
staging candidate and blocked preparation, with no active Timesheets binding
or retained release digests. Its P1 HTTP adapter supplies no Timesheets
operational enrollment or certified inventory authority.

Four bounded native census commands exited **0**, with unchanged cluster UID,
23 namespaces, 100 workload/service objects and 111 pods, and no matching
Timesheets/EventStore/Platform names or images. The candidate-specific
namespace read exited **1**: both `hexalith-staging` and
`hexalith-staging-data` were NotFound. The offline preparation command exited
**1**, reporting unavailable/invalid inputs and publishing no plan. Required
nested Platform inputs are absent and were not initialized. Exact commands,
retained preparation revisions/hash and discovery limits are in
[the candidate recheck](../../../docs/deployed-magic-link-platform-candidate-recheck.md).

After readiness/tracking reconciliation, the existing test executable passed
**13/13 in 0.120s**, zero errors/failures/skips/not-run, exit **0**:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor
git diff --check
```

Whitespace validation passed. Frozen spec-23 intent was compared with its
initial canonical revision and remains verbatim; status remains draft.
Canonical JSON checksum verification matched the retained preparation hash
and candidate declaration hash. The original qualification report remains
unchanged at the exact SHA-256 pinned by Platform's preparer.

Only evidence/tracking documentation changed in this run. Unrelated changes
appeared in EventStore, McpCli and Parties after the initial clean preflight;
they were preserved. EventStore source investigation observed two unresolved
conflicts; no new solution build, broad suite or local journey was run for
these documentation changes. The readiness runner uses existing build assets
and validates repository documents; it is not a current dependency-graph
build. Prior local passes retain their own revisions and dates.

Hosted deployment/release, production bindings, certified full inventory,
maintenance/source continuity and deployed subscriber/shared-rebuild evidence
remain absent. Story 3.6/Epic 3 remain in progress and release remains **FAIL**.
No infrastructure approval or domain/maintenance operation was issued.

### Deployed prerequisite recheck on build resume (2026-10-13)

Clean baseline: Timesheets `28ffeaee83e9414ce9e205e57f674e4c29a6e40b`;
Platform remains `2c3f0fd4befe4e7e2aa1a3f7ce8ae1d5e1808f3c`.
SDK `dotnet --version` returned **10.0.401**, matching `global.json`.
Four bounded native cluster reads exited **0**, observing the same namespace
UID, 23 namespaces, 100 workload/service objects and 111 pods. Names and
pod images contained no `timesheet`, `eventstore` or `platform` matches.
The projected responses contained no credentials or domain payloads; raw
responses were not retained. Exact commands and bounded-discovery limits are
in [the qualification record](../../../docs/deployed-magic-link-acceptance.md).

Independent source investigation confirmed missing production resource-binding,
verified-inventory and migration-authority providers. Existing mapped handlers
and local manual delivery do not establish deployed subscriber/shared-rebuild
acceptance. Draft spec-23 remains unready because external installation and
certification dependencies are missing. Story 3.6/Epic 3 remain in progress;
release remains **FAIL**. Only evidence/tracking documentation changed;
runtime code, deployment and historical local results remain unchanged.

After reconciliation, the existing readiness executable passed **13/13 in
0.195s**, zero errors/failures/skips/not-run, exit **0**:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor
git diff --check
```

Whitespace validation passed. The frozen spec-23 intent was compared with
`git show HEAD:_bmad-output/implementation-artifacts/spec-3-6-implement-eventstore-backed-magic-link-state-loading-23.md`
and is preserved verbatim; status remains draft. No new build, broad default
suite or local journey was run for these documentation edits.

### Fresh local acceptance and deployed qualification checkpoint (2026-10-13)

Baseline source: `11976c2d8000bfc83f1d816a085836beecf8f6d1`, clean main.
Read-only investigation did not establish a cause or production fix for the
earlier metadata HTTP 500. Both isolated attempts below reached the full
existing journey without changing runtime code or assertions.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-36-build23-baseline
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false -v:q
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false -v:q > /tmp/timesheets-36-build23-build.log 2>&1
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-36-build23-fresh
```

SDK **10.0.401** matches `global.json`. Restore exited **0**; fresh Debug
solution build exited **0**, zero warnings/errors in **17.64s**.

| Local attempt | Evidence | Result |
| --- | --- | --- |
| Baseline, pre-existing build assets | `/tmp/timesheets-36-build23-baseline/run-wihh1tih` | 1/1 passed in 137.078s; runner exit 0 |
| Fresh source-reference build | `/tmp/timesheets-36-build23-fresh/run-n0nc1rne` | 1/1 passed in 126.554s; runner exit 0 |

Each test reports zero errors, failures, skips and not-run cases. The
unchanged test requires stored recording/issuance, selected v7 candidate,
effective evidence/policy/lineage, valid GET, confirm/adjust, one race winner,
opaque replay denial, composed rebuild, actor restart and fresh post-restart
use. It invokes mapped projection delivery explicitly; this is not deployed
subscriber acceptance. Initial baseline transport timeouts recovered through
existing behavior. The historical metadata 500 remains unexplained.

Both attempts proved cleanup and shared preservation. Baseline exited all
14 captured PID/start identities; the fresh attempt exited all **16**.
Both final bound-resource inventories were empty and each removed its three
owned containers and component copies. Baseline shared DB0/13/14 counts were
114,959/2,704/773; fresh counts were **114,959/2,692/773**. Fresh permanent
counts were **114,788/2,671/773**, with **171/21/0** expiring keys.
Each run's before/after contents and retained TTL values/deadlines match,
with zero natural expirations during either interval. Both retained all
**65** scheduler KV entries, cluster identity and revision **12735**.
The lower DB13 count predates the fresh fixture; no cause is attributed.
Proof is in each `shared-preservation.json`, `recorded-process-exit.json`,
`resource-exit.json`, and `cleanup.json`.

A manual monitoring dump during the baseline included its temporary OTLP
API credential in tool output; the existing sanitizer did not remove that
header. Later monitoring output used selected non-sensitive fields. No raw
diagnostic dump was saved as acceptance evidence, and exact cleanup confirms
the owning runtime exited. No domain token or signing key was printed.

After evidence reconciliation, the narrow readiness lane passed **13/13 in
0.156s**, zero errors/failures/skips/not-run, exit **0**. Whitespace validation
also passed:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor > /tmp/timesheets-36-build23-readiness.log 2>&1
git diff --check
```

Only documentation and a draft qualification spec changed. The six default
suites were not rerun; their reviewed 1,899-pass evidence below remains
historical. The refreshed live pass closes the latest-tree local-proof gap,
not deployed certification. The draft [spec-23](../spec-3-6-implement-eventstore-backed-magic-link-state-loading-23.md)
needs the selected deployment, operator/access and genuine binding,
inventory/maintenance evidence before dependent work. Story 3.6/Epic 3
remain in progress; release remains **FAIL**. No legacy data was migrated,
cleared or repaired, and no staging, commit, push or dependency/gitlink update
occurred. The preceding failed attempts and completed specs are preserved.

### Recording response recovery and uniform denial headers (2026-10-13)

Scoped spec: `spec-3-6-implement-eventstore-backed-magic-link-state-loading-22.md`.
The response-first helper now polls the original command after a failed
submission response and retries classified transient status failures within
the same deadline. Completed status and exact stored recording/submission or
terminal-transition evidence remain required. Accepted identity mismatch,
nonretryable status, receipt corruption and caller cancellation retain their
denial/propagation behavior. Both public POST routes use the existing GET
privacy-header helper so denial headers remain equivalent across all routes.
Independent review found five correction items and one deadline-cleanup
follow-up; all were patched and targeted verification found no remaining
issue. No new public API, persistence path, authority, dependency, gitlink or
AppHost/ServiceDefaults change was made.

SDK `dotnet --version`: **10.0.401**, matching `global.json`. Exact commands:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false > /tmp/timesheets-36-record-response-restore.log 2>&1
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false > /tmp/timesheets-36-record-response-reviewed-build.log 2>&1
```

Restore exited 0. Reviewed solution build exited 0 in **8.17s**, zero
warnings/errors. Each default test project ran separately using its xUnit v3
executable; final logs use `/tmp/timesheets-36-record-response-final-<Project>.log`.

| Reviewed project | Result | Time |
| --- | --- | --- |
| ArchitectureTests | 57/57 | 4.981s |
| Contracts.Tests | 92/92 | 1.301s |
| Server.Tests | 558/558 | 2.758s |
| Works.Tests | 76/76 | 0.714s |
| Projections.Tests | 389/389 | 654.205s |
| IntegrationTests | 727 passed, five declared skips, 732 total | 680.674s |

Final total: **1,899 passed, five skipped, zero errors/failures/not-run in
1,904 cases**. Every executable exited 0. Commands:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -noColor > /tmp/timesheets-36-record-response-final-ArchitectureTests.log 2>&1
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests -noColor > /tmp/timesheets-36-record-response-final-Contracts.Tests.log 2>&1
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests -noColor > /tmp/timesheets-36-record-response-final-Server.Tests.log 2>&1
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Works.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Works.Tests -noColor > /tmp/timesheets-36-record-response-final-Works.Tests.log 2>&1
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -noColor > /tmp/timesheets-36-record-response-final-Projections.Tests.log 2>&1
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -noColor > /tmp/timesheets-36-record-response-final-IntegrationTests.log 2>&1
```

The reviewed focused HTTP selection passed **11/11 in 5.078s**, covering two
recording-policy recovery cases, six revoke/expire normal/lost/corrupted
receipt cases, and the three all-route invalid/cross-tenant/empty-token
header-equivalence matrices. Command:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method "*ExternalRecordingRecoversCommittedBatchAfterSubmitResponseFault" -method "*Management_terminal_transition_is_stored_in_target_stream_and_denies_later_use" -method "*Invalid_magic_link_http_boundary_responses_are_equivalent_and_opaque" -method "*Concrete_loader_cross_tenant_candidate_is_indistinguishable_from_an_unknown_token_on_every_route" -method "*Empty_or_missing_magic_link_token_is_indistinguishable_from_an_invalid_state_denial_on_every_route" -noColor > /tmp/timesheets-36-record-response-reviewed-http.log 2>&1
```

Recording service regressions include missing then Processing then Completed,
status 503 recovery, status 403 refusal, an independently cancelled transport
Task, caller cancellation, wrong accepted identity, exact one/two-event
receipts, and timestamp corruption. The exhausted-budget test consumes
800 ms before a response fault, keeps a one-second deadline with 500-ms
scheduling tolerance, and checks the original transport token is cancelled.
The HTTP fixtures assert stored event contents and retries without a second
submission. These are in-process fixtures, separate from real Dapr evidence.

The first source-reference build with new tests passed before production
changes; its six service cases and two HTTP cases all failed, reproducing the
response-first gap. Logs: `/tmp/timesheets-36-record-response-red-server.log`
and `-red-http.log`. The initial post-fix service/HTTP selections passed
33/33 and 5/5. The first broad Integration run then completed **720 passed,
three failed, five skipped, 728 total in 606.894s**. All three failures were
GET/POST privacy-header equivalence; the reviewed focused matrices above
pass after applying the existing headers to POST. Its full Projections run
passed 389/389 in 577.333s. Those first broad logs use
`/tmp/timesheets-36-record-response-<Project>.log` and predate review corrections.

Live commands/results:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-36-record-response-baseline
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-36-record-response-reviewed-live
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 /tmp/timesheets-36-record-response-diagnostic-run.py
```

- Baseline exited 1 before resource creation: `Matching Dapr app ID exists;
  no AppHost started`; evidence `baseline/run-oonsusu5` under the path above.
- Reviewed live attempt exited 1: **0/1 in 0.979s**, projection metadata
  returned HTTP 500 before domain commands. Evidence:
  `/tmp/timesheets-36-record-response-reviewed-live/run-t96n1_t4`.
  Private endpoints/components were verified. Original journey preservation
  matched; initial cleanup retained its dependencies because three captured
  resources remained past the shutdown window. All 14 captured PID/start
  identities later exited and no original bound resources remained.
- Verified follow-up used the runner's original container identities and
  ownership label, component path and captured process identities. Guarded
  cleanup removed all three private containers and component copies;
  evidence `/tmp/timesheets-36-record-response-reviewed-cleanup/run-kn4aqpc7`
  reports `complete: true` and all recorded processes gone. The subsequent
  preservation comparison failed because shared DB0's persistent digest
  changed during that interval; no cause is attributed or shared data repaired.
- The temporary diagnostic wrapper would capture sanitized Aspire logs before
  stopping a failed run. Its retry exited 1 at preflight instead: a foreign
  Works AppHost had restarted an `eventstore` sidecar. No resources started;
  evidence `/tmp/timesheets-36-record-response-diagnostic-live/run-siweg2xx`.
  That foreign runtime was left untouched. No metadata-500 cause is established.

These live results provide no passing new journey. Certified cross-tenant
legacy inventory, actual operator/resource-binding authority, enforced
quiescence, owner continuity and pending-delivery recovery, and deployed
subscriber/shared-rebuild acceptance remain open. **Story 3.6 remains
in-progress; release remains FAIL.** The earlier evidence below stays historical.

After the final evidence updates, launch-readiness fitness passed **13/13 in
0.144s**, no errors/failures/skips/not-run; `git diff --check` exited 0.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class "*LaunchReadinessTests" -noColor > /tmp/timesheets-36-record-response-final-readiness.log 2>&1
```

### Redaction clearance and final review patches (2026-10-13)

SDK 10.0.401 source-reference restore and warning-as-error Debug solution
build both exited 0 with zero warnings/errors. Each Timesheets test project
ran as its built executable, with these exact results before the final
review patches:

| Project | Result | Time |
| --- | --- | --- |
| ArchitectureTests | 57/57 | 2.457s |
| Contracts.Tests | 92/92 | 0.505s |
| Server.Tests | 545/545 | 0.806s |
| Works.Tests | 76/76 | 0.324s |
| Projections.Tests | 387/387 | 541.756s |
| IntegrationTests | 715 passed, five declared skips, 720 total | 508.201s |

Total: 1,872 passed, five skipped, zero errors or failures in 1,877 cases.
`python3 -m unittest discover -s scripts -p 'test_*magic_link*.py' -v`
passed 65/65. Test logs are `/tmp/timesheets-36-final-<Project>.log`; the
Python log is `/tmp/timesheets-36-redaction-python.log`.

`DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-36-redaction-live`
passed the opt-in journey 1/1 in 125.125 seconds on the redaction tree.
`/tmp/timesheets-36-redaction-live/run-fcnqc17t/cleanup.json` reports complete
cleanup, no failure reasons, and removal of all three owned containers;
the runner reports retained shared state, unexpired TTL values and scheduler
KV unchanged. The fixture uses private Redis, placement and scheduler.

After that broad verification, the bounded retry classifier gained HTTP 408;
v6 migration pages gained byte accounting; ordinary index insertion rejects
negative node IDs; blank v6 operator requests are opaque; and token display
GETs send no-store and no-referrer headers. Focused final-tree tests pass:
four HTTP retry cases, five v6 migration cases (including a maximum-width
128-owner byte-split case), one negative-root case, two blank-request cases,
and two token-header cases. The affected Projections and Integration test
projects build with zero warnings/errors. A final source-reference solution
build passed with zero warnings/errors; a combined Integration selection
passed 8/8, the v6 migration class 5/5, negative-root test 1/1, issuer
class 8/8, and launch-readiness checks 13/13. The full suites and live run above
predate these review patches. None of
these local results supplies certified deployed legacy inventory, migration
authority, source continuity or shared rebuild acceptance. Story 3.6 and
release readiness remain **FAIL**.

### Reviewed startup-hardening final result (2026-10-13)

The scoped increment is complete and independently reviewed. AppHost runtime
validation is mandatory whenever the fixture flag is enabled. Ordinary
starts, the supported isolated runner, completed fixture spec/frozen intent,
legacy environment and production defaults are preserved. Story 3.6 and
Epic 3 remain in progress; release readiness remains **FAIL**.

All evidence is under `/tmp/timesheets-apphost-hardening-20261013`.

| Final check | Result | Evidence under root |
| --- | --- | --- |
| SDK/source-reference restore | SDK 10.0.401; exit 0 | `restore.log` |
| Final Debug solution build | exit 0; 0 warnings/errors; 4.87s | `build-final.log` |
| Focused Python | 65/65; exit 0; no skips; 7.277s | `python-reviewed.log` |
| Python compile/diff whitespace | exit 0 | both changed Python scripts; `git diff --check` |
| Architecture with ordering tripwire | 57/57; exit 0; no errors/failures/skips/not-run; 2.187s | `architecture-final.log` |
| Final LaunchReadiness after evidence edits | 13/13; exit 0; no errors/failures/skips/not-run; 0.137s | `readiness-final.log` |
| Compiled configuration-only guards, polluted environment | 22/22; exit 0; two selections 0, twenty refusals -6 | `configuration-guards-reviewed.log`, `configuration-guards/run-815f7r2f` |
| Compiled infrastructure-backed guards | 22/22; driver exit 0; preservation/cleanup pass | `guards-final.log`, `guards/run-936k1qhn` |
| Supported isolated live journey | 1/1; runner exit 0; no errors/failures/skips/not-run; 138.093s | `live-retry.log`, `live/run-k4wmmm45` |

Review corrections clear controlled environment aliases and force validation-only
plus execution mode through command-line configuration; add a bare-fixture
publish refusal; protect the early-return ordering in Architecture; and avoid
socket allocation in configuration-only endpoint mismatch probes. The same
reviewer confirmed all four corrections and found no remaining concrete
regression. Structured partial-result aggregation was a pre-existing optional
improvement; failed command logs retain exact results. The original ledger
entry stays historical and an appended resolution explicitly links it.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -warnaserror -m:1 /nr:false
python3 -m unittest discover -s scripts -p 'test_*magic_link*.py' -v
python3 -m py_compile scripts/check_magic_link_apphost_guards.py scripts/test_isolated_magic_link_dapr.py
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -noColor
env timesheets_live_dapr_validate_only=0 timesheets_live_dapr_fixture=1 'Dapr:PlacementHostAddress=127.0.0.1:50005' dApR__SchedulerHostAddress=127.0.0.1:50006 ASPIRE_TIMESHEETS_LIVE_DAPR_COMPONENTS=/legacy/components DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/check_magic_link_apphost_guards.py --configuration-only --log-directory /tmp/timesheets-apphost-hardening-20261013/configuration-guards
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/check_magic_link_apphost_guards.py --log-directory /tmp/timesheets-apphost-hardening-20261013/guards
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-apphost-hardening-20261013/live
```

Live runtime proof identifies private Redis `127.0.0.1:59644`, placement
`127.0.0.1:59645` and scheduler `127.0.0.1:37639`. Actual components/control
targets were checked before commands. Its `recorded-process-exit.json` proves
all 16 captured PID/start identities exited, `resource-exit.json` is `[]`,
and `cleanup.json` confirms descendants gone, three owned containers removed
and component copies removed. Live and final guards preserve DB0 **113,816**
keys (113,690 permanent/126 expiring), DB13 **2,782** (2,671/111), DB14
**773 permanent**, and all **55 scheduler KV entries**, cluster identity and
revision 12593. Retained unexpired values/deadlines match, **zero natural
expirations**. These totals are fresh-baseline observations; previous-day
changes are not attributed, repaired or cleared. No legacy activation,
clear, migration, repair or simulated production authority occurred.

The first post-review build (`build-reviewed.log`) exited 1 in 5.16s with
CS0023 at ScaffoldGovernanceTests.cs:110: a void-returning Shouldly assertion
was chained. Splitting the assertions corrected it; the next full solution
build passed as shown above. The Python and polluted compiled run passed
while that test-only compile correction was being made. Runtime AppHost,
isolated runner and domain sources were unchanged after the passing live
journey, so the test-harness corrections required no repeat live execution.

All initial failed/blocked attempts and intermediate passes below remain
historical. No unchecked scoped work remains; full legacy/deployed gates
still apply. Final LaunchReadiness and preservation audit follow.

### Historical startup-hardening intermediate verification (2026-10-13)

Separate scoped increment: `spec-3-6-harden-live-dapr-fixture-startup.md`.
The bare fixture flag now fails closed without run mode, validated isolated
components and both explicit private control endpoints. The validated flag
also controls child fixture authorization. The completed fixture spec is
unchanged, as are ordinary defaults, topology/ports, ServiceDefaults,
dependencies, gitlinks, isolated runner and journey assertions. Story 3.6
and Epic 3 stay in progress; release readiness remains **FAIL** for the open
legacy/deployed certification gates.

Evidence root: `/tmp/timesheets-apphost-hardening-20261013`.

| Check | Exact result | Evidence under root |
| --- | --- | --- |
| SDK | `dotnet --version`: 10.0.401, matching `global.json` | pinned source-reference build |
| Source-reference restore | exit 0; 23/28 projects up to date | `restore.log` |
| Debug solution build | exit 0; 0 warnings/errors; 11.68s | `build.log` |
| Architecture | exit 0; 56/56; no errors/failures/skips/not-run; 2.435s | `architecture.log` |
| Compiled configuration-only guards | exit 0; 21/21: 2 selections exit 0, 19 refusals exit -6 | `configuration-guards-retry.log`, `configuration-guards/run-vbqi08b5/guards.json` |
| Default infrastructure-backed guards | exit 1; preflight refused matching `eventstore` ID; zero probes executed | `guards.log`, `guards/run-sv8dl4sk` |
| Isolated live runner | exit 1; same preflight refusal; zero live tests executed | `live.log`, `live/run-6v6b3b9r` |

```bash
dotnet --version
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -warnaserror -m:1 /nr:false
python3 -m unittest discover -s scripts -p 'test_*magic_link*.py' -v
python3 -m py_compile scripts/check_magic_link_apphost_guards.py scripts/test_isolated_magic_link_dapr.py
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/check_magic_link_apphost_guards.py --log-directory /tmp/timesheets-apphost-hardening-20261013/guards
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/check_magic_link_apphost_guards.py --configuration-only --log-directory /tmp/timesheets-apphost-hardening-20261013/configuration-guards
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-apphost-hardening-20261013/live
```

The first focused Python run passed 64/64 in 7.526s. After adding the
configuration-only driver, it passed 65/65 in 7.358s; a later final run is
recorded below. Python compilation and `git diff --check` exited 0.
The first configuration-only attempt (`run-onwjsid6`, exit 1, retained
`configuration-guards.log`) reached duplicate-control-endpoint with an
unset test-only placement value, causing a subprocess-environment TypeError.
All earlier probes exited, their temporary copies were removed, and cleanup
was complete. Both test endpoint properties are now set; Python asserts all
probe environment values are strings. The next attempt passed all 21.

Configuration-only probes run the real compiled AppHost with validation-only
set in every subprocess environment. The early return precedes security and
resource construction; regressed refusal checks therefore cannot launch any
resources. This mode creates only temporary component copies and logs, with
no Aspire/Dapr/Docker/storage commands. `guards-mode.json` identifies the
mode; `guards.json` records every result in a completed suite; failed attempts retain the sanitized command logs. The default guard runner and live
runner retain their preflight, ownership, preservation and cleanup behavior.

Initial AppHost/Dapr inventories were empty. A foreign Tenants AppHost
subsequently appeared with matching `eventstore` ID and occupied port 8080:
`/home/administrator/projects/hexalith/tenants/src/Hexalith.Tenants.AppHost/Hexalith.Tenants.AppHost.csproj`.
Neither refused runner created resources; both `cleanup.json` records have
`complete=true`, `components_created=false`, `containers_removed=0`.
No foreign stop occurred. No shared-state snapshot is claimed for these
preflight refusals; the foreign runtime can change its own services. The
prior 2026-10-12 1/1 live pass remains historical and does not certify this
new increment. No legacy activation, clear, migration, repair or simulated
production authority occurred. Unchanged domain suites were not repeated.

### Historical final reviewed fixture verification (2026-10-12)

The scoped isolation increment is implemented, reviewed and verified. Three
independent review layers ran across three rounds; all retained findings were
corrected and tested. The pre-existing bare `TIMESHEETS_LIVE_DAPR_FIXTURE=1`
direct-start configuration concern is appended to `deferred-work.md`; use the
isolated runner, which always supplies and verifies all private settings.
Story 3.6 remains in progress and release remains **FAIL** because legacy
inventory/authority/quiescence/continuity/pending-delivery recovery and deployed
certification are still open. The fixture spec alone is done. The Git index is
untouched and no commit, push, dependency update or gitlink change was made.

| Final check | Result | Evidence |
| --- | --- | --- |
| Source-reference Debug build | zero warnings/errors; unchanged compiled sources after the review patches | `/tmp/timesheets-isolation-round3-build-final.log` |
| Focused Python | 63/63, no skips; 7.170s | `/tmp/timesheets-isolation-final-python.log` |
| Python compile | exit 0 | all six changed/check scripts |
| Architecture | 56/56, no skips; 1.792s | `/tmp/timesheets-isolation-final-architecture.log` |
| Compiled AppHost guards | 16/16; driver exit 0; preservation and cleanup pass | `/tmp/timesheets-isolation-final-guards/run-c91gu8c4` |
| Latest isolated live journey | 1/1; no errors/failures/skips/not-run; 135.607s; runner exit 0 | `/tmp/timesheets-isolation-final-live/run-wpbnjn90` |
| LaunchReadiness after final evidence edits | 13/13; no skips; 0.116s | `/tmp/timesheets-isolation-final-readiness.log` |

```bash
python3 -m unittest discover -s scripts -p 'test_*magic_link*.py' -v
python3 -m py_compile scripts/run_isolated_magic_link_dapr.py scripts/test_isolated_magic_link_dapr.py scripts/check_magic_link_apphost_guards.py scripts/local_aspire_magic_link_journey.py scripts/magic_link_index_selection.py scripts/test_magic_link_index_selection.py
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -noColor -stopOnFail
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/check_magic_link_apphost_guards.py --log-directory /tmp/timesheets-isolation-final-guards
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-isolation-final-live
```

The patched runner adds case-insensitive/relative query redaction, replacement
UTF-8 decoding, cancellation handling for final snapshots, scheduler cluster
identity and revision evidence, and automatic verification of every captured
PID/start pair before deleting dependencies. Populated DB0/13/14 and changed
scheduler-content regressions now detect omission/ignored-comparison mutations.
The final run's generated `recorded-process-exit.json` reports `all_gone=true`
for all 16 captured identities; `resource-exit.json` is `[]`. `cleanup.json`
confirms all three owned containers and component copies removed, with journey
descendants gone. Final sanitized Dapr/Aspire/owned-container inventories are
empty. Ordinary defaults and all original stored-event, selected-v7,
effective-value/policy/lineage, confirm/adjust/race/replay/rebuild/restart/fresh-use
assertions remain. The six unchanged default suites retain their round-1
1,867 passes/five expected skips; no unrelated domain lane was repeated.

The latest before/after read-only snapshots match DB0 113,753 keys
(113,627 persistent/126 expiring), DB13 3,018 (2,671/347), DB14 773 persistent,
and all 57 scheduler KV entries. Scheduler cluster identity and revision 12535
also match. Retained expiring values/deadlines match with zero natural
expirations. No shared content was repaired, cleared or migrated; raw keys,
values and per-key digests remain in memory only.

Two final failed attempts are retained in the same unique evidence parent:
`run-w06whvgn` exited 125 because Docker could not publish the selected scheduler
port, before AppHost startup; all three owned containers were removed and shared
preservation passed. `run-v44rdkia` passed the live test 1/1 in 129.524s but
returned runner exit 1 because DB0 gained 191 persistent keys and scheduler KV
changed from 54 to 56 entries. DB13/14 and cleanup matched. Shared state kept
changing after the fixture was fully stopped, with empty local Dapr/Aspire
inventories; no cause or responsible workload is attributed. Read-only idle
aggregate snapshots then matched, and a fresh-baseline retry passed. Failed
logs are `/tmp/timesheets-isolation-final-live.log` and
`/tmp/timesheets-isolation-final-live-retry.log`; passing log is
`/tmp/timesheets-isolation-final-live-retry2.log`. The background aggregate
observation is `/tmp/timesheets-isolation-final-background-observation.json`.
Earlier state-store-only and round-2/3 results below remain historical. Final `git diff --check` passed; frozen intent matches the approved block exactly, all scoped tasks are checked, the Git index is empty, and final Dapr/Aspire/fixture-container/component-directory inventories are empty.

### Historical round-3 fully isolated fixture verification (2026-10-12)

All nine amended isolation tasks are implemented against the recorded
`2af8a543229f6427d4b07d1bc592add9f87aa3b2` baseline. Only owned source was
restored from the round-2 backup before applying the round-3 corrections.
The existing graph, application ports, checked-in state components,
ServiceDefaults, SDK, dependencies, gitlinks and production defaults remain.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -m:1 /nr:false -v:q
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -warnaserror -m:1 /nr:false -v:q
python3 -m unittest discover -s scripts -p 'test_*magic_link*.py' -v
python3 -m py_compile scripts/run_isolated_magic_link_dapr.py scripts/test_isolated_magic_link_dapr.py scripts/check_magic_link_apphost_guards.py scripts/local_aspire_magic_link_journey.py scripts/magic_link_index_selection.py scripts/test_magic_link_index_selection.py
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -noColor -stopOnFail
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/check_magic_link_apphost_guards.py --log-directory /tmp/timesheets-isolation-round3-guards
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-isolation-round3-live
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor -stopOnFail
```

| Latest check | Result | Evidence |
| --- | --- | --- |
| SDK 10.0.401 supported source-reference restore | exit 0 | `/tmp/timesheets-isolation-round3-restore-final.log` |
| Debug solution build | zero warnings/errors; 21.11s | `/tmp/timesheets-isolation-round3-build-final.log` |
| Focused Python | 55/55; zero skips; 6.907s | `/tmp/timesheets-isolation-round3-python-final.log` |
| Python compile | exit 0 | exact command above |
| Architecture | 56/56; zero skips; 2.874s | `/tmp/timesheets-isolation-round3-architecture.log` |
| Compiled guards | 16/16; driver exit 0 | `/tmp/timesheets-isolation-round3-guards/run-wjgil617` |
| Fully isolated live journey | 1/1; zero errors/failures/skips/not-run; 124.583s; runner exit 0 | `/tmp/timesheets-isolation-round3-live/run-liwouxcy` |
| LaunchReadiness after evidence edits | 13/13; zero skips; 0.169s | `/tmp/timesheets-isolation-round3-readiness.log` |

Preliminary restore/build without `HexalithCommonsRoot=/tmp/no-commons` also
exited 0, build zero warnings/errors in 12.52s; logs are
`/tmp/timesheets-isolation-round3-restore.log` and
`/tmp/timesheets-isolation-round3-build.log`. Final verification used the
prior supported override to avoid broadening the unrelated Commons graph.
No dependency declarations or pointers changed. Untouched domain suites
were not repeated; their round-1 1,867 passes/five expected skips remain
historical, and the default live skip is unchanged.

The 12 added Python cases preserve all 43 prior checks and demonstrate:
Dapr's case-insensitive YAML/YML enumeration through actual resource files;
complete multiline/nested JSON, YAML-block and URL-userinfo redaction;
nullable/malformed Docker ownership and published-port refusal; command,
resource-exit, cleanup and late preservation evidence-write failure status;
completed/timeout journey shutdown failure with retained sanitized streams;
a real surviving bound subprocess through the production resource scanner;
and valid/invalid scheduler envelope/row/count proof, including actual
HTTP-success error-response rejection and genuine empty ranges. Ordinary
child process-group timeout/interruption/reaping coverage remains.

The compiled driver adds shared Redis and actor/read-model/pubsub endpoint
mismatch probes to its historical 12 cases. All 16 probes exit before
constructing resources; two valid selections exit 0 and fourteen refusals
exit -6 with the expected safe configuration message. Guard cleanup removed
its three owned services/component copies and preservation matched.

The live run verified private Redis :61089, placement :61092 and scheduler
:35059 on 127.0.0.1 before commands, including private scheduler broadcast.
All original stored-event/readback/selected-v7/value-policy-lineage/race/
opaque-denial/rebuild/restart/fresh-use assertions passed. The successful
compiled test checks every journey endpoint marker. `resource-exit.json`
is `[]`; `recorded-process-exit.json` proves all 16 captured PID/start
identities are gone. `cleanup.json` is complete, descendants gone, components
removed and all three containers removed. Saved final Aspire/Dapr and exact
owned-container inventories are empty; their commands exited 0.

Read-only snapshots match DB0 113,492 keys (113,384 persistent/108 expiring),
DB13 3,018 (2,671/347), DB14 773 persistent and all 54 scheduler KV entries,
with zero natural expirations. Permanent values and all retained expiring
values/deadlines match. The counts changed from historical round 2 before
this fixture started; those cross-run changes are not attributed, repaired
or cleared. The validated scheduler range cannot certify an error/unknown
response as an empty success. Raw keys/values/per-key digests remain only
in memory; saved snapshots are aggregates. Fixture stdout/stderr and saved
command arguments are sanitized, including timeout streams and credentials.

Historical state-store-only attempts and round-2 evidence below remain
unchanged. Earlier DB13/14 fingerprints did not prove broker/scheduler
preservation. No earlier control-plane baseline exists and earlier shared
effects were not repaired or cleared. Independent review is pending.
Legacy inventory, actual authority, enforced quiescence, source continuity,
pending-delivery recovery and deployed certification remain open.
**Story 3.6 stays in progress; Story 3.6/release remain FAIL.**

### Historical round-2 fully isolated fixture verification (2026-10-12)

All amended fixture tasks are implemented. AppHost validation precedes
resource construction and preserves ordinary defaults. Private Redis
contains actor/read-model/pubsub state, while private placement/scheduler
containers use the installed immutable images and explicit private
endpoints. Scheduler advertisement uses its own host endpoint. The runner
checks actual loaded components and targets before commands, refuses any
existing AppHost, terminates/reaps journey descendants before exact-target
Aspire cleanup, requires bound resource exit, treats unavailable Docker
ownership as unknown, guards component deletion, retains original nonzero
status and uses unique sanitized evidence directories.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -m:1 /nr:false -v:q
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -warnaserror -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -m:1 /nr:false -v:q
python3 -m unittest discover -s scripts -p 'test_*magic_link*.py' -v
python3 -m py_compile scripts/run_isolated_magic_link_dapr.py scripts/test_isolated_magic_link_dapr.py scripts/check_magic_link_apphost_guards.py scripts/local_aspire_magic_link_journey.py scripts/magic_link_index_selection.py scripts/test_magic_link_index_selection.py
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -noColor -stopOnFail
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/check_magic_link_apphost_guards.py --log-directory /tmp/timesheets-isolation-round2-guards
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-isolation-round2-live
```

| Latest check | Result | Sanitized evidence |
| --- | --- | --- |
| SDK 10.0.401 source-reference restore | exit 0 | `/tmp/timesheets-isolation-round2-restore.log` |
| Debug solution build | zero warnings/errors; 7.30s | `/tmp/timesheets-isolation-round2-build.log` |
| Focused Python | 43/43; zero skips | `/tmp/timesheets-isolation-round2-python-final.log` |
| Python compile | exit 0 | command above |
| Architecture | 56/56; zero skips; 1.931s | `/tmp/timesheets-isolation-round2-architecture.log` |
| Compiled guard checks | 12/12; driver exit 0 | `/tmp/timesheets-isolation-round2-guards/run-smukev4h` |
| Fully isolated live journey | 1/1; zero errors/failures/skips/not-run; 140.368s; runner exit 0 | `/tmp/timesheets-isolation-round2-live/run-wovpme49` |

The checked-in guard driver exercises valid isolated and ordinary-default
selection, missing opt-in, wrong scope, missing state/pubsub component,
unsafe pubsub type, missing/shared placement, missing/shared scheduler and
duplicate control endpoint. It executes the compiled AppHost's validation
probe, which exits before resources; bad guards cannot fall back to retained
storage. Successful probes exit 0; ten refusal probes exit -6 with the
expected safe configuration message. All private infrastructure is removed.

Python checks retain original positives/faults and add real occupied IPv4
and supported IPv6 sockets; actual loaded actor/read-model/pubsub endpoint
mismatches and both sidecars' control targets; real subprocess descendant
timeout/interruption/termination/reaping; unavailable Docker recovery and
exact-name post-removal inspection; surviving-resource retention; component
delete failures and original exit; evidence reuse; zero/skipped/error test
rejection; JSON credentials and both timeout streams; and strict preserved
value/deadline comparisons with only confirmed natural expiry allowed.
The sorted-persistence v7 contract-order regression and token-free diagnostic
categories remain. The live test asserts the isolation endpoint marker and
all original real stored record/issue/delivery/use/race/rebuild/restart
readbacks. Default live/performance/infrastructure skips remain unchanged.
The six unchanged default domain suites passed 1,867 cases/five expected
skips in round 1 and were not repeated; those results are recorded below.

The live run's `runtime-isolation.json` records private Redis :57994,
placement :57996 and scheduler :43793, all on 127.0.0.1. `resource-exit.json`
is `[]`; `recorded-process-exit.json` confirms all 16 captured fixture PIDs,
including the AppHost/dashboard, are gone. `cleanup.json` reports complete,
journey descendants gone, components removed and three containers removed.
Final sanitized `aspire ps --non-interactive --format Json` returns `[]`,
`dapr list` is empty, and exact owned Docker inventories are empty.

Read-only `shared-before.json`/`shared-after.json` cover retained DB0 broker
and general state (113,203 keys: 113,113 persistent/90 expiring), DB13
actor/command state (3,018: 2,671/347), DB14 models (773 persistent), and
all 51 scheduler etcd KV entries. `shared-preservation.json` reports matching
persistent values, matching retained TTL values/deadlines and scheduler KV,
with zero confirmed natural expirations. Raw values, keys and individual
fingerprints are kept in memory only; saved evidence contains aggregates.
The runner's reproducible snapshot uses an atomic read-only Redis script
(KEYS/DUMP/PTTL/TIME) and an etcd v3 all-key range at loopback :2379.

Guard-driver attempts preceding the pass remain at
`run-5ownfzcs` (an expiring key changed during the old sequential capture),
`run-n_jlm6to` (Redis 6.2 lacks EVAL_RO), and `run-fwpoy5gb` (12/12 guards,
cleanup complete, permanent and scheduler fingerprints match, but the
single-initial-TIME TTL calculation could not prove deadline equality).
A controlled consecutive read showed zero TTL value changes and apparent
old-reader deltas of 41–147ms in DB0 and 1–3ms in DB13. Pairing every PTTL
with TIME fixed that measurement; the final guard retry's preservation
matches. No domain command ran in those probes. Their failures are retained,
and no earlier shared data was repaired or cleared.

A manual live inventory printed the run's temporary dashboard login query
in tool output. Saved fixture logs remained sanitized; exact cleanup proves
that dashboard/AppHost process is gone. No domain token or signing key was
printed, and all later inventories were sanitized before output.

Only the latest fully isolated live result supports current task checks.
First-three state-store-only attempts below do not prove previous broker or
scheduler preservation; no earlier control-plane baseline exists.
Independent complete inventory, real operator/resource-binding authority,
enforced writer/delivery quiescence, source continuity/handoff, recovery of
four legacy pending deliveries and deployed certification remain open.
Default authority/inventory bindings remain unchanged. **Story 3.6 remains
in progress and Story 3.6/release remain FAIL**. Independent review is pending.

### Prior state-store-only fixture acceptance (2026-10-12)

The approved component-path exception is implemented without changing the
resource graph, ServiceDefaults, dependencies, SDK or gitlinks. The AppHost
validates exact isolated actor/read-model component copies before resource
construction and requires run-mode opt-in. The disposable fixture uses the
immutable local image installed for `dapr_redis`, Docker-assigned loopback
port, memory-backed `/data`, and no shared volume. The journey verifies
both actual sidecar component files before any domain command.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -m:1 /nr:false -v:q
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -warnaserror -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -m:1 /nr:false -v:q
python3 -m unittest discover -s scripts -p 'test_*magic_link*.py' -v
python3 -m py_compile scripts/run_isolated_magic_link_dapr.py scripts/test_isolated_magic_link_dapr.py scripts/local_aspire_magic_link_journey.py scripts/magic_link_index_selection.py scripts/test_magic_link_index_selection.py
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-isolated-dapr-live-proof-3
```

SDK `10.0.401` restore exited **0**. The initial Debug build passed in
**16.92 seconds** and final build after the live-test marker assertion in
**6.06 seconds**, each with zero warnings/errors. All six default test
executables ran individually and sequentially with `-noColor -stopOnFail`:

| Suite (`tests/Hexalith.Timesheets.<suite>/bin/Debug/net10.0/Hexalith.Timesheets.<suite>`) | Total | Passed | Failed | Skipped | Seconds |
| --- | ---: | ---: | ---: | ---: | ---: |
| ArchitectureTests | 56 | 56 | 0 | 0 | 2.456 |
| Contracts.Tests | 92 | 92 | 0 | 0 | 0.541 |
| Server.Tests | 543 | 543 | 0 | 0 | 0.731 |
| Works.Tests | 76 | 76 | 0 | 0 | 0.324 |
| Projections.Tests | 386 | 386 | 0 | 0 | 540.210 |
| IntegrationTests | 719 | 714 | 0 | 5 | 467.510 |
| Total | 1872 | 1867 | 0 | 5 | |

The five expected skips remain live/performance/infrastructure opt-ins.
Python passed **25/25** and compilation exited **0**. Injected checks cover
component separation and unsafe refusal, foreign IDs/exact AppHost/occupied
ports, failed/interrupted startup and journey, exact owned cleanup, failed
shutdown retention, container identity recovery/refusal, loaded endpoint
verification and credential redaction. The sorted-persistence index
regression additionally requires the canonical C# member order after the
SDK sorts stored JSON properties; corruption and selection-change denials
remain checked.

After the broad suites, actual built AppHost starts were invoked with
`aspire start --no-build --non-interactive --apphost /home/administrator/projects/hexalith/timesheets/src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --format Json`.
Missing fixture opt-in, a Timesheets component with `eventstore` scope and
a missing required component each returned CLI exit **2** with the expected
safe guard message. No AppHost or Dapr sidecar remained after any refusal.
The driver was `python3 /tmp/timesheets-isolated-dapr-guard-checks.py`.

| State-store-only live attempt | Result | Evidence directory |
| --- | --- | --- |
| 1 | 0/1; failed in 72.710 seconds at selected candidate verification | `/tmp/timesheets-isolated-dapr-live-proof` |
| 2 | 0/1; failed in 58.798 seconds with `numeric-node-integrity-invalid` | `/tmp/timesheets-isolated-dapr-live-proof-2` |
| 3 | **1/1 passed in 118.546 seconds; runner exit 0** | `/tmp/timesheets-isolated-dapr-live-proof-3` |

The first two failures were the read-only verifier's nested property-order
mismatch. A sorted-node regression reproduced it before correction. The
passing third run retains real stored recording/issuance, exact selected v7
candidate, independent effective-value/policy and packed-lineage checks,
confirm/adjust, one race winner, opaque replay denial, composed rebuild and
post-restart models/replay/fresh link assertions. No proof was weakened.

Every runner attempt stopped only its bound exact AppHost and removed only
its own container and temporary components. Final `dapr list` reported no
instances; `aspire ps --non-interactive --format Json` returned `[]`; Docker
had no remaining `timesheets-live-dapr-` containers. Read-only whole-value
fingerprints of legacy DB13/DB14 matched the immediate pre-live snapshot,
with unchanged **3,018/773 key counts** after all three attempts. An earlier
offline snapshot had three more DB13 keys; their disappearance preceded
fixture startup and its cause was not established.

Build logs are `/tmp/timesheets-isolated-dapr-{build,final-build}.log`;
suite logs are `/tmp/timesheets-isolated-dapr-<suite>.log`; Python evidence
is `/tmp/timesheets-isolated-dapr-python.log`; actual guard logs are
`/tmp/timesheets-isolated-dapr-guard-{missing-opt-in,wrong-scope,missing-component}.log`.
Preservation summary is `/tmp/timesheets-isolated-dapr-preservation.json`.
Fixture lifecycle/journey logs in each attempt directory are sanitized;
credentials and raw links/tokens are excluded.

After the evidence edits,
`tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor`
passed **13/13 in 0.137 seconds**; log
`/tmp/timesheets-isolated-dapr-readiness.log`. `git diff --check` passed,
new-script whitespace checks passed, and no fixture component directory
remained. The Git index is untouched.

Those first three attempts provided state-store-only functional evidence.
Pub/sub and scheduler were shared: their preservation was not established
by DB13/14 fingerprints or final empty runtime inventories. No earlier
control-plane baseline exists; earlier shared effects were not repaired or
cleared. Only the current fully isolated evidence above now supports the
two materialization live task checks.
Legacy migration, independently verified cross-tenant coverage, real
operator/resource-binding authority, enforced source/delivery quiescence,
owner continuity/handoff, recovery of the four legacy pending deliveries
and deployed certification remain open. Default authority/inventory
registrations are unchanged. **Story 3.6 remains in progress and Story
3.6/release remain FAIL**; isolated acceptance is not production proof.

### Prior post-review v1 migration checks (2026-10-12)

The separate EventStore-backed `migrate-v1` implementation passed final focused
migration/digest checks 64/64 and composed ingress checks 37/37. Three
independent reviews are triaged; all patch findings have regression evidence
in the scoped migration spec. The pre-existing source-owner delivery
continuity/handoff prerequisite is deferred. The final source-reference Debug
solution build passed with zero warnings/errors in 10.07 seconds; all six
individual default suites passed: 1,872 total, 1,867 passed, five expected
skips, zero failures. Exact evidence is appended below.

No live journey was run for this migration increment. Independently verified
complete cross-tenant inventory, real operator authority, enforced source/
delivery quiescence, authoritative owner continuity/handoff and recovery of
the previously recorded four uncovered legacy journal items remain absent.
Default authority denies and the inventory provider is unavailable. Story
3.6 remains in progress and Story 3.6/release remain **FAIL**.

### Previous independent evidence and isolated Dapr run (2026-10-12)

With the `global.json` SDK `10.0.401`, source-reference solution restore
exited 0 and the Debug build with `--no-restore -warnaserror
-p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons
-m:1 /nr:false` exited 0 in 15.96 seconds, with zero warnings/errors.
`python3 -m unittest discover -s scripts -p 'test_magic_link_*.py' -v`
passed **9/9**; compilation of the five journey/helper/test scripts passed.
`tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor`
passed **13/13 in 0.277 seconds** after the evidence edits; `git diff --check`
passed. Its log is
`/tmp/timesheets-story3-6-20261012-implementation-readiness.log`.
The parent supplementary current-tree acceptance matrix passed **10/10 in
0.830 seconds**, with no skips: stored recording/submission/approval,
terminal-first normal delivery and reverse rebuild, changed-position
replay, applied pruning, aborted scratch cleanup, and capability/Time Entry
selected read/write outages. Evidence is
`/tmp/timesheets-story3-6-20261012-parent-matrix.log` and
`/tmp/timesheets-story3-6-20261012-parent-matrix.xml`.
The effective-value verifier independently requires the expected host
publication flag and selected policy version, exact authorized comments,
safe omission and decision state for excluded/legacy/default comments,
comment removal, and later approval/rejection state. Existing V7 numeric
candidate and packed-lineage checks remain.

Empty Dapr/Aspire preflight preceded an exact-target Timesheets AppHost
start with `TIMESHEETS_LIVE_DAPR_FIXTURE=1`. Both projects were healthy;
Dapr contained exactly one `eventstore` (HTTP 31640, app 8082) and one
`timesheets` (HTTP 31643, app 8081). The existing opt-in xUnit command
failed **0/1 in 76.410 seconds**. Catalog, non-fixture denial, and recorded
Time Entry effective-value/lineage checks passed before index delivery
failed with `reason=delivery_state_unavailable;
legacy-direct-root=present-unverified entries=72`. V7 selected candidate,
confirm/adjust effect, and restart acceptance were not reached. The exact
AppHost stop exited 0; `dapr list` reported no instances and Aspire returned
`[]`. No Redis state was cleared. Both live spec tasks remain unchecked,
and Story 3.6/release remain `FAIL` with story/spec status in progress.

The handler rejects this direct v1 root during ordinary bootstrap and
tenant-slice rebuild. A certified full cross-tenant legacy inventory and
authorized migration operator are absent; a tenant-only capture cannot
certify hidden collision contributions. Production authority, writer
enforcement/census, and deployed shared rebuild remain open. Runtime C#
was unchanged; prior broad test runs are retained below.

Commands, lifecycle details, and limits are in
[the local proof](../../../docs/local-magic-link-dapr-proof.md).
Exact logs are `/tmp/timesheets-story3-6-20261012-implementation-{restore,build,python,live,stop}.log`.
The start log redacts credentials and the description log retains only safe
resource metadata.

### Earlier isolated Dapr runs (2026-10-12)

`TIMESHEETS_LIVE_DAPR_FIXTURE=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home aspire start --no-build --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive --format Json`
started one Timesheets AppHost. `dapr list` showed one `eventstore` on app
port 8082 and one `timesheets` on app port 8081. The opt-in command was
`TIMESHEETS_LIVE_DAPR=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests' -noColor`.

| Run | Result |
| --- | --- |
| 1 | Failed in 45.166 seconds. Catalog delivery and non-fixture denial printed. The idempotent recording retry returned 403. |
| 2 | Failed in 79.433 seconds after the recording retry used the same bounded 403 wait. Index `/project/v2` returned status 2, `delivery_state_unavailable`. |
| 3 | After a 70-second pause, failed in 67.923 seconds (68.177 seconds wall clock) with the same index outcome. |

The direct state key `timesheets:magic-links:token-hash-capability-index:v1`
still holds 72 entries. `HasSelectedLegacyIndexAsync` throws
`SharedProjectionReadUnavailableException` for that key, and the dispatcher
maps it to `delivery_state_unavailable`. The V7 format and root were absent,
all 256 shard copy-on-write pointers were absent, and the global epoch
journal held 4 unprepared issuance deliveries. Redis was not cleared.
`python3 -m unittest test_magic_link_index_selection.py test_magic_link_evidence_lineage.py -v`
from `scripts/` passed 8/8. `aspire stop --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive`
then left `dapr list` empty and `aspire ps --non-interactive --format Json`
as `[]`. The two live spec tasks stay unchecked. Story 3.6 and release
remain `FAIL`.

### Round-24 directory and tenant-bounded rebuild proof (2026-10-12)

The source-reference Projections test project built with `--no-restore
-warnaserror -p:UseHexalithProjectReferences=true
-p:HexalithCommonsRoot=/tmp/no-commons -m:1 /nr:false -v:q`: zero warnings
and errors. `dotnet test ... --no-build --filter
FullyQualifiedName~TenantReplacementPreservesCandidatesBeyondFourHundredGlobalPages`
exited 1 before discovery because Microsoft.Testing.Platform rejects the
VSTest target on .NET 10; verification used the built xUnit executable. The
direct test executable passed the complete
`MagicLinkNumericIndexTests` class **3/3 in 19.755 seconds**. Its large test
now drives the actual index handler's streamed tenant finalization for four
tenants of 8,700 contributions each, producing more than 400 selected global
roster pages. It repeatedly replaces and prunes A and B, restores A, verifies
unrelated C and D candidates and shared-hash ambiguity, and bounds the
large replacement operation count below three times one tenant's inventory.
`MoreThanFourHundredNumericDirectoryPagesRemainSelectable` passed **1/1 in
0.358 seconds** with 401 selected numeric pages in one shard, two tenant
identities, an integrity-checked interior page, and resolution through the
concrete loader. `OneShardTenThousandDistinctMaximumOwnersUseBoundedDirectoryPages`
passed **1/1 in 8.015 seconds**: all 10,000 selected roster contributions
match the rebuild input and each resolves to its expected unique candidate;
an interior candidate also resolves through the concrete loader.
The exact executable selections were:

```text
tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -class Hexalith.Timesheets.Projections.Tests.MagicLinkNumericIndexTests -noColor
tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -method '*MoreThanFourHundredNumericDirectoryPagesRemainSelectable' -noColor
tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -method '*OneShardTenThousandDistinctMaximumOwnersUseBoundedDirectoryPages' -noColor
```

The full Projections executable
`tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -stopOnFail -noColor`
exited 0: **322/322 passed in 453.930 seconds**. The earlier intermittent
SDK promoted-value retirement failure did not recur in either the focused
tenant test or the full lane; its underlying batch result remains unknown.
The SDK's direct executables passed
`-class Hexalith.EventStore.Client.Tests.Projections.SharedProjectionEpochCoordinatorTests -noColor`
**20/20 in 0.349 seconds** and
`-class Hexalith.EventStore.DomainService.Tests.DomainPartitionedSharedProjectionRebuildTests -noColor`
**37/37 in 110.861 seconds**. These cover epoch retry, promotion/cleanup,
tenant-scoped global operation identity, and partitioned rebuild behavior.
The executable paths were
`references/Hexalith.EventStore/tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests`
and
`references/Hexalith.EventStore/tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests`,
respectively.
At this round the live Dapr journey was still blocked by the
unrelated Folders runtime's duplicate `eventstore` ID. The later isolated
runs above are the current live evidence. Story 3.6 and release
remain `FAIL`.

### Round-23 V7 bootstrap and effective-value checks (2026-10-12)

The full default Integration lane after the saved V7 edits exited 0:
**702 passed, five expected opt-in skips out of 707, in 515.854 seconds**.
It began before the final bootstrap performance edit. A source-reference
solution build after that edit passed with `--no-restore -warnaserror`, zero
warnings/errors. Focused Projection checks passed **3/3** for fresh V7
bootstrap, direct legacy-root denial, and a legacy value in the final shard.
The first-issuance legacy scan now reads all 256 possible shards in bounded
groups of 16 concurrent reads. This addresses a plausible cause of the
earlier `/project/v2` timeout; it has not been validated in a live run.
Narrow Python V7 selection and effective-evidence tests passed **8/8**,
including an interior candidate, cross-tenant ambiguity, server Base64 path
encoding, and an adjusted comment. `py_compile` and both repositories'
`git diff --check` passed. The full default Integration rerun against the
final bootstrap edit also exited 0: **702 passed, five expected skips out of
707, in 504.340 seconds**.

The local Dapr preflight cannot safely launch Timesheets: the unrelated
Folders AppHost owned a Dapr `eventstore` ID on app port 8080, while the
Timesheets journey requires exactly one `eventstore` ID on app port 8082.
Folders then briefly disappeared and restarted within ten seconds of two
empty preflight reads, so a stable isolated run could not begin. Folders was
left untouched and no Timesheets AppHost was started. No latest-tree live candidate or
effective-value readback is claimed. Story 3.6 and release remain `FAIL`.

### Round-22 bounded V7 tenant index and latest-tree attempt (2026-10-12)

The source-reference solution build passed with `--no-restore -warnaserror`
and zero warnings/errors. A V7 copy-on-write trie now uses bounded tenant
rosters and immutable node references. Focused verification passed:
Architecture **56/56**, Contracts **92/92**, Server **543/543**, and Works
**76/76** passed in their default lanes. The concentrated 10,000-owner
rebuild passed **1/1**; normal
10,000-candidate HTTP delivery **1/1 in 35.811 seconds**; selected-generation
promotion race **1/1**; ordered insertion across equal-prefix pages **1/1**;
and the more-than-400-roster-page tenant replacement **1/1** in isolated
runs. Narrow Python index and lineage tests passed **6/6** and `py_compile`
passed. The fresh full Projections command
`tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -noColor`
exited 1: **320 passed, one failed out of 321, in 522.274 seconds**. The
only failure was the new tenant replacement test: SDK promoted-value
retirement returned non-success during `BeginAsync` for `replace-tenant-a`.
The cleanup outcome remains under investigation. The tenant-bounded rebuild
task remains unchecked until this is resolved.

A fresh diagnostic rerun with the SDK result status included in any
retirement failure passed **321/321 in 517.904 seconds** with
`-stopOnFail -noColor`; the intermittent failure did not reproduce, so its
underlying batch status is still unknown. No full default Integration lane
was run after the V7 tree edits; its opt-in live test failed as recorded
below.

The latest-tree isolated Dapr journey failed **0/1 in 38.545 seconds** and
**0/1 in 77.228 seconds** at capability-index `/project/v2` delivery with
`delivery_state_unavailable`; it did not reach V7 candidate or effective
Time Entry readback. EventStore logs showed pending named-projection retries,
500/503 responses, actor rate limiting, and a host timeout. The Timesheets
AppHost was stopped; Dapr and Aspire returned empty inventories, and Redis
was not cleared. The failed attempts are recorded in
[the local proof](../../../docs/local-magic-link-dapr-proof.md). Story 3.6,
deployed acceptance, and release remain `FAIL`.

### Round-21 selected lookup and effective-value checks (2026-10-12)

The source-reference solution built with `--no-restore -warnaserror` and zero
warnings or errors. All six default Timesheets test executables exited 0;
the Integration lane reported its five expected opt-in skips, including the
live Dapr journey. Focused global-index tests passed **9/9**, including more
than 400 numeric directory pages, all 10,000 distinct one-shard contributions,
an interior-page loader candidate, and ordered insertion across a same-hash
page boundary. The selected-generation promotion race passed **2/2** for
inline and directory lookup. Record submission receipt tests passed **14/14**
and the adjustment policy-change HTTP test passed **1/1**. Narrow Python
lineage and selected-index tests passed **5/5**; `py_compile` and both
repositories' `git diff --check` passed.

The tenant-slice global rebuild capacity task remains open. The current index
rebuild still reads every selected directory and collision page, materializes
all owners in one list, and rewrites unrelated owners' admission markers. The
SDK's `ReadGenerationAsync` reads only the requested generation's physical
key, while streamed staging writes only supplied mutations. A sparse stage
therefore makes unchanged admissions and pages disappear after promotion.
Safe bounded replacement requires an explicit generation inheritance or
immutable-page-reference contract with deletion masking, a tenant-owned
prior-contribution roster, and bounded index manifests, plus retry and
cross-tenant tests. No such contract is implemented or claimed here.

The latest-tree isolated Dapr journey remains unrun because the unrelated
Folders runtime still owns a duplicate `eventstore` Dapr ID. It was left
untouched. Story and deployed launch gates remain `FAIL`.

### Round-20 selected integrity and receipt verification (2026-10-12)

The current source-reference solution build passed with `--no-restore
-warnaserror -p:UseHexalithProjectReferences=true
-p:HexalithCommonsRoot=/tmp/no-commons -m:1 /nr:false`, zero warnings and
errors. All six default Timesheets test executables exited 0 individually:
ArchitectureTests, Contracts.Tests, Server.Tests, Works.Tests,
Projections.Tests, and IntegrationTests. The Integration run retained its
five declared infrastructure/performance skips. The runner's quiet reporter
did not print totals, so no new overall case count is claimed here.

Focused checks passed: global index epoch **7/7**, including concentrated
10,000-owner collision pages and 10,000 distinct maximum-length owners in
one shard; normal mapped 10,000 high-entropy candidate delivery **1/1**;
normal mapped 65 same-shard hashes and concrete directory resolution before
and after tenant rebuild **1/1**; persisted adjustment comment tampering
**3/3**; record submission receipt class **13/13**; comment policy suffix
replay **1/1**; and EventStore sessionless cross-tenant global Abort **1/1**.
The narrow Python effective-value and packed-lineage suite passed **3/3**,
`py_compile` passed, and both Timesheets and EventStore `git diff --check`
passed. The selected index now verifies complete directory/collision page
manifests and content digests; persisted comments are compared to an
independently issued server decision and recording event IDs must be nonblank
and distinct. The policy toggle requires full owner replay before an enabled
version can be selected.

The latest-tree isolated Dapr journey has not run yet. Preflight found an
unrelated Folders Aspire runtime with its own Dapr `eventstore` ID. The
journey requires a unique `eventstore` ID; that runtime was left untouched.
No local Redis projection state was cleared. Deployed inventory, authority,
and certified shared rebuild remain open; Story 3.6 and release remain
`FAIL`.

### Round-19 five-task implementation (2026-10-12)

The restored round-18 tree plus the round-19 changes built through the
source-reference Debug solution with `--no-restore -warnaserror
-p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons
-m:1 /nr:false`: zero warnings and errors. All six default Timesheets
executables passed: Architecture **56/56**, Contracts **92/92**, Server
**535/535**, Works **76/76**, Projections **307/307** in 448.086 seconds,
and Integration **693 passed, five declared skips** out of 698 in 470.414
seconds. Overall: **1,759 passed, five skipped, zero failed**. The final
build and targeted composed owner-duplicate route passed after the last
owner-admission change. Both repositories pass `git diff --check`.

The one-shard capacity test staged, promoted, and read back 10,000 distinct
128-character hashes with maximum-length owner IDs. Numeric directory pages
kept every canonical staged value below 1 MiB and returned the exact selected
candidate; the earlier 10,000-owner concentrated same-hash page case also
passes. A composed route test covers a later issuance for an owner on an
earlier page and collision ambiguity. HTTP tests cover required redaction
despite positive server authority and an explicitly cleared replacement
comment through a second link. EventStore global-epoch cross-tenant tests
passed **3/3**, including the same operation ID and empty inventory case.
Python syntax and evidence-lineage tests passed **2/2**.

The first latest-tree Dapr attempt failed **0/1 in 68.606 seconds** when
the second recording stayed 403; a restart attempt failed **0/1 in 40.834
seconds** on a 503 negative owner read, and a five-second diagnostic retry
failed **0/1 in 19.560 seconds** at the same read. EventStore warnings showed anonymous
actor 429 limits and pending projection retries; the owner-read 503 was
backed by actor 429 exceptions. The fixture now waits across the configured
60-second anonymous tenant rate-limit window for recording and actor reads,
without changing authorization. The final isolated journey passed **1/1 in
106.313 seconds** with one `eventstore` and one `timesheets` Dapr ID. It
verified persisted confirm/adjust, race, restart, and selected readbacks.
The Timesheets AppHost was stopped afterward; Dapr and Aspire were empty.
The round-18 user-requested repeat failure is retained in the local proof.
Deployed owner inventory, installation authority, and a certified live shared
rebuild remain open. Story 3.6 and release remain `FAIL`.

### Round-18 implementation (2026-10-12)

Before the numeric page-address correction, the source-reference solution build with `--no-restore -warnaserror` passed
with zero warnings and errors. Timesheets Architecture 56/56, Contracts
92/92, Server 533/533, Works 76/76, and Projections 305/305 passed. Full
Integration ran 696 cases: 691 passed, five expected skips, zero failed.
The full Projections run took 360.472 seconds and the full Integration run
399.597 seconds; focused checks after their final related edits passed.

The new 10,000-owner same-hash capacity test passed SDK staging, promotion,
all selected-page readbacks, and tenant-slice prune while preserving an
unrelated hash. The global-index class passed 4/4, the new HTTP
server-issued display theory 2/2, and the state-loader class 88/88. Positive
external-record and magic-link-adjustment HTTP cases read back exact
server-stamped comments and accepted responses. EventStore's changed
idempotency class passed 27/27; a mapped coordinator test now accepts the
9,999-to-10,000 transition and rejects event 10,001 before route dispatch
(1/1 after a zero-warning test-project build). Both repositories pass
`git diff --check`.

The selected-page address was then corrected to use a fixed shard ordinal
and allocated numeric page ID; the hash-to-page manifest and page hash
binding remain inside private index values. Old hash-derived v3 page
manifests have no numeric ID map and fail closed. The affected Integration,
Projections, and Server test projects each built with `-warnaserror` and
zero warnings/errors. Focused global-index tests passed 4/4, the composed
normal route with two paged hashes in one shard passed 1/1, and the loader
class passed 89/89. The 10,000-owner concentrated test passed 1/1 after
checking that a stage retry leaves the previous selection intact until
commit. The broad lanes above were not repeated after this focused fix.

Selected legacy v2 index rows containing only a unique owner remain
unverified and fail closed. The committed baseline had no v2 selected shard,
and the current tenant-scoped capture cannot prove the absence of hidden
cross-tenant collisions. The isolated round-18 Dapr journey passed **1/1
in 118.195 seconds** with one app ID per resource, persisted
confirm/adjust, race, restart, and selected readbacks. Dapr and Aspire were
empty after stopping the AppHost. A subsequent warning-as-error
source-reference build succeeded without changing the live host binary
hashes. No deployed source inventory/authority certification is claimed;
Story 3.6 and release remain `FAIL` pending those gates.

### Round-17 seven-task implementation (2026-10-12)

The restored round-16 code plus the round-17 fixes build from source references with `dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -m:1 -nodeReuse:false -p:UseSharedCompilation=false -warnaserror`: zero warnings and errors. Full Timesheets lanes passed on the round-17 implementation for Architecture 56/56, Contracts 92/92, Server 530/530, Works 76/76, and Projections 304/304. Subsequent narrow fixture and validation edits passed their focused classes, without repeating the six-minute Projections lane. The full Integration run executed 694 cases: 687 passed, five declared skips, and two E2E assertions compared raw caller comments against newly server-stamped committed comments. After correcting those assertions, both pass in a focused fresh-build rerun, together with the new coordinator failed-commit/restart case (3/3). Normal mapped index poison, cross-tenant collision, and composed restart cases pass in focused runs. Both repositories pass `git diff --check`; the OpenAPI JSON parses.

The new regressions cover persisted ambiguity across tenant slices and prune, fail-closed legacy unique-only index rows, poison-event parking through `/project/v2`, server-owned comment decisions for external recording, magic-link adjustment, and correction, equal-lineage policy migration and removal, prior-tenant scratch cleanup, old MessageId reuse outside the receipt tail, duplicate domain metadata, and exact Time Entry row/event/payload lineage after a failed coordinator commit and host restart. The selected Time Entry policy defaults to no text publication; historical events without a server decision remain undisclosed. Independently certified deployed owner inventory and authority remain release gates.

After that in-process verification, the isolated round-17 Dapr journey passed **1/1 in 127.469 seconds** with exactly one `eventstore` and one `timesheets` app ID. It checked persisted confirm/adjust, race, EventStore restart, and selected readbacks. The Timesheets AppHost was stopped; `dapr list` and `aspire ps --format Json` were empty afterward. This local run does not certify deployed coverage, authority, or a live shared rebuild.

The full EventStore Client run reported 1,488/1,489 passing; its `SharedProjectionChunkedStageTests.TenThousandAggregateManifestPromotesOneGenerationAndSealsChunks` cleanup-timing assertion passed 1/1 when isolated. The DomainService full run reported 752/753 passing; its only failure is `DomainModuleAuthoringGuardrailTests.TenantsDomainService_DoesNotReferenceGeneratedApiHostOrDeclarePerMessageControllers`, which requires an absent nested Tenants checkout. Repository instructions prohibit initializing that nested submodule. Focused tests for the changed SDK paths passed. These full-lane failures do not establish a regression in the round-17 paths.

The EventStore Server full run reported 4,428 passed, one failed, and 25 declared skips out of 4,454. Its sole failure, `ProjectionDeliveryIdempotencyCoordinatorTests.AnchoredSuffixAdvancesDurablePrefixWithoutReplayingPriorEvents`, seeded an old completed-state fixture without the new full-prefix MessageId digest, so the coordinator correctly withheld incremental admission pending a full replay. The fixture now includes the digest, and the test class passes 26/26 after a zero-warning Server.Tests build. A separate assertion pins the old fixture's fail-closed full-replay fallback. The ten-minute full Server lane was not repeated after this focused repair.

### Round-16 global index and incremental mapped delivery (2026-10-12)

The source-reference solution built with `--no-restore -warnaserror
-p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons
-m:1 /nr:false`, zero warnings and zero errors. All six default executables
passed on the current tree: Architecture 56/56, Contracts 92/92, Server
523/523, Works 76/76, Projections 301/301, and Integration 687 passed with
five expected skips out of 692. Overall: **1,735 passed, five skipped, zero
failed**. Both repositories pass `git diff --check`.

Focused SDK and composed-host tests cover the 256-shard selected global
index, exact 10,000 high-entropy candidates through normal `/project/v2`
delivery, cross-tenant collision and tenant-slice prune, versioned source
identity, the 9,999→10,000 mapped suffix with bounded source/HTTP calls,
failed commit and restart, and coordinator-produced stage through the
authenticated host. The actor-grounded composed producer passed both two
and 10,000-owner cases in 359.474 seconds with a longer TestHost client
timeout. This is test coverage, not deployed source certification.

The first isolated local Dapr run failed **0/1 in 46.255 seconds** because
the fixture still read the retired single index key. After it was changed
to read the selected global shard, the rerun passed **1/1 in 116.727
seconds** with one `eventstore` and one `timesheets` Dapr ID, persisted
confirm/adjust, the one-winner race, restart, and exact selected readbacks.
Dapr and Aspire were empty after stopping this AppHost. This does not certify
deployed source coverage, authority, or a live shared rebuild. Story 3.6
and release remain `FAIL`.

### Historical round-15 bounded mapped delivery and replay (2026-10-12)

The source-reference solution built with `-warnaserror`, zero warnings, and
zero errors. Six default executables passed: Architecture 56/56, Contracts
92/92, Server 523/523, Works 76/76, Projections 293/293, and Integration
682 passed with five expected skips out of 687. Overall: **1,722 passed,
five skipped, zero failed**. The Architecture lane first found two stale
readiness prose assertions; those assertions were corrected, rebuilt with
zero warnings, and the lane then passed 56/56.

Focused EventStore coordinator 28/28, retry worker 10/10, orchestrator
79/79, catalog 8/8, DomainService host 89/89, and part-route authorization
14/14 passed. A real actor-backed ordinary source path delivered over 64 MiB
without the legacy whole-history array; a composed Timesheets host covered
10,000-event mapped stage/commit/verify/readback, all three named routes,
partial failure, and durable retry. A focused Dapr selected-read outage
regression passed after the broad suites.

The isolated local Dapr journey passed **1/1 in 112.376 seconds** with one
`eventstore` and one `timesheets` app ID, persisted confirm and adjust, the
one-winner race, restart, and exact selected readbacks. Dapr and Aspire were
empty after stopping the AppHost. [The local proof](../../../docs/local-magic-link-dapr-proof.md)
records commands and limits. This does not certify deployed coverage,
authority, or a live shared rebuild. Story 3.6 and release remain `FAIL`.

### Historical round-14 source recovery and exact selected evidence (2026-10-12)

The Timesheets source-reference solution built with `-warnaserror`, zero
warnings, and zero errors. All six default executables passed on the round-14
tree: Architecture 56/56, Contracts 92/92, Server 523/523, Works 76/76,
Projections 274/274, and Integration 682 passed with five expected skips out
of 687. Overall: **1,703 passed, five skipped, zero failed**. After the
readiness table was refreshed, the affected Architecture fitness class was
rebuilt with `-warnaserror` and passed 13/13. Both repositories pass
`git diff --check`.

EventStore's partitioned rebuild class passed 32/32, source producer 17/17,
and roster actor 7/7. The actor-grounded Timesheets integration passed 2/2,
including a 10,000-owner case. These cover pre-session Begin-page reclaim,
SDK phase receipts and sealed rejection recovery, and bounded producer owner
metadata. Focused Time Entry, capability selected-row, issue readback,
confirm/adjust command correlation, configured actor-name, OpenAPI, and
Python exact lineage checks passed.

The opt-in local Dapr journey passed **1/1 in 67.664 seconds** on this tree.
Its first attempt failed 0/1 in 117.700 seconds on a transient transport
failure in the added post-restart lineage stream probe; the bounded read-only
retry was then added. The passing rerun verified persisted confirm/adjust,
the race, EventStore restart, exact selected Time Entry lineage, and
capability readbacks. Strict pre/post checks each found one `eventstore` and
one `timesheets` Dapr ID; both Dapr and Aspire were empty after stopping the
Timesheets AppHost. [The local proof](../../../docs/local-magic-link-dapr-proof.md)
records the command. Deployed source coverage, authority, selected readback,
and a certified live shared rebuild remain open. Story 3.6 and release stay
`FAIL`.

### Historical round-13 paged Begin and selected repair (2026-10-12)

The Timesheets source-reference solution built with `-warnaserror`, zero
warnings, and zero errors. All six default executables passed on the round-13
tree: Architecture 56/56, Contracts 92/92, Server 523/523, Works 76/76,
Projections 267/267, and Integration 679 passed with five expected skips out
of 684. Overall: **1,693 passed, five skipped, zero failed**. Both
repositories pass `git diff --check`. Focused EventStore Client, Server,
Contracts, and DomainService regressions passed, including the paged 10,000
maximum-length erased-owner Begin, 1,001 colliding source owners, reserved
capture admission under pending writes, and nonzero global-position JSON.
The partitioned DomainService class passed 31/31. The Projections lane covers
legacy writer lineage migration, corrupted selected capability repair with a
pending valid journal event, and fragment-read promotion races. Integration
covers a lost issue response and exact stored command identity.
After refreshing the launch-readiness evidence, the affected Architecture
fitness class was rebuilt with `-warnaserror` and passed 13/13.

The opt-in local Dapr journey passed **1/1 in 125.174 seconds** on this tree.
It verified persisted confirm/adjust, the one-winner race, EventStore restart,
and selected Time Entry and capability readbacks. Strict pre/post checks each
found one `eventstore` and one `timesheets` Dapr app ID; both Dapr and Aspire
were empty after stopping this Timesheets AppHost. [The local proof](../../../docs/local-magic-link-dapr-proof.md)
records the command. This does not certify deployed source coverage, a live
shared rebuild, or deployed authority; Story 3.6 and release remain open.

### Historical round-12 bounded source rebuild (2026-10-12)

The Timesheets source-reference solution built with `-warnaserror`, zero
warnings, and zero errors. All six default executables passed: Architecture
56/56, Contracts 92/92, Server 523/523, Works 76/76, Projections 264/264,
and Integration 675 passed with five expected skips out of 680. Overall:
**1,686 passed, five skipped, zero failed**. The full Integration run preceded
the final EventStore hash-page edit; final-tree composed near-limit
commit/readback, cursor-tamper, and scratch-cleanup tests passed afterward.
Both repositories pass `git diff --check`.

The EventStore partitioned DomainService class passed 30/30; later focused
hash-page tamper, 30,008-chunk fenced-manifest, page-boundary commit cleanup,
and near-limit abort/retry checks passed. The source producer/gateway class
passed 16/16. A 10,000-owner test streamed three cryptographic event payloads
per owner with one active history reader at peak. A 10,000-event owner test
split two 1,000,000-byte random events into separately bounded serialized
requests. A 64 × near-1-MiB random-event case completed, committed, and read
back the last payload hash. Timesheets streamed 10,000 terminal contributions
without retaining an owner-wide view map; 4,000 contributions matched the
existing fold byte-for-byte. A composed chunked issuance and Time Entry
terminal owner rebuilt to the selected Used row. The maximum modeled 30,008
output chunks use paged hashes, and the owner manifest passed the actual
1-MiB fenced SDK batch gate. Commit and abort cleanup reclaimed pages and
receipts. Production installation authority still defaults deny.

The passing local Dapr journey below is round-11 evidence. The round-12 tree
has no new live Dapr run or certified shared-rebuild coverage; Story 3.6 and
the release decision remain open.

### Historical round-11 isolated magic-link Dapr journey (2026-10-11)

The opt-in `MagicLinkLiveDaprTests` executable invocation passed **1/1 in
114.866 seconds** with `TIMESHEETS_LIVE_DAPR=1`. Strict `dapr list` checks
before and after the journey each found one `eventstore` and one `timesheets`
app ID; after `aspire stop`, Dapr and Aspire lists were empty. The fixture
checked real persisted global-position continuity, both persisted confirm
and adjust batches, one-winner confirm race, restart, and exact selected Time
Entry and capability readbacks. The exact command and earlier failed attempts
are in [the local Dapr proof](../../../docs/local-magic-link-dapr-proof.md).

Two preceding isolated current-tree retries failed `[403,403]` at the confirm
race. The first had stored recording, use, confirmation, and competing commit
rejection: a delayed/lost POST response prevented the committed winner's 202.
Concurrent exact-status polling plus stored-event readback fixed that case;
four focused HTTP boundary tests and ten submission-service tests passed.
The second had only the recording event: signed workload stream reads received
429 and exhausted the transport timeout before submission. EventStore's
limiter treated DaprInternal workload calls as anonymous. The finite limiter
now keys them by the verified bound tenant and workload identity; its
configured-limiter regression passed 1/1. XML comments were checked on all
added public and internal source declarations in the modified and new
Timesheets and EventStore files; missing comments on four internal helpers
were added. The affected Timesheets Integration,
Server.Tests, and host builds and the EventStore host, Server.Tests, and root
AppHost builds passed with zero warnings/errors. The EventStore host rebuilt
after the comment edits with zero warnings/errors. A broader EventStore
IntegrationTests build under `-warnaserror` remains blocked by historical
Admin.UI/Sample `MSB3277` dependency conflicts and missing Sample.Api assets;
it is not counted as a passing lane.
The updated launch-readiness fitness class passed **13/13** after its two
historical no-live-pass assertions were revised; its affected project built
with zero warnings/errors. `git diff --check` passed in both repositories.

This passing local journey is distinct from a certified shared-rebuild Dapr
run and deployed acceptance. No production installation coverage receipt
issuer, independent virgin-store/all-writer certification, complete legacy
actor-key census, or deployed exact-binding identity issuer is installed.
Deployed selected readback and legacy terminal-writer retirement remain open.

### Round-11 source inventory producer (2026-10-11)

The final Debug source-reference Timesheets solution build passed with zero
warnings and errors. All six test executables passed: Architecture 56/56,
Contracts 92/92, Works 76/76, Server 523/523, Projections 258/258 (including
the greater-than-64-MiB case), and Integration 671 passed with five declared
skips out of 676. The total is **1,676 passed, five skipped, zero failed**.
Affected EventStore source-reference suites passed Client shared projection
87/87, DomainService shared rebuild 43/43, and Server source/route/erasure
61/61. The earlier round-10 full DomainService run remains historical at
732/734 with two unrelated guardrail/catalog failures; no broad EventStore
rerun was required for this increment.

The EventStore source roster registers owner writes before persistence,
persists a 64-page source cut, releases appends only after SDK Begin, and
holds erasure until every owner manifest is accepted. Its recovery state
survives restarted actor instances and producer retries after lost Begin,
Accumulate, Finalize, Stage, and Commit responses. A certified clean roster
accepted 10,000 owners and rejected an omitted captured owner. The operator
route rejected non-administrators and noncanonical tenant/domain scope; the
Dapr gateway rejected tenant, domain, and version-mismatched registrations.
The composed Timesheets capability handler rebuilt two and 10,000 high-entropy
owners through the real DomainService dispatcher with selected readback
(2/2, 307.2 seconds); the two-owner case then rebuilt completed erasure
tombstones and pruned its previously selected row. Focused EventStore source,
route, and erasure tests passed 15/15 after the frozen-count correction.
The producer also denies domains without mandatory writer-fence configuration.
No production `IInstallationCoverageAuthority` receipt issuer, certified
deployed legacy inventory, or live Dapr rebuild is present. Those remain
release gates.

### Round-11 partial implementation verification (2026-10-11)

The preserved round-10 Timesheets and EventStore code patches applied cleanly.
The Timesheets Debug source-reference solution built with zero warnings/errors
using the command below. The focused Time Entry handler class passed 41/41;
the new selected terminal-cursor test and existing terminal-first regression
passed 2/2; the re-signed compact ordinal test passed 1/1. The launch-readiness
fitness class passed 13/13 after its current-language assertion was updated.
The EventStore Client shared-epoch coordinator class passed 19/19. Python
compilation of the updated opt-in journey passed; no latest-tree Dapr journey
ran, so restart and rate-limit behavior remain unverified in the live fixture.
A full Projections executable run was voluntarily canceled after roughly three
minutes in its 10,000-owner capacity case because the changed code does not
affect that capacity path; the runner reported an `OperationCanceledException`
and exit 58. That run is **not** counted as a passing full suite.
The Time Entry handler now fails older-prefix delivery closed while preserving
the selected longer row, including when its approval field was changed and its
storage digest recomputed. EventStore DomainService builds with zero warnings
and errors; the focused independent scratch worker fault/retry and restarted
cleanup tests pass 2/2. The 10,000-owner high-entropy fault case passed 1/1
in 87.3 seconds after Commit stopped waiting for private scratch deletion; it
passed again in 88.0 seconds after the durable work index was added. The hosted
DomainService sweep scans a bounded durable work index every 30 seconds and
retries failed private cleanup after a response or restart. At that partial
verification point, the production shared-rebuild Begin inventory producer
was still open. The later source-inventory increment above adds its code
path; independent deployed coverage certification and live proof remain open.
Story 3.6 and launch remain open.

### Round-10 worktree verification (2026-10-11)

The Timesheets Debug source-reference solution built with zero warnings and
errors using `-p:HexalithCommonsRoot=/tmp/no-commons`. The default
Architecture, Contracts, Server, Projections, Works, and Integration suites
passed **56/56, 92/92, 523/523, 255/255, 76/76,
and 669 passed with five declared skips**, respectively: **1,671
passed, five skipped, zero failed**. The Projections full executable included
the greater-than-64-MiB capacity case; its standard lane separately passed
254/254 after the quiet full run exited zero. The first full
Integration run exposed two test fixtures lacking the new authoritative source
head; both fixtures were corrected and the full suite reran with zero failures
(`674` total in the JUnit report).

EventStore Client passed **1,487/1,487**. EventStore DomainService passed
**732/734**; only the two previously recorded missing nested-Tenants guardrail
subject and named-endpoint catalog-registry assertion failed. The strengthened
10,000-owner high-entropy rebuild capacity case passed **1/1 in 91.779 seconds**:
committed scratch cleanup failed on purpose, selected reads and live delivery
continued, a new epoch began, and retry removed old private scratch for all
10,000 captured owners. Focused SDK checks passed for actor-proven empty Time Entry deletion versus active
empty rejection through the normal mapped coordinator, mixed-catalog
Stage/Commit/Verify, sealed erased-owner tombstones versus active positive-head
omission, evolved captured-event identity, and selected-view repair/retry.

At that round-10 checkpoint, the shared Begin request sealed erased owners
alongside source heads but had no production Begin inventory producer. The
round-10 worktree had no opt-in Dapr rerun; the earlier round-9 0/1 issuance
HTTP 403 attempt below was then the latest live observation. The current
producer and latest-tree local pass are recorded above. Deployed erasure
authority, story acceptance, and release gates remain open.

The build command was
`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -warnaserror -m:1 /nr:false -v:q`.
The Projections full run used its built `net10.0` executable with
`-noLogo -reporter quiet`; the standard count came from a second run excluding
only `Large_valid_capability_inventory_streams_more_than64MiB_through_full_rebuild`.
EventStore DomainService ran its built `net10.0` executable with `-noLogo`;
the final strengthened capacity case then ran with `-method
'*HighEntropyPartitionsPromoteAndPruneWithoutTenantCandidateGrowth'`.

### Round-9 worktree verification (2026-10-11)

The Timesheets source-reference solution built with zero warnings and errors
using `-p:HexalithCommonsRoot=/tmp/no-commons`. At that checkpoint, default Architecture,
Contracts, Server, Projections, Works, and Integration executables passed
56/56, 92/92, 523/523, 252/252, 76/76, and 669 passed with five expected
skips: **1,668 passed, five skipped, zero failed**. Projections ran as 251
standard cases and one separate capacity case above 64 MiB; the changed Time
Entry handler class was rerun 36/36. EventStore Client passed 1,487/1,487;
the full DomainService executable passed 727/729. Its two failures are the
same historical missing nested-Tenants guardrail subject and named-endpoint
catalog-registry assertion. Focused EventStore coordinator and partitioned
dispatcher suites passed 19/19 and 18/18; streamed staging passed 9/9. The mapped Time Entry prune
regression passed after hashing canonical selected-row JSON. The changed
EventStore stream controller class passed 26/26. The broad EventStore Server
executable was stopped after roughly seven minutes of CPU-heavy execution
without a completed result; its full-suite status is unverified.

With the opt-in fixture setting enabled, the checked-in Aspire topology
started healthy on an earlier round-9 checkpoint and immediate preflight showed exactly one `eventstore` and
one `timesheets` Dapr ID. That journey passed persisted catalog
readback and non-fixture denial, then failed 0/1 in 83.705 seconds at
capability issuance HTTP 403; a diagnostic capability stream read returned
404. Timesheets logged a workload stream-read request exhausting its HTTP
timeouts. EventStore logged rate limiting and named-projection retry activity;
the underlying cause remains unproved. Only the Timesheets AppHost was
stopped. An unrelated topology later registered a duplicate `eventstore` ID,
so no further isolated run was started. The current tree includes later captured-envelope
and rebuild-head changes and has no live run; no latest-tree end-to-end pass is claimed.

### Historical round-8 full verification and isolated Dapr attempts (2026-10-11)

All six default Timesheets xUnit v3 executables were rebuilt with explicit
Debug source references and `-p:HexalithCommonsRoot=/tmp/no-commons`, then
ran individually: Architecture 56/56, Contracts 92/92, Server 523/523,
Projections 236/236, Works 76/76, and Integration 669 passed with five
expected skips. Overall: **1,652 passed, five skipped, zero failed**.
All six builds and the AppHost build reported zero warnings and errors.
EventStore Client passed **1,482/1,482**. The full DomainService executable
passed 725/727; its two failures remain the missing nested-Tenants guardrail
subject and the named-endpoint catalog-registry assertion recorded at the
prior checkpoint. No nested submodule was initialized.

`aspire start --no-build` and `aspire wait eventstore` / `aspire wait
timesheets` succeeded. `dapr list` showed one app for each ID before the
opt-in run and again after the third attempt. The first opt-in test failed
**0/1 in 77.897 seconds** after persisted catalog and non-fixture denial,
at capability issuance HTTP 403. The second failed **0/1 in 31.062 seconds**
after issuance and selected-model readback, when both confirm race requests
returned HTTP 403. The third failed **0/1 in 31.637 seconds** after a passing
confirm race and composed Time Entry rebuild readback, at adjust issuance
HTTP 403; a diagnostic capability stream read was unavailable. EventStore
logs included rate-limit and named-projection retry messages, without enough
correlation to establish the cause of any opaque 403. The fixture now reports
safe stored-event counts or names on issuance/race failure. `aspire stop`
stopped only the Timesheets AppHost, and `dapr list` was empty afterward.
No end-to-end live pass for that checkpoint is claimed. The public-API/live-fixture
task remains open, and production gates remain separate.

### Focused continuity verification (2026-10-11, same implementation)

After the round-7 baseline, the EventStore continuity, cancellation, abort
closeout, and Time Entry envelope/transition checks passed the affected
focused suites: EventStore partitioned rebuild **16/16**, fenced dispatch
**17/17**, and coordinator plus streamed stage **23/23**; Timesheets capability
handler **40/40**, Time Entry handler **22/22**, and composed HTTP boundary
**573/573**. The composed suite includes the mapped 10,000-event Time Entry
rebuild and failed-rebuild isolation. The three affected test projects and
the Timesheets IntegrationTests project built with zero warnings and errors
using `-p:HexalithCommonsRoot=/tmp/no-commons` where required.

The opt-in Dapr journey had not completed on that tree. The earlier
live observations below remain historical, and the public-API/live-fixture
task remains open for a clean end-to-end run. The fixture retains strict
duplicate-ID preflight, exact persisted readbacks, and bounded read-only
429/500/503 post-restart probes.

### Re-derived materialization verification (2026-10-11, prior checkpoint)

The six default Timesheets executables passed individually after the capture,
fold, cleanup, and compact-lineage changes: Architecture 56/56, Contracts
92/92, Server 523/523, Projections 232/232, Works 76/76, and Integration
669 passed with five expected skips. Overall: **1,648 passed, five skipped,
zero failed**. The Integration lane includes mapped HTTP replay of a 10,000
event, high-entropy Time Entry history, complete selected lineage readback,
and failed rebuild isolation. The Projections lane includes the corresponding
live aggregate-owned write and full-prefix digest comparison.

EventStore Client passed **1,481/1,481**. The partitioned DomainService class
passed **13/13**, including the dispatcher handoff regression;
the full DomainService executable passed 718/720, with only the two previously
recorded unrelated nested-Tenants guardrail and named-endpoint catalog
assertion failures. Paused owner scratch write versus abort, committed and
aborted terminal batch-marker removal, and committed-session crash/retry have
focused passing tests. Both repository `git diff --check` commands passed.
Affected project builds passed with `-p:HexalithCommonsRoot=/tmp/no-commons`;
the supported source-reference solution command still fails at
`Hexalith.EventStore.Contracts` with duplicate `Hexalith.Commons.UniqueIds`
CS1704. The default solution build fails in sibling `Hexalith.Works.Contracts`
with missing polymorphic-serialization references.

The opt-in live test started after read-only preflight showed no Dapr apps.
`aspire start --no-build` followed a focused AppHost build; `aspire wait`
reported both Timesheets and EventStore healthy, and `dapr list` showed exactly
one `timesheets` and one `eventstore` ID when the fixture began. A separate
AppHost added a second `eventstore` during the run. The fixture passed
persisted catalog delivery/readback and the non-fixture denial, then failed
capability issuance with HTTP 403: **0/1, 87.102 seconds**. The duplicate ID
and the 403 occurred in the same run; this observation does not prove the 403's
cause. Only the Timesheets AppHost was stopped, using explicit `aspire stop`;
the other AppHost was left running. Latest-tree live acceptance remains
provisional. The fixture's persisted Time Entry readbacks now verify the
lossless compact lineage bytes, and its post-restart read-only stream, status,
and state probes allow five bounded attempts for HTTP 429/500/503.

### Earlier materialization verification (2026-10-11)

All six default Timesheets xUnit v3 executables ran individually: Architecture
56/56, Contracts 92/92, Server 523/523, Projections 224/224, Works 76/76,
and Integration 668 passed with five expected skips. Overall: **1,639 passed,
five skipped, zero failed**. The Integration run includes persisted composed-host
issuance/use through `/project/v2` and a mapped full Time Entry replay that
deletes an existing selected row. The EventStore Client executable passed
1,479/1,479. The affected EventStore DomainService classes passed 55/55; its
full executable passed 581/583, with the same two unrelated historical
failures: missing nested Tenants guardrail project and the isolated named
endpoint catalog-registry assertion. The affected Timesheets Projections and
Integration project builds passed with zero warnings or errors. The
source-reference solution build still fails with the known duplicate
`Hexalith.Commons.UniqueIds` MSB3243/CS1704 conflict; the package-reference
solution build fails compiling sibling Works.Contracts with missing
polymorphic-serialization types. Both repositories' `git diff --check` passed.

The latest opt-in Dapr test exited 1 after catalog delivery and persisted
readback, at its combined non-fixture issue/missing-owner assertion. An
unrelated ChatBot AppHost introduced a second `eventstore` Dapr ID during the
run. The original fixture did not print each status, so that exact failure's
two statuses cannot be reconstructed. A diagnostic with both IDs present
returned alternating 500/404 for the same signed missing-owner read through
Timesheets' sidecar, while the Timesheets-owned EventStore sidecar returned
404 on all eight probes. A separate signed non-fixture issuance returned 403.
The fixture now reports only the two statuses if this assertion fails again.
A second opt-in attempt stopped at strict preflight with two `eventstore` IDs.
A final clean-preflight attempt reached persisted catalog, the non-fixture
denial, both exact persisted composed Time Entry rebuild readbacks, the confirm
race, and the adjust journey. It exited 1 after 122.714 seconds when the first
post-restart owner stream read returned HTTP 401. ChatBot had again introduced
a second `eventstore` ID during this run. Only the Timesheets AppHost started
for these attempts was stopped; ChatBot was left untouched. The earlier 1/1
opt-in success remains historical evidence, and current-tree local acceptance
remains provisional.

### Earlier materialized read-model increment (2026-10-11)

The latest default Timesheets run passed Architecture 56/56, Contracts 92/92,
Server 523/523, Projections 220/220, Works 76/76, and Integration 667 passed
with five expected skips: **1,634 passed, five skipped**. The Integration lane
includes an always-on composed-host test that invokes the mapped named Time
Entry rebuild route in the mixed projection catalog and reads the persisted
aggregate-owned evidence result. Two more always-on cases reject malformed
confirmation and correction through normal dispatch and mapped rebuild without
persisting a Fresh row. The full EventStore Client executable passed
1,477/1,477. After fixing handler-local cancellation classification, the full
EventStore DomainService run reported 581 passed and two previously recorded
unrelated failures: the absent nested Tenants guardrail and isolated
named-endpoint catalog assertion. The focused handler-local, store-owned, and
caller cancellation cases passed 6/6.

The final targeted pass after the round-4 corrections passed the EventStore
partitioned rebuild class 10/10 (including dispatcher-level promotion and
checkpoint-seeding interleaving), fenced dispatch class 13/13 (including
store-owned task cancellation), streamed-stage class 7/7 (including near-limit
operation-ID cleanup), and epoch coordinator class 11/11 (including legacy
checkpoint rebuild recovery). The Time Entry and capability handler classes
passed 13/13 and 33/33. The composed-host IntegrationTests project and
EventStore Client project built with zero warnings or errors. The opt-in
fixture now tests the mapped named Time Entry rebuild route and exact persisted
readback and permits five bounded read-only 429/500/503 probes after EventStore
restart; Python compilation and both repositories' `git diff --check` passed.
That fixture revision had not completed a live run. Its final
isolated attempt reached persisted catalog delivery and a stored Time Entry
record, then its first capability stream read returned HTTP 429 after an
unrelated ChatBot AppHost restarted with a second `eventstore` Dapr ID. The
Timesheets AppHost was stopped; the unrelated host was left untouched. The
preexisting 1/1 result below remains historical evidence.

An earlier checkpoint passed all six default Timesheets executables: Architecture
56, Contracts 92, Server 523, Projections 218, Works 76, and Integration 664
with five skips (**1,629 passed, five skipped**). Focused EventStore
partitioned rebuild, fenced dispatch, and streamed-stage suites passed 5/5,
8/8, and 5/5. A later targeted audit closed a promotion/seeding race with a
durable SDK handoff fence: a deterministic interleaving test proves a second
rebuild and ordinary delivery wait while checkpoints are seeded. After that
change, the affected partitioned, fenced, and streamed-stage classes passed
6/6, 8/8, and 5/5; three Timesheets capability rebuild cases passed 1/1 each.
The final fenced class passed 10/10 after adding Dapr-exception outage cases.
The new tests cover post-capture owner sequence handoff,
interrupted scratch cleanup and retry after coordinator restart, abort cleanup,
store outages before folding, and streamed-page readback. A later audit also
proved committed scratch cleanup can resume after another epoch starts.
Python compilation
of the opt-in fixture and both repositories' `git diff --check` passed.
The full EventStore Client executable passed 1,474/1,474. The full
DomainService executable passed 573/575; its two failures are the existing
absent nested-Tenants guardrail and the isolated named-endpoint catalog
registry assertion, outside the changed shared projection paths.

The source-reference solution build still exits 1 in EventStore.Contracts with
MSB3243/CS1704 for duplicate `Hexalith.Commons.UniqueIds` versions 2.30.1.0
and 1.0.0.0. A package-mode attempt exits 1 in sibling Works.Contracts with
missing `Hexalith.PolymorphicSerializations` types. Two then-current isolated
opt-in attempts reached persisted delivery: one stopped on command-status HTTP
500, and the next passed the confirm race but received three opaque 403
responses on adjust POST. A third attempt stopped before writing data when
ChatBot restarted with a duplicate `eventstore` Dapr ID. The Timesheets
AppHost was stopped and the unrelated process left untouched. The prior 1/1
opt-in result below is historical evidence for the earlier revision.

### Earlier materialized read-model run (2026-10-11)

That earlier uncommitted revision added SDK-delivered Time Entry evidence
and capability views. All six default Timesheets executables passed:
Architecture 56, Contracts 92, Server 523, Projections 206, Works 76, and
Integration 664 passed with five skips (**1617 passed, five skipped**). The
EventStore Client suite passed 1469/1469; focused partitioned DomainService
tests passed 3/3. The full DomainService suite passed 565/567, with one
failure requiring a prohibited nested Tenants submodule and one isolated
named-endpoint catalog-registry failure. The prescribed source-reference
Timesheets solution build is blocked by the pre-existing duplicate
`Hexalith.Commons.UniqueIds` assembly conflict (MSB3243/CS1704); affected
Timesheets project builds passed with zero warnings or errors.

The isolated opt-in Dapr run passed **1/1 in 160.146 seconds** with exactly
one `eventstore` and one `timesheets` Dapr app. It covered both new model
readbacks for confirm and adjust, the one-winner race, EventStore restart,
replay denial, and a fresh post-restart link. Two earlier attempts reached
the new readbacks but later returned transient opaque HTTP 403 responses.
Story 3.6 remains `in-progress`; launch remains `FAIL` for production gates.

### Opt-in local Dapr/EventStore acceptance (2026-10-11)

The checked-in Timesheets Aspire composition now runs distinct healthy
EventStore and Timesheets Dapr applications. The opt-in
`MagicLinkLiveDaprTests` and `MagicLinkLiveDaprFixture` run the isolated
[persisted journey](../../../docs/local-magic-link-dapr-proof.md#run-the-persisted-journey).
Development-only signed binding claims and a fixture guard supply test
identity. The real Timesheets HTTP writer receives EventStore `Completed` plus
exact readback before each `202`; the fixture also queries command status
directly and matches `Completed`, event count, owner, and committed sequence
to each stored record, issue, and use batch. The test independently checks stored record,
issue, use and effect payloads, delivered Fresh catalog and candidate index,
recording retry, one-winner race, equal opaque denials, and unchanged streams
after an EventStore process restart. A race loser can append a rejection
marker, but cannot append a second use or effect. The optional Timesheets
idempotency key was removed from the writer because EventStore has no trusted
intent adapter for Timesheets commands; the stable MessageId remains the retry
identity. No EventStore contract or deployed topology was changed.

`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build src/Hexalith.Timesheets/Hexalith.Timesheets.csproj --no-restore -m:1 -v:q` and the corresponding
IntegrationTests project build passed with zero warnings/errors. Direct
`python3 scripts/local_aspire_magic_link_journey.py` exited 0. The opted-in
executable command
`TIMESHEETS_LIVE_DAPR=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests'`
exited 0 with one passed, zero failed/skipped; the final status-strengthened
run passed in 73.929s. It required the explicit AppHost fixture setting,
denied a signed non-fixture request before any capability owner event,
checked exact adjustment values, reread catalog and index after restart, and
used a fresh post-restart link. This proves local
persistence only. Story 3.6 stays `in-progress` and launch stays `FAIL` for
production binding issuance, read-model wiring, deployed history inventory and
deployed acceptance.

The same exact opted-in command passed 1/1 in 71.494s after the authorization
review fixes, then exited 1 after direct status assertions were added: valid
confirm GET returned opaque `403` after persisted readback (83.307s). Direct
`python3 scripts/local_aspire_magic_link_journey.py` exited 0 against that
Aspire session. The next opted-in run exited 1 when adjust recording returned
opaque `403` (32.549s). Sanitized Timesheets warnings showed outbound
10-second timeouts and 429 retries. A bounded three-attempt wait was added
only for the stable-identity recording command and read-only valid GET; any
other status or continuing denial fails. The final opted-in run exited 0 with
one passed and zero failed/skipped in 73.929s.

The six default test executables passed individually: Architecture 56/56,
Contracts 92/92, Server 523/523, Projections 174/174, Integration 663 passed
and five skipped (668 total, including this opt-in lane), Works 76/76. Overall:
1,589 tests, 1,584 passed and five skipped. The current solution build command
`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`
exited 1 in `references/Hexalith.EventStore.Contracts` with MSB3243/CS1704:
duplicate `Hexalith.Commons.UniqueIds` assemblies, versions `2.30.1.0` and
`1.0.0.0`. The connected-topology proof had already recorded that same
failure before this fixture/writer increment.

The post-review default commands, run from the repository root without the
live opt-in variable, had these exact results:

| Exact command | Result |
| --- | --- |
| `tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests` | Exit 0; 56 passed. |
| `tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests` | Exit 0; 92 passed. |
| `tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests` | Exit 0; 523 passed. |
| `tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests` | Exit 0; 174 passed. |
| `tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests` | Exit 0; 663 passed, five skipped. |
| `tests/Hexalith.Timesheets.Works.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Works.Tests` | Exit 0; 76 passed. |
| `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` | Exit 1; EventStore.Contracts MSB3243/CS1704 duplicate UniqueIds versions above. |
| `python3 -m py_compile scripts/local_aspire_magic_link_journey.py` | Exit 0. |
| `git diff --check` | Exit 0. |

The release table's current Build gate was corrected to `FAIL`. The exact
focused command
`tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests'`
first exited 1 with 12/13 passed because it still expected `PASS`.
After that expectation was corrected,
`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --no-restore -m:1 -v:q`
exited 0 with zero warnings/errors; the focused command exited 0 with 13/13
passed, and the full ArchitectureTests command above exited 0 with 56/56 passed.

### Historical local Dapr acceptance preflight (2026-10-11)

The preflight in [the local Dapr proof record](../../../docs/local-magic-link-dapr-proof.md) stopped before the opt-in acceptance journey. `docker info --format '{{.ServerVersion}}'` returned `29.8.2`, and `dapr list` exited 0 but listed no `timesheets` app ID. `aspire ps --non-interactive` found an EventStore AppHost run owned by a separate EventStore integration test; it was not modified. Source inspection found no Timesheets Dapr module or trusted-effect allow-list entry in the EventStore AppHost, while the Timesheets AppHost declares no EventStore resource or Dapr sidecar. The EventStore local `statestore` component also excludes Timesheets and directs domain services to use a separate scoped component, so adding a sidecar alone would not deliver the persisted index/catalog. The environment exposed no Timesheets/EventStore/Dapr/signing variable names. A connected, isolated Timesheets identity and persisted index/catalog delivery cannot be prepared from the checked-in topology without the separately owned platform work.

Per the spec's preflight stop condition, `MagicLinkLiveDaprTests` and `MagicLinkLiveDaprFixture` were not added, and `TIMESHEETS_LIVE_DAPR=1` was not run. No record, issue, use, restart, race, or stored-event observation was made. `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests'` exited 0 with 13/13 passed; `git diff --check` found no whitespace errors. The 2026-10-10 in-process and actor tests below remain the latest full Timesheets suite evidence; this documentation-only preflight did not rerun restore or build. Story 3.6 remains `in-progress` and release remains `FAIL`.

### EventStore Time Entry recording increment (2026-10-10)

The external submission route now submits an internal recording intent to the existing keyed `timesheets` processor. The processor rehydrates current owner state, checks verified workload origin and actor, server-side authorization and references, and a Fresh Activity Type catalog before emitting one `StoredTimeEntryRecorded` and, when policy requires, one `StoredTimeEntrySubmitted`. The route returns `202` only after a bounded `Completed` status and matching stored-event readback. A stable command identity makes retries safe after uncertain results. The existing magic-link use path can then fold the recorded owner state.

In-process HTTP tests prove record-then-confirm, one recording across concurrent requests and retry, submitted-policy evidence, opaque denials for unauthorized, unavailable-catalog and conflicting submissions, and no second append after `PublishFailed` or a mismatched receipt. Review tests also cover actor-bound collision-safe retry identity, retained rejection and terminal markers before recording, exact readback correlation/scope/audit instant, and stored submission folds in loader and projection. The actor state-store fixture now seeds recording through the production processor and proves confirm/adjust after actor recreation and replay denial. These tests substitute the EventStore gateway, read-model store, and access guard; they do not prove live Dapr persistence, production caller authentication, or a trusted resource-binding issuer.

`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` and the matching warning-as-error Debug solution build passed with zero diagnostics after review patches. Six Timesheets executables ran individually: Architecture 56/56, Contracts 92/92, Server 523/523, Projections 174/174, Integration 663 passed and four skipped (667 total), Works 76/76. Overall: 1588 total, 1584 passed, four skipped, zero failed. A separate focused HTTP class rerun passed 567/568: `AssertSensitiveMaterialAbsent` falsely matched the generic substring `60` in a permitted timestamp; this pre-existing flaky assertion is tracked in `deferred-work.md`. Story 3.6 remains `in-progress`; release remains `FAIL`.

### Review patch closure: replay, polling, receipts, and projection scope (2026-10-10)

The in-process EventStore actor fixture now persists a rejected command before a later valid issue or use and proves the valid command commits both before and after actor recreation. The HTTP gateway fixture persists rejection outcomes and successful batches that report `EventsStored` or `PublishFailed`; `Received` stores no batch. All three statuses yield an opaque `403`. The fixture returns delayed `Completed` statuses, distinguishes workload-only routes, and checks malformed status receipts and tampered stored issue/use/confirm/adjust/expire events. A composed host test resolves `Program.cs`'s assertion source, then sends a signed request through its real workload gateway with tenant, domain, and authenticated actor bindings; blank `party_id` falls back to `NameIdentifier`. Direct processor positive/negative pairs exercise verified origin for use and origin/actor for issue and revoke. Unit tests cover a five-second default, configurable polling deadline, a 10-ms minimum interval, configuration binding and startup validation, cancellation, terminal statuses, and stable retry message identity. Projection tests reject unscoped or cross-tenant recordings and cross-tenant terminal evidence in entry, ledger, and period folds, including empty periods. OpenAPI includes verified `202`, malformed-body `400`, and opaque `403` cases. These tests do not establish a deployed EventStore/Dapr topology or deployed stream inventory.

The matching source-reference Debug restore and solution build passed with zero warnings/errors using `-p:Configuration=Debug -p:UseHexalithProjectReferences=true` for restore and `--configuration Debug -p:UseHexalithProjectReferences=true` for build. The IntegrationTests project rebuild after this review passed with zero warnings/errors. The bare documented build without the project-reference property failed in the Works submodule with `HXW0002` and missing dependency references; this is a pre-existing build-mode issue, not a passing gate. All six Timesheets executables ran individually: Architecture 56/56, Contracts 92/92, Server 512/512, Projections 173/173, Integration 652 passed plus four intentional skips (656 total), Works 76/76. Total: 1565 tests, 1561 passed, four skipped. The affected `MagicLinkConfirmationHttpBoundaryTests` class passed 557/557 after review patches. Timesheets HEAD pins EventStore `cf10b4c7e8c86ae6ad06c842ed567dd0e491ad46`, which local EventStore `origin/main` contains; earlier unpublished-pointer notes below are historical. Live topology, trusted production binding issuer, and deployed-history inventory remain open, so launch remains `FAIL` and Story 3.6 remains `in-progress`.

### Durable submission and workload gateway increment (2026-10-10)

The changed worktree uses an EventStore workload channel with signed tenant and domain bindings for submit, message-primary status, and stream read. EventStore stamps a verified `timesheets` origin; issue and revoke also require a signed actor binding. The Timesheets processor rejects absent origin, malformed intent, and forged actor/hash evidence. It uses actor-provided Time Entry state during `/process` and reads only the separate capability stream. Stored wrapper events are normalized before affected Time Entry and capability read-model folds.

Local proof covers valid confirm/adjust and issue/revoke/expire HTTP submissions, `Completed` status plus exact stored-event readback, one-winner concurrency and replay denial, legacy Used/Revoked/Expired histories, opaque Received/EventsStored/PublishFailed outcomes, protected keyed `/process` dispatch, and actor state-store commit/reload for confirmation, adjustment, and proposed-entry revoke/expire. Regressions cover ordinary multi-entry reports with equal local sequences, distinct payloads at one owner/sequence, explicit MissingStream for an unrecorded proposed-entry owner, denial on MissingEvent or malformed 404, queued issuance and use after expiry, expiry advancing during awaited authorization for issuance and both use actions, undefined numeric actions deserialized from JSON, repeated physical issuance, and a valid signed command crossing the real MediatR authorization pipeline. Other regressions cover malformed or terminal-only issuance and receipt field and audit-source mismatches. EventStore controller tests cover workload route policies, cross-tenant and cross-domain denial, reserved-origin spoofing, a workload status Location, and preservation of human routes. These tests use local in-memory stores and an in-process gateway fixture; they do not prove deployed Dapr persistence.

`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` and the matching `dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` passed with zero warnings/errors. The six individual Timesheets executables passed: Architecture 56/56, Contracts 92/92, Server 495/495, Projections 160/160, Integration 606 passed plus four declared skips (610 total), Works 76/76. Total: 1489 tests, 1485 passed, four skipped. The EventStore Server.Tests and Client.Tests projects built with zero warnings/errors. The focused EventStore controller and signed HTTP middleware selection passed 43/43; workload client transport passed 2/2. The EventStore server selection used `-class '*WorkloadGatewayControllerTests' -class '*DaprInternalAuthenticationHandlerTests'`; the client selection used `-class '*EventStoreGatewayWorkloadClientTests'`.

Release remains `FAIL`. The AppHost has no EventStore resource, deployed capability and Time Entry histories are unknown, and no live Dapr-backed write/restart/concurrency proof has run. The current authority-mode `JwtWorkloadAssertionIssuer` cannot sign per-request tenant/domain/actor bindings, so workload submission fails closed in that mode; production needs a trusted resource-binding issuer. Old capability-stream terminal writers must stop before the new Time Entry owner path is enabled. The EventStore submodule changes also require publication before a Timesheets gitlink can reference them.

### Fail-closed public POST and catalog status increment (2026-10-10)

At Timesheets baseline `61d6fc7a31544985bed5458972f81b7050b9c775`, the changed worktree returns the shared opaque 403 for valid confirm and adjust POST decisions because no durable commit occurs. Valid Fresh-catalog GET displays remain 200. The concrete loader retains only the status of a well-formed persisted non-Fresh catalog; it discards capability, Time Entry, items, cursor, timestamp, and detail. Missing, malformed, and unreadable catalogs remain Unavailable. The HTTP fixture's EventStore gateway records zero submit attempts for valid POSTs, and GET remains available after those POSTs. These are in-process assertions, not persisted concurrent single-use proof.

Read-only deployment and stream-inventory check: `src/Hexalith.Timesheets.AppHost/Program.cs` still declares no EventStore resource; the repository contains no Timesheets deployment endpoint or stream inventory artifact; and the available environment exposes no `EVENTSTORE`, `DAPR`, `APP_API_TOKEN`, `JWT`, `ASPIRE`, `AZURE`, or `KUBE` variable names (`printenv | cut -d= -f1 | rg -i 'EVENTSTORE|DAPR|APP_API_TOKEN|JWT|ASPIRE|AZURE|KUBE'`). The existing 2026-10-09 AppHost attempt failed on missing internal credentials. No deployed stream read was possible, so whether capability and Time Entry histories exist remains unknown. A release owner needs a deployed EventStore endpoint, read-only stream access, and tenant/aggregate scope to inventory those histories before choosing a write owner or migration. No stream migration was started.

`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` and the matching `dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` passed with zero warnings/errors. Direct focused classes passed: loader 80/80 and HTTP boundary 462/462. The six direct executables ran individually: Architecture 55/56 (the pre-existing Aspire `13.6.1` package-verdict assertion), Contracts 92/92, Server 490/490, Projections 150/150, Integration 557 pass/four intentional skips, Works 76/76. Total 1425: 1420 pass, one pre-existing failure, four skips. Story 3.6 remains in progress; durable atomic single-use, persisted concurrency, deployed-history inventory, and live-host proof remain open. Release stays `FAIL`.

Post-review verification removed the stale OpenAPI submit `202` responses, passed the two new contract cases, and strengthened status-field assertions. A changed readiness fitness assertion initially failed on old prose, was corrected, and the Architecture lane reran at 55/56 with only the pre-existing Aspire package-verdict assertion failing. Story 3.7 was reopened because its valid POST `202` acceptance is suspended; the current OpenAPI response description states that durable submission is unavailable.

### Post-review GET-only authorization guard (2026-10-09)

EventStore `f87affaefef5693fd80452eea02999e46fa1888b` closes a review-found authorization gap: an explicit weak policy on a GET-only canonical route could suppress the denying fallback. Timesheets `61dc18bdb3e28ed8bbee64488954b8f42b90c4e7` changes only its EventStore gitlink from `fe4e7559` to that local commit. Both commit messages passed commitlint; commit validation was not bypassed. No fetch or push occurred. Local EventStore `origin/main` remains at `fe4e7559`, so the current gitlink is **not yet proven fetchable in a fresh clone**; EventStore publication must precede any Timesheets push.

- SDK DomainService.Tests Debug build passed with zero warnings/errors. The five-method inventory/public-route selection, including the new weak-policy cases, passed 22/22 (`/tmp/timesheets-story36-review-sdk-focused.log`); the whole ExtensionsTests class passed 81/81 (`/tmp/timesheets-story36-review-sdk-class.log`). The full SDK executable ran 549 tests: 548 passed and the same absent nested-Tenants guardrail subject failed (`/tmp/timesheets-story36-review-sdk-full.log`). Repository policy forbids initializing that nested submodule.
- Timesheets source-reference Debug restore/build passed with zero warnings/errors (`/tmp/timesheets-story36-review-build.log`). The two HTTP classes passed 482/482 (`/tmp/timesheets-story36-review-http-classes.log`); the four scratch-root privacy cases passed 4/4 (`/tmp/timesheets-story36-review-scratch-http.log`). The six separate executables ran 1415 tests: Architecture 55/56 with the existing Aspire 13.6.1 package-verdict failure, Contracts 90/90, Server 482/482, Projections 150/150, Integration 557 passed and four intentional skips, Works 76/76. Overall: 1410 passed, one existing failure, four skips. Captures are `/tmp/timesheets-story36-review-<Project>.log`.

```bash
# Run the following SDK commands from references/Hexalith.EventStore.
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Debug -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_GetOnlyCanonicalRouteRequiresPrivateAccess' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_GetOnlyCanonicalRouteRejectsWeakExplicitAuthorization' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_AcceptsOneExplicitPublicRoute' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_RejectsInvalidPublicOptIns' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_RejectsWeakOverridesAndExtraAnonymousEndpoints'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests -class 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests
# Run the following Timesheets commands from the Timesheets root.
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests' -class 'Hexalith.Timesheets.IntegrationTests.InternalSurfaceGuardTests'
ASPNETCORE_TEST_CONTENTROOT_HEXALITH_TIMESHEETS=/tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Invalid_magic_link_http_boundary_emits_no_timesheets_sensitive_diagnostics' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.HostingDiagnosticsFilterSurvivesProviderSpecificInformationConfiguration' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Empty_magic_link_token_records_the_malformed_outcome_category_without_sensitive_diagnostics'
# Each of the six projects was also run individually as DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/<Project>/bin/Debug/net10.0/<Project>.
```

### Upstream pointer verification (2026-10-09)

Timesheets `46265ba71138617ac387bd68e63aa0d7e5685947` pins EventStore `fe4e7559f17ed53eea005b0ae5a4f6a358221780`, which local EventStore `origin/main` contains. The five owned SDK files are byte-identical between `fe4e7559` and the previously integrated `bd976eff37e80fa404ee47d4031ecf6c2545b541`. No new fetch or push was performed for this verification. `46265ba` moved the final gitlink alongside other files and pointers; the earlier `298899bfbdaeaa6df7528d32309b826f5b63f95d` remains the separate gitlink-only commit. The earlier section below describes the now-superseded local pointer and its then-unproven remote reachability.

- In `references/Hexalith.EventStore`, the Debug DomainService.Tests build passed with zero warnings/errors. The focused selection passed 19/19 (`/tmp/timesheets-story36-upstream-sdk-focused.log`); the entire ExtensionsTests class passed 78/78 (`/tmp/timesheets-story36-upstream-sdk-class.log`). The full DomainService executable ran 546 tests, with 545 passing and one `DomainModuleAuthoringGuardrailTests.TenantsDomainService_DoesNotReferenceGeneratedApiHostOrDeclarePerMessageControllers` failure because `references/Hexalith.EventStore/references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj` is absent (`/tmp/timesheets-story36-upstream-sdk-full.log`). Repository policy prohibits initializing that nested submodule.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Debug -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_GetOnlyCanonicalRouteRequiresPrivateAccess' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_AcceptsOneExplicitPublicRoute' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_RejectsInvalidPublicOptIns' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_RejectsWeakOverridesAndExtraAnonymousEndpoints'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests -class 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests
```

- In Timesheets, the source-reference Debug restore and build passed with zero warnings/errors. The two HTTP classes passed 482/482 (`/tmp/timesheets-story36-upstream-http-classes.log`), including the identical signed projection dispatch refused with 404 on the public port and persisted internally. Four scratch-root privacy cases passed 4/4 (`/tmp/timesheets-story36-upstream-scratch-http.log`).

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests' -class 'Hexalith.Timesheets.IntegrationTests.InternalSurfaceGuardTests'
mkdir -p /tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked
ASPNETCORE_TEST_CONTENTROOT_HEXALITH_TIMESHEETS=/tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Invalid_magic_link_http_boundary_emits_no_timesheets_sensitive_diagnostics' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.HostingDiagnosticsFilterSurvivesProviderSpecificInformationConfiguration' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Empty_magic_link_token_records_the_malformed_outcome_category_without_sensitive_diagnostics'
```

- Each Timesheets test executable ran separately as `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/<Project>/bin/Debug/net10.0/<Project>`. Captures are `/tmp/timesheets-story36-upstream-<Project>.log`. Architecture 55/56: the sole failure is the existing `Launch_readiness_record_captures_package_currency_verdict_dimensions` expectation for Aspire 13.6.1. Contracts 90/90; Server 482/482; Projections 150/150; Integration 557 passed and four intentional skips (561 total); Works 76/76. Overall: 1415 total, 1410 passed, one failure, four skips. These are in-process results; live persisted topology and durable capability-use writes remain open, and the release decision remains `FAIL`.

### Prior local public-route review-patch verification (2026-10-09)

The final local EventStore integration is commit `bd976eff37e80fa404ee47d4031ecf6c2545b541` on `fix/story-3-6-public-routes`, created from the locally available `origin/main` (`f463442cca19e4199982a23a08bae4a490767d4a`) by applying the SDK public-route commit `f615dd5d`, the externally captured inventory patch `dc140b0f`, and the diagnostics fixture correction `0b1b1455` without rewriting any existing main commits. Commitlint validated the integrated commit. Timesheets commit `298899bfbdaeaa6df7528d32309b826f5b63f95d` changes only its EventStore gitlink from `dc140b0f` to `bd976eff`. Local `git branch -r --contains bd976eff37e80fa404ee47d4031ecf6c2545b541` returned no branch. No fetch or push occurred in this run; remote reachability is unproven, and a fresh clone cannot rely on this pointer until EventStore publishes it. The earlier external `/pushall` commits `dc140b0f` and `2a3c21b` captured in-flight edits and mixed gitlinks; they are preserved as history.

- SDK build: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj --configuration Debug -warnaserror -m:1 /nr:false` passed with zero warnings/errors (`/tmp/timesheets-story36-sdk-integrated-build.log`). The focused inventory/public-route selection below passed 19/19 (`/tmp/timesheets-story36-sdk-integrated-focused.log`). The entire `EventStoreDomainServiceExtensionsTests` class passed 78/78 (`/tmp/timesheets-story36-sdk-integrated-class.log`). The full SDK DomainService test executable ran 546 tests: 545 passed and one `DomainModuleAuthoringGuardrailTests.TenantsDomainService_DoesNotReferenceGeneratedApiHostOrDeclarePerMessageControllers` failure because its subject is absent at `references/Hexalith.EventStore/references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj` (`/tmp/timesheets-story36-sdk-integrated-full.log`). Repository instructions prohibit initializing that nested submodule to satisfy the guardrail.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_GetOnlyCanonicalRouteRequiresPrivateAccess' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_AcceptsOneExplicitPublicRoute' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_RejectsInvalidPublicOptIns' -method 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests.EndpointInventory_RejectsWeakOverridesAndExtraAnonymousEndpoints'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests -class 'Hexalith.EventStore.DomainService.Tests.EventStoreDomainServiceExtensionsTests'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests
```

- Timesheets source-reference restore and build passed with zero warnings/errors. Captures: `/tmp/timesheets-story36-integrated-restore.log` and `/tmp/timesheets-story36-integrated-build.log`.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

- The two HTTP classes passed 482/482, including the same projection dispatch and signed workload assertion on the public and internal ports; public returned 404 and internal persisted the index entry (`/tmp/timesheets-story36-integrated-http-classes.log`). Four scratch-root privacy cases passed 4/4 (`/tmp/timesheets-story36-integrated-scratch-http.log`).

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests' -class 'Hexalith.Timesheets.IntegrationTests.InternalSurfaceGuardTests'
mkdir -p /tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked
ASPNETCORE_TEST_CONTENTROOT_HEXALITH_TIMESHEETS=/tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Invalid_magic_link_http_boundary_emits_no_timesheets_sensitive_diagnostics' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.HostingDiagnosticsFilterSurvivesProviderSpecificInformationConfiguration' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Empty_magic_link_token_records_the_malformed_outcome_category_without_sensitive_diagnostics'
```

- All six Timesheets executables ran individually with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/<Project>/bin/Debug/net10.0/<Project>` and captured to `/tmp/timesheets-story36-integrated-<Project>.log`. Architecture 55/56 (one existing Aspire 13.6.1 package-verdict mismatch in `Launch_readiness_record_captures_package_currency_verdict_dimensions`); Contracts 90/90; Server 482/482; Projections 150/150; Integration 557 pass / four intentional skips (561 total); Works 76/76. Overall 1415 total, 1410 pass, one failure, four skips. The earlier `/tmp/timesheets-public-*` captures from 16:36 are pre-patch. No live persisted topology, durable write, or release acceptance is claimed.

- Prior public-route compatibility rerun (2026-10-09, before the review patches), changed worktree based on `3e1ca73ba51f0c91467b74ac3dc85f88b6526b7e`: the EventStore SDK public-route/startup tests passed 24/24; Timesheets source-reference Debug restore/build passed with zero warnings/errors; `MagicLinkConfirmationHttpBoundaryTests` and `InternalSurfaceGuardTests` passed together (482/482), including anonymous calls to all four capability routes; all four scratch-root HTTP privacy cases passed. Six Timesheets lanes: 1415 total, 1410 pass, one pre-existing Architecture package-verdict failure, four intentional skips. The live AppHost started, but `aspire wait timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --timeout 30 --non-interactive` exited 18 because APP_API_TOKEN and JWT Authority/SigningKey are missing; `aspire stop` exited 0. In-process route and privacy verification does not prove live persisted topology or durable capability use.

  Live-topology commands and results for this rerun, in order (all with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`):

  ```bash
  aspire start --no-build --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive --format Json # exit 0
  aspire wait timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --timeout 30 --non-interactive # exit 18
  aspire logs timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --tail 30 # exit 0
  aspire stop --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive # exit 0
  ```

  The logs report `Domain-service internal credentials are not configured: APP_API_TOKEN must be configured. Authentication:JwtBearer requires exactly one of Authority or SigningKey to be configured.` Captures: `/tmp/timesheets-public-aspire-{start,wait,logs,stop}-last.log`.
- Prior spec-17 verification (2026-10-09), changed worktree based on `c75de3c2f6328e63e77682b6d3cac6c54a7ded87`: SDK `10.0.401`; README source-reference Debug restore/build passed with zero warnings/errors. The corrected non-HTTP privacy theory passed 432/432, including twelve new Timesheets/framework category, message and state controls that permit tenant identifiers in diagnostics while the external response rejects both fixture tenants. Six direct executables ran individually: Architecture 55/56; Contracts 90/90; Server 482/482; Projections 150/150; Integration 508 pass / 42 fail / four skips; Works 76/76. Total: 1408, 1361 pass, 43 fail, four skips. All 42 Integration failures are unexpected 401s; the Architecture failure is the existing Aspire package-verdict mismatch. The four scratch-root HTTP cases were rerun against this build and still failed at 401 before privacy assertions. Final logs: `/tmp/timesheets-story-3-6-spec-17-build-final.log`, `-privacy-final.log`, `-<Project>-final.log`, and `-scratch-http-final.log`; restore is in `-restore.log`. AppHost start exited 0, wait exited 18 because `APP_API_TOKEN` and JWT Authority/SigningKey are missing, and stop exited 0. No live HTTP or persisted-topology acceptance is claimed.
- Current focused command (2026-10-09): `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.PrivacyAssertionsRespectCategoryAndResponseScope'` exited 0 (432/432). This proves the helper scopes, not current HTTP behavior.
- Prior spec-16 final post-review verification (2026-10-09), changed worktree based on `bb7fe1fb93681a0c77eb8eab76a19e2232326def`: SDK `10.0.401`; the README source-reference Debug restore/build passed, and the post-review solution rebuild passed with zero warnings/errors. The in-lane non-HTTP privacy theory passed 420/420: harmless vocabulary in message/category/state across three framework/third-party categories with populated, empty and whitespace tokens; universal protected values across four categories; blank/whitespace-token protected-value failures; two distinct request tokens and hashes; response-only tenant exclusions; Timesheets/response generic exclusions; realistic permitted diagnostics and serialized production default ProblemDetails controls. All six final executables ran individually: Architecture 56 total / 55 pass / one failure; Contracts 90/90; Server 482/482; Projections 150/150; Integration 542 total / 496 pass / 42 fail / four existing skips; Works 76/76. Overall: 1396 total, 1349 pass, 43 fail, four skips. The 420 new cases account for the entire increase from the prior 976-test inventory. No new failure was introduced: the package-verdict failure and 42 unexpected-401 HTTP failures are unchanged. Before independent review, the initial matrix passed 189/189 and six lanes reported 1165 total / 1118 pass / 43 fail / four skips. All four selected scratch-root HTTP cases still stop at 401. Final-build logs are `/tmp/timesheets-story-3-6-spec-16-build-final.log`, `-privacy-helpers-final.log` and `-<Project>-final.log`; the `-scratch-http.log` run preceded the post-review rebuild; exact full commands remain in README.
- Prior spec-16 focused command (2026-10-09): `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.PrivacyAssertionsRespectCategoryAndResponseScope'` exited 0 (420/420 after review; 189/189 before review). These synthetic helper cases establish assertion behavior without proving current HTTP-boundary conformance, scopes or exception diagnostics.
- Prior spec-16 runtime baseline (2026-10-09): `DOTNET_CLI_HOME=/tmp/dotnet-cli-home aspire start --no-build --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive --format Json` exited 0; `aspire wait timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --timeout 30 --non-interactive` exited 18. Describe reported Timesheets Finished and security Healthy; `aspire logs timesheets --tail 40` reported missing `APP_API_TOKEN` and the required JWT Authority/SigningKey configuration. Explicit-AppHost `aspire stop` exited 0. No production source, credentials, dependency pin or authorization policy was changed.
- Prior privacy-assertion and record-patch verification (2026-10-08), changed worktree based on `aba488b18c4a4b96e08442538c41481c752c0382`: the prescribed source-reference Debug restore/build passed with zero warnings/errors. All six direct executables ran individually: Architecture 56 total / 55 pass / one failure; Contracts 90/90; Server 482/482; Projections 150/150; Integration 122 total / 76 pass / 42 failures / four existing skips; Works 76/76. Overall: 976 total, 929 passed, 43 failed, four skipped. The architecture failure is `Launch_readiness_record_captures_package_currency_verdict_dimensions`, which expects the current imported Aspire `13.6.1` text while the dated verdict records `13.6.0`. Every integration failure is an unexpected 401 after the pre-existing `da172a8effe484be96c17aaef1c007e1ecb8f27c` EventStore update enabled workload authorization before the endpoint paths under test. The four scratch-content-root privacy cases also stop at that 401, before their diagnostic assertions. These failures are unresolved; they are not a passing HTTP/privacy or release gate.
- Prior focused fallback verification (2026-10-08): a retained reflection verifier exercised the rebuilt IntegrationTests assembly's privacy helpers independently of HTTP authorization. All 144 checks passed: harmless domain vocabulary in framework messages is accepted; raw token/hash and all six protected fixture identifiers are rejected in category, message and structured state across three framework/third-party categories; Timesheets logs and response bodies retain the generic-vocabulary exclusions. Command: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet run --project _bmad-output/implementation-artifacts/tests/3-6-privacy-assertion-verifier/Verifier.csproj --configuration Debug -- "$PWD/tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0"`. This is helper-level evidence, not an HTTP boundary or exported-scope/exception proof. Before rebuilding, the previous IntegrationTests binary reproduced the checkout-path false positive with `ASPNETCORE_TEST_CONTENTROOT_HEXALITH_TIMESHEETS=/tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked`; the selected privacy test failed on the framework content-root message.
- Prior runtime baseline (2026-10-08): explicit-AppHost `aspire start` succeeded, but `aspire wait timesheets --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --timeout 30 --non-interactive` exited 18. Describe reported Timesheets Finished; logs reported missing `APP_API_TOKEN` and missing JWT Authority/SigningKey configuration. `aspire stop` succeeded. This current startup blocker differs from spec-14's historical port conflict. No authentication bypass, dependency change or submodule pointer change was made.
- Latest passing full run: spec-14 final post-review verification (2026-10-05), changed worktree based on `ee87e39f9f43e442e5426bb090afbe70be26492a`: source-reference Debug restore/build passed with zero warnings/errors. All six direct test executables passed individually: Architecture 56/56, Contracts 90/90, Server 482/482, Projections 150/150, Integration 122 total / 118 pass / four existing skips, Works 76/76 (976 total, 972 pass, four skips, zero failures). These final results include four additional HTTP review-remediation cases; the focused 72/72 loader, 101/101 projection and 39/39 HTTP/guard counts belong to the closure run before independent review. Live topology, durable atomic writes, concurrent single-use, non-Fresh diagnostic distinctions and deployed Activity Type inventory remain open; release remains `FAIL`. See [spec-14 final verification](../spec-3-6-implement-eventstore-backed-magic-link-state-loading-14.md#final-post-review-verification).
- Prior record-correction increment (2026-10-05), changed worktree based on `c9f543f0623c7cde775eac99d4f39ce70fb4c84e`: documentation only. The increment ran no new tests. The four record patches are closed; acceptance and release gaps remain open. Review triage then verified `aab64cf`, after `c9f543f` moved the Builds, EventStore, FrontComposer and Tenants pointers: the source-reference Debug restore/build passed with zero warnings and errors, and all six direct test executables passed individually: Architecture 56/56, Contracts 90/90, Server 479/479, Projections 146/146, Integration 106 pass/4 declared skips, and Works 76/76 (957 total, 953 pass, four skips).
- Prior review-patch increment (2026-10-05), changed worktree based on `c5fcf987046018c980c5def7c2d3e1a9ce310abe`: source-reference Debug restore/build passed with zero warnings and errors. All six direct test executables passed individually: Architecture 56/56, Contracts 90/90, Server 479/479, Projections 146/146, Integration 106 pass/4 declared skips, and Works 76/76 (957 total, 953 pass, four skips). New assertions pin the fail-closed `InternalSurfaceOptions` defaults and use MessageIds whose lexical order conflicts with event sequence in the complete-fold regression. Eight review patches were closed; acceptance and release gaps remained open. Pre-edit Aspire start succeeded; `aspire describe` reported Timesheets Finished with exit code 134; Aspire was stopped. No bind-failure cause was captured for that attempt. This is not a live EventStore proof.
- Prior date / build baseline: 2026-10-04, changed worktree based on `2b03356b4866900559c772c402301ac89c084ef5` (historical-review reconciliation and complete-fold determinism increment).
- Prior (2026-10-04) SDK: .NET `10.0.401` (`rollForward: latestPatch`).
- Prior (2026-10-04) Restore: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false` passed.
- Prior (2026-10-04) Build: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` passed with zero warnings and errors.
- The historical bare solution build and its explicit-Debug retry on 2026-10-03 failed with `HXW0002` and missing Works serialization references (176 errors): solution traversal removes the external project's Configuration, requiring the supported source-reference option. The first source-reference build hit `NETSDK1064` for unrestored Roslynator.Analyzers 4.15.0; restoring with the same source-reference mode resolved it. No package pins or submodule pointers changed.
- Prior (2026-10-04) Direct xUnit v3 executables, each invoked individually as `DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/<Project>/bin/Debug/net10.0/<Project>`: ArchitectureTests 56/56; Contracts.Tests 90/90; IntegrationTests 110 total, 106 pass, 4 declared skips; Projections.Tests 146/146; Server.Tests 479/479; Works.Tests 76/76.
- Prior reconciliation inventory: 957 tests, 953 passed, 4 declared infrastructure/performance skips, 0 failures. The existing performance lanes were left disabled.
- The first ArchitectureTests invocation in the prior alternate-configuration increment failed one of 55 cases because the shared catalog now expresses CommunityToolkit.Aspire.Hosting.Dapr as `$(HexalithAspireHostingDaprVersion)`. The existing catalog reader was adapted to resolve exact property references to unconditional defaults, preserving the concrete-version assertion and excluding the unrelated Folders-only override. The focused ArchitectureTests Debug build passed with zero warnings/errors; its rerun passed 55/55. The other five lanes also passed in that prior increment; current six-lane results are recorded above.
- Prior review coverage (retained): three server availability combinations, default loader resolution, canonical deactivation delivery plus all four external HTTP routes, configured-port projection delivery with persisted index/catalog assertions, public-port refusal of a dispatch proven to persist internally, all four public routes after internal delivery, and two HTTP claim-scoped administrator flows for new and existing capabilities through the actual host accessor and authorization request context. Opaque-denial HTTP coverage passed on 2026-10-05; the prior spec-17 rebuilt HTTP cases stopped at unexpected 401 responses before those assertions; the current rerun above passes.
- Prior alternate-configuration increment (baseline `30fc6d3092cbb376c500176ceeef351109e3fa7f`): the existing `AppHostExportsTheInternalListenerPortToTheHostOptionsSection` fitness check counts exactly one case-insensitive Timesheets InternalSurface configuration-key prefix in comment-stripped AppHost source, covering double-underscore exports, colon keys and command-line settings while accepting unrelated InternalSurface URLs. It rejects literal `AllowOnAnyPort` in both AppHost and host source and preserves both interpolated-verbatim prefix orders (`@$"` and `$@"`). The retained verifier `python3 _bmad-output/implementation-artifacts/tests/3-6-configuration-mutations.py` passed baseline, eight harmless literal/URL controls and restored-source checks; all 21 unsafe mutations were rejected, including colon-key/WithArgs overrides, host Configure/in-memory bypasses, interpolated-verbatim URL-hidden overrides and an actual end-of-line AppHost export comment with the chain semicolon on the next line. Block-comment and directive substitutes also retain their semicolon outside the disabled region. Both production files were restored byte-for-byte. This remains bounded lexical source fitness evidence; nested interpolation, escaped identifiers/keys, computed keys and runtime/deployment configuration need separate semantic verification. The verifier is a separate focused command; the one added catalog-reader regression raises the inventory to 956 tests.
- Prior reconciliation increment: all 37 historical unchecked review patches now carry evidence and a disposition (28 implemented/superseded, nine with remaining deferred work). `LoadTokenStateAsyncPreservesCompleteFoldedBundleAcrossOrderingAndDuplicateVariants` compares the entire serialized public bundle for ordered, reversed and shuffled/duplicate histories covering adjustment, contributor confirmation, submission, approval and approved correction, with explicit transition assertions and Fresh catalog metadata. The full Server.Tests lane passed 479/479. Deployed capability inventory, non-Fresh diagnostic distinctions, live topology, durable writes and concurrent single-use proof remain open.
- Prior reconciliation post-review verification: the focused Server.Tests Debug build with `--no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false` passed with zero warnings/errors; Server.Tests reran 479/479 and ArchitectureTests reran 56/56 after the review patches. The four other test projects are unchanged since their passing invocations. The one added Fact now exercises nine named independent ordering combinations with expected audit provenance, distinct actors, sensitive-policy comments and canonical unavailable AI metrics.
- Cumulative coverage includes the concrete loader, canonical projection delivery, four-route HTTP no-disclosure, correction-scope contract metadata, serialized scope replay, and legacy mismatched-retry rejection.
- The administrator HTTP flows inject `HttpContext.User` through `ClaimsStartupFilter` and substitute `ScriptedAccessGuard`; they prove claim-to-accessor and authorization-context mapping only, not authenticated deployment. At that prior verification baseline, the host registered no authentication scheme. The current SDK registers workload and sidecar authentication; the 2026-10-08 HTTP journeys are blocked by its authorization policies, as recorded above.
- The configured-port tests simulate `Connection.LocalPort` before the production guard in TestServer and bind the literal `Timesheets:InternalSurface:Port` setting through `IWebHostBuilder.UseSetting`. The endpoint fitness test ties the AppHost's `Timesheets__InternalSurface__Port` export to the Timesheets resource's unconditional, distinct 8080/8081 listeners and the host options section. They prove host configuration/middleware behavior, not a real listener, network isolation, durable command submission, or concurrent single-use consumption. Adding an in-memory configuration source at the deferred minimal-host callback initially failed with a disposed ConfigurationManager; the setting-based fixture passed.
- Historical runtime smoke (2026-10-03): the first Aspire start timed out during restore. A subsequent no-build start succeeded, but `aspire wait timesheets --timeout 30 --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive` exited 18 because Timesheets failed to start; describe showed Timesheets Finished and security Unhealthy. `aspire stop` completed cleanup. No passing current-topology evidence is claimed.
- Runner guidance: the current `dotnet test tests/Hexalith.Timesheets.Works.Tests/Hexalith.Timesheets.Works.Tests.csproj --no-build` invocation exits 1 with Microsoft.Testing.Platform's unsupported VSTest-target error on .NET 10; README now uses the verified direct executables by default. Runner configuration remains unchanged.
- Prior runtime check (2026-10-04, repeated before this reconciliation increment): `aspire start --no-build --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive --format Json` succeeded; `aspire wait timesheets --timeout 30 --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive` exited 18. Describe reported Timesheets Finished (exit 134); `aspire logs timesheets --tail 18` captured `System.IO.IOException: Failed to bind to address http://127.0.0.1:8080: address already in use`. All commands used the explicit AppHost path and `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`; `aspire stop` succeeded. This environment conflict does not explain the historical October 3 failure or prove a running magic-link journey.

- Prior alternate-configuration increment independent review patched four findings (unrelated URLs, ancestor conditions, disabled-region semicolons and interpolation controls) and grouped two verified semantic-analysis limitations into one deferred ledger item. Separate focused reproductions confirmed escaped and concatenated keys plus an escaped property identifier still pass the lexical check, with exact source-byte restoration. The final ArchitectureTests regression covers both element and ancestor conditions; its post-review lane has 56 tests.

**Workflow:** `bmad-qa-generate-e2e-tests`
**Date:** 2026-06-22
**Engineer:** QA automation (generation only — no code review)
**Feature under test:** `EventStoreMagicLinkConfirmationCapabilityStateLoader` + rebuildable token-hash → capability index

## Framework Detected

- .NET 10 / xUnit v3 `3.2.2` + Shouldly `4.3.0` + NSubstitute `6.0.0-rc.1` (existing project conventions reused — no new framework introduced).
- No UI/HTTP surface exists in this backend-only story (the HTTP-boundary `WebApplicationFactory` proof is owned by Story 3.7), so "E2E" here means **API/service-level loader tests** exercised through the real EventStore SDK seams via scripted gateway/read-model stubs.

## Generated Tests

All added to `tests/Hexalith.Timesheets.Server.Tests/EventStoreMagicLinkConfirmationCapabilityStateLoaderTests.cs`
(extending the dev's existing 16 loader tests — no rewrite).

### Gap tests applied (8 new cases)

- [x] `LoadTokenStateAsync_fails_closed_for_blank_token_without_any_read` (Theory: `""`, `"   "`) — **AC3**: blank/malformed token collapses to the identical opaque state *before* any index/EventStore read.
- [x] `LoadTokenStateAsync_fails_closed_when_token_hashing_rejects_malformed_token` — **AC3**: `DeriveHash` `ArgumentException` path fails closed with no reads (previously uncovered catch branch).
- [x] `LoadTokenStateAsync_returns_folded_expired_state_proving_index_is_not_expiry_authority` — **AC3**: parity with revoked/used — expiry truth lives only in the folded aggregate; the index stays a non-authoritative candidate resolver.
- [x] `LoadActivityTypeCatalogAsync_folds_fresh_tenant_catalog_for_admin_paths` — **AC1/AC4**: admin (tenant-less) catalog happy path returns a Fresh folded catalog from the domain-wide read.
- [x] `LoadActivityTypeCatalogAsync_folds_only_tenant_scoped_items_and_reflects_rename_and_deactivation` — **AC2**: project-scoped activity types excluded; rename/deactivate fold applied deterministically.
- [x] `LoadActivityTypeCatalogAsync_folds_events_across_continuation_pages` — **AC2**: the `ReadAllEventsAsync` continuation-token paging loop accumulates every page (previously entirely uncovered).
- [x] `LoadCapabilityAsync_folds_terminal_state_for_admin_revoke_and_expire_paths` — **AC1**: admin revoke/expire observe an already-terminal capability as terminal, not as a fresh issue.

### Test-harness extensions (no production code changed)

- `ThrowingTokenGenerator` stub (proves the malformed-token catch path).
- `ScriptedGatewayClient.WithPagedStream(...)` — multi-page continuation-token support so the paging loop is exercised.
- New deterministic event builders: `Expired()`, `SecondActivityCreated()`, `ProjectScopedActivityCreated()`.

## Coverage

- **Loader (`LoadTokenStateAsync` / `LoadCapabilityAsync` / `LoadActivityTypeCatalogAsync`):** all three methods now have happy-path + fail-closed + admin-path + determinism + paging coverage.
- **Acceptance-criteria mapping:**
  - **AC1** ✅ valid token resolves + folds capability/Time Entry/Fresh catalog; admin paths fold existing state.
  - **AC2** ✅ fold determinism across orderings/duplicates, catalog filtering, multi-page rebuild, index rebuild from `Issued`.
  - **AC3** ✅ full invalid set externally indistinguishable: blank, malformed, hashing-throw, unknown-hash, missing-aggregate, hash-mismatch, cross-tenant, missing-Time-Entry, read-throw, revoked, used, expired.
  - **AC4** ✅ fail-closed on missing trusted tenant + explicit Unavailable catalog on read failure.
- **Server.Tests lane:** **403 passed / 0 failed / 0 skipped** (was 395; +8 new cases).

## Verification

```
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.Server.Tests/...csproj -warnaserror -m:1 /nr:false
→ 0 Warning(s), 0 Error(s)

DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests
→ Total: 403, Errors: 0, Failed: 0, Skipped: 0
```

(`dotnet test` is blocked by local VSTest socket permissions — `SocketException (13): Permission denied` — so the README direct xUnit v3 executable fallback was used, per the story's Debug Log convention.)

## Notes / Deferrals

- The full HTTP-boundary equivalence test (`WebApplicationFactory` / `Mvc.Testing`) remains deferred to **Story 3.7** — the Timesheets module has no runtime host fixtures yet (all read-side readers are `Unavailable` stubs). Service-level no-disclosure equivalence across the AC3 invalid set is already proven by `MagicLinkConfirmationCapabilityCommandServiceTests`.
- Tests are independent (each builds its own scripted gateway/read-model), use deterministic fixed timestamps (no `Date.now`/sleeps/waits), and assert end-state (folded state / freshness), not mock call counts.

## Next Steps

- Run the suite in CI alongside the existing ArchitectureTests / Contracts / Projections / Integration lanes.
- When Story 3.7 lands the runtime host, lift the loader equivalence proofs to the HTTP boundary.

## Spec-15 checkout-path reproduction commands (2026-10-08)

Before rebuilding, the previous Debug binary failed one selected case on the harmless framework content-root message:

```bash
ASPNETCORE_TEST_CONTENTROOT_HEXALITH_TIMESHEETS=/tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Invalid_magic_link_http_boundary_emits_no_timesheets_sensitive_diagnostics'
```

After rebuilding, all four selected cases stop at unexpected 401 responses before the corrected assertions:

```bash
ASPNETCORE_TEST_CONTENTROOT_HEXALITH_TIMESHEETS=/tmp/timesheets-privacy-60-used-token-Draft-Delivery-comment-revoked DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Invalid_magic_link_http_boundary_emits_no_timesheets_sensitive_diagnostics' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.HostingDiagnosticsFilterSurvivesProviderSpecificInformationConfiguration' -method 'Hexalith.Timesheets.IntegrationTests.MagicLinkConfirmationHttpBoundaryTests.Empty_magic_link_token_records_the_malformed_outcome_category_without_sensitive_diagnostics'
```

Create that empty scratch directory before either invocation. The prior binary's failure is dated evidence, not reproducible from the current rebuilt output. The retained [privacy assertion verifier](3-6-privacy-assertion-verifier/Program.cs) and its command above reproduced 144 helper checks on 2026-10-08 without changing authentication; the current in-lane theory supersedes it.

The same four scratch-root HTTP cases were rerun on 2026-10-09 from the spec-16 pre-review output with the second command above: four failures, all `HttpStatusCode.Unauthorized`, before any privacy log loop. The spec-16 420-case in-lane theory superseded the retained verifier; the current spec-17 theory has 432 cases and also checks the exact fixture comment in every category.

Independent review strengthened eight test/record findings and deferred one pre-existing assertion false-positive risk: generic `60` substring checks can reject permitted correlation or timestamp data. The corresponding field-aware assertion follow-up is recorded in the spec-16 deferred-work section. These are test controls, not current HTTP or persisted-state evidence.

## Explicit verified v1-to-v7 migration implementation (2026-10-12)

Implemented the separately authenticated internal operator route
`/admin/timesheets/magic-link-index/migrate-v1`. Its request identifies an
operation and a server receipt; it never accepts a caller fingerprint as
coverage proof. `UnavailableMagicLinkV1VerifiedInventoryProvider` and the
existing default-deny migration authority remain the production defaults.
The v6 migrator, ordinary projection/loader, SDK sources, Redis, AppHost,
ServiceDefaults, dependencies and gitlinks were not changed.

The receipt binds expected retained contribution count/digest, exact source
cut, independently verified installation coverage (including certified erasure
exclusions), and externally enforced writer/delivery quiescence. The provider
contract includes terminal capabilities and omitted collisions. Inventories
replay in ordinal tenant/owner order, reject duplicate/conflicting owners and
more than 10,000 retained rows per tenant, and reject oversized identifiers.
Preflight validates the complete stream before epoch reservation; resume and
promotion recheck the source, receipt and authority. Checkpoints persist full
receipt/source bindings at complete tenant boundaries. Private append pages
contain at most eight logical mutations and respect the SDK byte limit;
roster pages contain at most 64 rows. I/O/cancellation stages resume after
coordinator/service recreation; invalidated evidence aborts without selecting
v7. Direct and selected v1 data remain available after promotion.

Exact focused commands (all executed from the repository root with SDK
`10.0.401`):

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.Projections.Tests/Hexalith.Timesheets.Projections.Tests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -class '*MagicLinkV1MigrationTests'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*InternalSurfaceGuardTests'
```

Both builds exited 0 with zero warnings/errors. The focused migration suite
passed 42/42 with no skips in 1.282 seconds; the composed internal-ingress
suite passed 31/31 with no skips in 6.517 seconds. Exact logs are
`/tmp/timesheets-v1-projections-build.log`,
`/tmp/timesheets-v1-migration-tests.log`,
`/tmp/timesheets-v1-integration-build.log`, and
`/tmp/timesheets-v1-internal-ingress-tests.log`. Fault tests assert that each
simulated interruption actually fired, including after selector CAS and before
the SDK commit marker. Fixtures represent simulated verification only.

No live lane or AppHost was started. This repository still lacks independently
verified immutable cross-tenant inventory/installation proof, deployed operator
authority, enforced quiescence and verified recovery of the previously observed
pending legacy journal. A caller-supplied receipt, legacy rows, checkpoint
census or observed maximum cannot replace them. Missing provider installation
is a runtime blocker. The existing live journey failure remains the latest
live evidence. Story 3.6 remains in progress and release remains **FAIL**.
Parent broad verification and independent build reviews are pending.

Post-invalidation verification also ran the unchanged v6 migration, numeric
index and global-index epoch classes with the new migration suite:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -class '*MagicLinkV1MigrationTests' -class '*MagicLinkV6MigrationTests' -class '*MagicLinkNumericIndexTests' -class '*MagicLinkGlobalIndexEpochTests'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests'
```

The index classes passed **61/61**, no skips, in 27.194 seconds; launch fitness
passed **13/13**, no skips, in 0.168 seconds. Logs are
`/tmp/timesheets-v1-index-focused-tests.log` and
`/tmp/timesheets-v1-launch-readiness-tests.log`. The final individual v1
rerun passed **42/42** in 1.282 seconds and ingress rerun passed **31/31**
in 6.517 seconds after the revoked-lookup recovery case was added. That
case returns no receipt on resume, aborts only the matching operation/receipt,
retains legacy selection, and succeeds under a fresh operation after restored
simulated evidence. `git diff --check` exited 0. No full-suite or deployed
acceptance is claimed by this handoff.

### Abort-cleanup restart recovery (2026-10-12)

The migration now persists an immutable numeric abort witness through
EventStore `ReadModelWritePolicy` before invoking SDK abort cleanup. It is
outside disposable generation pages and retains the original epoch,
operation, exact receipt and source binding after checkpoint 0 is erased.
A recreated migrator checks the witness before reading disposable checkpoints,
requires server-side authority for the original receipt fingerprint, and
rejects changed operation/receipt identifiers or changed receipt contents.
Revoked receipt lookup does not lose cleanup ownership. Matching retries
finish abort without selecting v7; the aborted operation stays terminal and
a fresh authorized operation can subsequently promote.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.Projections.Tests/Hexalith.Timesheets.Projections.Tests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -class '*MagicLinkV1MigrationTests'
```

The build exited 0 with zero warnings/errors in 9.81 seconds. All **43/43**
v1 migration cases passed, no skips, in 1.911 seconds. Logs are
`/tmp/timesheets-v1-abort-recovery-build.log` and
`/tmp/timesheets-v1-abort-recovery-tests.log`. The new deterministic test
fails once after the SDK physically deletes checkpoint 0, asserts Aborting
and unchanged legacy selection, recreates the coordinator/service, refuses
mismatched and unauthorized retries, finishes only the bound cleanup even
when receipt lookup returns null, and verifies all persisted v7 candidates
under a fresh operation. `git diff --check` exited 0. Earlier focused counts
remain historical; parent broad verification/review is pending.


## Parent verification of the v1 migration (2026-10-12)

The reviewed implementation candidate is the working tree based on
`60580d974c6e77a7e9fe0cbab93f22670c02ecda`, including abort-cleanup recovery.
SDK `10.0.401` is selected by current `global.json`.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Works.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Works.Tests -noColor
```

Restore exited 0. Build exited 0 with zero warnings/errors in 30.44 seconds.
Logs: `/tmp/timesheets-v1-parent-restore.log` and
`/tmp/timesheets-v1-parent-build.log`. The test projects were launched
individually, concurrently; each executable exited 0.

| Default suite | Total | Passed | Failed | Skipped | Time (seconds) |
|---|---:|---:|---:|---:|---:|
| ArchitectureTests | 56 | 56 | 0 | 0 | 6.615 |
| Contracts.Tests | 92 | 92 | 0 | 0 | 1.840 |
| Server.Tests | 543 | 543 | 0 | 0 | 2.060 |
| Projections.Tests | 365 | 365 | 0 | 0 | 620.589 |
| IntegrationTests | 713 | 708 | 0 | 5 | 648.213 |
| Works.Tests | 76 | 76 | 0 | 0 | 0.673 |
| Total | 1845 | 1840 | 0 | 5 | |

Per-project logs are `/tmp/timesheets-v1-parent-<suite>.log` with the exact
suite names above. Integration's five skips retain the declared performance,
live Dapr, and infrastructure opt-ins. No live or performance opt-in was set.

The scoped spec's complete matrix is covered by passing tests in
`MagicLinkV1MigrationTests` (43/43) and `InternalSurfaceGuardTests` (31/31):

| Matrix row | Passing coverage |
|---|---|
| Authority absent | MissingAuthorityCoverageOrQuiescenceNeverReservesAnEpoch; internal default-deny/authentication/provider cases |
| Unique-only legacy | DirectOrSelectedV1RecoversAllCertifiedCandidatesIncludingCrossTenantCollisions |
| Invalid inventory | InvalidCompleteInventoryIsRejectedBeforeReservation; ReceiptRevocationDuringStagingIsCheckedAgainBeforePromotion |
| Unsafe source | PendingLegacyDeliveryIsRefusedWithoutFabricatingCapturedIdentities; MixedOrMissingLegacyLayoutsAreRefusedBeforeReservation; invalidated-root/source-cut cases |
| Interrupted migration | InterruptedPrivatePagesCheckpointSealAndCommitResumeAfterRecreation; InterruptedAbortCleanupRetainsReceiptBindingAfterErasingFirstCheckpoint |
| Failed migration | InvalidatedStagedEvidenceAbortsWithLegacySelectedAndPrivateGenerationIsolated; revoked lookup and abort-cleanup recovery cases |
| Successful migration | DirectOrSelectedV1RecoversAllCertifiedCandidatesIncludingCrossTenantCollisions; Verified_internal_v1_route_selects_persisted_v7_candidates_and_retains_direct_legacy; changed-receipt refusal |

PhysicalStagingUsesAtMostEightMutationsAndSixtyFourRowsPerRosterPage checks
persisted bounded pages and every candidate/owner/tenant readback. Fixtures
simulate independent verification; they certify no deployed inventory.
Unchanged v6 and ordinary bootstrap/rebuild regressions passed in the full
projection and integration suites.

Additional inventory investigation found `ListTenantsQueryHandler` in the
sibling Tenants module serves a paginated tenant-index projection visible to
the caller (all tenants for a global administrator) and returns an empty
result when the index is absent. That view is not independent source coverage.
EventStore's installed source rosters remain tenant-scoped and require
independent coverage and all-writer receipts. Neither mechanism establishes
the missing complete global legacy owner census in this checkout.

Story 3.6 remains in progress and release FAIL. No live lane was run because
real inventory/authority/quiescence and recorded pending-journal recovery
cannot be established. Three independent build reviews are pending.


## Post-review v1 migration verification (2026-10-12)

Three independent context-free reviews completed. All 18 reviewer findings
were triaged individually before grouping; the parent then verified two
additional reservation races. All 19 patch findings are implemented and
have regression evidence in the scoped spec's review closure. One
pre-existing prerequisite is deferred: independently verified legacy
source-owner delivery continuity/handoff and pending-journal recovery.
No candidate or unique-only legacy row is converted into a source checkpoint.

The final edited-file checks were:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.Projections.Tests/Hexalith.Timesheets.Projections.Tests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -class '*MagicLinkV1MigrationTests' -class '*MagicLinkV1InventoryDigestTests'
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*InternalSurfaceGuardTests'
```

Both builds exited 0 with zero warnings/errors. Migration/digest passed
**64/64 in 17.710 seconds**; ingress passed **37/37 in 6.634 seconds**, with
no skips or failures. Logs are `/tmp/timesheets-v1-review-{build,tests,ingress-build,ingress-tests}.log`.
Fixed literal receipt and contribution vectors were independently computed
with Python SHA-256 and big-endian length prefixes. They cover property
order, Unicode/escaped values and empty inventory.

The 10,000-contribution boundary test uses a 256-byte tenant, 120-character
owners and 128-character hashes. Every persisted token/owner row and the
exact complete roster match the source. The measured largest physical page
canonical values total **194,660 bytes**, the largest roster page is
**155,722 bytes**, and **156 byte-split pages** were observed; append width
is at most eight. The SDK's real batch validation accepted all staged pages.
No global inventory is retained in memory; staging and membership checks
retain one bounded tenant, with global leaf counts traversed separately.

New deterministic callbacks demonstrate revocation/source changes during
final replay and after successful sealing, persisted Begin before checkpoint
zero, denied cleanup preserving exact state, changed malformed receipts,
checkpoint-root corruption, selected marker repair after certification
expiry, refusal of invalid selected layouts, and both preflight and
post-ownership intervening promotions. A concurrent abort during unbound
ownership recovery binds and isolates only the actually acquired epoch.
Terminal retries cannot allocate again. Every injected callback/fault is
asserted to have fired. Simulated receipt/authority fixtures certify no real
installation.

No live journey or AppHost was started. The independent inventory investigation
found no installed global legacy census or migration authority. Tenant-index
queries and per-tenant source rosters/snapshots cannot certify omitted global
collisions; checkpoint enumeration can skip absent/corrupt pages. Complete
immutable cross-tenant coverage, real authority, externally enforced source/
delivery quiescence, authoritative owner continuity/handoff and verified
recovery of the previously recorded four uncovered legacy journal entries
must exist before a real migration/live journey. Default provider remains
unavailable and authority default-deny. Story 3.6 and release remain **FAIL**.

### Final parent build and six default suites
The parent rebuilt the final source with SDK `10.0.401` after the review patches. No dependencies changed; the successful source-reference restore recorded above remains applicable.
```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -p:HexalithCommonsRoot=/tmp/no-commons -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Contracts.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Contracts.Tests -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Server.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Server.Tests -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.Works.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Works.Tests -noColor
```

Build exited 0 in **10.07 seconds**, zero warnings/errors; log `/tmp/timesheets-v1-postreview-build.log`. Every test executable exited 0. The six projects ran individually, concurrently; no live or performance opt-in was set.

| Default suite | Total | Passed | Failed | Skipped | Time (seconds) |
|---|---:|---:|---:|---:|---:|
| ArchitectureTests | 56 | 56 | 0 | 0 | 3.495 |
| Contracts.Tests | 92 | 92 | 0 | 0 | 0.915 |
| Server.Tests | 543 | 543 | 0 | 0 | 1.376 |
| Projections.Tests | 386 | 386 | 0 | 0 | 570.029 |
| IntegrationTests | 719 | 714 | 0 | 5 | 596.765 |
| Works.Tests | 76 | 76 | 0 | 0 | 0.581 |
| Total | 1872 | 1867 | 0 | 5 | |

Per-project logs are `/tmp/timesheets-v1-postreview-<suite>.log` with the exact suite names above; parsed totals are `/tmp/timesheets-v1-postreview-results.json`. The five expected Integration skips are the declared live, performance and infrastructure lanes. Earlier counts in this document are dated evidence.

The scoped migration spec is done. Story 3.6, its original live-dependent spec tasks and sprint state remain in progress; Story 3.6/release remain **FAIL**. Redis, AppHost, ServiceDefaults, ordinary delivery/loader, the v6 migrator, SDK sources, dependencies and gitlinks are unchanged. The Git index is untouched and HEAD remains `60580d974c6e77a7e9fe0cbab93f22670c02ecda`.


Final launch-readiness fitness after the evidence edits exited 0: **13/13 in
0.136 seconds**, no skips or failures:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor
```

Log: `/tmp/timesheets-v1-postreview-final-readiness.log`. `git diff --check`
passed. Protected-source/story comparisons and the empty staged diff confirm
preservation of the existing work. The final baseline-relative unified diff,
including every untracked file, is
`/tmp/timesheets-v1-migration-postreview-4uzfa6gj.diff`; no Git staging was used.

### Startup-hardening final Python/readiness and preservation audit (2026-10-13)

The final focused Python invocation above passed **65/65 in 7.398s**, exit 0,
no skips; `python-verified.log` under the hardening evidence root records it.
After the evidence edits, the focused LaunchReadiness executable passed
**13/13 in 0.135s**, exit 0, no errors/failures/skips/not-run:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor
```

Log: `readiness.log`. `preservation-baseline.json` and
`source-preservation.json` prove the completed fixture spec, sprint status,
launch-readiness verdict, checked-in components, SDK and centralized package
file hashes match; full HEAD, all indexed gitlinks and Git index bytes also
match. No staging, commit, push or dependency update occurred. The recorded
`runtime-inventory.json` then showed no Dapr instances, no AppHosts, no
fixture-labelled containers and no temporary guard component directories.
The foreign Tenants run had exited independently; a fresh-baseline retry of
the infrastructure-backed guards and isolated live runner is being recorded
separately from the preserved preflight failures above.

### Infrastructure-backed compiled guard retry (2026-10-13)

With empty runtime preflight, the same default guard command above passed
**21/21**, driver exit **0**. Evidence:
`/tmp/timesheets-apphost-hardening-20261013/guards/run-5gzxbytp`;
log `guards-retry.log`. Valid isolated and ordinary selection exited 0;
all 19 incomplete/unsafe/publish cases refused with exit -6. Every probe
returns before resources even if admission validation regresses.

Read-only before/after preservation reports `matches=true`: DB0 **113,816
keys** (113,690 permanent/126 expiring), DB13 **2,782** (2,671/111), DB14
**773 permanent**, and **55 scheduler KV entries** with the same cluster
identity and revision **12593**. Retained TTL values/deadlines match with
zero natural expirations. `cleanup.json` reports complete cleanup, journey
descendants gone, all three owned containers removed and components removed.
No AppHost/security/resource graph was constructed by any probe. These
fresh-baseline totals differ from the previous day's baselines; cross-run
changes are not attributed, repaired or cleared. The initial foreign-runtime
refusal remains historical failure evidence.

### Startup-hardening final preservation and cleanup audit (2026-10-13)

Final LaunchReadiness passed **13/13 in 0.137s**, exit 0, after the final
evidence/spec edits. Exact command:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -noColor
```

`source-preservation-final.json` confirms the completed isolation spec
(including frozen intent), sprint status, launch readiness, default components,
SDK and centralized package catalog hashes remain unchanged. HEAD remains
`a377d1240a86074b670d43796b9d2dcc49927f5e`; all indexed gitlinks and Git index
bytes match the baseline. The staged diff is empty. `runtime-inventory-final.json`
records no Dapr instances, no Aspire AppHosts, no fixture-labelled containers,
no temporary guard component directories, and the removed live component copy.
The passing live run's 16 captured identities, empty resource scanner and
three-container/component cleanup proofs are retained. Final `git diff --check`
passed. No stage/commit/push or dependency/gitlink update occurred. The scoped
startup-hardening spec is done; Story 3.6/Epic 3 stay in progress and release
readiness stays **FAIL** pending legacy and deployed certification.
