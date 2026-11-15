using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Presentation.UI.Startup
{
    /// <summary>
    /// Turns a stroke polyline into a filled ribbon outline with calligraphic character: ends taper to a fine point and
    /// the width breathes slightly with direction, as if drawn with a broad-nib pen held at a fixed angle. Pure geometry
    /// (unit space), no UI types, so the same code serves the presenter and the offline preview.
    /// </summary>
    public static class CalligraphicStroke
    {
        public const float NibAngleDegrees = 38f;
        public const float NibVariation = .22f;     // width swings between 1 - variation and 1
        private const float EndWidth = .12f;        // width factor at the very tip of a taper
        private const int CapSegments = 5;

        /// <summary>
        /// Writes the closed ribbon polygon for <paramref name="points"/> (a visible prefix of a stroke whose full length is
        /// <paramref name="fullLength"/>). <paramref name="closed"/> strokes do not taper. Returns false when there is nothing to fill.
        /// </summary>
        public static bool Build(IList<Vector2> points, float fullLength, float width, float taper, bool closed, List<Vector2> output)
        {
            output.Clear();
            int n = points.Count;
            if (n < 2 || width <= 0f) return false;
            // A closed outline that is fully drawn is a loop: no taper and no end caps (they would punch holes).
            bool loop = closed && (points[0] - points[n - 1]).sqrMagnitude < 1e-10f;

            var half = new float[n];
            var normal = new Vector2[n];
            float run = 0f;
            float nib = NibAngleDegrees * Mathf.Deg2Rad;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) run += Vector2.Distance(points[i - 1], points[i]);
                Vector2 before = i > 0 ? (points[i] - points[i - 1]) : (points[i + 1] - points[i]);
                Vector2 after = i < n - 1 ? (points[i + 1] - points[i]) : before;
                before = Unit(before); after = Unit(after);
                Vector2 tangent = Unit(before + after);
                if (tangent.sqrMagnitude < 1e-6f) tangent = after;
                Vector2 perp = new Vector2(-tangent.y, tangent.x);
                // miter: stretch the offset across sharp corners, within limits
                Vector2 perpBefore = new Vector2(-before.y, before.x);
                float miter = 1f / Mathf.Max(.55f, Vector2.Dot(perp, perpBefore));
                normal[i] = perp * miter;

                float angle = Mathf.Atan2(tangent.y, tangent.x);
                float nibFactor = 1f - NibVariation + NibVariation * Mathf.Abs(Mathf.Sin(angle - nib));
                float ends = 1f;
                if (!closed && taper > 0f && fullLength > 1e-5f)
                {
                    float span = Mathf.Max(1e-5f, taper * fullLength);
                    float head = Smooth(run / span);
                    float tail = Smooth((fullLength - run) / span);
                    ends = Mathf.Lerp(EndWidth, 1f, Mathf.Min(head, tail));
                }
                half[i] = Mathf.Max(width * .5f * ends * nibFactor, width * .5f * .08f);
            }

            for (int i = 0; i < n; i++) output.Add(points[i] + normal[i] * half[i]);
            if (!loop) AddCap(output, points[n - 1], normal[n - 1], half[n - 1], false);
            for (int i = n - 1; i >= 0; i--) output.Add(points[i] - normal[i] * half[i]);
            if (!loop) AddCap(output, points[0], normal[0], half[0], true);
            return true;
        }

        private static void AddCap(List<Vector2> output, Vector2 centre, Vector2 normal, float half, bool start)
        {
            // semicircle from +normal side around the tip to -normal side (end) or the reverse (start)
            Vector2 n = normal.sqrMagnitude < 1e-8f ? Vector2.up : normal.normalized;
            Vector2 t = new Vector2(n.y, -n.x);             // forward tangent
            if (start) t = -t;
            for (int s = 1; s < CapSegments; s++)
            {
                float a = Mathf.PI * s / CapSegments;
                Vector2 dir = n * Mathf.Cos(a) * (start ? -1f : 1f) + t * Mathf.Sin(a);
                output.Add(centre + dir * half);
            }
        }

        private static Vector2 Unit(Vector2 v) => v.sqrMagnitude < 1e-12f ? Vector2.zero : v.normalized;

        private static float Smooth(float value)
        {
            float x = Mathf.Clamp01(value);
            return x * x * (3f - 2f * x);
        }
    }
}
