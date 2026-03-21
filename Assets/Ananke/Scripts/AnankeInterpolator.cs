using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ananke
{
    public class AnankeInterpolator
    {
        private sealed class BufferedSnapshot
        {
            public AnankeEntitySnapshot Snapshot;
            public float ReceivedAt;
        }

        private sealed class SnapshotPair
        {
            public BufferedSnapshot Previous;
            public BufferedSnapshot Current;
        }

        private readonly Dictionary<int, SnapshotPair> _snapshotsByEntity = new();
        private float _tickIntervalSeconds = 0.05f;

        public void PushFrame(AnankeFrameEnvelope envelope, float receivedAt)
        {
            if (envelope == null || envelope.frames == null)
            {
                return;
            }

            _tickIntervalSeconds = envelope.tickHz > 0 ? 1f / envelope.tickHz : _tickIntervalSeconds;

            foreach (var frame in envelope.frames)
            {
                if (!_snapshotsByEntity.TryGetValue(frame.entityId, out var pair))
                {
                    pair = new SnapshotPair();
                    _snapshotsByEntity[frame.entityId] = pair;
                }

                pair.Previous = pair.Current;
                pair.Current = new BufferedSnapshot
                {
                    Snapshot = frame,
                    ReceivedAt = receivedAt,
                };
            }
        }

        public bool TryGetInterpolatedSnapshot(int entityId, float renderTime, out AnankeEntitySnapshot snapshot)
        {
            snapshot = null;

            if (!_snapshotsByEntity.TryGetValue(entityId, out var pair) || pair.Current == null)
            {
                return false;
            }

            if (pair.Previous == null)
            {
                snapshot = pair.Current.Snapshot;
                return true;
            }

            var elapsed = Mathf.Max(0f, renderTime - pair.Current.ReceivedAt);
            var alpha = Mathf.Clamp01(elapsed / Mathf.Max(_tickIntervalSeconds, 0.0001f));
            snapshot = Interpolate(pair.Previous.Snapshot, pair.Current.Snapshot, alpha);
            return true;
        }

        private static AnankeEntitySnapshot Interpolate(AnankeEntitySnapshot previous, AnankeEntitySnapshot current, float t)
        {
            return new AnankeEntitySnapshot
            {
                entityId = current.entityId,
                teamId = current.teamId,
                tick = Mathf.RoundToInt(Mathf.Lerp(previous.tick, current.tick, t)),
                position = AnankePosition.Lerp(previous.position, current.position, t),
                velocity = AnankePosition.Lerp(previous.velocity, current.velocity, t),
                animation = AnankeAnimationHints.Lerp(previous.animation, current.animation, t),
                pose = InterpolatePose(previous.pose, current.pose, t),
                grapple = AnankeGrapple.Blend(previous.grapple, current.grapple, t),
                condition = AnankeCondition.Lerp(previous.condition, current.condition, t),
                dead = t >= 0.5f ? current.dead : previous.dead,
                unconscious = t >= 0.5f ? current.unconscious : previous.unconscious,
            };
        }

        private static AnankePoseModifier[] InterpolatePose(AnankePoseModifier[] previous, AnankePoseModifier[] current, float t)
        {
            previous ??= new AnankePoseModifier[0];
            current ??= new AnankePoseModifier[0];

            var previousById = previous.Where(item => item != null && !string.IsNullOrEmpty(item.segmentId)).ToDictionary(item => item.segmentId);
            var currentById = current.Where(item => item != null && !string.IsNullOrEmpty(item.segmentId)).ToDictionary(item => item.segmentId);
            var segmentIds = previousById.Keys.Union(currentById.Keys);

            var list = new List<AnankePoseModifier>();
            foreach (var segmentId in segmentIds)
            {
                previousById.TryGetValue(segmentId, out var previousModifier);
                currentById.TryGetValue(segmentId, out var currentModifier);
                list.Add(AnankePoseModifier.Lerp(previousModifier, currentModifier, t));
            }

            return list.ToArray();
        }
    }
}
