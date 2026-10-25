"""Shared, fail-closed Timesheets release inventory and archive checks."""

from __future__ import annotations

import hashlib
import json
import re
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "tools/release-packages.json"
BUILDS_SHA = "544fd2e907d62ccdc4b8f24168b8ab5dfcc13eb9"
EXPECTED_IDS = frozenset("Hexalith.Timesheets." + area for area in
                         ("Contracts", "Client", "ReadModels", "Server", "Projections", "Testing"))
VERSION = re.compile(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?")


def unique_object(pairs: list[tuple[str, object]]) -> dict:
    """Reject ambiguous duplicate JSON properties."""
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON property: {key}")
        result[key] = value
    return result


def read_json(path: Path) -> dict:
    value = json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique_object)
    if not isinstance(value, dict):
        raise ValueError(f"Expected JSON object: {path}")
    return value


def require_version(version: str) -> str:
    if not isinstance(version, str) or VERSION.fullmatch(version) is None:
        raise ValueError("Expected a plain semantic version, optionally with a prerelease suffix")
    return version


def load_manifest(manifest: Path = MANIFEST, root: Path = ROOT) -> list[dict[str, str]]:
    payload = read_json(manifest)
    rows = payload.get("packages")
    if set(payload) != {"packages"} or not isinstance(rows, list) or len(rows) != 6:
        raise ValueError("Release manifest must declare exactly six packages")
    ids, projects = set(), set()
    for row in rows:
        if not isinstance(row, dict) or set(row) != {"id", "project"}:
            raise ValueError("Each release entry requires only id and project")
        package_id, project = row["id"], row["project"]
        if not isinstance(package_id, str) or package_id not in EXPECTED_IDS:
            raise ValueError(f"Forbidden release package: {package_id!r}")
        expected = f"src/{package_id}/{package_id}.csproj"
        if project != expected or not (root / project).is_file():
            raise ValueError(f"Invalid project for {package_id}: {project!r}")
        if root.resolve() not in (root / project).resolve().parents:
            raise ValueError(f"Release project escapes the repository: {project}")
        if package_id in ids or project in projects:
            raise ValueError(f"Duplicate release entry: {package_id}")
        ids.add(package_id)
        projects.add(project)
    if ids != EXPECTED_IDS:
        raise ValueError("Release inventory drift")
    return rows


def archive_metadata(path: Path) -> dict:
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise ValueError(f"Duplicate ZIP entries: {path.name}")
        if any(Path(name).is_absolute() or ".." in Path(name).parts or "\\" in name for name in names):
            raise ValueError(f"Unsafe ZIP entry: {path.name}")
        specs = [name for name in names if name.endswith(".nuspec")]
        if len(specs) != 1:
            raise ValueError(f"Expected one nuspec: {path.name}")
        document = ET.fromstring(archive.read(specs[0]))
        for node in document.iter():
            node.tag = node.tag.rsplit("}", 1)[-1]
        metadata_nodes = document.findall("metadata")
        if document.tag != "package" or len(metadata_nodes) != 1:
            raise ValueError(f"Missing package metadata: {path.name}")
        metadata = metadata_nodes[0]
        if any(len(metadata.findall(key)) != 1 for key in ("id", "version")):
            raise ValueError(f"Ambiguous package identity: {path.name}")
        package_id = metadata.findtext("id", "")
        version = require_version(metadata.findtext("version", ""))
        readme = metadata.findtext("readme", "")
        license_node = metadata.find("license")
        if path.suffix != ".snupkg" and (not readme or readme not in names or license_node is None
                                        or license_node.text != "MIT" or license_node.get("type") != "expression"
                                        or list(license_node) or len(metadata.findall("license")) != 1
                                        or len(metadata.findall("readme")) != 1):
            raise ValueError(f"Missing README or MIT license: {path.name}")
        dependencies = {}
        parsed_dependencies = []
        dependency_nodes = metadata.findall("dependencies")
        if len(dependency_nodes) > 1:
            raise ValueError(f"Ambiguous dependency metadata: {path.name}")
        groups = list(dependency_nodes[0]) if dependency_nodes else []
        if any(node.tag != "group" for node in groups) or len(groups) > 1:
            raise ValueError(f"Malformed dependency groups: {path.name}")
        for group in groups:
            if group.get("targetFramework") != "net10.0":
                raise ValueError(f"Unexpected dependency framework: {path.name}")
            for dependency in group:
                if dependency.tag != "dependency" or list(dependency) or set(dependency.attrib) - {"id", "version", "include", "exclude"}:
                    raise ValueError(f"Malformed dependency XML: {path.name}")
                dep_id, dep_version = dependency.get("id", ""), dependency.get("version", "")
                if (not dep_id or not dep_version or dep_id.strip() != dep_id or dep_version.strip() != dep_version
                    or dep_id.casefold() in {name.casefold() for name in dependencies}):
                    raise ValueError(f"Invalid or duplicate dependency: {path.name}")
                include = {value.strip().casefold() for value in dependency.get("include", "All").split(",")}
                exclude = {value.strip().casefold() for value in dependency.get("exclude", "Build,Analyzers").split(",")}
                if include != {"all"} or not exclude.issubset({"build", "analyzers"}):
                    raise ValueError(f"Restricted dependency assets: {dep_id} in {path.name}")
                dependencies[dep_id] = dep_version
                parsed_dependencies.append(dependency)
        if list(metadata.iter("dependency")) != parsed_dependencies:
            raise ValueError(f"Dependency nodes outside framework groups: {path.name}")
        return {"id": package_id, "version": version, "dependencies": dependencies, "files": names}


def validate_payload(metadata: dict, path: Path) -> None:
    """Allow only this package's one-framework payload and ordinary NuGet metadata."""
    package_id = metadata["id"]
    extensions = ("pdb",) if path.suffix == ".snupkg" else ("dll", "xml", "pdb")
    allowed = {f"lib/net10.0/{package_id}.{extension}" for extension in extensions}
    allowed.update({f"{package_id}.nuspec", "README.md", "_rels/.rels", "[Content_Types].xml",
                    "package/services/metadata/core-properties/nuget.psmdcp"})
    unexpected = set(metadata["files"]) - allowed
    if unexpected:
        raise ValueError(f"Unexpected package payload in {path.name}: {sorted(unexpected)}")


def inventory(directory: Path, rows: list[dict], version: str | None = None) -> tuple[str, dict[str, dict]]:
    if not directory.is_dir():
        raise ValueError(f"Package directory is missing: {directory}")
    archives = sorted(directory.glob("*.nupkg"))
    if len(archives) != 6:
        raise ValueError(f"Expected exactly six .nupkg archives, found {len(archives)}")
    packages = {}
    for path in archives:
        metadata = archive_metadata(path)
        validate_payload(metadata, path)
        package_id = metadata["id"]
        if package_id not in EXPECTED_IDS or package_id in packages:
            raise ValueError(f"Unexpected or duplicate package: {package_id}")
        if version is None:
            version = metadata["version"]
        if metadata["version"] != version or path.name != f"{package_id}.{version}.nupkg":
            raise ValueError(f"Package filename/version mismatch: {path.name}")
        if f"lib/net10.0/{package_id}.dll" not in metadata["files"]:
            raise ValueError(f"Missing net10.0 assembly: {path.name}")
        packages[package_id] = metadata
    expected_symbols = {f"{row['id']}.{version}.snupkg" for row in rows}
    if {p.name for p in directory.glob("*.snupkg")} != expected_symbols:
        raise ValueError("Symbol package inventory does not match the six primary archives")
    for path in directory.glob("*.snupkg"):
        metadata = archive_metadata(path)
        if (metadata["version"] != version or path.name != f"{metadata['id']}.{version}.snupkg"
            or f"lib/net10.0/{metadata['id']}.pdb" not in metadata["files"]):
            raise ValueError(f"Invalid symbol package: {path.name}")
        validate_payload(metadata, path)
    if packages.keys() != {row["id"] for row in rows}:
        raise ValueError("Package output differs from release manifest")
    return version, packages


def checksums(directory: Path) -> str:
    artifacts = sorted([*directory.glob("*.nupkg"), *directory.glob("*.snupkg")])
    return "".join(f"{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.name}\n" for p in artifacts)
