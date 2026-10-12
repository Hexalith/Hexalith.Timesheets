"""Read-only verification of a selected magic-link index candidate."""

import hashlib
import json
import base64


_V7_PREFIX = "timesheets:magic-links:token-hash-capability-index:v7:"


def _physical(scope_hash, generation, logical_key):
    return (f"shared-generation:{scope_hash}:{generation}:"
            + hashlib.sha256(logical_key.encode()).hexdigest().upper())


def _pascal(value):
    if isinstance(value, dict):
        return {key[:1].upper() + key[1:]: _pascal(item) for key, item in value.items()}
    if isinstance(value, list):
        return [_pascal(item) for item in value]
    return value


def _path(hash_value, candidate):
    def encode(value):
        return base64.b64encode(value.encode()).rstrip(b"=").decode()
    return (hash_value + "!" + encode(candidate["tenant"]["tenantId"])
            + "!" + encode(candidate["capabilityId"]["value"]))


def _read_v7_node(reference, scope_hash, read_model):
    if (not isinstance(reference, dict) or reference.get("generation", -1) < 0
            or reference.get("nodeId", -1) < 0 or len(reference.get("digest", "")) != 64):
        raise ValueError("Invalid numeric node reference")
    key = _V7_PREFIX + f"node:{reference['nodeId']:016d}"
    node = read_model(_physical(scope_hash, reference["generation"], key))
    children = node["children"]
    if not isinstance(children, dict) or len(children) > 256:
        raise ValueError("Invalid numeric node children")
    canonical = {
        "Prefix": node["prefix"], "Row": _pascal(node.get("row")),
        "Roster": _pascal(node.get("roster")),
        "Children": [{"Edge": edge, "Generation": child["generation"],
                      "NodeId": child["nodeId"], "Digest": child["digest"]}
                     for edge, child in sorted(children.items())],
    }
    digest = hashlib.sha256(json.dumps(canonical, separators=(",", ":"),
                                       ensure_ascii=True).encode()).hexdigest().upper()
    if digest != reference["digest"]:
        raise ValueError("Numeric node content does not match its selected reference")
    return node


def _selected_v7_candidate(token_hash, root, generation, scope_hash, read_model, read_snapshot):
    prefix = token_hash + "!"
    candidates = []
    visited = set()

    def visit(reference, walked):
        if reference is None or len(candidates) >= 2:
            return
        identity = (reference["generation"], reference["nodeId"])
        if identity in visited:
            raise ValueError("Numeric node is reused within one lookup")
        visited.add(identity)
        node = _read_v7_node(reference, scope_hash, read_model)
        walked += node["prefix"]
        if not (prefix.startswith(walked) or walked.startswith(prefix)):
            return
        row = node.get("row")
        if row is not None:
            candidate = row["candidate"]
            if row["tokenHash"] != token_hash or _path(token_hash, candidate) != walked:
                raise ValueError("Numeric leaf identity is inconsistent")
            if candidate in candidates:
                raise ValueError("Numeric leaf repeats a candidate")
            candidates.append(candidate)
        if node.get("roster") is not None:
            raise ValueError("Tenant roster appeared in the token tree")
        for edge, child in sorted(node["children"].items()):
            visit(child, walked + edge)

    try:
        if root.get("storageVersion") != 7:
            return None
        visit(root.get("tokenRoot"), "")
        current, current_generation, _, _ = read_snapshot(token_hash)
        return (candidates[0] if len(candidates) == 1 and current_generation == generation
                and current == root else None)
    except (KeyError, TypeError, ValueError, RuntimeError):
        return None


def selected_candidate(token_hash, index, generation, scope_hash, shard, read_model, read_snapshot):
    """Return a unique proven owner only while the selected root stays unchanged."""
    if index.get("storageVersion") == 7:
        return _selected_v7_candidate(token_hash, index, generation, scope_hash,
                                      read_model, read_snapshot)
    if index.get("storageVersion") != 6 or index.get("legacyContributionsUnverified"):
        return None
    if token_hash in index.get("candidatePageCounts", {}):
        return None
    owners = index.get("contributions", {}).get(token_hash, [])
    candidate = index.get("entries", {}).get(token_hash)
    if owners:
        candidate = candidate if len(owners) == 1 and candidate == owners[0] else None
    else:
        if candidate is not None:
            return None
        candidates = []
        prior_row = None
        for reference in index.get("directoryPages", []):
            if not reference["firstHash"] <= token_hash <= reference["lastHash"]:
                continue
            page_key = (f"timesheets:magic-links:token-hash-capability-index:v5:shard:"
                        f"{shard:02X}:page:{reference['pageId']:06d}")
            physical_key = (f"shared-generation:{scope_hash}:{generation}:"
                            + hashlib.sha256(page_key.encode()).hexdigest().upper())
            rows = read_model(physical_key)["entries"]
            serialized = [[row["tokenHash"], row["candidate"]["tenant"]["tenantId"],
                           row["candidate"]["capabilityId"]["value"]] for row in rows]
            digest = hashlib.sha256(json.dumps(serialized, separators=(",", ":")).encode()).hexdigest().upper()
            if (not rows or len(rows) > 64
                    or rows[0]["tokenHash"] != reference["firstHash"]
                    or rows[-1]["tokenHash"] != reference["lastHash"]
                    or index["directoryPageDigests"].get(str(reference["pageId"])) != digest
                    or serialized != sorted(serialized)
                    or len(set(tuple(row) for row in serialized)) != len(serialized)
                    or prior_row is not None and prior_row >= serialized[0]):
                return None
            prior_row = serialized[-1]
            candidates.extend(row["candidate"] for row in rows if row["tokenHash"] == token_hash)
        candidate = candidates[0] if len(candidates) == 1 else None

    current, current_generation, _, _ = read_snapshot(token_hash)
    return candidate if current_generation == generation and current == index else None
