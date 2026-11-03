# Bounded scheduler observation — 2026-11-03

This increment closes **three local findings across two ledger entries**: lost
parser-depth refusal metadata, unbounded scheduler response collection, and the
absence of a total scheduler observation deadline. Earlier resolved scheduler
and secret-scan findings are not counted again. Baseline Timesheets revision:
`53d112c59a3827cf14e2c4565f048832de88467e`.

## Behavior and limits

The scheduler request remains the exact direct POST to
`http://127.0.0.1:2379/v3/kv/range`, with JSON body
`{"key":"AA==","range_end":"AA=="}` and the protected opener that disables
proxies and redirects. Existing complete-range validation and private
fingerprints remain authoritative for preservation comparison.

The complete response body must fit within **4,194,304 bytes (4 MiB)**.
Oversized declared lengths refuse before reading the body. Reads request at
most 65,536 bytes and retain at most the limit plus one sentinel byte before
refusing streamed overflow. Sized reads also explicitly reject a declared
length that exceeds the received bytes. Duplicate lengths, unsupported transfer
coding, and combined length/transfer framing refuse. Chunked bodies bypass the
native `HTTPResponse.read` decoder: bounded raw reads validate nonnegative
hexadecimal chunk lengths against the remaining body budget before consuming
data, enforce CRLF separators and require a complete terminal chunk/trailer
terminator. Chunk metadata/trailers have a separate conservative **65,536-byte**
aggregate ceiling. Valid bounded chunked responses, including extensions and
trailers, retain their complete-range behavior.

The **20-second total deadline** covers connection/open, headers, body, JSON
parsing, complete-range validation and fingerprinting. A synchronous Linux
`SIGALRM` context blocks SIGALRM, SIGINT and SIGTERM while installing or tearing
down its timer/handler. Temporary SIGINT/SIGTERM wrappers also defer Python
callbacks scheduled by unblocked worker threads; a main-thread signal mask alone
cannot prevent those callbacks. Cleanup is claimed before installation, interrupted
transitions are completed, the timer is disarmed, and an owned pending alarm is
drained before restoring caller handlers and the exact caller signal mask.
Deferred setup interruption and deadline callbacks refuse before scheduler I/O;
deferred cleanup interruption is delivered only after restoring caller state.
The original caller `KeyboardInterrupt` takes precedence over a cleanup timeout.
No production observer thread or process is created. A late completed operation
still refuses after its monotonic deadline. Calls outside the main thread,
platforms without the required signal APIs, blocked/pending `SIGALRM`, and an
already active real-time timer, or an unrestorable native handler refuse before
scheduler I/O. Existing caller
signal/timer state is preserved.

`RecursionError` and `MemoryError` now enter the sanitized refusal path. The
on-disk failure receipt retains exactly `started_unix_ns`, `completed_unix_ns`,
`completed`, `phase`, `database`, and `exception_category`; payload details do not
enter it or rendered observation-failure tracebacks. Caller `KeyboardInterrupt`
semantics are preserved.

These are conservative local refusal ceilings. They do not qualify supported
deployed inventory sizes, memory consumption, native runtime behavior or deployed
preservation. Story 3.6 remains unfinished and the launch verdict remains **FAIL**.

## Verification

Executed from `/home/administrator/projects/hexalith/timesheets` on CPython
3.14.4 (`/usr/bin/python3`), with no environment overrides:

```bash
python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py -k scheduler -v
python3 -m unittest discover -s scripts -p test_isolated_magic_link_dapr.py -v
git diff --check
```

| Command | Final publication verification |
| --- | --- |
| Focused scheduler regressions | Exit 0; **33/33 passed**, 3.236 test seconds. |
| Complete offline fixture suite | Exit 0; **264/264 passed**, 84.051 test seconds, no failures/errors/skips. |
| Whitespace check | Exit 0; no output. |

The [final focused command receipt](../evidence/bounded-scheduler-observation-20261103/published-focused.json)
and [final complete-suite receipt](../evidence/bounded-scheduler-observation-20261103/published-fixture-suite.json)
record exact argument vectors, timestamps, interpreter identity, source hashes,
exit codes and output hashes. Adjacent compressed logs retain the successful
command output. Checked source SHA-256 identities:

| Source | SHA-256 |
| --- | --- |
| `scripts/run_isolated_magic_link_dapr.py` | `61b3545af43c660a54d05f9a03761235887df5d5147612231cfaf818b76ae4ac` |
| `scripts/test_isolated_magic_link_dapr.py` | `d85b1940f301621763e172baf86144a91dc950cb42dad917a0305d3af43f46f2` |

The focused lane exercises empty and nonempty complete ranges, actual 4 MiB
minus-one/exact/plus-one bodies over declared, close-delimited and chunked HTTP,
declared refusal before read, the one-byte overflow sentinel, 100,000 nested
arrays, parser resource refusal, real trickled headers/body and stalls, opening
and parser/fingerprint deadlines, delayed alarm delivery, real active-body
SIGINT interruption, truncation and protocol failures, peer/response closure,
deadline-state restoration, unsupported callers, exact safe receipts and strict
preservation comparisons. Existing poisoned-proxy and redirect-destination
regressions remain passing.

Review-loop additions reproduce the actual native negative-chunk path with an
approximately 8 MiB synthetic raw transport whose guard forbids any unbounded
read. Refusal occurs before any raw body read. Nine real chunked-wire cases cover
signed and oversized lengths, malformed separators, missing terminal/trailer
terminators, malformed and oversized metadata, and successful extensions/trailers.
A real near-limit base64 value exercises JSON decoding, validation, fingerprinting
and complete before/after comparison without whitespace accounting for the size.

Four injected transition cases interrupt immediately after handler installation,
after timer arming, before disarm and before handler restoration. Another case
preserves the identical original interruption through a cleanup timeout. Four
real pending-signal cases exercise SIGINT/SIGTERM immediately after installation
and before disarm, preserving an unrelated blocked signal and caller handlers.
An owned pending alarm is consumed before handler restoration. All cases assert
zero remaining owned timer and restored caller signal state. The HTTP helper
now directly joins every owned request handler as well as the accept loop, with
bounded cleanup and a failing assertion if any owned worker survives.

Eight trace-driven worker-delivery cases exercise SIGINT/SIGTERM callbacks from
an unblocked worker during setup, cleanup and wrapper restoration, including
cleanup while an original interruption is active. These verify the original
exception identity, restored caller handlers/mask, zero timer, safe on-disk
receipt and joined worker. A ninth worker case proves a deadline callback during
protected setup refuses before scheduler I/O rather than losing the deadline.
Fixture workers cancel safely if an assertion prevents their intended release.

Initial new fixture runs exposed reuse of a closed Redis transport double and a
message-bearing synthetic interruption assertion; both were corrected before
the first-pass checks. An earlier 247-test full pass preceded the blocked alarm
and disarm-cleanup regressions. The [18-test focused receipt](../evidence/bounded-scheduler-observation-20261103/focused.json)
and [249-test full receipt](../evidence/bounded-scheduler-observation-20261103/fixture-suite.json)
remain unchanged as first-pass, pre-review evidence. Review reproduced unsafe
native chunk decoding and interruption windows in that first pass; only the
separately named final receipts above qualify the re-derived implementation.

One [intermediate full run](../evidence/bounded-scheduler-observation-20261103/intermediate-concurrent-edit-fixture-suite.json)
was invalidated by editing the source while its trace-based test was already
loaded. Source-line matching then failed and the test's failure cleanup released
an interruption worker, aborting the command with SIGINT (exit -2, no suite
summary). Its retained log is diagnostic-only and does not identify a tested
final source. Worker cancellation was corrected; the final runs above used
unchanged source bytes and passed. This intermediate failure is separate from
the first-pass evidence and the final receipts.

No environmental blocker occurred in the final runs. Redis remained a RESP transport double and
HTTP fixtures used only owned ephemeral loopback listeners. No shared service,
foreign session, dependency source or gitlink was changed. Commit/push and the
final publication identity are handled after parent review; these receipts
establish local verification only.


## Final review corrections and disposition

The second review's direct corrections protect cleanup entry inside its retry,
close a response acquired before context entry, reject native header defects,
and preserve deferred SystemExit behind an operational error while retaining
original KeyboardInterrupt priority. Added regressions cover the exact worker
cleanup-entry window, an actual alarm after response acquisition, malformed
native headers, cumulative framing at 65,535/65,536/65,537 bytes, all four custom
chunk decoder waits, and eight owned default/ignored-signal subprocess cases.

The earlier `final-focused.json` (27 tests) and `final-fixture-suite.json`
(258 tests) remain unchanged as pre-patch second-review evidence. Only the
`published-*` receipts linked above bind the final published scripts. Parent
verification checks unchanged pre/post source hashes, compressed and decompressed
log hashes, all 33/264 test records and successful summaries. Commitlint and
`git diff --cached --check` pass before publication.

Two separate follow-ups remain in the deferred-work ledger: the pre-existing
JSON parser accepts duplicate object names, which can discard earlier rows;
and persistent native signal API failures have no qualified cleanup escalation.
The latter is unverified in a supported Linux environment. Neither is counted
as closed. No deployed acceptance or launch-status change is inferred.
