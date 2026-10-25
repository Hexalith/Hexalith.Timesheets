#!/usr/bin/env python3
"""Compile and execute six independent NuGet-only consumers outside the repository."""

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
    "Contracts": ("Hexalith.Timesheets.Contracts.References.TenantReference", '''
var tenant = new Hexalith.Timesheets.Contracts.References.TenantReference("consumer");
if (tenant.TenantId != "consumer") throw new InvalidOperationException("Tenant reference failed");'''),
    "Client": ("Hexalith.Timesheets.Client.TimesheetsClientOptions", '''
var options = new Hexalith.Timesheets.Client.TimesheetsClientOptions { Endpoint = new Uri("https://example.invalid") };
if (options.Endpoint.Host != "example.invalid") throw new InvalidOperationException("Client options failed");'''),
    "ReadModels": ("Hexalith.Timesheets.Server.MagicLinks.MagicLinkTokenHashCapabilityIndexProjection", '''
if (Hexalith.Timesheets.Server.MagicLinks.MagicLinkTokenHashCapabilityIndexProjection.ShardKey(255)
        != "timesheets:magic-links:token-hash-capability-index:v2:shard:FF"
    || Hexalith.Timesheets.Server.MagicLinks.MagicLinkTokenHashCapabilityIndexProjection.StateStoreName
        != "timesheets-readmodels")
    throw new InvalidOperationException("Read-model address compatibility failed");'''),
    "Server": ("Hexalith.Timesheets.Server.Authorization.TimesheetsAuthorizationDecision", '''
var decision = Hexalith.Timesheets.Server.Authorization.TimesheetsAuthorizationDecision.Denied("consumer");
if (decision.IsAuthorized || decision.Reason != "consumer") throw new InvalidOperationException("Authorization decision failed");'''),
    "Projections": ("Hexalith.Timesheets.Projections.ProjectionFreshnessMetadataMapper", '''
var checkpoint = new Hexalith.Timesheets.Projections.TimesheetsProjectionCheckpoint(
    "consumer", "test", 7, Hexalith.Timesheets.Projections.ProjectionFreshness.Fresh);
var metadata = Hexalith.Timesheets.Projections.ProjectionFreshnessMetadataMapper.ToMetadata(checkpoint);
if (!checkpoint.CanServeReads || metadata.Cursor != "7") throw new InvalidOperationException("Projection mapping failed");'''),
    "Testing": ("Hexalith.Timesheets.Testing.TimesheetsTestIds", '''
if (Hexalith.Timesheets.Testing.TimesheetsTestIds.Tenant.TenantId != "tenant-test"
    || Hexalith.Timesheets.Testing.TimesheetsTestIds.Project.ProjectId != "project-test")
    throw new InvalidOperationException("Test reference fixtures failed");'''),
}


def verify_restored_candidates(assets: dict, cache: Path, directory: Path, version: str, package_id: str) -> None:
    libraries = assets["libraries"]
    if any(value.get("type") != "package" for value in libraries.values()):
        raise ValueError(f"Source dependency leaked into isolated consumer: {package_id}")
    if package_id + "/" + version not in libraries:
        raise ValueError(f"Wrong restored candidate version: {package_id}")
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
        for row in rows:
            package_id = row["id"]
            area = package_id.rsplit(".", 1)[1]
            consumer = root / area
            consumer.mkdir()
            (consumer / "Consumer.csproj").write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors><NuGetAudit>true</NuGetAudit><NuGetAuditMode>all</NuGetAuditMode>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="{package_id}" />
  </ItemGroup>
</Project>
''', encoding="utf-8")
            (consumer / "Directory.Packages.props").write_text(f'''<Project>
  <ItemGroup><PackageVersion Include="{package_id}" Version="[{version}]" /></ItemGroup>
</Project>
''', encoding="utf-8")
            surface, snippet = SURFACES[area]
            (consumer / "Program.cs").write_text(f'''using System.Reflection;
{snippet}
var assembly = typeof({surface}).Assembly;
var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
if (version?.Split('+')[0] != "{version}")
    throw new InvalidOperationException($"Wrong assembly informational version: {{version}}");
Console.WriteLine("{package_id} {version}: consumer surface and assembly version verified");
''', encoding="utf-8")
            subprocess.run(["dotnet", "restore", "Consumer.csproj", "--configfile", str(root / "NuGet.Config")],
                           cwd=consumer, env=env, check=True)
            assets = read_json(consumer / "obj/project.assets.json")
            verify_restored_candidates(assets, Path(env["NUGET_PACKAGES"]), directory, version, package_id)
            subprocess.run(["dotnet", "build", "--configuration", "Release", "--no-restore", "-warnaserror", "-m:1", "/nr:false"],
                           cwd=consumer, env=env, check=True)
            subprocess.run(["dotnet", str(consumer / "bin/Release/net10.0/Consumer.dll")],
                           cwd=consumer, env=env, check=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package_directory", type=Path)
    try:
        main(parser.parse_args().package_directory.resolve())
    except (ValueError, OSError, KeyError, subprocess.CalledProcessError) as error:
        print(f"Consumer validation failed: {error}", file=sys.stderr)
        sys.exit(1)
