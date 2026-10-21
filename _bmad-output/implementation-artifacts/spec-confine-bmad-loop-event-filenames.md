---
title: 'Confine BMAD Loop event filenames'
type: 'bugfix'
created: '2026-10-21'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The next unresolved deferred-work item reports that BMAD Loop hook filenames interpolate task identifiers and event names without excluding path separators. A temporary-directory probe confirms that a filename such as `123-task/../../escaped-Stop.json` writes outside the event directory when its intermediate directory exists, including through the POSIX directory-descriptor writer.

**Approach:** Require a single portable filename at the event writer boundary before any filesystem work. Reject path separators, Windows drive/stream syntax, NUL, and empty or dot names through the existing quiet `OSError` handling. Preserve ordinary filenames and event payloads. Add regression coverage, close the selected ledger item, then commit and push on main as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation baseline: `f71425c897e988a219ff37c9279409ca7980e28b`; main and working tree were clean. This is a freeform tooling follow-up; sprint status is unchanged.
- Code map: `.bmad-loop/bmad_loop_hook.py` builds filenames in `main` and publishes through `_write_event`, using either POSIX `dir_fd` open/rename or path-based open/replace. Validate once at the sink before directory creation so both branches are protected. Preserve the existing redirect checks, exclusive temporary-file creation, private mode, atomic publish, stdin decoding, environment selection, and payload metadata.
- A literal `..` inside a basename does not traverse; the demonstrated escape requires a separator and an existing intermediate directory. Reject unsafe names rather than rewriting identifiers, avoiding collisions or assumptions about the external orchestrator's filename parsing. Check both slash forms regardless of the host platform, and colon for Windows drive-relative paths and alternate data streams. NUL must become `OSError` rather than an uncaught `ValueError`.
- Extend `scripts/tests/test_bmad_loop_hook.py` using stdlib unittest, isolated temporary directories, and the existing real subprocess conventions. Exercise unsafe task IDs and event arguments through `main`, direct writer traversal with pre-created intermediate directories and outside sentinels, rejection before filesystem work, normal complete event publication on both writer branches, and no temporary-file leftovers. Simulate the path-based branch on POSIX without claiming native Windows execution.
- Scope: the hook, its existing test file, this spec, and the selected ledger resolution. No dependency, domain persistence, API, host, agent instruction, or other deferred-item changes. No unresolved intent gap; external commit/push is explicitly authorized.
- Verification: run the focused hook suite and existing `scripts/tests` discovery. Demonstrate in disposable copies that bypassing filename validation is detected, including both separator forms and the real traversal case. Independently review the patch and triage all findings before committing. Existing CI already discovers this suite.
- Repository baseline: `aspire ps --format Json --non-interactive` reported no hosts. Started the exact Timesheets AppHost with `--no-build` and Debug/source-reference environment settings. Filtered `aspire describe` reported both projects and their Dapr executables Running/Healthy. The exact AppHost stopped successfully before code edits. This reuses existing compiled outputs and is not fresh-source or deployed acceptance evidence.
- Implemented the guard at `_write_event` before redirect checks or directory creation. Added five regression methods, including real subprocess refusal for unsafe task/event values under legacy and explicit event directories, normal Unicode/dot/underscore identifiers, direct malformed-name refusal, concrete outside-file traversal, and complete publication without leftover temporary files on both writer paths. The path-based branch is forced by clearing `os.supports_dir_fd` in the test only.
- Red/green verification: before the guard, the expanded focused suite failed the concrete traversal cases on both writer paths and unsafe-name checks. With the guard, `python3 -B -m unittest discover -s scripts/tests -p test_bmad_loop_hook.py -v` passes 13 tests; `python3 -B -m unittest discover -s scripts/tests -v` passes all 80 tests. The final `aspire ps` listing is empty. No C# source or build graph changed.
- Five disposable-copy mutations are detected: removing the guard, allowing forward slash, allowing backslash, allowing colon, and allowing NUL. Each expanded focused suite exits 1 with explicit assertion failures; no mutation changes the checkout. `git diff --check` passes.
- Pre-commit remote check: `git fetch --no-recurse-submodules origin main` succeeds. HEAD and origin/main both resolve to `f71425c897e988a219ff37c9279409ca7980e28b`; divergence is 0/0. No submodule pointers or contents changed.
- Review corrections narrow the writer docstring to filename confinement, rename the successful-write test to describe its actual end-state assertions, and explicitly skip directory-descriptor subcases when their platform capabilities are unavailable. Added redirected-directory and pre-existing-temporary-file sentinel tests on both writer paths; successful POSIX writes also assert no group/other permissions. The focused suite now passes 15 tests with no skips on this Linux checkout. Native Windows execution remains unclaimed.
- Final verification: `python3 -B -m unittest discover -s scripts/tests` passes all 82 tests. The existing release-tooling HTTPError cleanup ResourceWarning is non-failing. The independent reviewer confirmed all four corrections and reran the 15 focused tests successfully with no skips. Nothing newly deferred. The Conventional Commit message passes `npm exec --no -- commitlint --edit <temporary-message-file>`; whitespace checks pass.

## Review Triage Log

- BH1 — low, patch: the docstring called every accepted name portable although OS-specific invalid characters remain delegated to filesystem errors. Narrowed the docstring to single-filename confinement; slash, backslash, drive/stream syntax, NUL, and dot-name rejection remain explicit. The existing quiet `OSError` policy handles other native filesystem restrictions.
- BH2 — low, patch: the successful-write test claimed atomicity while checking only final contents and temporary-file absence. Renamed it to describe those verified outcomes. Atomic publication code is unchanged; no new atomic-observation claim is made.
- BH3 — medium, patch: unsupported systems could run the path-based writer twice under different labels. Directory-descriptor subcases now explicitly skip when `O_NOFOLLOW`, `O_DIRECTORY`, or required descriptor-relative operations are unavailable; fallback subcases still execute.
- BH4 — low, patch: the preservation constraints had no focused regression checks for redirected event directories, exclusive temporary creation, or private file access. Added target/sentinel preservation cases on both writer paths and POSIX group/other permission assertions. Tests accommodate unavailable directory symlinks explicitly.
