/**
 * ananke-unity-sidecar/server.js
 *
 * Node.js HTTP sidecar that drives the Ananke simulation at 20 Hz and exposes
 * a snapshot endpoint for Unity to poll.
 *
 * Endpoints:
 *   GET /health  →  { "ok": true }
 *   GET /state   →  AnankeSnapshot[] (see Assets/Ananke/Scripts/AnankeSnapshot.cs)
 *
 * TODO (M2 stretch): Upgrade to WebSocket push using the `ws` npm package so
 * Unity receives frames without polling overhead. Push a message after each
 * stepWorld call:  ws.send(JSON.stringify(latestSnapshots));
 *
 * Usage:
 *   npm install
 *   npm start           # production
 *   npm run dev         # auto-restart on file change
 */

import http from "node:http";
import {
  createWorld,
  stepWorld,
  extractRigSnapshots,
  buildAICommands,
  buildWorldIndex,
  buildSpatialIndex,
  AI_PRESETS,
  SCALE,
} from "@its-not-rocket-science/ananke";

// ── Configuration ─────────────────────────────────────────────────────────────

// Use port 3001 to avoid colliding with the Godot sidecar (3000) if both
// are running simultaneously on the same machine.
const PORT       = 3001;
const TICK_HZ    = 20;
const TICK_MS    = 1000 / TICK_HZ;
const WORLD_SEED = 42;

// ── World setup ───────────────────────────────────────────────────────────────

/**
 * Two-entity world: one KNIGHT_INFANTRY per team.
 * Entity 1 starts at x=0; entity 2 starts at x=0.6 m (close-combat range).
 *
 * @type {import("@its-not-rocket-science/ananke").WorldState}
 */
const world = createWorld(WORLD_SEED, [
  {
    id:        1,
    teamId:    1,
    seed:      1001,
    archetype: "KNIGHT_INFANTRY",
    weaponId:  "wpn_longsword",
    armourId:  "arm_plate",
    x_m:       0.0,
    y_m:       0.0,
  },
  {
    id:        2,
    teamId:    2,
    seed:      2001,
    archetype: "HUMAN_BASE",
    weaponId:  "wpn_fists",
    x_m:       0.6,
    y_m:       0.0,
  },
]);

/** @type {import("@its-not-rocket-science/ananke").KernelContext} */
const ctx = { trace: null };

/** @type {Map<number, import("@its-not-rocket-science/ananke").AIPolicy>} */
const policyMap = new Map([
  [1, AI_PRESETS.aggressiveMelee],
  [2, AI_PRESETS.aggressiveMelee],
]);

// ── Snapshot state ────────────────────────────────────────────────────────────

/** @type {object[]} */
let latestSnapshots = [];

/**
 * Convert a fixed-point Vec3 to real metres.
 * SCALE.m = 1000; value 600 → 0.6 m.
 *
 * @param {import("@its-not-rocket-science/ananke").Vec3} pos_m
 * @returns {{ x: number, y: number, z: number }}
 */
function toRealMetres(pos_m) {
  return {
    x: pos_m.x / SCALE.m,
    y: pos_m.y / SCALE.m,
    z: pos_m.z / SCALE.m,
  };
}

/**
 * Serialise a RigSnapshot to the wire format matching AnankeSnapshot.cs.
 * Q values are sent as integers (0–18000); C# divides by SCALE_Q = 18000f.
 *
 * @param {import("@its-not-rocket-science/ananke").RigSnapshot} snap
 * @param {import("@its-not-rocket-science/ananke").Entity}      entity
 * @returns {object}
 */
function serialiseSnapshot(snap, entity) {
  return {
    entityId:    snap.entityId,
    teamId:      snap.teamId,
    tick:        snap.tick,
    position:    toRealMetres(entity.position_m),
    animation:   snap.animation,
    pose:        snap.pose,
    grapple:     snap.grapple,
    dead:        snap.animation.dead,
    unconscious: snap.animation.unconscious,
  };
}

// ── Simulation loop ───────────────────────────────────────────────────────────

function tick() {
  const anyAlive = world.entities.some(e => !e.injury.dead);
  if (!anyAlive) return;

  const index   = buildWorldIndex(world);
  const spatial = buildSpatialIndex(world);
  const cmds    = buildAICommands(world, index, spatial, id => policyMap.get(id));

  stepWorld(world, cmds, ctx);

  const rigs = extractRigSnapshots(world);
  latestSnapshots = rigs.map(snap => {
    const entity = world.entities.find(e => e.id === snap.entityId);
    return serialiseSnapshot(snap, entity);
  });
}

const intervalId = setInterval(tick, TICK_MS);

// ── HTTP server ───────────────────────────────────────────────────────────────

const server = http.createServer((req, res) => {
  res.setHeader("Access-Control-Allow-Origin", "*");
  res.setHeader("Content-Type", "application/json");

  if (req.method === "GET" && req.url === "/health") {
    res.writeHead(200);
    res.end(JSON.stringify({ ok: true, tick: world.tick }));
    return;
  }

  if (req.method === "GET" && req.url === "/state") {
    res.writeHead(200);
    res.end(JSON.stringify(latestSnapshots));
    return;
  }

  // TODO (M2 stretch): WebSocket upgrade for push-based delivery.
  // if (req.headers.upgrade?.toLowerCase() === "websocket") { ... }

  res.writeHead(404);
  res.end(JSON.stringify({ error: "Not found" }));
});

server.listen(PORT, "127.0.0.1", () => {
  console.log(`Ananke Unity sidecar running on http://127.0.0.1:${PORT}`);
  console.log(`  Simulation: ${TICK_HZ} Hz  seed=${WORLD_SEED}`);
  console.log(`  Entities:   ${world.entities.map(e => `#${e.id} team${e.teamId}`).join(", ")}`);
  console.log(`  GET /health   →  { ok: true }`);
  console.log(`  GET /state    →  entity snapshot array`);
});

// ── Graceful shutdown ─────────────────────────────────────────────────────────

process.on("SIGTERM", () => {
  console.log("SIGTERM received — shutting down sidecar.");
  clearInterval(intervalId);
  server.close(() => process.exit(0));
});

process.on("SIGINT", () => {
  console.log("SIGINT received — shutting down sidecar.");
  clearInterval(intervalId);
  server.close(() => process.exit(0));
});
