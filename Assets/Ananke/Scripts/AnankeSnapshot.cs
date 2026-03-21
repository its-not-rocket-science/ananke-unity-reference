using System;
using UnityEngine;

namespace Ananke
{
    public static class AnankeScale
    {
        public const float Q = 10000f;
    }

    [Serializable]
    public class AnankePosition
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToUnityVector() => new Vector3(x, z, y);
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
    }

    [Serializable]
    public class AnankePoseModifier
    {
        public string segmentId;
        public int impairmentQ;
        public int structuralQ;
        public int surfaceQ;

        public float ImpairmentFloat() => impairmentQ / AnankeScale.Q;
    }

    [Serializable]
    public class AnankeGrappleConstraint
    {
        public bool isHolder;
        public int holdingEntityId;
        public bool isHeld;
        public int[] heldByIds;
        public string position;
        public int gripQ;

        public float GripFloat() => gripQ / AnankeScale.Q;
    }

    [Serializable]
    public class AnankeEntitySnapshot
    {
        public int entityId;
        public int teamId;
        public int tick;
        public AnankePosition position;
        public AnankeAnimationHints animation;
        public AnankePoseModifier[] pose;
        public AnankeGrappleConstraint grapple;
        public bool dead;
        public bool unconscious;
    }

    [Serializable]
    public class AnankeFrameEnvelope
    {
        public string type;
        public int tick;
        public int entityCount;
        public string generatedAtIso;
        public AnankeEntitySnapshot[] snapshots;
    }
}
