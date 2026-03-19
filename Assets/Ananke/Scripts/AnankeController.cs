// Assets/Ananke/Scripts/AnankeController.cs
//
// MonoBehaviour that polls the Ananke sidecar at 20 Hz, deserialises the JSON
// snapshot, and moves GameObjects to match simulation positions.
//
// Attach to an empty GameObject in the AnankeDemo scene.
// Assign entity GameObjects in the Inspector.
//
// TODO (M2): Drive Animator parameters from AnimationHints fields.
// TODO (M3): Map pose[].segmentId to HumanBodyBones; drive blend shapes.
// TODO (M4): Apply RigConstraint (Animation Rigging) from grapple data.
// TODO (M2 stretch): Replace UnityWebRequest polling with WebSocket push.
//   Use NativeWebSocket (https://github.com/endel/NativeWebSocket) or the
//   Unity Netcode transport layer.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Ananke
{
    public class AnankeController : MonoBehaviour
    {
        // ── Inspector fields ──────────────────────────────────────────────────

        [Header("Sidecar Connection")]
        [Tooltip("Base URL of the Ananke sidecar. Must match PORT in sidecar/server.js.")]
        public string sidecarUrl = "http://127.0.0.1:3001";

        [Tooltip("Poll interval in seconds. 0.05 = 20 Hz (matches simulation tick rate).")]
        public float pollIntervalSeconds = 0.05f;

        [Header("Entity GameObjects")]
        [Tooltip("Map entity IDs to scene GameObjects. Index 0 = entity 1, index 1 = entity 2.")]
        public GameObject[] entityObjects;

        // ── Private state ─────────────────────────────────────────────────────

        /// <summary>Map from entity id to its scene GameObject.</summary>
        private Dictionary<int, GameObject> _entityMap = new();

        /// <summary>Most recently parsed snapshots.</summary>
        private AnankeEntitySnapshot[] _latestSnapshots = Array.Empty<AnankeEntitySnapshot>();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            // Build entity map.
            // The array is assumed to be ordered: index 0 → entity id 1, etc.
            for (int i = 0; i < entityObjects.Length; i++)
            {
                if (entityObjects[i] != null)
                    _entityMap[i + 1] = entityObjects[i];
            }

            if (_entityMap.Count == 0)
                Debug.LogWarning("[AnankeController] No entity GameObjects assigned. Assign them in the Inspector.");

            // Start polling at 20 Hz using InvokeRepeating.
            // InvokeRepeating calls are reliable across frames unlike Update-based timers.
            InvokeRepeating(nameof(PollState), 0f, pollIntervalSeconds);

            Debug.Log($"[AnankeController] Polling {sidecarUrl}/state every {pollIntervalSeconds * 1000:F0} ms");
        }

        private void Update()
        {
            // Apply the most recently received snapshots every frame.
            // This runs at render rate (60 Hz) while polling runs at 20 Hz,
            // so multiple frames may apply the same snapshot until a new one
            // arrives — which is correct for a hold-last interpolation strategy.
            //
            // TODO (M2 stretch): Use Time.deltaTime accumulation against
            // snapshot.tick and the known TICK_HZ to interpolate position
            // between the previous and current snapshot for smooth motion.
            ApplySnapshots(_latestSnapshots);
        }

        // ── HTTP polling ──────────────────────────────────────────────────────

        private void PollState()
        {
            StartCoroutine(FetchState());
        }

        private IEnumerator FetchState()
        {
            using var request = UnityWebRequest.Get($"{sidecarUrl}/state");
            request.timeout = 2; // seconds

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[AnankeController] Sidecar request failed: {request.error}. " +
                                 "Is the sidecar running? Run: cd sidecar && npm start");
                yield break;
            }

            ParseAndStoreSnapshots(request.downloadHandler.text);
        }

        // ── JSON parsing ──────────────────────────────────────────────────────

        /// <summary>
        /// Parse the JSON array from the sidecar.
        ///
        /// JsonUtility does not support root JSON arrays, so we wrap the
        /// response in a helper object before deserialising.
        /// </summary>
        private void ParseAndStoreSnapshots(string json)
        {
            // Wrap the root array so JsonUtility can parse it.
            string wrapped = $"{{\"snapshots\":{json}}}";

            try
            {
                var list = JsonUtility.FromJson<AnankeSnapshotList>(wrapped);
                _latestSnapshots = list?.snapshots ?? Array.Empty<AnankeEntitySnapshot>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AnankeController] JSON parse error: {ex.Message}");
                _latestSnapshots = Array.Empty<AnankeEntitySnapshot>();
            }
        }

        // ── Snapshot application ──────────────────────────────────────────────

        private void ApplySnapshots(AnankeEntitySnapshot[] snapshots)
        {
            if (snapshots == null) return;

            foreach (var snap in snapshots)
            {
                if (!_entityMap.TryGetValue(snap.entityId, out var go) || go == null)
                    continue;

                ApplySnapshot(go, snap);
            }
        }

        private void ApplySnapshot(GameObject go, AnankeEntitySnapshot snap)
        {
            // ── Position ──────────────────────────────────────────────────────
            // Ananke: right-hand Y-up, Z = depth.
            // Unity:  left-hand Y-up. Map Ananke (x, y, z) → Unity (x, z, y).
            // Adjust if your scene uses a different orientation.
            if (snap.position != null)
            {
                go.transform.position = new Vector3(
                    snap.position.x,
                    snap.position.z,   // Ananke vertical Z → Unity Y
                    snap.position.y
                );
            }

            // ── Animator integration ──────────────────────────────────────────
            // TODO (M2): Uncomment and complete when an AnimatorController asset
            // has been created with matching parameter names.
            //
            // var animator = go.GetComponent<Animator>();
            // if (animator != null && snap.animation != null)
            // {
            //     var anim = snap.animation;
            //
            //     // Dead / KO override layers.
            //     animator.SetBool("IsDead",        anim.dead);
            //     animator.SetBool("IsUnconscious",  anim.unconscious);
            //     animator.SetBool("IsProne",        anim.prone);
            //
            //     // Locomotion: drive a Speed float from the active blend.
            //     float speed = 0f;
            //     if      (anim.sprint > 0) speed = 1.5f;
            //     else if (anim.run    > 0) speed = 1.0f;
            //     else if (anim.walk   > 0) speed = 0.5f;
            //     animator.SetFloat("Speed", speed);
            //
            //     // Combat blend weights.
            //     animator.SetFloat("GuardWeight",  AnankeAnimationHints.QToFloat(anim.guardingQ));
            //     animator.SetBool("IsAttacking",   anim.attackingQ > 0);
            //
            //     // Condition overlays.
            //     animator.SetFloat("ShockWeight", AnankeAnimationHints.QToFloat(anim.shockQ));
            // }

            // ── Pose modifiers ────────────────────────────────────────────────
            // TODO (M3): Map pose[].segmentId to HumanBodyBones and drive blend shapes.
            //
            // if (snap.pose != null)
            // {
            //     var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            //     foreach (var modifier in snap.pose)
            //     {
            //         int blendIndex = SegmentToBlendShapeIndex(modifier.segmentId);
            //         if (blendIndex >= 0 && smr != null)
            //         {
            //             smr.SetBlendShapeWeight(blendIndex,
            //                 modifier.ImpairmentFloat() * 100f); // Unity: 0–100
            //         }
            //     }
            // }

            // ── Grapple IK constraints ────────────────────────────────────────
            // TODO (M4): Activate Animation Rigging IK constraint when held.
            //
            // if (snap.grapple != null && snap.grapple.isHeld)
            // {
            //     // Find holder GameObject and lock IK target to its attach point.
            //     // Use snap.grapple.position to select the target anchor:
            //     //   "standing" → upright attachment
            //     //   "prone"    → ground-level attachment
            //     //   "pinned"   → ground-pin attachment
            //     float grip = snap.grapple.GripFloat();
            //     // Drive hand-close blend shape on holder with grip weight.
            // }
        }

        // ── Segment → blend shape mapping ─────────────────────────────────────

        /// <summary>
        /// Map an Ananke segment ID to a SkinnedMeshRenderer blend shape index.
        /// TODO (M3): Replace with a ScriptableObject mapping asset loaded at runtime.
        /// </summary>
        private int SegmentToBlendShapeIndex(string segmentId)
        {
            return segmentId switch
            {
                "thorax"   => 0,
                "abdomen"  => 1,
                "pelvis"   => 2,
                "head"     => 3,
                "leftArm"  => 4,
                "rightArm" => 5,
                "leftLeg"  => 6,
                "rightLeg" => 7,
                _          => -1,
            };
        }
    }
}
