import {
  SCALE,
  extractRigSnapshots,
  type Entity,
  type PoseModifier,
  type RigSnapshot,
  type WorldState,
} from "@its-not-rocket-science/ananke";

export interface SidecarPosition {
  x: number;
  y: number;
  z: number;
}

export interface SidecarCondition {
  shockQ: number;
  fearQ: number;
  consciousnessQ: number;
  fluidLossQ: number;
  fatigueQ: number;
  dead: boolean;
}

export interface SidecarEntitySnapshot {
  entityId: number;
  teamId: number;
  tick: number;
  position: SidecarPosition;
  velocity: SidecarPosition;
  animation: RigSnapshot["animation"];
  pose: PoseModifier[];
  grapple: RigSnapshot["grapple"];
  condition: SidecarCondition;
  dead: boolean;
  unconscious: boolean;
}

export interface SidecarFrame {
  scenarioId: string;
  tickHz: number;
  worldTick: number;
  generatedAt: string;
  frames: SidecarEntitySnapshot[];
}

function toRealMetres(vec: { x: number; y: number; z: number }): SidecarPosition {
  return {
    x: vec.x / SCALE.m,
    y: vec.y / SCALE.m,
    z: vec.z / SCALE.m,
  };
}

function serialiseCondition(entity: Entity): SidecarCondition {
  return {
    shockQ: entity.injury.shock,
    fearQ: entity.condition.fearQ ?? 0,
    consciousnessQ: entity.injury.consciousness,
    fluidLossQ: entity.injury.fluidLoss,
    fatigueQ: entity.energy.fatigue,
    dead: entity.injury.dead,
  };
}

function serialiseSnapshot(rigSnapshot: RigSnapshot, entity: Entity): SidecarEntitySnapshot {
  return {
    entityId: rigSnapshot.entityId,
    teamId: rigSnapshot.teamId,
    tick: rigSnapshot.tick,
    position: toRealMetres(entity.position_m),
    velocity: toRealMetres(entity.velocity_mps),
    animation: rigSnapshot.animation,
    pose: rigSnapshot.pose,
    grapple: rigSnapshot.grapple,
    condition: serialiseCondition(entity),
    dead: rigSnapshot.animation.dead,
    unconscious: rigSnapshot.animation.unconscious,
  };
}

export function serialiseFrame(world: WorldState, scenarioId: string, tickHz: number): SidecarFrame {
  const rigs = extractRigSnapshots(world);

  return {
    scenarioId,
    tickHz,
    worldTick: world.tick,
    generatedAt: new Date().toISOString(),
    frames: rigs.map((snapshot) => {
      const entity = world.entities.find((candidate) => candidate.id === snapshot.entityId);
      if (!entity) {
        throw new Error(`Missing entity ${snapshot.entityId} while serialising frame.`);
      }

      return serialiseSnapshot(snapshot, entity);
    }),
  };
}
