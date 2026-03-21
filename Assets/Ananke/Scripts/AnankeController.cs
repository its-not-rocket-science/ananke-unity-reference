using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ananke
{
    public class AnankeController : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("Receiver component that maintains the WebSocket connection.")]
        public AnankeReceiver receiver;

        [Tooltip("Optional entity roots. When left empty, placeholder rigs are created automatically.")]
        public GameObject[] entityObjects;

        private readonly Dictionary<int, GameObject> _entityMap = new();
        private readonly Dictionary<int, Dictionary<string, Transform>> _boneMaps = new();
        private readonly Dictionary<int, Vector3> _spawnPositions = new();
        private int _lastAppliedTick = -1;

        private void Awake()
        {
            if (receiver == null)
                receiver = GetComponent<AnankeReceiver>();

            EnsureDemoEntities();
        }

        private void Update()
        {
            if (receiver == null || receiver.LatestFrame == null)
                return;

            if (receiver.LatestFrame.tick == _lastAppliedTick)
                return;

            _lastAppliedTick = receiver.LatestFrame.tick;
            ApplySnapshots(receiver.LatestFrame.snapshots);
        }

        private void EnsureDemoEntities()
        {
            if (entityObjects != null && entityObjects.Length >= 2)
            {
                RegisterEntities(entityObjects);
                return;
            }

            entityObjects = new[]
            {
                CreatePlaceholderRig(1, new Vector3(-1.5f, 0f, 0f), Color.cyan),
                CreatePlaceholderRig(2, new Vector3(1.5f, 0f, 0f), Color.red),
            };

            RegisterEntities(entityObjects);
        }

        private void RegisterEntities(GameObject[] objects)
        {
            _entityMap.Clear();
            _boneMaps.Clear();
            _spawnPositions.Clear();

            for (int index = 0; index < objects.Length; index++)
            {
                var go = objects[index];
                if (go == null)
                    continue;

                var entityId = index + 1;
                _entityMap[entityId] = go;
                _boneMaps[entityId] = SkeletonMapper.IndexBones(go.transform);
                _spawnPositions[entityId] = go.transform.position;
            }
        }

        private void ApplySnapshots(AnankeEntitySnapshot[] snapshots)
        {
            if (snapshots == null)
                return;

            foreach (var snapshot in snapshots)
            {
                if (!_entityMap.TryGetValue(snapshot.entityId, out var entityRoot))
                    continue;

                ApplyRootTransform(entityRoot, snapshot);
                ApplyPose(entityRoot, snapshot);
            }
        }

        private void ApplyRootTransform(GameObject entityRoot, AnankeEntitySnapshot snapshot)
        {
            if (snapshot.position == null)
                return;

            var baseOffset = _spawnPositions.TryGetValue(snapshot.entityId, out var storedOffset)
                ? storedOffset
                : Vector3.zero;
            entityRoot.transform.position = baseOffset + snapshot.position.ToUnityVector();

            if (snapshot.animation == null)
                return;

            var tint = entityRoot.GetComponentInChildren<Renderer>();
            if (tint != null)
            {
                var shock = AnankeAnimationHints.QToFloat(snapshot.animation.shockQ);
                var colour = Color.Lerp(Color.white, Color.yellow, shock);
                if (snapshot.animation.dead)
                    colour = Color.gray;
                else if (snapshot.animation.unconscious)
                    colour = new Color(0.6f, 0.6f, 1f);

                tint.material.color = colour;
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

        private void ApplyPose(GameObject entityRoot, AnankeEntitySnapshot snapshot)
        {
            if (snapshot.pose == null)
                return;

            if (!_boneMaps.TryGetValue(snapshot.entityId, out var boneMap))
                boneMap = SkeletonMapper.IndexBones(entityRoot.transform);

            foreach (var modifier in snapshot.pose)
            {
                if (!boneMap.TryGetValue(modifier.segmentId, out var bone) || bone == null)
                    continue;

                var impairment = modifier.ImpairmentFloat();
                bone.localScale = Vector3.one * (1f + impairment * 0.12f);
                bone.localRotation = Quaternion.Euler(impairment * 8f, 0f, 0f);
            }
        }

        private static GameObject CreatePlaceholderRig(int entityId, Vector3 position, Color colour)
        {
            var root = new GameObject($"Entity{entityId}");
            root.transform.position = position;

            CreateBone(root.transform, "Pelvis", Vector3.zero, new Vector3(0.35f, 0.25f, 0.2f), colour);
            CreateBone(root.transform, "Torso", new Vector3(0f, 0.55f, 0f), new Vector3(0.4f, 0.55f, 0.22f), colour);
            CreateBone(root.transform, "Head", new Vector3(0f, 1.15f, 0f), new Vector3(0.25f, 0.25f, 0.25f), colour);
            CreateBone(root.transform, "LeftArm", new Vector3(-0.4f, 0.65f, 0f), new Vector3(0.18f, 0.45f, 0.18f), colour);
            CreateBone(root.transform, "RightArm", new Vector3(0.4f, 0.65f, 0f), new Vector3(0.18f, 0.45f, 0.18f), colour);
            CreateBone(root.transform, "LeftLeg", new Vector3(-0.15f, -0.55f, 0f), new Vector3(0.2f, 0.55f, 0.2f), colour);
            CreateBone(root.transform, "RightLeg", new Vector3(0.15f, -0.55f, 0f), new Vector3(0.2f, 0.55f, 0.2f), colour);

            return root;
        }

        private static GameObject CreateBone(Transform parent, string name, Vector3 localPosition, Vector3 scale, Color colour)
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = scale;
            var renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = colour;
            return primitive;
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
