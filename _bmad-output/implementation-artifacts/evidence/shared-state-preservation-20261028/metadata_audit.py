"""Read-only, aggregate Redis metadata; no key names, values or client details."""
import hashlib
import json
from pathlib import Path
import socket
import sys
import time
sys.path.insert(0, str(Path.cwd() / 'scripts'))
from local_runtime_admission import LocalRuntimeAdmission
from run_isolated_magic_link_dapr import running_dapr_processes

root = Path('/tmp/timesheets-shared-state-trace-20261028')
allowed_commands = {'set', 'hset', 'hmset', 'del', 'unlink', 'hdel', 'xadd', 'xdel', 'eval', 'evalsha',
                    'incr', 'incrby', 'expire', 'pexpire', 'persist', 'sadd', 'zadd', 'lpush', 'rpush',
                    'flushdb', 'flushall', 'restore', 'rename', 'fcall', 'xgroup|create'}
def info(section):
    with socket.create_connection(('127.0.0.1',6379), timeout=5) as connection:
        args = [b'INFO', section.encode()]
        connection.sendall(b'*2\r\n'+b''.join(b'$'+str(len(a)).encode()+b'\r\n'+a+b'\r\n' for a in args))
        with connection.makefile('rb') as stream:
            header = stream.readline()
            if not header.startswith(b'$'):
                raise RuntimeError('Metadata unavailable')
            length = int(header[1:])
            if not 0 <= length <= 1048576:
                raise RuntimeError('Metadata response exceeds bound')
            value = stream.read(length)
            if len(value)!=length or stream.read(2)!=b'\r\n':
                raise RuntimeError('Metadata response incomplete')
    return {key: content for row in value.decode().splitlines() if row and not row.startswith('#')
            for key,content in [row.split(':',1)]}
def sample():
    result={'started_unix_ns':time.time_ns(), 'native_dapr_count':len(running_dapr_processes())}
    server=info('server')
    result['server_identity_sha256']=hashlib.sha256(server['run_id'].encode()).hexdigest()
    result['uptime_seconds']=int(server['uptime_in_seconds'])
    keyspace=info('keyspace')
    result['databases']={}
    for database in (0,13,14):
        row=dict(field.split('=',1) for field in keyspace.get('db'+str(database),'keys=0,expires=0').split(','))
        result['databases'][str(database)]={key:int(row[key]) for key in ('keys','expires')}
    stats=info('commandstats')
    result['selected_command_calls']={name:int(dict(field.split('=',1) for field in stats['cmdstat_'+name].split(','))['calls'])
        for name in sorted(allowed_commands) if 'cmdstat_'+name in stats}
    result['completed_unix_ns']=time.time_ns()
    return result
report={'purpose':'Metadata-only observation with fixture stopped, after terminal runtime admission refusal',
        'runtime_retry':False,'resources_created':0,'services_stopped':0,'raw_keys_values_or_client_details_retained':False,
        'redis_commands':[['INFO','server'],['INFO','keyspace'],['INFO','commandstats']]}
try:
    with LocalRuntimeAdmission():
        report['lock_acquired']=True
        report['samples']=[sample()]
        time.sleep(10)
        report['samples'].append(sample())
        report['completed']=True
except Exception as error:
    report['completed']=False
    report['safe_failure_type']=type(error).__name__
(root/'readonly-metadata.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
