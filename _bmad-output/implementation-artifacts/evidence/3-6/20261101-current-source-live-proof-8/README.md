# Story 3.6 current-source live-proof attempt 8 — 2026-11-01

Disposition: **terminal runner admission refusal; live proof remains in-progress**. The single ordered availability inspection and the exact prescribed Debug/source restore and build passed. The supported isolated runner ran exactly once and exited **1** because **required port 8080 is occupied**. It started no AppHost or fixture infrastructure. Story 3.6 and this live-proof spec stay **in-progress**; release stays **FAIL**.

This authorization is separate from every earlier attempt and the [NU1506 repair](../20261101-nu1506-repair/README.md). The inspected Timesheets HEAD is `8b70017e13fa6f6d5af688b1cc40f7f41b24096a`; the frozen spec baseline remains `c4393bbcaad70ba12901c0d71c05fd7c9a9a4c36`. The existing dirty worktree, including the uncommitted Builds repair, is part of the source identity.

## Ordered availability inspection

The [unchanged retained harness](admission-inspection.py.txt) was copied byte-for-byte from the 2026-10-30 packet and invoked exactly once from the Timesheets root:

```text
python3 /tmp/timesheets-current-source-live-proof-20261101-attempt8-j9f27__e/admission-inspection.py
```

The [aggregate-only receipt](admission-inspection.json) records **2026-11-01T16:53:40.663106+00:00 → 2026-11-01T16:53:41.191104+00:00**, exit **0**, **0.527964s**. The canonical `LocalRuntimeAdmission` lock was acquired at **2026-11-01T16:53:40.663244+00:00** and released at **2026-11-01T16:53:41.191099+00:00**, before restore or runner execution. `dapr list --output json` and then `aspire ps --non-interactive --format Json` both exited **0** with successful empty JSON arrays. Original inventory streams remain private; only lengths, hashes and aggregate shape are retained.

## Exact conditional commands

The [command-capture harness](execution-command.py.txt) ran each command below once, sequentially, with only the shown explicit environment override. It retained original streams privately and recorded their SHA-256 hashes; inherited environment values were not captured.

```text
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-current-source-live-proof-20261101-attempt8-j9f27__e-runner
```

| Command | Start UTC | End UTC | Exit | Duration |
|---|---|---|---:|---:|
| [restore](restore.json) | 2026-11-01T16:53:46.571845+00:00 | 2026-11-01T16:53:49.634923+00:00 | 0 | 3.063062s |
| [build](build.json) | 2026-11-01T16:53:54.802221+00:00 | 2026-11-01T16:54:05.788290+00:00 | 0 | 10.987938s |
| [runner](runner.json) | 2026-11-01T16:54:08.663093+00:00 | 2026-11-01T16:54:09.288911+00:00 | 1 | 0.625816s |

[Restore stdout](restore.stdout.sanitized.txt), [restore stderr](restore.stderr.sanitized.txt), [build stdout](build.stdout.sanitized.txt), [build stderr](build.stderr.sanitized.txt), [runner stdout](runner.stdout.sanitized.txt) and [runner stderr](runner.stderr.sanitized.txt) retain diagnostics with only the canonical repository-root path replaced by `<repository>`. Build reports **0 warnings and 0 errors**. All three prescribed outer commands ran; none remains conditionally uninvoked. Later runner phases remained uninvoked after mandatory admission refused.

## Runner refusal and qualification limits

The supported runner retained its own lock, inventory, sidecar, port, isolation, provenance, preservation and cleanup logic unchanged. Its [admission receipt](runner/admission.json) records **admitted: false**, **resources_started: false**, **infrastructure_started: false**, **apphost_start_attempted: false**, with reason `Required port 8080 is occupied; no AppHost started`. Its Dapr and Aspire [aggregate inventories](runner/04-dapr-list.aggregate.json) ([Aspire](runner/05-aspire-ps.aggregate.json)) were also successful empty arrays. Individual runner-command UTC intervals are not emitted by the supported runner; the outer runner interval above is retained without inventing subcommand timings.

The [runner finalizer receipt](runner/cleanup.json) reports complete, zero removed containers and no created components. This is **no-resource finalization**, not qualified owned cleanup. No shared-store observation, disposable fixture resource, AppHost start, live journey, domain write, restart or rebuild occurred. Shared baseline, live journey, shared-state preservation and owned cleanup remain **unobserved and unqualified**. Creating no fixture does not establish cleanup success.

[Qualification](qualification.json) explicitly records every observed gate, the terminal required-port failure, all **22 unobserved gates**, no retries/fallback lanes/repairs, and no local-proof qualification. The runner observed [Aspire CLI 13.6.4, Dapr CLI 1.18.2 with reported runtime 1.18.7, and Docker server 29.8.2](runner/versions.json). Those version-command observations do not identify a running fixture or its assets. Current fixture-running asset and inherited-environment identity remain unobserved. The historical **173 focused cases** were not rerun.

## Actual source and preservation provenance

[Source provenance](source-provenance.json) records canonical root and root-declared submodule revisions, initial statuses, authored-file manifest digests, original and post-authorization document-prefix hashes, and actual uncommitted Builds content. Builds HEAD is `f0feeb234cbc16f88c2886a6ffae7122e05dd64b`; HEAD alone does not identify the repaired catalog.

- [Repaired catalog byte copy](source/Builds/Props/Directory.Packages.props): **32747 bytes**, SHA-256 `810cb04f400bc6601160c66a3bad2ac968d7cf8f17d027ea81b43b94d5ddcc6a`; [catalog HEAD-relative patch](source/Builds/Props/Directory.Packages.props.patch).
- [Existing Builds tools documentation byte copy](source/Builds/Tools/README.md) and [its patch](source/Builds/Tools/README.md.patch).
- [Existing durable regression byte copy](source/Builds/Tools/test_works_package_versions.py) and [its new-file patch against the absent HEAD path](source/Builds/Tools/test_works_package_versions.py.patch).
- [Combined tracked HEAD-relative Builds patch](source/Builds/HEAD-relative.patch): SHA-256 `668853ed5255d5c2ac8671ef9d99f2e6632c0ef701806e42dc43b625bc99171a`. Source provenance separately binds the untracked regression and each worktree/patch hash.

These are exact source artifacts; copied Markdown retains the source checkout's original links. The Builds checkout was not modified. All **11,922 Works authored-file** manifest entries remained byte-identical before/after. [Preservation baseline](preservation-baseline.json.gz) binds all **6,892** pre-invocation root files, initialized root-declared submodule authored files, and all four original/invocation document prefixes. It includes earlier uncommitted evidence. No prior packet or pre-existing document byte was overwritten.

[Report provenance](report-provenance.json) binds originals to retained reports/streams and withheld inventory hashes, and records exact harness/capture invocations. Runner log originals were already sanitized by the supported runner; their original hashes identify those emitted log files. Inventory log bodies remain private and are replaced with aggregate-only receipts.

## Static verification and remaining acceptance

Only required sanitized evidence/provenance, append-only recording and prescribed static receipt/hash/prefix/link/source/status/packet checks plus `git diff --check` followed terminal refusal. [Static verification](verification.json) and its [retained harness](verification.py.txt) check original receipt/output hashes and aggregate shape, transformations, source and prior-evidence preservation, both document-prefix sets, frozen intent, local links, source/status identities and the complete [packet SHA-256 manifest](SHA256SUMS). No further workflow review, tests, builds or runtime probes ran after the terminal result. The [initial static-check diagnostic](static-verification-initial.json) records a checker that followed a tracked symlink omitted by the original snapshot. The final checker matches the parent’s confirmed regular-file-only manifest semantics; no baseline content, HEAD or status changed. A [packaging diagnostic](static-verification-packaging.json) records that the first full link check ran before its own verification file existed; the final harness checks those generated links after writing them.

Source assertions, selected-index/effective-value, packed-lineage, replay/race/rebuild/restart, duplicate-id, privacy, recording-delay and server authorization/default-deny behavior remain unchanged. No store was cleared, migration run, dependency/submodule updated, or foreign resource stopped or changed. No AppHost, ServiceDefaults or topology change was made.

Current-source local qualification remains missing. Deployed authority, complete inventory/writer-delivery continuity and shared-rebuild acceptance remain unfinished. Story 3.6 and this live-proof spec remain **in-progress**; release remains **FAIL**. Everything remains **uncommitted**.

**Clean-checkout provenance correction — 2026-11-02:** The historical [SHA256SUMS](SHA256SUMS) and report provenance name `runner/01-aspire---version.log`, `runner/02-dapr---version.log`, and `runner/03-docker-version.log`. Those three `.log` files existed in the original runner directory but matched the repository's `*.log` ignore rule and were absent from the Git commit. A clean checkout therefore cannot verify SHA256SUMS rows 16–18; `sha256sum -c SHA256SUMS` fails for those missing paths. This appended correction also changes the README bytes, so its historical manifest row now fails against the updated README. The original committed README blob still matches that row. The hashes and manifest remain the original receipt and are not reconstructed or replaced; the other 34 tracked rows verify unchanged.
