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

## M2 — Animator Controller: AnimationHints → Animator parameters

- Create an `AnimatorController` asset with parameters matching the `AnimationHints` table in README.md.
- `AnankeController.cs` calls `Animator.SetBool` / `Animator.SetFloat` from each snapshot.
- Blend tree for locomotion: `Speed` float drives Idle/Walk/Run/Sprint transitions.
- Override layer for combat: `GuardWeight` and `IsAttacking` bool drive guard/attack clips.
- Overlay layer for condition: `ShockWeight` drives a stagger/flinch additive animation.

---

## M3 — HumanoidRig: segment IDs → Unity HumanBodyBones

- Map `RigSnapshot.pose[].segmentId` values to `HumanBodyBones` enum entries using a `ScriptableObject` mapping asset.
- Drive `Animator.SetBoneLocalRotation` (or `HumanPoseHandler`) from segment deformation data.
- `AnankeSnapshot.pose[]` `impairmentQ` drives blend shape weights on a `SkinnedMeshRenderer` for injury deformation.

Reference segment IDs from `HUMANOID_PLAN` in `@its-not-rocket-science/ananke`:
`thorax`, `abdomen`, `pelvis`, `head`, `neck`, `leftArm`, `rightArm`, `leftLeg`, `rightLeg`.

---

## M4 — GrapplePoseConstraint via Unity IK

- When `grapple.isHeld = true`, activate a `RigConstraint` (Animation Rigging package) locking the held entity's root to an attachment point on the holder.
- `position` field (`"standing"`, `"prone"`, `"pinned"`) selects the constraint target.
- `gripQ` (0–18000) drives a hand-close blend shape on the holder `SkinnedMeshRenderer`.
- Constraint released when `grapple.isHeld` becomes false.

---

## M5 — Full demo scene with health UI

- Replace placeholder capsules with fully rigged humanoid characters (Unity Humanoid rig).
- Canvas overlay with per-entity bars:
  - Shock bar: `shockQ / 18000f`.
  - Fatigue / fluid loss (polled from extended sidecar endpoint).
  - KO / Dead state indicator.
- Demo uses `KNIGHT_INFANTRY` vs `HUMAN_BASE` — same setup as the Ananke vertical slice.
- Replay export: press R to write the tick log to a JSON file for playback via Ananke's `replayTo()`.
