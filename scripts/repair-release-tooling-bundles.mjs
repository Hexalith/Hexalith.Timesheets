// Compatibility entry point for the existing install and CI commands. The unused
// npm publisher graph is now excluded by a locked npm alias; never patch packages.
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const readJson = path => JSON.parse(readFileSync(path, 'utf8'));
const manifest = readJson(join(root, 'package.json'));
const lock = readJson(join(root, 'package-lock.json'));
const configuration = readJson(join(root, '.releaserc.json'));
const expectedHooks = {
    '@semantic-release/commit-analyzer': ['analyzeCommits'],
    '@semantic-release/release-notes-generator': ['generateNotes'],
    '@semantic-release/exec': ['verifyConditions', 'analyzeCommits', 'verifyRelease', 'generateNotes',
        'prepare', 'publish', 'addChannel', 'success', 'fail'],
    '@semantic-release/github': ['verifyConditions', 'publish', 'addChannel', 'success', 'fail'],
};
const pluginNames = plugins => plugins.map(plugin => Array.isArray(plugin) ? plugin[0] : plugin);
assert.deepEqual(pluginNames(configuration.plugins), Object.keys(expectedHooks),
    'Release configuration must explicitly select the four NuGet release plugins');
assert.equal(manifest.overrides?.['semantic-release']?.['@semantic-release/npm'],
    'npm:@semantic-release/error@4.0.0', 'Missing exact unused npm publisher override');
const aliases = Object.entries(lock.packages).filter(([path]) => path.endsWith('/@semantic-release/npm'));
assert.equal(aliases.length, 1, 'Expected one locked unused npm publisher alias');
const [aliasPath, alias] = aliases[0];
const error = lock.packages['node_modules/@semantic-release/error'];
assert.equal(alias?.name, '@semantic-release/error', 'Unused npm publisher lock identity mismatch');
for (const key of ['version', 'resolved', 'integrity']) {
    assert.ok(error?.[key], `Missing locked error package ${key}`);
    assert.equal(alias[key], error[key], `Unused npm publisher lock ${key} mismatch`);
}
assert.equal(alias.version, '4.0.0', 'Unexpected unused npm publisher alias version');
for (const path of Object.keys(lock.packages)) {
    assert.ok(!/(^|\/)node_modules\/npm(?:\/|$)/.test(path), 'Unused bundled npm graph reintroduced');
}
assert.ok(!existsSync(join(root, 'node_modules', 'npm')), 'Unused installed npm graph reintroduced');

const require = createRequire(join(root, 'package.json'));
const semanticDirectory = dirname(require.resolve('semantic-release'));
const aliasDirectory = dirname(require.resolve('@semantic-release/npm', { paths: [semanticDirectory] }));
assert.equal(aliasDirectory, join(root, aliasPath), 'Installed unused npm publisher location mismatch');
const installedAlias = readJson(join(aliasDirectory, 'package.json'));
assert.equal(installedAlias.name, alias.name, 'Installed unused npm publisher identity mismatch');
assert.equal(installedAlias.version, alias.version, 'Installed unused npm publisher version mismatch');
assert.equal(readJson(join(semanticDirectory, 'package.json')).version, manifest.devDependencies['semantic-release'],
    'Installed semantic-release version mismatch');

// Use semantic-release's actual configuration/plugin loader. Importing and
// normalizing plugins does not execute any release lifecycle, including dry-run
// verify hooks that could call external services or publication preflights.
const loaded = [];
const logger = {
    success(message) {
        const match = /^Loaded plugin "([^"]+)" from "([^"]+)"$/.exec(message);
        assert.ok(match, `Unexpected plugin loader message: ${message}`);
        loaded.push(`${match[2]}:${match[1]}`);
    },
};
const { default: getConfig } = await import(pathToFileURL(join(semanticDirectory, 'lib', 'get-config.js')));
const effective = await getConfig({ cwd: root, env: process.env, stdout: process.stdout, stderr: process.stderr, logger });
assert.deepEqual(effective.options.plugins, configuration.plugins, 'Effective release plugins differ from the NuGet configuration');
const expected = Object.entries(expectedHooks).flatMap(([plugin, hooks]) => hooks.map(hook => `${plugin}:${hook}`));
assert.deepEqual(loaded.sort(), expected.sort(), 'Effective release lifecycle hooks differ from the NuGet configuration');
for (const hooks of Object.values(expectedHooks)) {
    for (const hook of hooks) assert.equal(typeof effective.plugins[hook], 'function', `Missing effective lifecycle: ${hook}`);
}
console.log(JSON.stringify({ plugins: Object.keys(expectedHooks), loadedHooks: loaded, lifecycleExecution: false }));
console.log('Verified NuGet-only release tooling: 4 plugins, 16 lifecycle hooks; no package mutation or lifecycle execution');
