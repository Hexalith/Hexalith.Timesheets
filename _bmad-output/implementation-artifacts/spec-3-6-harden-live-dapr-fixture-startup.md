---
title: 'Harden live Dapr fixture startup'
type: 'bugfix'
created: '2026-10-13'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Direct AppHost starts with only `TIMESHEETS_LIVE_DAPR_FIXTURE=1` can enable development fixture authorization while selecting shared Dapr defaults. The deferred entry in `deferred-work.md` requires a separate startup-hardening increment.

**Approach:** Require run mode, a validated isolated component directory and explicit private placement/scheduler endpoints whenever the fixture flag is enabled. Reject incomplete configuration before resource construction or fixture authorization. Preserve ordinary starts and the supported isolated runner. Add compiled probes for the bare flag and incomplete settings, retaining the unconditional validation-only exit before resources so regression probes remain harmless.

Validate with the `global.json` SDK, focused Python checks, compiled guards, Architecture/LaunchReadiness tests and the supported isolated live runner when prerequisites are present. Record exact results and preservation/cleanup evidence. Preserve the completed isolation spec's frozen intent and user changes. Do not activate, clear, migrate or repair legacy storage; do not stage, commit, push or update dependencies/gitlinks. Story 3.6 and Epic 3 remain in progress and release readiness remains FAIL until legacy/deployed certification gates close.

</frozen-after-approval>

## Implementation Notes

- New scoped increment based on `a377d1240a86074b670d43796b9d2dcc49927f5e`; no independent sprint story key. Story 3.6/Epic 3 stay in progress and release stays FAIL. The completed isolation spec is preserved byte-for-byte.
- Reuse existing component/content and control-plane validators. The initial gate now admits validation when either fixture enablement or an override is present; enable authorization using the same validated fixture flag. Validation-only still returns before security/resource setup. Ordinary selection, graph, ports and supported isolated runner remain unchanged.
- Extend compiled cases with bare flag, missing/empty/whitespace components and publish mode. Ordinary-default selection omits the fixture settings and control endpoints. Python checks inspect actual probe environment/arguments and cleanup.
- Runtime baseline was empty before source edits. An ordinary pre-edit start would activate retained storage, so only inventories were taken. A separate Tenants AppHost subsequently started with a matching `eventstore` Dapr ID. Both infrastructure-backed guards and the supported live runner correctly refused preflight with exit 1, creating no resources. Foreign resources were preserved.
- Add `--configuration-only` to the compiled guard utility: reuse all validation-only cases with unused private loopback values, create only temporary component copies and sanitized logs, and issue no runtime/container/storage commands. Default guard behavior and the isolated live runner remain intact. This makes the compiled configuration checks available with a concurrent foreign runtime. The first configuration-only attempt exposed an unset duplicate-endpoint test value; assign both test control endpoints to the fixture object and assert every subprocess environment value is a string. Retain that failed attempt's evidence.

- Infrastructure-backed compiled guards retried after the foreign runtime exited: 21/21, driver exit 0; preservation/cleanup pass in `guards/run-5gzxbytp`. Supported latest isolated live journey passed 1/1 in 138.093s, runner exit 0, `live/run-k4wmmm45`. Retained DB0/13/14 permanent/TTL values/deadlines and all 55 scheduler entries/cluster identity match, zero natural expirations. All 16 captured PID/start identities exited; three owned containers and component copies were removed. Both initial refusals remain recorded.
- Independent blind review found inherited configuration aliases could compromise the probe's environment flag. Remove controlled case/colon/host-prefix aliases and force validation-only plus execution mode through command-line configuration. Add bare-fixture publish coverage and deterministic offline mismatch endpoints. Python covers polluted environments, no sockets/storage/runtime commands and string environment values. Add an Architecture tripwire protecting the return before Add* security/resources and Build/Run calls. These are probe corrections; live runner and AppHost runtime sources remain unchanged after the live pass.

- Final reviewed checks: SDK 10.0.401 source-reference Debug build passed with 0 warnings/errors in 4.87s; Python 65/65 in 7.277s; Architecture 57/57 in 2.187s; configuration-only guards with polluted caller variables 22/22 and infrastructure-backed guards 22/22, both exit 0. Final infrastructure guards again matched shared preservation and removed three owned containers/components. The initial Architecture test compilation failed CS0023 in 5.16s; split the void-returning assertion chain and preserve that failure log alongside the successful build. Exact results are in tests/3-6-test-summary.md.
- No runtime/domain sources changed after the passing live journey. The reviewer rechecked all four safety/coverage patches and found no remaining concrete regression. Source/index preservation and final inventories are recorded separately; no Git mutation is authorized.

## Review Triage Log

| Finding | Verdict | Evidence and disposition |
| --- | --- | --- |
| Inherited environment aliases can defeat harmlessness | high | Environment-provider case/colon normalization makes exact-spelling removals insufficient; new offline mode exposes shared-default risk. Patch: remove controlled aliases including Aspire/Dotnet host prefixes, force validation-only and execution mode via command-line configuration, and test polluted environments. Real polluted compiled suite passed 22/22; reviewer confirmed correction. |
| Publish with bare fixture flag lacks coverage | medium | Valid-component publish previously covered only the older override admission branch. Patch: add publish-bare-fixture without components/control endpoints; compiled refusal passes, preserving the new fixture-enabled/run-mode invariant. |
| Early-return position lacks a regression tripwire | medium | Mock flag assertions do not prevent future resource setup above the return. Patch: Architecture checks the validation-only block returns before all Add* security/resource and Build/Run calls; Architecture 57/57 passed. |
| Offline mismatch probes allocate sockets | low | Synthetic config does not need a live allocated port. Simple patch: use three deterministic distinct unused values in configuration-only mode; Python rejects any allocator call. |
| Partial aggregate guard evidence absent on failure | low, rejected | This predates the increment. Every attempted command retains sanitized exact exit/output logs, including the initial failed offline attempt. Structured aggregation of partial/interrupted outcomes adds an evidence-format mechanism without recovering missing required results. Qualify guards.json as completed-suite evidence; retain the existing failure logs. |
| Original ledger entry should be modified | false | The ledger is append-only under this workflow; historical evidence must remain. The appended Resolution explicitly links the original completed spec and records final verification. Reviewer withdrew this finding after checking that policy. |

All patch groups are verified; no new deferred work was accepted. This scoped spec is done only for startup hardening. Parent Story 3.6/Epic 3 remain in progress; legacy/deployed release gates remain FAIL.

Final LaunchReadiness after evidence/spec edits passed 13/13 in 0.137s, exit 0; git diff --check passed. Final source/index/gitlink preservation hashes match the baseline. Final Aspire/Dapr/fixture-container/temporary-component inventories are empty. Full audit and exact evidence paths are in the test summary.
