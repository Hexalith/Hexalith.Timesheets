import datetime, hashlib, json, pathlib, subprocess, time, zipfile, xml.etree.ElementTree as ET
P=pathlib.Path('/tmp/timesheets-release-evidence-20261028/ci')
REPO='repos/Hexalith/Hexalith.Timesheets'
RUN=40670628561
commands=[]
def stamp(): return datetime.datetime.now(datetime.timezone.utc).isoformat()
def api(endpoint, dest):
    argv=['gh','api',f'{REPO}/{endpoint}']
    start=stamp(); p=subprocess.run(argv,capture_output=True)
    target=P/dest; target.write_bytes(p.stdout)
    commands.append({'argv':argv,'started_at':start,'completed_at':stamp(),'exit_code':p.returncode,'stdout_file':dest,'stdout_sha256':hashlib.sha256(p.stdout).hexdigest(),'stderr':p.stderr.decode(errors='replace')})
    (P/'audit-monitor-commands.json').write_text(json.dumps(commands,indent=2)+'\n')
    if p.returncode: return None
    if dest.endswith('.json'): return json.loads(p.stdout)
    return target
parsed=False
for i in range(1,61):
    prefix=f'audit-monitor-{i:02d}'
    r=api(f'actions/runs/{RUN}',prefix+'-run.json')
    j=api(f'actions/runs/{RUN}/jobs',prefix+'-jobs.json')
    a=api(f'actions/runs/{RUN}/artifacts',prefix+'-artifacts.json')
    if r is None or j is None or a is None:
        print(stamp(),'API transient failure',flush=True)
        time.sleep(55); continue
    print(stamp(),r['status'],r['conclusion'],[(x['name'],x['status'],x['conclusion']) for x in j['jobs']],flush=True)
    test_artifact=next((x for x in a['artifacts'] if x['name']=='blocking-test-results'),None)
    if test_artifact is not None and not parsed:
        z=api(f"actions/artifacts/{test_artifact['id']}/zip",'audit-blocking-test-results.zip')
        if z:
            digest=hashlib.sha256(z.read_bytes()).hexdigest()
            assert test_artifact['digest']=='sha256:'+digest
            target=P/'audit-blocking-test-results'; target.mkdir(exist_ok=True)
            with zipfile.ZipFile(z) as archive:
                assert all(not pathlib.Path(x.filename).is_absolute() and '..' not in pathlib.Path(x.filename).parts for x in archive.infolist())
                archive.extractall(target)
            results=[]
            ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
            for f in sorted(target.rglob('*.trx')):
                root=ET.parse(f).getroot()
                summary=root.find('t:ResultSummary',ns)
                counters=summary.find('t:Counters',ns)
                times=root.find('t:Times',ns)
                rows=[dict(x.attrib) for x in root.findall('t:Results/t:UnitTestResult',ns)]
                claims=[x for x in rows if any(y in x.get('testName','') for y in ['Claim','AdminRequestReadSetExcludesDemonstratedPromotedCatalogCleanup','MalformedIssuance'])]
                result={'file':str(f.relative_to(P)),'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'summary':dict(summary.attrib),'counters':dict(counters.attrib),'times':dict(times.attrib) if times is not None else None,'row_count':len(rows),'rows_by_outcome':{k:sum(x.get('outcome')==k for x in rows) for k in sorted({x.get('outcome') for x in rows})},'failed_rows':[x for x in rows if x.get('outcome') not in ['Passed','NotExecuted']],'claims_rows':claims}
                results.append(result)
            findings={'observed_at_utc':stamp(),'source_commit':r['head_sha'],'artifact':test_artifact,'verified_zip_sha256':digest,'trx':results,'file_manifest':{str(f.relative_to(P)):{'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'bytes':f.stat().st_size} for f in target.rglob('*') if f.is_file()}}
            (P/'audit-suite-results.json').write_text(json.dumps(findings,indent=2)+'\n')
            print('ARTIFACT PARSED',[(x['file'],x['counters']) for x in results],flush=True)
            parsed=True
    main=next((x for x in j['jobs'] if x['id']==122756180150),None)
    if main and main['status']=='completed' and not (P/'audit-build-job-final.log').exists():
        api('actions/jobs/122756180150/logs','audit-build-job-final.log')
        print('MAIN JOB COMPLETED',main['conclusion'],flush=True)
    if r['status']=='completed':
        api(f'actions/runs/{RUN}/logs','audit-full-run-logs.zip')
        (P/'audit-run-final.json').write_text(json.dumps(r,indent=2)+'\n')
        (P/'audit-jobs-final.json').write_text(json.dumps(j,indent=2)+'\n')
        (P/'audit-artifacts-final.json').write_text(json.dumps(a,indent=2)+'\n')
        print('MONITOR FINISHED',stamp(),r['conclusion'],'artifact_parsed',parsed,flush=True)
        break
    time.sleep(55)
else:
    print('MONITOR LIMIT REACHED',stamp(),flush=True)
