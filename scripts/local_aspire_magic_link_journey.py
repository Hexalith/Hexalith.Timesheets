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


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def await_success(operation, expected_status, label):
    for attempt in range(3):
        status, data = operation()
        if status == expected_status:
            return data
        require(status == 403, f"{label} returned {status}")
        if attempt < 2:
            time.sleep(1)
    raise RuntimeError(f"{label} remained denied after three bounded attempts")


def application(app_id, app_port):
    rows = [row.split() for row in subprocess.check_output(["dapr", "list"], text=True).splitlines()]
    matches = [row for row in rows if row and row[0] == app_id]
    require(len(matches) == 1, f"Expected one {app_id} Dapr application; found {len(matches)}")
    require(int(matches[0][3]) == app_port, f"{app_id} sidecar targets the wrong application port")
    return int(matches[0][1])


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
    status, data = gateway("POST", "/api/v1/streams/read/workload", {
        "tenant": scoped_tenant, "domain": "timesheets", "aggregateId": aggregate,
    }, "eventstore:stream-read", scoped_tenant)
    require(status == 200, f"Stream read for {aggregate} returned {status}")
    return json.loads(data)["events"]


def require_completed(events, aggregate):
    require(events, f"No stored events for {aggregate}")
    message_id = events[0]["correlationId"]
    require(message_id and all(event["correlationId"] == message_id for event in events),
            f"Stored batch for {aggregate} has inconsistent command correlation")
    status, data = gateway(
        "GET", "/api/v1/commands/status/workload/"
        + urllib.parse.quote(tenant, safe="") + "/" + urllib.parse.quote(message_id, safe=""),
        operation="eventstore:command-status",
    )
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


def project(events, aggregate, projection, fingerprint, scoped_tenant=tenant):
    body = {
        "request": {"tenantId": scoped_tenant, "domain": "timesheets", "aggregateId": aggregate, "events": events},
        "projectionTypes": [projection],
        "dispatchId": "live-dapr-dispatch-" + secrets.token_hex(8),
        "catalogFingerprint": fingerprint,
    }
    status, data = call(
        timesheets + "/project/v2", "POST", body,
        sign("timesheets", "eventstore", "domain-service:project"),
    )
    require(status == 200, f"{projection} delivery returned {status}")
    outcomes = json.loads(data)["outcomes"]
    require(len(outcomes) == 1 and outcomes[0]["projectionType"] == projection and outcomes[0]["status"] == 0,
            f"{projection} delivery did not complete")


def read_model(key_name):
    url = timesheets_sidecar + "/v1.0/state/timesheets-readmodels/" + urllib.parse.quote(key_name, safe="")
    status, data = call(url)
    require(status == 200, f"Read model {key_name} returned {status}")
    return json.loads(data)


metadata_status, metadata_data = call(
    timesheets + "/admin/operational-index-metadata", "POST",
    {"domains": ["timesheets"], "appId": "timesheets", "serviceVersion": "v1"},
    sign("timesheets", "eventstore", "domain-service:metadata"),
)
require(metadata_status == 200, f"Projection metadata returned {metadata_status}")
metadata = json.loads(metadata_data)
require(metadata["dispatchVersion"] == 2, "Projection dispatch v2 is unavailable")
routes = next(item for item in metadata["domains"] if item["domain"] == "timesheets")["namedProjectionTypes"]
require({"tenant-activity-type-catalog", "magic-link-token-hash-capability-index"} <= set(routes),
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
missing_status, _ = gateway("POST", "/api/v1/streams/read/workload", {
    "tenant": other_tenant, "domain": "timesheets", "aggregateId": other_capability,
}, "eventstore:stream-read", other_tenant)
require(other_status == 403 and missing_status == 404,
        "Non-fixture process assertion was not denied before an owner event")
print("non-fixture assertion denied without owner event")


def journey(action, race=False):
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
    await_success(lambda: management("/api/timesheets/external-contributions/", record), 202, "Recording")
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
    require(status == 202, f"Capability issuance returned {status}")
    token = json.loads(data)["oneTimeToken"]
    issued_events = stream(capability)
    require(len(issued_events) == 1 and event_names(capability) == ["StoredMagicLinkIssued"],
            "Capability stream has no matching stored issue")
    require_completed(issued_events, capability)
    issued_payload = stored_payload(issued_events[0])
    require(issued_payload["capabilityId"]["value"] == capability
            and issued_payload["tenant"]["tenantId"] == tenant
            and issued_payload["timeEntryId"]["value"] == entry
            and issued_payload["allowedAction"] == action,
            "Stored issue payload differs from the accepted request")
    project(issued_events, capability, "magic-link-token-hash-capability-index", fingerprint)
    token_hash = b64(hashlib.sha256(token.encode()).digest())
    index = read_model("timesheets:magic-links:token-hash-capability-index:v1")
    candidate = index["entries"].get(token_hash)
    require(candidate is not None and candidate["tenant"]["tenantId"] == tenant
            and candidate["capabilityId"]["value"] == capability,
            "Delivered candidate index does not match the stored issue")
    route = "confirm" if action == "Confirm" else "adjust"
    suffix = "?t=" + urllib.parse.quote(token, safe="")
    await_success(
        lambda: call(f"http://127.0.0.1:8080/api/timesheets/magic-links/{route}" + suffix),
        200, f"Valid {route} GET")
    payload = {} if action == "Confirm" else {
        "serviceDate": now.date().isoformat(), "durationMinutes": 45,
        "activityTypeId": {"value": activity}, "billableState": "Billable",
    }
    submit_url = f"http://127.0.0.1:8080/api/timesheets/magic-links/{route}/submit" + suffix
    if race:
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            replies = list(pool.map(lambda _: call(submit_url, "POST", payload), range(2)))
        codes = sorted(status for status, _ in replies)
        require(codes == [202, 403], f"Race returned {codes}, expected one winner")
        race_denial = next(body for status, body in replies if status == 403)
    else:
        status, _ = call(submit_url, "POST", payload)
        require(status == 202, f"Valid {route} POST returned {status}")
    names = event_names(entry)
    effect = "StoredTimeEntryConfirmed" if action == "Confirm" else "StoredTimeEntryAdjusted"
    require(names[:3] == ["StoredTimeEntryRecorded", "StoredMagicLinkUsed", effect]
            and names[3:] in ([], ["MagicLinkCommitRejected"]),
            f"Stored use/effect batch differs: {names}")
    final_events = stream(entry)
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
catalog_after = read_model(f"timesheets:magic-links:activity-type-catalog:{tenant}:v1")
require(catalog_after["projectionFreshness"]["state"] == "Fresh"
        and any(item["activityTypeId"]["value"] == activity for item in catalog_after["items"]),
        "Catalog was unavailable after restart")
index_after = read_model("timesheets:magic-links:token-hash-capability-index:v1")
for entry, url, payload, denial, names, token_hash, capability in [confirm, adjust]:
    candidate = index_after["entries"].get(token_hash)
    require(candidate is not None and candidate["tenant"]["tenantId"] == tenant
            and candidate["capabilityId"]["value"] == capability,
            "Candidate index was unavailable after restart")
    status, body = call(url, "POST", payload)
    require(status == 403 and body == denial, "Post-restart replay changed the opaque denial")
    after = event_names(entry)
    require(after == names,
            f"Post-restart stream differs: {after}")
journey("Confirm")
print("fresh post-restart link passed")
print("actor restart and replay passed", "tenant", tenant)
