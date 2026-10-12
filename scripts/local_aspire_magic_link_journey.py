"""Opt-in local EventStore/Dapr magic-link acceptance journey.

Run only against the checked-in Timesheets Aspire topology. Generated credentials
and one-time tokens stay in memory; output contains only isolated identifiers,
HTTP status codes, and stored event type names.
"""

import base64
import concurrent.futures
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

from magic_link_evidence_lineage import complete_evidence_lineage, effective_evidence_values_match
from magic_link_index_selection import selected_candidate


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def await_success(operation, expected_status, label, diagnostics=None, retry_delays=(1, 1)):
    for attempt in range(len(retry_delays) + 1):
        status, data = operation()
        if status == expected_status:
            return data
        if status != 403:
            detail = "" if diagnostics is None else "; " + diagnostics()
            raise RuntimeError(f"{label} returned {status}{detail}")
        if attempt < len(retry_delays):
            time.sleep(retry_delays[attempt])
    detail = "" if diagnostics is None else "; " + diagnostics()
    raise RuntimeError(f"{label} remained denied after {len(retry_delays) + 1} bounded attempts{detail}")


# EventStore's anonymous actor tenant limiter has a 60-second window and no queue.
# These waits cross one window without changing the HTTP route's authorization result.
ACTOR_RATE_LIMIT_RETRY_DELAYS = (1, 5, 15, 45)


def application(app_id, app_port):
    rows = [row.split() for row in subprocess.check_output(["dapr", "list"], text=True).splitlines()]
    matches = [row for row in rows if row and row[0] == app_id]
    require(len(matches) == 1, f"Expected one {app_id} Dapr application; found {len(matches)}")
    require(int(matches[0][3]) == app_port, f"{app_id} sidecar targets the wrong application port")
    return int(matches[0][1])


def assert_runtime_unique():
    application("eventstore", 8082)
    application("timesheets", 8081)


pids = subprocess.check_output(["pgrep", "-f", "/Hexalith.Timesheets$"], text=True).splitlines()
require(len(pids) == 1, f"Expected one Timesheets process; found {len(pids)}")
with open(f"/proc/{pids[0]}/environ", "rb") as process_environment:
    environment = dict(
        item.split("=", 1)
        for item in process_environment.read().decode().split("\0")
        if "=" in item
    )
require(environment.get("ASPNETCORE_ENVIRONMENT") == "Development", "Timesheets must run in Development")
require(environment.get("Timesheets__LiveDaprFixture__Enabled") == "true",
        "The run-mode live Dapr fixture setting is required")
key = environment["Authentication__JwtBearer__SigningKey"].encode()
app_token = environment["APP_API_TOKEN"]
timesheets_sidecar = environment["DAPR_HTTP_ENDPOINT"].rstrip("/")
eventstore_port = application("eventstore", 8082)
application("timesheets", 8081)
with urllib.request.urlopen(timesheets_sidecar + "/v1.0/metadata", timeout=20) as response:
    require(json.load(response).get("id") == "timesheets", "Timesheets points to another Dapr app")
eventstore = timesheets_sidecar + "/v1.0/invoke/eventstore/method"
timesheets = f"http://127.0.0.1:{eventstore_port}/v1.0/invoke/timesheets/method"
tenant = "live-dapr-" + secrets.token_hex(8)
actor = "live-actor-" + secrets.token_hex(6)
activity = "live-activity-" + secrets.token_hex(6)
project_id = "live-project-" + secrets.token_hex(6)


def b64(value):
    return base64.urlsafe_b64encode(value).rstrip(b"=").decode()


def sign(audience, caller, operation, bound_tenant=None, bound_actor=None):
    now = int(time.time())
    claims = {
        "iss": "hexalith-timesheets-dev",
        "aud": audience,
        "iat": now,
        "nbf": now,
        "exp": now + 120,
        "jti": secrets.token_hex(16),
        "azp": caller,
        "eventstore:operation": operation,
    }
    if bound_tenant:
        claims["eventstore:bound-tenant"] = bound_tenant
        claims["eventstore:bound-domain"] = "timesheets"
    if bound_actor:
        claims["eventstore:bound-actor"] = bound_actor
    unsigned = b64(b'{"alg":"HS256","typ":"JWT"}') + "." + b64(json.dumps(claims, separators=(",", ":")).encode())
    return unsigned + "." + b64(hmac.new(key, unsigned.encode(), hashlib.sha256).digest())


def call(url, method="GET", body=None, assertion=None, channel_token=None):
    headers = {"Content-Type": "application/json"}
    if assertion:
        headers["X-Hexalith-Workload-Assertion"] = assertion
    if channel_token:
        headers["dapr-api-token"] = channel_token
    request = urllib.request.Request(
        url, method=method, data=json.dumps(body, separators=(",", ":")).encode() if body is not None else None,
        headers=headers,
    )
    try:
        with urllib.request.urlopen(request, timeout=45) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()
    except (OSError, TimeoutError):
        raise RuntimeError("A local HTTP or Dapr request could not complete.") from None


def gateway(method, path, body=None, operation=None, scoped_tenant=tenant):
    return call(eventstore + path, method, body,
                sign("eventstore", "timesheets", operation, scoped_tenant, actor))


def management(path, body, scoped_tenant=tenant):
    return call(
        "http://127.0.0.1:8080" + path, "POST", body,
        sign("timesheets", "eventstore", "domain-service:process", scoped_tenant, actor), app_token,
    )


def stream(aggregate, scoped_tenant=tenant):
    for attempt in range(len(ACTOR_RATE_LIMIT_RETRY_DELAYS) + 1):
        try:
            status, data = gateway("POST", "/api/v1/streams/read/workload", {
                "tenant": scoped_tenant, "domain": "timesheets", "aggregateId": aggregate,
            }, "eventstore:stream-read", scoped_tenant)
        except RuntimeError:
            if attempt < len(ACTOR_RATE_LIMIT_RETRY_DELAYS):
                time.sleep(ACTOR_RATE_LIMIT_RETRY_DELAYS[attempt])
                continue
            raise
        if status == 200:
            return json.loads(data)["events"]
        require(status in (429, 500, 503), f"Stream read for {aggregate} returned {status}")
        if attempt < len(ACTOR_RATE_LIMIT_RETRY_DELAYS):
            time.sleep(ACTOR_RATE_LIMIT_RETRY_DELAYS[attempt])
    raise RuntimeError(f"Stream read for {aggregate} remained unavailable after bounded attempts")


def require_completed(events, aggregate):
    require(events, f"No stored events for {aggregate}")
    message_id = events[0]["correlationId"]
    require(message_id and all(event["correlationId"] == message_id for event in events),
            f"Stored batch for {aggregate} has inconsistent command correlation")
    for attempt in range(5):
        status, data = gateway(
            "GET", "/api/v1/commands/status/workload/"
            + urllib.parse.quote(tenant, safe="") + "/" + urllib.parse.quote(message_id, safe=""),
            operation="eventstore:command-status",
        )
        if status == 200 or status not in (429, 500, 503):
            break
        if attempt < 4:
            time.sleep(1)
    require(status == 200, f"Command status for {aggregate} returned {status}")
    result = json.loads(data)
    require(result["status"] == "Completed" and result["statusCode"] == 4
            and result["messageId"] == message_id and result["tenantId"] == tenant
            and result["domain"] == "timesheets" and result["aggregateId"] == aggregate
            and result["eventCount"] == len(events)
            and result["committedEventSequence"] == events[-1]["sequenceNumber"]
            and result.get("rejectionEventType") is None
            and result.get("failureReason") is None and result.get("retryable") is not True,
            f"Command for {aggregate} lacks matching Completed stored-event proof")


def event_names(aggregate):
    return [event["eventTypeName"].split(",", 1)[0].rsplit(".", 1)[-1] for event in stream(aggregate)]


def stored_payload(event):
    decoded = json.loads(base64.b64decode(event["payload"]))
    require("Event" in decoded, f"Stored event wrapper has keys {list(decoded)}")

    def camel(value):
        if isinstance(value, dict):
            return {name[:1].lower() + name[1:]: camel(item) for name, item in value.items()}
        if isinstance(value, list):
            return [camel(item) for item in value]
        return value

    return camel(decoded["Event"])


def verified_positioned_events(events, aggregate):
    previous_sequence = 0
    previous_position = 0
    for event in events:
        sequence = event.get("sequenceNumber")
        position = event.get("globalPosition")
        require(isinstance(sequence, int) and sequence == previous_sequence + 1
                and isinstance(position, int) and position > previous_position,
                f"Stored stream for {aggregate} has missing or discontinuous persisted positions")
        previous_sequence = sequence
        previous_position = position
    return events


def project(events, aggregate, projection, fingerprint, scoped_tenant=tenant):
    delivery_events = verified_positioned_events(events, aggregate)
    dispatch_id = "live-dapr-dispatch-" + secrets.token_hex(8)
    body = {
        "request": {"tenantId": scoped_tenant, "domain": "timesheets", "aggregateId": aggregate, "events": delivery_events},
        "projectionTypes": [projection],
        "dispatchId": dispatch_id,
        "catalogFingerprint": fingerprint,
    }
    for attempt in range(len(ACTOR_RATE_LIMIT_RETRY_DELAYS) + 1):
        status, data = call(
            timesheets + "/project/v2", "POST", body,
            sign("timesheets", "eventstore", "domain-service:project"),
        )
        outcomes = json.loads(data).get("outcomes", []) if status == 200 else []
        if (len(outcomes) == 1 and outcomes[0]["projectionType"] == projection
                and outcomes[0]["status"] in (0, 1)):
            return
        retryable = (status in (429, 500, 503)
                     or len(outcomes) == 1 and outcomes[0].get("reasonCode")
                     == "delivery_state_unavailable")
        if retryable and attempt < len(ACTOR_RATE_LIMIT_RETRY_DELAYS):
            time.sleep(ACTOR_RATE_LIMIT_RETRY_DELAYS[attempt])
            continue
        require(status == 200, f"{projection} delivery returned {status}")
        raise RuntimeError(f"{projection} delivery did not complete: {outcomes}")


def rebuild_time_entry(events, aggregate, fingerprint):
    delivery_events = verified_positioned_events(events, aggregate)
    body = {
        "request": {"tenantId": tenant, "domain": "timesheets", "aggregateId": aggregate,
                    "events": delivery_events,
                    "rebuildSourceHeadSequence": len(delivery_events)},
        "projectionTypes": ["time-entry-evidence"],
        "dispatchId": "live-dapr-rebuild-" + secrets.token_hex(8),
        "catalogFingerprint": fingerprint,
    }
    status, data = call(
        timesheets + "/project/rebuild/v1", "POST", body,
        sign("timesheets", "eventstore", "domain-service:project"),
    )
    require(status == 200, f"Composed Time Entry rebuild route returned {status}")
    outcomes = json.loads(data)["outcomes"]
    require(len(outcomes) == 1 and outcomes[0]["projectionType"] == "time-entry-evidence"
            and outcomes[0]["status"] == 0,
            f"Composed Time Entry rebuild did not commit: {outcomes}")


def read_model(key_name):
    url = timesheets_sidecar + "/v1.0/state/timesheets-readmodels/" + urllib.parse.quote(key_name, safe="")
    for attempt in range(5):
        status, data = call(url)
        if status == 200 or status not in (429, 500, 503):
            break
        if attempt < 4:
            time.sleep(1)
    require(status == 200, f"Read model {key_name} returned {status}")
    return json.loads(data)


def selected_capability(capability):
    family = "magic-link-confirmation-capabilities"
    components = ["timesheets-readmodels", tenant, "timesheets", family, family, "shared-epoch"]
    scope_bytes = "".join(f"{len(value)}:{value}" for value in components).encode()
    scope_hash = b64(hashlib.sha256(scope_bytes).digest())
    epoch = read_model("shared-epoch:" + scope_hash)
    logical_key = f"capability:{tenant}:{capability}"

    def selected_value(key_name):
        physical_key = (f"shared-generation:{scope_hash}:{epoch['activeGeneration']}:"
                        + hashlib.sha256(key_name.encode()).hexdigest().upper())
        return read_model(physical_key)

    storage = selected_value(logical_key)
    inline = storage.get("inline")
    if inline is not None:
        require(storage.get("digest") is None and storage.get("byteLength") == 0
                and storage.get("fragmentCount") == 0 and storage.get("fragmentBytes", 0) == 0,
                "Inline capability storage has conflicting fragment metadata")
        return inline

    digest = storage.get("digest")
    byte_length = storage.get("byteLength")
    fragment_count = storage.get("fragmentCount")
    stored_fragment_bytes = storage.get("fragmentBytes", 0)
    fragment_bytes = 24 * 1024 if stored_fragment_bytes == 0 else stored_fragment_bytes
    require(isinstance(digest, str) and len(digest) == 64
            and all(char in "0123456789ABCDEF" for char in digest)
            and isinstance(byte_length, int) and byte_length > 48 * 1024
            and stored_fragment_bytes in (0, 40 * 1024)
            and isinstance(fragment_count, int)
            and fragment_count == (byte_length + fragment_bytes - 1) // fragment_bytes
            and fragment_count <= 127,
            "Capability fragment pointer is invalid")
    pieces = []
    for ordinal in range(fragment_count):
        fragment_key = f"{logical_key}:fragment:{digest}:{ordinal}"
        fragment = selected_value(fragment_key)
        data = base64.b64decode(fragment["data"], validate=True)
        expected = min(fragment_bytes, byte_length - ordinal * fragment_bytes)
        require(len(data) == expected, "Capability fragment length is invalid")
        pieces.append(data)
    content = b"".join(pieces)
    require(len(content) == byte_length and hashlib.sha256(content).hexdigest().upper() == digest,
            "Capability fragment digest is invalid")
    return json.loads(content)


def selected_index(token_hash):
    return _selected_index_snapshot(token_hash)[0]


def _selected_index_snapshot(token_hash):
    family = "magic-link-token-hash-capability-index"
    components = ["timesheets-readmodels", "global-index", "timesheets", family, family, "shared-epoch"]
    scope_bytes = "".join(f"{len(value)}:{value}" for value in components).encode()
    scope_hash = b64(hashlib.sha256(scope_bytes).digest())
    epoch = read_model("shared-epoch:" + scope_hash)
    shard = hashlib.sha256(token_hash.encode()).digest()[0]
    generation = epoch["activeGeneration"]
    root_key = "timesheets:magic-links:token-hash-capability-index:v7:root"
    format_key = "timesheets:magic-links:token-hash-capability-index:v7:format"
    def physical(key_name):
        return (f"shared-generation:{scope_hash}:{generation}:"
                + hashlib.sha256(key_name.encode()).hexdigest().upper())
    try:
        selected_format = read_model(physical(format_key))
    except RuntimeError:
        selected_format = None
    if selected_format is not None:
        require(selected_format.get("storageVersion") == 7,
                "Selected numeric index format is invalid")
        return read_model(physical(root_key)), generation, scope_hash, shard
    logical_key = f"timesheets:magic-links:token-hash-capability-index:v2:shard:{shard:02X}"
    return read_model(physical(logical_key)), generation, scope_hash, shard


def selected_index_candidate(token_hash):
    """Resolve and verify one selected candidate without token-derived state keys."""
    index, generation, scope_hash, shard = _selected_index_snapshot(token_hash)
    return selected_candidate(token_hash, index, generation, scope_hash, shard,
                              read_model, _selected_index_snapshot)


def valid_get_diagnostics(token_hash, capability, entry):
    """Return only shape and state categories; no token, hash, owner, or payload."""
    try:
        assert_runtime_unique()
        runtime = "unique"
    except (RuntimeError, subprocess.CalledProcessError):
        runtime = "duplicate-or-unavailable"
    try:
        index = selected_index(token_hash)
        version = index.get("storageVersion", 0)
        version = version if isinstance(version, int) and 0 <= version <= 7 else "unknown"
        if version == 7:
            candidate_state = "numeric-unique" if selected_index_candidate(token_hash) else "numeric-missing-or-ambiguous"
        elif version != 6:
            candidate_state = "stale-selected-index-shape"
        elif index.get("legacyContributionsUnverified"):
            candidate_state = "unverified-legacy-contributions"
        elif token_hash in index.get("candidatePageCounts", {}):
            candidate_state = "paged-collision"
        else:
            owners = index.get("contributions", {}).get(token_hash, [])
            candidate = index.get("entries", {}).get(token_hash)
            if len(owners) == 1 and candidate == owners[0]:
                candidate_state = "inline-unique"
            elif len(owners) > 1:
                candidate_state = "ambiguous"
            elif index.get("directoryPages"):
                candidate_state = "directory-unique-or-missing" if selected_index_candidate(token_hash) else "directory-ambiguous-or-invalid"
            else:
                candidate_state = "missing"
        index_shape = ("v7-numeric-trie" if version == 7
                       else f"v{version}-pages-{len(index.get('directoryPages', []))}")
    except (RuntimeError, KeyError, ValueError, TypeError):
        index_shape = "unavailable"
        candidate_state = "unavailable"
    try:
        view = selected_capability(capability)
        capability_state = view.get("published", {}).get("state", "unpublished")
        if capability_state not in ("Issued", "Used", "Revoked", "Expired", "unpublished"):
            capability_state = "unknown"
    except (RuntimeError, KeyError, ValueError, TypeError):
        capability_state = "unavailable"
    try:
        owner_events = stream(entry)
        owner_state = "present" if owner_events else "empty"
    except (RuntimeError, KeyError, ValueError, TypeError):
        owner_state = "unavailable"
    return (f"safe diagnostics runtime={runtime} index-shape={index_shape} "
            f"candidate={candidate_state} capability={capability_state} owner={owner_state}")


metadata_status, metadata_data = call(
    timesheets + "/admin/operational-index-metadata", "POST",
    {"domains": ["timesheets"], "appId": "timesheets", "serviceVersion": "v1"},
    sign("timesheets", "eventstore", "domain-service:metadata"),
)
require(metadata_status == 200, f"Projection metadata returned {metadata_status}")
metadata = json.loads(metadata_data)
require(metadata["dispatchVersion"] == 2, "Projection dispatch v2 is unavailable")
routes = next(item for item in metadata["domains"] if item["domain"] == "timesheets")["namedProjectionTypes"]
require({"tenant-activity-type-catalog", "magic-link-token-hash-capability-index",
         "time-entry-evidence", "magic-link-confirmation-capabilities"} <= set(routes),
        "Required named projection routes are unavailable")
fingerprint = metadata["catalogFingerprint"]

now = datetime.now(timezone.utc)
catalog_event = {
    "eventTypeName": "Hexalith.Timesheets.Contracts.Events.ActivityTypes.ActivityTypeCreated",
    "payload": base64.b64encode(json.dumps({
        "activityTypeId": {"value": activity}, "scope": "Tenant", "project": None,
        "label": "Live Dapr Proof", "defaultBillableState": "Billable",
    }).encode()).decode(),
    "serializationFormat": "json", "sequenceNumber": 1,
    "timestamp": now.isoformat().replace("+00:00", "Z"),
    "correlationId": "live-dapr-catalog", "messageId": "live-dapr-catalog-" + secrets.token_hex(8),
    "userId": actor, "globalPosition": 1,
}
project([catalog_event], activity, "tenant-activity-type-catalog", fingerprint)
catalog = read_model(f"timesheets:magic-links:activity-type-catalog:{tenant}:v1")
require(catalog["projectionFreshness"]["state"] == "Fresh"
        and any(item["activityTypeId"]["value"] == activity for item in catalog["items"]),
        "Delivered catalog is not Fresh with the isolated activity")
print("catalog delivered and persisted")

other_tenant = "outside-fixture-" + secrets.token_hex(8)
other_capability = "live-capability-" + secrets.token_hex(8)
other_catalog_event = catalog_event | {"messageId": "live-dapr-catalog-" + secrets.token_hex(8)}
project([other_catalog_event], activity, "tenant-activity-type-catalog", fingerprint, other_tenant)
other_catalog = read_model(f"timesheets:magic-links:activity-type-catalog:{other_tenant}:v1")
require(other_catalog["projectionFreshness"]["state"] == "Fresh", "Non-fixture catalog is not Fresh")
other_issue = {
    "capabilityId": {"value": other_capability},
    "scope": {
        "contributor": {"partyId": actor},
        "target": {"targetKind": "Project", "targetId": project_id},
        "activityTypeId": {"value": activity},
        "timeEntryId": {"value": "live-entry-" + secrets.token_hex(8)},
        "targetKind": "ProposedTimeEntry",
    },
    "allowedAction": "Confirm",
    "expiresAtUtc": (datetime.now(timezone.utc) + timedelta(hours=1)).isoformat().replace("+00:00", "Z"),
    "source": {"sourceSystem": "live-dapr-proof", "sourceReference": "issue-" + secrets.token_hex(8)},
}
other_status, _ = management("/api/timesheets/magic-links/confirmation-capabilities", other_issue, other_tenant)
for missing_attempt in range(len(ACTOR_RATE_LIMIT_RETRY_DELAYS) + 1):
    missing_status, _ = gateway("POST", "/api/v1/streams/read/workload", {
        "tenant": other_tenant, "domain": "timesheets", "aggregateId": other_capability,
    }, "eventstore:stream-read", other_tenant)
    if missing_status not in (429, 500, 503) or missing_attempt == len(ACTOR_RATE_LIMIT_RETRY_DELAYS):
        break
    time.sleep(ACTOR_RATE_LIMIT_RETRY_DELAYS[missing_attempt])
require(other_status == 403 and missing_status == 404,
        f"Non-fixture process assertion failed: issue status {other_status}, "
        f"missing owner read status {missing_status}")
print("non-fixture assertion denied without owner event")


def journey(action, race=False):
    assert_runtime_unique()
    entry = "live-entry-" + secrets.token_hex(8)
    capability = "live-capability-" + secrets.token_hex(8)
    record = {
        "timeEntryId": {"value": entry},
        "target": {"targetKind": "Project", "targetId": project_id},
        "contributor": {"partyId": actor},
        "activityTypeId": {"value": activity},
        "serviceDate": now.date().isoformat(), "durationMinutes": 60,
        "billableState": "Billable",
        "source": {"sourceSystem": "live-dapr-proof", "externalRequestId": "record-" + secrets.token_hex(8)},
    }
    # A public 403 can hide an inner actor 429; retry across one rate-limit window.
    await_success(lambda: management("/api/timesheets/external-contributions/", record), 202,
                  "Recording", retry_delays=ACTOR_RATE_LIMIT_RETRY_DELAYS)
    record_events = stream(entry)
    recorded = [event["eventTypeName"].split(",", 1)[0].rsplit(".", 1)[-1] for event in record_events]
    require(recorded == ["StoredTimeEntryRecorded"], f"Recorded owner stream differs: {recorded}")
    require_completed(record_events, entry)
    record_payload = stored_payload(record_events[0])
    require(record_payload["timeEntryId"]["value"] == entry
            and record_payload["contributor"]["partyId"] == actor
            and record_payload["activityTypeId"]["value"] == activity
            and record_payload["durationMinutes"] == 60,
            "Stored record payload differs from the accepted request")
    project(record_events, entry, "time-entry-evidence", fingerprint)
    evidence = read_model(f"readmodel:{tenant}:timesheets:time-entry-evidence:{entry}:evidence")
    require(evidence["timeEntryId"]["value"] == entry
            and evidence["projectionFreshness"]["state"] == "Fresh"
            and complete_evidence_lineage(evidence, record_events)
            and effective_evidence_values_match(evidence, record_events),
            "Delivered record evidence is not fresh, complete, or equal to stored values")
    retry_status, _ = management("/api/timesheets/external-contributions/", record)
    require(retry_status == 202 and event_names(entry) == recorded,
            f"Recording retry returned {retry_status} or appended another event")

    issue = {
        "capabilityId": {"value": capability},
        "scope": {
            "contributor": {"partyId": actor},
            "target": record["target"], "activityTypeId": record["activityTypeId"],
            "timeEntryId": record["timeEntryId"], "targetKind": "ProposedTimeEntry",
        },
        "allowedAction": action,
        "expiresAtUtc": (datetime.now(timezone.utc) + timedelta(hours=1)).isoformat().replace("+00:00", "Z"),
        "source": {"sourceSystem": "live-dapr-proof", "sourceReference": "issue-" + secrets.token_hex(8)},
    }
    status, data = management("/api/timesheets/magic-links/confirmation-capabilities", issue)
    if status != 202:
        try:
            stored_issue_count = len(stream(capability))
            detail = f"; stored capability events: {stored_issue_count}"
        except RuntimeError as error:
            detail = f"; capability stream read unavailable ({error})"
        raise RuntimeError(f"Capability issuance returned {status}{detail}")
    token = json.loads(data)["oneTimeToken"]
    issued_events = stream(capability)
    require(len(issued_events) == 1
            and issued_events[0]["eventTypeName"].split(",", 1)[0].rsplit(".", 1)[-1] == "StoredMagicLinkIssued",
            "Capability stream has no matching stored issue")
    require_completed(issued_events, capability)
    issued_payload = stored_payload(issued_events[0])
    require(issued_payload["capabilityId"]["value"] == capability
            and issued_payload["tenant"]["tenantId"] == tenant
            and issued_payload["timeEntryId"]["value"] == entry
            and issued_payload["allowedAction"] == action,
            "Stored issue payload differs from the accepted request")
    project(issued_events, capability, "magic-link-token-hash-capability-index", fingerprint)
    project(issued_events, capability, "magic-link-confirmation-capabilities", fingerprint)
    issued_view = selected_capability(capability)
    require(issued_view["published"]["state"] == "Issued"
            and issued_view["published"]["capabilityId"]["value"] == capability
            and token.encode() not in json.dumps(issued_view).encode(),
            "Delivered capability issue is missing or exposed a token")
    token_hash = b64(hashlib.sha256(token.encode()).digest())
    require(token_hash not in json.dumps(issued_view), "Capability view exposed the token hash")
    candidate = selected_index_candidate(token_hash)
    require(candidate is not None and candidate["tenant"]["tenantId"] == tenant
            and candidate["capabilityId"]["value"] == capability,
            "Delivered candidate index does not match the stored issue")
    route = "confirm" if action == "Confirm" else "adjust"
    suffix = "?t=" + urllib.parse.quote(token, safe="")
    assert_runtime_unique()
    await_success(
        lambda: call(f"http://127.0.0.1:8080/api/timesheets/magic-links/{route}" + suffix),
        200, f"Valid {route} GET",
        lambda: valid_get_diagnostics(token_hash, capability, entry))
    assert_runtime_unique()
    payload = {} if action == "Confirm" else {
        "serviceDate": now.date().isoformat(), "durationMinutes": 45,
        "activityTypeId": {"value": activity}, "billableState": "Billable",
    }
    submit_url = f"http://127.0.0.1:8080/api/timesheets/magic-links/{route}/submit" + suffix
    if race:
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            replies = list(pool.map(lambda _: call(submit_url, "POST", payload), range(2)))
        codes = sorted(status for status, _ in replies)
        if codes != [202, 403]:
            try:
                observed_names = event_names(entry)
                detail = f"; stored event names: {observed_names}"
            except RuntimeError as error:
                detail = f"; owner stream read unavailable ({error})"
            raise RuntimeError(f"Race returned {codes}, expected one winner{detail}")
        race_denial = next(body for status, body in replies if status == 403)
    else:
        await_success(lambda: call(submit_url, "POST", payload), 202, f"Valid {route} POST")
    names = event_names(entry)
    effect = "StoredTimeEntryConfirmed" if action == "Confirm" else "StoredTimeEntryAdjusted"
    require(names[:3] == ["StoredTimeEntryRecorded", "StoredMagicLinkUsed", effect]
            and names[3:] in ([], ["MagicLinkCommitRejected"]),
            f"Stored use/effect batch differs: {names}")
    final_events = stream(entry)
    project(final_events, entry, "time-entry-evidence", fingerprint)
    project(final_events, entry, "magic-link-confirmation-capabilities", fingerprint)
    final_evidence = read_model(f"readmodel:{tenant}:timesheets:time-entry-evidence:{entry}:evidence")
    require(final_evidence["projectionFreshness"]["state"] == "Fresh"
            and complete_evidence_lineage(final_evidence, final_events)
            and effective_evidence_values_match(final_evidence, final_events),
            "Delivered Time Entry effect evidence is not fresh, complete, or equal to stored values")
    rebuild_time_entry(final_events, entry, fingerprint)
    rebuilt_evidence = read_model(f"readmodel:{tenant}:timesheets:time-entry-evidence:{entry}:evidence")
    require(rebuilt_evidence == final_evidence,
            "Composed Time Entry rebuild did not retain the exact persisted evidence")
    print("composed Time Entry rebuild committed and persisted")
    final_view = selected_capability(capability)
    require(final_view["published"]["state"] == "Used"
            and final_view["published"]["timeEntryId"]["value"] == entry,
            "Delivered capability use was not joined to issuance")
    require_completed(final_events[1:3], entry)
    used_payload = stored_payload(final_events[1])
    effect_payload = stored_payload(final_events[2])
    require(used_payload["capabilityId"]["value"] == capability
            and used_payload["tenant"]["tenantId"] == tenant
            and used_payload["timeEntryId"]["value"] == entry
            and effect_payload["timeEntryId"]["value"] == entry
            and effect_payload["tenant"]["tenantId"] == tenant,
            "Stored use/effect payload differs from the accepted request")
    if action == "Adjust":
        adjusted = effect_payload["adjustedValues"]
        require(adjusted["serviceDate"] == payload["serviceDate"]
                and adjusted["durationMinutes"] == payload["durationMinutes"]
                and adjusted["activityTypeId"] == payload["activityTypeId"]
                and adjusted["billableState"] == payload["billableState"],
                "Stored adjustment values differ from the submitted values")
    replay_status, replay_body = call(submit_url, "POST", payload)
    require(replay_status == 403, f"Replay returned {replay_status}")
    if race:
        require(replay_body == race_denial, "Race loser and replay returned different denials")
    print(action.lower(), "record/issue/index/GET/use/replay passed", "race" if race else "single")
    assert_runtime_unique()
    return entry, submit_url, payload, replay_body, names, token_hash, capability


confirm = journey("Confirm", race=True)
adjust = journey("Adjust")
subprocess.run([
    "aspire", "resource", "eventstore", "restart", "--apphost",
    "src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj", "--non-interactive",
], check=True, stdout=subprocess.DEVNULL)
subprocess.run([
    "aspire", "wait", "eventstore", "--timeout", "60", "--apphost",
    "src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj", "--non-interactive",
], check=True, stdout=subprocess.DEVNULL)
eventstore_port = application("eventstore", 8082)
application("timesheets", 8081)
timesheets = f"http://127.0.0.1:{eventstore_port}/v1.0/invoke/timesheets/method"
with urllib.request.urlopen(timesheets_sidecar + "/v1.0/metadata", timeout=20) as response:
    require(json.load(response).get("id") == "timesheets", "Timesheets points to another Dapr app after restart")
catalog_after = read_model(f"timesheets:magic-links:activity-type-catalog:{tenant}:v1")
require(catalog_after["projectionFreshness"]["state"] == "Fresh"
        and any(item["activityTypeId"]["value"] == activity for item in catalog_after["items"]),
        "Catalog was unavailable after restart")
for entry, url, payload, denial, names, token_hash, capability in [confirm, adjust]:
    candidate = selected_index_candidate(token_hash)
    require(candidate is not None and candidate["tenant"]["tenantId"] == tenant
            and candidate["capabilityId"]["value"] == capability,
            "Candidate index was unavailable after restart")
    persisted_evidence = read_model(
        f"readmodel:{tenant}:timesheets:time-entry-evidence:{entry}:evidence")
    require(persisted_evidence["timeEntryId"]["value"] == entry
            and persisted_evidence["projectionFreshness"]["state"] == "Fresh"
            and complete_evidence_lineage(persisted_evidence, stream(entry))
            and effective_evidence_values_match(persisted_evidence, stream(entry)),
            "Time Entry evidence was unavailable after restart")
    persisted_capability = selected_capability(capability)
    require(persisted_capability["published"]["capabilityId"]["value"] == capability
            and persisted_capability["published"]["state"] == "Used",
            "Capability use view was unavailable after restart")
    status, body = call(url, "POST", payload)
    require(status == 403 and body == denial, "Post-restart replay changed the opaque denial")
    after = event_names(entry)
    require(after == names,
            f"Post-restart stream differs: {after}")
journey("Confirm")
assert_runtime_unique()
print("fresh post-restart link passed")
print("actor restart and replay passed", "tenant", tenant)
