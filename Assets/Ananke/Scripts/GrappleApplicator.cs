using System.Collections.Generic;
using UnityEngine;

namespace Ananke
{
    /// <summary>
    /// Applies an AnankeGrappleConstraint to this entity's transform (isHeld path)
    /// and drives the GripWeight Animator parameter when this entity is the holder.
    /// </summary>
    public class GrappleApplicator : MonoBehaviour
    {
        [Tooltip("Target anchor when grapple position is 'standing'.")]
        public Transform standingAnchor;

        [Tooltip("Target anchor when grapple position is 'prone'.")]
        public Transform proneAnchor;

        [Tooltip("Target anchor when grapple position is 'pinned'.")]
        public Transform pinnedAnchor;

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void Apply(AnankeGrappleConstraint grapple, IReadOnlyDictionary<int, GameObject> entityMap)
        {
            if (grapple == null)
            {
                ClearGripWeight();
                return;
            }

            if (grapple.isHeld && grapple.heldByIds != null && grapple.heldByIds.Length > 0)
            {
                ApplyHeld(grapple, entityMap);
            }
            else if (grapple.isHolder)
            {
                ApplyHolder(grapple);
            }
            else
            {
                ClearGripWeight();
            }
        }

        // ── Held path: constrain this entity's root to the holder's anchor ──────

        private void ApplyHeld(AnankeGrappleConstraint grapple, IReadOnlyDictionary<int, GameObject> entityMap)
        {
            var holderId = grapple.heldByIds[0];
            if (!entityMap.TryGetValue(holderId, out var holder) || holder == null)
                return;

            var anchor = ResolveAnchor(grapple.position, holder.transform);

            // Converge toward the anchor; grip is fully established so use a fixed rate.
            transform.position = Vector3.Lerp(transform.position, anchor.position, 0.2f);
            transform.rotation = Quaternion.Slerp(transform.rotation, anchor.rotation, 0.1f);
        }

        // ── Holder path: drive the grip animation parameter on this entity ───────

        private void ApplyHolder(AnankeGrappleConstraint grapple)
        {
            // GripWeight [0, 1] controls hand-close in the Combat layer of AnankeAnimatorController.
            // Wire a hand-close blend tree or override clip to this parameter.
            if (_animator != null)
                _animator.SetFloat("GripWeight", grapple.GripFloat());
        }

        private void ClearGripWeight()
        {
            if (_animator != null)
                _animator.SetFloat("GripWeight", 0f);
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
