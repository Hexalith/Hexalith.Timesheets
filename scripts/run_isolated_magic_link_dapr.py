"""Run one real acceptance journey with disposable Redis and Dapr control services.

Build the current Debug source-reference solution first. No images are pulled and
only this invocation's infrastructure is removed. Sanitized evidence is retained
in a unique child of --log-directory, including failed and interrupted commands.
"""

import argparse
import base64
import binascii
import contextlib
import ctypes
import errno
import hashlib
import hmac
import http.client
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import signal
import socket
import ssl
import subprocess
import tempfile
import threading
import time
import urllib.request
import urllib.parse
import urllib.error
import xml.etree.ElementTree as ET

from local_runtime_admission import LocalRuntimeAdmission, RuntimeAdmissionError

ROOT = Path(__file__).resolve().parents[1]
APPHOST = ROOT / "src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj"
GATEWAY_PROJECT = ROOT / "references/Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj"
GATEWAY_EXECUTABLE_SETTING = "TIMESHEETS_LIVE_DAPR_GATEWAY_EXECUTABLE"
GATEWAY_DIRECTORY_SETTING = "TIMESHEETS_LIVE_DAPR_GATEWAY_WORKING_DIRECTORY"
SECURITY_SMOKE_SETTING = "TIMESHEETS_LIVE_DAPR_SECURITY_SMOKE"
SDK_LAUNCH_OVERRIDES = ("RunCommand", "RunArguments", "RunWorkingDirectory", "StartAction",
                        "StartProgram", "StartArguments", "StartWorkingDirectory")
DEPENDENCY_ROOTS = {"Hexalith" + name + "Root": "Hexalith." + name for name in
                    ("EventStore", "Tenants", "Parties", "Projects", "Works", "FrontComposer", "Commons")}
DEPENDENCY_ROOTS["HexalithTenantsBasePath"] = "Hexalith.Tenants/src"
DEFAULT_COMPONENTS = APPHOST.parent / "DaprComponents"
COMPONENT_FILES = (Path("statestore.yaml"), Path("Timesheets/timesheets-readmodels.yaml"), Path("pubsub.yaml"))
CATALOG_RECEIVER_POLICIES = {
    "eventstore": ("gateway", "timesheets", (
        ("api/v1/commands/bound/workload", "POST"),
        ("api/v1/commands/workload", "POST"),
        ("api/v1/commands/status/workload/*", "GET"),
        ("api/v1/streams/read/workload", "POST"))),
    "timesheets": ("timesheets", "eventstore", (
        ("process", "POST"), ("replay-state", "POST"), ("query", "POST"),
        ("project/v2", "POST"), ("project/rebuild/v1", "POST"),
        ("project/rebuild/shared/v1", "POST"),
        ("admin/operational-index-metadata", "POST"))),
}
TECHNICAL_RECEIVER_PROFILE = "eventstore-sdk-technical-http-composite-v1"
TRANSPORT_RUNTIME_SHA256 = "e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488"
CATALOG_DIAGNOSTIC_CATEGORIES = {
    "finite_operation_unenrolled": "finite-operation-unenrolled",
    "finite_code_changed": "finite-code-changed",
    "finite_endpoint_changed": "finite-endpoint-changed",
    "finite_policy_changed": "finite-policy-changed",
    "finite_operation_changed": "finite-operation-changed",
    "finite_acl_path_ungranted": "finite-acl-path-ungranted",
    "workload_credential_denied": "InternalAuthenticationDenied",
    "workload_operation_not_granted": "operation-not-granted",
    "local_inventory": "Local host inventory, topology, authentication or profile disagrees with the exact required host set.",
    "independent_registration": "Configured host identity lacks matching independent registration, topology and authentication observations.",
    "owner_operations": "The installed owner operation schemas or eligibility differ from the proposed generation.",
    "loaded_topology": "Loaded topology disagrees with the candidate:",
    "executable_targets": "The proposed declarations, executable targets or exact host inventory disagree with actual local registrations.",
    "local_policy": "The proposed admission, named projection capability or auxiliary operation disagrees with installed local policy.",
    "backend": "The catalog authority backend is unavailable.",
    "backend_etag": "The Dapr authority component did not supply an ETag.",
    "pending": "The activation attempt is not prepared.",
    "draining": "Prior dispatch is still draining.",
    "cas_conflicts": "Concurrent catalog authority updates could not be serialized.",
    "digest_generations": "Available digest generations disagree with configured policy.",
    "admission_adapter": "Installed admission adapter lacks a matching complete static declaration.",
    "development_trust": "An activated owner-bound production catalog is required; Development fixtures supply no production authority.",
    "custody": "Owner catalog custody is incomplete, changed or not private.",
}
CATALOG_DIAGNOSTIC_EXCEPTIONS = (
    "RouteCatalogUnavailableException", "RouteCatalogValidationException", "InvalidOperationException",
    "HttpRequestException", "OperationCanceledException", "TaskCanceledException", "DaprException")
CATALOG_DIAGNOSTIC_FRAMES = (
    "DevelopmentRouteCatalogBootstrap.ActivateAsync", "RouteCatalogAuthority.PrepareAsync", "RouteCatalogAuthority.ReadyAsync",
    "RouteCatalogAuthority.CommitAsync", "ObservedRouteCatalogHost.ObserveAsync", "DaprRouteCatalogStateStore.ReadAsync",
    "DaprRouteCatalogStateStore.CompareExchangeAsync", "RouteCatalogAdmissionObservation.CaptureAsync")
REQUIRED_PORTS = (8080, 8081, 8082, 3501)
SHARED_CONTROL_PORTS = (6050, 6060, 50005, 50006)
REDIS_OBSERVATION_MAX_KEYS = 1024
REDIS_OBSERVATION_MAX_VALUE_BYTES = 65536
REDIS_OBSERVATION_MAX_MEMORY_BYTES = 32 * 1024 * 1024
REDIS_OBSERVATION_MAX_SCRIPT_MS = 250
# Conservative local refusal ceilings, not qualified deployed inventory sizes.
# Require the entire scheduler range within 4 MiB and 20 seconds, including
# connection, headers, body, JSON parsing and complete-range fingerprinting.
SCHEDULER_OBSERVATION_MAX_BYTES = 4 * 1024 * 1024
SCHEDULER_OBSERVATION_DEADLINE_SECONDS = 20
SCHEDULER_OBSERVATION_READ_BYTES = 65536
# Bound chunk metadata as well as decoded body bytes; never delegate chunk
# lengths to HTTPResponse.read, which accepts negative native chunk sizes.
SCHEDULER_OBSERVATION_MAX_FRAMING_BYTES = 65536
PRIVATE_CONTROL_PORT_MIN = 20000
PRIVATE_CONTROL_PORT_MAX = 29999
CONTROL_PORT_ALLOCATION_ATTEMPTS = 20
HOST_SETTING = re.compile(r'(?m)^    - name: redisHost\n      value: "([^"\n]+)"$')
PUBSUB_TEMPLATE = '''apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: pubsub
spec:
  type: pubsub.redis
  version: v1
  metadata:
    - name: redisHost
      value: "localhost:6379"
    - name: redisPassword
      value: ""
scopes:
  - eventstore
'''


class FixtureError(RuntimeError):
    """A safe configuration, lifecycle, or command failure."""

    def __init__(self, message, exit_code=1):
        super().__init__(message)
        self.exit_code = exit_code


def require(condition, message):
    if not condition:
        raise FixtureError(message)


def catalog_receiver_policy(app_id):
    """Render the exact initially closed policy captured by the SDK."""
    receiver, caller, operations = CATALOG_RECEIVER_POLICIES[app_id]
    rows = "\n".join("          - name: " + method + "\n"
                     + "            httpVerb: [" + verb + "]\n"
                     + "            action: allow" for method, verb in operations)
    return ("apiVersion: dapr.io/v1alpha1\nkind: Configuration\nmetadata:\n"
            + "  name: timesheets-catalog-" + receiver + "\nspec:\n"
            + "  accessControl:\n    defaultAction: deny\n    trustDomain: public\n"
            + "    policies:\n      - appId: " + caller + "\n"
            + "        defaultAction: deny\n        trustDomain: public\n"
            + "        namespace: default\n        operations:\n" + rows + "\n")


def validate_public_port(value):
    """Select a local listener without colliding with fixed topology listeners."""
    require(not isinstance(value, bool) and isinstance(value, (str, int))
            and re.fullmatch(r"[0-9]+", str(value)) is not None,
            "Local public port is missing or unsafe")
    port = int(value)
    require(1024 <= port <= 65535 and port not in (8081, 8082, 3501, 6379, 2379, *SHARED_CONTROL_PORTS),
            "Local public port is missing or unsafe")
    return port


def public_port_argument(value):
    """Return argparse's usual fixed error for an invalid public listener."""
    try:
        return validate_public_port(value)
    except FixtureError as error:
        raise argparse.ArgumentTypeError(str(error)) from None


def private_endpoint(endpoint, forbidden=()):
    require(isinstance(endpoint, str) and re.fullmatch(r"127\.0\.0\.1:[0-9]{1,5}", endpoint),
            "An explicit private loopback endpoint is required")
    port = int(endpoint.rsplit(":", 1)[1])
    require(1024 <= port <= 65535 and port not in forbidden, "Unsafe isolated service port")
    return port


def validate_control_plane(placement, scheduler):
    private_endpoint(placement, SHARED_CONTROL_PORTS)
    private_endpoint(scheduler, SHARED_CONTROL_PORTS)
    require(placement != scheduler, "Isolated control services must have distinct endpoints")


def component_template(relative, defaults=DEFAULT_COMPONENTS):
    return PUBSUB_TEMPLATE if relative == Path("pubsub.yaml") else (defaults / relative).read_text()


def validate_components(directory, endpoint, opted_in=True, defaults=DEFAULT_COMPONENTS):
    """Accept exact scoped component copies on one private Redis endpoint."""
    directory = Path(directory)
    require(opted_in, "Isolated components require explicit fixture opt-in")
    private_endpoint(endpoint, (6379,))
    require(directory.is_dir() and not directory.is_symlink(), "Isolated component directory is missing or unsafe")
    actual = [path.relative_to(directory) for path in directory.rglob("*") if path.is_file()]
    require(set(actual) == set(COMPONENT_FILES)
            and not any(path.is_symlink() for path in directory.rglob("*")),
            "Isolated component files are missing or unsafe")
    for relative in COMPONENT_FILES:
        contents = (directory / relative).read_text()
        matches = list(HOST_SETTING.finditer(contents))
        require(len(matches) == 1 and matches[0].group(1) == endpoint
                and HOST_SETTING.sub('    - name: redisHost\n      value: "localhost:6379"', contents)
                == component_template(relative, defaults),
                "Isolated component names, scopes, databases, or endpoint are unsafe")


def write_components(directory, endpoint):
    directory = Path(directory)
    for relative in COMPONENT_FILES:
        destination = directory / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        contents, count = HOST_SETTING.subn(
            '    - name: redisHost\n      value: "' + endpoint + '"', component_template(relative))
        require(count == 1, "Tracked component endpoint is unavailable")
        destination.write_text(contents)
    validate_components(directory, endpoint)


def assert_ports_available(public_port=8080):
    for port in (public_port if port == 8080 else port for port in REQUIRED_PORTS):
        for family, address in ((socket.AF_INET, "0.0.0.0"), (socket.AF_INET6, "::")):
            try:
                with socket.socket(family) as listener:
                    if family == socket.AF_INET6:
                        listener.setsockopt(socket.IPPROTO_IPV6, socket.IPV6_V6ONLY, 1)
                    listener.bind((address, port))
            except OSError as error:
                if family == socket.AF_INET6 and error.errno in (errno.EAFNOSUPPORT, errno.EADDRNOTAVAIL):
                    continue
                raise FixtureError(f"Required port {port} is occupied; no AppHost started") from None


def available_control_port(excluded=()):
    """Probe randomized lower-range candidates before Docker verifies publication."""
    candidates = secrets.SystemRandom().sample(
        range(PRIVATE_CONTROL_PORT_MIN, PRIVATE_CONTROL_PORT_MAX + 1), CONTROL_PORT_ALLOCATION_ATTEMPTS)
    forbidden = SHARED_CONTROL_PORTS + REQUIRED_PORTS + tuple(excluded)
    for port in candidates:
        if not PRIVATE_CONTROL_PORT_MIN <= port <= PRIVATE_CONTROL_PORT_MAX or port in forbidden:
            continue
        try:
            with socket.socket() as listener:
                listener.bind(("127.0.0.1", port))
            return port
        except OSError:
            continue
    raise FixtureError("Could not allocate a private control-service endpoint")


def json_output(output):
    """Tolerate an Aspire banner preceding machine-readable JSON."""
    decoder = json.JSONDecoder()
    for offset, character in enumerate(output):
        if character in "[{":
            try:
                result, _ = decoder.raw_decode(output[offset:])
                return result
            except ValueError:
                continue
    raise FixtureError("Runtime inspection did not return valid JSON")


def target_apphosts(rows):
    require(isinstance(rows, list), "Aspire inspection returned an unsafe shape")
    found = []
    for row in rows:
        require(isinstance(row, dict), "Aspire inspection returned an unsafe shape")
        path = next((value for key, value in row.items()
                     if key.lower() in ("apphostpath", "projectpath", "apphost") and isinstance(value, str)), None)
        require(path is not None, "Aspire inspection did not identify its AppHost")
        if Path(path).resolve() == APPHOST:
            found.append(row)
    return found


def sanitized(output):
    """Remove text and structured credentials before retaining any output."""
    if isinstance(output, bytes):
        output = output.decode(errors="replace")
    output = re.sub(r"\x1b\[[0-9;]*m", "", output or "")
    output = re.sub(r"(?i)([a-z][a-z0-9+.-]*://)[^/\s\"<>]*@", r"\1[redacted]@", output)
    output = re.sub(r'''(?:https?://[^\s"<>]+|[^\s"'<>]*\?[^\s"'<>]+)''',
                    lambda match: match.group(0).split("?", 1)[0], output, flags=re.IGNORECASE)
    output = re.sub(r"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", "[redacted]", output)
    # Consume complete JSON values before line handling: a field's string,
    # object, or array can span arbitrarily many lines. Keep the line fallback
    # for embedded/escaped JSON, environment variables, and YAML scalars.
    credential_names = (
        r"signing.?key|(?:app|dapr).?api.?token|one.?time.?token|token.?hash|redis.?password|"
        r"dashboard.?login.?token|authorization|workload.?assertion|"
        r"otel.?exporter.?otlp(?:.?(?:traces|metrics|logs))?.?headers|(?:x.?)?api.?key")
    generic_names = r"access.?token|refresh.?token|token|password|secret"
    credential = re.compile(
        credential_names + rf'''|(?:["']|\\")?(?:{generic_names})(?:["']|\\")?\s*[:=]''',
        re.IGNORECASE)
    decoder = json.JSONDecoder()
    named_credential = re.compile(rf"(?:{credential_names}|{generic_names})", re.IGNORECASE)
    json_tokens = re.compile(r'"(?:[^"\\]|\\.)*(?:"|\\?$)|[{}]', re.DOTALL)
    field_separator = re.compile(r"\s*:\s*")

    def redact_json(text):
        # Track object boundaries independently of complete JSON decoding so
        # interrupted records are removed without consuming earlier log text.
        objects = []
        spans = []
        for match in json_tokens.finditer(text):
            token = match.group()
            if token == "{":
                objects.append([match.start(), False])
                continue
            if token == "}":
                if objects:
                    start, sensitive = objects.pop()
                    if sensitive:
                        spans.append((start, match.end(), '"[redacted]"'))
                continue
            try:
                name = decoder.decode(token)
            except ValueError:
                continue
            if name.lstrip().startswith(("{", "[")):
                redacted = redact_json(name)
                if redacted != name:
                    spans.append((match.start(), match.end(), json.dumps(redacted)))
            separator = field_separator.match(text, match.end())
            if not objects or separator is None:
                continue
            sensitive = named_credential.fullmatch(name)
            try:
                value, end = decoder.raw_decode(text, separator.end())
            except ValueError:
                if sensitive:
                    spans.append((separator.end(), len(text), '"[redacted]"'))
                continue
            if sensitive:
                spans.append((separator.end(), end, '"[redacted]"'))
            elif name.lower() in ("name", "key") and isinstance(value, str) and named_credential.fullmatch(value):
                objects[-1][1] = True
        spans.extend((start, len(text), '"[redacted]"') for start, sensitive in objects if sensitive)
        retained = []
        previous_end = 0
        for start, end, replacement in sorted(spans, key=lambda span: (span[0], -span[1])):
            if start >= previous_end:
                retained.extend((text[previous_end:start], replacement))
                previous_end = end
        retained.append(text[previous_end:])
        return "".join(retained)

    output = redact_json(output)
    value_fields = re.compile(rf'''(?i)["']?(?:{credential_names}|{generic_names})["']?\s*[:=]\s*''')
    quoted_value = re.compile(r'''"(?:[^"\\]|\\.)*(?:"|$)|'(?:[^'\\]|\\.)*(?:'|$)''', re.DOTALL)
    spans = []
    for match in value_fields.finditer(output):
        if spans and match.start() < spans[-1][1]:
            continue
        try:
            _, end = decoder.raw_decode(output, match.end())
            spans.append((match.end(), end))
        except ValueError:
            quoted = quoted_value.match(output, match.end())
            if quoted:
                spans.append((match.end(), quoted.end()))
    for start, end in reversed(spans):
        output = output[:start] + '"[redacted]"' + output[end:]
    lines = output.splitlines()
    # YAML environment records may also put value before name or use equal
    # indentation. Remove their mapping block, bounded by the next list item,
    # parent indentation, or unrelated plain log line.
    yaml_field = re.compile(r'''^([ \t]*)(-[ \t]+)?["']?([A-Za-z][A-Za-z0-9_-]*)["']?:[ \t]*(.*)$''')
    yaml_redacted = set()
    for index, line in enumerate(lines):
        field = yaml_field.match(line)
        if not field or field[3].lower() not in ("name", "key") or not named_credential.fullmatch(
                field[4].split(" #", 1)[0].strip().strip("\"'")):
            continue
        indentation = len(field[1]) + len(field[2] or "")
        start, end = index, index + 1
        if not field[2]:
            while start > 0:
                previous = lines[start - 1]
                if previous.lstrip().startswith("#"):
                    start -= 1
                    continue
                sibling = yaml_field.match(previous)
                leading = len(previous) - len(previous.lstrip())
                if previous.strip() and leading < indentation:
                    if sibling and sibling[2] and len(sibling[1]) + len(sibling[2]) == indentation:
                        start -= 1
                    break
                if previous.strip() and leading == indentation and (not sibling or sibling[2]):
                    break
                start -= 1
        while end < len(lines):
            following = lines[end]
            if following.lstrip().startswith("#"):
                end += 1
                continue
            sibling = yaml_field.match(following)
            leading = len(following) - len(following.lstrip())
            if following.strip() and (leading < indentation
                    or leading == indentation and (not sibling or sibling[2])):
                break
            end += 1
        yaml_redacted.update(range(start, end))
    retained = []
    index = 0
    while index < len(lines):
        if index in yaml_redacted:
            if index - 1 not in yaml_redacted:
                retained.append("[credential record redacted]")
            index += 1
            continue
        line = lines[index]
        index += 1
        if not credential.search(line):
            retained.append(line)
            continue
        retained.append("[credential line redacted]")
        # YAML block/folded and multiline plain scalars, including Dapr's
        # '- name: redisPassword' / 'value:' shape, end at this indentation.
        indentation = len(line) - len(line.lstrip())
        while index < len(lines) and (not lines[index].strip()
                or len(lines[index]) - len(lines[index].lstrip()) > indentation):
            index += 1
    return "\n".join(retained) + "\n"


def require_one_passing_test(result):
    summaries = re.findall(r"Total:\s*(\d+),\s*Errors:\s*(\d+),\s*Failed:\s*(\d+),"
                           r"\s*Skipped:\s*(\d+),\s*Not Run:\s*(\d+)", result.stdout or "")
    require(result.returncode == 0 and summaries == [("1", "0", "0", "0", "0")],
            "Expected exactly one executed passing live test with no errors or skips")


def process_group_members(group):
    members = []
    for directory in Path("/proc").iterdir():
        if not directory.name.isdigit():
            continue
        try:
            fields = (directory / "stat").read_text().rsplit(")", 1)[1].split()
            if int(fields[2]) == group:
                members.append((int(directory.name), fields[0]))
        except FileNotFoundError:
            continue
    return members


def reap_process_group(group):
    while True:
        try:
            pid, _ = os.waitpid(-group, os.WNOHANG)
            if pid == 0:
                break
        except ChildProcessError:
            break


def stop_process_group(process):
    """Stop only the journey session and reap adopted orphan descendants."""
    for sig in (signal.SIGTERM, signal.SIGKILL):
        try:
            os.killpg(process.pid, sig)
        except ProcessLookupError:
            pass
        until = time.monotonic() + (2 if sig == signal.SIGTERM else 5)
        while time.monotonic() < until:
            process.poll()
            reap_process_group(process.pid)
            if not process_group_members(process.pid):
                return
            time.sleep(0.05)
    raise FixtureError("Journey descendants remain; fixture infrastructure must be retained")


def bound_resource_processes(directory, placement, scheduler):
    """Read ownership bindings; never signal Aspire resources manually."""
    found = []
    for process in Path("/proc").iterdir():
        if not process.name.isdigit():
            continue
        try:
            # Aspire children retain the invoking UID. Other users' process
            # environments are inaccessible and cannot belong to this fixture.
            if process.stat().st_uid != os.getuid():
                continue
            arguments = (process / "cmdline").read_bytes().split(b"\0")
            executable = Path(os.fsdecode(arguments[0])).name if arguments else ""
            if executable not in ("daprd", "sentry", "placement", "scheduler", "dotnet", "aspire", "Aspire.Dashboard", "Hexalith.Timesheets", "Hexalith.EventStore", "Hexalith.Timesheets.AppHost") \
                    and not executable.startswith("dcp"):
                continue
            environment = (process / "environ").read_bytes().split(b"\0")
            bound = f"TIMESHEETS_LIVE_DAPR_COMPONENTS={directory}".encode() in environment
            if executable == "daprd":
                bound |= any(str(directory).encode() in arg for arg in arguments)
                bound |= bool(placement and placement.encode() in arguments or scheduler and scheduler.encode() in arguments)
            if bound:
                fields = (process / "stat").read_text().rsplit(")", 1)[1].split()
                identity = {"pid": int(process.name), "start": fields[19], "state": fields[0]}
                if executable == "Aspire.Dashboard" or executable.startswith("dcp"):
                    identity.update(executable=str((process / "exe").resolve(strict=True)), parent=int(fields[1]))
                found.append(identity)
        except (FileNotFoundError, ProcessLookupError):
            continue
    return found


def running_dapr_processes(directory=Path("/proc")):
    """Cross-check visible sidecars without relying on CLI metadata or signaling them."""
    found = []
    try:
        for process in Path(directory).iterdir():
            if not process.name.isdigit():
                continue
            try:
                if (process / "comm").read_text().strip() != "daprd":
                    continue
                fields = (process / "stat").read_text().rsplit(")", 1)[1].split()
                require(len(fields) > 19, "Dapr process inventory is unavailable; no resources started")
                if fields[0] != "Z":
                    found.append({"pid": int(process.name), "start": fields[19]})
            except (FileNotFoundError, ProcessLookupError):
                continue
    except (OSError, ValueError, IndexError):
        raise FixtureError("Dapr process inventory is unavailable; no resources started") from None
    return found


def gateway_process_identity(executable, bound_identities, directory=Path("/proc")):
    """Verify the selected native process and its exact fixture-bound DCP parent."""
    message = "Gateway native command and direct DCP ownership could not be verified"
    selected = Path(executable).resolve()
    identities = {(row["pid"], row["start"]) for row in bound_identities}

    def read_process(pid):
        process = Path(directory) / str(pid)
        fields = (process / "stat").read_text().rsplit(")", 1)[1].split()
        arguments = (process / "cmdline").read_bytes().split(b"\0")
        if arguments[-1] == b"":
            arguments.pop()
        return {"pid": pid, "start": fields[19], "state": fields[0], "parent": int(fields[1]),
                "executable": str((process / "exe").resolve(strict=True))}, arguments

    try:
        candidates = []
        for identity in bound_identities:
            try:
                process, arguments = read_process(identity["pid"])
            except FileNotFoundError:
                continue
            if process["executable"] == str(selected):
                require((process["pid"], process["start"]) in identities, message)
                require(arguments == [os.fsencode(executable)] and process["state"] != "Z", message)
                parent, _ = read_process(process["parent"])
                require((parent["pid"], parent["start"]) in identities
                        and Path(parent["executable"]).name in ("dcp", "dcpctrl") and parent["state"] != "Z", message)
                candidates.append({**process, "command": os.fsdecode(arguments[0]), "argument_count": 0,
                                   "parent_identity": parent})
        require(len(candidates) == 1, message)
        return candidates[0]
    except (OSError, ValueError, IndexError, KeyError, TypeError):
        raise FixtureError(message) from None


def recorded_process_exits(identities):
    """Check every captured PID/start pair even if its resource binding changed."""
    exits = []
    for identity in identities:
        try:
            fields = Path(f"/proc/{identity['pid']}/stat").read_text().rsplit(")", 1)[1].split()
            require(len(fields) > 19, "Captured process identity inspection is unavailable")
            gone = fields[19] != identity["start"]
        except FileNotFoundError:
            gone = True
        exits.append(dict(identity, gone=gone))
    return {"all_gone": all(row["gone"] for row in exits), "processes": exits}


def runtime_host_identity(identities, assets_path, directory=Path("/proc")):
    """Require the actual fixture-bound DCP and dashboard from restored NuGet assets."""
    message = "Current NuGet DCP/dashboard process ownership could not be verified"
    try:
        assets = json.loads(Path(assets_path).read_text())
        expected = {}
        for prefix, name in (("Aspire.Hosting.Orchestration.", "dcp"), ("Aspire.Dashboard.Sdk.", "dashboard")):
            packages = [key for key in assets["libraries"] if key.startswith(prefix)]
            require(len(packages) == 1, message)
            package, version = packages[0].split("/")
            relative = Path(assets["libraries"][packages[0]]["path"])
            require(not relative.is_absolute() and ".." not in relative.parts
                    and relative.parts == (package.lower(), version), message)
            folders = assets["packageFolders"]
            require(isinstance(folders, dict) and bool(folders) and all(Path(folder).is_absolute() for folder in folders), message)
            roots = sorted({(Path(folder) / relative).resolve() for folder in folders}, key=str)
            expected[name] = (package.lower(), version, roots)
        def tree_root(nodes):
            roots = [row for row in nodes.values() if row["parent"] not in nodes]
            require(len(roots) == 1, message)
            root = roots[0]
            for row in nodes.values():
                visited = set()
                while row["pid"] != root["pid"]:
                    require(row["pid"] not in visited and row["parent"] in nodes, message)
                    visited.add(row["pid"])
                    row = nodes[row["parent"]]
            return root

        def package_directory(executable, kind):
            roots = expected[kind][2]
            matching = [root for root in roots if executable.is_relative_to(root)]
            require(len(matching) == 1, message)
            return matching[0]

        require(len({row["pid"] for row in identities}) == len(identities), message)
        # Capture metadata identifies required roles before short-lived DCP
        # workers can exit. Only those noncritical workers may disappear.
        captured_dcp = {row["pid"]: row for row in identities if Path(row.get("executable", "")).name == "dcp"}
        captured_dashboard = [row for row in identities if Path(row.get("executable", "")).name == "Aspire.Dashboard"]
        critical = set()
        if captured_dcp or captured_dashboard:
            root = tree_root(captured_dcp)
            require(len(captured_dashboard) == 1, message)
            critical.add(captured_dashboard[0]["pid"])
            parent = captured_dashboard[0]["parent"]
            require(parent in captured_dcp, message)
            while True:
                critical.add(parent)
                if parent == root["pid"]:
                    break
                parent = captured_dcp[parent]["parent"]

        found = {name: [] for name in expected}
        exited_workers = []
        hashes = {}
        def record_exited_worker(identity, fields):
            require(identity["pid"] in captured_dcp and identity["pid"] not in critical, message)
            package_directory(Path(identity["executable"]), "dcp")
            if fields is not None:
                require(fields[19] == identity["start"] and fields[0] == "Z", message)
            exited_workers.append({"pid": identity["pid"], "start": identity["start"],
                "state": None if fields is None else fields[0], "executable": identity["executable"], "parent": identity["parent"]})

        for identity in identities:
            process = Path(directory) / str(identity["pid"])
            try:
                fields = (process / "stat").read_text().rsplit(")", 1)[1].split()
            except FileNotFoundError:
                fields = None
            if fields is None or fields[0] == "Z":
                record_exited_worker(identity, fields)
                continue
            require(fields[19] == identity["start"] and fields[0] != "Z", message)
            try:
                executable = (process / "exe").resolve(strict=True)
            except FileNotFoundError:
                try:
                    fields = (process / "stat").read_text().rsplit(")", 1)[1].split()
                except FileNotFoundError:
                    fields = None
                record_exited_worker(identity, fields)
                continue
            name = "dcp" if executable.name == "dcp" else "dashboard" if executable.name == "Aspire.Dashboard" else None
            if name:
                package, version, _ = expected[name]
                root = package_directory(executable, name)
                if executable not in hashes:
                    hashes[executable] = hashlib.sha256(executable.read_bytes()).hexdigest()
                found[name].append({**identity, "state": fields[0], "executable": str(executable), "parent": int(fields[1]),
                                    "package": package, "version": version,
                                    "package_directory": str(root), "sha256": hashes[executable]})
        require(len(found["dashboard"]) == 1, message)
        dashboard = found["dashboard"][0]
        dcp = {row["pid"]: row for row in found["dcp"]}
        daemon = tree_root(dcp)
        require(len({row["executable"] for row in dcp.values()}) == 1, message)
        require(dashboard["parent"] in dcp, message)
        controller = dcp[dashboard["parent"]]
        # A missing/unbound daemon must not promote the resource controller
        # into the accepted root of this SDK's observed two-role DCP graph.
        require(controller["pid"] != daemon["pid"], message)
        # The daemon can be reparented outside the fixture. Its bound PID/start
        # and the single connected DCP tree establish scope, not its adopter.
        required = {dashboard["pid"], daemon["pid"], controller["pid"]} | critical
        observed = {row["pid"]: row for rows in found.values() for row in rows}
        for pid in required:
            require(pid in observed, message)
            row = observed[pid]
            process = Path(directory) / str(pid)
            fields = (process / "stat").read_text().rsplit(")", 1)[1].split()
            require(fields[19] == row["start"] and fields[0] != "Z"
                    and str((process / "exe").resolve(strict=True)) == row["executable"]
                    and (pid == daemon["pid"] or int(fields[1]) == row["parent"]), message)
        return {"dcp": daemon, "dcp_controller": controller, "dashboard": dashboard,
                "dcp_processes": sorted(dcp.values(), key=lambda row: row["pid"]), "exited_workers": exited_workers}
    except (OSError, ValueError, IndexError, KeyError, TypeError):
        raise FixtureError(message) from None


def runtime_host_diagnostics(identities, assets_path, fixture_directory, directory=Path("/proc")):
    """Allowlist failure observations without changing which processes are accepted."""
    report = {"expected_package_directories": {}, "observed_processes": [], "candidate_processes": []}
    try:
        assets = json.loads(Path(assets_path).read_text())
        for prefix, name in (("Aspire.Hosting.Orchestration.", "dcp"), ("Aspire.Dashboard.Sdk.", "dashboard")):
            roots = set()
            for package, library in assets["libraries"].items():
                if package.startswith(prefix):
                    relative = Path(library["path"])
                    if not relative.is_absolute() and ".." not in relative.parts:
                        roots.update(str((Path(folder) / relative).resolve()) for folder in assets["packageFolders"]
                                     if Path(folder).is_absolute())
            report["expected_package_directories"][name] = sorted(roots)
    except (OSError, ValueError, KeyError, TypeError, AttributeError):
        pass

    def read_identity(pid):
        observed = {"pid": pid, "start": None, "state": None, "executable": None, "parent": None}
        try:
            process = Path(directory) / str(pid)
            fields = (process / "stat").read_text().rsplit(")", 1)[1].split()
            if re.fullmatch(r"[0-9]+", fields[19]) and re.fullmatch(r"[0-9]+", fields[1]) \
                    and fields[0] in ("R", "S", "D", "Z", "T", "t", "X", "x", "K", "W", "P", "I"):
                observed.update(start=fields[19], state=fields[0], parent=int(fields[1]))
            observed["executable"] = str((process / "exe").resolve(strict=True))
        except (OSError, ValueError, IndexError):
            pass
        return observed

    captured = set()
    for identity in identities:
        pid = identity.get("pid")
        if isinstance(pid, int) and not isinstance(pid, bool) and pid > 0:
            observed = read_identity(pid)
            if isinstance(identity.get("start"), str) and re.fullmatch(r"[0-9]+", identity["start"]):
                captured.add((pid, identity["start"]))
                observed["captured_start"] = identity["start"]
            report["observed_processes"].append(observed)
    try:
        processes = list(Path(directory).iterdir())
    except OSError:
        processes = []
    for process in processes:
        if not process.name.isdigit():
            continue
        try:
            if process.stat().st_uid != os.getuid():
                continue
            candidate = read_identity(int(process.name))
            executable = Path(candidate["executable"] or "").name
            if executable != "Aspire.Dashboard" and not executable.startswith("dcp"):
                continue
            candidate["captured"] = (candidate["pid"], candidate["start"]) in captured
            # The value is compared in memory and discarded; no environment
            # record or raw command line is copied into diagnostic evidence.
            try:
                entries = (process / "environ").read_bytes().split(b"\0")
                candidate["marker_present"] = f"TIMESHEETS_LIVE_DAPR_COMPONENTS={fixture_directory}".encode() in entries
            except OSError:
                pass
            ancestors = []
            parent = candidate["parent"]
            visited = {candidate["pid"]}
            while parent and parent not in visited and len(ancestors) < 32:
                visited.add(parent)
                ancestor = read_identity(parent)
                ancestors.append(ancestor)
                parent = ancestor["parent"]
            candidate["ancestors"] = ancestors
            report["candidate_processes"].append(candidate)
        except (OSError, ValueError, TypeError):
            continue
    return report


def security_http_url_diagnostics(value, public_port):
    """Describe URL guard predicates using fixed categories and bounded numbers."""
    report = {"url_string": isinstance(value, str), "parse_succeeded": False,
        "scheme": "unavailable", "http_scheme_matches": False, "scheme_allowed": False,
        "hostname_category": "unavailable", "hostname_allowed": False,
        "path_empty_or_slash": False, "no_query": False, "no_fragment": False, "no_userinfo": False,
        "port_parse_valid": False, "port_present": None, "bounded_port": None, "port_in_allowed_range": False,
        "reserved_port_match": False, "public_port_match": False}
    if not isinstance(value, str):
        return report
    try:
        parsed = urllib.parse.urlsplit(value)
        report["parse_succeeded"] = True
        report["scheme"] = parsed.scheme if parsed.scheme in ("http", "https") else "other" if parsed.scheme else "absent"
        report["http_scheme_matches"] = parsed.scheme == "http"
        report["scheme_allowed"] = parsed.scheme in ("http", "https")
        host = parsed.hostname
        categories = {"localhost": "localhost", "127.0.0.1": "ipv4-loopback", "::1": "ipv6-loopback",
                      "0.0.0.0": "wildcard", "::": "wildcard"}
        report["hostname_category"] = categories.get(host, "other" if host else "absent")
        report["hostname_allowed"] = host in ("localhost", "127.0.0.1")
        report.update(path_empty_or_slash=parsed.path in ("", "/"), no_query=not parsed.query,
                      no_fragment=not parsed.fragment, no_userinfo=not parsed.username and not parsed.password)
        port = parsed.port
        report.update(port_parse_valid=True, port_present=port is not None)
        if port is not None and 1 <= port <= 65535:
            report.update(bounded_port=port, port_in_allowed_range=1024 <= port <= 65535,
                          reserved_port_match=port in REQUIRED_PORTS, public_port_match=port == public_port)
    except (ValueError, TypeError, AttributeError):
        pass
    return report


def discover_security_http_endpoint(container, public_port, command=None, environment=None, diagnostics=None):
    """Match Aspire's named HTTP URL to the exact owned container's private binding."""
    message = "Aspire security HTTP endpoint could not be verified"
    report = {} if diagnostics is None else diagnostics
    report.update(discovery_command_succeeded=False, description_shape_matches=False,
                  container_identity_matches=False, http_endpoint_unambiguous=False,
                  port_binding_shape_matches=False, port_safe=False, port_bindings=[])
    arguments = ["aspire", "describe", "security", "--apphost", str(APPHOST), "--non-interactive", "--format", "Json"]
    try:
        if command is None:
            result = subprocess.run(arguments, cwd=ROOT, env=environment or os.environ, timeout=30,
                text=True, encoding="utf-8", errors="replace", capture_output=True, check=False)
        else:
            result = command(arguments, cwd=ROOT, env=environment or os.environ, timeout=30)
        require(result.returncode == 0, message)
        report["discovery_command_succeeded"] = True
        # Aspire CLI 13.6.1 ResourceJson: snapshot resources, urls, and
        # KnownProperties.Container.Id (container.id). Never transcript this JSON:
        # it also contains arbitrary environment/property values and dashboard URLs.
        snapshot = json_output(result.stdout)
        resources = snapshot["resources"]
        require(isinstance(resources, list) and len(resources) == 1, message)
        resource = resources[0]
        require(isinstance(resource, dict) and resource.get("resourceType", "").casefold() == "container"
                and (resource.get("name") == "security" or resource.get("displayName") == "security"), message)
        report["description_shape_matches"] = True
        identity = container["Id"]
        report["container_identity_matches"] = (isinstance(identity, str) and re.fullmatch(r"[0-9a-f]{64}", identity) is not None
            and resource["properties"]["container.id"] == identity)
        require(report["container_identity_matches"], message)
        urls = resource["urls"]
        require(isinstance(urls, list), message)
        http = [url for url in urls if isinstance(url, dict) and url.get("name") == "http"
                and url.get("isInternal", False) is False]
        report["http_endpoint_unambiguous"] = len(http) == 1
        require(report["http_endpoint_unambiguous"], message)
        report["url_checks"] = security_http_url_diagnostics(http[0].get("url"), public_port)
        parsed = urllib.parse.urlsplit(http[0]["url"])
        require(parsed.scheme in ("http", "https") and parsed.hostname in ("localhost", "127.0.0.1")
                and parsed.path in ("", "/") and not parsed.query and not parsed.fragment
                and not parsed.username and not parsed.password and parsed.port is not None, message)
        port = private_endpoint("127.0.0.1:" + str(parsed.port), (public_port, *REQUIRED_PORTS))
        endpoint = {"resource": "security", "name": "http", "scheme": parsed.scheme, "host": parsed.hostname,
                    "published_port": port, "container_id": identity}
        report["http_endpoint"] = dict(endpoint)
        matches = []
        for target, bindings in container["NetworkSettings"]["Ports"].items():
            if bindings is None:
                continue
            target_match = re.fullmatch(r"([0-9]{1,5})/(tcp|udp)", target)
            require(target_match is not None and 1 <= int(target_match[1]) <= 65535 and isinstance(bindings, list), message)
            for binding in bindings:
                host = binding["HostIp"]
                published = binding["HostPort"]
                require(isinstance(published, str) and re.fullmatch(r"[0-9]{1,5}", published) is not None, message)
                published_port = int(published)
                report["port_bindings"].append({"container_port": int(target_match[1]), "protocol": target_match[2],
                    "host": host if host in ("127.0.0.1", "0.0.0.0", "::1", "::") else "other",
                    "published_port": published_port})
                require(host == "127.0.0.1" and 1024 <= published_port <= 65535, message)
                if published_port == port and target_match[2] == "tcp":
                    matches.append(int(target_match[1]))
        report["port_binding_shape_matches"] = len(matches) == 1
        require(report["port_binding_shape_matches"], message)
        report["port_safe"] = True
        endpoint.update(container_port=matches[0], binding_host="127.0.0.1")
        report["http_endpoint"] = dict(endpoint)
        return endpoint
    except (FixtureError, OSError, ValueError, KeyError, TypeError, AttributeError, subprocess.TimeoutExpired):
        raise FixtureError(message) from None


def security_credential_handoff(container, public_port, diagnostics=None, describe_command=None, describe_environment=None):
    """Authenticate in memory, retaining only fixed-label failure diagnostics."""
    message = "Generated security credentials or realm scope claims could not be verified"
    report = {} if diagnostics is None else diagnostics
    report.clear()
    report.update(stage="environment", environment_shape_matches=False, generated_credentials_shape_matches=False)
    try:
        environment = dict(value.split("=", 1) for value in container["Config"]["Env"] if "=" in value)
        username = environment["HEXALITH_EVENTSTORE_CLIENT_USERNAME"]
        password = environment["HEXALITH_EVENTSTORE_CLIENT_PASSWORD"]
        report["environment_shape_matches"] = isinstance(username, str) and isinstance(password, str)
        report["generated_credentials_shape_matches"] = (re.fullmatch(r"[a-z0-9]{24,}", username) is not None
            and re.fullmatch(r"[a-zA-Z0-9]{32,}", password) is not None)
        require(report["generated_credentials_shape_matches"], message)
        report["stage"] = "port"
        endpoint = discover_security_http_endpoint(container, public_port, describe_command, describe_environment, report)
        origin = endpoint["scheme"] + "://" + endpoint["host"] + ":" + str(endpoint["published_port"]) + "/realms/hexalith"
        body = urllib.parse.urlencode({"grant_type": "password", "client_id": "hexalith-eventstore",
                                       "username": username, "password": password}).encode()
        request = urllib.request.Request(origin + "/protocol/openid-connect/token", data=body,
                                          headers={"Content-Type": "application/x-www-form-urlencoded"})
        report["stage"] = "token_http"
        # Authenticated HTTPS keeps normal certificate and hostname verification.
        context = ssl.create_default_context() if endpoint["scheme"] == "https" else None
        with owned_http_open(request, timeout=20, context=context) as response:
            report["stage"] = "token_shape"
            payload = json.load(response)
        report["token_response_object"] = isinstance(payload, dict)
        token = payload["access_token"]
        report["access_token_string"] = isinstance(token, str)
        require(report["access_token_string"], message)
        parts = token.split(".")
        report["jwt_three_parts"] = len(parts) == 3 and all(parts)
        require(report["jwt_three_parts"], message)
        claims = json.loads(base64.urlsafe_b64decode(parts[1] + "=="))
        report["claims_object"] = isinstance(claims, dict)
        require(report["claims_object"], message)
        report["stage"] = "claims"
        def string_list(value):
            return isinstance(value, list) and all(isinstance(item, str) for item in value)
        checks = {
            "issuer_matches": lambda: claims["iss"] == origin,
            "username_matches": lambda: claims["preferred_username"] == username,
            "audience_matches": lambda: (isinstance(claims["aud"], str) or string_list(claims["aud"]))
                and "hexalith-eventstore" in ([claims["aud"]] if isinstance(claims["aud"], str) else claims["aud"]),
            "tenant_claims_match": lambda: string_list(claims["eventstore:tenant"])
                and set(claims["eventstore:tenant"]) == {"tenant-a", "tenant-b", "system"},
            "domain_claims_match": lambda: string_list(claims["eventstore:domain"]) and claims["eventstore:domain"] == ["timesheets"],
            "permission_claims_match": lambda: string_list(claims["eventstore:permission"])
                and set(claims["eventstore:permission"]) == {"command:submit", "command:replay", "command:query"},
        }
        report["claim_matches"] = {}
        for label, comparison in checks.items():
            try:
                report["claim_matches"][label] = comparison()
            except (ValueError, KeyError, TypeError):
                report["claim_matches"][label] = False
        require(all(report["claim_matches"].values()), message)
        report["stage"] = "complete"
        return {"generated_credentials_accepted": True, "realm": "hexalith", "audience": "hexalith-eventstore",
                "tenant_claims_match": True, "domain_claims_match": True, "permission_claims_match": True}
    except urllib.error.HTTPError as error:
        # Parse only a bounded body in memory. Arbitrary response fields,
        # descriptions, URLs, headers and exception strings never enter evidence.
        statuses = {400, 401, 403, 404, 405, 408, 409, 415, 422, 429, 500, 502, 503, 504}
        report.update(stage="token_http", http_status=error.code if type(error.code) is int and error.code in statuses else None,
                      oauth_error_code=None, error_category="http_rejection")
        try:
            body = error.read(65537)
            payload = json.loads(body) if len(body) <= 65536 else None
            if isinstance(payload, dict):
                code = payload.get("error")
                allowed = {"invalid_request", "invalid_client", "invalid_grant", "unauthorized_client", "unsupported_grant_type",
                           "invalid_scope", "access_denied", "temporarily_unavailable", "server_error", "invalid_token", "insufficient_scope"}
                if isinstance(code, str) and code in allowed:
                    report.update(oauth_error_code=code, error_category="oauth_rejection")
                    description = payload.get("error_description")
                    known = {"account is not fully set up": "profile_setup_required",
                             "invalid user credentials": "invalid_user_credentials",
                             "account disabled": "account_disabled", "account is disabled": "account_disabled"}
                    if code == "invalid_grant" and isinstance(description, str) and description.strip().casefold() in known:
                        report["error_category"] = known[description.strip().casefold()]
                    elif code == "invalid_client":
                        report["error_category"] = "client_authentication_failed"
                    elif code == "unauthorized_client":
                        report["error_category"] = "client_grant_not_allowed"
                    elif code in ("server_error", "temporarily_unavailable"):
                        report["error_category"] = "token_endpoint_unavailable"
        except (OSError, ValueError, TypeError):
            pass
        finally:
            # Closing also prevents HTTPError's finalizer from emitting a
            # resource warning containing its untrusted reason or URL.
            try:
                error.close()
            except OSError:
                pass
        raise FixtureError(message) from None
    except ssl.SSLError as error:
        report["error_category"] = "tls_verification_failed" if isinstance(error, ssl.SSLCertVerificationError) else "tls_handshake_failed"
        raise FixtureError(message) from None
    except urllib.error.URLError as error:
        report["error_category"] = ("tls_verification_failed" if isinstance(error.reason, ssl.SSLCertVerificationError)
            else "tls_handshake_failed" if isinstance(error.reason, ssl.SSLError) else "token_transport_failure")
        raise FixtureError(message) from None
    except (FixtureError, OSError, ValueError, IndexError, KeyError, TypeError, AttributeError):
        report["error_category"] = {"environment": "environment_shape_invalid", "port": "port_shape_invalid",
            "token_http": "token_transport_failure", "token_shape": "token_shape_invalid", "claims": "claim_mismatch"}.get(
                report["stage"], "handoff_failure")
        raise FixtureError(message) from None


def dashboard_diagnostic_identity(identity):
    """Allow only kernel numeric identity fields into failure diagnostics."""
    pid, start = identity.get("pid"), identity.get("start")
    return {"pid": pid if type(pid) is int and pid > 0 else None,
            "start": start if isinstance(start, str) and re.fullmatch(r"[0-9]+", start) else None}


def dashboard_process_observation(pid, fields):
    """Retain numeric identity and the allowlisted kernel process state only."""
    return {**dashboard_diagnostic_identity({"pid": pid, "start": fields[19]}),
            "state": fields[0] if fields[0] in ("R", "S", "D", "Z", "T", "t", "X", "x", "K", "W", "P", "I") else None}


def dashboard_owner_identity(pid, directory):
    """Sample an observed holder without allowing unavailable metadata to mask refusal."""
    owner = {"pid": pid, "start": None, "identity_visibility": "unavailable"}
    try:
        observed = (directory / str(pid) / "stat").read_text().rsplit(")", 1)[1].split()
        owner.update(dashboard_diagnostic_identity({"pid": pid, "start": observed[19]}))
        owner["identity_visibility"] = "observed" if owner["start"] is not None else "unavailable"
    except FileNotFoundError:
        owner["identity_visibility"] = "disappeared"
    except PermissionError:
        owner["identity_visibility"] = "inaccessible"
    except (OSError, ValueError, IndexError):
        pass
    return owner


def dashboard_listener_identity(identity, endpoint, directory=Path("/proc"), diagnostic=None):
    """Verify explicit listeners within observable descriptors, retaining visibility limits."""
    message = "Owned dashboard socket ownership could not be verified"
    report = diagnostic if diagnostic is not None else {}
    report.clear()
    report.update(verified=False, stage="expected_process", expected_identity=dashboard_diagnostic_identity(identity),
                  socket_inodes=[], socket_owners={}, owner_identities=[], expected_process_observations={},
                  observation_started_unix_ns=time.time_ns(), visibility={
                      "scope": "observable_process_descriptors", "inspected_processes": 0,
                      "inaccessible_processes": 0, "inaccessible_descriptors": 0,
                      "disappeared_processes": 0, "disappeared_descriptors": 0,
                      "socket_tables_complete": False, "descriptor_scan_completed": False,
                      "exclusivity_established": False, "unobserved_transfer_excluded": False})
    try:
        return _dashboard_listener_identity(identity, endpoint, directory, report)
    except (OSError, ValueError, IndexError, KeyError, TypeError, FixtureError):
        raise FixtureError(message) from None
    finally:
        report["observation_completed_unix_ns"] = time.time_ns()


def _dashboard_listener_identity(identity, endpoint, directory, report):
    """Collect partial allowlisted observations before each opaque refusal."""
    message = "Owned dashboard socket ownership could not be verified"
    directory = Path(directory)
    process = directory / str(identity["pid"])
    fields = (process / "stat").read_text().rsplit(")", 1)[1].split()
    report["expected_process_observations"]["before_inventory"] = dashboard_process_observation(identity["pid"], fields)
    require(fields[19] == identity["start"] and fields[0] != "Z", message)
    # Linux can expose an IPv4 loopback listener through an IPv6 mapped bind.
    # It can answer the same IPv4 origin and must not escape the owner check.
    ipv4_loopback = {"0100007F", "0000000000000000FFFF00000100007F"}
    addresses = {"127.0.0.1": ipv4_loopback, "::1": {"00000000000000000000000001000000"},
                 "localhost": ipv4_loopback | {"00000000000000000000000001000000"}}[endpoint.hostname]
    sockets = set()
    loopback_hosts = set()
    report["stage"] = "socket_tables"
    for table in ("tcp", "tcp6"):
        for line in (directory / "net" / table).read_text().splitlines()[1:]:
            row = line.split()
            address, port = row[1].split(":")
            if int(port, 16) == endpoint.port and row[3] == "0A":
                # Require explicit matching sockets. An unrelated IPv6 wildcard
                # may coexist with a valid IPv4 listener (including v6only=false
                # with reuse); it is not evidence for this numeric origin.
                if address in addresses:
                    require(row[9].isdigit() and int(row[9]) > 0, message)
                    sockets.add(row[9])
                    report["socket_inodes"] = sorted(sockets)
                    loopback_hosts.add("::1" if address == "00000000000000000000000001000000" else "127.0.0.1")
    report["visibility"]["socket_tables_complete"] = True
    require(bool(sockets), message)
    owners = {inode: set() for inode in sockets}
    report["socket_owners"] = {inode: [] for inode in sorted(sockets)}
    visibility = report["visibility"]
    report["stage"] = "descriptor_inventory"
    for candidate in directory.iterdir():
        if not candidate.name.isdigit():
            continue
        try:
            for descriptor in (candidate / "fd").iterdir():
                try:
                    target = os.readlink(descriptor)
                except FileNotFoundError:
                    visibility["disappeared_descriptors"] += 1
                    continue
                except PermissionError:
                    visibility["inaccessible_descriptors"] += 1
                    continue
                match = re.fullmatch(r"socket:\[([0-9]+)\]", target)
                if match and match[1] in owners:
                    pid = int(candidate.name)
                    owners[match[1]].add(pid)
                    report["socket_owners"][match[1]] = sorted(owners[match[1]])
                    if not any(owner["pid"] == pid for owner in report["owner_identities"]):
                        report["owner_identities"].append(dashboard_owner_identity(pid, directory))
                        report["owner_identities"].sort(key=lambda owner: owner["pid"])
            visibility["inspected_processes"] += 1
        except FileNotFoundError:
            visibility["disappeared_processes"] += 1
        except PermissionError:
            # These holders remain unknown, even when every visible owner is
            # the expected process. Never infer system-wide exclusivity.
            visibility["inaccessible_processes"] += 1
            continue
    visibility["descriptor_scan_completed"] = True
    report["stage"] = "owner_match"
    require(all(pids == {identity["pid"]} for pids in owners.values()), message)
    # Recheck after inventory: a PID may have exited or been reused while its
    # descriptors were being inspected.
    report["stage"] = "expected_process_recheck"
    current = (process / "stat").read_text().rsplit(")", 1)[1].split()
    report["expected_process_observations"]["after_inventory"] = dashboard_process_observation(identity["pid"], current)
    require(current[19] == identity["start"] and current[0] != "Z", message)
    report.update(verified=True, stage="complete", pid=identity["pid"], start=identity["start"],
                  loopback_hosts=sorted(loopback_hosts))
    return report


def dashboard_listener_fingerprint(observation):
    """Compare listener identity independently of unrelated process/descriptor churn."""
    return {key: observation[key] for key in ("pid", "start", "socket_inodes", "loopback_hosts")}


def verify_dashboard_ready(identity, directory=Path("/proc"), diagnostic=None):
    """Check the owned dashboard listener once after Aspire resource readiness."""
    message = "Owned dashboard HTTP readiness could not be verified"
    report = diagnostic if diagnostic is not None else {}
    report.clear()
    report.update(ready=False, stage="expected_process", expected_identity=dashboard_diagnostic_identity(identity),
                  ownership_observations={}, observation_started_unix_ns=time.time_ns(),
                  visibility={"scope": "observable_process_descriptors", "exclusivity_established": False,
                              "unobserved_transfer_excluded": False})

    def observe_failed_health():
        # A health transport failure may follow a socket transfer. This extra
        # inventory is diagnostic only and can never turn refusal into success.
        if report["stage"] == "health_request":
            observation = report["ownership_observations"]["after_failed_health"] = {}
            try:
                dashboard_listener_identity(identity, endpoint, directory, diagnostic=observation)
            except Exception:
                # Keep partial allowlisted data and the original opaque refusal.
                pass

    try:
        process = Path(directory) / str(identity["pid"])
        fields = (process / "stat").read_text().rsplit(")", 1)[1].split()
        report["observed_expected_identity"] = dashboard_process_observation(identity["pid"], fields)
        require(fields[19] == identity["start"] and fields[0] != "Z", message)
        report["stage"] = "environment"
        environment = dict(value.split("=", 1) for value in (process / "environ").read_bytes().decode().split("\0") if "=" in value)
        urls = environment["ASPNETCORE_URLS"].split(";")
        report["stage"] = "endpoint_validation"
        require(bool(urls), message)
        endpoint = urllib.parse.urlsplit(urls[0])
        require(endpoint.scheme in ("http", "https") and endpoint.hostname in ("localhost", "127.0.0.1", "::1")
                and endpoint.port and endpoint.path in ("", "/") and "?" not in urls[0] and "#" not in urls[0]
                and endpoint.username is None and endpoint.password is None
                and all(ord(character) > 32 and ord(character) != 127 for character in urls[0]), message)
        # Aspire Dashboard 13.6.1 maps /health anonymously; the browser root may
        # challenge with a login redirect and is not a direct health probe.
        report["stage"] = "before_health"
        before = report["ownership_observations"]["before_health"] = {}
        dashboard_listener_identity(identity, endpoint, directory, diagnostic=before)
        connect_host = (endpoint.hostname if endpoint.hostname != "localhost" else
                        "127.0.0.1" if "127.0.0.1" in before["loopback_hosts"] else "::1")
        authority = f"[{connect_host}]:{endpoint.port}" if ":" in connect_host else f"{connect_host}:{endpoint.port}"
        health_url = urllib.parse.urlunsplit((endpoint.scheme, authority, "/health", "", ""))
        # Preserve the configured HTTP host while connecting to the exact
        # numeric listener proved above, independently of localhost resolution.
        request = urllib.request.Request(health_url, headers={"Host": endpoint.netloc})
        # The socket inventory binds this loopback listener. Local Aspire
        # developer certificates need not be installed in Python's CA bundle.
        context = ssl.create_default_context()
        context.check_hostname = False
        context.verify_mode = ssl.CERT_NONE
        report["stage"] = "health_request"
        with owned_http_open(request, timeout=20, context=context) as response:
            require(response.status == 200, message)
        report["stage"] = "after_health"
        after = report["ownership_observations"]["after_health"] = {}
        dashboard_listener_identity(identity, endpoint, directory, diagnostic=after)
        report["stage"] = "identity_comparison"
        require(dashboard_listener_fingerprint(before) == dashboard_listener_fingerprint(after), message)
        visibility = {"scope": "observable_process_descriptors", "exclusivity_established": False,
                      "unobserved_transfer_excluded": False}
        for key in ("inspected_processes", "inaccessible_processes", "inaccessible_descriptors",
                    "disappeared_processes", "disappeared_descriptors"):
            visibility[key + "_observations"] = before["visibility"][key] + after["visibility"][key]
        result = {"ready": True, "scheme": endpoint.scheme, "loopback": True, "port": endpoint.port,
                "path": "/health", "connect_host": connect_host,
                "ownership": dashboard_listener_fingerprint(after),
                "ownership_observations": {"before_health": before, "after_health": after},
                "visibility": visibility}
        report.update(result, stage="complete")
        return result
    except urllib.error.HTTPError as error:
        try:
            error.close()
        except OSError:
            pass
        observe_failed_health()
        raise FixtureError(message) from None
    except (OSError, ValueError, IndexError, KeyError, TypeError, FixtureError, http.client.HTTPException):
        observe_failed_health()
        raise FixtureError(message) from None
    finally:
        report["observation_completed_unix_ns"] = time.time_ns()


class SharedComparison:
    """One run's private, domain-separated comparison key; never sent to services."""

    def __init__(self):
        self._key = secrets.token_bytes(32)

    def fingerprint(self, domain, value):
        return hmac.digest(self._key, domain + b"\0" + value, "sha256")


class SharedObservationError(FixtureError):
    """Safe phase evidence for one failed observation, without service responses."""

    def __init__(self, observation):
        super().__init__("Shared-service observation failed during " + observation["phase"] + "; preservation is unproved")
        self.shared_observation = observation


class SharedSnapshot(dict):
    """Serializable aggregate evidence; comparison material lives only in attributes."""

    def __init__(self, comparison):
        require(isinstance(comparison, SharedComparison), "An explicit shared comparison scope is required")
        super().__init__(redis={})
        self.comparison = comparison
        self.persistent = {}
        self.expiring = {}
        self.captured_at = {}
        self.redis_identity = None
        self.scheduler_identity = None
        self.scheduler_content = None


def shared_preservation(before, after):
    """Allow only a previously captured, elapsed Redis TTL to remove a key."""
    require(before.comparison is after.comparison, "Shared preservation comparison scopes differ")
    require(all(isinstance(snapshot.redis_identity, bytes) and len(snapshot.redis_identity) == 32
                for snapshot in (before, after)), "Redis preservation identity is unavailable")
    require(all(isinstance(value, bytes) and len(value) == 32 for snapshot in (before, after)
                for value in (snapshot.scheduler_identity, snapshot.scheduler_content)),
            "Scheduler preservation comparison is unavailable")
    expires = {}
    redis = {}
    mismatches = []
    redis_identity_matches = before.redis_identity == after.redis_identity
    if not redis_identity_matches:
        mismatches.append("Redis server identity changed")
    scheduler_fields = ("keys",)
    require(all(field in snapshot["scheduler"] for snapshot in (before, after) for field in scheduler_fields),
            "Scheduler preservation identity is unavailable")
    identity_matches = before.scheduler_identity == after.scheduler_identity
    contents_match = (before.scheduler_content == after.scheduler_content
                     and before["scheduler"]["keys"] == after["scheduler"]["keys"])
    scheduler = {"identity_matches": identity_matches, "contents_match": contents_match,
                 "keys_before": before["scheduler"]["keys"], "keys_after": after["scheduler"]["keys"],
                 "revision_before": before["scheduler"].get("revision"),
                 "revision_after": after["scheduler"].get("revision")}
    require(all(set(snapshot["redis"]) == {"0", "13", "14"} for snapshot in (before, after)),
            "Redis preservation requires complete DB0/13/14 coverage")
    for database in before["redis"]:
        previous, current = before["redis"][database], after["redis"][database]
        require(all(database in snapshot.persistent and database in snapshot.expiring
                    and database in snapshot.captured_at for snapshot in (before, after)),
                "Redis preservation comparison is unavailable")
        old_persistent, new_persistent = before.persistent[database], after.persistent[database]
        for snapshot, public in ((before, previous), (after, current)):
            require(all(isinstance(public.get(field), int) and not isinstance(public[field], bool)
                        for field in ("keys", "persistent_keys", "expiring_keys"))
                    and len(snapshot.persistent[database]) == public["persistent_keys"]
                    and len(snapshot.expiring[database]) == public["expiring_keys"]
                    and public["keys"] == public["persistent_keys"] + public["expiring_keys"],
                    "Redis comparison coverage is incomplete")
        old, new = before.expiring[database], after.expiring[database]
        require(old_persistent.keys().isdisjoint(old) and new_persistent.keys().isdisjoint(new),
                "Redis preservation TTL classification is inconsistent")
        persistent_added = sum(key not in old_persistent and key not in old for key in new_persistent)
        persistent_removed = sum(key not in new_persistent and key not in new for key in old_persistent)
        persistent_updates = sum(value != new_persistent[key] for key, value in old_persistent.items() if key in new_persistent)
        to_expiring = sum(key in old_persistent for key in new)
        to_persistent = sum(key in new_persistent for key in old)
        persistent_matches = old_persistent == new_persistent
        allowed = missing = value_changes = deadline_changes = 0
        for key, (value, deadline) in old.items():
            if key not in new:
                if key in new_persistent:
                    continue
                if deadline <= after.captured_at[database]:
                    allowed += 1
                else:
                    missing += 1
            else:
                new_value, new_deadline = new[key]
                value_changes += value != new_value
                deadline_changes += abs(deadline - new_deadline) > 2
        added = sum(key not in old and key not in old_persistent for key in new)
        expiring_matches = not any((added, missing, value_changes, deadline_changes, to_expiring, to_persistent))
        redis[database] = {
            "captured_before_unix_ms": before.captured_at[database],
            "captured_after_unix_ms": after.captured_at[database],
            "persistent": {"matches": persistent_matches, "keys_before": previous["persistent_keys"],
                           "keys_after": current["persistent_keys"], "added": persistent_added,
                           "removed": persistent_removed, "value_changes": persistent_updates},
            "ttl_transitions": {"persistent_to_expiring": to_expiring, "expiring_to_persistent": to_persistent},
            "expiring": {"matches": expiring_matches, "added": added, "missing_unexpired": missing,
                         "value_changes": value_changes, "deadline_changes": deadline_changes,
                         "confirmed_natural_expirations": allowed}}
        if not persistent_matches:
            mismatches.append(f"Redis DB {database} persistent content changed")
        if not expiring_matches:
            mismatches.append(f"Redis DB {database} expiring content or deadlines changed")
        expires[database] = allowed
    if not identity_matches:
        mismatches.append("Scheduler cluster identity changed")
    if not contents_match:
        mismatches.append("Scheduler contents changed")
    return {"matches": not mismatches, "confirmed_natural_expirations": expires,
            "redis_server": {"identity_matches": redis_identity_matches},
            "observation": {"before": before.get("observation"), "after": after.get("observation")},
            "redis": redis, "scheduler": scheduler, "mismatches": mismatches,
            "method": "Redis INFO server identity; atomic read-only KEYS/DUMP/PTTL/TIME DB0/13/14; etcd v3 all-key range on loopback:2379",
            "attribution": "Snapshot differences do not establish a writer or intervening mutation history",
            "ttl_rule": "Retained expiring values and deadlines match; absent keys allowed only after their captured deadline"}


def scheduler_fingerprint(response, snapshot=None):
    """Accept only a complete etcd range, including a genuine empty range."""
    message = "Scheduler preservation range is unavailable or malformed"
    require(isinstance(response, dict) and set(response) <= {"header", "kvs", "count", "more"}, message)
    header = response.get("header")
    require(isinstance(header, dict) and set(header) == {"cluster_id", "member_id", "revision", "raft_term"}, message)

    def integer(value, minimum=0):
        require(not isinstance(value, bool) and isinstance(value, (str, int))
                and re.fullmatch(r"[0-9]+", str(value)) and int(value) >= minimum, message)
        return int(value)

    for key, value in header.items():
        integer(value, 0 if key == "revision" else 1)
    require(response.get("more", False) is False, message)
    # Proto JSON omits default count/kvs for empty ranges. A validated range
    # header distinguishes those from an error or unknown success response.
    rows = response.get("kvs", [])
    require(isinstance(rows, list) and integer(response.get("count", 0)) == len(rows), message)
    keys = set()
    for row in rows:
        require(isinstance(row, dict) and {"key", "create_revision", "mod_revision", "version"} <= set(row)
                and set(row) <= {"key", "value", "create_revision", "mod_revision", "version", "lease"}, message)
        for field in ("key", "value"):
            encoded = row.get(field, "")
            require(isinstance(encoded, str), message)
            try:
                decoded = base64.b64decode(encoded, validate=True)
            except (ValueError, binascii.Error):
                raise FixtureError(message) from None
            require(base64.b64encode(decoded).decode() == encoded and (field != "key" or decoded), message)
        require(row["key"] not in keys, message)
        keys.add(row["key"])
        created = integer(row["create_revision"], 1)
        modified = integer(row["mod_revision"], created)
        require(modified <= int(header["revision"]), message)
        integer(row["version"], 1)
        if "lease" in row:
            integer(row["lease"])
    rows = sorted(rows, key=lambda row: row["key"])
    if snapshot is not None:
        snapshot.scheduler_identity = snapshot.comparison.fingerprint(b"scheduler-identity", str(int(header["cluster_id"])).encode())
        snapshot.scheduler_content = snapshot.comparison.fingerprint(b"scheduler-content",
            json.dumps(rows, sort_keys=True, separators=(",", ":")).encode())
    return {"revision": str(int(header["revision"])), "keys": len(rows)}


@contextlib.contextmanager
def scheduler_observation_deadline():
    """Own a synchronous deadline, deferring callbacks during signal changes.

    Signal masks alone do not protect Python callbacks: another unblocked thread
    can receive the signal and schedule its handler on the main Python thread.
    Temporary wrappers therefore defer caller interruption throughout setup and
    teardown, then replay it only after restoring caller state. No observer
    thread or process is created.
    """
    message = "Scheduler preservation total deadline is unavailable"
    require(threading.current_thread() is threading.main_thread()
            and all(hasattr(signal, name) for name in
                    ("setitimer", "getitimer", "ITIMER_REAL", "SIGALRM", "SIGINT", "SIGTERM",
                     "pthread_sigmask", "sigpending", "sigwait")), message)
    protected = {signal.SIGALRM, signal.SIGINT, signal.SIGTERM}
    previous_mask = signal.pthread_sigmask(signal.SIG_BLOCK, set())
    handlers = {kind: signal.getsignal(kind) for kind in protected}
    require(all(handler in (signal.SIG_DFL, signal.SIG_IGN) or callable(handler)
                for handler in handlers.values()), message)
    claimed = False
    wrapping = False
    critical = True
    deadline_expired = False
    deferred = {}
    failure = None

    def retain(error):
        nonlocal failure
        if failure is None or (isinstance(error, KeyboardInterrupt) and not isinstance(failure, KeyboardInterrupt)):
            failure = error
        elif isinstance(error, SystemExit) and not isinstance(failure, (KeyboardInterrupt, SystemExit)):
            failure = error

    def dispatch(kind, frame):
        handler = handlers[kind]
        if callable(handler):
            handler(kind, frame)
        elif handler == signal.SIG_DFL:
            # Preserve native termination semantics for callers without the
            # runner's SIGTERM -> KeyboardInterrupt handler.
            signal.signal(kind, handler)
            os.kill(os.getpid(), kind)

    def interrupted(kind, frame):
        if critical:
            # At most the two interruption signals are retained. Standard
            # pending signals coalesce; no unbounded callback queue is needed.
            deferred.setdefault(kind, frame)
        else:
            dispatch(kind, frame)

    def expired(_signal, _frame):
        nonlocal deadline_expired
        deadline_expired = True
        if not critical:
            raise TimeoutError("Scheduler preservation observation exceeded its total deadline")

    def transition(action, complete):
        # Finish syscalls interrupted before or after their effect. The wrappers
        # defer real callbacks, including those scheduled by another thread.
        while True:
            try:
                action()
                return
            except BaseException as error:
                retain(error)
                if complete():
                    return

    try:
        signal.pthread_sigmask(signal.SIG_BLOCK, protected)
        require(signal.SIGALRM not in previous_mask and signal.SIGALRM not in signal.sigpending(), message)
        require(not any(signal.getitimer(signal.ITIMER_REAL)), message)
        deadline = time.monotonic() + SCHEDULER_OBSERVATION_DEADLINE_SECONDS
        # Claim wrapper cleanup before the first installation, for an exception
        # immediately after signal.signal changes the real handler.
        wrapping = True
        signal.signal(signal.SIGINT, interrupted)
        signal.signal(signal.SIGTERM, interrupted)
        claimed = True
        signal.signal(signal.SIGALRM, expired)
        signal.setitimer(signal.ITIMER_REAL, SCHEDULER_OBSERVATION_DEADLINE_SECONDS)
        critical = False
        signal.pthread_sigmask(signal.SIG_SETMASK, previous_mask)
        while deferred:
            kind = next(iter(deferred))
            dispatch(kind, deferred.pop(kind))
        if deadline_expired:
            expired(None, None)
        yield
        if time.monotonic() >= deadline:
            expired(None, None)
    except BaseException as error:
        retain(error)
    finally:
        # A callback can interrupt installation/restoration of the wrappers
        # themselves. Retry the entire critical section in that case, with all
        # ownership facts already saved. Every operation is idempotent.
        while True:
            try:
                critical = True
                transition(lambda: signal.pthread_sigmask(signal.SIG_BLOCK, protected),
                           lambda: protected <= signal.pthread_sigmask(signal.SIG_BLOCK, set()))
                if wrapping:
                    for kind in (signal.SIGINT, signal.SIGTERM):
                        transition(lambda: signal.signal(kind, interrupted),
                                   lambda: signal.getsignal(kind) is interrupted)
                if claimed:
                    transition(lambda: signal.setitimer(signal.ITIMER_REAL, 0),
                               lambda: not any(signal.getitimer(signal.ITIMER_REAL)))
                    # Never deliver a pending owned alarm to the restored caller
                    # handler, possibly SIG_DFL. Foreign alarms were refused.
                    if signal.SIGALRM in signal.sigpending():
                        transition(lambda: signal.sigwait({signal.SIGALRM}),
                                   lambda: signal.SIGALRM not in signal.sigpending())
                    transition(lambda: signal.signal(signal.SIGALRM, handlers[signal.SIGALRM]),
                               lambda: signal.getsignal(signal.SIGALRM) == handlers[signal.SIGALRM])
                transition(lambda: signal.pthread_sigmask(signal.SIG_SETMASK, previous_mask),
                           lambda: signal.pthread_sigmask(signal.SIG_BLOCK, set()) == previous_mask)
                if wrapping:
                    for kind in (signal.SIGTERM, signal.SIGINT):
                        transition(lambda: signal.signal(kind, handlers[kind]),
                                   lambda: signal.getsignal(kind) == handlers[kind])
                break
            except BaseException as error:
                retain(error)
                continue
    for kind, frame in deferred.items():
        try:
            dispatch(kind, frame)
        except BaseException as error:
            retain(error)
    if failure is not None:
        raise failure


def read_bounded_scheduler_body(response):
    """Collect only a complete, strictly framed range within local byte limits."""
    message = "Scheduler preservation response exceeds its safety bound or is malformed"
    headers = getattr(response, "headers", None)
    declared = None
    transfers = []
    if headers is not None:
        require(not headers.defects, message)
        lengths = headers.get_all("Content-Length", [])
        transfers = headers.get_all("Transfer-Encoding", [])
        require(len(lengths) <= 1 and len(transfers) <= 1 and not (lengths and transfers), message)
        require(not transfers or transfers[0].lower() == "chunked", message)
        if lengths:
            require(re.fullmatch(r"[0-9]+", lengths[0]) is not None, message)
            declared = int(lengths[0])
            require(declared <= SCHEDULER_OBSERVATION_MAX_BYTES, message)
    body = bytearray()
    if transfers:
        # HTTPResponse.read(n) is not a body bound for chunked transfers: its
        # native negative chunk path can invoke raw read(-1). Parse the bounded
        # framing on fp directly and check each size before consuming its data.
        raw = response.fp
        framing_bytes = 0

        def line():
            nonlocal framing_bytes
            value = raw.readline(SCHEDULER_OBSERVATION_MAX_FRAMING_BYTES - framing_bytes + 1)
            framing_bytes += len(value)
            require(framing_bytes <= SCHEDULER_OBSERVATION_MAX_FRAMING_BYTES, message)
            if not value:
                raise http.client.IncompleteRead(b"")
            require(value.endswith(b"\r\n"), message)
            return value

        def exact(amount):
            data = bytearray()
            while len(data) < amount:
                chunk = raw.read(min(SCHEDULER_OBSERVATION_READ_BYTES, amount - len(data)))
                if not chunk:
                    raise http.client.IncompleteRead(b"", amount - len(data))
                data.extend(chunk)
            return data

        token = rb"[!#$%&'*+\-.^_`|~0-9A-Za-z]+"
        quoted = rb'"(?:[\t !#-\[\]-~]|\\[\t -~])*"'
        extension = rb"(?:[ \t]*;[ \t]*" + token + rb"(?:[ \t]*=[ \t]*(?:" + token + b"|" + quoted + rb"))?)*"
        while True:
            size_line = line()
            require(re.fullmatch(rb"[0-9a-fA-F]+" + extension + rb"[ \t]*\r\n", size_line) is not None, message)
            size = int(size_line.split(b";", 1)[0], 16)
            require(size <= SCHEDULER_OBSERVATION_MAX_BYTES - len(body), message)
            if size == 0:
                while True:
                    trailer = line()
                    if trailer == b"\r\n":
                        return body
                    require(re.fullmatch(token + rb":[\t\x20-\x7e\x80-\xff]*\r\n", trailer) is not None, message)
                    require(trailer.split(b":", 1)[0].lower() not in (b"content-length", b"transfer-encoding"), message)
            body.extend(exact(size))
            require(exact(2) == b"\r\n", message)
            framing_bytes += 2
            require(framing_bytes <= SCHEDULER_OBSERVATION_MAX_FRAMING_BYTES, message)
    while True:
        chunk = response.read(min(SCHEDULER_OBSERVATION_READ_BYTES,
                                  SCHEDULER_OBSERVATION_MAX_BYTES + 1 - len(body)))
        if not chunk:
            break
        body.extend(chunk)
        require(len(body) <= SCHEDULER_OBSERVATION_MAX_BYTES, message)
    if declared is not None and len(body) != declared:
        raise http.client.IncompleteRead(b"", declared - len(body))
    return body


def read_only_shared_snapshot(comparison):
    """Fingerprint retained broker/state and scheduler KV without exposing data."""
    def read_resp(stream):
        prefix = stream.read(1)
        line = stream.readline().rstrip(b"\r\n")
        if prefix == b"+":
            return line
        if prefix == b":":
            return int(line)
        if prefix == b"$":
            length = int(line)
            if length < 0:
                return None
            value = stream.read(length)
            require(stream.read(2) == b"\r\n", "Redis snapshot response was incomplete")
            return value
        if prefix == b"*":
            return [read_resp(stream) for _ in range(int(line))]
        if prefix == b"-":
            raise FixtureError("Redis preservation observation exceeded its safety bound or failed")
        raise FixtureError("Redis preservation snapshot is unavailable")

    require(isinstance(comparison, SharedComparison), "An explicit shared comparison scope is required")
    observation = {"started_unix_ns": time.time_ns(), "completed": False, "phase": "initializing", "database": None}
    try:
        result = SharedSnapshot(comparison)
        observation["phase"] = "redis_connection"
        with socket.create_connection(("127.0.0.1", 6379), timeout=20) as connection:
            with connection.makefile("rb") as stream:
                def command(*arguments):
                    arguments = [arg if isinstance(arg, bytes) else str(arg).encode() for arg in arguments]
                    connection.sendall(b"*" + str(len(arguments)).encode() + b"\r\n" + b"".join(
                        b"$" + str(len(arg)).encode() + b"\r\n" + arg + b"\r\n" for arg in arguments))
                    return read_resp(stream)
                observation["phase"] = "redis_identity"
                server = command("INFO", "server")
                require(isinstance(server, bytes), "Redis preservation identity is unavailable")
                identities = re.findall(rb"(?m)^run_id:([0-9a-f]{40})\r?$", server)
                require(len(identities) == 1 and len(re.findall(rb"(?m)^run_id:", server)) == 1,
                        "Redis preservation identity is unavailable or malformed")
                result.redis_identity = result.comparison.fingerprint(b"redis-identity", identities[0])
                for database in (0, 13, 14):
                    observation.update(phase="redis_database", database=database)
                    require(command("SELECT", database) == b"OK", "Redis snapshot database selection failed")
                    memory = command("INFO", "memory")
                    require(isinstance(memory, bytes), "Redis preservation memory bound is unavailable")
                    usage = re.findall(rb"(?m)^used_memory:([0-9]+)\r?$", memory)
                    require(len(usage) == 1 and int(usage[0]) <= REDIS_OBSERVATION_MAX_MEMORY_BYTES,
                            "Redis preservation observation exceeds its safety bound")
                    # DBSIZE is constant-time. Refuse before invoking Lua on an
                    # oversized shared database, including the observed large DB0.
                    count = command("DBSIZE")
                    require(type(count) is int and count <= REDIS_OBSERVATION_MAX_KEYS,
                            "Redis preservation observation exceeds its safety bound")
                    # Script reads only and holds atomic access. Pair each PTTL
                    # with TIME so its absolute expiry remains comparable even
                    # while a large broker snapshot takes measurable time.
                    script = f"""
local max_keys = {REDIS_OBSERVATION_MAX_KEYS}
local max_value_bytes = {REDIS_OBSERVATION_MAX_VALUE_BYTES}
local max_memory_bytes = {REDIS_OBSERVATION_MAX_MEMORY_BYTES}
local max_script_ms = {REDIS_OBSERVATION_MAX_SCRIPT_MS}
-- Repeat the cardinality check atomically before KEYS, closing the preflight race.
local used_memory = tonumber(string.match(redis.call('INFO', 'memory'), 'used_memory:(%d+)'))
if not used_memory or used_memory > max_memory_bytes or redis.call('DBSIZE') > max_keys then
    return redis.error_reply('observation-bound')
end
local started = redis.call('TIME')
local started_ms = tonumber(started[1]) * 1000 + math.floor(tonumber(started[2]) / 1000)
local keys = redis.call('KEYS', '*')
local rows = {{}}
for _, key in ipairs(keys) do
    local now = redis.call('TIME')
    local key_bytes = redis.call('MEMORY', 'USAGE', key, 'SAMPLES', 0)
    if tonumber(now[1]) * 1000 + math.floor(tonumber(now[2]) / 1000) - started_ms > max_script_ms
        or not key_bytes or key_bytes > max_value_bytes then
        return redis.error_reply('observation-bound')
    end
    local value = redis.call('DUMP', key)
    if value then
        local expiry_now = redis.call('TIME')
        local ttl = redis.call('PTTL', key)
        local deadline = ttl
        if ttl >= 0 then
            deadline = tonumber(expiry_now[1]) * 1000 + math.floor(tonumber(expiry_now[2]) / 1000) + ttl
        end
        table.insert(rows, {{redis.sha1hex(key), redis.sha1hex(value), deadline}})
    end
end
local finished = redis.call('TIME')
if tonumber(finished[1]) * 1000 + math.floor(tonumber(finished[2]) / 1000) - started_ms > max_script_ms then
    return redis.error_reply('observation-bound')
end
return {{finished, rows}}
"""
                    now, rows = command("EVAL", script, 0)
                    timestamp = int(now[0]) * 1000 + int(now[1]) // 1000
                    # Lua's existing fixed-size digests never leave memory. Key them
                    # locally without exporting the run secret or expanding the read.
                    permanent, expiring = {}, {}
                    for key, value, ttl in rows:
                        require(isinstance(key, bytes) and isinstance(value, bytes)
                                and re.fullmatch(rb"[0-9a-f]{40}", key) and re.fullmatch(rb"[0-9a-f]{40}", value)
                                and isinstance(ttl, int) and not isinstance(ttl, bool) and ttl >= -1,
                                "Redis preservation row is malformed")
                        key = result.comparison.fingerprint(b"redis-key", key)
                        value = result.comparison.fingerprint(b"redis-value", value)
                        require(key not in permanent and key not in expiring, "Redis preservation row is duplicated")
                        if ttl == -1:
                            permanent[key] = value
                        else:
                            expiring[key] = (value, ttl)
                    result.persistent[str(database)] = permanent
                    result.expiring[str(database)] = expiring
                    result.captured_at[str(database)] = timestamp
                    result["redis"][str(database)] = {"keys": len(rows), "persistent_keys": len(permanent),
                        "expiring_keys": len(result.expiring[str(database)]), "captured_at_unix_ms": timestamp}
        observation.update(phase="scheduler_range", database=None)
        request = urllib.request.Request("http://127.0.0.1:2379/v3/kv/range", data=b'{"key":"AA==","range_end":"AA=="}',
                                         headers={"Content-Type": "application/json"})
        with scheduler_observation_deadline():
            response = None
            try:
                try:
                    response = owned_http_open(request, timeout=SCHEDULER_OBSERVATION_DEADLINE_SECONDS)
                except urllib.error.HTTPError as error:
                    with error:
                        raise
                with response as scheduler_response:
                    body = read_bounded_scheduler_body(scheduler_response)

                    def unique_scheduler_pairs(pairs):
                        values = {}
                        for name, value in pairs:
                            require(name not in values, "Scheduler preservation range contains duplicate JSON names")
                            values[name] = value
                        return values

                    try:
                        scheduler = json.loads(body, object_pairs_hook=unique_scheduler_pairs)
                    except ValueError:
                        raise FixtureError("Scheduler preservation range did not return valid JSON") from None
            finally:
                if response is not None:
                    response.close()
            result["scheduler"] = scheduler_fingerprint(scheduler, result)
        observation.update(completed=True, phase="complete")
        result["observation"] = observation
        return result
    except (FixtureError, OSError, http.client.HTTPException, ValueError, KeyError, TypeError, IndexError,
            RecursionError, MemoryError, KeyboardInterrupt) as error:
        observation["exception_category"] = type(error).__name__
        if isinstance(error, KeyboardInterrupt):
            error.shared_observation = observation
            raise
        raise SharedObservationError(observation) from None
    finally:
        observation["completed_unix_ns"] = time.time_ns()


def safe_invocation_failure_category(body):
    """Classify only fixed wire error identifiers, never an arbitrary response body."""
    if body in (b"", ""):
        return "empty_http_body"
    if not isinstance(body, (bytes, str)) or len(body) > 65536:
        return "unclassified_body_withheld"
    try:
        value = json.loads(body)
    except (ValueError, TypeError, UnicodeError, RecursionError):
        return "unclassified_body_withheld"
    codes = {"ERR_PERMISSION_DENIED": "dapr_permission_denied", "ERR_DIRECT_INVOKE": "dapr_direct_invoke_failed"}
    code = value.get("errorCode") if isinstance(value, dict) else None
    return codes.get(code, "unclassified_body_withheld") if isinstance(code, str) else "unclassified_body_withheld"


def metadata_invocation_boundary(status, body):
    """Classify the pinned metadata wire boundary without attributing an exact receiver predicate."""
    category = safe_invocation_failure_category(body)
    if category in ("dapr_direct_invoke_failed", "dapr_permission_denied"):
        return "dapr-peer-transport"
    # Pinned Dapr serializes transport failures as JSON and forwards empty receiver
    # HTTP failures separately. This is source-derived boundary evidence only.
    if type(status) is int and status in (401, 403, 503) and category == "empty_http_body":
        return "receiver-http"
    return "unqualified"


def catalog_metadata_receiver_facts(observation, captured_files):
    """Retain only fixed endpoint facts and a verified SDK code digest for diagnosis."""
    surface = observation.get("ReceiverSurface") or {}
    endpoints = surface.get("Endpoints") or []
    matches = [endpoint for endpoint in endpoints if endpoint.get("Pattern") == "/admin/operational-index-metadata"]
    facts = {"endpoint_count": len(matches), "finite_profile_matches": surface.get("FenceVersion") == "eventstore-finite-dapr-receiver-v1"}
    if len(matches) == 1:
        endpoint = matches[0]
        policies = endpoint.get("Policies") or []
        facts.update(post_method_matches=endpoint.get("Methods") == ["POST"],
            canonical_policy_present="EventStoreWorkload:domain-service:metadata" in policies,
            canonical_operation_matches=endpoint.get("Operation") == "domain-service:metadata",
            policy_count=len(policies), admitted=endpoint.get("Admitted") is True,
            anonymous=endpoint.get("Anonymous") is True, patterned=endpoint.get("IsPattern") is True)
    owners = [value for name, value in (observation.get("ExecutableOwners") or {}).items()
              if name.split(",", 1)[0] == "Hexalith.EventStore.DomainService"]
    matched = len(owners) == 1 and isinstance(owners[0], str) and re.fullmatch(r"[0-9a-f]{64}", owners[0]) is not None \
        and captured_files.get("src/Hexalith.Timesheets/bin/Debug/net10.0/Hexalith.EventStore.DomainService.dll") == owners[0]
    facts["domain_sdk_code_matches"] = matched
    facts["domain_sdk_sha256"] = owners[0] if matched else None
    return facts


def catalog_custody_owner_matches(profile, owner):
    """Match the fixture's exact owner plus only the pinned SDK constructor defaults."""
    defaults = {"SecretStore": "secretstore", "LogicalSecretName": "eventstore-idempotency-ring"}
    required = {"CustodyDirectory", "AuthorityComponentName", "AuthorityStateKey", "ActiveDigestKeyGeneration", "DigestKeys", "SecretMapGeneration"}
    if (not isinstance(profile, dict) or not isinstance(owner, dict)
            or not required <= set(profile) <= required | set(defaults)
            or set(owner) != required | set(defaults)
            or any(name in profile and profile[name] != value for name, value in defaults.items())):
        return False
    return owner == {**profile, **defaults}


def verify_rendered_catalog_authority(rendered, endpoint):
    """Allow only the renderer's exact DB-13 scalar forms for this complete private backend."""
    message = "Rendered catalog authority differs from the private owner backend"
    rows = rendered["spec"]["metadata"]
    require(isinstance(rows, list) and all(isinstance(item, dict)
            and set(item) == {"name", "value"} and isinstance(item["name"], str) for item in rows), message)
    metadata = {item["name"]: item["value"] for item in rows}
    database = metadata.get("redisDB")
    require(type(database) in (str, int) and database in ("13", 13), message)
    metadata["redisDB"] = "13"
    require(len(metadata) == len(rows)
            and rendered["apiVersion"] == "dapr.io/v1alpha1" and rendered["kind"] == "Component"
            and rendered["metadata"]["name"] == "timesheets-route-authority"
            and rendered["spec"]["type"] == "state.redis" and rendered["spec"]["version"] == "v1"
            and isinstance(rendered["scopes"], list) and len(rendered["scopes"]) == 2
            and all(isinstance(scope, str) for scope in rendered["scopes"])
            and set(rendered["scopes"]) == {"eventstore", "timesheets"}
            and metadata == {"redisHost": endpoint, "redisPassword": "", "redisDB": "13", "keyPrefix": "name"}, message)


def verify_catalog_technical_membership(activation, observations):
    """Require the approved complete gateway-only composite and unchanged finite peer."""
    message = "Catalog technical receiver membership or witnesses differ from the selected gateway"
    receivers = activation.get("TechnicalHttpReceivers")
    require(isinstance(receivers, list) and len(receivers) == 1, message)
    receiver = receivers[0]
    require(isinstance(receiver, dict) and receiver.get("AppId") == "eventstore"
            and receiver.get("FenceVersion") == TECHNICAL_RECEIVER_PROFILE, message)
    gateway = observations["eventstore"]
    surface = gateway.get("ReceiverSurface")
    finite = observations["timesheets"].get("ReceiverSurface")
    require(isinstance(surface, dict) and surface.get("FenceVersion") == TECHNICAL_RECEIVER_PROFILE
            and isinstance(finite, dict) and finite.get("FenceVersion") == "eventstore-finite-dapr-receiver-v1"
            and finite.get("TechnicalHttp") is None, message)
    actions = receiver.get("Endpoints")
    canonical = lambda value: json.dumps(value, sort_keys=True, separators=(",", ":"))
    require(isinstance(actions, list) and 0 < len(actions) <= 256
            and isinstance(surface.get("TechnicalHttp"), list)
            and sorted(map(canonical, actions)) == sorted(map(canonical, surface["TechnicalHttp"])), message)
    keys = set()
    for action in actions:
        require(isinstance(action, dict) and isinstance(action.get("Pattern"), str)
                and isinstance(action.get("Methods"), list) and len(action["Methods"]) == 1
                and action["Methods"][0] in ("GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS")
                and isinstance(action.get("Policies"), list) and bool(action["Policies"])
                and isinstance(action.get("ActionIdentity"), str) and bool(action["ActionIdentity"]), message)
        key = (action["Pattern"].casefold(), tuple(action["Methods"]))
        require(key not in keys, message)
        keys.add(key)
        require(all(isinstance(action.get(name), str) and re.fullmatch(r"[0-9a-f]{64}", action[name])
                    for name in ("RequestDigest", "ResponseDigest", "PolicyDigest", "FilterDigest")), message)
    require(sum(action["Pattern"] == "/api/v1/commands/status/workload/{tenant}/{messageId}"
                and action["Methods"] == ["GET"] and action.get("CredentialKind") == "workload"
                for action in actions) == 1, message)
    actual = surface.get("Endpoints")
    require(isinstance(actual, list), message)
    technical = [endpoint for endpoint in actual if endpoint.get("Operation") == "sdk-technical-http"]
    require(len(technical) == len(actions) and all(sum(endpoint.get("Pattern") == action["Pattern"]
            and endpoint.get("Methods") == action["Methods"] and endpoint.get("Policies") == action["Policies"]
            and endpoint.get("Admitted") is True and endpoint.get("Anonymous") is False
            for endpoint in technical) == 1 for action in actions), message)
    invocations = receiver.get("Invocations")
    require(isinstance(invocations, list) and len(invocations) == 4
            and sorted(map(canonical, invocations)) == sorted(map(canonical, gateway.get("DeclaredInvocations") or [])), message)
    expected = {("timesheets", method, verb) for method, verb in CATALOG_RECEIVER_POLICIES["eventstore"][2]}
    require(all(isinstance(item, dict) and isinstance(item.get("HttpMethods"), list)
                and len(item["HttpMethods"]) == 1 for item in invocations)
            and {(item.get("CallerAppId"), item.get("Method"), item["HttpMethods"][0]) for item in invocations} == expected, message)
    return len(actions)


class IsolatedFixture:
    """Own private infrastructure and one exact Aspire AppHost lifecycle."""

    def __init__(self, command=None, port_check=None, log_directory=None,
                 ownership_check=None, resource_check=bound_resource_processes, public_port=8080,
                 security_smoke=False, route_catalog=False):
        self.public_port = validate_public_port(public_port)
        self.security_smoke = security_smoke
        self.route_catalog = route_catalog
        require(not (security_smoke and route_catalog), "Catalog and security fixture modes require separate runs")
        self.shared_comparison = SharedComparison()
        self.security_container = None
        self.command = command or self._command
        self.injected_command = command is not None
        self.port_check = port_check or (lambda: assert_ports_available(self.public_port))
        if log_directory:
            parent = Path(log_directory)
            parent.mkdir(parents=True, exist_ok=True)
            self.log_directory = Path(tempfile.mkdtemp(prefix="run-", dir=parent))
        else:
            self.log_directory = Path(tempfile.mkdtemp(prefix="timesheets-isolated-dapr-proof-"))
        self.scratch = None
        self.containers = {}
        self.fixture_id = secrets.token_hex(16)
        self.container_name = "timesheets-live-dapr-" + self.fixture_id + "-redis"
        self.container_id = None
        self.placement = None
        self.scheduler = None
        # MSBuild treats environment property names case-insensitively, while
        # the process environment on Linux can contain differently cased keys.
        controlled = {name.casefold() for name in ("Configuration", "UseHexalithProjectReferences", "UseNuGetDeps",
                                                  GATEWAY_EXECUTABLE_SETTING, GATEWAY_DIRECTORY_SETTING, SECURITY_SMOKE_SETTING,
                                                  "TIMESHEETS_LIVE_DAPR_CATALOG_PROFILE",
                                                  "TIMESHEETS_LIVE_DAPR_CATALOG_FIXTURE_ID",
                                                  "TIMESHEETS_LIVE_DAPR_CATALOG_GATEWAY_POLICY",
                                                  "TIMESHEETS_LIVE_DAPR_CATALOG_TIMESHEETS_POLICY",
                                                  "Timesheets:LocalPublicPort",
                                                  "TIMESHEETS_LIVE_DAPR_MODEL_ONLY", "TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY",
                                                  "TIMESHEETS_LIVE_DAPR_CONSTRUCTION_ONLY",
                                                  *SDK_LAUNCH_OVERRIDES)}
        if self.route_catalog:
            controlled.update(name.casefold() for name in (
                "Environment", "DOTNET_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT", "ASPIRE_ENVIRONMENT",
                "MSBUILDDISABLENODEREUSE"))
        self.environment = {}
        for name, value in os.environ.items():
            normalized = name.replace("__", ":").casefold()
            aliases = [normalized]
            for prefix in ("aspire_", "dotnet_", "aspnetcore_"):
                if normalized.startswith(prefix):
                    aliases.append(normalized[len(prefix):])
            if not any(alias in controlled for alias in aliases):
                if self.route_catalog and (name.startswith("DAPR_") or name == "APP_API_TOKEN"
                        or name == "TIMESHEETS_LIVE_DAPR_TRANSPORT_RUNTIME"):
                    continue
                self.environment[name] = value
        # Aspire evaluates launch and selected-child builds through MSBuild. Keep the owned
        # runtime on the Debug source graph built for this local journey even
        # when the repository or caller defaults to Release/package mode.
        self.environment.update({"Configuration": "Debug", "UseHexalithProjectReferences": "true",
                                 "UseNuGetDeps": "false", "Timesheets__LocalPublicPort": str(self.public_port)})
        if self.route_catalog:
            # The catalog owner APIs are Development-only. No launch profile
            # supplies the AppHost environment during the CLI launch.
            # SDK child builds must not leave reusable workers bound to this fixture.
            self.environment.update({"DOTNET_ENVIRONMENT": "Development", "ASPNETCORE_ENVIRONMENT": "Development",
                                     "ASPIRE_ENVIRONMENT": "Development", "MSBUILDDISABLENODEREUSE": "1"})
        self.ownership_check = ownership_check or self._owned_apphost
        self.resource_check = resource_check
        self.start_attempted = False
        self.journey_shutdown_confirmed = True
        self.ordinal = 0
        self.shared_before = None
        self.evidence_write_failed = False
        self.captured_processes = []
        self.admission_established = False
        self.fixture_backup = None
        self.redis_image = None
        self.dependency_graph = None
        self.dashboard_readiness = None
        self.dashboard_failure = None
        self.catalog_private = None
        self.catalog_before = None
        self.source_before = None
        self.catalog_verifier_directory = None
        self.catalog_verifier_configuration = "Debug"
        self.catalog_verifier_identity = None

    def capture_source_identity(self):
        """Bind runtime receipts to exact source diffs, restored packages and built assets."""
        identities = {}
        roots = {"timesheets": ROOT, "eventstore": GATEWAY_PROJECT.parents[2]}
        if self.dependency_graph is not None:
            current = self.evaluate_dependency_graph()
            require(current == self.dependency_graph, "Evaluated dependency graph changed during qualification")
            roots.update({name: ROOT / "references" / name for name in self.dependency_graph["repositories"]})
            # CPM imports Builds without a ProjectReference edge. Its dirty
            # catalog changes must participate in the same source comparison.
            roots["Hexalith.Builds"] = ROOT / "references/Hexalith.Builds"
            identities["dependency_graph"] = self.dependency_graph
        for name, root in roots.items():
            head = self.run_command(["git", "-C", str(root), "rev-parse", "HEAD"]).stdout.strip()
            # Retain a digest of local modifications, never raw patch contents.
            patch = self.command(["git", "-C", str(root), "diff", "HEAD", "--no-ext-diff", "--binary"],
                                 cwd=ROOT, env=self.environment, timeout=30)
            require(patch.returncode == 0 and re.fullmatch(r"[0-9a-f]{40}", head), "Source identity is unavailable")
            identities[name] = {"head": head, "diff_sha256": hashlib.sha256(patch.stdout.encode()).hexdigest()}
            untracked = self.command(["git", "-C", str(root), "ls-files", "--others", "--exclude-standard", "-z"],
                                     cwd=ROOT, env=self.environment, timeout=30)
            require(untracked.returncode == 0, "Untracked build input identity is unavailable")
            inputs = {}
            for value in untracked.stdout.split("\0"):
                if not value:
                    continue
                relative = Path(value)
                require(not relative.is_absolute() and ".." not in relative.parts, "Untracked build input identity is unavailable")
                in_tree = relative.parts[0] in ("src", "tests", "test", "scripts")
                root_build = len(relative.parts) == 1 and (relative.suffix.casefold() in (
                    ".props", ".targets", ".proj", ".csproj", ".fsproj", ".vbproj", ".slnx", ".rsp")
                    or relative.name.casefold() in ("global.json", "nuget.config", "package.json", "package-lock.json",
                        "pnpm-lock.yaml", "yarn.lock", "bun.lock", "bun.lockb", "makefile"))
                if (in_tree or root_build) and not any(part.casefold() in (
                        "bin", "obj", "testresults", "__pycache__", "evidence", "_bmad-output") for part in relative.parts):
                    inputs[str(relative)] = hashlib.sha256((root / relative).read_bytes()).hexdigest()
            identities[name]["untracked_build_inputs"] = dict(sorted(inputs.items()))
        gateway = Path(self.environment[GATEWAY_EXECUTABLE_SETTING])
        # The Linux apphost's dotted assembly name is its complete native file
        # name, so append .dll instead of replacing the apparent final suffix.
        gateway_assembly = gateway.with_name(gateway.name + ".dll")
        module = ROOT / "src/Hexalith.Timesheets/bin/Debug/net10.0/Hexalith.Timesheets"
        integration = ROOT / "tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests"
        paths = [APPHOST, APPHOST.parent / "Program.cs", APPHOST.parent / "SecurityConstructionProbe.cs",
                 APPHOST.parent / "KeycloakRealms/hexalith-realm.json", APPHOST.parent / "obj/project.assets.json",
                 APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost.dll",
                 gateway, gateway_assembly, module, module.with_name(module.name + ".dll"),
                 integration, integration.with_name(integration.name + ".dll")]
        if self.dependency_graph is not None:
            # Fingerprint complete static output trees: configuration, native
            # runtime assets, satellite assemblies and nested content all affect
            # execution. Retain only paths and content digests, never contents.
            for executable in (APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost", gateway, module, integration):
                paths.append(executable)
                paths.extend(path for path in executable.parent.rglob("*") if path.is_file())
            paths.extend((ROOT / record.get("project", project.split("|", 1)[0])).parent / "obj/project.assets.json"
                         for project, record in self.dependency_graph["projects"].items())
            paths = [path.resolve() for path in paths]
        identities["files"] = {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}
        if self.catalog_verifier_directory is not None:
            identities["catalog_binding_verifier"] = self.catalog_verifier_manifest()
        return identities

    def evaluated_import_identity(self, arguments, project):
        """Read preprocessing only in memory; retain hashes of actual imported files."""
        message = "Evaluated import identity is unavailable; no resources started"
        result = self.command([*arguments, "-preprocess"], cwd=project.parent,
                              env=self.environment, timeout=30)
        require(result.returncode == 0, message)
        # MSBuild surrounds every expanded import with a comment containing its
        # absolute filename on a separate line. Never transcript this source XML.
        paths = {project}
        for comment in re.findall(r"<!--(.*?)-->", result.stdout, re.DOTALL):
            for line in comment.splitlines():
                candidate = Path(line.strip())
                if candidate.is_absolute() and candidate.is_file():
                    paths.add(candidate.resolve())
        return {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(paths)}

    def evaluate_dependency_graph(self):
        """Evaluate the real project closure with inherited, case-insensitive globals."""
        message = "Evaluated dependency graph is outside the recorded default roots; no resources started"
        declared = set(re.findall(r"(?m)^\s*path\s*=\s*(references/[^\r\n]+)", (ROOT / ".gitmodules").read_text()))
        allowed = set()
        for relative in declared:
            candidate = (ROOT / relative).resolve()
            require(candidate.is_relative_to(ROOT / "references"), message)
            if candidate.is_dir():
                allowed.add(candidate)
        fixed = {"configuration": "Debug", "usehexalithprojectreferences": "true", "usenugetdeps": "false"}

        def properties(values):
            result = {}
            for value in values.split(";"):
                if not value:
                    continue
                name, separator, content = value.partition("=")
                require(bool(separator) and bool(name.strip()), message)
                result[name.strip().casefold()] = content
            return result

        def fingerprint(values):
            return {name: hashlib.sha256(value.encode()).hexdigest() for name, value in sorted(values.items())}

        projects = {}
        repositories = set()
        pending = [(APPHOST, fixed), (ROOT / "tests/Hexalith.Timesheets.IntegrationTests/Hexalith.Timesheets.IntegrationTests.csproj", fixed)]
        seen = set()
        while pending:
            project, globals_ = pending.pop()
            project = project.resolve()
            signature = (str(project), tuple(sorted(globals_.items())))
            if signature in seen:
                continue
            seen.add(signature)
            require(all(globals_.get(name) == value for name, value in fixed.items()), message)
            owner = next((root for root in allowed if project.is_relative_to(root)), None)
            require(owner is not None or project.is_relative_to(ROOT / "src") or project.is_relative_to(ROOT / "tests"), message)
            if owner is not None:
                repositories.add(owner.name)
            arguments = ["dotnet", "msbuild", str(project), "-nologo",
                *["-p:" + name + "=" + value for name, value in sorted(globals_.items())], "-m:1", "/nr:false"]
            # Globals may contain private values. Query output and preprocessing
            # stay in memory; evidence records only property-value fingerprints.
            result = self.command([*arguments, "-getProperty:" + ",".join(DEPENDENCY_ROOTS), "-getItem:ProjectReference"],
                                  cwd=project.parent, env=self.environment, timeout=30)
            require(result.returncode == 0, message)
            try:
                evaluated = json.loads(result.stdout)
                evaluated_properties, references = evaluated["Properties"], evaluated["Items"]["ProjectReference"]
                require(isinstance(evaluated_properties, dict) and isinstance(references, list), message)
                roots = {}
                for name, relative in DEPENDENCY_ROOTS.items():
                    value = evaluated_properties.get(name, "")
                    require(isinstance(value, str), message)
                    if value:
                        path = Path(value.replace("\\", "/"))
                        root = (path if path.is_absolute() else project.parent / path).resolve()
                        require(root == (ROOT / "references" / relative).resolve(), message)
                        roots[name] = str(root.relative_to(ROOT))
                edges = []
                for reference in references:
                    reference = {name.casefold(): value for name, value in reference.items()}
                    path = Path(reference["fullpath"].replace("\\", "/"))
                    target = (path if path.is_absolute() else project.parent / path).resolve()
                    inherited = dict(globals_)
                    # MSBuild edge properties replace inherited global values;
                    # edge removals apply case-insensitively afterwards.
                    for metadata in ("Properties", "AdditionalProperties", "SetConfiguration", "SetPlatform", "SetTargetFramework"):
                        inherited.update(properties(reference.get(metadata.casefold(), "")))
                    removed = reference.get("globalpropertiestoremove", "") + ";" + reference.get("undefineproperties", "")
                    for name in removed.split(";"):
                        inherited.pop(name.strip().casefold(), None)
                    require(all(inherited.get(name) == value for name, value in fixed.items()), message)
                    edges.append({"project": str(target.relative_to(ROOT)), "globals": fingerprint(inherited)})
                    pending.append((target, inherited))
                digest = hashlib.sha256(json.dumps(sorted(globals_.items())).encode()).hexdigest()
                key = str(project.relative_to(ROOT)) + "|" + digest
                projects[key] = {"project": str(project.relative_to(ROOT)), "globals": fingerprint(globals_), "roots": roots,
                    "imports": self.evaluated_import_identity(arguments, project),
                    "references": sorted(edges, key=lambda row: (row["project"], json.dumps(row["globals"], sort_keys=True)))}
            except (ValueError, KeyError, TypeError, OSError):
                raise FixtureError(message) from None
        return {"repositories": sorted(repositories), "projects": dict(sorted(projects.items()))}

    def validate_dependency_graph(self):
        """Record the admitted graph before a build or any resource creation."""
        self.dependency_graph = self.evaluate_dependency_graph()
        require(self.write_evidence("dependency-graph.json", json.dumps(self.dependency_graph, indent=2) + "\n"),
                "Dependency graph evidence is incomplete; no resources started")

    def verify_security_runtime(self):
        """Verify owned current-graph services after Aspire readiness, without disclosing secrets."""
        identities = self.resource_check(self.scratch, self.placement, self.scheduler)
        assets_path = APPHOST.parent / "obj/project.assets.json"
        try:
            host = runtime_host_identity(identities, assets_path)
        except FixtureError:
            try:
                diagnostic = runtime_host_diagnostics(identities, assets_path, self.scratch)
            except Exception:
                # Diagnostic collection must never replace the original refusal
                # or retain arbitrary exception text from private process data.
                diagnostic = {"expected_package_directories": {}, "observed_processes": [], "candidate_processes": []}
            self.write_evidence("runtime-host-failure.json", json.dumps(diagnostic, indent=2) + "\n")
            raise
        self.captured_processes = identities
        host["dashboard_readiness"] = self.observe_dashboard_readiness(host["dashboard"], "initial")
        self.dashboard_readiness = host["dashboard_readiness"]
        require(self.write_evidence("runtime-host.json", json.dumps(host, indent=2) + "\n"), "Runtime host evidence is incomplete")
        listing = self.run_command(["docker", "container", "ls", "--all", "--no-trunc", "--filter",
            "label=hexalith.timesheets.components=" + self.scratch, "--format", "{{.ID}}"])
        ids = listing.stdout.split()
        require(len(ids) == 1 and re.fullmatch(r"[0-9a-f]{64}", ids[0]), "Security container ownership is unavailable")
        self.security_container = ids[0]
        # Docker inspect includes generated credentials. Keep this response only in
        # memory, and never pass it through a command transcript or exception.
        try:
            inspected = self.command(["docker", "inspect", ids[0]], cwd=ROOT, env=self.environment, timeout=30)
        except (OSError, subprocess.TimeoutExpired):
            raise FixtureError("Security container inspection failed") from None
        require(inspected.returncode == 0, "Security container inspection failed")
        try:
            container, = json.loads(inspected.stdout)
            require(container["Id"] == ids[0]
                    and container["Config"]["Labels"]["hexalith.timesheets.components"] == self.scratch
                    and container["State"]["Running"] is True, "Security container ownership is unavailable")
            for bindings in container["NetworkSettings"]["Ports"].values():
                require(bindings is None or all(binding["HostIp"] == "127.0.0.1" for binding in bindings),
                        "Security container publication is not private")
            self.security_container = ids[0]
            handoff_diagnostic = {}
            try:
                handoff = security_credential_handoff(container, self.public_port, diagnostics=handoff_diagnostic,
                    describe_command=self.command, describe_environment=self.environment)
            except FixtureError:
                self.write_evidence("security-credential-failure.json", json.dumps(handoff_diagnostic, indent=2) + "\n")
                raise
        except (ValueError, KeyError, TypeError):
            raise FixtureError("Security container inspection is unsafe") from None
        require(self.write_evidence("security-runtime.json", json.dumps({"container": self.security_container,
            "loopback_only": True, "http_endpoint": handoff_diagnostic.get("http_endpoint"), **handoff}, indent=2) + "\n"), "Security runtime evidence is incomplete")

    def write_evidence(self, name, contents):
        """A diagnostic write cannot replace the journey's original result."""
        try:
            (self.log_directory / name).write_text(contents)
            return True
        except OSError:
            self.evidence_write_failed = True
            print("Fixture evidence could not be written; cleanup evidence is incomplete", flush=True)
            return False

    @staticmethod
    def _command(arguments, *, cwd, env, timeout):
        return subprocess.run(arguments, cwd=cwd, env=env, timeout=timeout,
                              text=True, encoding="utf-8", errors="replace", capture_output=True, check=False)

    def _journey_command(self, arguments, *, cwd, env, timeout):
        # Adopt descendants so a killed xUnit parent cannot leave an unreaped
        # Python journey. AppHost stop always goes through Aspire separately.
        require(ctypes.CDLL(None, use_errno=True).prctl(36, 1, 0, 0, 0) == 0,
                "Journey descendant ownership could not be established")
        process = subprocess.Popen(arguments, cwd=cwd, env=env, start_new_session=True,
                                   text=True, encoding="utf-8", errors="replace",
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        self.journey_shutdown_confirmed = False
        result = None
        failure = None
        try:
            output, error = process.communicate(timeout=timeout)
            result = subprocess.CompletedProcess(arguments, process.returncode, output, error)
        except KeyboardInterrupt as interruption:
            failure = interruption
        except subprocess.TimeoutExpired as interruption:
            failure = interruption
        finally:
            try:
                stop_process_group(process)
                process.wait(timeout=5)
                self.journey_shutdown_confirmed = True
            except (FixtureError, OSError, subprocess.TimeoutExpired, KeyboardInterrupt):
                # Keep the captured command result even if shutdown is unknown.
                # Cleanup will retain all infrastructure and report incomplete.
                self.journey_shutdown_confirmed = False
        if failure is not None:
            try:
                output, error = process.communicate(timeout=0.2)
                failure.stdout, failure.stderr = output, error
            except subprocess.TimeoutExpired as captured:
                failure.stdout, failure.stderr = captured.stdout, captured.stderr
            raise failure
        return result

    def run_command(self, arguments, timeout=60, check=True, journey=False):
        self.ordinal += 1
        label = f"{self.ordinal:02d}-" + Path(arguments[0]).name + "-" + re.sub(r"[^a-zA-Z0-9-]", "", arguments[1])
        command = self._journey_command if journey and not self.injected_command else self.command
        private_inventory = self.route_catalog and arguments[:2] in (["dapr", "list"], ["aspire", "ps"])
        try:
            result = command(arguments, cwd=ROOT, env=self.environment, timeout=timeout)
        except KeyboardInterrupt as interruption:
            self.write_evidence(label + ".log", "Command interrupted.\nStdout:\n"
                + ("Inventory output withheld.\n" if private_inventory else sanitized(getattr(interruption, "stdout", "")))
                + "Stderr:\n" + ("Inventory diagnostics withheld.\n" if private_inventory
                                 else sanitized(getattr(interruption, "stderr", ""))))
            raise
        except (subprocess.TimeoutExpired, OSError) as error:
            self.write_evidence(label + ".log",
                "Command could not complete safely.\nStdout:\n"
                + ("Inventory output withheld.\n" if private_inventory else sanitized(getattr(error, "stdout", "")))
                + "Stderr:\n" + ("Inventory diagnostics withheld.\n" if private_inventory
                                 else sanitized(getattr(error, "stderr", ""))))
            raise FixtureError(f"{label} could not complete safely") from None
        stdout_evidence = sanitized(result.stdout)
        if private_inventory:
            try:
                rows = json_output(result.stdout)
                stdout_evidence = json.dumps({"inventory_shape": "array" if isinstance(rows, list) else "other",
                                               "entry_count": len(rows) if isinstance(rows, list) else None}) + "\n"
            except FixtureError:
                stdout_evidence = "Inventory output unavailable or malformed.\n"
        self.write_evidence(label + ".log",
            "Command: " + sanitized(" ".join(arguments)) + f"Exit: {result.returncode}\nStdout:\n"
            + stdout_evidence + "Stderr:\n"
            + ("Inventory diagnostics withheld.\n" if private_inventory else sanitized(result.stderr)))
        if check and result.returncode != 0:
            raise FixtureError(f"{label} exited {result.returncode}; see sanitized evidence", result.returncode)
        return result

    def preflight(self):
        self.admission_established = False
        state = {"resources_started": bool(self.containers) or self.start_attempted,
                 "infrastructure_started": bool(self.containers), "apphost_start_attempted": self.start_attempted}
        try:
            self._preflight()
        except FixtureError as error:
            self.write_evidence("admission.json", json.dumps({"admitted": False, **state,
                "reason": str(error)}, indent=2) + "\n")
            raise
        require(self.write_evidence("admission.json", json.dumps({"admitted": True, **state}, indent=2) + "\n"),
                "Admission evidence is incomplete; no AppHost started")
        self.admission_established = True

    def _preflight(self):
        try:
            dapr = json.loads(self.run_command(["dapr", "list", "--output", "json"]).stdout)
        except ValueError:
            raise FixtureError("Dapr application inventory is unavailable; no AppHost started") from None
        require(isinstance(dapr, list) and not dapr, "A Dapr application is already running or inventory is unsafe; no AppHost started")
        require(not running_dapr_processes(), "A Dapr sidecar is present outside the empty CLI inventory; no resources started")
        apps = json_output(self.run_command(["aspire", "ps", "--non-interactive", "--format", "Json"]).stdout)
        require(isinstance(apps, list) and not apps, "An AppHost is already running; no AppHost started")
        self.port_check()

    def resolve_gateway_invocation(self):
        """Resolve the exact source project's native launch without creating resources."""
        result = self.run_command(["dotnet", "msbuild", str(GATEWAY_PROJECT), "-nologo",
            "-target:ComputeRunArguments", "-getProperty:RunCommand,RunArguments,RunWorkingDirectory",
            "-p:Configuration=Debug", "-p:UseHexalithProjectReferences=true", "-p:UseNuGetDeps=false",
            "-m:1", "/nr:false"])
        message = "Gateway SDK run metadata is unsupported; no resources started"
        try:
            metadata = json.loads(result.stdout)
            properties = metadata["Properties"]
            executable = properties["RunCommand"]
            arguments = properties["RunArguments"]
            directory = properties["RunWorkingDirectory"]
            require(all(isinstance(value, str) for value in (executable, arguments, directory)), message)
            require(arguments == "" and Path(executable).is_absolute() and Path(executable).is_file()
                    and os.access(executable, os.X_OK) and Path(directory).is_absolute()
                    and Path(directory).is_dir(), message)
        except (ValueError, KeyError, TypeError, OSError):
            raise FixtureError(message) from None
        require(self.write_evidence("gateway-invocation.json", sanitized(json.dumps({
            "project": str(GATEWAY_PROJECT), "configuration": "Debug", "source_references": True,
            "executable": executable, "arguments": arguments, "working_directory": directory}, indent=2)) + "\n"),
            "Gateway invocation evidence is incomplete; no resources started")
        self.environment.update({GATEWAY_EXECUTABLE_SETTING: executable, GATEWAY_DIRECTORY_SETTING: directory})

    def capture_shared_snapshot(self, reader=None):
        """Retain a safe failure receipt without repeating an observation."""
        try:
            return (reader or read_only_shared_snapshot)(self.shared_comparison)
        except (SharedObservationError, KeyboardInterrupt) as error:
            observation = getattr(error, "shared_observation", None)
            if observation is not None:
                self.write_evidence("shared-observation-failure.json", json.dumps(observation, indent=2) + "\n")
            raise

    def capture_shared_baseline(self, snapshot_reader=None):
        """Admit only two matching read-only observations before creating resources."""
        reader = snapshot_reader or read_only_shared_snapshot
        before = self.capture_shared_snapshot(reader)
        before_observed = time.time_ns()
        observation_started = time.monotonic()
        require(self.write_evidence("shared-baseline-before.json", json.dumps(before, indent=2) + "\n"),
                "Shared baseline evidence is incomplete; no resources started")
        time.sleep(1)
        after = self.capture_shared_snapshot(reader)
        after_observed = time.time_ns()
        elapsed = time.monotonic() - observation_started
        require(self.write_evidence("shared-before.json", json.dumps(after, indent=2) + "\n"),
                "Shared baseline evidence is incomplete; no resources started")
        preservation = shared_preservation(before, after)
        require(all(snapshot["scheduler"].get("revision") is not None for snapshot in (before, after)),
                "Scheduler baseline revision is unavailable; no resources started")
        revision_matches = before["scheduler"]["revision"] == after["scheduler"]["revision"]
        preservation["scheduler"]["baseline_revision_matches"] = revision_matches
        if not revision_matches:
            preservation["matches"] = False
            preservation["mismatches"].append("Scheduler revision changed during baseline observation")
        preservation["observation"] = {"before_unix_ns": before_observed, "after_unix_ns": after_observed,
                                       "elapsed_seconds": elapsed, "requested_pause_seconds": 1}
        require(self.write_evidence("shared-baseline-preservation.json", json.dumps(preservation, indent=2) + "\n"),
                "Shared baseline evidence is incomplete; no resources started")
        require(preservation["matches"], "Retained shared services changed before fixture startup; no resources started: "
                + "; ".join(preservation["mismatches"]))
        self.shared_before = after

    def capture_gateway_process(self, phase):
        """Retain validated native ownership and include its identity in exact cleanup."""
        process = gateway_process_identity(self.environment[GATEWAY_EXECUTABLE_SETTING],
            self.resource_check(self.scratch, self.placement, self.scheduler))
        for identity in (process, process["parent_identity"]):
            if not any(row["pid"] == identity["pid"] and row["start"] == identity["start"] for row in self.captured_processes):
                self.captured_processes.append({key: identity[key] for key in ("pid", "start", "state")})
        require(self.write_evidence("gateway-process-" + phase + ".json", sanitized(json.dumps(process, indent=2))),
                "Gateway process evidence is incomplete")
        return process

    def fixture_cutover_source(self):
        """Bind the empty-store cutover to this exact local source and native build."""
        root = GATEWAY_PROJECT.parents[2]
        commit = self.run_command(["git", "-C", str(root), "rev-parse", "HEAD"]).stdout.strip()
        patch = self.command(["git", "-C", str(root), "diff", "HEAD", "--no-ext-diff", "--binary"],
                             cwd=ROOT, env=self.environment, timeout=30)
        require(re.fullmatch(r"[0-9a-f]{40}", commit) and patch.returncode == 0,
                "Fixture cutover source identity is unavailable")
        native = Path(self.environment[GATEWAY_EXECUTABLE_SETTING])
        return {"head": commit, "diff_sha256": hashlib.sha256(patch.stdout.encode()).hexdigest(),
                "native_sha256": hashlib.sha256(native.read_bytes()).hexdigest(),
                "assembly_sha256": hashlib.sha256(native.with_name(native.name + ".dll").read_bytes()).hexdigest()}

    def prepare_fixture_bootstrap(self):
        """Durably preserve a reconstructible empty backup of only the fresh owned Redis."""
        require(self.admission_established and not self.start_attempted and self.fixture_backup is None
                and self.environment.get("TIMESHEETS_LIVE_DAPR_FIXTURE") == "1",
                "Fixture bootstrap requires admitted fresh infrastructure before AppHost startup")
        validate_components(self.scratch, self.environment.get("TIMESHEETS_LIVE_DAPR_REDIS_HOST", ""))
        require(self.recover_container(self.container_name) == self.container_id and self.container_id is not None
                and self.container_id in self.containers.values()
                and isinstance(self.redis_image, str) and re.fullmatch(r"sha256:[0-9a-f]{64}", self.redis_image),
                "Fixture bootstrap Redis ownership is unavailable")
        databases = {}
        for database in range(16):
            count = self.run_command(["docker", "exec", self.container_id, "redis-cli", "--raw", "-n", str(database), "DBSIZE"]).stdout.strip()
            require(count == "0", "Fixture bootstrap requires every private Redis database to be empty")
            databases[str(database)] = 0
        receipt = {"format": "empty-disposable-redis-backup-v1", "fixture_id": self.fixture_id,
            "container_id": self.container_id, "image": self.redis_image, "databases": databases,
            "components_sha256": {str(relative): hashlib.sha256((Path(self.scratch) / relative).read_bytes()).hexdigest()
                                  for relative in COMPONENT_FILES}, "source": self.fixture_cutover_source(),
            "admitted_without_old_writers_or_retry_workers": True, "downgrade_prohibited": True,
            "scope": "fresh-private-local-fixture-only"}
        body = json.dumps(receipt, indent=2, sort_keys=True) + "\n"
        name = "empty-fixture-backup.json"
        require(self.write_evidence(name, body), "Empty fixture backup could not be retained")
        path = self.log_directory / name
        # This is the complete backup for an empty store, not a deployed/shared
        # database backup. Persist it before allowing the platform activation API.
        for target in (path, self.log_directory):
            descriptor = os.open(target, os.O_RDONLY)
            try:
                os.fsync(descriptor)
            finally:
                os.close(descriptor)
        self.fixture_backup = {"path": path, "sha256": hashlib.sha256(body.encode()).hexdigest(), "receipt": receipt}

    def bootstrap_fixture_protocol(self):
        """Activate the platform v2 marker through its authenticated operator API only."""
        diagnostic = {"stage": "preconditions", "activated": False, "attempts": 0, "http_status": None,
                      "category": "preconditions_failed", "scope": "fresh-private-local-fixture-only"}
        failed = True
        try:
            backup = self.fixture_backup
            require(self.admission_established and self.start_attempted and backup is not None
                    and self.environment.get("TIMESHEETS_LIVE_DAPR_FIXTURE") == "1",
                    "Fixture protocol bootstrap has no admitted empty backup")
            require(hashlib.sha256(backup["path"].read_bytes()).hexdigest() == backup["sha256"]
                    and json.loads(backup["path"].read_text()) == backup["receipt"], "Empty fixture backup identity changed")
            receipt = backup["receipt"]
            require(receipt["fixture_id"] == self.fixture_id and receipt["container_id"] == self.container_id
                    and receipt["image"] == self.redis_image and receipt["databases"] == {str(index): 0 for index in range(16)}
                    and self.recover_container(self.container_name) == self.container_id,
                    "Fixture bootstrap identity changed after its empty backup")
            validate_components(self.scratch, self.environment["TIMESHEETS_LIVE_DAPR_REDIS_HOST"])
            wait_fixture_gateway_alive(diagnostic)
            diagnostic.update(stage="preconditions", category="preconditions_failed")
            require(receipt["source"] == self.fixture_cutover_source(), "Fixture bootstrap source changed after its empty backup")
            require(receipt["components_sha256"] == {str(relative): hashlib.sha256((Path(self.scratch) / relative).read_bytes()).hexdigest()
                                                     for relative in COMPONENT_FILES}, "Fixture bootstrap component identity changed")
            sidecars = verify_runtime_components(self.environment["TIMESHEETS_LIVE_DAPR_REDIS_HOST"], self.scratch,
                self.placement, self.scheduler, catalog_profile=self.catalog_private,
                observation=lambda result: self.write_evidence(
                    "runtime-dapr-identity-failure.json", json.dumps(result, indent=2) + "\n"))
            require({row["pid"] for row in running_dapr_processes()} == {row["pid"] for row in sidecars.values()},
                    "Fixture bootstrap found a sidecar outside the admitted private topology")
            if self.catalog_private is not None:
                proof = verify_catalog_transport(sidecars, self.catalog_private, self.placement, self.scheduler)
                require(self.write_evidence("route-transport-before.json", json.dumps(proof, indent=2) + "\n"),
                        "Owned transport evidence is incomplete before writer activation")
            gateway = gateway_process_identity(self.environment[GATEWAY_EXECUTABLE_SETTING],
                self.resource_check(self.scratch, self.placement, self.scheduler))
            signing_key = fixture_gateway_signing_key(gateway, self.scratch)
            request = {"cutoverCommit": receipt["source"]["head"],
                "backupReference": str(backup["path"].resolve()) + "#sha256=" + backup["sha256"],
                "writersQuiesced": True, "retryWorkersQuiesced": True, "downgradeProhibitedAcknowledged": True}
            diagnostic.update(backup_sha256=backup["sha256"], cutover_commit=receipt["source"]["head"])
            activate_fixture_writer_protocol(signing_key, request, diagnostic)
            failed = False
        finally:
            written = self.write_evidence("projection-writer-bootstrap.json", json.dumps(diagnostic, indent=2) + "\n")
            if not failed:
                require(written, "Fixture protocol bootstrap evidence is incomplete")

    def capture_runtime_readiness(self):
        """Observe the admitted private graph immediately after Aspire readiness."""
        sidecars, refusal = {}, None
        try:
            sidecars = verify_runtime_components(self.environment["TIMESHEETS_LIVE_DAPR_REDIS_HOST"], self.scratch,
                self.placement, self.scheduler, catalog_profile=self.catalog_private,
                observation=lambda result: self.write_evidence(
                    "runtime-dapr-identity-failure.json", json.dumps(result, indent=2) + "\n"))
        except FixtureError as error:
            refusal = error
        try:
            gateway = gateway_process_identity(self.environment[GATEWAY_EXECUTABLE_SETTING],
                self.resource_check(self.scratch, self.placement, self.scheduler))
            readiness = gateway_readiness_observation(gateway, sidecars.get("eventstore", {"arguments": []}), self.scratch)
        except FixtureError:
            if refusal is None:
                raise
            readiness = {"gateway_ready": "Unavailable", "sidecar_metadata": "Unavailable"}
        written = self.write_evidence("runtime-readiness.json", json.dumps(readiness, indent=2) + "\n")
        if refusal is not None:
            raise refusal
        require(written, "Runtime readiness evidence is incomplete")

    def catalog_verifier_manifest(self):
        directory = Path(self.catalog_verifier_directory)
        relative_output = Path("bin") / self.catalog_verifier_configuration / "net10.0"
        output = directory / relative_output
        return {"source_sha256": hashlib.sha256((ROOT / "scripts/CatalogBindingVerifier.cs").read_bytes()).hexdigest(),
                "compiler_inputs": {name: hashlib.sha256((directory / name).read_bytes()).hexdigest()
                                    for name in ("Program.cs", "CatalogBindingVerifier.csproj")},
                "artifacts": {str(path.relative_to(directory)): hashlib.sha256(path.read_bytes()).hexdigest()
                              for path in sorted(output.rglob("*")) if path.is_file()},
                "sdk_assemblies": {name: hashlib.sha256((APPHOST.parent / relative_output / (name + ".dll")).read_bytes()).hexdigest()
                                   for name in ("Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults")}}

    def prepare_catalog_binding_verifier(self, configuration="Debug"):
        require(self.catalog_verifier_directory is None, "Catalog SDK verifier was already prepared")
        require(configuration in ("Debug", "Release"), "Catalog SDK verifier configuration is invalid")
        self.catalog_verifier_configuration = configuration
        relative_output = Path("bin") / configuration / "net10.0"
        directory = Path(tempfile.mkdtemp(prefix="timesheets-catalog-verifier-"))
        directory.chmod(0o700)
        self.catalog_verifier_directory = directory
        shutil.copyfile(ROOT / "scripts/CatalogBindingVerifier.cs", directory / "Program.cs")
        project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
        properties = ET.SubElement(project, "PropertyGroup")
        for name, value in (("OutputType", "Exe"), ("TargetFramework", "net10.0"),
                            ("ImplicitUsings", "enable"), ("Nullable", "enable")):
            ET.SubElement(properties, name).text = value
        items = ET.SubElement(project, "ItemGroup")
        ET.SubElement(items, "FrameworkReference", Include="Microsoft.AspNetCore.App")
        for name in ("Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults"):
            reference = ET.SubElement(items, "Reference", Include=name)
            ET.SubElement(reference, "HintPath").text = str(APPHOST.parent / relative_output / (name + ".dll"))
        (directory / "CatalogBindingVerifier.csproj").write_text(ET.tostring(project, encoding="unicode"))
        self.run_command(["dotnet", "build", str(directory / "CatalogBindingVerifier.csproj"),
                          "--configuration", configuration, "--disable-build-servers", "-warnaserror", "-m:1", "/nr:false"], timeout=60)
        manifest = self.catalog_verifier_manifest()
        require(bool(manifest["artifacts"]) and manifest["compiler_inputs"]["Program.cs"] == manifest["source_sha256"],
                "Catalog SDK verifier artifact or source is unavailable")
        for name, digest in manifest["sdk_assemblies"].items():
            require(manifest["artifacts"].get(str(relative_output / (name + ".dll"))) == digest,
                    "Catalog SDK verifier references different captured assemblies")
        self.catalog_verifier_identity = manifest

    def verify_catalog_sdk_binding(self, directory):
        message = "Catalog SDK binding proof or captured verifier is invalid"
        require(self.catalog_verifier_directory is not None and self.source_before is not None
                and self.catalog_verifier_manifest() == self.catalog_verifier_identity
                and self.source_before.get("catalog_binding_verifier") == self.catalog_verifier_identity, message)
        gateway = Path(self.environment[GATEWAY_EXECUTABLE_SETTING])
        result = self.command([str(self.catalog_verifier_directory / "bin" / self.catalog_verifier_configuration / "net10.0/CatalogBindingVerifier"),
                               str(directory), str(gateway.parent)],
                              cwd=ROOT, env=self.environment, timeout=10)
        # The verifier withholds all parser/SDK diagnostics; never transcript unknown output here.
        try:
            proof = json.loads(result.stdout)
        except (ValueError, TypeError):
            raise FixtureError(message) from None
        if isinstance(proof, dict) and proof.get("valid") is False and proof.get("stage") in (
                "input", "catalog", "membership", "inventory", "invocations", "code", "status", "surface", "topology", "proof"):
            self.write_evidence("catalog-binding-verifier-failure.json", json.dumps({"valid": False, "stage": proof["stage"]}) + "\n")
        require(result.returncode == 0 and isinstance(proof, dict)
                and set(proof) == {"valid", "binding_digest", "technical_action_count", "sdk_assemblies", "action_assemblies"}
                and proof["valid"] is True and isinstance(proof["binding_digest"], str)
                and re.fullmatch(r"[0-9a-f]{64}", proof["binding_digest"])
                and type(proof["technical_action_count"]) is int and proof["technical_action_count"] > 0
                and proof["sdk_assemblies"] == self.catalog_verifier_identity["sdk_assemblies"]
                and isinstance(proof["action_assemblies"], dict) and bool(proof["action_assemblies"])
                and all(name in ("Hexalith.EventStore.Gateway", "Hexalith.EventStore.Admin.Server")
                        and self.source_before["files"].get(str((gateway.parent / (name + ".dll")).relative_to(ROOT))) == digest
                        for name, digest in proof["action_assemblies"].items()), message)
        return proof

    def capture_route_catalog(self, phase):
        try:
            return self._capture_route_catalog(phase)
        except FixtureError:
            self.capture_catalog_failure_diagnostics()
            raise
        except (OSError, ValueError, TypeError, KeyError, IndexError, AttributeError, UnicodeError, RecursionError):
            self.capture_catalog_failure_diagnostics()
            raise FixtureError("SDK catalog observation is malformed or incomplete") from None

    def capture_catalog_failure_diagnostics(self, phase="catalog"):
        """Retain only constant diagnostic matches from fixed resources under verified ownership."""
        receipt = {"ownership_verified": False, "resources": {}, "raw_output_retained": False,
                   "phase": "journey" if phase == "journey" else "catalog"}
        try:
            if not self.start_attempted or not self.ownership_check():
                receipt["status"] = "ownership_unavailable"
                return
            receipt["ownership_verified"] = True
            for resource in ("eventstore", "timesheets"):
                row = {"status": "unavailable", "raw_output_withheld": True,
                       "sdk_categories": [], "exception_types": [], "frames": []}
                receipt["resources"][resource] = row
                try:
                    if not self.ownership_check():
                        row["status"] = "ownership_unavailable"
                        break
                    result = self.command(["aspire", "logs", resource, "--apphost", str(APPHOST),
                        "--format", "Json", "--tail", "100", "--non-interactive"],
                        cwd=ROOT, env=self.environment, timeout=10)
                    row.update(status="completed", exit_code=result.returncode if isinstance(result.returncode, int) else None)
                    raw = (result.stdout or "") + (result.stderr or "")
                    row["sdk_categories"] = sorted(name for name, message in CATALOG_DIAGNOSTIC_CATEGORIES.items() if message in raw)
                    row["exception_types"] = sorted(name for name in CATALOG_DIAGNOSTIC_EXCEPTIONS if name in raw)
                    row["frames"] = sorted(name for name in CATALOG_DIAGNOSTIC_FRAMES if name in raw)
                except subprocess.TimeoutExpired:
                    row["status"] = "timeout"
                except KeyboardInterrupt:
                    row["status"] = "interrupted"
                    receipt["status"] = "interrupted"
                    raise
                except Exception:
                    row["status"] = "unavailable"
            receipt["status"] = "observed"
        except KeyboardInterrupt:
            receipt["status"] = "interrupted"
            raise
        except Exception:
            receipt["status"] = "ownership_unavailable"
        finally:
            self.write_evidence("catalog-failure-diagnostics.json", json.dumps(receipt, indent=2) + "\n")

    def wait_for_catalog_processes(self, until):
        """Wait for actual owned native receivers and sidecars without accepting launchers."""
        while True:
            observed = {}
            sidecar_directories = {}
            for identity in self.resource_check(self.scratch, self.placement, self.scheduler):
                pid = identity["pid"]
                try:
                    arguments = (Path("/proc") / str(pid) / "cmdline").read_bytes().decode().split("\0")
                    values = dict(item.split("=", 1) for item in
                                  (Path("/proc") / str(pid) / "environ").read_bytes().decode().split("\0") if "=" in item)
                except (OSError, ValueError, UnicodeDecodeError):
                    continue
                if arguments and Path(arguments[0]).name == "daprd" and arguments.count("--app-id") == 1:
                    app_index = arguments.index("--app-id") + 1
                    app = arguments[app_index] if app_index < len(arguments) else None
                    if app in CATALOG_RECEIVER_POLICIES:
                        require(app not in sidecar_directories, "Catalog sidecar identity is not unique")
                        sidecar_directories[app] = verify_catalog_sidecar_policy(app, arguments, self.catalog_private)
                for app in ("eventstore", "timesheets"):
                    executable = "Hexalith.EventStore" if app == "eventstore" else "Hexalith.Timesheets"
                    if any(Path(value).name == executable for value in arguments if value):
                        require(app not in observed and values.get("TIMESHEETS_LIVE_DAPR_COMPONENTS") == self.scratch,
                                "Catalog host process identity is not uniquely owned")
                        observed[app] = values
            if (set(observed) == {"eventstore", "timesheets"}
                    and set(sidecar_directories) == {"eventstore", "timesheets"}):
                return observed, sidecar_directories
            if time.monotonic() >= until:
                require(set(observed) == {"eventstore", "timesheets"}, "Actual catalog host processes are unavailable")
                raise FixtureError("Actual catalog sidecar processes are unavailable")
            time.sleep(0.2)

    def _capture_route_catalog(self, phase):
        """Read the SDK's real receiver, policy and committed generation observations."""
        require(self.route_catalog and self.catalog_private is not None, "Catalog observation lacks its private owner")
        until = time.monotonic() + 60
        observed, sidecar_directories = self.wait_for_catalog_processes(until)
        directory_values = {values.get("EventStore__RouteCatalog__DevelopmentBootstrap__Directory")
                            for values in observed.values()}
        require(len(directory_values) == 1 and None not in directory_values,
                "Catalog receivers selected different private coordination")
        directory = Path(directory_values.pop())
        require(directory.is_dir() and directory.name.startswith("hexalith-route-catalog-")
                and directory.parent == Path(tempfile.gettempdir())
                and sidecar_directories == {"eventstore": directory, "timesheets": directory},
                "Catalog coordination differs from the selected sidecar ACLs")

        required = ["catalog.json", "activation.json", "eventstore.observation.json",
                    "timesheets.observation.json", "eventstore.committed.json",
                    "timesheets.committed.json", "authority.component.json"]
        while not all((directory / name).is_file() for name in required) and time.monotonic() < until:
            time.sleep(0.2)
        require(all((directory / name).is_file() for name in required),
                "SDK catalog receivers did not commit one complete generation")
        documents = {}
        for name in required:
            path = directory / name
            require(path.is_file() and path.stat().st_size < 4 * 1024 * 1024
                    and path.stat().st_mode & 0o077 == 0,
                    "Private catalog observation is absent or unsafe")
            documents[name] = json.loads(path.read_text())
        catalog = documents["catalog.json"]
        expected_routes = {
            "timesheets-commit-magic-link-issue", "timesheets-commit-magic-link-transition",
            "timesheets-commit-magic-link-use", "timesheets-commit-time-entry-record",
            "timesheets-magic-link-token-hash-capability-index",
            "timesheets-magic-link-confirmation-capabilities",
            "timesheets-tenant-activity-type-catalog", "timesheets-time-entry-evidence",
        }
        route_ids = {item["routeId"] for item in catalog["declarations"]}
        require(catalog["schemaVersion"] == 3 and catalog["profile"] == "Development"
                and route_ids == expected_routes and len(catalog["routes"]) == 8
                and len(catalog["operations"]) == 8 and len(catalog["idempotency"]) == 4,
                "SDK catalog differs from the eight compiled owner routes and policies")
        catalog_digest = hashlib.sha256((directory / "catalog.json").read_bytes()).hexdigest()
        activation = documents["activation.json"]
        technical_receivers = activation.get("TechnicalHttpReceivers")
        require(activation["RootDigest"] == catalog_digest and len(activation["RequiredHosts"]) == 2
                and {host["HostId"] for host in activation["RequiredHosts"]} == {"eventstore", "timesheets"},
                "Committed catalog activation differs from its selected owner")
        technical_action_count = verify_catalog_technical_membership(activation,
            {app: documents[app + ".observation.json"] for app in ("eventstore", "timesheets")})
        proof = self.verify_catalog_sdk_binding(directory)
        require(proof["binding_digest"] == activation.get("TrustProof")
                and proof["technical_action_count"] == technical_action_count,
                "Catalog SDK binding proof differs from the captured activation")
        require(self.source_before is not None, "Captured Debug/source binary identity is unavailable")
        captured_files = self.source_before["files"]
        observations = {}
        readback_epochs = set()
        expected_invocations = {
            "eventstore": {("timesheets", method, verb) for method, verb in [
                ("api/v1/commands/bound/workload", "POST"),
                ("api/v1/commands/workload", "POST"),
                ("api/v1/commands/status/workload/*", "GET"),
                ("api/v1/streams/read/workload", "POST")]},
            "timesheets": {("eventstore", method, "POST") for method in [
                "process", "replay-state", "query", "project/v2", "project/rebuild/v1",
                "project/rebuild/shared/v1", "admin/operational-index-metadata"]},
        }
        for app in ("eventstore", "timesheets"):
            observation = documents[app + ".observation.json"]
            readback = documents[app + ".committed.json"]
            binding_matches = ((readback.get("AttemptId") == activation["AttemptId"]
                                and readback.get("ActivationBindingDigest") == activation["TrustProof"])
                               if technical_receivers else (readback.get("AttemptId") is None
                                                            and readback.get("ActivationBindingDigest") is None))
            invocations = observation.get("DeclaredInvocations") or []
            actual_invocations = {(item["CallerAppId"], item["Method"], verb)
                                  for item in invocations for verb in item["HttpMethods"]}
            require(observation["HostId"] == app and observation["AppId"] == app
                    and set(observation["RequiredHostIds"]) == {"eventstore", "timesheets"}
                    and set(observation["SelectedAppIds"]) == {"eventstore", "timesheets"}
                    and actual_invocations == expected_invocations[app]
                    and readback["Serving"] is True and type(readback["Epoch"]) is int
                    and readback["Epoch"] > 0 and readback["RootDigest"] == catalog_digest
                    and readback["Generation"] == catalog["generation"]
                    and readback["Profile"] == catalog["profile"]
                    and readback["RouteFacetDigest"] == catalog["routeFacetDigest"]
                    and readback["IdempotencyFacetDigest"] == catalog["idempotencyFacetDigest"]
                    and binding_matches
                    and sorted(json.dumps(host, sort_keys=True) for host in readback["RequiredHosts"])
                    == sorted(json.dumps(host, sort_keys=True) for host in activation["RequiredHosts"]),
                    "Catalog receiver or persisted readback differs from the committed generation")
            readback_epochs.add(readback["Epoch"])
            if app == "timesheets":
                owners = observation["ExecutableOwners"]
                require(isinstance(owners, dict)
                        and {"Hexalith.Timesheets.Server", "Hexalith.Timesheets.Projections"}
                        <= {name.split(",", 1)[0] for name in owners},
                        "Catalog executable owners differ from captured Debug/source binaries")
                for name, digest in owners.items():
                    if name.startswith("Hexalith."):
                        binary = "src/Hexalith.Timesheets/bin/Debug/net10.0/" + name.split(",", 1)[0] + ".dll"
                        require(re.fullmatch(r"[0-9a-f]{64}", digest) is not None
                                and captured_files.get(binary) == digest,
                                "Catalog executable owners differ from captured Debug/source binaries")
            observations[app] = {
                "host_id": app, "app_id": observation["AppId"],
                "declared_invocations": observation.get("DeclaredInvocations"),
                "declaration_route_ids": sorted(item["RouteId"] for item in observation.get("Declarations") or []),
                "target_route_ids": sorted(item["RouteId"] for item in observation.get("Targets") or []),
                "operation_route_ids": sorted(item["RouteId"] for item in observation.get("Operations") or []),
                "admission_route_ids": sorted(item["RouteId"] for item in observation.get("AdmissionPolicies") or []),
                "executable_owners": observation.get("ExecutableOwners"),
                "serving": readback["Serving"], "epoch": readback["Epoch"],
            }
        require(len(readback_epochs) == 1
                and observations["timesheets"]["declaration_route_ids"] == sorted(expected_routes)
                and observations["timesheets"]["target_route_ids"] == sorted(expected_routes)
                and observations["timesheets"]["operation_route_ids"] == sorted(expected_routes)
                and observations["timesheets"]["admission_route_ids"] == []
                and observations["eventstore"]["admission_route_ids"] == sorted([
                    "timesheets-commit-magic-link-issue", "timesheets-commit-magic-link-transition",
                    "timesheets-commit-magic-link-use", "timesheets-commit-time-entry-record"]),
                "Actual catalog receiver or workload policies are incomplete")
        authority = documents["authority.component.json"]
        require(authority["metadata"]["name"] == "timesheets-route-authority"
                and set(authority["scopes"]) == {"eventstore", "timesheets"},
                "Committed authority component differs from the private owner")
        receipt = {"phase": phase, "schema_version": catalog["schemaVersion"],
                   "generation": catalog["generation"], "root_digest": catalog_digest,
                   "route_ids": sorted(route_ids), "receivers": observations,
                   "authority_component": authority["metadata"]["name"],
                   "technical_receiver_app_ids": ["eventstore"], "technical_action_count": technical_action_count,
                   "metadata_receiver": catalog_metadata_receiver_facts(documents["timesheets.observation.json"], captured_files),
                   "activation_binding_digest": proof["binding_digest"], "sdk_binding_verified": True,
                   "complete": True}
        require(self.write_evidence("route-catalog-" + phase + ".json", json.dumps(receipt, indent=2) + "\n"),
                "Catalog observation evidence is incomplete")
        return receipt

    def verify_gateway_replacement(self, before, after):
        """Require the unchanged journey to replace the native PID/start identity."""
        proof = {"before": before, "after": after,
                 "identity_replaced": (before["pid"], before["start"]) != (after["pid"], after["start"]),
                 "same_dcp_parent": all(before["parent_identity"][key] == after["parent_identity"][key]
                                        for key in ("pid", "start")),
                 "old_identity_exited": recorded_process_exits([before])["all_gone"]}
        require(self.write_evidence("gateway-process-replacement.json", sanitized(json.dumps(proof, indent=2))),
                "Gateway replacement evidence is incomplete")
        require(all(proof[key] for key in ("identity_replaced", "same_dcp_parent", "old_identity_exited")),
                "Gateway native PID/start replacement could not be verified")

    def observe_dashboard_readiness(self, identity, phase):
        """Persist partial allowlisted diagnostics before the original refusal escapes."""
        diagnostic = {}
        self.dashboard_failure = None
        try:
            return verify_dashboard_ready(identity, diagnostic=diagnostic)
        except (FixtureError, OSError, KeyboardInterrupt):
            self.retain_dashboard_failure(diagnostic, phase)
            raise

    def retain_dashboard_failure(self, diagnostic, phase):
        """Write an allowlisted observation without replacing the original refusal."""
        self.dashboard_failure = diagnostic
        self.write_evidence("dashboard-listener-" + phase + "-failure.json", json.dumps({
            "verified": False, "observation_stage": phase, "fixture_qualification_complete": False,
            "diagnostic": diagnostic, "recorded_at_unix_ns": time.time_ns()}, indent=2) + "\n")

    def capture_dashboard_after_journey(self, journey_outcome):
        """Retain another actual native listener observation after the gateway restart journey."""
        require(self.dashboard_readiness is not None, "Initial dashboard observation is unavailable")
        diagnostic = {"ready": False, "stage": "resource_discovery", "ownership_observations": {},
                      "expected_identity": dashboard_diagnostic_identity(self.dashboard_readiness.get("ownership", {})),
                      "observation_started_unix_ns": time.time_ns(),
                      "visibility": {"scope": "observable_process_descriptors", "exclusivity_established": False,
                                     "unobserved_transfer_excluded": False}}
        self.dashboard_failure = diagnostic
        try:
            identities = self.resource_check(self.scratch, self.placement, self.scheduler)
            diagnostic["stage"] = "runtime_host_identity"
            host = runtime_host_identity(identities, APPHOST.parent / "obj/project.assets.json")
        except (FixtureError, OSError, KeyboardInterrupt):
            diagnostic["observation_completed_unix_ns"] = time.time_ns()
            self.retain_dashboard_failure(diagnostic, "post-journey")
            raise
        after = self.observe_dashboard_readiness(host["dashboard"], "post-journey")
        proof = {"before_journey": self.dashboard_readiness, "after_journey": after,
                 "journey_outcome": journey_outcome, "recorded_at_unix_ns": time.time_ns(),
                 "observation_stage": "after_journey_and_gateway_proof_before_cleanup",
                 "fixture_qualification_complete": False,
                 "same_observed_listener_identity": self.dashboard_readiness["ownership"] == after["ownership"],
                 "scope": "observable_process_descriptors", "exclusivity_established": False,
                 "unobserved_transfer_excluded": False}
        require(self.write_evidence("dashboard-listener-lifecycle.json", json.dumps(proof, indent=2) + "\n"),
                "Dashboard listener lifecycle evidence is incomplete")

    def _owned_apphost(self):
        rows = target_apphosts(json_output(self.run_command(
            ["aspire", "ps", "--non-interactive", "--format", "Json"]).stdout))
        if not rows:
            return False
        require(len(rows) == 1, "Ambiguous AppHost ownership; no stop attempted")
        pid = next((value for key, value in rows[0].items() if key.lower() in ("apphostpid", "pid", "processid")), None)
        require(isinstance(pid, int), "AppHost process ownership is unavailable; no stop attempted")
        try:
            contents = Path(f"/proc/{pid}/environ").read_bytes().split(b"\0")
        except OSError:
            raise FixtureError("AppHost process ownership is unavailable; no stop attempted") from None
        return (f"TIMESHEETS_LIVE_DAPR_COMPONENTS={self.scratch}".encode() in contents
                and b"TIMESHEETS_LIVE_DAPR_FIXTURE=1" in contents)

    def recover_container(self, name):
        inspected = self.run_command(["docker", "inspect", name, "--format", '{{json .}}'], check=False)
        if inspected.returncode != 0:
            # A failed inspect is unknown, including daemon/transport errors.
            # A separate successful exact-name inventory can prove absence.
            absent = self.run_command(["docker", "container", "ls", "--all", "--filter",
                                       "name=^/" + name + "$", "--format", "{{.ID}}"], check=False)
            require(absent.returncode == 0 and not absent.stdout.strip(),
                    "Container ownership/absence is unknown; no removal attempted")
            return None
        container = json_output(inspected.stdout)
        message = "Container ownership is unavailable; no removal attempted"
        require(isinstance(container, dict), message)
        config = container.get("Config")
        require(isinstance(config, dict), message)
        labels = config.get("Labels")
        require(isinstance(labels, dict) and labels.get("hexalith.timesheets.fixture") == self.fixture_id, message)
        identity = container.get("Id", "")
        require(isinstance(identity, str) and re.fullmatch(r"[0-9a-f]{64}", identity),
                "Container identity is unavailable; no removal attempted")
        return identity

    def catalog_custody_retained(self):
        """Require the SDK's complete private custody before deleting local digest material."""
        if self.catalog_private is None or self.catalog_before is None:
            return False
        try:
            root = self.catalog_private / "custody"
            manifest = root / "current.json"
            owner = root / "owner.json"
            if (not root.is_dir() or root.is_symlink() or root.stat().st_mode & 0o077
                or not manifest.is_file() or manifest.is_symlink() or manifest.stat().st_mode & 0o077
                or not owner.is_file() or owner.is_symlink() or owner.stat().st_mode & 0o077
                or not catalog_custody_owner_matches(json.loads((self.catalog_private / "owner-profile.json").read_text()),
                                                    json.loads(owner.read_text()))):
                return False
            inventory = json.loads(manifest.read_text())
            generation = inventory.get("generation")
            if (not isinstance(inventory, dict) or len(inventory) > 4096
                or not isinstance(generation, str) or re.fullmatch(r"generation-[0-9a-f]{32}", generation) is None):
                return False
            retained = root / generation
            if not retained.is_dir() or retained.is_symlink() or retained.stat().st_mode & 0o077:
                return False
            files = {path.name for path in retained.iterdir()}
            if files != set(inventory) - {"generation"} or not {
                "catalog.json", "activation.json", "eventstore.committed.json", "timesheets.committed.json",
                "authority.backend.json", "authority.component.json", "authority.dependencies.json"
            } <= files:
                return False
            for name in files:
                path = retained / name
                digest = inventory[name]
                if (not path.is_file() or path.is_symlink() or path.stat().st_mode & 0o077
                    or path.stat().st_size > 16 * 1024 * 1024 or not isinstance(digest, str)
                    or re.fullmatch(r"[0-9a-f]{64}", digest) is None
                    or hashlib.sha256(path.read_bytes()).hexdigest() != digest):
                    return False
            activation = json.loads((retained / "activation.json").read_text())
            return (inventory["catalog.json"] == self.catalog_before["root_digest"]
                    and activation["RootDigest"] == self.catalog_before["root_digest"])
        except (OSError, ValueError, TypeError, KeyError, AttributeError, UnicodeError, RecursionError):
            return False

    def cleanup(self):
        failed = not self.journey_shutdown_confirmed
        reasons = ["Journey descendant shutdown was not established"] if failed else []
        if self.start_attempted and not failed:
            try:
                if self.ownership_check():
                    result = self.run_command(["aspire", "stop", "--non-interactive", "--apphost", str(APPHOST)], check=False)
                    require(result.returncode == 0, "Exact fixture AppHost stop failed")
                require(not self.ownership_check(), "Fixture AppHost is still running")
                until = time.monotonic() + 10
                while True:
                    processes = self.resource_check(self.scratch, self.placement, self.scheduler)
                    recorded = recorded_process_exits(self.captured_processes)
                    if (not processes and recorded["all_gone"]) or time.monotonic() >= until:
                        break
                    time.sleep(0.1)
                self.write_evidence("resource-exit.json", json.dumps(processes, indent=2) + "\n")
                self.write_evidence("recorded-process-exit.json", json.dumps(recorded, indent=2) + "\n")
                require(not processes, "Fixture-bound resources remain; retaining infrastructure")
                require(recorded["all_gone"], "Captured fixture processes remain; retaining infrastructure")
                if self.security_smoke:
                    # Readiness can fail before the first credential inspection.
                    # The invocation label still identifies every security container
                    # that could have been created by this fixture's AppHost.
                    remaining = self.run_command(["docker", "container", "ls", "--all", "--no-trunc", "--filter",
                        "label=hexalith.timesheets.components=" + self.scratch, "--format", "{{.ID}}"], check=False)
                    require(remaining.returncode == 0 and not remaining.stdout.strip(),
                            "Fixture-bound security container remains or absence is unknown; retaining infrastructure")
                if self.security_container:
                    remaining = self.run_command(["docker", "container", "ls", "--all", "--no-trunc", "--filter",
                        "id=" + self.security_container, "--format", "{{.ID}}"], check=False)
                    require(remaining.returncode == 0 and not remaining.stdout.strip(),
                            "Owned security container remains; retaining fixture infrastructure")
            except (FixtureError, OSError, KeyboardInterrupt) as error:
                failed = True
                reasons.append(str(error) if isinstance(error, FixtureError) else "Resource shutdown inspection did not complete")
        # Resolve every identity before removing any dependency. Preserve all
        # fixture artifacts if Docker ownership cannot be established.
        recovered = []
        if not failed and self.catalog_private is not None and (self.catalog_private / "transport-custody").exists():
            try:
                proof = verify_catalog_transport_cleanup(self.catalog_private)
                require(self.write_evidence("route-transport-cleanup.json", json.dumps(proof, indent=2) + "\n"),
                        "Owned transport cleanup evidence is incomplete")
            except (FixtureError, OSError, ValueError):
                failed = True
                reasons.append("Owned transport cleanup or custody is incomplete; retaining protected profile")
        if not failed:
            try:
                for name, expected in self.containers.items():
                    identity = self.recover_container(name)
                    require(not expected or identity in (None, expected), "Container identity changed; no removal attempted")
                    if identity:
                        recovered.append((name, identity))
            except (FixtureError, OSError, KeyboardInterrupt) as error:
                failed = True
                reasons.append(str(error) if isinstance(error, FixtureError) else "Docker ownership inspection did not complete")
        if not failed:
            for name, identity in reversed(recovered):
                try:
                    result = self.run_command(["docker", "rm", "--force", "--volumes", identity], check=False)
                    require(result.returncode == 0, "Fixture container cleanup failed")
                    require(self.recover_container(name) is None, "Fixture container is still present")
                except (FixtureError, OSError, KeyboardInterrupt) as error:
                    failed = True
                    reasons.append(str(error) if isinstance(error, FixtureError) else "Docker removal inspection did not complete")
                    break
        if not failed and self.scratch:
            try:
                shutil.rmtree(self.scratch)
            except OSError:
                failed = True
                reasons.append("Fixture component removal failed")
        if not failed and self.catalog_private:
            if not self.catalog_custody_retained():
                failed = True
                reasons.append("Private catalog custody is incomplete; retaining protected profile")
            else:
                try:
                    shutil.rmtree(self.catalog_private)
                except OSError:
                    failed = True
                    reasons.append("Private catalog custody removal failed")
        if self.catalog_verifier_directory is not None:
            try:
                shutil.rmtree(self.catalog_verifier_directory)
                self.catalog_verifier_directory = None
            except OSError:
                failed = True
                reasons.append("Owned catalog verifier removal failed")
        if self.evidence_write_failed:
            failed = True
            reasons.append("Fixture evidence write failed")
        self.write_evidence("cleanup.json", json.dumps({
            "complete": not failed, "journey_descendants_gone": self.journey_shutdown_confirmed,
            "failure_reasons": reasons,
            "components_created": self.scratch is not None,
            "components_removed": bool(self.scratch and not Path(self.scratch).exists()),
            "catalog_profile_removed": bool(self.catalog_private and not self.catalog_private.exists()),
            "containers_removed": len(recovered) if not failed else None}, indent=2) + "\n")
        return not failed and not self.evidence_write_failed

    def installed_image(self, container):
        image = self.run_command(["docker", "inspect", container, "--format", "{{.Image}}"]).stdout.strip()
        require(re.fullmatch(r"sha256:[0-9a-f]{64}", image), "Installed immutable service image is unavailable")
        found = self.run_command(["docker", "image", "inspect", image, "--format", "{{.Id}}"])
        require(found.stdout.strip() == image, "Installed immutable service image does not match")
        return image

    def create_container(self, service, image, options, arguments):
        name = "timesheets-live-dapr-" + self.fixture_id + "-" + service
        self.containers[name] = None  # Record intent before Docker can create it.
        created = self.run_command(["docker", "run", "--detach", "--pull=never", "--name", name,
            "--label", "hexalith.timesheets.fixture=" + self.fixture_id, *options, image, *arguments])
        identity = created.stdout.strip()
        require(re.fullmatch(r"[0-9a-f]{64}", identity), "Fixture container identity is unavailable")
        self.containers[name] = identity
        return identity

    def container_endpoint(self, identity, port):
        ports = json_output(self.run_command([
            "docker", "inspect", identity, "--format", "{{json .NetworkSettings.Ports}}"]).stdout)
        message = "Fixture service is not loopback-only or its published port is unavailable"
        require(isinstance(ports, dict), message)
        bindings = ports.get(str(port) + "/tcp")
        require(isinstance(bindings, list) and len(bindings) == 1 and isinstance(bindings[0], dict)
                and bindings[0].get("HostIp") == "127.0.0.1"
                and isinstance(bindings[0].get("HostPort"), str), message)
        endpoint = "127.0.0.1:" + bindings[0]["HostPort"]
        private_endpoint(endpoint)
        return endpoint

    def start_infrastructure(self):
        images = {service: self.installed_image("dapr_" + service) for service in
                  (("redis",) if self.route_catalog else ("redis", "placement", "scheduler"))}
        self.redis_image = images["redis"]
        redis_port = available_control_port((self.public_port,))
        self.container_id = self.create_container("redis", images["redis"],
            ["--publish", f"127.0.0.1:{redis_port}:6379", "--tmpfs", "/data"],
            ["redis-server", "--save", "", "--appendonly", "no"])
        endpoint = self.container_endpoint(self.container_id, 6379)
        require(endpoint == "127.0.0.1:" + str(redis_port), "Redis publication does not match its selected private endpoint")
        for attempt in range(10):
            ready = self.run_command(["docker", "exec", self.container_id, "redis-cli", "ping"], check=False)
            if ready.returncode == 0 and ready.stdout.strip() == "PONG":
                break
            if attempt < 9:
                time.sleep(0.2)
        require(ready.returncode == 0 and ready.stdout.strip() == "PONG", "Disposable Redis did not become ready")
        placement_port = available_control_port((self.public_port, redis_port))
        self.placement = "127.0.0.1:" + str(placement_port)
        if not self.route_catalog:
            placement_id = self.create_container("placement", images["placement"],
                ["--entrypoint", "/placement", "--publish", f"127.0.0.1:{placement_port}:50005"],
                ["--mode=standalone", "--inmem-store-enabled=true"])
            self.placement = self.container_endpoint(placement_id, 50005)
        require(self.placement == "127.0.0.1:" + str(placement_port), "Placement publication does not match its selected private endpoint")
        # Scheduler advertises the host endpoint; select it before launch. A
        # competing bind fails Docker startup rather than selecting a shared port.
        scheduler_port = available_control_port((self.public_port, redis_port, placement_port))
        self.scheduler = "127.0.0.1:" + str(scheduler_port)
        if not self.route_catalog:
            scheduler_id = self.create_container("scheduler", images["scheduler"],
                ["--entrypoint", "/scheduler", "--publish", f"127.0.0.1:{scheduler_port}:50006", "--tmpfs", "/data"],
                ["--mode=standalone", "--etcd-data-dir=/data", "--etcd-client-listen-address=127.0.0.1",
                 "--override-broadcast-host-port=" + self.scheduler])
            require(self.container_endpoint(scheduler_id, 50006) == self.scheduler, "Scheduler advertisement does not match its private endpoint")
        validate_control_plane(self.placement, self.scheduler)
        self.scratch = tempfile.mkdtemp(prefix="timesheets-live-dapr-components-")
        write_components(self.scratch, endpoint)
        self.environment.update({"TIMESHEETS_LIVE_DAPR_FIXTURE": "1", "TIMESHEETS_LIVE_DAPR_COMPONENTS": self.scratch,
            "TIMESHEETS_LIVE_DAPR_REDIS_HOST": endpoint, "TIMESHEETS_LIVE_DAPR": "1",
            "Dapr__PlacementHostAddress": self.placement, "Dapr__SchedulerHostAddress": self.scheduler})
        print("isolated fixture mode: actor/read-model/pubsub Redis and private placement/scheduler", flush=True)

    def prepare_route_catalog(self):
        """Create one private SDK owner profile and closed receiver policies for the owned Redis."""
        require(self.route_catalog and self.fixture_backup is not None and not self.start_attempted
                and self.catalog_private is None, "Catalog preparation requires an admitted fresh private store")
        root = Path(tempfile.mkdtemp(prefix="timesheets-route-catalog-"))
        root.chmod(0o700)
        self.catalog_private = root

        def private_file(name, contents):
            path = root / name
            descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
            with os.fdopen(descriptor, "w", encoding="utf-8") as output:
                output.write(contents)
                output.flush()
                os.fsync(output.fileno())
            return str(path)

        generation = "fixture-v1"
        profile = {
            "CustodyDirectory": str(root / "custody"),
            "AuthorityComponentName": "timesheets-route-authority",
            "AuthorityStateKey": "timesheets-route-" + self.fixture_id,
            "ActiveDigestKeyGeneration": generation,
            "DigestKeys": {generation: base64.b64encode(secrets.token_bytes(32)).decode()},
            "SecretMapGeneration": generation,
        }

        self.environment.update({
            "TIMESHEETS_LIVE_DAPR_CATALOG_FIXTURE_ID": self.fixture_id,
            "TIMESHEETS_LIVE_DAPR_CATALOG_PROFILE": private_file("owner-profile.json", json.dumps(profile)),
            "TIMESHEETS_LIVE_DAPR_CATALOG_GATEWAY_POLICY": private_file("gateway-policy.yaml", catalog_receiver_policy("eventstore")),
            "TIMESHEETS_LIVE_DAPR_CATALOG_TIMESHEETS_POLICY": private_file("timesheets-policy.yaml", catalog_receiver_policy("timesheets")),
        })
        runtime = Path.home() / ".dapr/bin/daprd"
        # The owner capability validates these bytes before creating transport resources.
        self.environment["TIMESHEETS_LIVE_DAPR_TRANSPORT_RUNTIME"] = str(runtime)
        require(self.write_evidence("catalog-selection.json", json.dumps({
            "mode": "isolated-development", "owner_profile_private": True,
            "authority_component": profile["AuthorityComponentName"],
            "authority_state_key_sha256": hashlib.sha256(profile["AuthorityStateKey"].encode()).hexdigest(),
            "gateway_methods": [method for method, _ in CATALOG_RECEIVER_POLICIES["eventstore"][2]],
            "timesheets_methods": [method for method, _ in CATALOG_RECEIVER_POLICIES["timesheets"][2]],
        }, indent=2) + "\n"), "Catalog selection evidence is incomplete")

    def execute(self):
        try:
            with LocalRuntimeAdmission():
                return self._execute_admitted()
        except RuntimeAdmissionError as error:
            self.write_evidence("admission.json", json.dumps({"admitted": False, "resources_started": False,
                "reason": str(error)}, indent=2) + "\n")
            self.cleanup()
            print(str(error), flush=True)
            return 1

    def _execute_admitted(self):
        exit_code = 1
        try:
            self.run_command(["aspire", "--version"])
            self.run_command(["dapr", "--version"])
            self.run_command(["docker", "version", "--format", "{{.Server.Version}}"])
            self.preflight()
            if not self.injected_command and not self.route_catalog:
                self.capture_shared_baseline()
                self.preflight()
            self.validate_dependency_graph()
            self.resolve_gateway_invocation()
            if self.security_smoke or self.route_catalog:
                # Rebuild only after idle admission. This binds the selected source
                # graph to the executable that the subsequent launch uses.
                self.run_command(["dotnet", "build", str(ROOT / "Hexalith.Timesheets.slnx"), "--configuration", "Debug", "--no-restore",
                    "-p:UseHexalithProjectReferences=true", "-warnaserror", "-m:1", "/nr:false"], timeout=180)
                if self.route_catalog and not self.injected_command:
                    self.prepare_catalog_binding_verifier()
                source_before = self.capture_source_identity()
                self.source_before = source_before
                require(self.write_evidence("source-before.json", json.dumps(source_before, indent=2) + "\n"),
                        "Source identity evidence is incomplete; no resources started")
            self.start_infrastructure()
            self.preflight()
            if not self.injected_command:
                self.prepare_fixture_bootstrap()
            if self.route_catalog and not self.injected_command:
                self.prepare_route_catalog()
            if self.security_smoke:
                self.environment[SECURITY_SMOKE_SETTING] = "1"
            self.start_attempted = True
            launch = ["aspire", "start"]
            if not self.route_catalog:
                launch.append("--no-build")
            # Selected owner preparation requires an ordinary build-capable launch.
            # The pre-start snapshot still binds every asset across that build.
            self.run_command([*launch, "--non-interactive", "--apphost", str(APPHOST), "--format", "Json"], timeout=180)
            require(self.ownership_check(), "The started AppHost is not owned by this fixture")
            if not self.injected_command:
                for resource in ("eventstore", "timesheets"):
                    self.run_command(["aspire", "wait", resource, "--status", "up", "--timeout", "60",
                        "--apphost", str(APPHOST), "--non-interactive"], timeout=70)
                if self.route_catalog:
                    catalog_before = self.capture_route_catalog("before")
                    self.catalog_before = catalog_before
                self.bootstrap_fixture_protocol()
            for resource in (("security", "eventstore", "timesheets") if self.security_smoke else ("eventstore", "timesheets")):
                self.run_command(["aspire", "wait", resource, "--timeout", "60", "--apphost", str(APPHOST), "--non-interactive"], timeout=70)
            if not self.injected_command:
                self.capture_runtime_readiness()
                if self.route_catalog:
                    print("compiled eight-route SDK catalog and exact receivers committed", flush=True)
            if self.security_smoke:
                self.verify_security_runtime()
            if not self.injected_command:
                current = self.resource_check(self.scratch, self.placement, self.scheduler)
                self.captured_processes.extend(identity for identity in current if not any(
                    (row["pid"], row["start"]) == (identity["pid"], identity["start"]) for row in self.captured_processes))
                self.write_evidence("runtime-isolation.json", json.dumps({
                    "redis": self.environment["TIMESHEETS_LIVE_DAPR_REDIS_HOST"],
                    "placement": self.placement, "scheduler": self.scheduler,
                    "components": str(self.scratch), "bound_processes": self.captured_processes}, indent=2) + "\n")
            before = self.capture_gateway_process("before") if not self.injected_command else None
            executable = ROOT / "tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests"
            journey_outcome = {"command_completed": False, "command_exit_code": None,
                               "validation_completed": False, "journey_succeeded": False,
                               "command_started_unix_ns": time.time_ns()}
            try:
                result = self.run_command([str(executable), "-class", "*MagicLinkLiveDaprTests", "-noColor"], timeout=510, check=False, journey=True)
                exit_code = result.returncode
                if self.route_catalog and exit_code != 0:
                    self.capture_catalog_failure_diagnostics("journey")
                journey_outcome.update(command_completed=True, command_exit_code=exit_code,
                                       command_completed_unix_ns=time.time_ns())
                print(sanitized((result.stdout or "") + (result.stderr or "")), end="", flush=True)
                if exit_code == 0:
                    require_one_passing_test(result)
                if self.security_smoke or self.route_catalog:
                    source_after = self.capture_source_identity()
                    require(self.write_evidence("source-after.json", json.dumps(source_after, indent=2) + "\n"),
                            "Source identity evidence is incomplete")
                    require(source_after == source_before, "Source or built assets changed during runtime acceptance")
                if self.route_catalog:
                    sidecars_after = verify_runtime_components(self.environment["TIMESHEETS_LIVE_DAPR_REDIS_HOST"],
                        self.scratch, self.placement, self.scheduler, catalog_profile=self.catalog_private)
                    require({row["pid"] for row in running_dapr_processes()}
                            == {row["pid"] for row in sidecars_after.values()},
                            "Post-restart sidecars differ from the admitted private topology")
                    catalog_after = self.capture_route_catalog("after")
                    require(catalog_after["root_digest"] == catalog_before["root_digest"]
                            and catalog_after["generation"] == catalog_before["generation"]
                            and {app: row["epoch"] for app, row in catalog_after["receivers"].items()}
                            == {app: row["epoch"] for app, row in catalog_before["receivers"].items()},
                            "Catalog generation or committed readbacks changed across gateway restart")
                journey_outcome["validation_completed"] = True
                journey_outcome["journey_succeeded"] = exit_code == 0
            finally:
                if before is not None:
                    try:
                        self.verify_gateway_replacement(before, self.capture_gateway_process("after"))
                    except (FixtureError, OSError, KeyboardInterrupt) as error:
                        self.write_evidence("gateway-process-proof-failure.json", json.dumps({
                            "verified": False, "message": "Gateway native PID/start replacement could not be verified"}) + "\n")
                        print("Gateway native PID/start replacement could not be verified", flush=True)
                        if exit_code == 0:
                            exit_code = 130 if isinstance(error, KeyboardInterrupt) else 1
                if self.security_smoke and self.dashboard_readiness is not None:
                    journey_outcome["exit_code_before_listener_observation"] = exit_code
                    try:
                        self.capture_dashboard_after_journey(journey_outcome)
                    except (FixtureError, OSError, KeyboardInterrupt) as error:
                        self.write_evidence("dashboard-listener-lifecycle-failure.json", json.dumps({
                            "verified": False, "message": "Post-journey dashboard listener could not be observed",
                            "ownership_failure": self.dashboard_failure,
                            "journey_outcome": journey_outcome, "recorded_at_unix_ns": time.time_ns(),
                            "observation_stage": "after_journey_and_gateway_proof_before_cleanup",
                            "fixture_qualification_complete": False,
                            "exclusivity_established": False, "unobserved_transfer_excluded": False}) + "\n")
                        print("Post-journey dashboard listener could not be observed", flush=True)
                        if exit_code == 0:
                            exit_code = 130 if isinstance(error, KeyboardInterrupt) else 1
        except KeyboardInterrupt:
            exit_code = 130
            print("Isolated fixture interrupted; cleaning up owned resources", flush=True)
        except (FixtureError, OSError) as error:
            exit_code = getattr(error, "exit_code", 1)
            print(str(error) if isinstance(error, FixtureError) else "Fixture operation could not complete safely", flush=True)
        finally:
            if not self.cleanup():
                print("Owned cleanup or evidence is incomplete; see available fixture diagnostics", flush=True)
                if exit_code == 0:
                    exit_code = 1
            if self.shared_before is not None:
                try:
                    after = self.capture_shared_snapshot()
                    self.write_evidence("shared-after.json", json.dumps(after, indent=2) + "\n")
                    preservation = shared_preservation(self.shared_before, after)
                    self.write_evidence("shared-preservation.json", json.dumps(preservation, indent=2) + "\n")
                    require(preservation["matches"], "Shared-service preservation failed: " + "; ".join(preservation["mismatches"]))
                    print("Retained broker/state persistent values, unexpired TTL values and scheduler KV match", flush=True)
                except KeyboardInterrupt:
                    print("Shared-service preservation interrupted; preservation is unproved", flush=True)
                    exit_code = exit_code or 130
                except (FixtureError, OSError, ValueError) as error:
                    print(str(error) if isinstance(error, FixtureError)
                          else "Shared-service preservation could not be established; see evidence", flush=True)
                    if exit_code == 0:
                        exit_code = 1
            print(f"Sanitized fixture evidence: {self.log_directory}", flush=True)
            if self.evidence_write_failed and exit_code == 0:
                exit_code = 1
        return exit_code


def fixture_gateway_signing_key(gateway, directory, process_directory=Path("/proc")):
    """Read the existing ephemeral Development issuer only from the exact owned process."""
    message = "Fixture bootstrap Development authentication identity is unavailable"
    try:
        process = Path(process_directory) / str(gateway["pid"])
        fields = (process / "stat").read_text().rsplit(")", 1)[1].split()
        environment = dict(item.split("=", 1) for item in (process / "environ").read_bytes().decode().split("\0") if "=" in item)
        require(fields[19] == gateway["start"] and fields[0] != "Z"
                and environment.get("TIMESHEETS_LIVE_DAPR_COMPONENTS") == str(directory)
                and environment.get("ASPNETCORE_ENVIRONMENT") == "Development"
                and environment.get("Authentication__JwtBearer__Authority") == ""
                and environment.get("Authentication__JwtBearer__Issuer") == "hexalith-timesheets-dev"
                and environment.get("Authentication__JwtBearer__Audience") == "hexalith-eventstore"
                and environment.get("Authentication__JwtBearer__AllowedAlgorithms__0") == "HS256", message)
        key = environment.get("Authentication__JwtBearer__SigningKey", "")
        require(re.fullmatch(r"[0-9A-F]{64}", key), message)
        return key.encode()
    except (OSError, ValueError, IndexError, UnicodeError):
        raise FixtureError(message) from None


class FixtureNoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, file, code, message, headers, new_url):
        return None


def owned_http_open(request, *, timeout, context=None):
    """Keep a validated owned request on its direct endpoint and TLS context."""
    handlers = [FixtureNoRedirect(), urllib.request.ProxyHandler({})]
    if context is not None:
        handlers.append(urllib.request.HTTPSHandler(context=context))
    return urllib.request.build_opener(*handlers).open(request, timeout=timeout)


def wait_fixture_gateway_alive(diagnostic, timeout=180):
    """Wait for HTTP liveness after process startup; never substitute it for readiness."""
    deadline = time.monotonic() + timeout
    result = {"attempts": 0, "http_status": None, "category": "transport_unavailable"}
    diagnostic.update(stage="liveness", liveness=result)
    while time.monotonic() < deadline:
        result["attempts"] += 1
        try:
            try:
                response = owned_http_open("http://127.0.0.1:8082/alive", timeout=min(5, max(0.1, deadline - time.monotonic())))
            except urllib.error.HTTPError as error:
                response = error
            with response:
                status = response.status
            result["http_status"] = status if type(status) is int and 100 <= status <= 599 else None
            result["category"] = "alive" if status == 200 else "http_unavailable"
            if status == 200:
                return
        except (OSError, TimeoutError):
            result.update(http_status=None, category="transport_unavailable")
        remaining = deadline - time.monotonic()
        if remaining > 0:
            time.sleep(min(1, remaining))
    raise FixtureError("Fixture gateway HTTP liveness did not become available within its bound")


def activate_fixture_writer_protocol(signing_key, body, diagnostic):
    """Call the normal admin API with a short-lived local Bearer token; never log it."""
    delays = (1, 2, 5, 10)
    for attempt in range(len(delays) + 1):
        diagnostic.update(stage="activation", attempts=attempt + 1, category="transport_unavailable")
        now = int(time.time())
        claims = {"iss": "hexalith-timesheets-dev", "aud": "hexalith-eventstore", "sub": "fixture-cutover-" + secrets.token_hex(12),
                  "role": "GlobalAdministrator", "iat": now, "nbf": now, "exp": now + 60, "jti": secrets.token_hex(16)}
        def b64(value):
            return base64.urlsafe_b64encode(value).rstrip(b"=").decode()
        unsigned = b64(b'{"alg":"HS256","typ":"JWT"}') + "." + b64(json.dumps(claims, separators=(",", ":")).encode())
        token = unsigned + "." + b64(hmac.new(signing_key, unsigned.encode(), hashlib.sha256).digest())
        request = urllib.request.Request("http://127.0.0.1:8082/api/v1/admin/projections/delivery-writer-protocol/activate",
            method="POST", data=json.dumps(body, separators=(",", ":")).encode(),
            headers={"Content-Type": "application/json", "Authorization": "Bearer " + token})
        try:
            try:
                response = owned_http_open(request, timeout=5)
            except urllib.error.HTTPError as error:
                response = error
            with response:
                status = response.status
                data = response.read(4097)
            diagnostic["http_status"] = status if type(status) is int and 100 <= status <= 599 else None
            diagnostic["category"] = {400: "preconditions_refused", 401: "authentication_refused", 403: "authorization_refused",
                                      409: "conflict", 503: "storage_unavailable"}.get(status, "http_refused")
            if status == 200:
                diagnostic["category"] = "invalid_response"
                try:
                    activated = len(data) <= 4096 and json.loads(data) == {"status": "Activated"}
                except (ValueError, UnicodeError):
                    activated = False
                require(activated, "Fixture protocol activation response was invalid")
                diagnostic.update(activated=True, category="activated", stage="complete")
                return
            require(status == 503, "Fixture protocol activation was refused")
        except (OSError, TimeoutError):
            diagnostic.update(http_status=None, category="transport_unavailable")
        if attempt < len(delays):
            time.sleep(delays[attempt])
    raise FixtureError("Fixture protocol activation remained unavailable after bounded attempts")


def runtime_dapr_identity_observation(pids, directory, placement, scheduler, process_directory=Path("/proc")):
    """Observe identity cardinality without retaining arguments or environment values."""
    result = {"counts": {"eventstore": 0, "timesheets": 0, "other": 0}, "processes": [], "scan_complete": True}
    for pid in pids:
        try:
            process = Path(process_directory) / str(int(pid))
            fields = (process / "stat").read_text().rsplit(")", 1)[1].split()
            if fields[0] == "Z":
                continue
            arguments = (process / "cmdline").read_bytes().decode().split("\0")
            environment = (process / "environ").read_bytes().split(b"\0")
            def option(name):
                return arguments[arguments.index(name) + 1] if arguments.count(name) == 1 else None
            app = option("--app-id")
            category = app if app in ("eventstore", "timesheets") else "other"
            result["counts"][category] += 1
            result["processes"].append({"pid": int(pid), "start": fields[19] if fields[19].isdigit() else None,
                "app": category, "fixture_marker_matches": f"TIMESHEETS_LIVE_DAPR_COMPONENTS={directory}".encode() in environment,
                "app_loopback": option("--app-channel-address") in (None, "127.0.0.1", "localhost", "::1"),
                "app_target_matches": option("--app-port") == {"eventstore": "8082", "timesheets": "8081"}.get(category),
                "placement_target_matches": option("--placement-host-address") == placement,
                "scheduler_target_matches": option("--scheduler-host-address") == scheduler})
        except (OSError, ValueError, IndexError, UnicodeError):
            result["scan_complete"] = False
    return result


def gateway_readiness_observation(gateway, sidecar, directory, process_directory=Path("/proc")):
    """Retain fixed readiness categories and endpoint equality booleans only."""
    result = {"pid": gateway["pid"], "start": gateway["start"], "identity_matches": False,
        "fixture_marker_matches": False, "http_port_shape_matches": False, "http_endpoint_loopback": False,
        "http_endpoint_matches_port": False, "sidecar_identity_matches": False,
        "gateway_ready": "Unavailable", "gateway_http_status": None, "gateway_checks": {}, "sidecar_metadata": "Unavailable",
        "actor_host_ready": "Unknown", "actor_placement": "Unknown", "actor_runtime": "Unknown",
        "aggregate_actor_registered": False}
    try:
        process = Path(process_directory) / str(gateway["pid"])
        fields = (process / "stat").read_text().rsplit(")", 1)[1].split()
        result["identity_matches"] = fields[19] == gateway["start"] and fields[0] != "Z"
        environment = dict(item.split("=", 1) for item in (process / "environ").read_bytes().decode().split("\0") if "=" in item)
        result["fixture_marker_matches"] = environment.get("TIMESHEETS_LIVE_DAPR_COMPONENTS") == str(directory)
        port = environment.get("DAPR_HTTP_PORT", "")
        result["http_port_shape_matches"] = bool(re.fullmatch(r"[0-9]{1,5}", port)) and 1024 <= int(port) <= 65535
        url = urllib.parse.urlsplit(environment.get("DAPR_HTTP_ENDPOINT", ""))
        result["http_endpoint_loopback"] = (url.scheme == "http" and url.hostname in ("localhost", "127.0.0.1", "::1")
            and url.path in ("", "/") and not url.query and not url.fragment and not url.username and not url.password)
        result["http_endpoint_matches_port"] = (result["http_endpoint_loopback"] and result["http_port_shape_matches"]
            and url.port == int(port))
    except (OSError, ValueError, IndexError, UnicodeError):
        return result
    if not result["identity_matches"] or not result["fixture_marker_matches"]:
        return result

    def read_response(address):
        try:
            response = owned_http_open(address, timeout=5)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            data = response.read(1048577)
            status = getattr(response, "status", None)
        require(len(data) <= 1048576, "Readiness response exceeds its observation bound")
        return status, data

    try:
        status, body = read_response("http://127.0.0.1:8082/ready")
        result["gateway_http_status"] = status if type(status) is int and 100 <= status <= 599 else None
        statuses = ("Healthy", "Degraded", "Unhealthy")
        # The platform default writer emits one exact plain-text enum. A host
        # may opt into its JSON writer; retain only the same fixed status fields.
        try:
            ready = json.loads(body)
        except (ValueError, UnicodeError):
            ready = {"status": body.decode("ascii")} if body in (b"Healthy", b"Degraded", b"Unhealthy") else {}
        if not isinstance(ready, dict):
            ready = {}
        result["gateway_ready"] = ready.get("status") if ready.get("status") in statuses else "Invalid"
        checks = ready.get("results", {})
        if isinstance(checks, dict):
            for name in ("dapr-sidecar", "dapr-actor-placement", "dapr-statestore", "dapr-app-channel-token"):
                status = checks.get(name, {}).get("status") if isinstance(checks.get(name), dict) else None
                result["gateway_checks"][name] = status if status in statuses else "Unknown"
    except (OSError, ValueError, TypeError, AttributeError, FixtureError):
        pass
    if not result["http_endpoint_matches_port"]:
        return result
    try:
        _, body = read_response(f"http://127.0.0.1:{port}/v1.0/metadata")
        metadata = json.loads(body)
        result["sidecar_identity_matches"] = metadata.get("id") == "eventstore"
        if not result["sidecar_identity_matches"]:
            result["sidecar_metadata"] = "WrongApplication"
            return result
        runtime = metadata.get("actorRuntime", {})
        result["sidecar_metadata"] = "Available" if isinstance(runtime, dict) else "Invalid"
        if isinstance(runtime, dict):
            result["actor_host_ready"] = "Ready" if runtime.get("hostReady") is True else "NotReady" if runtime.get("hostReady") is False else "Unknown"
            result["actor_placement"] = {"placement: connected": "Connected", "placement: disconnected": "Disconnected"}.get(runtime.get("placement"), "Unknown")
            result["actor_runtime"] = {"RUNNING": "Running", "DISABLED": "Disabled", "INITIALIZING": "Initializing"}.get(runtime.get("runtimeStatus"), "Unknown")
        actors = metadata.get("actors", [])
        result["aggregate_actor_registered"] = isinstance(actors, list) and any(
            isinstance(actor, dict) and actor.get("type") == "AggregateActor" for actor in actors)
    except (OSError, ValueError, TypeError, AttributeError, FixtureError):
        pass
    return result


def verify_catalog_sidecar_policy(app_id, arguments, catalog_profile):
    """Accept only the SDK's private, exact default-deny receiver configuration."""
    receiver, caller, operations = CATALOG_RECEIVER_POLICIES[app_id]
    failure = "Catalog sidecar did not select its owned closed policy"
    try:
        profile = Path(catalog_profile)
        original = profile / ("gateway-policy.yaml" if app_id == "eventstore" else "timesheets-policy.yaml")
        require(profile.is_dir() and not profile.is_symlink() and profile.stat().st_mode & 0o077 == 0
                and original.is_file() and not original.is_symlink() and original.stat().st_mode & 0o077 == 0
                and original.read_text(encoding="utf-8") == catalog_receiver_policy(app_id), failure)
        require(arguments.count("--config") == 1, failure)
        index = arguments.index("--config") + 1
        require(index < len(arguments), failure)
        selected = Path(arguments[index])
        directory = selected.parent
        require(selected.is_absolute() and selected == selected.resolve()
                and selected.name == app_id + ".configuration.json"
                and directory.parent == Path(tempfile.gettempdir()).resolve()
                and directory.name.startswith("hexalith-route-catalog-")
                and directory.is_dir() and not directory.is_symlink()
                and directory.stat().st_mode & 0o077 == 0
                and selected.is_file() and not selected.is_symlink()
                and selected.stat().st_mode & 0o077 == 0
                and selected.stat().st_size <= 1024 * 1024, failure)

        def unique_pairs(pairs):
            result = {}
            for key, value in pairs:
                require(key not in result, failure)
                result[key] = value
            return result

        rendered = json.loads(selected.read_text(encoding="utf-8"), object_pairs_hook=unique_pairs)
        expected = {
            "apiVersion": "dapr.io/v1alpha1", "kind": "Configuration",
            "metadata": {"name": "timesheets-catalog-" + receiver},
            "spec": {"mtls": {"enabled": True, "workloadCertTTL": "1h", "allowedClockSkew": "15s"}, "accessControl": {
                "defaultAction": "deny", "trustDomain": "public",
                "policies": [{"appId": caller, "defaultAction": "deny", "trustDomain": "public",
                              "namespace": "default",
                              "operations": [{"name": method, "httpVerb": [verb], "action": "allow"}
                                             for method, verb in operations]}],
            }},
        }
        require(rendered == expected, failure)
        return directory
    except (OSError, UnicodeError, ValueError, TypeError, RecursionError):
        raise FixtureError(failure) from None


def private_transport_document(path):
    """Read a bounded owner-only receipt, withholding parser diagnostics and arbitrary values."""
    path = Path(path)
    require(path.is_file() and not path.is_symlink() and path.stat().st_uid == os.getuid()
            and path.stat().st_mode & 0o077 == 0 and path.stat().st_nlink == 1
            and path.stat().st_size <= 1024 * 1024, "Owned transport receipt is absent or unsafe")
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, "Owned transport receipt is ambiguous")
            result[key] = value
        return result
    try:
        return json.loads(path.read_bytes(), object_pairs_hook=unique)
    except (ValueError, UnicodeError, RecursionError):
        raise FixtureError("Owned transport receipt is invalid") from None


def verify_catalog_transport(sidecars, profile, placement, scheduler, process_directory=Path("/proc")):
    """Bind actual native peers and TLS control services to the owner's private trust, without emitting credentials."""
    failure = "Owned authenticated Development transport is incomplete"
    try:
        receipt = private_transport_document(Path(profile) / "transport-custody/ready.json")
        root = Path(receipt["directory"])
        require(receipt["schemaVersion"] == 1 and receipt["ready"] is True
                and receipt["environment"] == "Development" and receipt["trustDomain"] == "public"
                and receipt["namespace"] == "default" and receipt["runtimeSha256"] == TRANSPORT_RUNTIME_SHA256
                and receipt["placementAddress"] == placement and receipt["schedulerAddress"] == scheduler, failure)
        lock = json.loads((GATEWAY_PROJECT.parents[2] / "deploy/dapr/qualification/runtime-lock.json").read_bytes())
        require(receipt["images"] == {name: lock["images"][name]["reference"]
                                      for name in ("sentry", "placement", "scheduler")}, failure)
        require(root.is_absolute() and root.resolve() == root and root.parent == Path(tempfile.gettempdir()).resolve()
                and root.name.startswith("eventstore-development-transport-") and root.is_dir()
                and root.stat().st_uid == os.getuid() and root.stat().st_mode & 0o077 == 0, failure)
        private_endpoint(receipt["sentryAddress"])
        require(len({placement, scheduler, receipt["sentryAddress"]}) == 3, failure)
        credentials = {}
        for name in ("ca.crt", "issuer.crt", "issuer.key"):
            path = root / "credentials" / name
            require(path.is_file() and not path.is_symlink() and path.stat().st_mode & 0o077 == 0
                    and path.stat().st_size <= 65536 and path.stat().st_uid == os.getuid(), failure)
            credentials[name] = hashlib.sha256(path.read_bytes()).hexdigest()
        require(receipt["trustAnchorSha256"] == credentials["ca.crt"]
                and hashlib.sha256((root / "runtime/.dapr/bin/daprd").read_bytes()).hexdigest() == receipt["adapterSha256"], failure)

        def flag(arguments, name):
            positions = [i for i, value in enumerate(arguments) if value == name or value.startswith(name + "=")]
            require(len(positions) == 1, failure)
            index = positions[0]
            if arguments[index] != name:
                return arguments[index].split("=", 1)[1]
            require(index + 1 < len(arguments) and not arguments[index + 1].startswith("--"), failure)
            return arguments[index + 1]

        def observed(pid):
            process = Path(process_directory) / str(pid)
            fields = (process / "stat").read_text().rsplit(")", 1)[1].split()
            require(fields[0] != "Z" and fields[19].isdigit(), failure)
            arguments = [value.decode() for value in (process / "cmdline").read_bytes().split(b"\0") if value]
            environment = dict(value.split(b"=", 1) for value in (process / "environ").read_bytes().split(b"\0") if b"=" in value)
            executable = (process / "exe").resolve(strict=True)
            return fields[19], arguments, environment, executable

        require(set(sidecars) == {"eventstore", "timesheets"}, failure)
        native = {}
        for app, selected in sidecars.items():
            start, arguments, environment, executable = observed(selected["pid"])
            require(hashlib.sha256(executable.read_bytes()).hexdigest() == TRANSPORT_RUNTIME_SHA256
                    and arguments.count("--enable-mtls") == 1
                    and not any(value.startswith("--enable-mtls=") for value in arguments)
                    and flag(arguments, "--sentry-address") == receipt["sentryAddress"]
                    and flag(arguments, "--control-plane-trust-domain") == "public"
                    and flag(arguments, "--control-plane-namespace") == "default"
                    and flag(arguments, "--placement-host-address") == placement
                    and flag(arguments, "--scheduler-host-address") == scheduler
                    and flag(arguments, "--app-id") == app and environment.get(b"NAMESPACE") == b"default", failure)
            for variable, name in ((b"DAPR_TRUST_ANCHORS", "ca.crt"), (b"DAPR_CERT_CHAIN", "issuer.crt"), (b"DAPR_CERT_KEY", "issuer.key")):
                require(hashlib.sha256(environment.get(variable, b"")).hexdigest() == credentials[name], failure)
            native[app] = {"pid": selected["pid"], "start": start, "owned_trust_matches": True}
        controls = {}
        require(len(receipt["processes"]) == 3, failure)
        for service, identity in zip(("sentry", "placement", "scheduler"), receipt["processes"]):
            start, arguments, environment, executable = observed(identity["pid"])
            require(start == identity["start"] and executable == root / service
                    and hashlib.sha256(executable.read_bytes()).hexdigest() == receipt["binaries"][service]
                    and environment.get(b"NAMESPACE") == b"default" and flag(arguments, "--trust-domain") == "public", failure)
            if service == "sentry":
                require(flag(arguments, "--issuer-credentials") == str(root / "credentials")
                        and flag(arguments, "--port") == receipt["sentryAddress"].split(":")[1], failure)
            else:
                require(flag(arguments, "--tls-enabled") == "true"
                        and flag(arguments, "--trust-anchors-file") == str(root / "credentials/ca.crt")
                        and flag(arguments, "--sentry-address") == receipt["sentryAddress"]
                        and flag(arguments, "--port") == {"placement": placement, "scheduler": scheduler}[service].split(":")[1], failure)
            require(flag(arguments, "--listen-address") == "127.0.0.1", failure)
            controls[service] = {"pid": identity["pid"], "start": start, "owned_trust_matches": True}
        return {"configured_authenticated_transport": True, "peer_delivery_requires_request_evidence": True,
                "trust_anchor_sha256": credentials["ca.crt"], "runtime_sha256": TRANSPORT_RUNTIME_SHA256,
                "sidecars": native, "control_plane": controls,
                "ready_receipt_sha256": hashlib.sha256((Path(profile) / "transport-custody/ready.json").read_bytes()).hexdigest()}
    except (OSError, KeyError, ValueError, TypeError, IndexError, UnicodeError, RecursionError):
        raise FixtureError(failure) from None


def verify_catalog_transport_cleanup(profile):
    """Require actual trust removal and exact recorded control-process exit before removing the profile."""
    failure = "Owned transport cleanup is incomplete"
    try:
        ready = private_transport_document(Path(profile) / "transport-custody/ready.json")
        cleanup = private_transport_document(Path(profile) / "transport-custody/cleanup.json")
        require(cleanup["schemaVersion"] == 1 and cleanup["complete"] is True
                and cleanup["startupQualified"] is True and cleanup["ownedProcessesExited"] is True
                and cleanup["temporaryDirectoryRemoved"] is True
                and cleanup["trustAnchorSha256"] == ready["trustAnchorSha256"]
                and not Path(ready["directory"]).exists(), failure)
        identities = [{"pid": identity["pid"], "start": identity["start"]} for identity in ready["processes"]]
        require(recorded_process_exits(identities)["all_gone"], failure)
        return {"complete": True, "owned_processes_exited": True, "temporary_trust_removed": True,
                "trust_anchor_sha256": ready["trustAnchorSha256"]}
    except (OSError, KeyError, ValueError, TypeError, RecursionError):
        raise FixtureError(failure) from None


def verify_runtime_components(endpoint, directory, placement, scheduler, observation=None,
                              process_directory=Path("/proc"), catalog_profile=None):
    """Verify actual daprd components and control-plane targets before commands."""
    validate_components(directory, endpoint)
    validate_control_plane(placement, scheduler)
    try:
        pids = subprocess.check_output(["pgrep", "-x", "daprd"], text=True).splitlines()
    except subprocess.CalledProcessError as error:
        if error.returncode != 1:
            raise FixtureError("Fixture Dapr runtime inventory is unavailable") from None
        pids = []
    sidecars = {}
    catalog_directories = set()
    for app_id, relative in (("eventstore", COMPONENT_FILES[0]), ("timesheets", COMPONENT_FILES[1])):
        matching = []
        for pid in pids:
            try:
                arguments = (Path(process_directory) / pid / "cmdline").read_bytes().decode().split("\0")
            except FileNotFoundError:
                continue
            if "--app-id" in arguments and arguments[arguments.index("--app-id") + 1] == app_id:
                matching.append({"pid": int(pid), "arguments": arguments})
        if len(matching) != 1:
            diagnostic = runtime_dapr_identity_observation(pids, directory, placement, scheduler, process_directory)
            if observation is not None:
                observation(diagnostic)
            else:
                print("Fixture Dapr identity observation: " + json.dumps(diagnostic), flush=True)
        require(len(matching) == 1, "Fixture Dapr runtime identity is not unique")
        sidecars[app_id] = matching[0]
        arguments = matching[0]["arguments"]
        for flag, address in (("--placement-host-address", placement), ("--scheduler-host-address", scheduler)):
            require(arguments.count(flag) == 1 and arguments[arguments.index(flag) + 1] == address,
                    "Dapr did not target the intended isolated control service")
        directories = [Path(arguments[index + 1]) for index, value in enumerate(arguments) if value == "--resources-path"]
        files = [path for resource_directory in directories for path in dapr_manifests(resource_directory)]
        contents = [path.read_text() for path in files]
        expected = [(Path(directory) / relative).read_text()]
        if app_id == "eventstore":
            expected.append((Path(directory) / "pubsub.yaml").read_text())
        if catalog_profile is not None:
            catalog_directories.add(verify_catalog_sidecar_policy(app_id, arguments, catalog_profile))
            authority = [path for path in files if path.name == "timesheets-route-authority.yaml"]
            require(len(authority) == 1, "Catalog sidecar did not load one exact owned authority component")
            try:
                rendered = json.loads(authority[0].read_text())
                verify_rendered_catalog_authority(rendered, endpoint)
            except (ValueError, KeyError, TypeError, AttributeError):
                raise FixtureError("Rendered catalog authority is unavailable") from None
            expected.append(contents[files.index(authority[0])])
        require(len(contents) == len(expected) and sorted(contents) == sorted(expected),
                "Dapr did not load exactly the intended isolated components")
    if catalog_profile is not None:
        require(len(catalog_directories) == 1, "Catalog sidecars did not select one SDK owner directory")
        verify_catalog_transport(sidecars, catalog_profile, placement, scheduler, process_directory)
    return sidecars


def dapr_manifests(directory):
    """Match Dapr's top-level YAML loader, including .yml and mixed case."""
    require(Path(directory).is_dir(), "Dapr component resource directory is unavailable")
    return [path for path in Path(directory).iterdir()
            if path.is_file() and path.suffix.lower() in (".yaml", ".yml")]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--log-directory", type=Path)
    parser.add_argument("--public-port", type=public_port_argument, default=8080,
                        help="Local public listener (default: 8080); internal and gateway ports stay fixed")
    parser.add_argument("--security-smoke", action="store_true",
                        help="Explicitly qualify generated Keycloak credentials and current NuGet runtime ownership")
    parser.add_argument("--route-catalog", action="store_true",
                        help="Compose the SDK catalog over this run's fresh private Redis and receiver policies")
    arguments = parser.parse_args()

    def interrupted(_signal, _frame):
        raise KeyboardInterrupt

    signal.signal(signal.SIGTERM, interrupted)
    return IsolatedFixture(log_directory=arguments.log_directory, public_port=arguments.public_port,
                           security_smoke=arguments.security_smoke, route_catalog=arguments.route_catalog).execute()


if __name__ == "__main__":
    raise SystemExit(main())
