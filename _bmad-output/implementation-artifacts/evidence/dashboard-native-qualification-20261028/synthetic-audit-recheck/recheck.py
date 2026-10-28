"""Synthetic offline evidence-consistency recheck; never operates native runtimes."""
import argparse
import copy
import datetime
import gzip
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

RUN_RELATIVE = Path('runtime/run-4y_0l_yg')

def load_json(directory, name):
    path = directory / RUN_RELATIVE / name
    if path.exists():
        return json.loads(path.read_text())
    return json.loads(gzip.decompress(path.with_name(path.name + '.gz').read_bytes()))

def save_json(directory, name, data):
    path = directory / RUN_RELATIVE / name
    raw = (json.dumps(data, indent=2) + '\n').encode()
    if path.exists():
        path.write_bytes(raw)
    else:
        path.with_name(path.name + '.gz').write_bytes(gzip.compress(raw, mtime=0))

def update_manifest(directory):
    path = directory / 'native-receipt-manifest.json'
    data = json.loads(path.read_text())
    for name, row in data['files'].items():
        stored = (directory / name).read_bytes()
        original = gzip.decompress(stored) if name.endswith('.gz') else stored
        for prefix, payload in [('stored', stored), ('original', original)]:
            row[prefix + '_bytes'] = len(payload)
            row[prefix + '_sha256'] = hashlib.sha256(payload).hexdigest()
    path.write_text(json.dumps(data, indent=2) + '\n')

def patch_receipts(directory, patch):
    for name in ['source-before.json', 'source-after.json', 'runtime-host.json',
                 'dashboard-listener-lifecycle.json', 'gateway-process-replacement.json',
                 'recorded-process-exit.json', 'shared-after.json']:
        data = load_json(directory, name)
        before = copy.deepcopy(data)
        patch(name, data)
        if data != before:
            save_json(directory, name, data)

# The following eight functions reproduce the exact semantic substitutions
# used during the first review. The first review applied them in memory by
# wrapping module.receipt; this recheck changes temporary evidence copies.
def dirty_source(name, data):
    if name in ('source-before.json', 'source-after.json'):
        data['timesheets']['head'] = '0' * 40
        data['timesheets']['diff_sha256'] = '1' * 64
        data['timesheets']['untracked_build_inputs'] = {'bad.cs': '2' * 64}

def dcp_wrong(name, data):
    if name == 'runtime-host.json':
        for key in ('dcp', 'dcp_controller'):
            data[key]['version'] = '0.0.0'
            data[key]['sha256'] = '0' * 64

def sockets_empty(name, data):
    def clear(row):
        row['ownership']['socket_inodes'] = []
        for sample in row['ownership_observations'].values():
            sample['socket_inodes'] = []
            sample['socket_owners'] = {}
    if name == 'runtime-host.json':
        clear(data['dashboard_readiness'])
    if name == 'dashboard-listener-lifecycle.json':
        for phase in ('before_journey', 'after_journey'):
            clear(data[phase])

def wrong_starts(name, data):
    def change(row):
        for sample in row['ownership_observations'].values():
            sample['expected_identity']['start'] = 'BAD'
            sample['owner_identities'][0]['start'] = 'BAD'
            for item in sample['expected_process_observations'].values():
                item['start'] = 'BAD'
    if name == 'runtime-host.json':
        change(data['dashboard_readiness'])
    if name == 'dashboard-listener-lifecycle.json':
        for phase in ('before_journey', 'after_journey'):
            change(data[phase])

def unsafe_health(name, data):
    def change(row):
        row['loopback'] = False
        row['scheme'] = 'http'
        row['path'] = '/different'
        row['connect_host'] = '198.51.100.1'
    if name == 'runtime-host.json':
        change(data['dashboard_readiness'])
    if name == 'dashboard-listener-lifecycle.json':
        for phase in ('before_journey', 'after_journey'):
            change(data[phase])

def gateway_same(name, data):
    if name == 'gateway-process-replacement.json':
        data['after'] = copy.deepcopy(data['before'])

def exits_missing(name, data):
    if name == 'recorded-process-exit.json':
        keep = {3875660, 3875657, 3936843}
        data['processes'] = [row for row in data['processes'] if row['pid'] in keep]

def preservation_broken(name, data):
    if name == 'shared-after.json':
        data['redis']['0']['persistent_sha256'] = '0' * 64
        data['scheduler']['sha256'] = '1' * 64

SEMANTIC = [
    ('wrong-dirty-source', dirty_source),
    ('wrong-dcp-identities', dcp_wrong),
    ('empty-listener-inodes', sockets_empty),
    ('contradictory-owner-starts', wrong_starts),
    ('nonloopback-health-endpoint', unsafe_health),
    ('unchanged-gateway-identity', gateway_same),
    ('incomplete-process-exit-set', exits_missing),
    ('contradictory-shared-state', preservation_broken),
]

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    results = []
    source = args.source.resolve()
    original_hashes = {
        str(path.relative_to(source)): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in source.rglob('*') if path.is_file()
    }
    def execute(label, mutate=None, optimize=False, expected=0, rehash=False):
        directory = args.output / label
        if directory.exists():
            raise RuntimeError('Refusing to overwrite previous evidence: ' + str(directory))
        shutil.copytree(source, directory)
        if mutate:
            mutate(directory)
        if rehash:
            update_manifest(directory)
        command = [sys.executable, *(['-O'] if optimize else []), str(directory / 'audit-native.py')]
        environment = dict(os.environ, PYTHONDONTWRITEBYTECODE='1')
        environment.pop('PYTHONOPTIMIZE', None)
        started = datetime.datetime.now(datetime.timezone.utc).isoformat()
        completed = subprocess.run(command, env=environment, capture_output=True, text=True, timeout=20)
        (args.output / (label + '.stdout')).write_text(completed.stdout)
        (args.output / (label + '.stderr')).write_text(completed.stderr)
        accepted = completed.returncode == 0
        results.append({
            'label': label, 'command': command,
            'environment': {'PYTHONDONTWRITEBYTECODE': '1', 'PYTHONOPTIMIZE': 'unset'},
            'started_utc': started, 'exit_code': completed.returncode,
            'expected': 'accept' if expected == 0 else 'reject',
            'matched_expectation': accepted == (expected == 0),
            'temporary_manifest_recomputed': rehash,
            'stdout': label + '.stdout', 'stderr': label + '.stderr',
        })
        print(label + ': ' + ('accepted' if accepted else 'rejected') + ', exit ' + str(completed.returncode))
    execute('baseline')
    def optimization(directory):
        path = directory / 'native-run.json'
        data = json.loads(path.read_text());data['exit_code'] = 99
        path.write_text(json.dumps(data, indent=2) + '\n')
    execute('optimized-native-exit-99', optimization, optimize=True, expected=1)
    def missing_manifest_integrity(directory):
        path = directory / RUN_RELATIVE / '03-docker-version.log.gz'
        path.write_bytes(gzip.compress(gzip.decompress(path.read_bytes()) + b'\nSynthetic corruption\n', mtime=0))
    execute('manifest-original-bytes-altered', missing_manifest_integrity, expected=1)
    for label, patch in SEMANTIC:
        execute(label, lambda directory, patch=patch: patch_receipts(directory, patch), expected=1, rehash=True)
    final_hashes = {
        str(path.relative_to(source)): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in source.rglob('*') if path.is_file()
    }
    report = {
        'scope': 'Synthetic offline evidence-consistency checks only; no native execution, failure or transfer.',
        'source': str(source),
        'auditor_sha256': original_hashes['audit-native.py'],
        'source_tree_unchanged': original_hashes == final_hashes,
        'original_findings': 10,
        'mutants_rejected': sum(row['expected'] == 'reject' and row['exit_code'] != 0 for row in results),
        'all_expectations_met': all(row['matched_expectation'] for row in results),
        'commands': results,
    }
    (args.output / 'results.json').write_text(json.dumps(report, indent=2) + '\n')
    return 0 if report['all_expectations_met'] and report['source_tree_unchanged'] else 1

if __name__ == '__main__':
    raise SystemExit(main())
