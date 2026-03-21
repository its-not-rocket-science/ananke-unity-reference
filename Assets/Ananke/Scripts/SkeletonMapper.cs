using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ananke
{
    public static class SkeletonMapper
    {
        private static readonly Dictionary<string, HumanBodyBones> DefaultMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["head"] = HumanBodyBones.Head,
            ["neck"] = HumanBodyBones.Neck,
            ["torso"] = HumanBodyBones.Spine,
            ["thorax"] = HumanBodyBones.Chest,
            ["abdomen"] = HumanBodyBones.Spine,
            ["pelvis"] = HumanBodyBones.Hips,
            ["leftArm"] = HumanBodyBones.LeftUpperArm,
            ["rightArm"] = HumanBodyBones.RightUpperArm,
            ["leftLeg"] = HumanBodyBones.LeftUpperLeg,
            ["rightLeg"] = HumanBodyBones.RightUpperLeg,
        };

        public static HumanBodyBones Resolve(string segmentId, AnankeSkeletonConfig config = null)
        {
            if (config != null && config.TryResolveBone(segmentId, out var overrideBone))
            {
                return overrideBone;
            }

            return !string.IsNullOrEmpty(segmentId) && DefaultMappings.TryGetValue(segmentId, out var bone)
                ? bone
                : HumanBodyBones.LastBone;
        }

        public static int ResolveBlendShapeIndex(string segmentId, AnankeSkeletonConfig config = null)
        {
            return config != null && config.TryResolveBlendShape(segmentId, out var index) ? index : -1;
        }
    }

    [CreateAssetMenu(menuName = "Ananke/Skeleton Config", fileName = "AnankeSkeletonConfig")]
    public class AnankeSkeletonConfig : ScriptableObject
    {
        [Serializable]
        public class SegmentBinding
        {
            public string segmentId;
            public HumanBodyBones humanBodyBone = HumanBodyBones.LastBone;
            public int blendShapeIndex = -1;
        }

        [SerializeField] private SegmentBinding[] bindings = Array.Empty<SegmentBinding>();

        public bool TryResolveBone(string segmentId, out HumanBodyBones bone)
        {
            foreach (var binding in bindings)
            {
                if (binding != null && string.Equals(binding.segmentId, segmentId, StringComparison.OrdinalIgnoreCase))
                {
                    bone = binding.humanBodyBone;
                    return bone != HumanBodyBones.LastBone;
                }
            }

            bone = HumanBodyBones.LastBone;
            return false;
        }

        public bool TryResolveBlendShape(string segmentId, out int blendShapeIndex)
        {
            foreach (var binding in bindings)
            {
                if (binding != null && string.Equals(binding.segmentId, segmentId, StringComparison.OrdinalIgnoreCase))
                {
                    blendShapeIndex = binding.blendShapeIndex;
                    return blendShapeIndex >= 0;
                }
            }

            blendShapeIndex = -1;
            return false;
        }
    }
}
