"""Repeatable compiled-AppHost configuration checks on owned private services.

Run after the Debug solution build. Every probe exits before constructing
resources. Shared/missing-control probes and ordinary-default selection cannot
activate retained storage even if the validation under test regresses.
--configuration-only runs the same no-resource probes without starting private
services or requiring an idle runtime; it supplies unused loopback test values.
"""

import argparse
import json
from pathlib import Path
import signal
import subprocess
import tempfile

from run_isolated_magic_link_dapr import (
    APPHOST, COMPONENT_FILES, GATEWAY_EXECUTABLE_SETTING, GATEWAY_DIRECTORY_SETTING,
    IsolatedFixture, FixtureError, available_control_port, json_output, read_only_shared_snapshot,
    require, shared_preservation, write_components,
)


def run_guards(log_directory=None, configuration_only=False):
    fixture = IsolatedFixture(log_directory=log_directory)
    # IConfiguration treats environment keys case-insensitively and aliases __
    # to :. Remove caller overrides, including host-prefixed providers, so
    # missing-setting probes actually exercise missing settings.
    controlled = {key.casefold() for key in ("TIMESHEETS_LIVE_DAPR_FIXTURE", "TIMESHEETS_LIVE_DAPR_COMPONENTS",
        "TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY", "TIMESHEETS_LIVE_DAPR_REDIS_HOST",
        GATEWAY_EXECUTABLE_SETTING, GATEWAY_DIRECTORY_SETTING,
        "Dapr:PlacementHostAddress", "Dapr:SchedulerHostAddress", "AppHost:Operation", "Publishing:Publisher")}
    for key in tuple(fixture.environment):
        normalized = key.replace("__", ":").casefold()
        aliases = [normalized]
        for prefix in ("aspire_", "dotnet_"):
            if normalized.startswith(prefix):
                aliases.append(normalized[len(prefix):])
        if any(alias in controlled for alias in aliases):
            del fixture.environment[key]
    fixture.environment["TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY"] = "1"
    status = 1
    try:
        if configuration_only:
            fixture.placement = "127.0.0.1:16050"
            fixture.scheduler = "127.0.0.1:16060"
            fixture.environment.update({"TIMESHEETS_LIVE_DAPR_FIXTURE": "1",
                "TIMESHEETS_LIVE_DAPR_REDIS_HOST": "127.0.0.1:16379",
                "Dapr__PlacementHostAddress": fixture.placement,
                "Dapr__SchedulerHostAddress": fixture.scheduler})
        else:
            fixture.preflight()
            fixture.capture_shared_baseline(read_only_shared_snapshot)
            fixture.preflight()
            fixture.start_infrastructure()
            fixture.preflight()
        fixture.write_evidence("guards-mode.json", json.dumps({"configuration_only": configuration_only,
            "resources_enabled": False}, indent=2) + "\n")
        executable = APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
        require(executable.is_file(), "Build the Debug AppHost before running guard checks")
        baseline = dict(fixture.environment)
        cases = ("valid-isolated", "ordinary-defaults", "bare-fixture-flag", "missing-components",
                 "empty-components", "whitespace-components", "publish-fixture", "publish-bare-fixture",
                 "missing-opt-in", "wrong-scope", "missing-component",
                 "missing-pubsub", "unsafe-pubsub", "missing-placement", "shared-placement",
                 "missing-scheduler", "shared-scheduler", "duplicate-control-endpoint",
                 "shared-redis", "mismatched-actor-redis", "mismatched-readmodel-redis", "mismatched-pubsub-redis",
                 "valid-compiled-isolated", "compiled-executable-only", "compiled-directory-only",
                 "compiled-empty-executable", "compiled-empty-directory", "compiled-blank-executable", "compiled-blank-directory",
                 "ordinary-compiled", "ordinary-compiled-executable-only", "ordinary-compiled-directory-only",
                 "publish-compiled", "unsafe-components-compiled", "missing-placement-compiled")
        results = []
        for name in cases:
            with tempfile.TemporaryDirectory(prefix="timesheets-guard-components-") as directory:
                write_components(directory, baseline["TIMESHEETS_LIVE_DAPR_REDIS_HOST"])
                fixture.environment = dict(baseline)
                fixture.environment["TIMESHEETS_LIVE_DAPR_COMPONENTS"] = directory
                expected = "Isolated Dapr components are missing or unsafe."
                mode = "publish" if name.startswith("publish-") else "run"
                # Command-line configuration has precedence over every inherited
                # provider. A regressed admission guard still cannot run resources.
                arguments = [str(executable), "--guard-probe=" + name,
                             "--TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY=1", "--AppHost:Operation=" + mode]
                if "compiled" in name:
                    fixture.environment.update({GATEWAY_EXECUTABLE_SETTING: str(executable),
                                                GATEWAY_DIRECTORY_SETTING: str(executable.parent)})
                    expected = "Compiled gateway launch requires both executable and working directory."
                    if name.endswith("executable-only"):
                        fixture.environment.pop(GATEWAY_DIRECTORY_SETTING)
                    elif name.endswith("directory-only"):
                        fixture.environment.pop(GATEWAY_EXECUTABLE_SETTING)
                    elif name.startswith(("compiled-empty-", "compiled-blank-")):
                        setting = GATEWAY_EXECUTABLE_SETTING if name.endswith("executable") else GATEWAY_DIRECTORY_SETTING
                        fixture.environment[setting] = "" if "empty" in name else " \t "
                    if name.startswith("ordinary-") or name == "publish-compiled":
                        expected = "Compiled gateway launch requires a validated isolated run-mode fixture."
                if name == "ordinary-defaults" or name.startswith("ordinary-compiled"):
                    fixture.environment.pop("TIMESHEETS_LIVE_DAPR_COMPONENTS")
                    fixture.environment.pop("TIMESHEETS_LIVE_DAPR_FIXTURE")
                    fixture.environment.pop("Dapr__PlacementHostAddress")
                    fixture.environment.pop("Dapr__SchedulerHostAddress")
                elif name in ("bare-fixture-flag", "missing-components", "empty-components", "whitespace-components"):
                    expected = "Isolated Dapr fixture requires an explicit component directory."
                    if name in ("bare-fixture-flag", "missing-components"):
                        fixture.environment.pop("TIMESHEETS_LIVE_DAPR_COMPONENTS")
                    else:
                        fixture.environment["TIMESHEETS_LIVE_DAPR_COMPONENTS"] = "" if name == "empty-components" else " \t "
                    if name == "bare-fixture-flag":
                        fixture.environment.pop("Dapr__PlacementHostAddress")
                        fixture.environment.pop("Dapr__SchedulerHostAddress")
                elif name in ("publish-fixture", "publish-bare-fixture", "publish-compiled"):
                    if name == "publish-bare-fixture":
                        fixture.environment.pop("TIMESHEETS_LIVE_DAPR_COMPONENTS")
                        fixture.environment.pop("Dapr__PlacementHostAddress")
                        fixture.environment.pop("Dapr__SchedulerHostAddress")
                    arguments.extend(["--publisher=manifest", "--output-path=" + str(Path(directory) / "unused-manifest.json")])
                    if name != "publish-compiled":
                        expected = "Isolated Dapr components require an explicit run-mode fixture opt-in."
                elif name == "missing-opt-in":
                    fixture.environment.pop("TIMESHEETS_LIVE_DAPR_FIXTURE")
                    expected = "Isolated Dapr components require an explicit run-mode fixture opt-in."
                elif name == "wrong-scope":
                    path = Path(directory) / "Timesheets/timesheets-readmodels.yaml"
                    path.write_text(path.read_text().replace("  - timesheets", "  - eventstore"))
                elif name == "unsafe-components-compiled":
                    expected = "Isolated Dapr components are missing or unsafe."
                    (Path(directory) / "pubsub.yaml").unlink()
                elif name == "missing-placement-compiled":
                    expected = "Isolated Dapr placement and scheduler endpoints are missing or unsafe."
                    fixture.environment.pop("Dapr__PlacementHostAddress")
                elif name == "missing-component":
                    (Path(directory) / "Timesheets/timesheets-readmodels.yaml").unlink()
                elif name == "missing-pubsub":
                    (Path(directory) / "pubsub.yaml").unlink()
                elif name == "unsafe-pubsub":
                    path = Path(directory) / "pubsub.yaml"
                    path.write_text(path.read_text().replace("type: pubsub.redis", "type: pubsub.unsafe"))
                elif name == "shared-redis":
                    for relative in COMPONENT_FILES:
                        path = Path(directory) / relative
                        path.write_text(path.read_text().replace(baseline["TIMESHEETS_LIVE_DAPR_REDIS_HOST"], "127.0.0.1:6379"))
                elif name.startswith("mismatched-"):
                    relative = COMPONENT_FILES[("actor", "readmodel", "pubsub").index(name.split("-")[1])]
                    path = Path(directory) / relative
                    port = 16400 + COMPONENT_FILES.index(relative) if configuration_only else available_control_port()
                    replacement = "127.0.0.1:" + str(port)
                    require(replacement != baseline["TIMESHEETS_LIVE_DAPR_REDIS_HOST"], "Guard mismatch endpoint was not distinct")
                    path.write_text(path.read_text().replace(baseline["TIMESHEETS_LIVE_DAPR_REDIS_HOST"], replacement))
                elif name in ("missing-placement", "shared-placement", "missing-scheduler", "shared-scheduler", "duplicate-control-endpoint"):
                    expected = "Isolated Dapr placement and scheduler endpoints are missing or unsafe."
                    if name.startswith("missing-"):
                        fixture.environment.pop("Dapr__" + name.split("-")[1].title() + "HostAddress")
                    elif name == "shared-placement":
                        fixture.environment["Dapr__PlacementHostAddress"] = "127.0.0.1:50005"
                    elif name == "shared-scheduler":
                        fixture.environment["Dapr__SchedulerHostAddress"] = "127.0.0.1:50006"
                    else:
                        fixture.environment["Dapr__SchedulerHostAddress"] = fixture.placement
                # Direct execution is a compiled configuration probe, with no
                # Build/Run or resource construction. It is not an AppHost launch.
                result = fixture.run_command(arguments, check=False)
                output = (result.stdout or "") + (result.stderr or "")
                if name in ("valid-isolated", "ordinary-defaults", "valid-compiled-isolated"):
                    marker = "checked-in defaults" if name == "ordinary-defaults" else "isolated fixture"
                    require(result.returncode == 0 and "Dapr component selection: " + marker in output,
                            name + " selection was not verified")
                    launch = "compiled executable" if name == "valid-compiled-isolated" else "project"
                    require("Gateway launch selection: " + launch in output, name + " gateway selection was not verified")
                else:
                    require(result.returncode != 0 and expected in output, name + " did not refuse before resource construction")
                if not configuration_only:
                    require(not json_output(fixture.run_command(["aspire", "ps", "--non-interactive", "--format", "Json"]).stdout),
                            "An AppHost appeared during guard checks; verification cannot continue")
                    fixture.preflight()
                results.append({"case": name, "exit": result.returncode, "passed": True})
                print(name + ": compiled configuration check passed; exit " + str(result.returncode), flush=True)
        fixture.write_evidence("guards.json", json.dumps(results, indent=2) + "\n")
        fixture.environment = baseline
        status = 0
    except KeyboardInterrupt:
        status = 130
    except (FixtureError, OSError, subprocess.TimeoutExpired) as error:
        print(str(error) if isinstance(error, FixtureError) else "Guard checks could not complete safely", flush=True)
    finally:
        if not fixture.cleanup() and status == 0:
            status = 1
        if fixture.shared_before is not None:
            try:
                after = read_only_shared_snapshot()
                fixture.write_evidence("shared-after.json", json.dumps(after, indent=2) + "\n")
                preservation = shared_preservation(fixture.shared_before, after)
                fixture.write_evidence("shared-preservation.json", json.dumps(preservation, indent=2) + "\n")
                require(preservation["matches"], "Shared-service preservation failed: " + "; ".join(preservation["mismatches"]))
            except KeyboardInterrupt:
                print("Shared-service preservation interrupted; preservation is unproved", flush=True)
                status = status or 130
            except (FixtureError, OSError, ValueError) as error:
                print(str(error) if isinstance(error, FixtureError)
                      else "Shared-service preservation could not be established; see evidence", flush=True)
                status = status or 1
        print("Sanitized compiled guard evidence: " + str(fixture.log_directory), flush=True)
        if fixture.evidence_write_failed and status == 0:
            status = 1
    return status


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--log-directory", type=Path)
    parser.add_argument("--configuration-only", action="store_true",
                        help="Run only compiled validation probes; never start infrastructure or resources")
    arguments = parser.parse_args()
    def interrupted(_signal, _frame):
        raise KeyboardInterrupt
    signal.signal(signal.SIGTERM, interrupted)
    raise SystemExit(run_guards(arguments.log_directory, arguments.configuration_only))
