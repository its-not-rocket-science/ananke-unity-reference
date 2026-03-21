import {
  SCALE,
  createWorld,
  extractRigSnapshots,
  q,
  stepWorld,
} from "@its-not-rocket-science/ananke";
import type {
  AnankeEntitySnapshot,
  AnankeFrameEnvelope,
  AnankePosition,
} from "./protocol.js";

type Command = { kind: "attack"; targetId: number; weaponSlot: "mainHand" };
export type CommandMap = Map<number, Command[]>;

export interface SimVec3 {
  x: number;
  y: number;
  z: number;
}

export interface SimEntity {
  id: number;
  teamId: number;
  position_m: SimVec3;
  injury: { dead: boolean };
}

export interface SimPoseModifier {
  segmentId: string;
  impairmentQ: number;
  structuralQ: number;
  surfaceQ: number;
}

export interface SimRigSnapshot {
  entityId: number;
  teamId: number;
  tick: number;
  animation: AnankeEntitySnapshot["animation"];
  pose: SimPoseModifier[];
  grapple: AnankeEntitySnapshot["grapple"];
}

export interface ScenarioState {
  world: { tick: number; entities: SimEntity[] };
  tick: () => AnankeFrameEnvelope;
  getLatestFrame: () => AnankeFrameEnvelope;
}

const WORLD_SEED = 42;

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

function createFrameEnvelope(worldTick: number, snapshots: AnankeEntitySnapshot[]): AnankeFrameEnvelope {
  return {
    type: "snapshot_frame",
    tick: snapshots[0]?.tick ?? worldTick,
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

export function createScenario(): ScenarioState {
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

  const typedWorld = world as unknown as { tick: number; entities: SimEntity[] };

  const ctx = {
    tractionCoeff: q(0.8),
  };

  let latestFrame = createFrameEnvelope(typedWorld.tick, []);

  const updateFrame = (): AnankeFrameEnvelope => {
    const snapshots = (extractRigSnapshots(world) as SimRigSnapshot[]).map((snapshot) => {
      const entity = typedWorld.entities.find((candidate) => candidate.id === snapshot.entityId);
      if (entity === undefined) {
        throw new Error(`Unable to locate entity ${snapshot.entityId} in world state.`);
      }

      return serialiseSnapshot(snapshot, entity);
    });

    latestFrame = createFrameEnvelope(typedWorld.tick, snapshots);
    return latestFrame;
  };

  const tick = (): AnankeFrameEnvelope => {
    if (typedWorld.entities.every((entity) => entity.injury.dead)) {
      return latestFrame;
    }

    stepWorld(world, buildCommands(), ctx);
    return updateFrame();
  };

  latestFrame = updateFrame();

  return {
    world: typedWorld,
    tick,
    getLatestFrame: () => latestFrame,
  };
}
