# ananke-unity-reference

Unity 6 humanoid rig plugin driven by the [Ananke](https://github.com/its-not-rocket-science/ananke) physics simulation engine.

## What it is

This project demonstrates how to drive a Unity 6 scene with deterministic, physics-grounded character simulation from Ananke. Ananke runs in a Node.js sidecar process and exposes simulation state over HTTP (and eventually WebSocket). Unity polls the sidecar at 20 Hz, reads entity positions and animation state, and renders the result at 60 Hz.

Simulation physics — impact energy, injury regions, stamina, grapple constraints, shock — are computed entirely by Ananke. Unity is a pure renderer.

## Architecture

```
┌─────────────────────────────────┐       HTTP / WebSocket (localhost:3001)
│  Node.js sidecar  (20 Hz)       │ ─────────────────────────────────────►
│  @its-not-rocket-science/ananke │                                        │
│  stepWorld → extractRigSnapshots│ ◄─────────────────────────────────────
│  GET /state  GET /health        │       JSON snapshot (positions, anim)
└─────────────────────────────────┘

                                        ┌──────────────────────────────────┐
                                        │  Unity 6 scene  (60 Hz)          │
                                        │  AnankeController.cs             │
                                        │  UnityWebRequest  (20 Hz)        │
                                        │  moves GameObjects               │
                                        │  drives Animator parameters      │
                                        └──────────────────────────────────┘
```

The tick rate for Ananke is 20 Hz (matching `TICK_HZ` in the engine). Unity renders at 60 Hz; smooth motion between ticks uses Unity's `Time.deltaTime` accumulation against the `interpolation_factor` field in the snapshot.

## Prerequisites

- [Unity 6](https://unity.com/releases/unity-6) (6000.0 or later)
- Node.js 18 or later
- npm 9 or later

## Quick start

**1. Start the sidecar**

```bash
cd sidecar
npm install
npm start
# Sidecar listens on http://localhost:3001
# GET /health  →  { "ok": true }
# GET /state   →  JSON snapshot array
```

**2. Open the Unity project**

Open Unity Hub, click "Open", and select the root folder of this repository.
Unity will import the project and detect `ProjectSettings/ProjectVersion.txt`.

**3. Run the AnankeDemo scene**

Open `Assets/Ananke/AnankeDemo.unity` in Unity and press Play. Two placeholder
capsules should move to match the Ananke simulation output.

> Note: `AnankeDemo.unity` is a placeholder text file. Open Unity, create a new
> scene, add the `AnankeController` MonoBehaviour to an empty GameObject, and
> save it as `AnankeDemo.unity`. See M1 in ROADMAP.md.

## Snapshot JSON shape

`GET /state` returns an array of `AnankeSnapshot` objects:

```jsonc
[
  {
    "entityId": 1,
    "teamId": 1,
    "tick": 42,
    // World-space position in real metres (converted from Ananke fixed-point).
    "position": { "x": 0.0, "y": 0.0, "z": 0.0 },
    "animation": {
      // Locomotion blend — exactly one is SCALE.Q (18000) when mobile.
      "idle":      18000,
      "walk":      0,
      "run":       0,
      "sprint":    0,
      "crawl":     0,
      // Combat blend weights — nonzero during guard/attack.
      "guardingQ":  0,
      "attackingQ": 0,
      // Condition overlays.
      "shockQ":     1200,
      "fearQ":      0,
      // State flags.
      "prone":      false,
      "unconscious": false,
      "dead":       false
    },
    "pose": [
      { "segmentId": "thorax", "impairmentQ": 4500, "structuralQ": 0, "surfaceQ": 4500 }
    ],
    "grapple": {
      "isHolder": false,
      "isHeld": false,
      "heldByIds": [],
      "position": "standing",
      "gripQ": 0
    }
  }
]
```

All `Q` values use `SCALE.Q = 18000`. The C# helper `AnankeSnapshot.QToFloat(q)` converts to a normalised float.

## AnimationHints → Unity Animator

Map the `animation` fields to Unity `Animator` parameters:

| Ananke field   | Animator parameter    | Type    | Notes                              |
|----------------|-----------------------|---------|------------------------------------|
| `idle`         | `IsIdle`              | Bool    | `animation.idle == 18000`          |
| `walk`         | `Speed`               | Float   | 0.5 when walking, 1.0 when running |
| `run`          | `Speed`               | Float   | `run / SCALE_Q`                    |
| `sprint`       | `IsSprinting`         | Bool    | —                                  |
| `crawl`        | `IsCrawling`          | Bool    | —                                  |
| `guardingQ`    | `GuardWeight`         | Float   | `guardingQ / 18000f`               |
| `attackingQ`   | `IsAttacking`         | Bool    | Nonzero during attack cooldown     |
| `shockQ`       | `ShockWeight`         | Float   | Drives stagger blend tree          |
| `prone`        | `IsProne`             | Bool    | —                                  |
| `unconscious`  | `IsUnconscious`       | Bool    | —                                  |
| `dead`         | `IsDead`              | Bool    | Triggers death animation           |

See `Assets/Ananke/Scripts/AnankeController.cs` for TODO stubs.

## Fixed-point coordinate conversion

Ananke stores positions as integers: `SCALE.m = 1000`, so `600 = 0.6 m`. The sidecar converts to real metres before sending, so Unity receives `float` metres directly. Axis mapping:

```csharp
// Ananke Y-up, Z depth → Unity Y-up
transform.position = new Vector3(
    snapshot.position.x,
    snapshot.position.z,   // Ananke Z = vertical in some configurations
    snapshot.position.y
);
```

Adjust the axis mapping to match your scene orientation.

## Further reading

- [Ananke on GitHub](https://github.com/its-not-rocket-science/ananke)
- [docs/bridge-contract.md](https://github.com/its-not-rocket-science/ananke/blob/main/docs/bridge-contract.md) — full bridge API contract
- [ROADMAP.md](./ROADMAP.md) — implementation milestones
