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


## New authorized attempt — 2026-10-31 (attempt 4)

The current user invocation authorizes **one new attempt**, separate from all
completed refusals above. This is the active continuation; earlier authorizations
and unchecked tasks are historical. Preserve all pre-existing document bytes,
frozen intent, baseline identifier, source assertions and authorization defaults.
The current working tree is authorized for this bounded continuation. Leave all
changes uncommitted; do not stage, commit, discard, branch or update dependencies.

- [ ] Inspect availability **exactly once** under the canonical
  `LocalRuntimeAdmission` lock. Copy the retained ordered harness byte-for-byte
  from `evidence/3-6/20261030-current-source-live-proof/admission-inspection.py.txt`
  to `/tmp/timesheets-current-source-live-proof-20261031-attempt4-dsz4_11q/admission-inspection.py` and invoke
  `python3 /tmp/timesheets-current-source-live-proof-20261031-attempt4-dsz4_11q/admission-inspection.py` once from the repository root.
  Dapr inventory comes first; Aspire inventory is permitted only after successful
  empty Dapr inventory. Do not stop foreign resources. Release the inspection
  lock before the runner. If blocked, retain a fresh sanitized blocker packet
  and stop without retry, fallback, alternate lane or additional probe.
- [ ] Only if idle, execute sequentially the exact root AGENTS.md commands:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`,
  then `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`.
  Only after both pass, invoke the supported runner **exactly once**, preserving
  every admission check:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-current-source-live-proof-20261031-attempt4-dsz4_11q-runner`.
  Stop at the first refusal or failure. No runtime/build/test command has run for
  this authorization at the point this section was appended.
- [ ] Retain a fresh sanitized packet at
  `evidence/3-6/20261031-current-source-live-proof-4/`, including source identity,
  exact executed and conditional uninvoked commands, UTC timing, exits, original
  hashes and sanitized provenance. Keep foreign rows and sensitive originals
  private. Explicitly mark every unobserved gate, including shared baseline,
  live journey, shared-state preservation and owned cleanup. Success requires
  **live journey, shared-state preservation and owned cleanup all to pass**;
  no fixture creation does not establish cleanup success. Preserve prior packets.
- [ ] Append disposition and evidence links to this spec, the Story 3.6 record
  `3-6-implement-eventstore-backed-magic-link-state-loading.md`,
  `tests/3-6-test-summary.md` and `../../docs/launch-readiness.md`. Keep this spec
  and Story 3.6 **in-progress**, release **FAIL** while deployed authority,
  complete inventory/writer-delivery continuity and shared-rebuild acceptance
  remain unfinished. Preserve historical checkbox states.

The private pre-invocation snapshot is `/tmp/timesheets-current-source-live-proof-20261031-attempt4-dsz4_11q/before.json`.
Its `manifest` contains tracked root file byte counts and SHA-256 hashes; its four
`document-N.original` files preserve exact original document prefixes. The
snapshot also retains full canonical root and root-declared submodule source
identities without initializing/updating any submodules. Capture the current
invocation prefixes before execution too. Retain sanitized manifest hashes,
source identity, prefix hashes and original-to-sanitized receipt provenance in
the new packet. Follow the repository's tracked guidance and configuration before
execution. Root Story 3.6 sprint status is already in-progress; leave it intact.

Do not clear stores, run migrations, change AppHost/ServiceDefaults, topology or
dependencies, or use shared stores for fixture writes. Preserve assertions and
default-deny behavior. Historical 173 focused cases need no rerun. After terminal
refusal or failure, complete the required sanitized recording and append-only
document updates, then perform **only** prescribed static receipt/hash/prefix/link
checks and `git diff --check`: receipt/output hashes and aggregate shape, unchanged
source and prior evidence, all four original and invocation document byte
prefixes, frozen intent, local Markdown links, source/status identities, packet
hash manifest and `git diff --check`. No workflow review, tests, builds or runtime
probes follow a terminal result. Leave conditional unobserved live-proof tasks open.


## Attempt 4 disposition — 2026-10-31

The single ordered availability inspection ran **2026-10-31T06:48:08.091813+00:00 → 2026-10-31T06:48:08.444458+00:00**, exit **1**, **0.35261s**, at Timesheets `7983a715b0d15935d9c32304c35203c796d9e6a4`. The canonical `LocalRuntimeAdmission` lock was acquired at **2026-10-31T06:48:08.091930+00:00** and released at **2026-10-31T06:48:08.444449+00:00**. Its sole runtime command, `dapr list --output json`, ran **2026-10-31T06:48:08.091936+00:00 → 2026-10-31T06:48:08.444059+00:00**, exit **0**, **0.352145s**, and returned a JSON array of **eight application objects**. The required successful empty inventory was absent.

- [x] Attempt 4's single ordered locked inspection completed with terminal admission refusal; the [sanitized receipt](evidence/3-6/20261031-current-source-live-proof-4/admission-inspection.json) retains exact commands, UTC timing, exits, aggregate shape and original hashes.
- [ ] Conditional Debug/source restore/build and the supported isolated runner remain uninvoked because idle admission failed. Current-source live proof stays open; this authorization ended without retry.
- [x] The fresh [attempt 4 evidence packet](evidence/3-6/20261031-current-source-live-proof-4/README.md) retains source identity, original/sanitized provenance, conditional uninvoked commands and every unobserved gate, preserving all prior packets.
- [x] This disposition and evidence links were appended to this spec, the Story 3.6 record, test summary and launch-readiness record; all four original and invocation document prefixes remain intact.

No Aspire inventory, solution restore/build, supported runner, shared-store observation, fixture resource or fixture domain write followed. There was no retry, fallback, alternate lane, migration or foreign-resource change. Shared baseline, live journey, shared-state preservation and owned cleanup remain **unobserved**; creating no fixture resources does not qualify runner cleanup. Only required sanitized recording, append-only document updates and prescribed static receipt/hash/prefix/link/source/status/packet checks plus `git diff --check` followed terminal refusal. Source assertions, authorization/default-deny behavior, all prior receipts and every pre-existing document byte are preserved. The historical **173 focused cases** were not rerun; no current built/running-asset, runtime-version or inherited-environment claim is made.

[Static verification](evidence/3-6/20261031-current-source-live-proof-4/verification.json) covers receipt/output hashes and shape, unchanged source and prior evidence, all four original and invocation byte prefixes, frozen intent, local Markdown links, source/status identities, the packet hash manifest and `git diff --check`. Historical checkbox states and the frozen baseline identifier are preserved.

Current-source local qualification and deployed authority, complete inventory/writer-delivery continuity and shared-rebuild acceptance remain unfinished. Story 3.6 and this live-proof spec remain **in-progress**; release remains **FAIL**. All changes remain **uncommitted**.


## New authorized attempt — 2026-10-31 (attempt 5)

The current user invocation authorizes **one new attempt 5**, separate from all
earlier authorizations and refusals. This is the active continuation; every
earlier attempt and unchecked task is historical. Preserve all pre-existing
document bytes, frozen intent, baseline identifier, source assertions and
authorization defaults. The working tree is authorized for this bounded
continuation. Leave all changes uncommitted; do not stage, commit, discard,
branch, update dependencies or alter any prior receipt.

- [ ] Inspect availability **exactly once** under the canonical
  `LocalRuntimeAdmission` lock, using the unchanged ordered harness at
  `evidence/3-6/20261030-current-source-live-proof/admission-inspection.py.txt`.
  Copy it byte-for-byte to
  `/tmp/timesheets-current-source-live-proof-20261031-attempt5-ux9ub4ck/admission-inspection.py`
  and invoke `python3 /tmp/timesheets-current-source-live-proof-20261031-attempt5-ux9ub4ck/admission-inspection.py`
  once from the repository root. Dapr inventory comes first; Aspire inventory
  is permitted only after successful empty Dapr inventory. Do not stop foreign
  resources. Release the inspection lock before invoking the runner. If blocked,
  retain a fresh sanitized blocker packet and stop without retry, fallback,
  alternate lane or additional probe.
- [ ] Only if idle, restore/build matching Debug/source assets sequentially,
  using the exact root AGENTS.md commands:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`,
  then `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`.
  Only after both pass, invoke the supported runner **exactly once** with the
  fresh log directory:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-current-source-live-proof-20261031-attempt5-ux9ub4ck-runner`.
  Preserve every runner admission check. Stop at the first refusal or failure.
  No runtime/build/test command has run for this authorization when this section
  is appended.
- [ ] Retain a fresh sanitized packet at
  `evidence/3-6/20261031-current-source-live-proof-5/`, including source identity,
  exact executed and conditional uninvoked commands, UTC timing, exits, original
  hashes and sanitized provenance. Keep raw foreign rows, identities, endpoints
  and sensitive originals private. Explicitly mark **every unobserved gate**.
  Success requires **live journey, shared-state preservation and owned cleanup
  all to pass**; no fixture creation does not establish cleanup success. Preserve
  all previous receipts and packets.
- [ ] Append the disposition and evidence links to this spec, the Story 3.6
  record `3-6-implement-eventstore-backed-magic-link-state-loading.md`,
  `tests/3-6-test-summary.md` and `../../docs/launch-readiness.md`. Keep this spec
  and Story 3.6 **in-progress**, release **FAIL** while deployed authority,
  complete inventory/writer-delivery continuity and shared-rebuild acceptance
  remain unfinished. Preserve historical checkbox states.

The private pre-invocation snapshot is
`/tmp/timesheets-current-source-live-proof-20261031-attempt5-ux9ub4ck/before.json`.
Its `manifest` contains tracked root file byte counts and SHA-256 hashes; its
`documents` entries identify the four `document-N.original` files preserving exact
original document prefixes. It also retains full canonical root and root-declared
submodule source identities and status without initializing/updating submodules.
Capture the post-authorization invocation prefixes before execution too. Retain
sanitized manifest digests, source identities, prefix hashes and
original-to-sanitized report provenance in the packet. The inspected HEAD is
`cfbf34c2bedefff30cef9dc5691ffa48085554da`; preserve the older frozen frontmatter
baseline. Story 3.6 sprint status is already in-progress; leave it intact.
Follow relevant tracked guidance, configuration, runner and test contracts before
execution. Do not load skills from `references/`.

Do not clear stores, run migrations, change AppHost/ServiceDefaults, topology or
dependencies, or use shared stores for fixture writes. Preserve selected-index,
effective-value, packed-lineage, replay/race/rebuild/restart, duplicate-id, privacy,
recording-delay assertions and server authorization/default-deny behavior. The
historical 173 focused cases need no rerun. After terminal refusal or failure,
complete required sanitized receipt/provenance and append-only document recording,
then perform **only** prescribed static receipt/hash/prefix/link checks and
`git diff --check`: original receipt/output hashes and aggregate shape, unchanged
source and prior evidence, all four original and invocation document byte
prefixes, frozen intent, local Markdown links, source/status identities and the
packet hash manifest. No further workflow review, tests, builds or runtime probes
follow a terminal result. Leave conditional unobserved live-proof tasks open;
record completed tasks in the appended disposition so invocation prefixes remain
intact. This explicit terminal restriction overrides generic build workflow
review and additional verification steps.


## Attempt 5 disposition — 2026-10-31

The single ordered availability inspection ran **2026-10-31T16:08:19.149284+00:00 → 2026-10-31T16:08:19.589302+00:00**, exit **1**, **0.439979s**, at Timesheets `cfbf34c2bedefff30cef9dc5691ffa48085554da`. The canonical `LocalRuntimeAdmission` lock was acquired at **2026-10-31T16:08:19.149479+00:00** and released at **2026-10-31T16:08:19.589295+00:00**. Its sole runtime command, `dapr list --output json`, ran **2026-10-31T16:08:19.149488+00:00 → 2026-10-31T16:08:19.588918+00:00**, exit **0**, **0.439443s**, and returned a JSON array of **eight application objects**. The required successful empty inventory was absent.

- [x] Attempt 5's single ordered locked inspection completed with terminal admission refusal; the [sanitized receipt](evidence/3-6/20261031-current-source-live-proof-5/admission-inspection.json) retains exact commands, UTC timing, exits, aggregate shape and original hashes.
- [ ] Conditional Debug/source restore/build and the supported isolated runner remain uninvoked because idle admission failed. Current-source live proof stays open; this authorization ended without retry.
- [x] The fresh [attempt 5 evidence packet](evidence/3-6/20261031-current-source-live-proof-5/README.md) retains source identity, original/sanitized provenance, conditional uninvoked commands and every unobserved gate, preserving all prior packets.
- [x] This disposition and evidence links were appended to this spec, the Story 3.6 record, test summary and launch-readiness record; all four original and invocation document prefixes remain intact.

No Aspire inventory, solution restore/build, supported runner, shared-store observation, fixture resource or fixture domain write followed. There was no retry, fallback, alternate lane, migration or foreign-resource change. Shared baseline, live journey, shared-state preservation and owned cleanup remain **unobserved**; creating no fixture resources does not qualify runner cleanup. Only required sanitized recording, append-only document updates and prescribed static receipt/hash/prefix/link/source/status/packet checks plus `git diff --check` followed terminal refusal. Source assertions, authorization/default-deny behavior, all prior receipts and every pre-existing document byte are preserved. The historical **173 focused cases** were not rerun; no current built/running-asset, runtime-version or inherited-environment claim is made.

[Static verification](evidence/3-6/20261031-current-source-live-proof-5/verification.json) covers receipt/output hashes and shape, unchanged source and prior evidence, all four original and invocation byte prefixes, frozen intent, local Markdown links, source/status identities and the packet hash manifest. Historical checkbox states and the frozen baseline identifier are preserved.

Current-source local qualification and deployed authority, complete inventory/writer-delivery continuity and shared-rebuild acceptance remain unfinished. Story 3.6 and this live-proof spec remain **in-progress**; release remains **FAIL**. All changes remain **uncommitted**.


## New authorized attempt — 2026-10-31 (attempt 6)

The current user invocation authorizes **one new attempt 6**, separate from all
earlier authorizations and refusals. This is the active continuation; every
earlier attempt and unchecked task is historical. Preserve all pre-existing
document bytes, frozen intent, baseline identifier, source assertions and
authorization defaults. The working tree is authorized for this bounded
continuation. Leave all changes uncommitted; do not stage, commit, discard,
branch, update dependencies or alter any prior receipt.

- [ ] Inspect availability **exactly once** under the canonical
  `LocalRuntimeAdmission` lock, using the unchanged ordered harness at
  `evidence/3-6/20261030-current-source-live-proof/admission-inspection.py.txt`.
  Copy it byte-for-byte to
  `/tmp/timesheets-current-source-live-proof-20261031-attempt6-1u9p466q/admission-inspection.py`
  and invoke `python3 /tmp/timesheets-current-source-live-proof-20261031-attempt6-1u9p466q/admission-inspection.py`
  once from the repository root. Dapr inventory comes first; Aspire inventory
  is permitted only after successful empty Dapr inventory. Do not stop foreign
  resources. Release the inspection lock before invoking the runner. If blocked,
  retain a fresh sanitized blocker packet and stop without retry, fallback,
  alternate lane or additional probe.
- [ ] Only if idle, restore/build matching Debug/source assets sequentially,
  using the exact root AGENTS.md commands:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`,
  then `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`.
  Only after both pass, invoke the supported runner **exactly once** with the
  fresh log directory:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-current-source-live-proof-20261031-attempt6-1u9p466q-runner`.
  Preserve every runner admission check. Stop at the first refusal or failure.
  No runtime/build/test command has run for this authorization when this section
  is appended.
- [ ] Retain a fresh sanitized packet at
  `evidence/3-6/20261031-current-source-live-proof-6/`, including source identity,
  exact executed and conditional uninvoked commands, UTC timing, exits, original
  hashes and sanitized provenance. Keep raw foreign rows, identities, endpoints
  and sensitive originals private. Explicitly mark **every unobserved gate**.
  Success requires **live journey, shared-state preservation and owned cleanup
  all to pass**; no fixture creation does not establish cleanup success. Preserve
  all previous receipts and packets.
- [ ] Append the disposition and evidence links to this spec, the Story 3.6
  record `3-6-implement-eventstore-backed-magic-link-state-loading.md`,
  `tests/3-6-test-summary.md` and `../../docs/launch-readiness.md`. Keep this spec
  and Story 3.6 **in-progress**, release **FAIL** while deployed authority,
  complete inventory/writer-delivery continuity and shared-rebuild acceptance
  remain unfinished. Preserve historical checkbox states.

The private pre-invocation snapshot is
`/tmp/timesheets-current-source-live-proof-20261031-attempt6-1u9p466q/before.json`.
Its `manifest` contains tracked root file byte counts and SHA-256 hashes; its
`documents` entries identify the four `document-N.original` files preserving exact
original document prefixes. It also retains full canonical root and root-declared
submodule source identities and status without initializing/updating submodules.
Capture the post-authorization invocation prefixes before execution too. Retain
sanitized manifest digests, source identities, prefix hashes and
original-to-sanitized report provenance in the packet. The inspected HEAD is
`cfbf34c2bedefff30cef9dc5691ffa48085554da`; preserve the older frozen frontmatter
baseline. Story 3.6 sprint status is already in-progress; leave it intact.
Follow relevant tracked guidance, configuration, runner and test contracts before
execution. Do not load skills from `references/`.

Do not clear stores, run migrations, change AppHost/ServiceDefaults, topology or
dependencies, or use shared stores for fixture writes. Preserve selected-index,
effective-value, packed-lineage, replay/race/rebuild/restart, duplicate-id, privacy,
recording-delay assertions and server authorization/default-deny behavior. The
historical 173 focused cases need no rerun. After terminal refusal or failure,
complete required sanitized receipt/provenance and append-only document recording,
then perform **only** prescribed static receipt/hash/prefix/link checks and
`git diff --check`: original receipt/output hashes and aggregate shape, unchanged
source and prior evidence, all four original and invocation document byte
prefixes, frozen intent, local Markdown links, source/status identities and the
packet hash manifest. No further workflow review, tests, builds or runtime probes
follow a terminal result. Leave conditional unobserved live-proof tasks open;
record completed tasks in the appended disposition so invocation prefixes remain
intact. This explicit terminal restriction overrides generic build workflow
review and additional verification steps.



## Attempt 6 disposition — 2026-10-31

The single ordered availability inspection ran **2026-10-31T17:32:23.842549+00:00 → 2026-10-31T17:32:23.955163+00:00**, exit **1**, **0.112582s**, at Timesheets `cfbf34c2bedefff30cef9dc5691ffa48085554da`. The canonical `LocalRuntimeAdmission` lock was acquired at **2026-10-31T17:32:23.842666+00:00** and released at **2026-10-31T17:32:23.955157+00:00**. Its sole runtime command, `dapr list --output json`, ran **2026-10-31T17:32:23.842669+00:00 → 2026-10-31T17:32:23.954784+00:00**, exit **0**, **0.112136s**, and returned a JSON array of **eight application objects**. The required successful empty inventory was absent.

- [x] Attempt 6's single ordered locked inspection completed with terminal admission refusal; the [sanitized receipt](evidence/3-6/20261031-current-source-live-proof-6/admission-inspection.json) retains exact commands, UTC timing, exits, aggregate shape and original hashes.
- [ ] Conditional Debug/source restore/build and the supported isolated runner remain uninvoked because idle admission failed. Current-source live proof stays open; this authorization ended without retry.
- [x] The fresh [attempt 6 evidence packet](evidence/3-6/20261031-current-source-live-proof-6/README.md) retains source identity, original/sanitized provenance, conditional uninvoked commands and every unobserved gate, preserving all prior packets.
- [x] This disposition and evidence links were appended to this spec, the Story 3.6 record, test summary and launch-readiness record; all four original and invocation document prefixes remain intact.

No Aspire inventory, solution restore/build, supported runner, shared-store observation, fixture resource or fixture domain write followed. There was no retry, fallback, alternate lane, migration or foreign-resource change. Shared baseline, live journey, shared-state preservation and owned cleanup remain **unobserved**; creating no fixture resources does not qualify runner cleanup. Only required sanitized recording, append-only document updates and prescribed static receipt/hash/prefix/link/source/status/packet checks plus `git diff --check` followed terminal refusal. Source assertions, authorization/default-deny behavior, all prior receipts and every pre-existing document byte are preserved. The historical **173 focused cases** were not rerun; no current built/running-asset, runtime-version or inherited-environment claim is made.

[Static verification](evidence/3-6/20261031-current-source-live-proof-6/verification.json) covers receipt/output hashes and shape, unchanged source and prior evidence, all four original and invocation byte prefixes, frozen intent, local Markdown links, source/status identities and the packet hash manifest. Historical checkbox states and the frozen baseline identifier are preserved.

Current-source local qualification and deployed authority, complete inventory/writer-delivery continuity and shared-rebuild acceptance remain unfinished. Story 3.6 and this live-proof spec remain **in-progress**; release remains **FAIL**. All changes remain **uncommitted**.


## New authorized attempt — 2026-11-01 (attempt 7)

The current user invocation authorizes **one new attempt 7**, separate from all
earlier authorizations and refusals. This is the active continuation; every
earlier attempt and unchecked task is historical. Preserve every pre-existing
document byte, frozen intent, baseline identifier, prior receipt, source assertion,
user change and authorization/default-deny behavior. The working tree is authorized
for this bounded continuation. Leave changes **uncommitted**; do not stage, commit,
discard, branch, update dependencies or alter any earlier evidence.

- [ ] Inspect availability **exactly once** under the canonical
  `LocalRuntimeAdmission` lock using the unchanged retained ordered harness at
  `evidence/3-6/20261030-current-source-live-proof/admission-inspection.py.txt`.
  Copy it byte-for-byte to `/tmp/timesheets-current-source-live-proof-20261101-attempt7-hragbqob/admission-inspection.py`
  and invoke `python3 /tmp/timesheets-current-source-live-proof-20261101-attempt7-hragbqob/admission-inspection.py`
  once from the repository root. Dapr inventory comes first; Aspire inventory is
  permitted only after a successful empty Dapr inventory. Do not stop foreign
  resources. Release the inspection lock before invoking the runner. If blocked,
  retain a fresh sanitized blocker packet and stop without retry or further probe.
- [ ] Only if idle, restore/build matching Debug/source assets sequentially using
  the **exact root AGENTS.md commands**:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`,
  then `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`.
  Only after both pass, invoke the supported isolated runner **exactly once** with
  a fresh log directory:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-current-source-live-proof-20261101-attempt7-hragbqob-runner`.
  Preserve every runner admission check. Stop at the **first refusal or failure**.
  No runtime/build/test command has run for this authorization when this section
  is appended.
- [ ] Retain a fresh sanitized packet at
  `evidence/3-6/20261101-current-source-live-proof-7/`, including source identity,
  exact executed and conditional uninvoked commands, UTC timing, exits, original
  hashes and sanitized provenance. Keep original foreign rows, identities,
  endpoints and sensitive originals private. Explicitly mark **every unobserved
  gate**. Success requires **live journey, shared-state preservation and owned
  cleanup all to pass**; creating no fixture does not qualify runner cleanup.
- [ ] Append disposition and evidence links to this spec, the Story 3.6 record
  `3-6-implement-eventstore-backed-magic-link-state-loading.md`, test summary
  `tests/3-6-test-summary.md` and `../../docs/launch-readiness.md`. Keep this spec
  and Story 3.6 **in-progress** and release **FAIL** while deployed authority,
  complete inventory/writer-delivery continuity and shared-rebuild acceptance remain
  unfinished. Preserve all historical checkbox states; record completed work in
  the appended disposition so invocation prefixes remain intact.

The private pre-invocation snapshot is `/tmp/timesheets-current-source-live-proof-20261101-attempt7-hragbqob/before.json`.
Its `manifest` contains tracked root file byte counts and SHA-256 hashes; its
`documents` entries identify four `document-N.original` files retaining exact
original document prefixes. It also retains full canonical root and root-declared
submodule source identities and status without initializing/updating submodules.
Capture post-authorization invocation prefixes before execution as well. Retain
sanitized manifest digests, source identities, prefix hashes and original-to-
sanitized provenance in the packet. The inspected HEAD is
`4181f242bc879a2c547a5a7390692f8c7dfde6b9`; preserve the older frozen frontmatter baseline.
Story 3.6 sprint status is already in-progress; leave it intact. Follow relevant
tracked guidance, configuration, runner and test contracts before execution.
Do not discover or load skills from `references/`.

Do not clear stores, run migrations, change AppHost/ServiceDefaults, topology or
dependencies, or use shared stores for fixture writes. Preserve selected-index,
effective-value, packed-lineage, replay/race/rebuild/restart, duplicate-id, privacy,
recording-delay assertions and server authorization/default-deny behavior. The
historical 173 focused cases need no rerun.

After terminal refusal or failure, complete required sanitized receipt/provenance
and append-only document recording, then perform **only prescribed static
receipt/hash/prefix/link checks and `git diff --check`**: original receipt/output
hashes and aggregate shape, unchanged source and prior evidence, all four original
and invocation document byte prefixes, frozen intent, local Markdown links,
source/status identities and the packet hash manifest. **Do not run further
workflow review, tests, builds or runtime probes.** This explicit terminal
restriction overrides generic build workflow review, task completion, staging and
additional verification steps. Leave conditional unobserved live-proof tasks open.


## Attempt 7 disposition — 2026-11-01

The single ordered availability inspection ran **2026-11-01T12:48:14.313768+00:00 → 2026-11-01T12:48:24.894417+00:00**, exit **0**, **10.5806s**, at Timesheets `4181f242bc879a2c547a5a7390692f8c7dfde6b9`. The canonical `LocalRuntimeAdmission` lock was acquired at **2026-11-01T12:48:14.314019+00:00** and released at **2026-11-01T12:48:24.894412+00:00**. `dapr list --output json` and then `aspire ps --non-interactive --format Json` both exited **0** with successful empty JSON-array inventories. The exact prescribed Debug/source restore ran **2026-11-01T12:48:52.740783+00:00 → 2026-11-01T12:48:55.054727+00:00**, exit **1**, **2.31394s**. It stopped at **NU1506** in `Hexalith.Works.Contracts`: duplicate `PackageVersion` entries for `Microsoft.Build` **18.9.6**, `Microsoft.Build.Framework` **18.9.6** and `Microsoft.Build.Locator` **1.11.2**.

- [x] Attempt 7's single ordered locked availability inspection passed; its [aggregate-only receipt](evidence/3-6/20261101-current-source-live-proof-7/admission-inspection.json) retains exact argv, UTC timing, exits and original stream hashes without inventory identities.
- [x] The exact conditional [Debug/source restore](evidence/3-6/20261101-current-source-live-proof-7/restore.json) ran once and failed with NU1506. This is the terminal failure for this authorization.
- [ ] Solution build and the supported isolated runner remain uninvoked after restore failure. Current-source live proof stays open; this authorization ended without retry.
- [x] The fresh [attempt 7 evidence packet](evidence/3-6/20261101-current-source-live-proof-7/README.md) retains sanitized restore diagnostics, source identity, original/sanitized provenance, conditional uninvoked commands and every unobserved gate, preserving all earlier packets.
- [x] This disposition and links were appended to this spec, the Story 3.6 record, test summary and launch-readiness record; all four original and invocation document prefixes remain intact.

No solution build, supported runner, shared-store observation, fixture resource or fixture domain write followed. There was no retry, fallback, alternate lane, migration, dependency edit or foreign-resource change. Shared baseline, live journey, shared-state preservation and owned cleanup remain **unobserved**; creating no fixture does not qualify runner cleanup. Only required sanitized recording, append-only document updates and prescribed static receipt/hash/prefix/link/source/status/packet checks plus `git diff --check` followed terminal restore failure. Source assertions, authorization/default-deny behavior, all prior receipts and every pre-existing document byte are preserved. The historical **173 focused cases** were not rerun. Restore stderr reports SDK **10.0.403**; this is a restore-tool observation only. No current built/running-asset, Dapr/Aspire runtime-version or inherited-environment claim is made.

[Static verification](evidence/3-6/20261101-current-source-live-proof-7/verification.json) covers original receipt/output hashes and aggregate shape, sanitized report provenance, unchanged source/prior evidence, all four original and invocation byte prefixes, frozen intent, local Markdown links, source/status identities, the packet hash manifest and `git diff --check`. Historical checkbox states and the frozen baseline identifier remain preserved.

Current-source local qualification and deployed authority, complete inventory/writer-delivery continuity and shared-rebuild acceptance remain unfinished. Story 3.6 and this live-proof spec remain **in-progress**; release remains **FAIL**. All changes remain **uncommitted**.


## New authorized attempt — 2026-11-01 (attempt 8)

This user invocation authorizes **exactly one new attempt 8**, separate from all
historical attempts and the NU1506 repair. This is the active continuation; do
not replay earlier authorizations or execute their unchecked tasks. Start from
[evidence for the NU1506 repair](evidence/3-6/20261101-nu1506-repair/README.md),
this spec and [attempt 7](evidence/3-6/20261101-current-source-live-proof-7/README.md).
The existing dirty working tree is explicitly authorized for this bounded run.
Preserve all user changes, every earlier receipt, all pre-existing document bytes,
the frozen intent, frontmatter baseline, assertions and authorization defaults.
Leave everything **uncommitted**; do not stage, commit, discard, branch, update
submodules or dependencies, or change source, AppHost, ServiceDefaults or topology.

- [ ] Inspect availability **exactly once** from the Timesheets root under the
  canonical `LocalRuntimeAdmission` lock. Copy the retained ordered harness
  byte-for-byte from
  `evidence/3-6/20261030-current-source-live-proof/admission-inspection.py.txt`
  to `/tmp/timesheets-current-source-live-proof-20261101-attempt8-j9f27__e/admission-inspection.py` and invoke
  `python3 /tmp/timesheets-current-source-live-proof-20261101-attempt8-j9f27__e/admission-inspection.py`
  once. Dapr inventory comes first; Aspire inventory is permitted only after a
  successful empty Dapr inventory. Do not stop foreign resources. Release the
  inspection lock before invoking the runner. At the first refusal or failed gate,
  retain its result and stop without retries, fallback lanes or additional repairs.
- [ ] Only after successful admission, run the exact root AGENTS.md commands
  sequentially from the root, with the stated environment prefix:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`,
  then, only if restore passes,
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`.
  Only after **every prerequisite passes**, invoke the existing supported runner
  **exactly once**, retaining all its admission, isolation, provenance, privacy,
  preservation and cleanup checks:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-current-source-live-proof-20261101-attempt8-j9f27__e-runner`.
  Stop at the first failed gate. Access stores only through the prescribed harness.
  No runtime inventory, restore, build or runner has run for this authorization
  when this section is appended.
- [ ] Create a fresh sanitized packet at
  `evidence/3-6/20261101-current-source-live-proof-8/` (the actual UTC date), with
  exact executed and conditional uninvoked commands, start/end UTC timing,
  durations, exits, sanitized diagnostics, original-to-sanitized stream hashes,
  source identity and observed outcomes for **every gate**. Retain the unchanged
  admission harness and command-capture harness. Keep inventory rows, foreign
  identities, endpoints, tokens and sensitive originals private. Local proof
  requires **live journey, shared-state preservation and owned cleanup all to
  pass**. Creating no fixture does not qualify runner cleanup. Mark every
  unobserved gate explicitly and report only observed qualification.
- [ ] Include the **actual uncommitted Builds worktree content and SHA-256** in
  source provenance. Preserve the NU1506 repair in
  `references/Hexalith.Builds/Props/Directory.Packages.props`, its durable regression
  and any other existing edits. HEAD alone does not identify this source. Retain
  an exact byte copy of the repaired catalog (and existing changed Builds source
  files) in the attempt packet with worktree and HEAD-relative patch hashes and
  canonical Builds HEAD. Do not modify the Builds checkout. Keep all Works
  authored files **read-only**, recording their before/after manifest equality.
- [ ] Append the attempt-8 disposition and links to this spec,
  `3-6-implement-eventstore-backed-magic-link-state-loading.md`,
  `tests/3-6-test-summary.md` and `../../docs/launch-readiness.md`. Keep this spec
  and Story 3.6 **in-progress**, and release **FAIL** while deployed authority,
  complete inventory/writer-delivery continuity and shared-rebuild acceptance
  remain unfinished. Preserve historical checkbox states and all original and
  invocation prefixes; record completed attempt-8 work in a new disposition.

The private pre-invocation snapshot is `/tmp/timesheets-current-source-live-proof-20261101-attempt8-j9f27__e/before.json`.
Its `manifest` maps all tracked and nonignored untracked root files to byte counts
and SHA-256 values, including earlier uncommitted evidence. Its `documents` maps
all four documents to exact `document-N.original` files and hashes. Its
`submodules` retains full canonical root-declared submodule identities, statuses
and authored-file manifests without initializing or updating any submodule.
The inspected Timesheets HEAD is `8b70017e13fa6f6d5af688b1cc40f7f41b24096a`;
preserve the older frozen baseline. Capture an `invocation.json` with exact
post-authorization document prefixes before executing admission, and retain their
hashes in source provenance. The parent workflow already rendered bmad-build
exactly once and resolved this spec to implementation; do not render it again.
Story 3.6 sprint status and the parent epic are already in-progress; leave them
intact. Follow tracked guidance, configuration, runner and test contracts before
execution. Do not discover or load skills under `references/`.

Do not clear stores, run migrations or use shared stores for fixture writes.
Preserve selected-index, effective-value, packed-lineage, replay/race/rebuild/
restart, duplicate-id, privacy, recording-delay and server default-deny behavior.
The historical 173 focused cases need no rerun. After a terminal refusal or
failure, finish only required sanitized evidence/provenance and append-only
recording, then perform **only prescribed static receipt/hash/prefix/link checks
and `git diff --check`**: original receipt/output hashes and aggregate shape,
sanitized report provenance, unchanged source and every prior receipt, all four
original and invocation document byte prefixes, frozen intent, local Markdown
links, source/status identities and a complete packet SHA-256 manifest.
**No further workflow review, tests, builds or runtime probes follow a terminal
result.** This explicit task restriction overrides generic workflow review,
completion, staging and further verification instructions. Conditional unobserved
live-proof tasks remain open. No retries, fallback lanes or repairs are authorized.


## Attempt 8 disposition — 2026-11-01

The single ordered availability inspection ran **2026-11-01T16:53:40.663106+00:00 → 2026-11-01T16:53:41.191104+00:00**, exit **0**, **0.527964s**, at Timesheets `8b70017e13fa6f6d5af688b1cc40f7f41b24096a`. The canonical `LocalRuntimeAdmission` lock was acquired at **2026-11-01T16:53:40.663244+00:00** and released at **2026-11-01T16:53:41.191099+00:00**. Dapr and then Aspire inventories both returned successful empty JSON arrays. The exact prescribed Debug/source restore passed **2026-11-01T16:53:46.571845+00:00 → 2026-11-01T16:53:49.634923+00:00**, exit **0**, **3.063062s**. The exact build passed **2026-11-01T16:53:54.802221+00:00 → 2026-11-01T16:54:05.788290+00:00**, exit **0**, **10.987938s**, reporting **0 warnings and 0 errors**.

The supported isolated runner was invoked **exactly once**, **2026-11-01T16:54:08.663093+00:00 → 2026-11-01T16:54:09.288911+00:00**, exit **1**, **0.625816s**. Its mandatory admission refused: **Required port 8080 is occupied; no AppHost started**. Its own inventories were successful empty arrays; its [admission receipt](evidence/3-6/20261101-current-source-live-proof-8/runner/admission.json) records no resources, infrastructure or AppHost start. This is the terminal result; no retry, fallback lane, repair or additional runtime/build/test probe followed.

- [x] Attempt 8's single ordered locked availability inspection, exact Debug/source restore and build completed successfully; the [fresh evidence packet](evidence/3-6/20261101-current-source-live-proof-8/README.md) retains exact commands, UTC timing, durations, exits and original/sanitized stream hashes.
- [x] The supported runner ran once and retained its mandatory admission refusal; all three prescribed outer commands ran. Later runner stages remain uninvoked after refusal.
- [ ] Current-source live journey, shared-state preservation and owned cleanup remain unobserved and unqualified. The runner [finalizer receipt](evidence/3-6/20261101-current-source-live-proof-8/runner/cleanup.json) reports complete with zero created resources; no-resource finalization does not qualify owned cleanup.
- [x] [Source provenance](evidence/3-6/20261101-current-source-live-proof-8/source-provenance.json) retains canonical Builds HEAD `f0feeb234cbc16f88c2886a6ffae7122e05dd64b`, byte-exact copies of all three existing Builds edits, their worktree/HEAD-relative patch hashes, and the repaired catalog SHA-256 `810cb04f400bc6601160c66a3bad2ac968d7cf8f17d027ea81b43b94d5ddcc6a`. Builds was not modified. All **11,922 Works authored-file** manifest entries remain byte-identical.
- [x] This disposition and links were appended to all four required documents; every original and invocation document byte prefix, frozen intent, baseline identifier, prior receipt and user change remains preserved.

No shared-store observation, fixture resource, AppHost start, live journey, domain write, restart or rebuild occurred. Shared baseline, live journey, shared-state preservation and owned cleanup remain **unobserved**; creating no fixture does not qualify cleanup. [Qualification](evidence/3-6/20261101-current-source-live-proof-8/qualification.json) explicitly records the observed gates and all **22 unobserved gates**. Runner version commands observed Aspire CLI **13.6.4**, Dapr CLI **1.18.2** with reported runtime **1.18.7**, and Docker server **29.8.2**; these observations do not identify running fixture assets. Inherited environment identity remains unobserved. The historical **173 focused cases** were not rerun.

Only required sanitized recording, append-only updates and prescribed static receipt/hash/prefix/link/source/status/packet checks plus `git diff --check` followed terminal refusal. [Static verification](evidence/3-6/20261101-current-source-live-proof-8/verification.json) covers receipt/output hashes and aggregate shape, original-to-sanitized provenance, unchanged source and all earlier evidence, both prefix sets, frozen intent, local Markdown links, source/status identities and the packet SHA-256 manifest. Assertions and server authorization/default-deny behavior are unchanged. Historical checkbox states remain intact.

Current-source local qualification and deployed authority, complete inventory/writer-delivery continuity and shared-rebuild acceptance remain unfinished. Story 3.6 and this live-proof spec remain **in-progress**; release remains **FAIL**. All changes remain **uncommitted**.


## New authorized attempt — 2026-11-01 (attempt 9)

On 2026-11-01 the user explicitly authorized **exactly one new attempt 9**, and
selected the supported runner's README-documented `--public-port 18080` option.
Attempt 8's runner refused because required port 8080 was occupied. Read-only
`ss`/`ps` observation at about 18:00 UTC attributes 8080 to the `dcp` of a
foreign Hexalith.Tenants AppHost started with `aspire run` on 2026-10-30. It
is not listed by `aspire ps`; it was not and must not be stopped. A foreign
Conversations integration-test process was also running. Ports 18080, 8081,
8082 and 3501 were free. `--public-port 18080` is the **only authorized
difference** from attempt 8's runner argv. The runner validates it, excludes it
from private allocations and passes it to the AppHost (`Timesheets:LocalPublicPort`)
and every public journey request. Internal/gateway/sidecar ports, runner checks
and assertions stay unchanged. Precedent:
[Aspire 13.6.4 qualification](spec-aspire-1364-local-qualification.md) and the
[preservation investigation](tests/shared-state-preservation-investigation.md).

This is the active continuation; every earlier attempt and unchecked task is
historical. Preserve every pre-existing document byte, frozen intent, baseline
identifier, prior receipt, source assertion and authorization/default-deny
behavior. Leave changes **uncommitted**; do not stage, commit, discard, branch,
update submodules or dependencies, or change source, AppHost, ServiceDefaults or
topology. Do not discover or load skills under `references/`.

- [ ] Inspect availability **exactly once** from the Timesheets root under the
  canonical `LocalRuntimeAdmission` lock. Copy the retained ordered harness
  byte-for-byte from
  `evidence/3-6/20261030-current-source-live-proof/admission-inspection.py.txt`
  to `/tmp/claude-1000/-home-administrator-projects-hexalith-timesheets/5b63ed99-c23e-48ca-89fc-d122f354b51c/scratchpad/attempt9-private/admission-inspection.py`
  and invoke it once with `python3`. Dapr inventory comes first; Aspire inventory
  only after a successful empty Dapr inventory. Release the inspection lock before
  the runner. At the first refusal or failed gate, retain the result and stop
  without retry, fallback lane or repair.
- [ ] Only after successful admission, run sequentially from the root, each once:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false`,
  then, only if restore passes,
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false`,
  then, only if build passes, the supported runner **exactly once**:
  `DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --public-port 18080 --log-directory /tmp/claude-1000/-home-administrator-projects-hexalith-timesheets/5b63ed99-c23e-48ca-89fc-d122f354b51c/scratchpad/attempt9-runner`.
  Capture each with attempt 8's
  [command-capture harness](evidence/3-6/20261101-current-source-live-proof-8/execution-command.py.txt),
  changing only its runner command string to the one above. A passing journey can
  take several minutes; do not interrupt it. Stop at the first failed gate.
- [ ] Create a fresh sanitized packet at
  `evidence/3-6/20261101-current-source-live-proof-9/` with exact executed and
  conditional uninvoked commands, UTC timing, durations, exits, sanitized
  diagnostics, original-to-sanitized stream hashes, source identity, the
  port-selection rationale, and the observed outcome of **every gate** using
  attempt 8's [qualification gate list](evidence/3-6/20261101-current-source-live-proof-8/qualification.json).
  Keep inventory rows, foreign identities, endpoints, tokens and sensitive
  originals private. Local proof requires **live journey, shared-state preservation
  and owned cleanup all to pass**; no-resource finalization does not qualify cleanup.
  Deployed gates stay unobserved.
- [ ] Source provenance: canonical root HEAD `f39e2a8944ed648fdccea2fff70698815bb988ba`
  and all 13 root-declared submodule HEADs. All were clean at snapshot time. Builds
  HEAD `a346bfeae5f441ed4a6293a211a2efdc468774b8` (on `origin/main`) contains the
  committed NU1506 repair. Its catalog SHA-256 is now
  `e0aea6a906982bcbc04057840ed0f615cf43b48903e2e3fb4bcb7137face39c5`, which differs
  from attempt 8's uncommitted copy because of later Builds commits. Record it
  without copying unchanged committed sources. Prove all root-declared submodule
  authored-file manifests, including Works and Builds, are byte-identical
  before/after.
- [ ] Append the attempt-9 disposition and links to this spec,
  `3-6-implement-eventstore-backed-magic-link-state-loading.md`,
  `tests/3-6-test-summary.md` and `../../docs/launch-readiness.md`. Preserve
  historical checkbox states and all original and invocation prefixes. If local
  proof qualifies, say so with exact evidence. Story 3.6 stays **in-progress**
  and release **FAIL** either way, while deployed authority, complete
  inventory/writer-delivery continuity and shared-rebuild acceptance remain
  unfinished.

The private pre-invocation snapshot is `/tmp/claude-1000/-home-administrator-projects-hexalith-timesheets/5b63ed99-c23e-48ca-89fc-d122f354b51c/scratchpad/attempt9-private/before.json`
(recorded 2026-11-01T18:05:28.713451+00:00). It maps 6,928 regular tracked and
nonignored untracked root files to byte counts and SHA-256 values. It maps the
four documents to exact `document-N.original` prefixes and holds full canonical
identities, statuses and authored-file manifests for all 13 root-declared
submodules, without initializing or updating any submodule. Before admission,
capture an `invocation.json` holding the exact post-authorization document
prefixes, and retain their hashes in source provenance. Story 3.6 and Epic 3
sprint status are already in-progress; leave them unchanged.

Do not clear stores, run migrations or use shared stores for fixture writes.
Preserve selected-index, effective-value, packed-lineage, replay/race/rebuild/
restart, duplicate-id, privacy, recording-delay and server default-deny behavior.
The historical 173 focused cases need no rerun. After a terminal refusal or
failure, only finish the sanitized evidence/provenance and the append-only
recording. Then run only the static checks: receipt/output hashes and aggregate
shape, sanitized report provenance, unchanged source and prior receipts, both
document prefix sets, frozen intent, local Markdown links, source/status
identities, a complete packet SHA-256 manifest, and `git diff --check`. No
further tests, builds or runtime probes follow a terminal result.
If the runner passes, run the same static checks. Also run the existing
ArchitectureTests executable with `-class '*LaunchReadinessTests' -noColor` once,
after the document appends.


## Attempt 9 disposition — 2026-11-01

The single ordered availability inspection ran **2026-11-01T18:08:19.365326+00:00 → 2026-11-01T18:08:19.819649+00:00**, exit **0**, **0.454289s**, at Timesheets `f39e2a8944ed648fdccea2fff70698815bb988ba`. The canonical `LocalRuntimeAdmission` lock was acquired at **2026-11-01T18:08:19.365575+00:00** and released at **2026-11-01T18:08:19.819642+00:00**. Dapr and then Aspire inventories both returned successful empty JSON arrays. The exact prescribed Debug/source restore passed **2026-11-01T18:08:23.005690+00:00 → 2026-11-01T18:08:27.436676+00:00**, exit **0**, **4.430972s**. The exact build passed **2026-11-01T18:08:30.371852+00:00 → 2026-11-01T18:09:07.717193+00:00**, exit **0**, **37.345896s**, reporting **0 warnings and 0 errors**.

The supported isolated runner was invoked **exactly once**, **2026-11-01T18:09:14.678146+00:00 → 2026-11-01T18:09:35.395963+00:00**, exit **1**, **20.718872s**. Its argv differs from attempt 8 in two places: the user-selected `--public-port 18080` and the required fresh per-attempt `--log-directory`. Its mandatory admission passed: empty Dapr/Aspire inventories, no out-of-inventory Dapr sidecar, and ports 18080, 8081, 8082 and 3501 bindable on the IPv4 wildcard and, where the host supports IPv6, the IPv6 wildcard ([admission receipt](evidence/3-6/20261101-current-source-live-proof-9/runner/admission.json)). Its first read-only shared-service baseline observation then failed after **20.020463s** with **`TimeoutError`**, phase **`redis_database`**, database **0**: **Shared-service observation failed during redis_database; preservation is unproved** ([failure receipt](evidence/3-6/20261101-current-source-live-proof-9/runner/shared-observation-failure.json)). This is the terminal result; no retry, fallback lane, repair or additional runtime/build/test probe followed.

The runner sets phase `redis_database` only after its connection and `INFO server` identity read succeed; the phase covers `SELECT 0` and one atomic Lua `KEYS`/`DUMP`/`PTTL`/`TIME` script over every DB0 key. The 2026-10-28 [preservation investigation](tests/shared-state-preservation-investigation.md) recorded DB0 at about 829k keys and two earlier `TimeoutError`s from this reader (its first audit and the guarded attempt). Attempt 9 did not measure DB0, and no writer or cause is attributed. The atomic read-only script blocks other shared-Redis clients while it runs, and Redis keeps running a script after its client disconnects, so any delay may have lasted beyond 20.02 seconds. This possible impact on foreign clients was not observed. Forward prerequisite: another attempt with the unchanged reader needs an idle or smaller shared DB0, or a separately authorized bounded-observation change; this run authorizes neither.

- [x] Attempt 9's single ordered locked availability inspection, exact Debug/source restore and build completed successfully; the [fresh evidence packet](evidence/3-6/20261101-current-source-live-proof-9/README.md) retains exact commands, UTC timing, durations, exits, original/sanitized stream hashes and the public-port selection rationale.
- [x] The supported runner ran once with `--public-port 18080` and a fresh log directory; its mandatory admission passed and its first shared baseline observation failed. All three prescribed outer commands ran; later runner stages remain uninvoked.
- [ ] Current-source shared baseline failed; live journey, shared-state preservation and owned cleanup remain unobserved and unqualified. The runner [finalizer receipt](evidence/3-6/20261101-current-source-live-proof-9/runner/cleanup.json) reports complete with zero created resources; no-resource finalization does not qualify owned cleanup.
- [x] [Source provenance](evidence/3-6/20261101-current-source-live-proof-9/source-provenance.json) records root HEAD `f39e2a8944ed648fdccea2fff70698815bb988ba` and all 13 clean root-declared submodule HEADs with matching gitlinks. Builds HEAD `a346bfeae5f441ed4a6293a211a2efdc468774b8` contains the committed NU1506 repair; its catalog SHA-256 is `e0aea6a906982bcbc04057840ed0f615cf43b48903e2e3fb4bcb7137face39c5`, differing from attempt 8's uncommitted copy because of later Builds commits. Every root-declared submodule authored-file manifest, including **11,922 Works** and **3,205 Builds** entries, remains byte-identical before/after.
- [x] This disposition and links were appended to all four required documents; every original and invocation document byte prefix, frozen intent, baseline identifier, prior receipt and user change remains preserved.

No fixture infrastructure, AppHost start, live journey, domain write, restart or rebuild occurred, and no shared-state preservation comparison exists. [Qualification](evidence/3-6/20261101-current-source-live-proof-9/qualification.json) records the observed outcome of all **23 gates** from attempt 8's list: runner required-port admission passed, shared-state baseline failed and **21 gates** remain unobserved. It records `foreign_client_availability_impact` as possible and unobserved; `foreign_resource_changes` remains false for state changes only. Runner version commands observed Aspire CLI **13.6.4**, Dapr CLI **1.18.2** with reported runtime **1.18.7**, and Docker server **29.8.2**; these do not identify running fixture assets. Inherited environment identity remains unobserved. The historical **173 focused cases** were not rerun, and the conditional LaunchReadinessTests run was not invoked because the runner failed. The foreign listener on 8080 was not stopped or changed.

Only required sanitized recording, append-only updates and prescribed static receipt/hash/prefix/link/source/status/packet checks plus `git diff --check` followed the terminal failure. [Static verification](evidence/3-6/20261101-current-source-live-proof-9/verification.json) covers receipt/output hashes and aggregate shape, original-to-sanitized provenance, unchanged source and all earlier evidence, both prefix sets, frozen intent, local Markdown links, that Git ignores no packet file or link target, source/status identities, submodule manifest equality and the packet SHA-256 manifest. Assertions and server authorization/default-deny behavior are unchanged. Historical checkbox states remain intact.

Current-source local qualification and deployed authority, complete inventory/writer-delivery continuity and shared-rebuild acceptance remain unfinished. Story 3.6 and this live-proof spec remain **in-progress**; release remains **FAIL**. All changes remain **uncommitted**.


## Review Triage Log — attempt 9 (2026-11-01)

The three review layers read the attempt-9 change set against HEAD
`f39e2a8944ed648fdccea2fff70698815bb988ba`: four document appends and the new
packet. The frozen baseline diff spans ten earlier, separately recorded commits.
Verification Gap reported no gaps. This section is appended instead of filling
the original empty `## Review Triage Log` heading, so that the pinned document
prefixes stay intact. The frontmatter `in-review` status was transient.
B = Blind Hunter, E = Edge Case Hunter.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | medium | patch G1 | The 2026-10-28 investigation records DB0 at about 829k keys and two earlier `TimeoutError`s from this reader. The disposition cites only one and omits both facts. |
| B2 | medium | patch G2 | The atomic Lua read blocks other Redis clients while it runs, and Redis keeps running it after the client disconnects. This possible impact on foreign clients is unrecorded. |
| B3 | low | patch G1 | Phase `redis_database` is set only after connection and `INFO server` succeed. The README names the SELECT/script scope, but the dispositions do not. |
| B4 | low | patch G5 | `--log-directory` also differs from attempt 8, so "only argv difference" is literally false. The authorization section is spec text and is left unchanged. |
| B5 | low | reject | The fix would edit this build's spec. The packet's statement that no foreign identity is retained is accurate. A port-choice receipt cannot be retained without disclosing foreign identity, and the runner's own admission confirms the ports. |
| B6 | low | reject | Private originals stay outside the repository by design, as in attempts 7–8. The packet's own hashes can be re-verified. A packet-only verifier is more than a direct correction, and that audit is rare. The home path is already in 311 committed files. |
| B7 | maybe-false | defer | `LaunchReadinessTests` were not run after the document appends because the spec stops all tests after a terminal result. Whether pinned text breaks is unknown; running that class would settle it. |
| B8 | false | reject | `PYTHONOPTIMIZE` was unset, and `verification.json` holds computed results, so the checks ran. The `-O` weakness is in the pre-existing harness only. |
| B9 | low | patch G6 | The receipt shows Dapr finished (`.527955`) before Aspire started (`.528287`), but the check is weaker than the claim. The authorization links resolve. The durations come from a monotonic clock, which is not stated. |
| B10 | false | reject | `admission.json` reports `admitted: true` only after `_preflight` reaches `port_check()` without raising, so the bindability inference holds. |
| B11 | low | patch G7 | The three non-spec documents say "this live-proof spec" and use a heading without context. This pattern was copied from attempt 8. |
| B12 | medium | patch G1 + defer | No forward prerequisite is recorded; G1 patches this. The stale "Current Assessment" summary is older than this change and its fix would edit earlier bytes, so it is deferred. |
| B13 | false | reject | The project convention is `[x]` for an action performed and `[ ]` for an unqualified gate. Authorization checkboxes stay as they are to keep the invocation prefixes intact. |
| E1 | medium | patch G3 + defer | `.gitignore:121` `*.log` ignores runner `01`–`03` logs that the README links and SHA256SUMS lists. Attempt 8's committed SHA256SUMS already lists the same uncommitted logs; that is deferred. |
| E2 | low | patch G4 | Verification checks that files exist, not that git will track them. That is how E1 passed. |
| E3 | false | reject | No ignored file under `_bmad-output` is newer than the pre-invocation snapshot. |
| E4 | low | reject | Retained raw log bytes cannot be trimmed without breaking hash provenance. Committed evidence text already follows this practice. |
| E5 | false | reject | Same as B8. |
| E6 | low | reject | The pre-existing harness guard did not fail: `runner.json` shows a single runner invocation. Fixing it would add guards. |
| E7 | low | reject | The pre-existing harness has no timeout, but all commands returned. Fixing it would add guards. |
| E8 | false | reject | The README claims only literal repository-root substitution, and the home path is already committed widely. |
| E9 | false | reject | The current top-level line 108 still reads FAIL, so no stale PASS was produced. |
| E10 | false | reject | Builds `origin/main` resolves to HEAD `a346bfeae5f441ed4a6293a211a2efdc468774b8`. |
| E11 | low | patch G8 | The runner skips the IPv6 bind where IPv6 is unsupported, so "bindable" should name IPv4 and say IPv6 where supported. |
| E12 | medium | defer | This is pre-existing runner design: one atomic `KEYS`/`DUMP` over the whole shared DB0, with a 20-second client timeout. It has failed three times and may block foreign clients. Changing it needs separate authorization. |
