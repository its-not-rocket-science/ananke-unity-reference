using System;
using UnityEngine;

namespace Ananke
{
    public static class AnankeScale
    {
        public const float Q = 18000f;
    }

    [Serializable]
    public class AnankePosition
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToUnityPosition() => new Vector3(x, z, y);
        public static AnankePosition Lerp(AnankePosition a, AnankePosition b, float t)
        {
            a ??= new AnankePosition();
            b ??= new AnankePosition();

            return new AnankePosition
            {
                x = Mathf.Lerp(a.x, b.x, t),
                y = Mathf.Lerp(a.y, b.y, t),
                z = Mathf.Lerp(a.z, b.z, t),
            };
        }
    }

    [Serializable]
    public class AnankeCondition
    {
        public int shockQ;
        public int fearQ;
        public int consciousnessQ;
        public int fluidLossQ;
        public int fatigueQ;
        public bool dead;

        public float Shock => shockQ / AnankeScale.Q;
        public float Fear => fearQ / AnankeScale.Q;
        public float Consciousness => consciousnessQ / AnankeScale.Q;
        public float FluidLoss => fluidLossQ / AnankeScale.Q;
        public float Fatigue => fatigueQ / AnankeScale.Q;

        public static AnankeCondition Lerp(AnankeCondition a, AnankeCondition b, float t)
        {
            a ??= new AnankeCondition();
            b ??= new AnankeCondition();

            return new AnankeCondition
            {
                shockQ = Mathf.RoundToInt(Mathf.Lerp(a.shockQ, b.shockQ, t)),
                fearQ = Mathf.RoundToInt(Mathf.Lerp(a.fearQ, b.fearQ, t)),
                consciousnessQ = Mathf.RoundToInt(Mathf.Lerp(a.consciousnessQ, b.consciousnessQ, t)),
                fluidLossQ = Mathf.RoundToInt(Mathf.Lerp(a.fluidLossQ, b.fluidLossQ, t)),
                fatigueQ = Mathf.RoundToInt(Mathf.Lerp(a.fatigueQ, b.fatigueQ, t)),
                dead = t >= 0.5f ? b.dead : a.dead,
            };
        }
    }

    [Serializable]
    public class AnankeAnimationHints
    {
        public int idle;
        public int walk;
        public int run;
        public int sprint;
        public int crawl;
        public int guardingQ;
        public int attackingQ;
        public int shockQ;
        public int fearQ;
        public bool prone;
        public bool unconscious;
        public bool dead;

        public static float QToFloat(int q) => q / AnankeScale.Q;

        public float LocomotionMagnitude
        {
            get
            {
                var locomotion = Mathf.Max(QToFloat(walk), QToFloat(run));
                locomotion = Mathf.Max(locomotion, QToFloat(sprint));
                locomotion = Mathf.Max(locomotion, QToFloat(crawl));
                return locomotion;
            }
        }
        public int PrimaryStateCode => dead ? 5 : unconscious ? 4 : prone ? 3 : attackingQ > 0 ? 2 : guardingQ > 0 ? 1 : 0;

        public static AnankeAnimationHints Lerp(AnankeAnimationHints a, AnankeAnimationHints b, float t)
        {
            a ??= new AnankeAnimationHints();
            b ??= new AnankeAnimationHints();

            return new AnankeAnimationHints
            {
                idle = Mathf.RoundToInt(Mathf.Lerp(a.idle, b.idle, t)),
                walk = Mathf.RoundToInt(Mathf.Lerp(a.walk, b.walk, t)),
                run = Mathf.RoundToInt(Mathf.Lerp(a.run, b.run, t)),
                sprint = Mathf.RoundToInt(Mathf.Lerp(a.sprint, b.sprint, t)),
                crawl = Mathf.RoundToInt(Mathf.Lerp(a.crawl, b.crawl, t)),
                guardingQ = Mathf.RoundToInt(Mathf.Lerp(a.guardingQ, b.guardingQ, t)),
                attackingQ = Mathf.RoundToInt(Mathf.Lerp(a.attackingQ, b.attackingQ, t)),
                shockQ = Mathf.RoundToInt(Mathf.Lerp(a.shockQ, b.shockQ, t)),
                fearQ = Mathf.RoundToInt(Mathf.Lerp(a.fearQ, b.fearQ, t)),
                prone = t >= 0.5f ? b.prone : a.prone,
                unconscious = t >= 0.5f ? b.unconscious : a.unconscious,
                dead = t >= 0.5f ? b.dead : a.dead,
            };
        }
    }

    [Serializable]
    public class AnankePoseModifier
    {
        public string segmentId;
        public int impairmentQ;
        public int structuralQ;
        public int surfaceQ;

        public float ImpairmentFloat() => impairmentQ / AnankeScale.Q;
        public float StructuralFloat() => structuralQ / AnankeScale.Q;
        public float SurfaceFloat() => surfaceQ / AnankeScale.Q;

        public static AnankePoseModifier Lerp(AnankePoseModifier a, AnankePoseModifier b, float t)
        {
            a ??= new AnankePoseModifier();
            b ??= new AnankePoseModifier();

            return new AnankePoseModifier
            {
                segmentId = string.IsNullOrEmpty(b.segmentId) ? a.segmentId : b.segmentId,
                impairmentQ = Mathf.RoundToInt(Mathf.Lerp(a.impairmentQ, b.impairmentQ, t)),
                structuralQ = Mathf.RoundToInt(Mathf.Lerp(a.structuralQ, b.structuralQ, t)),
                surfaceQ = Mathf.RoundToInt(Mathf.Lerp(a.surfaceQ, b.surfaceQ, t)),
            };
        }
    }

    [Serializable]
    public class AnankeGrapple
    {
        public bool isHolder;
        public int holdingEntityId;
        public bool isHeld;
        public int[] heldByIds;
        public string position;
        public int gripQ;

        public float GripFloat() => gripQ / AnankeScale.Q;

        public static AnankeGrapple Blend(AnankeGrapple a, AnankeGrapple b, float t)
        {
            a ??= new AnankeGrapple();
            b ??= new AnankeGrapple();
            return t >= 0.5f ? b : a;
        }
    }

    [Serializable]
    public class AnankeEntitySnapshot
    {
        public int entityId;
        public int teamId;
        public int tick;
        public AnankePosition position;
        public AnankePosition velocity;
        public AnankeAnimationHints animation;
        public AnankePoseModifier[] pose;
        public AnankeGrapple grapple;
        public AnankeCondition condition;
        public bool dead;
        public bool unconscious;
    }

    [Serializable]
    public class AnankeFrameEnvelope
    {
        public string scenarioId;
        public int tickHz;
        public int worldTick;
        public string generatedAt;
        public AnankeEntitySnapshot[] frames;
    }

    [Serializable]
    public class AnankeSnapshotList
    {
        public AnankeEntitySnapshot[] snapshots;
    }
}
