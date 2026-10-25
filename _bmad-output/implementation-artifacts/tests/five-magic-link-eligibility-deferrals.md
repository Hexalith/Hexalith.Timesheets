# Five magic-link eligibility and authorization deferrals

The user accepted both recommended product decisions on 2026-10-25. This increment closes **DW-59, DW-62, DW-73, DW-76 and DW-124** under the [approved specification](../spec-five-magic-link-eligibility-deferrals.md). Final qualification covers **2,693 distinct passing cases and five expected skips**; review is complete and no new work is deferred.

The implementation began at Timesheets `a71c2c1e14a8630eed87866b95e6c62445685481`. An external fast-forward advanced the checkout to `1a80faec5360e0276cb908b594922a88895ff27e` during implementation. That already-published commit changes only the Works fallback in `Directory.Packages.props`; it is preserved and is not part of this increment. Verification uses the latter checkout plus the uncommitted implementation. Neither baseline identifier claims to identify the final tested worktree by itself.

## Decisions and implemented behavior

| ID | Decision and implementation | Acceptance boundary |
|---|---|---|
| DW-59 | An otherwise valid scoped link can confirm an external contributor's entry after approval or correction. Display and aggregate/service validation share the external-entry eligibility rule. Confirmation adds contributor evidence without changing approval, lock or correction state. | Existing identity/target/type scope checks still apply. This does not authorize arbitrary changed scope after correction. |
| DW-62 | Preserve the settled confirm-as-is rule: a retired tenant-owned recorded Activity Type can still be displayed and confirmed. Current recorded-type evidence must be fresh, unambiguous and tenant-owned. | The old retention note remains historical. This batch appends a new dated disposition backed by current focused proof; it does not reverse the settled policy. |
| DW-73 | Display can show the retired recorded type. Submission may replace it with an active type allowed by the supplied catalog's capture-availability gates. Both display and commit require valid recorded-type evidence. | Selecting the retired type again remains denied. The existing Project-restriction gap remains DW-63; this increment does not supply Project-specific restriction evidence. |
| DW-76 | The historical no-authentication-scheme diagnosis is already superseded: the host invokes authentication/authorization and the SDK supplies workload authentication. Existing missing/forged-credential regressions are rerun. | Production tenant/Party identity remains open under DW-113. Fixture identities are not production identity evidence. |
| DW-124 | Actorless expiry uses `IMagicLinkSystemExpiryAuthorizer` with gateway-verified workload origin and authoritative tenant/capability/entry/target scope. HTTP management, the ambient accessor and outgoing assertions now share a resolver that excludes implicit synthetic workload subjects. Actorless HTTP expiry still requires tenant admission and authoritative system policy. The processing clock must reach expiry, independent of a future submitted audit instant. Explicit actors retain Party management authorization. The kernel and retained four-argument processor constructor deny system expiry by default. | A host must explicitly provide workload policy to enable system expiry. No workload name is interpreted as a Party. Natural token expiry checks remain independent of an emitted expiry transition. |

## Source map

- [Command service](../../../src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkConfirmationCapabilityCommandService.cs): shared confirmation/adjustment scope and recorded-type evidence.
- [Time Entry aggregate](../../../src/Hexalith.Timesheets.Server/TimeEntries/TimeEntry.cs): evidence-only external confirmation eligibility.
- [EventStore processor](../../../src/Hexalith.Timesheets.Server/MagicLinks/MagicLinkEventStoreDomainProcessor.cs): authoritative use checks and separate expiry authorization paths.
- [Workload authorization contract](../../../src/Hexalith.Timesheets.Server/MagicLinks/IMagicLinkSystemExpiryAuthorizer.cs), [deny default](../../../src/Hexalith.Timesheets.Server/MagicLinks/DenyAllMagicLinkSystemExpiryAuthorizer.cs), and [kernel registration](../../../src/Hexalith.Timesheets.Server/Runtime/ServiceCollectionExtensions.cs): explicit opt-in policy seam with fail-closed defaults.
- [HTTP host](../../../src/Hexalith.Timesheets/Program.cs): existing authentication middleware, unchanged by this increment.

- [Management endpoints](../../../src/Hexalith.Timesheets/Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs), [trusted context](../../../src/Hexalith.Timesheets/Runtime/HttpContextTimesheetsTrustedContextAccessor.cs) and [outgoing assertion source](../../../src/Hexalith.Timesheets/Runtime/EventStoreGatewayWorkloadAssertionSource.cs): shared synthetic-subject exclusion and actorless HTTP handoff.

## Verification

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore Hexalith.Timesheets.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -m:1 /nr:false
DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet build Hexalith.Timesheets.slnx --configuration Debug --no-restore -p:UseHexalithProjectReferences=true -warnaserror -m:1 /nr:false
```

All six executables ran individually. The initial run passed 2,674 cases with five expected skips. After review corrections, Server, Architecture and Integration were rerun; unchanged Contracts, Works and Projections evidence remains applicable. Projections tests do not call the changed processor/expiry/HTTP identity paths.

| Project | Qualified passing cases | Skipped | Evidence |
|---|---:|---:|---|
| Hexalith.Timesheets.ArchitectureTests | 118 | 0 | Post-review full run |
| Hexalith.Timesheets.Contracts.Tests | 108 | 0 | Unchanged applicable full run |
| Hexalith.Timesheets.Server.Tests | 720 | 0 | Post-review full run |
| Hexalith.Timesheets.Projections.Tests | 737 | 0 | Unchanged applicable full run |
| Hexalith.Timesheets.IntegrationTests | 934 | 5 | 933 full-run passes plus corrected source-fitness case; see qualification below |
| Hexalith.Timesheets.Works.Tests | 76 | 0 | Unchanged applicable full run |

The final full Integration command exited 1 because a new comment mentioned `EventStore`, violating the existing source-fitness guard. Its other 933 cases passed. Only that comment word changed afterward; the full Debug/source warnings-as-errors build then passed, and the entire affected source-fitness class passed **4/4**. The full Integration suite was not repeated after this comment-only correction. Qualified totals replace the failed case with its passing recheck, excluding duplicate passes.

The [portable verification receipt](../evidence/five-magic-link-eligibility-deferrals/20261025/verification.json) retains exact full-run and recheck commands, raw exit codes/counts, qualified counts, named cases, full declared route parameters, XML/source fingerprints and skip reasons. Focused selections and the deliberately failing mutant are not added to distinct passing totals. Raw runner XML/logs remain in `/tmp/timesheets-eligibility-*`; the retained summary does not require those temporary files.

## Review and acceptance audit

All three configured review layers ran. Eleven filed findings were individually triaged: ten were patched, and one spec-only progress-log observation was rejected under the workflow rule. The edge-case layer returned no findings. No new deferrals remain. Independent targeted rechecks confirmed the HTTP expiry identity handoff, processing-clock bound and authoritative confirmation verification gap are resolved.

Review corrections passed **98 focused server and 42 focused integration cases**. A freshness-only processor mutant failed the unrelated-type confirmation case as intended; exact source bytes were restored, rebuilt and both control cases passed. The retained mutation failure is evidence of regression sensitivity, not an outstanding failed gate.

| Matrix row | Executed proof |
|---|---|
| Confirmation | Approved/corrected/combined histories preserve lifecycle state while storing use/confirmation evidence. Corrected contributor, target, type and scope changes refuse the old link. Missing recorded-type evidence after preflight refuses authoritative confirmation; the freshness-only mutant is caught. |
| Adjustment | Retired recorded type displays; selecting it again has the canonical opaque refusal; active replacement stores previous/adjusted type evidence and refuses replay. Missing/foreign/ambiguous/stale recorded-type evidence fails closed. |
| Expiry | Default denial, implicit workload/Party-name collision, explicit Party evidence, actorless HTTP allow/deny, tenant admission, recorded-owner matching, exact Work target and processing-clock boundary are covered. Persisted terminal evidence survives actor recreation. |
| DW-76 | Missing/forged credentials and unrelated-operation assertions refuse management/projection access. Fixture claims do not establish production identity. |

## Limits

No AppHost, dependency, gitlink or foreign runtime was changed by this increment. No current-graph AppHost runtime or deployed authority, inventory, continuity, shared-rebuild, Project-restriction or production tenant/Party identity acceptance is inferred. DW-63 and DW-113 remain open. System expiry remains denied until its host supplies authoritative system policy and tenant admission; token expiry checks remain independent of an emitted expiry transition. The launch verdict remains **FAIL**.
