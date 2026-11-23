---
title: 'Recheck the five deployed magic-link deferrals'
type: 'chore'
created: '2026-11-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 'f6110cbfa52c2a5dec831892ed8d57deb3ee3657'
---

<frozen-after-approval reason="offline scope supported by current release evidence">

## Intent

Reassess DW-86, DW-113, DW-119, DW-132 and DW-145 on the current Timesheets and
Platform main revisions. Use the Platform-owned enrollment preparer, hosted
fixtures and publication records to identify evidence that the deployment owner
can act on. Record the result in the qualification handoff and link it from
the deployed draft. A gate can close only with its required installed-release
evidence.

</frozen-after-approval>

## Implementation Notes

Platform's newer signed-record tooling leaves the Timesheets proposal and
Development binding at the same Git objects. The exact Platform pins reproduce
the blocked plan twice and identify the discrepancy between Platform's
enrollment source pin and the current Timesheets candidate. The handoff retains
the reproducible preparation result, source identities and per-item evidence
requirements. The deployed qualification draft remains open.

From the standalone Platform checkout, set `timesheets_root` to a clean
checkout at the baseline commit. Create a fresh output directory outside both
checkouts with real directory parents.

```bash
verification_dir=$(mktemp -d)
python3 eng/timesheets-deployment/prepare.py \
  --source-root "$timesheets_root" --output "$verification_dir/plan.json"
python3 eng/timesheets-deployment/prepare.py \
  --source-root "$timesheets_root" --output "$verification_dir/reproduced.json"
cmp "$verification_dir/plan.json" "$verification_dir/reproduced.json"
```

Using the clean Timesheets candidate identified above, both invocations exited
zero and `cmp` confirmed equal files. Platform's own direct references stayed
at their recorded pins. The inspection used GitHub's deployment status and
release APIs and made no cluster requests.

In the same standalone Platform checkout, run the following with
`HELM_BIN` set to Helm `4.2.0` from Platform's pinned toolchain. The chart
test looks for a sibling EventStore checkout, so a `../Hexalith.EventStore`
symlink in the disposable workspace pointed to Platform's direct
EventStore pin `d810b7a9a4db5454783a5eb776d60b708264ea45`.

```bash
HELM_BIN="$helm_bin" PYTHONDONTWRITEBYTECODE=1 \
  /usr/bin/python3 -m unittest discover -s eng/timesheets-deployment -p 'test_*.py'
```

All 63 tests passed. The first run from the umbrella lacked the direct fixture
inputs and Helm. The bundled Python also lacked `fcntl.F_ADD_SEALS`.
The standalone pins, explicit Helm executable, system Python and sibling
EventStore path supplied those prerequisites for the passing run.

## Review Triage Log

- Medium, patched. The prepared plan reports `owner-complete-declaration` as
  missing. The handoff now names the required bindings, retention fields and
  hosting backlog.
- Medium, patched. DW-113 explicitly requires observation of the installed
  identity source and its active EventStore trust binding.
- Medium, patched. DW-119 stays open if either measured observation is missing
  or late.
- Medium, patched. DW-132 requires pending-delivery accounting and verification
  of recovery.
- Low, patched. DW-86 conditions remediation approval on a certified inventory
  identifying affected histories.
- Low, patched. Reproduction uses a fresh external directory, two unique output
  paths and a byte comparison.
