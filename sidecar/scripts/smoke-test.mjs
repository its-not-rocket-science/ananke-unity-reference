import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const port = process.env.ANANKE_PORT ?? '7374';
const baseUrl = `http://127.0.0.1:${port}`;
const sidecarDir = resolve(dirname(fileURLToPath(import.meta.url)), '..');

function ensure(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

async function fetchJson(path) {
  const response = await fetch(`${baseUrl}${path}`);
  ensure(response.ok, `Request to ${path} failed with ${response.status}.`);
  return response.json();
}

async function main() {
  const child = spawn(process.execPath, ['dist/main.js'], {
    cwd: sidecarDir,
    env: { ...process.env, ANANKE_PORT: port },
    stdio: ['ignore', 'pipe', 'pipe'],
  });

  let stdout = '';
  let stderr = '';
  child.stdout.on('data', (chunk) => {
    stdout += chunk.toString();
  });
  child.stderr.on('data', (chunk) => {
    stderr += chunk.toString();
  });

  try {
    let ready = false;
    for (let attempt = 0; attempt < 40; attempt += 1) {
      await delay(250);
      try {
        const health = await fetchJson('/health');
        ensure(health.ok === true, 'Health endpoint did not return ok=true.');
        ready = true;
        break;
      } catch {
        // retry
      }
    }

    ensure(ready, `Sidecar never became healthy.\nSTDOUT:\n${stdout}\nSTDERR:\n${stderr}`);

    await delay(350);

    const frame = await fetchJson('/frame');
    ensure(Array.isArray(frame.frames), 'Frame payload is missing a frames array.');
    ensure(frame.frames.length === 2, `Expected 2 entity frames, received ${frame.frames.length}.`);
    ensure(typeof frame.worldTick === 'number' && frame.worldTick >= 0, 'Frame payload is missing worldTick.');
    ensure(frame.frames.every((entity) => typeof entity.position?.x === 'number'), 'Entity positions were not serialised.');

    const state = await fetchJson('/state');
    ensure(Array.isArray(state), '/state did not return the frame list.');
    ensure(state.length === frame.frames.length, '/state returned a different number of frames.');

    console.log('Smoke test passed.');
  } finally {
    child.kill('SIGTERM');
    await new Promise((resolve) => child.once('exit', resolve));
  }
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
