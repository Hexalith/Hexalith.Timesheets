"""Equivalent reconstruction of the first review's in-memory mutation recipe.

Requires the pre-fix auditor and its evidence directory to reproduce the original
acceptances. Current fixed auditors are expected to reject these mutations.
The original auditor bytes were not saved by the first review.
"""
import argparse
import copy
import importlib.util
import json
from pathlib import Path
import sys

from recheck import SEMANTIC

parser = argparse.ArgumentParser()
parser.add_argument('--audit', type=Path, required=True)
parser.add_argument('--optimization-only', action='store_true')
args = parser.parse_args()
spec = importlib.util.spec_from_file_location('native_audit', args.audit)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
original_receipt = module.receipt
original_read = module.read

if args.optimization_only:
    module.read = lambda path: json.dumps({**json.loads(original_read(path)), 'exit_code': 99}) if path.name == 'native-run.json' else original_read(path)
    print('optimized audit with native exit 99:', module.audit()['passed'])
    raise SystemExit(0)

for label, patch in SEMANTIC:
    def changed(name, patch=patch):
        data = copy.deepcopy(original_receipt(name))
        patch(name, data)
        return data
    module.receipt = changed
    try:
        print(label, 'accepted:', module.audit()['passed'])
    except Exception as error:
        print(label, type(error).__name__, str(error))
    finally:
        module.receipt = original_receipt
