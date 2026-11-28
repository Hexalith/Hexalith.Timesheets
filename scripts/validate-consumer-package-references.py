#!/usr/bin/env python3
"""Compile and execute six isolated and one composed NuGet-only consumer outside the repository."""

import argparse
import hashlib
import os
import subprocess
import sys
import tempfile
from pathlib import Path
from xml.etree import ElementTree as ET

from release_common import EXPECTED_IDS, ROOT, inventory, load_manifest, read_json

SURFACES = {
    "Contracts": "Hexalith.Timesheets.Contracts.References.TenantReference",
    "Client": "Hexalith.Timesheets.Client.TimesheetsClientOptions",
    "ReadModels": "Hexalith.Timesheets.Server.MagicLinks.MagicLinkTokenHashCapabilityIndexProjection",
    "Server": "Hexalith.Timesheets.Server.Authorization.TimesheetsAuthorizationDecision",
    "Projections": "Hexalith.Timesheets.Projections.ProjectionFreshnessMetadataMapper",
    "Testing": "Hexalith.Timesheets.Testing.TimesheetsTestIds",
}
COMPOSED_PACKAGES = ("Hexalith.Timesheets.Server", "Hexalith.Timesheets.Projections")


def verify_restored_candidates(
    assets: dict, cache: Path, directory: Path, version: str, package_ids: tuple[str, ...]
) -> None:
    libraries = assets["libraries"]
    if any(value.get("type") != "package" for value in libraries.values()):
        raise ValueError(f"Source dependency leaked into NuGet consumer: {', '.join(package_ids)}")
    for package_id in package_ids:
        if package_id + "/" + version not in libraries:
            raise ValueError(f"Wrong restored candidate version: {package_id}")
    if package_ids == ("Hexalith.Timesheets.Projections",) and any(
        identity.split("/", 1)[0].casefold() == "hexalith.timesheets.server" for identity in libraries
    ):
        raise ValueError("Timesheets Server leaked into the isolated Projections restore")
    candidates = {name.casefold() for name in EXPECTED_IDS}
    for identity, library in libraries.items():
        restored_id, restored_version = identity.split("/", 1)
        if restored_id.casefold() not in candidates:
            continue
        if restored_version != version:
            raise ValueError(f"Wrong restored candidate version: {identity}")
        canonical_id = next(name for name in EXPECTED_IDS if name.casefold() == restored_id.casefold())
        local_archive = directory / f"{canonical_id}.{version}.nupkg"
        restored_directory = cache / restored_id.lower() / version.lower()
        restored_archive = restored_directory / f"{restored_id.lower()}.{version.lower()}.nupkg"
        if hashlib.sha256(restored_archive.read_bytes()).digest() != hashlib.sha256(local_archive.read_bytes()).digest():
            raise ValueError(f"Restored candidate bytes differ from local archive: {identity}")
        metadata = read_json(restored_directory / ".nupkg.metadata")
        if metadata.get("source") != str(directory):
            raise ValueError(f"Restored candidate source is not the local feed: {identity}")


def write_consumer(consumer: Path, package_ids: tuple[str, ...], version: str) -> None:
    consumer.mkdir()
    project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
    properties = ET.SubElement(project, "PropertyGroup")
    for name, value in {
        "OutputType": "Exe", "TargetFramework": "net11.0", "ImplicitUsings": "enable", "Nullable": "enable",
        "TreatWarningsAsErrors": "true", "NuGetAudit": "true", "NuGetAuditMode": "all",
        "ManagePackageVersionsCentrally": "true",
    }.items():
        ET.SubElement(properties, name).text = value
    references = ET.SubElement(project, "ItemGroup")
    packages = ET.Element("Project")
    versions = ET.SubElement(packages, "ItemGroup")
    snippet = (ROOT / f"tools/package-consumers/{consumer.name}.cs").read_text(encoding="utf-8")
    for index, package_id in enumerate(package_ids):
        ET.SubElement(references, "PackageReference", Include=package_id)
        ET.SubElement(versions, "PackageVersion", Include=package_id, Version=f"[{version}]")
        surface = SURFACES[package_id.rsplit(".", 1)[1]]
        snippet += f'''
var candidateVersion{index} = System.Reflection.CustomAttributeExtensions
    .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof({surface}).Assembly)
    ?.InformationalVersion;
if (candidateVersion{index}?.Split('+')[0] != "{version}")
    throw new InvalidOperationException($"Wrong assembly informational version for {package_id}: {{candidateVersion{index}}}");
Console.WriteLine("{package_id} {version}: consumer surface and assembly version verified ({consumer.name})");
'''
    ET.ElementTree(project).write(consumer / "Consumer.csproj", encoding="utf-8")
    ET.ElementTree(packages).write(consumer / "Directory.Packages.props", encoding="utf-8")
    (consumer / "Program.cs").write_text(snippet, encoding="utf-8")


def main(directory: Path) -> None:
    rows = load_manifest()
    version, _ = inventory(directory, rows)
    with tempfile.TemporaryDirectory(prefix="timesheets-consumers-") as temporary:
        root = Path(temporary)
        (root / "global.json").write_text((ROOT / "global.json").read_text(), encoding="utf-8")
        # Clear inherited sources and use a private cache so repository props, source
        # references, credentials and previously built package bytes cannot rescue a failure.
        config = ET.Element("configuration")
        sources = ET.SubElement(config, "packageSources")
        ET.SubElement(sources, "clear")
        ET.SubElement(sources, "add", key="candidate", value=str(directory))
        ET.SubElement(sources, "add", key="nuget.org", value="https://api.nuget.org/v3/index.json")
        mappings = ET.SubElement(config, "packageSourceMapping")
        candidate = ET.SubElement(mappings, "packageSource", key="candidate")
        for package_id in sorted(EXPECTED_IDS):
            ET.SubElement(candidate, "package", pattern=package_id)
        # Exact ID mappings take precedence over the fallback wildcard, so these
        # six IDs are eligible only from candidate, including transitive edges.
        external = ET.SubElement(mappings, "packageSource", key="nuget.org")
        ET.SubElement(external, "package", pattern="*")
        ET.ElementTree(config).write(root / "NuGet.Config", encoding="utf-8", xml_declaration=True)
        env = os.environ.copy()
        for key in list(env):
            if key.startswith("Hexalith") or key in {"UseHexalithProjectReferences", "UseNuGetDeps", "MSBuildSDKsPath"}:
                env.pop(key)
        env["NUGET_PACKAGES"] = str(root / ".nuget/packages")
        env["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1"
        env["MSBUILDDISABLENODEREUSE"] = "1"
        consumers = [(row["id"].rsplit(".", 1)[1], (row["id"],)) for row in rows]
        consumers.append(("Composed", COMPOSED_PACKAGES))
        for area, package_ids in consumers:
            consumer = root / area
            write_consumer(consumer, package_ids, version)
            subprocess.run(["dotnet", "restore", "Consumer.csproj", "--configfile", str(root / "NuGet.Config")],
                           cwd=consumer, env=env, check=True)
            assets = read_json(consumer / "obj/project.assets.json")
            verify_restored_candidates(assets, Path(env["NUGET_PACKAGES"]), directory, version, package_ids)
            subprocess.run(["dotnet", "build", "--configuration", "Release", "--no-restore", "-warnaserror", "-m:1", "/nr:false"],
                           cwd=consumer, env=env, check=True)
            subprocess.run(["dotnet", str(consumer / "bin/Release/net11.0/Consumer.dll")],
                           cwd=consumer, env=env, check=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_directory", type=Path)
    try:
        main(parser.parse_args().package_directory.resolve())
    except (ValueError, OSError, KeyError, subprocess.CalledProcessError) as error:
        print(f"Consumer validation failed: {error}", file=sys.stderr)
        sys.exit(1)
