"""Read-only verification of a selected magic-link index candidate."""

import hashlib
import json
import base64


_V7_PREFIX = "timesheets:magic-links:token-hash-capability-index:v7:"


def legacy_index_diagnostics(read_optional_model):
    """Describe bootstrap obstruction without emitting a hash, owner or payload."""
    try:
        legacy = read_optional_model("timesheets:magic-links:token-hash-capability-index:v1")
        if legacy is None:
            return "legacy-direct-root=absent"
        if not isinstance(legacy, dict) or not isinstance(legacy.get("entries"), dict):
            return "legacy-direct-root=invalid"
        return f"legacy-direct-root=present-unverified entries={len(legacy['entries'])}"
    except (KeyError, TypeError, ValueError, RuntimeError):
        return "legacy-direct-root=unavailable"


def _physical(scope_hash, generation, logical_key):
    return (f"shared-generation:{scope_hash}:{generation}:"
            + hashlib.sha256(logical_key.encode()).hexdigest().upper())


def _pascal(value):
    if isinstance(value, dict):
        return {key[:1].upper() + key[1:]: _pascal(item) for key, item in value.items()}
    if isinstance(value, list):
        return [_pascal(item) for item in value]
    return value


def _contract_row(row):
    """Restore the typed C# member order after SDK JSON property sorting."""
    if row is None:
        return None
    candidate = row["candidate"]
    return {"TokenHash": row["tokenHash"], "Candidate": {
        "Tenant": {"TenantId": candidate["tenant"]["tenantId"]},
        "CapabilityId": {"Value": candidate["capabilityId"]["value"]},
    }}


def _path(hash_value, candidate):
    """Match TokenPath: UTF-8 Base64 with '+' and '/' replaced and padding removed."""
    def encode(value):
        return base64.urlsafe_b64encode(value.encode()).rstrip(b"=").decode()
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
        "Prefix": node["prefix"], "Row": _contract_row(node.get("row")),
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


def _selected_v7_candidate(token_hash, root, generation, scope_hash, read_model, read_snapshot, diagnostic=None):
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
        if len(candidates) != 1 or current_generation != generation or current != root:
            if diagnostic:
                diagnostic("candidate-count-or-selection-changed")
            return None
        return candidates[0]
    except (KeyError, TypeError, ValueError, RuntimeError) as error:
        if diagnostic:
            reason = {KeyError: "numeric-node-shape-invalid", TypeError: "numeric-node-shape-invalid",
                      ValueError: "numeric-node-integrity-invalid", RuntimeError: "numeric-node-read-unavailable"}
            diagnostic(reason.get(type(error), "numeric-node-read-unavailable"))
        return None


def selected_index_snapshot(token_hash, read_model):
    """Read the selected format; only an explicitly absent marker permits v6."""
    family = "magic-link-token-hash-capability-index"
    components = ["timesheets-readmodels", "global-index", "timesheets", family, family, "shared-epoch"]
    scope_bytes = "".join(f"{len(value)}:{value}" for value in components).encode()
    scope_hash = base64.urlsafe_b64encode(hashlib.sha256(scope_bytes).digest()).rstrip(b"=").decode()
    epoch = read_model("shared-epoch:" + scope_hash)
    generation = epoch["activeGeneration"]
    shard = hashlib.sha256(token_hash.encode()).digest()[0]
    selected_format = read_model(_physical(scope_hash, generation, _V7_PREFIX + "format"),
                                 allow_missing=True)
    if selected_format is not None:
        if (not isinstance(selected_format, dict)
                or not _integer(selected_format.get("storageVersion"))
                or selected_format["storageVersion"] != 7):
            raise ValueError("Selected numeric index format is invalid")
        logical_key = _V7_PREFIX + "root"
    else:
        logical_key = f"timesheets:magic-links:token-hash-capability-index:v2:shard:{shard:02X}"
    return read_model(_physical(scope_hash, generation, logical_key)), generation, scope_hash, shard


def _integer(value):
    return type(value) is int and -(2 ** 31) <= value < 2 ** 31


def _text(value):
    return isinstance(value, str) and bool(value.strip())


def _owner(candidate):
    tenant = candidate["tenant"]["tenantId"]
    capability = candidate["capabilityId"]["value"]
    if not _text(tenant) or not _text(capability):
        raise ValueError("Invalid candidate identity")
    return tenant, capability


def _digest(value):
    return (isinstance(value, str) and len(value) == 64
            and all(character in "0123456789ABCDEF" for character in value))


def _ordinal(value):
    """Compare strings as UTF-16 code units, matching String.CompareOrdinal."""
    if not isinstance(value, str):
        raise ValueError("Invalid ordinal string")
    return value.encode("utf-16-be")


def _content_digest(value):
    return hashlib.sha256(_contract_json(value).encode()).hexdigest().upper()


def _contract_json(value):
    """Match System.Text.Json's default encoder for v6 string-array contracts."""
    if isinstance(value, list):
        return "[" + ",".join(_contract_json(item) for item in value) + "]"
    if isinstance(value, dict):
        return "{" + ",".join(_contract_json(key) + ":" + _contract_json(item)
                               for key, item in value.items()) + "}"
    if not isinstance(value, str):
        raise ValueError("Invalid v6 digest contract")
    escaped = []
    short = {"\b": "\\b", "\t": "\\t", "\n": "\\n", "\f": "\\f", "\r": "\\r", "\\": "\\\\"}
    for character in value:
        code = ord(character)
        if character in short:
            escaped.append(short[character])
        elif 0x20 <= code < 0x7F and character not in '\"&\'<>+`':
            escaped.append(character)
        elif code <= 0xFFFF:
            if 0xD800 <= code <= 0xDFFF:
                raise ValueError("Unpaired surrogate in v6 digest contract")
            escaped.append(f"\\u{code:04X}")
        else:
            code -= 0x10000
            escaped.append(f"\\u{0xD800 + (code >> 10):04X}\\u{0xDC00 + (code & 0x3FF):04X}")
    return '"' + "".join(escaped) + '"'


def _v6_manifest(index):
    """Check the selected manifest before trusting any inline or paged owner."""
    entries = index.get("entries")
    contributions = index.get("contributions", {})
    directory = index.get("directoryPages", [])
    counts = index.get("candidatePageCounts", {})
    ids = index.get("candidatePageIds", {})
    candidate_digests = index.get("candidatePageDigests", {})
    directory_digests = index.get("directoryPageDigests", {})
    next_id = index.get("nextCandidatePageId", 0)
    if (not all(isinstance(value, dict) for value in
                (entries, contributions, counts, ids, candidate_digests, directory_digests))
            or not isinstance(directory, list) or len(entries) > 64 or len(contributions) > 64
            or not _integer(next_id) or next_id < 0 or counts.keys() != ids.keys()):
        raise ValueError("Invalid selected manifest")
    used = set()

    def admit(page_id, digests):
        if (not _integer(page_id) or not 0 <= page_id < next_id or page_id in used
                or not _digest(digests.get(str(page_id)))):
            raise ValueError("Invalid selected page reference")
        used.add(page_id)

    prior_last = None
    for reference in directory:
        first, last = reference["firstHash"], reference["lastHash"]
        if (not _text(first) or not _text(last) or _ordinal(first) > _ordinal(last)
                or prior_last is not None and _ordinal(prior_last) > _ordinal(first)):
            raise ValueError("Invalid directory range")
        admit(reference["pageId"], directory_digests)
        prior_last = last
    for token in entries.keys() | contributions.keys() | counts.keys():
        if any(_ordinal(reference["firstHash"]) <= _ordinal(token) <= _ordinal(reference["lastHash"])
               for reference in directory):
            raise ValueError("Inline and paged ranges overlap")
    if contributions.keys() & counts.keys() or entries.keys() - contributions.keys():
        raise ValueError("Invalid inline contribution membership")
    for token, owners in contributions.items():
        if not isinstance(owners, list) or not 1 <= len(owners) <= 64:
            raise ValueError("Invalid inline owners")
        identities = [_owner(owner) for owner in owners]
        if (len(set(identities)) != len(identities)
                or len(owners) == 1 and entries.get(token) != owners[0]
                or len(owners) > 1 and token in entries):
            raise ValueError("Invalid inline owner selection")
    page_count = 0
    for token, count in counts.items():
        pages = ids[token]
        if (not _text(token) or not _integer(count) or not 1 <= count <= 10000
                or not isinstance(pages, list) or len(pages) != count):
            raise ValueError("Invalid collision page count")
        for page_id in pages:
            admit(page_id, candidate_digests)
        page_count += count
    if len(candidate_digests) != page_count or len(directory_digests) != len(directory):
        raise ValueError("Unbound page digest")
    return entries, contributions, directory, ids, candidate_digests, directory_digests


def _selected_v6_candidate(token_hash, index, generation, scope_hash, shard,
                           read_model, read_snapshot, diagnostic):
    try:
        if index.get("legacyContributionsUnverified", False) is not False:
            return None
        entries, contributions, directory, ids, candidate_digests, directory_digests = _v6_manifest(index)
        if token_hash in ids:
            return None
        selected_rows = []
        previous = None
        for reference in directory:
            page_id = reference["pageId"]
            key = (f"timesheets:magic-links:token-hash-capability-index:v5:shard:"
                   f"{shard:02X}:page:{page_id:06d}")
            rows = read_model(_physical(scope_hash, generation, key))["entries"]
            if not isinstance(rows, list) or not 1 <= len(rows) <= 64:
                raise ValueError("Invalid directory page")
            serialized = [[row["tokenHash"], *_owner(row["candidate"])] for row in rows]
            if (serialized[0][0] != reference["firstHash"]
                    or serialized[-1][0] != reference["lastHash"]
                    or _content_digest(serialized) != directory_digests[str(page_id)]):
                raise ValueError("Invalid directory content binding")
            for row in serialized:
                order = tuple(_ordinal(value) for value in row)
                if (not _text(row[0]) or hashlib.sha256(row[0].encode()).digest()[0] != shard
                        or not _ordinal(reference["firstHash"]) <= order[0] <= _ordinal(reference["lastHash"])
                        or previous is not None and previous >= order):
                    raise ValueError("Invalid directory row order or scope")
                previous = order
            selected_rows.extend(row["candidate"] for row in rows if row["tokenHash"] == token_hash)
        for token, pages in ids.items():
            for page_id in pages:
                key = (f"timesheets:magic-links:token-hash-capability-index:v4:shard:"
                       f"{shard:02X}:page:{page_id:06d}")
                page = read_model(_physical(scope_hash, generation, key))
                owners = page["candidates"]
                if page["tokenHash"] != token or not isinstance(owners, list) or not 1 <= len(owners) <= 64:
                    raise ValueError("Invalid collision page")
                identities = [_owner(owner) for owner in owners]
                content = {"TokenHash": token, "Candidates": [list(owner) for owner in identities]}
                if (len(set(identities)) != len(identities)
                        or _content_digest(content) != candidate_digests[str(page_id)]):
                    raise ValueError("Invalid collision content binding")
        owners = contributions.get(token_hash, [])
        candidate = (entries.get(token_hash) if len(owners) == 1
                     else selected_rows[0] if not owners and len(selected_rows) == 1 else None)
        current, current_generation, _, _ = read_snapshot(token_hash)
        return candidate if current_generation == generation and current == index else None
    except (KeyError, TypeError, ValueError, RuntimeError):
        if diagnostic:
            diagnostic("legacy-shard-invalid-or-unavailable")
        return None


def selected_candidate(token_hash, index, generation, scope_hash, shard, read_model, read_snapshot, diagnostic=None):
    """Return a unique proven owner only while the complete selected shard is valid."""
    if not _integer(index.get("storageVersion")):
        return None
    if index.get("storageVersion") == 7:
        return _selected_v7_candidate(token_hash, index, generation, scope_hash,
                                      read_model, read_snapshot, diagnostic)
    if index.get("storageVersion") != 6:
        return None
    return _selected_v6_candidate(token_hash, index, generation, scope_hash, shard,
                                  read_model, read_snapshot, diagnostic)
