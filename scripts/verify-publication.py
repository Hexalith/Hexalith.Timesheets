#!/usr/bin/env python3
"""Require the exact released version on all five official NuGet indexes."""

import argparse
import json
import re
import subprocess
import sys
import time
from pathlib import Path

from release_common import ROOT, load_manifest, require_version
from verify_publication import published_versions


def verify(version: str, attempts: int = 6, interval: int = 10) -> dict:
    version = require_version(version)
    ids = [row["id"] for row in load_manifest()]
    results = {}
    for attempt in range(attempts):
        for package_id in ids:
            try:
                present = version.lower() in published_versions(package_id)
                results[package_id] = "published" if present else "missing"
            except (ValueError, OSError) as error:
                results[package_id] = str(error)
        if all(value == "published" for value in results.values()):
            break
        if attempt + 1 < attempts:
            time.sleep(interval)
    return {"version": version, "packages": results, "complete": all(v == "published" for v in results.values())}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("version", nargs="?")
    parser.add_argument("--source-sha")
    parser.add_argument("--release-result", choices=["success", "failure", "cancelled"], default="success")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/publication-verification.json")
    try:
        args = parser.parse_args(argv)
        version = args.version
        if args.source_sha:
            if re.fullmatch("[0-9a-f]{40}", args.source_sha) is None:
                raise ValueError("Expected an exact lowercase source SHA")
            tags = subprocess.check_output(["git", "tag", "--points-at", args.source_sha, "--list", "v*"], cwd=ROOT, text=True).splitlines()
            tags = [tag for tag in tags if tag.startswith("v")]
            if not tags and args.release_result == "success":
                print("Semantic release produced no version for this source; publication verification is explicitly skipped.")
                return 0
            if len(tags) != 1:
                raise ValueError(f"Expected one exact-source release tag, found {tags}")
            tagged_version = require_version(tags[0][1:])
            if version is not None and version != tagged_version:
                raise ValueError("Requested version differs from the exact-source release tag")
            version = tagged_version
        result = verify(version)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(result, indent=2))
        if not result["complete"]:
            raise ValueError("Missing or partial publication; see package-specific results above")
        return 0
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        print(f"Publication verification failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
