import http, { type IncomingMessage, type ServerResponse } from "node:http";
import { createHash } from "node:crypto";
import { Socket } from "node:net";
import {
  SCALE,
  createWorld,
  extractRigSnapshots,
  q,
  stepWorld,
} from "@its-not-rocket-science/ananke";
import {
  DEFAULT_HOST,
  DEFAULT_PORT,
  STREAM_PATH,
  type AnankeEntitySnapshot,
  type AnankeFrameEnvelope,
  type AnankePosition,
} from "./protocol.js";

type Command = { kind: "attack"; targetId: number; weaponSlot: "mainHand" };
type CommandMap = Map<number, Command[]>;

interface SimVec3 { x: number; y: number; z: number; }
interface SimEntity {
  id: number;
  teamId: number;
  position_m: SimVec3;
  injury: { dead: boolean };
}
interface SimPoseModifier {
  segmentId: string;
  impairmentQ: number;
  structuralQ: number;
  surfaceQ: number;
}
interface SimRigSnapshot {
  entityId: number;
  teamId: number;
  tick: number;
  animation: AnankeEntitySnapshot["animation"];
  pose: SimPoseModifier[];
  grapple: AnankeEntitySnapshot["grapple"];
}

type ClientSocket = Socket & { __anankeAlive?: boolean };

const PORT = Number.parseInt(process.env.PORT ?? `${DEFAULT_PORT}`, 10);
const HOST = process.env.HOST ?? DEFAULT_HOST;
const TICK_HZ = 20;
const TICK_MS = Math.round(1000 / TICK_HZ);
const WORLD_SEED = 42;
const WS_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

const world = createWorld(WORLD_SEED, [
  {
    id: 1,
    teamId: 1,
    seed: 1001,
    archetype: "KNIGHT_INFANTRY",
    weaponId: "wpn_longsword",
    armourId: "arm_plate",
    x_m: 0,
    y_m: 0,
  },
  {
    id: 2,
    teamId: 2,
    seed: 2001,
    archetype: "HUMAN_BASE",
    weaponId: "wpn_boxing_gloves",
    x_m: 0.6,
    y_m: 0,
  },
]);

const ctx = {
  tractionCoeff: q(0.8),
};

const clients = new Set<ClientSocket>();
let latestSnapshots: AnankeEntitySnapshot[] = [];
let latestFrame: AnankeFrameEnvelope = createFrameEnvelope([]);
let intervalId: NodeJS.Timeout | null = null;
let shuttingDown = false;

function toRealMetres(position: SimVec3): AnankePosition {
  return {
    x: position.x / SCALE.m,
    y: position.y / SCALE.m,
    z: position.z / SCALE.m,
  };
}

function serialiseSnapshot(snapshot: SimRigSnapshot, entity: SimEntity): AnankeEntitySnapshot {
  return {
    entityId: snapshot.entityId,
    teamId: snapshot.teamId,
    tick: snapshot.tick,
    position: toRealMetres(entity.position_m),
    animation: snapshot.animation,
    pose: snapshot.pose.map((modifier) => ({
      segmentId: modifier.segmentId,
      impairmentQ: modifier.impairmentQ,
      structuralQ: modifier.structuralQ,
      surfaceQ: modifier.surfaceQ,
    })),
    grapple: snapshot.grapple,
    dead: snapshot.animation.dead,
    unconscious: snapshot.animation.unconscious,
  };
}

function createFrameEnvelope(snapshots: AnankeEntitySnapshot[]): AnankeFrameEnvelope {
  return {
    type: "snapshot_frame",
    tick: snapshots[0]?.tick ?? world.tick,
    entityCount: snapshots.length,
    generatedAtIso: new Date().toISOString(),
    snapshots,
  };
}

function buildCommands(): CommandMap {
  return new Map<number, Command[]>([
    [1, [{ kind: "attack", targetId: 2, weaponSlot: "mainHand" }]],
    [2, [{ kind: "attack", targetId: 1, weaponSlot: "mainHand" }]],
  ]);
}

function publishFrame(): void {
  const payload = JSON.stringify(latestFrame);
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
  if ((world.entities as SimEntity[]).every((entity) => entity.injury.dead)) {
    return;
  }

  stepWorld(world, buildCommands(), ctx);

  latestSnapshots = (extractRigSnapshots(world) as SimRigSnapshot[]).map((snapshot) => {
    const entity = (world.entities as SimEntity[]).find((candidate) => candidate.id === snapshot.entityId);
    if (entity === undefined) {
      throw new Error(`Unable to locate entity ${snapshot.entityId} in world state.`);
    }

    return serialiseSnapshot(snapshot, entity);
  });

  latestFrame = createFrameEnvelope(latestSnapshots);
  publishFrame();
}

function encodeWebSocketFrame(payload: string): Buffer {
  const message = Buffer.from(payload, "utf8");
  const length = message.length;

  if (length < 126) {
    return Buffer.concat([Buffer.from([0x81, length]), message]);
  }

  if (length < 65536) {
    const header = Buffer.alloc(4);
    header[0] = 0x81;
    header[1] = 126;
    header.writeUInt16BE(length, 2);
    return Buffer.concat([header, message]);
  }

  const header = Buffer.alloc(10);
  header[0] = 0x81;
  header[1] = 127;
  header.writeBigUInt64BE(BigInt(length), 2);
  return Buffer.concat([header, message]);
}

function decodeClientFrame(buffer: Buffer): { opcode: number; payload: Buffer } | null {
  if (buffer.length < 2) {
    return null;
  }

  const opcode = buffer[0] & 0x0f;
  const masked = (buffer[1] & 0x80) !== 0;
  let offset = 2;
  let payloadLength = buffer[1] & 0x7f;

  if (payloadLength === 126) {
    if (buffer.length < 4) return null;
    payloadLength = buffer.readUInt16BE(2);
    offset = 4;
  } else if (payloadLength === 127) {
    if (buffer.length < 10) return null;
    const bigLength = buffer.readBigUInt64BE(2);
    if (bigLength > BigInt(Number.MAX_SAFE_INTEGER)) {
      throw new Error("Received oversized WebSocket frame.");
    }
    payloadLength = Number(bigLength);
    offset = 10;
  }

  const maskLength = masked ? 4 : 0;
  if (buffer.length < offset + maskLength + payloadLength) {
    return null;
  }

  const payload = buffer.subarray(offset + maskLength, offset + maskLength + payloadLength);
  if (!masked) {
    return { opcode, payload };
  }

  const mask = buffer.subarray(offset, offset + 4);
  const unmasked = Buffer.alloc(payloadLength);
  for (let index = 0; index < payloadLength; index += 1) {
    unmasked[index] = payload[index] ^ mask[index % 4]!;
  }

  return { opcode, payload: unmasked };
}

function acceptWebSocket(req: IncomingMessage, socket: ClientSocket): void {
  const key = req.headers["sec-websocket-key"];
  if (typeof key !== "string") {
    socket.write("HTTP/1.1 400 Bad Request\r\n\r\n");
    socket.destroy();
    return;
  }

  const accept = createHash("sha1").update(`${key}${WS_GUID}`).digest("base64");
  const headers = [
    "HTTP/1.1 101 Switching Protocols",
    "Upgrade: websocket",
    "Connection: Upgrade",
    `Sec-WebSocket-Accept: ${accept}`,
    "\r\n",
  ];

  socket.write(headers.join("\r\n"));
  socket.__anankeAlive = true;
  clients.add(socket);
  socket.write(encodeWebSocketFrame(JSON.stringify(latestFrame)));

  socket.on("data", (chunk: Buffer) => {
    try {
      const frame = decodeClientFrame(chunk);
      if (frame === null) {
        return;
      }

      if (frame.opcode === 0x8) {
        socket.end(Buffer.from([0x88, 0x00]));
      } else if (frame.opcode === 0x9) {
        const pong = Buffer.concat([Buffer.from([0x8a, frame.payload.length]), frame.payload]);
        socket.write(pong);
      }
    } catch {
      socket.destroy();
    }
  });

  const cleanup = (): void => {
    clients.delete(socket);
  };

  socket.on("close", cleanup);
  socket.on("end", cleanup);
  socket.on("error", cleanup);
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
      tick: world.tick,
      entityCount: world.entities.length,
    });
    return;
  }

  if (req.method === "GET" && url === "/state") {
    writeJson(res, 200, latestFrame);
    return;
  }

  if (req.method === "GET" && url === "/") {
    writeJson(res, 200, {
      name: "ananke-engine-sidecar",
      streamUrl: `ws://${HOST}:${PORT}${STREAM_PATH}`,
      latestTick: latestFrame.tick,
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

  acceptWebSocket(req, socket as ClientSocket);
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
  console.log(`  Entities: ${(world.entities as SimEntity[]).map((entity) => `#${entity.id}/team${entity.teamId}`).join(", ")}`);
});

process.on("SIGINT", () => shutdown("SIGINT"));
process.on("SIGTERM", () => shutdown("SIGTERM"));
