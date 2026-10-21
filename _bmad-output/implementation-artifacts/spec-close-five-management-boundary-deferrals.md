---
title: 'Close five deferred magic-link management boundary gaps'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Close five existing deferred magic-link management items: reject null revoke/expire identifiers; require capability, contributor, Time Entry and audit-source issuance fields; authorize expiry before submission; authorize management requests before loading state; and require the existing Process workload policy on management routes. Preserve opaque denials, public anonymous capability routes, server-side resource revalidation, fail-closed production defaults and EventStore persistence. Verify the bounded fixes, resolve the five ledger entries, then commit and push to main as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation baseline: clean main at `7de2ac5d0052111b5f6d72564617005d4f2e5df8`. User explicitly requests five deferred items and publication to main. These are one bounded management-boundary goal, with no unresolved product choices or irreversible implementation actions. Use the oneshot route.
- Five exact ledger summaries: “Reject null capability identifiers on revoke and expire before dereferencing them”; “Require the capability id, contributor, Time Entry id and audit source in magic-link issuance validation”; “Add the missing server-side authorization decision to magic-link expiry before durable submission”; “Authorize admin issue/revoke/expire requests before the loader replays capability streams or reads the tenant catalog”; “Give magic-link management endpoints a route-specific workload policy”.
- `Endpoints/MagicLinks/MagicLinkConfirmationCapabilityEndpoints.cs` currently dereferences transition IDs and loads catalog/capabilities before domain authorization. Apply `EventStoreDomainServicePolicies.Process` to its management group, matching the existing successful fixture's DomainServiceProcess assertion. Add a coarse Timesheets MagicLinkIssuance gate before state reads and retain the full state-dependent service authorization afterwards. Public routes are mapped outside that group.
- `Server/MagicLinks/MagicLinkConfirmationCapability.cs:ValidateIssue` lacks required-member checks. Add aggregate validation for CapabilityId, Contributor, TimeEntryId and Source without persisting incomplete events. `MagicLinkConfirmationCapabilityCommandService` already gates resolved expiry; expose equivalent asynchronous expiry to the endpoint, and close the synchronous unguarded entry point without introducing blocking async calls.
- Reuse `MagicLinkConfirmationCapabilityCommandServiceTests` and the composed in-process `MagicLinkConfirmationHttpBoundaryTests` fixtures for malformed bodies, denied coarse and resolved access, wrong-operation signed assertions, and successful issue/revoke/expire controls. Assert denied management performs no state reads, token generation or command submission where applicable. Existing aggregate issuance tests can assert required-member rejection directly.
- Repository architecture, baseline, state/Git instructions, .editorconfig, root build files and affected project/test configuration were inspected. No .gitattributes is present. Do not edit dependencies, AppHost, technical modules or synchronized agent guidance.
- Runtime admission: Aspire/Dapr inspection finds an existing foreign Tenants AppHost using eventstore and port 8080. A Timesheets start would collide, so use resource-free composed HTTP tests and individual built suites. Do not claim runtime/deployed acceptance; launch remains FAIL.
- Validation: matching Debug/source solution restore/build with catalog HexalithEventStoreVersion when required; focused tests first, then all six individual test executables. Run the configured independent Blind Hunter review, triage every finding, and record verification before publication.
- Implemented the Process group policy and pre-read domain gate; issuance admission carries the known resource/contributor references, while transitions use coarse admission followed by state-dependent authorization. Expiry calls the existing resolved authorization path through ExpireAsync. The legacy synchronous method remains source-compatible but always returns a rejection because asynchronous authorization cannot safely run there; callers use ExpireAsync for authorized expiry. Required issuance fields are checked at both HTTP and aggregate boundaries.
- Review corrections use the existing ITimesheetsTenantAccessValidator for transition admission, avoiding resource-dependent access policies before state is available. The full access guard still runs with folded contributor/target references. Issuance refuses missing required members before token generation, and the retained synchronous expiry method is marked obsolete with a migration message and README guidance. No dependency pins or gitlinks changed.
- Initial focused server run passed 49/49 and the broader pre-review server suite passed 564/564. The earlier focused HTTP process was stopped after review corrections so the updated build can run focused regressions and the complete IntegrationTests suite; no partial run is counted as final verification.

## Review Triage Log

- Medium, patch: a complete transition access guard called before state loading could deny legitimate policies requiring resource references. Replaced coarse transition admission with the existing tenant/role validator, retaining full resource authorization after loading. Default denial registrations remain intact.
- Medium, patch: the resource-denied expiry test used a future expiry, allowing premature-expiry validation to mask an authorization regression. Its expiry case now seeds an already-expired authoritative capability.
- Low, patch: invalid direct service issuance generated token material before required-member rejection. The pre-generation branch now returns aggregate validation for malformed identity/audit members, and tests require zero GenerateCount.
- Medium, patch: synchronous expiry's fail-closed compatibility behavior needed compile-time migration guidance. Added an Obsolete message and README instructions to use ExpireAsync; a narrowly scoped suppression tests the obsolete refusal itself.
- Low, patch: the new Work-reference admission branch lacked independent coverage. The admission theory now checks Project and Work requests, including absence of the other reference and no state reads on denial.
- Low, finalization action: the selected ledger entries were still open during review of the in-progress spec. Record their source-backed resolutions after final verification; do not claim completion before checks finish.
- Independent recheck confirms all five code/test findings addressed, with no remaining defect identified in those fixes. All six original findings are recorded above. Exactly the five selected ledger entries now carry source-backed resolution notes; other open entries and their historical evidence are preserved.
- Final matching Debug/source solution build passed with zero warnings/errors; final Server.Tests passed 564/564 and the focused HTTP selection passed 33/33, including null/missing transition IDs, incomplete issuance, denied admission, unrelated signed operations, resource-denied expiry/revocation and Project/Work admission. Full IntegrationTests and the unchanged projection suite are still running; do not commit or finalize until every required default suite passes.
- Compatibility follow-through for review finding 1: the Development-only opt-in LiveDaprFixtureAccessGuard now also supplies ITimesheetsTenantAccessValidator through the existing guarded host registration. Both interfaces share the same isolated-tenant/actor predicate; normal production defaults stay denied. Three composed-host regression cases verify the registered fixture admits the isolated identity and refuses ordinary tenants or missing actors. The earlier full integration process was superseded before final verification to include this wiring; it is not counted as a completed test run.
- Broad verification exposed the existing endpoint source-fitness guard's blanket EventStore-name ban: its earlier allowance covered only public-route registration. The guard now explicitly requires and permits the exact SDK Process policy call while continuing to forbid other EventStore references. Runtime wrong-operation refusal coverage remains independent. Rebuild and rerun the full IntegrationTests suite after this source-test correction; superseded runs do not count as final passes.

## Final Verification

- Final matching solution restore/build succeeded, with zero warnings/errors. All six individual executable suites passed: **2232 passed, five intentional skips, zero failures/errors/not-run** out of 2237 tests. Focused HTTP regressions passed 36/36 and endpoint fitness passed 4/4. Commands, project counts/timings, review result and runtime-admission limits are recorded in [verification evidence](tests/five-management-boundary-handoffs.md).
- The later independent rechecks found no concrete defect in shared local-fixture admission or the narrowly permitted SDK-policy source check. Exactly five original ledger entries were resolved, with no new deferrals. The old synchronous expiry method's fail-closed behavior is documented as a breaking change in the validated Conventional Commit message.
