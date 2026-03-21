using System.Collections.Generic;
using UnityEngine;

namespace Ananke
{
    public class AnankeController : MonoBehaviour
    {
        [Header("Dependencies")]
        public AnankeReceiver receiver;
        public AnankeSkeletonConfig skeletonConfig;

        [Header("Entity GameObjects")]
        [Tooltip("Index 0 maps to entity id 1, index 1 to entity id 2, and so on.")]
        public GameObject[] entityObjects;

        private readonly Dictionary<int, GameObject> _entityMap = new();
        private readonly AnankeInterpolator _interpolator = new();

        private void Awake()
        {
            if (receiver == null)
            {
                receiver = GetComponent<AnankeReceiver>();
            }

            RebuildEntityMap();
        }

        private void OnEnable()
        {
            if (receiver != null)
            {
                receiver.FrameReceived += OnFrameReceived;
            }
        }

        private void OnDisable()
        {
            if (receiver != null)
            {
                receiver.FrameReceived -= OnFrameReceived;
            }
        }

        private void Update()
        {
            foreach (var pair in _entityMap)
            {
                if (!_interpolator.TryGetInterpolatedSnapshot(pair.Key, Time.time, out var snapshot))
                {
                    continue;
                }

                ApplySnapshot(pair.Value, snapshot);
            }
        }

        private void RebuildEntityMap()
        {
            _entityMap.Clear();
            for (var index = 0; index < entityObjects.Length; index++)
            {
                if (entityObjects[index] != null)
                {
                    _entityMap[index + 1] = entityObjects[index];
                }
            }
        }

        private void OnFrameReceived(AnankeFrameEnvelope envelope)
        {
            _interpolator.PushFrame(envelope, Time.time);
        }

        private void ApplySnapshot(GameObject target, AnankeEntitySnapshot snapshot)
        {
            if (target == null || snapshot?.position == null)
            {
                return;
            }

            target.transform.position = snapshot.position.ToUnityPosition();

            ApplyPoseModifiers(target, snapshot.pose);

            var animationDriver = target.GetComponent<AnimationDriver>();
            if (animationDriver != null)
            {
                animationDriver.ApplyHints(snapshot.animation, snapshot.condition);
            }

            var grappleApplicator = target.GetComponent<GrappleApplicator>();
            if (grappleApplicator != null)
            {
                grappleApplicator.Apply(snapshot.grapple, _entityMap);
            }
        }

        private void ApplyPoseModifiers(GameObject target, AnankePoseModifier[] poseModifiers)
        {
            if (poseModifiers == null || poseModifiers.Length == 0)
            {
                return;
            }

            var animator = target.GetComponent<Animator>();
            var skin = target.GetComponentInChildren<SkinnedMeshRenderer>();

            foreach (var poseModifier in poseModifiers)
            {
                if (poseModifier == null)
                {
                    continue;
                }

                if (skin != null)
                {
                    var blendShapeIndex = SkeletonMapper.ResolveBlendShapeIndex(poseModifier.segmentId, skeletonConfig);
                    if (skin.sharedMesh != null && blendShapeIndex >= 0 && blendShapeIndex < skin.sharedMesh.blendShapeCount)
                    {
                        skin.SetBlendShapeWeight(blendShapeIndex, poseModifier.ImpairmentFloat() * 100f);
                    }
                }

                var boneId = SkeletonMapper.Resolve(poseModifier.segmentId, skeletonConfig);
                if (animator == null || boneId == HumanBodyBones.LastBone)
                {
                    continue;
                }

                var bone = animator.GetBoneTransform(boneId);
                if (bone == null)
                {
                    continue;
                }

                var targetRotation = Quaternion.Euler(0f, 0f, -20f * poseModifier.ImpairmentFloat());
                bone.localRotation = Quaternion.Slerp(bone.localRotation, targetRotation, 0.5f);
            }
        }
    }
}
