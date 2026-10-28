import pathlib,subprocess,json,hashlib,sys,gzip,os
r=pathlib.Path(os.environ.get('CLAIMS_ROOT','/tmp/timesheets-claims-investigation-20261028'));ev=pathlib.Path('/tmp/timesheets-claims-evidence-20261028');name=sys.argv[1]
def git(p,*a): return subprocess.check_output(['git','-C',str(p),*a],text=True).strip()
repos={'.':r};repos.update({l.split(' ',1)[1]:r/l.split(' ',1)[1] for l in git(r,'config','-f','.gitmodules','--get-regexp','submodule.*.path').splitlines()})
f={};ids={}
for n,p in repos.items():
 ids[n]={'head':git(p,'rev-parse','HEAD'),'status':git(p,'status','--porcelain=v2'),'nested_submodules':git(p,'submodule','status')}
 for fn in subprocess.check_output(['git','-C',str(p),'ls-files','--cached','--others','--exclude-standard','-z']).decode().split('\0'):
  q=p/fn
  if q.is_file() and (q.suffix in ('.cs','.csproj','.props','.targets','.slnx') or q.name in ('global.json','.editorconfig','.gitattributes','.gitmodules','NuGet.Config','nuget.config')) and not fn.startswith(('_bmad/', '_bmad-output/', '.agents/')):
   f[str(q.relative_to(r))]=hashlib.sha256(q.read_bytes()).hexdigest()
for q in r.rglob('*'):
 if q.is_file() and ('bin/Debug/' in q.as_posix() or q.name in ('project.assets.json','project.nuget.cache')) and '.git' not in q.parts and 'node_modules' not in q.parts:
  f[str(q.relative_to(r))]=hashlib.sha256(q.read_bytes()).hexdigest()
out={'repositories':ids,'files':f,'sdk':subprocess.check_output(['dotnet','--version'],cwd=r,text=True).strip()}
(ev/(name+'.json.gz')).write_bytes(gzip.compress((json.dumps(out,indent=2)+'\n').encode(),mtime=0));print(name,len(f))
