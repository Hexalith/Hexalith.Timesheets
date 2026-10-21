---
title: 'Keep generated Codex hooks local to each checkout'
type: 'bugfix'
created: '2026-10-21'
status: 'in-progress'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The first deferred-work item records that tracked Codex hooks invoke a deleted story automator and embed machine-specific checkout and executable paths. Fresh checkouts inherit broken commands.

**Approach:** Treat `.codex/hooks.json` as locally generated BMAD configuration: stop tracking it, ignore that exact root path, and document generation through the installed BMAD CLI. Preserve the current checkout's installed Stop and SessionStart relay registrations while removing the obsolete automator entry. Resolve this ledger item, verify local relay behavior and fresh-checkout setup, and commit and push the completed change on `main` as requested.

</frozen-after-approval>

## Implementation Notes

- Investigation baseline: `f38d898be035e5186f38700d46064e11759d6e39`; clean `main`, synchronized with `origin/main` after fetch.
- Route facts: no intent gaps; no unapproved irreversible operations; small footprint limited to `.codex/hooks.json` tracking/local cleanup, `.gitignore`, README setup, this spec, and the deferred ledger. No domain, AppHost, dependency, or sibling edits.
- Installed BMAD `install._installed_relay_parts` requires an absolute executable; `codex_trust._commands` requires the exact installed relay command. PATH, HOME, and copied-script substitutions cannot preserve that validation. Keeping generated absolute commands local avoids duplicate relay registrations during worktree provisioning.
- Official OpenAI hook documentation confirms commands run with the session cwd and hooks may be project-local: https://learn.chatgpt.com/docs/hooks . BMAD's installed CLI relay is independent of checkout cwd.
- Plan: remove the tracked hook file from Git while retaining a corrected local copy; ignore only `/.codex/hooks.json`; document `bmad-loop init --cli codex --no-skills` from each checkout root. Existing `.codex/config.toml` remains tracked.
- Verification: inspect index/ignore behavior; run both real local relay commands with isolated BMAD event directories and without orchestration environment; exercise documented init in a relocated temporary checkout with spaces; run the existing Python tooling suite and `git diff --check`. No permanent tests or .NET runtime run are needed for this reversible developer-tool configuration change.
- Implemented the planned five-file change; the ignored local hook JSON retains exactly one installed command per event and no automator reference. README also explains upgrading existing checkouts and trusting generated commands locally.
- Verification passed: index excludes `.codex/hooks.json` and retains `.codex/config.toml`; exact ignore matches; setup ran twice in a temporary Git checkout with spaces and produced no duplicates or skill copies; both local and freshly generated configurations emitted one Stop and one SessionStart event from nested directories and made silent no-ops without orchestration variables. Temporary artifacts were cleaned automatically.
- `python3 -m unittest discover -s scripts/tests -v`: 67 passed. `git diff --check` and `git diff --cached --check`: passed. An existing Python HTTPError ResourceWarning appeared without failures. No domain/runtime acceptance is inferred.
- Concurrent submodule changes appeared after the clean initial check. They are outside this increment and will remain unstaged and untouched.
