"""Linux local-only signed gateway probe for the Timesheets Aspire run.

Reads the generated key from the running Timesheets process without printing it.
Submits an invalid command in a random tenant; a domain rejection proves the
bound assertion passed gateway authentication without creating domain events.
"""

import base64,hashlib,hmac,json,time,urllib.request,urllib.error,secrets,subprocess
pids=subprocess.check_output(['pgrep','-f','/Hexalith.Timesheets$'],text=True).splitlines()
if len(pids)!=1: raise RuntimeError(f'Expected one Timesheets process, found {len(pids)}')
pid=int(pids[0])
env=dict(item.split('=',1) for item in open(f'/proc/{pid}/environ','rb').read().decode().split('\0') if '=' in item)
key=env['Authentication__JwtBearer__SigningKey'].encode()
app_rows=[line.split() for line in subprocess.check_output(['dapr','list'],text=True).splitlines() if line.split()]
matches=[row for row in app_rows if row[0]=='timesheets']
if len(matches)!=1: raise RuntimeError(f'Expected one Timesheets Dapr app ID, found {len(matches)}')
eventstore_matches=[row for row in app_rows if row[0]=='eventstore']
if len(eventstore_matches)!=1 or int(eventstore_matches[0][3])!=8082: raise RuntimeError('Expected one local EventStore Dapr app on port 8082')
if int(matches[0][3])!=8081: raise RuntimeError('Timesheets Dapr sidecar does not target the internal port')
sidecar_url=env['DAPR_HTTP_ENDPOINT'].rstrip('/')
with urllib.request.urlopen(sidecar_url+'/v1.0/metadata',timeout=20) as response:
 if json.load(response).get('id')!='timesheets': raise RuntimeError('Timesheets process points to a different Dapr app')
base=sidecar_url+'/v1.0/invoke/eventstore/method'
tenant='topology-proof-'+secrets.token_hex(4)
alpha='0123456789ABCDEFGHJKMNPQRSTVWXYZ'
def ulid():
 n=(int(time.time()*1000)<<80)|int.from_bytes(secrets.token_bytes(10),'big'); s=''
 for _ in range(26): s=alpha[n&31]+s;n>>=5
 return s
def b64(v): return base64.urlsafe_b64encode(v).rstrip(b'=').decode()
def token(op, bound_tenant=tenant):
 now=int(time.time());head={'alg':'HS256','typ':'JWT'}
 payload={'iss':'hexalith-timesheets-dev','aud':'eventstore','iat':now,'nbf':now,'exp':now+120,'jti':secrets.token_hex(16),'azp':'timesheets','eventstore:operation':op,'eventstore:bound-tenant':bound_tenant,'eventstore:bound-domain':'timesheets'}
 raw=b64(json.dumps(head,separators=(',',':')).encode())+'.'+b64(json.dumps(payload,separators=(',',':')).encode())
 return raw+'.'+b64(hmac.new(key,raw.encode(),hashlib.sha256).digest())
def call(method,path,body=None,assertion=None):
 headers={'Content-Type':'application/json'}
 if assertion: headers['X-Hexalith-Workload-Assertion']=assertion
 req=urllib.request.Request(base+path,method=method,data=json.dumps(body).encode() if body is not None else None,headers=headers)
 try:
  with urllib.request.urlopen(req,timeout=20) as r: return r.status,r.read().decode()
 except urllib.error.HTTPError as e: return e.code,e.read().decode()
message=ulid(); aggregate=ulid()
body={'messageId':message,'tenant':tenant,'domain':'timesheets','aggregateId':aggregate,'commandType':'topology.probe.invalid','payload':{}}
for label,assertion,expected in [('missing',None,401),('wrong-tenant',token('eventstore:command-submit',tenant+'-other'),403),('bound',token('eventstore:command-submit'),422)]:
 status,_=call('POST','/api/v1/commands/workload',body,assertion)
 assert status==expected,(label,status)
 print('command',label,status)
for _ in range(15):
 status,response=call('GET',f'/api/v1/commands/status/workload/{tenant}/{message}',assertion=token('eventstore:command-status'))
 if status==200:
  result=json.loads(response).get('status')
  assert result=='Rejected',result
  print('status bound',status,result)
  break
 time.sleep(.4)
else: raise AssertionError(('status bound',status))
status,response=call('GET',f'/api/v1/commands/status/workload/{tenant}/{message}',assertion=token('eventstore:command-status',tenant+'-other'))
assert status==403,status
print('status wrong-tenant',status)
for label,assertion,expected in [('missing',None,401),('wrong-tenant',token('eventstore:stream-read',tenant+'-other'),403),('bound',token('eventstore:stream-read'),200)]:
 status,response=call('POST','/api/v1/streams/read/workload',{'tenant':tenant,'domain':'timesheets','aggregateId':aggregate},assertion)
 assert status==expected,(label,status)
 print('stream',label,status)
