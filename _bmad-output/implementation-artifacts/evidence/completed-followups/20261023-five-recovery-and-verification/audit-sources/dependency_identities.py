"""Record committed gitlinks and actual HEAD/dirty state of root-declared checkouts.

Usage from repository root: python3 dependency_identities.py OUTPUT.json
This is an inventory superset, not a claim every checkout enters every build.
"""
import json
from pathlib import Path
import subprocess
import sys


def git(*args, cwd=None):
    return subprocess.check_output(["git", *args], cwd=cwd, text=True).strip()


paths = git("config", "--file", ".gitmodules", "--get-regexp", r"submodule\..*\.path").splitlines()
result = {
    "timesheetsBaseline": git("rev-parse", "HEAD"),
    "rootDirty": bool(git("status", "--porcelain")),
    "inventory": "all root-declared dependency checkouts; includes dependencies not compiled by every project",
    "dependencies": [],
}
for line in paths:
    path = line.split(maxsplit=1)[1]
    status = git("status", "--porcelain", cwd=path).splitlines()
    result["dependencies"].append({
        "path": path,
        "committedGitlink": git("ls-tree", "HEAD", "--", path).split()[2],
        "actualHead": git("rev-parse", "HEAD", cwd=path),
        "dirty": bool(status),
        "status": status,
    })
Path(sys.argv[1]).write_text(json.dumps(result, indent=2) + "\n")
