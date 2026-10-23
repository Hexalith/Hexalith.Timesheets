"""Narrow selected-index checks without a running Dapr application."""

import base64
import hashlib
import json
import unittest
from copy import deepcopy

from magic_link_index_selection import (
    _content_digest, _path, _physical, legacy_index_diagnostics, selected_candidate, selected_index_snapshot,
)


class SelectedIndexTests(unittest.TestCase):
    @staticmethod
    def ready_epoch():
        return {"version": 1, "epoch": 5, "activeGeneration": 5, "phase": 0,
                "captureHighWatermarks": {}, "consumerCheckpoints": {},
                "requiredWriters": ["timesheets-token-candidate-index"],
                "registeredWriters": ["timesheets-token-candidate-index"],
                "stageKeys": [], "journal": [], "deliveryConflicts": [], "parkingEvidence": []}

    @staticmethod
    def conflict():
        return {"sourceStream": "owner", "envelopePosition": 1, "observedEpoch": 5,
                "acceptedDigest": "A" * 64, "conflictingDigest": "B" * 64}

    @staticmethod
    def parking():
        return {"sourceStream": "owner", "envelopePosition": 1, "observedEpoch": 5,
                "failureCount": 1}

    def read_snapshot(self, epoch, index=None):
        def read(key, allow_missing=False):
            if key.startswith("shared-epoch:"):
                return epoch
            return None if allow_missing else index or {"storageVersion": 6}
        return selected_index_snapshot("hash", read)

    def test_captured_digest_alignment_count_and_nonempty_are_required(self):
        for length in (0, 1, 31, 33, 64):
            epoch = self.ready_epoch() | {"consumerCheckpoints": {"owner": {
                "position": 1, "token": "", "ownerSequenceNumber": 1,
                "capturedEventDigests": base64.b64encode(bytes(length)).decode()}}}
            with self.subTest(length=length), self.assertRaisesRegex(ValueError, "typed state or scope"):
                self.read_snapshot(epoch)
        for sequence, length in ((1, 32), (2, 32), (2, 64)):
            epoch = self.ready_epoch() | {"consumerCheckpoints": {"owner": {
                "position": sequence, "token": "", "ownerSequenceNumber": sequence,
                "capturedEventDigests": base64.b64encode(bytes(length)).decode()}}}
            with self.subTest(sequence=sequence, length=length):
                self.assertEqual(5, self.read_snapshot(epoch)[1])

    def test_legacy_bootstrap_diagnostic_only_reports_shape_and_count(self):
        sensitive = "sensitive-token-hash-and-owner"
        legacy = {"entries": {sensitive: {"tenant": sensitive, "capabilityId": sensitive}}}
        diagnostic = legacy_index_diagnostics(lambda _: legacy)
        self.assertEqual("legacy-direct-root=present-unverified entries=1", diagnostic)
        self.assertNotIn(sensitive, diagnostic)
        self.assertEqual("legacy-direct-root=absent", legacy_index_diagnostics(lambda _: None))
        self.assertEqual("legacy-direct-root=invalid", legacy_index_diagnostics(lambda _: {}))

        def unavailable(_):
            raise RuntimeError(sensitive)

        self.assertEqual("legacy-direct-root=unavailable", legacy_index_diagnostics(unavailable))

    def test_numeric_path_matches_server_url_safe_base64(self):
        candidate = {"tenant": {"tenantId": "\u083e"},
                     "capabilityId": {"value": "aa?"}}
        # U+083E is 4KC+ and "aa?" is YWE/ in standard Base64. The server path
        # uses the URL-safe alphabet and strips padding, so those become 4KC- and YWE_.
        self.assertEqual("hash!4KC-!YWE_", _path("hash", candidate))
        self.assertNotEqual("hash!4KC+!YWE/", _path("hash", candidate))

    def test_numeric_candidate_requires_content_bound_node_and_stable_root(self):
        candidate = {"tenant": {"tenantId": "tenant-1"},
                     "capabilityId": {"value": "capability-1"}}
        token_hash = "interior-hash"
        path = (token_hash + "!" + "dGVuYW50LTE" + "!" + "Y2FwYWJpbGl0eS0x")
        row = {"tokenHash": token_hash, "candidate": candidate}
        node = {"prefix": path, "row": row, "roster": None, "children": {}}
        canonical = {"Prefix": path,
                     "Row": {"TokenHash": token_hash, "Candidate": {
                         "Tenant": {"TenantId": "tenant-1"},
                         "CapabilityId": {"Value": "capability-1"}}},
                     "Roster": None, "Children": []}
        digest = hashlib.sha256(json.dumps(canonical, separators=(",", ":"))
                                .encode()).hexdigest().upper()
        root = {"storageVersion": 7, "tokenRoot": {
            "generation": 4, "nodeId": 12, "digest": digest}}
        keys = []

        def read_node(key):
            keys.append(key)
            return node

        def snapshot(_):
            return root, 5, "scope", 0

        self.assertEqual(candidate, selected_candidate(token_hash, root, 5, "scope", 0,
                                                        read_node, snapshot))
        # SDK batch persistence sorts every JSON object's properties. Integrity
        # is defined by the C# contracts' member order, independent of that
        # persisted property order.
        persisted_node = json.loads(json.dumps(node, sort_keys=True))
        self.assertEqual(candidate, selected_candidate(token_hash, root, 5, "scope", 0,
                                                        lambda _: persisted_node, snapshot))
        self.assertEqual(1, len(keys))
        self.assertIn(":4:", keys[0])
        self.assertIsNone(selected_candidate(token_hash, root, 5, "scope", 0,
                                              lambda _: node | {"prefix": "altered"}, snapshot))
        self.assertIsNone(selected_candidate(token_hash, root, 5, "scope", 0,
                                              read_node, lambda _: (root, 6, "scope", 0)))
        diagnostics = []
        self.assertIsNone(selected_candidate(
            token_hash, root, 5, "scope", 0, read_node,
            lambda _: self.read_snapshot(self.ready_epoch() | {"phase": 2}, root),
            diagnostics.append))
        self.assertEqual(["numeric-node-integrity-invalid"], diagnostics)

    def test_numeric_interior_candidate_rejects_cross_tenant_ambiguity(self):
        token_hash = "shared-hash"
        first = {"tenant": {"tenantId": "tenant-1"},
                 "capabilityId": {"value": "capability-1"}}
        second = {"tenant": {"tenantId": "other"},
                  "capabilityId": {"value": "capability-2"}}

        def make_leaf(candidate, node_id):
            path = _path(token_hash, candidate)
            prefix = path[len(token_hash) + 2:]
            row = {"tokenHash": token_hash, "candidate": candidate}
            node = {"prefix": prefix, "row": row, "roster": None,
                    "children": {}}
            canonical = {"Prefix": prefix, "Row": {"TokenHash": token_hash,
                         "Candidate": {"Tenant": {"TenantId": candidate["tenant"]["tenantId"]},
                                       "CapabilityId": {"Value": candidate["capabilityId"]["value"]}}},
                         "Roster": None, "Children": []}
            digest = hashlib.sha256(json.dumps(canonical, separators=(",", ":"))
                                    .encode()).hexdigest().upper()
            return node, {"generation": 3, "nodeId": node_id, "digest": digest}

        first_node, first_ref = make_leaf(first, 11)
        second_node, second_ref = make_leaf(second, 12)

        def make_root(children):
            prefix = token_hash + "!"
            node = {"prefix": prefix, "row": None, "roster": None,
                    "children": children}
            canonical = {"Prefix": prefix, "Row": None, "Roster": None,
                         "Children": [{"Edge": edge, "Generation": ref["generation"],
                                       "NodeId": ref["nodeId"], "Digest": ref["digest"]}
                                      for edge, ref in sorted(children.items())]}
            digest = hashlib.sha256(json.dumps(canonical, separators=(",", ":"))
                                    .encode()).hexdigest().upper()
            return node, {"storageVersion": 7, "tokenRoot": {
                "generation": 3, "nodeId": 10, "digest": digest}}

        nodes = {10: None, 11: first_node, 12: second_node}

        def read_node(key):
            for node_id, node in nodes.items():
                logical = f"timesheets:magic-links:token-hash-capability-index:v7:node:{node_id:016d}"
                if key == _physical("scope", 3, logical):
                    return node
            raise RuntimeError("missing selected node")

        nodes[10], unique_root = make_root({"d": first_ref})
        self.assertEqual(first, selected_candidate(token_hash, unique_root, 3, "scope", 0,
                                                   read_node, lambda _: (unique_root, 3, "scope", 0)))
        nodes[10], ambiguous_root = make_root({"b": second_ref, "d": first_ref})
        self.assertIsNone(selected_candidate(token_hash, ambiguous_root, 3, "scope", 0,
                                              read_node, lambda _: (ambiguous_root, 3, "scope", 0)))

    def test_directory_candidate_requires_page_digest_and_stable_selection(self):
        token_hash = "interior-hash"
        shard = hashlib.sha256(token_hash.encode()).digest()[0]
        candidate = {"tenant": {"tenantId": "tenant-1"},
                     "capabilityId": {"value": "capability-1"}}
        rows = [{"tokenHash": token_hash, "candidate": candidate}]
        digest_rows = [[token_hash, "tenant-1", "capability-1"]]
        digest = hashlib.sha256(json.dumps(digest_rows, separators=(",", ":")).encode()).hexdigest().upper()
        index = {"storageVersion": 6, "legacyContributionsUnverified": False,
                 "entries": {}, "contributions": {}, "candidatePageCounts": {},
                 "directoryPages": [{"pageId": 2, "firstHash": token_hash,
                                     "lastHash": token_hash}],
                 "directoryPageDigests": {"2": digest}, "nextCandidatePageId": 3}
        page_reads = []

        def read_page(key):
            page_reads.append(key)
            return {"entries": rows}

        def snapshot(_):
            return index, 7, "scope", shard

        self.assertEqual(candidate, selected_candidate(token_hash, index, 7, "scope", shard,
                                                        read_page, snapshot))
        self.assertIn(":7:", page_reads[0])
        index["directoryPageDigests"]["2"] = "0" * 64
        self.assertIsNone(selected_candidate(token_hash, index, 7, "scope", shard,
                                              read_page, snapshot))
        index["directoryPageDigests"]["2"] = digest
        self.assertIsNone(selected_candidate(token_hash, index, 7, "scope", shard,
                                              read_page, lambda _: (index, 8, "scope", shard)))

    def test_inline_candidate_fails_if_selection_changes(self):
        candidate = {"tenant": {"tenantId": "tenant-1"},
                     "capabilityId": {"value": "capability-1"}}
        index = {"storageVersion": 6, "legacyContributionsUnverified": False,
                 "entries": {"hash": candidate}, "contributions": {"hash": [candidate]},
                 "candidatePageCounts": {}}
        self.assertIsNone(selected_candidate("hash", index, 3, "scope", 0,
                                              lambda _: self.fail("unexpected page read"),
                                              lambda _: (index, 4, "scope", 0)))

    def legacy_shard(self):
        token_hash = "inline-hash"
        shard = hashlib.sha256(token_hash.encode()).digest()[0]

        def matching_hash(prefix):
            return next(value for i in range(10000)
                        if hashlib.sha256((value := f"{prefix}-{i}").encode()).digest()[0] == shard)

        directory_hash = matching_hash("page")
        collision_hash = matching_hash("collision")
        owner = {"tenant": {"tenantId": "tenant-1"}, "capabilityId": {"value": "capability-1"}}
        other = {"tenant": {"tenantId": "tenant-2"}, "capabilityId": {"value": "capability-2"}}
        directory_rows = [{"tokenHash": directory_hash, "candidate": other}]
        collision = {"tokenHash": collision_hash, "candidates": [owner, other]}

        def digest(content):
            return hashlib.sha256(json.dumps(content, separators=(",", ":")).encode()).hexdigest().upper()

        index = {
            "storageVersion": 6, "legacyContributionsUnverified": False,
            "entries": {token_hash: owner}, "contributions": {token_hash: [owner]},
            "directoryPages": [{"pageId": 2, "firstHash": directory_hash, "lastHash": directory_hash}],
            "directoryPageDigests": {"2": digest([[directory_hash, "tenant-2", "capability-2"]])},
            "candidatePageCounts": {collision_hash: 1}, "candidatePageIds": {collision_hash: [3]},
            "candidatePageDigests": {"3": digest({"TokenHash": collision_hash, "Candidates": [
                ["tenant-1", "capability-1"], ["tenant-2", "capability-2"]]})},
            "nextCandidatePageId": 4,
        }
        pages = {
            _physical("scope", 5, f"timesheets:magic-links:token-hash-capability-index:v5:shard:{shard:02X}:page:000002"):
                {"entries": directory_rows},
            _physical("scope", 5, f"timesheets:magic-links:token-hash-capability-index:v4:shard:{shard:02X}:page:000003"):
                collision,
        }
        return token_hash, shard, owner, index, pages

    def test_inline_hit_requires_all_directory_and_collision_pages(self):
        token_hash, shard, owner, index, pages = self.legacy_shard()
        reads = []

        def read(key):
            reads.append(key)
            return pages[key]

        self.assertEqual(owner, selected_candidate(token_hash, index, 5, "scope", shard,
                                                   read, lambda _: (index, 5, "scope", shard)))
        self.assertEqual(list(pages), reads)
        for missing in pages:
            with self.subTest(missing_page=list(pages).index(missing)):
                damaged = dict(pages)
                del damaged[missing]
                self.assertIsNone(selected_candidate(token_hash, index, 5, "scope", shard,
                                                      damaged.__getitem__, lambda _: (index, 5, "scope", shard)))

    def test_inline_hit_rejects_corrupt_unrelated_page_contents(self):
        for page_number in (0, 1):
            token_hash, shard, _, index, pages = self.legacy_shard()
            key = list(pages)[page_number]
            page = pages[key]
            if page_number == 0:
                page["entries"][0]["candidate"]["capabilityId"]["value"] = "corrupt"
            else:
                page["candidates"] = page["candidates"][:1]
            with self.subTest(page=page_number):
                diagnostics = []
                self.assertIsNone(selected_candidate(token_hash, index, 5, "scope", shard,
                                                      pages.__getitem__, lambda _: (index, 5, "scope", shard),
                                                      diagnostics.append))
                self.assertEqual(["legacy-shard-invalid-or-unavailable"], diagnostics)

    def test_manifest_rejects_overlapping_ranges_reused_ids_and_orphan_digests(self):
        token_hash, shard, _, original, pages = self.legacy_shard()
        invalid = []
        index = deepcopy(original)
        index["directoryPages"][0]["firstHash"] = token_hash
        invalid.append(index)
        index = deepcopy(original)
        collision_hash = next(iter(index["candidatePageIds"]))
        index["candidatePageIds"][collision_hash] = [2]
        invalid.append(index)
        index = deepcopy(original)
        index["candidatePageDigests"]["99"] = "F" * 64
        invalid.append(index)
        index = deepcopy(original)
        index["nextCandidatePageId"] = 3
        invalid.append(index)
        index = deepcopy(original)
        index["candidatePageCounts"][collision_hash] = 2
        invalid.append(index)
        index = deepcopy(original)
        index["contributions"][token_hash] *= 2
        invalid.append(index)
        index = deepcopy(original)
        index["directoryPageDigests"]["2"] = "a" * 64
        invalid.append(index)
        index = deepcopy(original)
        index["entries"]["orphan"] = index["entries"][token_hash]
        invalid.append(index)
        index = deepcopy(original)
        index["directoryPages"] = None
        invalid.append(index)
        for property_name, value in (("legacyContributionsUnverified", None),
                                     ("legacyContributionsUnverified", 0),
                                     ("storageVersion", 6.0), ("nextCandidatePageId", 2 ** 40)):
            index = deepcopy(original)
            index[property_name] = value
            invalid.append(index)
        for case, index in enumerate(invalid):
            with self.subTest(case=case):
                self.assertIsNone(selected_candidate(token_hash, index, 5, "scope", shard,
                                                      pages.__getitem__, lambda _: (index, 5, "scope", shard)))

    def test_directory_hit_requires_the_required_entries_member(self):
        _, shard, _, index, pages = self.legacy_shard()
        token_hash = index["directoryPages"][0]["firstHash"]
        index["entries"] = {}
        index["contributions"] = {}
        candidate = next(iter(pages.values()))["entries"][0]["candidate"]
        self.assertEqual(candidate, selected_candidate(token_hash, index, 5, "scope", shard,
                                                       pages.__getitem__, lambda _: (index, 5, "scope", shard)))
        del index["entries"]
        self.assertIsNone(selected_candidate(token_hash, index, 5, "scope", shard,
                                              pages.__getitem__, lambda _: (index, 5, "scope", shard)))

    def test_v6_digests_match_sdk_default_encoder_golden_fixtures(self):
        # Generated with System.Text.Json under the repository's SDK 10.0.402.
        fixtures = {
            "tenant<one": "A500C6E02D506E938B8BCA3508FE8ABDAAD8F03C4B6EF7F4A54176AF62295E80",
            "café": "8AB8DD9489E19178C49A03AB0C849E9A927F0DCDBC48C676149ABE5E34F0E33B",
            "😀": "A75D5546CA58C38341A325867AC59165123259F72221F4A5EBDB7BFA318DA5B2",
            'quote"': "E2051D48D49463A25A6EF75D4687FA3F867F7BB85AE85D7F956AFC0ECB79BF45",
            "backslash\\": "79BA8B64E287F8A977E3FF91780F33193B2E4213175B307C1AFF7654CFDE8906",
            "plus+": "CC463883598B75776793F2B73749DC8BEEB74D53E9A510D3B334C645863922BC",
            "backtick`": "5E4DD34CF47963677903267B5A670125F7128B51E35BDB73DDF1E36735E5C081",
            "less<greater>amp&single'": "855F7BB9BCF296CE92334322425C760B3BCEB0D8766836ABA012269854631F3B",
            'nul\x00ctrl\x01back\btab\tline\nform\freturn\rend\x1f':
                "1EBB11E87F9D937BC301322D648E5EA03CEC297B07E043564E130ABBD15E0F66",
        }
        for identity, digest in fixtures.items():
            with self.subTest(identity=repr(identity)):
                content = {"TokenHash": "collision-hash", "Candidates": [[identity, "capability-1"]]}
                self.assertEqual(digest, _content_digest(content))
                token_hash, shard, owner, index, pages = self.legacy_shard()
                index["candidatePageCounts"] = {"collision-hash": 1}
                index["candidatePageIds"] = {"collision-hash": [3]}
                index["candidatePageDigests"]["3"] = digest
                pages[list(pages)[1]] = {"tokenHash": "collision-hash", "candidates": [
                    {"tenant": {"tenantId": identity}, "capabilityId": {"value": "capability-1"}}]}
                self.assertEqual(owner, selected_candidate(token_hash, index, 5, "scope", shard,
                                                           pages.__getitem__, lambda _: (index, 5, "scope", shard)))

    def test_complete_shard_uses_dotnet_utf16_directory_order(self):
        # SDK 10.0.402 sorts the surrogate pair for U+1F600 before U+E000.
        directory_hash = "directory-hash"
        shard = hashlib.sha256(directory_hash.encode()).digest()[0]
        token_hash = next(f"inline-{i}" for i in range(10000)
                          if hashlib.sha256(f"inline-{i}".encode()).digest()[0] == shard)
        owner = {"tenant": {"tenantId": "tenant-1"}, "capabilityId": {"value": "capability-1"}}
        rows = [{"tokenHash": directory_hash, "candidate": {
            "tenant": {"tenantId": tenant}, "capabilityId": {"value": capability}}}
            for tenant, capability in (("😀", "capability-1"), ("\uE000", "capability-2"))]
        index = {
            "storageVersion": 6, "entries": {token_hash: owner}, "contributions": {token_hash: [owner]},
            "directoryPages": [{"pageId": 2, "firstHash": directory_hash, "lastHash": directory_hash}],
            "directoryPageDigests": {"2": "D2DCB63E1696D5BCF8D292552CBA5225230079CDF10D151EE633DE6D5F260E12"},
            "nextCandidatePageId": 3,
        }
        self.assertEqual(owner, selected_candidate(token_hash, index, 5, "scope", shard,
                                                   lambda _: {"entries": rows},
                                                   lambda _: (index, 5, "scope", shard)))
        rows.reverse()
        index["directoryPageDigests"]["2"] = _content_digest([
            [row["tokenHash"], row["candidate"]["tenant"]["tenantId"],
             row["candidate"]["capabilityId"]["value"]] for row in rows])
        self.assertIsNone(selected_candidate(token_hash, index, 5, "scope", shard,
                                              lambda _: {"entries": rows},
                                              lambda _: (index, 5, "scope", shard)))

    def test_valid_digest_does_not_admit_invalid_page_scope_or_identity(self):
        token_hash, shard, _, index, pages = self.legacy_shard()
        key = next(iter(pages))
        wrong_hash = next(f"wrong-{i}" for i in range(100)
                          if hashlib.sha256(f"wrong-{i}".encode()).digest()[0] != shard)
        pages[key]["entries"][0]["tokenHash"] = wrong_hash
        index["directoryPages"][0].update(firstHash=wrong_hash, lastHash=wrong_hash)
        rows = [[wrong_hash, "tenant-2", "capability-2"]]
        index["directoryPageDigests"]["2"] = hashlib.sha256(
            json.dumps(rows, separators=(",", ":")).encode()).hexdigest().upper()
        self.assertIsNone(selected_candidate(token_hash, index, 5, "scope", shard,
                                              pages.__getitem__, lambda _: (index, 5, "scope", shard)))
        token_hash, shard, _, index, pages = self.legacy_shard()
        next(iter(pages.values()))["entries"][0]["candidate"]["tenant"] = None
        self.assertIsNone(selected_candidate(token_hash, index, 5, "scope", shard,
                                              pages.__getitem__, lambda _: (index, 5, "scope", shard)))

    def test_complete_shard_rechecks_selection_and_keeps_ambiguous_target_closed(self):
        token_hash, shard, _, index, pages = self.legacy_shard()
        self.assertIsNone(selected_candidate(token_hash, index, 5, "scope", shard,
                                              pages.__getitem__, lambda _: (index, 6, "scope", shard)))
        collision_hash = next(iter(index["candidatePageIds"]))
        self.assertIsNone(selected_candidate(collision_hash, index, 5, "scope", shard,
                                              pages.__getitem__, lambda _: (index, 5, "scope", shard)))

    def test_snapshot_only_falls_back_when_format_is_absent(self):
        for version in (None, {"storageVersion": 7}):
            reads = []

            def read(key, allow_missing=False):
                reads.append((key, allow_missing))
                if key.startswith("shared-epoch:"):
                    return self.ready_epoch()
                if allow_missing:
                    return version
                return {"storageVersion": 6 if version is None else 7}

            index, generation, scope, shard = selected_index_snapshot("hash", read)
            self.assertEqual(5, generation)
            self.assertEqual(6 if version is None else 7, index["storageVersion"])
            logical = (f"timesheets:magic-links:token-hash-capability-index:v2:shard:{shard:02X}"
                       if version is None else "timesheets:magic-links:token-hash-capability-index:v7:root")
            self.assertEqual((_physical(scope, 5, logical), False), reads[-2])
            self.assertEqual(reads[0], reads[-1])
            self.assertTrue(reads[1][1])

    def test_snapshot_propagates_unavailable_or_invalid_format_without_legacy_read(self):
        for format_result in (RuntimeError("sensitive-store-outage"), ValueError("invalid-json"),
                              {}, {"storageVersion": 6}, {"storageVersion": 7.0}, [], "invalid"):
            reads = []

            def read(key, allow_missing=False):
                reads.append(key)
                if len(reads) == 1:
                    return self.ready_epoch()
                self.assertTrue(allow_missing)
                if isinstance(format_result, Exception):
                    raise format_result
                return format_result

            with self.subTest(format_type=type(format_result).__name__):
                with self.assertRaises((RuntimeError, ValueError)):
                    selected_index_snapshot("hash", read)
                self.assertEqual(2, len(reads))

    def test_typed_admission_refuses_four_ledger_corruptions(self):
        for change in ({"copyOnWriteAnchorGeneration": 6}, {"consumerCheckpoints": None},
                       {"requiredWriters": ["foreign-writer"]}, {"stageCompletedPageCount": -1}):
            with self.subTest(change=change), self.assertRaisesRegex(ValueError, "typed state or scope"):
                self.read_snapshot(self.ready_epoch() | change)

    def test_typed_admission_requires_containers_and_typed_scalars(self):
        for field in ("captureHighWatermarks", "consumerCheckpoints", "requiredWriters",
                      "registeredWriters", "stageKeys", "deliveryConflicts", "parkingEvidence"):
            for value in (None, "bad", 1):
                with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                    self.read_snapshot(self.ready_epoch() | {field: value})
            missing = self.ready_epoch()
            del missing[field]
            with self.subTest(missing=field), self.assertRaises(ValueError):
                self.read_snapshot(missing)
        for change in ({"copyOnWriteAnchorGeneration": True}, {"captureHighWatermarks": {"o": True}},
                       {"registeredWriters": ["foreign"]}, {"operationTenantId": 1},
                       {"requiredWriters": [], "registeredWriters": ["timesheets-token-candidate-index"]},
                       {"consumerCheckpoints": {"o": {"position": 1, "token": None}}},
                       {"consumerCheckpoints": {"o": {"position": True, "token": ""}}},
                       {"consumerCheckpoints": {"o": {"position": 1, "token": "!"}}},
                       {"retainedControlIndexNames": ["index", "index"]},
                       {"retainedControlIndexNames": [None]}, {"stageReservedPageCount": True},
                       {"cleanupStreamEpoch": "0"}):
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.read_snapshot(self.ready_epoch() | change)

    def test_typed_admission_refuses_unsupported_staging_and_chunks(self):
        for field in ("stageMutations", "stageChunks", "cleanupChunks", "captureChunks",
                      "cleanupPreparedChunks", "stageControlIndexIntent"):
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.read_snapshot(self.ready_epoch() | {field: {}})
        for change in ({"stageKeys": ["private"]}, {"stageStreaming": True},
                       {"stageExecutionStarted": True}, {"stageSealedPageCount": 1},
                       {"cleanupStreamPageCount": 1}, {"cleanupPromotedRetirements": True}):
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.read_snapshot(self.ready_epoch() | change)

    def test_typed_admission_accepts_healthy_sdk_shaped_open_snapshot(self):
        snapshot = self.ready_epoch() | {
            "copyOnWriteAnchorGeneration": 0, "operationId": "completed-rebuild",
            "inventoryFingerprint": "inventory", "stageFingerprint": "sealed",
            "operationTenantId": "tenant-1", "captureHighWatermarks": {"owner": 4},
            "consumerCheckpoints": {"owner": {"position": 4, "token": "AQI=",
                                               "ownerSequenceNumber": 2,
                                               "capturedEventDigests": None}},
            "stageMutations": None, "stageManifestDigest": None,
            "stageExecutionStarted": False, "stageStreaming": False,
            "stageCompletedPageCount": 0, "stageSealedPageCount": 0,
            "stageReservedPageCount": 0, "cleanupStreamEpoch": 0,
            "cleanupStreamPageCount": 0, "retainedControlIndexNames": ["catalog"],
        }
        self.assertEqual(5, self.read_snapshot(snapshot)[1])

    def test_snapshot_refuses_nonopen_pending_and_offboarded_epochs_before_reading_content(self):
        for field, value in (("phase", 1), ("phase", 2), ("phase", 3), ("phase", 4),
                             ("journal", [{"failure": None}]),
                             ("offboardingAuditId", "offboarded"),
                             ("ownerSequenceHandoffPending", True)):
            reads = []

            def read(key, allow_missing=False):
                reads.append(key)
                self.assertEqual(1, len(reads))
                return self.ready_epoch() | {field: value}

            with self.subTest(field=field, value=value):
                with self.assertRaisesRegex(ValueError, "Selected epoch is not ready"):
                    selected_index_snapshot("hash", read)

    def test_snapshot_refuses_malformed_lifecycle_fields(self):
        for field, value in (("version", None), ("version", True), ("version", 2),
                             ("epoch", -1), ("epoch", 4), ("epoch", "5"),
                             ("activeGeneration", -1), ("activeGeneration", True),
                             ("activeGeneration", 2 ** 63), ("phase", "Open"),
                             ("phase", True), ("phase", 5), ("journal", None),
                             ("journal", {}), ("deliveryConflicts", None),
                             ("parkingEvidence", {}), ("offboardingAuditId", ""),
                             ("ownerSequenceHandoffPending", None)):
            with self.subTest(field=field, value=value):
                with self.assertRaises(ValueError):
                    self.read_snapshot(self.ready_epoch() | {field: value})
        for invalid in (None, [], "invalid"):
            with self.subTest(epoch_type=type(invalid).__name__):
                with self.assertRaises(ValueError):
                    self.read_snapshot(invalid)
        # Generation is a signed Int64 in the SDK, not the v6 page ordinal Int32.
        large = self.ready_epoch() | {"epoch": 2 ** 40, "activeGeneration": 2 ** 40}
        self.assertEqual(2 ** 40, self.read_snapshot(large)[1])

    def test_snapshot_refuses_unresolved_or_overflow_readiness_evidence(self):
        for evidence in ({"deliveryConflicts": [self.conflict()]},
                         {"parkingEvidence": [self.parking()]},
                         {"deliveryConflictOverflow": self.conflict()},
                         {"parkingEvidenceOverflow": self.parking()}):
            with self.subTest(evidence=list(evidence)):
                with self.assertRaises(ValueError):
                    self.read_snapshot(self.ready_epoch() | evidence)

    def test_snapshot_admits_resolved_conflicts_and_parking(self):
        resolutions = ({"resolvedByOperationId": "repair"},
                       {"requiredOwnerSequenceNumber": 2, "resolvedByOwnerSequenceNumber": 2})
        for resolution in resolutions:
            with self.subTest(resolution=resolution):
                epoch = self.ready_epoch() | {
                    "deliveryConflicts": [self.conflict() | resolution],
                    "parkingEvidence": [self.parking() | {"resolvedByOperationId": "repair"}]}
                self.assertEqual(5, self.read_snapshot(epoch)[1])

    def test_snapshot_refuses_malformed_resolved_readiness_evidence(self):
        conflict = self.conflict() | {"resolvedByOperationId": "repair"}
        parking = self.parking() | {"resolvedByOperationId": "repair"}
        invalid = []
        for field, value in (("sourceStream", None), ("sourceStream", "é" * 257),
                             ("acceptedDigest", None), ("conflictingDigest", "x" * 64),
                             ("observedEpoch", 6), ("envelopePosition", -1),
                             ("requiredOwnerSequenceNumber", 0),
                             ("resolvedByOwnerSequenceNumber", 1),
                             ("resolvedByOperationId", "")):
            invalid.append({"deliveryConflicts": [conflict | {field: value}]})
        invalid.append({"deliveryConflicts": [conflict | {
            "requiredOwnerSequenceNumber": 2, "resolvedByOwnerSequenceNumber": 1}]})
        for field, value in (("failureCount", 0), ("failureCount", True),
                             ("failureCount", 1001), ("resolvedByOperationId", " ")):
            invalid.append({"parkingEvidence": [parking | {field: value}]})
        invalid.extend(({"deliveryConflicts": [None]}, {"parkingEvidence": [None]},
                        {"deliveryConflicts": [conflict] * 129},
                        {"parkingEvidence": [parking] * 129}))
        for case, evidence in enumerate(invalid):
            with self.subTest(case=case):
                with self.assertRaises(ValueError):
                    self.read_snapshot(self.ready_epoch() | evidence)

    def test_snapshot_rechecks_epoch_even_when_selected_root_and_generation_are_unchanged(self):
        for change in ({"phase": 2}, {"journal": [{}]}, {"offboardingAuditId": "offboard"},
                       {"deliveryConflicts": [self.conflict()]},
                       {"operationId": "changed-but-still-open"}):
            epoch = self.ready_epoch()
            reads = []

            def read(key, allow_missing=False):
                reads.append(key)
                if key.startswith("shared-epoch:"):
                    return epoch
                if allow_missing:
                    return None
                epoch.update(change)
                return {"storageVersion": 6}

            with self.subTest(change=list(change)):
                with self.assertRaises(ValueError):
                    selected_index_snapshot("hash", read)
                self.assertEqual(4, len(reads))
                self.assertEqual(reads[0], reads[-1])

    def test_final_candidate_recheck_refuses_new_readiness_failure_without_sensitive_diagnostics(self):
        token_hash, shard, _, index, pages = self.legacy_shard()
        sensitive = "sensitive-owner-token-hash-payload"
        epoch = self.ready_epoch() | {"offboardingAuditId": sensitive}
        diagnostics = []
        self.assertIsNone(selected_candidate(
            token_hash, index, 5, "scope", shard, pages.__getitem__,
            lambda _: self.read_snapshot(epoch, index), diagnostics.append))
        self.assertEqual(["legacy-shard-invalid-or-unavailable"], diagnostics)
        with self.assertRaises(ValueError) as caught:
            self.read_snapshot(epoch, index)
        self.assertNotIn(sensitive, str(caught.exception))
        self.assertNotIn(sensitive, " ".join(diagnostics))

    def test_diagnostic_capacity_guard_conservatively_refuses_large_typed_json(self):
        self.assertEqual(5, self.read_snapshot(self.ready_epoch() | {"operationId": "x" * 1024})[1])
        # The SDK serializes these characters as six-byte Unicode escapes.
        # Counting only the raw JSON or Unicode character count misses capacity.
        for content in ("<" * (90 * 1024), "😀" * (45 * 1024), "x" * (505 * 1024)):
            with self.subTest(character=content[0]):
                with self.assertRaisesRegex(ValueError, "diagnostic capacity"):
                    self.read_snapshot(self.ready_epoch() | {"operationId": content})

    def test_snapshot_refuses_excessive_depth_before_copying_or_reading_content(self):
        sensitive = "sensitive-owner-token-hash-payload"
        nested = sensitive
        for _ in range(500):
            nested = {"nested": nested}
        epoch = self.ready_epoch() | {"unknown": nested}
        reads = []

        def read(key, allow_missing=False):
            reads.append(key)
            self.assertEqual(1, len(reads))
            return epoch

        with self.assertRaises(ValueError) as caught:
            selected_index_snapshot("hash", read)
        self.assertEqual("Selected epoch shape is invalid", str(caught.exception))
        self.assertNotIn(sensitive, str(caught.exception))
        self.assertEqual(1, len(reads))


if __name__ == "__main__":
    unittest.main()
