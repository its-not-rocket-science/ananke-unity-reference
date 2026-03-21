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
