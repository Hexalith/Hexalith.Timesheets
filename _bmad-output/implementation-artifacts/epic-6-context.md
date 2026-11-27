# Epic 6 Context: Qualify Project Time and Finance Evidence

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Contributors, approvers, operators, and finance consumers must be able to rely on the recorded state, authorization, recovery, and audit evidence of one composed Project workflow and its finance outputs at the actual boundaries where they use them. This epic resolves audit gaps across released packages, host composition, multi-entry period recovery, external API reliability, ledger fold cost, and finance CSV delivery, separating implementation from deployed qualification.

## Stories

- Story 6.1: Enforce export comment policy on every representation
- Story 6.2: Require a durable audit before delivering finance output
- Story 6.3: Read one complete ledger snapshot per export
- Story 6.4: Persist workload-authenticated external confirmation
- Story 6.5: Bound external recording readback and report uncertainty
- Story 6.6: Support independent and composed route discovery
- Story 6.7: Wire one Project capture-to-ledger workflow
- Story 6.8: Qualify the deployed Project workflow
- Story 6.9: Reduce repeated ledger folds while preserving evidence
- Story 6.10: Recover interrupted period submissions and reviews

## Requirements & Constraints

- **Fail-closed authority:** Resolve tenant, actor, target, and operation authority at the server boundary; never trust caller-supplied claims or persist copied Tenant, Party, Project, or Work data. Gate access before loading state, dispatching writes, or disclosing reads. Missing policies, unavailable deciding providers, and cross-tenant access fail closed without disclosing protected details.
- **Per-value comment disclosure:** Evaluate display and export policies independently per value across current, original, and corrected comments on direct reads, rebuilds, previews, CSV bytes, and returned rows. Direct ledger reads publish text only when host projection-comment policy is configured and enabled and the value carries a supported server-issued publication version (1, 2, or 3) permitting inclusion; caller-supplied flags cannot grant disclosure. Finance export requires both trusted tenant export policy and version 3 server-issued per-value clearance; export permission cannot restore text withheld at publication, and legacy values without clearance are omitted. Always preserve IDs and correction lineage.
- **Finance snapshots and receipts:** Export produces deterministic CSV v1 of approved billable evidence only (no rates, invoices, taxes, payroll, or revenue recognition). Preview and generation require a billable-only filter, fresh data on every page, and a nonempty authorized scope; preview creates no file, rows, comment text, or audit event. Full export starts at page one (or rejects partial-page requests), reads one consistent ledger version in deterministic order, traverses empty authorized pages with advancing cursors, and withholds all output on scope, disclosure, freshness, version, or cursor failure. Before release, verify a durable receipt matching request identity, requester, tenant, filters, row count, format version, and SHA-256 hash of the exact CSV bytes, recording UTC request/generation instants, correlation, output scope, and freshness without comments or row bodies. Audit rejection, uncertainty, or identity reuse for different scope/content withholds all output; retrying after lost acknowledgement or restart recovers one matching receipt.
- **External API reliability and privacy:** Workload confirmation verifies the tenant-scoped entry's contributor and target, preserves approval state, applies once across retries and restarts, and returns opaque refusals to unauthorized tenants or contributors. External recording honors caller cancellation and a configured readback deadline, resolves retries under the same command identity to one recording after timeout and restart, and separates policy refusal from indeterminate commit in low-cardinality outcomes. Logs, traces, and public responses exclude raw links, tokens, credentials, secrets, personal identities, comments, command bodies, event payloads, and export rows.
- **Period admission and recovery:** Failed whole-period submission writes zero effects and returns field-specific guidance. A new period review requires fresh summaries covering the period owner's full membership, including entries outside a selected rejection; missing, incomplete, or stale evidence blocks the decision. Interrupted period operations stay pending until durable period and entry histories account for every required effect once, preserving original attribution, already-submitted entries, and unaffected members.

## Technical Decisions

- **Authoritative streams and ledger folds:** EventStore owner streams are the sole write authority; aggregates stay pure while services handle I/O. Faster ledger folds normalize history once and fold canonical events per tenant and owner stream, preserving tenant checks, sequence validation, duplicate handling, replay order, effective rows, superseded lineage, and golden CSV bytes. Measure elapsed time and allocations against an agreed tenant volume and budget before considering separate materialization.
- **Export verification:** The default audit recorder commits and verifies the matching EventStore receipt through the authorized workload gateway and fails closed when unavailable. Retaining a consistent snapshot across pages or detecting version drift with a retryable failure are both permissible if no partial or mixed-version output can be delivered.
- **Multi-stream period intent:** Identify submission by tenant and period ID, and review by tenant, period ID, and approval decision ID. Persist admitted intent and expected owner effects in EventStore before the first write—binding actor, contributor, authority, UTC instant, period boundary, member IDs (compared as a set), action, rejection reason, and per-entry rejection details (keyed by entry ID). Reordered retries match; conflicting intent fails closed. Recover intent across restarts, concurrent retries, and period no-ops, resuming only outstanding effects under concurrency checks; conflicting durable evidence blocks completion.
- **External readback:** Workload confirmation loads the recorded tenant-scoped owner from EventStore and acknowledges only verified effect readback. External recording reuses the bounded readback-deadline pattern used for magic-link submissions.
- **Package and receiver discovery:** Contracts stays infrastructure-free; ReadModels shares SDK index helpers and stored wrappers consumed independently by Server and Projections. Preserve public CLR identities, type forwarding, and stored JSON shapes when moving shared discovery ownership, and version/migrate existing combined members and attributes. A Projections-only consumer enumerates its inventory solely from declared dependencies without loading Server; a composed host enrolls every installed command and named projection (with schemas and receiver methods) once.
- **Release composition:** Retain the existing HTTP host, AppHost, and ServiceDefaults topology without adding a second host or UI project, keeping reusable kernel defaults fail-closed. Production-equivalent tenant, Party, Project, activity, approval, evidence-policy, and ledger-reader bindings belong in the owning release composition rather than Development/source-only fixtures.

## UX & Interaction Patterns

- **Consumer-host ownership:** Timesheets owns contracts, FrontComposer metadata, and authorization/freshness semantics; a consuming FrontComposer host owns Fluent UI V5 rendering, keyboard/focus/responsive behavior, and WCAG 2.2 AA verification.
- **Export preview and readiness:** Summarize readiness, freshness, row count, and comment policy across published values without returning rows or comment text. On a ready scope, report `Allowed` only when at least one published comment exists and every published value permits export; report `Excluded` for tenant exclusion, no comments, or no permitted values; for mixed permissions report `Excluded` with readiness detail `"Comments are permitted for some values only."` while keeping export ready. Missing required policy yields `PolicyRequired`; access denial returns an opaque denial with no preview. Offer download only after snapshot and audit verification succeed; otherwise keep download unavailable with a neutral retry message.
- **Pending period recovery:** While an interrupted period operation awaits recovery, show the period as pending, expose only verified individual entry states, and disable actions requiring whole-period completion until all required effects are verified.

## Cross-Story Dependencies

- **Finance delivery (6.1–6.3):** Finance CSV delivery requires all three stories. Start with comment disclosure (6.1) and the fail-closed audit guard (6.2), then complete receipt recovery (6.2) and single-snapshot pagination (6.3) once EventStore replay-identity and snapshot semantics are aligned.
- **External API (6.4–6.5):** Workload confirmation and bounded recording readback share EventStore readback, deadline, cancellation, and post-restart retry semantics.
- **Package discovery (6.6):** Independent, but align discovery compatibility before enrolling new command receivers in 6.7 and 6.10.
- **Project composition and qualification (6.7–6.8):** Timesheets and Platform wire the Project workflow in 6.7 before the release owner qualifies restart, rebuild, and cross-tenant isolation in 6.8.
- **Ledger fold cost (6.9):** Depends on export/correction golden cases from 6.1–6.3 and the reporting owner's representative volume and budget.
- **Period recovery (6.10):** Independent of finance delivery once Timesheets and EventStore settle the persistence entry point for admitted intent and effect verification.
- **Separate release gates:** Deployed magic-link authority, continuity, shared rebuild, and ingress privacy stay in Stories 3.6–3.7; tenant policy, legal hold, product UAT, and live Works activation remain separate release gates.
