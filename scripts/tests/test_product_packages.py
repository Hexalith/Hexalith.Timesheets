"""Verify Timesheets package payloads, dependencies and isolated-consumer provenance."""

import importlib.util
import json
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import release_common as common


def module(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).resolve().parents[1] / filename)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


consumer = module("consumer", "validate-consumer-package-references.py")
validator = module("validator", "validate-nuget-packages.py")
packer = module("packer", "pack-release-packages.py")


class InventoryTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.rows = []
        for package_id in sorted(common.EXPECTED_IDS):
            relative = f"src/{package_id}/{package_id}.csproj"
            project = self.root / relative
            project.parent.mkdir(parents=True)
            project.write_text('<Project Sdk="Microsoft.NET.Sdk" />')
            self.rows.append({"id": package_id, "project": relative})
        self.manifest = self.root / "manifest.json"
        self.save()

    def save(self):
        self.manifest.write_text(json.dumps({"packages": self.rows}))

    def archives(self, version="1.2.3"):
        directory = self.root / "packages"
        directory.mkdir(exist_ok=True)
        for row in self.rows:
            for suffix in ("nupkg", "snupkg"):
                with zipfile.ZipFile(directory / f"{row['id']}.{version}.{suffix}", "w") as archive:
                    archive.writestr(row["id"] + ".nuspec", f'''<package><metadata>
<id>{row['id']}</id><version>{version}</version><readme>README.md</readme>
<license type="expression">MIT</license><dependencies><group targetFramework="net11.0" /></dependencies>
</metadata></package>''')
                    archive.writestr("README.md", "readme")
                    extension = "dll" if suffix == "nupkg" else "pdb"
                    archive.writestr(f"lib/net11.0/{row['id']}.{extension}", "payload fixture")
        return directory

    def test_exact_inventory_passes(self):
        self.assertEqual(len(common.load_manifest(self.manifest, self.root)), 6)
        version, packages = common.inventory(self.archives(), self.rows)
        self.assertEqual(version, "1.2.3")
        self.assertEqual(packages.keys(), common.EXPECTED_IDS)

    def test_current_manifest_and_consumers_include_required_readmodels_package(self):
        self.assertEqual({row["id"] for row in common.load_manifest()}, common.EXPECTED_IDS)
        self.assertIn("Hexalith.Timesheets.ReadModels", common.EXPECTED_IDS)
        self.assertEqual({"Hexalith.Timesheets." + area for area in consumer.SURFACES}, common.EXPECTED_IDS)

    def test_omitting_readmodels_from_manifest_refuses(self):
        self.rows = [row for row in self.rows if row["id"] != "Hexalith.Timesheets.ReadModels"]
        self.save()
        with self.assertRaisesRegex(ValueError, "exactly six"):
            common.load_manifest(self.manifest, self.root)

    def test_duplicate_json_properties_refuse(self):
        self.manifest.write_text('{"packages": [], "packages": []}')
        with self.assertRaisesRegex(ValueError, "Duplicate JSON"):
            common.load_manifest(self.manifest, self.root)

    def test_duplicate_package_refuses(self):
        self.rows[-1] = self.rows[0]
        self.save()
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            common.load_manifest(self.manifest, self.root)

    def test_host_cannot_become_seventh_or_replacement_package(self):
        self.rows[-1] = {"id": "Hexalith.Timesheets.AppHost", "project": "src/host.csproj"}
        self.save()
        with self.assertRaisesRegex(ValueError, "Forbidden"):
            common.load_manifest(self.manifest, self.root)

    def test_project_path_escape_refuses(self):
        self.rows[0]["project"] = "../outside.csproj"
        self.save()
        with self.assertRaisesRegex(ValueError, "Invalid project"):
            common.load_manifest(self.manifest, self.root)

    def test_missing_archive_refuses(self):
        directory = self.archives()
        next(directory.glob("*.nupkg")).unlink()
        with self.assertRaisesRegex(ValueError, "exactly six"):
            common.inventory(directory, self.rows)

    def test_extra_archive_refuses(self):
        directory = self.archives()
        (directory / "unshipped.nupkg").write_bytes(b"")
        with self.assertRaisesRegex(ValueError, "exactly six"):
            common.inventory(directory, self.rows)

    def test_expected_version_mismatch_refuses(self):
        with self.assertRaisesRegex(ValueError, "version mismatch"):
            common.inventory(self.archives(), self.rows, "1.2.4")

    def test_missing_symbol_archive_refuses(self):
        directory = self.archives()
        next(directory.glob("*.snupkg")).unlink()
        with self.assertRaisesRegex(ValueError, "Symbol package"):
            common.inventory(directory, self.rows)

    def test_candidate_byte_mutation_changes_checksum(self):
        directory = self.archives()
        original = common.checksums(directory)
        with next(directory.glob("*.nupkg")).open("ab") as stream:
            stream.write(b"tampered")
        self.assertNotEqual(original, common.checksums(directory))

    def mutate_spec(self, path, transform):
        with zipfile.ZipFile(path) as archive:
            contents = {name: archive.read(name) for name in archive.namelist()}
        spec_name = next(name for name in contents if name.endswith(".nuspec"))
        contents[spec_name] = transform(contents[spec_name].decode()).encode()
        with zipfile.ZipFile(path, "w") as archive:
            for name, content in contents.items():
                archive.writestr(name, content)

    def test_license_file_named_mit_is_not_an_expression(self):
        directory = self.archives()
        self.mutate_spec(next(directory.glob("*.nupkg")), lambda text: text.replace('type="expression"', 'type="file"'))
        with self.assertRaisesRegex(ValueError, "MIT license"):
            common.inventory(directory, self.rows)

    def test_direct_dependency_outside_framework_group_refuses(self):
        directory = self.archives()
        self.mutate_spec(next(directory.glob("*.nupkg")), lambda text: text.replace('<dependencies>', '<dependencies><dependency id="Hidden.Host" version="1.0.0" />'))
        with self.assertRaisesRegex(ValueError, "Malformed dependency groups"):
            common.inventory(directory, self.rows)

    def test_unknown_dependency_xml_refuses(self):
        directory = self.archives()
        self.mutate_spec(next(directory.glob("*.nupkg")), lambda text: text.replace('<group targetFramework="net11.0" />', '<group targetFramework="net11.0"><hidden id="Leak" /></group>'))
        with self.assertRaisesRegex(ValueError, "Malformed dependency XML"):
            common.inventory(directory, self.rows)

    def test_symbol_metadata_version_mismatch_refuses(self):
        directory = self.archives()
        self.mutate_spec(next(directory.glob("*.snupkg")), lambda text: text.replace('<version>1.2.3</version>', '<version>9.9.9</version>'))
        with self.assertRaisesRegex(ValueError, "Invalid symbol"):
            common.inventory(directory, self.rows)

    def test_case_insensitive_duplicate_dependencies_refuse(self):
        directory = self.archives()
        self.mutate_spec(next(directory.glob("*.nupkg")), lambda text: text.replace('<group targetFramework="net11.0" />', '<group targetFramework="net11.0"><dependency id="Example" version="1.0.0" /><dependency id="example" version="1.0.0" /></group>'))
        with self.assertRaisesRegex(ValueError, "duplicate dependency"):
            common.inventory(directory, self.rows)

    def test_injected_assembly_or_framework_payload_refuses(self):
        directory = self.archives()
        for suffix, payload in (("nupkg", "lib/net11.0/Hexalith.Timesheets.AppHost.dll"),
                                ("nupkg", "lib/net11.0/Other.dll"),
                                ("nupkg", "lib/net9.0/{id}.dll"),
                                ("nupkg", "ref/net11.0/{id}.dll"),
                                ("nupkg", "runtimes/linux/lib/net11.0/{id}.dll"),
                                ("snupkg", "lib/net9.0/{id}.pdb"),
                                ("snupkg", "lib/net11.0/Other.pdb")):
            with self.subTest(suffix=suffix, payload=payload):
                path = next(directory.glob("*." + suffix))
                with zipfile.ZipFile(path, "a") as archive:
                    archive.writestr(payload.format(id=self.rows[0]["id"]), "injected")
                with self.assertRaisesRegex(ValueError, "Unexpected package payload"):
                    common.inventory(directory, self.rows)
                # Each mutation must independently fail an otherwise valid inventory.
                self.archives()

    def test_restricted_dependency_assets_refuse(self):
        for attributes in ('exclude="All"', 'exclude="Runtime"', 'exclude="Compile"',
                           'include="Compile"', 'include="Runtime"', 'include="Build,Analyzers"'):
            with self.subTest(attributes=attributes):
                directory = self.archives()
                self.mutate_spec(next(directory.glob("*.nupkg")), lambda text: text.replace(
                    '<group targetFramework="net11.0" />',
                    f'<group targetFramework="net11.0"><dependency id="Example" version="1.0.0" {attributes} /></group>'))
                with self.assertRaisesRegex(ValueError, "Restricted dependency assets"):
                    common.inventory(directory, self.rows)

    def test_generated_build_analyzers_exclusion_passes(self):
        directory = self.archives()
        self.mutate_spec(next(directory.glob("*.nupkg")), lambda text: text.replace(
            '<group targetFramework="net11.0" />',
            '<group targetFramework="net11.0"><dependency id="Example" version="1.0.0" exclude="Build,Analyzers" /></group>'))
        common.inventory(directory, self.rows)


class ConsumerRestoreTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.cache = self.root / "cache"
        self.feed = self.root / "feed"
        self.feed.mkdir()
        self.package = "Hexalith.Timesheets.Contracts"
        restored = self.cache / self.package.lower() / "1.2.3"
        restored.mkdir(parents=True)
        self.archive = restored / (self.package.lower() + ".1.2.3.nupkg")
        self.archive.write_bytes(b"exact local candidate")
        (self.feed / (self.package + ".1.2.3.nupkg")).write_bytes(self.archive.read_bytes())
        self.metadata = restored / ".nupkg.metadata"
        self.metadata.write_text(json.dumps({"source": str(self.feed)}))
        self.assets = {"libraries": {self.package + "/1.2.3": {"type": "package"}}}

    def validate(self):
        consumer.verify_restored_candidates(self.assets, self.cache, self.feed, "1.2.3", self.package)

    def test_exact_restored_candidate_bytes_and_local_source_pass(self):
        self.validate()

    def test_wrong_restored_version_refuses(self):
        self.assets["libraries"] = {self.package + "/1.2.2": {"type": "package"}}
        with self.assertRaisesRegex(ValueError, "Wrong restored candidate version"):
            self.validate()

    def test_wrong_restored_candidate_bytes_refuse(self):
        self.archive.write_bytes(b"different feed bytes")
        with self.assertRaisesRegex(ValueError, "bytes differ"):
            self.validate()

    def test_wrong_restored_source_refuses_even_with_matching_bytes(self):
        self.metadata.write_text(json.dumps({"source": "https://api.nuget.org/v3/index.json"}))
        with self.assertRaisesRegex(ValueError, "source is not the local feed"):
            self.validate()

    def test_wrong_transitive_candidate_version_refuses(self):
        self.assets["libraries"]["Hexalith.Timesheets.Client/1.2.2"] = {"type": "package"}
        with self.assertRaisesRegex(ValueError, "Wrong restored candidate version"):
            self.validate()

    def test_wrong_transitive_readmodels_candidate_version_refuses(self):
        self.assets["libraries"]["Hexalith.Timesheets.ReadModels/1.2.2"] = {"type": "package"}
        with self.assertRaisesRegex(ValueError, "Wrong restored candidate version"):
            self.validate()


class RestoreBackedValidatorTests(unittest.TestCase):
    def setUp(self):
        self.fixture = InventoryTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.directory = self.fixture.archives()
        for row in self.fixture.rows:
            project = self.fixture.root / row["project"]
            assets = project.parent / "obj/project.assets.json"
            assets.parent.mkdir()
            assets.write_text(json.dumps({
                "project": {"restore": {"projectPath": str(project)}, "frameworks": {"net11.0": {"dependencies": {}}}},
                "projectFileDependencyGroups": {"net11.0": ["Example.Runtime >= 1.3.0"]},
                "libraries": {"Example.Runtime/1.3.0": {"type": "package"}},
                "targets": {"net11.0": {"Example.Runtime/1.3.0": {}}},
            }))
            self.fixture.mutate_spec(self.directory / (row["id"] + ".1.2.3.nupkg"), lambda text: text.replace(
                '<group targetFramework="net11.0" />',
                '<group targetFramework="net11.0"><dependency id="Example.Runtime" version="1.3.0" exclude="Build,Analyzers" /></group>'))
        self.ledger()

    def ledger(self):
        (self.directory / "release-artifacts.sha256").write_text(common.checksums(self.directory))

    def validate(self):
        with patch.object(validator, "ROOT", self.fixture.root), patch.object(validator, "load_manifest", return_value=self.fixture.rows):
            return validator.validate(self.directory)

    def test_restore_backed_exact_dependency_versions_pass(self):
        self.assertEqual(self.validate()[0], "1.2.3")

    def test_server_dependency_on_shipped_readmodels_passes(self):
        row = next(row for row in self.fixture.rows if row["id"] == "Hexalith.Timesheets.Server")
        assets_path = (self.fixture.root / row["project"]).parent / "obj/project.assets.json"
        assets = json.loads(assets_path.read_text())
        assets["projectFileDependencyGroups"]["net11.0"].append("Hexalith.Timesheets.ReadModels >= 1.2.3")
        assets["libraries"]["Hexalith.Timesheets.ReadModels/1.2.3"] = {"type": "project"}
        assets_path.write_text(json.dumps(assets))
        self.fixture.mutate_spec(self.directory / "Hexalith.Timesheets.Server.1.2.3.nupkg",
                                 lambda text: text.replace('</group>',
                                     '<dependency id="Hexalith.Timesheets.ReadModels" version="1.2.3" /></group>'))
        self.ledger()
        self.assertEqual(self.validate()[0], "1.2.3")

    def test_underdeclared_restored_dependency_refuses(self):
        self.fixture.mutate_spec(next(self.directory.glob("*.nupkg")), lambda text: text.replace('version="1.3.0"', 'version="1.2.0"'))
        self.ledger()
        with self.assertRaisesRegex(ValueError, "Dependency version mismatch: Example.Runtime 1.2.0, expected 1.3.0"):
            self.validate()

    def test_packer_existing_output_refuses_without_launching_dotnet(self):
        with patch.object(packer, "load_manifest", return_value=self.fixture.rows), patch.object(sys, "argv", ["pack", str(self.directory), "1.2.3"]), patch.object(packer.subprocess, "run") as run:
            with self.assertRaisesRegex(ValueError, "empty package output"):
                packer.main()
            run.assert_not_called()


if __name__ == "__main__":
    unittest.main()
