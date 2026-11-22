# Deployed magic-link qualification handoff

## Readiness on 22 November 2026

**DW-86, DW-113, DW-119, DW-132 and DW-145 remain open.** Story 3.6 stays in
progress and the release decision remains **FAIL**. The qualification needs one
installed Platform-managed Timesheets release and target with current authority
and certified inventory. The isolated Development run stopped at the first
projection metadata request, before issuance or delivery. [Launch readiness](launch-readiness.md#story-36-qualification-on-2026-11-22)

| Source | Observed evidence |
| --- | --- |
| Platform `main` at `44dd1e52904cd07ef49be74edd947c75f9b76374` | The proposal reports `BLOCKED_NOT_FOR_EXECUTION`, approval and dispatcher are absent, both proposed approved staging values files are absent in the checkout, and the recorded recovery prerequisite expired on 18 October 2026. The source binding still requires Development/source mode. [Platform proposal](https://github.com/Hexalith/Hexalith.Platform/blob/44dd1e52904cd07ef49be74edd947c75f9b76374/eng/timesheets-deployment/platform-managed-operations.json) · [Source binding](https://github.com/Hexalith/Hexalith.Platform/blob/44dd1e52904cd07ef49be74edd947c75f9b76374/src/Hexalith.Platform.Composition/PlatformTimesheetsBinding.cs) |
| Latest Platform GitHub deployment record inspected | Production record `7963191374` finished successfully in the `publish` job of **Publish Platform test extension**, which published a fixture package. [Workflow run](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44902658431/job/136842569651) |
| Latest Timesheets GitHub deployment record inspected | Production record `7899306309` refers to the **Release** workflow and its `v2.1.1` NuGet publication. The attached packages and publication manifest attest to package publication. [Workflow run](https://github.com/Hexalith/Hexalith.Timesheets/actions/runs/44421310299/job/135214900513) · [Release assets](https://github.com/Hexalith/Hexalith.Timesheets/releases/tag/v2.1.1) |

The two GitHub deployment records have empty environment URLs. Neither job
provides the installed Timesheets image digest, target or observed domain
providers. The recheck used Git, repository files and GitHub metadata. Cluster
state was unobserved. The [dated qualification report](deployed-magic-link-acceptance.md)
is pinned by Platform's preparer and retains its original bytes.

## Bind the acceptance run

Platform supplies the retained release identifier, immutable source and image
digests, selected environment, cluster and namespace identities, observation
time, and installed public and private endpoint bindings. Confirm the running
image and endpoint routing against that handoff. Record the EventStore and Dapr
app identities, the approved tenant and actor scope, and the provider and
operation authority for that scope. EventStore owns the resource-binding issuer
contract. Timesheets supplies the authoritative-state inventory and assesses
its domain assertions. Platform runs those checks against the installed
release. Bind each receipt to the same release and target, with an inventory
cut or issuance correlation where applicable. [Platform ownership](https://github.com/Hexalith/Hexalith.Platform/blob/44dd1e52904cd07ef49be74edd947c75f9b76374/eng/timesheets-deployment/platform-managed-operations.md)

Start with evidence review and read-only inventory. Before an issuance,
revocation, rebuild or migration, obtain the Platform owner's approval for the
exact commands and target, including the intended effect, responsible operator
and recovery procedure. Recheck target identity, operator authority and
prerequisite validity before each approved operation. Record sanitized
observations, timings, counts and evidence references. Keep bearer tokens,
magic-link URLs and raw tenant histories in their approved systems.

## Evidence required for each deferral

| ID | Evidence to collect |
| --- | --- |
| DW-86 | Obtain certified coverage of capability and Time Entry histories through a named source cut. Identify histories with legacy Activity Type ownership and record the approved disposition for each affected set. For a correction or revoke-and-reissue, verify persisted results and the disposition of the old capability. An empty inventory requires the same coverage proof. |
| DW-113 | Identify the actual server-side tenant and Party identity source and its active EventStore trust binding. With enrolled principals, verify authorized issuance and revocation and refusal for forged context, cross-tenant scope and actor mismatch. Confirm both authorized effects in the owner stream and verify that refused requests leave it unchanged. |
| DW-119 | Measure the interval from committed issuance to the first persisted token-hash index observation and the first successful public resolution under the same issuance correlation. Record the observed delay and compare it to the release owner's readiness requirement before claiming immediate resolution. A missed deadline or missing persisted observation leaves this item open. |
| DW-132 | Verify the installed `timesheets-readmodels` component and its Timesheets scope. Inventory prior component layouts and certify coverage of the authoritative histories feeding both the index and tenant Activity Type catalog, including histories predating handler deployment when the prior store is empty. For a rebuild or migration, verify owner continuity, account for and verify recovery of pending deliveries, and enforce writer and delivery quiescence through promotion or abort. Check both selected read models against the certified inventory before opening traffic. |
| DW-145 | Identify the installed subscriber and correlate its automatically delivered issuance event with the persisted index update and owner checkpoint. Use installed subscriber/receiver observations to attribute delivery. Exclude explicit projection dispatch from this run. |

Keep DW-119 and DW-145 outcomes independently reviewable in one run. The
delivery evidence establishes who projected the event. The timing observations
establish when the link became usable. Use the execution report specified in the
draft to retain per-item outcomes, exact commands with credentials omitted,
source and target identities, and references to persisted-state evidence.
Record each disposition and any unmet requirement against its ledger entry.
[Qualification draft](../_bmad-output/implementation-artifacts/spec-five-deployed-magic-link-deferrals-20261115.md#tasks--acceptance) · [Deferred-work ledger](../_bmad-output/implementation-artifacts/deferred-work.md)
