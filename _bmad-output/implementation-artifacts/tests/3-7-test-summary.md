# Test Automation Summary — Story 3.7 (Prove Magic-Link No-Disclosure at the HTTP Boundary)

**Workflow:** `bmad-qa-generate-e2e-tests`
**Date:** 2026-06-22
**Engineer:** QA automation (generation only — no code review / no story validation)
**Feature under test:** the four external magic-link routes
(`GET/POST /api/timesheets/magic-links/confirm`, `GET/POST /api/timesheets/magic-links/adjust`)
exercised over real HTTP through `WebApplicationFactory<Program>`.

## Framework Detected

- .NET 10 / xUnit v3 `3.2.2` + Shouldly `4.3.0` + `Microsoft.AspNetCore.Mvc.Testing` `10.0.9`
  (existing project conventions reused — **no new framework introduced**).
- "E2E" here is **in-process HTTP-boundary** testing: a real `HttpClient` against the real composed
  `Program.cs` host (in-memory `TestServer`, no Dapr/EventStore), overriding only the data-source seams
  (`IMagicLinkConfirmationCapabilityStateLoader`, `ITimesheetsAccessGuard`, `IReadModelStore`, `TimeProvider`)
  inside `ConfigureTestServices`. No production DI default is weakened.
- Test method convention: `Snake_case_describes_behavior` (local IntegrationTests convention).

## Coverage Baseline (story-delivered, kept green)

The dev landed 5 HTTP-boundary tests + 1 fitness fact in `review`. This QA pass **extends** them — no rewrite,
no production change. Pre-existing tests:

- `Invalid_magic_link_http_boundary_responses_are_equivalent_and_opaque` (44 = 4 routes × 11 invalid cases)
- `Valid_magic_link_http_boundary_requests_are_distinguishable_from_invalid_denials`
- `Empty_magic_link_token_uses_the_same_opaque_boundary_denial`
- `Repeated_invalid_magic_link_attempts_are_byte_equivalent_without_throttling_headers`
- `Invalid_magic_link_http_boundary_emits_no_timesheets_sensitive_diagnostics`

## Gaps Discovered & Auto-Applied (4 new tests)

All added to `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs`.

- [x] `Empty_or_missing_magic_link_token_is_indistinguishable_from_an_invalid_state_denial_on_every_route`
  — **AC1 / AC2**. The blank-token branch was only proven on `confirm-get` (GET), asserting just status +
  content-type. This drives **empty (`?t=`), whitespace (`?t=%20%20`), and a completely absent `?t=`** across
  **all four** external routes and asserts each collapses **byte-for-byte** (normalized body + header set) onto a
  genuine invalid-state denial — closing the equivalence proof on the boundary the prior test left partial.
- [x] `Valid_magic_link_display_responses_do_not_disclose_internal_sensitive_material`
  — **AC2 applied to the success path / AC3**. The success contrast asserted only the *positive* fields. This
  asserts the authorized `200 OK` confirm/adjust display bodies **omit** the internal time-entry comment text
  (`"sensitive customer comment"`), the upstream `ExternalSource` provenance (`supplier-api` / `request-1`), the
  internal approval state (`Draft`), and every raw identifier (`party-*`, `time-entry-1`, `tenant-1`). Confirms
  no-disclosure holds even when the host *succeeds*, not only when it denies.
- [x] `Malformed_submit_body_neither_dispatches_nor_discloses_on_magic_link_submit_routes`
  — **AC1 / AC2 (request-binding boundary)**. A malformed JSON command body on both submit routes fails closed at
  binding: it returns **400**, **never dispatches (no 202)**, and never echoes the query **token value** or any
  business state (none is loaded — binding fails before the handler). Covers a boundary with no service-level
  analogue. (See Finding below.)
- [x] `Empty_magic_link_token_records_the_malformed_outcome_category_without_sensitive_diagnostics`
  — **AC4**. The diagnostics proof covered only the `Unknown` outcome category. This proves the **second** emitted
  category, `Malformed` (the blank-token path), is also recorded for internal audit **and** privacy-safe — so every
  category the boundary actually emits is internal-only and leaks nothing.

## Finding (documented, not a production defect)

The malformed-submit-body test surfaced that the **in-process test host runs in the `Development` environment**,
so a binding failure renders the **developer exception page** (a framework stack trace). This is a test-host
artifact, **not** a production disclosure — the developer page only registers in `Development`; production returns
a bare `400`. The stack trace legitimately contains framework type names (e.g. `CancellationToken`), which is why
the test asserts absence of the **token value and business material** specifically, rather than the generic word
`"token"`. Verified empirically: the secret token value and all business-sensitive values are **absent** from the
malformed-body response. No production change recommended (out of this story's scope; the no-dispatch + no-secret
invariant holds in every environment).

## Coverage

| Acceptance Criterion | Status | Executable proof |
| --- | --- | --- |
| **AC1** equivalent neutral 403 across the invalid matrix on all 4 routes | ✅ | invalid-matrix equivalence (44) **+ blank/missing-token equivalence on all 4 routes** |
| **AC2** no tenant/party/project/work/token/hash/reason/`RecoveryPath` disclosure | ✅ | sensitive-absence on every invalid body **+ on the valid display bodies** **+ on the malformed-body 400** |
| **AC3** non-vacuous distinguishable success (200 / 202) | ✅ | valid display/submit contrast (kept) **+ valid display omits internal material** |
| **AC4** privacy-safe invalid-link diagnostics, category internal-only | ✅ | `Unknown` category (kept) **+ `Malformed` category** + `DiagnosticsPrivacyTests` fitness lane |
| **AC5** repeated/abuse indistinguishability, no throttling headers | ✅ | byte-equivalent repeats, no `Retry-After`/`X-RateLimit-*` (kept) |
| **AC6** builds clean `-warnaserror`, fitness invariants stay green | ✅ | 0/0 build; ArchitectureTests 28/28 green |

- **External routes covered:** 4/4 (confirm GET+POST, adjust GET+POST).
- **Boundary branches:** empty / whitespace / missing `?t=` (all 4 routes), malformed submit body (both POST routes).
- **HTTP-boundary tests in the class:** 5 → **9** (`+4`).

## Test Results (exact, real)

Run via the built xUnit v3 executable directly (the sandbox blocks the VSTest socket listener:
`System.Net.Sockets.SocketException (13): Permission denied`); the `TestServer` transport is in-memory, so the
HTTP-boundary tests run under the direct executable — verified.

- `Hexalith.Timesheets.IntegrationTests` — **Total 75, Failed 0, Skipped 3** (was 71; **+4** this pass; the 3
  skips are pre-existing infra/perf lanes). HTTP-boundary class alone: **9/9** pass.
- `Hexalith.Timesheets.ArchitectureTests` — **Total 28, Failed 0, Skipped 0** (unchanged — no fitness/source files
  touched; `Magic_link_external_routes_share_one_denial_helper`, `DiagnosticsPrivacyTests`,
  `DependencyDirectionTests` all stay green).
- Build: `dotnet build … --no-restore -warnaserror -m:1 /nr:false` → **0 Warning(s), 0 Error(s)** for both lanes.

## Files Changed (this QA pass)

- `tests/Hexalith.Timesheets.IntegrationTests/MagicLinkConfirmationHttpBoundaryTests.cs` (modified) — +4 gap tests
  and supporting helpers (`SendWithRawQueryAsync`, `BlankTokenQueries`, `InternalDisclosureMaterial`). **No new
  test doubles or fixtures** — reuses the story's scripted loader/access-guard/time/claims/logger doubles.

No production code, package, project-reference, or fitness-test changes were made by this QA pass.

## Next Steps

- Run both lanes in CI (already invariant-guarded by the fitness lane).
- When a future runtime story wires the live token-hash projection topology, lift the faked-loader success
  contrast (AC3) to a fully live valid token end-to-end (Story 3.6 caveat).
- Optional hardening (out of scope here): if production should also be proven, a `Production`-environment factory
  variant could assert the bare `400` body for malformed input — noted as a follow-up, not a defect.
