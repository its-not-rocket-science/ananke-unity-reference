# ananke-unity-reference — Roadmap

## M1 — Entity positions via HTTP polling ✅ COMPLETE

**Status:** Complete

- Sidecar (`sidecar/src/main.ts`) runs `stepWorld` at 20 Hz and serves `GET /frame` envelopes.
- `AnankeReceiver.cs` polls via `UnityWebRequest` at 20 Hz and fires `FrameReceived` events.
- `AnankeInterpolator.cs` double-buffers frames and computes blend factor `_t` at display rate.
- `AnankeController.cs` drives placeholder capsule `GameObject` positions from interpolated state.
- `AnankeSnapshot.cs` defines `AnankeScale.Q = 10000f` (fixed — was erroneously 18000 in earlier
  revisions).

Acceptance criteria — all met:
- Two capsules move in the Unity viewport driven by Ananke simulation positions.
- `GET /health` returns `{ "ok": true }`.
- Sidecar exits cleanly on SIGTERM.
- `AnankeCondition`, `AnankeAnimationHints`, `AnankePoseModifier` types correctly normalise
  Q values to `[0,1]` by dividing by `AnankeScale.Q = 10000f`.

Stretch goal: Upgrade HTTP polling to WebSocket push using Unity's `NativeWebSocket` or
`com.unity.netcode.gameobjects` transport for lower latency.

---

## M2 — Animator Controller: AnimationHints → Animator parameters ✅ COMPLETE

**Status:** Complete

- `AnimationDriver.cs` drives all Animator parameters from `AnankeAnimationHints` and `AnankeCondition`.
- `Assets/Ananke/AnankeAnimatorController.controller` provides the Unity Animator asset with all parameters pre-declared and three layers wired: Base Layer (PrimaryState int), Combat Override, ShockOverlay Additive.
- Parameters declared: `PrimaryState` (Int 0–5), `Speed` (Float), `GuardWeight`, `AttackWeight`, `ShockBlend`, `FearBlend`, `InjuryBlend` (all Float), `IsProne`, `IsUnconscious`, `IsDead` (Bool).
- Base-layer AnyState transitions on `PrimaryState == 0–5` → Idle / Guard / Attack / Prone / KO / Dead.
- Assign real animation clips and wire blend trees in the inspector. `AnimationDriver.cs` does not change.

---

## M3 — HumanoidRig: segment IDs → Unity HumanBodyBones ✅ COMPLETE

**Status:** Complete

- `SkeletonMapper.cs` maps `RigSnapshot.pose[].segmentId` to `HumanBodyBones` with a default table covering all nine canonical Ananke segments (`head`, `neck`, `thorax`, `abdomen`, `pelvis`, `leftArm`, `rightArm`, `leftLeg`, `rightLeg`).
- Override any mapping via `AnankeSkeletonConfig` ScriptableObject (create at `Assets/Ananke/Skeleton Config`).
- `AnankeController.ApplyPoseModifiers` drives `Animator.GetBoneTransform` Z-rotation from `impairmentQ` and `SkinnedMeshRenderer.SetBlendShapeWeight` from the same value.
- `AnankeCondition` fields (`shockQ`, `consciousnessQ`, etc.) are now included in every sidecar snapshot frame so `InjuryBlend` receives real data.

---

## M4 — GrapplePoseConstraint via Unity IK

- When `grapple.isHeld = true`, activate a `RigConstraint` (Animation Rigging package) locking the held entity's root to an attachment point on the holder.
- `position` field (`"standing"`, `"prone"`, `"pinned"`) selects the constraint target.
- `gripQ` (0–10000) drives a hand-close blend shape on the holder `SkinnedMeshRenderer`.
- Constraint released when `grapple.isHeld` becomes false.

---

## M5 — Full demo scene with health UI

- Replace placeholder capsules with fully rigged humanoid characters (Unity Humanoid rig).
- Canvas overlay with per-entity bars:
  - Shock bar: `shockQ / AnankeScale.Q`.
  - Fatigue / fluid loss (polled from extended sidecar endpoint).
  - KO / Dead state indicator.
- Demo uses `KNIGHT_INFANTRY` vs `HUMAN_BASE` — same setup as the Ananke vertical slice.
- Replay export: press R to write the tick log to a JSON file for playback via Ananke's `replayTo()`.
