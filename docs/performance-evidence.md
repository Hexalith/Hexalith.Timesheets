# Performance Evidence Lane

Launch-relevant targets:

- Common command acknowledgements: `500 ms p95`.
- Common report queries: `2s p95`.

Story 1.1 reserves the test lane and documentation point only. Later data-bearing stories must add realistic tenant, project, contributor, period, and ledger fixtures without slowing the fast architecture/unit baseline.

Evidence should be collected in the integration/performance lane and should distinguish:

- Command acknowledgement latency through the EventStore-backed write path.
- Projection rebuild and checkpoint behavior.
- Common report query latency over rebuildable read models.
- Infrastructure-dependent setup cost, which must not be counted as fast unit baseline time.
