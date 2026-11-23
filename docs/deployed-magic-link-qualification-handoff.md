# Deployed magic-link qualification handoff

## Readiness on 23 November 2026

**DW-86, DW-113, DW-119, DW-132 and DW-145 remain open.** Story 3.6 stays in
progress and the release decision remains **FAIL**. The qualification needs one
installed Platform-managed Timesheets release and target with current authority
and certified inventory. The isolated Development run stopped at the first
projection metadata request, before issuance or delivery. [Launch readiness](launch-readiness.md#story-36-qualification-on-2026-11-22)

| Source | Observed evidence |
| --- | --- |
| Platform `main` at `194a79e3fc6d0794bd49f6d08ae99e58efc615b0` | The Timesheets proposal directory and Development/source binding match the 22 November observations. The proposal still reports `BLOCKED_NOT_FOR_EXECUTION` with both approval and dispatcher flags false. The checkout lacks the two approved staging values files, and the recorded recovery prerequisite expired on 18 October 2026. [Platform proposal](https://github.com/Hexalith/Hexalith.Platform/blob/194a79e3fc6d0794bd49f6d08ae99e58efc615b0/eng/timesheets-deployment/platform-managed-operations.json) · [Source binding](https://github.com/Hexalith/Hexalith.Platform/blob/194a79e3fc6d0794bd49f6d08ae99e58efc615b0/src/Hexalith.Platform.Composition/PlatformTimesheetsBinding.cs) |
| Latest Platform GitHub deployment record inspected | Production record `7963191374` finished successfully in the `publish` job of **Publish Platform test extension**, which published a fixture package. [Workflow run](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44902658431/job/136842569651) |
| Latest Timesheets GitHub deployment record inspected | Production record `7899306309` refers to the **Release** workflow and its `v2.1.1` NuGet publication. The attached packages and publication manifest attest to package publication. [Workflow run](https://github.com/Hexalith/Hexalith.Timesheets/actions/runs/44421310299/job/135214900513) · [Release assets](https://github.com/Hexalith/Hexalith.Timesheets/releases/tag/v2.1.1) |

Both GitHub deployment records report package publication and have empty
environment URLs. Qualification still requires an installed Timesheets image
digest, target and observed domain providers. Platform Story 4.6 supplies
signed staging record encoding and verification. Story 4.7 owns durable
decisions and replay, and subsequent stories own activation and live
qualification. Cluster state was unobserved.
[Platform Story 4.6](https://github.com/Hexalith/Hexalith.Platform/blob/194a79e3fc6d0794bd49f6d08ae99e58efc615b0/_bmad-output/implementation-artifacts/spec-4-6-sign-staging-evidence-v2-decisions.md)

### Offline enrollment recheck

Platform's `prepare.py` evaluated the clean Timesheets `main` candidate
`f6110cbfa52c2a5dec831892ed8d57deb3ee3657` from a standalone Platform checkout
at the revision above with its required direct submodule pins. Two runs
produced byte-identical plans with `preparationSha256`
`294bcea4e5e91c51070a5c56082a1029ef1ee9c221239b9cb3b99892d271b2c2`.
Both reported `prepared-blocked` and `deploymentAuthorized: false`. The retained
release fields for the image and chart digests, provenance and rollback
package digest were empty. The preparer reported `source-enrollment-drift`. Platform
declares, indexes and checks out Timesheets
`151fd37308eb08f942040c283209e7886ad954a6`, which differs from the candidate.
The Platform release owner needs to select and bind the release source.
The plan also reports `owner-complete-declaration` as missing. Timesheets,
EventStore and Platform still need to bind Dapr and its extensions, delivery
topics, secrets, egress and providers for the selected release. Platform's
staging isolation and durable broker stories (4.14, 4.17) are in progress.
Staging secrets, Dapr trust and the first staging release (4.15, 4.16, 4.22)
remain in the backlog.
[Platform preparer](https://github.com/Hexalith/Hexalith.Platform/blob/194a79e3fc6d0794bd49f6d08ae99e58efc615b0/eng/timesheets-deployment/prepare.py) · [Platform sprint status](https://github.com/Hexalith/Hexalith.Platform/blob/194a79e3fc6d0794bd49f6d08ae99e58efc615b0/_bmad-output/implementation-artifacts/sprint-status.yaml)

The Platform enrollment suite ran 63 tests with zero failures or skips.
The suite covers offline preparation, Helm rendering with fixture values and
native-inspection logic with mocked responses. The dated qualification report
remains the preparer's pinned source for the historical site observation.
[Dated qualification report](deployed-magic-link-acceptance.md)

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
cut or issuance correlation where applicable. [Platform ownership](https://github.com/Hexalith/Hexalith.Platform/blob/194a79e3fc6d0794bd49f6d08ae99e58efc615b0/eng/timesheets-deployment/platform-managed-operations.md)

Reconcile the handoff's release source with the enrollment pin. Start with
evidence review and read-only inventory. Before an issuance,
revocation, rebuild or migration, obtain the Platform owner's approval for the
exact commands and target, including the intended effect, responsible operator
and recovery procedure. Recheck target identity, operator authority and
prerequisite validity before each approved operation. Record sanitized
observations, timings, counts and evidence references. Keep bearer tokens,
magic-link URLs and raw tenant histories in their approved systems.

## Evidence required for each deferral

| ID | Missing input | Evidence to collect |
| --- | --- | --- |
| DW-86 | Timesheets source owner’s certified history cut, plus approved remediation if the inventory finds affected histories | Inventory capability and Time Entry histories for legacy Activity Type ownership and record the approved disposition for each affected set. For a correction or revoke-and-reissue, verify persisted results and the disposition of the old capability. An empty inventory requires the same coverage proof. |
| DW-113 | Platform and EventStore production issuer binding with enrolled tenant and Party identity providers | Observe the actual server-side identity source and its active EventStore trust binding. Verify authorized issuance and revocation and refusal for forged context, cross-tenant scope and actor mismatch. Confirm both authorized effects in the owner stream and verify that refused requests leave it unchanged. |
| DW-119 | Installed issuance/index/public routes and the release owner’s readiness budget | Measure the interval from committed issuance to the first persisted token-hash index observation and the first successful public resolution under the same issuance correlation. Compare both delays to the approved readiness requirement. Keep the entry open if either observation is missing or late. |
| DW-132 | Platform’s installed component inventory and Timesheets’ certified source cut and migration authority | Verify the scope of `timesheets-readmodels`. Inventory prior layouts and certify the authoritative histories feeding the token-hash index and tenant Activity Type catalog, including histories predating the handlers when the prior store is empty. For a rebuild or migration, prove owner continuity, account for pending deliveries and verify their recovery. Enforce writer and delivery quiescence through promotion or abort. Check both read models against the certified inventory before opening traffic. |
| DW-145 | Platform and EventStore installed subscriber and receiver observations | Correlate automatic issuance delivery with the persisted index update and owner checkpoint. Attribute the delivery to the installed subscriber and receiver. Exclude explicit projection dispatch from this run. |

Keep DW-119 and DW-145 outcomes independently reviewable in one run. The
delivery evidence establishes who projected the event. The timing observations
establish when the link became usable. Use the execution report specified in the
draft to retain per-item outcomes, exact commands with credentials omitted,
source and target identities, and references to persisted-state evidence.
Record each disposition and any unmet requirement against its ledger entry.
[Qualification draft](../_bmad-output/implementation-artifacts/spec-five-deployed-magic-link-deferrals-20261115.md#tasks--acceptance) · [Deferred-work ledger](../_bmad-output/implementation-artifacts/deferred-work.md)
