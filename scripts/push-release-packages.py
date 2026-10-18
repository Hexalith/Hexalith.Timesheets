#!/usr/bin/env python3
"""Publish the unchanged validated manifest archives without duplicate suppression."""

import importlib.util
import hashlib
import json
import os
import subprocess
import sys

from release_common import ROOT, checksums, inventory, load_manifest, read_json, require_version
from verify_publication import published_versions


def load_preflight():
    spec = importlib.util.spec_from_file_location("release_preflight", ROOT / "scripts/release-preflight.py")
    preflight = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(preflight)
    return preflight


def main(version: str) -> None:
    require_version(version)
    preflight = load_preflight()
    preflight.version_preflight("publish", version)
    directory, rows = ROOT / "nupkgs", load_manifest()
    inventory(directory, rows, version)
    expected = checksums(directory)
    frozen_identity = {
        **preflight.candidate_identity(version),
        "artifactsSha256": hashlib.sha256(expected.encode()).hexdigest(),
    }
    if read_json(preflight.IDENTITY) != frozen_identity:
        raise ValueError("Candidate identity or package bytes changed during publication")
    # Retain only the known public identity fields and archive hashes even if a
    # later NuGet write fails before GitHub release assets can be uploaded.
    print("Frozen release identity: " + json.dumps(frozen_identity, sort_keys=True), flush=True)
    print("Release artifact SHA256 ledger:\n" + expected, end="", flush=True)
    for row in rows:
        # Revalidate source/protection and unchanged bytes at each irreversible write.
        preflight.check_source()
        if (checksums(directory) != expected or read_json(preflight.IDENTITY) != frozen_identity
            or frozen_identity != {**preflight.candidate_identity(version),
                                   "artifactsSha256": hashlib.sha256(expected.encode()).hexdigest()}):
            raise ValueError("Candidate identity or package bytes changed during publication")
        if version.lower() in published_versions(row["id"]):
            raise ValueError(f"Occupied NuGet version: {row['id']} {version}")
        archive = directory / f"{row['id']}.{version}.nupkg"
        result = subprocess.run(["dotnet", "nuget", "push", str(archive), "--source",
                                 "https://api.nuget.org/v3/index.json", "--api-key", os.environ["NUGET_API_KEY"]],
                                cwd=ROOT, check=False)
        if result.returncode:
            # Never stringify CalledProcessError: its command includes the secret API key.
            raise ValueError(f"NuGet push failed for {row['id']} with exit code {result.returncode}; inspect partial publication")


if __name__ == "__main__":
    try:
        if len(sys.argv) != 2:
            raise ValueError("Expected the exact release version")
        main(sys.argv[1])
    except (ValueError, OSError, KeyError) as error:
        print(f"Publication refused: {error}", file=sys.stderr)
        sys.exit(1)
