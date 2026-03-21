import { spawn } from 'node:child_process';
import { setTimeout as delay } from 'node:timers/promises';
import assert from 'node:assert/strict';

const sidecar = spawn('node', ['dist/main.js'], {
  cwd: process.cwd(),
  stdio: ['ignore', 'pipe', 'pipe'],
});

const logs = [];
for (const stream of [sidecar.stdout, sidecar.stderr]) {
  stream.on('data', (chunk) => logs.push(String(chunk)));
}

async function waitForHealth(url, retries = 50) {
  for (let attempt = 0; attempt < retries; attempt += 1) {
    try {
      const response = await fetch(url);
      if (response.ok) {
        return response.json();
      }
    } catch {
      // retry
    }
    await delay(100);
  }

  throw new Error(`Timed out waiting for ${url}\n${logs.join('')}`);
}

const frames = [];
let socket;

try {
  const health = await waitForHealth('http://127.0.0.1:3001/health');
  assert.equal(health.ok, true);
  assert.equal(health.transport.websocket, true);

  await new Promise((resolve, reject) => {
    socket = new WebSocket('ws://127.0.0.1:3001/stream');
    const timeout = setTimeout(() => reject(new Error('Timed out waiting for WebSocket frames.')), 5000);

    socket.addEventListener('open', () => undefined);
    socket.addEventListener('message', (event) => {
      frames.push(JSON.parse(event.data));
      if (frames.length >= 3) {
        clearTimeout(timeout);
        resolve();
      }
    });
    socket.addEventListener('error', (event) => {
      clearTimeout(timeout);
      reject(new Error(`WebSocket error: ${JSON.stringify(event)}`));
    });
  });

  const stateResponse = await fetch('http://127.0.0.1:3001/state');
  assert.equal(stateResponse.ok, true);
  const state = await stateResponse.json();

  assert.equal(frames[0].type, 'snapshot_frame');
  assert.equal(typeof frames[0].tick, 'number');
  assert.equal(frames[0].entityCount, 2);
  assert.equal(Array.isArray(frames[0].snapshots), true);
  assert.equal(frames[0].snapshots.length, 2);
  assert.equal(typeof frames[0].snapshots[0].position.x, 'number');
  assert.equal(typeof frames[0].snapshots[0].animation.attackingQ, 'number');
  assert.equal(Array.isArray(frames[0].snapshots[0].pose), true);
  assert.equal(state.type, 'snapshot_frame');
  assert.equal(state.entityCount, 2);
  assert.equal(state.snapshots.length, 2);
  assert.ok(frames[2].tick >= frames[0].tick);

  console.log(`Verified ${frames.length} streamed frames through ws://127.0.0.1:3001/stream.`);
} finally {
  socket?.close();
  sidecar.kill('SIGTERM');
  await new Promise((resolve) => sidecar.once('exit', resolve));
}
