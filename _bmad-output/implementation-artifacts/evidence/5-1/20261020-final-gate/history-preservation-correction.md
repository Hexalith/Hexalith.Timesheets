# Historical Receipt Preservation Correction — 2026-10-20

Parent diff verification found that the temporary evidence-publication helper had replaced three named gate rows throughout `docs/launch-readiness.md`, including the archived table. The `Tests (full suite)`, `Privacy/logging scans`, and `Projection rebuild/idempotency` rows below `#### Earlier release verification and decision table (historical)` are now restored exactly from baseline `d72899b4489aaca0b9a9b96d0ac267165920caf8`.

The new current gate table and the entire prefix above the historical release subsection remain unchanged. Both complete launch historical receipt groups now match the baseline after reversing only the intentional heading nesting. Nine additional comparisons also match: package audit evidence, historical Aspire smoke, both historical performance measured-run sections, the original 2026-06-23 QA receipt, and four original story Dev Agent Record/review suffixes. [Preservation hashes and corrected-row identities](history-preservation-correction.json) record these checks. The temporary publishing helper now restricts named gate replacements to the current release-gate section.

The artifact-check attribution is corrected to `preexisting_handoff_changes_preserved`: the sprint/spec paths were already present in the parent workflow handoff, not human/user edits. All prior compressed and retained raw receipts remain unchanged; the correction adds separate verification receipts.

Relevant verification only:

```bash
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -class '*LaunchReadinessTests' -xml _bmad-output/implementation-artifacts/evidence/5-1/20261020-final-gate/history-correction-readiness.xml
DOTNET_CLI_HOME=/tmp/dotnet-cli-home tests/Hexalith.Timesheets.ArchitectureTests/bin/Debug/net10.0/Hexalith.Timesheets.ArchitectureTests -xml _bmad-output/implementation-artifacts/evidence/5-1/20261020-final-gate/history-correction-architecture.xml
```

Readiness passed **13/13**, zero failures/skips, **0.526 s** ([XML](history-correction-readiness.xml.gz) / [log](history-correction-readiness.log.gz)). Full Architecture passed **62/62**, zero failures/skips, **18.278 s** ([XML](history-correction-architecture.xml.gz) / [log](history-correction-architecture.log.gz)). [Exact argv and UTC receipts](history-correction-verification.json) identify these separate correction runs. No runtime source, dependencies, submodules, or unrelated long suites are changed or repeated. Overall launch remains FAIL, and the initial Projection cleanup failure/retry evidence remains preserved.
