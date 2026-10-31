---
title: 'Obtain current-source isolated live magic-link proof for Story 3.6'
type: 'chore'
created: '2026-10-30'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'c4393bbcaad70ba12901c0d71c05fd7c9a9a4c36'
story_key: '3-6-implement-eventstore-backed-magic-link-state-loading'
context:
  - 'AGENTS.md'
  - 'references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Completed spec-21 retained 173 passing focused cases, but its one live attempt stopped at admission when eight Dapr applications were running. Current-source isolated live magic-link qualification remains missing.

**Approach:** Inspect runtime admission without disturbing foreign resources. If blocked, retain a sanitized blocker receipt and stop. If idle, prepare matching Debug/source assets and invoke the supported isolated runner exactly once. Success requires the live journey, shared-state preservation, and owned cleanup all to pass.

## Boundaries & Constraints

**Always:** Preserve selected-index/effective-value, packed-lineage, replay/race/rebuild/restart, duplicate-id, privacy and recording-delay assertions; server authorization/default-deny behavior; prior evidence and user edits. Use EventStore for fixture domain writes. Retain source identity, exact commands, UTC timing, exits and sanitized evidence with original hashes. Report unobserved gates. Keep Story 3.6 and incomplete proof in-progress, release FAIL, and changes uncommitted.

**Never:** Stop foreign resources; clear stores; run migrations; change AppHost, ServiceDefaults, topology, dependencies, authorization, or assertions; use shared stores for fixture writes; retry a refused or failed attempt; treat the earlier 173 cases or a local pass as deployed acceptance.

</frozen-after-approval>

## Code Map

- `scripts/run_isolated_magic_link_dapr.py` — supported runner rejects unsafe inventories, sidecars and ports; holds admission lock through cleanup/preservation; admission precedes shared observation and infrastructure.
- `scripts/local_runtime_admission.py` — canonical nonblocking admission lock. Reuse it for the availability inspection; release before invoking the runner, whose own checks remain mandatory.
- `scripts/local_aspire_magic_link_journey.py` and `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkLiveDaprTests.cs` — existing persisted journey and required outcome markers; preserve unchanged.
- `spec-3-6-implement-eventstore-backed-magic-link-state-loading-21.md` and final spec-21 section in `tests/3-6-test-summary.md` — completed context and 173-case mapping; no rerun needed.
- `epic-3-context.md` — refreshed planning cache; runtime/deployed evidence remains independently required.

## Tasks & Acceptance

**Execution:**

- [x] `scripts/local_runtime_admission.py` / runtime inventory — inspected once under the canonical lock. `dapr list --output json` exited 0 with a JSON array of eight objects, so idle admission was refused. No Aspire inventory, build, runner or further runtime probe followed. [Sanitized receipt and original hashes](evidence/3-6/20261030-current-source-live-proof/admission-inspection.json).
- [ ] `Hexalith.Timesheets.slnx` / `scripts/run_isolated_magic_link_dapr.py` — only after idle inventory, restore/build matching Debug/source assets, then run once: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-current-source-live-proof-20261030`. Preserve the runner's own admission checks. Stop after any failure; no alternative lane or retry.
- [x] `evidence/3-6/20261030-current-source-live-proof/` — [retained admission/provenance packet](evidence/3-6/20261030-current-source-live-proof/README.md), including explicit unobserved baseline, journey, preservation and cleanup dispositions. No local proof is qualified.
- [x] `3-6-implement-eventstore-backed-magic-link-state-loading.md`, `tests/3-6-test-summary.md`, `docs/launch-readiness.md`, and this spec — appended disposition/links. Sprint Story 3.6 remains in-progress, release remains FAIL, and this spec's conditional live-proof task remains open.

**Acceptance Criteria:**

- Given foreign activity or unsafe admission, when inspection refuses, then record the exact blocker, start no fixture/build, stop without retry, and make no live or preservation claim.
- Given idle admission, when the single supported attempt completes, then qualify local proof only when journey, shared-state preservation and cleanup all pass; otherwise retain the terminal failure and stop.
- Given existing source assertions, defaults and evidence, when this continuation ends, then their contents remain preserved and only evidence/planning documents are changed.
- Given unfinished deployed authority, inventory/continuity and shared-rebuild acceptance, when local evidence is reconciled, then Story 3.6 stays in-progress and release readiness stays FAIL.

## Implementation Notes

The single locked inspection ran **2026-10-30T18:02:28.049236+00:00 → 2026-10-30T18:02:28.312107+00:00**, exit **1**, **0.262842s**. Its sole runtime command, `dapr list --output json`, exited **0** with **eight object rows**, so the required successful empty inventory was absent. The lock was released; no Aspire inventory, restore/build, supported runner, shared-store observation, fixture resource or domain write followed. No retry or migration occurred.

[Exact commands, UTC timing, exit codes, original output hashes and sanitized aggregate shape](evidence/3-6/20261030-current-source-live-proof/README.md) are retained without foreign rows. Shared baseline, journey, shared-state preservation and owned cleanup remain unobserved. The source assertions, authorization defaults, prior evidence and user edits are preserved. The earlier 173 focused cases were not rerun and do not qualify this runtime. Story 3.6 and this proof remain **in-progress**; deployed authority/inventory/continuity/shared-rebuild acceptance remains unfinished and release remains **FAIL**.

## Spec Change Log

## Review Triage Log

## Design Notes

No intent gaps. The invocation authorizes conditional execution and terminal stop. Side effects are disposable owned infrastructure and EventStore fixture events. Footprint: spec, refreshed epic cache, evidence and three append-only documents. Blockage ends this invocation without qualifying live proof.

## Verification

Only if admitted, restore/build `Hexalith.Timesheets.slnx` with the exact Debug/source commands in root AGENTS.md, including `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`. No build before idle admission.

Check receipts, source hashes, original evidence prefixes, Markdown links and `git diff --check`. No additional tests/probes after terminal blockage/failure.

Only the prescribed static receipt/hash/prefix/link checks and `git diff --check` ran after terminal blockage. [Verification results](evidence/3-6/20261030-current-source-live-proof/verification.json) are retained in the packet; no post-refusal test, build or runtime probe is claimed.


## Authorized continuation — 2026-10-31

The user explicitly authorized **one new attempt** on 2026-10-31. The checked tasks and 2026-10-30 dispositions above are historical evidence, not completion of this new attempt. Preserve the existing working tree, frozen intent, baseline commit, prior receipts and all original document bytes. This authorization does not permit retrying a refused or failed attempt within this continuation.

- [ ] Inspect runtime availability once under `LocalRuntimeAdmission` using the ordered inspection harness retained in `evidence/3-6/20261030-current-source-live-proof/admission-inspection.py.txt`: Dapr inventory first, Aspire inventory only after successful empty Dapr inventory. Retain exact commands, UTC timing, exits, aggregate shape and original hashes; withhold foreign rows/identities. If admission is blocked, retain a **new** sanitized blocker packet and stop all runtime/build/test work without retry.
- [ ] Only if idle, restore/build matching Debug/source assets using the root AGENTS.md commands with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`, then invoke the supported runner **exactly once** with a fresh log directory: `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-current-source-live-proof-20261031`. Preserve its mandatory admission checks. Stop after any failure; no alternate lane or retry.
- [ ] Retain evidence in a fresh directory under `evidence/3-6/20261031-current-source-live-proof/` (use a unique suffix if already present), without overwriting either 2026-10-30 packet. Retain source identity and original/sanitized provenance; explicitly mark unobserved gates. Local success requires **live journey, shared-state preservation and owned cleanup all passing**.
- [ ] Append this attempt's disposition and links to this spec, `3-6-implement-eventstore-backed-magic-link-state-loading.md`, `tests/3-6-test-summary.md`, and `docs/launch-readiness.md`. Keep this spec and Story 3.6 **in-progress**, release **FAIL** while deployed authority/inventory/continuity/shared-rebuild acceptance remains unfinished, and all changes **uncommitted**. After terminal refusal/failure, only static receipt/hash/prefix/link checks and `git diff --check` are permitted.

Do not stop foreign resources, clear stores, run migrations, change AppHost/ServiceDefaults, topology, dependencies, authorization/default-deny behavior or assertions, or use shared stores for fixture writes. No rerun of the historical 173 focused cases is needed. Preserve user changes; do not stage or commit.


## Authorized continuation disposition — 2026-10-31

The one newly authorized availability inspection used the canonical `LocalRuntimeAdmission` lock at Timesheets `c4393bbcaad70ba12901c0d71c05fd7c9a9a4c36`. It ran **2026-10-31T05:06:39.210529+00:00 → 2026-10-31T05:06:39.320801+00:00**, exit **1**, **0.110244s**. The lock was acquired at **2026-10-31T05:06:39.210640+00:00** and released at **2026-10-31T05:06:39.320797+00:00**. The only runtime command, `dapr list --output json`, ran **2026-10-31T05:06:39.210643+00:00 → 2026-10-31T05:06:39.320462+00:00**, exited **0** after **0.109844s**, and returned a JSON array of **eight application objects**. The required successful empty inventory was absent.

- [x] The single ordered locked inspection completed with terminal admission refusal; its [sanitized receipt](evidence/3-6/20261031-current-source-live-proof/admission-inspection.json) retains exact commands, UTC timing, exits, aggregate shape and original output hashes.
- [ ] Conditional Debug/source restore/build and supported isolated runner remain uninvoked because idle admission failed. Current-source live proof remains open; this invocation ended without retry.
- [x] A fresh [evidence packet](evidence/3-6/20261031-current-source-live-proof/README.md) retains source identity, original/sanitized provenance and every unobserved gate without overwriting either 2026-10-30 packet.
- [x] This disposition and links were appended to this spec, the Story 3.6 record, test summary and launch-readiness record.

No Aspire inventory, solution restore/build, supported runner, shared-store observation, fixture resource or fixture domain write followed. There was no retry, alternate lane, migration or foreign-resource change. Shared baseline, live journey, shared-state preservation and owned cleanup remain **unobserved**; creating no fixture resources does not qualify runner cleanup. Only static receipt/hash/prefix/link checks and `git diff --check` followed refusal. Earlier evidence, source assertions, authorization/default-deny behavior and all original document bytes are preserved; the historical **173 focused cases** were not rerun. No current built/running-asset, runtime-version or inherited-environment claim is made.

[Static verification](evidence/3-6/20261031-current-source-live-proof/verification.json) covers receipt/output hashes and shape, source/prior-evidence hashes, original byte prefixes, frozen intent, local Markdown links and `git diff --check`.

Current-source local qualification and deployed authority, complete inventory/writer-delivery continuity and shared-rebuild acceptance remain unfinished. Story 3.6 and this live-proof spec remain **in-progress**; release remains **FAIL**. All changes remain **uncommitted**.


## New authorized attempt — 2026-10-31 (attempt 2)

The current user invocation authorizes **one further attempt**, separate from the
completed 2026-10-30 and 2026-10-31 05:06 UTC refusals. Their receipts and all
pre-existing document bytes must remain intact. This section is the active
continuation; earlier completed authorization sections are historical. Preserve
the existing frozen intent, frontmatter baseline, source assertions and
authorization defaults. All changes remain uncommitted.

- [ ] Inspect availability **once** under the canonical `LocalRuntimeAdmission`
  lock, using the exact ordered harness retained at
  `evidence/3-6/20261030-current-source-live-proof/admission-inspection.py.txt`.
  Dapr inventory comes first; Aspire inventory is permitted only after a
  successful empty Dapr inventory. Release the inspection lock before any runner
  invocation. If blocked, retain a new sanitized blocker packet and stop all
  runtime/build/test work; no retry or alternate lane.
- [ ] Only if idle, restore/build matching Debug/source assets with the root
  AGENTS.md commands, each prefixed `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`, then
  invoke `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3
  scripts/run_isolated_magic_link_dapr.py --log-directory FRESH_LOG_DIRECTORY`
  exactly once, preserving all runner admission checks. Any restore/build/runner
  failure ends the attempt without retry. Use the fresh path in the provenance
  snapshot below for `FRESH_LOG_DIRECTORY`; do not reuse earlier log paths.
- [ ] Retain sanitized evidence in the fresh directory
  `evidence/3-6/20261031-current-source-live-proof-2/`, with source identity, exact
  commands, UTC timing, exits, original hashes and sanitized provenance. Explicitly
  mark unobserved gates. Local qualification requires live journey, shared-state
  preservation and owned cleanup all to pass.
- [ ] Append the disposition and evidence links to this spec, the Story 3.6
  record, test summary and launch-readiness document. Story 3.6 and this spec stay
  **in-progress**; release stays **FAIL** while deployed acceptance is unfinished.
  Preserve every earlier receipt and user edit. After terminal refusal/failure,
  perform only static receipt/hash/prefix/link checks and `git diff --check`.

The pre-attempt source/document/evidence snapshot is retained privately at
`/tmp/timesheets-current-source-live-proof-20261031-attempt2-rmqgw3i6/before.json`; four `document-N.original` files preserve the exact
pre-invocation prefixes. Copy its relevant provenance into the new evidence packet
without including private data. No runtime command has run for this authorization
at the point this section was appended.

Do not stop foreign resources, clear stores, run migrations, change
AppHost/ServiceDefaults, topology or dependencies, or use shared stores for fixture
writes. Preserve assertions and default-deny behavior. Historical 173 focused cases
need no rerun. Do not stage, commit, or create additional probes.


## Attempt 2 disposition — 2026-10-31

The single ordered availability inspection ran **2026-10-31T06:04:10.176510+00:00 → 2026-10-31T06:04:10.407450+00:00**, exit **1**, **0.230897s**, at Timesheets `c4393bbcaad70ba12901c0d71c05fd7c9a9a4c36`. The canonical `LocalRuntimeAdmission` lock was acquired at **2026-10-31T06:04:10.176629+00:00** and released at **2026-10-31T06:04:10.407447+00:00**. Its sole runtime command, `dapr list --output json`, ran **2026-10-31T06:04:10.176633+00:00 → 2026-10-31T06:04:10.407131+00:00**, exit **0**, **0.230515s**, and returned a JSON array of **eight application objects**. The required successful empty inventory was absent.

- [x] The newly authorized single locked inspection completed with terminal admission refusal; the [sanitized receipt](evidence/3-6/20261031-current-source-live-proof-2/admission-inspection.json) retains exact commands, UTC timing, exits, aggregate shape and original hashes.
- [ ] Conditional Debug/source restore/build and the supported isolated runner remain uninvoked because idle admission failed. The live-proof task remains open and this invocation ended without retry.
- [x] The fresh [attempt 2 evidence packet](evidence/3-6/20261031-current-source-live-proof-2/README.md) retains source identity, original/sanitized provenance and every unobserved gate, preserving all earlier packets.
- [x] This attempt's disposition and links were appended to this spec, the Story 3.6 record, test summary and launch-readiness document.

No Aspire inventory, solution restore/build, supported runner, shared-store observation, fixture resource or fixture domain write followed. There was no retry, alternate lane, migration or foreign-resource change. Shared baseline, live journey, shared-state preservation and owned cleanup remain **unobserved**; creating no fixture resources does not qualify runner cleanup. Only static receipt/hash/prefix/link checks and `git diff --check` followed terminal refusal. Source assertions, authorization/default-deny behavior, prior receipts and every pre-existing document byte are preserved. The historical **173 focused cases** were not rerun; no current built/running-asset, runtime-version or inherited-environment claim is made.

[Static verification](evidence/3-6/20261031-current-source-live-proof-2/verification.json) covers original receipt/output hashes and shape, source/prior-evidence hashes, original byte prefixes, frozen intent, local Markdown links and `git diff --check`.

Current-source local qualification and deployed authority, complete inventory/writer-delivery continuity and shared-rebuild acceptance remain unfinished. Story 3.6 and this live-proof spec remain **in-progress**; release remains **FAIL**. All changes remain **uncommitted**.


## New authorized attempt — 2026-10-31 (attempt 3)

The current user invocation authorizes **one new attempt**, separate from every
completed refusal above. This is the active continuation; earlier authorization
sections and receipts are historical. Preserve every pre-existing document byte,
source assertion, authorization default, frozen intent and baseline identifier.
The existing working tree is authorized for this bounded continuation; all
changes remain uncommitted.

- [ ] Inspect availability **exactly once** under `LocalRuntimeAdmission`, using
  the unchanged ordered harness retained at
  `evidence/3-6/20261030-current-source-live-proof/admission-inspection.py.txt`.
  Copy it byte-for-byte into the fresh private scratch directory and invoke it
  once from the repository root. Dapr inventory comes first; Aspire inventory
  is permitted only after successful empty Dapr inventory. Do not stop foreign
  resources. Release the inspection lock before invoking the runner. If blocked,
  retain a fresh sanitized blocker receipt and stop without retry.
- [ ] Only if idle, restore/build matching Debug/source assets, sequentially:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`,
  then `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`.
  Only after both pass, invoke exactly once:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-current-source-live-proof-20261031-attempt3-2d4f_pb8-runner`.
  Preserve all runner admission checks. Any refusal or failure ends the attempt;
  no retry, fallback, alternate lane or additional probe is authorized.
- [ ] Retain a fresh sanitized packet at
  `evidence/3-6/20261031-current-source-live-proof-3/`, including source identity,
  exact executed and conditional uninvoked commands, UTC timing, exits, original
  hashes, sanitized provenance and explicit unobserved gates. Keep raw foreign
  inventories and sensitive originals private. Success requires the live journey,
  shared-state preservation and owned cleanup **all to pass**; no fixture creation
  does not establish cleanup success. Preserve every prior packet.
- [ ] Append the disposition and evidence links to this spec,
  `3-6-implement-eventstore-backed-magic-link-state-loading.md`,
  `tests/3-6-test-summary.md` and `../../docs/launch-readiness.md`. Keep this spec
  and Story 3.6 **in-progress**, and release **FAIL** while deployed authority,
  complete inventory/writer-delivery continuity and shared-rebuild acceptance
  remain unfinished. Verify receipt/output hashes and shape, unchanged source
  and prior evidence, all four original document byte prefixes, frozen intent,
  local Markdown links and `git diff --check`.

The pre-invocation source/document/evidence snapshot is private at
`/tmp/timesheets-current-source-live-proof-20261031-attempt3-2d4f_pb8/before.json`;
its four `document-N.original` files preserve the exact original document bytes.
Retain relevant sanitized source identities, manifest hashes and prefix hashes
in the fresh packet. The snapshot includes current root-declared submodule
identities without initializing or updating them. No runtime command has run for
this authorization when this section is appended.

Do not clear stores, run migrations, change AppHost/ServiceDefaults, topology or
dependencies, or use shared stores for fixture writes. Preserve assertions and
default-deny behavior. The historical 173 focused cases need no rerun. Do not
stage, commit or discard user changes. After terminal refusal/failure, complete
only the required sanitized receipt/provenance and append-only document recording,
then the prescribed static receipt/hash/prefix/link checks and `git diff --check`;
no further workflow review, tests, builds or runtime probes follow. The conditional
live-proof task stays open if admission fails. Do not mark historical unchecked
tasks as completed by this new attempt.


## Attempt 3 disposition — 2026-10-31

The single ordered availability inspection ran **2026-10-31T06:36:18.721203+00:00 → 2026-10-31T06:36:18.824957+00:00**, exit **1**, **0.10373s**, at Timesheets `c4393bbcaad70ba12901c0d71c05fd7c9a9a4c36`. The canonical `LocalRuntimeAdmission` lock was acquired at **2026-10-31T06:36:18.721279+00:00** and released at **2026-10-31T06:36:18.824953+00:00**. Its sole runtime command, `dapr list --output json`, ran **2026-10-31T06:36:18.721281+00:00 → 2026-10-31T06:36:18.824622+00:00**, exit **0**, **0.103359s**, and returned a JSON array of **eight application objects**. The required successful empty inventory was absent.

- [x] Attempt 3's single ordered locked inspection completed with terminal admission refusal; its [sanitized receipt](evidence/3-6/20261031-current-source-live-proof-3/admission-inspection.json) retains exact commands, UTC timing, exits, aggregate shape and original hashes.
- [ ] Conditional Debug/source restore/build and the supported isolated runner remain uninvoked because idle admission failed. Current-source live proof stays open; this authorization ended without retry.
- [x] The fresh [attempt 3 evidence packet](evidence/3-6/20261031-current-source-live-proof-3/README.md) retains source identity, original/sanitized provenance, conditional uninvoked commands and every unobserved gate, preserving all prior packets.
- [x] This disposition and links were appended to this spec, the Story 3.6 record, test summary and launch-readiness record; original and invocation document prefixes remain intact.

No Aspire inventory, solution restore/build, supported runner, shared-store observation, fixture resource or fixture domain write followed. There was no retry, alternate lane, migration or foreign-resource change. Shared baseline, live journey, shared-state preservation and owned cleanup remain **unobserved**; creating no fixture resources does not qualify runner cleanup. Only the required sanitized receipt/provenance, append-only document recording, static receipt/hash/prefix/link checks and `git diff --check` followed terminal refusal. Source assertions, authorization/default-deny behavior, prior receipts and every pre-existing document byte are preserved. The historical **173 focused cases** were not rerun; no current built/running-asset, runtime-version or inherited-environment claim is made.

[Static verification](evidence/3-6/20261031-current-source-live-proof-3/verification.json) covers receipt/output hashes and shape, unchanged source and prior evidence, all four original and invocation document byte prefixes, frozen intent, local Markdown links and `git diff --check`. Historical unchecked tasks retain their original state.

Current-source local qualification and deployed authority, complete inventory/writer-delivery continuity and shared-rebuild acceptance remain unfinished. Story 3.6 and this live-proof spec remain **in-progress**; release remains **FAIL**. All changes remain **uncommitted**.
