"""Failure-injected fixture checks; never start an AppHost or Docker container."""

import ast
import contextlib
import http.server
import io
import os
import select
import signal
import socket
import sys
import threading
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import time
import unittest
import xml.etree.ElementTree as ET
from unittest.mock import MagicMock, patch

import local_runtime_admission as admission
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
        self.run_properties = {"RunCommand": sys.executable, "RunArguments": "",
                               "RunWorkingDirectory": str(fixture.ROOT)}

    def __call__(self, arguments, **kwargs):
        self.calls.append((arguments, {**kwargs, "env": dict(kwargs["env"])}))
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
        if "-preprocess" in arguments:
            output = "<!--\n" + arguments[2] + "\n-->"
        elif arguments[:2] == ["dotnet", "msbuild"]:
            output = json.dumps({"Properties": {}, "Items": {"ProjectReference": []}} if "-getItem:ProjectReference" in arguments
                                else {"Properties": self.run_properties})
        elif arguments[:2] in (["aspire", "ps"], ["dapr", "list"]):
            output = "[]"
        elif arguments[:3] == ["aspire", "describe", "security"]:
            output = json.dumps({"resources": [{"name": "security", "resourceType": "Container",
                "properties": {"container.id": "f" * 64}, "urls": [{"name": "http", "url": "http://127.0.0.1:18180"}]}]})
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


class RedisSnapshotConnection:
    """RESP transport double; never connects to a service."""

    def __init__(self, databases, server=b"# Server\r\nrun_id:" + b"a" * 40 + b"\r\n", digest_rows=True):
        self.databases = databases
        self.server = server
        self.digest_rows = digest_rows
        self.database = None
        self.arguments = []
        self.stream = io.BytesIO()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        pass

    def makefile(self, _):
        return self.stream

    @staticmethod
    def resp(value):
        if isinstance(value, list):
            return b"*" + str(len(value)).encode() + b"\r\n" + b"".join(RedisSnapshotConnection.resp(item) for item in value)
        if isinstance(value, int):
            return b":" + str(value).encode() + b"\r\n"
        value = value if isinstance(value, bytes) else str(value).encode()
        return b"$" + str(len(value)).encode() + b"\r\n" + value + b"\r\n"

    def sendall(self, data):
        request = io.BytesIO(data)
        count = int(request.readline()[1:])
        arguments = []
        for _ in range(count):
            length = int(request.readline()[1:])
            arguments.append(request.read(length))
            request.read(2)
        self.arguments.append(arguments)
        if arguments[0] == b"INFO":
            response = self.resp(self.server)
        elif arguments[0] == b"SELECT":
            self.database = int(arguments[1])
            response = b"+OK\r\n"
        else:
            rows = self.databases[self.database]
            if self.digest_rows:
                rows = [[fixture.hashlib.sha1(key.encode()).hexdigest(), fixture.hashlib.sha1(value.encode()).hexdigest(), ttl]
                        for key, value, ttl in rows]
            response = self.resp([["1", "0"], rows])
        position = self.stream.tell()
        self.stream.seek(0, io.SEEK_END)
        self.stream.write(response)
        self.stream.seek(position)


class IsolatedFixtureTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.logs = Path(self.temporary.name) / "logs"
        self.comparison = fixture.SharedComparison()
        self.native_dapr_inventory = fixture.running_dapr_processes
        inventory = patch.object(fixture, "running_dapr_processes", return_value=[])
        inventory.start()
        self.addCleanup(inventory.stop)

    def new_shared_snapshot(self):
        snapshot = fixture.SharedSnapshot(self.comparison)
        snapshot.redis_identity = self.comparison.fingerprint(b"redis-identity", b"server")
        snapshot.scheduler_identity = self.comparison.fingerprint(b"scheduler-identity", b"1")
        snapshot.scheduler_content = self.comparison.fingerprint(b"scheduler-content", b"fixed")
        for database in ("0", "13", "14"):
            snapshot["redis"][database] = {"keys": 0, "persistent_keys": 0, "expiring_keys": 0}
            snapshot.persistent[database] = {}
            snapshot.expiring[database] = {}
            snapshot.captured_at[database] = 900
        return snapshot

    @staticmethod
    def update_shared_counts(snapshot):
        for database in snapshot["redis"]:
            persistent, expiring = len(snapshot.persistent[database]), len(snapshot.expiring[database])
            snapshot["redis"][database].update(keys=persistent + expiring, persistent_keys=persistent, expiring_keys=expiring)

    def read_shared_rows(self, databases, server=b"# Server\r\nrun_id:" + b"a" * 40 + b"\r\n",
                         comparison=None, scheduler=None, digest_rows=True):
        connection = RedisSnapshotConnection(databases, server, digest_rows)
        if scheduler is None:
            scheduler = {"header": {"cluster_id": "1", "member_id": "2", "revision": "3", "raft_term": "1"}}
        with patch.object(fixture.socket, "create_connection", return_value=connection) as connect, \
                patch.object(fixture.urllib.request, "urlopen", return_value=io.BytesIO(json.dumps(scheduler).encode())) as urlopen:
            snapshot = fixture.read_only_shared_snapshot(comparison if comparison is not None else self.comparison)
        connect.assert_called_once_with(("127.0.0.1", 6379), timeout=20)
        self.assertEqual(20, urlopen.call_args.kwargs["timeout"])
        return snapshot, connection

    @contextlib.contextmanager
    def owned_http_server(self, respond):
        class Handler(http.server.BaseHTTPRequestHandler):
            def do_GET(self):
                respond(self)

            do_POST = do_GET

            def log_message(self, *_arguments):
                pass

        server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        worker = threading.Thread(target=lambda: server.serve_forever(poll_interval=0.01), daemon=True)
        worker.start()
        try:
            yield "http://127.0.0.1:" + str(server.server_port)
        finally:
            server.shutdown()
            server.server_close()
            worker.join(timeout=2)

    @staticmethod
    def dashboard_socket_fixture(process, port, *, inode="123", address="0100007F", owner="1"):
        """Build a deterministic proc fixture; real kernel ownership is tested separately."""
        network = process.parent / "net"
        network.mkdir(exist_ok=True)
        for table in ("tcp", "tcp6"):
            (network / table).write_text("header\n" + (f"0: {address}:{port:04X} 00000000:0000 0A 0 0 0 0 0 {inode}\n"
                if table == ("tcp" if len(address) == 8 else "tcp6") else ""))
        descriptors = process.parent / owner / "fd"
        descriptors.mkdir(parents=True, exist_ok=True)
        link = descriptors / "3"
        link.unlink(missing_ok=True)
        link.symlink_to("socket:[" + inode + "]")

    def test_public_port_validation_and_explicit_runner_environment(self):
        for invalid in ("", " ", "abc", "18.080", "+18080", "-1", True, None, 1023, 65536,
                        8081, 8082, 3501, 6379, 2379, 6050, 6060, 50005, 50006):
            with self.subTest(value=invalid), self.assertRaises(fixture.FixtureError):
                fixture.IsolatedFixture(public_port=invalid, log_directory=self.logs)
        caller = dict(os.environ)
        with patch.dict(os.environ, {"timesheets__localpublicport": "8081", "ASPIRE_Timesheets__LocalPublicPort": "8082"}):
            instance = fixture.IsolatedFixture(public_port=18080, log_directory=self.logs)
        self.assertEqual(caller, dict(os.environ))
        self.assertEqual("18080", instance.environment["Timesheets__LocalPublicPort"])
        self.assertNotIn("timesheets__localpublicport", instance.environment)
        self.assertNotIn("ASPIRE_Timesheets__LocalPublicPort", instance.environment)
        self.assertEqual(8080, fixture.validate_public_port("8080"))
        self.assertEqual(1024, fixture.validate_public_port("1024"))
        self.assertEqual(65535, fixture.validate_public_port("65535"))
        with patch.object(fixture, "assert_ports_available") as check:
            instance.port_check()
            check.assert_called_once_with(18080)

    def test_selected_public_port_checks_a_real_listener_before_infrastructure(self):
        with socket.socket(socket.AF_INET) as listener:
            listener.bind(("127.0.0.1", 0))
            listener.listen()
            commands = Commands()
            instance = fixture.IsolatedFixture(command=commands, public_port=listener.getsockname()[1], log_directory=self.logs)
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1, instance.execute())
            self.assertFalse(commands.redis_attempted)

    def test_journey_public_request_expressions_use_the_selected_origin(self):
        source = (fixture.ROOT / "scripts/local_aspire_magic_link_journey.py").read_text()
        tree = ast.parse(source)
        self.assertNotIn("http://127.0.0.1:8080", source)
        # Evaluate the actual management, display and submit URL expressions with
        # a selected origin, without importing the runtime journey or starting it.
        namespace = self.public_initialization(source, {"Timesheets__LocalPublicPort": "18080"},
                                               {"Timesheets__LocalPublicPort": "18080"})
        origin = namespace["public_origin"]
        self.assertEqual("http://127.0.0.1:18080", origin)
        management = next(node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == "management")
        namespace.update({"tenant": "isolated", "actor": "actor", "app_token": "app-token",
                          "sign": lambda *args, **kwargs: "assertion", "call": lambda url, *args, **kwargs: url})
        exec(compile(ast.Module(body=[management], type_ignores=[]), "journey-management", "exec"), namespace)
        self.assertEqual(origin + "/management", namespace["management"]("/management", {}))
        urls = [node for node in ast.walk(tree) if isinstance(node, ast.JoinedStr)
                and any(isinstance(part, ast.FormattedValue) and isinstance(part.value, ast.Name)
                        and part.value.id == "public_origin" for part in node.values)]
        self.assertEqual(2, len(urls))
        evaluated = [eval(compile(ast.Expression(node), "journey-url", "eval"),
                          {"public_origin": origin, "route": "confirm"}) for node in urls]
        self.assertEqual({origin + "/api/timesheets/magic-links/confirm",
                          origin + "/api/timesheets/magic-links/confirm/submit"}, set(evaluated))

    @staticmethod
    def public_initialization(source, host_environment, caller_environment=None):
        # Execute the production initialization statements, including environment
        # lookup, validation, agreement and origin construction, without runtime I/O.
        tree = ast.parse(source)
        start = next(index for index, node in enumerate(tree.body) if isinstance(node, ast.Assign)
                     and any(isinstance(target, ast.Name) and target.id == "public_port" for target in node.targets))
        end = next(index for index, node in enumerate(tree.body) if isinstance(node, ast.Assign)
                   and any(isinstance(target, ast.Name) and target.id == "public_origin" for target in node.targets))
        required = [node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == "require"]
        from types import SimpleNamespace
        namespace = {"environment": host_environment, "os": SimpleNamespace(environ=caller_environment or {}),
                     "validate_public_port": fixture.validate_public_port}
        exec(compile(ast.Module(body=required + tree.body[start:end + 1], type_ignores=[]), "public-initialization", "exec"), namespace)
        return namespace

    def test_journey_initialization_refuses_mismatch_and_hardcoded_default_mutant(self):
        source = (fixture.ROOT / "scripts/local_aspire_magic_link_journey.py").read_text()
        for host in ("8080", "malformed", None):
            with self.subTest(host=host), self.assertRaises(RuntimeError):
                self.public_initialization(source, {"Timesheets__LocalPublicPort": host}, {"Timesheets__LocalPublicPort": "18080"})
        with self.assertRaises(fixture.FixtureError):
            self.public_initialization(source, {"Timesheets__LocalPublicPort": "18080"}, {"Timesheets__LocalPublicPort": "bad"})
        mutation = source.replace('public_port = validate_public_port(os.environ.get("Timesheets__LocalPublicPort", "8080"))',
                                  'public_port = 8080')
        self.assertNotEqual(source, mutation)
        with self.assertRaises(RuntimeError):
            self.public_initialization(mutation, {"Timesheets__LocalPublicPort": "18080"}, {"Timesheets__LocalPublicPort": "18080"})

    def test_projection_probe_uses_validated_process_public_port(self):
        source = (fixture.ROOT / "scripts/local_aspire_projection_probe.py").read_text()
        namespace = self.public_initialization(source, {"Timesheets__LocalPublicPort": "18080"})
        self.assertEqual("http://127.0.0.1:18080", namespace["public_origin"])
        urls = [keyword.value for node in ast.walk(ast.parse(source)) if isinstance(node, ast.Call)
                and isinstance(node.func, ast.Name) and node.func.id == "post"
                for keyword in node.keywords if keyword.arg == "url"]
        self.assertEqual(1, len(urls))
        self.assertEqual("http://127.0.0.1:18080/project/v2", eval(compile(ast.Expression(urls[0]), "projection-url", "eval"), namespace))
        self.assertNotIn("http://127.0.0.1:8080", source)
        for value in (None, "bad", "8081", "65536"):
            with self.subTest(value=value), self.assertRaises(fixture.FixtureError):
                self.public_initialization(source, {"Timesheets__LocalPublicPort": value})

    @staticmethod
    def projection_journey_namespace():
        tree = ast.parse((fixture.ROOT / "scripts/local_aspire_magic_link_journey.py").read_text())
        names = {"require", "verified_positioned_events", "projection_failure_diagnostic", "project"}
        nodes = [node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name in names
                 or isinstance(node, ast.Assign) and any(isinstance(target, ast.Name)
                    and target.id == "PROJECTION_FAILURE_REASON_CODES" for target in node.targets)]
        from magic_link_index_selection import legacy_index_diagnostics
        namespace = {"tenant": "fixture-tenant", "json": json, "secrets": fixture.secrets, "time": MagicMock(monotonic=lambda: 1.0),
                     "timesheets": "private-origin", "ACTOR_RATE_LIMIT_RETRY_DELAYS": (1, 5, 15, 45),
                     "legacy_index_diagnostics": legacy_index_diagnostics, "read_model": lambda *_args, **_kwargs: None,
                     "sign": lambda *_args, **_kwargs: "unused"}
        exec(compile(ast.Module(body=nodes, type_ignores=[]), "journey-delivery", "exec"), namespace)
        return namespace

    def test_delivery_diagnostic_matches_sdk_reason_constants_and_rejects_arbitrary_fields(self):
        namespace = self.projection_journey_namespace()
        expected = namespace["PROJECTION_FAILURE_REASON_CODES"]
        contract = fixture.GATEWAY_PROJECT.parents[1] / "Hexalith.EventStore.Contracts/Projections/ProjectionDispatchReasonCodes.cs"
        proof = fixture.GATEWAY_PROJECT.parents[1] / "Hexalith.EventStore.Client/Projections/SharedProjectionEpochCoordinator.AppliedProof.cs"
        if contract.is_file() and proof.is_file():
            sdk_codes = set(fixture.re.findall(r'public const string \w+ = "([^"]+)";', contract.read_text()))
            sdk_codes.update(fixture.re.findall(r'return new\(false, "([^"]+)"', proof.read_text()))
            self.assertEqual(sdk_codes, expected)
        private = fixture.secrets.token_hex(24)
        describe = namespace["projection_failure_diagnostic"]
        for reason in expected:
            for status in range(5):
                report = describe(200, [{"status": status, "reasonCode": reason, "state": {"token": private},
                                         "projectionType": private, "arbitrary": private}], "legacy-direct-root=present-unverified entries=2")
                self.assertEqual(reason, report["reason"])
                self.assertEqual(status, report["outcome_status"])
                self.assertTrue(report["legacy_root_present"])
                self.assertTrue(report["legacy_entries_nonempty"])
                self.assertNotIn(private, json.dumps(report))
                self.assertEqual(json.dumps(report), fixture.sanitized(json.dumps(report)).strip())
        for value in (None, True, -1, 5, private, [private], {"token": private}):
            with self.subTest(value=value):
                report = describe(value, [{"status": value, "reasonCode": value}], private)
                self.assertIsNone(report["http_status"])
                self.assertIsNone(report["outcome_status"])
                self.assertIn(report["reason"], ("absent", "unknown"))
                self.assertNotIn(private, json.dumps(report))
        self.assertIsNone(describe(200, [None] * 65)["outcome_count"])

    def test_delivery_diagnostic_privacy_checks_run_without_the_eventstore_source_checkout(self):
        missing = Path(self.temporary.name) / "missing-sdk/src/Hexalith.EventStore/Hexalith.EventStore.csproj"
        with patch.object(fixture, "GATEWAY_PROJECT", missing):
            self.test_delivery_diagnostic_matches_sdk_reason_constants_and_rejects_arbitrary_fields()

    def test_delivery_failure_prints_separate_safe_line_that_survives_resource_name_redaction(self):
        namespace = self.projection_journey_namespace()
        private = fixture.secrets.token_hex(24)
        route = "magic-link-token-hash-capability-index"
        namespace["call"] = MagicMock(return_value=(200, json.dumps({"version": 2, "outcomes": [{"projectionType": route,
            "status": 4, "reasonCode": "delivery_identity_conflict", "state": {"token": private}, "arbitrary": private}]}).encode()))
        namespace["read_model"] = lambda *_args, **_kwargs: {"entries": {private: {"tenant": private}}}
        output = io.StringIO()
        with contextlib.redirect_stdout(output), self.assertRaises(RuntimeError) as failure:
            namespace["project"]([{"sequenceNumber": 1, "globalPosition": 7, "payload": private}], private, route, private)
        retained = fixture.sanitized(output.getvalue() + str(failure.exception))
        lines = [line for line in retained.splitlines() if line.startswith("Delivery outcome: ")]
        self.assertEqual(1, len(lines))
        report = json.loads(lines[0].removeprefix("Delivery outcome: "))
        self.assertEqual((200, 1, 4, "delivery_identity_conflict"), tuple(report[name] for name in (
            "http_status", "outcome_count", "outcome_status", "reason")))
        self.assertTrue(report["legacy_entries_nonempty"])
        self.assertNotIn(private, retained)
        self.assertNotIn(route, retained)
        self.assertEqual(1, namespace["call"].call_count)
        namespace["time"].sleep.assert_not_called()

    def test_delivery_retries_valid_nonterminal_outcomes_with_the_exact_same_dispatch_and_body(self):
        private = fixture.secrets.token_hex(24)
        for status, reason in ((2, "projection_partial_retry"), (2, "delivery_in_progress"), (2, "delivery_gap"),
                              (2, "head_changed"), (3, "projection_handler_failure"), (3, private), (2, None)):
            namespace = self.projection_journey_namespace()
            snapshots = []
            def call(_url, _method, body, _assertion):
                snapshots.append(json.dumps(body, sort_keys=True))
                return 200, json.dumps({"version": 2, "outcomes": [{"projectionType": "fixture-route",
                    "status": status if len(snapshots) == 1 else 1,
                    "reasonCode": reason if len(snapshots) == 1 else None}]}).encode()
            namespace["call"] = MagicMock(side_effect=call)
            output = io.StringIO()
            with self.subTest(status=status, reason=reason), contextlib.redirect_stdout(output):
                namespace["project"]([{"sequenceNumber": 1, "globalPosition": 7}], "owner", "fixture-route", "catalog")
            self.assertEqual(2, namespace["call"].call_count)
            self.assertEqual(snapshots[0], snapshots[1])
            self.assertTrue(json.loads(snapshots[0])["dispatchId"])
            namespace["time"].sleep.assert_called_once_with(1)
            self.assertNotIn(private, fixture.sanitized(output.getvalue()))

    def test_delivery_exhaustion_requires_completion_and_keeps_the_existing_attempt_bound(self):
        for status in (2, 3):
            namespace = self.projection_journey_namespace()
            namespace["call"] = MagicMock(return_value=(200, json.dumps({"version": 2, "outcomes": [
                {"projectionType": "fixture-route", "status": status, "reasonCode": "projection_handler_failure"}]}).encode()))
            with self.subTest(status=status), contextlib.redirect_stdout(io.StringIO()), self.assertRaises(RuntimeError):
                namespace["project"]([{"sequenceNumber": 1, "globalPosition": 7}], "owner", "fixture-route", "catalog")
            self.assertEqual(5, namespace["call"].call_count)
            self.assertEqual([(1,), (5,), (15,), (45,)], [call.args for call in namespace["time"].sleep.call_args_list])
        for http_status in (429, 500, 503):
            namespace = self.projection_journey_namespace()
            namespace["call"] = MagicMock(side_effect=[(http_status, b""), (200, json.dumps({"version": 2,
                "outcomes": [{"projectionType": "fixture-route", "status": 0}]}).encode())])
            with contextlib.redirect_stdout(io.StringIO()):
                namespace["project"]([{"sequenceNumber": 1, "globalPosition": 7}], "owner", "fixture-route", "catalog")
            self.assertEqual(2, namespace["call"].call_count)

    def test_delivery_refuses_terminal_identity_conflict_and_malformed_results_without_retry(self):
        private = fixture.secrets.token_hex(24)
        valid = {"version": 2, "outcomes": [{"projectionType": "fixture-route", "status": 2, "reasonCode": "head_changed"}]}
        rejected = [{**valid, "version": value} for value in (None, True, 3)]
        rejected.extend({"version": 2, "outcomes": [{**valid["outcomes"][0], "status": value}]}
                        for value in (None, True, False, -1, 5, 2.0, private))
        rejected.extend({"version": 2, "outcomes": [{**valid["outcomes"][0], "status": value,
                            "reasonCode": "delivery_identity_conflict"}]} for value in range(5))
        rejected.append({"version": 2, "outcomes": [{"projectionType": "fixture-route", "status": 4,
                                                     "reasonCode": "delivery_state_unavailable"}]})
        rejected.append({"version": 2, "outcomes": [{**valid["outcomes"][0], "projectionType": private}]})
        rejected.extend({"version": 2, "outcomes": value} for value in ([], [{}, {}], None, private, [private]))
        rejected.extend({"version": 2, "outcomes": [{**valid["outcomes"][0], "reasonCode": value}]}
                        for value in (True, [private], private * 6, "not-ascii-\u00e9"))
        rejected.append({"version": 2, "outcomes": [{**valid["outcomes"][0], "state": {"token": private}}]})
        rejected.extend(([private], private))
        for payload in rejected:
            namespace = self.projection_journey_namespace()
            namespace["call"] = MagicMock(return_value=(200, json.dumps(payload).encode()))
            output = io.StringIO()
            with self.subTest(payload=payload), contextlib.redirect_stdout(output), self.assertRaises(RuntimeError):
                namespace["project"]([{"sequenceNumber": 1, "globalPosition": 7}], "owner", "fixture-route", "catalog")
            self.assertEqual(1, namespace["call"].call_count)
            namespace["time"].sleep.assert_not_called()
            self.assertNotIn(private, fixture.sanitized(output.getvalue()))

    def test_delivery_completion_accepts_only_matching_integer_sdk_success_enums(self):
        for status in (0, 1):
            namespace = self.projection_journey_namespace()
            namespace["call"] = MagicMock(return_value=(200, json.dumps({"version": 2, "outcomes": [
                {"projectionType": "fixture-route", "status": status}]}).encode()))
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                namespace["project"]([{"sequenceNumber": 1, "globalPosition": 7}], "owner", "fixture-route", "catalog")
            self.assertEqual({"attempts": 1, "elapsed_seconds": 0.0, "completed": True},
                json.loads(output.getvalue().removeprefix("Delivery timing: ")))
            self.assertEqual(1, namespace["call"].call_count)

    def test_private_allocations_exclude_selected_public_listener_and_each_other(self):
        listener = MagicMock()
        listener.__enter__.return_value = listener
        selections = ([25000, 22001], [25000, 22001, 22002], [25000, 22001, 22002, 22003])
        commands = Commands()
        instance = fixture.IsolatedFixture(command=commands, public_port=25000, log_directory=self.logs)
        try:
            with patch.object(fixture.socket, "socket", return_value=listener), \
                    patch.object(fixture.secrets.SystemRandom, "sample", side_effect=selections), contextlib.redirect_stdout(io.StringIO()):
                instance.start_infrastructure()
            bindings = [args[args.index("--publish") + 1] for args, _ in commands.calls if args[:2] == ["docker", "run"]]
            self.assertEqual(["127.0.0.1:22001:6379", "127.0.0.1:22002:50005", "127.0.0.1:22003:50006"], bindings)
            self.assertEqual([(("127.0.0.1", port),) for port in (22001, 22002, 22003)],
                             [call.args for call in listener.bind.call_args_list])
        finally:
            instance.cleanup()

    def test_private_port_candidates_enforce_lower_range_and_reserved_public_and_explicit_exclusions(self):
        for selected in (20000, 29999):
            with self.subTest(selected=selected):
                listener = MagicMock()
                listener.__enter__.return_value = listener
                excluded = (25000, 22001)
                candidates = [19999, 30000, 51393, *fixture.SHARED_CONTROL_PORTS, *fixture.REQUIRED_PORTS, *excluded, selected]
                with patch.object(fixture.secrets.SystemRandom, "sample", return_value=candidates) as randomize, \
                        patch.object(fixture.socket, "socket", return_value=listener):
                    self.assertEqual(selected, fixture.available_control_port(excluded))
                population, attempts = randomize.call_args.args
                self.assertEqual((20000, 29999), (population.start, population.stop - 1))
                self.assertEqual(20, attempts)
                listener.bind.assert_called_once_with(("127.0.0.1", selected))

    def test_private_port_allocator_skips_a_real_occupied_lower_range_listener(self):
        occupied = fixture.available_control_port()
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", occupied))
            listener.listen()
            available = fixture.available_control_port((occupied,))
            with patch.object(fixture.secrets.SystemRandom, "sample", return_value=[occupied, available]):
                selected = fixture.available_control_port()
            self.assertEqual(available, selected)
            self.assertNotEqual(occupied, selected)
            self.assertTrue(20000 <= selected <= 29999)

    def test_private_port_allocator_fails_closed_when_the_candidate_budget_is_exhausted(self):
        candidates = list(range(22000, 22020))
        for all_excluded in (False, True):
            with self.subTest(all_excluded=all_excluded):
                listener = MagicMock()
                listener.__enter__.return_value = listener
                listener.bind.side_effect = OSError("occupied candidate")
                with patch.object(fixture.secrets.SystemRandom, "sample", return_value=candidates), \
                        patch.object(fixture.socket, "socket", return_value=listener) as opened, \
                        self.assertRaisesRegex(fixture.FixtureError, "Could not allocate a private control-service endpoint"):
                    fixture.available_control_port(tuple(candidates) if all_excluded else ())
                if all_excluded:
                    opened.assert_not_called()
                else:
                    self.assertEqual(20, opened.call_count)
                    self.assertEqual(20, listener.__exit__.call_count)
                    self.assertEqual([(("127.0.0.1", port),) for port in candidates],
                                     [call.args for call in listener.bind.call_args_list])

    def test_runtime_children_discard_probe_only_settings_and_provider_aliases(self):
        pollution = {prefix + name: "1" for prefix in ("", "ASPIRE_", "DOTNET_", "ASPNETCORE_")
                     for name in ("TIMESHEETS_LIVE_DAPR_MODEL_ONLY", "timesheets_live_dapr_validate_only",
                                  "TIMESHEETS_LIVE_DAPR_CONSTRUCTION_ONLY")}
        caller = dict(os.environ)
        commands = Commands()
        with patch.dict(os.environ, pollution):
            instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs,
                ownership_check=lambda: commands.started and not commands.stopped)
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0, instance.execute())
        self.assertEqual(caller, dict(os.environ))
        for _, kwargs in commands.calls:
            self.assertFalse(set(pollution) & set(kwargs["env"]))

    @staticmethod
    def admission_probe(cwd, tmpdir):
        environment = dict(os.environ, PYTHONPATH=str(fixture.ROOT / "scripts"), TMPDIR=str(tmpdir))
        code = ("from local_runtime_admission import LocalRuntimeAdmission, RuntimeAdmissionError\n"
                "try:\n with LocalRuntimeAdmission(): print('admitted')\n"
                "except RuntimeAdmissionError as error:\n print(str(error)); raise SystemExit(7)\n")
        return subprocess.run([sys.executable, "-c", code], cwd=cwd, env=environment,
                              capture_output=True, text=True, timeout=5)

    def test_runtime_admission_contends_across_process_checkout_and_tmpdir_and_releases(self):
        other = Path(self.temporary.name) / "other-checkout"
        other.mkdir()
        alternate_tmp = Path(self.temporary.name) / "other-tmp"
        alternate_tmp.mkdir()
        with admission.LocalRuntimeAdmission():
            result = self.admission_probe(other, alternate_tmp)
            self.assertEqual(7, result.returncode)
            self.assertIn("admission is busy or unsafe", result.stdout)
            self.assertNotIn(str(fixture.ROOT), result.stdout + result.stderr)
        self.assertEqual(0, self.admission_probe(other, alternate_tmp).returncode)
        self.assertTrue(admission.LOCK_PATH.is_file())

    def test_runtime_admission_releases_after_graceful_and_abrupt_process_exit(self):
        import select
        code = ("import sys\nfrom local_runtime_admission import LocalRuntimeAdmission\n"
                "with LocalRuntimeAdmission():\n print('held', flush=True)\n sys.stdin.read()\n")
        for abrupt in (False, True):
            with self.subTest(abrupt=abrupt):
                process = subprocess.Popen([sys.executable, "-c", code], stdin=subprocess.PIPE,
                    stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
                    env=dict(os.environ, PYTHONPATH=str(fixture.ROOT / "scripts")))
                try:
                    ready, _, _ = select.select([process.stdout], [], [], 5)
                    self.assertTrue(ready, "Admission holder did not become ready")
                    self.assertEqual("held", process.stdout.readline().strip())
                    self.assertEqual(7, self.admission_probe(self.temporary.name, self.temporary.name).returncode)
                    if abrupt:
                        process.kill()
                    process.communicate(input="", timeout=5)
                    self.assertEqual(0, self.admission_probe(self.temporary.name, self.temporary.name).returncode)
                finally:
                    if process.poll() is None:
                        process.kill()
                    process.communicate(timeout=5)

    def test_admission_refuses_unsafe_paths_and_unavailable_locking(self):
        with patch.object(admission, "fcntl", None), self.assertRaises(admission.RuntimeAdmissionError):
            with admission.LocalRuntimeAdmission():
                self.fail("Unavailable locking was accepted")
        with tempfile.TemporaryDirectory(dir="/tmp", prefix="timesheets-lock-check-") as directory:
            unsafe = Path(directory) / "symlink"
            unsafe.symlink_to(admission.LOCK_PATH)
            with patch.object(admission, "LOCK_PATH", unsafe), self.assertRaises(admission.RuntimeAdmissionError):
                with admission.LocalRuntimeAdmission():
                    self.fail("Unsafe lock path was accepted")
        descriptor, path = tempfile.mkstemp(dir="/tmp", prefix="timesheets-unsafe-lock-")
        os.close(descriptor)
        unsafe = Path(path)
        try:
            unsafe.chmod(0o644)
            with patch.object(admission, "LOCK_PATH", unsafe), self.assertRaises(admission.RuntimeAdmissionError):
                with admission.LocalRuntimeAdmission():
                    self.fail("A publicly writable/readable lock was accepted")
            unsafe.unlink()
            unsafe.symlink_to(admission.LOCK_PATH)
            with patch.object(admission, "LOCK_PATH", unsafe), self.assertRaises(admission.RuntimeAdmissionError):
                with admission.LocalRuntimeAdmission():
                    self.fail("A symlink lock was accepted")
        finally:
            unsafe.unlink(missing_ok=True)

    def test_configuration_guards_need_no_runtime_lock_and_refuse_endpoint_model_changes(self):
        executable = fixture.APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
        original_is_file = Path.is_file
        for changed in (None, "isExternal", "isExplicitlyProxied"):
            with self.subTest(changed=changed):
                commands = self.guard_commands()
                original = commands.failure
                if changed:
                    def mutation(arguments):
                        result = original(arguments)
                        if "--guard-probe=model-default" in arguments:
                            result.stdout = result.stdout.replace('"' + changed + '": false', '"' + changed + '": true')
                        return result
                    commands.failure = mutation
                instance = fixture.IsolatedFixture(command=commands, log_directory=self.logs)
                with patch.object(guards, "LocalRuntimeAdmission", side_effect=AssertionError("Unexpected admission lock")), \
                        patch.object(guards, "IsolatedFixture", return_value=instance), \
                        patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(1 if changed else 0, guards.run_guards(configuration_only=True))
                self.assertFalse(commands.started)
                self.assertFalse(commands.containers)

    def test_invalid_public_port_cli_refuses_without_infrastructure(self):
        for value in ("invalid", "8081", "65536"):
            result = subprocess.run([sys.executable, str(fixture.ROOT / "scripts/run_isolated_magic_link_dapr.py"),
                                     "--public-port", value], capture_output=True, text=True, timeout=5)
            self.assertEqual(2, result.returncode)
            self.assertIn("Local public port is missing or unsafe", result.stderr)
            self.assertNotIn("Traceback", result.stderr)

    def test_both_runtime_drivers_refuse_contention_before_creating_infrastructure(self):
        commands = Commands()
        instance = fixture.IsolatedFixture(command=commands, log_directory=self.logs)
        with admission.LocalRuntimeAdmission(), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(1, instance.execute())
            with patch.object(guards, "IsolatedFixture", side_effect=AssertionError("Unexpected fixture creation")):
                self.assertEqual(1, guards.run_guards())
        self.assertFalse(commands.calls)

    def test_admission_is_held_through_failure_cleanup_and_preservation(self):
        commands = Commands(lambda args: subprocess.CompletedProcess(args, 42, "", "failed")
                            if args[:2] == ["aspire", "start"] else None)
        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs,
            ownership_check=lambda: commands.started and not commands.stopped)
        snapshot = self.shared_snapshot()
        snapshot["scheduler"]["revision"] = "1"
        instance.shared_before = snapshot
        cleanup = instance.cleanup
        def while_cleanup():
            self.assertEqual(7, self.admission_probe(self.temporary.name, self.temporary.name).returncode)
            return cleanup()
        def while_preservation(comparison):
            self.assertEqual(7, self.admission_probe(self.temporary.name, self.temporary.name).returncode)
            return snapshot
        with patch.object(instance, "cleanup", side_effect=while_cleanup), \
                patch.object(fixture, "read_only_shared_snapshot", side_effect=while_preservation) as reader, \
                contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(42, instance.execute())
        reader.assert_called_once_with(instance.shared_comparison)
        self.assertFalse(Path(instance.scratch).exists())
        self.assertEqual(0, self.admission_probe(self.temporary.name, self.temporary.name).returncode)

    def test_guard_admission_is_held_through_failure_cleanup_and_preservation(self):
        commands = self.guard_commands(failed=True)
        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs)
        snapshot = self.shared_snapshot()
        snapshot["scheduler"]["revision"] = "1"
        cleanup = instance.cleanup
        def while_cleanup():
            self.assertEqual(7, self.admission_probe(self.temporary.name, self.temporary.name).returncode)
            return cleanup()
        def while_preservation(comparison):
            self.assertEqual(7, self.admission_probe(self.temporary.name, self.temporary.name).returncode)
            return snapshot
        executable = fixture.APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
        original_is_file = Path.is_file
        with patch.object(guards, "IsolatedFixture", return_value=instance), \
                patch.object(guards, "read_only_shared_snapshot", side_effect=while_preservation) as reader, \
                patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                patch.object(instance, "cleanup", side_effect=while_cleanup), \
                contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(1, guards.run_guards())
        self.assertEqual([((instance.shared_comparison,), {})] * 3, reader.call_args_list)
        self.assertFalse(Path(instance.scratch).exists())
        self.assertEqual(0, self.admission_probe(self.temporary.name, self.temporary.name).returncode)

    def run_fixture(self, commands, port_check=lambda: None, owned=None):
        instance = fixture.IsolatedFixture(command=commands, port_check=port_check,
            log_directory=self.logs, ownership_check=owned or (lambda: commands.started and not commands.stopped))
        with contextlib.redirect_stdout(io.StringIO()):
            result = instance.execute()
        if instance.scratch:
            self.addCleanup(lambda: shutil.rmtree(instance.scratch) if Path(instance.scratch).exists() else None)
        return instance, result

    @staticmethod
    def gateway_identities():
        parent = {"pid": 999999996, "start": "owner"}
        return [{"pid": 999999997, "start": "1", "parent_identity": parent},
                {"pid": 999999998, "start": "2", "parent_identity": parent}]

    @staticmethod
    def guard_commands(failed=False, package=False):
        commands = Commands()
        def probe(arguments):
            names = [argument.split("=", 1)[1] for argument in arguments if argument.startswith("--guard-probe=")]
            if not names:
                return None
            name = names[0]
            if failed:
                return subprocess.CompletedProcess(arguments, 42, "Guard failed", "")
            if name in ("full-construction", "security-construction"):
                return subprocess.CompletedProcess(arguments, 0,
                    'Timesheets construction model: {"gatewayCredentials": 1, "timesheetsCredentials": 1}\n'
                    + 'Timesheets gateway readiness model: ' + json.dumps(guards.expected_gateway_readiness(name == "security-construction")) + '\n'
                    + 'Timesheets security references: ' + json.dumps(guards.expected_security_references(name == "security-construction")), "")
            if name.startswith("security-"):
                return subprocess.CompletedProcess(arguments, 0 if name == "security-valid" else -6,
                    "Security selection: isolated smoke." if name == "security-valid" else "",
                    "Security smoke requires a validated isolated run-mode fixture.")
            if name.startswith("model-"):
                port = 18080 if name == "model-alternate" else 8080
                output = "Timesheets endpoint model: " + json.dumps([
                    {"name": "internal", "port": 8081, "isExternal": False, "isExplicitlyProxied": False},
                    {"name": "public", "port": port, "isExternal": False, "isExplicitlyProxied": False}])
                return subprocess.CompletedProcess(arguments, 0, output, "")
            if name.endswith("local-port") and name != "valid-local-port":
                message = ("Local public port overrides require run mode." if name.startswith("publish-")
                           else "Local public port is missing or unsafe.")
                return subprocess.CompletedProcess(arguments, -6, "", message)
            if package and "compiled" in name:
                return subprocess.CompletedProcess(arguments, -6, "",
                    "Compiled gateway launch requires a Debug/source AppHost build.")
            if name in ("valid-isolated", "ordinary-defaults", "valid-compiled-isolated", "valid-local-port",
                        "valid-component-without-diagnostic", "valid-component-with-misleading-diagnostic", "valid-trimmed-control"):
                message = "Dapr component selection: " + ("checked-in defaults" if name == "ordinary-defaults" else "isolated fixture")
                message += "\nGateway launch selection: " + ("compiled executable" if name == "valid-compiled-isolated" else "project")
                return subprocess.CompletedProcess(arguments, 0, message, "")
            message = "Isolated Dapr components are missing or unsafe."
            if name in ("default-control-collision", "default-trimmed-control-collision", "component-redis-collision",
                        "default-component-redis-collision", "misleading-component-redis-collision"):
                message = "Local public port is missing or unsafe."
            if name in ("missing-opt-in", "publish-fixture", "publish-bare-fixture"):
                message = "Isolated Dapr components require an explicit run-mode fixture opt-in."
            if name in ("bare-fixture-flag", "missing-components", "empty-components", "whitespace-components"):
                message = "Isolated Dapr fixture requires an explicit component directory."
            if name in ("missing-placement", "shared-placement", "missing-scheduler", "shared-scheduler", "duplicate-control-endpoint"):
                message = "Isolated Dapr placement and scheduler endpoints are missing or unsafe."
            if name.startswith("compiled-"):
                message = "Compiled gateway launch requires both executable and working directory."
            if name.startswith("ordinary-compiled") or name == "publish-compiled":
                message = "Compiled gateway launch requires a validated isolated run-mode fixture."
            if name == "missing-placement-compiled":
                message = "Isolated Dapr placement and scheduler endpoints are missing or unsafe."
            return subprocess.CompletedProcess(arguments, -6, "", message)
        commands.failure = probe
        return commands

    def test_live_commands_use_debug_sources_without_mutating_the_caller_environment(self):
        for inherited in ({}, {"Configuration": "Release", "UseHexalithProjectReferences": "false",
                               "UseNuGetDeps": "true"},
                          {"configuration": "Release", "CONFIGURATION": "Release",
                           "usehexalithprojectreferences": "false", "USEHEXALITHPROJECTREFERENCES": "false",
                           "usenugetdeps": "true", "USENUGETDEPS": "true"}):
            with self.subTest(inherited=inherited), patch.dict(os.environ, inherited, clear=True):
                caller = dict(os.environ)
                commands = Commands()
                instance, result = self.run_fixture(commands)
                self.assertEqual(0, result)
                self.assertEqual(caller, dict(os.environ))
                runtime_commands = [(arguments, kwargs["env"]) for arguments, kwargs in commands.calls
                                    if arguments[:2] in (["aspire", "start"], ["aspire", "wait"])
                                    or "-class" in arguments]
                self.assertEqual(4, len(runtime_commands))
                for arguments, environment in runtime_commands:
                    self.assertEqual({"Configuration", "UseHexalithProjectReferences", "UseNuGetDeps"},
                                     {key for key in environment if key.casefold() in
                                      ("configuration", "usehexalithprojectreferences", "usenugetdeps")})
                    self.assertEqual("Debug", environment["Configuration"], arguments)
                    self.assertEqual("true", environment["UseHexalithProjectReferences"], arguments)
                    self.assertEqual("false", environment["UseNuGetDeps"], arguments)
                    self.assertEqual("1", environment["TIMESHEETS_LIVE_DAPR_FIXTURE"], arguments)
                    self.assertEqual(instance.scratch, environment["TIMESHEETS_LIVE_DAPR_COMPONENTS"], arguments)
                self.assertTrue(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])

    def test_security_realm_contains_only_platform_generated_credentials_and_timesheets_claims(self):
        realm = json.loads((fixture.APPHOST.parent / "KeycloakRealms/hexalith-realm.json").read_text())
        user, = realm["users"]
        self.assertEqual("${HEXALITH_EVENTSTORE_CLIENT_USERNAME}", user["username"])
        self.assertEqual([{"type": "password", "value": "${HEXALITH_EVENTSTORE_CLIENT_PASSWORD}", "temporary": False}],
                         user["credentials"])
        self.assertEqual(["timesheets"], user["attributes"]["domains"])
        self.assertEqual({"tenant-a", "tenant-b", "system"}, set(user["attributes"]["tenants"]))
        self.assertEqual({"command:submit", "command:query", "command:replay"}, set(user["attributes"]["permissions"]))
        client = next(client for client in realm["clients"] if client["clientId"] == "hexalith-eventstore")
        self.assertTrue(client["directAccessGrantsEnabled"])
        self.assertTrue({"eventstore:tenant", "eventstore:domain", "eventstore:permission"} <= {
            mapper["config"].get("claim.name") for mapper in client["protocolMappers"]})

    def test_generated_realm_technical_account_has_a_complete_synthetic_verified_profile(self):
        realm = json.loads((fixture.APPHOST.parent / "KeycloakRealms/hexalith-realm.json").read_text())
        user, = realm["users"]
        for field in ("firstName", "lastName"):
            self.assertIsInstance(user[field], str)
            self.assertEqual(user[field].strip(), user[field])
            self.assertTrue(user[field])
            self.assertTrue(any(marker in user[field].casefold() for marker in ("synthetic", "fixture")))
            self.assertNotIn("${", user[field])
        self.assertRegex(user["email"], r"^[a-z0-9][a-z0-9._+-]*@[a-z0-9.-]+\.invalid$")
        self.assertIn("fixture", user["email"].split("@", 1)[0])
        self.assertIs(user["emailVerified"], True)
        self.assertIs(user["enabled"], True)
        self.assertEqual([], user["requiredActions"])
        self.assertEqual("${HEXALITH_EVENTSTORE_CLIENT_USERNAME}", user["username"])
        self.assertEqual("${HEXALITH_EVENTSTORE_CLIENT_PASSWORD}", user["credentials"][0]["value"])
        self.assertIs(user["credentials"][0]["temporary"], False)

    def test_security_reference_driver_refuses_missing_duplicate_non_secret_and_wrong_bindings(self):
        executable = fixture.APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
        original_is_file = Path.is_file
        for defect in ("missing", "duplicate", "non-secret", "wrong-resource", "wrong-parameter", "not-generated"):
            with self.subTest(defect=defect):
                commands = self.guard_commands()
                original = commands.failure
                def mutation(arguments):
                    result = original(arguments)
                    if "--guard-probe=security-construction" in arguments:
                        bindings = guards.expected_security_references(True)
                        if defect == "missing":
                            bindings.pop()
                        elif defect == "duplicate":
                            bindings.append(dict(bindings[-1]))
                        elif defect == "non-secret":
                            bindings[-1]["secret"] = False
                        elif defect == "not-generated":
                            bindings[-1]["generated"] = False
                        else:
                            bindings[-1]["resource" if defect == "wrong-resource" else "parameter"] = "other"
                        result.stdout = result.stdout.split("Timesheets security references: ")[0] + "Timesheets security references: " + json.dumps(bindings)
                    return result
                commands.failure = mutation
                instance = fixture.IsolatedFixture(command=commands, log_directory=self.logs)
                with patch.object(guards, "IsolatedFixture", return_value=instance), \
                        patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(1, guards.run_guards(configuration_only=True))
                self.assertFalse(commands.started)
                self.assertFalse(commands.containers)

    def test_security_smoke_runs_only_after_admission_and_requires_current_source_and_readiness(self):
        for defect in (None, "foreign", "readiness", "credential-handoff", "source-changed"):
            with self.subTest(defect=defect):
                def failure(arguments):
                    if defect == "foreign" and arguments[:2] == ["dapr", "list"]:
                        return subprocess.CompletedProcess(arguments, 0, '[{"appId":"foreign"}]', "")
                    if defect == "readiness" and arguments[:3] == ["aspire", "wait", "security"]:
                        return subprocess.CompletedProcess(arguments, 18, "", "not ready")
                commands = Commands(failure)
                instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs,
                    security_smoke=True, ownership_check=lambda: commands.started and not commands.stopped)
                sources = [{"head": "current"}, {"head": "changed" if defect == "source-changed" else "current"}]
                with patch.object(instance, "capture_source_identity", side_effect=sources), \
                        patch.object(instance, "verify_security_runtime", side_effect=fixture.FixtureError("handoff failed")
                                     if defect == "credential-handoff" else None) as verification, \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(0 if defect is None else 1 if defect != "readiness" else 18, instance.execute())
                if defect == "foreign":
                    self.assertFalse(commands.started)
                    self.assertFalse(commands.redis_attempted)
                    self.assertFalse(any(args[:2] == ["dotnet", "build"] for args, _ in commands.calls))
                    self.assertFalse(json.loads((instance.log_directory / "admission.json").read_text())["admitted"])
                    verification.assert_not_called()
                else:
                    start = next(index for index, (args, _) in enumerate(commands.calls) if args[:2] == ["aspire", "start"])
                    build = next(index for index, (args, _) in enumerate(commands.calls) if args[:2] == ["dotnet", "build"])
                    self.assertLess(build, start)
                    self.assertEqual(["dotnet", "build", str(fixture.ROOT / "Hexalith.Timesheets.slnx"), "--configuration", "Debug",
                        "--no-restore", "-p:UseHexalithProjectReferences=true", "-warnaserror", "-m:1", "/nr:false"],
                        commands.calls[build][0])
                    self.assertEqual("1", commands.calls[start][1]["env"][fixture.SECURITY_SMOKE_SETTING])
                    self.assertEqual("security", next(args[2] for args, _ in commands.calls if args[:2] == ["aspire", "wait"]))
                    self.assertTrue(commands.stopped)
                self.assertFalse(commands.containers)

    def test_security_handoff_uses_generated_credentials_in_memory_and_refuses_unresolved_or_wrong_claims(self):
        username, password = fixture.secrets.token_hex(12), fixture.secrets.token_hex(24)
        container = {"Id": "f" * 64, "Config": {"Env": ["HEXALITH_EVENTSTORE_CLIENT_USERNAME=" + username,
                                       "HEXALITH_EVENTSTORE_CLIENT_PASSWORD=" + password]},
                     "NetworkSettings": {"Ports": {"8080/tcp": [{"HostIp": "127.0.0.1", "HostPort": "18180"}]}}}
        claims = {"iss": "http://127.0.0.1:18180/realms/hexalith", "preferred_username": username,
                  "aud": "hexalith-eventstore", "eventstore:tenant": ["tenant-a", "tenant-b", "system"],
                  "eventstore:domain": ["timesheets"], "eventstore:permission": ["command:submit", "command:query", "command:replay"]}
        for defect in (None, "placeholder", "claims", "public", "request-failure"):
            with self.subTest(defect=defect):
                selected = json.loads(json.dumps(container))
                token_claims = dict(claims)
                if defect == "placeholder":
                    selected["Config"]["Env"][-1] = "HEXALITH_EVENTSTORE_CLIENT_PASSWORD=${UNRESOLVED}"
                if defect == "claims":
                    token_claims["eventstore:domain"] = ["another-domain"]
                if defect == "public":
                    selected["NetworkSettings"]["Ports"]["8080/tcp"][0]["HostIp"] = "0.0.0.0"
                token = "header." + fixture.base64.urlsafe_b64encode(json.dumps(token_claims).encode()).decode().rstrip("=") + ".signature"
                with patch.object(fixture, "owned_http_open", side_effect=OSError(password)
                                  if defect == "request-failure" else None,
                                  return_value=io.BytesIO(json.dumps({"access_token": token}).encode())) as request:
                    if defect is None:
                        result = fixture.security_credential_handoff(selected, 8080, describe_command=self.security_describe_command(selected))
                        self.assertTrue(result["generated_credentials_accepted"])
                        self.assertNotIn(password, json.dumps(result))
                        self.assertIn(password.encode(), request.call_args.args[0].data)
                    else:
                        with self.assertRaises(fixture.FixtureError) as failure:
                            fixture.security_credential_handoff(selected, 8080, describe_command=self.security_describe_command(selected))
                        self.assertNotIn(password, str(failure.exception))
                        if defect in ("placeholder", "public"):
                            request.assert_not_called()

    def credential_handoff_fixture(self):
        username, password = fixture.secrets.token_hex(12), fixture.secrets.token_hex(24)
        container = {"Id": "f" * 64, "Config": {"Env": ["HEXALITH_EVENTSTORE_CLIENT_USERNAME=" + username,
            "HEXALITH_EVENTSTORE_CLIENT_PASSWORD=" + password], "Labels": {"hexalith.timesheets.components": "owned"}},
            "State": {"Running": True}, "NetworkSettings": {"Ports": {
                "8080/tcp": [{"HostIp": "127.0.0.1", "HostPort": "18180"}]}}}
        claims = {"iss": "http://127.0.0.1:18180/realms/hexalith", "preferred_username": username,
            "aud": "hexalith-eventstore", "eventstore:tenant": ["tenant-a", "tenant-b", "system"],
            "eventstore:domain": ["timesheets"], "eventstore:permission": ["command:submit", "command:query", "command:replay"]}
        return container, claims, username, password

    def security_describe_command(self, container, published_port=18180, snapshot=None):
        description = snapshot if snapshot is not None else {"resources": [{"name": "security-resource",
            "displayName": "security", "resourceType": "Container", "properties": {"container.id": container["Id"]},
            "urls": [{"name": "http", "url": "http://127.0.0.1:" + str(published_port)}]}]}
        def command(arguments, **_options):
            self.assertEqual(["aspire", "describe", "security", "--apphost", str(fixture.APPHOST),
                              "--non-interactive", "--format", "Json"], arguments)
            return subprocess.CompletedProcess(arguments, 0, json.dumps(description), "")
        return command

    def test_handoff_discovers_nondefault_container_and_published_http_ports_for_both_callers(self):
        for scheme, target, published, host in (("http", 8180, 28180, "localhost"), ("http", 19080, 48180, "127.0.0.1"),
                ("http", 8080, 18180, "127.0.0.1"), ("https", 8443, 28180, "localhost"), ("https", 19443, 48180, "127.0.0.1")):
            with self.subTest(scheme=scheme, target=target, published=published, host=host):
                container, claims, username, password = self.credential_handoff_fixture()
                private_value = fixture.secrets.token_hex(24)
                container["NetworkSettings"]["Ports"] = {str(target) + "/tcp": [{"HostIp": "127.0.0.1", "HostPort": str(published)}],
                    "9000/tcp": [{"HostIp": "127.0.0.1", "HostPort": str(published + 1)}]}
                origin = scheme + "://" + host + ":" + str(published)
                claims["iss"] = origin + "/realms/hexalith"
                description = {"resources": [{"name": "security-native", "displayName": "security", "resourceType": "Container",
                    "properties": {"container.id": container["Id"], "arbitrary": private_value}, "environment": {"PRIVATE": private_value},
                    "dashboardUrl": "http://localhost:18888/login?t=" + private_value,
                    "urls": [{"name": "management", "url": "http://localhost:" + str(published + 1)},
                             {"name": "http", "url": "http://security:" + str(target), "isInternal": True},
                             {"name": "http", "url": origin}]}]}
                token = "header." + fixture.base64.urlsafe_b64encode(json.dumps(claims).encode()).decode().rstrip("=") + ".signature"
                observed = []
                for injected in (True, False):
                    report = {}
                    command = self.security_describe_command(container, snapshot=description)
                    completed = command(["aspire", "describe", "security", "--apphost", str(fixture.APPHOST),
                        "--non-interactive", "--format", "Json"])
                    with patch.object(fixture.subprocess, "run", return_value=completed) as default_reader, \
                            patch.object(fixture, "owned_http_open", return_value=io.BytesIO(json.dumps({"access_token": token}).encode())) as request:
                        result = fixture.security_credential_handoff(container, 8080, report,
                            describe_command=command if injected else None)
                    self.assertTrue(result["generated_credentials_accepted"])
                    self.assertEqual(origin + "/realms/hexalith/protocol/openid-connect/token", request.call_args.args[0].full_url)
                    self.assertEqual(target, report["http_endpoint"]["container_port"])
                    self.assertEqual(published, report["http_endpoint"]["published_port"])
                    self.assertEqual(container["Id"], report["http_endpoint"]["container_id"])
                    self.assertEqual(scheme, report["http_endpoint"]["scheme"])
                    self.assertTrue(report["url_checks"]["scheme_allowed"])
                    if scheme == "https":
                        context = request.call_args.kwargs["context"]
                        self.assertTrue(context.check_hostname)
                        self.assertEqual(fixture.ssl.CERT_REQUIRED, context.verify_mode)
                    if injected:
                        default_reader.assert_not_called()
                    else:
                        self.assertEqual(completed.args, default_reader.call_args.args[0])
                    observed.append(report["http_endpoint"])
                    for sensitive in (private_value, username, password, token):
                        self.assertNotIn(sensitive, json.dumps(report))
                self.assertEqual(observed[0], observed[1])

    def test_handoff_refuses_wrong_container_nonloopback_ambiguous_and_unmatched_discovered_endpoints(self):
        for defect in ("wrong-container", "wrong-resource", "ambiguous-resource", "nonloopback-url", "nonloopback-binding",
                       "ambiguous-http", "internal-only", "unmatched-port", "ambiguous-binding", "udp-binding", "query", "userinfo",
                       "malformed-json", "describe-failed", "describe-timeout"):
            with self.subTest(defect=defect):
                container, _, username, password = self.credential_handoff_fixture()
                private_value = fixture.secrets.token_hex(24)
                description = {"resources": [{"name": "security", "resourceType": "Container",
                    "properties": {"container.id": container["Id"], "private": private_value}, "environment": {"private": private_value},
                    "urls": [{"name": "http", "url": "http://127.0.0.1:18180"}]}]}
                resource = description["resources"][0]
                if defect == "wrong-container":
                    resource["properties"]["container.id"] = "e" * 64
                elif defect == "wrong-resource":
                    resource["name"] = "another-resource"
                elif defect == "ambiguous-resource":
                    description["resources"].append(dict(resource))
                elif defect == "nonloopback-url":
                    resource["urls"][0]["url"] = "http://example.invalid:18180"
                elif defect == "nonloopback-binding":
                    container["NetworkSettings"]["Ports"]["8080/tcp"][0]["HostIp"] = "0.0.0.0"
                elif defect == "ambiguous-http":
                    resource["urls"].append(dict(resource["urls"][0]))
                elif defect == "internal-only":
                    resource["urls"][0]["isInternal"] = True
                elif defect == "unmatched-port":
                    resource["urls"][0]["url"] = "http://127.0.0.1:28180"
                elif defect == "ambiguous-binding":
                    container["NetworkSettings"]["Ports"]["8180/tcp"] = list(container["NetworkSettings"]["Ports"]["8080/tcp"])
                elif defect == "udp-binding":
                    container["NetworkSettings"]["Ports"]["8080/udp"] = container["NetworkSettings"]["Ports"].pop("8080/tcp")
                elif defect == "query":
                    resource["urls"][0]["url"] += "?secret=" + private_value
                elif defect == "userinfo":
                    resource["urls"][0]["url"] = "http://user:" + private_value + "@127.0.0.1:18180"
                def command(arguments, **_options):
                    if defect == "describe-timeout":
                        raise subprocess.TimeoutExpired(arguments, 30, output=private_value, stderr=private_value)
                    return subprocess.CompletedProcess(arguments, 1 if defect == "describe-failed" else 0,
                        private_value if defect == "malformed-json" else json.dumps(description), private_value)
                report = {}
                with patch.object(fixture, "owned_http_open") as request, self.assertRaises(fixture.FixtureError) as failure:
                    fixture.security_credential_handoff(container, 8080, report, describe_command=command)
                request.assert_not_called()
                self.assertEqual("port", report["stage"])
                self.assertEqual("port_shape_invalid", report["error_category"])
                for sensitive in (private_value, username, password):
                    self.assertNotIn(sensitive, json.dumps(report) + str(failure.exception))

    def test_security_url_predicates_are_allowlisted_and_preserve_every_existing_url_guard(self):
        container, _, username, password = self.credential_handoff_fixture()
        private_value = fixture.secrets.token_hex(24)
        cases = [
            ("http://127.0.0.1:18180", True, {"scheme": "http", "hostname_category": "ipv4-loopback", "bounded_port": 18180}),
            ("https://localhost:18180", True, {"scheme": "https", "http_scheme_matches": False, "scheme_allowed": True}),
            ("unknown" + private_value + "://localhost:18180", False, {"scheme": "other", "scheme_allowed": False}),
            ("ftp://localhost:18180", False, {"scheme": "other", "scheme_allowed": False}),
            ("https://localhost:18180?q=" + private_value, False, {"no_query": False}),
            ("https://user:" + private_value + "@localhost:18180", False, {"no_userinfo": False}),
            ("https://" + private_value + ".invalid:18180", False, {"hostname_category": "other", "hostname_allowed": False}),
            ("http://" + private_value + ".invalid:18180", False, {"hostname_category": "other", "hostname_allowed": False}),
            ("http://[::1]:18180", False, {"hostname_category": "ipv6-loopback", "hostname_allowed": False}),
            ("http://0.0.0.0:18180", False, {"hostname_category": "wildcard", "hostname_allowed": False}),
            ("http://localhost:18180/" + private_value, False, {"path_empty_or_slash": False}),
            ("http://localhost:18180?q=" + private_value, False, {"no_query": False}),
            ("http://localhost:18180#" + private_value, False, {"no_fragment": False}),
            ("http://user:" + private_value + "@localhost:18180", False, {"no_userinfo": False}),
            ("http://localhost", False, {"port_parse_valid": True, "port_present": False, "bounded_port": None}),
            ("http://localhost:70000", False, {"port_parse_valid": False, "bounded_port": None}),
            ("http://localhost:" + private_value, False, {"port_parse_valid": False, "bounded_port": None}),
            ("http://localhost:80", False, {"bounded_port": 80, "port_in_allowed_range": False}),
            ("http://localhost:8080", False, {"reserved_port_match": True, "public_port_match": True}),
            ("http://localhost:8081", False, {"reserved_port_match": True, "public_port_match": False}),
            ("http://[" + private_value, False, {"parse_succeeded": False}),
            ({"private": private_value}, False, {"url_string": False, "parse_succeeded": False}),
        ]
        for index, (url, accepted, expected) in enumerate(cases):
            with self.subTest(case=index):
                description = {"resources": [{"name": "security", "resourceType": "Container",
                    "properties": {"container.id": container["Id"]}, "urls": [{"name": "http", "url": url}]}]}
                report = {}
                command = self.security_describe_command(container, snapshot=description)
                if accepted:
                    fixture.discover_security_http_endpoint(container, 8080, command=command, diagnostics=report)
                else:
                    with self.assertRaises(fixture.FixtureError):
                        fixture.discover_security_http_endpoint(container, 8080, command=command, diagnostics=report)
                checks = report["url_checks"]
                for key, value in expected.items():
                    self.assertEqual(value, checks[key], key)
                self.assertIn(checks["scheme"], ("unavailable", "http", "https", "other", "absent"))
                self.assertIn(checks["hostname_category"], ("unavailable", "localhost", "ipv4-loopback", "ipv6-loopback", "wildcard", "other", "absent"))
                if checks["bounded_port"] is not None:
                    self.assertTrue(1 <= checks["bounded_port"] <= 65535)
                for sensitive in (private_value, username, password):
                    self.assertNotIn(sensitive, json.dumps(report))

        description["resources"][0]["urls"] = [{"name": "http"}]
        report = {}
        with self.assertRaises(fixture.FixtureError):
            fixture.discover_security_http_endpoint(container, 8080,
                command=self.security_describe_command(container, snapshot=description), diagnostics=report)
        self.assertFalse(report["url_checks"]["url_string"])
        selected_public = fixture.security_http_url_diagnostics("http://localhost:18080", 18080)
        self.assertTrue(selected_public["public_port_match"])
        self.assertFalse(selected_public["reserved_port_match"])

    def test_https_credentials_fail_closed_on_tls_verification_without_downgrade_or_raw_error(self):
        container, _, username, password = self.credential_handoff_fixture()
        private_value = fixture.secrets.token_hex(24)
        description = {"resources": [{"name": "security", "resourceType": "Container",
            "properties": {"container.id": container["Id"]}, "urls": [{"name": "http", "url": "https://localhost:18180"}]}]}
        for wrapped in (False, True):
            for certificate_failure in (False, True):
                with self.subTest(wrapped=wrapped, certificate_failure=certificate_failure):
                    reason = fixture.ssl.SSLCertVerificationError(private_value) if certificate_failure else fixture.ssl.SSLError(private_value)
                    error = fixture.urllib.error.URLError(reason) if wrapped else reason
                    report = {}
                    with patch.object(fixture.ssl, "_create_unverified_context", side_effect=AssertionError("Unverified TLS is forbidden")), \
                            patch.object(fixture, "owned_http_open", side_effect=error) as request, \
                            self.assertRaises(fixture.FixtureError) as failure:
                        fixture.security_credential_handoff(container, 8080, report,
                            describe_command=self.security_describe_command(container, snapshot=description))
                    request.assert_called_once()
                    self.assertEqual("https://localhost:18180/realms/hexalith/protocol/openid-connect/token", request.call_args.args[0].full_url)
                    context = request.call_args.kwargs["context"]
                    self.assertTrue(context.check_hostname)
                    self.assertEqual(fixture.ssl.CERT_REQUIRED, context.verify_mode)
                    self.assertEqual("tls_verification_failed" if certificate_failure else "tls_handshake_failed", report["error_category"])
                    self.assertEqual("token_http", report["stage"])
                    for sensitive in (private_value, username, password):
                        self.assertNotIn(sensitive, json.dumps(report) + str(failure.exception))

    def test_security_runtime_retains_only_allowlisted_endpoint_metadata_from_describe(self):
        container, claims, username, password = self.credential_handoff_fixture()
        private_value = fixture.secrets.token_hex(24)
        container["NetworkSettings"]["Ports"] = {"8180/tcp": [{"HostIp": "127.0.0.1", "HostPort": "18180"}]}
        claims["iss"] = "http://localhost:18180/realms/hexalith"
        description = {"resources": [{"name": "security", "resourceType": "Container",
            "properties": {"container.id": container["Id"], "arbitrary": private_value}, "environment": {"PRIVATE": private_value},
            "dashboardUrl": "http://localhost/login?t=" + private_value, "urls": [{"name": "http", "url": "http://localhost:18180"}]}]}
        token = "header." + fixture.base64.urlsafe_b64encode(json.dumps(claims).encode()).decode().rstrip("=") + ".signature"
        def command(arguments):
            if arguments[:3] == ["aspire", "describe", "security"]:
                return self.security_describe_command(container, snapshot=description)(arguments)
            if arguments[:3] == ["docker", "container", "ls"]:
                return subprocess.CompletedProcess(arguments, 0, container["Id"], "")
            if arguments == ["docker", "inspect", container["Id"]]:
                return subprocess.CompletedProcess(arguments, 0, json.dumps([container]), "")
        instance = fixture.IsolatedFixture(command=Commands(command), log_directory=self.logs, resource_check=lambda *_: [])
        instance.scratch = "owned"
        with patch.object(fixture, "runtime_host_identity", return_value={"dashboard": {}}), \
                patch.object(fixture, "verify_dashboard_ready", return_value={"ready": True}), \
                patch.object(fixture, "owned_http_open", return_value=io.BytesIO(json.dumps({"access_token": token}).encode())):
            instance.verify_security_runtime()
        receipt = json.loads((instance.log_directory / "security-runtime.json").read_text())
        self.assertEqual(8180, receipt["http_endpoint"]["container_port"])
        self.assertEqual("localhost", receipt["http_endpoint"]["host"])
        self.assertFalse(any("describe" in path.name for path in instance.log_directory.iterdir()))
        retained = "".join(path.read_text() for path in instance.log_directory.iterdir())
        for sensitive in (private_value, username, password, token):
            self.assertNotIn(sensitive, retained)

    def test_handoff_http_diagnostics_allowlist_status_oauth_code_and_known_profile_category(self):
        container, _, username, password = self.credential_handoff_fixture()
        private_value = fixture.secrets.token_hex(24)
        cases = [
            (400, {"error": "invalid_grant", "error_description": "Account is not fully set up"}, "invalid_grant", "profile_setup_required"),
            (401, {"error": "invalid_client", "error_description": private_value}, "invalid_client", "client_authentication_failed"),
            (400, {"error": "invalid_grant", "error_description": private_value}, "invalid_grant", "oauth_rejection"),
            (503, {"error": "temporarily_unavailable", "error_description": private_value}, "temporarily_unavailable", "token_endpoint_unavailable"),
            (403, {"error": private_value, "error_description": private_value}, None, "http_rejection"),
            (private_value, {"error": private_value}, None, "http_rejection"),
            (400, private_value, None, "http_rejection"),
            (400, {"error": "invalid_grant", "error_description": "Account is not fully set up" + private_value}, "invalid_grant", "oauth_rejection"),
            (400, " " * 65537, None, "http_rejection"),
        ]
        for status, payload, code, category in cases:
            with self.subTest(status_type=type(status).__name__, code=code, category=category):
                data = payload.encode() if isinstance(payload, str) else json.dumps(payload).encode()
                error = fixture.urllib.error.HTTPError("http://private/" + private_value, status, private_value,
                    {"private-header": private_value}, io.BytesIO(data))
                report = {}
                with patch.object(fixture, "owned_http_open", side_effect=error), \
                        self.assertRaises(fixture.FixtureError) as failure:
                    fixture.security_credential_handoff(container, 8080, report, describe_command=self.security_describe_command(container))
                self.assertEqual("token_http", report["stage"])
                self.assertEqual(code, report["oauth_error_code"])
                self.assertEqual(category, report["error_category"])
                self.assertEqual(status if isinstance(status, int) else None, report["http_status"])
                self.assertTrue(error.fp.closed)
                retained = json.dumps(report) + str(failure.exception)
                for sensitive in (private_value, username, password):
                    self.assertNotIn(sensitive, retained)
                self.assertNotIn("error_description", retained)

    def test_handoff_diagnostics_distinguish_environment_ports_token_shapes_and_each_claim_without_values(self):
        container, claims, username, password = self.credential_handoff_fixture()
        private_value = fixture.secrets.token_hex(24)
        claim_labels = {"iss": "issuer_matches", "preferred_username": "username_matches", "aud": "audience_matches",
            "eventstore:tenant": "tenant_claims_match", "eventstore:domain": "domain_claims_match",
            "eventstore:permission": "permission_claims_match"}
        for defect in ("environment", "port", "transport", "invalid-json", "missing-token", "non-string-token",
                       "invalid-token", "non-object-claims", *claim_labels):
            with self.subTest(defect=defect):
                selected, token_claims = json.loads(json.dumps(container)), dict(claims)
                if defect == "environment":
                    selected["Config"]["Env"] = []
                if defect == "port":
                    selected["NetworkSettings"]["Ports"]["8080/tcp"][0]["HostIp"] = "0.0.0.0"
                if defect in claim_labels:
                    token_claims[defect] = private_value
                if defect == "non-object-claims":
                    token_claims = [private_value]
                token = "header." + fixture.base64.urlsafe_b64encode(json.dumps(token_claims).encode()).decode().rstrip("=") + ".signature"
                response = {"access_token": token}
                if defect == "missing-token":
                    response = {"private": private_value}
                if defect == "non-string-token":
                    response = {"access_token": {"private": private_value}}
                if defect == "invalid-token":
                    response = {"access_token": private_value}
                body = private_value.encode() if defect == "invalid-json" else json.dumps(response).encode()
                report = {}
                with patch.object(fixture, "owned_http_open", return_value=io.BytesIO(body),
                                  side_effect=OSError(private_value) if defect == "transport" else None) as request, \
                        self.assertRaises(fixture.FixtureError) as failure:
                    fixture.security_credential_handoff(selected, 8080, report, describe_command=self.security_describe_command(selected))
                expected_stage = ("environment" if defect == "environment" else "port" if defect == "port" else
                                  "token_http" if defect == "transport" else "claims" if defect in claim_labels else "token_shape")
                self.assertEqual(expected_stage, report["stage"])
                if defect in claim_labels:
                    self.assertFalse(report["claim_matches"][claim_labels[defect]])
                    self.assertEqual(set(claim_labels.values()), set(report["claim_matches"]))
                    self.assertTrue(all(isinstance(value, bool) for value in report["claim_matches"].values()))
                    self.assertEqual(5, sum(report["claim_matches"].values()))
                if defect in ("environment", "port"):
                    request.assert_not_called()
                retained = json.dumps(report) + str(failure.exception)
                for sensitive in (private_value, username, password, token):
                    self.assertNotIn(sensitive, retained)

    def test_handoff_rejects_missing_token_segments_and_non_string_list_claim_shapes(self):
        container, claims, username, password = self.credential_handoff_fixture()
        private = fixture.secrets.token_hex(24)
        encode = lambda value: fixture.base64.urlsafe_b64encode(json.dumps(value).encode()).decode().rstrip("=")
        encoded = encode(claims)
        invalid_tokens = ("header." + encoded, "header." + encoded + ".signature.extra", "." + encoded + ".signature",
                          "header..signature", "header." + encoded + ".")
        cases = [("token", token, None) for token in invalid_tokens]
        labels = {"aud": "audience_matches", "eventstore:tenant": "tenant_claims_match",
                  "eventstore:domain": "domain_claims_match", "eventstore:permission": "permission_claims_match"}
        for name, label in labels.items():
            values = claims[name] if isinstance(claims[name], list) else [claims[name]]
            for invalid in ({value: private for value in values}, [*values, {"private": private}], [*values, True], None):
                cases.append((label, "header." + encode({**claims, name: invalid}) + ".signature", label))
        for label, token, claim_label in cases:
            report = {}
            with self.subTest(label=label), patch.object(fixture, "owned_http_open",
                    return_value=io.BytesIO(json.dumps({"access_token": token}).encode())), self.assertRaises(fixture.FixtureError) as failure:
                fixture.security_credential_handoff(container, 8080, report, describe_command=self.security_describe_command(container))
            self.assertEqual("claims" if claim_label else "token_shape", report["stage"])
            if claim_label:
                self.assertFalse(report["claim_matches"][claim_label])
            else:
                self.assertFalse(report["jwt_three_parts"])
            retained = json.dumps(report) + str(failure.exception)
            for sensitive in (private, token, username, password):
                self.assertFalse(sensitive in retained)
        for audience in ("hexalith-eventstore", ["hexalith-eventstore", "another-audience"]):
            token = "header." + encode({**claims, "aud": audience}) + ".signature"
            with self.subTest(audience_type=type(audience).__name__), patch.object(fixture, "owned_http_open",
                    return_value=io.BytesIO(json.dumps({"access_token": token}).encode())):
                self.assertTrue(fixture.security_credential_handoff(container, 8080,
                    describe_command=self.security_describe_command(container))["generated_credentials_accepted"])

    def test_security_runtime_retains_safe_handoff_failure_and_preserves_error_when_diagnostic_write_fails(self):
        container, _, username, password = self.credential_handoff_fixture()
        private_value = fixture.secrets.token_hex(24)
        def command(arguments):
            if arguments[:3] == ["docker", "container", "ls"]:
                return subprocess.CompletedProcess(arguments, 0, container["Id"], "")
            if arguments == ["docker", "inspect", container["Id"]]:
                return subprocess.CompletedProcess(arguments, 0, json.dumps([container]), "")
        for write_fails in (False, True):
            instance = fixture.IsolatedFixture(command=Commands(command), log_directory=self.logs, resource_check=lambda *_: [])
            instance.scratch = "owned"
            body = json.dumps({"error": "invalid_grant", "error_description": "Account is not fully set up", "private": private_value}).encode()
            error = fixture.urllib.error.HTTPError("http://private/" + private_value, 400, private_value, {}, io.BytesIO(body))
            write_text = Path.write_text
            def write_receipt(path, *args, **kwargs):
                if write_fails and path.name == "security-credential-failure.json":
                    raise OSError(private_value)
                return write_text(path, *args, **kwargs)
            with self.subTest(write_fails=write_fails), \
                    patch.object(fixture, "runtime_host_identity", return_value={"dashboard": {}}), \
                    patch.object(fixture, "verify_dashboard_ready", return_value={"ready": True}), \
                    patch.object(fixture, "owned_http_open", side_effect=error), \
                    patch.object(Path, "write_text", write_receipt):
                with contextlib.redirect_stdout(io.StringIO()), self.assertRaisesRegex(fixture.FixtureError,
                        "Generated security credentials or realm scope claims could not be verified"):
                    instance.verify_security_runtime()
            if write_fails:
                self.assertTrue(instance.evidence_write_failed)
            else:
                receipt = json.loads((instance.log_directory / "security-credential-failure.json").read_text())
                self.assertEqual("profile_setup_required", receipt["error_category"])
                self.assertEqual(400, receipt["http_status"])
            for path in instance.log_directory.iterdir():
                for sensitive in (private_value, username, password):
                    self.assertNotIn(sensitive, path.read_text())

    def test_dashboard_readiness_requires_an_owned_loopback_listener(self):
        directory = Path(self.temporary.name) / "dashboard-proc"
        process = directory / "1"
        process.mkdir(parents=True)
        identity = {"pid": 1, "start": "current"}
        self.dashboard_socket_fixture(process, 18888)
        private = fixture.secrets.token_hex(24)
        for defect in (None, "changed-start", "wildcard", "missing", "unavailable", "path", "query", "fragment",
                       "empty-query", "empty-fragment", "userinfo", "empty-userinfo", "control"):
            with self.subTest(defect=defect):
                (process / "stat").write_text("1 (dashboard) " + " ".join([
                    "S", "0", *(["0"] * 17), "changed" if defect == "changed-start" else "current"]))
                endpoint = {"wildcard": "http://0.0.0.0:18888", "path": "https://127.0.0.1:18888/" + private,
                    "query": "https://127.0.0.1:18888/?private=" + private, "fragment": "https://127.0.0.1:18888/#" + private,
                    "empty-query": "https://127.0.0.1:18888?", "empty-fragment": "https://127.0.0.1:18888#",
                    "userinfo": "https://" + private + "@127.0.0.1:18888", "empty-userinfo": "https://@127.0.0.1:18888",
                    "control": "https://127.0.0.1:18888\n"}.get(defect, "https://127.0.0.1:18888")
                (process / "environ").write_bytes(b"" if defect == "missing" else ("ASPNETCORE_URLS=" + endpoint).encode())
                response = MagicMock()
                response.__enter__.return_value.status = 200
                with patch.object(fixture, "owned_http_open", return_value=response,
                                  side_effect=OSError("unavailable") if defect == "unavailable" else None) as request:
                    if defect is None:
                        result = fixture.verify_dashboard_ready(identity, directory)
                        expected = {"ready": True, "scheme": "https", "loopback": True, "port": 18888, "path": "/health",
                                          "connect_host": "127.0.0.1", "ownership": {"pid": 1, "start": "current",
                                          "socket_inodes": ["123"], "loopback_hosts": ["127.0.0.1"]}}
                        self.assertEqual(expected, {key: result[key] for key in expected})
                        request.assert_called_once()
                        self.assertEqual("https://127.0.0.1:18888/health", request.call_args.args[0].full_url)
                    else:
                        with self.assertRaises(fixture.FixtureError):
                            fixture.verify_dashboard_ready(identity, directory)
                        if defect != "unavailable":
                            request.assert_not_called()

    def test_dashboard_readiness_uses_supported_direct_health_route_even_when_browser_root_challenges(self):
        paths = []
        def dashboard(handler):
            paths.append(handler.path)
            handler.send_response(200 if handler.path == "/health" else 302)
            if handler.path != "/health":
                handler.send_header("Location", "/login")
            handler.end_headers()
            handler.wfile.write(b"Healthy")

        process = Path(self.temporary.name) / "healthy-dashboard-proc" / "1"
        process.mkdir(parents=True)
        (process / "stat").write_text("1 (dashboard) " + " ".join(["S", "0", *(["0"] * 17), "current"]))
        with self.owned_http_server(dashboard) as origin:
            self.dashboard_socket_fixture(process, fixture.urllib.parse.urlsplit(origin).port)
            try:
                fixture.owned_http_open(origin, timeout=2)
                self.fail("Browser root should challenge")
            except fixture.urllib.error.HTTPError as response:
                with response:
                    self.assertEqual(302, response.code)
            self.assertEqual(["/"], paths)
            paths.clear()
            for suffix in ("", "/"):
                with self.subTest(trailing_slash=bool(suffix)):
                    (process / "environ").write_bytes(("ASPNETCORE_URLS=" + origin + suffix).encode())
                    result = fixture.verify_dashboard_ready({"pid": 1, "start": "current"}, process.parent)
                    expected = {"ready": True, "scheme": "http", "loopback": True,
                        "port": fixture.urllib.parse.urlsplit(origin).port, "path": "/health", "connect_host": "127.0.0.1",
                        "ownership": {"pid": 1, "start": "current", "socket_inodes": ["123"],
                                      "loopback_hosts": ["127.0.0.1"]}}
                    self.assertEqual(expected, {key: result[key] for key in expected})
            self.assertEqual(["/health", "/health"], paths)

    def test_owned_dashboard_and_credentials_refuse_actual_redirects_without_leaking_response_or_credentials(self):
        private = fixture.secrets.token_hex(24)
        sink_hits = []
        def sink(handler):
            sink_hits.append(handler.command)
            handler.send_response(200)
            handler.end_headers()
            handler.wfile.write(b"{}")

        process = Path(self.temporary.name) / "redirect-dashboard-proc" / "1"
        process.mkdir(parents=True)
        (process / "stat").write_text("1 (dashboard) " + " ".join(["S", "0", *(["0"] * 17), "current"]))
        with self.owned_http_server(sink) as destination:
            for status in (302, 307):
                source_hits = []
                def redirect(handler):
                    source_hits.append((handler.command, handler.path))
                    handler.send_response(status, private)
                    handler.send_header("Location", destination + "/?private=" + private)
                    handler.end_headers()
                    handler.wfile.write(json.dumps({"error": "invalid_grant", "error_description": private}).encode())

                with self.subTest(status=status), self.owned_http_server(redirect) as origin:
                    self.dashboard_socket_fixture(process, fixture.urllib.parse.urlsplit(origin).port)
                    (process / "environ").write_bytes(("ASPNETCORE_URLS=" + origin).encode())
                    output = io.StringIO()
                    dashboard_diagnostic = {}
                    with contextlib.redirect_stdout(output), contextlib.redirect_stderr(output), \
                            self.assertRaises(fixture.FixtureError) as dashboard_failure:
                        fixture.verify_dashboard_ready({"pid": 1, "start": "current"}, process.parent,
                                                       diagnostic=dashboard_diagnostic)
                    container, _, username, password = self.credential_handoff_fixture()
                    published = fixture.urllib.parse.urlsplit(origin).port
                    container["NetworkSettings"]["Ports"]["8080/tcp"][0]["HostPort"] = str(published)
                    diagnostic = {}
                    with contextlib.redirect_stdout(output), contextlib.redirect_stderr(output), \
                            self.assertRaises(fixture.FixtureError) as credential_failure:
                        fixture.security_credential_handoff(container, 8080, diagnostic,
                            describe_command=self.security_describe_command(container, published_port=published))
                    self.assertEqual([("GET", "/health"), ("POST", "/realms/hexalith/protocol/openid-connect/token")], source_hits)
                    self.assertEqual([], sink_hits)
                    self.assertEqual("token_http", diagnostic["stage"])
                    self.assertEqual("invalid_grant", diagnostic["oauth_error_code"])
                    self.assertEqual("health_request", dashboard_diagnostic["stage"])
                    evidence = (json.dumps(diagnostic) + json.dumps(dashboard_diagnostic)
                        + str(dashboard_failure.exception) + str(credential_failure.exception) + output.getvalue())
                    for sensitive in (private, username, password, destination):
                        self.assertFalse(sensitive in evidence)

    def test_owned_http_opener_bypasses_poisoned_proxy_environment_without_consulting_it(self):
        owned_hits, proxy_hits = [], []
        def owned(handler):
            owned_hits.append(handler.command)
            handler.send_response(200)
            handler.end_headers()
            handler.wfile.write(b"owned")

        def proxy(handler):
            proxy_hits.append(handler.command)
            handler.send_response(502)
            handler.end_headers()

        with self.owned_http_server(owned) as origin, self.owned_http_server(proxy) as proxy_origin:
            poisoned = {name: proxy_origin for name in ("HTTP_PROXY", "http_proxy", "HTTPS_PROXY", "https_proxy", "ALL_PROXY", "all_proxy")}
            for bypass in ({}, {"NO_PROXY": "", "no_proxy": ""}):
                with self.subTest(bypass_present=bool(bypass)), patch.dict(os.environ, {**poisoned, **bypass}, clear=True), \
                        patch.object(fixture.urllib.request, "getproxies", side_effect=AssertionError("Proxy environment must not be consulted")):
                    with fixture.owned_http_open(origin, timeout=2) as response:
                        self.assertEqual(200, response.status)
                        self.assertEqual(b"owned", response.read())
        self.assertEqual(["GET", "GET"], owned_hits)
        self.assertEqual([], proxy_hits)

    def test_dashboard_socket_inventory_refuses_missing_foreign_ambiguous_and_changed_owners(self):
        process = Path(self.temporary.name) / "socket-proc" / "1"
        process.mkdir(parents=True)
        stat = "1 (dashboard) " + " ".join(["S", "0", *(["0"] * 17), "current"])
        (process / "stat").write_text(stat)
        (process / "environ").write_bytes(b"ASPNETCORE_URLS=http://127.0.0.1:18888")
        identity = {"pid": 1, "start": "current"}
        for defect in ("missing-table", "missing-listener", "foreign", "shared", "second-listener",
                       "mapped-listener", "wildcard", "changed-start", "changed-socket"):
            with self.subTest(defect=defect):
                for candidate in process.parent.glob("*/fd/*"):
                    candidate.unlink()
                (process / "stat").write_text(stat)
                self.dashboard_socket_fixture(process, 18888)
                if defect == "missing-table": (process.parent / "net/tcp6").unlink()
                if defect == "missing-listener": (process.parent / "net/tcp").write_text("header\n")
                if defect == "foreign":
                    (process / "fd/3").unlink()
                    self.dashboard_socket_fixture(process, 18888, owner="2")
                if defect == "shared": self.dashboard_socket_fixture(process, 18888, owner="2")
                if defect == "second-listener":
                    with (process.parent / "net/tcp").open("a") as table:
                        table.write("1: 0100007F:49C8 00000000:0000 0A 0 0 0 0 0 456\n")
                if defect == "mapped-listener":
                    with (process.parent / "net/tcp6").open("a") as table:
                        table.write("1: 0000000000000000FFFF00000100007F:49C8 00000000:0000 0A 0 0 0 0 0 456\n")
                if defect == "wildcard": self.dashboard_socket_fixture(process, 18888, address="00000000")
                response = MagicMock()
                response.__enter__.return_value.status = 200
                def request(*_args, **_kwargs):
                    if defect == "changed-start": (process / "stat").write_text(stat.replace("current", "changed"))
                    if defect == "changed-socket": self.dashboard_socket_fixture(process, 18888, inode="456")
                    return response
                diagnostic = {}
                with patch.object(fixture, "owned_http_open", side_effect=request) as opened, self.assertRaises(fixture.FixtureError):
                    fixture.verify_dashboard_ready(identity, process.parent, diagnostic=diagnostic)
                self.assertEqual(1 if defect.startswith("changed-") else 0, opened.call_count)
                self.assertFalse(diagnostic["ready"])
                self.assertEqual("after_health" if defect == "changed-start" else
                    "identity_comparison" if defect == "changed-socket" else "before_health", diagnostic["stage"])
                if defect == "changed-socket":
                    observations = diagnostic["ownership_observations"]
                    self.assertEqual(["123"], observations["before_health"]["socket_inodes"])
                    self.assertEqual(["456"], observations["after_health"]["socket_inodes"])

    def test_dashboard_observations_retain_inaccessible_holders_as_unknown(self):
        process = Path(self.temporary.name) / "inaccessible-dashboard-proc" / "1"
        process.mkdir(parents=True)
        (process / "stat").write_text("1 (dashboard) " + " ".join(["S", "0", *(["0"] * 17), "current"]))
        (process / "environ").write_bytes(b"ASPNETCORE_URLS=http://127.0.0.1:18888")
        self.dashboard_socket_fixture(process, 18888)
        self.dashboard_socket_fixture(process, 18888, owner="2")
        real_iterdir, real_readlink = Path.iterdir, os.readlink
        for inaccessible_directory in (True, False):
            def iterdir(path):
                if inaccessible_directory and path == process.parent / "2/fd":
                    raise PermissionError("private")
                return real_iterdir(path)
            def readlink(path):
                if not inaccessible_directory and Path(path) == process.parent / "2/fd/3":
                    raise PermissionError("private")
                return real_readlink(path)
            response = MagicMock()
            response.__enter__.return_value.status = 200
            with self.subTest(inaccessible_directory=inaccessible_directory), \
                    patch.object(Path, "iterdir", iterdir), patch.object(fixture.os, "readlink", readlink), \
                    patch.object(fixture, "owned_http_open", return_value=response):
                result = fixture.verify_dashboard_ready({"pid": 1, "start": "current"}, process.parent)
            visibility = result["visibility"]
            self.assertEqual("observable_process_descriptors", visibility["scope"])
            self.assertFalse(visibility["exclusivity_established"])
            self.assertFalse(visibility["unobserved_transfer_excluded"])
            field = "inaccessible_processes" if inaccessible_directory else "inaccessible_descriptors"
            self.assertEqual(2, visibility[field + "_observations"])
            for observation in result["ownership_observations"].values():
                self.assertEqual(1, observation["visibility"][field])
                self.assertFalse(observation["visibility"]["exclusivity_established"])

    def test_dashboard_listener_identity_is_stable_despite_unrelated_process_churn(self):
        process = Path(self.temporary.name) / "churning-dashboard-proc" / "1"
        process.mkdir(parents=True)
        (process / "stat").write_text("1 (dashboard) " + " ".join(["S", "0", *(["0"] * 17), "current"]))
        (process / "environ").write_bytes(b"ASPNETCORE_URLS=http://127.0.0.1:18888")
        self.dashboard_socket_fixture(process, 18888)
        response = MagicMock()
        response.__enter__.return_value.status = 200
        def request(*_args, **_kwargs):
            (process.parent / "2/fd").mkdir(parents=True)
            return response
        with patch.object(fixture, "owned_http_open", side_effect=request):
            result = fixture.verify_dashboard_ready({"pid": 1, "start": "current"}, process.parent)
        before, after = (result["ownership_observations"][name] for name in ("before_health", "after_health"))
        self.assertEqual(fixture.dashboard_listener_fingerprint(before), fixture.dashboard_listener_fingerprint(after))
        self.assertEqual(1, before["visibility"]["inspected_processes"])
        self.assertEqual(2, after["visibility"]["inspected_processes"])
        self.assertEqual(3, result["visibility"]["inspected_processes_observations"])

    def test_dashboard_descriptor_denial_does_not_hide_a_later_visible_foreign_holder(self):
        process = Path(self.temporary.name) / "partly-inaccessible-dashboard-proc" / "1"
        process.mkdir(parents=True)
        (process / "stat").write_text("1 (dashboard) " + " ".join(["S", "0", *(["0"] * 17), "current"]))
        (process / "environ").write_bytes(b"ASPNETCORE_URLS=http://127.0.0.1:18888")
        self.dashboard_socket_fixture(process, 18888)
        foreign = process.parent / "2/fd"
        foreign.mkdir(parents=True)
        denied, shared = foreign / "3", foreign / "4"
        denied.symlink_to("socket:[456]")
        shared.symlink_to("socket:[123]")
        reads, real_readlink, real_iterdir = [], os.readlink, Path.iterdir
        def readlink(path):
            reads.append(Path(path))
            if Path(path) == denied:
                raise PermissionError("private")
            return real_readlink(path)
        def iterdir(path):
            return iter((denied, shared)) if path == foreign else real_iterdir(path)
        with patch.object(fixture.os, "readlink", readlink), patch.object(Path, "iterdir", iterdir), \
                patch.object(fixture, "owned_http_open") as opened, self.assertRaises(fixture.FixtureError):
            fixture.verify_dashboard_ready({"pid": 1, "start": "current"}, process.parent)
        self.assertLess(reads.index(denied), reads.index(shared))
        opened.assert_not_called()

    def dashboard_process_handshake(self, process, expected, timeout=5):
        """Read a bounded, content-free child handshake without blocking on a partial line."""
        deadline, line = time.monotonic() + timeout, bytearray()
        while len(line) < 128:
            remaining = deadline - time.monotonic()
            if remaining <= 0 or not select.select([process.stdout], [], [], remaining)[0]:
                self.fail("Dashboard process handshake timed out")
            value = os.read(process.stdout.fileno(), 1)
            if not value:
                self.fail("Dashboard process ended before its handshake")
            line.extend(value)
            if value == b"\n":
                self.assertEqual(expected.encode(), line)
                return
        self.fail("Dashboard process handshake exceeded its bounds")

    def cleanup_dashboard_processes(self, processes, timeout=5):
        """Attempt termination, kill/reap on timeout, and close every owned child stream."""
        failures = []
        for process in processes:
            try:
                process.terminate()
            except OSError:
                # A failed polite termination still enters kill/reap below.
                pass
        for process in processes:
            try:
                try:
                    process.wait(timeout=timeout)
                except (subprocess.TimeoutExpired, OSError):
                    try:
                        process.kill()
                    except ProcessLookupError:
                        pass
                    process.wait(timeout=timeout)
            except (subprocess.TimeoutExpired, OSError):
                failures.append("Owned dashboard child could not be reaped")
            finally:
                for stream in (process.stdin, process.stdout, process.stderr):
                    if stream is not None:
                        try:
                            stream.close()
                        except OSError:
                            failures.append("Owned dashboard child stream could not be closed")
        self.assertFalse(failures, "; ".join(failures))

    @contextlib.contextmanager
    def real_dashboard_process(self, family=socket.AF_INET, advertised_origin=None, bind_host=None, advertise_host=None):
        host = bind_host or ("127.0.0.1" if family == socket.AF_INET else "::1")
        with socket.socket(family) as reservation:
            reservation.bind((host, 0))
            port = reservation.getsockname()[1]
        origin = (f"http://127.0.0.1:{port}" if family == socket.AF_INET or host == "::ffff:127.0.0.1"
                  else f"http://[{host}]:{port}")
        if advertise_host:
            advertised_origin = f"http://{advertise_host}:{port}"
        code = """import http.server, socket, sys
class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        self.send_response(200)
        self.end_headers()
        self.wfile.write(b'Healthy')
    def log_message(self, *args): pass
class Server(http.server.HTTPServer):
    address_family = int(sys.argv[1])
server = Server((sys.argv[2], int(sys.argv[3])), Handler)
print('ready', flush=True)
server.serve_forever()
"""
        process = subprocess.Popen([sys.executable, "-u", "-c", code, str(family), host, str(port)],
            env=dict(os.environ, ASPNETCORE_URLS=advertised_origin or origin), stdout=subprocess.PIPE,
            stderr=subprocess.PIPE, text=True)
        try:
            self.dashboard_process_handshake(process, "ready\n")
            fields = Path(f"/proc/{process.pid}/stat").read_text().rsplit(")", 1)[1].split()
            yield process, {"pid": process.pid, "start": fields[19]}, origin
        finally:
            self.cleanup_dashboard_processes([process])

    def test_dashboard_health_verifies_real_tcp_and_tcp6_kernel_socket_owners(self):
        for family, bind_host in ((socket.AF_INET, None), (socket.AF_INET6, None), (socket.AF_INET6, "::ffff:127.0.0.1")):
            with self.subTest(family=family, bind_host=bind_host), self.real_dashboard_process(family, bind_host=bind_host) as (_, identity, origin):
                result = fixture.verify_dashboard_ready(identity)
                self.assertTrue(result["ready"])
                self.assertEqual(identity["pid"], result["ownership"]["pid"])
                self.assertTrue(result["ownership"]["socket_inodes"])
                self.assertEqual(fixture.urllib.parse.urlsplit(origin).port, result["port"])
        with self.real_dashboard_process() as (_, foreign, origin):
            with self.real_dashboard_process(advertised_origin=origin) as (_, identity, _):
                with self.assertRaises(fixture.FixtureError):
                    fixture.verify_dashboard_ready(identity)
                self.assertTrue(fixture.verify_dashboard_ready(foreign)["ready"])

    @contextlib.contextmanager
    def real_listener_handoff_processes(self, *, handshake_timeout=5, stall_owner=False):
        """Synthetic processes inherit and transfer a real TCP listener using SCM_RIGHTS."""
        listener = socket.socket()
        listener.bind(("127.0.0.1", 0))
        listener.listen(5)
        origin = "http://127.0.0.1:" + str(listener.getsockname()[1])
        sender, receiver = socket.socketpair()
        common = """import array, http.server, socket, sys, threading
class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        self.send_response(200)
        self.end_headers()
        self.wfile.write(b'Healthy')
    def log_message(self, *args): pass
def serve(listener):
    server = http.server.HTTPServer(listener.getsockname(), Handler, bind_and_activate=False)
    server.socket.close()
    server.socket = listener
    threading.Thread(target=server.serve_forever, daemon=True).start()
    return server
"""
        owner_code = common + """
listener = socket.socket(fileno=int(sys.argv[1]))
transport = socket.socket(fileno=int(sys.argv[2]))
server = serve(listener)
print('ready', flush=True)
mode = sys.stdin.readline().strip()
transport.sendmsg([b'listener'], [(socket.SOL_SOCKET, socket.SCM_RIGHTS, array.array('i', [listener.fileno()]))])
if mode == 'transfer':
    server.shutdown()
    server.server_close()
print('sent', flush=True)
sys.stdin.read()
"""
        receiver_code = common + """
transport = socket.socket(fileno=int(sys.argv[1]))
print('waiting', flush=True)
_, controls, _, _ = transport.recvmsg(64, socket.CMSG_SPACE(array.array('i').itemsize))
fds = array.array('i')
fds.frombytes(controls[0][2])
listener = socket.socket(fileno=fds[0])
server = serve(listener)
print('received', flush=True)
sys.stdin.read()
"""
        processes = []
        try:
            environment = dict(os.environ, ASPNETCORE_URLS=origin)
            if stall_owner:
                owner_code = "import time; time.sleep(60)"
            owner = subprocess.Popen([sys.executable, "-u", "-c", owner_code, str(listener.fileno()), str(sender.fileno())],
                pass_fds=(listener.fileno(), sender.fileno()), env=environment, stdin=subprocess.PIPE,
                stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            processes.append(owner)
            recipient = subprocess.Popen([sys.executable, "-u", "-c", receiver_code, str(receiver.fileno())],
                pass_fds=(receiver.fileno(),), env=environment, stdin=subprocess.PIPE,
                stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            processes.append(recipient)
            listener.close()
            sender.close()
            receiver.close()
            self.dashboard_process_handshake(owner, "ready\n", handshake_timeout)
            self.dashboard_process_handshake(recipient, "waiting\n", handshake_timeout)
            identities = []
            for process in processes:
                fields = Path(f"/proc/{process.pid}/stat").read_text().rsplit(")", 1)[1].split()
                identities.append({"pid": process.pid, "start": fields[19]})
            yield owner, recipient, identities, origin
        finally:
            listener.close()
            sender.close()
            receiver.close()
            self.cleanup_dashboard_processes(processes)

    def test_dashboard_health_observes_real_inherited_listener_without_claiming_exclusivity(self):
        with self.real_listener_handoff_processes() as (_, _, identities, _):
            result = fixture.verify_dashboard_ready(identities[0])
            self.assertEqual(identities[0]["pid"], result["ownership"]["pid"])
            self.assertTrue(result["ownership"]["socket_inodes"])
            self.assertFalse(result["visibility"]["exclusivity_established"])

    def test_dashboard_health_refuses_real_shared_or_transferred_listener_owners(self):
        for mode in ("share", "transfer"):
            with self.subTest(mode=mode), self.real_listener_handoff_processes() as (owner, recipient, identities, _):
                before = fixture.verify_dashboard_ready(identities[0])
                owner.stdin.write(mode + "\n")
                owner.stdin.flush()
                self.dashboard_process_handshake(owner, "sent\n")
                self.dashboard_process_handshake(recipient, "received\n")
                with self.assertRaises(fixture.FixtureError):
                    fixture.verify_dashboard_ready(identities[0])
                if mode == "share":
                    with self.assertRaises(fixture.FixtureError):
                        fixture.verify_dashboard_ready(identities[1])
                else:
                    after = fixture.verify_dashboard_ready(identities[1])
                    self.assertEqual(before["ownership"]["socket_inodes"], after["ownership"]["socket_inodes"])
                    self.assertNotEqual(before["ownership"]["pid"], after["ownership"]["pid"])

    def test_dashboard_failure_receipts_retain_real_sharing_and_transfer_before_or_after_health(self):
        """Controlled Python SCM_RIGHTS lifecycles are not native Dashboard observations."""
        private = fixture.secrets.token_hex(24)
        for mode in ("share", "transfer"):
            for phase in ("before_health", "after_health"):
                with self.subTest(mode=mode, phase=phase), patch.dict(os.environ, {"PRIVATE_TEST_VALUE": private}), \
                        self.real_listener_handoff_processes() as (owner, recipient, identities, _):
                    before = fixture.verify_dashboard_ready(identities[0])
                    instance = fixture.IsolatedFixture(command=Commands(), log_directory=self.logs)
                    def handoff():
                        owner.stdin.write(mode + "\n")
                        owner.stdin.flush()
                        self.dashboard_process_handshake(owner, "sent\n")
                        self.dashboard_process_handshake(recipient, "received\n")
                    opened = fixture.owned_http_open
                    def request(*args, **kwargs):
                        response = opened(*args, **kwargs)
                        handoff()
                        return response
                    if phase == "before_health":
                        handoff()
                    output = io.StringIO()
                    with patch.object(fixture, "owned_http_open", side_effect=request) as health, \
                            contextlib.redirect_stdout(output), self.assertRaises(fixture.FixtureError) as refused:
                        instance.observe_dashboard_readiness(identities[0], "initial")
                    self.assertEqual("Owned dashboard HTTP readiness could not be verified", str(refused.exception))
                    self.assertEqual(int(phase == "after_health"), health.call_count)
                    receipt = (instance.log_directory / "dashboard-listener-initial-failure.json").read_text()
                    proof = json.loads(receipt)
                    diagnostic = proof["diagnostic"]
                    self.assertFalse(proof["verified"])
                    self.assertFalse(proof["fixture_qualification_complete"])
                    self.assertEqual(identities[0], diagnostic["expected_identity"])
                    self.assertEqual(phase, diagnostic["stage"])
                    failed = diagnostic["ownership_observations"][phase]
                    self.assertEqual("owner_match", failed["stage"])
                    self.assertFalse(failed["verified"])
                    self.assertEqual(before["ownership"]["socket_inodes"], failed["socket_inodes"])
                    expected_pids = sorted(row["pid"] for row in identities) if mode == "share" else [recipient.pid]
                    self.assertTrue(all(pids == expected_pids for pids in failed["socket_owners"].values()))
                    self.assertEqual([dict(row, identity_visibility="observed") for row in sorted(
                        identities if mode == "share" else identities[1:], key=lambda row: row["pid"])], failed["owner_identities"])
                    self.assertTrue(failed["visibility"]["descriptor_scan_completed"])
                    self.assertFalse(failed["visibility"]["exclusivity_established"])
                    self.assertFalse(failed["visibility"]["unobserved_transfer_excluded"])
                    if phase == "after_health":
                        self.assertTrue(diagnostic["ownership_observations"]["before_health"]["verified"])
                    self.assertNotIn(private, receipt + str(refused.exception) + output.getvalue())
                    self.assertLessEqual(diagnostic["observation_started_unix_ns"], diagnostic["observation_completed_unix_ns"])
                self.assertTrue(all(child.poll() is not None for child in (owner, recipient)))

    def test_dashboard_failure_evidence_retains_incomplete_and_partial_visibility(self):
        private = fixture.secrets.token_hex(24)
        process = Path(self.temporary.name) / "failure-visibility-proc" / "1"
        process.mkdir(parents=True)
        (process / "stat").write_text("1 (dashboard) " + " ".join(["S", "0", *(["0"] * 17), "12345"]))
        (process / "environ").write_bytes(("ASPNETCORE_URLS=http://127.0.0.1:18888\0PRIVATE=" + private).encode())
        self.dashboard_socket_fixture(process, 18888)
        self.dashboard_socket_fixture(process, 18888, owner="2")
        (process.parent / "2/stat").write_text("2 (holder) " + " ".join(["S", "0", *(["0"] * 17), "23456"]))
        real_iterdir, real_readlink, real_read_text = Path.iterdir, os.readlink, Path.read_text
        for defect in ("inaccessible_process", "inaccessible_descriptor", "disappeared_process", "disappeared_descriptor",
                       "partial_inventory", "partial_tables", "denied_then_visible", "inaccessible_owner_identity"):
            with self.subTest(defect=defect):
                diagnostic = {}
                def iterdir(path):
                    if path == process.parent:
                        def candidates():
                            yield process.parent / "2"
                            if defect == "partial_inventory":
                                raise OSError(private)
                            yield process
                        return candidates()
                    if path == process.parent / "2/fd":
                        if defect == "inaccessible_process": raise PermissionError(private)
                        if defect == "disappeared_process": raise FileNotFoundError(private)
                        if defect == "denied_then_visible": return iter((path / "4", path / "3"))
                    return real_iterdir(path)
                def readlink(path):
                    if Path(path) == process.parent / "2/fd/3":
                        if defect == "inaccessible_descriptor": raise PermissionError(private)
                        if defect == "disappeared_descriptor": raise FileNotFoundError(private)
                    if Path(path) == process.parent / "2/fd/4": raise PermissionError(private)
                    if Path(path) == process / "fd/3": raise PermissionError(private)
                    return real_readlink(path)
                def read_text(path, *args, **kwargs):
                    if defect == "partial_tables" and path == process.parent / "net/tcp6": raise OSError(private)
                    if defect == "inaccessible_owner_identity" and path == process.parent / "2/stat": raise PermissionError(private)
                    return real_read_text(path, *args, **kwargs)
                with patch.object(Path, "iterdir", iterdir), patch.object(fixture.os, "readlink", readlink), \
                        patch.object(Path, "read_text", read_text), patch.object(fixture, "owned_http_open") as opened, \
                        self.assertRaises(fixture.FixtureError) as refused:
                    fixture.verify_dashboard_ready({"pid": 1, "start": "12345"}, process.parent, diagnostic=diagnostic)
                opened.assert_not_called()
                failed = diagnostic["ownership_observations"]["before_health"]
                self.assertEqual(["123"], failed["socket_inodes"])
                self.assertEqual({"pid": 1, "start": "12345"}, diagnostic["expected_identity"])
                self.assertEqual("socket_tables" if defect == "partial_tables" else
                    "descriptor_inventory" if defect == "partial_inventory" else "owner_match", failed["stage"])
                visibility = failed["visibility"]
                self.assertEqual(defect != "partial_tables", visibility["socket_tables_complete"])
                self.assertEqual(defect not in ("partial_inventory", "partial_tables"), visibility["descriptor_scan_completed"])
                if defect not in ("partial_inventory", "partial_tables"):
                    self.assertGreater(visibility["inaccessible_descriptors"], 0)
                counter = {"inaccessible_process": "inaccessible_processes", "disappeared_process": "disappeared_processes",
                           "disappeared_descriptor": "disappeared_descriptors"}.get(defect)
                if counter: self.assertEqual(1, visibility[counter])
                if defect in ("partial_inventory", "denied_then_visible"):
                    self.assertEqual({"123": [2]}, failed["socket_owners"])
                    self.assertEqual([{"pid": 2, "start": "23456", "identity_visibility": "observed"}], failed["owner_identities"])
                if defect == "inaccessible_owner_identity":
                    self.assertEqual([{"pid": 2, "start": None, "identity_visibility": "inaccessible"}], failed["owner_identities"])
                self.assertFalse(visibility["exclusivity_established"])
                self.assertFalse(visibility["unobserved_transfer_excluded"])
                self.assertNotIn(private, json.dumps(diagnostic) + str(refused.exception))

    def test_dashboard_failure_stages_do_not_copy_private_environment_or_exception_details(self):
        private = fixture.secrets.token_hex(24)
        with self.real_dashboard_process() as (_, identity, _):
            for stage in ("expected_process", "environment", "endpoint_validation", "health_request"):
                diagnostic = {}
                read_bytes = Path.read_bytes
                def environment(path):
                    if path == Path(f"/proc/{identity['pid']}/environ"):
                        if stage == "environment": raise PermissionError(private)
                        if stage == "endpoint_validation": return ("ASPNETCORE_URLS=http://user:" + private + "@127.0.0.1:18888").encode()
                    return read_bytes(path)
                expected = dict(identity, start="0") if stage == "expected_process" else identity
                with self.subTest(stage=stage), patch.object(Path, "read_bytes", environment), \
                        patch.object(fixture, "owned_http_open", side_effect=OSError(private)), \
                        self.assertRaises(fixture.FixtureError) as refused:
                    fixture.verify_dashboard_ready(expected, diagnostic=diagnostic)
                self.assertEqual(stage, diagnostic["stage"])
                self.assertNotIn(private, json.dumps(diagnostic) + str(refused.exception))
                self.assertEqual("Owned dashboard HTTP readiness could not be verified", str(refused.exception))

    def test_dashboard_listener_failure_retains_expected_identity_recheck(self):
        process = Path(self.temporary.name) / "recheck-failure-proc" / "1"
        process.mkdir(parents=True)
        stat = "1 (dashboard) " + " ".join(["S", "0", *(["0"] * 17), "12345"])
        (process / "stat").write_text(stat)
        self.dashboard_socket_fixture(process, 18888)
        reads, read_text = [], Path.read_text
        def observe(path, *args, **kwargs):
            if path == process / "stat":
                reads.append(path)
                if len(reads) == 3: return stat.replace("12345", "67890")
            return read_text(path, *args, **kwargs)
        diagnostic = {}
        with patch.object(Path, "read_text", observe), self.assertRaises(fixture.FixtureError) as refused:
            fixture.dashboard_listener_identity({"pid": 1, "start": "12345"},
                fixture.urllib.parse.urlsplit("http://127.0.0.1:18888"), process.parent, diagnostic=diagnostic)
        self.assertEqual("Owned dashboard socket ownership could not be verified", str(refused.exception))
        self.assertEqual("expected_process_recheck", diagnostic["stage"])
        self.assertEqual({"before_inventory": {"pid": 1, "start": "12345", "state": "S"},
                          "after_inventory": {"pid": 1, "start": "67890", "state": "S"}}, diagnostic["expected_process_observations"])
        self.assertEqual({"123": [1]}, diagnostic["socket_owners"])
        self.assertFalse(diagnostic["verified"])

    def test_dashboard_protocol_refusals_retain_opaque_failure_receipts(self):
        private = fixture.secrets.token_hex(24)
        process = Path(self.temporary.name) / "protocol-failure-proc" / "1"
        process.mkdir(parents=True)
        (process / "stat").write_text("1 (dashboard) " + " ".join(["S", "0", *(["0"] * 17), "12345"]))
        verify = fixture.verify_dashboard_ready
        for response in (private.encode() + b"\r\n\r\n", b"HTTP/42.0 200 " + private.encode() + b"\r\n\r\n"):
            def malformed(handler):
                handler.wfile.write(response)
            with self.subTest(protocol=response[:4]), self.owned_http_server(malformed) as origin:
                self.dashboard_socket_fixture(process, fixture.urllib.parse.urlsplit(origin).port)
                (process / "environ").write_bytes(("ASPNETCORE_URLS=" + origin).encode())
                instance = fixture.IsolatedFixture(command=Commands(), log_directory=self.logs)
                output = io.StringIO()
                with patch.object(fixture, "verify_dashboard_ready", side_effect=lambda identity, diagnostic:
                        verify(identity, process.parent, diagnostic=diagnostic)), contextlib.redirect_stdout(output), \
                        contextlib.redirect_stderr(output), self.assertRaises(fixture.FixtureError) as refused:
                    instance.observe_dashboard_readiness({"pid": 1, "start": "12345"}, "initial")
                receipt = (instance.log_directory / "dashboard-listener-initial-failure.json").read_text()
                diagnostic = json.loads(receipt)["diagnostic"]
                self.assertEqual("health_request", diagnostic["stage"])
                self.assertTrue(diagnostic["ownership_observations"]["after_failed_health"]["verified"])
                self.assertFalse(diagnostic["ready"])
                self.assertEqual("Owned dashboard HTTP readiness could not be verified", str(refused.exception))
                self.assertNotIn(private, receipt + str(refused.exception) + output.getvalue())

    def test_dashboard_failed_health_retains_real_conflicting_holders_without_acceptance(self):
        private = fixture.secrets.token_hex(24)
        for mode in ("share", "transfer"):
            for error_kind in ("transport", "http"):
                with self.subTest(mode=mode, error_kind=error_kind), \
                        self.real_listener_handoff_processes() as (owner, recipient, identities, _):
                    def fail(*_args, **_kwargs):
                        owner.stdin.write(mode + "\n")
                        owner.stdin.flush()
                        self.dashboard_process_handshake(owner, "sent\n")
                        self.dashboard_process_handshake(recipient, "received\n")
                        if error_kind == "http":
                            raise fixture.urllib.error.HTTPError(private, 503, private, {}, io.BytesIO(private.encode()))
                        raise fixture.urllib.error.URLError(private)
                    diagnostic = {}
                    with patch.object(fixture, "owned_http_open", side_effect=fail), self.assertRaises(fixture.FixtureError) as refused:
                        fixture.verify_dashboard_ready(identities[0], diagnostic=diagnostic)
                    observations = diagnostic["ownership_observations"]
                    self.assertTrue(observations["before_health"]["verified"])
                    failed = observations["after_failed_health"]
                    self.assertFalse(failed["verified"])
                    self.assertEqual("owner_match", failed["stage"])
                    self.assertEqual(observations["before_health"]["socket_inodes"], failed["socket_inodes"])
                    expected_pids = sorted(identity["pid"] for identity in identities) if mode == "share" else [recipient.pid]
                    self.assertTrue(all(pids == expected_pids for pids in failed["socket_owners"].values()))
                    self.assertEqual("health_request", diagnostic["stage"])
                    self.assertNotIn(private, json.dumps(diagnostic) + str(refused.exception))

    def test_post_journey_discovery_refusals_retain_prior_expected_dashboard_identity(self):
        private = fixture.secrets.token_hex(24)
        with self.real_dashboard_process() as (_, identity, _):
            for stage in ("resource_discovery", "runtime_host_identity"):
                instance = fixture.IsolatedFixture(command=Commands(), log_directory=self.logs, resource_check=lambda *_: [])
                instance.dashboard_readiness = fixture.verify_dashboard_ready(identity)
                with self.subTest(stage=stage), contextlib.ExitStack() as stack:
                    if stage == "resource_discovery":
                        stack.enter_context(patch.object(instance, "resource_check", side_effect=OSError(private)))
                    else:
                        stack.enter_context(patch.object(fixture, "runtime_host_identity", side_effect=fixture.FixtureError("opaque refusal")))
                    with self.assertRaises((OSError, fixture.FixtureError)):
                        instance.capture_dashboard_after_journey({})
                receipt = (instance.log_directory / "dashboard-listener-post-journey-failure.json").read_text()
                diagnostic = json.loads(receipt)["diagnostic"]
                self.assertEqual(stage, diagnostic["stage"])
                self.assertEqual(identity, diagnostic["expected_identity"])
                self.assertEqual({}, diagnostic["ownership_observations"])
                self.assertFalse(diagnostic["visibility"]["exclusivity_established"])
                self.assertNotIn(private, receipt)

    def test_dashboard_zombie_refusal_retains_the_allowlisted_process_state(self):
        process = Path(self.temporary.name) / "zombie-failure-proc" / "1"
        process.mkdir(parents=True)
        (process / "stat").write_text("1 (dashboard) " + " ".join(["Z", "0", *(["0"] * 17), "12345"]))
        diagnostic = {}
        with self.assertRaises(fixture.FixtureError):
            fixture.verify_dashboard_ready({"pid": 1, "start": "12345"}, process.parent, diagnostic=diagnostic)
        self.assertEqual("expected_process", diagnostic["stage"])
        self.assertEqual({"pid": 1, "start": "12345", "state": "Z"}, diagnostic["observed_expected_identity"])

    def test_post_journey_dashboard_receipt_retains_real_listener_observations_and_limits(self):
        with self.real_dashboard_process() as (_, identity, _):
            instance = fixture.IsolatedFixture(command=Commands(), log_directory=self.logs, resource_check=lambda *_: [])
            instance.dashboard_readiness = fixture.verify_dashboard_ready(identity)
            with patch.object(fixture, "runtime_host_identity", return_value={"dashboard": identity}):
                instance.capture_dashboard_after_journey({"command_completed": True, "command_exit_code": 0,
                    "validation_completed": True, "exit_code_before_listener_observation": 0})
            proof = json.loads((instance.log_directory / "dashboard-listener-lifecycle.json").read_text())
            self.assertTrue(proof["same_observed_listener_identity"])
            self.assertFalse(proof["exclusivity_established"])
            self.assertFalse(proof["unobserved_transfer_excluded"])
            for phase in ("before_journey", "after_journey"):
                for observation in proof[phase]["ownership_observations"].values():
                    self.assertEqual(identity["pid"], observation["pid"])
                    self.assertEqual(identity["start"], observation["start"])
                    self.assertTrue(observation["socket_inodes"])
                    self.assertLessEqual(observation["observation_started_unix_ns"], observation["observation_completed_unix_ns"])
            self.assertFalse(proof["fixture_qualification_complete"])
            self.assertEqual("after_journey_and_gateway_proof_before_cleanup", proof["observation_stage"])

    def test_stalled_dashboard_handshake_is_bounded_and_reaps_every_owned_child(self):
        processes, real_popen = [], subprocess.Popen
        def launch(*args, **kwargs):
            process = real_popen(*args, **kwargs)
            processes.append(process)
            return process
        started = time.monotonic()
        with patch.object(subprocess, "Popen", side_effect=launch), \
                self.assertRaisesRegex(AssertionError, "handshake timed out"), \
                self.real_listener_handoff_processes(handshake_timeout=0.05, stall_owner=True):
            self.fail("A stalled child must never reach the caller")
        self.assertLess(time.monotonic() - started, 5)
        self.assertEqual(2, len(processes))
        for process in processes:
            self.assertIsNotNone(process.returncode)
            self.assertTrue(all(stream.closed for stream in (process.stdin, process.stdout, process.stderr)))

    def test_dashboard_cleanup_kills_after_timeout_or_failed_termination_and_closes_every_stream(self):
        for termination_fails in (False, True):
            with self.subTest(termination_fails=termination_fails):
                children = []
                try:
                    for ignore_termination in (True, False):
                        code = ("import signal,time; " + ("signal.signal(signal.SIGTERM, signal.SIG_IGN); " if ignore_termination else "")
                                + "print('ready', flush=True); time.sleep(60)")
                        child = subprocess.Popen([sys.executable, "-u", "-c", code], stdin=subprocess.PIPE,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
                        children.append(child)
                        self.dashboard_process_handshake(child, "ready\n")
                    with contextlib.ExitStack() as stack:
                        if termination_fails:
                            stack.enter_context(patch.object(children[0], "terminate", side_effect=OSError("simulated failure")))
                        killed = stack.enter_context(patch.object(children[0], "kill", wraps=children[0].kill))
                        self.cleanup_dashboard_processes(children, timeout=0.05)
                        killed.assert_called_once()
                    for child in children:
                        self.assertIsNotNone(child.returncode)
                        self.assertTrue(all(stream.closed for stream in (child.stdin, child.stdout, child.stderr)))
                finally:
                    self.cleanup_dashboard_processes(children, timeout=0.05)

    def test_dashboard_health_pins_the_verified_listener_despite_poisoned_localhost_resolution(self):
        original_lookup = socket.getaddrinfo
        localhost_lookups = []
        def poisoned(host, *args, **kwargs):
            if host == "localhost":
                localhost_lookups.append(host)
                return original_lookup("127.0.0.2", *args, **kwargs)
            return original_lookup(host, *args, **kwargs)
        with self.real_dashboard_process(advertise_host="localhost") as (_, identity, origin), \
                patch.object(fixture.socket, "getaddrinfo", side_effect=poisoned), \
                patch.object(fixture, "owned_http_open", wraps=fixture.owned_http_open) as opened:
            result = fixture.verify_dashboard_ready(identity)
            self.assertEqual("127.0.0.1", result["connect_host"])
            self.assertEqual([], localhost_lookups)
            request = opened.call_args.args[0]
            self.assertEqual(origin + "/health", request.full_url)
            self.assertEqual("localhost:" + str(result["port"]), request.get_header("Host"))

    @staticmethod
    def call_journey_namespace():
        tree = ast.parse((fixture.ROOT / "scripts/local_aspire_magic_link_journey.py").read_text())
        node = next(node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == "call")
        namespace = {"json": json, "urllib": fixture.urllib, "owned_http_open": fixture.owned_http_open}
        exec(compile(ast.Module(body=[node], type_ignores=[]), "journey-call", "exec"), namespace)
        return namespace

    def test_journey_http_contract_refuses_redirects_bypasses_proxies_and_keeps_response_bytes(self):
        sink_hits, proxy_hits = [], []
        def sink(handler):
            sink_hits.append(dict(handler.headers))
            handler.send_response(200)
            handler.end_headers()
        def proxy(handler):
            proxy_hits.append(dict(handler.headers))
            handler.send_response(502)
            handler.end_headers()
        with self.owned_http_server(sink) as destination, self.owned_http_server(proxy) as proxy_origin:
            for status in (200, 302, 307, 400, 500):
                origin_hits = []
                def respond(handler):
                    origin_hits.append((dict(handler.headers), handler.rfile.read(int(handler.headers.get("Content-Length", 0)))))
                    handler.send_response(status)
                    handler.send_header("Location", destination + "/credential-sink")
                    handler.end_headers()
                    handler.wfile.write(b'original-response\x00\xff')
                with self.subTest(status=status), self.owned_http_server(respond) as origin:
                    poison = {name: proxy_origin for name in ("HTTP_PROXY", "http_proxy", "HTTPS_PROXY", "https_proxy", "ALL_PROXY", "all_proxy")}
                    with patch.dict(os.environ, dict(poison, NO_PROXY="", no_proxy="")), \
                            patch.object(fixture.urllib.request, "urlopen", side_effect=AssertionError("Default opener forbidden")):
                        result = self.call_journey_namespace()["call"](origin, "POST", {"input": 1}, "assertion-secret", "channel-secret")
                    self.assertEqual((status, b'original-response\x00\xff'), result)
                    self.assertEqual(1, len(origin_hits))
                    headers, body = origin_hits[0]
                    self.assertEqual("assertion-secret", headers["X-Hexalith-Workload-Assertion"])
                    self.assertEqual("channel-secret", headers["Dapr-Api-Token"])
                    self.assertEqual(b'{"input":1}', body)
        self.assertEqual([], sink_hits)
        self.assertEqual([], proxy_hits)
        tree = ast.parse((fixture.ROOT / "scripts/local_aspire_magic_link_journey.py").read_text())
        self.assertFalse(any(isinstance(node, ast.Attribute) and node.attr == "urlopen" for node in ast.walk(tree)))
        metadata_calls = [node for node in ast.walk(tree) if isinstance(node, ast.Call)
                          and isinstance(node.func, ast.Name) and node.func.id == "owned_http_open"
                          and isinstance(node.args[0], ast.BinOp)]
        self.assertEqual(2, len(metadata_calls))

    def test_owned_http_opener_preserves_the_exact_callers_tls_context(self):
        contexts = (fixture.ssl.create_default_context(), fixture.ssl._create_unverified_context())
        for context in contexts:
            with self.subTest(verify_mode=context.verify_mode), patch.object(fixture.urllib.request, "build_opener") as built:
                fixture.owned_http_open("https://127.0.0.1:18180", timeout=2, context=context)
            handlers = built.call_args.args
            self.assertTrue(any(isinstance(handler, fixture.FixtureNoRedirect) for handler in handlers))
            proxy = next(handler for handler in handlers if isinstance(handler, fixture.urllib.request.ProxyHandler))
            self.assertEqual({}, proxy.proxies)
            https = next(handler for handler in handlers if isinstance(handler, fixture.urllib.request.HTTPSHandler))
            self.assertIs(context, https._context)
            built.return_value.open.assert_called_once_with("https://127.0.0.1:18180", timeout=2)

    def test_security_cleanup_requires_aspire_to_remove_the_exact_owned_container(self):
        for remains in (False, True):
            with self.subTest(remains=remains):
                container_id = "f" * 64
                def failure(arguments):
                    if arguments[:3] == ["docker", "container", "ls"] and "id=" + container_id in arguments:
                        return subprocess.CompletedProcess(arguments, 0, container_id if remains else "", "")
                commands = Commands(failure)
                instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs,
                    ownership_check=lambda: commands.started and not commands.stopped)
                instance.start_infrastructure()
                scratch = instance.scratch
                instance.start_attempted = commands.started = True
                instance.security_container = container_id
                with contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(not remains, instance.cleanup())
                self.assertTrue(commands.stopped)
                self.assertFalse(any(args[:2] == ["docker", "rm"] and args[-1] == container_id for args, _ in commands.calls))
                self.assertEqual(remains, Path(scratch).exists())
                self.assertEqual(remains, bool(commands.containers))
                if Path(scratch).exists():
                    shutil.rmtree(scratch)

    def test_security_runtime_inspection_keeps_generated_credentials_out_of_receipts(self):
        for fails in (False, True):
            with self.subTest(fails=fails):
                container_id = "f" * 64
                private_value = fixture.secrets.token_hex(24)
                def failure(arguments):
                    if arguments[:3] == ["docker", "container", "ls"]:
                        return subprocess.CompletedProcess(arguments, 0, container_id, "")
                    if arguments == ["docker", "inspect", container_id]:
                        if fails:
                            raise OSError(private_value)
                        return subprocess.CompletedProcess(arguments, 0, json.dumps([{"Id": container_id,
                            "Config": {"Labels": {"hexalith.timesheets.components": "owned"},
                                       "Env": ["HEXALITH_EVENTSTORE_CLIENT_PASSWORD=" + private_value]},
                            "State": {"Running": True}, "NetworkSettings": {"Ports": {
                                "8080/tcp": [{"HostIp": "127.0.0.1", "HostPort": "18180"}]}}}]), "")
                instance = fixture.IsolatedFixture(command=Commands(failure), log_directory=self.logs,
                    resource_check=lambda *_: [])
                instance.scratch = "owned"
                with patch.object(fixture, "runtime_host_identity", return_value={"dashboard": {}}), \
                        patch.object(fixture, "verify_dashboard_ready", return_value={"ready": True}), \
                        patch.object(fixture, "security_credential_handoff", return_value={"generated_credentials_accepted": True}):
                    if fails:
                        with self.assertRaises(fixture.FixtureError) as failure:
                            instance.verify_security_runtime()
                        self.assertNotIn(private_value, str(failure.exception))
                    else:
                        instance.verify_security_runtime()
                self.assertEqual(container_id, instance.security_container)
                self.assertNotIn(private_value, "".join(path.read_text() for path in instance.log_directory.iterdir()))

    def test_early_security_readiness_failure_requires_label_bound_container_absence_before_cleanup(self):
        for inventory in ("", "f" * 64, None):
            with self.subTest(inventory=inventory):
                def failure(arguments):
                    if arguments[:3] == ["aspire", "wait", "security"]:
                        return subprocess.CompletedProcess(arguments, 18, "", "not ready")
                    if arguments[:3] == ["docker", "container", "ls"] and any(
                            value.startswith("label=hexalith.timesheets.components=") for value in arguments):
                        return subprocess.CompletedProcess(arguments, 1 if inventory is None else 0, inventory or "", "")
                commands = Commands(failure)
                instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs,
                    security_smoke=True, ownership_check=lambda: commands.started and not commands.stopped)
                with patch.object(instance, "capture_source_identity", return_value={"head": "current"}), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(18, instance.execute())
                self.assertIsNone(instance.security_container)
                self.assertTrue(commands.stopped)
                complete = json.loads((instance.log_directory / "cleanup.json").read_text())["complete"]
                self.assertEqual(inventory == "", complete)
                self.assertEqual(inventory != "", Path(instance.scratch).exists())
                self.assertEqual(inventory != "", bool(commands.containers))
                if inventory != "":
                    self.assertFalse(any(args[:2] == ["docker", "rm"] for args, _ in commands.calls))
                if Path(instance.scratch).exists():
                    shutil.rmtree(instance.scratch)

    def test_late_preflight_refusal_records_started_infrastructure_and_no_apphost_attempt(self):
        inspections = 0
        def failure(arguments):
            nonlocal inspections
            if arguments[:2] == ["dapr", "list"]:
                inspections += 1
                if inspections == 2:
                    return subprocess.CompletedProcess(arguments, 0, '[{"appId":"foreign"}]', "")
        commands = Commands(failure)
        instance, status = self.run_fixture(commands)
        self.assertEqual(1, status)
        self.assertFalse(commands.started)
        self.assertTrue(commands.redis_attempted)
        receipt = json.loads((instance.log_directory / "admission.json").read_text())
        self.assertEqual({"admitted": False, "resources_started": True, "infrastructure_started": True,
                          "apphost_start_attempted": False,
                          "reason": "A Dapr application is already running or inventory is unsafe; no AppHost started"}, receipt)
        self.assertFalse(commands.containers)
        self.assertFalse(Path(instance.scratch).exists())
        self.assertTrue(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])

    def test_current_runtime_requires_unique_bound_nuget_dcp_and_dashboard(self):
        directory = Path(self.temporary.name) / "runtime-proc"
        assets = Path(self.temporary.name) / "runtime-assets.json"
        package_folder = Path(self.temporary.name) / "restored-packages"
        packages = {name: {"path": name.lower()} for name in (
            "Aspire.Hosting.Orchestration.linux-x64/13.6.1", "Aspire.Dashboard.Sdk.linux-x64/13.6.1")}
        assets.write_text(json.dumps({"libraries": packages, "packageFolders": {str(package_folder): {}}}))
        identities = [{"pid": pid, "start": str(pid)} for pid in (1, 2, 3, 4)]
        def process(pid, name, package, parent=1, start=None, restored=True):
            executable = (package_folder if restored else Path(self.temporary.name) / "lookalike-packages") / package / "13.6.1/tools" / name
            executable.parent.mkdir(parents=True, exist_ok=True)
            executable.touch()
            path = directory / str(pid)
            path.mkdir(parents=True, exist_ok=True)
            (path / "stat").write_text(f"{pid} (process) " + " ".join(["S", str(parent), *(["0"] * 17), start or str(pid)]))
            (path / "exe").unlink(missing_ok=True)
            (path / "exe").symlink_to(executable)
        process(1, "Hexalith.Timesheets.AppHost", "apphost", parent=0)
        process(2, "dcp", "aspire.hosting.orchestration.linux-x64")
        process(4, "dcp", "aspire.hosting.orchestration.linux-x64", parent=2)
        process(3, "Aspire.Dashboard", "aspire.dashboard.sdk.linux-x64", parent=4)
        result = fixture.runtime_host_identity(identities, assets, directory)
        self.assertEqual({"dcp", "dcp_controller", "dashboard", "dcp_processes", "exited_workers"}, set(result))
        for defect in ("missing", "stale", "foreign-package", "matching-name-outside-restored-root", "unbound-controller", "duplicate"):
            with self.subTest(defect=defect):
                process(2, "dcp", "aspire.hosting.orchestration.linux-x64")
                process(4, "dcp", "aspire.hosting.orchestration.linux-x64", parent=2)
                process(3, "Aspire.Dashboard", "aspire.dashboard.sdk.linux-x64", parent=4)
                selected = list(identities)
                if defect == "missing":
                    selected.pop()
                elif defect == "stale":
                    process(3, "Aspire.Dashboard", "aspire.dashboard.sdk.linux-x64", parent=4, start="changed")
                elif defect == "foreign-package":
                    process(2, "dcp", "cli-bundle")
                elif defect == "matching-name-outside-restored-root":
                    process(2, "dcp", "aspire.hosting.orchestration.linux-x64", restored=False)
                elif defect == "unbound-controller":
                    selected.pop(3)
                else:
                    process(5, "dcp", "aspire.hosting.orchestration.linux-x64")
                    selected.append({"pid": 5, "start": "5"})
                with self.assertRaises(fixture.FixtureError):
                    fixture.runtime_host_identity(selected, assets, directory)

    def dcp_runtime_graph(self, name):
        root = Path(self.temporary.name) / name
        directory, packages = root / "proc", root / "packages"
        assets = root / "project.assets.json"
        executables = {"dcp": packages / "aspire.hosting.orchestration.linux-x64/13.6.1/tools/dcp",
                       "dashboard": packages / "aspire.dashboard.sdk.linux-x64/13.6.1/tools/Aspire.Dashboard",
                       "gateway": root / "gateway/Hexalith.EventStore"}
        for executable in executables.values():
            executable.parent.mkdir(parents=True, exist_ok=True)
            executable.write_bytes(executable.name.encode())
        assets.write_text(json.dumps({"packageFolders": {str(packages): {}}, "libraries": {
            name: {"path": name.lower()} for name in ("Aspire.Hosting.Orchestration.linux-x64/13.6.1",
                                                       "Aspire.Dashboard.Sdk.linux-x64/13.6.1")}}))
        def process(pid, parent, kind="dcp", start=None, state="S", executable=None):
            path = directory / str(pid)
            path.mkdir(parents=True, exist_ok=True)
            executable = executable or executables[kind]
            start = str(pid * 10) if start is None else start
            (path / "stat").write_text(f"{pid} (process) " + " ".join([state, str(parent), *(["0"] * 17), start]))
            (path / "exe").unlink(missing_ok=True)
            (path / "exe").symlink_to(executable)
            (path / "cmdline").write_bytes(os.fsencode(executable) + b"\0")
            return {"pid": pid, "start": start, "state": state, "parent": parent, "executable": str(executable)}
        # Observed NuGet DCP graph: reparented daemon, controller and eleven
        # workers share one native binary; dashboard and gateway use controller.
        identities = [process(82484, 343), process(82571, 82484)]
        identities.extend(process(pid, 82571) for pid in (
            83098, 83181, 83182, 83192, 83304, 83313, 84529, 84992, 85866, 86494, 87236))
        identities.extend((process(83189, 82571, "dashboard"), process(83179, 82571, "gateway")))
        return directory, assets, identities, executables, process

    def test_runtime_host_accepts_one_reparented_multiprocess_dcp_tree_and_preserves_direct_gateway_proof(self):
        directory, assets, identities, executables, _ = self.dcp_runtime_graph("observed-dcp-tree")
        result = fixture.runtime_host_identity(identities, assets, directory)
        self.assertEqual(82484, result["dcp"]["pid"])
        self.assertEqual(343, result["dcp"]["parent"])
        self.assertEqual(82571, result["dcp_controller"]["pid"])
        self.assertEqual(83189, result["dashboard"]["pid"])
        self.assertEqual(13, len(result["dcp_processes"]))
        gateway = fixture.gateway_process_identity(str(executables["gateway"]), identities, directory)
        self.assertEqual(result["dcp_controller"]["pid"], gateway["parent_identity"]["pid"])
        self.assertEqual(0, gateway["argument_count"])

    def test_runtime_host_rejects_disconnected_wrong_package_stale_and_unbound_critical_roles(self):
        for defect in ("second-daemon", "second-tree", "cycle", "wrong-dcp-package", "wrong-dashboard-package", "different-dcp-binary",
                       "stale-daemon", "stale-controller", "stale-dashboard", "stale-worker",
                       "zombie-daemon", "zombie-controller", "zombie-dashboard", "controller-changes-during-validation",
                       "missing-daemon", "missing-controller", "missing-dashboard", "unbound-daemon", "unbound-controller", "unbound-dashboard"):
            with self.subTest(defect=defect):
                directory, assets, identities, executables, process = self.dcp_runtime_graph(defect)
                if defect in ("second-daemon", "second-tree"):
                    identities.append(process(90001, 343))
                    if defect == "second-tree":
                        identities.append(process(90002, 90001))
                elif defect == "cycle":
                    process(87236, 87236)
                elif defect == "different-dcp-binary":
                    executable = executables["dcp"].parent / "another/dcp"
                    executable.parent.mkdir()
                    executable.touch()
                    process(87236, 82571, executable=executable)
                elif defect.startswith("wrong-"):
                    kind = "dcp" if defect == "wrong-dcp-package" else "dashboard"
                    executable = directory.parent / "lookalike" / executables[kind].relative_to(directory.parent / "packages")
                    executable.parent.mkdir(parents=True)
                    executable.touch()
                    process(82484 if kind == "dcp" else 83189, 343 if kind == "dcp" else 82571,
                            kind, executable=executable)
                elif defect.startswith(("stale-", "missing-", "unbound-", "zombie-")):
                    role = defect.split("-", 1)[1]
                    pid = {"daemon": 82484, "controller": 82571, "dashboard": 83189, "worker": 87236}[role]
                    if defect.startswith("missing-"):
                        shutil.rmtree(directory / str(pid))
                    elif defect.startswith("unbound-"):
                        identities = [row for row in identities if row["pid"] != pid]
                    else:
                        old = next(row for row in identities if row["pid"] == pid)
                        process(pid, old["parent"], "dashboard" if role == "dashboard" else "dcp",
                                start="changed" if defect.startswith("stale-") else old["start"],
                                state="Z" if defect.startswith("zombie-") else "S")
                read_text, reads = Path.read_text, 0
                def read_snapshot(path, *args, **kwargs):
                    nonlocal reads
                    contents = read_text(path, *args, **kwargs)
                    if defect == "controller-changes-during-validation" and path == directory / "82571/stat":
                        reads += 1
                        if reads > 1:
                            return contents.rsplit(" ", 1)[0] + " changed"
                    return contents
                with patch.object(Path, "read_text", read_snapshot), self.assertRaises(fixture.FixtureError):
                    fixture.runtime_host_identity(identities, assets, directory)

    def test_runtime_host_allows_only_proven_exited_noncritical_dcp_workers(self):
        for departure in ("gone", "zombie", "exit-during-inspection", "unknown-gone"):
            with self.subTest(departure=departure):
                directory, assets, identities, _, process = self.dcp_runtime_graph(departure)
                worker = directory / "87236"
                if departure in ("gone", "unknown-gone"):
                    shutil.rmtree(worker)
                    if departure == "unknown-gone":
                        identities = [{key: row[key] for key in ("pid", "start", "state")} if row["pid"] == 87236
                                      else row for row in identities]
                elif departure == "zombie":
                    process(87236, 82571, state="Z")
                resolve = Path.resolve
                def resolve_with_exit(path, *args, **kwargs):
                    if departure == "exit-during-inspection" and path == worker / "exe":
                        shutil.rmtree(worker)
                    return resolve(path, *args, **kwargs)
                with patch.object(Path, "resolve", resolve_with_exit):
                    if departure == "unknown-gone":
                        with self.assertRaises(fixture.FixtureError):
                            fixture.runtime_host_identity(identities, assets, directory)
                    else:
                        result = fixture.runtime_host_identity(identities, assets, directory)
                        self.assertEqual([87236], [row["pid"] for row in result["exited_workers"]])
                        self.assertEqual(12, len(result["dcp_processes"]))

    def test_runtime_host_failure_retains_allowlisted_bound_and_unbound_candidate_diagnostics(self):
        directory = Path(self.temporary.name) / "diagnostic-proc"
        package_folder = Path(self.temporary.name) / "diagnostic-packages"
        assets = Path(self.temporary.name) / "diagnostic-assets.json"
        packages = {name: {"path": name.lower()} for name in (
            "Aspire.Hosting.Orchestration.linux-x64/13.6.1", "Aspire.Dashboard.Sdk.linux-x64/13.6.1")}
        assets.write_text(json.dumps({"libraries": packages, "packageFolders": {str(package_folder): {}}}))
        private_value, marker = fixture.secrets.token_hex(24), fixture.secrets.token_hex(24)
        def process(pid, name, package, parent, marked):
            executable = package_folder / package / "13.6.1/tools" / name
            executable.parent.mkdir(parents=True, exist_ok=True)
            executable.touch()
            path = directory / str(pid)
            path.mkdir(parents=True)
            (path / "stat").write_text(f"{pid} (process) " + " ".join(["S", str(parent), *(["0"] * 17), str(pid * 10)]))
            (path / "exe").symlink_to(executable)
            (path / "cmdline").write_bytes(("--password=" + private_value).encode())
            environment = "PASSWORD=" + private_value + "\0"
            if marked:
                environment += "TIMESHEETS_LIVE_DAPR_COMPONENTS=" + marker + "\0"
            (path / "environ").write_bytes(environment.encode())
        process(1, "Hexalith.Timesheets.AppHost", "apphost", 0, True)
        process(2, "dcp", "aspire.hosting.orchestration.linux-x64", 1, False)
        process(3, "Aspire.Dashboard", "aspire.dashboard.sdk.linux-x64", 2, True)
        bound = [{"pid": 1, "start": "10", "password": private_value, "cmdline": private_value}]
        # Diagnostics must not make the unbound candidates acceptable.
        with self.assertRaises(fixture.FixtureError):
            fixture.runtime_host_identity(bound, assets, directory)
        read_bytes = Path.read_bytes
        def checked_read(path):
            self.assertNotEqual("cmdline", path.name)
            return read_bytes(path)
        with patch.object(Path, "read_bytes", checked_read):
            report = fixture.runtime_host_diagnostics(bound, assets, marker, directory)
        encoded = json.dumps(report)
        self.assertNotIn(private_value, encoded)
        self.assertNotIn(marker, encoded)
        self.assertNotIn("password", encoded.lower())
        self.assertNotIn("cmdline", encoded)
        self.assertEqual({"dcp", "dashboard"}, set(report["expected_package_directories"]))
        self.assertEqual([1], [row["pid"] for row in report["observed_processes"]])
        candidates = {row["pid"]: row for row in report["candidate_processes"]}
        self.assertEqual({2, 3}, set(candidates))
        self.assertFalse(candidates[2]["captured"])
        self.assertFalse(candidates[2]["marker_present"])
        self.assertTrue(candidates[3]["marker_present"])
        self.assertEqual([2, 1], [row["pid"] for row in candidates[3]["ancestors"]])
        for row in report["observed_processes"] + report["candidate_processes"]:
            self.assertTrue({"pid", "start", "state", "executable", "parent"} <= set(row))
        instance = fixture.IsolatedFixture(command=Commands(), log_directory=self.logs, resource_check=lambda *_: bound)
        instance.scratch = marker
        diagnostic = fixture.runtime_host_diagnostics
        for evidence_fails in (False, True):
            with self.subTest(evidence_fails=evidence_fails), \
                    patch.object(fixture, "runtime_host_identity", side_effect=fixture.FixtureError("ownership refused")), \
                    patch.object(fixture, "runtime_host_diagnostics", side_effect=lambda identities, _assets, binding:
                                 diagnostic(identities, assets, binding, directory)), \
                    patch.object(Path, "write_text", side_effect=OSError(private_value)) if evidence_fails else contextlib.nullcontext():
                with contextlib.redirect_stdout(io.StringIO()), self.assertRaisesRegex(fixture.FixtureError, "ownership refused"):
                    instance.verify_security_runtime()
            if not evidence_fails:
                retained = json.loads((instance.log_directory / "runtime-host-failure.json").read_text())
                self.assertEqual(report, retained)
            else:
                self.assertTrue(instance.evidence_write_failed)
        self.assertNotIn(private_value, "".join(path.read_text() for path in instance.log_directory.iterdir()))

        with patch.object(fixture, "runtime_host_identity", side_effect=fixture.FixtureError("ownership refused")), \
                patch.object(fixture, "runtime_host_diagnostics", side_effect=OSError(private_value)), \
                self.assertRaisesRegex(fixture.FixtureError, "ownership refused"):
            instance.verify_security_runtime()
        self.assertNotIn(private_value, (instance.log_directory / "runtime-host-failure.json").read_text())

    def test_gateway_resolution_uses_the_pinned_workspace_query_and_discards_inherited_aliases(self):
        inherited = {key: "inherited unsafe value" for key in (
            fixture.GATEWAY_EXECUTABLE_SETTING, fixture.GATEWAY_DIRECTORY_SETTING,
            fixture.GATEWAY_EXECUTABLE_SETTING.lower(), fixture.GATEWAY_DIRECTORY_SETTING.lower(),
            "ASPIRE_" + fixture.GATEWAY_EXECUTABLE_SETTING, "DOTNET_" + fixture.GATEWAY_DIRECTORY_SETTING)}
        directory = Path(self.temporary.name) / "gateway workspace with spaces"
        directory.mkdir()
        executable = directory / "compiled gateway"
        executable.symlink_to(sys.executable)
        commands = Commands()
        commands.run_properties.update(RunCommand=str(executable), RunWorkingDirectory=str(directory))
        with patch.dict(os.environ, inherited):
            caller = dict(os.environ)
            instance, status = self.run_fixture(commands)
            self.assertEqual(0, status)
            self.assertEqual(caller, dict(os.environ))
        query_index = next(index for index, (args, _) in enumerate(commands.calls)
                           if "-target:ComputeRunArguments" in args)
        arguments, options = commands.calls[query_index]
        self.assertEqual(str(fixture.GATEWAY_PROJECT), arguments[2])
        self.assertEqual(fixture.ROOT, options["cwd"])
        self.assertIn("-target:ComputeRunArguments", arguments)
        self.assertIn("-getProperty:RunCommand,RunArguments,RunWorkingDirectory", arguments)
        for setting in ("Configuration=Debug", "UseHexalithProjectReferences=true", "UseNuGetDeps=false"):
            self.assertIn("-p:" + setting, arguments)
        self.assertFalse(set(inherited) & set(options["env"]))
        creation_index = next(index for index, (args, _) in enumerate(commands.calls) if args[:2] == ["docker", "run"])
        self.assertLess(query_index, creation_index)
        for args, options in commands.calls:
            if args[:2] == ["aspire", "start"] or "-class" in args:
                self.assertEqual(str(executable), options["env"][fixture.GATEWAY_EXECUTABLE_SETTING])
                self.assertEqual(str(directory), options["env"][fixture.GATEWAY_DIRECTORY_SETTING])
                self.assertFalse((set(inherited) - {fixture.GATEWAY_EXECUTABLE_SETTING, fixture.GATEWAY_DIRECTORY_SETTING})
                                 & set(options["env"]))
        selected = json.loads((instance.log_directory / "gateway-invocation.json").read_text())
        self.assertEqual(str(executable), selected["executable"])
        self.assertEqual(str(directory), selected["working_directory"])
        self.assertEqual("", selected["arguments"])

    def test_source_capture_hashes_the_dotted_native_gateway_and_its_exact_companion_assembly(self):
        workspace = Path(self.temporary.name) / "source capture workspace"
        apphost = workspace / "src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj"
        gateway_project = workspace / "references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj"
        gateway = gateway_project.parent / "bin/Debug/net10.0/Hexalith.EventStore"
        companion = gateway.parent / "Hexalith.EventStore.dll"
        decoy = gateway.parent / "Hexalith.dll"
        module = workspace / "src/Hexalith.Timesheets/bin/Debug/net10.0/Hexalith.Timesheets"
        integration = workspace / "tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests"
        module_assembly = module.with_name(module.name + ".dll")
        integration_assembly = integration.with_name(integration.name + ".dll")
        files = {
            apphost: b"project", apphost.parent / "Program.cs": b"program",
            apphost.parent / "SecurityConstructionProbe.cs": b"construction probe",
            apphost.parent / "KeycloakRealms/hexalith-realm.json": b"realm",
            apphost.parent / "obj/project.assets.json": b"restored assets",
            apphost.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost.dll": b"apphost assembly",
            gateway: b"native gateway", companion: b"gateway assembly", decoy: b"wrong assembly",
            module: b"native module", module_assembly: b"module assembly",
            integration: b"native test", integration_assembly: b"test assembly",
            module.with_suffix(".dll"): b"wrong module assembly",
            integration.with_suffix(".dll"): b"wrong test assembly",
        }
        for path, contents in files.items():
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(contents)
        heads = {str(workspace): "a" * 40, str(gateway_project.parents[2]): "b" * 40}
        untracked = {root: [] for root in heads}
        patches = {}
        def git(arguments):
            if arguments[:2] == ["git", "-C"]:
                output = (heads[arguments[2]] if arguments[3:] == ["rev-parse", "HEAD"] else
                    "\0".join(untracked[arguments[2]]) if arguments[3:] == ["ls-files", "--others", "--exclude-standard", "-z"]
                    else patches.get(arguments[2], "local source patch"))
                return subprocess.CompletedProcess(arguments, 0, output, "")
        commands = Commands(git)
        instance = fixture.IsolatedFixture(command=commands, log_directory=self.logs)
        instance.environment[fixture.GATEWAY_EXECUTABLE_SETTING] = str(gateway)
        with patch.object(fixture, "ROOT", workspace), patch.object(fixture, "APPHOST", apphost), \
                patch.object(fixture, "GATEWAY_PROJECT", gateway_project):
            captured = instance.capture_source_identity()
            gateway_key, companion_key = str(gateway.relative_to(workspace)), str(companion.relative_to(workspace))
            self.assertEqual(fixture.hashlib.sha256(files[gateway]).hexdigest(), captured["files"][gateway_key])
            self.assertEqual(fixture.hashlib.sha256(files[companion]).hexdigest(), captured["files"][companion_key])
            self.assertNotIn(str(decoy.relative_to(workspace)), captured["files"])
            for asset in (module, module_assembly, integration, integration_assembly):
                self.assertEqual(fixture.hashlib.sha256(files[asset]).hexdigest(),
                                 captured["files"][str(asset.relative_to(workspace))])
            for decoy_asset in (module.with_suffix(".dll"), integration.with_suffix(".dll")):
                self.assertNotIn(str(decoy_asset.relative_to(workspace)), captured["files"])
            self.assertEqual(heads[str(workspace)], captured["timesheets"]["head"])
            self.assertEqual(heads[str(gateway_project.parents[2])], captured["eventstore"]["head"])
            self.assertEqual(fixture.hashlib.sha256(b"local source patch").hexdigest(), captured["eventstore"]["diff_sha256"])
            for asset in (companion, module, module_assembly, integration, integration_assembly):
                with self.subTest(asset=asset):
                    original = asset.read_bytes()
                    asset.write_bytes(original + b"changed")
                    changed = instance.capture_source_identity()
                    key = str(asset.relative_to(workspace))
                    self.assertNotEqual(captured["files"][key], changed["files"][key])
                    self.assertEqual({name: value for name, value in captured["files"].items() if name != key},
                                     {name: value for name, value in changed["files"].items() if name != key})
                    asset.unlink()
                    with self.assertRaises(FileNotFoundError):
                        instance.capture_source_identity()
                    asset.write_bytes(original)
            private = fixture.secrets.token_hex(24).encode()
            for name, root in (("timesheets", workspace), ("eventstore", gateway_project.parents[2])):
                for relative in (Path("src/Untracked.cs"), Path("tests/Untracked.cs"), Path("scripts/untracked.py"), Path("Untracked.targets")):
                    with self.subTest(repository=name, relative=relative):
                        path = root / relative
                        path.parent.mkdir(parents=True, exist_ok=True)
                        path.write_bytes(private)
                        untracked[str(root)].append(str(relative))
                        before = instance.capture_source_identity()
                        path.write_bytes(private + b"changed")
                        after = instance.capture_source_identity()
                        self.assertNotEqual(before[name]["untracked_build_inputs"][str(relative)],
                                            after[name]["untracked_build_inputs"][str(relative)])
                        self.assertEqual(before[name]["diff_sha256"], after[name]["diff_sha256"])
                        self.assertEqual(before["files"], after["files"])
                        self.assertNotIn(private.decode(), json.dumps(after))
                for relative in (Path("tests/evidence/result.json"), Path("tests/TestResults/output.xml"),
                                 Path("src/obj/generated.cs"), Path("_bmad-output/evidence/result.json")):
                    path = root / relative
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(private)
                    untracked[str(root)].append(str(relative))
                    before = instance.capture_source_identity()
                    path.write_bytes(private + b"changed")
                    self.assertEqual(before, instance.capture_source_identity())
            commons = workspace / "references/Hexalith.Commons"
            commons.mkdir()
            heads[str(commons)] = "c" * 40
            untracked[str(commons)] = ["src/extra.cs"]
            extra = commons / "src/extra.cs"
            extra.parent.mkdir()
            extra.write_bytes(private)
            apphost_native = apphost.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
            apphost_native.write_bytes(b"native apphost")
            gateway_assets = gateway_project.parent / "obj/project.assets.json"
            gateway_assets.parent.mkdir()
            gateway_assets.write_bytes(b"gateway restore graph")
            dependency_assembly = module.parent / "Hexalith.Commons.dll"
            dependency_assembly.write_bytes(b"dependency assembly")
            manifest = module.parent / "Hexalith.Timesheets.deps.json"
            manifest.write_bytes(b"launch manifest")
            graph = {"repositories": ["Hexalith.Commons"], "projects": {
                str(apphost.relative_to(workspace)): {}, str(gateway_project.relative_to(workspace)) + "|Version=1": {}}}
            builds = workspace / "references/Hexalith.Builds"
            builds.mkdir()
            heads[str(builds)] = "d" * 40
            untracked[str(builds)] = ["extra-catalog.props"]
            catalog = builds / "extra-catalog.props"
            catalog.write_bytes(private)
            instance.dependency_graph = graph
            with patch.object(instance, "evaluate_dependency_graph", return_value=graph):
                before = instance.capture_source_identity()
                self.assertEqual(graph, before["dependency_graph"])
                self.assertEqual("c" * 40, before["Hexalith.Commons"]["head"])
                self.assertEqual("d" * 40, before["Hexalith.Builds"]["head"])
                patches[str(builds)] = "changed catalog patch"
                self.assertNotEqual(before["Hexalith.Builds"]["diff_sha256"],
                    instance.capture_source_identity()["Hexalith.Builds"]["diff_sha256"])
                catalog.write_bytes(private + b"changed")
                self.assertNotEqual(before["Hexalith.Builds"]["untracked_build_inputs"],
                    instance.capture_source_identity()["Hexalith.Builds"]["untracked_build_inputs"])
                self.assertEqual(fixture.hashlib.sha256(private).hexdigest(),
                    before["Hexalith.Commons"]["untracked_build_inputs"]["src/extra.cs"])
                nested = module.parent / "runtimes/linux-x64/native/helper.so"
                nested.parent.mkdir(parents=True)
                nested.write_bytes(private)
                config = gateway.parent / "appsettings.Development.json"
                config.write_bytes(private)
                for asset in (dependency_assembly, manifest, apphost_native, gateway_assets, nested, config):
                    before = instance.capture_source_identity()
                    key = str(asset.relative_to(workspace))
                    self.assertIn(key, before["files"])
                    saved = asset.read_bytes()
                    asset.write_bytes(saved + b"changed")
                    self.assertNotEqual(before["files"][key], instance.capture_source_identity()["files"][key])
                    asset.write_bytes(saved)
                extra.write_bytes(private + b"changed")
                self.assertNotEqual(before["Hexalith.Commons"]["untracked_build_inputs"],
                    instance.capture_source_identity()["Hexalith.Commons"]["untracked_build_inputs"])
            with patch.object(instance, "evaluate_dependency_graph", return_value={"changed": True}), \
                    self.assertRaisesRegex(fixture.FixtureError, "graph changed"):
                instance.capture_source_identity()
        self.assertFalse(commands.started)
        self.assertFalse(commands.redis_attempted)
        self.assertFalse(instance.start_attempted)

    def test_unsupported_gateway_metadata_refuses_before_creating_infrastructure(self):
        non_executable = Path(self.temporary.name) / "non-executable"
        non_executable.write_text("not executable")
        valid = Commands().run_properties
        payloads = ["not JSON", "null", "[]", "{}", '{"Properties":null}',
                    json.dumps({"Properties": []})]
        for field in valid:
            payloads.append(json.dumps({"Properties": {key: value for key, value in valid.items() if key != field}}))
            for value in (None, 123, []):
                payloads.append(json.dumps({"Properties": {**valid, field: value}}))
        for field, values in (("RunCommand", ("", "relative-command", str(non_executable), str(self.logs), "/missing/gateway")),
                              ("RunArguments", (" ", "--argument", "gateway.dll")),
                              ("RunWorkingDirectory", ("", "relative-directory", str(non_executable), "/missing/directory"))):
            payloads.extend(json.dumps({"Properties": {**valid, field: value}}) for value in values)
        for payload in payloads:
            with self.subTest(payload=payload):
                commands = Commands(lambda args: subprocess.CompletedProcess(args, 0, payload, "")
                                    if "-target:ComputeRunArguments" in args else None)
                instance, status = self.run_fixture(commands)
                self.assertNotEqual(0, status)
                self.assertFalse(commands.started)
                self.assertFalse(commands.redis_attempted)
                self.assertIsNone(instance.scratch)
                self.assertNotIn(fixture.GATEWAY_EXECUTABLE_SETTING, instance.environment)
                self.assertTrue(any("-target:ComputeRunArguments" in args for args, _ in commands.calls))
                self.assertTrue(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])

    def test_gateway_query_failure_retains_sanitized_original_status_without_creating_resources(self):
        commands = Commands(lambda args: subprocess.CompletedProcess(args, 27, "Authorization: Bearer private-query-token", "")
                            if "-target:ComputeRunArguments" in args else None)
        instance, status = self.run_fixture(commands)
        self.assertEqual(27, status)
        self.assertTrue(any("-target:ComputeRunArguments" in args for args, _ in commands.calls))
        self.assertFalse(commands.redis_attempted)
        self.assertFalse(commands.started)
        logs = "".join(path.read_text() for path in instance.log_directory.glob("*.log"))
        self.assertNotIn("private-query-token", logs)
        self.assertIn("redacted", logs)

    def test_actual_msbuild_dependency_root_overrides_are_refused_before_any_resource_start(self):
        alternate = Path(self.temporary.name) / "alternate dependency"
        alternate.mkdir()
        # Evaluation needs no package pins or restored submodules. Retain the
        # actual consuming projects/props, but supply an empty CPM catalog so
        # this refusal regression also runs in a source-only script checkout.
        catalog = Path(self.temporary.name) / "evaluation-catalog.props"
        catalog.write_text("<Project/>")
        for setting in fixture.DEPENDENCY_ROOTS:
            with self.subTest(setting=setting):
                commands = Commands()
                instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None,
                    log_directory=self.logs, ownership_check=lambda: commands.started and not commands.stopped)
                instance.environment[setting] = str(alternate)
                instance.environment["DirectoryPackagesPropsPath"] = str(catalog)
                def real_query(arguments):
                    if "-getItem:ProjectReference" in arguments:
                        return subprocess.run(arguments, cwd=fixture.ROOT, env=instance.environment,
                                              text=True, capture_output=True, timeout=30, check=False)
                commands.failure = real_query
                with contextlib.redirect_stdout(io.StringIO()):
                    self.assertNotEqual(0, instance.execute())
                self.assertFalse(commands.started)
                self.assertFalse(commands.redis_attempted)
                self.assertFalse(instance.start_attempted)
                self.assertIsNone(instance.scratch)
                self.assertFalse((instance.log_directory / "source-before.json").exists())
        # An evaluated project import is not discoverable by just screening the
        # caller's root environment variables. Exercise the real MSBuild import.
        custom = Path(self.temporary.name) / "alternate.props"
        custom.write_text('<Project><Import Project="' + str(fixture.ROOT / "Directory.Build.props")
            + '"/><PropertyGroup><HexalithWorksRoot>' + str(alternate)
            + '</HexalithWorksRoot></PropertyGroup></Project>')
        commands = Commands()
        instance = fixture.IsolatedFixture(command=commands, log_directory=self.logs)
        instance.environment["DirectoryBuildPropsPath"] = str(custom)
        instance.environment["DirectoryPackagesPropsPath"] = str(catalog)
        commands.failure = lambda args: subprocess.run(args, cwd=fixture.ROOT, env=instance.environment,
            text=True, capture_output=True, timeout=30, check=False) if "-getItem:ProjectReference" in args else None
        with self.assertRaisesRegex(fixture.FixtureError, "outside the recorded default roots"):
            instance.validate_dependency_graph()
        self.assertFalse(commands.started)
        self.assertFalse(commands.redis_attempted)

    def test_dependency_graph_refuses_escaped_project_edges_and_records_forwarded_properties(self):
        commands = Commands()
        instance = fixture.IsolatedFixture(command=commands, log_directory=self.logs)
        outside = Path(self.temporary.name) / "foreign.csproj"
        def graph(arguments):
            if "-getItem:ProjectReference" in arguments:
                return subprocess.CompletedProcess(arguments, 0, json.dumps({"Properties": {}, "Items": {
                    "ProjectReference": [{"FullPath": str(outside), "AdditionalProperties": ";Version=1"}]}}), "")
        commands.failure = graph
        with self.assertRaises(fixture.FixtureError):
            instance.validate_dependency_graph()
        self.assertFalse(any(args[2] == str(outside) for args, _ in commands.calls))
        self.assertFalse(commands.redis_attempted)

    def test_successful_recursive_msbuild_globals_relative_roots_and_import_identity(self):
        workspace = Path(self.temporary.name) / "recursive graph"
        apphost = workspace / "src/AppHost/AppHost.csproj"
        integration = workspace / "tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj"
        children = [workspace / f"src/{name}/{name}.csproj" for name in ("A", "B", "C")]
        works = workspace / "references/Hexalith.Works"
        works.mkdir(parents=True)
        children[2] = works / "src/C/C.csproj"
        (workspace / ".gitmodules").write_text('[submodule "Works"]\n path = references/Hexalith.Works\n')
        imported = workspace / "shared.props"
        private = fixture.secrets.token_hex(24)
        imported.write_text('<Project><PropertyGroup><PrivateBuildInput>' + private + '</PrivateBuildInput></PropertyGroup></Project>')
        for path in (apphost, integration, *children): path.parent.mkdir(parents=True, exist_ok=True)
        integration.write_text('<Project/>')
        apphost.write_text('<Project><ItemGroup><ProjectReference Include="' + str(children[0])
            + '"><AdditionalProperties>Flavor=parent;DropMe=remove</AdditionalProperties></ProjectReference></ItemGroup></Project>')
        relative = os.path.relpath(works, children[0].parent)
        children[0].write_text('<Project><PropertyGroup><HexalithWorksRoot>' + relative + '</HexalithWorksRoot></PropertyGroup>'
            '<ItemGroup><ProjectReference Include="' + str(children[1])
            + '"><AdditionalProperties>fLaVoR=child;New=keep</AdditionalProperties>'
            '<GlobalPropertiesToRemove>dRoPmE</GlobalPropertiesToRemove></ProjectReference></ItemGroup></Project>')
        template = '<Project><Import Project="' + str(imported) + '"/><PropertyGroup>' \
            '<HexalithWorksRoot Condition="\'$(Flavor)\' != \'child\' or \'$(New)\' != \'keep\' or \'$(DropMe)\' != \'\'">' \
            + str(workspace / 'wrong-root') + '</HexalithWorksRoot></PropertyGroup>{edge}</Project>'
        children[1].write_text(template.format(edge='<ItemGroup><ProjectReference Include="' + str(children[2]) + '"/></ItemGroup>'))
        children[2].write_text(template.format(edge=''))
        instance = fixture.IsolatedFixture(log_directory=self.logs)
        with patch.object(fixture, "ROOT", workspace), patch.object(fixture, "APPHOST", apphost):
            graph = instance.evaluate_dependency_graph()
            self.assertEqual(5, len(graph["projects"]))
            self.assertEqual(["Hexalith.Works"], graph["repositories"])
            records = {record["project"]: record for record in graph["projects"].values()}
            self.assertEqual("references/Hexalith.Works", records[str(children[0].relative_to(workspace))]["roots"]["HexalithWorksRoot"])
            for child in children[1:]:
                globals_ = records[str(child.relative_to(workspace))]["globals"]
                self.assertEqual(fixture.hashlib.sha256(b"child").hexdigest(), globals_["flavor"])
                self.assertEqual(fixture.hashlib.sha256(b"keep").hexdigest(), globals_["new"])
                self.assertNotIn("dropme", globals_)
            self.assertIn(str(imported), records[str(children[1].relative_to(workspace))]["imports"])
            imported.write_text('<Project><PropertyGroup><PrivateBuildInput>changed</PrivateBuildInput></PropertyGroup></Project>')
            self.assertNotEqual(graph, instance.evaluate_dependency_graph())
            instance.dependency_graph = graph
            with self.assertRaisesRegex(fixture.FixtureError, "graph changed"):
                instance.capture_source_identity()
            self.assertNotIn(private, json.dumps(graph))
            self.assertNotIn(private, ''.join(path.read_text() for path in instance.log_directory.glob('*.log')))
            children[2].write_text('<Project><PropertyGroup><HexalithWorksRoot>../../alternate</HexalithWorksRoot></PropertyGroup></Project>')
            with self.assertRaises(fixture.FixtureError): instance.validate_dependency_graph()
            self.assertFalse(instance.start_attempted)
            for metadata in ('<AdditionalProperties>Configuration=Release</AdditionalProperties>',
                             '<GlobalPropertiesToRemove>UseHexalithProjectReferences</GlobalPropertiesToRemove>'):
                apphost.write_text('<Project><ItemGroup><ProjectReference Include="' + str(children[0]) + '">' + metadata
                    + '</ProjectReference></ItemGroup></Project>')
                with self.assertRaises(fixture.FixtureError): instance.validate_dependency_graph()

    def test_real_ipv6_wildcard_can_coexist_with_explicit_owned_ipv4_listener(self):
        for v6only in (True, False):
            with self.subTest(v6only=v6only), socket.socket(socket.AF_INET6) as wildcard:
                wildcard.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
                wildcard.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEPORT, 1)
                wildcard.setsockopt(socket.IPPROTO_IPV6, socket.IPV6_V6ONLY, int(v6only))
                wildcard.bind(('::', 0))
                wildcard.listen()
                port = wildcard.getsockname()[1]
                class Server(http.server.ThreadingHTTPServer):
                    allow_reuse_port = True
                class Handler(http.server.BaseHTTPRequestHandler):
                    def do_GET(self):
                        self.send_response(200)
                        self.end_headers()
                    def log_message(self, *_): pass
                with Server(('127.0.0.1', port), Handler) as explicit:
                    worker = threading.Thread(target=explicit.serve_forever, daemon=True)
                    worker.start()
                    process = Path(self.temporary.name) / f'wildcard-{v6only}' / str(os.getpid())
                    process.mkdir(parents=True)
                    (process / 'stat').write_bytes(Path('/proc/self/stat').read_bytes())
                    fields = Path('/proc/self/stat').read_text().rsplit(')', 1)[1].split()
                    (process / 'environ').write_bytes(f'ASPNETCORE_URLS=http://127.0.0.1:{port}'.encode())
                    (process / 'fd').symlink_to('/proc/self/fd', target_is_directory=True)
                    (process.parent / 'net').symlink_to('/proc/net', target_is_directory=True)
                    try:
                        result = fixture.verify_dashboard_ready({'pid': os.getpid(), 'start': fields[19]}, process.parent)
                        self.assertTrue(result['ready'])
                        self.assertEqual(1, len(result['ownership']['socket_inodes']))
                    finally:
                        explicit.shutdown()
                        worker.join(timeout=2)

    def test_nondefault_dependency_root_is_the_only_reason_for_graph_refusal(self):
        # Keep the project closure otherwise valid. A refusal caused only by a
        # missing foreign project would not verify the evaluated-root policy.
        for setting in fixture.DEPENDENCY_ROOTS:
            with self.subTest(setting=setting):
                commands = Commands()
                instance = fixture.IsolatedFixture(command=commands, log_directory=self.logs)
                # Remain inside the workspace so relative-path serialization
                # cannot independently reject the otherwise valid graph.
                properties = {setting: str(fixture.ROOT / "alternate-evaluated-root")}
                commands.failure = lambda args: subprocess.CompletedProcess(args, 0, json.dumps({
                    "Properties": properties, "Items": {"ProjectReference": []}}), "") \
                    if "-getItem:ProjectReference" in args else None
                with self.assertRaisesRegex(fixture.FixtureError, "outside the recorded default roots"):
                    instance.validate_dependency_graph()
                self.assertIsNone(instance.dependency_graph)
                self.assertFalse((instance.log_directory / "dependency-graph.json").exists())
                self.assertFalse(commands.started)
                self.assertFalse(commands.redis_attempted)

    def test_poisoned_sdk_run_and_start_environment_cannot_replace_the_gateway_query(self):
        # Query the real SDK's default native launch metadata without requiring a
        # built sibling checkout. The file only satisfies metadata admission; it
        # is never launched, and no restore/build or infrastructure is needed.
        project = Path(self.temporary.name) / "GatewayProbe.csproj"
        project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                           '<TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>'
                           '<RunWorkingDirectory>$(MSBuildProjectDirectory)</RunWorkingDirectory>'
                           '</PropertyGroup></Project>')
        executable = project.parent / "bin/Debug/net10.0/GatewayProbe"
        executable.parent.mkdir(parents=True)
        executable.touch(mode=0o700)
        poison = {"RunCommand": "/usr/bin/true", "RunArguments": "", "RunWorkingDirectory": "/tmp",
                  "StartAction": "Program", "StartProgram": "/usr/bin/true", "StartArguments": "",
                  "StartWorkingDirectory": "/tmp"}
        aliases = {alias: value for name, value in poison.items()
                   for alias in (name, name.lower(), "ASPIRE_" + name.upper(), "DOTNET_" + name)}
        with patch.dict(os.environ, aliases), patch.object(fixture, "GATEWAY_PROJECT", project):
            caller = dict(os.environ)
            instance = fixture.IsolatedFixture(log_directory=self.logs)
            self.assertFalse(set(aliases) & set(instance.environment))
            instance.resolve_gateway_invocation()
            self.assertEqual(caller, dict(os.environ))
        selected = json.loads((instance.log_directory / "gateway-invocation.json").read_text())
        self.assertEqual(str(executable), selected["executable"])
        self.assertEqual(str(project.parent), selected["working_directory"])
        self.assertEqual("", selected["arguments"])

    def test_removing_inherited_launch_overrides_preserves_project_defined_metadata(self):
        project = Path(self.temporary.name) / "Project.csproj"
        root = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
        properties = ET.SubElement(root, "PropertyGroup")
        for name, value in {"TargetFramework": "net10.0", "OutputType": "Exe", "StartAction": "Program",
                            "StartProgram": sys.executable, "StartWorkingDirectory": self.temporary.name}.items():
            ET.SubElement(properties, name).text = value
        ET.ElementTree(root).write(project, encoding="unicode")
        with patch.dict(os.environ, {"RunCommand": "/usr/bin/true", "StartProgram": "/usr/bin/false"}), \
                patch.object(fixture, "GATEWAY_PROJECT", project):
            instance = fixture.IsolatedFixture(log_directory=self.logs)
            instance.resolve_gateway_invocation()
        self.assertEqual(sys.executable, instance.environment[fixture.GATEWAY_EXECUTABLE_SETTING])
        self.assertEqual(self.temporary.name, instance.environment[fixture.GATEWAY_DIRECTORY_SETTING])

    def test_gateway_process_checks_require_native_command_and_fixture_bound_dcp_parent(self):
        directory = Path(self.temporary.name) / "proc"
        selected = Path(self.temporary.name) / "gateway"
        selected.touch()
        dcp = Path(self.temporary.name) / "dcp"
        dcp.touch()
        other = Path(self.temporary.name) / "dotnet"
        other.touch()
        def process(pid, parent, start, executable, arguments):
            path = directory / str(pid)
            path.mkdir(parents=True, exist_ok=True)
            (path / "stat").write_text(f"{pid} (process) " + " ".join(["S", str(parent), *(["0"] * 17), start]))
            (path / "cmdline").write_bytes(b"\0".join(os.fsencode(arg) for arg in arguments) + b"\0")
            (path / "exe").unlink(missing_ok=True)
            (path / "exe").symlink_to(executable)
        identities = [{"pid": 1, "start": "native"}, {"pid": 2, "start": "owner"}]
        process(1, 2, "native", selected, [selected])
        process(2, 0, "owner", dcp, [dcp])
        result = fixture.gateway_process_identity(str(selected), identities, directory)
        self.assertEqual(str(selected), result["command"])
        self.assertEqual(0, result["argument_count"])
        self.assertEqual(2, result["parent_identity"]["pid"])
        for scenario in ("wrapper-command", "arguments", "wrapper-parent", "unbound-parent", "changed-start", "duplicate"):
            with self.subTest(scenario=scenario):
                process(1, 2, "native", selected, [selected])
                process(2, 0, "owner", dcp, [dcp])
                bound = list(identities)
                if scenario == "wrapper-command":
                    process(1, 2, "native", selected, ["dotnet", "run", "PRIVATE-TOKEN"])
                elif scenario == "arguments":
                    process(1, 2, "native", selected, [selected, "PRIVATE-TOKEN"])
                elif scenario == "wrapper-parent":
                    process(2, 0, "owner", other, [other])
                elif scenario == "unbound-parent":
                    bound.pop()
                elif scenario == "changed-start":
                    process(1, 2, "changed", selected, [selected])
                else:
                    process(3, 2, "second", selected, [selected])
                    bound.append({"pid": 3, "start": "second"})
                with self.assertRaises(fixture.FixtureError) as failure:
                    fixture.gateway_process_identity(str(selected), bound, directory)
                self.assertNotIn("PRIVATE-TOKEN", str(failure.exception))

    def test_gateway_proof_is_repeatable_and_preserves_primary_failure_and_owned_cleanup(self):
        for status in (0, 42):
            for proof_failure in (False, True):
                with self.subTest(status=status, proof_failure=proof_failure):
                    commands = Commands(lambda args: subprocess.CompletedProcess(args, status,
                        "Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0", "") if "-class" in args else None)
                    instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs,
                        ownership_check=lambda: commands.started and not commands.stopped, resource_check=lambda *_: [])
                    instance.injected_command = False
                    identities = self.gateway_identities()
                    if proof_failure:
                        identities[1] = fixture.FixtureError("native inspection failed")
                    with patch.object(instance, "_journey_command", commands), \
                            patch.object(instance, "capture_gateway_process", side_effect=identities) as capture, \
                            patch.object(fixture, "read_only_shared_snapshot", return_value=self.shared_snapshot()), \
                            patch.object(fixture.time, "sleep"), patch.object(fixture, "verify_runtime_components"), patch.object(instance, "capture_runtime_readiness"), \
                            patch.object(instance, "prepare_fixture_bootstrap"), patch.object(instance, "bootstrap_fixture_protocol"), \
                            contextlib.redirect_stdout(io.StringIO()):
                        self.assertEqual(status or int(proof_failure), instance.execute())
                    self.assertEqual([("before",), ("after",)], [call.args for call in capture.call_args_list])
                    self.assertFalse(commands.containers)
                    self.assertTrue(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])
                    self.assertTrue((instance.log_directory / ("gateway-process-proof-failure.json" if proof_failure
                        else "gateway-process-replacement.json")).is_file())

    def test_execute_captures_post_journey_dashboard_outcome_and_failure_preserves_owned_cleanup(self):
        with self.real_dashboard_process() as (_, identity, _):
            initial = fixture.verify_dashboard_ready(identity)
            for status in (0, 42):
                for capture_fails in (False, True):
                    with self.subTest(status=status, capture_fails=capture_fails):
                        commands = Commands(lambda args: subprocess.CompletedProcess(args, status,
                            "Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0", "") if "-class" in args else None)
                        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs,
                            security_smoke=True, ownership_check=lambda: commands.started and not commands.stopped,
                            resource_check=lambda *_: [])
                        instance.injected_command = False
                        def security_runtime():
                            instance.dashboard_readiness = initial
                        with patch.object(instance, "_journey_command", commands), \
                                patch.object(instance, "capture_gateway_process", side_effect=self.gateway_identities()), \
                                patch.object(fixture, "read_only_shared_snapshot", return_value=self.shared_snapshot()), \
                                patch.object(instance, "capture_source_identity", return_value={"source": "unchanged"}), \
                                patch.object(instance, "verify_security_runtime", side_effect=security_runtime), \
                                patch.object(fixture, "runtime_host_identity", return_value={"dashboard": identity}), \
                                patch.object(fixture, "verify_dashboard_ready", wraps=fixture.verify_dashboard_ready,
                                    side_effect=fixture.FixtureError("safe capture refusal") if capture_fails else None), \
                                patch.object(fixture.time, "sleep"), patch.object(fixture, "verify_runtime_components"), \
                                patch.object(instance, "capture_runtime_readiness"), patch.object(instance, "prepare_fixture_bootstrap"), \
                                patch.object(instance, "bootstrap_fixture_protocol"), contextlib.redirect_stdout(io.StringIO()):
                            self.assertEqual(status or int(capture_fails), instance.execute())
                        filename = "dashboard-listener-lifecycle-failure.json" if capture_fails else "dashboard-listener-lifecycle.json"
                        proof = json.loads((instance.log_directory / filename).read_text())
                        self.assertEqual(status, proof["journey_outcome"]["command_exit_code"])
                        self.assertEqual(status == 0, proof["journey_outcome"]["journey_succeeded"])
                        self.assertTrue(proof["journey_outcome"]["command_completed"])
                        self.assertTrue(proof["journey_outcome"]["validation_completed"])
                        self.assertEqual("after_journey_and_gateway_proof_before_cleanup", proof["observation_stage"])
                        self.assertFalse(proof["fixture_qualification_complete"])
                        self.assertGreaterEqual(proof["recorded_at_unix_ns"],
                            proof["journey_outcome"]["command_completed_unix_ns"])
                        if not capture_fails:
                            observation = proof["after_journey"]["ownership_observations"]["before_health"]
                            self.assertGreaterEqual(observation["observation_started_unix_ns"],
                                proof["journey_outcome"]["command_completed_unix_ns"])
                        self.assertTrue(commands.stopped)
                        self.assertFalse(commands.containers)
                        self.assertTrue(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])

    def test_execute_retains_real_ownership_failure_evidence_and_reaps_owned_processes(self):
        """Real synthetic sockets, injected infrastructure; no native Dashboard transfer claim."""
        private = fixture.secrets.token_hex(24)
        for mode in ("share", "transfer"):
            for phase in ("initial", "post-journey"):
                for status in ((0,) if phase == "initial" else (0, 42)):
                    with self.subTest(mode=mode, phase=phase, status=status), \
                            patch.dict(os.environ, {"PRIVATE_TEST_VALUE": private}), \
                            self.real_listener_handoff_processes() as (owner, recipient, identities, _):
                        initial = fixture.verify_dashboard_ready(identities[0])
                        before_cleanup = []
                        def handoff():
                            owner.stdin.write(mode + "\n")
                            owner.stdin.flush()
                            self.dashboard_process_handshake(owner, "sent\n")
                            self.dashboard_process_handshake(recipient, "received\n")
                        def command(arguments):
                            if arguments[:2] == ["aspire", "stop"]:
                                before_cleanup.append((instance.log_directory / ("dashboard-listener-" + phase + "-failure.json")).read_bytes())
                                self.cleanup_dashboard_processes([owner, recipient])
                            if "-class" in arguments:
                                handoff()
                                return subprocess.CompletedProcess(arguments, status,
                                    "Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0", "")
                        commands = Commands(command)
                        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs,
                            security_smoke=True, ownership_check=lambda: commands.started and not commands.stopped,
                            resource_check=lambda *_: [] if commands.stopped else identities)
                        instance.injected_command = False
                        def security_runtime():
                            instance.captured_processes = list(identities)
                            instance.dashboard_readiness = initial
                        if phase == "initial": handoff()
                        output = io.StringIO()
                        with contextlib.ExitStack() as stack:
                            if phase == "post-journey":
                                stack.enter_context(patch.object(instance, "verify_security_runtime", side_effect=security_runtime))
                            for name in ("capture_runtime_readiness", "prepare_fixture_bootstrap", "bootstrap_fixture_protocol"):
                                stack.enter_context(patch.object(instance, name))
                            stack.enter_context(patch.object(instance, "_journey_command", commands))
                            stack.enter_context(patch.object(instance, "capture_gateway_process", side_effect=self.gateway_identities()))
                            stack.enter_context(patch.object(instance, "capture_source_identity", return_value={"source": "unchanged"}))
                            stack.enter_context(patch.object(fixture, "runtime_host_identity", return_value={"dashboard": identities[0]}))
                            stack.enter_context(patch.object(fixture, "read_only_shared_snapshot", return_value=self.shared_snapshot()))
                            stack.enter_context(patch.object(fixture.time, "sleep"))
                            stack.enter_context(patch.object(fixture, "verify_runtime_components"))
                            stack.enter_context(contextlib.redirect_stdout(output))
                            self.assertEqual(status or 1, instance.execute())
                        receipt_path = instance.log_directory / ("dashboard-listener-" + phase + "-failure.json")
                        receipt_bytes = receipt_path.read_bytes()
                        proof = json.loads(receipt_bytes)
                        diagnostic = proof["diagnostic"]
                        self.assertEqual(identities[0], diagnostic["expected_identity"])
                        self.assertEqual("before_health", diagnostic["stage"])
                        failed = diagnostic["ownership_observations"]["before_health"]
                        self.assertEqual("owner_match", failed["stage"])
                        self.assertEqual(initial["ownership"]["socket_inodes"], failed["socket_inodes"])
                        self.assertTrue(commands.stopped)
                        self.assertFalse(commands.containers)
                        self.assertTrue(all(child.poll() is not None for child in (owner, recipient)))
                        self.assertFalse(Path(instance.scratch).exists())
                        self.assertTrue(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])
                        self.assertTrue(json.loads((instance.log_directory / "recorded-process-exit.json").read_text())["all_gone"])
                        self.assertNotIn(private, receipt_bytes.decode() + output.getvalue())
                        if phase == "post-journey":
                            lifecycle = json.loads((instance.log_directory / "dashboard-listener-lifecycle-failure.json").read_text())
                            self.assertEqual(diagnostic, lifecycle["ownership_failure"])
                            self.assertEqual(status, lifecycle["journey_outcome"]["command_exit_code"])
                        self.assertEqual([receipt_bytes], before_cleanup)

    def test_gateway_replacement_requires_new_identity_same_dcp_and_exited_old_identity(self):
        instance = fixture.IsolatedFixture(log_directory=self.logs)
        before, after = self.gateway_identities()
        for unchanged, moved_owner, old_alive in ((True, False, False), (False, True, False), (False, False, True)):
            with self.subTest(unchanged=unchanged, moved_owner=moved_owner, old_alive=old_alive):
                candidate = before if unchanged else dict(after)
                if moved_owner:
                    candidate["parent_identity"] = {"pid": 999999995, "start": "other"}
                with patch.object(fixture, "recorded_process_exits", return_value={"all_gone": not old_alive}), \
                        self.assertRaises(fixture.FixtureError):
                    instance.verify_gateway_replacement(before, candidate)

    def test_gateway_capture_retains_sanitized_evidence_and_all_cleanup_identities(self):
        instance = fixture.IsolatedFixture(log_directory=self.logs)
        instance.environment[fixture.GATEWAY_EXECUTABLE_SETTING] = sys.executable
        before, after = self.gateway_identities()
        parent = {"pid": 999999996, "start": "owner", "state": "S"}
        before = {**before, "state": "S", "parent_identity": parent}
        after = {**after, "state": "S", "parent_identity": parent}
        with patch.object(instance, "resource_check", return_value=[]), \
                patch.object(fixture, "gateway_process_identity", side_effect=(before, after)):
            self.assertEqual(before, instance.capture_gateway_process("before"))
            self.assertEqual(after, instance.capture_gateway_process("after"))
        self.assertEqual({(row["pid"], row["start"]) for row in (before, after, parent)},
                         {(row["pid"], row["start"]) for row in instance.captured_processes})
        for phase in ("before", "after"):
            self.assertTrue((instance.log_directory / ("gateway-process-" + phase + ".json")).is_file())
        failed = fixture.IsolatedFixture(log_directory=self.logs)
        failed.environment[fixture.GATEWAY_EXECUTABLE_SETTING] = sys.executable
        with patch.object(failed, "resource_check", return_value=[]), \
                patch.object(fixture, "gateway_process_identity", return_value=after), \
                patch.object(failed, "write_evidence", return_value=False), self.assertRaises(fixture.FixtureError):
            failed.capture_gateway_process("after")
        self.assertEqual({(after["pid"], after["start"]), (parent["pid"], parent["start"])},
                         {(row["pid"], row["start"]) for row in failed.captured_processes})

    def test_gateway_before_proof_refuses_journey_and_after_cancellation_preserves_existing_failure(self):
        for original_status, before_failure in ((0, True), (0, False), (42, False)):
            with self.subTest(original_status=original_status, before_failure=before_failure):
                commands = Commands(lambda args: subprocess.CompletedProcess(args, original_status,
                    "Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0", "") if "-class" in args else None)
                instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs,
                    ownership_check=lambda: commands.started and not commands.stopped, resource_check=lambda *_: [])
                instance.injected_command = False
                identities = [fixture.FixtureError("native ownership failed")] if before_failure else [self.gateway_identities()[0], KeyboardInterrupt()]
                with patch.object(instance, "_journey_command", commands), \
                        patch.object(instance, "capture_gateway_process", side_effect=identities), \
                        patch.object(fixture, "read_only_shared_snapshot", return_value=self.shared_snapshot()), \
                        patch.object(fixture.time, "sleep"), patch.object(fixture, "verify_runtime_components"), patch.object(instance, "capture_runtime_readiness"), \
                            patch.object(instance, "prepare_fixture_bootstrap"), patch.object(instance, "bootstrap_fixture_protocol"), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(1 if before_failure else original_status or 130, instance.execute())
                self.assertEqual(not before_failure, any("-class" in args for args, _ in commands.calls))
                self.assertFalse(commands.containers)
                self.assertTrue(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])

    def test_aspire_resolves_current_assets_instead_of_reusing_another_configuration(self):
        # Aspire 13.6.1 reads this escape hatch from the project configuration,
        # not the environment. Its cached launch command omits ambient MSBuild
        # properties from the cache fingerprint and can otherwise select Release.
        config = json.loads((fixture.ROOT / "aspire.config.json").read_text())
        self.assertIn(config.get("dotnetAppHostInfoCacheDisabled"), (True, "true"))

    def test_compiled_guard_driver_covers_incomplete_fixture_settings_with_harmless_probes(self):
        commands = self.guard_commands()
        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs)
        snapshot = self.new_shared_snapshot()
        snapshot["scheduler"] = {"revision": "1", "keys": 0}
        executable = fixture.APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
        original_is_file = Path.is_file
        with patch.object(guards, "IsolatedFixture", return_value=instance), \
                patch.object(guards, "read_only_shared_snapshot", return_value=snapshot), \
                patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, guards.run_guards())
        probes = {arguments[1].split("=", 1)[1]: (arguments, kwargs["env"])
                  for arguments, kwargs in commands.calls if arguments[0] == str(executable)}
        self.assertEqual(67, len(probes))
        for name, (arguments, environment) in probes.items():
            model = name.startswith("model-") or name in ("full-construction", "security-construction")
            self.assertEqual("1", environment["TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY"], name)
            self.assertIn("--TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY=" + ("0" if model else "1"), arguments)
            if name.startswith("model-"):
                self.assertIn("--TIMESHEETS_LIVE_DAPR_MODEL_ONLY=1", arguments)
            if name == "full-construction":
                self.assertIn("--TIMESHEETS_LIVE_DAPR_CONSTRUCTION_ONLY=1", arguments)
                self.assertIn("--TIMESHEETS_LIVE_DAPR_MODEL_ONLY=0", arguments)
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
        self.assertEqual(67, len(commands.calls))
        for arguments, kwargs in commands.calls:
            self.assertEqual(str(executable), arguments[0])
            self.assertEqual("1", kwargs["env"]["TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY"])
            self.assertTrue(all(isinstance(value, str) for value in kwargs["env"].values()))
            probe = arguments[1].split("=", 1)[1]
            model = probe.startswith("model-") or probe in ("full-construction", "security-construction")
            self.assertIn("--TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY=" + ("0" if model else "1"), arguments)
            self.assertFalse(set(polluted) & set(kwargs["env"]))
        self.assertIsNone(instance.scratch)
        self.assertFalse(commands.containers)
        self.assertEqual({"configuration": "Debug", "configuration_only": True, "resources_enabled": False},
                         json.loads((instance.log_directory / "guards-mode.json").read_text()))

    def test_full_construction_probe_refuses_duplicate_missing_or_failed_credentials_without_starting(self):
        executable = fixture.APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
        original_is_file = Path.is_file
        for changed, exit_code in (("gatewayCredentials", 0), ("timesheetsCredentials", 0), (None, -6)):
            with self.subTest(changed=changed, exit_code=exit_code):
                commands = self.guard_commands()
                original = commands.failure
                def mutation(arguments):
                    result = original(arguments)
                    if "--guard-probe=full-construction" in arguments:
                        result.returncode = exit_code
                        if changed:
                            result.stdout = result.stdout.replace('"' + changed + '": 1',
                                '"' + changed + '": ' + ("2" if changed == "gatewayCredentials" else "0"))
                    return result
                commands.failure = mutation
                instance = fixture.IsolatedFixture(command=commands, log_directory=self.logs)
                with patch.object(guards, "IsolatedFixture", return_value=instance), \
                        patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(1, guards.run_guards(configuration_only=True))
                self.assertFalse(commands.started)
                self.assertFalse(commands.containers)

    def test_compiled_guard_requires_actual_gateway_readiness_result_in_both_construction_models(self):
        executable = fixture.APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
        original_is_file = Path.is_file
        for probe in ("full-construction", "security-construction"):
            for field, value in (("endpoint", "https"), ("path", "/alive"), ("port", 8080),
                                 ("unavailableRejected", False), ("readyAccepted", False),
                                 ("healthCheckAttached", False), ("moduleWait", "WaitForCompletion"), ("gatewayReferenced", False)):
                with self.subTest(probe=probe, field=field):
                    commands = self.guard_commands()
                    original = commands.failure
                    def mutation(arguments):
                        result = original(arguments)
                        if "--guard-probe=" + probe in arguments:
                            expected = guards.expected_gateway_readiness(probe == "security-construction")
                            replacement = not expected[field] if isinstance(expected[field], bool) else value
                            result.stdout = result.stdout.replace(json.dumps(expected), json.dumps({**expected, field: replacement}))
                        return result
                    commands.failure = mutation
                    instance = fixture.IsolatedFixture(command=commands, log_directory=self.logs)
                    with patch.object(guards, "IsolatedFixture", return_value=instance), \
                            patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                            contextlib.redirect_stdout(io.StringIO()):
                        self.assertEqual(1, guards.run_guards(configuration_only=True))
                    self.assertFalse(commands.started)
                    self.assertFalse(commands.containers)

    def test_release_configuration_guards_select_package_assets_and_require_adapter_refusal(self):
        commands = self.guard_commands(package=True)
        instance = fixture.IsolatedFixture(command=commands, log_directory=self.logs)
        executable = fixture.APPHOST.parent / "bin/Release/net10.0/Hexalith.Timesheets.AppHost"
        original_is_file = Path.is_file
        caller = dict(os.environ)
        with patch.object(guards, "IsolatedFixture", return_value=instance), \
                patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                patch.object(guards, "available_control_port", side_effect=AssertionError("Unexpected socket bind")), \
                patch.object(guards, "read_only_shared_snapshot", side_effect=AssertionError("Unexpected storage read")), \
                contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, guards.run_guards(configuration_only=True, configuration="Release"))
        self.assertEqual(caller, dict(os.environ))
        self.assertEqual(67, len(commands.calls))
        for arguments, kwargs in commands.calls:
            self.assertEqual(str(executable), arguments[0])
            probe = arguments[1].split("=", 1)[1]
            model = probe.startswith("model-") or probe in ("full-construction", "security-construction")
            self.assertIn("--TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY=" + ("0" if model else "1"), arguments)
            self.assertEqual("Release", kwargs["env"]["Configuration"])
            self.assertEqual("false", kwargs["env"]["UseHexalithProjectReferences"])
            self.assertEqual("true", kwargs["env"]["UseNuGetDeps"])
        self.assertIsNone(instance.scratch)
        self.assertFalse(commands.containers)
        self.assertEqual({"configuration": "Release", "configuration_only": True, "resources_enabled": False},
                         json.loads((instance.log_directory / "guards-mode.json").read_text()))
        self.assertTrue(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])

    def test_release_guards_refuse_debug_adapter_success(self):
        commands = self.guard_commands()
        instance = fixture.IsolatedFixture(command=commands, log_directory=self.logs)
        executable = fixture.APPHOST.parent / "bin/Release/net10.0/Hexalith.Timesheets.AppHost"
        original_is_file = Path.is_file
        with patch.object(guards, "IsolatedFixture", return_value=instance), \
                patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(1, guards.run_guards(configuration_only=True, configuration="Release"))
        self.assertIn("valid-compiled-isolated did not refuse", output.getvalue())
        self.assertFalse(commands.containers)

    def test_release_guards_require_matching_binary_without_debug_fallback(self):
        commands = self.guard_commands(package=True)
        instance = fixture.IsolatedFixture(command=commands, log_directory=self.logs)
        debug = fixture.APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
        with patch.object(guards, "IsolatedFixture", return_value=instance), \
                patch.object(Path, "is_file", lambda path: path == debug), \
                contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(1, guards.run_guards(configuration_only=True, configuration="Release"))
        self.assertIn("Build the Release AppHost", output.getvalue())
        self.assertFalse(commands.calls)

    def test_release_guards_refuse_infrastructure_mode_before_fixture_creation(self):
        with patch.object(guards, "IsolatedFixture", side_effect=AssertionError("Unexpected fixture creation")):
            with self.assertRaisesRegex(fixture.FixtureError, "Release guard checks require --configuration-only"):
                guards.run_guards(configuration="Release")

    def test_release_guard_cli_refuses_infrastructure_mode(self):
        result = subprocess.run([sys.executable, str(Path(guards.__file__)), "--configuration", "Release"],
                                capture_output=True, text=True, timeout=10)
        self.assertEqual(2, result.returncode)
        self.assertIn("Release guard checks require --configuration-only", result.stderr)

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
            '[{"appId":"eventstore"}]', "") if args[:2] == ["dapr", "list"] else None)
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
        binding = launch[launch.index("--publish") + 1]
        self.assertRegex(binding, r"^127\.0\.0\.1:[0-9]+:6379$")
        self.assertNotEqual(8080, int(binding.split(":")[1]))
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

    def test_dapr_cardinality_failure_retains_only_safe_live_process_identity_and_target_predicates(self):
        directory = Path(self.temporary.name) / "components"
        fixture.write_components(directory, "127.0.0.1:49152")
        processes = Path(self.temporary.name) / "proc"
        marker = fixture.secrets.token_hex(24)
        placement, scheduler = "127.0.0.1:49153", "127.0.0.1:49154"
        for pid, app, state, marked in ((123, "eventstore", "S", True), (456, "eventstore", "S", False),
                                        (789, marker, "S", False), (987, "eventstore", "Z", True)):
            process = processes / str(pid)
            process.mkdir(parents=True)
            (process / "stat").write_text(f"{pid} (daprd) " + " ".join([state, "1", *(["0"] * 17), str(pid * 10)]))
            arguments = ["daprd", "--app-id", app, "--app-port", "8082", "--app-channel-address", "127.0.0.1",
                         "--placement-host-address", placement, "--scheduler-host-address", scheduler, "--token", marker]
            (process / "cmdline").write_bytes("\0".join(arguments).encode())
            (process / "environ").write_bytes(("PASSWORD=" + marker + "\0" + (
                "TIMESHEETS_LIVE_DAPR_COMPONENTS=" + str(directory) + "\0" if marked else "")).encode())
        observation = fixture.runtime_dapr_identity_observation(["123", "456", "789", "987"], directory,
            placement, scheduler, processes)
        self.assertEqual({"eventstore": 2, "timesheets": 0, "other": 1}, observation["counts"])
        self.assertEqual([123, 456, 789], [row["pid"] for row in observation["processes"]])
        self.assertTrue(observation["processes"][0]["fixture_marker_matches"])
        self.assertFalse(observation["processes"][1]["fixture_marker_matches"])
        for private in (marker, str(directory), placement, scheduler, "--token", "PASSWORD"):
            self.assertNotIn(private, json.dumps(observation))
        for pids in ("", "123\n456\n"):
            with self.subTest(pids=pids), patch.object(fixture.subprocess, "check_output", return_value=pids):
                retained = []
                with self.assertRaisesRegex(fixture.FixtureError, "identity is not unique"):
                    fixture.verify_runtime_components("127.0.0.1:49152", directory, placement, scheduler,
                        observation=retained.append, process_directory=processes)
                self.assertEqual(1, len(retained))
                self.assertEqual(2 if pids else 0, retained[0]["counts"]["eventstore"])
        with patch.object(fixture.subprocess, "check_output", side_effect=subprocess.CalledProcessError(1, "pgrep")):
            retained = []
            with self.assertRaisesRegex(fixture.FixtureError, "identity is not unique"):
                fixture.verify_runtime_components("127.0.0.1:49152", directory, placement, scheduler, retained.append)
            self.assertEqual([], retained[0]["processes"])

    def test_gateway_readiness_observes_real_process_environment_and_only_fixed_health_and_actor_fields(self):
        directory = Path(self.temporary.name) / "proc"
        process = directory / "123"
        process.mkdir(parents=True)
        marker, secret = fixture.secrets.token_hex(24), fixture.secrets.token_hex(24)
        (process / "stat").write_text("123 (gateway) " + " ".join(["S", "1", *(["0"] * 17), "77"]))
        (process / "environ").write_bytes((f"TIMESHEETS_LIVE_DAPR_COMPONENTS={marker}\0DAPR_HTTP_PORT=3501\0"
            f"DAPR_HTTP_ENDPOINT=http://localhost:3501\0PASSWORD={secret}\0").encode())
        gateway = {"pid": 123, "start": "77"}
        # Aspire's injected endpoint is a loopback proxy; daprd's target port
        # deliberately differs. Numeric equality to that target is incorrect.
        sidecar = {"arguments": ["daprd", "--dapr-http-port", "18389", "--token", secret]}
        health = {"status": "Unhealthy", "description": secret, "results": {
            "dapr-actor-placement": {"status": "Unhealthy", "description": secret}, secret: {"status": secret}}}
        metadata = {"id": "eventstore", "actorRuntime": {"hostReady": False, "placement": "placement: disconnected", "runtimeStatus": "INITIALIZING"},
                    "actors": [{"type": "AggregateActor", "count": 0}, {"type": secret}], "secret": secret}
        calls = []
        def response(url, **kwargs):
            calls.append(url)
            return io.BytesIO(json.dumps(health if url.endswith("/ready") else metadata).encode())
        with patch.object(fixture, "owned_http_open", response), \
                patch.object(fixture.urllib.request, "urlopen", side_effect=AssertionError("Default opener is forbidden")):
            result = fixture.gateway_readiness_observation(gateway, sidecar, marker, directory)
        self.assertEqual(["http://127.0.0.1:8082/ready", "http://127.0.0.1:3501/v1.0/metadata"], calls)
        self.assertTrue(result["http_endpoint_matches_port"])
        self.assertTrue(result["sidecar_identity_matches"])
        self.assertEqual("Unhealthy", result["gateway_ready"])
        self.assertEqual("NotReady", result["actor_host_ready"])
        self.assertEqual("Disconnected", result["actor_placement"])
        self.assertEqual("Initializing", result["actor_runtime"])
        self.assertTrue(result["aggregate_actor_registered"])
        self.assertNotIn(secret, json.dumps(result))
        metadata["id"] = "timesheets"
        with patch.object(fixture, "owned_http_open", response):
            wrong = fixture.gateway_readiness_observation(gateway, sidecar, marker, directory)
        self.assertEqual("WrongApplication", wrong["sidecar_metadata"])
        self.assertFalse(wrong["sidecar_identity_matches"])
        self.assertFalse(wrong["aggregate_actor_registered"])
        metadata["id"] = "eventstore"
        self.assertNotIn(marker, json.dumps(result))
        health["status"] = "Healthy"
        metadata["actorRuntime"].update(hostReady=True, placement="placement: connected", runtimeStatus="RUNNING")
        with patch.object(fixture, "owned_http_open", response):
            result = fixture.gateway_readiness_observation(gateway, sidecar, marker, directory)
        self.assertEqual(("Healthy", "Ready", "Connected", "Running"), tuple(result[name] for name in (
            "gateway_ready", "actor_host_ready", "actor_placement", "actor_runtime")))
        for field in ("placement", "runtimeStatus"):
            metadata["actorRuntime"][field] = secret
        with patch.object(fixture, "owned_http_open", response):
            result = fixture.gateway_readiness_observation(gateway, sidecar, marker, directory)
        self.assertEqual("Unknown", result["actor_placement"])
        self.assertEqual("Unknown", result["actor_runtime"])
        self.assertNotIn(secret, json.dumps(result))
        for status, body, expected in ((200, b"Healthy", "Healthy"), (200, b"Degraded", "Degraded"),
                                        (503, b"Unhealthy", "Unhealthy"), (200, b'"Healthy"', "Invalid"),
                                        (200, ("Healthy " + secret).encode(), "Invalid"),
                                        (200, json.dumps({"status": secret}).encode(), "Invalid")):
            def raw_response(url, **_kwargs):
                if url.endswith("/ready"):
                    if status == 503:
                        raise fixture.urllib.error.HTTPError(url, status, secret, {}, io.BytesIO(body))
                    response = io.BytesIO(body)
                    response.status = status
                    return response
                return io.BytesIO(json.dumps(metadata).encode())
            with self.subTest(status=status, expected=expected), patch.object(fixture, "owned_http_open", raw_response):
                result = fixture.gateway_readiness_observation(gateway, sidecar, marker, directory)
            self.assertEqual(expected, result["gateway_ready"])
            self.assertEqual(status, result["gateway_http_status"])
            self.assertNotIn(secret, json.dumps(result))

    def test_gateway_readiness_does_not_contact_unsafe_or_mismatched_sidecar_and_discards_raw_errors(self):
        directory = Path(self.temporary.name) / "proc"
        process = directory / "123"
        process.mkdir(parents=True)
        marker = fixture.secrets.token_hex(24)
        (process / "stat").write_text("123 (gateway) " + " ".join(["S", "1", *(["0"] * 17), "77"]))
        gateway = {"pid": 123, "start": "77"}
        sidecar = {"arguments": ["daprd", "--dapr-http-port", "3501"]}
        for endpoint, port in (("http://example.invalid:3501", "3501"), ("http://localhost:3502", "3501"),
                               ("http://localhost:3501", "3502"), ("http://localhost:3501?token=" + marker, "3501")):
            (process / "environ").write_bytes((f"TIMESHEETS_LIVE_DAPR_COMPONENTS={marker}\0DAPR_HTTP_PORT={port}\0"
                f"DAPR_HTTP_ENDPOINT={endpoint}\0").encode())
            with self.subTest(endpoint=endpoint), patch.object(fixture, "owned_http_open", side_effect=OSError(marker)) as opened:
                result = fixture.gateway_readiness_observation(gateway, sidecar, marker, directory)
                self.assertEqual(1, opened.call_count)
                self.assertEqual("http://127.0.0.1:8082/ready", opened.call_args.args[0])
            self.assertEqual("Unavailable", result["sidecar_metadata"])
            self.assertNotIn(marker, json.dumps(result))
        with patch.object(fixture, "owned_http_open") as opened:
            result = fixture.gateway_readiness_observation({"pid": 123, "start": "stale"}, sidecar, marker, directory)
            opened.assert_not_called()
        self.assertFalse(result["identity_matches"])

    def test_gateway_readiness_refuses_actual_redirects_and_proxy_environment_on_both_owned_origins(self):
        directory = Path(self.temporary.name) / "direct-readiness-proc"
        process = directory / "123"
        process.mkdir(parents=True)
        marker, private = fixture.secrets.token_hex(24), fixture.secrets.token_hex(24)
        (process / "stat").write_text("123 (gateway) " + " ".join(["S", "1", *(["0"] * 17), "77"]))
        (process / "environ").write_bytes((f"TIMESHEETS_LIVE_DAPR_COMPONENTS={marker}\0DAPR_HTTP_PORT=3501\0"
            "DAPR_HTTP_ENDPOINT=http://localhost:3501\0").encode())
        connection = fixture.urllib.request.http.client.HTTPConnection
        for status in (302, 307):
            hosts = []
            def redirect(handler):
                handler.send_response(status, private)
                handler.send_header("Location", "http://example.invalid/" + private)
                handler.end_headers()
                handler.wfile.write(private.encode())

            with self.subTest(status=status), self.owned_http_server(redirect) as origin:
                class DirectConnection(connection):
                    def __init__(self, host, *args, **kwargs):
                        # Substitute only the socket target so the test binds no
                        # fixture ports, while urllib handles real redirects.
                        hosts.append(host)
                        super().__init__(fixture.urllib.parse.urlsplit(origin).netloc, *args, **kwargs)
                poisoned = {"HTTP_PROXY": "http://proxy.invalid:19999", "http_proxy": "http://proxy.invalid:19999"}
                with patch.dict(os.environ, poisoned, clear=True), \
                        patch.object(fixture.urllib.request.http.client, "HTTPConnection", DirectConnection), \
                        patch.object(fixture.urllib.request, "getproxies", side_effect=AssertionError("Proxy lookup is forbidden")):
                    result = fixture.gateway_readiness_observation({"pid": 123, "start": "77"}, {}, marker, directory)
                self.assertEqual(["127.0.0.1:8082", "127.0.0.1:3501"], hosts)
                self.assertEqual(status, result["gateway_http_status"])
                self.assertEqual("Invalid", result["gateway_ready"])
                self.assertEqual("Unavailable", result["sidecar_metadata"])
                self.assertFalse(private in json.dumps(result))

    def test_cardinality_refusal_retains_gateway_readiness_and_diagnostic_write_failure_keeps_original_refusal(self):
        instance = fixture.IsolatedFixture(command=Commands(), log_directory=self.logs, resource_check=lambda *_: [])
        instance.scratch = fixture.secrets.token_hex(24)
        instance.environment.update({"TIMESHEETS_LIVE_DAPR_REDIS_HOST": "127.0.0.1:49152",
                                     fixture.GATEWAY_EXECUTABLE_SETTING: "/unused/gateway"})
        diagnostic = {"counts": {"eventstore": 0, "timesheets": 0, "other": 0}, "processes": [], "scan_complete": True}
        readiness = {"pid": 123, "start": "77", "gateway_ready": "Unhealthy", "sidecar_metadata": "Unavailable"}
        def refusal(*_args, observation, **_kwargs):
            observation(diagnostic)
            raise fixture.FixtureError("Fixture Dapr runtime identity is not unique")
        for write_fails in (False, True):
            with self.subTest(write_fails=write_fails), \
                    patch.object(fixture, "verify_runtime_components", side_effect=refusal), \
                    patch.object(fixture, "gateway_process_identity", return_value={"pid": 123, "start": "77"}), \
                    patch.object(fixture, "gateway_readiness_observation", return_value=readiness) as observe, \
                    patch.object(Path, "write_text", side_effect=OSError("private")) if write_fails else contextlib.nullcontext(), \
                    contextlib.redirect_stdout(io.StringIO()), \
                    self.assertRaisesRegex(fixture.FixtureError, "identity is not unique"):
                instance.capture_runtime_readiness()
            self.assertEqual({"arguments": []}, observe.call_args.args[1])
            if not write_fails:
                self.assertEqual(diagnostic, json.loads((instance.log_directory / "runtime-dapr-identity-failure.json").read_text()))
                self.assertEqual(readiness, json.loads((instance.log_directory / "runtime-readiness.json").read_text()))
            else:
                self.assertTrue(instance.evidence_write_failed)

    def bootstrap_instance(self):
        commands = Commands(lambda args: subprocess.CompletedProcess(args, 0, "0\n", "")
                            if args[:2] == ["docker", "exec"] and args[-1] == "DBSIZE" else None)
        instance = fixture.IsolatedFixture(command=commands, log_directory=self.logs, port_check=lambda: None)
        with contextlib.redirect_stdout(io.StringIO()):
            instance.start_infrastructure()
            instance.preflight()
        self.addCleanup(lambda: shutil.rmtree(instance.scratch) if Path(instance.scratch).exists() else None)
        source = {"head": "a" * 40, "diff_sha256": "b" * 64, "native_sha256": "c" * 64, "assembly_sha256": "d" * 64}
        patched = patch.object(instance, "fixture_cutover_source", return_value=source)
        patched.start()
        self.addCleanup(patched.stop)
        return instance, commands, source

    def test_fixture_backup_durably_certifies_all_empty_databases_and_exact_owned_identity_before_start(self):
        instance, commands, source = self.bootstrap_instance()
        with patch.object(fixture.os, "fsync", wraps=fixture.os.fsync) as flushed:
            instance.prepare_fixture_bootstrap()
        backup = instance.fixture_backup
        receipt = json.loads(backup["path"].read_text())
        self.assertEqual({str(index): 0 for index in range(16)}, receipt["databases"])
        self.assertEqual(instance.container_id, receipt["container_id"])
        self.assertEqual(commands.image, receipt["image"])
        self.assertEqual(source, receipt["source"])
        self.assertEqual("fresh-private-local-fixture-only", receipt["scope"])
        self.assertTrue(receipt["downgrade_prohibited"])
        self.assertEqual(2, flushed.call_count)
        self.assertEqual(backup["sha256"], fixture.hashlib.sha256(backup["path"].read_bytes()).hexdigest())
        self.assertFalse(commands.started)
        self.assertEqual(16, sum(args[-1] == "DBSIZE" for args, _ in commands.calls))

    def test_fixture_backup_refuses_nonempty_unadmitted_or_replaced_private_store(self):
        for defect in ("nonempty", "unadmitted", "replaced", "after-start"):
            instance, commands, _ = self.bootstrap_instance()
            if defect == "nonempty":
                commands.failure = lambda args: subprocess.CompletedProcess(args, 0, "1\n", "") if args[-1] == "DBSIZE" else None
            elif defect == "unadmitted":
                instance.admission_established = False
            elif defect == "replaced":
                instance.container_id = "e" * 64
            else:
                instance.start_attempted = True
            with self.subTest(defect=defect), self.assertRaises(fixture.FixtureError):
                instance.prepare_fixture_bootstrap()
            self.assertIsNone(instance.fixture_backup)
            self.assertFalse(commands.started)

    def test_fixture_protocol_bootstrap_uses_only_operator_api_with_empty_backup_and_current_source(self):
        for security in (False, True):
            instance, commands, source = self.bootstrap_instance()
            instance.security_smoke = security
            instance.prepare_fixture_bootstrap()
            instance.start_attempted = True
            sides = {"eventstore": {"pid": 123}, "timesheets": {"pid": 456}}
            with patch.object(fixture, "verify_runtime_components", return_value=sides), \
                    patch.object(instance, "resource_check", return_value=[]), \
                    patch.object(fixture, "wait_fixture_gateway_alive"), \
                    patch.object(fixture, "running_dapr_processes", return_value=[{"pid": 123}, {"pid": 456}]), \
                    patch.object(fixture, "gateway_process_identity", return_value={"pid": 789, "start": "77"}), \
                    patch.object(fixture, "fixture_gateway_signing_key", return_value=b"generated-key"), \
                    patch.object(fixture, "activate_fixture_writer_protocol") as activate:
                instance.environment[fixture.GATEWAY_EXECUTABLE_SETTING] = "/unused/gateway"
                instance.bootstrap_fixture_protocol()
            body = activate.call_args.args[1]
            self.assertEqual(source["head"], body["cutoverCommit"])
            self.assertIn(instance.fixture_backup["sha256"], body["backupReference"])
            self.assertTrue(all(body[name] is True for name in (
                "writersQuiesced", "retryWorkersQuiesced", "downgradeProhibitedAcknowledged")))
            self.assertTrue((instance.log_directory / "projection-writer-bootstrap.json").is_file())
            self.assertFalse(commands.started)

    def test_fixture_protocol_bootstrap_refuses_changed_backup_source_components_container_or_extra_sidecar(self):
        messages = {"backup": "Empty fixture backup identity changed", "source": "Fixture bootstrap source changed after its empty backup",
            "components": "Isolated component names, scopes, databases, or endpoint are unsafe", "container": "Fixture bootstrap identity changed after its empty backup",
            "extra-sidecar": "Fixture bootstrap found a sidecar outside the admitted private topology"}
        for defect in messages:
            instance, commands, source = self.bootstrap_instance()
            instance.prepare_fixture_bootstrap()
            instance.start_attempted = True
            instance.environment[fixture.GATEWAY_EXECUTABLE_SETTING] = "/unused/gateway"
            if defect == "backup":
                instance.fixture_backup["path"].write_text("{}")
            elif defect == "source":
                instance.fixture_cutover_source.return_value = {**source, "head": "e" * 40}
            elif defect == "components":
                (Path(instance.scratch) / "statestore.yaml").write_text("changed")
            elif defect == "container":
                instance.container_id = "e" * 64
            sides = {"eventstore": {"pid": 123}, "timesheets": {"pid": 456}}
            inventory = [{"pid": 123}, {"pid": 456}] + ([{"pid": 999}] if defect == "extra-sidecar" else [])
            with self.subTest(defect=defect), patch.object(fixture, "verify_runtime_components", return_value=sides), \
                    patch.object(instance, "resource_check", return_value=[]), \
                    patch.object(fixture, "wait_fixture_gateway_alive"), \
                    patch.object(fixture, "running_dapr_processes", return_value=inventory), \
                    patch.object(fixture, "gateway_process_identity", return_value={"pid": 789, "start": "77"}), \
                    patch.object(fixture, "fixture_gateway_signing_key", return_value=b"generated-key"), \
                    patch.object(fixture, "activate_fixture_writer_protocol") as activate, \
                    self.assertRaisesRegex(fixture.FixtureError, "^" + messages[defect] + "$"):
                instance.bootstrap_fixture_protocol()
            activate.assert_not_called()
            self.assertFalse(json.loads((instance.log_directory / "projection-writer-bootstrap.json").read_text())["activated"])
            self.assertFalse(any(args[-1] in ("SET", "HSET", "FLUSHALL") for args, _ in commands.calls))

    def test_bootstrap_reads_only_current_fixture_bound_generated_development_signing_key(self):
        directory = Path(self.temporary.name) / "auth-proc"
        process = directory / "123"
        process.mkdir(parents=True)
        (process / "stat").write_text("123 (gateway) " + " ".join(["S", "1", *(["0"] * 17), "77"]))
        marker, key = fixture.secrets.token_hex(12), fixture.secrets.token_hex(32).upper()
        environment = {"TIMESHEETS_LIVE_DAPR_COMPONENTS": marker, "ASPNETCORE_ENVIRONMENT": "Development",
                       "Authentication__JwtBearer__Authority": "", "Authentication__JwtBearer__Issuer": "hexalith-timesheets-dev",
                       "Authentication__JwtBearer__Audience": "hexalith-eventstore", "Authentication__JwtBearer__AllowedAlgorithms__0": "HS256",
                       "Authentication__JwtBearer__SigningKey": key}
        def write(values):
            (process / "environ").write_bytes("\0".join(name + "=" + value for name, value in values.items()).encode())
        write(environment)
        self.assertEqual(key.encode(), fixture.fixture_gateway_signing_key({"pid": 123, "start": "77"}, marker, directory))
        for name in environment:
            write({**environment, name: "unsafe"})
            with self.subTest(name=name), self.assertRaises(fixture.FixtureError) as failed:
                fixture.fixture_gateway_signing_key({"pid": 123, "start": "77"}, marker, directory)
            self.assertNotIn(key, str(failed.exception))
        write(environment)
        with self.assertRaises(fixture.FixtureError):
            fixture.fixture_gateway_signing_key({"pid": 123, "start": "stale"}, marker, directory)

    def test_operator_activation_uses_short_lived_signed_bearer_and_never_retains_token_or_raw_response(self):
        key = fixture.secrets.token_hex(32).encode()
        body = {"cutoverCommit": "a" * 40, "backupReference": "empty-fixture-backup#sha256=" + "b" * 64,
                "writersQuiesced": True, "retryWorkersQuiesced": True, "downgradeProhibitedAcknowledged": True}
        diagnostic, tokens = {}, []
        def opened(request, timeout):
            self.assertEqual(5, timeout)
            self.assertEqual("POST", request.method)
            self.assertEqual("http://127.0.0.1:8082/api/v1/admin/projections/delivery-writer-protocol/activate", request.full_url)
            self.assertEqual(body, json.loads(request.data))
            self.assertFalse(any("assertion" in name.lower() for name in request.headers))
            token = request.get_header("Authorization").removeprefix("Bearer ")
            tokens.append(token)
            header, payload, signature = token.split(".")
            decoded = json.loads(fixture.base64.urlsafe_b64decode(payload + "=="))
            self.assertEqual("GlobalAdministrator", decoded["role"])
            self.assertEqual("hexalith-eventstore", decoded["aud"])
            self.assertEqual("hexalith-timesheets-dev", decoded["iss"])
            self.assertEqual(60, decoded["exp"] - decoded["iat"])
            expected = fixture.hmac.new(key, (header + "." + payload).encode(), fixture.hashlib.sha256).digest()
            self.assertEqual(expected, fixture.base64.urlsafe_b64decode(signature + "=="))
            response = io.BytesIO(b'{"status":"Activated"}')
            response.status = 200
            return response
        opener = MagicMock()
        opener.open.side_effect = opened
        with patch.object(fixture.urllib.request, "build_opener", return_value=opener) as built:
            fixture.activate_fixture_writer_protocol(key, body, diagnostic)
        self.assertTrue(diagnostic["activated"])
        handler = built.call_args.args[0]
        self.assertIsNone(handler.redirect_request(None, None, 302, "private", {}, "http://example.invalid"))
        self.assertNotIn(tokens[0], json.dumps(diagnostic))
        self.assertNotIn(key.decode(), json.dumps(diagnostic))

    def test_bootstrap_waits_for_actual_http_liveness_with_a_bound_and_without_readiness_or_response_content(self):
        private = fixture.secrets.token_hex(24)
        ready = io.BytesIO(private.encode())
        ready.status = 200
        opener = MagicMock()
        opener.open.side_effect = [OSError(private), fixture.urllib.error.HTTPError(
            "http://127.0.0.1:8082/alive", 503, private, {}, io.BytesIO(private.encode())), ready]
        diagnostic = {}
        with patch.object(fixture.urllib.request, "build_opener", return_value=opener), patch.object(fixture.time, "sleep"):
            fixture.wait_fixture_gateway_alive(diagnostic)
        self.assertEqual("alive", diagnostic["liveness"]["category"])
        self.assertEqual(3, diagnostic["liveness"]["attempts"])
        self.assertEqual(200, diagnostic["liveness"]["http_status"])
        self.assertNotIn(private, json.dumps(diagnostic))
        self.assertTrue(all(call.args == ("http://127.0.0.1:8082/alive",) for call in opener.open.call_args_list))
        opener.open.side_effect = OSError(private)
        diagnostic = {}
        ticks = iter(range(100))
        with patch.object(fixture.urllib.request, "build_opener", return_value=opener), \
                patch.object(fixture.time, "sleep"), patch.object(fixture.time, "monotonic", side_effect=lambda: next(ticks)), \
                self.assertRaisesRegex(fixture.FixtureError, "liveness.*bound"):
            fixture.wait_fixture_gateway_alive(diagnostic, timeout=3)
        self.assertEqual("transport_unavailable", diagnostic["liveness"]["category"])
        self.assertNotIn(private, json.dumps(diagnostic))

    def test_operator_activation_fails_closed_on_auth_conflict_redirect_invalid_response_and_bounded_outage(self):
        private = fixture.secrets.token_hex(24)
        for code, expected in ((400, "preconditions_refused"), (401, "authentication_refused"), (403, "authorization_refused"),
                               (409, "conflict"), (302, "http_refused"), (503, "storage_unavailable"), (200, "invalid_response")):
            opener = MagicMock()
            def opened(*_args, **_kwargs):
                raise fixture.urllib.error.HTTPError("http://127.0.0.1:8082", code, private, {}, io.BytesIO(private.encode()))
            opener.open.side_effect = opened
            diagnostic = {}
            with self.subTest(code=code), patch.object(fixture.urllib.request, "build_opener", return_value=opener), \
                    patch.object(fixture.time, "sleep"), self.assertRaises(fixture.FixtureError) as failure:
                fixture.activate_fixture_writer_protocol(b"generated-key", {}, diagnostic)
            self.assertEqual(expected, diagnostic["category"])
            self.assertEqual(5 if code == 503 else 1, opener.open.call_count)
            self.assertNotIn(private, json.dumps(diagnostic) + str(failure.exception))
        opener.open.side_effect = OSError(private)
        diagnostic = {}
        with patch.object(fixture.urllib.request, "build_opener", return_value=opener), \
                patch.object(fixture.time, "sleep"), self.assertRaises(fixture.FixtureError):
            fixture.activate_fixture_writer_protocol(b"generated-key", {}, diagnostic)
        self.assertEqual("transport_unavailable", diagnostic["category"])
        self.assertEqual(5, diagnostic["attempts"])
        self.assertNotIn(private, json.dumps(diagnostic))

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
            result = self.new_shared_snapshot()
            result["scheduler"] = {"revision": "1", "keys": 1}
            result.persistent["0"] = {"persistent-key": "value"}
            result.expiring["0"] = expiring
            result.captured_at["0"] = captured
            self.update_shared_counts(result)
            return result
        before = snapshot({"key-a": ("value-a", 1000)}, 900)
        self.assertTrue(fixture.shared_preservation(before, snapshot({}, 1001))["matches"])
        self.assertFalse(fixture.shared_preservation(before, snapshot({}, 999))["matches"])
        for altered in ({"key-a": ("value-b", 1000)}, {"key-a": ("value-a", 1100)},
                        {"key-a": ("value-a", 1000), "new-key": ("new-value", 2000)}):
            self.assertFalse(fixture.shared_preservation(before, snapshot(altered, 950))["matches"])
        after = snapshot({"key-a": ("value-a", 1001)}, 950)
        self.assertTrue(fixture.shared_preservation(before, after)["matches"])
        after.persistent["0"]["persistent-key"] = "changed"
        self.assertFalse(fixture.shared_preservation(before, after)["matches"])

    def shared_snapshot(self):
        snapshot = self.new_shared_snapshot()
        snapshot.persistent["0"] = {"persistent-key": "value"}
        snapshot.expiring["0"] = {"PRIVATE-KEY": ("PRIVATE-VALUE", 1000)}
        self.update_shared_counts(snapshot)
        snapshot["scheduler"] = {"revision": "3", "keys": 2}
        return snapshot

    def test_preservation_reports_actual_changed_services_without_keys_or_values(self):
        before, after = self.shared_snapshot(), self.shared_snapshot()
        after.persistent["0"].update({f"new-{index}": "value" for index in range(14)})
        self.update_shared_counts(after)
        after["scheduler"]["revision"] = "17"
        after.scheduler_content = self.comparison.fingerprint(b"scheduler-content", b"changed")
        result = fixture.shared_preservation(before, after)
        self.assertFalse(result["matches"])
        self.assertEqual(["Redis DB 0 persistent content changed", "Scheduler contents changed"], result["mismatches"])
        self.assertEqual({"matches": False, "keys_before": 1, "keys_after": 15, "added": 14, "removed": 0, "value_changes": 0}, result["redis"]["0"]["persistent"])
        self.assertTrue(result["scheduler"]["identity_matches"])
        self.assertFalse(result["scheduler"]["contents_match"])
        self.assertEqual("3", result["scheduler"]["revision_before"])
        self.assertEqual("17", result["scheduler"]["revision_after"])
        self.assertNotIn("PRIVATE-KEY", json.dumps(result))
        self.assertNotIn("PRIVATE-VALUE", json.dumps(result))

    def test_preservation_reports_expiring_mutations_and_confirmed_expiry_separately(self):
        before, after = self.shared_snapshot(), self.shared_snapshot()
        before.expiring["0"].update({"expired": ("old", 910), "missing": ("old", 2000)})
        after.captured_at["0"] = 950
        after.expiring["0"] = {"PRIVATE-KEY": ("changed", 1100), "added": ("new", 2000)}
        self.update_shared_counts(before)
        self.update_shared_counts(after)
        result = fixture.shared_preservation(before, after)
        self.assertFalse(result["matches"])
        self.assertEqual(["Redis DB 0 expiring content or deadlines changed"], result["mismatches"])
        self.assertEqual({"matches": False, "added": 1, "missing_unexpired": 1,
                          "value_changes": 1, "deadline_changes": 1, "confirmed_natural_expirations": 1},
                         result["redis"]["0"]["expiring"])
        self.assertEqual({"0": 1, "13": 0, "14": 0}, result["confirmed_natural_expirations"])

    def test_preservation_counts_offsetting_persistent_differences_and_same_count_update(self):
        rows = {0: [["removed", "one", -1], ["updated", "old", -1], ["retained", "fixed", -1]], 13: [], 14: []}
        before, _ = self.read_shared_rows(rows)
        for replacement, expected in (([["added", "new", -1], ["updated", "changed", -1], ["retained", "fixed", -1]],
                                       {"added": 1, "removed": 1, "value_changes": 1}),
                                      ([["removed", "one", -1], ["updated", "changed", -1], ["retained", "fixed", -1]],
                                       {"added": 0, "removed": 0, "value_changes": 1})):
            after, _ = self.read_shared_rows(rows | {0: replacement})
            report = fixture.shared_preservation(before, after)
            self.assertFalse(report["matches"])
            self.assertTrue(report["redis_server"]["identity_matches"])
            self.assertEqual({"matches": False, "keys_before": 3, "keys_after": 3, **expected},
                             report["redis"]["0"]["persistent"])
            self.assertEqual(["Redis DB 0 persistent content changed"], report["mismatches"])

    def test_preservation_reports_ttl_transitions_without_allowing_expiry_or_double_counting(self):
        for old_ttl, new_ttl, direction in ((-1, 2000, "persistent_to_expiring"),
                                            (2000, -1, "expiring_to_persistent"),
                                            (900, -1, "expiring_to_persistent")):
            with self.subTest(old_ttl=old_ttl, new_ttl=new_ttl):
                before, _ = self.read_shared_rows({0: [["key", "value", old_ttl]], 13: [], 14: []})
                after, _ = self.read_shared_rows({0: [["key", "value", new_ttl]], 13: [], 14: []})
                report = fixture.shared_preservation(before, after)
                self.assertFalse(report["matches"])
                self.assertEqual(1, report["redis"]["0"]["ttl_transitions"][direction])
                self.assertEqual(1, sum(report["redis"]["0"]["ttl_transitions"].values()))
                self.assertEqual({"0": 0, "13": 0, "14": 0}, report["confirmed_natural_expirations"])
                self.assertEqual(0, report["redis"]["0"]["persistent"]["added"])
                self.assertEqual(0, report["redis"]["0"]["persistent"]["removed"])
                self.assertEqual(0, report["redis"]["0"]["expiring"]["added"])
                self.assertEqual(0, report["redis"]["0"]["expiring"]["missing_unexpired"])
                self.assertFalse(report["redis"]["0"]["expiring"]["matches"])

    def test_preservation_distinguishes_server_replacement_even_when_contents_match(self):
        rows = {0: [["key", "value", -1]], 13: [], 14: []}
        before, _ = self.read_shared_rows(rows)
        identical, _ = self.read_shared_rows(rows)
        self.assertTrue(fixture.shared_preservation(before, identical)["matches"])
        after, _ = self.read_shared_rows(rows, server=b"run_id:" + b"b" * 40 + b"\r\n")
        report = fixture.shared_preservation(before, after)
        self.assertFalse(report["matches"])
        self.assertFalse(report["redis_server"]["identity_matches"])
        self.assertTrue(report["redis"]["0"]["persistent"]["matches"])
        self.assertEqual(["Redis server identity changed"], report["mismatches"])

    def test_preservation_refuses_missing_identity_private_coverage_and_comparison_scope(self):
        for change in (lambda snapshot: setattr(snapshot, "redis_identity", None),
                       lambda snapshot: setattr(snapshot, "scheduler_identity", None),
                       lambda snapshot: setattr(snapshot, "scheduler_content", None),
                       lambda snapshot: setattr(snapshot, "comparison", fixture.SharedComparison()),
                       lambda snapshot: snapshot["redis"].clear(),
                       lambda snapshot: snapshot["redis"].pop("14"),
                       lambda snapshot: snapshot["redis"]["0"].update(keys=999),
                       lambda snapshot: snapshot.expiring["0"].clear(),
                       lambda snapshot: snapshot.persistent.clear(),
                       lambda snapshot: snapshot.persistent["0"].clear()):
            before, after = self.shared_snapshot(), self.shared_snapshot()
            change(after)
            with self.assertRaises(fixture.FixtureError):
                fixture.shared_preservation(before, after)

    def test_snapshot_reader_refuses_unavailable_malformed_or_duplicate_server_identity(self):
        for server in (b"", b"# Server\r\n", b"run_id:PRIVATE\r\n", b"run_id:" + b"a" * 39 + b"\r\n",
                       b"run_id:" + b"a" * 40 + b"\r\nrun_id:" + b"b" * 40 + b"\r\n",
                       b"run_id:" + b"a" * 40 + b"\r\nrun_id:invalid\r\n"):
            connection = RedisSnapshotConnection({}, server)
            with self.subTest(server=server), \
                    patch.object(fixture.socket, "create_connection", return_value=connection), \
                    patch.object(fixture.urllib.request, "urlopen", side_effect=AssertionError("Unexpected scheduler read")), \
                    self.assertRaises(fixture.FixtureError) as failure:
                fixture.read_only_shared_snapshot(self.comparison)
            self.assertNotIn("PRIVATE", str(failure.exception))
            self.assertEqual([[b"INFO", b"server"]], connection.arguments)

    def test_snapshot_reader_refuses_invalid_or_duplicate_digest_rows(self):
        key, value = "a" * 40, "b" * 40
        for rows in ([[key, value, -2]], [[key, value, -1], [key, value, 2000]],
                     [["private-key", value, -1]], [[key, "private-value", -1]]):
            with self.subTest(rows=rows), self.assertRaises(fixture.FixtureError):
                self.read_shared_rows({0: rows, 13: [], 14: []}, digest_rows=False)

    def test_snapshot_requires_explicit_scope_before_any_service_read(self):
        with patch.object(fixture.socket, "create_connection", side_effect=AssertionError("Unexpected Redis read")), \
                patch.object(fixture.urllib.request, "urlopen", side_effect=AssertionError("Unexpected scheduler read")):
            with self.assertRaises(TypeError):
                fixture.read_only_shared_snapshot()
            with self.assertRaises(fixture.FixtureError):
                fixture.read_only_shared_snapshot(None)

    def test_snapshot_preserves_aggregate_observation_times_for_natural_expiry(self):
        before, _ = self.read_shared_rows({0: [["expiring", "value", 900]], 13: [], 14: []})
        after, _ = self.read_shared_rows({0: [], 13: [], 14: []})
        report = fixture.shared_preservation(before, after)
        self.assertTrue(report["matches"])
        self.assertEqual(1, report["confirmed_natural_expirations"]["0"])
        for snapshot in (before, after):
            self.assertTrue(snapshot["observation"]["completed"])
            self.assertLessEqual(snapshot["observation"]["started_unix_ns"], snapshot["observation"]["completed_unix_ns"])
            for database in ("0", "13", "14"):
                self.assertEqual(1000, snapshot["redis"][database]["captured_at_unix_ms"])
                self.assertEqual(1000, report["redis"][database]["captured_before_unix_ms"])
                self.assertEqual(1000, report["redis"][database]["captured_after_unix_ms"])
        self.assertEqual(before["observation"], report["observation"]["before"])
        self.assertEqual(after["observation"], report["observation"]["after"])

    def test_failed_observations_retain_safe_phase_and_category_without_retry(self):
        for mode in ("connection", "identity", "database", "scheduler", "interrupt"):
            with self.subTest(mode=mode):
                connection = RedisSnapshotConnection({0: [], 13: [], 14: []})
                sendall = connection.sendall
                def send(data):
                    if (mode == "identity" and b"INFO" in data) or (mode == "database" and b"EVAL" in data):
                        raise TimeoutError("PRIVATE response or endpoint")
                    sendall(data)
                connection.sendall = send
                instance = fixture.IsolatedFixture(log_directory=self.logs)
                with patch.object(fixture.socket, "create_connection", side_effect=TimeoutError("PRIVATE")
                                  if mode == "connection" else lambda *_args, **_kwargs: connection) as connect, \
                        patch.object(fixture.urllib.request, "urlopen", side_effect=KeyboardInterrupt()
                                     if mode == "interrupt" else TimeoutError("PRIVATE scheduler")) as urlopen:
                    with self.assertRaises(KeyboardInterrupt if mode == "interrupt" else fixture.SharedObservationError):
                        instance.capture_shared_snapshot()
                receipt = json.loads((instance.log_directory / "shared-observation-failure.json").read_text())
                phase = {"connection": "redis_connection", "identity": "redis_identity", "database": "redis_database",
                         "scheduler": "scheduler_range", "interrupt": "scheduler_range"}[mode]
                self.assertFalse(receipt["completed"])
                self.assertEqual(phase, receipt["phase"])
                self.assertEqual(0 if mode == "database" else None, receipt["database"])
                self.assertEqual("KeyboardInterrupt" if mode == "interrupt" else "TimeoutError", receipt["exception_category"])
                self.assertLessEqual(receipt["started_unix_ns"], receipt["completed_unix_ns"])
                self.assertNotIn("PRIVATE", json.dumps(receipt))
                self.assertIsNone(instance.shared_before)
                self.assertFalse(instance.start_attempted)
                self.assertFalse(instance.containers)
                self.assertEqual(1, connect.call_count)
                self.assertEqual(1 if mode in ("scheduler", "interrupt") else 0, urlopen.call_count)

    def test_private_fingerprints_are_run_scoped_domain_separated_and_never_serialized(self):
        rows = {0: [["PRIVATE-key", "PRIVATE-value", -1], ["PRIVATE-ttl", "PRIVATE-expiring", 10000]], 13: [], 14: []}
        before, connection = self.read_shared_rows(rows)
        after, _ = self.read_shared_rows(rows)
        other, _ = self.read_shared_rows(rows, comparison=fixture.SharedComparison())
        self.assertEqual(before.persistent, after.persistent)
        self.assertNotEqual(before.persistent, other.persistent)
        self.assertNotEqual(self.comparison.fingerprint(b"redis-key", b"same"),
                            self.comparison.fingerprint(b"redis-value", b"same"))
        report = fixture.shared_preservation(before, after)
        serialized = json.dumps([before, after, report]) + repr(before) + repr(after)
        for private in ("PRIVATE", self.comparison._key.hex(), before.redis_identity.hex(),
                        before.scheduler_identity.hex(), before.scheduler_content.hex(),
                        next(iter(before.persistent["0"])).hex(), next(iter(before.persistent["0"].values())).hex(),
                        fixture.hashlib.sha1(b"PRIVATE-key").hexdigest(), fixture.hashlib.sha1(b"PRIVATE-value").hexdigest(),
                        "persistent_sha256", "sha256", "cluster_id"):
            self.assertNotIn(private, serialized)
        self.assertNotIn(self.comparison._key, b"".join(b"".join(args) for args in connection.arguments))
        with self.assertRaises(fixture.FixtureError):
            fixture.shared_preservation(before, other)

    def test_preflight_refuses_standalone_dapr_apps_and_unverifiable_json(self):
        for output in ('[{"appId":"standalone-other"}]', '{}', 'null', '', 'not-json []'):
            with self.subTest(output=output):
                commands = Commands(lambda args: subprocess.CompletedProcess(args, 0, output, "")
                                    if args[:2] == ["dapr", "list"] else None)
                instance, result = self.run_fixture(commands)
                self.assertEqual(1, result)
                self.assertFalse(commands.started)
                self.assertFalse(commands.stopped)
                self.assertFalse(commands.containers)
                self.assertIn(["dapr", "list", "--output", "json"], [args for args, _ in commands.calls])
                self.assertTrue(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])

    def test_unstable_baseline_refuses_live_and_guard_resources_before_creation(self):
        before, after = self.shared_snapshot(), self.shared_snapshot()
        after.persistent["0"]["persistent-key"] = "changed"
        for mode in ("live", "guards"):
            with self.subTest(mode=mode):
                commands = self.guard_commands() if mode == "guards" else Commands()
                instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs)
                instance.injected_command = False
                reader_owner = guards if mode == "guards" else fixture
                with patch.object(reader_owner, "read_only_shared_snapshot", side_effect=(before, after)), \
                        patch.object(fixture.time, "sleep"), contextlib.redirect_stdout(io.StringIO()) as output:
                    if mode == "guards":
                        with patch.object(guards, "IsolatedFixture", return_value=instance):
                            result = guards.run_guards()
                    else:
                        result = instance.execute()
                self.assertEqual(1, result)
                self.assertFalse(commands.started)
                self.assertFalse(commands.stopped)
                self.assertFalse(commands.containers)
                self.assertIsNone(instance.scratch)
                self.assertIn("before fixture startup", output.getvalue())
                report = json.loads((instance.log_directory / "shared-baseline-preservation.json").read_text())
                self.assertFalse(report["matches"])
                self.assertTrue((instance.log_directory / "shared-baseline-before.json").is_file())
                self.assertTrue((instance.log_directory / "shared-before.json").is_file())

    def test_stable_baseline_selects_latest_observation_and_requires_evidence(self):
        before, after = self.shared_snapshot(), self.shared_snapshot()
        after.captured_at["0"] = 950
        instance = fixture.IsolatedFixture(log_directory=self.logs)
        with patch.object(fixture, "read_only_shared_snapshot", side_effect=(before, after)) as reader, \
                patch.object(fixture.time, "sleep") as pause, \
                patch.object(fixture.time, "time_ns", side_effect=(1000000000, 2250000000)), \
                patch.object(fixture.time, "monotonic", side_effect=(10, 11.25)):
            instance.capture_shared_baseline()
        pause.assert_called_once_with(1)
        self.assertIs(after, instance.shared_before)
        self.assertEqual([((instance.shared_comparison,), {})] * 2, reader.call_args_list)
        report = json.loads((instance.log_directory / "shared-baseline-preservation.json").read_text())
        self.assertTrue(report["matches"])
        self.assertEqual({"before_unix_ns": 1000000000, "after_unix_ns": 2250000000,
                          "elapsed_seconds": 1.25, "requested_pause_seconds": 1}, report["observation"])
        for failed_name in ("shared-baseline-before.json", "shared-before.json", "shared-baseline-preservation.json"):
            with self.subTest(failed_name=failed_name):
                refused = fixture.IsolatedFixture(log_directory=self.logs)
                original_write = refused.write_evidence
                refused.write_evidence = lambda name, content: False if name == failed_name else original_write(name, content)
                with patch.object(fixture, "read_only_shared_snapshot", return_value=before), \
                        patch.object(fixture.time, "sleep"), self.assertRaises(fixture.FixtureError):
                    refused.capture_shared_baseline()
                self.assertIsNone(refused.shared_before)

    def test_guard_refuses_a_new_foreign_apphost_without_blame_or_foreign_stop(self):
        commands = self.guard_commands()
        probe = commands.failure
        checks = 0
        def appear(arguments):
            nonlocal checks
            if arguments[:2] == ["aspire", "ps"]:
                checks += 1
                if checks > 3:
                    return subprocess.CompletedProcess(arguments, 0,
                        '[{"appHostPath":"/foreign/Other.AppHost.csproj"}]', "")
            return probe(arguments)
        commands.failure = appear
        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs)
        executable = fixture.APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
        original_is_file = Path.is_file
        with patch.object(guards, "IsolatedFixture", return_value=instance), \
                patch.object(guards, "read_only_shared_snapshot", return_value=self.shared_snapshot()), \
                patch.object(fixture.time, "sleep"), \
                patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(1, guards.run_guards())
        self.assertIn("An AppHost appeared during guard checks", output.getvalue())
        self.assertNotIn("probe constructed", output.getvalue())
        self.assertFalse(commands.started)
        self.assertFalse(commands.stopped)
        self.assertFalse(commands.containers)
        self.assertTrue(json.loads((instance.log_directory / "shared-preservation.json").read_text())["matches"])

    def test_baseline_rejects_revision_activity_even_when_final_contents_match(self):
        before, after = self.shared_snapshot(), self.shared_snapshot()
        after["scheduler"]["revision"] = "4"
        self.assertTrue(fixture.shared_preservation(before, after)["matches"])
        instance = fixture.IsolatedFixture(log_directory=self.logs)
        with patch.object(fixture, "read_only_shared_snapshot", side_effect=(before, after)), \
                patch.object(fixture.time, "sleep"), self.assertRaises(fixture.FixtureError):
            instance.capture_shared_baseline()
        report = json.loads((instance.log_directory / "shared-baseline-preservation.json").read_text())
        self.assertFalse(report["matches"])
        self.assertFalse(report["scheduler"]["baseline_revision_matches"])
        self.assertEqual(["Scheduler revision changed during baseline observation"], report["mismatches"])
        self.assertIsNone(instance.shared_before)

    def test_runtime_arriving_during_baseline_refuses_infrastructure_creation(self):
        for mode in ("live", "guards"):
            with self.subTest(mode=mode):
                commands = self.guard_commands() if mode == "guards" else Commands()
                original_probe = commands.failure
                checks = 0
                def appear(arguments):
                    nonlocal checks
                    if arguments[:2] == ["aspire", "ps"]:
                        checks += 1
                        if checks > 1:
                            return subprocess.CompletedProcess(arguments, 0, '[{"appHostPath":"/foreign/AppHost.csproj"}]', "")
                    return original_probe(arguments) if original_probe else None
                commands.failure = appear
                instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs)
                instance.injected_command = False
                reader_owner = guards if mode == "guards" else fixture
                with patch.object(reader_owner, "read_only_shared_snapshot", return_value=self.shared_snapshot()), \
                        patch.object(fixture.time, "sleep"), contextlib.redirect_stdout(io.StringIO()):
                    if mode == "guards":
                        with patch.object(guards, "IsolatedFixture", return_value=instance):
                            result = guards.run_guards()
                    else:
                        result = instance.execute()
                self.assertEqual(1, result)
                self.assertIsNone(instance.scratch)
                self.assertFalse(commands.redis_attempted)
                self.assertFalse(commands.started)
                self.assertFalse(commands.stopped)

    def test_native_dapr_inventory_refuses_visible_unlisted_and_unverifiable_sidecars(self):
        directory = Path(self.temporary.name) / "proc"
        process = directory / "123"
        process.mkdir(parents=True)
        (process / "comm").write_text("daprd\n")
        (process / "stat").write_text("123 (daprd) S " + " ".join(["0"] * 18 + ["77"]))
        self.assertEqual([{"pid": 123, "start": "77"}], self.native_dapr_inventory(directory))
        commands = Commands()
        with patch.object(fixture, "running_dapr_processes", side_effect=lambda: self.native_dapr_inventory(directory)):
            _, result = self.run_fixture(commands)
        self.assertEqual(1, result)
        self.assertFalse(commands.started)
        self.assertFalse(commands.redis_attempted)
        (process / "stat").write_text("123 (daprd) Z " + " ".join(["0"] * 18 + ["77"]))
        self.assertEqual([], self.native_dapr_inventory(directory))
        (process / "stat").write_text("malformed")
        with self.assertRaises(fixture.FixtureError):
            self.native_dapr_inventory(directory)
        with patch.object(Path, "read_text", side_effect=PermissionError()), self.assertRaises(fixture.FixtureError):
            self.native_dapr_inventory(directory)

    def test_native_dapr_inventory_detects_a_real_process_without_cli_metadata(self):
        script = "import ctypes,time;assert ctypes.CDLL(None).prctl(15,b'daprd',0,0,0)==0;print('ready',flush=True);time.sleep(60)"
        process = subprocess.Popen([sys.executable, "-c", script], stdout=subprocess.PIPE, text=True)
        try:
            self.assertEqual("ready\n", process.stdout.readline())
            self.assertIn(process.pid, [row["pid"] for row in self.native_dapr_inventory()])
            with patch.object(fixture, "running_dapr_processes", self.native_dapr_inventory):
                commands = Commands()
                _, result = self.run_fixture(commands)
            self.assertEqual(1, result)
            self.assertFalse(commands.started)
            self.assertFalse(commands.redis_attempted)
        finally:
            process.terminate()
            process.wait(timeout=5)
            process.stdout.close()

    def test_guard_final_mutation_is_reported_after_success_or_existing_failure(self):
        for failed in (False, True):
            with self.subTest(failed=failed):
                before, after = self.shared_snapshot(), self.shared_snapshot()
                after.scheduler_content = self.comparison.fingerprint(b"scheduler-content", b"changed")
                commands = self.guard_commands(failed)
                instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs)
                executable = fixture.APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
                original_is_file = Path.is_file
                with patch.object(guards, "IsolatedFixture", return_value=instance), \
                        patch.object(guards, "read_only_shared_snapshot", side_effect=(before, before, after)), \
                        patch.object(fixture.time, "sleep"), \
                        patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                        contextlib.redirect_stdout(io.StringIO()) as output:
                    self.assertEqual(1, guards.run_guards())
                self.assertIn("Scheduler contents changed", output.getvalue())
                if failed:
                    self.assertIn("selection was not verified", output.getvalue())
                self.assertFalse(commands.containers)
                self.assertFalse(Path(instance.scratch).exists())
                self.assertFalse(json.loads((instance.log_directory / "shared-preservation.json").read_text())["matches"])

    def test_final_mutation_diagnostics_keep_original_journey_result(self):
        for original_exit in (0, 42):
            with self.subTest(original_exit=original_exit):
                before, after = self.shared_snapshot(), self.shared_snapshot()
                after.scheduler_content = self.comparison.fingerprint(b"scheduler-content", b"changed")
                commands = Commands(lambda args: subprocess.CompletedProcess(args, original_exit,
                    "Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0", "") if "-class" in args else None)
                instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs,
                    ownership_check=lambda: commands.started and not commands.stopped, resource_check=lambda *_: [])
                instance.injected_command = False
                with patch.object(instance, "_journey_command", commands), \
                        patch.object(instance, "capture_gateway_process", side_effect=self.gateway_identities()), \
                        patch.object(fixture, "read_only_shared_snapshot", side_effect=(before, before, after)), \
                        patch.object(fixture.time, "sleep"), patch.object(fixture, "verify_runtime_components"), patch.object(instance, "capture_runtime_readiness"), \
                            patch.object(instance, "prepare_fixture_bootstrap"), patch.object(instance, "bootstrap_fixture_protocol"), \
                        contextlib.redirect_stdout(io.StringIO()) as output:
                    self.assertEqual(original_exit or 1, instance.execute())
                self.assertIn("Scheduler contents changed", output.getvalue())
                self.assertFalse(commands.containers)
                self.assertFalse(Path(instance.scratch).exists())

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
        snapshot = self.new_shared_snapshot()
        snapshot["scheduler"] = {"revision": "1", "keys": 0}
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
                            patch.object(instance, "capture_gateway_process", side_effect=self.gateway_identities()), \
                            patch.object(fixture, "read_only_shared_snapshot", return_value=snapshot), \
                            patch.object(fixture, "verify_runtime_components"), patch.object(instance, "capture_runtime_readiness"), \
                            patch.object(instance, "prepare_fixture_bootstrap"), patch.object(instance, "bootstrap_fixture_protocol"), \
                            patch.object(Path, "write_text", write), contextlib.redirect_stdout(io.StringIO()):
                        self.assertEqual(original_exit or 1, instance.execute())
                    self.assertTrue(instance.evidence_write_failed)
                    self.assertTrue(json.loads((instance.log_directory / "cleanup.json").read_text())["complete"])
                    self.assertFalse(Path(instance.scratch).exists())

    def test_guard_late_preservation_write_failure_cannot_return_success(self):
        snapshot = self.new_shared_snapshot()
        snapshot["scheduler"] = {"revision": "1", "keys": 0}
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
        header = {"cluster_id": "1", "member_id": "2", "revision": "3", "raft_term": "1"}
        for response in (b'{"error":"unavailable"}', b'{"header":{},"count":"0"}',
                         b'{"count":"0","kvs":[]}', b'not-json'):
            with self.subTest(response=response), \
                    patch.object(fixture.socket, "create_connection", return_value=RedisSnapshotConnection({0: [], 13: [], 14: []})), \
                    patch.object(fixture.urllib.request, "urlopen", return_value=io.BytesIO(response)):
                with self.assertRaises(fixture.FixtureError):
                    fixture.read_only_shared_snapshot(self.comparison)
        with patch.object(fixture.socket, "create_connection", return_value=RedisSnapshotConnection({0: [], 13: [], 14: []})), \
                patch.object(fixture.urllib.request, "urlopen", return_value=io.BytesIO(json.dumps({"header": header}).encode())):
            snapshot = fixture.read_only_shared_snapshot(self.comparison)
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
        snapshot = self.new_shared_snapshot()
        snapshot["scheduler"] = {"revision": "1", "keys": 0}
        for original_exit in (0, 42):
            with self.subTest(original_exit=original_exit):
                commands = Commands(lambda args: subprocess.CompletedProcess(args, original_exit,
                    "Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0", "") if "-class" in args else None)
                instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None,
                    log_directory=self.logs, ownership_check=lambda: commands.started and not commands.stopped)
                instance.injected_command = False
                with patch.object(instance, "_journey_command", commands), \
                        patch.object(instance, "capture_gateway_process", side_effect=self.gateway_identities()), \
                        patch.object(fixture, "read_only_shared_snapshot", side_effect=(snapshot, snapshot, KeyboardInterrupt())), \
                        patch.object(fixture, "verify_runtime_components"), patch.object(instance, "capture_runtime_readiness"), \
                            patch.object(instance, "prepare_fixture_bootstrap"), patch.object(instance, "bootstrap_fixture_protocol"), contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(original_exit or 130, instance.execute())
                self.assertFalse(Path(instance.scratch).exists())

    def test_guard_final_preservation_cancellation_keeps_existing_failure(self):
        snapshot = self.new_shared_snapshot()
        snapshot["scheduler"] = {"revision": "1", "keys": 0}
        executable = fixture.APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
        original_is_file = Path.is_file
        for failed in (False, True):
            with self.subTest(failed=failed):
                instance = fixture.IsolatedFixture(command=self.guard_commands(failed), port_check=lambda: None,
                    log_directory=self.logs)
                with patch.object(guards, "IsolatedFixture", return_value=instance), \
                        patch.object(guards, "read_only_shared_snapshot", side_effect=(snapshot, snapshot, KeyboardInterrupt())), \
                        patch.object(Path, "is_file", lambda path: path == executable or original_is_file(path)), \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(1 if failed else 130, guards.run_guards())
                self.assertFalse(Path(instance.scratch).exists())

    def test_scheduler_preservation_requires_same_cluster_and_range_contents(self):
        header = {"cluster_id": "01", "member_id": "2", "revision": "003", "raft_term": "1"}
        row = {"key": "a2V5", "value": "dmFsdWU=", "create_revision": "1", "mod_revision": "3", "version": "1"}
        def snapshot(range_response):
            value = self.new_shared_snapshot()
            value["scheduler"] = fixture.scheduler_fingerprint(range_response, value)
            return value
        before = snapshot({"header": header, "kvs": [row], "count": "1"})
        self.assertEqual(self.comparison.fingerprint(b"scheduler-identity", b"1"), before.scheduler_identity)
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
        snapshot = self.new_shared_snapshot()
        snapshot["scheduler"] = {"revision": "1", "keys": 0}
        identity = {"pid": 999999999, "start": "1", "state": "S"}
        instance = fixture.IsolatedFixture(command=commands, port_check=lambda: None, log_directory=self.logs,
            ownership_check=lambda: commands.started and not commands.stopped, resource_check=lambda *_: [])
        instance.injected_command = False
        with patch.object(instance, "_journey_command", commands), \
                patch.object(instance, "capture_gateway_process", side_effect=self.gateway_identities()), \
                patch.object(instance, "resource_check", side_effect=([identity], [])), \
                patch.object(fixture, "read_only_shared_snapshot", return_value=snapshot), \
                patch.object(fixture, "verify_runtime_components"), patch.object(instance, "capture_runtime_readiness"), \
                            patch.object(instance, "prepare_fixture_bootstrap"), patch.object(instance, "bootstrap_fixture_protocol"), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, instance.execute())
        self.assertEqual([identity], instance.captured_processes)
        evidence = json.loads((instance.log_directory / "recorded-process-exit.json").read_text())
        self.assertTrue(evidence["all_gone"])
        self.assertEqual([dict(identity, gone=True)], evidence["processes"])

    def test_populated_snapshot_reader_selects_db0_13_14_and_detects_changed_broker_value(self):
        databases = {0: [["broker-key", "broker-v1", -1], ["broker-ttl", "ttl-value", 10000]],
                     13: [["actor-key", "actor-value", -1]], 14: [["model-key", "model-value", -1]]}
        def read(database_rows):
            snapshot, connection = self.read_shared_rows(database_rows)
            self.assertEqual([0, 13, 14], [int(args[1]) for args in connection.arguments if args[0] == b"SELECT"])
            self.assertEqual([b"INFO", b"SELECT", b"EVAL", b"SELECT", b"EVAL", b"SELECT", b"EVAL"],
                             [args[0] for args in connection.arguments])
            self.assertEqual([b"INFO", b"server"], connection.arguments[0])
            self.assertEqual({"0", "13", "14"}, set(snapshot["redis"]))
            self.assertEqual({"keys": 2, "persistent_keys": 1, "expiring_keys": 1, "captured_at_unix_ms": 1000}, snapshot["redis"]["0"])
            key = self.comparison.fingerprint(b"redis-key", fixture.hashlib.sha1(b"broker-ttl").hexdigest().encode())
            value = self.comparison.fingerprint(b"redis-value", fixture.hashlib.sha1(b"ttl-value").hexdigest().encode())
            self.assertEqual({key: (value, 10000)}, snapshot.expiring["0"])
            return snapshot
        before = read(databases)
        self.assertTrue(fixture.shared_preservation(before, read(databases))["matches"])
        changed = dict(databases)
        changed[0] = [["broker-key", "broker-v2", -1], ["broker-ttl", "ttl-value", 10000]]
        self.assertFalse(fixture.shared_preservation(before, read(changed))["matches"])


if __name__ == "__main__":
    unittest.main()
