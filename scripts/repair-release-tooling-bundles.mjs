import { cpSync, existsSync, mkdtempSync, readFileSync, lstatSync, readdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';

function artifactDigest(directory) {
    const digest = createHash('sha256');
    function visit(path, relative) {
        for (const entry of readdirSync(path, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
            const location = join(path, entry.name);
            const name = join(relative, entry.name);
            if (entry.isSymbolicLink() || !(entry.isDirectory() || entry.isFile())) {
                throw new Error('Unexpected repair artifact filesystem entry');
            }
            digest.update(JSON.stringify([name, entry.isDirectory() ? 'directory' : 'file']));
            if (entry.isDirectory()) visit(location, name);
            else {
                const bytes = readFileSync(location);
                digest.update(String(bytes.length) + ':');
                digest.update(bytes);
            }
        }
    }
    visit(directory, '');
    return digest.digest('hex');
}

// npm ships these children inside its tarball, so root overrides cannot replace them.
// Install the two registry artifacts from a dedicated integrity-pinned lock, then
// replace only those package directories. Never rewrite npm's own manifest or code.
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const source = join(root, 'scripts', 'release-tooling-bundle-repair');
const lock = JSON.parse(readFileSync(join(source, 'package-lock.json'), 'utf8'));
const rootLock = JSON.parse(readFileSync(join(root, 'package-lock.json'), 'utf8'));
const names = ['ip-address', 'undici'];
for (const name of names) {
    const pinned = lock.packages[`node_modules/${name}`];
    const selected = rootLock.packages[`node_modules/npm/node_modules/${name}`];
    if (!pinned || !selected || selected.version !== pinned.version
        || selected.integrity !== pinned.integrity || selected.resolved !== pinned.resolved) {
        throw new Error(`Bundle repair lock mismatch: ${name}`);
    }
}
const npmDirectory = join(root, 'node_modules', 'npm');
for (const directory of [join(root, 'node_modules'), npmDirectory, join(npmDirectory, 'node_modules'),
    ...names.map(name => join(npmDirectory, 'node_modules', name))]) {
    if (existsSync(directory) && lstatSync(directory).isSymbolicLink()) throw new Error('Bundle repair refuses symbolic-link destinations');
}
const npmVersion = JSON.parse(readFileSync(join(npmDirectory, 'package.json'), 'utf8')).version;
if (npmVersion !== rootLock.packages['node_modules/npm'].version) {
    throw new Error('Unexpected bundled npm version');
}
const npmCli = process.env.npm_execpath;
if (!npmCli || !existsSync(npmCli)) throw new Error('Run bundle repair through npm install/ci');
const temporary = mkdtempSync(join(tmpdir(), 'timesheets-npm-bundle-repair-'));
try {
    for (const name of ['package.json', 'package-lock.json']) cpSync(join(source, name), join(temporary, name));
    const installed = spawnSync(process.execPath,
        [npmCli, 'ci', '--prefix', temporary, '--ignore-scripts', '--no-fund'],
        { stdio: 'inherit', cwd: root, env: process.env });
    if (installed.error || installed.status !== 0) throw installed.error ?? new Error('Pinned bundle repair install failed');
    const expectedArtifacts = new Map();
    for (const name of names) {
        const prepared = join(temporary, 'node_modules', name);
        if (JSON.parse(readFileSync(join(prepared, 'package.json'), 'utf8')).version
            !== lock.packages[`node_modules/${name}`].version) throw new Error(`Unexpected repair artifact: ${name}`);
        expectedArtifacts.set(name, artifactDigest(prepared));
    }
    for (const name of names) {
        const target = join(npmDirectory, 'node_modules', name);
        rmSync(target, { recursive: true, force: true });
        // Copy across filesystems; keep temporary cleanup independent of the destination.
        cpSync(join(temporary, 'node_modules', name), target, { recursive: true, errorOnExist: true });
    }
    // Verify the actual canonical destinations independently of the copy loop.
    for (const name of names) {
        const installed = join(npmDirectory, 'node_modules', name);
        if (JSON.parse(readFileSync(join(installed, 'package.json'), 'utf8')).version
            !== lock.packages[`node_modules/${name}`].version
            || artifactDigest(installed) !== expectedArtifacts.get(name)) {
            throw new Error(`Installed repair artifact mismatch: ${name}`);
        }
    }
    console.log('Installed integrity-pinned npm bundle repairs: ip-address 10.7.5, undici 6.29.1');
} finally {
    rmSync(temporary, { recursive: true, force: true });
}
