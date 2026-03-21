using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ananke
{
    public static class SkeletonMapper
    {
        private static readonly Dictionary<string, string> BoneNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["head"] = "Head",
            ["neck"] = "Neck",
            ["torso"] = "Torso",
            ["thorax"] = "Torso",
            ["abdomen"] = "Torso",
            ["pelvis"] = "Pelvis",
            ["leftArm"] = "LeftArm",
            ["rightArm"] = "RightArm",
            ["leftLeg"] = "LeftLeg",
            ["rightLeg"] = "RightLeg",
        };

        public static string ResolveBoneName(string segmentId)
        {
            return BoneNames.TryGetValue(segmentId, out var boneName) ? boneName : "Torso";
        }

        public static Dictionary<string, Transform> IndexBones(Transform root)
        {
            var map = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in BoneNames)
            {
                var bone = FindDeepChild(root, pair.Value);
                if (bone != null)
                    map[pair.Key] = bone;
            }

            return map;
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
                return parent;

            for (int index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                var match = FindDeepChild(child, childName);
                if (match != null)
                    return match;
            }

            return null;
        }
    }
}
