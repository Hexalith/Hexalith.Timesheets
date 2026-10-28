The first review inspected the then-current, uncommitted 104-line audit-native.py in the Timesheets worktree. Its original file bytes/hash were not retained. `original-reproduction.py` reconstructs the equivalent executed algorithm; its eight mutation functions are the same functions used in that review.

Original execution method: import the auditor via importlib.util.spec_from_file_location; save its receipt function; wrap receipt with deepcopy plus one mutation; call audit; restore the original function between cases. All eight semantic mutations printed `accepted: True`. Separately, run Python with `-O`, wrap read(native-run.json) to replace exit_code with 99, and call audit; the result printed `optimized audit with native exit 99: True`. The unmodified original audit also exactly matched retained native-audit.json. These operations were read-only and in memory.

Equivalent commands, given a saved original auditor/evidence tree:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 /tmp/timesheets-native-qualification-20261028/review-recheck/original-reproduction.py --audit /path/to/original/evidence/audit-native.py
PYTHONDONTWRITEBYTECODE=1 python3 -O /tmp/timesheets-native-qualification-20261028/review-recheck/original-reproduction.py --audit /path/to/original/evidence/audit-native.py --optimization-only
```

The manifest omission finding was initially established by reading the original checker: it never read native-receipt-manifest.json. A separate read-only script checked every original/stored hash and length in all 103 entries; there were no mismatches. No manifest-corruption mutant was executed against the original auditor. The post-fix recheck adds that executable corruption case.

Post-fix commands actually executed:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 /tmp/timesheets-native-qualification-20261028/review-recheck/recheck.py --source /home/administrator/projects/hexalith/timesheets/_bmad-output/implementation-artifacts/evidence/dashboard-native-qualification-20261028 --output /tmp/timesheets-native-qualification-20261028/review-recheck/results
```

The copied auditor SHA-256 is e1085cb7f331ed77a5ad615b4c384d93c943b987bf37a90c23ba6fe2afbf7a18. The unaltered copied baseline exits 0. All ten requested mutants exit 1. The eight semantic mutants have temporary manifest hashes/lengths recomputed after alteration; they therefore reach semantic checks instead of failing file integrity checks. Exact per-case commands, results and stderr are in results/results.json and its sibling stdout/stderr files. All copied cases remain under results/ for inspection. The source evidence tree was byte-identical before/after.

These are synthetic offline evidence-consistency checks. They supply no native execution, ownership refusal, listener transfer, or deployed acceptance evidence. No repository files, runtimes, locks or long suites were changed or operated.
