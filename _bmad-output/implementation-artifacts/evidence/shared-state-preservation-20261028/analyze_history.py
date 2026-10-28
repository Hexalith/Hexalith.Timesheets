"""Validate retained bytes and emit aggregate historical facts only."""
import gzip
import hashlib
import json
from pathlib import Path

base=Path('_bmad-output/implementation-artifacts/evidence/remaining-repair-and-guidance-20261028/baseline-runtime')
retention=json.loads((base/'retention-map.json').read_text())
def verify(condition, message):
    if not condition:
        raise ValueError(message)

for row in retention['files']:
    compressed=(base/row['archive']).read_bytes()
    raw=gzip.decompress(compressed)
    verify(hashlib.sha256(compressed).hexdigest()==row['archive_sha256'], 'Archive SHA256 mismatch: '+row['archive'])
    verify(len(raw)==row['bytes'], 'Original length mismatch: '+row['archive'])
    verify(hashlib.sha256(raw).hexdigest()==row['sha256'], 'Original SHA256 mismatch: '+row['archive'])
run=base/'baseline-runtime/run-nzdrukv5'
def load(name):
    return json.loads(gzip.decompress((run/(name+'.gz')).read_bytes()))
before=load('shared-before.json')
after=load('shared-after.json')
baseline=load('shared-baseline-preservation.json')
final=load('shared-preservation.json')
isolation=load('runtime-isolation.json')
receipt=json.loads(gzip.decompress((base/'baseline-runtime.json.gz').read_bytes()))
summary=json.loads((base/'summary.json').read_text())
verify(summary['shared_preservation']==final, 'Summary preservation mismatch')
verify(before==load('shared-baseline-before.json'), 'Admission snapshots differ')
verify(baseline['matches'] is True and final['matches'] is False, 'Preservation outcome mismatch')
verify(after['redis']['0']['persistent_keys']-before['redis']['0']['persistent_keys']==11, 'Persistent key delta mismatch')
verify(final['mismatches']==['Redis DB 0 persistent content changed'], 'Mismatch category differs')
verify(all(final['redis'][db]['persistent']['matches'] for db in ('13','14')), 'DB13/14 persistent drift')
verify(all(final['redis'][db]['expiring']['matches'] for db in ('0','13','14')), 'Expiring content drift')
verify(final['scheduler']['contents_match'] and final['scheduler']['identity_matches'], 'Scheduler drift')
verify(len({isolation[k] for k in ('redis','placement','scheduler')})==3, 'Private endpoint collision')
verify(all(str(isolation[k]).startswith('127.0.0.1:') and 20000<=int(isolation[k].split(':')[-1])<=29999 for k in ('redis','placement','scheduler')), 'Private endpoint range mismatch')
source_before=gzip.decompress((run/'source-before.json.gz').read_bytes())
source_after=gzip.decompress((run/'source-after.json.gz').read_bytes())
verify(source_before==source_after, 'Source snapshots differ')
verify(summary['source_before_after_equal'] is True, 'Summary source comparison mismatch')
report={
    'historical_source':str(base),
    'retention_manifest_sha256':hashlib.sha256((base/'retention-map.json').read_bytes()).hexdigest(),
    'retained_files_verified':len(retention['files']),
    'verification':'Compressed SHA256, decompressed byte length and decompressed SHA256 match for every file',
    'command':receipt['command'], 'environment_keys':sorted(receipt['environment']),
    'started_utc':receipt['started_utc'], 'finished_utc':receipt['finished_utc'],
    'exit_code':receipt['exit_code'], 'elapsed_seconds':receipt['elapsed_seconds'],
    'baseline_samples_equal':True, 'baseline_comparison':baseline,
    'shared_before':before, 'shared_after':after, 'preservation':final,
    'persistent_db0_net_change':11,
    'private_endpoints_distinct_and_in_private_loopback_range':True,
    'bound_process_count':len(isolation['bound_processes']),
    'cleanup':summary['cleanup'],
    'source_before_after_equal':source_before==source_after,
    'historical_attribution':'Unclassified: persistent per-key rows, Redis server identity and writer timeline were not retained',
    'counts_are_net_changes':True,
    'no_demonstrated_fixture_or_comparison_defect':True,
    'runtime_qualified':False, 'launch_verdict':'FAIL', 'deployed_acceptance':'open'}
print(json.dumps(report,indent=2))
