"""Capture each built test executable's pre-enumerated full/json discovery.

Usage from repository root: python3 discover.py OUTPUT.json
"""
import json
from pathlib import Path
import subprocess
import sys

projects = ("ArchitectureTests", "Contracts.Tests", "IntegrationTests", "Projections.Tests", "Server.Tests", "Works.Tests")
result = {}
for suffix in projects:
    project = "Hexalith.Timesheets." + suffix
    executable = Path("tests") / project / "bin" / "Debug" / "net10.0" / project
    output = subprocess.check_output([str(executable), "-preEnumerateTheories", "-list", "full/json"], text=True)
    result[project] = json.loads(output)
Path(sys.argv[1]).write_text(json.dumps(result, indent=2) + "\n")
print(f"Captured {sum(map(len, result.values()))} cases in {len(result)} suites.")
