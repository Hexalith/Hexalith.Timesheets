"""Compare xUnit full/json inventories, applying only manifest method renames.

Usage: python3 compare_discovery.py BEFORE.json.gz AFTER.json.gz MANIFEST.json.gz
Additional cases are allowed and reported; removed or altered original cases fail.
"""
import collections
import gzip
import json
import sys


def read(path):
    opener = gzip.open if path.endswith(".gz") else open
    with opener(path, "rt") as stream:
        return json.load(stream)


before, after, manifest = map(read, sys.argv[1:])
names = {item["Old"]: item["New"] for item in manifest}
matched = added = 0
for project, cases in before.items():
    def normalized(case, rename=False):
        case = {key: value for key, value in case.items() if key not in ("ID", "Assembly")}
        if rename and case["Method"] in names:
            old = case["Method"]
            case["Method"] = names[old]
            prefix = case["Class"] + "." + old
            assert case["DisplayName"].startswith(prefix)
            case["DisplayName"] = case["Class"] + "." + names[old] + case["DisplayName"][len(prefix):]
        return json.dumps(case, sort_keys=True)
    expected = collections.Counter(normalized(case, True) for case in cases)
    actual = collections.Counter(normalized(case) for case in after[project])
    missing = expected - actual
    assert not missing, f"Removed or altered cases in {project}: {list(missing)[:5]}"
    matched += sum(expected.values())
    added += sum((actual - expected).values())
print(f"Equivalent original cases: {matched}; additional cases: {added}; no removed/altered cases.")
