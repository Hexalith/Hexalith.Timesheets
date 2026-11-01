"""Verify the NuGet-only locked graph and real plugin loader without release writes."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = 'scripts/repair-release-tooling-bundles.mjs'


class ReleaseToolingGraphTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        (self.root / 'scripts').mkdir()
        shutil.copy2(ROOT / SCRIPT, self.root / SCRIPT)
        for name in ('package.json', 'package-lock.json', '.releaserc.json'):
            shutil.copy2(ROOT / name, self.root / name)
        # Dereference links while copying so every dependency payload is private,
        # including selected plugins and executable entries under .bin.
        shutil.copytree(ROOT / 'node_modules', self.root / 'node_modules', symlinks=False)

    def invoke(self):
        return subprocess.run(['node', str(self.root / SCRIPT)], capture_output=True,
                              text=True, check=False, cwd=self.root, timeout=30)

    def update(self, name, transform):
        path = self.root / name
        value = json.loads(path.read_text())
        transform(value)
        path.write_text(json.dumps(value))

    def owned_payloads(self):
        snapshot = {}
        for path in self.root.rglob('*'):
            mode = path.lstat().st_mode
            if stat.S_ISLNK(mode):
                entry = ('symlink', os.readlink(path))
            elif stat.S_ISDIR(mode):
                entry = ('directory',)
            elif stat.S_ISREG(mode):
                entry = ('file', hashlib.sha256(path.read_bytes()).hexdigest())
            else:
                entry = ('special', stat.S_IFMT(mode))
            snapshot[str(path.relative_to(self.root))] = (stat.S_IMODE(mode), *entry)
        return snapshot

    def assert_refused(self, expected):
        before = self.owned_payloads()
        result = self.invoke()
        self.assertNotEqual(0, result.returncode, result.stdout)
        self.assertIn(expected, result.stderr)
        self.assertNotIn('Verified NuGet-only release tooling', result.stdout)
        self.assertEqual(before, self.owned_payloads())

    def test_locked_graph_loads_all_four_plugins_and_sixteen_hooks_without_mutation(self):
        before = self.owned_payloads()
        result = self.invoke()
        self.assertEqual(0, result.returncode, result.stderr)
        evidence = json.loads(result.stdout.splitlines()[0])
        self.assertEqual(len(evidence['plugins']), 4)
        self.assertEqual(len(evidence['loadedHooks']), 16)
        self.assertFalse(evidence['lifecycleExecution'])
        self.assertEqual(before, self.owned_payloads())

    def test_selected_plugin_append_is_detected_without_mutating_owning_install(self):
        plugin = 'node_modules/@semantic-release/exec/index.js'
        original = (ROOT / plugin).read_bytes()
        script = self.root / SCRIPT
        script.write_text("import { appendFileSync } from 'node:fs';\n" + script.read_text()
                          + "\nappendFileSync(new URL('../" + plugin
                          + "', import.meta.url), '\\n// mutation control\\n');\n")
        before = self.owned_payloads()
        result = self.invoke()
        self.assertEqual(0, result.returncode, result.stderr)
        with self.assertRaises(AssertionError):
            self.assertEqual(before, self.owned_payloads())
        self.assertEqual(original + b'\n// mutation control\n', (self.root / plugin).read_bytes())
        self.assertEqual(original, (ROOT / plugin).read_bytes())

    def test_lock_conflict_refuses_before_plugin_loading_or_mutation(self):
        def conflict(lock):
            alias = next(value for key, value in lock['packages'].items()
                         if key.endswith('/@semantic-release/npm'))
            alias['integrity'] = 'conflicting'
        self.update('package-lock.json', conflict)
        self.assert_refused('lock integrity mismatch')

    def test_reintroduced_bundled_npm_lock_graph_refuses(self):
        self.update('package-lock.json', lambda lock: lock['packages'].update({
            'node_modules/npm/node_modules/glob': {'version': '14.0.1'},
        }))
        self.assert_refused('Unused bundled npm graph reintroduced')

    def test_reintroduced_installed_npm_refuses_without_touching_payload(self):
        target = self.root / 'node_modules/npm'
        target.mkdir()
        (target / 'preserved').write_text('original npm content')
        self.assert_refused('Unused installed npm graph reintroduced')

    def test_installed_alias_identity_drift_refuses(self):
        self.update('node_modules/semantic-release/node_modules/@semantic-release/npm/package.json',
                    lambda package: package.update(name='@semantic-release/npm'))
        self.assert_refused('Installed unused npm publisher identity mismatch')

    def test_configured_npm_publisher_refuses(self):
        self.update('.releaserc.json', lambda config: config['plugins'].append('@semantic-release/npm'))
        self.assert_refused('four NuGet release plugins')

    def test_missing_selected_plugin_fails_the_real_loader(self):
        shutil.rmtree(self.root / 'node_modules/@semantic-release/exec')
        self.assert_refused('Cannot find module')

    def test_missing_lifecycle_export_fails_the_real_loader_check(self):
        target = self.root / 'node_modules/@semantic-release/exec'
        shutil.rmtree(target)
        target.mkdir()
        (target / 'package.json').write_text(json.dumps({'type': 'module', 'main': 'index.js'}))
        (target / 'index.js').write_text('export async function verifyConditions() {}\n')
        self.assert_refused('Effective release lifecycle hooks differ')

    def test_configuration_loading_never_executes_release_commands(self):
        def commands(config):
            for hook in ('verifyConditions', 'analyzeCommits', 'verifyRelease', 'generateNotes',
                         'prepare', 'publish', 'addChannel', 'success', 'fail'):
                config['plugins'][2][1][hook + 'Cmd'] = 'node -e "throw new Error(\'lifecycle executed\')"'
        self.update('.releaserc.json', commands)
        result = self.invoke()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn('no package mutation or lifecycle execution', result.stdout)


class ReleaseToolingWorkflowTests(unittest.TestCase):
    @staticmethod
    def validate(text):
        commands = [line.strip()[5:] for line in text.splitlines() if line.strip().startswith('run: ')]
        install = 'npm ci --ignore-scripts'
        installations = [index for index, command in enumerate(commands) if command == install]
        if not installations:
            raise ValueError('Missing locked tooling installation')
        for index in installations:
            if commands[index + 1:index + 4] != ['npm run repair:release-tooling', 'npm audit', 'npm audit signatures']:
                raise ValueError('Install must immediately verify configuration, audit all dependencies and verify signatures')

    def test_owned_installs_verify_before_full_audit_and_signatures(self):
        for name in ('ci.yml', 'release.yml'):
            with self.subTest(workflow=name):
                self.validate((ROOT / '.github/workflows' / name).read_text())

    def test_removing_or_moving_verification_before_install_is_detected(self):
        workflow = (ROOT / '.github/workflows/release.yml').read_text()
        removed = workflow.replace('        run: npm run repair:release-tooling\n', '')
        with self.assertRaises(ValueError):
            self.validate(removed)
        moved = removed.replace('        run: npm ci --ignore-scripts',
                                '        run: npm run repair:release-tooling\n        run: npm ci --ignore-scripts')
        with self.assertRaises(ValueError):
            self.validate(moved)

    def test_later_install_requires_its_own_verification_audits_and_signatures(self):
        following = ['npm run repair:release-tooling', 'npm audit', 'npm audit signatures']
        for name in ('ci.yml', 'release.yml'):
            workflow = (ROOT / '.github/workflows' / name).read_text()
            later = workflow + '\n        run: npm ci --ignore-scripts\n'
            complete = later + ''.join('        run: ' + command + '\n' for command in following)
            with self.subTest(workflow=name, missing=None):
                self.validate(complete)
            for missing in following:
                with self.subTest(workflow=name, missing=missing), self.assertRaises(ValueError):
                    self.validate(later + ''.join('        run: ' + command + '\n'
                                                 for command in following if command != missing))

    def test_missing_install_weakened_audit_or_missing_signatures_refuse(self):
        workflow = (ROOT / '.github/workflows/ci.yml').read_text()
        for command in ('npm ci --ignore-scripts', 'npm audit', 'npm audit signatures'):
            with self.subTest(command=command), self.assertRaises(ValueError):
                self.validate(workflow.replace('        run: ' + command + '\n', ''))
        for audit in ('npm audit --omit=dev', 'npm audit --audit-level=high'):
            with self.subTest(audit=audit), self.assertRaises(ValueError):
                self.validate(workflow.replace('run: npm audit\n', 'run: ' + audit + '\n'))


if __name__ == '__main__':
    unittest.main()
