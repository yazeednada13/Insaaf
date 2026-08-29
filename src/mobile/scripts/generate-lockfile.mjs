/**
 * Generates package-lock.json using registry.npmjs.org (avoids hung npm CLI on some Windows setups).
 * Run: node scripts/generate-lockfile.mjs
 */
import { writeFileSync, readFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

const rootDir = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const packageJson = JSON.parse(readFileSync(resolve(rootDir, 'package.json'), 'utf8'));

const allDeps = {
  ...packageJson.dependencies ?? {},
  ...packageJson.devDependencies ?? {},
};

const lock = {
  name: packageJson.name,
  version: packageJson.version,
  lockfileVersion: 3,
  requires: true,
  packages: {
    '': {
      name: packageJson.name,
      version: packageJson.version,
      dependencies: packageJson.dependencies ?? {},
      devDependencies: packageJson.devDependencies ?? {},
    },
  },
};

const visited = new Set();

async function fetchJson(url) {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`Failed ${url}: ${response.status}`);
  }
  return response.json();
}

function parseDependency(name, spec) {
  if (spec.startsWith('npm:')) {
    const rest = spec.slice(4);
    const at = rest.lastIndexOf('@');
    if (at > 0) {
      return { name: rest.slice(0, at), spec: rest.slice(at + 1) };
    }
  }

  return { name, spec };
}

function pickVersion(versions, spec) {
  const includePrerelease = spec.includes('-');
  const available = Object.keys(versions)
    .filter((v) => includePrerelease || !v.includes('-'))
    .sort((a, b) => {
      const pa = a.split('.').map((part) => Number(part.split('-')[0]) || 0);
      const pb = b.split('.').map((part) => Number(part.split('-')[0]) || 0);
      for (let i = 0; i < 3; i += 1) {
        if (pa[i] !== pb[i]) return pa[i] - pb[i];
      }
      return a.localeCompare(b);
    });

  const exact = spec.replace(/^[\^~]/, '');
  if (available.includes(exact)) {
    return exact;
  }

  if (spec.startsWith('~')) {
    const rest = spec.slice(1);
    const baseParts = rest.split('.').map(Number);

    if (baseParts.length === 1) {
      const match = available.filter((v) => Number(v.split('.')[0]) === baseParts[0]);
      return match.at(-1);
    }

    const base = baseParts;
    const match = available.filter((v) => {
      const core = v.split('-')[0];
      const p = core.split('.').map(Number);
      return p[0] === base[0] && p[1] === base[1];
    });
    return match.at(-1);
  }

  if (spec.startsWith('^')) {
    const raw = spec.slice(1);
    if (raw.includes('-')) {
      const major = Number(raw.split('.')[0]);
      const match = available.filter((v) => Number(v.split('.')[0]) === major);
      return match.at(-1);
    }

    const base = raw.split('.').map(Number);
    const match = available.filter((v) => {
      const core = v.split('-')[0];
      const p = core.split('.').map(Number);
      if (base[0] === 0) {
        if (base[1] === 0) return p[0] === 0 && p[1] === 0 && p[2] === base[2];
        return p[0] === 0 && p[1] === base[1];
      }
      return p[0] === base[0];
    });
    return match.at(-1);
  }

  return available.includes(spec) ? spec : available.at(-1);
}

async function resolvePackage(name, spec, parentPath = '') {
  const meta = await fetchJson(`https://registry.npmjs.org/${encodeURIComponent(name)}`);
  const version = pickVersion(meta.versions, spec) ?? meta['dist-tags']?.latest;
  if (!version) {
    throw new Error(`No version for ${name}@${spec}`);
  }

  const key = `${name}@${version}`;
  if (visited.has(key)) {
    return;
  }
  visited.add(key);

  const nodePath = parentPath ? `${parentPath}/node_modules/${name}` : `node_modules/${name}`;
  if (!lock.packages[nodePath]) {
    const versionMeta = meta.versions[version];
    lock.packages[nodePath] = {
      version,
      resolved: versionMeta.dist.tarball,
      integrity: versionMeta.dist.integrity ?? undefined,
      license: versionMeta.license,
    };
  }

  const versionMeta = meta.versions[version];
  const childDeps = versionMeta.dependencies ?? {};

  for (const [depName, depSpec] of Object.entries(childDeps)) {
    const { name: resolvedName, spec: resolvedSpec } = parseDependency(depName, depSpec);
    await resolvePackage(resolvedName, resolvedSpec, nodePath);
  }
}

for (const [name, spec] of Object.entries(allDeps)) {
  console.log(`Resolving ${name}@${spec}`);
  await resolvePackage(name, spec);
}

writeFileSync(resolve(rootDir, 'package-lock.json'), JSON.stringify(lock, null, 2));
console.log('package-lock.json written with', Object.keys(lock.packages).length, 'packages');
