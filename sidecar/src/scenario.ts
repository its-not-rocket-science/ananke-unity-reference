import {
  createWorld,
  extractRigSnapshots,
  q,
  SCALE,
  stepWorld,
  type CommandMap,
  type WorldState,
} from "@its-not-rocket-science/ananke";
import type {
  AnankeAnimationHints,
  AnankeEntitySnapshot,
  AnankeFrameEnvelope,
  AnankeGrappleConstraint,
  AnankePosition,
  AnankePoseModifier,
} from "./protocol.js";

export interface ScenarioState {
  world: WorldState;
  tick: () => AnankeFrameEnvelope;
  getLatestFrame: () => AnankeFrameEnvelope;
}

const WORLD_SEED = 42;
const TICK_HZ = 20;

function toMetres(vec: { x: number; y: number; z: number }): AnankePosition {
  return { x: vec.x / SCALE.m, y: vec.y / SCALE.m, z: vec.z / SCALE.m };
}

function serialiseSnapshot(snapshot: {
  entityId: number;
  teamId: number;
  tick: number;
  animation: AnankeAnimationHints;
  pose: AnankePoseModifier[];
  grapple: AnankeGrappleConstraint | null;
}, entity: { position_m: AnankePosition; velocity_mps: AnankePosition; injury: { shock: number; consciousness: number; fluidLoss: number; dead: boolean }; energy: { fatigue: number }; condition: { fearQ?: number } }): AnankeEntitySnapshot {
  return {
    entityId: snapshot.entityId,
    teamId: snapshot.teamId,
    tick: snapshot.tick,
    position: toMetres(entity.position_m as { x: number; y: number; z: number }),
    velocity: toMetres((entity.velocity_mps ?? { x: 0, y: 0, z: 0 }) as { x: number; y: number; z: number }),
    animation: snapshot.animation,
    pose: snapshot.pose,
    grapple: snapshot.grapple as AnankeGrappleConstraint,
    condition: {
      shockQ: entity.injury.shock,
      fearQ: entity.condition.fearQ ?? 0,
      consciousnessQ: entity.injury.consciousness,
      fluidLossQ: entity.injury.fluidLoss,
      fatigueQ: entity.energy.fatigue,
      dead: entity.injury.dead,
    },
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

function buildCommands(world: WorldState): CommandMap {
  const commands: CommandMap = new Map();
  const living = world.entities.filter((entity) => !entity.injury.dead);

  for (const entity of living) {
    const hasTarget = living.some((candidate) => candidate.teamId !== entity.teamId);
    if (!hasTarget) continue;

    commands.set(entity.id, [
      { kind: "attackNearest", mode: "strike", intensity: q(1.0) },
      { kind: "defend", mode: entity.id === 1 ? "parry" : "dodge", intensity: q(0.55) },
    ]);
  }

  return commands;
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
      x_m: -0.45,
      y_m: 0,
    },
    {
      id: 2,
      teamId: 2,
      seed: 2001,
      archetype: "HUMAN_BASE",
      weaponId: "wpn_boxing_gloves",
      x_m: 0.45,
      y_m: 0,
    },
  ]);

  const ctx = { tractionCoeff: q(0.8) };

  function updateFrame(): AnankeFrameEnvelope {
    const rigs = extractRigSnapshots(world) as Array<{
      entityId: number;
      teamId: number;
      tick: number;
      animation: AnankeAnimationHints;
      pose: AnankePoseModifier[];
      grapple: AnankeGrappleConstraint | null;
    }>;

    const snapshots = rigs.map((snapshot) => {
      const entity = (world.entities as unknown[]).find(
        (candidate) => (candidate as { id: number }).id === snapshot.entityId,
      ) as {
        position_m: AnankePosition;
        velocity_mps: AnankePosition;
        injury: { shock: number; consciousness: number; fluidLoss: number; dead: boolean };
        energy: { fatigue: number };
        condition: { fearQ?: number };
      } | undefined;

      if (!entity) {
        throw new Error(`Entity ${snapshot.entityId} not found in world state.`);
      }

      return serialiseSnapshot(snapshot, entity);
    });

    return createFrameEnvelope(world.tick, snapshots);
  }

  let latestFrame = updateFrame();

  function tick(): AnankeFrameEnvelope {
    if ((world.entities as Array<{ injury: { dead: boolean } }>).every((e) => e.injury.dead)) {
      return latestFrame;
    }
    stepWorld(world, buildCommands(world), ctx);
    latestFrame = updateFrame();
    return latestFrame;
  }

  return {
    world,
    tick,
    getLatestFrame: () => latestFrame,
  };
}

export { TICK_HZ };
