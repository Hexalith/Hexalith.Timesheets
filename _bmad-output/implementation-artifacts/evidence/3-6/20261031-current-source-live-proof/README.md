# Story 3.6 current-source live-proof admission — 2026-10-31

Disposition: **blocked; proof remains in-progress**. The one newly authorized
availability inspection found **eight Dapr application objects** and ended before
Aspire inventory, restore/build, the supported runner, shared-store observation,
or fixture writes. Story 3.6 remains **in-progress** and release remains **FAIL**.

The [continuation spec](../../../spec-3-6-obtain-current-source-isolated-live-magic-link-proof.md)
authorized one new attempt on 2026-10-31. Both 2026-10-30 evidence packets and all
original document bytes were preserved. The canonical `LocalRuntimeAdmission`
lock was acquired at **2026-10-31T05:06:39.210640+00:00** and released at
**2026-10-31T05:06:39.320797+00:00**. The inspection ran once,
**2026-10-31T05:06:39.210529+00:00 → 2026-10-31T05:06:39.320801+00:00**,
exit **1**, **0.110244s**.

The exact harness invocation was:

```text
python3 /tmp/timesheets-current-source-live-proof-inspection-20261031-qb59_xa5/inspect_admission.py
```

Its only runtime command was:

```text
dapr list --output json
```

It ran **2026-10-31T05:06:39.210643+00:00 → 2026-10-31T05:06:39.320462+00:00**,
exit **0**, **0.109844s**, returning a JSON array of
**eight objects**. Stdout: **3,451 bytes**, SHA-256
`69da8a78cdbb842539445567c8f12e61395eff0ff3458640a81dbb66641b5df8`. Stderr: **0 bytes**, SHA-256
`e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.
Foreign application rows, fields, endpoints and process identities are withheld;
private original retention and byte-level transformations are recorded in
[report provenance](report-provenance.json).

The [admission receipt](admission-inspection.json) retains exact argv, UTC timing,
exits, output hashes, ordered-command disposition and aggregate shape. The exact
[inspection harness](admission-inspection.py.txt) is a byte-for-byte copy of the
retained 2026-10-30 harness, executed from the private temporary directory. It
checks Dapr first and only permits Aspire inventory after successful empty Dapr
inventory. No further inventory or availability probe ran.

The conditional runner command was **not invoked**, because idle admission failed:

```text
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 scripts/run_isolated_magic_link_dapr.py --log-directory /tmp/timesheets-current-source-live-proof-20261031
```

[Qualification](qualification.json) records the uninvoked restore/build commands
and every unobserved gate: shared baseline, live journey, shared-state preservation
and owned cleanup. No fixture resource was created; no runner cleanup qualification
is claimed. No retry, alternate lane, migration, shared-store write or foreign
resource stop occurred. No historical focused case was rerun. Only the prescribed
static receipt/hash/prefix/link checks and `git diff --check` followed refusal.

[Source provenance](source-provenance.json) binds inspected Timesheets source
`c4393bbcaad70ba12901c0d71c05fd7c9a9a4c36`, root-declared submodule commits,
selected source hashes, the initial tracked diff and original source/document/
evidence manifests. This identifies inspected source, without identifying a built
or running asset or claiming current runtime versions or inherited environment.
The prior **173 focused cases** retain their historical spec-21 meaning.

[Static verification](verification.json) records original-output hash/shape,
source and prior-evidence hashes, append-only document prefixes, unchanged frozen
intent, local Markdown links and `git diff --check`. [SHA256SUMS](SHA256SUMS)
covers the packet files. Current-source local qualification and deployed authority,
complete inventory/writer-delivery continuity and shared-rebuild acceptance remain
unfinished. Story 3.6 and this spec remain **in-progress**; release remains **FAIL**.
