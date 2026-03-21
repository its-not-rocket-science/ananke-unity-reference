import { createHash } from "node:crypto";
import type { IncomingMessage } from "node:http";
import { Socket } from "node:net";

const WS_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

export type ClientSocket = Socket;

export function encodeWebSocketFrame(payload: string): Buffer {
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

export function decodeClientFrame(buffer: Buffer): { opcode: number; payload: Buffer } | null {
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

export function acceptWebSocket(req: IncomingMessage, socket: ClientSocket): boolean {
  const key = req.headers["sec-websocket-key"];
  if (typeof key !== "string") {
    socket.write("HTTP/1.1 400 Bad Request\r\n\r\n");
    socket.destroy();
    return false;
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
  return true;
}
