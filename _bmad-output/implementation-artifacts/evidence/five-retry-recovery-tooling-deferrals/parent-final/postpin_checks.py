import concurrent.futures,json,os,subprocess,time
from pathlib import Path
root=Path.cwd();out=Path(__file__).resolve().parent;env=dict(os.environ,DOTNET_CLI_HOME='/tmp/dotnet-cli-home');rows=[]
def run(name,args):
 start=time.monotonic()
 with (out/(name+'.log')).open('w') as log:p=subprocess.run(args,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
 rows.append({'name':name,'command':args,'exit_code':p.returncode,'seconds':round(time.monotonic()-start,3),'log':name+'.log'})
 (out/'postpin-checks.json').write_text(json.dumps(rows,indent=2)+'\n');print(name,p.returncode,flush=True)
 return p.returncode
if run('postpin-restore',['dotnet','restore','Hexalith.Timesheets.slnx','-p:Configuration=Debug','-p:UseHexalithProjectReferences=true','-m:1','/nr:false']):raise SystemExit(1)
if run('postpin-build',['dotnet','build','Hexalith.Timesheets.slnx','--configuration','Debug','--no-restore','-p:UseHexalithProjectReferences=true','-warnaserror','-m:1','/nr:false']):raise SystemExit(1)
commands=[]
for project in ['Hexalith.Timesheets.ArchitectureTests','Hexalith.Timesheets.Contracts.Tests','Hexalith.Timesheets.Server.Tests','Hexalith.Timesheets.Works.Tests']:
 commands.append(('postpin-'+project,[str(root/f'tests/{project}/bin/Debug/net10.0/{project}'),'-noColor']))
commands.append(('postpin-http',[str(root/'tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests'),'-method','*KeyedHttpRetry*','-method','*ConcurrentIdenticalKeyed*','-method','*ClientKeyNeverCreates*','-method','*GatewayDefaultHttpDeadline*','-method','*OriginalComment*','-method','*ReplacementActivity*','-noColor']))
commands.append(('postpin-migration',[str(root/'tests/Hexalith.Timesheets.Projections.Tests/bin/Debug/net10.0/Hexalith.Timesheets.Projections.Tests'),'-class','*MagicLinkV6MigrationTests','-noColor']))
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:codes=list(pool.map(lambda pair:run(*pair),commands))
raise SystemExit(1 if any(codes) else 0)
