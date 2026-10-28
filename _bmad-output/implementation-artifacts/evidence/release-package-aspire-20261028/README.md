# Release package qualification evidence

This is an evidence bundle, not production tooling. No compiled assemblies are included; `release-build-artifacts.json` retains their 2,021 hashes. CI publishes Release assembly paths but no assembly hash manifest. Original CI ZIPs, API responses and logs bind successful verification to the published source; these are not a reproducible-build attestation.

Run the portable, read-only independent verifier from any directory:

```bash
python3 /path/to/release-package-aspire-20261028/verify-retained-evidence.py.txt /path/to/release-package-aspire-20261028
```

It reads the original ZIP directly, verifies the archive digests against source-bound raw API metadata, checks all six TRX inventories/counters/serial intervals/Release paths and the exact nine unique claims variants, and cross-checks the retained derived suite, source-provenance and final-run summaries against raw inputs. When run inside this repository it also checks final report/ledger hashes from the preservation receipt. It performs no builds, tests, network access, extraction, runtime operations or writes.

`ci/audit-parse-final.py` and other recorded helpers are historical transcripts of the executed audit. The original parser requires `/tmp/timesheets-release-evidence-20261028/ci/audit-blocking-test-results/` extracted from `ci/audit-blocking-test-results.zip`; it writes its derived summary and labels inputs using the original source/run constants. Do not treat that historical helper alone as source attestation. Use the portable verifier above for independently checked artifact/source binding and complete expected variant validation.

Reconstruction map for derived records:

- `ci/audit-final-suite-audit.json`: original `audit-blocking-test-results.zip` → parse each TRX Counters, Times and UnitTestResult rows; order by the six configured projects; require non-overlap and the exact five accessor/two cleanup/two malformed variants. The portable verifier independently rederives and compares these fields and totals.
- `ci/audit-release-suite-provenance.json`: `checkout-source.json` full published gitlink list + `ci/audit-build-job-final.log` checkout lines + raw TRX TestMethod codeBase/Times + pinned `ci/audit-domain-ci.yml` six command expansions. The verifier independently checks source, all thirteen root links, Release paths, coverage flag and exact suite inventory against these retained inputs; expanded commands are inspectable in the workflow/log.
- `ci/audit-final-summary.json`: raw `ci/audit-run-final.json` and `ci/audit-jobs-final.json` → source/run/conclusion/timestamps/job identities and conclusions; log and artifact digests are separately retained. The verifier independently compares these core fields. Additional explanatory text in the summaries remains authored interpretation, not API output.

The original incorrect coverage inference, in-progress 404 log response, hash-comparison assumption and subsequent corrections remain retained. Five skips consist of three opt-in runtime/performance facts and two unconditional placeholder facts; enabling flags cannot execute the placeholders. Historical failures, launch FAIL, deployed/native gates and unclassified process disappearances remain unchanged.
