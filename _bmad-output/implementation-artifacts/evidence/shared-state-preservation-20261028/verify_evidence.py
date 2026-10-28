"""Offline artifact verification; never imports or invokes live audit helpers."""
import contextlib
import gzip
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess
import sys

base=Path('_bmad-output/implementation-artifacts/evidence/shared-state-preservation-20261028')
source=base/'analyze_history.py'
expected=(base/'historical-analysis.json').read_bytes()
for options in ([],['-O'],['-OO']):
    result=subprocess.run([sys.executable,*options,str(source)],capture_output=True,check=True)
    if result.stdout!=expected:
        raise RuntimeError('Historical reproduction mismatch')
negative=0
text=source.read_text()
marker="retention=json.loads((base/'retention-map.json').read_text())"
for field,wrong in [('archive_sha256',"'0'*64"),('sha256',"'0'*64"),('bytes',"-1")]:
    altered=text.replace(marker,marker+f"\nretention['files'][0]['{field}']={wrong}",1)
    for optimization in (0,1,2):
        try:
            with contextlib.redirect_stdout(io.StringIO()):
                exec(compile(altered,str(source),'exec',optimize=optimization),{})
        except ValueError:
            negative+=1
        else:
            raise RuntimeError('Corrupt integrity evidence was accepted')
source_marker="source_after=gzip.decompress((run/'source-after.json.gz').read_bytes())"
altered=text.replace(source_marker,source_marker+"+b'changed'",1)
for optimization in (0,1,2):
    try:
        with contextlib.redirect_stdout(io.StringIO()):
            exec(compile(altered,str(source),'exec',optimize=optimization),{})
    except ValueError:
        negative+=1
    else:
        raise RuntimeError('Changed source snapshot was accepted')
json_paths=list(base.glob('*.json'))
python_paths=list(base.glob('*.py'))
for path in json_paths:
    json.loads(path.read_text())
for path in python_paths:
    compile(path.read_bytes(),str(path),'exec')
checks=json.loads((base/'checks.json').read_text())
for command in checks['commands']:
    if 'executed_source_sha256' in command:
        payload=(base/command['retained_source']).read_bytes()
        if command.get('source_encoding','').startswith('lossless gzip'):
            payload=gzip.decompress(payload)
        if hashlib.sha256(payload).hexdigest()!=command['executed_source_sha256']:
            raise RuntimeError('Execution source hash mismatch')
for name,field in [('runtime-admission.json','admission.json'),('runtime-cleanup.json','cleanup.json')]:
    receipt=json.loads((base/'runtime-refusal.json').read_text())
    if hashlib.sha256((base/name).read_bytes()).hexdigest()!=receipt['original_file_sha256'][field]:
        raise RuntimeError('Safe original receipt mismatch')
for command in checks['commands']:
    if command.get('result_file')=='fixture-tests.log.gz':
        payload=(base/'fixture-tests.log.gz').read_bytes()
        if hashlib.sha256(payload).hexdigest()!=command['result_archive_sha256'] or hashlib.sha256(gzip.decompress(payload)).hexdigest()!=command['result_original_sha256']:
            raise RuntimeError('Test receipt mismatch')
report=Path('_bmad-output/implementation-artifacts/tests/shared-state-preservation-investigation.md')
links=re.findall(r'\]\(([^)]+)\)',report.read_text())
for target in links:
    if not (report.parent/target.split('#')[0]).exists():
        raise RuntimeError('Missing report link')
print(json.dumps({'historical_reproductions':3,'negative_integrity_or_source_cases_refused':negative,
 'optimization_levels':[0,1,2],'json_files_parsed':len(json_paths),'python_sources_compiled':len(python_paths),
 'execution_source_hashes_match':True,'compressed_test_receipt_matches':True,
 'safe_original_receipts_match':True,'report_links_resolve':len(links),'live_services_queried':False},indent=2))
