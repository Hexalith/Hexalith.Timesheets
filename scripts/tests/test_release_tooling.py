"""Exercise malformed artifacts and refusal paths without credentials or network writes."""

import importlib.util
import contextlib
import hashlib
import http.client
import io
import json
import os
import sys
import tempfile
import unittest
import urllib.error
import zipfile
from types import SimpleNamespace
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import release_common as common
import verify_publication as registry


def module(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).resolve().parents[1] / filename)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


preflight = module("preflight", "release-preflight.py")
verification = module("verification", "verify-publication.py")
test_evidence = module("test_evidence", "validate-test-results.py")
publisher = module("publisher", "push-release-packages.py")
consumer = module("consumer", "validate-consumer-package-references.py")
validator = module("validator", "validate-nuget-packages.py")
packer = module("packer", "pack-release-packages.py")
SHA = "a" * 40


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
<license type="expression">MIT</license><dependencies><group targetFramework="net10.0" /></dependencies>
</metadata></package>''')
                    archive.writestr("README.md", "readme")
                    extension = "dll" if suffix == "nupkg" else "pdb"
                    archive.writestr(f"lib/net10.0/{row['id']}.{extension}", "payload fixture")
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
        self.mutate_spec(next(directory.glob("*.nupkg")), lambda text: text.replace('<group targetFramework="net10.0" />', '<group targetFramework="net10.0"><hidden id="Leak" /></group>'))
        with self.assertRaisesRegex(ValueError, "Malformed dependency XML"):
            common.inventory(directory, self.rows)

    def test_symbol_metadata_version_mismatch_refuses(self):
        directory = self.archives()
        self.mutate_spec(next(directory.glob("*.snupkg")), lambda text: text.replace('<version>1.2.3</version>', '<version>9.9.9</version>'))
        with self.assertRaisesRegex(ValueError, "Invalid symbol"):
            common.inventory(directory, self.rows)

    def test_case_insensitive_duplicate_dependencies_refuse(self):
        directory = self.archives()
        self.mutate_spec(next(directory.glob("*.nupkg")), lambda text: text.replace('<group targetFramework="net10.0" />', '<group targetFramework="net10.0"><dependency id="Example" version="1.0.0" /><dependency id="example" version="1.0.0" /></group>'))
        with self.assertRaisesRegex(ValueError, "duplicate dependency"):
            common.inventory(directory, self.rows)

    def test_injected_assembly_or_framework_payload_refuses(self):
        directory = self.archives()
        for suffix, payload in (("nupkg", "lib/net10.0/Hexalith.Timesheets.AppHost.dll"),
                                ("nupkg", "lib/net10.0/Other.dll"),
                                ("nupkg", "lib/net9.0/{id}.dll"),
                                ("nupkg", "ref/net10.0/{id}.dll"),
                                ("nupkg", "runtimes/linux/lib/net10.0/{id}.dll"),
                                ("snupkg", "lib/net9.0/{id}.pdb"),
                                ("snupkg", "lib/net10.0/Other.pdb")):
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
                    '<group targetFramework="net10.0" />',
                    f'<group targetFramework="net10.0"><dependency id="Example" version="1.0.0" {attributes} /></group>'))
                with self.assertRaisesRegex(ValueError, "Restricted dependency assets"):
                    common.inventory(directory, self.rows)

    def test_generated_build_analyzers_exclusion_passes(self):
        directory = self.archives()
        self.mutate_spec(next(directory.glob("*.nupkg")), lambda text: text.replace(
            '<group targetFramework="net10.0" />',
            '<group targetFramework="net10.0"><dependency id="Example" version="1.0.0" exclude="Build,Analyzers" /></group>'))
        common.inventory(directory, self.rows)


class SourceGateTests(unittest.TestCase):
    def setUp(self):
        self.environment = patch.dict(os.environ, {
            "GITHUB_REPOSITORY": preflight.REPOSITORY, "GITHUB_SHA": SHA, "GITHUB_REF": "refs/heads/main",
            "HEXALITH_RELEASE_PUBLISH_ENABLED": "true",
        }, clear=True)
        self.environment.start()
        self.addCleanup(self.environment.stop)
        self.responses = {
            "main": {"object": {"sha": SHA}},
            "environment": {"protection_rules": [{"type": "required_reviewers", "reviewers": [{"id": 1}]}],
                            "deployment_branch_policy": {"protected_branches": True}},
        }
        for workflow in ("ci.yml", "commitlint.yml", "codeql.yml"):
            self.responses[workflow] = {"workflow_runs": [{"head_sha": SHA, "head_branch": "main", "event": "push",
                                                           "status": "completed", "conclusion": "success"}]}

    def github(self, path):
        key = "main" if "/git/ref/" in path else path.split("/workflows/")[1].split("/")[0] if "/runs?" in path else "environment"
        return self.responses[key]

    def check(self, **kwargs):
        with patch.object(preflight, "github", side_effect=self.github), patch.object(preflight.subprocess, "check_output", return_value=SHA + "\n"):
            return preflight.check_source(require_publish_flag=True, **kwargs)

    def test_exact_green_main_and_protection_pass(self):
        self.assertTrue(self.check())

    def test_main_advanced_refuses(self):
        self.responses["main"]["object"]["sha"] = "b" * 40
        with self.assertRaisesRegex(ValueError, "stale"):
            self.check()

    def test_pr_or_old_ci_cannot_authorize_release(self):
        self.responses["ci.yml"]["workflow_runs"][0]["event"] = "pull_request"
        with self.assertRaisesRegex(ValueError, "No successful push CI"):
            self.check()

    def test_each_independent_source_workflow_must_be_successful_at_exact_source(self):
        for workflow in ("ci.yml", "commitlint.yml", "codeql.yml"):
            run = self.responses[workflow]["workflow_runs"][0]
            for field, bad_value in (("conclusion", "failure"), ("status", "in_progress"),
                                     ("head_sha", "b" * 40), ("event", "pull_request"),
                                     ("head_branch", "feature")):
                with self.subTest(workflow=workflow, field=field):
                    original = run[field]
                    run[field] = bad_value
                    with self.assertRaisesRegex(ValueError, workflow):
                        self.check()
                    run[field] = original

    def test_fork_refuses_before_api_access(self):
        os.environ["GITHUB_REPOSITORY"] = "someone/fork"
        with self.assertRaisesRegex(ValueError, "Timesheets repository"):
            self.check()

    def test_non_main_dispatch_refuses(self):
        os.environ["GITHUB_REF"] = "refs/heads/feature"
        with self.assertRaisesRegex(ValueError, "select main"):
            self.check()

    def test_freeze_is_exact_case_sensitive(self):
        for value in ("", "TRUE", "True", " true", "false"):
            os.environ["HEXALITH_RELEASE_PUBLISH_ENABLED"] = value
            self.assertFalse(self.check(allow_frozen=True))
            with self.assertRaisesRegex(ValueError, "frozen"):
                self.check()

    def test_unprotected_environment_refuses(self):
        self.responses["environment"]["protection_rules"] = []
        with self.assertRaisesRegex(ValueError, "required-reviewer"):
            self.check()

    def test_unrestricted_environment_refuses(self):
        self.responses["environment"]["deployment_branch_policy"] = None
        with self.assertRaisesRegex(ValueError, "restrict deployment"):
            self.check()

    def test_inaccessible_environment_is_an_error_not_a_freeze(self):
        with patch.object(preflight, "github", side_effect=ValueError("GitHub prerequisite could not be read: HTTP 403")), patch.object(preflight.subprocess, "check_output", return_value=SHA):
            with self.assertRaisesRegex(ValueError, "could not be read"):
                preflight.check_source(allow_frozen=True, require_publish_flag=True)


class PrePublicationTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.manifest = self.root / "manifest.json"
        self.manifest.write_text('{"packages": []}')
        self.identity = self.root / "identity.json"
        values = {
            "GITHUB_SHA": SHA, "NUGET_API_KEY": "synthetic-test-key",
            "HEXALITH_BUILDS_EXECUTION_SHA": common.BUILDS_SHA,
            "HEXALITH_RELEASE_ENVIRONMENT": "production", "HEXALITH_RELEASE_SOURCE_BRANCH": "main",
            "HEXALITH_RELEASE_SOURCE_CI_WORKFLOW": "ci.yml",
            "HEXALITH_RELEASE_PACKAGE_MANIFEST": "tools/release-packages.json",
            "HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT": "6", "HEXALITH_RELEASE_REQUIRE_AUTHORITY": "false",
        }
        for mock in (patch.dict(os.environ, values, clear=True), patch.object(preflight, "MANIFEST", self.manifest),
                     patch.object(preflight, "IDENTITY", self.identity), patch.object(preflight, "check_source", return_value=True),
                     patch.object(preflight, "load_manifest", return_value=[{"id": name} for name in common.EXPECTED_IDS])):
            mock.start()
            self.addCleanup(mock.stop)

    def test_missing_secret_prevents_identity_creation(self):
        os.environ.pop("NUGET_API_KEY")
        with self.assertRaisesRegex(ValueError, "NUGET_API_KEY"):
            preflight.version_preflight("verify", "1.2.3")
        self.assertFalse(self.identity.exists())

    def test_changed_builds_identity_prevents_identity_creation(self):
        os.environ["HEXALITH_BUILDS_EXECUTION_SHA"] = "b" * 40
        with self.assertRaisesRegex(ValueError, "approved Timesheets contract"):
            preflight.version_preflight("verify", "1.2.3")
        self.assertFalse(self.identity.exists())

    def test_stale_five_package_count_prevents_identity_creation(self):
        os.environ["HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT"] = "5"
        with self.assertRaisesRegex(ValueError, "approved Timesheets contract"):
            preflight.version_preflight("verify", "1.2.3")
        self.assertFalse(self.identity.exists())

    def test_occupied_version_prevents_identity_creation(self):
        with patch.object(preflight, "published_versions", return_value=["1.2.3"]):
            with self.assertRaisesRegex(ValueError, "Occupied"):
                preflight.version_preflight("verify", "1.2.3")
        self.assertFalse(self.identity.exists())

    def test_manifest_drift_after_verify_refuses_prepare(self):
        with patch.object(preflight, "published_versions", return_value=[]):
            preflight.version_preflight("verify", "1.2.3")
            self.manifest.write_text('{"packages": ["changed"]}')
            with self.assertRaisesRegex(ValueError, "manifest changed"):
                preflight.version_preflight("prepare", "1.2.3")


class TestEvidenceTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        for area in ("ArchitectureTests", "Contracts.Tests", "Server.Tests", "Projections.Tests", "Works.Tests", "IntegrationTests"):
            (self.root / f"Hexalith.Timesheets.{area}.trx").write_text('<TestRun><Results><UnitTestResult outcome="Passed" /><UnitTestResult outcome="Passed" /></Results><ResultSummary><Counters total="2" executed="2" passed="2" failed="0" /></ResultSummary></TestRun>')

    def test_all_six_reports_pass(self):
        self.assertEqual(len(test_evidence.validate(self.root)), 6)

    def test_missing_works_report_refuses(self):
        (self.root / "Hexalith.Timesheets.Works.Tests.trx").unlink()
        with self.assertRaisesRegex(ValueError, "Works.Tests"):
            test_evidence.validate(self.root)

    def test_zero_test_report_refuses(self):
        (self.root / "Hexalith.Timesheets.Works.Tests.trx").write_text('<TestRun><Counters total="0" /></TestRun>')
        with self.assertRaisesRegex(ValueError, "Empty"):
            test_evidence.validate(self.root)

    def test_failing_report_refuses(self):
        (self.root / "Hexalith.Timesheets.Works.Tests.trx").write_text('<TestRun><Counters total="2" executed="2" passed="1" failed="1" /></TestRun>')
        with self.assertRaisesRegex(ValueError, "failing"):
            test_evidence.validate(self.root)

    def test_missing_case_results_refuse(self):
        (self.root / "Hexalith.Timesheets.Works.Tests.trx").write_text('<TestRun><Counters total="2" executed="2" passed="2" /></TestRun>')
        with self.assertRaisesRegex(ValueError, "Missing test case"):
            test_evidence.validate(self.root)


class PublicationTests(unittest.TestCase):
    def test_404_means_absent(self):
        with patch.object(registry.urllib.request, "urlopen", side_effect=urllib.error.HTTPError("feed", 404, "missing", {}, None)):
            self.assertEqual(registry.published_versions("Hexalith.Timesheets.Contracts"), [])

    def test_registry_transport_failure_is_bounded_and_refuses(self):
        with patch.object(registry.urllib.request, "urlopen", side_effect=urllib.error.URLError("offline")) as request, patch.object(registry.time, "sleep"):
            with self.assertRaisesRegex(ValueError, "unavailable"):
                registry.published_versions("Hexalith.Timesheets.Contracts")
            self.assertEqual(request.call_count, 3)

    def test_truncated_response_body_retries_then_succeeds(self):
        class TruncatedBody(io.BytesIO):
            def read(self, *args):
                raise http.client.IncompleteRead(b'{"versions":', 10)

        good = io.BytesIO(b'{"versions": ["1.0.1"]}')
        good.status = 200
        truncated = TruncatedBody()
        truncated.status = 200
        with patch.object(registry.urllib.request, "urlopen", side_effect=[truncated, good]) as request, patch.object(registry.time, "sleep") as sleep:
            self.assertEqual(registry.published_versions("Hexalith.Timesheets.Contracts"), ["1.0.1"])
            self.assertEqual(request.call_count, 2)
            sleep.assert_called_once_with(2)

    def test_protocol_failures_exhaust_bounded_retries(self):
        for error in (http.client.IncompleteRead(b"partial", 4), http.client.RemoteDisconnected("closed")):
            with self.subTest(error=type(error).__name__), patch.object(registry.urllib.request, "urlopen", side_effect=error) as request, patch.object(registry.time, "sleep") as sleep:
                with self.assertRaisesRegex(ValueError, "unavailable"):
                    registry.published_versions("Hexalith.Timesheets.Contracts")
                self.assertEqual(request.call_count, 3)
                self.assertEqual(sleep.call_count, 2)

    def test_malformed_registry_index_refuses(self):
        response = io.BytesIO(b'{"versions": "1.0.0"}')
        response.status = 200
        with patch.object(registry.urllib.request, "urlopen", return_value=response):
            with self.assertRaisesRegex(ValueError, "Malformed"):
                registry.published_versions("Hexalith.Timesheets.Contracts")

    def test_missing_or_different_version_fails_verification(self):
        with patch.object(verification, "published_versions", return_value=["1.0.0"]):
            result = verification.verify("1.0.1", attempts=1)
        self.assertFalse(result["complete"])
        self.assertEqual(set(result["packages"].values()), {"missing"})

    def test_partial_publication_reports_each_missing_id(self):
        first = next(iter(common.EXPECTED_IDS))
        with patch.object(verification, "published_versions", side_effect=lambda package: ["1.0.1"] if package == first else []):
            result = verification.verify("1.0.1", attempts=1)
        self.assertFalse(result["complete"])
        self.assertEqual(sum(v == "missing" for v in result["packages"].values()), 5)

    def test_all_six_exact_versions_pass(self):
        with patch.object(verification, "published_versions", return_value=["1.0.1"]):
            self.assertTrue(verification.verify("1.0.1", attempts=1)["complete"])


class PublisherBoundaryTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        directory = self.root / "nupkgs"
        directory.mkdir()
        self.rows = [{"id": name} for name in sorted(common.EXPECTED_IDS)]
        for row in self.rows:
            (directory / f"{row['id']}.1.2.3.nupkg").write_text("candidate fixture bytes")
        self.identity_path = self.root / "identity.json"
        self.identity = {"version": "1.2.3", "artifactsSha256": hashlib.sha256(common.checksums(directory).encode()).hexdigest()}
        self.identity_path.write_text(json.dumps(self.identity))
        self.preflight = SimpleNamespace(
            version_preflight=lambda phase, version: None,
            check_source=lambda: True,
            candidate_identity=lambda version: {"version": version},
            IDENTITY=self.identity_path,
        )
        for mock in (patch.object(publisher, "ROOT", self.root), patch.object(publisher, "load_preflight", return_value=self.preflight),
                     patch.object(publisher, "load_manifest", return_value=self.rows), patch.object(publisher, "inventory", return_value=("1.2.3", {})),
                     patch.object(publisher, "published_versions", return_value=[]), patch.dict(os.environ, {"NUGET_API_KEY": "synthetic-secret-never-print"})):
            mock.start()
            self.addCleanup(mock.stop)

    def test_unchanged_candidate_invokes_exactly_six_pushes(self):
        with patch.object(publisher.subprocess, "run", return_value=SimpleNamespace(returncode=0)) as push:
            publisher.main("1.2.3")
        self.assertEqual(push.call_count, 6)
        for call, row in zip(push.call_args_list, self.rows):
            self.assertEqual(Path(call.args[0][3]).name, row['id'] + ".1.2.3.nupkg")
            self.assertNotIn("--skip-duplicate", call.args[0])

    def test_changed_identity_prevents_every_push(self):
        self.identity["version"] = "9.9.9"
        self.identity_path.write_text(json.dumps(self.identity))
        with patch.object(publisher.subprocess, "run") as push:
            with self.assertRaisesRegex(ValueError, "identity or package bytes"):
                publisher.main("1.2.3")
            push.assert_not_called()

    def test_changed_candidate_bytes_prevent_every_push(self):
        next((self.root / "nupkgs").glob("*.nupkg")).write_text("mutated")
        with patch.object(publisher.subprocess, "run") as push:
            with self.assertRaisesRegex(ValueError, "identity or package bytes"):
                publisher.main("1.2.3")
            push.assert_not_called()

    def test_occupied_version_prevents_every_push(self):
        with patch.object(publisher, "published_versions", return_value=["1.2.3"]), patch.object(publisher.subprocess, "run") as push:
            with self.assertRaisesRegex(ValueError, "Occupied"):
                publisher.main("1.2.3")
            push.assert_not_called()

    def test_failed_push_stops_without_secret_in_exception(self):
        output = io.StringIO()
        with patch.object(publisher.subprocess, "run", return_value=SimpleNamespace(returncode=1)) as push, contextlib.redirect_stdout(output):
            with self.assertRaises(ValueError) as failure:
                publisher.main("1.2.3")
        self.assertEqual(push.call_count, 1)
        self.assertNotIn("synthetic-secret-never-print", str(failure.exception))
        self.assertIn("partial publication", str(failure.exception))
        self.assertIn(json.dumps(self.identity, sort_keys=True), output.getvalue())
        self.assertIn(common.checksums(self.root / "nupkgs"), output.getvalue())
        self.assertNotIn("synthetic-secret-never-print", output.getvalue())

    def test_evidence_is_logged_before_first_push(self):
        output = io.StringIO()

        def push(*args, **kwargs):
            self.assertIn(json.dumps(self.identity, sort_keys=True), output.getvalue())
            self.assertIn(common.checksums(self.root / "nupkgs"), output.getvalue())
            return SimpleNamespace(returncode=0)

        with patch.object(publisher.subprocess, "run", side_effect=push), contextlib.redirect_stdout(output):
            publisher.main("1.2.3")

    def test_source_lost_after_first_push_refuses_second_write(self):
        def source():
            if push.call_count:
                raise ValueError("Source eligibility lost")
            return True

        with patch.object(publisher.subprocess, "run", return_value=SimpleNamespace(returncode=0)) as push, patch.object(self.preflight, "check_source", side_effect=source), contextlib.redirect_stdout(io.StringIO()):
            with self.assertRaisesRegex(ValueError, "Source eligibility lost"):
                publisher.main("1.2.3")
            self.assertEqual(push.call_count, 1)


class PublicationCliTests(unittest.TestCase):
    def invoke(self, tags, result="success", version=None):
        argv = ([version] if version else []) + ["--source-sha", SHA, "--release-result", result]
        with patch.object(verification.subprocess, "check_output", return_value=tags), patch.object(verification, "verify") as query, contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            code = verification.main(argv)
            query.assert_not_called()
            return code

    def test_no_release_success_explicitly_skips(self):
        self.assertEqual(self.invoke(""), 0)

    def test_failure_without_tag_refuses(self):
        self.assertEqual(self.invoke("", "failure"), 1)

    def test_multiple_tags_refuse(self):
        self.assertEqual(self.invoke("v1.0.0\nv1.0.1\n"), 1)

    def test_malformed_tag_refuses(self):
        self.assertEqual(self.invoke("vnot-a-version\n"), 1)

    def test_requested_version_must_match_source_tag(self):
        self.assertEqual(self.invoke("v1.0.1\n", version="1.0.2"), 1)

    def run_retained_failure(self, lookup):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "publication.json"
            with patch.object(verification.subprocess, "check_output", return_value="v1.0.1\n"), patch.object(verification, "published_versions", side_effect=lookup), patch.object(verification.time, "sleep"), contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                code = verification.main(["--source-sha", SHA, "--output", str(output)])
            self.assertEqual(code, 1)
            evidence = json.loads(output.read_text())
            self.assertFalse(evidence["complete"])
            self.assertEqual(evidence["version"], "1.0.1")
            self.assertEqual(set(evidence["packages"]), common.EXPECTED_IDS)
            return evidence

    def test_actual_cli_partial_publication_fails_and_retains_all_six_results(self):
        missing = "Hexalith.Timesheets.Testing"
        evidence = self.run_retained_failure(lambda package: [] if package == missing else ["1.0.1"])
        self.assertEqual(evidence["packages"][missing], "missing")
        self.assertEqual(list(evidence["packages"].values()).count("published"), 5)

    def test_exhausted_protocol_failure_retains_per_package_evidence(self):
        failed = "Hexalith.Timesheets.Testing"

        def lookup(package):
            return registry.published_versions(package) if package == failed else ["1.0.1"]

        with patch.object(registry.urllib.request, "urlopen", side_effect=http.client.IncompleteRead(b"partial", 5)) as request, patch.object(registry.time, "sleep"):
            evidence = self.run_retained_failure(lookup)
        self.assertEqual(request.call_count, 18)  # Six publication polls, each with three transport attempts.
        self.assertIn("unavailable", evidence["packages"][failed])
        self.assertEqual(list(evidence["packages"].values()).count("published"), 5)


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
                "project": {"restore": {"projectPath": str(project)}, "frameworks": {"net10.0": {"dependencies": {}}}},
                "projectFileDependencyGroups": {"net10.0": ["Example.Runtime >= 1.3.0"]},
                "libraries": {"Example.Runtime/1.3.0": {"type": "package"}},
                "targets": {"net10.0": {"Example.Runtime/1.3.0": {}}},
            }))
            self.fixture.mutate_spec(self.directory / (row["id"] + ".1.2.3.nupkg"), lambda text: text.replace(
                '<group targetFramework="net10.0" />',
                '<group targetFramework="net10.0"><dependency id="Example.Runtime" version="1.3.0" exclude="Build,Analyzers" /></group>'))
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
        assets["projectFileDependencyGroups"]["net10.0"].append("Hexalith.Timesheets.ReadModels >= 1.2.3")
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
