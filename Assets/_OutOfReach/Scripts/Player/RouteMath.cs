using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Player
{
    /// <summary>
    /// Geometry of a constrained route: a polyline (the tether rail). No physics and no free flight. The player is
    /// moved by distance along the line (<see cref="PointAtDistance"/>), which can never jump between segments;
    /// <see cref="Constrain"/> keeps the older corridor projection for callers that need it.
    /// </summary>
    public static class RouteMath
    {
        /// <summary>Below this horizontal extent, metres, a stretch of route counts as vertical (no heading of its own).</summary>
        public const float MinimumHeadingMetres = 0.2f;

        /// <summary>
        /// The point at a distance from the start, measured along the line. Distances outside the line are clamped
        /// to its ends; an empty route returns the zero vector.
        /// </summary>
        public static Vector3 PointAtDistance(IReadOnlyList<Vector3> points, float distance)
        {
            if (points == null || points.Count == 0)
            {
                return Vector3.zero;
            }

            if (points.Count == 1 || float.IsNaN(distance) || distance <= 0f)
            {
                return points[0];
            }

            float travelled = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                float segment = Vector3.Distance(points[i - 1], points[i]);
                if (segment > 1e-6f && distance <= travelled + segment)
                {
                    return Vector3.Lerp(points[i - 1], points[i], (distance - travelled) / segment);
                }

                travelled += segment;
            }

            return points[points.Count - 1];
        }

        /// <summary>
        /// Horizontal unit direction of the route around a distance, pointing towards its end: the horizontal part
        /// of the line between distance - window and distance + window. Where the route is locally vertical the
        /// overall start-to-end heading is used; a route that is vertical as a whole returns the zero vector.
        /// </summary>
        public static Vector3 HorizontalHeading(IReadOnlyList<Vector3> points, float distance, float windowMetres)
        {
            if (points == null || points.Count < 2)
            {
                return Vector3.zero;
            }

            float length = Length(points);
            float half = Mathf.Max(0.01f, windowMetres);
            float at = Mathf.Clamp(float.IsNaN(distance) ? 0f : distance, 0f, length);
            Vector3 local = PointAtDistance(points, Mathf.Min(length, at + half)) - PointAtDistance(points, Mathf.Max(0f, at - half));
            local.y = 0f;
            if (local.sqrMagnitude >= MinimumHeadingMetres * MinimumHeadingMetres)
            {
                return local.normalized;
            }

            Vector3 overall = points[points.Count - 1] - points[0];
            overall.y = 0f;
            return overall.sqrMagnitude >= MinimumHeadingMetres * MinimumHeadingMetres ? overall.normalized : Vector3.zero;
        }

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

        // EVA-TETHER-01: hand-over-hand grip lattice. The rope offers "rungs" (grip stations) every stroke length,
        // counted from the route start. A glove holds the rung inside the reach band (near, near + stroke] ahead of
        // the body in the facing sense; it stays fixed on the rope while the body moves past it, so the body's travel
        // through one stroke is exactly one arm pull. The presentation (EvaTetherClimbMath) uses the same lattice.

        /// <summary>
        /// The holding rung (route distance) for a body at <paramref name="distance"/> facing
        /// <paramref name="facingSense"/> (+1 towards the route end, -1 towards the start): the unique rung whose
        /// distance ahead of the body, measured in the facing sense, lies in (near, near + stroke].
        /// </summary>
        public static float HoldingRung(float distance, int facingSense, float strokeMetres, float nearMetres)
        {
            float stroke = Mathf.Max(0.05f, strokeMetres);
            float near = Mathf.Max(0f, nearMetres);
            float d = float.IsNaN(distance) ? 0f : distance;
            if (facingSense >= 0)
            {
                return (Mathf.Floor((d + near) / stroke) + 1f) * stroke;
            }

            return (Mathf.Ceil((d - near) / stroke) - 1f) * stroke;
        }

        /// <summary>
        /// Phase of the current arm pull in [0, 1): 0 when the leading glove has just closed on its rung at full
        /// reach, approaching 1 as that glove arrives by the chest and the other glove takes over.
        /// </summary>
        public static float HandStrokePhase(float distance, int facingSense, float strokeMetres, float nearMetres)
        {
            float stroke = Mathf.Max(0.05f, strokeMetres);
            float near = Mathf.Max(0f, nearMetres);
            float d = float.IsNaN(distance) ? 0f : distance;
            float rung = HoldingRung(d, facingSense, stroke, near);
            float ahead = facingSense >= 0 ? rung - d : d - rung;
            return Mathf.Clamp01((near + stroke - ahead) / stroke) % 1f;
        }

        /// <summary>
        /// Speed factor through one pull: slowest as the gloves change over (phase 0), fastest mid-pull. Normalized so
        /// the time-averaged speed over a whole stroke equals the route speed (1 / sqrt(1 - surge^2) scaling).
        /// </summary>
        public static float PullSpeedFactor(float phase, float surge)
        {
            float a = Mathf.Clamp(surge, 0f, 0.6f);
            float p = float.IsNaN(phase) ? 0f : phase;
            return (1f - (a * Mathf.Cos(2f * Mathf.PI * p))) / Mathf.Sqrt(1f - (a * a));
        }
    }
}
