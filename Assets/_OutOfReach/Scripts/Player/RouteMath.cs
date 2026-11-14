using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Player
{
    /// <summary>
    /// Geometry of a constrained route: a polyline (the tether rail) with limited sideways freedom. No physics and
    /// no free flight: a position is always pulled back to within the lateral freedom of the line, and never past
    /// its first or last point.
    /// </summary>
    public static class RouteMath
    {
        /// <summary>Total length of the polyline in metres.</summary>
        public static float Length(IReadOnlyList<Vector3> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                length += Vector3.Distance(points[i - 1], points[i]);
            }

            return length;
        }

        /// <summary>Closest point on the polyline and its distance from the start, measured along the line.</summary>
        public static Vector3 ClosestPoint(IReadOnlyList<Vector3> points, Vector3 position, out float distanceAlong)
        {
            distanceAlong = 0f;
            if (points == null || points.Count == 0)
            {
                return position;
            }

            if (points.Count == 1)
            {
                return points[0];
            }

            Vector3 best = points[0];
            float bestSquared = float.PositiveInfinity;
            float travelled = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                Vector3 a = points[i - 1];
                Vector3 b = points[i];
                Vector3 segment = b - a;
                float segmentLength = segment.magnitude;
                float t = segmentLength > 1e-6f ? Mathf.Clamp01(Vector3.Dot(position - a, segment) / (segmentLength * segmentLength)) : 0f;
                Vector3 candidate = a + (segment * t);
                float squared = (position - candidate).sqrMagnitude;
                if (squared < bestSquared)
                {
                    bestSquared = squared;
                    best = candidate;
                    distanceAlong = travelled + (segmentLength * t);
                }

                travelled += segmentLength;
            }

            return best;
        }

        /// <summary>Pulls a desired position back into the corridor around the polyline.</summary>
        public static Vector3 Constrain(IReadOnlyList<Vector3> points, Vector3 desired, float lateralFreedomMetres)
        {
            Vector3 onLine = ClosestPoint(points, desired, out float unused);
            Vector3 offset = desired - onLine;
            float freedom = Mathf.Max(0f, lateralFreedomMetres);
            return offset.sqrMagnitude <= freedom * freedom ? desired : onLine + (offset.normalized * freedom);
        }
    }
}
