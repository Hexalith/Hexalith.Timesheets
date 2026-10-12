# Local magic-link Dapr proof

## Round-23 latest-tree preflight (2026-10-12)

After the round-22 `/project/v2` failure, the V7 bootstrap scan was changed
from 256 serial legacy-shard reads to bounded groups of 16 concurrent reads.
Focused source-reference tests passed 3/3 and the narrow Python selection and
effective-value suite passed 8/8. This is a code-level diagnosis, not a
passing local Dapr result. The latest preflight found an unrelated Folders
Aspire AppHost with an `eventstore` Dapr ID on app port 8080. The Timesheets
journey requires exactly one `eventstore` ID on app port 8082. Folders then
briefly disappeared and restarted within ten seconds of two empty Dapr/Aspire
preflight reads; no stable isolation window existed. The Timesheets AppHost
was not started. Folders was left untouched and Redis was not cleared. The
round-22 failed attempts remain the latest isolated local result; deployed
inventory, authority, and certified live shared rebuild remain unproven.
Story 3.6 and release remain `FAIL`.

## Round-21 isolated local attempt (2026-10-12)

The current V7 numeric-tree fixture passed source-reference build and Python
unit checks before launch. `dapr list` found no Dapr instances. The Timesheets
AppHost was started with `TIMESHEETS_LIVE_DAPR_FIXTURE=1`; `aspire wait`
reported `eventstore` and `timesheets` healthy, with exactly one Dapr app ID
for each. The opt-in command below failed **0/1 in 38.545 seconds** when
the capability-index `/project/v2` projection returned
`delivery_state_unavailable`. Catalog and non-fixture denial checks had
passed. A same-AppHost rerun after adding bounded retries for that
projection admission failed **0/1 in 77.228 seconds** at the same point:

```bash
TIMESHEETS_LIVE_DAPR=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests' -noColor
```

EventStore logs contained named-projection retries for the current and
historical `live-dapr-*` tenants, HTTP 500/503 responses, and actor rate
limiting; the Timesheets host logged a ten-second timeout. These observations
do not establish one root cause. The fixture did not reach V7 candidate or
effective-value readback, so this tree has no passing local Dapr proof.
`aspire stop --apphost
src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj
--non-interactive` stopped only the Timesheets AppHost. Dapr listed no
instances and `aspire ps --format Json --non-interactive` returned `[]`
afterward. Redis was not cleared. Deployed inventory, authority, and a
certified live shared rebuild remain open; Story 3.6 and release remain
`FAIL`.

## Round-20 latest-tree preflight (2026-10-12)

The latest-tree journey adds an independent comparison of selected effective
Time Entry values to the stored recording and adjustment events, including
duration, billable, and approval state. Its narrow Python suite passed 3/3
and `py_compile` passed. The opt-in Dapr journey has not run on this tree:
process preflight found an unrelated Folders Aspire AppHost with a Dapr
`eventstore` app ID. The required one-`eventstore`, one-`timesheets`
isolation could not be established. The Folders runtime was left untouched;
the Timesheets AppHost was not started and Redis was not cleared. The
round-19 result below remains historical until an isolated latest-tree run
passes. Deployed persistence and Story 3.6 gates remain `FAIL`.

## Round-19 isolated local run (2026-10-12)

The latest-tree opt-in journey passed **1/1 in 106.313 seconds** after the
fixture's bounded retries for recording and actor reads were extended across
EventStore's configured 60-second anonymous tenant rate-limit window.
`dapr list -o json` and `aspire ps --non-interactive --format Json` were `[]`
before launch. The Timesheets AppHost started with
`TIMESHEETS_LIVE_DAPR_FIXTURE=1` and `aspire start --no-build --apphost
src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj
--non-interactive --format Json`; `aspire wait` reported both projects healthy.
The Dapr list contained exactly one `eventstore` ID on app port 8082 and one
`timesheets` ID on app port 8081. The script checked uniqueness throughout.
The passing command was:

```bash
TIMESHEETS_LIVE_DAPR=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests' -noColor
```

It verified persisted confirm and adjust, the one-winner race, EventStore
restart, and selected readbacks. On a valid GET denial, the fixture now
reports only runtime uniqueness, index storage version and page count,
candidate category, capability state category, and owner presence category;
it does not print a token, hash, owner ID, or comment. The successful run did
not trigger this denial diagnostic. `aspire stop --apphost
src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj
--non-interactive` stopped only this AppHost. Dapr and Aspire both returned
`[]` afterward. The Redis projection store was not cleared or modified by
cleanup.

Three earlier round-19 attempts remain failure evidence. The first failed
**0/1 in 68.606 seconds**: catalog delivery, non-fixture denial, Time Entry
rebuild, and the confirm record/issue/index/GET/use/race passed, then the
adjust recording stayed HTTP 403 after three attempts at 0, 1, and 2
seconds. EventStore warnings showed anonymous actor HTTP 429 and pending
named-projection retries, but the recording 403 alone did not prove that
cause. The second failed **0/1 in 40.834 seconds** before recording: the
negative non-fixture owner read returned 503 after a valid 403 issuance
denial. A same-AppHost diagnostic rerun failed **0/1 in 19.560 seconds**;
the negative owner read still returned 503 after five one-second retries.
EventStore's recent `StreamsController` warnings for
these reads contained Dapr actor 429 exceptions, establishing the inner
rate-limit cause of that 503. Its configuration allows 1,000 anonymous
requests per 60-second tenant window and 100 per one-second consumer window,
with no queue; the rejection handler sets `Retry-After` from the denying
lease. The public Timesheets 403 does not expose that inner header. The
fixture now bounds recording and actor-read retries at offsets 0, 1, 6,
21, and 66 seconds. These waits do not alter authorization or index trust.

The round-18 first run passed **1/1 in 118.195 seconds**, but a
user-requested repeat failed **0/1** after selected index readback when
confirm GET remained denied. Its AppHost was stopped and Dapr/Aspire were
empty afterward. The round-19 pass does not establish deployed inventory,
authority, or a certified live shared rebuild. Story 3.6 and release remain
`FAIL`.

## Round-18 isolated local run (2026-10-12)

The numeric page-address worktree passed the opt-in journey **1/1 in
118.195 seconds**. `dapr list` and `aspire ps --format Json` were empty
before launch. The Timesheets AppHost started with
`TIMESHEETS_LIVE_DAPR_FIXTURE=1` and `aspire start --no-build --apphost
src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj
--non-interactive --format Json`; `aspire wait` reported both resources
healthy, and Dapr listed exactly one `eventstore` and one `timesheets` ID.
The passing command was:

```bash
TIMESHEETS_LIVE_DAPR=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests' -noColor
```

The fixture checked persisted confirm/adjust, race, EventStore restart,
and selected readbacks. After stopping only this AppHost, `dapr list`
returned no instances and `aspire ps --format Json` returned `[]`.
A subsequent warning-as-error source-reference solution build succeeded
without changing the AppHost, Timesheets host, or Projections binary hashes
used by this run. Deployed inventory, authority, and certified live shared
rebuild remain open; Story 3.6 and release remain `FAIL`.

## Round-17 isolated local run (2026-10-12)

The round-17 worktree passed the opt-in journey **1/1 in 127.469 seconds**.
`dapr list` and `aspire ps --format Json` were empty before starting the
Timesheets AppHost with `TIMESHEETS_LIVE_DAPR_FIXTURE=1` and
`aspire start --no-build --apphost
src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj
--non-interactive --format Json`. `aspire wait` reported both `eventstore`
and `timesheets` healthy, and Dapr listed exactly one app ID for each. The
passing command was:

```bash
TIMESHEETS_LIVE_DAPR=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests' -noColor
```

The fixture checked persisted confirm and adjust, a one-winner use race,
EventStore restart, and exact selected model readbacks. `aspire stop
--apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj
--non-interactive` stopped only this AppHost; Dapr and Aspire were empty
afterward. This does not certify deployed inventory, authority, or a live
shared rebuild. Story 3.6 and release remain `FAIL`.

## Round-16 isolated local run (2026-10-12)

The current implementation passed the opt-in journey **1/1 in 116.727
seconds** after the fixture was updated for the selected global token-index
shard. The first attempt failed **0/1 in 46.255 seconds** after a stored
issuance because it read the retired single `v1` index key; that failure did
not reach confirm/adjust. `python3 -m py_compile` passed for the updated
fixture. The passing rerun checked persisted confirm and adjust, a one-winner
race, EventStore restart, exact selected Time Entry/capability rows, and the
selected index shard before and after restart.

Before each start, `dapr list` and `aspire ps --format Json` were empty.
The Timesheets AppHost was started with `TIMESHEETS_LIVE_DAPR_FIXTURE=1`,
`aspire start --no-build --apphost
src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj
--non-interactive --format Json`, and both resources passed `aspire wait`.
Each run saw exactly one `eventstore` and one `timesheets` Dapr app ID. The
passing command was:

```bash
TIMESHEETS_LIVE_DAPR=1 tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests' -noColor
```

After `aspire stop --apphost
src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj
--non-interactive`, both Dapr and Aspire lists were empty. This local run
does not certify deployed source coverage, authority binding, or a live
shared rebuild. Story 3.6 and release remain open.

## Historical round-15 isolated local run (2026-10-12)

The round-15 implementation tree passed the opt-in journey **1/1 in 112.376
seconds**. Before launch, `dapr list` and `aspire ps --format Json` were empty.
The Timesheets AppHost was started with `TIMESHEETS_LIVE_DAPR_FIXTURE=1` and
`aspire start --no-build --apphost
src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj
--non-interactive --format Json`. `aspire wait eventstore` and `aspire wait
timesheets` reported healthy, and `dapr list` showed exactly one of each app
ID. The test command was:

```bash
TIMESHEETS_LIVE_DAPR=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests' -noColor
```

The fixture checked persisted issue, confirm, and adjust, a one-winner use
race, EventStore restart, and exact selected Time Entry and capability
readback. After `aspire stop --apphost
src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj
--non-interactive`, `dapr list` and `aspire ps --format Json` were empty. This
run does not certify deployed source coverage, authority binding, or a live
shared rebuild. Story 3.6 and release remain open.

## Historical round-14 isolated local run (2026-10-12)

The current round-14 worktree passed the opt-in journey **1/1 in 67.664
seconds** after a bounded transport retry was added to the new exact
post-restart lineage read. An initial run failed **0/1 in 117.700 seconds**
at that read with `A local HTTP or Dapr request could not complete`; before
the failure it had passed catalog delivery, non-fixture denial, persisted
confirm and adjust, the race, and the EventStore restart. The retry changes
only this read-only stream probe. The passing rerun verified those steps and
the exact selected Time Entry event and payload lineage after restart.

Both runs used the Timesheets AppHost started with
`TIMESHEETS_LIVE_DAPR_FIXTURE=1` and this test command:

```bash
TIMESHEETS_LIVE_DAPR=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests'
```

`aspire wait` reported both resources healthy;
strict pre/post `dapr list` checks showed one `eventstore` and one
`timesheets` app ID. After stopping this AppHost with `aspire stop --apphost`,
both Dapr and Aspire reported no running instances. This local proof does not
certify deployed source coverage, authority binding, or a certified live
shared rebuild. Story 3.6 and release remain open.

## Historical round-13 isolated local run (2026-10-12)

The round-13 worktree passed the opt-in journey **1/1 in 125.174
seconds** with this command:

```bash
TIMESHEETS_LIVE_DAPR=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests'
```

The Timesheets AppHost was started with `TIMESHEETS_LIVE_DAPR_FIXTURE=1` and
`aspire start --no-build --apphost
src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj
--non-interactive`. Both `aspire wait eventstore` and `aspire wait timesheets`
reported healthy. `dapr list` showed exactly one `eventstore` and one
`timesheets` app ID before and after the journey. After stopping this AppHost
through `aspire stop --apphost`, both Dapr and Aspire reported no running
instances. The fixture checked persisted confirm and adjust, the one-winner
race, EventStore restart, and selected Time Entry and capability readbacks.
This local run does not certify deployed source coverage, authority binding,
or a certified live shared rebuild. Story 3.6 and release remain open.

## Historical round-11 isolated local run (2026-10-11)

The opt-in journey passed **1/1 in 114.866 seconds** on the round-11 worktree:

```bash
TIMESHEETS_LIVE_DAPR=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests'
```

The operator checked `dapr list` before and after the journey: each check
showed exactly one `eventstore` and one `timesheets` app ID. After stopping
only this Timesheets AppHost with `aspire stop`, both Dapr and Aspire resource
lists were empty. The fixture verified persisted confirm and adjust batches,
one-winner confirm race and replay denial, actor restart, and exact selected
Time Entry and capability readbacks. It uses persisted positive, increasing
global positions with contiguous owner sequences and rejects a duplicate
Dapr ID at preflight and after restart. Read-only stream, command-status, and
selected-model probes have bounded HTTP 429/500/503 retries. The five-second
default submission polling budget, single-write behavior, and opaque external
denial remain in force.

Two earlier latest-tree retries failed before these corrections. In the first,
both confirm-race calls returned HTTP `[403,403]` even though EventStore had
stored `StoredTimeEntryRecorded`, a capability use, a confirmation, and a
rejected competing commit. The submission service awaited the POST response;
a lost or delayed response after durable commit exhausted its deadline and
hid the successful winner. It now checks exact command status concurrently
with the POST and requires matching stored-event readback before returning
HTTP 202. In the next isolated retry, both calls again returned `[403,403]`,
but the target stream contained only `StoredTimeEntryRecorded`: workload
stream-read POSTs received HTTP 429 and timed out before any use command was
submitted. EventStore's rate limiter had put signed DaprInternal workload
requests into the anonymous partition because it looked only for human JWT
claims. It now partitions scheme-checked, signed workload requests by their
verified bound tenant and workload identity, retaining finite rate limits.
The first and second failures are historical results on earlier revisions of
the round-11 worktree; its passing run is recorded above.

This is local Development-topology evidence, not deployed acceptance. The
shared-rebuild Begin producer still has no independently installed production
coverage-authority receipt issuer, certified virgin-store/all-writer proof,
complete legacy actor-key census, or certified live shared-rebuild run. The
deployed authority-mode identity issuer also lacks the required exact
tenant/domain/actor binding; deployed delivery/readback and retirement of old
capability terminal writers remain open. Story 3.6 and release stay in progress.

## Historical live attempt on an earlier round-9 checkpoint (2026-10-11)

That earlier checkpoint's opt-in journey has
**no clean pass**. After a zero-warning AppHost
build, `aspire start --no-build` with `TIMESHEETS_LIVE_DAPR_FIXTURE=1` and both
`aspire wait` checks succeeded. Immediate `dapr list` preflight showed exactly
one `eventstore` and one `timesheets` app ID. The journey passed persisted
catalog delivery/readback and non-fixture denial, then failed **0/1 in 83.705
seconds** at capability issuance HTTP 403. Its diagnostic capability-stream
read returned 404. Timesheets logs show an EventStore workload stream-read
POST exhausting two 10-second attempt timeouts and its total request timeout;
EventStore logged rate limiting and named-projection
`delivery_state_unavailable` retries. Those observations do not establish why
the read stalled or why issuance was denied. There was one `eventstore` ID
during the run. Only the Timesheets AppHost was stopped through `aspire stop`;
an unrelated topology later registered another `eventstore` ID and was left
untouched. A preliminary fixture invocation stopped immediately because the
AppHost lacked the opt-in run-mode setting; it made no journey request. The
local live gate was open at that checkpoint.

## Historical round-8 attempts (2026-10-11)

The preserved round-8 re-derived worktree had **no clean opt-in pass**. The
Timesheets AppHost was built and started with the checked-in topology;
`aspire wait` found `timesheets` and `eventstore` healthy, and `dapr list`
showed one of each app ID before the run and after the third attempt. Three
isolated attempts passed persisted catalog delivery and non-fixture denial.
They failed at capability issuance HTTP 403 (77.897 seconds), at a confirm
race where both requests returned HTTP 403 (31.062 seconds), and at adjust
issuance HTTP 403 after confirm, its race, and composed Time Entry rebuild
readback had passed (31.637 seconds). EventStore logged rate-limit and
named-projection retry activity; the opaque denials' cause is still
unproven. The Timesheets AppHost was stopped with explicit `aspire stop`,
and `dapr list` was empty afterward. The fixture retains exact compact
Time Entry lineage checks, five bounded post-restart read-only retries for
HTTP 429/500/503, and safe event-count/name diagnostics at issuance/race
failure. None of these runs reached the post-restart checks.

An earlier 2026-10-11 opt-in attempt started with one healthy `eventstore` and
one healthy `timesheets` Dapr app. It persisted and read back the catalog,
then exited 1 after 22.378 seconds at the combined non-fixture issuance and
missing-owner read assertion. An unrelated ChatBot AppHost added a second
`eventstore` ID during the run. The assertion did not print its two individual
statuses, so the exact failing leg of that attempt is unknown. A diagnostic
while both IDs were present sent the same signed missing-owner request eight
times through Timesheets' sidecar and observed alternating HTTP 500 and 404;
the Timesheets-owned EventStore sidecar returned eight HTTP 404 responses.
A separate signed non-fixture issuance returned HTTP 403. The fixture now
prints only those two statuses on assertion failure. A subsequent opt-in
attempt exited at strict preflight because two `eventstore` IDs existed. A
final clean-preflight run reached the persisted catalog, non-fixture denial,
both composed Time Entry rebuild readbacks, confirm race and adjust journey.
It exited 1 after 122.714 seconds when a post-restart owner stream read
returned HTTP 401; ChatBot had again introduced a second `eventstore` ID
during the run. The Timesheets AppHost was stopped after each attempt; the
unrelated AppHost was left untouched. No clean end-to-end acceptance for
that checkpoint is claimed.

The 2026-10-11 opt-in journey passed against an earlier revision of the
checked-in local Aspire composition. That revision had no passing
opt-in run. Two isolated attempts reached stored-event delivery: the first
stopped at an EventStore command-status HTTP 500; the second passed the confirm
race, then received three opaque 403 responses on the valid adjust POST.
The next run stopped at strict preflight when an unrelated ChatBot AppHost
restarted with a second `eventstore` Dapr ID. A later run passed catalog
delivery and a stored recording, then its first capability stream read returned
HTTP 429 after ChatBot restarted with a second `eventstore` ID. The Timesheets
AppHost was stopped; the unrelated AppHost was left untouched. The round-8
fixture also exercised the mapped Time Entry rebuild endpoint and checked exact
persisted evidence; the final attempt reached those checks as described above.
Story 3.6 and launch remain open for production gates.

## Materialized read-model extension (historical passing local run)

The Story 3.6 fixture now delivers the actual stored Time Entry record and
use/effect history to `time-entry-evidence`, and the stored issuance and use
to `magic-link-confirmation-capabilities`. It reads the aggregate-owned
evidence key and the capability's selected shared generation through Dapr,
checks Fresh evidence lineage and the joined Used display, and verifies that
no token or token hash appears in the capability value. On 2026-10-11, the
isolated opt-in `MagicLinkLiveDaprTests` command below passed **1/1 in
160.146 seconds** with exactly one `eventstore` and one `timesheets` Dapr app.
It covered both new model readbacks for confirm and adjust, the one-winner
race, EventStore restart, replay denial, and a fresh post-restart link. The
Timesheets AppHost was stopped after the run. Two earlier attempts had passed
the new readbacks but failed later at a transient opaque HTTP 403; the final
passing run is historical evidence for its tested revision. Production gates remain open.

## Run the persisted journey

From the Timesheets repository root, with Docker and Dapr available, use the
checked-in Aspire composition. Its EventStore and Timesheets processes must be
the only local applications with those Dapr IDs; the fixture checks this before
it writes isolated data.

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build src/Hexalith.Timesheets/Hexalith.Timesheets.csproj --no-restore -m:1 -v:q
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --no-restore -m:1 -v:q
TIMESHEETS_LIVE_DAPR_FIXTURE=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home aspire run --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive
```

Leave `aspire run` in its own terminal. In a second terminal, inspect both app
IDs and run the opt-in test:

```bash
dapr list
TIMESHEETS_LIVE_DAPR=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests'
```

The fixture reads the per-run Development signing key, app-channel token and
Timesheets Dapr endpoint from the unique Timesheets process environment on
Linux, in memory. It signs resource-bound assertions for the real EventStore
gateway and SDK projection routes. Do not capture the environment, assertion,
raw link or token hash. A random `live-dapr-` tenant, actor, activity, entry and
capability isolate each run. The explicit AppHost setting passes
`Timesheets__LiveDaprFixture__Enabled=true` only in local run mode; the host
installs the Development-only claim mapping and access guard only with that
setting. The guard permits only the journey's operations and tenant prefix;
non-fixture tenants still fail closed. These
are test identities, not production authorization.

The fixture sends a synthetic `ActivityTypeCreated` through the authenticated
SDK `/project/v2` route, verifies `Completed`, then reads the Fresh catalog via
the Dapr read-model component. It records a Time Entry and issues each link via
the real Timesheets HTTP routes. Those routes require EventStore `Completed`
status and exact stored-event readback before returning `202`; the fixture
also queries each record, issue, and use command status directly and matches
`Completed`, event count, owner, and committed sequence to its stored batch.
It independently reads the stored record and issue payloads. It delivers each
actual stored issue event through `/project/v2`, verifies the token-hash index
via Dapr, then checks valid GET, confirm and adjust POST, the stored use/effect
payloads, a one-winner confirm race, equal opaque race/replay denials and a
recording retry with no extra event. A signed non-fixture process assertion is
denied before any capability owner event. The stored adjustment fields match
the submitted values. The race loser may append one EventStore
rejection marker; it never appends another use or effect. The fixture runs
`aspire resource eventstore restart` and `aspire wait eventstore`, then checks
both replay denials and unchanged owner streams. It rereads the catalog and
candidate index and uses a fresh link after restart so a loader outage cannot
look like successful replay denial. A recording retry keeps its source-derived
command identity. The fixture permits at most three attempts for recording,
valid GET, and valid POST when the host returns an opaque `403` during transient
local gateway unavailability; any other status or continued denial fails the
test. Read-only stream and command-status probes retry bounded HTTP 500/503
responses and also HTTP 429 after EventStore restart; completion still requires exact stored-event and
command-status matches.

The direct inspection command is
`python3 scripts/local_aspire_magic_link_journey.py`. It performs signed
EventStore stream reads and separate Dapr read-model reads before and after
the actor restart without printing protected payloads. Its tenant ID in the
final line identifies the isolated run; rerunning the command creates a new
tenant. The test leaves isolated EventStore and read-model data in the local
containers. Do not flush shared Redis or delete actor state as cleanup. Stop
only this Aspire run:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home aspire stop --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive
```

**Observed 2026-10-11:** both changed projects built with zero warnings or
errors. `python3 scripts/local_aspire_magic_link_journey.py` exited 0 and
reported persisted catalog, confirm race, adjust, and restart/replay success.
The strengthened opted-in xUnit executable exited 0: one passed, zero failed
or skipped in 73.929 seconds after the direct command-status and bounded
readiness checks were added. The fixture fails if a required app, credential, store,
projection route, stored payload, or replay guarantee disappears. This is local
Dapr/EventStore persistence evidence only; production identity issuance,
deployed history inventory, deployed read-model wiring and deployed
acceptance remain open.

The exact opted-in command shown above first passed 1/1 in 71.494 seconds
after the authorization review fixes. After direct command-status assertions
were added, its next run exited 1 with valid confirm GET returning opaque
`403` after persisted record, issue, and index checks (83.307 seconds).
`python3 scripts/local_aspire_magic_link_journey.py` then exited 0 against
the same Aspire session. A further run of the same opted-in command exited 1
when the adjust recording returned opaque `403` (32.549 seconds). Sanitized
Timesheets warning telemetry showed outbound 10-second timeouts and 429
retries. After adding bounded retries only for the stable-identity recording
and read-only valid GET, the final opted-in command exited 0 with one passed,
zero failed/skipped in 73.929 seconds. These failures did not produce an
acceptance claim, and their raw assertions and token material were not captured.

The six default Timesheets executables passed individually: 1,584 passed and
five skipped across 1,589 cases. The solution build with
`-p:UseHexalithProjectReferences=true -warnaserror` still fails in
`references/Hexalith.EventStore.Contracts` with MSB3243/CS1704 for duplicate
`Hexalith.Commons.UniqueIds` versions `2.30.1.0` and `1.0.0.0`; the same
failure was recorded in the connected-topology increment before this journey.

The post-review default commands, run from the repository root without the
live opt-in variable, returned:

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

After the current build gate was marked `FAIL` in `docs/launch-readiness.md`,
`tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests'`
first exited 1 with one stale `PASS` expectation (12/13 passed). The
expectation was updated to the observed gate. Then
`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build tests/Hexalith.Timesheets.ArchitectureTests/Hexalith.Timesheets.ArchitectureTests.csproj --no-restore -m:1 -v:q`
exited 0 with zero warnings/errors, the same focused command exited 0 with
13/13 passed, and the full ArchitectureTests command in the table exited 0
with 56/56 passed.

`DOTNET_CLI_HOME=/tmp/dotnet-cli-home aspire stop --apphost src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --non-interactive`
exited 0 after the final live run; this session's AppHost stopped cleanly.
`dapr list` then exited 0 with `No Dapr instances found.`

## Preflight on 2026-10-11

**Blocked before the acceptance journey.** The checked-in EventStore AppHost can run
EventStore, Tenants, and its sample domain, but it does not attach Timesheets as a
Dapr domain module or admit `timesheets` as a trusted effect submitter. The
Timesheets AppHost explicitly has no EventStore resource or Dapr sidecar. The
only running EventStore AppHost observed during preflight belonged to a separate
EventStore integration test, so its resources were left untouched. No
`timesheets` Dapr application was registered. Reaching a real Timesheets
record/issue/use path would require platform-owned topology work that this
spec forbids adding here. No live test or fixture was added, no test state was
written, and no local or deployed acceptance is claimed.

| Check | Exact command and observed result |
| --- | --- |
| SDK and container daemon | `dotnet --version` → `10.0.401` (the checked-in `global.json` pin); `docker info --format '{{.ServerVersion}}'` → `29.8.2`. |
| Dapr applications | `dapr list` exited 0 and listed `eventstore`, `eventstore-admin`, `tenants`, `tenants-api`, `sample`, `sample-api`, `sample-blazor-ui`, and `eventstore-admin-ui`; it listed no `timesheets` application. |
| Running AppHost ownership | `aspire ps --non-interactive` listed `Hexalith.EventStore.AppHost.csproj` as running at PID 8448. `readlink /proc/8448/cwd` returned `/home/administrator/projects/hexalith/eventstore/tests/Hexalith.EventStore.IntegrationTests/bin/Debug/net10.0`; its command line ran `SignalRDaprProjectionDispatchProofTests`. The process was externally owned and was not stopped or restarted. |
| Repository AppHost attachment | `rg -n 'AddEventStoreDomainModule|WithEventStoreTrustedEffectSubmitter|timesheets' references/Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Program.cs` found sample/Tenants attachment and only a comment explaining trusted submitter setup; it found no Timesheets attachment or `timesheets` allow-list call. `src/Hexalith.Timesheets.AppHost/Program.cs` states that EventStore topology is deferred and attaches no Dapr sidecar. |
| Persisted read-model access | `references/Hexalith.EventStore/src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml` scopes `statestore` to `eventstore`, `eventstore-admin`, and `tenants` and explicitly directs new domain services to a separate scoped component. Timesheets' token index and Activity Type catalog use `statestore` through `DaprReadModelStore`; adding a Timesheets sidecar alone would still leave those reads and writes unavailable. |
| Credentials | `env | cut -d= -f1 | rg -i '^(TIMESHEETS|EVENTSTORE|ASPIRE|DAPR|AUTHENTICATION|APP_API_TOKEN|JWT)' | sort` produced no variable names. No secret values were read or captured. EventStore's local signing credentials are generated per AppHost run and are not connected to a Timesheets resource in the checked-in topology. |
| Isolated state and projections | No Timesheets sidecar, isolated test identity, or delivery route exists in the current topology. The token-hash index and fresh Activity Type catalog therefore cannot be populated and inspected through real delivery for this journey. Existing local Redis and the externally owned runtime were not modified. |

The decisive prerequisite is a platform-owned local topology that attaches the
existing Timesheets HTTP host with Dapr app ID `timesheets`, configures the
EventStore gateway and workload signing, admits the required trusted effect
calls, and provides a platform-approved scoped read-model store for delivery
and loading of the index and catalog. It must also provide isolated test
identities/state and a development fixture access guard without weakening
production authorization. The
authority-mode issuer still cannot bind tenant/domain/actor claims and remains
a separate production gate.

## Historical re-run procedure before the prerequisite was delivered

At this earlier preflight, the opt-in lane could not reach a Timesheets Dapr
application. The connected topology and the working commands are documented
above. Keep signing keys, credentials, raw links, and token hashes out of
command captures.

The verification command proposed at that preflight was:

```bash
TIMESHEETS_LIVE_DAPR=1 tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests -class '*MagicLinkLiveDaprTests'
```

The implemented lane fails if an opted-in prerequisite disappears. It requires
`Completed` plus exact stored-event readback before HTTP success, inspects the
stored record/issue/use/effect events and delivered index/catalog, then repeats
after actor or process restart and races concurrent use requests. One stored
use/effect batch and common opaque denial for every loser are required. The
earlier preflight did not reach any of these observations. No processes were
started by that preflight, so no cleanup command was run.

## Connected local topology proof on 2026-10-11

This later run supersedes the topology findings in the preflight above. The
preflight remains a record of what was unavailable before the local AppHost
composition was added. From the repository root, these commands produced the
observations below:

| Command | Observed result |
| --- | --- |
| `dotnet build src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj --no-restore -m:1 -v:q` | Passed with zero warnings and errors. |
| `dotnet build references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/Hexalith.EventStore.Aspire.csproj --no-restore -m:1 -v:q` and the corresponding `Hexalith.EventStore.DomainService.csproj` build | Both passed with zero warnings and errors. |
| From `references/Hexalith.EventStore`: `tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests -method '*AdminOperationalIndexMetadata_Create_IncludesNamedRouteForKeyedProcessorDomain' -method '*NamedProjectionMetadata_IncludesAsyncOnlyKeyedProcessorDomain'` | Two passed, zero failed. |
| `aspire run --non-interactive` followed by `aspire describe --non-interactive --format Json` | EventStore and Timesheets projects and their Dapr sidecars reported `Running` and `Healthy`. EventStore listens on local 8082; Timesheets retains public 8080 and internal 8081. |
| `dapr list -o json` | Distinct `eventstore` and `timesheets` app IDs; their sidecars target app ports 8082 and 8081 respectively. |
| `python3 scripts/local_aspire_gateway_probe.py` | A resource-bound Timesheets assertion reached the command processor (invalid probe command rejected 422), status returned `Rejected` (200), and stream read returned 200. Missing assertions returned 401; wrong-tenant assertions returned 403 on command, status, and stream routes. No domain event was committed by this probe. |
| `python3 scripts/local_aspire_projection_probe.py` | Signed metadata returned dispatch v2 and both Timesheets named routes. A signed direct request to public `http://127.0.0.1:8080/project/v2` returned 404; the sidecar invocation reached the internal listener. A synthetic `ActivityTypeCreated` delivery returned 200/`Completed`, and a separate Dapr GET through `timesheets-readmodels` returned the catalog item with `Fresh` cursor 1. A synthetic `MagicLinkConfirmationCapabilityIssued` delivery returned 200/`Completed`; a separate Dapr GET verified its token-hash candidate index entry mapped to the expected isolated tenant and capability without printing the hash. Missing and wrong-operation assertions returned 401 and 403. The Timesheets sidecar listed only `timesheets-readmodels`; GET against `statestore` returned 400. |

The two local-only scripts require unique Timesheets process and Dapr app IDs,
bind the process to its Aspire-provided Dapr endpoint, and read the generated
Development signing key from its environment in memory on Linux. They print no
key, app-channel token, raw link, or token hash. The
synthetic projection request invokes the SDK route; it does not write directly
to the read-model component. Its isolated tenant entry remains in the local
read-model Redis database. This is delivery-infrastructure proof only. No
EventStore-actor-originated Timesheets event, record/issue/use journey,
single-use race, or restart acceptance passed here; Story 3.6 remains in
progress and release remains **FAIL**.

Broader build/test checks remain blocked outside this topology increment:
`dotnet build Hexalith.Timesheets.slnx --no-restore -m:1 -v:q` failed in
`Hexalith.Works.Contracts` with missing EventStore event contract types;
`dotnet build Hexalith.Timesheets.slnx --no-restore -p:UseHexalithProjectReferences=true -m:1 -v:q`
failed with CS1704 for duplicate `Hexalith.Commons.UniqueIds` assemblies
(versions `2.30.1.0` and `1.0.0.0`). From the EventStore repository,
`dotnet build tests/Hexalith.EventStore.AppHost.Tests/Hexalith.EventStore.AppHost.Tests.csproj --no-restore -p:BuildProjectReferences=false -m:1 -v:q`
failed with CS0006 because its AppHost reference assembly was absent; the
ordinary project graph also failed in the existing Admin UI
(`INotificationService`) and sample API (missing assets). Its new AppPort unit
case could not run. The Timesheets projection-handler class passed (70/70)
through its built test executable after `dotnet test` hit the repository's
VSTest-on-.NET-10 tooling error.
