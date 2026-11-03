# Five scheduler and secret-scan findings — 2026-11-03

The user selected five findings across three deferred-work entries. This batch
fixes RP-B1/RP-E1 and RP-B2 and requalifies all three fixture findings grouped in
the upstream reusable-content secret-scan entry. Baseline Timesheets revision:
`34a521e9daa263612663ca5b70628d2f106d3b9a`.

| Finding | Result |
| --- | --- |
| RP-B1/RP-E1: malformed scheduler HTTP responses escape safe receipts | `http.client.HTTPException` joins the existing sanitized refusal path, including failures during open and body read. |
| RP-B2: scheduler reads may use environment proxies or redirects | The fixed loopback range request uses `owned_http_open`, retaining its 20-second timeout, POST body and JSON validation. Redirects refuse and their response handles close. |
| `CatalogBackendHttpTests.cs:51` reusable credential finding | Current source has the inert placeholder; the unchanged current-source scanner passes. |
| `CatalogRefusalDiagnosticsTests.cs:16` reusable credential finding | Current source has inert placeholders; the unchanged current-source scanner passes. |
| `CatalogTransportIsolationTests.cs:106` reusable credential finding | Current source has the inert placeholder; the unchanged current-source scanner passes. |

## Scheduler checks

Baseline fixture suite: **234 passed**. Before the production fix, the two new
regressions fail with eight assertion failures and six errors across their
subtests. The raw exception escape is reproduced for `BadStatusLine`,
`IncompleteRead` and `LineTooLong`; `RemoteDisconnected` already enters the
existing `OSError` path. Its assertions also require the protected opener.
The [red output](../evidence/five-scheduler-and-secret-findings-20261103/scheduler-red.txt)
contains synthetic `PRIVATE` markers, never service data.
This diagnostic has no contemporaneous command/source manifest; it is supporting
failure output. The final green receipt binds the checked runner and test files.

Focused checks pass for eight injected protocol failures, successful direct
scheduler reading with poisoned proxy variables, five refused redirect statuses
(301/302/303/307/308), and HTTP error response closure. Tests assert the exact
original endpoint, method, range body, timeout and unchanged failure receipt
shape, including the absence of raw details from rendered tracebacks. No proxy
or redirect destination receives a request. Redis uses a transport double;
HTTP servers listen only on test-owned ephemeral loopback ports.
Review added real malformed-status and truncated-body responses through the
protected opener, exact receipt-key checks and response-context exit assertions.

An intermediate full run exposed stale mocks for the previous HTTP opener and
a test response-context mismatch. Both were corrected before final validation;
that run is not passing evidence.

Initial complete fixture suite: **237 passed**, zero failures/errors/skips, in
69.256 test seconds (69.514 command seconds). This validates the final production
fix and pre-review test assertions. Its result and source hashes are recorded in
[the initial command receipt](../evidence/five-scheduler-and-secret-findings-20261103/initial-fixture-suite.json)
and its adjacent stdout/stderr. The command is:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py
```

After review added assertions and real malformed-wire cases, the same full
command exits **1**: 237 tests, 110 failure records and 17 error records.
`LocalRuntimeAdmission` reports `Local runtime admission is busy or unsafe; no
resources started`; existing fixture checks then fail because setup stopped at
that guard. The separate admission probe still refuses. No foreign holder was
changed, and no current post-review full-suite pass is claimed. The
[contended run](../evidence/five-scheduler-and-secret-findings-20261103/post-review-contended-fixture-suite.json)
retains complete output and the final source hashes.

The final affected scheduler lane passes **3/3** (0.215 test seconds), including
all eight injected protocol cases, two real malformed-wire cases, five redirects,
proxy bypass and response cleanup. Its [focused command receipt](../evidence/five-scheduler-and-secret-findings-20261103/focused.json)
records the exact method arguments, final source hashes and CPython 3.14.4.

## Current-source secret-scan gate

EventStore revision: `0e5ecebc13e8d11afe6d46b6e6de5c877c58acc7`.
SDK: `10.0.403`. From `references/Hexalith.EventStore`, the following commands
pass with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`:

```bash
dotnet restore tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests -method '*SecretsProtectionTests.TrackedReusableContent_DoesNotContainUsableSecrets' -noColor -xml /tmp/timesheets-deferred-scan-20261103/secret-scan.xml
```

Restore and build exit zero, with **zero warnings/errors**. The exact test passes
**1/1**, zero failures/skips/errors, in 12.031 test seconds. The runner warns that
`-xml` is deprecated in favor of `-result-xml`; it successfully writes the retained
XML. The test assembly has SHA-256
`1e3738f22d03fa1fa269d952dc0412b90e3634a6dab9cedf10492e77a8677ab4`
before and after execution. EventStore, its scanner inputs and four build
dependency repositories remain clean and unchanged. No scanner exemption or
dependency edit was needed: the repairs were already in current source.

[Scanner receipts](../evidence/five-scheduler-and-secret-findings-20261103/secret-scan/verification.json)
retain commands, timestamps, full revisions, source/assembly hashes, XML and
the three exact fixture checks. Their original relative `SHA256SUMS` remains
valid from that directory.
An additive [literal check](../evidence/five-scheduler-and-secret-findings-20261103/fixture-placeholder-verification.json)
pins `__HEXALITH_TEST_SECRET__` in BackendHttp and RefusalDiagnostics and
`<username>`/`<password>` in TransportIsolation against the same source hashes.

## Review disposition

All nine blind-review findings were triaged. Six verification improvements were
applied: actual HTTP parser failures, response-context cleanup checks, exact
receipt fields, Python interpreter identity, red-evidence provenance limits and
explicit safe-placeholder literals. No production correctness issue remains
within the five agreed findings.

Two pre-existing follow-ups remain in the ledger. A parent reproduction confirms
that both the baseline and current scheduler parser raise `RecursionError`
without safe observation metadata for 100000 nested arrays; no shared service
was read. The existing unbounded JSON body read also lacks a byte ceiling and a
total duration budget; unacceptable deployed resource use was not demonstrated.
These are separate future hardening work, not passing runtime qualifications.

## Limits

This is local tooling and current-source scanner evidence. No shared Redis or
scheduler was read; no AppHost, Dapr, Docker or deployed workload was started.
No complete Timesheets .NET suite or live-runtime acceptance is claimed by these
focused checks. The post-review full Python rerun remains admission-blocked;
the earlier passing full run and final focused evidence retain their distinct
scope. Existing deployment, authority, preservation, native dashboard
and Story 3.6 gates remain open. Launch remains **FAIL**.
