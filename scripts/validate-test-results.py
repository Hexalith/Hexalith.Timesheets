#!/usr/bin/env python3
"""Require fresh, nonempty, successful TRX evidence from all six solution test projects."""

import argparse
import sys
from pathlib import Path
from xml.etree import ElementTree as ET

from release_common import ROOT


def validate(directory: Path) -> dict:
    solution = ET.parse(ROOT / "Hexalith.Timesheets.slnx")
    names = {Path(project.get("Path", "")).stem for project in solution.findall(".//Project")
             if project.get("Path", "").startswith("tests/")}
    if len(names) != 6 or "Hexalith.Timesheets.Works.Tests" not in names:
        raise ValueError("The required six-project test inventory changed")
    reports = {}
    for name in sorted(names):
        matches = list(directory.rglob(name + ".trx"))
        if len(matches) != 1:
            raise ValueError(f"Expected exactly one current TRX for {name}, found {len(matches)}")
        document = ET.parse(matches[0])
        counters = [node for node in document.iter() if node.tag.rsplit("}", 1)[-1] == "Counters"]
        if len(counters) != 1:
            raise ValueError(f"Invalid TRX counters: {name}")
        counts = {key: int(counters[0].get(key, "0")) for key in ("total", "executed", "passed", "failed", "error", "timeout", "aborted")}
        if (counts["total"] <= 0 or counts["executed"] <= 0 or counts["passed"] <= 0
            or not counts["passed"] <= counts["executed"] <= counts["total"]
            or any(counts[key] != 0 for key in ("failed", "error", "timeout", "aborted"))):
            raise ValueError(f"Empty, all-skipped or failing test evidence for {name}: {counts}")
        results = [node for node in document.iter() if node.tag.rsplit("}", 1)[-1] == "UnitTestResult"]
        if len(results) != counts["total"] or sum(node.get("outcome") == "Passed" for node in results) != counts["passed"]:
            raise ValueError(f"Missing test case results or inconsistent counters: {name}")
        if any(node.get("outcome") not in {"Passed", "NotExecuted"} for node in results):
            raise ValueError(f"Non-successful test outcome in {name}")
        reports[name] = counts
    print("Validated successful TRX evidence for all six projects, including Works.Tests")
    return reports


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("result_directory", type=Path)
    try:
        validate(parser.parse_args().result_directory)
    except (ValueError, OSError, ET.ParseError) as error:
        print(f"Test evidence validation failed: {error}", file=sys.stderr)
        sys.exit(1)
