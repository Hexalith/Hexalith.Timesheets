"""Run final source-mode checks with durable, independently owned receipts."""
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import subprocess
import threading
import time
import xml.etree.ElementTree as ET

ROOT = Path.cwd().resolve()
SDK = ROOT / 'references/Hexalith.EventStore'
OUTPUT = Path(__file__).resolve().parent
ENVIRONMENT = dict(os.environ, DOTNET_CLI_HOME='/tmp/dotnet-cli-home')
RESULTS = []
LOCK = threading.Lock()


def run(name, command, cwd=ROOT):
    start = time.monotonic()
    print('START', name, flush=True)
    with (OUTPUT / (name + '.log')).open('w') as log:
        process = subprocess.Popen(command, cwd=cwd, env=ENVIRONMENT,
                                   stdout=log, stderr=subprocess.STDOUT)
        (OUTPUT / (name + '-process.json')).write_text(json.dumps({
            'pid': process.pid, 'command': command, 'cwd': str(cwd),
            'started_at_unix': time.time()}, indent=2) + '\n')
        code = process.wait()
    result = {'name': name, 'command': command, 'cwd': str(cwd),
              'exit_code': code, 'seconds': round(time.monotonic() - start, 3),
              'log': name + '.log'}
    xml = OUTPUT / (name + '.xml')
    if xml.exists():
        result['assemblies'] = [dict(assembly.attrib) for assembly in
                                ET.parse(xml).getroot().iter('assembly')]
        result['xml_sha256'] = hashlib.sha256(xml.read_bytes()).hexdigest()
    with LOCK:
        RESULTS.append(result)
        (OUTPUT / 'checks.json').write_text(json.dumps(RESULTS, indent=2) + '\n')
    print('FINISH', name, code, result['seconds'], flush=True)
    return code


guard = ROOT / '_bmad-output/implementation-artifacts/evidence/five-retry-recovery-tooling-deferrals/review-fixes/run_unchanged_secret_guard.py'
if run('tracked-secret-guard', ['python3', str(guard), '--sdk-root', str(SDK),
                              '--evidence-directory', str(OUTPUT / 'secret-guard')]):
    raise SystemExit(1)
if run('sdk-projections', [str(SDK / 'tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests'),
                           '-class', 'Hexalith.EventStore.Client.Tests.Projections.*',
                           '-longRunning', '60', '-noColor', '-xml', str(OUTPUT / 'sdk-projections.xml')], SDK):
    raise SystemExit(1)
if run('restore', ['dotnet', 'restore', 'Hexalith.Timesheets.slnx',
                   '-p:Configuration=Debug', '-p:UseHexalithProjectReferences=true', '-m:1', '/nr:false']):
    raise SystemExit(1)
if run('build', ['dotnet', 'build', 'Hexalith.Timesheets.slnx', '--configuration', 'Debug',
                 '--no-restore', '-p:UseHexalithProjectReferences=true', '-warnaserror', '-m:1', '/nr:false']):
    raise SystemExit(1)


def suite(project):
    return run(project, [str(ROOT / f'tests/{project}/bin/Debug/net10.0/{project}'),
                         '-longRunning', '60', '-noColor', '-xml', str(OUTPUT / (project + '.xml'))])


projects = ['Hexalith.Timesheets.ArchitectureTests', 'Hexalith.Timesheets.Contracts.Tests',
            'Hexalith.Timesheets.Server.Tests', 'Hexalith.Timesheets.Works.Tests',
            'Hexalith.Timesheets.Projections.Tests', 'Hexalith.Timesheets.IntegrationTests']
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
    outcomes = list(executor.map(suite, projects))
raise SystemExit(1 if any(outcomes) else 0)
