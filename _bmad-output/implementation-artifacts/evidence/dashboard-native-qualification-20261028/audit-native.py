"""Recheck the retained successful native run without operating any runtime."""

import gzip
import hashlib
import json
from pathlib import Path
import re
import sys


EVIDENCE = Path(__file__).resolve().parent
RUN = EVIDENCE / "runtime/run-4y_0l_yg"


def read(path):
    """Read a receipt, including its losslessly compressed representation."""
    if path.exists():
        return path.read_text()
    return gzip.decompress(path.with_name(path.name + ".gz").read_bytes()).decode()


def receipt(name):
    """Read a JSON receipt from the native fixture."""
    return json.loads(read(RUN / name))


def audit():
    """Check identity, ordering, outcome, cleanup and explicit visibility limits."""
    if sys.flags.optimize:
        raise RuntimeError("Run this assertion-based evidence check without Python optimization")
    manifest = json.loads(read(EVIDENCE / "native-receipt-manifest.json"))["files"]
    actual = {str(path.relative_to(EVIDENCE)) for path in RUN.iterdir() if path.is_file()}
    assert set(manifest) == actual and len(manifest) == 103
    for name, expected_file in manifest.items():
        stored = (EVIDENCE / name).read_bytes()
        original = gzip.decompress(stored) if name.endswith(".gz") else stored
        assert len(stored) == expected_file["stored_bytes"]
        assert len(original) == expected_file["original_bytes"]
        assert hashlib.sha256(stored).hexdigest() == expected_file["stored_sha256"]
        assert hashlib.sha256(original).hexdigest() == expected_file["original_sha256"]
    command = json.loads(read(EVIDENCE / "native-run.json"))
    assert command["exit_code"] == 0
    assert receipt("admission.json")["admitted"] is True
    source = receipt("source-before.json")
    assert source == receipt("source-after.json")
    initial = json.loads(read(EVIDENCE / "initial.json"))
    verification = json.loads(read(EVIDENCE / "verification.json"))
    assert source["timesheets"]["head"] == initial["head"]
    for record in source.values():
        if isinstance(record, dict) and "head" in record:
            assert re.fullmatch(r"[0-9a-f]{40}", record["head"])
            assert record["diff_sha256"] == hashlib.sha256(b"").hexdigest()
            assert record["untracked_build_inputs"] == {}
    for name, digest in verification["source_sha256"].items():
        assert digest == initial["sha256"][name]
    lifecycle = receipt("dashboard-listener-lifecycle.json")
    host = receipt("runtime-host.json")
    dashboard = host["dashboard"]
    packages = json.loads(read(EVIDENCE / "restored-native-packages.json"))
    package = next(row for row in packages["packages"] if row["package"].startswith("Aspire.Dashboard.Sdk."))
    assert package["package"].endswith("/" + dashboard["version"])
    assert package["files_sha256"]["tools/Aspire.Dashboard"] == dashboard["sha256"]
    assert packages["assets_sha256"] == source["files"]["src/Hexalith.Timesheets.AppHost/obj/project.assets.json"]
    orchestration = next(row for row in packages["packages"] if row["package"].startswith("Aspire.Hosting.Orchestration."))
    dcp_rows = host["dcp_processes"]
    assert dcp_rows and host["dcp"] in dcp_rows and host["dcp_controller"] in dcp_rows
    assert host["dcp_controller"]["parent"] == host["dcp"]["pid"]
    assert dashboard["parent"] == host["dcp_controller"]["pid"]
    for row in dcp_rows:
        assert orchestration["package"].endswith("/" + row["version"])
        assert row["sha256"] == orchestration["files_sha256"]["tools/dcp"]
        assert row["executable"] == host["dcp"]["executable"]
    assert host["dashboard_readiness"] == lifecycle["before_journey"]
    assert lifecycle["same_observed_listener_identity"] is True
    assert lifecycle["fixture_qualification_complete"] is False
    assert lifecycle["exclusivity_established"] is False
    assert lifecycle["unobserved_transfer_excluded"] is False
    expected = lifecycle["before_journey"]["ownership"]
    assert type(dashboard["pid"]) is int and dashboard["pid"] > 0
    assert dashboard["start"].isdigit()
    assert expected["socket_inodes"] and all(inode.isdigit() and int(inode) > 0 for inode in expected["socket_inodes"])
    assert len(expected["socket_inodes"]) == len(set(expected["socket_inodes"]))
    samples = []
    for phase in ("before_journey", "after_journey"):
        observed = lifecycle[phase]
        assert observed["ready"] is True and observed["ownership"] == expected
        assert observed["visibility"]["scope"] == "observable_process_descriptors"
        assert observed["loopback"] is True and observed["scheme"] in ("http", "https")
        assert type(observed["port"]) is int and 1024 <= observed["port"] <= 65535
        assert observed["port"] == lifecycle["before_journey"]["port"]
        assert observed["path"] == "/health" and observed["connect_host"] in ("127.0.0.1", "::1")
        for health in ("before_health", "after_health"):
            row = observed["ownership_observations"][health]
            assert row["verified"] is True and row["stage"] == "complete"
            assert row["pid"] == dashboard["pid"] and row["start"] == dashboard["start"]
            assert row["socket_inodes"] == expected["socket_inodes"]
            assert row["socket_owners"] == {inode: [dashboard["pid"]] for inode in row["socket_inodes"]}
            assert row["expected_identity"] == {"pid": dashboard["pid"], "start": dashboard["start"]}
            assert row["owner_identities"] == [{"pid": dashboard["pid"], "start": dashboard["start"], "identity_visibility": "observed"}]
            for boundary in ("before_inventory", "after_inventory"):
                identity = row["expected_process_observations"][boundary]
                assert identity["pid"] == dashboard["pid"] and identity["start"] == dashboard["start"]
                assert identity["state"] in ("R", "S", "D", "I", "T", "t", "W", "P")
            assert row["loopback_hosts"] == expected["loopback_hosts"]
            assert row["loopback_hosts"] and set(row["loopback_hosts"]) <= {"127.0.0.1", "::1"}
            assert observed["connect_host"] in row["loopback_hosts"]
            assert row["visibility"]["socket_tables_complete"] is True
            assert row["visibility"]["descriptor_scan_completed"] is True
            assert row["visibility"]["exclusivity_established"] is False
            assert row["visibility"]["unobserved_transfer_excluded"] is False
            assert row["observation_started_unix_ns"] <= row["observation_completed_unix_ns"]
            samples.append({"phase": phase, "health": health, **row})
    outcome = lifecycle["journey_outcome"]
    assert all(outcome[key] is True for key in ("command_completed", "validation_completed", "journey_succeeded"))
    assert outcome["command_exit_code"] == outcome["exit_code_before_listener_observation"] == 0
    for left, right in zip(samples, samples[1:]):
        assert left["observation_completed_unix_ns"] <= right["observation_started_unix_ns"]
    assert samples[1]["observation_completed_unix_ns"] <= outcome["command_started_unix_ns"]
    assert outcome["command_completed_unix_ns"] <= samples[2]["observation_started_unix_ns"]
    journey = read(RUN / "58-Hexalith.Timesheets.IntegrationTests--class.log")
    match = re.search(r"Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: ([0-9.]+)s", journey)
    assert match is not None
    gateway = receipt("gateway-process-replacement.json")
    assert all(gateway[key] is True for key in ("identity_replaced", "same_dcp_parent", "old_identity_exited"))
    for phase in ("before", "after"):
        assert gateway[phase] == receipt("gateway-process-" + phase + ".json")
        parent = gateway[phase]["parent_identity"]
        assert parent["pid"] == host["dcp_controller"]["pid"]
        assert parent["start"] == host["dcp_controller"]["start"]
        assert parent["executable"] == host["dcp_controller"]["executable"]
        assert gateway[phase]["parent"] == parent["pid"]
    assert (gateway["before"]["pid"], gateway["before"]["start"]) != (gateway["after"]["pid"], gateway["after"]["start"])
    cleanup = receipt("cleanup.json")
    assert cleanup["complete"] is True and cleanup["journey_descendants_gone"] is True
    assert cleanup["containers_removed"] == 3 and cleanup["components_removed"] is True
    exits = receipt("recorded-process-exit.json")
    assert exits["all_gone"] is True and all(row["gone"] is True for row in exits["processes"])
    gone = {(row["pid"], row["start"]) for row in exits["processes"]}
    captured = receipt("runtime-isolation.json")["bound_processes"]
    required = {(row["pid"], row["start"]) for row in captured}
    required.update((gateway[phase]["pid"], gateway[phase]["start"]) for phase in ("before", "after"))
    assert gone == required and len(gone) == len(exits["processes"])
    assert {(row["pid"], row["start"]) for row in [dashboard, *dcp_rows]} <= gone
    assert receipt("resource-exit.json") == []
    security = receipt("security-runtime.json")
    assert all(security[key] is True for key in ("loopback_only", "generated_credentials_accepted", "tenant_claims_match", "domain_claims_match", "permission_claims_match"))
    preservation = receipt("shared-preservation.json")
    assert preservation["matches"] is True and preservation["mismatches"] == []
    shared_before, shared_after = receipt("shared-before.json"), receipt("shared-after.json")
    for database, before in shared_before["redis"].items():
        after = shared_after["redis"][database]
        result = preservation["redis"][database]
        assert before["persistent_sha256"] == after["persistent_sha256"]
        assert before["persistent_keys"] == after["persistent_keys"]
        assert result["persistent"]["matches"] is True
        assert result["persistent"]["keys_before"] == before["persistent_keys"]
        assert result["persistent"]["keys_after"] == after["persistent_keys"]
        assert result["expiring"]["matches"] is True
        assert all(result["expiring"][key] == 0 for key in ("added", "missing_unexpired", "value_changes", "deadline_changes", "confirmed_natural_expirations"))
        assert before["expiring_keys"] == after["expiring_keys"]
        assert before["keys"] == before["persistent_keys"] + before["expiring_keys"]
        assert after["keys"] == after["persistent_keys"] + after["expiring_keys"]
    assert shared_before["scheduler"] == shared_after["scheduler"]
    scheduler = preservation["scheduler"]
    assert scheduler["identity_matches"] is True and scheduler["contents_match"] is True
    for phase, snapshot in (("before", shared_before), ("after", shared_after)):
        assert scheduler["keys_" + phase] == snapshot["scheduler"]["keys"]
        assert scheduler["revision_" + phase] == snapshot["scheduler"]["revision"]
    assert receipt("shared-baseline-preservation.json")["matches"] is True
    watcher = json.loads(read(EVIDENCE / "native-failure-watch.json"))
    assert watcher["completed"] is True and watcher["failure_receipts"] == {}
    assert not list(RUN.glob("dashboard-listener-*-failure.json*"))
    return {"passed": True, "manifest_receipts_verified": len(manifest), "native_dashboard": dashboard, "package_metadata": package,
            "source": {key: value for key, value in source.items() if isinstance(value, dict) and "head" in value},
            "source_before_equals_after": True, "samples": samples, "journey_seconds": float(match[1]),
            "fixture_seconds": command["elapsed_seconds"], "captured_processes_exited": len(gone),
            "cleanup": cleanup, "shared_preservation": preservation,
            "actual_native_ownership_failure_observed": False,
            "native_failure_cleanup_retention_qualified": False,
            "inaccessible_holders_excluded": False, "unobserved_transfer_excluded": False,
            "system_wide_exclusivity_established": False,
            "offline_expiring_comparison_limit": "Raw expiring keys, values and deadlines are deliberately absent; detailed equality relies on the runner's retained preservation predicates",
            "scope": "local native samples and completed fixture; no deployed acceptance"}


if __name__ == "__main__":
    print(json.dumps(audit(), indent=2))
