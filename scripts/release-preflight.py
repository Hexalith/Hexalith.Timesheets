#!/usr/bin/env python3
"""Prove exact green main, protected release settings, inventory and unused versions."""

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
from pathlib import Path

from release_common import BUILDS_SHA, MANIFEST, ROOT, checksums, inventory, load_manifest, read_json, require_version
from verify_publication import published_versions

REPOSITORY = "Hexalith/Hexalith.Timesheets"
IDENTITY = ROOT / ".hexalith/release/identity.json"


def github(path: str) -> dict:
    result = subprocess.run(["gh", "api", path], capture_output=True, text=True, check=False)
    if result.returncode:
        raise ValueError(f"GitHub prerequisite could not be read: {path}")
    payload = json.loads(result.stdout)
    if not isinstance(payload, dict):
        raise ValueError(f"Invalid GitHub response: {path}")
    return payload


def check_source(allow_frozen: bool = False, require_publish_flag: bool = False) -> bool:
    repository, sha = os.environ.get("GITHUB_REPOSITORY"), os.environ.get("GITHUB_SHA", "")
    if repository != REPOSITORY or re.fullmatch("[0-9a-f]{40}", sha) is None:
        raise ValueError("Release must run in the Timesheets repository at an exact commit")
    if os.environ.get("GITHUB_REF") != "refs/heads/main":
        raise ValueError("Release dispatch must select main")
    head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    if head != sha or github(f"repos/{repository}/git/ref/heads/main").get("object", {}).get("sha") != sha:
        raise ValueError("Release source is stale or differs from the dispatched main SHA")
    for workflow in ("ci.yml", "commitlint.yml", "codeql.yml"):
        # The runs list's branch, event, head_sha, and status filters currently
        # return an empty page even when the unfiltered page contains the
        # matching successful push. Scan that page and keep the predicates here.
        runs = github(f"repos/{repository}/actions/workflows/{workflow}/runs?per_page=100")
        if not any(run.get("head_sha") == sha and run.get("head_branch") == "main"
                   and run.get("event") == "push" and run.get("status") == "completed"
                   and run.get("conclusion") == "success" for run in runs.get("workflow_runs", [])):
            raise ValueError(f"No successful push CI exists for {workflow} at the exact current main SHA")
    # GITHUB_TOKEN has no Variables permission. The caller supplies the flag from
    # GitHub's vars context, and the pinned shared release checks it again before
    # Semantic Release. Do not turn a forbidden REST settings read into a freeze.
    if require_publish_flag and os.environ.get("HEXALITH_RELEASE_PUBLISH_ENABLED") != "true":
        if allow_frozen:
            print("Publication is frozen: repository HEXALITH_RELEASE_PUBLISH_ENABLED is not exactly 'true'.")
            return False
        raise ValueError("Publication is frozen")
    environment = github(f"repos/{repository}/environments/production")
    if not any(rule.get("type") == "required_reviewers" and rule.get("reviewers")
               for rule in environment.get("protection_rules", [])):
        raise ValueError("production must have required-reviewer protection")
    branch_policy = environment.get("deployment_branch_policy")
    if not isinstance(branch_policy, dict) or not (branch_policy.get("protected_branches") is True or branch_policy.get("custom_branch_policies") is True):
        raise ValueError("production must restrict deployment branches")
    return True


def candidate_identity(version: str) -> dict:
    return {
        "repository": REPOSITORY, "source": os.environ.get("GITHUB_SHA"), "version": version,
        "builds": BUILDS_SHA, "manifestSha256": hashlib.sha256(MANIFEST.read_bytes()).hexdigest(),
    }


def version_preflight(phase: str, version: str) -> None:
    version = require_version(version)
    if (os.environ.get("HEXALITH_BUILDS_EXECUTION_SHA") != BUILDS_SHA
        or os.environ.get("HEXALITH_RELEASE_ENVIRONMENT") != "production"
        or os.environ.get("HEXALITH_RELEASE_SOURCE_BRANCH") != "main"
        or os.environ.get("HEXALITH_RELEASE_SOURCE_CI_WORKFLOW") != "ci.yml"
        or os.environ.get("HEXALITH_RELEASE_PACKAGE_MANIFEST") != "tools/release-packages.json"
        or os.environ.get("HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT") != "6"
        or os.environ.get("HEXALITH_RELEASE_REQUIRE_AUTHORITY") != "false"):
        raise ValueError("Shared release inputs differ from the approved Timesheets contract")
    if not os.environ.get("NUGET_API_KEY", "").strip():
        raise ValueError("NUGET_API_KEY is required before creating a release tag")
    check_source()
    rows = load_manifest()
    for row in rows:
        if version.lower() in published_versions(row["id"]):
            raise ValueError(f"Occupied NuGet version: {row['id']} {version}")
    identity = candidate_identity(version)
    if phase == "verify":
        IDENTITY.parent.mkdir(parents=True, exist_ok=True)
        if IDENTITY.exists() and read_json(IDENTITY) != identity:
            raise ValueError("A different release identity already exists")
        IDENTITY.write_text(json.dumps(identity, sort_keys=True) + "\n", encoding="utf-8")
    else:
        frozen = read_json(IDENTITY)
        if any(frozen.get(key) != value for key, value in identity.items()):
            raise ValueError("Source, version, Builds or manifest changed after verification")
        directory = ROOT / "nupkgs"
        inventory(directory, rows, version)
        ledger = checksums(directory)
        if (directory / "release-artifacts.sha256").read_text(encoding="utf-8") != ledger:
            raise ValueError("Candidate package bytes changed")
        digest = hashlib.sha256(ledger.encode()).hexdigest()
        if phase == "prepare":
            frozen["artifactsSha256"] = digest
            IDENTITY.write_text(json.dumps(frozen, sort_keys=True) + "\n", encoding="utf-8")
        elif frozen.get("artifactsSha256") != digest:
            raise ValueError("Candidate artifacts changed after prepare")
    print(f"Release {phase} prerequisites passed for six packages at {version}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("phase", choices=["source", "verify", "prepare", "publish"])
    parser.add_argument("version", nargs="?")
    parser.add_argument("--allow-frozen", action="store_true")
    try:
        args = parser.parse_args()
        if args.phase == "source":
            enabled = check_source(args.allow_frozen, require_publish_flag=True)
            if os.environ.get("GITHUB_OUTPUT"):
                with Path(os.environ["GITHUB_OUTPUT"]).open("a", encoding="utf-8") as stream:
                    stream.write(f"publish-enabled={str(enabled).lower()}\n")
        else:
            version_preflight(args.phase, args.version)
    except (ValueError, OSError, KeyError, subprocess.CalledProcessError) as error:
        print(f"Release preflight refused: {error}", file=sys.stderr)
        sys.exit(1)
