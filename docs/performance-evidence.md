# Performance Evidence Lane

Launch-relevant targets:

- Common command acknowledgements: `500 ms p95`.
- Common Project and Work actual-time report queries over tenant/project/period filters: `2s p95`.

The current integration project contains in-process workflow coverage for capture, submission, approval, correction, locking, period submission, period approval, operational Time Entry queries, Approved-Time Ledger queries, Project/Work actual-time reports, approved billable-time export, and dashboard overview composition. This is functional workflow proof, not launch latency evidence.

The performance lane remains reserved with an explicit skipped test until realistic EventStore-backed tenant, project, work, contributor, period, ledger, and actual-time report fixtures exist without slowing the fast architecture/unit baseline.

Evidence should be collected in the integration/performance lane and should distinguish:

- Command acknowledgement latency through the EventStore-backed write path.
- Projection rebuild and checkpoint behavior.
- Common Project and Work actual-time report query latency over rebuildable read models.
- Common operational Time Entry query latency over bounded tenant/project/period filters with stable ordering and opaque cursor paging, rather than unbounded read-model scans.
- Infrastructure-dependent setup cost, which must not be counted as fast unit baseline time.
