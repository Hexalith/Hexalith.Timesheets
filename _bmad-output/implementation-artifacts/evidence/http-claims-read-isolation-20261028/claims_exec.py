import subprocess,pathlib,sys,datetime,time,gzip,json,hashlib,os
root=pathlib.Path(os.environ.get('CLAIMS_ROOT','/tmp/timesheets-claims-investigation-20261028')); ev=pathlib.Path('/tmp/timesheets-claims-evidence-20261028')
label=sys.argv[1]; args=sys.argv[2:]
def git(p,*a): return subprocess.check_output(['git','-C',str(p),*a],text=True).strip()
def identity():
 repos={'.':root}; repos.update({l.split(' ',1)[1]:root/l.split(' ',1)[1] for l in git(root,'config','-f','.gitmodules','--get-regexp','submodule.*.path').splitlines()})
 return {n:{'head':git(p,'rev-parse','HEAD'),'status':git(p,'status','--porcelain=v2'), 'diff_sha256':hashlib.sha256(subprocess.check_output(['git','-C',str(p),'diff','HEAD','--'])).hexdigest()} for n,p in repos.items()}
def artifacts():
 d=root/'tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0'
 return {str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in d.iterdir() if p.is_file() and (p.suffix in ('.dll','.json','.pdb') or p.name=='Hexalith.Timesheets.IntegrationTests')} if d.exists() else {}
def changed_inputs():
 names=subprocess.check_output(['git','-C',str(root),'ls-files','--cached','--others','--exclude-standard','-z']).decode().split('\0')
 return {n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in names if (root/n).is_file() and (PathSuffix:=pathlib.Path(n).suffix) in ('.cs','.csproj','.props','.targets','.slnx') and n.startswith(('tests/','src/','Directory.'))}
r={'command':args,'cwd':str(root),'environment':{'DOTNET_CLI_HOME':'/tmp/dotnet-cli-home'},'source_before':identity(),'test_artifacts_before':artifacts(),'changed_inputs':changed_inputs(),'started_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
t=time.monotonic(); env=os.environ.copy();env['DOTNET_CLI_HOME']='/tmp/dotnet-cli-home'
with (ev/(label+'.log')).open('wb') as f: p=subprocess.run(args,cwd=root,env=env,stdout=f,stderr=subprocess.STDOUT)
r.update(exit_code=p.returncode,elapsed_seconds=round(time.monotonic()-t,3),source_after=identity(),test_artifacts_after=artifacts())
data=(ev/(label+'.log')).read_bytes(); (ev/(label+'.log.gz')).write_bytes(gzip.compress(data,mtime=0)); r.update(log=label+'.log.gz',log_sha256=hashlib.sha256(data).hexdigest())
(ev/(label+'.json')).write_text(json.dumps(r,indent=2)+'\n'); print(json.dumps({k:v for k,v in r.items() if k not in ('source_before','source_after','test_artifacts_before','test_artifacts_after','changed_inputs')},indent=2)); print(data.decode(errors='replace')[-5500:]); sys.exit(p.returncode)
