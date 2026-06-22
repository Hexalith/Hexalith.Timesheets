# Boundary Decision Record

## Timesheets Owns

- Time entry lifecycle and submission state.
- Timesheet period submission, approval, rejection, and grouped review evidence.
- Time Entry correction and locking rules, including rejected-entry correction and approved-entry additive correction.
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

`Hexalith.Projects` and `Hexalith.Works` are present as root-level submodules in this workspace. Their contract surfaces are verified by the stories that perform trust-bearing integration with those modules.

- Project references are validated through the `Hexalith.Projects` `GetProjectAsync` consumer query.
- Work references are validated through the `Hexalith.Works` `get-work-item` consumer query, consumed by `WorksQueryWorkReferenceValidator` (in `src/Hexalith.Timesheets.Works`) over the Timesheets-owned `IWorksQueryChannel` port (Story 1.10, 2026-06-22). The validator is fail-closed by default and copies no Works-owned state; the host opts in with `AddTimesheetsWorksReferenceValidation()`, otherwise the `DenyAllWorkReferenceValidator` default keeps Work writes closed.
- Remaining trust-bearing surfaces still pending their owning stories (for example Works planned-effort for Story 4.8) keep their fail-closed defaults until those stories make the adapter concrete.
