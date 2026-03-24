# ananke-unity-reference

![Ananke version](https://img.shields.io/badge/ananke-0.1.14-6366f1)
![Unity](https://img.shields.io/badge/Unity-6%2B-000000?logo=unity&logoColor=white)
![Node.js](https://img.shields.io/badge/Node.js-18%2B-339933?logo=node.js&logoColor=white)
![TypeScript](https://img.shields.io/badge/TypeScript-5.x-3178c6?logo=typescript&logoColor=white)
![C#](https://img.shields.io/badge/C%23-11-239120?logo=csharp&logoColor=white)
![Status](https://img.shields.io/badge/status-reference%20implementation-orange)

Minimal runnable Unity 6 plugin that drives a humanoid character rig from Ananke's physics simulation. This is the canonical reference implementation for Unity integrators. Once complete, it will be listed in [Ananke's ecosystem.md](https://github.com/its-not-rocket-science/ananke/blob/master/docs/ecosystem.md).

---

## Table of contents

1. [Purpose](#purpose)
2. [Prerequisites](#prerequisites)
3. [Architecture](#architecture)
4. [What gets built](#what-gets-built)
5. [Quick start](#quick-start)
6. [File layout](#file-layout)
7. [Ananke API surface used](#ananke-api-surface-used)
8. [Tick interpolation strategy](#tick-interpolation-strategy)
9. [Demo scene](#demo-scene)
10. [API compliance checklist](#api-compliance-checklist)
11. [Contributing](#contributing)

---

## Purpose

Ananke is a headless simulation kernel that outputs structured data at 20 Hz: entity positions, injury state, animation hints, and grapple constraints. This project wires that data to a Unity 6 humanoid skeleton.

It is deliberately minimal: one demo scene, one C# receiver, one TypeScript sidecar. No custom physics override, no editor extension, no asset pipeline. The goal is a working Knight vs Brawler duel that any Unity developer can clone and play within five minutes.

---

## Prerequisites

| Dependency | Minimum version | Notes |
|-----------|----------------|-------|
| Unity | 6 (6000.0 LTS) | Universal Render Pipeline recommended |
| Ananke | 0.1.0 | Cloned alongside this repo |
| Node.js | 18 | For the TypeScript sidecar |
| npm | 9 | Bundled with Node.js 18 |
| .NET | 8 | Comes with Unity 6 |

Clone Ananke into a sibling directory before cloning this project:

```
workspace/
  ananke/                      ← https://github.com/its-not-rocket-science/ananke
  ananke-unity-reference/      ← this repo
```

The sidecar imports from `../ananke/dist/src/...` until Ananke is published to npm.

---

## Architecture

The integration now uses a **TypeScript sidecar ↔ engine WebSocket** channel. The sidecar owns the simulation and publishes the latest rig frame over `ws://127.0.0.1:3001/stream`, while Unity and Godot each consume the same frame envelope with engine-specific receiver scripts.

```
┌──────────────────────────────────────────────────────────┐
│  TypeScript sidecar (Node.js, 20 Hz)                     │
│                                                          │
│  stepWorld() ──► extractRigSnapshots()                   │
│               ──► serialise snapshot_frame envelope      │
│               ──► GET /health + GET /state               │
│               ──► WS /stream push                        │
└─────────────────────────────────┬────────────────────────┘
                                  │
                  ┌───────────────┴───────────────┐
                  │                               │
┌─────────────────▼────────────────┐ ┌────────────▼─────────────┐
│ Unity 6                           │ │ Godot 4                  │
│ AnankeReceiver.cs                 │ │ ananke_websocket_client  │
│ AnankeController.cs               │ │ ananke_demo_scene.gd     │
│ SkeletonMapper.cs                 │ │ ananke_skeleton_mapper   │
└──────────────────────────────────┘ └──────────────────────────┘
```

### Why WebSocket?

Both reference engines now use a shared push stream. The TypeScript sidecar still exposes `GET /health` and `GET /state` for inspection, but the primary transport is WebSocket so Unity and Godot can react to each simulation tick without polling overhead.

---

## What gets built

### Skeleton bone mapping

Both engines ship a placeholder segment mapper keyed by the canonical Ananke segment IDs (`head`, `torso`, `leftArm`, `rightArm`, `leftLeg`, `rightLeg`, `pelvis`, `neck`). Unity resolves those IDs to placeholder child transforms and Godot resolves them to `Node3D` names in the demo scene.


`SkeletonMapper.cs` maps Ananke's segment IDs to Unity's `HumanBodyBones` enum:

| Ananke segment | Unity HumanBodyBones |
|---------------|---------------------|
| `head`        | `HumanBodyBones.Head` |
| `torso`       | `HumanBodyBones.Spine` |
| `leftArm`     | `HumanBodyBones.LeftUpperArm` |
| `rightArm`    | `HumanBodyBones.RightUpperArm` |
| `leftLeg`     | `HumanBodyBones.LeftUpperLeg` |
| `rightLeg`    | `HumanBodyBones.RightUpperLeg` |

```csharp
// SkeletonMapper.cs
public static HumanBodyBones Resolve(string segmentId) => segmentId switch {
    "head"     => HumanBodyBones.Head,
    "torso"    => HumanBodyBones.Spine,
    "leftArm"  => HumanBodyBones.LeftUpperArm,
    "rightArm" => HumanBodyBones.RightUpperArm,
    "leftLeg"  => HumanBodyBones.LeftUpperLeg,
    "rightLeg" => HumanBodyBones.RightUpperLeg,
    _          => HumanBodyBones.LastBone, // unmapped
};
```

Override this mapping in the `AnankeSkeletonConfig` ScriptableObject if your rig uses different bone names.

### Animator state machine

`AnimationDriver.cs` reads `primaryState` from `AnimationHints` and sets an `Animator` parameter. Wire this parameter to a state machine in your `AnimatorController`:

```csharp
// AnimationDriver.cs
void ApplyHints(AnankeAnimationHints hints) {
    _animator.SetTrigger(hints.primaryState); // "idle", "attack", "flee", "prone", etc.
    _animator.SetFloat("injuryBlend", hints.injuryWeight);
    _animator.SetBool("isConscious",  hints.consciousness > 0.1f);
}
```

### Physics Rigidbody (optional)

Ananke computes world-space positions. Unity's `Rigidbody` is not needed for rendering — simply set `transform.position` from the interpolated snapshot. If your game needs Unity physics collision (e.g., for player camera blocking), attach a kinematic `Rigidbody` and use `MovePosition`:

```csharp
_rigidbody.MovePosition(interpolatedPosition);
```

Do not give the Rigidbody `isKinematic = false` — Ananke owns all simulation physics.

---

## Quick start

```bash
# 1. Install sidecar dependencies
cd sidecar && npm install

# 2. Start the TypeScript sidecar
npm start
# Prints the HTTP health endpoint and the WebSocket stream URL

# 3. Verify the bridge contract locally
npm run test:bridge

# 4. Open Unity or Godot
# Unity: add AnankeReceiver + AnankeController to an empty GameObject.
# Godot: open godot/project.godot and run the default scene.
```

Both demo integrations create two placeholder rigs and consume the same `snapshot_frame` envelope from the sidecar.
The demo scene opens a viewport with two characters. The sidecar runs the Knight vs Brawler scenario and serves `GET /frame` envelopes for Unity plus a legacy `GET /state` array for simple polling clients.

---

## File layout

```
ananke-unity-reference/
├── sidecar/                        TypeScript sidecar (Node.js)
│   ├── src/
│   │   ├── main.ts                 Simulation loop + HTTP/WebSocket transport
│   │   └── protocol.ts             Shared wire-frame shape
│   ├── scripts/
│   │   └── verify-bridge.mjs       Local stream verification
│   │   ├── main.ts                 Entry: sim loop + HTTP server
│   │   ├── scenario.ts             Knight vs Brawler setup
│   │   └── serialiser.ts           Frame → JSON for Unity
│   ├── package.json
│   └── tsconfig.json
│
├── Assets/Ananke/Scripts/          Unity 6 runtime scripts
│   ├── AnankeReceiver.cs           ClientWebSocket receiver
│   ├── AnankeController.cs         Placeholder rig driver
│   ├── AnankeSnapshot.cs           Shared frame DTOs
│   └── SkeletonMapper.cs           Segment → placeholder bone map
│
├── godot/                          Godot 4 reference client
│   ├── project.godot
│   ├── scenes/
│   │   └── AnankeDemo.tscn         Placeholder duel scene
│   └── scripts/
│       ├── ananke_demo_scene.gd
│       ├── ananke_skeleton_mapper.gd
│       └── ananke_websocket_client.gd
│
└── README.md
```

---

## Ananke API surface used

All imports are from Ananke's **Tier 1 (Stable)** surface as documented in
[`docs/bridge-contract.md`](https://github.com/its-not-rocket-science/ananke/blob/master/docs/bridge-contract.md)
and [`STABLE_API.md`](https://github.com/its-not-rocket-science/ananke/blob/master/STABLE_API.md).

| Ananke export | Used in | Tier |
|--------------|---------|------|
| `stepWorld(world, cmds, ctx)` | `sidecar/src/main.ts` | Tier 1 |
| `generateIndividual(seed, archetype)` | `sidecar/src/scenario.ts` | Tier 1 |
| `extractRigSnapshots(world)` | `sidecar/src/main.ts` | Tier 1 |
| `deriveAnimationHints(entity)` | `sidecar/src/serialiser.ts` | Tier 1 |
| `derivePoseModifiers(entity)` | `sidecar/src/serialiser.ts` | Tier 1 |
| `deriveGrappleConstraint(entity, world)` | `sidecar/src/serialiser.ts` | Tier 1 |
| `serializeReplay(replay)` | `sidecar/src/replay.ts` | Tier 1 |
| `SCALE` | `sidecar/src/serialiser.ts` | Tier 1 |

The complete field-by-field contract for `AnimationHints`, `GrapplePoseConstraint`, and
`InterpolatedState` is documented in
[`docs/bridge-contract.md`](https://github.com/its-not-rocket-science/ananke/blob/master/docs/bridge-contract.md).

---

## Tick interpolation strategy

Unity's `FixedUpdate` runs at 50 Hz (0.02 s) by default, which aligns conveniently with Ananke's 20 Hz (every 2.5 FixedUpdate calls). The interpolator stores the two most recent simulation frames and computes a blend factor `t` in `Update`:

```csharp
// AnankeInterpolator.cs
void Update() {
    float elapsed = Time.time - _prevFrameTime;
    float interval = _currFrameTime - _prevFrameTime;
    _t = interval > 0f ? Mathf.Clamp01(elapsed / interval) : 1f;
}

Vector3 GetPosition(string segmentId) {
    var prev = _prevFrame.bones[segmentId].position;
    var curr = _currFrame.bones[segmentId].position;
    return Vector3.Lerp(prev, curr, _t);
}
```

Positions arrive from the sidecar in metres (already divided by `SCALE.m = 10000`). Boolean flags (`dead`, `unconscious`) snap to the new value when `_t >= 0.5f`. The sidecar does not extrapolate.

---

## Demo scene

The demo scene replicates the Knight vs Brawler scenario from `tools/vertical-slice.ts`:

- **Knight**: plate armour, longsword, high structural integrity
- **Brawler**: no armour, bare hands, high stamina
- **Outcome display**: winner, tick count, entity state (shock, fluid loss, consciousness)
- **Controls**: Space = new seed, R = replay last fight, Escape = quit

The scene is a visual proof, not a game. It shows that Ananke produces physically differentiated outcomes visible in a renderer: armour slows shock accumulation, energy depletes, per-region injury, emergent fight end, consciousness degrades independently.

---

## API compliance checklist

When submitting this project to the Ananke ecosystem, verify the following:

- [ ] No direct imports from `src/sim/kernel.ts` internals (only Stable/Experimental tier exports)
- [ ] Positions already in metres when they reach C# (divided by `SCALE.m = 10000` in the sidecar)
- [ ] Interpolation factor `_t` clamped to `[0.0, 1.0]`; no extrapolation unless explicitly opted in
- [ ] `deriveGrappleConstraint` result checked for `null` before applying joint locks
- [ ] Boolean flags (`dead`, `unconscious`) snap at `_t >= 0.5f`, not lerped
- [ ] `serializeReplay` output can be deserialized and replayed deterministically
- [ ] Demo scene runs 200 ticks without exception on seeds 1, 42, and 99
- [ ] HTTP connection failure is handled gracefully (Unity shows "waiting for sidecar" overlay)
- [ ] No `Rigidbody` with `isKinematic = false` on Ananke-controlled entities

---

## Contributing

1. Fork this repository and create a feature branch.
2. The sidecar must stay under 500 lines of TypeScript. Keep simulation complexity in Ananke.
3. All new C# files must have an XML summary comment on the class.
4. Run `npm run typecheck` in `sidecar/` before opening a PR.
5. If you add a new bone mapping preset, add a corresponding test scene.

To list this project in Ananke's `docs/ecosystem.md`, open a PR to the Ananke repository adding a row to the Renderer Bridges table with a link and a one-line description.
