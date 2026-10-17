# Story 3.6 implementation and actual observation packet

**Result: Story 3.6 remains in progress; deployed acceptance NOT RUN; release FAIL.**
The owner authorized implementation, local tests, fresh offline preparation and
read-only inspection with existing access. No deployment, recovery, migration,
permission change, domain mutation on a live target, registry push, Git write or
submodule update occurred. The owner self-reviews project documentation under
Platform's current sole-owner policy. Independent software reviewers checked the
technical changes; their reviews do not authenticate operational facts.

The [current acceptance report](../../../../../docs/deployed-magic-link-2026-10-17.md)
and [machine assessment](acceptance-matrix.json) distinguish implemented capability,
current native facts, local runtime proof and missing deployed acceptance.

## Current target and authority observations

- [Original native observation](native-observation.json), command/result in
  [native-command-result.json](native-command-result.json), completed 14:28:55Z.
  The fresh recovery prerequisite binds these original bytes unchanged.
- [First reviewed collector observation](native-observation-reviewed.json) and
  [command/source identity](native-reviewed-command.json) retain the intermediate
  14:47:51Z pass before the immutable-context follow-up.
- The [final sealed-context observation](native-observation-sealed-context.json) and
  [command/source record](native-sealed-context-command.json) are retained
  separately. The existing inline certificate identity is handed to child clients
  through sealed anonymous memory, with no private bytes in evidence or disk files.
  All network calls pin the expected endpoint, IP SAN and verified TLS; config,
  public certificates and cluster identity are rechecked before accepting a result.
- [Server and node version](native-target-version.json): Kubernetes v1.34.9,
  node1 Ready. Current cluster UID is
  `b5860674-1d1e-4a3b-b413-e1bb3b9a4557`; API `https://192.168.1.30:6443`.
  Both planned staging namespaces and identifiable Timesheets resources are absent.
  Other-story isolated EventStore resources are preserved and not adopted.
- The actual separate API TLS connection verifies configured CA and IP SAN,
  TLS 1.3/ALPN HTTP/1.1. That connection presents no client certificate; the
  authenticated client evidence comes from kubectl's actual identity response,
  whose credential fingerprint matches the selected public certificate.
- Current wildcard native permissions and the actual `jpiquot-cluster-admin`
  binding are observed at their cuts. The [additional twelve entitlement checks](../../../../../references/Hexalith.EventStore/docs/evidence/story-3-6/20261017t142649z-production-authority-preparation/current-rights.json)
  all returned yes. Historical grant/revocation completeness and Timesheets
  production domain or migration authority are not inferred.

This is a separately authorized native observation implementation. It does not
activate, revalidate or qualify the expired candidate's frozen parser/runtime.
Raw native objects, Secret objects, private keys, raw tokens and domain payloads
are not retained. Summaries explicitly omit environment and secret metadata
values while retaining typed ACL, backend and acceptance-condition identities.

## Implemented capabilities and owning records

- Platform [hosting preparation](../../../../../references/Hexalith.Platform/eng/timesheets-deployment/hosted-package-preparation.md): maintained application/environment charts, split listeners, public route allowlist,
  private SDK rebuild paths, scoped Dapr/RBAC/network settings, required existing
  registry pull-Secret reference. Original packages and reviewed-v2 packages are
  separate. No released image or installed artifact is manufactured.
- EventStore [production authority](../../../../../references/Hexalith.EventStore/docs/security/workload-binding-authority.md): explicit RSA signer and current grant checks, exact tenant/domain/actor
  binding, validating outbound client, dedicated workload JWT trust and disabled
  defaults. Actual signer/client/grants/endpoint enrollment remains missing.
  Initial [187-test authentication evidence](../../../../../references/Hexalith.EventStore/docs/evidence/story-3-6/20261017t142649z-production-authority-preparation/verification.json)
  is a dated pre-review result; review fixes receive their own record.
- Platform [fresh prerequisite](../../../../../references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-27/20261017t143112z-fresh-recovery-prerequisite/README.md): RID `20261017t143202z`, prepared `2026-10-17T14:32:02Z`, exclusive ceiling
  `2026-10-18T14:32:02Z`, packet SHA-256
  `d14efe6e8e82d983187416392ccc77207b446827f149caf3b45ed4b1ae90939a`.
  It is unsigned/incomplete preparation, not a current recovery point.
  Later shape-check fixes are recorded separately; the packet was not resealed.

## Actual local runtime and verification

- [Solution build](solution-build.json), [full log](solution-build.log): pinned
  SDK 10.0.401, Debug source references, zero warnings/errors. All six individual
  [default suites](timesheets-tests-summary.json) passed **1,899 tests**, with
  five declared skips and zero failures in 1,904 total. These ran after the initial
  authority implementation, before the subsequent review transport patches.
- [Fresh disposable runtime](local-runtime-result.json): **1/1 passed in
  126.032s**, zero skips, using existing built host assets. Original assertions
  cover fresh catalog, persisted confirm and adjust, one race winner, replay denial,
  composed rebuild, actor restart and post-restart use. The new production
  authority was not enrolled in this local journey.
- [Cleanup](local-runtime/cleanup.json), [captured exits](local-runtime/recorded-process-exit.json),
  [remaining resource inventory](local-runtime/resource-exit.json) and
  [shared preservation](local-runtime/shared-preservation.json): all captured
  identities exited, no bound resources remain, three owned containers and
  component copies removed; shared broker/state/TTL/scheduler fingerprints match.
- [Reviewed solution build](solution-build-reviewed.json) passes zero warnings or
  errors. A composed HTTP persistence regression passes 1/1 after the review
  patches; [exact command/result](reviewed-http-regression.json) and
  [log](reviewed-http-regression.log) are retained. The six large default suites
  above were not repeated after narrow authority patches.
- [Local SDK archive command](container-preparation.json) and
  [artifact identity](container-identity.json): Linux amd64 Debug candidate,
  non-root `app`, archive SHA-256
  `ba02223104839a33ea1d6031f7f329accda2ce2c3d4de775d410c3f81ce1c41c`.
  No registry push or production Release artifact is claimed. The archive
  precedes review patches; any reviewed candidate is recorded separately.
- [Reviewed archive identity and private retained copy](container-identity-reviewed.json),
  archive SHA-256 `0dd15a4de359b8f644fd156d5844168bbd7ce2dd53fa884c4fcc661375953aa3`,
  follow [the reviewed SDK publish](container-preparation-reviewed.json).
  Earlier artifacts are unchanged.
- Focused reviewed Platform results retain exact commands and logs separately.
  They cover collector refusal/privacy, actual renderer behavior and offline
  preparation/custody. They are software checks, not deployed acceptance.
  [Root focused verification](platform-focused-checks.json) passes 45 cases:
  collector 16, fresh prerequisites 14 and real Helm renderer 15. The final
  [Ingress default-backend regression](native-final-regression.json) retains
  the last collector correction. EventStore's [reviewed authority record](../../../../../references/Hexalith.EventStore/docs/evidence/story-3-6/20261017t144520z-production-authority-review/verification-final.json)
  passes 83 focused cases, with a separate independent 14-case follow-up.

Initial failures are retained in [initial-results.json](initial-results.json):
the collector's first filename shadowed Python's stdlib before any native call,
and an interim authority edit failed the solution build with CA1062. Both were
corrected. The broader Platform enrollment suite remains blocked: 31 existing
fixture errors require unavailable nested Timesheets inputs; 21 cases passed.
No nested dependency was initialized and no broad pass is claimed. One intermediate
collector fixture expectation also required correction after immutable-context
testing; its final focused tests pass.

A separate status command printed a temporary local dashboard login URL in tool
output. It is excluded from this packet, and the owned AppHost session subsequently
stopped. This limitation is recorded without retaining or repeating its value.

## Review, preservation and remaining operational work

Three independent layers reviewed the same multi-repository snapshot. Every
finding, including duplicates, is triaged in the continuation
[spec](../../../spec-3-6-deployment-authority-runtime-continuation.md).
Fixes address streaming bounds, immutable client/target custody, accurate installed
wiring, issuer rotation, metadata caching, rate partitions, actual transmitted
credentials, expiry clipping, malformed packet handling and pull authentication.

[Historical integrity and latest-policy comparison](historical-preservation-and-latest-policy.json)
retain the original qualification SHA-256
`f3fa3e844f0e23f732371e80258945feede0866b0608e5768457433ebacb2118`
and original verifier refusal SHA-256
`38610efadcc9e0358a806e9178c9de75cb737079e864e5186cee21c744d6f810`.
The original exit 1 and expired ceiling `2026-10-16T16:27:56Z` remain unchanged;
the old exact verifier was not executed. The newer standalone Platform revision
`2749ad8abdafc038ded44f6154d7182c2fd71422` changes readiness test evidence only;
its documentary policy/production plan bytes match the inspected reference copy.

Before a live approval request, the handoff still needs a retained complete Release,
qualified staging environment/Gateway/Redis, protected production issuer and
server-side provider enrollment, current accepted recovery, and complete legacy
inventory/source continuity/writer-delivery maintenance evidence. Source hashes,
an absent deployment, cluster-admin rights, owner documents or a local test cannot
fill those factual slots. No executable live command is presented as ready with
invented inputs. The [blocked operational proposal](operational-plan.json) contains
exact chart/tool hashes, target, proposed argv/effects and forward-only failure
procedure; missing approved values and prerequisites prevent execution.
Story 3.6/Epic 3 remain in progress, spec-23 draft, Platform 4.1
operational gate closed and release FAIL.

## Final consistency checks

[Final source/preservation record](final-source-and-preservation.json) binds all
38 changed technical files across their owning repository HEADs, confirms zero
staged changes and successful whitespace checks, and verifies unchanged shared
entry points, original pinned qualification, spec-23 and prior checkpoint prefix.
Packet links resolve and the credential-pattern scan has zero hits.
[Final launch-readiness command](launch-readiness-final.json) and
[log](launch-readiness-final.log) pass 13/13 after documentation updates.
The explicit local verdict remains incomplete; no statement here grants execution.
