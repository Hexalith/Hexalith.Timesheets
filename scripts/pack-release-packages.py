#!/usr/bin/env python3
"""Build and pack the exact manifest at its final version in Release package mode."""

import argparse
import subprocess
import sys
from pathlib import Path

from release_common import ROOT, checksums, inventory, load_manifest, require_version


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output_directory", type=Path)
    parser.add_argument("version")
    args = parser.parse_args()
    version, rows = require_version(args.version), load_manifest()
    output = args.output_directory.resolve()
    output.mkdir(parents=True, exist_ok=True)
    if list(output.iterdir()):
        raise ValueError("Use an empty package output directory; existing artifacts are never overwritten")
    for row in rows:
        subprocess.run([
            "dotnet", "pack", row["project"], "--configuration", "Release", "--no-restore",
            "--output", str(output), f"-p:Version={version}", f"-p:PackageVersion={version}",
            f"-p:MinVerVersionOverride={version}", "-p:UseHexalithProjectReferences=false",
            "-p:ContinuousIntegrationBuild=true", "-warnaserror", "-m:1", "/nr:false",
        ], cwd=ROOT, check=True)
    inventory(output, rows, version)
    (output / "release-artifacts.sha256").write_text(checksums(output), encoding="utf-8")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        print(f"Packing failed: {error}", file=sys.stderr)
        sys.exit(1)
