---
title: 'Redact telemetry and API-key credentials from local diagnostics'
type: 'bugfix'
created: '2026-10-18'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The local magic-link diagnostic sanitizer can retain or display OTLP exporter header credentials and API keys. A prior monitoring inspection exposed this gap, and the user selected this deferred item for implementation.

**Approach:** Extend the existing sanitizer to remove OTLP header values (including signal-specific settings) and API-key values in plain headers, environment assignments, structured output, escaped JSON, and multiline values. Recognize structured environment-variable name/value pairs as credential-bearing records. Preserve useful non-sensitive progress and the existing URL, JWT, signing, channel, and domain credential protections. Add synthetic regressions through the sanitizer and command-evidence paths, and record the result in the deferred ledger.

</frozen-after-approval>

## Implementation Notes

- Planning: no unresolved intent gaps, irreversible operations, new dependencies, or public API changes. The bounded footprint is the diagnostic script, its existing Python test module, this spec, and the deferred ledger.
- Code map: `scripts/run_isolated_magic_link_dapr.py::sanitized` owns credential filtering; reuse its complete JSON-value decoding and line/indentation fallback. Cover both direct fields and named environment-variable records without starting an AppHost or inspecting runtime credentials.
- Code map: `scripts/test_isolated_magic_link_dapr.py` provides synthetic structured/escaped/multiline credential cases and injected command execution. Verify retained command logs as well as returned sanitizer text; keep safe progress assertions.
- Verification baseline: `python3 -m unittest discover -s scripts -p 'test_isolated_magic_link_dapr.py'` passed all 56 tests. `aspire ps --non-interactive --format Json` identified an already-running Works AppHost; the fixture's preflight refuses other running AppHosts. No Timesheets runtime journey is required for this offline sanitizer change.
- Scope: projection/migration hardening, BMAD tooling maintenance, and guidance/launch reconciliation remain separate deferred goals. No domain, AppHost, platform, deployment, historical evidence, or launch-verdict changes are included.
- Implementation: shared the credential-name vocabulary between JSON-value and line filtering; added OTLP headers, signal-specific headers, and API-key spellings. Complete named JSON records are scrubbed regardless of field order, including nested values, while ordinary endpoint records survive.
- Regressions: five new tests cover plain headers/assignments, bytes, direct/escaped/nested JSON, name/key-value records, multiline YAML, and saved command arguments/stdout/stderr. Before the fix the 61-test run failed on 82 subcases; after the fix all 61 tests passed in 7.956 seconds.
- Review patches: track individual JSON object boundaries and decode property names, including nested encoded JSON strings. Scrub incomplete named records, reordered/equal-indent YAML mappings, and complete multiline quoted assignments. Preserve harmless braced log text and later progress. Seven additional tests cover these cases and interrupted byte-stream evidence; all 67 tests pass.
- Final verification: all 68 tests pass in 7.073 seconds with the same focused unittest command; `git diff --check` passes. The final total supersedes intermediate counts above and includes the EOF quoted-string/brace regression. Follow-up review confirmed the original fixes and identified YAML comment/quoted-key variants, now covered and fixed. No additional deferred goals were introduced.

## Review Triage Log

- BH1 — medium, patch: the truncated-record fallback could consume a harmless earlier `{phase}` plus all subsequent progress. JSON token/object tracking now restricts removal to the credential-bearing object; the reported case has a passing regression.
- BH2 — medium, patch: named YAML records retained values before the name or at equal indentation. Mapping-block removal now handles either order while respecting list/parent/plain-log boundaries; all three reported layouts have passing regressions.
- BH3 — medium, patch: encoded JSON property names bypassed literal matching. Decode property names and recursively scrub encoded JSON strings; direct, top-level encoded, and embedded-document cases pass.
- BH4 — medium, patch: multiline quoted assignments retained unindented continuation lines. Quote-aware value removal now handles double- and single-quoted assignments; regressions verify all credential lines disappear and subsequent progress survives.
- BH5 — low, patch: the ledger needed a resolution linked to the delivered spec and verification. An appended resolution now records this local increment without editing historical entries.
- BH6 — medium, patch: comments between YAML record fields stopped the mapping scan. Boundary detection now skips comments in both directions; value-first and name-first regressions pass.
- BH7 — medium, patch: quoted YAML keys bypassed record recognition. The field recognizer now accepts quoted keys; the reported value-first case passes.
