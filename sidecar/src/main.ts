import http, { type ServerResponse } from "node:http";
import {
  DEFAULT_HOST,
  DEFAULT_PORT,
  STREAM_PATH,
} from "./protocol.js";
import { createScenario } from "./scenario.js";
import {
  acceptWebSocket,
  decodeClientFrame,
  encodeWebSocketFrame,
  type ClientSocket,
} from "./websocket.js";

const PORT = Number.parseInt(process.env.PORT ?? `${DEFAULT_PORT}`, 10);
const HOST = process.env.HOST ?? DEFAULT_HOST;
const TICK_HZ = 20;
const TICK_MS = Math.round(1000 / TICK_HZ);

const scenario = createScenario();
const clients = new Set<ClientSocket>();
let intervalId: NodeJS.Timeout | null = null;
let shuttingDown = false;

function publishFrame(): void {
  const payload = JSON.stringify(scenario.getLatestFrame());
  for (const socket of clients) {
    if (socket.destroyed) {
      clients.delete(socket);
      continue;
    }

    try {
      socket.write(encodeWebSocketFrame(payload));
    } catch {
      clients.delete(socket);
      socket.destroy();
    }
  }
}

function tick(): void {
  scenario.tick();
  publishFrame();
}

function writeJson(res: ServerResponse, status: number, payload: unknown): void {
  res.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Access-Control-Allow-Origin": "*",
    "Cache-Control": "no-store",
  });
  res.end(JSON.stringify(payload));
}

const server = http.createServer((req, res) => {
  const url = req.url ?? "/";
  if (req.method === "GET" && url === "/health") {
    writeJson(res, 200, {
      ok: true,
      transport: { http: true, websocket: true, path: STREAM_PATH },
      tick: scenario.world.tick,
      entityCount: scenario.world.entities.length,
    });
    return;
  }

  if (req.method === "GET" && url === "/state") {
    writeJson(res, 200, scenario.getLatestFrame());
    return;
  }

  if (req.method === "GET" && url === "/") {
    writeJson(res, 200, {
      name: "ananke-engine-sidecar",
      streamUrl: `ws://${HOST}:${PORT}${STREAM_PATH}`,
      latestTick: scenario.getLatestFrame().tick,
    });
    return;
  }

  writeJson(res, 404, { error: "Not found" });
});

server.on("upgrade", (req, socket, head) => {
  if (req.url !== STREAM_PATH) {
    socket.write("HTTP/1.1 404 Not Found\r\n\r\n");
    socket.destroy();
    return;
  }

  if (head.length > 0) {
    socket.unshift(head);
  }

  const client = socket as ClientSocket;
  if (!acceptWebSocket(req, client)) {
    return;
  }

  clients.add(client);
  client.write(encodeWebSocketFrame(JSON.stringify(scenario.getLatestFrame())));

  client.on("data", (chunk: Buffer) => {
    try {
      const frame = decodeClientFrame(chunk);
      if (frame === null) {
        return;
      }

      if (frame.opcode === 0x8) {
        client.end(Buffer.from([0x88, 0x00]));
      } else if (frame.opcode === 0x9) {
        const pong = Buffer.concat([Buffer.from([0x8a, frame.payload.length]), frame.payload]);
        client.write(pong);
      }
    } catch {
      client.destroy();
    }
  });

  const cleanup = (): void => {
    clients.delete(client);
  };

  client.on("close", cleanup);
  client.on("end", cleanup);
  client.on("error", cleanup);
});

function shutdown(signal: NodeJS.Signals): void {
  if (shuttingDown) {
    return;
  }

  shuttingDown = true;
  if (intervalId !== null) {
    clearInterval(intervalId);
  }

  for (const socket of clients) {
    try {
      socket.end(Buffer.from([0x88, 0x00]));
    } catch {
      socket.destroy();
    }
  }

  server.close(() => {
    console.log(`${signal} received — sidecar shut down cleanly.`);
    process.exit(0);
  });
}

tick();
intervalId = setInterval(tick, TICK_MS);

server.listen(PORT, HOST, () => {
  console.log(`Ananke sidecar ready at http://${HOST}:${PORT}`);
  console.log(`  WebSocket stream: ws://${HOST}:${PORT}${STREAM_PATH}`);
  console.log(`  Simulation tick rate: ${TICK_HZ} Hz`);
  console.log(`  Entities: ${scenario.world.entities.map((entity) => `#${entity.id}/team${entity.teamId}`).join(", ")}`);
});

process.on("SIGINT", () => shutdown("SIGINT"));
process.on("SIGTERM", () => shutdown("SIGTERM"));
import http from "node:http";
import { stepWorld } from "@its-not-rocket-science/ananke";
import { buildWorldIndex } from "../node_modules/@its-not-rocket-science/ananke/dist/src/sim/indexing.js";
import { buildSpatialIndex } from "../node_modules/@its-not-rocket-science/ananke/dist/src/sim/spatial.js";
import { buildAICommands } from "../node_modules/@its-not-rocket-science/ananke/dist/src/sim/ai/system.js";
import { createDefaultScenario } from "./scenario.js";
import { serialiseFrame, type SidecarFrame } from "./serialiser.js";

const HOST = process.env.ANANKE_HOST ?? "127.0.0.1";
const PORT = Number.parseInt(process.env.ANANKE_PORT ?? "7374", 10);
const scenario = createDefaultScenario();
const tickMs = Math.round(1000 / scenario.tickHz);

let latestFrame: SidecarFrame = serialiseFrame(scenario.world, "knight-vs-brawler", scenario.tickHz);
let intervalId: NodeJS.Timeout | undefined;

function advanceSimulation(): void {
  const anyAlive = scenario.world.entities.some((entity) => !entity.injury.dead);
  if (!anyAlive) {
    return;
  }

  const worldIndex = buildWorldIndex(scenario.world);
  const spatialIndex = buildSpatialIndex(scenario.world, scenario.ctx.cellSize_m ?? 1000);
  const commands = buildAICommands(
    scenario.world,
    worldIndex,
    spatialIndex,
    (entityId: number) => scenario.policyMap.get(entityId),
  );

  stepWorld(scenario.world, commands, scenario.ctx);
  latestFrame = serialiseFrame(scenario.world, "knight-vs-brawler", scenario.tickHz);
}

function writeJson(res: http.ServerResponse, statusCode: number, payload: unknown): void {
  res.writeHead(statusCode, { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" });
  res.end(JSON.stringify(payload));
}

export function createSidecarServer(): http.Server {
  return http.createServer((req, res) => {
    if (!req.url) {
      writeJson(res, 400, { error: "Missing URL." });
      return;
    }

    if (req.method === "GET" && req.url === "/health") {
      writeJson(res, 200, {
        ok: true,
        tickHz: scenario.tickHz,
        worldTick: scenario.world.tick,
        scenarioId: latestFrame.scenarioId,
      });
      return;
    }

    if (req.method === "GET" && req.url === "/frame") {
      writeJson(res, 200, latestFrame);
      return;
    }

    if (req.method === "GET" && req.url === "/state") {
      writeJson(res, 200, latestFrame.frames);
      return;
    }

    writeJson(res, 404, { error: "Not found." });
  });
}

export function startSidecar(): { server: http.Server; stop: () => Promise<void> } {
  const server = createSidecarServer();

  intervalId = setInterval(advanceSimulation, tickMs);

  server.listen(PORT, HOST, () => {
    console.log(`Ananke sidecar ready at http://${HOST}:${PORT}`);
    console.log(`Simulation tick rate: ${scenario.tickHz} Hz (world seed ${scenario.worldSeed})`);
    console.log(`Serving GET /health, GET /frame, and GET /state`);
  });

  const stop = () =>
    new Promise<void>((resolve, reject) => {
      if (intervalId) {
        clearInterval(intervalId);
        intervalId = undefined;
      }

      server.close((error) => {
        if (error) {
          reject(error);
          return;
        }

        resolve();
      });
    });

  return { server, stop };
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const { stop } = startSidecar();

  const shutdown = async (signal: NodeJS.Signals) => {
    console.log(`${signal} received, shutting down sidecar.`);
    await stop();
    process.exit(0);
  };

  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);
}
