# Boundary Decision Record

## Timesheets Owns

- Time entry lifecycle and submission state.
- Timesheet period submission, approval, rejection, correction, and locking rules.
- Activity type governance for tenant and project scopes.
- External contributor confirmation state and evidence.
- Approved-time ledger projections, reporting views, and finance export evidence.
- Timesheets-specific audit decisions and event contracts.

## Timesheets References

- `Hexalith.EventStore`: authoritative event-sourced persistence, domain-service hosting, replay, query, projection, and command status infrastructure.
- `Hexalith.Tenants`: tenant lifecycle, membership, role, and configuration authority.
- `Hexalith.Parties`: stable party identity and personal data authority.
- `Hexalith.Projects`: stable project identity and project-owned state authority.
- `Hexalith.Works`: stable work identity and work-owned lifecycle/planning authority.
- `Hexalith.FrontComposer`: shell and generated command/projection UI metadata model.

## Durable Data Rule

Timesheets durable events and read models store stable Tenant, Party, Project, and Work identifiers. They must not copy Party personal data, Project names or hierarchy, Work lifecycle/planning state, Tenant membership state, tokens, secrets, or sibling display data.

## Runtime Rule

Aggregates stay pure. Authorization, tenant lookup, sibling validation, HTTP calls, logging, clocks, filesystem access, and UI shaping stay outside aggregate decisions.

## Current Scaffold Note

`Hexalith.Projects` and `Hexalith.Works` are present as root-level submodules in this workspace. Their exact contract surfaces must still be verified by the stories that perform trust-bearing integration with those modules.
