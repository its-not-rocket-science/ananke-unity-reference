import { createWorld, q, type EntitySpec, type WorldState } from "@its-not-rocket-science/ananke";
import { AI_PRESETS } from "../node_modules/@its-not-rocket-science/ananke/dist/src/sim/ai/presets.js";
import type { AIPolicy } from "../node_modules/@its-not-rocket-science/ananke/dist/src/sim/ai/types.js";
import type { KernelContext } from "../node_modules/@its-not-rocket-science/ananke/dist/src/sim/context.js";

export interface SidecarScenario {
  world: WorldState;
  ctx: KernelContext;
  tickHz: number;
  worldSeed: number;
  policyMap: Map<number, AIPolicy>;
}

export const DEFAULT_TICK_HZ = 20;
export const DEFAULT_WORLD_SEED = 42;

const DEFAULT_ENTITIES: EntitySpec[] = [
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
    weaponId: "wpn_fists",
    x_m: 0.6,
    y_m: 0,
  },
];

export function createDefaultScenario(): SidecarScenario {
  const world = createWorld(DEFAULT_WORLD_SEED, DEFAULT_ENTITIES);

  const policyMap = new Map<number, AIPolicy>([
    [1, AI_PRESETS.aggressiveMelee],
    [2, AI_PRESETS.aggressiveMelee],
  ]);

  return {
    world,
    ctx: { tractionCoeff: q(1) },
    tickHz: DEFAULT_TICK_HZ,
    worldSeed: DEFAULT_WORLD_SEED,
    policyMap,
  };
}
