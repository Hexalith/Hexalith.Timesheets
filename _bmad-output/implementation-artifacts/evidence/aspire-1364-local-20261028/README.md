# Aspire 13.6.4 local qualification records

Fresh Debug/source restore/build passed. The sole supported security-smoke
invocation refused admission at occupied port 3501, before resources or AppHost
startup. No retry or lifecycle repair followed. Current native dashboard,
credential, persisted-journey and gateway-replacement acceptance is unfinished.

From a Timesheets checkout containing the published baseline Git objects and
updated report, run this read-only consistency check:

```bash
python3 _bmad-output/implementation-artifacts/evidence/aspire-1364-local-20261028/verify-records.py.txt _bmad-output/implementation-artifacts/evidence/aspire-1364-local-20261028 --project-root .
```

The bundle path may be an export at any directory depth. `--project-root` is
explicit because source-Git-object and report-link checks require that repository.
Add `--check-local-artifacts` only while the original isolated checkout and NuGet
cache remain available. It compares all 934 current static files plus recorded
package files against their hashes. Neither mode makes a runtime observation.

Every stored file except `manifest.json` is listed in the manifest. Gzip entries
retain both stored and decompressed hashes and original byte counts. Original
receipt `log` fields name the pre-compression files; `retention-map.json` maps
those names to the `.log.gz` files actually retained. For example:

```bash
gzip -dc _bmad-output/implementation-artifacts/evidence/aspire-1364-local-20261028/build.log.gz
```

`run-capture.py.txt`, `capture-preservation.py.txt` and `pack-evidence.py.txt`
preserve the command/snapshot/retention helpers. They have absolute scratch paths
and write outputs; they are not the read-only checker and should not be run against
historical records. The initial checker and its 30-file manifest/result are
preserved separately. Independent review found semantic gaps in that initial
checker; use the corrected final checker above. Review mutations demonstrate
checker refusals only, never native runtime ownership failures.

`collector-reconstruction.py.txt` is a later transcription of the originally
inline derived-identity and socket collection logic. Its provenance differs from
contemporaneous command receipts. It can collect a new observation, not recreate
an earlier `/proc` state. `derivation-inputs.json` maps 47 byte-checked raw assets
and seven groups of raw nuspec/cache metadata, copied later without restoring or
building again. All assets still matched their earlier recorded SHA-256. Thirty
are the new local restore; seventeen are tracked historical proof assets inherited
from dependency source trees. Native executables and whole NuGet archives are not
embedded; their recorded hashes can be compared with the original cache.

`workspace-before.json` has no observation timestamp. Its ordering before checkout
preparation follows the session's sequential capture, not an independently timed
boundary. `checkout-commands.json.gz` retains timed preparation commands. The
visible-process baseline begins after that preparation; it supplies no earlier
runtime-preservation evidence. All 22 absent PID/start identities and the earlier
51/66 and 18/22 historical disappearances remain unclassified. Later empty socket
tables neither explain nor disprove the initial bind refusal.

Historical Release verification passed before the report/ledger append. Its final
document hashes intentionally predate this change. Existing historical artifacts
are unchanged. Launch remains FAIL; all unfinished deployed and native gates stay
open. This bundle is consistency evidence, not independent runtime attestation or
a claim of continuous preservation.

The package metadata directory matches a general repository ignore rule. Its 21
metadata artifacts must be explicitly force-staged when preparing this evidence increment;
no ignore rule is changed. Before committing, this command exports only indexed
bundle bytes and checks that export at an arbitrary shallow location:

```bash
python3 _bmad-output/implementation-artifacts/evidence/aspire-1364-local-20261028/check-staged-export.py.txt --project-root .
```

It requires both the expected indexed file set and byte equality with the working
tree, then runs the final checker on those exported bytes. This checks the actual
publication candidate instead of relying on files that Git might ignore.

Nuspec metadata is also stored as lossless gzip to preserve original CRLF/whitespace bytes while keeping the staged whitespace check clean. `retention-map.json` explicitly maps those original metadata names.
