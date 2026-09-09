using System;
using UnityEngine;
using UnityEngine.AI;

namespace CircuitRacing
{
    [DefaultExecutionOrder(-200)]
    public sealed class RoadRoute : MonoBehaviour
    {
        public Vector3[] points;
        public float[] distances;
        public float width = 16f;
        // Optional finish marker for routes that include a visual road continuation
        // beyond the playable race distance.
        public float finishDistance = -1f;
        public NavMeshData navigation;
        public float Length => distances[distances.Length - 1];
        public float FinishDistance => finishDistance > 0f ? Mathf.Min(finishDistance, Length - 1f) : Length - 100f;
        private NavMeshDataInstance navigationInstance;

        private void Awake()
        {
            if (navigation != null) navigationInstance = NavMesh.AddNavMeshData(navigation);
        }

        private void OnDestroy()
        {
            if (navigationInstance.valid) navigationInstance.Remove();
        }

        private int Segment(float distance)
        {
            int index = Array.BinarySearch(distances, Mathf.Clamp(distance, 0f, Length));
            if (index < 0) index = ~index - 1;
            return Mathf.Clamp(index, 0, points.Length - 2);
        }

        public Vector3 Sample(float distance, float lane = 0f)
        {
            int index = Segment(distance);
            float t = Mathf.InverseLerp(distances[index], distances[index + 1], distance);
            Vector3 forward = (points[index + 1] - points[index]).normalized;
            return Vector3.Lerp(points[index], points[index + 1], t) + Vector3.Cross(Vector3.up, forward).normalized * lane;
        }

        public Vector3 Direction(float distance)
        {
            return (Sample(Mathf.Min(Length, distance + 5f)) - Sample(Mathf.Max(0f, distance - 5f))).normalized;
        }

        public float Curvature(float distance)
        {
            Vector3 a = Vector3.ProjectOnPlane(Direction(distance - 10f), Vector3.up).normalized;
            Vector3 b = Vector3.ProjectOnPlane(Direction(distance + 10f), Vector3.up).normalized;
            return Vector3.Angle(a, b) * Mathf.Deg2Rad / 20f;
        }

        public float Project(Vector3 position, ref int hint, out float separation)
        {
            float best = float.PositiveInfinity;
            float result = 0f;
            int bestIndex = hint;
            FindProjection(position, Mathf.Max(0, hint - 16), Mathf.Min(points.Length - 2, hint + 16), ref best, ref result, ref bestIndex);
            if (best > 45f * 45f) FindProjection(position, 0, points.Length - 2, ref best, ref result, ref bestIndex);
            hint = bestIndex;
            separation = Mathf.Sqrt(best);
            return result;
        }

        private void FindProjection(Vector3 position, int first, int last, ref float best, ref float result, ref int bestIndex)
        {
            for (int i = first; i <= last; i++)
            {
                Vector3 segment = points[i + 1] - points[i];
                float t = Mathf.Clamp01(Vector3.Dot(position - points[i], segment) / Mathf.Max(0.001f, segment.sqrMagnitude));
                float squared = (position - points[i] - segment * t).sqrMagnitude;
                if (squared >= best) continue;
                best = squared;
                bestIndex = i;
                result = Mathf.Lerp(distances[i], distances[i + 1], t);
            }
        }
    }
}
