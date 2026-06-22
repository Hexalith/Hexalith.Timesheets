# Performance Evidence Lane

Launch-relevant targets:

- Common command acknowledgements (NFR10): `500 ms p95` in a warmed local service.
- Common Project and Work actual-time report queries over tenant/project/period filters (NFR11): `2s p95`.

Verdict vocabulary used below: **pass / concern / fail / waived**.

## NFR10 — Capture and governance command acknowledgement evidence

Story 1.11 makes NFR10 a measured launch-readiness lane so Epic 5 aggregates this evidence instead of creating the first measurement path. The measured lane drives the **real in-process composed command pipeline** (authorization gate → aggregate decision → acknowledgement result) for the capture and governance command surface and records the per-scenario p95. Each measured iteration asserts the real acknowledgement outcome (dispatched / accepted), so a degenerate or no-op path cannot register as "fast".

### Measured run

- Test: `CaptureAndGovernanceCommandPerformanceLaneTests.Capture_and_governance_command_acknowledgements_record_nfr10_p95_evidence` (`tests/Hexalith.Timesheets.IntegrationTests`).
- Opt-in: `TIMESHEETS_PERF=1` (the lane is skipped by default; see "How to run the performance lane").
- Iterations: 100 warm-up (discarded) + 500 measured per scenario; p95 via sorted nearest-rank.
- Machine / runtime: AMD Ryzen 9 9950X3D (16 logical CPUs), Ubuntu 26.04 (WSL2), .NET SDK 10.0.301 / runtime 10.0.9, Debug build, single run.

| Command scenario (kind) | p95 (ms) | min (ms) | median (ms) | max (ms) |
|---|---|---|---|---|
| Record draft time entry (capture) | 0.0018 | 0.0003 | 0.0003 | 0.0400 |
| Submit time entries for approval (governance) | 0.0026 | 0.0005 | 0.0006 | 0.0052 |
| Approve submitted time entry (governance) | 0.0031 | 0.0007 | 0.0007 | 0.0074 |
| Reject submitted time entry (governance) | 0.0029 | 0.0007 | 0.0007 | 0.0149 |
| Correct rejected time entry (governance) | 0.0036 | 0.0009 | 0.0010 | 0.0499 |
| Correct approved time entry / locking add-correction (governance) | 0.0036 | 0.0010 | 0.0010 | 0.0175 |
| Submit timesheet period (governance) | 0.0056 | 0.0025 | 0.0035 | 0.0420 |
| Approve timesheet period (governance) | 0.0040 | 0.0012 | 0.0013 | 0.0376 |
| Reject timesheet period (governance) | 0.0039 | 0.0014 | 0.0015 | 0.0171 |
| Create tenant Activity Type (governance) | 0.0015 | 0.0001 | 0.0001 | 0.0036 |
| Create project Activity Type (governance) | 0.0015 | 0.0001 | 0.0001 | 0.0047 |

Worst-case command acknowledgement p95 across capture/governance: **0.0056 ms** (submit timesheet period).

### NFR10 comparison and verdict

- In-process composed command-decision path vs NFR10 `500 ms p95`: worst-case measured p95 is **0.0056 ms**, roughly five orders of magnitude inside the target. **Verdict: pass** for the in-process command-acknowledgement surface (capture + governance). Per-scenario results are all **pass**.
- EventStore-backed end-to-end command acknowledgement (persistence + Dapr wire path): **Verdict: waived (deferred)**. This path is the more realistic NFR10 target but needs runtime fixtures that do not exist yet, so it remains reserved (see below). This story owns it and defers it; Epic 5 aggregates this verdict rather than re-measuring.

### Scope and honesty constraint

This lane measures the **in-process composed command-decision + authorization + result** path, not the full EventStore-backed persistence/Dapr wire path. "Warmed local service" here means a warmed in-process command pipeline with warm-up iterations discarded before measurement — a legitimate, reproducible NFR10 lower bound for the command-decision path, and the part Story 1.1 reserved as runnable now. The in-process number does **not** prove the EventStore-backed wire path; that path's verdict is recorded above as waived/deferred.

## Reserved measurement still pending

The performance lane keeps an explicit statically-skipped placeholder
(`PerformanceEvidenceLaneTests.Performance_lane_is_reserved_for_launch_latency_evidence`) for the full
EventStore-backed wire/persistence measurement until realistic EventStore-backed tenant, project, work,
contributor, period, ledger, and actual-time report fixtures exist without slowing the fast
architecture/unit baseline.

Evidence should be collected in the integration/performance lane and should distinguish:

- Command acknowledgement latency through the EventStore-backed write path.
- Projection rebuild and checkpoint behavior.
- Common Project and Work actual-time report query latency over rebuildable read models.
- Common operational Time Entry query latency over bounded tenant/project/period filters with stable ordering and opaque cursor paging, rather than unbounded read-model scans.
- Infrastructure-dependent setup cost, which must not be counted as fast unit baseline time.

## How to run the performance lane

The lane is **skipped by default** so it never enters the fast unit baseline. Opt in with `TIMESHEETS_PERF=1`:

```bash
# Build first (the lane lives in the infra-free IntegrationTests project).
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --no-restore -warnaserror

# Opt-in run via dotnet test (set the env var on the same invocation):
TIMESHEETS_PERF=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet test tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj --no-build

# If dotnet test is blocked by local VSTest socket permissions, run the built xUnit v3 executable directly:
TIMESHEETS_PERF=1 DOTNET_CLI_HOME=/tmp/dotnet-cli-home \
  tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests \
  -class "Hexalith.Timesheets.IntegrationTests.CaptureAndGovernanceCommandPerformanceLaneTests" \
  -xml /tmp/perf-evidence.xml
```

Without `TIMESHEETS_PERF=1` the test dynamically skips (`Assert.Skip`) and the fast baseline is unaffected. The per-scenario p95/min/median/max are emitted via `ITestOutputHelper` (timing aggregates and scenario names only — no command bodies, payloads, comments, or personal data, per NFR12); the `-xml` report above captures that output for copy-pasting into this document.
