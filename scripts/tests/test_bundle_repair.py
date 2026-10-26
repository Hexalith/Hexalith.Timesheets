"""Fail-closed install-script checks without network or package-directory mutation."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class BundleRepairTests(unittest.TestCase):
    def _fixture(self, directory):
        root = Path(directory)
        (root / 'scripts/release-tooling-bundle-repair').mkdir(parents=True)
        shutil.copy2(ROOT / 'scripts/repair-release-tooling-bundles.mjs', root / 'scripts')
        for name in ('package.json', 'package-lock.json'):
            shutil.copy2(ROOT / 'scripts/release-tooling-bundle-repair' / name,
                         root / 'scripts/release-tooling-bundle-repair')
        shutil.copy2(ROOT / 'package-lock.json', root)
        target = root / 'node_modules/npm'
        target.mkdir(parents=True)
        version = json.loads((root / 'package-lock.json').read_text())['packages']['node_modules/npm']['version']
        (target / 'package.json').write_text(json.dumps({'version': version}))
        return root, target

    def _successful_install(self, root, target):
        cli = root / 'fixture-npm.cjs'
        cli.write_text("""
const fs = require('fs'), path = require('path');
const prefix = process.argv[process.argv.indexOf('--prefix') + 1];
for (const [name, version] of [['ip-address', '10.7.5'], ['undici', '6.29.1']]) {
    const directory = path.join(prefix, 'node_modules', name);
    fs.mkdirSync(directory, {recursive: true});
    fs.writeFileSync(path.join(directory, 'package.json'), JSON.stringify({name, version}));
    fs.writeFileSync(path.join(directory, 'fixture-payload.js'), name + ':' + version);
}
""")
        for name in ('ip-address', 'undici'):
            directory = target / 'node_modules' / name
            directory.mkdir(parents=True)
            (directory / 'package.json').write_text(json.dumps({'version': '0.0.0'}))
            (directory / 'old.js').write_text('old payload')
        (target / 'unaffected.js').write_text('original npm content')
        return dict(os.environ, npm_execpath=str(cli))

    def test_successful_install_replaces_both_actual_payloads_and_preserves_other_npm_files(self):
        with tempfile.TemporaryDirectory() as temporary:
            root, target = self._fixture(temporary)
            environment = self._successful_install(root, target)
            original_manifest = (target / 'package.json').read_bytes()
            result = subprocess.run(['node', str(root / 'scripts/repair-release-tooling-bundles.mjs')],
                                    capture_output=True, text=True, check=False, env=environment)
            self.assertEqual(0, result.returncode, result.stderr)
            for name, version in [('ip-address', '10.7.5'), ('undici', '6.29.1')]:
                installed = target / 'node_modules' / name
                self.assertEqual(version, json.loads((installed / 'package.json').read_text())['version'])
                self.assertEqual(name + ':' + version, (installed / 'fixture-payload.js').read_text())
                self.assertFalse((installed / 'old.js').exists())
            self.assertEqual(original_manifest, (target / 'package.json').read_bytes())
            self.assertEqual('original npm content', (target / 'unaffected.js').read_text())

    def test_wrong_copy_destination_fails_post_copy_artifact_check(self):
        with tempfile.TemporaryDirectory() as temporary:
            root, target = self._fixture(temporary)
            environment = self._successful_install(root, target)
            script = root / 'scripts/repair-release-tooling-bundles.mjs'
            text = script.read_text()
            original = "cpSync(join(temporary, 'node_modules', name), target,"
            self.assertIn(original, text)
            script.write_text(text.replace(original,
                "cpSync(join(temporary, 'node_modules', name), join(npmDirectory, 'wrong', name),"))
            result = subprocess.run(['node', str(script)], capture_output=True, text=True, check=False, env=environment)
            self.assertNotEqual(0, result.returncode)
            self.assertNotIn('Installed integrity-pinned npm bundle repairs', result.stdout)
            self.assertEqual('original npm content', (target / 'unaffected.js').read_text())

    def test_lock_conflict_refuses_before_install_or_destination_mutation(self):
        with tempfile.TemporaryDirectory() as temporary:
            root, target = self._fixture(temporary)
            lock = json.loads((root / 'package-lock.json').read_text())
            lock['packages']['node_modules/npm/node_modules/undici']['integrity'] = 'conflicting'
            (root / 'package-lock.json').write_text(json.dumps(lock))
            sentinel = target / 'preserved'
            sentinel.write_text('original')
            result = subprocess.run(['node', str(root / 'scripts/repair-release-tooling-bundles.mjs')],
                                    capture_output=True, text=True, check=False)
            self.assertNotEqual(0, result.returncode)
            self.assertIn('Bundle repair lock mismatch', result.stderr)
            self.assertEqual('original', sentinel.read_text())

    def test_symlink_destination_refuses_without_touching_external_directory(self):
        with tempfile.TemporaryDirectory() as temporary, tempfile.TemporaryDirectory() as external:
            root, target = self._fixture(temporary)
            children = target / 'node_modules'
            children.mkdir()
            (children / 'undici').symlink_to(external, target_is_directory=True)
            sentinel = Path(external) / 'preserved'
            sentinel.write_text('original')
            result = subprocess.run(['node', str(root / 'scripts/repair-release-tooling-bundles.mjs')],
                                    capture_output=True, text=True, check=False)
            self.assertNotEqual(0, result.returncode)
            self.assertIn('refuses symbolic-link destinations', result.stderr)
            self.assertEqual('original', sentinel.read_text())


class BundleRepairWorkflowTests(unittest.TestCase):
    @staticmethod
    def validate(text):
        lines = text.splitlines()
        for index, line in enumerate(lines):
            if 'run: npm ci --ignore-scripts' not in line:
                continue
            following = [candidate.strip()[5:] for candidate in lines[index + 1:]
                         if candidate.strip().startswith('run: ')]
            if not following or following[0] != 'npm run repair:release-tooling':
                raise ValueError('Ignored lifecycle install must immediately run pinned bundle repair')
            if len(following) < 3 or following[1:3] != ['npm audit', 'npm audit signatures']:
                raise ValueError('Installed tree audit and signature checks must follow repair')

    def test_owned_ignored_script_installs_repair_before_audits_and_validators(self):
        for workflow in (ROOT / '.github/workflows').glob('*.yml'):
            with self.subTest(workflow=workflow.name):
                self.validate(workflow.read_text())

    def test_removing_or_moving_repair_before_install_is_detected(self):
        workflow = (ROOT / '.github/workflows/release.yml').read_text()
        removed = workflow.replace('        run: npm run repair:release-tooling\n', '')
        with self.assertRaises(ValueError):
            self.validate(removed)
        moved = removed.replace('        run: npm ci --ignore-scripts',
                                '        run: npm run repair:release-tooling\n        run: npm ci --ignore-scripts')
        with self.assertRaises(ValueError):
            self.validate(moved)
