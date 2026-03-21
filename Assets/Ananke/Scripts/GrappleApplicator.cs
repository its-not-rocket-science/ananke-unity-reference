using System.Collections.Generic;
using UnityEngine;

namespace Ananke
{
    public class GrappleApplicator : MonoBehaviour
    {
        public Transform standingAnchor;
        public Transform proneAnchor;
        public Transform pinnedAnchor;

        public void Apply(AnankeGrapple grapple, IReadOnlyDictionary<int, GameObject> entityMap)
        {
            if (grapple == null || !grapple.isHeld || grapple.heldByIds == null || grapple.heldByIds.Length == 0)
            {
                return;
            }

            var holderId = grapple.heldByIds[0];
            if (!entityMap.TryGetValue(holderId, out var holder) || holder == null)
            {
                return;
            }

            var anchor = ResolveAnchor(grapple.position, holder.transform);
            var weight = Mathf.Clamp01(grapple.GripFloat());
            transform.position = Vector3.Lerp(transform.position, anchor.position, weight);
            transform.rotation = Quaternion.Slerp(transform.rotation, anchor.rotation, weight);
        }

        private Transform ResolveAnchor(string grapplePosition, Transform fallback)
        {
            return grapplePosition switch
            {
                "prone" => proneAnchor != null ? proneAnchor : fallback,
                "pinned" => pinnedAnchor != null ? pinnedAnchor : fallback,
                _ => standingAnchor != null ? standingAnchor : fallback,
            };
        }
    }
}
