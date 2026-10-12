"""Independent readback checks for compact Time Entry owner lineage."""

import base64
import hashlib
import json


EVENT_CODES = {
    "StoredTimeEntryRecorded": 1,
    "TimeEntryRecorded": 1,
    "StoredTimeEntrySubmitted": 2,
    "TimeEntrySubmitted": 2,
    "StoredTimeEntryConfirmed": 3,
    "TimeEntryContributorConfirmed": 3,
    "StoredTimeEntryAdjusted": 4,
    "TimeEntryAdjustedThroughMagicLink": 4,
    "TimeEntryApproved": 5,
    "TimeEntryRejected": 6,
    "TimeEntryCorrected": 7,
    "TimeEntryApprovedCorrected": 8,
}


def _canonical_projection_event(event):
    """Match ProjectionEventDto's default JSON member order and casing."""
    fields = (
        "eventTypeName", "payload", "serializationFormat", "sequenceNumber",
        "timestamp", "correlationId", "messageId", "userId",
    )
    value = {field[:1].upper() + field[1:]: event.get(field) for field in fields}
    optional = (
        "metadataVersion", "storedEventContractType", "storedPayloadVersion",
        "storedSerializationFormat", "effectiveEventContractType",
        "effectivePayloadVersion", "effectiveSerializationFormat", "effectivePayload",
        "storedEventTypeName", "storedDigest", "registryFingerprint", "isAdapted",
    )
    for field in optional:
        if event.get(field) is not None:
            value[field[:1].upper() + field[1:]] = event[field]
    value["GlobalPosition"] = event["globalPosition"]
    return json.dumps(value, ensure_ascii=True, separators=(",", ":")).encode("utf-8")


def complete_evidence_lineage(model, owner_events):
    """Verify each packed item and SHA-256 digest against the persisted owner prefix."""
    try:
        events = base64.b64decode(model["packedEventLineage"], validate=True)
        digests = base64.b64decode(model["packedPayloadLineage"], validate=True)
        expected = [
            (EVENT_CODES[name], event["sequenceNumber"])
            for event in owner_events
            if (name := event["eventTypeName"].split(",", 1)[0].rsplit(".", 1)[-1]) in EVENT_CODES
        ]
        if (len(events) != len(expected) * 10 or len(digests) != len(owner_events) * 32):
            return False
        for index, (code, ordinal) in enumerate(expected):
            offset = index * 10
            if (events[offset] != code
                    or int.from_bytes(events[offset + 1:offset + 9], "little", signed=True) != ordinal
                    or events[offset + 9] != 1):
                return False
        return all(
            digests[index * 32:(index + 1) * 32]
            == hashlib.sha256(_canonical_projection_event(event)).digest()
            for index, event in enumerate(owner_events)
        )
    except (KeyError, TypeError, ValueError, IndexError):
        return False


def effective_evidence_values_match(model, owner_events):
    """Compare selected effective values with the stored owner's recognized effects."""
    try:
        expected = None
        approval = None
        for event in owner_events:
            name = event["eventTypeName"].split(",", 1)[0].rsplit(".", 1)[-1]
            if name not in ("StoredTimeEntryRecorded", "StoredTimeEntryAdjusted",
                            "StoredTimeEntrySubmitted", "TimeEntrySubmitted",
                            "TimeEntryApproved", "TimeEntryRejected"):
                continue
            wrapped = json.loads(base64.b64decode(event["payload"], validate=True))
            payload = wrapped["Event"] if name.startswith("Stored") else wrapped
            if name == "StoredTimeEntryRecorded":
                expected = payload
                approval = payload["ApprovalState"]
            elif name == "StoredTimeEntryAdjusted" and expected is not None:
                expected = expected | payload["AdjustedValues"]
            elif expected is not None:
                approval = payload["ApprovalState"]
        if expected is None:
            return False

        def camel(value):
            if isinstance(value, dict):
                return {key[:1].lower() + key[1:]: camel(item) for key, item in value.items()}
            if isinstance(value, list):
                return [camel(item) for item in value]
            return value

        fields = ("TimeEntryId", "Target", "Contributor", "ActivityTypeId",
                  "ActivityTypeScope", "ServiceDate", "DurationMinutes",
                  "BillableState", "ContributorCategory", "AiMetrics")
        if any(model[field[:1].lower() + field[1:]] != camel(expected[field]) for field in fields):
            return False
        if model["approvalState"] != approval:
            return False
        expected_comment = camel(expected.get("Comment"))
        selected_comment = model.get("comment")
        if expected_comment is None:
            return selected_comment is None and model.get("commentProjectionState") != "Allowed"
        if selected_comment is not None and (selected_comment != expected_comment
                                             or model.get("commentProjectionState") != "Allowed"):
            return False
        # The selected view can deliberately omit sensitive text. A published
        # comment must always match the exact stored, server-stamped comment.
        if selected_comment is None:
            return model.get("commentProjectionState") != "Allowed"
        return (expected_comment["policy"].get("serverPublicationVersion") == 1
                and expected_comment["policy"].get("serverProjectionInclusion") == "Allowed")
    except (KeyError, TypeError, ValueError, IndexError):
        return False
