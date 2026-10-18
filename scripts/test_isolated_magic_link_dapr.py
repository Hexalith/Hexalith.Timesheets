"""Failure-injected fixture checks; never start an AppHost or Docker container."""

import contextlib
import io
import os
import signal
import socket
import sys
import threading
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import run_isolated_magic_link_dapr as fixture
import check_magic_link_apphost_guards as guards


class Commands:
    def __init__(self, failure=None):
        self.calls = []
        self.failure = failure
        self.started = False
        self.stopped = False
        self.image = "sha256:" + "a" * 64
        self.container = "b" * 64
        self.redis_attempted = False
        self.containers = {}
        self.owner = None

    def __call__(self, arguments, **kwargs):
        self.calls.append((arguments, kwargs))
        if arguments[:2] == ["aspire", "start"]:
            self.started = True
        if arguments[:2] == ["docker", "run"]:
            self.redis_attempted = True
            name = arguments[arguments.index("--name") + 1]
            identity = hex(len(self.containers) + 11)[2:] * 64
            binding = arguments[arguments.index("--publish") + 1].split(":")
            host_port = binding[1] or str(49152 + len(self.containers))
            self.containers[name] = {"Id": identity, "Config": {"Labels": {
                "hexalith.timesheets.fixture": arguments[arguments.index("--label") + 1].split("=", 1)[1]}},
                "ports": {binding[2] + "/tcp": [{"HostIp": "127.0.0.1", "HostPort": host_port}]}}
            if name.endswith("-redis"):
                self.container = identity
        if self.failure:
            result = self.failure(arguments)
            if result is not None:
                return result
        output = ""
        if arguments[:2] == ["aspire", "ps"]:
            output = "[]"
        elif arguments[:2] == ["docker", "inspect"] and arguments[2].startswith("dapr_"):
            output = self.image
        elif arguments[:3] == ["docker", "image", "inspect"]:
            output = self.image
        elif arguments[:2] == ["docker", "run"]:
            output = self.containers[arguments[arguments.index("--name") + 1]]["Id"]
        elif arguments[:2] == ["docker", "inspect"]:
            container = next((value for name, value in self.containers.items()
                              if arguments[2] in (name, value["Id"])), None)
            if container is None:
                return subprocess.CompletedProcess(arguments, 1, "", "No such object")
            output = json.dumps(container["ports"] if "NetworkSettings.Ports" in arguments[-1] else container)
        elif arguments[:2] == ["docker", "rm"]:
            self.containers = {name: value for name, value in self.containers.items() if value["Id"] != arguments[-1]}
        elif arguments[:2] == ["docker", "exec"]:
            output = "PONG"
        elif arguments[:2] == ["aspire", "stop"]:
            self.stopped = True
        elif "-class" in arguments:
            output = "Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0"
        return subprocess.CompletedProcess(arguments, 0, output, "")


class IsolatedFixtureTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.logs = Path(self.temporary.name) / "logs"

    def run_fixture(self, commands, port_check=lambda: None, owned=None):
        instance = fixture.IsolatedFixture(command=commands, port_check=port_check,
            log_directory=self.logs, ownership_check=owned or (lambda: commands.started and not commands.stopped))
        with contextlib.redirect_stdout(io.StringIO()):
            result = instance.execute()
        if instance.scratch:
            self.addCleanup(lambda: shutil.rmtree(instance.scratch) if Path(instance.scratch).exists() else None)
        return instance, result

    @staticmethod
    def guard_commands(failed=False):
        commands = Commands()
        def probe(arguments):
            names = [argument.split("=", 1)[1] for argument in arguments if argument.startswith("--guard-probe=")]
            if not names:
                return None
            name = names[0]
            if failed:
                return subprocess.CompletedProcess(arguments, 42, "Guard failed", "")
            if name in ("valid-isolated", "ordinary-defaults"):
                message = "Dapr component selection: " + ("isolated fixture" if name == "valid-isolated" else "checked-in defaults")
                return subprocess.CompletedProcess(arguments, 0, message, "")
            message = "Isolated Dapr components are missing or unsafe."
            if name in ("missing-opt-in", "publish-fixture", "publish-bare-fixture"):
                message = "Isolated Dapr components require an explicit run-mode fixture opt-in."
            if name in ("bare-fixture-flag", "missing-components", "empty-components", "whitespace-components"):
                message = "Isolated Dapr fixture requires an explicit component directory."
            if name in ("missing-placement", "shared-placement", "missing-scheduler", "shared-scheduler", "duplicate-control-endpoint"):
                message = "Isolated Dapr placement and scheduler endpoints are missing or unsafe."
            return subprocess.CompletedProcess(arguments, -6, "", message)
        commands.failure = probe
        return commands

    def test_compiled_guard_driver_covers_incomplete_fixture_settings_with_harmless_probes(self):
        commands = self.guard_commands()
        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs)
        snapshot = fixture.SharedSnapshot()
        snapshot["scheduler"] = {"cluster_id": "1", "revision": "1", "keys": 0, "sha256": "fixed"}
        executable = fixture.APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
        original_is_file = Path.is_file
        with patch.object(guards, "IsolatedFixture", return_value=instance), \
                patch.object(guards, "read_only_shared_snapshot", return_value=snapshot), \
                patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, guards.run_guards())
        probes = {arguments[1].split("=", 1)[1]: (arguments, kwargs["env"])
                  for arguments, kwargs in commands.calls if arguments[0] == str(executable)}
        self.assertEqual(22, len(probes))
        for name, (arguments, environment) in probes.items():
            self.assertEqual("1", environment["TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY"], name)
            self.assertIn("--TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY=1", arguments)
        for name in ("bare-fixture-flag", "missing-components", "empty-components", "whitespace-components"):
            environment = probes[name][1]
            self.assertEqual("1", environment["TIMESHEETS_LIVE_DAPR_FIXTURE"])
            self.assertFalse(environment.get("TIMESHEETS_LIVE_DAPR_COMPONENTS", "").strip())
        for name in ("bare-fixture-flag", "ordinary-defaults", "publish-bare-fixture"):
            self.assertNotIn("TIMESHEETS_LIVE_DAPR_COMPONENTS", probes[name][1])
            self.assertNotIn("Dapr__PlacementHostAddress", probes[name][1])
            self.assertNotIn("Dapr__SchedulerHostAddress", probes[name][1])
        self.assertNotIn("TIMESHEETS_LIVE_DAPR_FIXTURE", probes["ordinary-defaults"][1])
        for setting in ("Dapr__PlacementHostAddress", "Dapr__SchedulerHostAddress"):
            self.assertTrue(probes["missing-components"][1][setting].startswith("127.0.0.1:"))
        self.assertIn("--publisher=manifest", probes["publish-fixture"][0])
        self.assertIn("--AppHost:Operation=publish", probes["publish-bare-fixture"][0])
        self.assertEqual("1", probes["publish-bare-fixture"][1]["TIMESHEETS_LIVE_DAPR_FIXTURE"])
        self.assertFalse(commands.started)
        self.assertFalse(commands.containers)
        self.assertTrue(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])
        self.assertFalse(Path(instance.scratch).exists())

    def test_configuration_only_guards_issue_only_validation_probes(self):
        commands = self.guard_commands()
        instance = fixture.IsolatedFixture(command=commands, log_directory=self.logs)
        polluted = {"timesheets_live_dapr_validate_only": "0", "timesheets_live_dapr_fixture": "1",
                    "Dapr:PlacementHostAddress": "127.0.0.1:50005", "dApR__SchedulerHostAddress": "127.0.0.1:50006",
                    "ASPIRE_TIMESHEETS_LIVE_DAPR_COMPONENTS": "/legacy/components",
                    "DOTNET_TIMESHEETS_LIVE_DAPR_COMPONENTS": "/legacy/components",
                    "AppHost__Operation": "publish", "Publishing__Publisher": "manifest"}
        instance.environment.update(polluted)
        executable = fixture.APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
        original_is_file = Path.is_file
        with patch.object(guards, "IsolatedFixture", return_value=instance), \
                patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                patch.object(guards, "available_control_port", side_effect=AssertionError("Unexpected socket bind")), \
                patch.object(guards, "read_only_shared_snapshot", side_effect=AssertionError("Unexpected storage read")), \
                contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, guards.run_guards(configuration_only=True))
        self.assertEqual(22, len(commands.calls))
        for arguments, kwargs in commands.calls:
            self.assertEqual(str(executable), arguments[0])
            self.assertEqual("1", kwargs["env"]["TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY"])
            self.assertTrue(all(isinstance(value, str) for value in kwargs["env"].values()))
            self.assertIn("--TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY=1", arguments)
            self.assertFalse(set(polluted) & set(kwargs["env"]))
        self.assertIsNone(instance.scratch)
        self.assertFalse(commands.containers)
        self.assertEqual({"configuration_only": True, "resources_enabled": False},
                         json.loads((instance.log_directory / "guards-mode.json").read_text()))

    def test_components_preserve_names_scopes_database_separation_and_defaults(self):
        originals = {path: (fixture.DEFAULT_COMPONENTS / path).read_bytes() for path in fixture.COMPONENT_FILES[:2]}
        directory = Path(self.temporary.name) / "components"
        fixture.write_components(directory, "127.0.0.1:49152")
        actor = (directory / fixture.COMPONENT_FILES[0]).read_text()
        readmodels = (directory / fixture.COMPONENT_FILES[1]).read_text()
        self.assertIn('value: "13"', actor)
        self.assertIn('value: "14"', readmodels)
        self.assertIn("scopes:\n  - eventstore", actor)
        self.assertIn("scopes:\n  - timesheets", readmodels)
        self.assertNotIn("actorStateStore", readmodels)
        for path, content in originals.items():
            self.assertEqual(content, (fixture.DEFAULT_COMPONENTS / path).read_bytes())

    def test_unsafe_components_and_missing_opt_in_are_refused(self):
        directory = Path(self.temporary.name) / "components"
        fixture.write_components(directory, "127.0.0.1:49152")
        for endpoint in ("localhost:6379", "127.0.0.1:6379", "0.0.0.0:49152", "127.0.0.1:0"):
            with self.subTest(endpoint=endpoint), self.assertRaises(fixture.FixtureError):
                fixture.validate_components(directory, endpoint)
        with self.assertRaises(fixture.FixtureError):
            fixture.validate_components(directory, "127.0.0.1:49152", opted_in=False)
        readmodels = directory / fixture.COMPONENT_FILES[1]
        original = readmodels.read_text()
        for change in (original.replace("- timesheets", "- eventstore"),
                       original.replace('value: "14"', 'value: "13"'),
                       original.replace("name: timesheets-readmodels", "name: statestore"),
                       original.replace("127.0.0.1:49152", "127.0.0.1:49153")):
            readmodels.write_text(change)
            with self.assertRaises(fixture.FixtureError):
                fixture.validate_components(directory, "127.0.0.1:49152")
        readmodels.write_text(original)
        (directory / "unexpected.yaml").write_text(original)
        with self.assertRaises(fixture.FixtureError):
            fixture.validate_components(directory, "127.0.0.1:49152")
        (directory / "unexpected.yaml").unlink()
        readmodels.unlink()
        with self.assertRaises(fixture.FixtureError):
            fixture.validate_components(directory, "127.0.0.1:49152")

    def test_matching_dapr_id_refuses_start_and_foreign_cleanup(self):
        commands = Commands(lambda args: subprocess.CompletedProcess(args, 0,
            "eventstore 3501 50001 8082 foreign\n", "") if args[:2] == ["dapr", "list"] else None)
        _, result = self.run_fixture(commands)
        self.assertEqual(1, result)
        self.assertFalse(any(args[:2] in (["aspire", "start"], ["aspire", "stop"], ["docker", "run"], ["docker", "rm"])
                             for args, _ in commands.calls))

    def test_existing_exact_apphost_is_not_stopped(self):
        commands = Commands(lambda args: subprocess.CompletedProcess(args, 0,
            json.dumps([{"appHostPath": str(fixture.APPHOST), "pid": 123}]), "")
            if args[:2] == ["aspire", "ps"] else None)
        _, result = self.run_fixture(commands)
        self.assertEqual(1, result)
        self.assertFalse(commands.started)
        self.assertFalse(commands.stopped)

    def test_apphost_ownership_uses_the_aspire_process_binding(self):
        commands = Commands()
        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None,
            log_directory=self.logs)
        instance.scratch = "/tmp/fixture-components"
        commands.failure = lambda args: (subprocess.CompletedProcess(args, 0,
            json.dumps([{"appHostPath": str(fixture.APPHOST), "appHostPid": 123, "cliPid": 456}]), "")
            if args[:2] == ["aspire", "ps"] else None)
        with patch.object(Path, "read_bytes", return_value=(
                b"TIMESHEETS_LIVE_DAPR_COMPONENTS=/tmp/fixture-components\0TIMESHEETS_LIVE_DAPR_FIXTURE=1\0")):
            self.assertTrue(instance._owned_apphost())
        with patch.object(Path, "read_bytes", return_value=b"TIMESHEETS_LIVE_DAPR_FIXTURE=1\0"):
            self.assertFalse(instance._owned_apphost())

    def test_occupied_port_refuses_start(self):
        def occupied():
            raise fixture.FixtureError("Required port 8080 is occupied")
        commands = Commands()
        _, result = self.run_fixture(commands, port_check=occupied)
        self.assertEqual(1, result)
        self.assertFalse(commands.redis_attempted)
        self.assertFalse(commands.started)

    def test_failed_start_and_interruption_stop_only_exact_owned_apphost(self):
        for interruption in (False, True):
            def fail(arguments):
                if arguments[:2] == ["aspire", "start"]:
                    if interruption:
                        raise KeyboardInterrupt
                    return subprocess.CompletedProcess(arguments, 27, "Startup failed", "")
            commands = Commands(fail)
            instance, result = self.run_fixture(commands)
            self.assertEqual(130 if interruption else 27, result)
            self.assertTrue(commands.stopped)
            self.assertFalse(Path(instance.scratch).exists())
            stops = [args for args, _ in commands.calls if args[:2] == ["aspire", "stop"]]
            self.assertEqual([["aspire", "stop", "--non-interactive", "--apphost", str(fixture.APPHOST)]], stops)
            removals = [args for args, _ in commands.calls if args[:2] == ["docker", "rm"]]
            self.assertEqual(3, len(removals))
            self.assertEqual(["docker", "rm", "--force", "--volumes", commands.container], removals[-1])

    def test_failed_or_interrupted_journey_preserves_exit_and_cleans_owned_storage(self):
        for interruption in (False, True):
            def fail(arguments):
                if "-class" in arguments:
                    if interruption:
                        raise KeyboardInterrupt
                    return subprocess.CompletedProcess(arguments, 42, "Journey failed", "")
            commands = Commands(fail)
            instance, result = self.run_fixture(commands)
            self.assertEqual(130 if interruption else 42, result)
            self.assertTrue(commands.stopped)
            self.assertFalse(Path(instance.scratch).exists())
            self.assertTrue(self.logs.exists())

    def test_foreign_runtime_after_failed_start_is_not_stopped(self):
        commands = Commands(lambda args: subprocess.CompletedProcess(args, 1, "Failed", "")
                            if args[:2] == ["aspire", "start"] else None)
        _, result = self.run_fixture(commands, owned=lambda: False)
        self.assertEqual(1, result)
        self.assertFalse(commands.stopped)

    def test_shutdown_failure_retains_fixture_artifacts(self):
        commands = Commands(lambda args: subprocess.CompletedProcess(args, 1, "Stop failed", "")
                            if args[:2] == ["aspire", "stop"] else None)
        instance, result = self.run_fixture(commands)
        self.assertEqual(1, result)
        self.assertTrue(Path(instance.scratch).exists())
        self.addCleanup(lambda: shutil.rmtree(instance.scratch))
        self.assertFalse(any(args[:2] == ["docker", "rm"] for args, _ in commands.calls))

    def test_disposable_image_port_and_no_shared_volume_configuration(self):
        commands = Commands()
        instance, result = self.run_fixture(commands)
        self.assertEqual(0, result)
        launch = next(args for args, _ in commands.calls if args[:2] == ["docker", "run"])
        self.assertIn("--pull=never", launch)
        self.assertIn(commands.image, launch)
        self.assertEqual("127.0.0.1::6379", launch[launch.index("--publish") + 1])
        self.assertEqual("/data", launch[launch.index("--tmpfs") + 1])
        self.assertNotIn("--volume", launch)
        self.assertNotIn("-v", launch)
        start_env = next(kwargs["env"] for args, kwargs in commands.calls if args[:2] == ["aspire", "start"])
        self.assertEqual(instance.scratch, start_env["TIMESHEETS_LIVE_DAPR_COMPONENTS"])
        self.assertEqual("1", start_env["TIMESHEETS_LIVE_DAPR_FIXTURE"])

    def test_container_creation_interruption_recovers_only_owned_identity(self):
        commands = Commands()
        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None,
            log_directory=self.logs, ownership_check=lambda: False)
        def fail(arguments):
            if arguments[:2] == ["docker", "run"]:
                raise KeyboardInterrupt
            if arguments[:2] == ["docker", "inspect"] and arguments[2] == instance.container_name:
                return subprocess.CompletedProcess(arguments, 0, json.dumps({"Id": commands.container,
                    "Config": {"Labels": {"hexalith.timesheets.fixture": instance.fixture_id}}}), "")
        commands.failure = fail
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(130, instance.execute())
        self.assertIn(["docker", "rm", "--force", "--volumes", commands.container], [args for args, _ in commands.calls])

    def test_unknown_container_identity_never_removes_foreign_container(self):
        commands = Commands()
        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None,
            log_directory=self.logs, ownership_check=lambda: False)
        def fail(arguments):
            if arguments[:2] == ["docker", "run"]:
                return subprocess.CompletedProcess(arguments, 0, "dapr_redis", "")
            if arguments[:2] == ["docker", "inspect"] and arguments[2] == instance.container_name:
                return subprocess.CompletedProcess(arguments, 0, json.dumps({"Id": commands.container,
                    "Config": {"Labels": {"hexalith.timesheets.fixture": "someone-else"}}}), "")
        commands.failure = fail
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(1, instance.execute())
        self.assertFalse(any(args[:2] == ["docker", "rm"] for args, _ in commands.calls))

    def test_runtime_endpoint_check_refuses_wrong_actual_sidecar_component(self):
        directory = Path(self.temporary.name) / "components"
        fixture.write_components(directory, "127.0.0.1:49152")
        with patch.object(fixture.subprocess, "check_output", return_value="123\n"), \
                patch.object(Path, "read_bytes", return_value=b"daprd\0--app-id\0eventstore\0--resources-path\0/missing\0"):
            with self.assertRaises(fixture.FixtureError):
                fixture.verify_runtime_components("127.0.0.1:49152", directory, "127.0.0.1:49153", "127.0.0.1:49154")

    def test_runtime_component_check_verifies_both_loaded_stores(self):
        directory = Path(self.temporary.name) / "components"
        fixture.write_components(directory, "127.0.0.1:49152")
        actor_directory = Path(self.temporary.name) / "actor-sidecar"
        actor_directory.mkdir()
        shutil.copyfile(directory / fixture.COMPONENT_FILES[0], actor_directory / "component.yaml")
        shutil.copyfile(directory / fixture.COMPONENT_FILES[2], actor_directory / "pubsub.yaml")
        commands = {"/proc/123/cmdline": ["daprd", "--app-id", "eventstore", "--resources-path", str(actor_directory)],
                    "/proc/456/cmdline": ["daprd", "--app-id", "timesheets", "--resources-path", str(directory / "Timesheets")]}
        for command in commands.values():
            command.extend(["--placement-host-address", "127.0.0.1:49153", "--scheduler-host-address", "127.0.0.1:49154"])
        def arguments(path):
            return "\0".join(commands[str(path)]).encode()
        with patch.object(fixture.subprocess, "check_output", return_value="123\n456\n"), \
                patch.object(Path, "read_bytes", arguments):
            fixture.verify_runtime_components("127.0.0.1:49152", directory, "127.0.0.1:49153", "127.0.0.1:49154")
            shutil.copyfile(directory / fixture.COMPONENT_FILES[1], actor_directory / "foreign.yaml")
            with self.assertRaises(fixture.FixtureError):
                fixture.verify_runtime_components("127.0.0.1:49152", directory, "127.0.0.1:49153", "127.0.0.1:49154")

    def test_evidence_sanitizer_removes_credentials_and_token_queries(self):
        secret = "do-not-retain-this-secret"
        result = fixture.sanitized("SigningKey=" + secret + "\n"
            + "http://localhost:8080/confirm?t=" + secret + "\n"
            + "dashboardLoginToken: " + secret + "\n")
        self.assertNotIn(secret, result)

    def test_telemetry_headers_and_api_keys_are_redacted_from_plain_output(self):
        for field in ("OTEL_EXPORTER_OTLP_HEADERS", "OTEL_EXPORTER_OTLP_TRACES_HEADERS",
                      "OTEL_EXPORTER_OTLP_METRICS_HEADERS", "OTEL_EXPORTER_OTLP_LOGS_HEADERS",
                      "X-API-Key", "api-key", "API_KEY", "apiKey"):
            for separator in (": ", "="):
                with self.subTest(field=field, separator=separator):
                    output = field + separator + "SYNTHETIC-CREDENTIAL\nsafe progress"
                    redacted = fixture.sanitized(output.encode())
                    self.assertNotIn("SYNTHETIC-CREDENTIAL", redacted)
                    self.assertIn("safe progress", redacted)

    def test_telemetry_and_api_key_json_values_are_completely_redacted(self):
        for field in ("OTEL_EXPORTER_OTLP_HEADERS", "OTEL_EXPORTER_OTLP_TRACES_HEADERS",
                      "OTEL_EXPORTER_OTLP_METRICS_HEADERS", "OTEL_EXPORTER_OTLP_LOGS_HEADERS",
                      "X-API-Key", "api-key", "API_KEY", "apiKey"):
            for value in ('SYNTHETIC-CREDENTIAL with "quoted" parts\nand another line',
                          {"nested": ["SYNTHETIC-CREDENTIAL", "SYNTHETIC-CREDENTIAL-two"]}):
                structured = json.dumps({field: value}, indent=2)
                for output in (structured, json.dumps({"embedded": structured})):
                    with self.subTest(field=field, value=value, output=output):
                        redacted = fixture.sanitized(output + "\nsafe progress")
                        self.assertNotIn("SYNTHETIC-CREDENTIAL", redacted)
                        self.assertIn("safe progress", redacted)

    def test_named_telemetry_and_api_key_json_records_are_redacted_in_either_order(self):
        for field in ("OTEL_EXPORTER_OTLP_HEADERS", "OTEL_EXPORTER_OTLP_TRACES_HEADERS",
                      "OTEL_EXPORTER_OTLP_METRICS_HEADERS", "OTEL_EXPORTER_OTLP_LOGS_HEADERS", "X-API-Key"):
            for name_field in ("name", "Name", "key"):
                for name_first in (True, False):
                    pairs = [(name_field, field), ("value", {"nested": ["SYNTHETIC-CREDENTIAL"]})]
                    record = dict(pairs if name_first else reversed(pairs))
                    output = json.dumps({"environmentVariables": [record,
                        {"name": "OTEL_EXPORTER_OTLP_ENDPOINT", "value": "http://localhost:4317"}],
                        "progress": "safe progress"}, indent=2)
                    with self.subTest(field=field, name_field=name_field, name_first=name_first):
                        redacted = fixture.sanitized(output)
                        self.assertNotIn("SYNTHETIC-CREDENTIAL", redacted)
                        self.assertIn("safe progress", redacted)
                        self.assertIn("http://localhost:4317", redacted)

    def test_multiline_telemetry_and_api_key_yaml_values_are_redacted(self):
        for output in (
                "OTEL_EXPORTER_OTLP_HEADERS: |\n  SYNTHETIC-CREDENTIAL\n  continuation\nsafe progress",
                "X-API-Key: >-\n  SYNTHETIC-CREDENTIAL\n  continuation\nsafe progress",
                "environment:\n  - name: OTEL_EXPORTER_OTLP_LOGS_HEADERS\n"
                "    value: SYNTHETIC-CREDENTIAL\nsafe progress"):
            with self.subTest(output=output):
                redacted = fixture.sanitized(output)
                self.assertNotIn("SYNTHETIC-CREDENTIAL", redacted)
                self.assertNotIn("continuation", redacted)
                self.assertIn("safe progress", redacted)

    def test_truncated_named_credential_json_is_discarded(self):
        for name_first in (True, False):
            pairs = [("name", "OTEL_EXPORTER_OTLP_HEADERS"), ("value", "SYNTHETIC-CREDENTIAL")]
            record = json.dumps(dict(pairs if name_first else reversed(pairs)), indent=2).rstrip("}")
            with self.subTest(name_first=name_first):
                redacted = fixture.sanitized("safe progress\n" + record)
                self.assertNotIn("SYNTHETIC-CREDENTIAL", redacted)
                self.assertIn("safe progress", redacted)

    def test_harmless_braced_text_does_not_discard_later_diagnostics(self):
        output = 'starting {phase}\n' + json.dumps({"name": "OTEL_EXPORTER_OTLP_HEADERS",
            "value": "SYNTHETIC-CREDENTIAL"}, indent=2) + "\nsafe completion"
        redacted = fixture.sanitized(output)
        self.assertNotIn("SYNTHETIC-CREDENTIAL", redacted)
        self.assertIn("starting {phase}", redacted)
        self.assertIn("safe completion", redacted)

    def test_truncated_json_string_braces_do_not_end_the_credential_record(self):
        for ending in ("", "\\"):
            output = 'safe progress\n{\n  "name": "OTEL_EXPORTER_OTLP_HEADERS",\n  "value": "' \
                + "SYNTHETIC-PREFIX}SYNTHETIC-SUFFIX" + ending
            with self.subTest(ending=ending):
                redacted = fixture.sanitized(output)
                self.assertNotIn("SYNTHETIC-", redacted)
                self.assertIn("safe progress", redacted)

    def test_named_yaml_credentials_are_redacted_regardless_of_field_order_or_indentation(self):
        for record in (
                "environment:\n  - value: SYNTHETIC-CREDENTIAL\n    name: OTEL_EXPORTER_OTLP_HEADERS",
                "environment:\n  name: OTEL_EXPORTER_OTLP_HEADERS\n  value: SYNTHETIC-CREDENTIAL",
                "environment:\n  value: SYNTHETIC-CREDENTIAL\n  name: OTEL_EXPORTER_OTLP_HEADERS",
                "environment:\n  - value: SYNTHETIC-CREDENTIAL\n    # setting\n    name: OTEL_EXPORTER_OTLP_HEADERS",
                "environment:\n  - name: OTEL_EXPORTER_OTLP_HEADERS\n    # setting\n    value: SYNTHETIC-CREDENTIAL",
                'environment:\n  - "value": SYNTHETIC-CREDENTIAL\n    "name": OTEL_EXPORTER_OTLP_HEADERS'):
            with self.subTest(record=record):
                redacted = fixture.sanitized(record + "\nsafe progress")
                self.assertNotIn("SYNTHETIC-CREDENTIAL", redacted)
                self.assertIn("safe progress", redacted)

    def test_unicode_escaped_json_credential_keys_are_redacted(self):
        for structured in ('{"api\\u004bey": "SYNTHETIC-CREDENTIAL"}',
                           '{"\\u004fTEL_EXPORTER_OTLP_HEADERS": "SYNTHETIC-CREDENTIAL"}'):
            for output in (structured, json.dumps(structured), json.dumps({"embedded": structured})):
                with self.subTest(output=output):
                    redacted = fixture.sanitized(output + "\nsafe progress")
                    self.assertNotIn("SYNTHETIC-CREDENTIAL", redacted)
                    self.assertIn("safe progress", redacted)

    def test_multiline_quoted_environment_credentials_are_completely_redacted(self):
        for field in ("OTEL_EXPORTER_OTLP_HEADERS", "API_KEY"):
            for quote in ('"', "'"):
                output = field + "=" + quote + "SYNTHETIC-FIRST\nSYNTHETIC-CONTINUATION" + quote
                with self.subTest(field=field, quote=quote):
                    redacted = fixture.sanitized(output + "\nsafe progress")
                    self.assertNotIn("SYNTHETIC-", redacted)
                    self.assertIn("safe progress", redacted)

    def test_command_evidence_redacts_telemetry_and_api_keys_in_arguments_and_streams(self):
        arguments = ["diagnostic", "inspect", "--api-key=SYNTHETIC-ARGUMENT"]
        stdout = json.dumps({"environmentVariables": [
            {"name": "OTEL_EXPORTER_OTLP_HEADERS", "value": "SYNTHETIC-STDOUT"}]}, indent=2)
        stderr = "X-API-Key: SYNTHETIC-STDERR\nsafe stderr progress"
        command_result = subprocess.CompletedProcess(arguments, 0, stdout, stderr)
        instance = fixture.IsolatedFixture(command=lambda *args, **kwargs: command_result,
            log_directory=self.logs)
        self.assertIs(command_result, instance.run_command(arguments))
        evidence = (instance.log_directory / "01-diagnostic-inspect.log").read_text()
        self.assertNotIn("SYNTHETIC-", evidence)
        self.assertIn("safe stderr progress", evidence)
        self.assertIn("Exit: 0", evidence)

    def test_timeout_evidence_discards_truncated_named_credentials_from_both_streams(self):
        arguments = ["diagnostic", "inspect"]
        stdout = b'safe stdout progress\n{\n  "name": "OTEL_EXPORTER_OTLP_HEADERS",\n  "value": "SYNTHETIC-STDOUT"'
        stderr = b'safe stderr progress\n{\n  "value": "SYNTHETIC-STDERR",\n  "name": "X-API-Key"'
        def timeout(*args, **kwargs):
            raise subprocess.TimeoutExpired(arguments, 1, output=stdout, stderr=stderr)
        instance = fixture.IsolatedFixture(command=timeout, log_directory=self.logs)
        with self.assertRaises(fixture.FixtureError):
            instance.run_command(arguments)
        evidence = (instance.log_directory / "01-diagnostic-inspect.log").read_text()
        self.assertNotIn("SYNTHETIC-", evidence)
        self.assertIn("safe stdout progress", evidence)
        self.assertIn("safe stderr progress", evidence)
        self.assertIn("Command could not complete safely.", evidence)

    def test_every_running_apphost_refuses_preflight(self):
        commands = Commands(lambda args: subprocess.CompletedProcess(args, 0,
            json.dumps([{"appHostPath": "/some/unrelated/Folders.AppHost.csproj", "pid": 123}]), "")
            if args[:2] == ["aspire", "ps"] else None)
        _, result = self.run_fixture(commands)
        self.assertEqual(1, result)
        self.assertFalse(commands.redis_attempted)
        self.assertFalse(commands.started)
        self.assertFalse(commands.stopped)

    def test_real_ipv4_listener_refuses_start(self):
        with socket.socket(socket.AF_INET) as listener:
            listener.bind(("127.0.0.1", 0))
            listener.listen()
            with patch.object(fixture, "REQUIRED_PORTS", (listener.getsockname()[1],)):
                commands = Commands()
                _, result = self.run_fixture(commands, port_check=fixture.assert_ports_available)
                self.assertEqual(1, result)
                self.assertFalse(commands.redis_attempted)
                self.assertFalse(commands.started)

    def test_real_supported_ipv6_listener_refuses_start(self):
        try:
            listener = socket.socket(socket.AF_INET6)
            listener.setsockopt(socket.IPPROTO_IPV6, socket.IPV6_V6ONLY, 1)
            listener.bind(("::1", 0))
        except OSError as error:
            self.skipTest("IPv6 listener unsupported: " + str(error.errno))
        with listener:
            listener.listen()
            with patch.object(fixture, "REQUIRED_PORTS", (listener.getsockname()[1],)):
                commands = Commands()
                _, result = self.run_fixture(commands, port_check=fixture.assert_ports_available)
                self.assertEqual(1, result)
                self.assertFalse(commands.redis_attempted)
                self.assertFalse(commands.started)

    def test_control_services_use_immutable_images_and_private_advertisement(self):
        commands = Commands()
        instance, result = self.run_fixture(commands)
        self.assertEqual(0, result)
        launches = [args for args, _ in commands.calls if args[:2] == ["docker", "run"]]
        self.assertEqual(3, len(launches))
        for launch in launches:
            self.assertIn("--pull=never", launch)
            self.assertIn(commands.image, launch)
            self.assertNotIn("--volume", launch)
            self.assertNotIn("-v", launch)
            self.assertTrue(launch[launch.index("--publish") + 1].startswith("127.0.0.1:"))
        scheduler = next(args for args in launches if "/scheduler" in args)
        self.assertIn("--override-broadcast-host-port=" + instance.scheduler, scheduler)
        self.assertIn("--etcd-data-dir=/data", scheduler)
        self.assertEqual("/data", scheduler[scheduler.index("--tmpfs") + 1])
        environment = next(kwargs["env"] for args, kwargs in commands.calls if args[:2] == ["aspire", "start"])
        self.assertEqual(instance.placement, environment["Dapr__PlacementHostAddress"])
        self.assertEqual(instance.scheduler, environment["Dapr__SchedulerHostAddress"])
        fixture.validate_control_plane(instance.placement, instance.scheduler)

    def test_shared_missing_or_duplicate_control_endpoints_are_refused(self):
        for bad in ("", "localhost:50005", "127.0.0.1:50005", "127.0.0.1:50006",
                    "127.0.0.1:6050", "127.0.0.1:6060", "0.0.0.0:49154"):
            for position in (0, 1):
                endpoints = ["127.0.0.1:49153", "127.0.0.1:49154"]
                endpoints[position] = bad
                with self.subTest(bad=bad, position=position), self.assertRaises(fixture.FixtureError):
                    fixture.validate_control_plane(*endpoints)
        with self.assertRaises(fixture.FixtureError):
            fixture.validate_control_plane("127.0.0.1:49153", "127.0.0.1:49153")

    def test_runtime_checks_actual_endpoint_mismatch_for_each_loaded_component(self):
        directory = Path(self.temporary.name) / "expected"
        fixture.write_components(directory, "127.0.0.1:49152")
        actual = Path(self.temporary.name) / "actual"
        shutil.copytree(directory, actual)
        commands = {"/proc/123/cmdline": ["daprd", "--app-id", "eventstore", "--resources-path", str(actual)],
                    "/proc/456/cmdline": ["daprd", "--app-id", "timesheets", "--resources-path", str(actual / "Timesheets")]}
        for command in commands.values():
            command.extend(["--placement-host-address", "127.0.0.1:49153", "--scheduler-host-address", "127.0.0.1:49154"])
        def arguments(path):
            return "\0".join(commands[str(path)]).encode()
        with patch.object(fixture.subprocess, "check_output", return_value="123\n456\n"), \
                patch.object(Path, "read_bytes", arguments):
            fixture.verify_runtime_components("127.0.0.1:49152", directory, "127.0.0.1:49153", "127.0.0.1:49154")
            for relative in fixture.COMPONENT_FILES:
                path = actual / relative
                original = path.read_text()
                for wrong in ("localhost:6379", "127.0.0.1:49155"):
                    path.write_text(original.replace("127.0.0.1:49152", wrong))
                    with self.subTest(component=relative, wrong=wrong), self.assertRaises(fixture.FixtureError):
                        fixture.verify_runtime_components("127.0.0.1:49152", directory, "127.0.0.1:49153", "127.0.0.1:49154")
                path.write_text(original)
            for command in commands.values():
                for flag in ("--placement-host-address", "--scheduler-host-address"):
                    index = command.index(flag) + 1
                    original = command[index]
                    command[index] = "localhost:50006"
                    with self.subTest(flag=flag), self.assertRaises(fixture.FixtureError):
                        fixture.verify_runtime_components("127.0.0.1:49152", directory, "127.0.0.1:49153", "127.0.0.1:49154")
                    command[index] = original

    def test_unknown_docker_recovery_retains_artifacts_and_original_exit(self):
        commands = Commands()
        def fail(arguments):
            if arguments[:2] == ["docker", "run"]:
                return subprocess.CompletedProcess(arguments, 27, "", "Docker transport failed")
            if arguments[:2] == ["docker", "inspect"] and not arguments[2].startswith("dapr_"):
                return subprocess.CompletedProcess(arguments, 1, "", "Docker unavailable")
            if arguments[:3] == ["docker", "container", "ls"]:
                return subprocess.CompletedProcess(arguments, 1, "", "Docker unavailable")
        commands.failure = fail
        instance, result = self.run_fixture(commands)
        self.assertEqual(27, result)
        self.assertFalse(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])
        self.assertFalse(any(args[:2] == ["docker", "rm"] for args, _ in commands.calls))

    def test_post_removal_inspect_failure_checks_the_exact_owned_name(self):
        commands = Commands()
        removed_name = []
        def fail(arguments):
            if arguments[:2] == ["docker", "rm"]:
                removed_name.append(next(name for name, value in commands.containers.items()
                                         if value["Id"] == arguments[-1]))
            if removed_name and arguments[:2] == ["docker", "inspect"]:
                return subprocess.CompletedProcess(arguments, 1, "", "Inspect transport failed")
            if removed_name and arguments[:3] == ["docker", "container", "ls"]:
                # Correct name inventory sees a surviving container. An ID
                # incorrectly used as a name filter would falsely report empty.
                output = commands.container if "name=^/" + removed_name[-1] + "$" in arguments else ""
                return subprocess.CompletedProcess(arguments, 0, output, "")
        commands.failure = fail
        instance, result = self.run_fixture(commands)
        self.assertEqual(1, result)
        self.assertTrue(Path(instance.scratch).exists())
        evidence = json.loads((instance.log_directory / "cleanup.json").read_text())
        self.assertFalse(evidence["complete"])
        self.assertIn("unknown", evidence["failure_reasons"][0])

    def test_confirmed_docker_absence_can_complete_recovery(self):
        commands = Commands()
        def fail(arguments):
            if arguments[:2] == ["docker", "run"]:
                commands.containers.clear()
                return subprocess.CompletedProcess(arguments, 27, "", "Container creation failed")
        commands.failure = fail
        instance, result = self.run_fixture(commands)
        self.assertEqual(27, result)
        self.assertTrue(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])
        self.assertFalse(any(args[:2] == ["docker", "rm"] for args, _ in commands.calls))

    def test_surviving_bound_resource_preserves_dependencies(self):
        commands = Commands()
        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None,
            log_directory=self.logs, ownership_check=lambda: commands.started and not commands.stopped,
            resource_check=lambda *_: [{"pid": 123, "start": "456", "state": "S"}])
        with patch.object(fixture.time, "monotonic", side_effect=(0, 100)), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(1, instance.execute())
        self.assertTrue(Path(instance.scratch).exists())
        self.addCleanup(shutil.rmtree, instance.scratch)
        self.assertFalse(any(args[:2] == ["docker", "rm"] for args, _ in commands.calls))

    def test_component_removal_failure_preserves_failure_or_marks_success_incomplete(self):
        for original_exit in (0, 42):
            commands = Commands(lambda args: subprocess.CompletedProcess(args, original_exit,
                "Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0", "") if "-class" in args else None)
            with patch.object(fixture.shutil, "rmtree", side_effect=PermissionError("Cannot remove components")):
                instance, result = self.run_fixture(commands)
            self.assertEqual(original_exit or 1, result)
            self.assertTrue(Path(instance.scratch).exists())
            self.addCleanup(shutil.rmtree, instance.scratch)
            self.assertFalse(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])

    def test_log_parent_reuse_creates_unique_evidence_and_keeps_previous_logs(self):
        first, result = self.run_fixture(Commands())
        self.assertEqual(0, result)
        before = {path.name: path.read_bytes() for path in first.log_directory.iterdir()}
        second, result = self.run_fixture(Commands())
        self.assertEqual(0, result)
        self.assertNotEqual(first.log_directory, second.log_directory)
        self.assertEqual(before, {path.name: path.read_bytes() for path in first.log_directory.iterdir()})

    def test_zero_skipped_failed_errors_or_duplicate_live_summaries_are_rejected(self):
        for summary in ("", "Total: 0, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0",
                        "Total: 1, Errors: 0, Failed: 0, Skipped: 1, Not Run: 0",
                        "Total: 1, Errors: 1, Failed: 0, Skipped: 0, Not Run: 0",
                        "Total: 1, Errors: 0, Failed: 1, Skipped: 0, Not Run: 0",
                        "Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 1",
                        "Total: 2, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0",
                        "Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0\n" * 2):
            commands = Commands(lambda args: subprocess.CompletedProcess(args, 0, summary, "") if "-class" in args else None)
            instance, result = self.run_fixture(commands)
            self.assertEqual(1, result)
            self.assertTrue(commands.stopped)
            self.assertFalse(Path(instance.scratch).exists())

    def test_structured_credential_fields_are_redacted(self):
        for field in ("token", "Authorization", "SigningKey", "APP_API_TOKEN", "accessToken", "password"):
            for value in ("SECRET", "Bearer SECRET", 'SECRET with \"quoted\" parts'):
                output = json.dumps({field: value})
                self.assertNotIn("SECRET", fixture.sanitized(output))
                self.assertNotIn("SECRET", fixture.sanitized(json.dumps({"embedded": output})))

    def test_preservation_allows_only_confirmed_natural_expiry(self):
        def snapshot(expiring, captured):
            result = fixture.SharedSnapshot()
            result["redis"] = {"0": {"persistent_keys": 1, "persistent_sha256": "fixed"}}
            result["scheduler"] = {"cluster_id": "1", "revision": "1", "keys": 1, "sha256": "fixed"}
            result.expiring = {"0": expiring}
            result.captured_at = {"0": captured}
            return result
        before = snapshot({"key-a": ("value-a", 1000)}, 900)
        self.assertTrue(fixture.shared_preservation(before, snapshot({}, 1001))["matches"])
        self.assertFalse(fixture.shared_preservation(before, snapshot({}, 999))["matches"])
        for altered in ({"key-a": ("value-b", 1000)}, {"key-a": ("value-a", 1100)},
                        {"key-a": ("value-a", 1000), "new-key": ("new-value", 2000)}):
            self.assertFalse(fixture.shared_preservation(before, snapshot(altered, 950))["matches"])
        after = snapshot({"key-a": ("value-a", 1001)}, 950)
        self.assertTrue(fixture.shared_preservation(before, after)["matches"])
        after["redis"]["0"]["persistent_sha256"] = "changed"
        self.assertFalse(fixture.shared_preservation(before, after)["matches"])

    def test_timeout_retains_both_sanitized_streams(self):
        def timeout(arguments, **_):
            raise subprocess.TimeoutExpired(arguments, 1, output=b'progress\n{"token": "SECRET"}',
                                            stderr=b'failure details\n{"Authorization": "Bearer SECRET"}')
        instance = fixture.IsolatedFixture(command=timeout, log_directory=self.logs)
        with self.assertRaises(fixture.FixtureError):
            instance.run_command(["python3", "journey"], timeout=1)
        log = next(instance.log_directory.glob("*.log")).read_text()
        self.assertIn("progress", log)
        self.assertIn("failure details", log)
        self.assertNotIn("SECRET", log)

    def _process_tree_command(self):
        pid_file = Path(self.temporary.name) / "descendant-pids"
        program = """
import os, signal, sys, time
signal.signal(signal.SIGTERM, signal.SIG_IGN)
child = os.fork()
if child == 0:
    grandchild = os.fork()
    with open(sys.argv[1], 'a') as f:
        f.write(str(os.getpid()) + '\\n')
        f.flush()
print('journey progress', flush=True)
print('journey failure details', file=sys.stderr, flush=True)
time.sleep(60)
"""
        return [sys.executable, "-c", program, str(pid_file)], pid_file

    def test_real_journey_timeout_terminates_and_reaps_descendants(self):
        arguments, pid_file = self._process_tree_command()
        instance = fixture.IsolatedFixture(log_directory=self.logs)
        with self.assertRaises(fixture.FixtureError):
            instance.run_command(arguments, timeout=0.4, journey=True)
        self.assertTrue(instance.journey_shutdown_confirmed)
        pids = [int(value) for value in pid_file.read_text().splitlines()]
        self.assertEqual(2, len(pids))
        for pid in pids:
            self.assertFalse(Path(f"/proc/{pid}").exists())
            with self.assertRaises(ChildProcessError):
                os.waitpid(pid, os.WNOHANG)
        log = next(instance.log_directory.glob("*.log")).read_text()
        self.assertIn("journey progress", log)
        self.assertIn("journey failure details", log)

    def test_real_journey_interruption_terminates_and_reaps_descendants(self):
        arguments, pid_file = self._process_tree_command()
        instance = fixture.IsolatedFixture(log_directory=self.logs)
        timer = threading.Timer(0.4, lambda: os.kill(os.getpid(), signal.SIGINT))
        timer.start()
        try:
            with self.assertRaises(KeyboardInterrupt):
                instance.run_command(arguments, timeout=20, journey=True)
        finally:
            timer.cancel()
            timer.join()
        self.assertTrue(instance.journey_shutdown_confirmed)
        pids = [int(value) for value in pid_file.read_text().splitlines()]
        self.assertEqual(2, len(pids))
        for pid in pids:
            self.assertFalse(Path(f"/proc/{pid}").exists())
        log = next(instance.log_directory.glob("*.log")).read_text()
        self.assertIn("journey progress", log)
        self.assertIn("journey failure details", log)

    def test_dapr_loaded_manifest_extensions_and_case_are_all_examined(self):
        expected = Path(self.temporary.name) / "expected"
        fixture.write_components(expected, "127.0.0.1:49152")
        actual = Path(self.temporary.name) / "actual"
        shutil.copytree(expected, actual)
        arguments = {
            "/proc/123/cmdline": ["daprd", "--app-id", "eventstore", "--resources-path", str(actual)],
            "/proc/456/cmdline": ["daprd", "--app-id", "timesheets", "--resources-path", str(actual / "Timesheets")],
        }
        for command in arguments.values():
            command.extend(["--placement-host-address", "127.0.0.1:49153", "--scheduler-host-address", "127.0.0.1:49154"])
        with patch.object(fixture.subprocess, "check_output", return_value="123\n456\n"), \
                patch.object(Path, "read_bytes", lambda path: "\0".join(arguments[str(path)]).encode()):
            for extension in (".yml", ".YML", ".Yaml", ".YAML"):
                for resource in (actual, actual / "Timesheets"):
                    with self.subTest(extension=extension, resource=resource):
                        foreign = resource / ("foreign" + extension)
                        foreign.write_text((expected / fixture.COMPONENT_FILES[0]).read_text())
                        with self.assertRaises(fixture.FixtureError):
                            fixture.verify_runtime_components("127.0.0.1:49152", expected,
                                                              "127.0.0.1:49153", "127.0.0.1:49154")
                        foreign.unlink()
            # Valid copies are also recognized under Dapr's alternative names.
            (actual / "statestore.yaml").rename(actual / "statestore.YML")
            (actual / "Timesheets/timesheets-readmodels.yaml").rename(actual / "Timesheets/readmodels.Yaml")
            fixture.verify_runtime_components("127.0.0.1:49152", expected, "127.0.0.1:49153", "127.0.0.1:49154")

    def test_multiline_json_yaml_and_url_credentials_are_completely_redacted(self):
        outputs = (
            '{"token":\n  "SECRET"}\nsafe progress',
            json.dumps({"token": {"nested": {"password": "SECRET"}, "other": "SECRET-two"},
                        "progress": "safe progress"}, indent=2),
            'token: |\n  SECRET\n  SECRET-two\nsafe progress',
            'Authorization: >-\n  Bearer SECRET\n  SECRET-two\nsafe progress',
            'password: first-part\n  SECRET\nsafe progress',
            'metadata:\n  - name: redisPassword\n    value: SECRET\nsafe progress',
            'https://user:SECRET@example.test/status?t=SECRET-two\nsafe progress',
            'redis://user:SECRET@127.0.0.1:49152/0\nsafe progress',
        )
        for output in outputs:
            with self.subTest(output=output):
                redacted = fixture.sanitized(output)
                self.assertNotIn("SECRET", redacted)
                self.assertIn("safe progress", redacted)

    def test_nullable_and_malformed_docker_ownership_retains_original_failure(self):
        for value in (None, [], {"Config": None}, {"Config": {}}, {"Config": {"Labels": None}},
                      {"Config": {"Labels": []}}, {"Config": {"Labels": "invalid"}}):
            with self.subTest(value=value):
                commands = Commands()
                def fail(arguments):
                    if "-class" in arguments:
                        return subprocess.CompletedProcess(arguments, 42, "Journey failed", "failure details")
                    if arguments[:2] == ["docker", "inspect"] and arguments[2].startswith("timesheets-live-dapr-"):
                        return subprocess.CompletedProcess(arguments, 0, json.dumps(value), "")
                commands.failure = fail
                instance, result = self.run_fixture(commands)
                self.assertEqual(42, result)
                self.assertTrue(Path(instance.scratch).exists())
                self.assertFalse(any(args[:2] == ["docker", "rm"] for args, _ in commands.calls))
                self.assertFalse(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])

    def test_nullable_and_malformed_published_ports_fail_safely(self):
        for value in (None, [], {}, {"6379/tcp": None}, {"6379/tcp": {}}, {"6379/tcp": [None]},
                      {"6379/tcp": [{"HostIp": "127.0.0.1", "HostPort": None}]},
                      {"6379/tcp": [{"HostIp": "127.0.0.1", "HostPort": 49152}]}):
            with self.subTest(value=value):
                commands = Commands(lambda arguments: subprocess.CompletedProcess(arguments, 0, json.dumps(value), "")
                    if arguments[:2] == ["docker", "inspect"] and "NetworkSettings.Ports" in arguments[-1] else None)
                instance, result = self.run_fixture(commands)
                self.assertEqual(1, result)
                self.assertFalse(commands.started)
                self.assertTrue(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])

    def test_evidence_write_failure_preserves_original_exit_and_reports_incomplete(self):
        for filename in ("cleanup.json", ".log", "resource-exit.json"):
            for original_exit in (0, 42):
                with self.subTest(filename=filename, original_exit=original_exit):
                    commands = Commands(lambda args: subprocess.CompletedProcess(args, original_exit,
                        "Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0", "failure details")
                        if "-class" in args else None)
                    original_write = Path.write_text
                    def write(path, *args, **kwargs):
                        if path.name == filename or filename == ".log" and path.suffix == ".log":
                            raise PermissionError("evidence write blocked")
                        return original_write(path, *args, **kwargs)
                    with patch.object(Path, "write_text", write):
                        instance, result = self.run_fixture(commands)
                    self.assertEqual(original_exit or 1, result)
                    self.assertTrue(instance.evidence_write_failed)
                    self.assertFalse(Path(instance.scratch).exists())

    def test_late_preservation_evidence_write_failure_cannot_return_success(self):
        snapshot = fixture.SharedSnapshot()
        snapshot["scheduler"] = {"cluster_id": "1", "revision": "1", "keys": 0, "sha256": "fixed"}
        for filename in ("shared-after.json", "shared-preservation.json"):
            for original_exit in (0, 42):
                with self.subTest(filename=filename, original_exit=original_exit):
                    commands = Commands(lambda args: subprocess.CompletedProcess(args, original_exit,
                        "Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0", "") if "-class" in args else None)
                    instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None,
                        log_directory=self.logs, ownership_check=lambda: commands.started and not commands.stopped)
                    instance.injected_command = False
                    original_write = Path.write_text
                    def write(path, *args, **kwargs):
                        if path.name == filename:
                            raise PermissionError("late evidence write blocked")
                        return original_write(path, *args, **kwargs)
                    with patch.object(instance, "_journey_command", commands), \
                            patch.object(fixture, "read_only_shared_snapshot", return_value=snapshot), \
                            patch.object(fixture, "verify_runtime_components"), \
                            patch.object(Path, "write_text", write), contextlib.redirect_stdout(io.StringIO()):
                        self.assertEqual(original_exit or 1, instance.execute())
                    self.assertTrue(instance.evidence_write_failed)
                    self.assertTrue(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])
                    self.assertFalse(Path(instance.scratch).exists())

    def test_guard_late_preservation_write_failure_cannot_return_success(self):
        snapshot = fixture.SharedSnapshot()
        snapshot["scheduler"] = {"cluster_id": "1", "revision": "1", "keys": 0, "sha256": "fixed"}
        for filename in ("shared-after.json", "shared-preservation.json"):
            with self.subTest(filename=filename):
                commands = self.guard_commands()
                instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs)
                original_write = Path.write_text
                original_is_file = Path.is_file
                executable = fixture.APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
                def write(path, *args, **kwargs):
                    if path.name == filename:
                        raise PermissionError("late guard evidence write blocked")
                    return original_write(path, *args, **kwargs)
                with patch.object(guards, "IsolatedFixture", return_value=instance), \
                        patch.object(guards, "read_only_shared_snapshot", return_value=snapshot), \
                        patch.object(Path, "write_text", write), \
                        patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(1, guards.run_guards())
                self.assertTrue(instance.evidence_write_failed)
                self.assertFalse(Path(instance.scratch).exists())

    def test_completed_journey_shutdown_failure_retains_status_streams_and_dependencies(self):
        commands = Commands()
        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None,
            log_directory=self.logs, ownership_check=lambda: commands.started and not commands.stopped)
        def journey(arguments):
            if "-class" in arguments:
                return instance._journey_command([sys.executable, "-c",
                    "import sys; print('captured progress'); print('captured failure', file=sys.stderr); sys.exit(42)"],
                    cwd=fixture.ROOT, env=instance.environment, timeout=5)
        commands.failure = journey
        with patch.object(fixture, "stop_process_group", side_effect=fixture.FixtureError("Unproved shutdown")), \
                contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(42, instance.execute())
        self.assertFalse(instance.journey_shutdown_confirmed)
        self.assertFalse(commands.stopped)
        self.assertTrue(Path(instance.scratch).exists())
        self.addCleanup(shutil.rmtree, instance.scratch)
        log = next(path for path in instance.log_directory.glob("*.log") if "captured progress" in path.read_text())
        self.assertIn("captured failure", log.read_text())
        self.assertIn("Exit: 42", log.read_text())
        self.assertFalse(any(args[:2] == ["docker", "rm"] for args, _ in commands.calls))

    def test_timeout_shutdown_failure_retains_both_sanitized_streams(self):
        instance = fixture.IsolatedFixture(log_directory=self.logs)
        original_stop = fixture.stop_process_group
        def stop_then_refuse(process):
            original_stop(process)
            raise fixture.FixtureError("Unproved shutdown")
        with patch.object(fixture, "stop_process_group", stop_then_refuse):
            with self.assertRaises(fixture.FixtureError):
                instance.run_command([sys.executable, "-c",
                    "import sys,time;print('captured progress',flush=True);print('captured failure',file=sys.stderr,flush=True);"
                    "print('{\"token\":\\n \"SECRET\"}',file=sys.stderr,flush=True);time.sleep(60)"], timeout=0.4, journey=True)
        self.assertFalse(instance.journey_shutdown_confirmed)
        log = next(instance.log_directory.glob("*.log")).read_text()
        self.assertIn("captured progress", log)
        self.assertIn("captured failure", log)
        self.assertNotIn("SECRET", log)

    def test_real_bound_surviving_process_blocks_dependency_deletion(self):
        commands = Commands()
        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None,
            log_directory=self.logs, ownership_check=lambda: commands.started and not commands.stopped)
        probe = Path(self.temporary.name) / "dotnet"
        probe.symlink_to(sys.executable)
        processes = []
        def launch_bound(arguments):
            if arguments[:2] == ["aspire", "start"]:
                environment = dict(os.environ, TIMESHEETS_LIVE_DAPR_COMPONENTS=instance.scratch)
                processes.append(subprocess.Popen([str(probe), "-c", "import time; time.sleep(60)"], env=environment))
        commands.failure = launch_bound
        try:
            with patch.object(fixture.time, "monotonic", side_effect=(0, 100)), contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1, instance.execute())
            self.assertIsNone(processes[0].poll())
            actual = fixture.bound_resource_processes(instance.scratch, instance.placement, instance.scheduler)
            self.assertIn(processes[0].pid, [process["pid"] for process in actual])
            self.assertTrue(Path(instance.scratch).exists())
            self.assertFalse(any(args[:2] == ["docker", "rm"] for args, _ in commands.calls))
            self.assertFalse(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])
        finally:
            for process in processes:
                process.terminate()
                process.wait(timeout=5)
            if instance.scratch and Path(instance.scratch).exists():
                shutil.rmtree(instance.scratch)

    def test_scheduler_range_requires_valid_envelope_rows_count_and_complete_range(self):
        header = {"cluster_id": "1", "member_id": "2", "revision": "3", "raft_term": "1"}
        row = {"key": "a2V5", "value": "dmFsdWU=", "create_revision": "1", "mod_revision": "3", "version": "1"}
        valid = {"header": header, "kvs": [row], "count": "1"}
        self.assertEqual(1, fixture.scheduler_fingerprint(valid)["keys"])
        self.assertEqual(0, fixture.scheduler_fingerprint({"header": header})["keys"])
        self.assertEqual(0, fixture.scheduler_fingerprint({"header": header, "kvs": [], "count": "0"})["keys"])
        invalid = [None, [], {}, {"error": "unavailable"}, {"header": {}}, {"header": header, "unknown": True},
                   dict(valid, more=True), dict(valid, count="2"), dict(valid, count=None), dict(valid, count=True),
                   dict(valid, kvs=None), dict(valid, kvs=[None]), dict(valid, kvs=[row, row], count="2")]
        for field, value in (("key", "%%%"), ("key", ""), ("value", None), ("value", "invalid"),
                             ("create_revision", "0"), ("mod_revision", "4"), ("version", False)):
            invalid.append(dict(valid, kvs=[dict(row, **{field: value})]))
        for response in invalid:
            with self.subTest(response=response), self.assertRaises(fixture.FixtureError):
                fixture.scheduler_fingerprint(response)

    def test_snapshot_reader_rejects_http_success_with_invalid_scheduler_range(self):
        class RedisConnection:
            def __enter__(self):
                return self

            def __exit__(self, *_):
                pass

            def sendall(self, _):
                pass

            def makefile(self, _):
                return io.BytesIO(b'+OK\r\n*2\r\n*2\r\n$1\r\n1\r\n$1\r\n0\r\n*0\r\n' * 3)

        header = {"cluster_id": "1", "member_id": "2", "revision": "3", "raft_term": "1"}
        for response in (b'{"error":"unavailable"}', b'{"header":{},"count":"0"}',
                         b'{"count":"0","kvs":[]}', b'not-json'):
            with self.subTest(response=response), \
                    patch.object(fixture.socket, "create_connection", return_value=RedisConnection()), \
                    patch.object(fixture.urllib.request, "urlopen", return_value=io.BytesIO(response)):
                with self.assertRaises(fixture.FixtureError):
                    fixture.read_only_shared_snapshot()
        with patch.object(fixture.socket, "create_connection", return_value=RedisConnection()), \
                patch.object(fixture.urllib.request, "urlopen", return_value=io.BytesIO(json.dumps({"header": header}).encode())):
            snapshot = fixture.read_only_shared_snapshot()
            self.assertEqual(0, snapshot["scheduler"]["keys"])

    def test_query_redaction_covers_uppercase_schemes_and_relative_urls(self):
        for path in ("HTTP://example.test/confirm", "HTTPS://example.test/adjust", "/confirm", "../adjust", "confirm", ""):
            with self.subTest(path=path):
                output = fixture.sanitized(path + "?t=SECRET&code=SECRET-two\nsafe progress")
                self.assertNotIn("SECRET", output)
                self.assertIn("safe progress", output)
                if path:
                    self.assertIn(path, output)

    def test_invalid_utf8_capture_keeps_both_streams_and_original_nonzero_exit(self):
        arguments = [sys.executable, "-c", "import os,sys;os.write(1,b'progress\\xff\\n');"
                     "os.write(2,b'failure\\xfe\\n');sys.exit(42)"]
        for journey in (False, True):
            with self.subTest(journey=journey):
                instance = fixture.IsolatedFixture(log_directory=self.logs)
                with self.assertRaises(fixture.FixtureError) as failure:
                    instance.run_command(arguments, timeout=5, journey=journey)
                self.assertEqual(42, failure.exception.exit_code)
                log = next(instance.log_directory.glob("*.log")).read_text()
                self.assertIn("Exit: 42", log)
                self.assertIn("progress\ufffd", log)
                self.assertIn("failure\ufffd", log)
                self.assertTrue(instance.journey_shutdown_confirmed)

    def test_final_preservation_cancellation_keeps_completed_journey_failure(self):
        snapshot = fixture.SharedSnapshot()
        snapshot["scheduler"] = {"cluster_id": "1", "revision": "1", "keys": 0, "sha256": "fixed"}
        for original_exit in (0, 42):
            with self.subTest(original_exit=original_exit):
                commands = Commands(lambda args: subprocess.CompletedProcess(args, original_exit,
                    "Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0", "") if "-class" in args else None)
                instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None,
                    log_directory=self.logs, ownership_check=lambda: commands.started and not commands.stopped)
                instance.injected_command = False
                with patch.object(instance, "_journey_command", commands), \
                        patch.object(fixture, "read_only_shared_snapshot", side_effect=(snapshot, KeyboardInterrupt())), \
                        patch.object(fixture, "verify_runtime_components"), contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(original_exit or 130, instance.execute())
                self.assertFalse(Path(instance.scratch).exists())

    def test_guard_final_preservation_cancellation_keeps_existing_failure(self):
        snapshot = fixture.SharedSnapshot()
        snapshot["scheduler"] = {"cluster_id": "1", "revision": "1", "keys": 0, "sha256": "fixed"}
        executable = fixture.APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
        original_is_file = Path.is_file
        for failed in (False, True):
            with self.subTest(failed=failed):
                instance = fixture.IsolatedFixture(command=self.guard_commands(failed), port_check=lambda: None,
                    log_directory=self.logs)
                with patch.object(guards, "IsolatedFixture", return_value=instance), \
                        patch.object(guards, "read_only_shared_snapshot", side_effect=(snapshot, KeyboardInterrupt())), \
                        patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(1 if failed else 130, guards.run_guards())
                self.assertFalse(Path(instance.scratch).exists())

    def test_scheduler_preservation_requires_same_cluster_and_range_contents(self):
        header = {"cluster_id": "01", "member_id": "2", "revision": "003", "raft_term": "1"}
        row = {"key": "a2V5", "value": "dmFsdWU=", "create_revision": "1", "mod_revision": "3", "version": "1"}
        def snapshot(range_response):
            value = fixture.SharedSnapshot()
            value["scheduler"] = fixture.scheduler_fingerprint(range_response)
            return value
        before = snapshot({"header": header, "kvs": [row], "count": "1"})
        self.assertEqual("1", before["scheduler"]["cluster_id"])
        self.assertEqual("3", before["scheduler"]["revision"])
        changed_leader = dict(header, cluster_id=1, member_id="4", revision="4", raft_term="2")
        after = snapshot({"header": changed_leader, "kvs": [row], "count": "1"})
        self.assertTrue(fixture.shared_preservation(before, after)["matches"])
        other_cluster = snapshot({"header": dict(header, cluster_id="2"), "kvs": [row], "count": "1"})
        self.assertFalse(fixture.shared_preservation(before, other_cluster)["matches"])
        changed_value = snapshot({"header": header, "kvs": [dict(row, value="Y2hhbmdlZA==")], "count": "1"})
        self.assertFalse(fixture.shared_preservation(before, changed_value)["matches"])

    def test_captured_live_identity_blocks_deletion_even_when_current_scanner_cannot_find_it(self):
        commands = Commands()
        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs,
            ownership_check=lambda: commands.started and not commands.stopped)
        process = subprocess.Popen([sys.executable, "-c", "import time;time.sleep(60)"])
        try:
            fields = Path(f"/proc/{process.pid}/stat").read_text().rsplit(")", 1)[1].split()
            identity = {"pid": process.pid, "start": fields[19], "state": fields[0]}
            instance.captured_processes = [identity]
            with patch.object(fixture.time, "monotonic", side_effect=(0, 100)), contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1, instance.execute())
            self.assertEqual([], fixture.bound_resource_processes(instance.scratch, instance.placement, instance.scheduler))
            evidence = json.loads((instance.log_directory / "recorded-process-exit.json").read_text())
            self.assertFalse(evidence["all_gone"])
            self.assertEqual([dict(identity, gone=False)], evidence["processes"])
            self.assertTrue(Path(instance.scratch).exists())
            self.assertFalse(any(args[:2] == ["docker", "rm"] for args, _ in commands.calls))
        finally:
            process.terminate()
            process.wait(timeout=5)
            if instance.scratch and Path(instance.scratch).exists():
                shutil.rmtree(instance.scratch)
        self.assertTrue(fixture.recorded_process_exits([identity])["all_gone"])

    def test_runner_retains_captured_identities_and_generates_exit_evidence(self):
        commands = Commands()
        snapshot = fixture.SharedSnapshot()
        snapshot["scheduler"] = {"cluster_id": "1", "revision": "1", "keys": 0, "sha256": "fixed"}
        identity = {"pid": 999999999, "start": "1", "state": "S"}
        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs,
            ownership_check=lambda: commands.started and not commands.stopped, resource_check=lambda *_: [])
        instance.injected_command = False
        with patch.object(instance, "_journey_command", commands), \
                patch.object(instance, "resource_check", side_effect=([identity], [])), \
                patch.object(fixture, "read_only_shared_snapshot", return_value=snapshot), \
                patch.object(fixture, "verify_runtime_components"), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, instance.execute())
        self.assertEqual([identity], instance.captured_processes)
        evidence = json.loads((instance.log_directory / "recorded-process-exit.json").read_text())
        self.assertTrue(evidence["all_gone"])
        self.assertEqual([dict(identity, gone=True)], evidence["processes"])

    def test_populated_snapshot_reader_selects_db0_13_14_and_detects_changed_broker_value(self):
        def resp(value):
            if isinstance(value, list):
                return b"*" + str(len(value)).encode() + b"\r\n" + b"".join(resp(item) for item in value)
            if isinstance(value, int):
                return b":" + str(value).encode() + b"\r\n"
            value = str(value).encode()
            return b"$" + str(len(value)).encode() + b"\r\n" + value + b"\r\n"
        class RedisConnection:
            def __init__(self, databases):
                self.databases = databases
                self.database = None
                self.selected = []
                self.stream = io.BytesIO()

            def __enter__(self):
                return self

            def __exit__(self, *_):
                pass

            def makefile(self, _):
                return self.stream

            def sendall(self, data):
                request = io.BytesIO(data)
                count = int(request.readline()[1:])
                arguments = []
                for _ in range(count):
                    length = int(request.readline()[1:])
                    arguments.append(request.read(length))
                    request.read(2)
                if arguments[0] == b"SELECT":
                    self.database = int(arguments[1])
                    self.selected.append(self.database)
                    response = b"+OK\r\n"
                else:
                    response = resp([["1", "0"], self.databases[self.database]])
                position = self.stream.tell()
                self.stream.seek(0, io.SEEK_END)
                self.stream.write(response)
                self.stream.seek(position)

        databases = {0: [["broker-key", "broker-v1", -1], ["broker-ttl", "ttl-value", 10000]],
                     13: [["actor-key", "actor-value", -1]], 14: [["model-key", "model-value", -1]]}
        scheduler = {"header": {"cluster_id": "1", "member_id": "2", "revision": "3", "raft_term": "1"}}
        def read(database_rows):
            connection = RedisConnection(database_rows)
            with patch.object(fixture.socket, "create_connection", return_value=connection), \
                    patch.object(fixture.urllib.request, "urlopen", return_value=io.BytesIO(json.dumps(scheduler).encode())):
                snapshot = fixture.read_only_shared_snapshot()
            self.assertEqual([0, 13, 14], connection.selected)
            self.assertEqual({"0", "13", "14"}, set(snapshot["redis"]))
            for database, expected_key in (("0", "broker-key"), ("13", "actor-key"), ("14", "model-key")):
                permanent = [(key, value) for key, value, ttl in database_rows[int(database)] if ttl == -1]
                expected = fixture.hashlib.sha256(json.dumps(permanent, separators=(",", ":")).encode()).hexdigest()
                self.assertEqual(expected, snapshot["redis"][database]["persistent_sha256"])
                self.assertEqual(expected_key, permanent[0][0])
            self.assertEqual({"broker-ttl": ("ttl-value", 10000)}, snapshot.expiring["0"])
            return snapshot
        before = read(databases)
        self.assertTrue(fixture.shared_preservation(before, read(databases))["matches"])
        changed = dict(databases)
        changed[0] = [["broker-key", "broker-v2", -1], ["broker-ttl", "ttl-value", 10000]]
        self.assertFalse(fixture.shared_preservation(before, read(changed))["matches"])


if __name__ == "__main__":
    unittest.main()
