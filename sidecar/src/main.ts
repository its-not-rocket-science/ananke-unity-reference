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
    });
    return;
  }

  if (req.method === "GET" && url === "/state") {
    writeJson(res, 200, scenario.getLatestFrame());
    return;
  }

  if (req.method === "GET" && url === "/") {
    writeJson(res, 200, {
      name: "ananke-unity-sidecar",
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
});

process.on("SIGINT", () => shutdown("SIGINT"));
process.on("SIGTERM", () => shutdown("SIGTERM"));
