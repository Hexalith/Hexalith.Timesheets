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

## Shared Read-Model Ownership — 2026-10-25

`Hexalith.Timesheets.Contracts` owns pure index DTOs, shared policy options and domain/publication constants. `Hexalith.Timesheets.ReadModels` owns the Timesheets-specific SDK index helper, tree reader/builder and stored event wrappers; it references Contracts and EventStore Client/Contracts, with no Server or hosting dependency. Server and Projections consume ReadModels independently. Projections retains its explicit EventStore DomainService dependency for SDK projection handlers; that intentional SDK runtime closure is distinct from a dependency on Timesheets Server.

Relocated public types retain their CLR namespaces. Server forwards those types to their new assemblies, preserving existing type resolution; stored payload full names and JSON shapes remain unchanged. ReadModels adds a library package, with no additional host or topology.

`TenantActivityTypeCatalogReadModelAddress` in Contracts is the canonical tenant catalog address. The historical `timesheets:magic-links:activity-type-catalog:{tenant}:v1` key, `timesheets-readmodels` store and `catalog` slot are intentional compatibility contracts. The old magic-link address delegates to this owner. `TenantActivityTypeCatalogProjectionHandler` remains the sole canonical catalog writer; introducing another physical key would require an explicit migration decision.

Both fresh v7 index bootstrap paths inspect every direct legacy shard with bounded concurrency and refuse persisted rows, including empty, malformed and JSON-null rows. Refusal preserves legacy contents and pending recovery state. This conservative guard removes the need to assume an absent direct-shard layout; it does not inventory a deployment or authorize concurrent obsolete writers. Deployed inventory, certified migration and shared-rebuild acceptance remain required under Story 3.6.

## Current Scaffold Note

`Hexalith.Projects` and `Hexalith.Works` are present as `references/` submodules in this workspace. The sole default Works checkout is `<workspace>/references/Hexalith.Works`; a repository-root `Hexalith.Works` checkout is forbidden, and `Hexalith.Timesheets/Hexalith.Works` must never be initialized. Their contract surfaces are verified by the stories that perform trust-bearing integration with those modules.

- Project references are validated through the `Hexalith.Projects` `GetProjectAsync` consumer query.
- Work references are validated through the `Hexalith.Works` `get-work-item` consumer query, consumed by `WorksQueryWorkReferenceValidator` (in `src/Hexalith.Timesheets.Works`) over the Timesheets-owned `IWorksQueryChannel` port (Story 1.10, 2026-06-22). The validator is fail-closed by default and copies no Works-owned state; the host opts in with `AddTimesheetsWorksReferenceValidation()`, otherwise the `DenyAllWorkReferenceValidator` default keeps Work writes closed.
- Works planned-effort reporting is concrete behind `WorksQueryWorkPlannedEffortProvider` (Story 4.8, 2026-06-22), but the default host keeps `UnavailableWorkPlannedEffortProvider` until a live Works channel is bound and `AddTimesheetsWorksPlannedEffortReporting()` is called.
- Launch-readiness classifications, owner-named waivers, and the overall release-gate decision are recorded in `docs/launch-readiness.md`.

## Rendering and Runtime Acceptance — 2026-10-20

Timesheets owns UI-neutral contracts, action/status semantics, and FrontComposer-compatible metadata. A consuming FrontComposer host owns rendered Fluent UI V5 composition and browser/WCAG 2.2 AA evidence; no Timesheets UI project may be added.

The HTTP host registers the projection assembly and EventStore processing, including canonical token-hash capability index, tenant Activity Type catalog, and Time Entry evidence handlers. Mapped in-process and historical isolated local persistence proof exist. The latest local journey passed on 2026-10-18 using the earlier AppHost graph; current-graph runtime qualification and deployed authority, inventory/continuity, and shared-rebuild acceptance remain unfinished under Story 3.6. Works host channels still require explicit opt-in, and export preview still has no dedicated HTTP route. [Launch readiness](launch-readiness.md) records approval sources and the overall **FAIL** verdict.
