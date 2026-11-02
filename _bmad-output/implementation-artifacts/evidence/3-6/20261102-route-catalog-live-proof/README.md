# Story 3.6 private route-catalog attempt — 2026-11-02

The single supported command was:

```bash
python3 scripts/run_isolated_magic_link_dapr.py --route-catalog --log-directory _bmad-output/implementation-artifacts/evidence/3-6/20261102-route-catalog-live-proof
```

It exited **1** at mandatory idle admission. The Dapr inventory already contained eight applications under a separate EventStore AppHost. The runner refused before source snapshot, private Redis/control-plane creation, owner-profile creation, AppHost startup, catalog composition, fixture HTTP calls, EventStore/Dapr persistence, restart or rebuild. It did not read or lock shared Redis. No foreign process or state was changed. The [admission](admission.json), [finalizer](cleanup.json) and [qualification](verification.json) receipts contain only the sanitized outcome. Raw foreign inventory rows and per-command logs were discarded from this packet.

The no-resource finalizer completed; it cannot qualify cleanup after an owned runtime. Actual receiver/workload observations, stored EventStore and Dapr end state, source consistency across lifecycle, restart/rebuild preservation and deployed authority remain **unobserved**. Story 3.6 stays in progress and launch stays **FAIL**.
