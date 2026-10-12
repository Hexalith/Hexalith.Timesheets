"""Narrow selected-index checks without a running Dapr application."""

import hashlib
import json
import unittest

from magic_link_index_selection import _path, _physical, selected_candidate


class SelectedIndexTests(unittest.TestCase):
    def test_numeric_path_matches_standard_base64_encoding(self):
        candidate = {"tenant": {"tenantId": "\u083e"},
                     "capabilityId": {"value": "capability-1"}}
        self.assertEqual("hash!4KC+!Y2FwYWJpbGl0eS0x", _path("hash", candidate))

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
        self.assertEqual(1, len(keys))
        self.assertIn(":4:", keys[0])
        self.assertIsNone(selected_candidate(token_hash, root, 5, "scope", 0,
                                              lambda _: node | {"prefix": "altered"}, snapshot))
        self.assertIsNone(selected_candidate(token_hash, root, 5, "scope", 0,
                                              read_node, lambda _: (root, 6, "scope", 0)))

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
                 "directoryPageDigests": {"2": digest}}
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


if __name__ == "__main__":
    unittest.main()
