// Assets/Ananke/Scripts/AnankeSnapshot.cs
//
// C# data classes matching the JSON snapshot shape sent by sidecar/server.js.
// Deserialise with JsonUtility.FromJson<AnankeSnapshotList>(json).
//
// All Q (fixed-point) integer fields use SCALE_Q = 18000 as the denominator.
// Call AnankeSnapshot.QToFloat(q) to convert to a normalised float in [0, 1].
//
// Source reference: @its-not-rocket-science/ananke
//   AnimationHints  — src/model3d.ts
//   PoseModifier    — src/model3d.ts
//   GrapplePoseConstraint — src/model3d.ts
//   InterpolatedState     — src/bridge/types.ts

using System;
using UnityEngine;

namespace Ananke
{
    /// <summary>
    /// Ananke fixed-point Q scale. SCALE.Q = 18000 in src/units.ts.
    /// Divide any Q integer field by this to get a normalised float.
    /// </summary>
    public static class AnankeScale
    {
        public const float Q = 18000f;
    }

    // ── Wire types ────────────────────────────────────────────────────────────

    /// <summary>
    /// World-space position in real metres. Converted from fixed-point by the
    /// sidecar (SCALE.m = 1000; integer 600 → 0.6 m).
    /// </summary>
    [Serializable]
    public class AnankePosition
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3() => new Vector3(x, z, y);
    }

    /// <summary>
    /// Animation blend weights and state flags from deriveAnimationHints().
    /// Locomotion fields (idle/walk/run/sprint/crawl) are mutually exclusive;
    /// exactly one equals SCALE.Q (18000) when the entity is mobile.
    /// </summary>
    [Serializable]
    public class AnankeAnimationHints
    {
        // Locomotion blend — mutually exclusive.
        public int idle;
        public int walk;
        public int run;
        public int sprint;
        public int crawl;

        // Combat blend weights.
        /// <summary>Active defence blend weight (0–18000).</summary>
        public int guardingQ;
        /// <summary>Attack blend weight; nonzero during attack cooldown.</summary>
        public int attackingQ;

        // Condition overlays.
        /// <summary>Shock level (0–18000). Drives stagger/flinch blend.</summary>
        public int shockQ;
        /// <summary>Fear level (0–18000).</summary>
        public int fearQ;

        // State flags.
        public bool prone;
        public bool unconscious;
        public bool dead;

        /// <summary>
        /// Convert a Q integer field to a normalised float in [0, 1].
        /// Example: QToFloat(guardingQ)
        /// </summary>
        public static float QToFloat(int q) => q / AnankeScale.Q;
    }

    /// <summary>
    /// Per-region injury deformation blend weights from derivePoseModifiers().
    /// Map segmentId to a HumanBodyBones bone or blend shape index.
    /// </summary>
    [Serializable]
    public class AnakePoseModifier
    {
        /// <summary>Ananke body segment ID (e.g. "thorax", "leftArm").</summary>
        public string segmentId;

        /// <summary>Overall deformation blend: max(structuralQ, surfaceQ).</summary>
        public int impairmentQ;

        /// <summary>Structural (bone/joint) damage (0–18000).</summary>
        public int structuralQ;

        /// <summary>Surface (skin/tissue) damage (0–18000).</summary>
        public int surfaceQ;

        public float ImpairmentFloat() => impairmentQ / AnankeScale.Q;
        public float StructuralFloat() => structuralQ / AnankeScale.Q;
        public float SurfaceFloat()    => surfaceQ    / AnankeScale.Q;
    }

    /// <summary>
    /// Grapple relationship data from deriveGrappleConstraint().
    /// Use to activate IK constraints between grappling entities.
    /// </summary>
    [Serializable]
    public class AnankeGrapple
    {
        public bool   isHolder;
        public int    holdingEntityId;   // 0 when isHolder is false
        public bool   isHeld;
        public int[]  heldByIds;         // entity ids holding this entity
        /// <summary>"standing" | "prone" | "pinned"</summary>
        public string position;
        /// <summary>Grip strength (0–18000). Drive hand-close blend shape.</summary>
        public int    gripQ;

        public float GripFloat() => gripQ / AnankeScale.Q;
    }

    /// <summary>
    /// Complete per-entity snapshot sent by the sidecar each tick.
    /// Matches the serialiseSnapshot() output in sidecar/server.js.
    /// </summary>
    [Serializable]
    public class AnankeEntitySnapshot
    {
        public int    entityId;
        public int    teamId;
        public int    tick;

        public AnankePosition       position;
        public AnankeAnimationHints animation;
        public AnakePoseModifier[]  pose;
        public AnankeGrapple        grapple;

        // Convenience fields (also present in animation).
        public bool dead;
        public bool unconscious;
    }

    /// <summary>
    /// Wrapper list needed because JsonUtility cannot deserialise a root JSON array.
    /// Wrap the response: { "snapshots": [...] } if you switch to this type,
    /// or use AnankeSnapshotParser.Parse() which handles the root array manually.
    /// </summary>
    [Serializable]
    public class AnankeSnapshotList
    {
        public AnankeEntitySnapshot[] snapshots;
    }
}
