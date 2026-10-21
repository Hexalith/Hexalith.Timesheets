---
title: 'Pin BMAD Loop malformed-input behavior'
type: 'chore'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The next unresolved deferred-work entry claims the BMAD Loop hook crashes on non-UTF-8 stdin. Investigation disproves the claim: `UnicodeDecodeError` inherits from the already-caught `ValueError`, and real subprocess probes emit a complete event without a hook failure. This behavior has no repository regression coverage.

**Approach:** Add subprocess regression tests for malformed raw input under strict UTF-8 and surrogateescape decoding, with a valid Unicode control and inactive-session checks. Verify exit status, quiet output, event contents, and temporary-file cleanup using isolated directories. Correct and close the selected ledger entry with the evidence. Commit and push this increment on main as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation baseline: `ceaba6eae07eec74b2dd9863744d4355c973f9c8`; main and working tree were clean. This is a tooling follow-up, not an epic story; sprint status is unchanged.
- Code map: `.bmad-loop/bmad_loop_hook.py:180` contains the active-session guard and `json.load(sys.stdin)` fallback. Its `except (json.JSONDecodeError, ValueError)` already handles decoding errors. Strict decoding drops malformed payload metadata; surrogateescape permits invalid bytes inside JSON strings as escaped surrogates in the serialized event. Preserve both existing behaviors.
- Add `scripts/tests/test_bmad_loop_hook.py`, using the stdlib unittest and temporary-directory conventions in `scripts/tests/test_release_tooling.py`. Invoke the actual relay with byte input and a subprocess timeout; clear inherited orchestration variables and set decoder modes explicitly. Existing CI discovery in `.github/workflows/ci.yml` and `npm run test:release-tooling` already include this location.
- Scope: the new test file, this spec, and one `resolution_note` in `_bmad-output/implementation-artifacts/deferred-work.md`. No runtime hook, dependency, domain state, API, host, agent instructions, or other deferred item changes. No unresolved intent gaps or irreversible actions beyond the explicitly requested commit/push.
- Verify the focused suite and existing `scripts/tests` discovery; demonstrate that removing the `ValueError` handler in a disposable copy fails the strict-decoding regression. Review independently before committing.
- Implemented six subprocess tests covering strict malformed bytes, surrogateescape malformed JSON and string metadata, malformed/empty/non-object JSON, valid Unicode under both decoders, and each missing orchestration guard variable. Tests inspect the complete serialized event and require quiet exit 0; inherited `BMAD_LOOP_*` values are removed before using temporary run directories.
- The focused command `python3 -B -m unittest discover -s scripts/tests -p test_bmad_loop_hook.py -v` passes all six tests. The suite is included in existing Python discovery without a CI change.
- Repository baseline check: `aspire ps --format Json --non-interactive` initially returned no running hosts. Started the exact Timesheets AppHost through `aspire start --no-build` with Debug/source-reference environment settings; `aspire describe` reported its project resources Running/Healthy. Stopped that exact host successfully before test edits. This reused existing compiled assets and supplies no current-source build, persistence, or deployed acceptance evidence.
- Verification: `python3 -B -m unittest discover -s scripts/tests -v` passes all 73 tests. An existing HTTPError cleanup ResourceWarning in the release-tooling suite remains non-failing. A disposable-copy mutation removing only `ValueError` from the relay's handler fails all three strict malformed-byte cases with `UnicodeDecodeError`; the source checkout is untouched by the probe. `git diff --check` passes, and the post-stop Aspire process listing is empty.
- Pre-commit remote check: fetched `origin/main` without recursing into submodules; HEAD and origin/main both resolve to `ceaba6eae07eec74b2dd9863744d4355c973f9c8`, with no divergence.
- Review corrections expanded the suite to eight tests: inactive sessions now cover both missing and empty environment values, a separate open-pipe case requires exit before stdin EOF and kills/reaps a timed-out child, and malformed input reaches a distinct explicitly configured event directory under both decoders without creating the legacy run directory. Full discovery now passes 75 tests. The Conventional Commit message passes local commitlint.
- Final negative verification detects all four disposable mutations: removing the decoding handler, accepting empty orchestration values, ignoring the explicit event channel, and moving the inactive guard below stdin parsing. The last mutation times out all four open-pipe cases and the test cleanup reaps each child. Independent patch verification passes the eight focused tests, confirms all three review findings are addressed, and finds no remaining concrete problem; nothing is newly deferred.

## Review Triage Log

- BH1 — medium, patch: complete stdin did not protect the early inactive-session guard from moving below a blocking read. Added a real subprocess case with stdin held open, requiring exit before EOF for missing and empty guard variables; timeout cleanup kills and reaps the child.
- BH2 — medium, patch: missing-only environment cases allowed empty values to activate the hook undetected. Added empty values for each guard variable and assert the isolated working directory remains empty.
- BH3 — medium, patch: malformed input only exercised the legacy event directory. Added strict and surrogateescape cases using an explicit `BMAD_LOOP_EVENTS_DIR`, assert the complete event there, and require the legacy run directory to remain absent.
