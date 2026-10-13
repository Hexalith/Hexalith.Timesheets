"""Repeatable compiled-AppHost configuration checks on owned private services.

Run after the Debug solution build. Every probe exits before constructing
resources. Shared/missing-control probes and ordinary-default selection cannot
activate retained storage even if the validation under test regresses.
"""

import argparse
import json
from pathlib import Path
import signal
import subprocess
import tempfile

from run_isolated_magic_link_dapr import (
    APPHOST, COMPONENT_FILES, IsolatedFixture, FixtureError, available_control_port, json_output, read_only_shared_snapshot,
    require, shared_preservation, write_components,
)


def run_guards(log_directory=None):
    fixture = IsolatedFixture(log_directory=log_directory)
    fixture.environment["TIMESHEETS_LIVE_DAPR_VALIDATE_ONLY"] = "1"
    status = 1
    try:
        fixture.preflight()
        fixture.shared_before = read_only_shared_snapshot()
        fixture.write_evidence("shared-before.json", json.dumps(fixture.shared_before, indent=2) + "\n")
        fixture.start_infrastructure()
        fixture.preflight()
        executable = APPHOST.parent / "bin/Debug/net10.0/Hexalith.Timesheets.AppHost"
        require(executable.is_file(), "Build the Debug AppHost before running guard checks")
        baseline = dict(fixture.environment)
        cases = ("valid-isolated", "ordinary-defaults", "missing-opt-in", "wrong-scope", "missing-component",
                 "missing-pubsub", "unsafe-pubsub", "missing-placement", "shared-placement",
                 "missing-scheduler", "shared-scheduler", "duplicate-control-endpoint",
                 "shared-redis", "mismatched-actor-redis", "mismatched-readmodel-redis", "mismatched-pubsub-redis")
        results = []
        for name in cases:
            with tempfile.TemporaryDirectory(prefix="timesheets-guard-components-") as directory:
                write_components(directory, baseline["TIMESHEETS_LIVE_DAPR_REDIS_HOST"])
                fixture.environment = dict(baseline)
                fixture.environment["TIMESHEETS_LIVE_DAPR_COMPONENTS"] = directory
                expected = "Isolated Dapr components are missing or unsafe."
                if name == "ordinary-defaults":
                    fixture.environment.pop("TIMESHEETS_LIVE_DAPR_COMPONENTS")
                    fixture.environment.pop("TIMESHEETS_LIVE_DAPR_FIXTURE")
                elif name == "missing-opt-in":
                    fixture.environment.pop("TIMESHEETS_LIVE_DAPR_FIXTURE")
                    expected = "Isolated Dapr components require an explicit run-mode fixture opt-in."
                elif name == "wrong-scope":
                    path = Path(directory) / "Timesheets/timesheets-readmodels.yaml"
                    path.write_text(path.read_text().replace("  - timesheets", "  - eventstore"))
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
                    replacement = "127.0.0.1:" + str(available_control_port())
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
                result = fixture.run_command([str(executable), "--guard-probe=" + name], check=False)
                output = (result.stdout or "") + (result.stderr or "")
                if name in ("valid-isolated", "ordinary-defaults"):
                    marker = "isolated fixture" if name == "valid-isolated" else "checked-in defaults"
                    require(result.returncode == 0 and "Dapr component selection: " + marker in output,
                            name + " selection was not verified")
                else:
                    require(result.returncode != 0 and expected in output, name + " did not refuse before resource construction")
                require(not json_output(fixture.run_command(["aspire", "ps", "--non-interactive", "--format", "Json"]).stdout),
                        "A guard probe constructed an AppHost")
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
                require(preservation["matches"], "Retained shared-service preservation was not established")
            except KeyboardInterrupt:
                print("Shared-service preservation interrupted; preservation is unproved", flush=True)
                status = status or 130
            except (FixtureError, OSError, ValueError):
                status = status or 1
        print("Sanitized compiled guard evidence: " + str(fixture.log_directory), flush=True)
        if fixture.evidence_write_failed and status == 0:
            status = 1
    return status


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--log-directory", type=Path)
    arguments = parser.parse_args()
    def interrupted(_signal, _frame):
        raise KeyboardInterrupt
    signal.signal(signal.SIGTERM, interrupted)
    raise SystemExit(run_guards(arguments.log_directory))
