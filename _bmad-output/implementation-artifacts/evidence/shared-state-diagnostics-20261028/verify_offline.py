import argparse
import ast
import hashlib
import json
from pathlib import Path
import random
import subprocess
import sys
import time

BASELINE = 'ccf6fb1dc7469322e2c85f8fe1db4bfe5fcb28bb'
parser = argparse.ArgumentParser(description='Verify preservation diagnostics without contacting any service.')
parser.add_argument('--output', type=Path, default=Path('/tmp/timesheets-preservation-independent-verification.json'))
arguments = parser.parse_args()
for candidate in (Path(__file__).resolve().parent, Path.cwd()):
    root_result = subprocess.run(['git', 'rev-parse', '--show-toplevel'], cwd=candidate,
                                 stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
    if root_result.returncode == 0:
        ROOT = Path(root_result.stdout.strip())
        break
else:
    raise RuntimeError('Run from the repository root or retain this verifier inside its repository')
sys.path.insert(0, str(ROOT / 'scripts'))
import run_isolated_magic_link_dapr as fixture

started = time.time_ns()
source_path = ROOT / 'scripts/run_isolated_magic_link_dapr.py'
old_source = subprocess.check_output(['git', 'show', BASELINE + ':scripts/run_isolated_magic_link_dapr.py'], cwd=ROOT).decode()
new_source = source_path.read_text()

def snapshot_function(source):
    tree = ast.parse(source)
    return next(node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == 'read_only_shared_snapshot')

def script_literal(source):
    fn = snapshot_function(source)
    return next(ast.literal_eval(node.value) for node in ast.walk(fn)
                if isinstance(node, ast.Assign) and any(isinstance(target, ast.Name) and target.id == 'script' for target in node.targets))

if script_literal(old_source) != script_literal(new_source):
    raise RuntimeError('Redis key-read Lua changed')
range_body = b'{"key":"AA==","range_end":"AA=="}'
for source in (old_source, new_source):
    fn = snapshot_function(source)
    if sum(isinstance(node, ast.Constant) and node.value == range_body for node in ast.walk(fn)) != 1:
        raise RuntimeError('Scheduler range payload changed')
    commands = [node for node in ast.walk(fn) if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == 'command']
    selected = [ast.literal_eval(node.args[0]) for node in commands]
    expected = ['SELECT', 'EVAL'] if source is old_source else ['INFO', 'SELECT', 'EVAL']
    if sorted(selected) != sorted(expected):
        raise RuntimeError(('Unexpected snapshot command budget', selected))
    database_loops = [node for node in ast.walk(fn) if isinstance(node, ast.For) and isinstance(node.target, ast.Name) and node.target.id == 'database']
    if len(database_loops) != 1 or ast.literal_eval(database_loops[0].iter) != (0, 13, 14):
        raise RuntimeError('Database scope changed')
    timeouts = {node.func.attr: ast.literal_eval(keyword.value) for node in ast.walk(fn)
                if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute)
                and node.func.attr in ('create_connection', 'urlopen') for keyword in node.keywords if keyword.arg == 'timeout'}
    if timeouts != {'create_connection': 20, 'urlopen': 20}:
        raise RuntimeError('Snapshot socket/HTTP timeouts changed')

comparison = fixture.SharedComparison()
def make_snapshot(states, captured):
    snapshot = fixture.SharedSnapshot(comparison)
    snapshot.redis_identity = comparison.fingerprint(b'redis-identity', b'synthetic-server')
    snapshot.scheduler_identity = comparison.fingerprint(b'scheduler-identity', b'synthetic-scheduler')
    snapshot.scheduler_content = comparison.fingerprint(b'scheduler-content', b'empty')
    snapshot['scheduler'] = {'keys': 0, 'revision': '1'}
    persistent, expiring = {}, {}
    for key, state in enumerate(states):
        if state is None:
            continue
        value, deadline = state
        token = comparison.fingerprint(b'redis-key', str(key).encode())
        fingerprint = comparison.fingerprint(b'redis-value', value.encode())
        if deadline == -1:
            persistent[token] = fingerprint
        else:
            expiring[token] = (fingerprint, deadline)
    snapshot.persistent['0'] = persistent
    snapshot.expiring['0'] = expiring
    snapshot.captured_at['0'] = captured
    snapshot['redis']['0'] = {'keys': len(persistent) + len(expiring), 'persistent_keys': len(persistent), 'expiring_keys': len(expiring)}
    for database in ('13', '14'):
        snapshot.persistent[database] = {}
        snapshot.expiring[database] = {}
        snapshot.captured_at[database] = captured
        snapshot['redis'][database] = {'keys': 0, 'persistent_keys': 0, 'expiring_keys': 0}
    return snapshot

def permitted(old, new):
    if old is None:
        return new is None
    value, deadline = old
    if new is None:
        return deadline != -1 and deadline <= 1000
    new_value, new_deadline = new
    if deadline == -1 or new_deadline == -1:
        return old == new
    return value == new_value and abs(deadline - new_deadline) <= 2

states = [None, ('one', -1), ('two', -1), ('one', 950), ('one', 1200), ('one', 1201), ('two', 1200)]
rng = random.Random(64721)
for _ in range(1000):
    old = [rng.choice(states) for _ in range(12)]
    new = [rng.choice(states) for _ in range(12)]
    before, after = make_snapshot(old, 900), make_snapshot(new, 1000)
    result = fixture.shared_preservation(before, after)
    expected = all(permitted(a, b) for a, b in zip(old, new))
    if result['matches'] != expected:
        raise RuntimeError('Comparator differs from independent state oracle')
    p = result['redis']['0']['persistent']
    transitions = result['redis']['0']['ttl_transitions']
    if p['keys_after'] - p['keys_before'] != p['added'] - p['removed'] + transitions['expiring_to_persistent'] - transitions['persistent_to_expiring']:
        raise RuntimeError('Persistent delta accounting does not reconcile')
    public = json.dumps([before, after, result])
    for secret in (comparison._key.hex(), before.redis_identity.hex(), before.scheduler_identity.hex(), before.scheduler_content.hex()):
        if secret in public:
            raise RuntimeError('Private comparison material serialized')

for old in states:
    for new in states:
        result = fixture.shared_preservation(make_snapshot([old], 900), make_snapshot([new], 1000))
        if result['matches'] != permitted(old, new):
            raise RuntimeError('Single-key exhaustive oracle disagreement')

paths = ['scripts/run_isolated_magic_link_dapr.py', 'scripts/check_magic_link_apphost_guards.py', 'scripts/test_isolated_magic_link_dapr.py', 'docs/local-magic-link-dapr-proof.md']
protected_argv = ['git', 'diff', '--exit-code', BASELINE, '--', '.gitmodules', 'global.json',
                  'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'references',
                  '_bmad-output/implementation-artifacts/evidence/shared-state-preservation-20261028',
                  '_bmad-output/implementation-artifacts/evidence/remaining-repair-and-guidance-20261028/baseline-runtime']
protected = subprocess.run(protected_argv, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
if protected.returncode:
    raise RuntimeError('Protected historical evidence, pins or gitlinks differ from the starting revision')
previous = json.loads(arguments.output.read_text()) if arguments.output.exists() else {}
history = previous.get('verification_runs', [])
if not history and previous:
    history.append({key: value for key, value in previous.items() if key not in ('focused_tests', 'protected_verification')})
report = dict(previous)
if 'focused_tests' in previous and 'focused_test_source_snapshot_sha256' not in previous:
    report['focused_test_source_snapshot_sha256'] = previous['source_sha256']
run = {'passed': True, 'baseline_revision': BASELINE, 'argv': sys.argv, 'started_unix_ns': started,
       'completed_unix_ns': time.time_ns(),
       'source_sha256': {path: hashlib.sha256((ROOT / path).read_bytes()).hexdigest() for path in paths},
       'checks': {'redis_lua_byte_identical_to_baseline': True, 'scheduler_range_payload_identical': True,
                  'database_scope': [0, 13, 14], 'socket_http_timeouts_seconds': 20,
                  'redis_command_budget': 'one INFO server plus unchanged SELECT/EVAL for DB0/13/14',
                  'randomized_12_key_comparisons': 1000, 'exhaustive_single_key_comparisons': 49,
                  'private_material_excluded_from_json': True, 'persistent_delta_accounting': True},
       'baseline_protected_verification': {'argv': protected_argv, 'exit_code': protected.returncode},
       'limits': ['No service contacted; no runtime qualification or preservation of a live store established.',
                  'Snapshot comparison cannot establish writer ownership or intermediate writes.',
                  'INFO identity is observed on the same connection; no continuous isolation proof is claimed.'],
       'verifier_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
report.update(run)
history.append(run)
report['verification_runs'] = history
arguments.output.write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
