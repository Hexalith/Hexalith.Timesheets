"""Linux local-only named projection and Dapr component isolation probe.

Uses the Aspire run's generated signing key in memory, without printing it.
The synthetic event exercises delivery infrastructure; it is not an EventStore
actor commit or Story 3.6 acceptance journey.
"""

import base64
import hashlib
import hmac
import json
import secrets
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone

from run_isolated_magic_link_dapr import validate_public_port

pids = subprocess.check_output(['pgrep', '-f', '/Hexalith.Timesheets$'], text=True).splitlines()
if len(pids) != 1:
    raise RuntimeError(f'Expected one Timesheets process, found {len(pids)}')
pid = int(pids[0])
environment = dict(item.split('=', 1) for item in open(f'/proc/{pid}/environ', 'rb').read().decode().split('\0') if '=' in item)
public_port = validate_public_port(environment.get('Timesheets__LocalPublicPort'))
public_origin = f'http://127.0.0.1:{public_port}'
key = environment['Authentication__JwtBearer__SigningKey'].encode()
apps = subprocess.check_output(['dapr', 'list'], text=True).splitlines()
def dapr_app(app_id, expected_app_port):
    matches = [line.split() for line in apps if line.split() and line.split()[0] == app_id]
    if len(matches) != 1:
        raise RuntimeError(f'Expected one {app_id} Dapr app ID, found {len(matches)}')
    if int(matches[0][3]) != expected_app_port:
        raise RuntimeError(f'{app_id} Dapr app port is not {expected_app_port}')
    return int(matches[0][1])

eventstore_port = dapr_app('eventstore', 8082)
timesheets_port = dapr_app('timesheets', 8081)
timesheets_sidecar = environment['DAPR_HTTP_ENDPOINT'].rstrip('/')
with urllib.request.urlopen(timesheets_sidecar + '/v1.0/metadata', timeout=20) as response:
    if json.load(response).get('id') != 'timesheets':
        raise RuntimeError('Timesheets process points to a different Dapr app')
invoke = f'http://127.0.0.1:{eventstore_port}/v1.0/invoke/timesheets/method'

def b64(value):
    return base64.urlsafe_b64encode(value).rstrip(b'=').decode()

def assertion(operation):
    now = int(time.time())
    header = {'alg': 'HS256', 'typ': 'JWT'}
    claims = {
        'iss': 'hexalith-timesheets-dev', 'aud': 'timesheets',
        'iat': now, 'nbf': now, 'exp': now + 120,
        'jti': secrets.token_hex(16), 'azp': 'eventstore',
        'eventstore:operation': operation,
    }
    unsigned = b64(json.dumps(header, separators=(',', ':')).encode()) + '.' + b64(json.dumps(claims, separators=(',', ':')).encode())
    return unsigned + '.' + b64(hmac.new(key, unsigned.encode(), hashlib.sha256).digest())

def post(path, body, token=None, url=None, app_api_token=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['X-Hexalith-Workload-Assertion'] = token
    if app_api_token:
        headers['dapr-api-token'] = app_api_token
    request = urllib.request.Request((url or invoke + path), method='POST', data=json.dumps(body).encode(), headers=headers)
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode()[:300]

metadata_status, metadata = post('/admin/operational-index-metadata', {'domains': ['timesheets'], 'appId': 'timesheets', 'serviceVersion': 'v1'}, assertion('domain-service:metadata'))
assert metadata_status == 200, ('metadata', metadata_status, metadata)
assert metadata['dispatchVersion'] == 2
assert any(domain['domain'] == 'timesheets' and {'tenant-activity-type-catalog', 'magic-link-token-hash-capability-index'} <= set(domain['namedProjectionTypes']) for domain in metadata['domains'])
fingerprint = metadata['catalogFingerprint']
print('metadata', metadata_status, metadata['dispatchVersion'], [(domain['domain'], domain['namedProjectionTypes']) for domain in metadata['domains']])

tenant = 'topology-projection-' + secrets.token_hex(5)
activity = 'activity-' + secrets.token_hex(5)
payload = {
    'activityTypeId': {'value': activity},
    'scope': 'Tenant',
    'project': None,
    'label': 'Topology Probe',
    'defaultBillableState': 'Billable',
}
now = datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')
event = {
    'eventTypeName': 'Hexalith.Timesheets.Contracts.Events.ActivityTypes.ActivityTypeCreated',
    'payload': base64.b64encode(json.dumps(payload).encode()).decode(),
    'serializationFormat': 'json',
    'sequenceNumber': 1,
    'timestamp': now,
    'correlationId': 'topology-projection-proof',
    'messageId': 'topology-event-' + secrets.token_hex(8),
    'userId': 'topology-probe',
    'globalPosition': 1,
}
dispatch = {
    'request': {'tenantId': tenant, 'domain': 'timesheets', 'aggregateId': activity, 'events': [event]},
    'projectionTypes': ['tenant-activity-type-catalog'],
    'dispatchId': 'topology-dispatch-' + secrets.token_hex(8),
    'catalogFingerprint': fingerprint,
}
public_status, _ = post('/project/v2', dispatch, assertion('domain-service:project'),
                        url=public_origin + '/project/v2',
                        app_api_token=environment['APP_API_TOKEN'])
assert public_status == 404, public_status
print('public projection route unavailable', public_status)
for label, token, expected in [('missing', None, 401), ('wrong-operation', assertion('domain-service:metadata'), 403), ('bound', assertion('domain-service:project'), 200)]:
    status, response = post('/project/v2', dispatch, token)
    assert status == expected, (label, status, response)
    if label == 'bound':
        assert response['version'] == 2
        assert [(outcome['projectionType'], outcome['status']) for outcome in response['outcomes']] == [('tenant-activity-type-catalog', 0)]
    print('projection', label, status, response if label == 'bound' else '')

state_key = f'timesheets:magic-links:activity-type-catalog:{tenant}:v1'
state_url = f'{timesheets_sidecar}/v1.0/state/timesheets-readmodels/{urllib.parse.quote(state_key, safe="")}'
try:
    with urllib.request.urlopen(state_url, timeout=20) as response:
        model = json.load(response)
        assert response.status == 200
        assert any(item.get('activityTypeId', {}).get('value') == activity for item in model.get('items', []))
        assert model['projectionFreshness']['state'] == 'Fresh'
        print('readback', response.status, 'tenant', tenant, 'activity', activity, 'items', [(item.get('activityTypeId'), item.get('label')) for item in model.get('items', [])], 'freshness', model.get('projectionFreshness'))
except urllib.error.HTTPError as error:
    raise AssertionError(('readback', error.code, error.read().decode()[:300])) from error

capability = 'capability-' + secrets.token_hex(5)
token_hash = hashlib.sha256(secrets.token_bytes(32)).hexdigest()
issued = {
    'capabilityId': {'value': capability},
    'tenant': {'tenantId': tenant},
    'contributor': {'partyId': 'topology-probe'},
    'target': {'targetKind': 'Project', 'targetId': 'topology-project'},
    'activityTypeId': {'value': activity},
    'timeEntryId': {'value': 'time-entry-' + secrets.token_hex(5)},
    'targetKind': 'ProposedTimeEntry',
    'allowedAction': 'Confirm',
    'tokenHash': {'value': token_hash},
    'expiresAtUtc': (datetime.now(timezone.utc) + timedelta(days=1)).isoformat().replace('+00:00', 'Z'),
    'issuer': {'partyId': 'topology-probe'},
    'issuedAtUtc': now,
    'source': {'sourceSystem': 'topology-probe', 'sourceReference': 'synthetic-issuance'},
    'isSingleUse': True,
}
issuance_event = dict(event)
issuance_event.update(
    eventTypeName='Hexalith.Timesheets.Contracts.Events.MagicLinks.MagicLinkConfirmationCapabilityIssued',
    payload=base64.b64encode(json.dumps(issued).encode()).decode(),
    messageId='topology-issuance-' + secrets.token_hex(8),
)
issuance_dispatch = {
    'request': {'tenantId': tenant, 'domain': 'timesheets', 'aggregateId': capability, 'events': [issuance_event]},
    'projectionTypes': ['magic-link-token-hash-capability-index'],
    'dispatchId': 'topology-issuance-dispatch-' + secrets.token_hex(8),
    'catalogFingerprint': fingerprint,
}
index_status, index_response = post('/project/v2', issuance_dispatch, assertion('domain-service:project'))
assert index_status == 200, ('issuance projection', index_status, index_response)
assert index_response['version'] == 2
assert [(outcome['projectionType'], outcome['status']) for outcome in index_response['outcomes']] == [('magic-link-token-hash-capability-index', 0)]
print('index projection', index_status, 'Completed')

index_key = 'timesheets:magic-links:token-hash-capability-index:v1'
index_url = f'{timesheets_sidecar}/v1.0/state/timesheets-readmodels/{urllib.parse.quote(index_key, safe="")}'
with urllib.request.urlopen(index_url, timeout=20) as response:
    index = json.load(response)
    candidate = index['entries'].get(token_hash)
    assert candidate is not None
    assert candidate['tenant']['tenantId'] == tenant
    assert candidate['capabilityId']['value'] == capability
    print('index readback', response.status, 'candidate tenant/capability verified')

with urllib.request.urlopen(timesheets_sidecar + '/v1.0/metadata', timeout=20) as response:
    sidecar = json.load(response)
components = sorted(component['name'] for component in sidecar['components'])
assert sidecar['id'] == 'timesheets'
assert components == ['timesheets-readmodels'], components
print('timesheets components', components)

actor_url = timesheets_sidecar + '/v1.0/state/statestore/topology-proof-absent'
try:
    urllib.request.urlopen(actor_url, timeout=20)
    raise AssertionError('Timesheets sidecar unexpectedly exposed statestore')
except urllib.error.HTTPError as error:
    assert error.code == 400, error.code
    print('actor store denied', error.code)
