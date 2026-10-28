"""Bounded synthetic retention/comparison measurement; no service access."""
import argparse
import hashlib
import json
from pathlib import Path
import resource
import socket
import subprocess
import sys
import time
import urllib.request

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--keys', type=int, default=829212)
parser.add_argument('--output', type=Path, default=Path('/tmp/timesheets-preservation-synthetic-benchmark.json'))
arguments = parser.parse_args()
if arguments.keys < 1 or arguments.keys > 829212:
    raise RuntimeError('Synthetic population must stay within the historical count')
MEMORY_CAP = 1024 * 1024 * 1024
resource.setrlimit(resource.RLIMIT_AS, (MEMORY_CAP, MEMORY_CAP))
for candidate in (Path(__file__).resolve().parent, Path.cwd()):
    result = subprocess.run(['git', 'rev-parse', '--show-toplevel'], cwd=candidate,
                            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
    if result.returncode == 0:
        root = Path(result.stdout.strip())
        break
else:
    raise RuntimeError('Run inside the repository or retain this script within it')

def no_service(*_args, **_kwargs):
    raise RuntimeError('Synthetic benchmark forbids service access')

socket.create_connection = no_service
urllib.request.urlopen = no_service
sys.path.insert(0, str(root / 'scripts'))
import run_isolated_magic_link_dapr as fixture

def memory_summary():
    return {line.split(':', 1)[0]: int(line.split(':', 1)[1].strip().split()[0])
            for line in Path('/proc/self/status').read_text().splitlines()
            if line.startswith(('VmPeak:', 'VmSize:', 'VmHWM:', 'VmRSS:'))}

initial_memory = memory_summary()
initial_rusage_maxrss = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
started_wall = time.time_ns()
started = time.monotonic()
comparison = fixture.SharedComparison()
build_times = []

def make_snapshot():
    begin = time.monotonic()
    snapshot = fixture.SharedSnapshot(comparison)
    snapshot.redis_identity = comparison.fingerprint(b'redis-identity', b'synthetic-server')
    snapshot['scheduler'] = fixture.scheduler_fingerprint(
        {'header': {'cluster_id': '1', 'member_id': '2', 'revision': '3', 'raft_term': '1'}}, snapshot)
    for database in ('0', '13', '14'):
        snapshot.persistent[database] = {}
        snapshot.expiring[database] = {}
        snapshot.captured_at[database] = 1000
        snapshot['redis'][database] = {'keys': 0, 'persistent_keys': 0, 'expiring_keys': 0}
    persistent = snapshot.persistent['0']
    for index in range(arguments.keys):
        # Stand-ins for Lua's existing digest rows, processed with the actual
        # comparison method. No RESP payload or key-reading operation exists.
        key_digest = hashlib.sha1(('synthetic-key-' + str(index)).encode()).hexdigest().encode()
        value_digest = hashlib.sha1(('synthetic-value-' + str(index)).encode()).hexdigest().encode()
        persistent[comparison.fingerprint(b'redis-key', key_digest)] = comparison.fingerprint(b'redis-value', value_digest)
    snapshot['redis']['0'].update(keys=len(persistent), persistent_keys=len(persistent))
    build_times.append(time.monotonic() - begin)
    return snapshot

status = 'incomplete'
try:
    before = make_snapshot()
    after = make_snapshot()
    begin_compare = time.monotonic()
    aggregate = fixture.shared_preservation(before, after)
    compare_seconds = time.monotonic() - begin_compare
    if not aggregate['matches']:
        raise RuntimeError('Identical synthetic snapshots did not match')
    status = 'passed'
except MemoryError:
    compare_seconds = None
    status = 'memory_cap_reached'

report = {'status': status, 'started_unix_ns': started_wall, 'completed_unix_ns': time.time_ns(),
          'argv': sys.argv, 'keys_per_snapshot': arguments.keys, 'retained_snapshots': 2,
          'memory_cap_bytes': MEMORY_CAP, 'build_seconds': build_times, 'comparison_seconds': compare_seconds,
          'total_seconds': time.monotonic() - started, 'subprocess_peak_rss_kib': memory_summary()['VmHWM'],
          'process_start_memory_kib': initial_memory, 'process_final_memory_kib': memory_summary(),
          'rusage_maxrss_initial_kib': initial_rusage_maxrss,
          'rusage_maxrss_final_kib': resource.getrusage(resource.RUSAGE_SELF).ru_maxrss,
          'rlimit_as_bytes': list(resource.getrlimit(resource.RLIMIT_AS)),
          'source_sha256': hashlib.sha256((root / 'scripts/run_isolated_magic_link_dapr.py').read_bytes()).hexdigest(),
          'benchmark_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
          'measurement_scope': 'Two retained keyed persistent maps, empty TTL maps, and the actual aggregate comparator.',
          'excluded': ['Live service I/O', 'Redis Lua execution', 'RESP decoding/row-buffer peak', 'Python row validation',
                       'Scheduler populated range cost'],
          'limits': ['Synthetic observation only; not a full-reader memory bound, shared-state timeout qualification, or runtime acceptance.',
                     'No real service reads or writes; memory cap failure ends measurement without escalation.']}
previous = json.loads(arguments.output.read_text()) if arguments.output.exists() else None
if previous is not None:
    report['prior_measurements'] = [dict(previous, measurement_limit='Initial getrusage peak conflicts with the address-space cap; not a qualified RSS measurement')]
arguments.output.write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
raise SystemExit(0 if status == 'passed' else 1)
