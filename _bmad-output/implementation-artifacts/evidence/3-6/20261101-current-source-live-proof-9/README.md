# Story 3.6 current-source live-proof attempt 9 — 2026-11-01

Disposition: **terminal runner shared-baseline observation failure; live proof remains in-progress**. The single ordered availability inspection and the exact prescribed Debug/source restore and build passed. The supported isolated runner ran exactly once with `--public-port 18080`. Its mandatory admission passed. Its first read-only shared-service baseline observation then failed with **`TimeoutError`** in phase **`redis_database`**, database **0**. The runner exited **1** without starting fixture infrastructure or an AppHost. Story 3.6 and the live-proof spec stay **in-progress**; release stays **FAIL**.

This authorization is separate from every earlier attempt. The inspected Timesheets HEAD is `f39e2a8944ed648fdccea2fff70698815bb988ba`; the frozen spec baseline remains `c4393bbcaad70ba12901c0d71c05fd7c9a9a4c36`. Root and all 13 root-declared submodules were clean when the pre-invocation snapshot was recorded at **2026-11-01T18:05:28.713451+00:00**; the only later root change before admission was the appended attempt-9 authorization in the [live-proof spec](../../../spec-3-6-obtain-current-source-isolated-live-magic-link-proof.md).

## Authorization and public-port selection

[Attempt 8](../20261101-current-source-live-proof-8/README.md) ended when its runner refused: `Required port 8080 is occupied; no AppHost started`. The attempt-9 authorization records a read-only `ss`/`ps` observation at about 18:00 UTC. It attributed 8080 to a foreign AppHost orchestrator process that `aspire ps` does not list, and recorded that 18080, 8081, 8082 and 3501 were free. That observation preceded this invocation and was not repeated here; no foreign process identity is retained in this packet. The foreign listener was not stopped or changed.

The user selected the runner's README-documented `--public-port 18080` option. The runner argv differs from attempt 8 in exactly two places: this user-selected public port and the required fresh per-attempt `--log-directory`. The runner validates the port, excludes it from private allocations and passes it to the AppHost (`Timesheets:LocalPublicPort`) and every public journey request. Internal, gateway and sidecar ports, runner checks and assertions are unchanged. The [command-capture harness](execution-command.py.txt) differs from attempt 8's [retained harness](../20261101-current-source-live-proof-8/execution-command.py.txt) only in that runner command string.

## Ordered availability inspection

The [unchanged retained harness](admission-inspection.py.txt) was copied byte-for-byte from the [2026-10-30 packet](../20261030-current-source-live-proof/admission-inspection.py.txt) and invoked exactly once from the Timesheets root:

```text
python3 /tmp/claude-1000/-home-administrator-projects-hexalith-timesheets/5b63ed99-c23e-48ca-89fc-d122f354b51c/scratchpad/attempt9-private/admission-inspection.py
```

The [aggregate-only receipt](admission-inspection.json) records **2026-11-01T18:08:19.365326+00:00 → 2026-11-01T18:08:19.819649+00:00**, exit **0**, **0.454289s**. The canonical `LocalRuntimeAdmission` lock was acquired at **2026-11-01T18:08:19.365575+00:00** and released at **2026-11-01T18:08:19.819642+00:00**, before restore or runner execution. `dapr list --output json` (**18:08:19.365583 → 18:08:19.527955**, exit **0**) and then `aspire ps --non-interactive --format Json` (**18:08:19.528287 → 18:08:19.819346**, exit **0**) both returned successful empty JSON arrays. Original inventory streams remain private; only lengths, hashes and aggregate shape are retained.

## Exact conditional commands

The command-capture harness ran each command below once, sequentially, from the Timesheets root, with only the shown explicit environment override. It retained original streams privately and recorded their SHA-256 hashes; inherited environment values were not captured.

```text
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --public-port 18080 --log-directory /tmp/claude-1000/-home-administrator-projects-hexalith-timesheets/5b63ed99-c23e-48ca-89fc-d122f354b51c/scratchpad/attempt9-runner
```

| Command | Start UTC | End UTC | Exit | Duration |
|---|---|---|---:|---:|
| [restore](restore.json) | 2026-11-01T18:08:23.005690+00:00 | 2026-11-01T18:08:27.436676+00:00 | 0 | 4.430972s |
| [build](build.json) | 2026-11-01T18:08:30.371852+00:00 | 2026-11-01T18:09:07.717193+00:00 | 0 | 37.345896s |
| [runner](runner.json) | 2026-11-01T18:09:14.678146+00:00 | 2026-11-01T18:09:35.395963+00:00 | 1 | 20.718872s |

Durations are monotonic-clock measurements, so they can differ from the difference between their UTC endpoints by up to about 1 ms.

[Restore stdout](restore.stdout.sanitized.txt), [restore stderr](restore.stderr.sanitized.txt), [build stdout](build.stdout.sanitized.txt), [build stderr](build.stderr.sanitized.txt), [runner stdout](runner.stdout.sanitized.txt) and [runner stderr](runner.stderr.sanitized.txt) retain diagnostics with only the canonical repository-root path replaced by `<repository>`. The build reports **0 warnings and 0 errors**. All three prescribed outer commands ran; none remains conditionally uninvoked. Later runner stages remained uninvoked after the shared baseline failure.

## Runner outcome and qualification limits

The supported runner retained its own lock, inventory, sidecar, port, isolation, provenance, preservation and cleanup logic unchanged. Its [version logs](runner/01-aspire---version.log.txt) ([Dapr](runner/02-dapr---version.log.txt), [Docker](runner/03-docker-version.log.txt)), retained byte-for-byte with an added `.txt` suffix because `.gitignore` ignores `*.log`, observed [Aspire CLI 13.6.4, Dapr CLI 1.18.2 with reported runtime 1.18.7, and Docker server 29.8.2](runner/versions.json). Its Dapr and Aspire [aggregate inventories](runner/04-dapr-list.aggregate.json) ([Aspire](runner/05-aspire-ps.aggregate.json)) were successful empty arrays. Its [admission receipt](runner/admission.json) records **admitted: true**, with no resources, infrastructure or AppHost start. Admission therefore also found no Dapr sidecar outside the empty inventory and found ports 18080, 8081, 8082 and 3501 bindable on the IPv4 wildcard and, where the host supports IPv6, the IPv6 wildcard.

The runner then began its first read-only shared-service baseline observation. Its [failure receipt](runner/shared-observation-failure.json) records **2026-11-01T18:09:15.355289+00:00 → 2026-11-01T18:09:35.375751+00:00** (**20.020463s**), `completed: false`, phase **`redis_database`**, database **0**, exception category **`TimeoutError`**. The runner printed `Shared-service observation failed during redis_database; preservation is unproved`. The runner sets phase `redis_database` only after its connection and `INFO server` identity read succeed. The phase covers `SELECT 0` and one atomic Lua `KEYS`/`DUMP`/`PTTL`/`TIME` script over every DB0 key; the reader's socket timeout is 20 seconds. The failure type does not identify the individual operation, its cause, or any writer. No response, key, value or exception text was retained. No complete snapshot pair or second observation exists.

The 2026-10-28 [preservation investigation](../../../tests/shared-state-preservation-investigation.md) recorded DB0 at about 829k keys and two earlier `TimeoutError`s from this reader: its first audit and the guarded attempt, which also failed in phase `redis_database` on database 0. Attempt 9 did not measure DB0, and no writer or cause is attributed to any of these failures.

The atomic read-only script blocks other shared-Redis clients while it runs. Redis keeps running a script after its client disconnects, so any delay may have lasted beyond 20.02 seconds. This possible impact on foreign clients was **not observed**; [qualification](qualification.json) records it as `foreign_client_availability_impact: "possible; unobserved"`, while `foreign_resource_changes: false` covers state changes only.

Forward prerequisite: another attempt with the unchanged reader needs an idle or smaller shared DB0, or a separately authorized bounded-observation change. This run authorizes neither.

The [runner finalizer receipt](runner/cleanup.json) reports complete, zero removed containers and no created components. This is **no-resource finalization**, not qualified owned cleanup. No disposable fixture resource, AppHost start, live journey, domain write, restart or rebuild occurred, so the runner retained no shared-state preservation comparison. Shared baseline **failed**; live journey, shared-state preservation and owned cleanup remain **unobserved and unqualified**. Creating no fixture does not establish cleanup success.

[Qualification](qualification.json) records the observed outcome of all **23 gates** from attempt 8's gate list: runner required-port admission **passed**, shared-state baseline **failed**, and **21 gates unobserved**, including deployed authority, complete deployed inventory/writer-delivery continuity and deployed shared-rebuild acceptance. It also records no retries, fallback lanes or repairs, and no local-proof qualification. Individual runner subcommand UTC intervals are not emitted by the supported runner; only the outer interval and the shared-observation receipt timing are retained. Current fixture-running asset and inherited-environment identity remain unobserved. The historical **173 focused cases** were not rerun. Because the runner failed, the conditional `ArchitectureTests -class '*LaunchReadinessTests'` run was not invoked.

## Source and preservation provenance

[Source provenance](source-provenance.json) records canonical root HEAD `f39e2a8944ed648fdccea2fff70698815bb988ba`, all 13 root-declared submodule HEADs, branches, statuses, matching root gitlinks and authored-file manifest digests, plus original and post-authorization document-prefix hashes. Builds HEAD `a346bfeae5f441ed4a6293a211a2efdc468774b8` (`fix(packaging): avoid duplicate Works MSBuild package versions`) is the local `origin/main` tip; no fetch was performed. It contains the committed NU1506 repair. Its clean `Props/Directory.Packages.props` is **32748 bytes**, SHA-256 `e0aea6a906982bcbc04057840ed0f615cf43b48903e2e3fb4bcb7137face39c5`, equal to its HEAD blob. This differs from attempt 8's uncommitted copy (`810cb04f400bc6601160c66a3bad2ac968d7cf8f17d027ea81b43b94d5ddcc6a`) because of later Builds commits. The Builds tools documentation and durable regression equal attempt 8's retained copies. Unchanged committed sources are identified by HEAD blobs and not copied.

The Builds and Works checkouts were not modified. [Preservation baseline](preservation-baseline.json.gz) binds all **6,928** pre-invocation root files, every root-declared submodule's authored-file manifest (including **11,922 Works** and **3,205 Builds** entries) and both document-prefix sets. [Report provenance](report-provenance.json) binds every retained report or stream to its private original, records exact harness invocations and lists withheld inventory streams by hash. Runner logs were already sanitized by the supported runner; inventory log bodies remain private behind aggregate-only receipts.

## Static verification and remaining acceptance

Only required sanitized evidence/provenance, append-only recording and prescribed static receipt/hash/prefix/link/source/status/packet checks plus `git diff --check` followed the terminal failure. [Static verification](verification.json) and its [retained harness](verification.py.txt) check original receipt/output hashes and aggregate shape, sanitized report provenance, unchanged source and all earlier receipts, both document-prefix sets, frozen intent, local links, that Git ignores no packet file or local link target, source/status identities, before/after equality of every root-declared submodule authored-file manifest and the complete [packet SHA-256 manifest](SHA256SUMS). It accepts only two later review appends as prefix-preserving: the spec's `## Review Triage Log — attempt 9 (2026-11-01)` section and four entries at the end of `deferred-work.md`, checked against its HEAD bytes. No tests, builds or runtime probes ran after the terminal result.

Source assertions, selected-index/effective-value, packed-lineage, replay/race/rebuild/restart, duplicate-id, privacy, recording-delay and server authorization/default-deny behavior remain unchanged. No store was cleared, migration run, dependency/submodule updated, or foreign resource stopped or changed. No AppHost, ServiceDefaults or topology change was made.

Current-source local qualification remains missing. Deployed authority, complete inventory/writer-delivery continuity and shared-rebuild acceptance remain unfinished. Story 3.6 and the live-proof spec remain **in-progress**; release remains **FAIL**. Everything remains **uncommitted**.
