export const SCALE_Q = 10000;
export const DEFAULT_PORT = 3001;
export const DEFAULT_HOST = "127.0.0.1";
export const STREAM_PATH = "/stream";

export type GrapplePosition = "standing" | "prone" | "pinned" | "mounted";

export interface AnankePosition {
  x: number;
  y: number;
  z: number;
}

export interface AnankeAnimationHints {
  idle: number;
  walk: number;
  run: number;
  sprint: number;
  crawl: number;
  guardingQ: number;
  attackingQ: number;
  shockQ: number;
  fearQ: number;
  prone: boolean;
  unconscious: boolean;
  dead: boolean;
}

export interface AnankePoseModifier {
  segmentId: string;
  impairmentQ: number;
  structuralQ: number;
  surfaceQ: number;
}

export interface AnankeGrappleConstraint {
  isHolder: boolean;
  holdingEntityId?: number;
  isHeld: boolean;
  heldByIds: number[];
  position: GrapplePosition;
  gripQ: number;
}

export interface AnankeEntitySnapshot {
  entityId: number;
  teamId: number;
  tick: number;
  position: AnankePosition;
  animation: AnankeAnimationHints;
  pose: AnankePoseModifier[];
  grapple: AnankeGrappleConstraint;
  dead: boolean;
  unconscious: boolean;
}

export interface AnankeFrameEnvelope {
  type: "snapshot_frame";
  tick: number;
  entityCount: number;
  generatedAtIso: string;
  snapshots: AnankeEntitySnapshot[];
}
