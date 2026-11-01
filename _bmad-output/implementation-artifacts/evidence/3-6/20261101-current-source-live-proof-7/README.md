# Story 3.6 current-source live-proof attempt 7 — 2026-11-01

Disposition: **terminal restore failure; live proof remains in-progress**. Idle availability passed, then the single prescribed restore failed with NU1506. Story 3.6 stays **in-progress** and release stays **FAIL**.

The single ordered availability inspection ran **2026-11-01T12:48:14.313768+00:00 → 2026-11-01T12:48:24.894417+00:00**, exit **0**, **10.5806s**, at Timesheets `4181f242bc879a2c547a5a7390692f8c7dfde6b9`. The canonical `LocalRuntimeAdmission` lock was acquired at **2026-11-01T12:48:14.314019+00:00** and released at **2026-11-01T12:48:24.894412+00:00**. `dapr list --output json` and then `aspire ps --non-interactive --format Json` both exited **0** with successful empty JSON-array inventories. The exact prescribed Debug/source restore ran **2026-11-01T12:48:52.740783+00:00 → 2026-11-01T12:48:55.054727+00:00**, exit **1**, **2.31394s**. It stopped at **NU1506** in `Hexalith.Works.Contracts`: duplicate `PackageVersion` entries for `Microsoft.Build` **18.9.6**, `Microsoft.Build.Framework` **18.9.6** and `Microsoft.Build.Locator` **1.11.2**.

The [attempt 7 authorization](../../../spec-3-6-obtain-current-source-isolated-live-magic-link-proof.md#new-authorized-attempt--2026-11-01-attempt-7) is separate from all earlier refusals. Current inspected HEAD is `4181f242bc879a2c547a5a7390692f8c7dfde6b9`; the frozen spec baseline remains `c4393bbcaad70ba12901c0d71c05fd7c9a9a4c36`.

The [unchanged retained ordered harness](admission-inspection.py.txt) was copied byte-for-byte and invoked exactly once from the repository root:

```text
python3 /tmp/timesheets-current-source-live-proof-20261101-attempt7-hragbqob/admission-inspection.py
```

The [aggregate-only admission receipt](admission-inspection.json) records these ordered runtime inventory commands:

- `dapr list --output json`: **2026-11-01T12:48:14.314027+00:00 → 2026-11-01T12:48:14.476233+00:00**, exit **0**, **0.162224s**; stdout **2 bytes**, SHA-256 `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945`; stderr **0 bytes**, SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.
- `aspire ps --non-interactive --format Json`: **2026-11-01T12:48:14.476543+00:00 → 2026-11-01T12:48:24.894113+00:00**, exit **0**, **10.417591s**; stdout **3 bytes**, SHA-256 `37517e5f3dc66819f61f5a7bb8ace1921282415f10551d2defa5c3eb0985b570`; stderr **0 bytes**, SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.

Original inventory streams remain private; no inventory fields, foreign rows, identities or endpoints are retained.

The following exact prescribed command ran once after the inspection lock was released:

```text
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
```

The [restore receipt](restore.json), [sanitized stdout](restore.stdout.sanitized.txt) and [sanitized stderr](restore.stderr.sanitized.txt) retain its terminal failure. Original stdout was **569 bytes**, SHA-256 `d26f8b9fc1f791dd33a7870c02ac9ca49646b6dbbeba937df139a3517d7fa9c8`; original stderr was **1046 bytes**, SHA-256 `b9c7c8c1869de208594b9d4b62855624184bf55076e4c209f1b0414f869e14db`. Only the canonical repository-root path was replaced with `<repository>` in retained restore streams. The [capture harness](execution-command.py.txt) executed the exact command and recorded original stream hashes without printing those streams.

These conditional commands were **not invoked**:

```text
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-current-source-live-proof-20261101-attempt7-hragbqob-runner
```

[Qualification](qualification.json) explicitly records the observed restore failure, uninvoked commands and every unobserved gate. No solution build, supported runner, shared-store observation, fixture resource or fixture domain write followed. There was no retry, fallback, alternate lane, migration, dependency edit or foreign-resource change. Shared baseline, live journey, shared-state preservation and owned cleanup remain **unobserved**; creating no fixture does not qualify runner cleanup. Only required sanitized recording, append-only document updates and prescribed static receipt/hash/prefix/link/source/status/packet checks plus `git diff --check` followed terminal restore failure. Source assertions, authorization/default-deny behavior, all prior receipts and every pre-existing document byte are preserved. The historical **173 focused cases** were not rerun. Restore stderr reports SDK **10.0.403**; this is a restore-tool observation only. No current built/running-asset, Dapr/Aspire runtime-version or inherited-environment claim is made.

[Report provenance](report-provenance.json) records original-to-retained hashes, sanitization and privately withheld inventory streams. [Source provenance](source-provenance.json) records canonical root/root-declared submodule identities, manifest digests, selected source hashes and all four original/invocation document-prefix hashes. It identifies inspected source only.

[Static verification](verification.json) covers receipt/output hashes and aggregate shape, sanitized report provenance, unchanged source and prior evidence, document prefixes, frozen intent, local Markdown links, source/status identities and `git diff --check`. [SHA256SUMS](SHA256SUMS) covers every packet file except itself.

Current-source local qualification and deployed authority, complete inventory/writer-delivery continuity and shared-rebuild acceptance remain unfinished. Story 3.6 and this live-proof spec remain **in-progress**; release remains **FAIL**. All changes remain **uncommitted**.
