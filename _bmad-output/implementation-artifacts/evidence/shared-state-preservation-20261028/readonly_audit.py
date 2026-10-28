"""Aggregate-only audit; never start or stop services or retain key-level data."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time
sys.path.insert(0, str(Path.cwd() / 'scripts'))
from local_runtime_admission import LocalRuntimeAdmission
from run_isolated_magic_link_dapr import read_only_shared_snapshot, shared_preservation, running_dapr_processes, json_output

root = Path('/tmp/timesheets-shared-state-trace-20261028')
report = {'purpose': 'Read-only observation with this fixture stopped; no historical writer attribution',
          'resources_created': 0, 'services_stopped': 0, 'raw_keys_or_values_retained': False,
          'source_revision': subprocess.check_output(['git','rev-parse','HEAD'], text=True).strip()}
def inventory():
    results = {}
    for name, command in [('dapr', ['dapr','list','--output','json']),
                          ('aspire', ['aspire','ps','--non-interactive','--format','Json'])]:
        observed = subprocess.run(command, capture_output=True, text=True, timeout=30)
        item = {'command': command, 'exit_code': observed.returncode}
        try:
            rows = json.loads(observed.stdout) if name == 'dapr' else json_output(observed.stdout)
            item['inventory_valid'] = isinstance(rows, list)
            item['count'] = len(rows) if isinstance(rows, list) else None
        except (ValueError, TypeError):
            item['inventory_valid'] = False
        results[name] = item
    results['native_dapr_count'] = len(running_dapr_processes())
    return results
try:
    with LocalRuntimeAdmission():
        report['lock_acquired'] = True
        report['inventory_before'] = inventory()
        samples = []
        snapshots = []
        for index in range(2):
            started = time.time_ns()
            snapshot = read_only_shared_snapshot()
            completed = time.time_ns()
            snapshots.append(snapshot)
            samples.append({'started_unix_ns': started, 'completed_unix_ns': completed, 'snapshot': snapshot})
            if index == 0:
                time.sleep(10)
        report['samples'] = samples
        report['preservation'] = shared_preservation(*snapshots)
        report['inventory_after'] = inventory()
        report['completed'] = True
except Exception as error:
    report['completed'] = False
    report['safe_failure_type'] = type(error).__name__
(root / 'readonly-observation.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
