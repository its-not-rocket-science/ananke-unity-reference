using System.Collections.Generic;
using UnityEngine;

namespace Ananke
{
    /// <summary>
    /// Applies an AnankeGrappleConstraint to this entity's transform.
    /// When isHeld is true, the entity is pulled toward an anchor point on the holder.
    /// </summary>
    public class GrappleApplicator : MonoBehaviour
    {
        [Tooltip("Target anchor when grapple position is 'standing'.")]
        public Transform standingAnchor;

        [Tooltip("Target anchor when grapple position is 'prone'.")]
        public Transform proneAnchor;

        [Tooltip("Target anchor when grapple position is 'pinned'.")]
        public Transform pinnedAnchor;

        public void Apply(AnankeGrappleConstraint grapple, IReadOnlyDictionary<int, GameObject> entityMap)
        {
            if (grapple == null || !grapple.isHeld || grapple.heldByIds == null || grapple.heldByIds.Length == 0)
                return;

            var holderId = grapple.heldByIds[0];
            if (!entityMap.TryGetValue(holderId, out var holder) || holder == null)
                return;

            var anchor = ResolveAnchor(grapple.position, holder.transform);
            var weight = Mathf.Clamp01(grapple.GripFloat());
            transform.position = Vector3.Lerp(transform.position, anchor.position, weight);
            transform.rotation = Quaternion.Slerp(transform.rotation, anchor.rotation, weight);
        }

        private Transform ResolveAnchor(string grapplePosition, Transform fallback)
        {
            return grapplePosition switch
            {
                "prone"  => proneAnchor  != null ? proneAnchor  : fallback,
                "pinned" => pinnedAnchor != null ? pinnedAnchor : fallback,
                _        => standingAnchor != null ? standingAnchor : fallback,
            };
        }
    }
}
