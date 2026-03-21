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
            }
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
        }
    }
}
