# Project capture-to-ledger workflow

The `Hexalith.Timesheets` HTTP host owns the opt-in composition for one tenant and
one Project. A contributor records and submits time. A configured Project
approver approves it and can append a correction. The ledger exposes the
effective approved evidence and, when requested, its superseded original with
approval and correction lineage.

## Provider bindings

| Provider | Source of authority |
| --- | --- |
| Tenant | Tenants supplies the actor's active membership and role through the EventStore gateway's `get-user-tenants` query. The tenant index must report current, complete evidence. |
| Party | Parties through the EventStore gateway validates the contributor's Party in that tenant. |
| Project | Projects through the EventStore gateway validates the configured target and its lifecycle. |
| Activity | The Timesheets tenant Activity Type catalog supplies active types and Project restrictions. The selected catalog generation needs complete Project policy coverage from an authoritative rebuild. |
| Approval | The host configures Project approver Party IDs for the selected tenant and Project. Existing approval policy evaluates the actor and the requested action. |
| Evidence policy | The host supplies the tenant's approved retention, legal hold and comment sensitivity decisions. |
| Ledger reader | The Timesheets delivered Approved Time Ledger projection supplies tenant-scoped approved rows and their source lineage. Writes require an enrolled, healthy ledger generation. |

Set `Timesheets:ProjectWorkflow:Enabled` and supply the owning `TenantId`,
`ProjectId`, `ApproverPartyIds` and `EvidencePolicy` in that configuration section.
Apply the tenant's approved evidence policy to `LegalHoldPolicyConfigured`,
`TenantRetentionOverridePolicyConfigured` and `CommentSensitivityPolicyConfigured`.
This composition suppresses projection comment text. It retains the configured
retention, legal hold and comment sensitivity gates.

Set `Timesheets:ProjectWorkflow:AuthorityGatewayEndpoint` to the HTTPS origin of the
EventStore gateway's authenticated ingress. Configure the SDK's authority client
under `EventStore:Authentication`. Enroll `tenants-get-user-tenants` in the
gateway catalog and grant the service identity `tenants:read`. Assign its
authenticated subject the `TenantOwner` role scoped to the enrolled tenant. The
schema-three catalog uses the authority credential's
`eventstore:surface=service` claim. Grant the service identity read access to
Party detail and Project lifecycle in that tenant. The host uses the Timesheets
workload identity on the existing Dapr transport for owner command and status
requests.

The reusable Server kernel supplies deny or unavailable defaults. The host
authorizes authenticated tenant and actor evidence against the providers and
refuses access when required authority is unavailable. Timesheets persists
sibling identifiers through EventStore. The Activity Type and ledger projections
consume delivered owner events.

## Entry points

Send human requests under the host's authenticated `HumanPolicy`. Use the same
`Idempotency-Key` of up to 128 characters when retrying the same mutation. The
authenticated subject must match the actor's Party ID. Confirmed mutations return
`204 No Content`, ledger reads return `200 OK`, and workflow refusals return an
empty `404 Not Found`. An unconfirmed mutation returns an empty
`503 Service Unavailable` with `Retry-After`. Retry it with the same body and key
to recover its original status.

| Method and path | Request contract |
| --- | --- |
| `POST /api/timesheets/time-entries` | Employee `RecordTimeEntry` for the enrolled Project and authenticated contributor. |
| `POST /api/timesheets/time-entries/{id}/submit` | `SubmitTimeEntriesForApproval` with exactly one selected entry that matches the path. |
| `POST /api/timesheets/time-entries/{id}/approve` | `ApproveTimeEntry` with the entry ID from the path. |
| `POST /api/timesheets/time-entries/{id}/corrections` | `CorrectApprovedTimeEntry` with the entry ID from the path. |
| `POST /api/timesheets/approved-ledger` | `QueryApprovedTimeLedger`. Set `IncludeSupersededRows` to include the original row. |

The EventStore SDK exposes the four Project command receivers on `POST /process`,
delivers the named `approved-time-ledger` projection on `POST /project/v2`, and
dispatches `QueryApprovedTimeLedger` on `POST /query`. The SDK receiver policies and
Timesheets internal-listener guard apply to these routes. The query route requires
SDK-verified delegated caller evidence. The signed aggregate ID must identify the
configured Project and match any Project filter in the payload.

## Dependency graph

The Release build and focused workflow tests used EventStore **5.6.0**, Projects
Contracts **2.0.0**, Parties Contracts **4.0.2**, and Tenants Contracts **8.1.1**
with .NET SDK **11.0.100**. Debug used the root-declared source revisions. The
local workflow tests exercised the EventStore aggregate actor and SDK routes
with in-memory state and controlled sibling gateway responses. All seven packed
consumers passed, including standalone Server, standalone Projections, and their
composed route catalog. The three sibling Contracts references belong to the
non-packed HTTP host.

## Story 6.8 deployment prerequisites

Platform owns the production-equivalent host and EventStore gateway activation,
the internal receiver and public ingress policies, and the release route
catalog. The tenant owner supplies a tenant with active membership, an active
Party and Project, a populated Activity Type catalog, designated approvers, and
approved evidence policy. Enroll the ledger writer and prepare an opening
generation through EventStore before serving commands. For existing owner
streams, rebuild from complete histories so ordinary delivery can validate
future suffixes against the correct prefix. Qualify a tenant volume whose
accumulated ledger fits the SDK shared rebuild candidate limit, which defaults
to 1 MiB.

The release owner verifies the recorded events and ledger lineage after restart
and after projection rebuild, and repeats the cross-tenant refusals from a second
tenant.

The [launch readiness record](launch-readiness.md) tracks deployed qualification,
tenant and legal decisions, FR14 evidence in Stories 3.6 and 3.7, and product UAT.
