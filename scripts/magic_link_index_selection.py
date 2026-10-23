"""Read-only verification of a selected magic-link index candidate."""

import hashlib
import json
import base64
from copy import deepcopy


_V7_PREFIX = "timesheets:magic-links:token-hash-capability-index:v7:"
_EPOCH_READY_BYTES = 504 * 1024


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


def _long_integer(value):
    return type(value) is int and 0 <= value <= 2 ** 63 - 1


def _epoch_size_bound(value, depth=0):
    """Conservatively bound default-encoded typed JSON, including omitted defaults.

    Each object gets 1 KiB for members supplied by typed deserialization; the
    caller adds another 8 KiB for the epoch's optional members. This intentionally
    refuses some large diagnostic snapshots before the SDK's capacity boundary.
    """
    if depth > 64:
        raise ValueError("Selected epoch shape is invalid")
    if isinstance(value, dict):
        return 1024 + 2 + sum(len(_contract_json(key)) + 2
                              + _epoch_size_bound(item, depth + 1)
                              for key, item in value.items())
    if isinstance(value, list):
        return 2 + sum(1 + _epoch_size_bound(item, depth + 1) for item in value)
    if isinstance(value, str):
        return len(_contract_json(value))
    if value is None:
        return 4
    if type(value) is bool:
        return 5
    if type(value) is int and -(2 ** 63) <= value <= 2 ** 63 - 1:
        return len(str(value))
    raise ValueError("Selected epoch shape is invalid")


def _admit_typed_epoch(epoch):
    """Validate the supported Open/drained subset of the SDK state and global scope.

    Chunk/staging protocols deliberately remain unsupported. These local checks
    neither share the SDK's private validators nor provide ETag admission.
    """
    refusal = "Selected epoch typed state or scope is invalid or unsupported"

    def refuse():
        raise ValueError(refusal)

    for name in ("captureHighWatermarks", "consumerCheckpoints"):
        if not isinstance(epoch.get(name), dict):
            refuse()
    for name in ("requiredWriters", "registeredWriters", "stageKeys", "deliveryConflicts", "parkingEvidence"):
        if not isinstance(epoch.get(name), list):
            refuse()
    anchor = epoch.get("copyOnWriteAnchorGeneration")
    if anchor is not None and (not _long_integer(anchor) or anchor > epoch["activeGeneration"]):
        refuse()
    for name in ("operationId", "inventoryFingerprint", "stageFingerprint", "operationTenantId"):
        if epoch.get(name) is not None and not _text(epoch[name]):
            refuse()
    expected = "timesheets-token-candidate-index"
    for name in ("requiredWriters", "registeredWriters"):
        writers = epoch[name]
        if any(writer != expected for writer in writers) or len(writers) > 1:
            refuse()
    if set(epoch["registeredWriters"]) - set(epoch["requiredWriters"]):
        refuse()
    if any(not _text(key) or not _long_integer(position)
           for key, position in epoch["captureHighWatermarks"].items()):
        refuse()

    def byte_string(value):
        if not isinstance(value, str):
            return False
        try:
            base64.b64decode(value, validate=True)
            return True
        except ValueError:
            return False

    for key, checkpoint in epoch["consumerCheckpoints"].items():
        if (not _text(key) or not isinstance(checkpoint, dict)
                or not _long_integer(checkpoint.get("position"))
                or not byte_string(checkpoint.get("token"))
                or not _long_integer(checkpoint.get("ownerSequenceNumber", 0))
                or checkpoint.get("capturedEventDigests") is not None
                    and not byte_string(checkpoint["capturedEventDigests"])
                or checkpoint.get("capturedIdentityReference") is not None):
            refuse()
        captured = checkpoint.get("capturedEventDigests")
        if captured is not None:
            length = len(base64.b64decode(captured, validate=True))
            if not length or length % 32 or length // 32 > checkpoint.get("ownerSequenceNumber", 0):
                refuse()
    retained = epoch.get("retainedControlIndexNames")
    if retained is not None and (not isinstance(retained, list) or len(retained) > 8
                                or any(not _text(name) for name in retained)
                                or len(set(retained)) != len(retained)):
        refuse()
    # Open snapshots with outstanding staging/chunk/cleanup data need the SDK's
    # authoritative protocol; local diagnostics refuse them conservatively.
    for name in ("stageMutations", "stageManifestDigest", "stageControlIndexIntent",
                 "stageChunks", "cleanupChunks", "captureChunks", "cleanupPreparedChunks",
                 "cleanupStreamOperationId"):
        if epoch.get(name) is not None:
            refuse()
    if epoch["stageKeys"]:
        refuse()
    for name in ("stageExecutionStarted", "stageStreaming", "cleanupPromotedRetirements"):
        if epoch.get(name, False) is not False:
            refuse()
    for name in ("stageCompletedPageCount", "stageSealedPageCount", "stageReservedPageCount",
                 "cleanupStreamEpoch", "cleanupStreamPageCount"):
        value = epoch.get(name, 0)
        if type(value) is not int or value != 0:
            refuse()


def _admit_epoch(epoch):
    """Require a drained, available epoch without degraded readiness evidence.

    This is conservative local evidence, not the SDK's typed validation or ETag
    admission. In particular, an unchanged JSON snapshot cannot prove that its
    ETag stayed unchanged between reads.
    """
    if (not isinstance(epoch, dict) or type(epoch.get("version")) is not int
            or epoch["version"] != 1 or not _long_integer(epoch.get("epoch"))
            or not _long_integer(epoch.get("activeGeneration"))
            or epoch["activeGeneration"] > epoch["epoch"]
            or type(epoch.get("phase")) is not int or epoch["phase"] != 0
            or not isinstance(epoch.get("journal"), list) or epoch["journal"]
            or epoch.get("offboardingAuditId") is not None
            or epoch.get("ownerSequenceHandoffPending", False) is not False):
        raise ValueError("Selected epoch is not ready")
    _admit_typed_epoch(epoch)
    conflicts = epoch["deliveryConflicts"]
    parking = epoch["parkingEvidence"]
    if (not isinstance(conflicts, list) or len(conflicts) > 128
            or not isinstance(parking, list) or len(parking) > 128
            or epoch.get("deliveryConflictOverflow") is not None
            or epoch.get("parkingEvidenceOverflow") is not None):
        raise ValueError("Selected epoch readiness evidence is invalid or degraded")

    def valid_identity(item):
        return (isinstance(item, dict) and _text(item.get("sourceStream"))
                and len(item["sourceStream"].encode()) <= 512
                and _long_integer(item.get("envelopePosition"))
                and _long_integer(item.get("observedEpoch"))
                and item["observedEpoch"] <= epoch["epoch"]
                and (item.get("resolvedByOperationId") is None
                     or _text(item["resolvedByOperationId"])))

    for conflict in conflicts:
        if not valid_identity(conflict):
            raise ValueError("Selected epoch conflict evidence is invalid")
        required = conflict.get("requiredOwnerSequenceNumber")
        resolved = conflict.get("resolvedByOwnerSequenceNumber")
        if (any(not isinstance(conflict.get(name), str)
                or len(conflict[name]) != 64
                or any(character not in "0123456789ABCDEFabcdef" for character in conflict[name])
                for name in ("acceptedDigest", "conflictingDigest"))
                or required is not None and (not _long_integer(required) or required == 0)
                or resolved is not None and (not _long_integer(resolved) or required is None
                                             or resolved != required)
                or conflict.get("resolvedByOperationId") is None and resolved is None):
            raise ValueError("Selected epoch conflict evidence is invalid or unresolved")
    for parked in parking:
        if (not valid_identity(parked) or type(parked.get("failureCount")) is not int
                or not 1 <= parked["failureCount"] <= 1000
                or parked.get("resolvedByOperationId") is None):
            raise ValueError("Selected epoch parking evidence is invalid or unresolved")
    if 8 * 1024 + _epoch_size_bound(epoch) > _EPOCH_READY_BYTES:
        raise ValueError("Selected epoch diagnostic capacity is unavailable")


def selected_index_snapshot(token_hash, read_model):
    """Read a ready, stable selection; only an explicitly absent marker permits v6."""
    family = "magic-link-token-hash-capability-index"
    components = ["timesheets-readmodels", "global-index", "timesheets", family, family, "shared-epoch"]
    scope_bytes = "".join(f"{len(value)}:{value}" for value in components).encode()
    scope_hash = base64.urlsafe_b64encode(hashlib.sha256(scope_bytes).digest()).rstrip(b"=").decode()
    epoch_key = "shared-epoch:" + scope_hash
    raw_epoch = read_model(epoch_key)
    _admit_epoch(raw_epoch)
    epoch = deepcopy(raw_epoch)
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
    index = read_model(_physical(scope_hash, generation, logical_key))
    after = read_model(epoch_key)
    _admit_epoch(after)
    if after != epoch:
        raise ValueError("Selected epoch changed during the diagnostic read")
    return index, generation, scope_hash, shard


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
