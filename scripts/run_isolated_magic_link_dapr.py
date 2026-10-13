"""Run one real acceptance journey with disposable Redis and Dapr control services.

Build the current Debug source-reference solution first. No images are pulled and
only this invocation's infrastructure is removed. Sanitized evidence is retained
in a unique child of --log-directory, including failed and interrupted commands.
"""

import argparse
import base64
import binascii
import ctypes
import errno
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import signal
import socket
import subprocess
import tempfile
import time
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
APPHOST = ROOT / "src/Hexalith.Timesheets.AppHost/Hexalith.Timesheets.AppHost.csproj"
DEFAULT_COMPONENTS = APPHOST.parent / "DaprComponents"
COMPONENT_FILES = (Path("statestore.yaml"), Path("Timesheets/timesheets-readmodels.yaml"), Path("pubsub.yaml"))
REQUIRED_PORTS = (8080, 8081, 8082, 3501)
SHARED_CONTROL_PORTS = (6050, 6060, 50005, 50006)
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


def assert_ports_available():
    for port in REQUIRED_PORTS:
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


def available_control_port():
    for _ in range(20):
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            port = listener.getsockname()[1]
        if port not in SHARED_CONTROL_PORTS + REQUIRED_PORTS:
            return port
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
    credential = re.compile(
        r"signing.?key|(?:app|dapr).?api.?token|one.?time.?token|token.?hash|redis.?password|"
        r"dashboard.?login.?token|authorization|workload.?assertion|"
        r"(?:[\"']|\\\")?(?:access.?token|refresh.?token|token|password|secret)(?:[\"']|\\\")?\s*[:=]",
        re.IGNORECASE)
    decoder = json.JSONDecoder()
    value_fields = re.compile(r'''(?i)["']?(?:signing.?key|(?:app|dapr).?api.?token|one.?time.?token|token.?hash|redis.?password|dashboard.?login.?token|authorization|workload.?assertion|access.?token|refresh.?token|token|password|secret)["']?\s*[:=]\s*''')
    spans = []
    for match in value_fields.finditer(output):
        if spans and match.start() < spans[-1][1]:
            continue
        try:
            _, length = decoder.raw_decode(output[match.end():])
            spans.append((match.end(), match.end() + length))
        except ValueError:
            pass
    for start, end in reversed(spans):
        output = output[:start] + '"[redacted]"' + output[end:]
    lines = output.splitlines()
    retained = []
    index = 0
    while index < len(lines):
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
            if executable not in ("daprd", "dotnet", "aspire", "Hexalith.Timesheets", "Hexalith.EventStore", "Hexalith.Timesheets.AppHost") \
                    and not executable.startswith("dcp"):
                continue
            environment = (process / "environ").read_bytes().split(b"\0")
            bound = f"TIMESHEETS_LIVE_DAPR_COMPONENTS={directory}".encode() in environment
            if executable == "daprd":
                bound |= any(str(directory).encode() in arg for arg in arguments)
                bound |= bool(placement and placement.encode() in arguments or scheduler and scheduler.encode() in arguments)
            if bound:
                fields = (process / "stat").read_text().rsplit(")", 1)[1].split()
                found.append({"pid": int(process.name), "start": fields[19], "state": fields[0]})
        except (FileNotFoundError, ProcessLookupError):
            continue
    return found


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


class SharedSnapshot(dict):
    """Public aggregate evidence plus private in-memory TTL comparison data."""

    def __init__(self):
        super().__init__(redis={})
        self.expiring = {}
        self.captured_at = {}


def shared_preservation(before, after):
    """Allow only a previously captured, elapsed Redis TTL to remove a key."""
    expires = {}
    scheduler_fields = ("cluster_id", "keys", "sha256")
    require(all(field in snapshot["scheduler"] for snapshot in (before, after) for field in scheduler_fields),
            "Scheduler preservation identity is unavailable")
    matches = all(before["scheduler"][field] == after["scheduler"][field] for field in scheduler_fields)
    for database in before["redis"]:
        previous, current = before["redis"][database], after["redis"][database]
        matches &= (previous["persistent_keys"], previous["persistent_sha256"]) == (
            current["persistent_keys"], current["persistent_sha256"])
        old, new = before.expiring[database], after.expiring[database]
        allowed = 0
        for key, (value, deadline) in old.items():
            if key not in new:
                if deadline <= after.captured_at[database]:
                    allowed += 1
                else:
                    matches = False
            else:
                new_value, new_deadline = new[key]
                matches &= value == new_value and abs(deadline - new_deadline) <= 2
        matches &= not (new.keys() - old.keys())
        expires[database] = allowed
    return {"matches": bool(matches), "confirmed_natural_expirations": expires,
            "method": "Atomic read-only Redis KEYS/DUMP/PTTL/TIME DB0/13/14; etcd v3 all-key range on loopback:2379",
            "ttl_rule": "Retained expiring values and deadlines match; absent keys allowed only after their captured deadline"}


def scheduler_fingerprint(response):
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
    return {"cluster_id": str(int(header["cluster_id"])), "revision": str(int(header["revision"])),
            "keys": len(rows), "sha256": hashlib.sha256(
        json.dumps(rows, sort_keys=True, separators=(",", ":")).encode()).hexdigest()}


def read_only_shared_snapshot():
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
        raise FixtureError("Redis preservation snapshot is unavailable")

    result = SharedSnapshot()
    with socket.create_connection(("127.0.0.1", 6379), timeout=20) as connection:
        with connection.makefile("rb") as stream:
            def command(*arguments):
                arguments = [arg if isinstance(arg, bytes) else str(arg).encode() for arg in arguments]
                connection.sendall(b"*" + str(len(arguments)).encode() + b"\r\n" + b"".join(
                    b"$" + str(len(arg)).encode() + b"\r\n" + arg + b"\r\n" for arg in arguments))
                return read_resp(stream)
            for database in (0, 13, 14):
                require(command("SELECT", database) == b"OK", "Redis snapshot database selection failed")
                # Script reads only and holds atomic access. Pair each PTTL
                # with TIME so its absolute expiry remains comparable even
                # while a large broker snapshot takes measurable time.
                script = """
local keys = redis.call('KEYS', '*')
local rows = {}
for _, key in ipairs(keys) do
    local value = redis.call('DUMP', key)
    if value then
        local now = redis.call('TIME')
        local ttl = redis.call('PTTL', key)
        local deadline = ttl
        if ttl >= 0 then
            deadline = tonumber(now[1]) * 1000 + math.floor(tonumber(now[2]) / 1000) + ttl
        end
        table.insert(rows, {redis.sha1hex(key), redis.sha1hex(value), deadline})
    end
end
return {redis.call('TIME'), rows}
"""
                now, rows = command("EVAL", script, 0)
                timestamp = int(now[0]) * 1000 + int(now[1]) // 1000
                permanent = sorted((key.decode(), value.decode()) for key, value, ttl in rows if ttl == -1)
                result.expiring[str(database)] = {key.decode(): (value.decode(), ttl)
                                                  for key, value, ttl in rows if ttl >= 0}
                result.captured_at[str(database)] = timestamp
                result["redis"][str(database)] = {"keys": len(rows), "persistent_keys": len(permanent),
                    "persistent_sha256": hashlib.sha256(json.dumps(permanent, separators=(",", ":")).encode()).hexdigest(),
                    "expiring_keys": len(result.expiring[str(database)])}
    request = urllib.request.Request("http://127.0.0.1:2379/v3/kv/range", data=b'{"key":"AA==","range_end":"AA=="}',
                                     headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=20) as response:
        try:
            scheduler = json.load(response)
        except ValueError:
            raise FixtureError("Scheduler preservation range did not return valid JSON") from None
    result["scheduler"] = scheduler_fingerprint(scheduler)
    return result


class IsolatedFixture:
    """Own private infrastructure and one exact Aspire AppHost lifecycle."""

    def __init__(self, command=None, port_check=assert_ports_available, log_directory=None,
                 ownership_check=None, resource_check=bound_resource_processes):
        self.command = command or self._command
        self.injected_command = command is not None
        self.port_check = port_check
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
        self.environment = dict(os.environ)
        self.ownership_check = ownership_check or self._owned_apphost
        self.resource_check = resource_check
        self.start_attempted = False
        self.journey_shutdown_confirmed = True
        self.ordinal = 0
        self.shared_before = None
        self.evidence_write_failed = False
        self.captured_processes = []

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
        try:
            result = command(arguments, cwd=ROOT, env=self.environment, timeout=timeout)
        except KeyboardInterrupt as interruption:
            self.write_evidence(label + ".log", "Command interrupted.\nStdout:\n"
                + sanitized(getattr(interruption, "stdout", "")) + "Stderr:\n"
                + sanitized(getattr(interruption, "stderr", "")))
            raise
        except (subprocess.TimeoutExpired, OSError) as error:
            self.write_evidence(label + ".log",
                "Command could not complete safely.\nStdout:\n" + sanitized(getattr(error, "stdout", ""))
                + "Stderr:\n" + sanitized(getattr(error, "stderr", "")))
            raise FixtureError(f"{label} could not complete safely") from None
        self.write_evidence(label + ".log",
            "Command: " + sanitized(" ".join(arguments)) + f"Exit: {result.returncode}\nStdout:\n"
            + sanitized(result.stdout) + "Stderr:\n" + sanitized(result.stderr))
        if check and result.returncode != 0:
            raise FixtureError(f"{label} exited {result.returncode}; see sanitized evidence", result.returncode)
        return result

    def preflight(self):
        dapr = self.run_command(["dapr", "list"]).stdout
        require(not any(row.split() and row.split()[0] in ("eventstore", "timesheets")
                        for row in dapr.splitlines()), "Matching Dapr app ID exists; no AppHost started")
        apps = json_output(self.run_command(["aspire", "ps", "--non-interactive", "--format", "Json"]).stdout)
        require(isinstance(apps, list) and not apps, "An AppHost is already running; no AppHost started")
        self.port_check()

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
            except (FixtureError, OSError, KeyboardInterrupt) as error:
                failed = True
                reasons.append(str(error) if isinstance(error, FixtureError) else "Resource shutdown inspection did not complete")
        # Resolve every identity before removing any dependency. Preserve all
        # fixture artifacts if Docker ownership cannot be established.
        recovered = []
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
        if self.evidence_write_failed:
            failed = True
            reasons.append("Fixture evidence write failed")
        self.write_evidence("cleanup.json", json.dumps({
            "complete": not failed, "journey_descendants_gone": self.journey_shutdown_confirmed,
            "failure_reasons": reasons,
            "components_created": self.scratch is not None,
            "components_removed": bool(self.scratch and not Path(self.scratch).exists()),
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
        images = {service: self.installed_image("dapr_" + service) for service in ("redis", "placement", "scheduler")}
        self.container_id = self.create_container("redis", images["redis"],
            ["--publish", "127.0.0.1::6379", "--tmpfs", "/data"],
            ["redis-server", "--save", "", "--appendonly", "no"])
        endpoint = self.container_endpoint(self.container_id, 6379)
        for attempt in range(10):
            ready = self.run_command(["docker", "exec", self.container_id, "redis-cli", "ping"], check=False)
            if ready.returncode == 0 and ready.stdout.strip() == "PONG":
                break
            if attempt < 9:
                time.sleep(0.2)
        require(ready.returncode == 0 and ready.stdout.strip() == "PONG", "Disposable Redis did not become ready")
        placement_id = self.create_container("placement", images["placement"],
            ["--entrypoint", "/placement", "--publish", "127.0.0.1::50005"],
            ["--mode=standalone", "--inmem-store-enabled=true"])
        self.placement = self.container_endpoint(placement_id, 50005)
        # Scheduler advertises the host endpoint; select it before launch. A
        # competing bind fails Docker startup rather than selecting a shared port.
        scheduler_port = available_control_port()
        self.scheduler = "127.0.0.1:" + str(scheduler_port)
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

    def execute(self):
        exit_code = 1
        try:
            self.run_command(["aspire", "--version"])
            self.run_command(["dapr", "--version"])
            self.run_command(["docker", "version", "--format", "{{.Server.Version}}"])
            self.preflight()
            if not self.injected_command:
                self.shared_before = read_only_shared_snapshot()
                self.write_evidence("shared-before.json", json.dumps(self.shared_before, indent=2) + "\n")
            self.start_infrastructure()
            self.preflight()
            self.start_attempted = True
            self.run_command(["aspire", "start", "--no-build", "--non-interactive", "--apphost", str(APPHOST), "--format", "Json"], timeout=180)
            require(self.ownership_check(), "The started AppHost is not owned by this fixture")
            for resource in ("eventstore", "timesheets"):
                self.run_command(["aspire", "wait", resource, "--timeout", "60", "--apphost", str(APPHOST), "--non-interactive"], timeout=70)
            if not self.injected_command:
                verify_runtime_components(self.environment["TIMESHEETS_LIVE_DAPR_REDIS_HOST"], self.scratch,
                                          self.placement, self.scheduler)
                self.captured_processes = self.resource_check(self.scratch, self.placement, self.scheduler)
                self.write_evidence("runtime-isolation.json", json.dumps({
                    "redis": self.environment["TIMESHEETS_LIVE_DAPR_REDIS_HOST"],
                    "placement": self.placement, "scheduler": self.scheduler,
                    "components": str(self.scratch), "bound_processes": self.captured_processes}, indent=2) + "\n")
            executable = ROOT / "tests/Hexalith.Timesheets.IntegrationTests/bin/Debug/net10.0/Hexalith.Timesheets.IntegrationTests"
            result = self.run_command([str(executable), "-class", "*MagicLinkLiveDaprTests", "-noColor"], timeout=510, check=False, journey=True)
            exit_code = result.returncode
            print(sanitized((result.stdout or "") + (result.stderr or "")), end="", flush=True)
            if exit_code == 0:
                require_one_passing_test(result)
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
                    after = read_only_shared_snapshot()
                    self.write_evidence("shared-after.json", json.dumps(after, indent=2) + "\n")
                    preservation = shared_preservation(self.shared_before, after)
                    self.write_evidence("shared-preservation.json", json.dumps(preservation, indent=2) + "\n")
                    require(preservation["matches"], "Shared-service fingerprints changed; preservation is unproved")
                    print("Retained broker/state persistent values, unexpired TTL values and scheduler KV match", flush=True)
                except KeyboardInterrupt:
                    print("Shared-service preservation interrupted; preservation is unproved", flush=True)
                    exit_code = exit_code or 130
                except (FixtureError, OSError, ValueError):
                    print("Shared-service preservation could not be established; see evidence", flush=True)
                    if exit_code == 0:
                        exit_code = 1
            print(f"Sanitized fixture evidence: {self.log_directory}", flush=True)
            if self.evidence_write_failed and exit_code == 0:
                exit_code = 1
        return exit_code


def verify_runtime_components(endpoint, directory, placement, scheduler):
    """Verify actual daprd components and control-plane targets before commands."""
    validate_components(directory, endpoint)
    validate_control_plane(placement, scheduler)
    pids = subprocess.check_output(["pgrep", "-x", "daprd"], text=True).splitlines()
    for app_id, relative in (("eventstore", COMPONENT_FILES[0]), ("timesheets", COMPONENT_FILES[1])):
        matching = []
        for pid in pids:
            try:
                arguments = Path(f"/proc/{pid}/cmdline").read_bytes().decode().split("\0")
            except FileNotFoundError:
                continue
            if "--app-id" in arguments and arguments[arguments.index("--app-id") + 1] == app_id:
                matching.append(arguments)
        require(len(matching) == 1, "Fixture Dapr runtime identity is not unique")
        arguments = matching[0]
        for flag, address in (("--placement-host-address", placement), ("--scheduler-host-address", scheduler)):
            require(arguments.count(flag) == 1 and arguments[arguments.index(flag) + 1] == address,
                    "Dapr did not target the intended isolated control service")
        directories = [Path(arguments[index + 1]) for index, value in enumerate(arguments) if value == "--resources-path"]
        files = [path for resource_directory in directories for path in dapr_manifests(resource_directory)]
        contents = [path.read_text() for path in files]
        expected = [(Path(directory) / relative).read_text()]
        if app_id == "eventstore":
            expected.append((Path(directory) / "pubsub.yaml").read_text())
        require(len(contents) == len(expected) and sorted(contents) == sorted(expected),
                "Dapr did not load exactly the intended isolated components")


def dapr_manifests(directory):
    """Match Dapr's top-level YAML loader, including .yml and mixed case."""
    require(Path(directory).is_dir(), "Dapr component resource directory is unavailable")
    return [path for path in Path(directory).iterdir()
            if path.is_file() and path.suffix.lower() in (".yaml", ".yml")]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--log-directory", type=Path)
    arguments = parser.parse_args()

    def interrupted(_signal, _frame):
        raise KeyboardInterrupt

    signal.signal(signal.SIGTERM, interrupted)
    return IsolatedFixture(log_directory=arguments.log_directory).execute()


if __name__ == "__main__":
    raise SystemExit(main())
