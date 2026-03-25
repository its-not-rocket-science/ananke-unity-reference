using System.Collections.Generic;
using UnityEngine;

namespace Ananke
{
    /// <summary>
    /// Routes interpolated AnankeEntitySnapshots to entity GameObjects.
    /// Drives position, bone pose, animation parameters, and grapple constraints
    /// from each frame delivered by AnankeReceiver.
    /// </summary>
    public class AnankeController : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Receiver component that maintains the WebSocket connection.")]
        public AnankeReceiver receiver;

        [Tooltip("Optional ScriptableObject to override default segment → bone mappings.")]
        public AnankeSkeletonConfig skeletonConfig;

        [Header("Entity GameObjects")]
        [Tooltip("Index 0 maps to entity id 1, index 1 to entity id 2, etc. Leave empty to auto-create placeholder rigs.")]
        public GameObject[] entityObjects;

        private readonly Dictionary<int, GameObject> _entityMap = new();
        private readonly AnankeInterpolator _interpolator = new();

        private void Awake()
        {
            if (receiver == null)
                receiver = GetComponent<AnankeReceiver>();

            EnsureEntityObjects();
            RebuildEntityMap();
        }

        private void OnEnable()
        {
            if (receiver != null)
                receiver.FrameReceived += OnFrameReceived;
        }

        private void OnDisable()
        {
            if (receiver != null)
                receiver.FrameReceived -= OnFrameReceived;
        }

        private void Update()
        {
            foreach (var pair in _entityMap)
            {
                if (_interpolator.TryGetInterpolatedSnapshot(pair.Key, Time.time, out var snapshot))
                    ApplySnapshot(pair.Value, snapshot);
            }
        }

        private void OnFrameReceived(AnankeFrameEnvelope envelope)
        {
            _interpolator.PushFrame(envelope, Time.time);
        }

        private void EnsureEntityObjects()
        {
            if (entityObjects != null && entityObjects.Length >= 2)
                return;

            entityObjects = new[]
            {
                CreatePlaceholderRig(1, new Vector3(-1.5f, 0f, 0f), Color.cyan),
                CreatePlaceholderRig(2, new Vector3( 1.5f, 0f, 0f), Color.red),
            };
        }

        private void RebuildEntityMap()
        {
            _entityMap.Clear();
            for (var index = 0; index < entityObjects.Length; index++)
            {
                if (entityObjects[index] != null)
                    _entityMap[index + 1] = entityObjects[index];
            }
        }

        private void ApplySnapshot(GameObject target, AnankeEntitySnapshot snapshot)
        {
            if (target == null || snapshot?.position == null)
                return;

            target.transform.position = snapshot.position.ToUnityPosition();

            ApplyPoseModifiers(target, snapshot.pose);

            var animDriver = target.GetComponent<AnimationDriver>();
            if (animDriver != null)
                animDriver.ApplyHints(snapshot.animation, snapshot.condition);

            var grapple = target.GetComponent<GrappleApplicator>();
            if (grapple != null)
                grapple.Apply(snapshot.grapple, _entityMap);
        }

        private void ApplyPoseModifiers(GameObject target, AnankePoseModifier[] poseModifiers)
        {
            if (poseModifiers == null || poseModifiers.Length == 0)
                return;

            var animator = target.GetComponent<Animator>();
            var skin = target.GetComponentInChildren<SkinnedMeshRenderer>();

            foreach (var poseModifier in poseModifiers)
            {
                if (poseModifier == null)
                    continue;

                if (skin != null)
                {
                    var blendShapeIndex = SkeletonMapper.ResolveBlendShapeIndex(poseModifier.segmentId, skeletonConfig);
                    if (skin.sharedMesh != null && blendShapeIndex >= 0 && blendShapeIndex < skin.sharedMesh.blendShapeCount)
                        skin.SetBlendShapeWeight(blendShapeIndex, poseModifier.ImpairmentFloat() * 100f);
                }

                var boneId = SkeletonMapper.Resolve(poseModifier.segmentId, skeletonConfig);
                if (animator == null || boneId == HumanBodyBones.LastBone)
                    continue;

                var bone = animator.GetBoneTransform(boneId);
                if (bone == null)
                    continue;

                var targetRotation = Quaternion.Euler(0f, 0f, -20f * poseModifier.ImpairmentFloat());
                bone.localRotation = Quaternion.Slerp(bone.localRotation, targetRotation, 0.5f);
            }
        }

        // ── Placeholder rig ──────────────────────────────────────────────────────

        private static GameObject CreatePlaceholderRig(int entityId, Vector3 position, Color colour)
        {
            var root = new GameObject($"Entity{entityId}");
            root.transform.position = position;

            CreateSegment(root.transform, "pelvis",    Vector3.zero,                    new Vector3(0.35f, 0.25f, 0.20f), colour);
            CreateSegment(root.transform, "thorax",    new Vector3( 0f,  0.55f,  0f),   new Vector3(0.40f, 0.55f, 0.22f), colour);
            CreateSegment(root.transform, "head",      new Vector3( 0f,  1.15f,  0f),   new Vector3(0.25f, 0.25f, 0.25f), colour);
            CreateSegment(root.transform, "neck",      new Vector3( 0f,  0.90f,  0f),   new Vector3(0.15f, 0.20f, 0.15f), colour);
            CreateSegment(root.transform, "leftArm",   new Vector3(-0.4f, 0.65f, 0f),   new Vector3(0.18f, 0.45f, 0.18f), colour);
            CreateSegment(root.transform, "rightArm",  new Vector3( 0.4f, 0.65f, 0f),   new Vector3(0.18f, 0.45f, 0.18f), colour);
            CreateSegment(root.transform, "leftLeg",   new Vector3(-0.15f, -0.55f, 0f), new Vector3(0.20f, 0.55f, 0.20f), colour);
            CreateSegment(root.transform, "rightLeg",  new Vector3( 0.15f, -0.55f, 0f), new Vector3(0.20f, 0.55f, 0.20f), colour);

            return root;
        }

        private static void CreateSegment(Transform parent, string segmentId, Vector3 localPosition, Vector3 scale, Color colour)
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitive.name = segmentId;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = scale;
            var rend = primitive.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = colour;
        }
    }
}
