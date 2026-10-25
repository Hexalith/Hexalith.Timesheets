#!/usr/bin/env python3
"""Validate archive inventory, metadata and restore-backed package boundaries."""

import argparse
import sys
from pathlib import Path

from release_common import ROOT, EXPECTED_IDS, checksums, inventory, load_manifest, read_json


def validate(directory: Path) -> tuple[str, dict]:
    rows = load_manifest()
    version, packages = inventory(directory, rows)
    for row in rows:
        package = packages[row["id"]]
        assets = read_json(ROOT / Path(row["project"]).parent / "obj/project.assets.json")
        project = assets["project"]
        if Path(project["restore"]["projectPath"]).resolve() != (ROOT / row["project"]).resolve():
            raise ValueError(f"Restore assets belong to another project: {row['id']}")
        framework = project["frameworks"]["net10.0"]
        private = {name for name, dep in framework.get("dependencies", {}).items()
                   if dep.get("suppressParent", "").casefold() == "all"}
        expected = {item.split()[0] for item in assets["projectFileDependencyGroups"]["net10.0"]} - private
        expected.update(assets.get("centralTransitiveDependencyGroups", {}).get("net10.0", {}))
        if package["dependencies"].keys() != expected:
            raise ValueError(f"Dependency inventory mismatch for {row['id']}: expected {sorted(expected)}, got {sorted(package['dependencies'])}")
        for name, details in assets["libraries"].items():
            if details.get("type") == "project" and name.split('/')[0] not in EXPECTED_IDS:
                raise ValueError(f"External or unpublished source dependency leaked into restore: {name}")
        for dependency, declared in package["dependencies"].items():
            if dependency.startswith("Hexalith.Timesheets") and dependency not in EXPECTED_IDS:
                raise ValueError(f"Unshipped Timesheets dependency: {dependency}")
            if any(fragment in dependency.casefold() for fragment in (".apphost", ".tests", ".sample")):
                raise ValueError(f"Forbidden dependency: {dependency}")
            if dependency in EXPECTED_IDS:
                minimum = version
            else:
                target = assets["targets"]["net10.0"]
                matches = [key.split('/', 1)[1] for key in target if key.split('/')[0] == dependency]
                if len(matches) != 1:
                    raise ValueError(f"Dependency version is unproven: {dependency}")
                minimum = matches[0]
            if declared not in (minimum, f"[{minimum}, )", f"[{minimum}]"):
                raise ValueError(f"Dependency version mismatch: {dependency} {declared}, expected {minimum}")
    ledger = directory / "release-artifacts.sha256"
    if not ledger.is_file() or ledger.read_text(encoding="utf-8") != checksums(directory):
        raise ValueError("Package checksum ledger is missing or changed")
    print(f"Validated exactly six packages and symbols at {version}")
    return version, packages


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_directory", type=Path)
    try:
        validate(parser.parse_args().package_directory.resolve())
    except (ValueError, OSError, KeyError) as error:
        print(f"Package validation failed: {error}", file=sys.stderr)
        sys.exit(1)
