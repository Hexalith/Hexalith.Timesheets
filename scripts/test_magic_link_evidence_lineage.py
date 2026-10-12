"""Narrow checks for the local persisted-lineage proof."""

import base64
import hashlib
import json
import unittest

from magic_link_evidence_lineage import complete_evidence_lineage, effective_evidence_values_match


class EvidenceLineageTests(unittest.TestCase):
    def test_selected_effective_values_follow_stored_record_and_adjustment(self):
        def event(name, payload, stored=True):
            return {
                "eventTypeName": name,
                "payload": base64.b64encode(json.dumps({"Event": payload} if stored else payload).encode()).decode(),
            }

        recorded = event("StoredTimeEntryRecorded", {
            "TimeEntryId": {"Value": "entry-1"},
            "Target": {"TargetKind": "Project", "TargetId": "project-1"},
            "Contributor": {"PartyId": "party-1"},
            "ServiceDate": "2026-06-19", "DurationMinutes": 60,
            "ActivityTypeId": {"Value": "activity-1"}, "BillableState": "Billable",
            "ActivityTypeScope": "Tenant", "ContributorCategory": "ExternalContributor",
            "AiMetrics": None, "Comment": None, "ApprovalState": "Draft",
        })
        adjusted = event("StoredTimeEntryAdjusted", {
            "AdjustedValues": {
                "ServiceDate": "2026-06-20", "DurationMinutes": 45,
                "ActivityTypeId": {"Value": "activity-1"}, "BillableState": "NonBillable",
            }
        })
        model = {
            "timeEntryId": {"value": "entry-1"},
            "target": {"targetKind": "Project", "targetId": "project-1"},
            "contributor": {"partyId": "party-1"},
            "serviceDate": "2026-06-19", "durationMinutes": 60,
            "activityTypeId": {"value": "activity-1"}, "billableState": "Billable",
            "activityTypeScope": "Tenant", "contributorCategory": "ExternalContributor",
            "aiMetrics": None, "comment": None, "commentProjectionState": "Unknown",
            "commentPublicationPolicyVersion": 1,
            "approvalState": "Draft",
        }
        self.assertTrue(effective_evidence_values_match(model, [recorded]))
        self.assertFalse(effective_evidence_values_match(model | {"durationMinutes": 61}, [recorded]))
        self.assertFalse(effective_evidence_values_match(model | {"approvalState": "Approved"}, [recorded]))
        self.assertFalse(effective_evidence_values_match(model | {
            "target": {"targetKind": "Work", "targetId": "work-1"}}, [recorded]))
        self.assertFalse(effective_evidence_values_match(model | {
            "contributorCategory": "InternalEmployee"}, [recorded]))
        self.assertFalse(effective_evidence_values_match(model | {
            "activityTypeId": {"value": "different"}}, [recorded]))
        self.assertFalse(effective_evidence_values_match(model | {
            "contributor": {"partyId": "other"}}, [recorded]))
        final = model | {"serviceDate": "2026-06-20", "durationMinutes": 45,
                         "billableState": "NonBillable"}
        self.assertTrue(effective_evidence_values_match(final, [recorded, adjusted]))
        self.assertFalse(effective_evidence_values_match(
            final | {"billableState": "Billable"}, [recorded, adjusted]))
        approved = event("TimeEntryApproved", {"ApprovalState": "Approved"}, stored=False)
        self.assertTrue(effective_evidence_values_match(
            final | {"approvalState": "Approved"}, [recorded, adjusted, approved]))
        self.assertFalse(effective_evidence_values_match(final, [recorded, adjusted, approved]))
        submitted = event("StoredTimeEntrySubmitted", {"ApprovalState": "Submitted"})
        self.assertTrue(effective_evidence_values_match(
            final | {"approvalState": "Submitted"}, [recorded, adjusted, submitted]))
        self.assertFalse(effective_evidence_values_match(final, [recorded, adjusted, submitted]))

        with_metrics_and_comment = json.loads(base64.b64decode(recorded["payload"]))["Event"]
        with_metrics_and_comment["AiMetrics"] = {"Origin": "Assisted", "MinutesSaved": 5}
        with_metrics_and_comment["Comment"] = {
            "Text": "permitted", "Policy": {"ServerPublicationVersion": 1,
                                             "ServerProjectionInclusion": "Allowed"}}
        recorded_with_comment = event("StoredTimeEntryRecorded", with_metrics_and_comment)
        selected = model | {
            "aiMetrics": {"origin": "Assisted", "minutesSaved": 5},
            "commentProjectionState": "Allowed",
            "commentPublicationPolicyVersion": 2,
            "comment": {"text": "permitted", "policy": {
                "serverPublicationVersion": 1, "serverProjectionInclusion": "Allowed"}}}
        self.assertTrue(effective_evidence_values_match(
            selected, [recorded_with_comment], projection_comments_allowed=True))
        self.assertFalse(effective_evidence_values_match(
            selected | {"comment": {"text": "changed"}}, [recorded_with_comment],
            projection_comments_allowed=True))
        self.assertFalse(effective_evidence_values_match(
            selected | {"aiMetrics": {"origin": "Assisted", "minutesSaved": 6}},
            [recorded_with_comment], projection_comments_allowed=True))
        self.assertFalse(effective_evidence_values_match(
            selected | {"commentProjectionState": "Excluded"}, [recorded_with_comment],
            projection_comments_allowed=True))
        self.assertFalse(effective_evidence_values_match(
            selected | {"comment": None, "commentProjectionState": "PolicyRequired"},
            [recorded_with_comment], projection_comments_allowed=True))
        self.assertFalse(effective_evidence_values_match(selected, [recorded_with_comment]))
        unpublished = selected | {"comment": None, "commentProjectionState": "PolicyRequired",
                                  "commentPublicationPolicyVersion": 1}
        self.assertTrue(effective_evidence_values_match(unpublished, [recorded_with_comment]))
        self.assertFalse(effective_evidence_values_match(
            unpublished | {"commentPublicationPolicyVersion": 2}, [recorded_with_comment]))
        adjusted_comment = event("StoredTimeEntryAdjusted", {"AdjustedValues": {
            "Comment": {"Text": "adjusted", "Policy": {
                "ServerPublicationVersion": 1,
                "ServerProjectionInclusion": "Allowed"}}}})
        selected_adjusted = selected | {"comment": {"text": "adjusted", "policy": {
            "serverPublicationVersion": 1,
            "serverProjectionInclusion": "Allowed"}}}
        self.assertTrue(effective_evidence_values_match(
            selected_adjusted, [recorded_with_comment, adjusted_comment],
            projection_comments_allowed=True))
        self.assertFalse(effective_evidence_values_match(
            selected, [recorded_with_comment, adjusted_comment], projection_comments_allowed=True))

        removed_comment = event("StoredTimeEntryAdjusted", {"AdjustedValues": {"Comment": None}})
        self.assertTrue(effective_evidence_values_match(
            selected | {"comment": None, "commentProjectionState": "Unknown"},
            [recorded_with_comment, removed_comment], projection_comments_allowed=True))
        self.assertFalse(effective_evidence_values_match(
            selected, [recorded_with_comment, removed_comment], projection_comments_allowed=True))

        for version, decision, state in ((1, "Excluded", "Excluded"),
                                          (0, "Allowed", "PolicyRequired"),
                                          (1, "PolicyRequired", "PolicyRequired")):
            with self.subTest(version=version, decision=decision):
                untrusted = with_metrics_and_comment | {"Comment": {
                    "Text": "permitted", "Policy": {"ProjectionInclusion": "Allowed",
                        "ServerPublicationVersion": version, "ServerProjectionInclusion": decision}}}
                source = event("StoredTimeEntryRecorded", untrusted)
                omitted = selected | {"comment": None, "commentProjectionState": state}
                self.assertTrue(effective_evidence_values_match(
                    omitted, [source], projection_comments_allowed=True))
                self.assertFalse(effective_evidence_values_match(
                    omitted | {"commentProjectionState": "Allowed", "comment": {
                        "text": "permitted", "policy": {"projectionInclusion": "Allowed",
                            "serverPublicationVersion": version, "serverProjectionInclusion": decision}}},
                    [source], projection_comments_allowed=True))

        rejected = event("TimeEntryRejected", {"ApprovalState": "Rejected"}, stored=False)
        self.assertTrue(effective_evidence_values_match(
            final | {"approvalState": "Rejected"}, [recorded, adjusted, submitted, rejected]))
        self.assertFalse(effective_evidence_values_match(
            final | {"approvalState": "Submitted"}, [recorded, adjusted, submitted, rejected]))
        self.assertFalse(effective_evidence_values_match(
            final | {"approvalState": "Rejected"}, [recorded, adjusted, submitted, rejected,
                event("TimeEntryCorrected", {}, stored=False)]))

    def test_exact_persisted_event_and_payload_are_required(self):
        event = {
            "eventTypeName": "Hexalith.Timesheets.Server.MagicLinks.StoredTimeEntryRecorded",
            "payload": "e30=", "serializationFormat": "json", "sequenceNumber": 1,
            "timestamp": "2026-06-19T12:00:00+00:00", "correlationId": "command-1",
            "messageId": "event-1", "userId": "actor-1", "metadataVersion": 1,
            "globalPosition": 42,
        }
        canonical = (
            b'{"EventTypeName":"Hexalith.Timesheets.Server.MagicLinks.StoredTimeEntryRecorded",'
            b'"Payload":"e30=","SerializationFormat":"json","SequenceNumber":1,'
            b'"Timestamp":"2026-06-19T12:00:00+00:00","CorrelationId":"command-1",'
            b'"MessageId":"event-1","UserId":"actor-1","MetadataVersion":1,'
            b'"GlobalPosition":42}'
        )
        model = {
            "packedEventLineage": base64.b64encode(b"\x01" + (1).to_bytes(8, "little") + b"\x01").decode(),
            "packedPayloadLineage": base64.b64encode(hashlib.sha256(canonical).digest()).decode(),
        }
        self.assertTrue(complete_evidence_lineage(model, [event]))
        self.assertFalse(complete_evidence_lineage(model, [event | {"globalPosition": 43}]))
        changed_event = bytearray(base64.b64decode(model["packedEventLineage"]))
        changed_event[0] = 2
        self.assertFalse(complete_evidence_lineage(
            model | {"packedEventLineage": base64.b64encode(changed_event).decode()}, [event]))
        changed_digest = bytearray(base64.b64decode(model["packedPayloadLineage"]))
        changed_digest[0] ^= 1
        self.assertFalse(complete_evidence_lineage(
            model | {"packedPayloadLineage": base64.b64encode(changed_digest).decode()}, [event]))

    def test_versioned_envelope_provenance_is_part_of_exact_digest(self):
        event = {
            "eventTypeName": "TimeEntryRecorded", "payload": "e30=", "serializationFormat": "json",
            "sequenceNumber": 1, "timestamp": "2026-10-12T00:00:00+00:00",
            "correlationId": "command-1", "messageId": "event-1", "userId": "actor-1",
            "metadataVersion": 2, "storedEventContractType": "stored-v2",
            "storedPayloadVersion": 2, "storedSerializationFormat": "json",
            "effectiveEventContractType": "effective-v3", "effectivePayloadVersion": 3,
            "effectiveSerializationFormat": "json", "effectivePayload": "e30=",
            "storedEventTypeName": "StoredTimeEntryRecorded", "storedDigest": "AQID",
            "registryFingerprint": "registry-1", "isAdapted": True, "globalPosition": 42,
        }
        canonical = (
            b'{"EventTypeName":"TimeEntryRecorded","Payload":"e30=","SerializationFormat":"json",'
            b'"SequenceNumber":1,"Timestamp":"2026-10-12T00:00:00+00:00",'
            b'"CorrelationId":"command-1","MessageId":"event-1","UserId":"actor-1",'
            b'"MetadataVersion":2,"StoredEventContractType":"stored-v2","StoredPayloadVersion":2,'
            b'"StoredSerializationFormat":"json","EffectiveEventContractType":"effective-v3",'
            b'"EffectivePayloadVersion":3,"EffectiveSerializationFormat":"json",'
            b'"EffectivePayload":"e30=","StoredEventTypeName":"StoredTimeEntryRecorded",'
            b'"StoredDigest":"AQID","RegistryFingerprint":"registry-1","IsAdapted":true,'
            b'"GlobalPosition":42}'
        )
        model = {
            "packedEventLineage": base64.b64encode(b"\x01" + (1).to_bytes(8, "little") + b"\x01").decode(),
            "packedPayloadLineage": base64.b64encode(hashlib.sha256(canonical).digest()).decode(),
        }
        self.assertTrue(complete_evidence_lineage(model, [event]))
        self.assertFalse(complete_evidence_lineage(model, [event | {"storedPayloadVersion": 4}]))


if __name__ == "__main__":
    unittest.main()
