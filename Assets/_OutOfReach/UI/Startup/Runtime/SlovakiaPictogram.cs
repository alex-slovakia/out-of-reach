using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Presentation.UI.Startup
{
    /// <summary>One pencil stroke: a wobbly polyline in the pictogram's unit box, revealed between two draw-on times.</summary>
    public sealed class PencilStroke
    {
        public Vector2[] Points;
        public float[] Lengths;      // cumulative length, Lengths[i] = distance from Points[0] to Points[i]
        public float Width;          // fraction of the pictogram height
        public float Alpha;
        public float RevealStart;    // draw-on progress at which this stroke starts
        public float RevealEnd;      // ... and finishes
        public bool IsAccent;        // capital marker, drawn solid
    }

    /// <summary>
    /// Original hand-built Slovakia pictogram: a simplified national outline from about forty approximate
    /// longitude/latitude points of the border (an illustration, not a survey or a NASA dataset) redrawn as layered,
    /// jittered pencil passes with light diagonal hatching. Geometry is deterministic (seeded), unit-box based and
    /// independent of any UI panel. Y grows downward so strokes map straight to UI Toolkit space.
    /// </summary>
    public static class SlovakiaPictogram
    {
        // lon, lat degrees, clockwise from the Danube at Devin. Approximate, simplified.
        private static readonly float[] LonLat =
        {
            16.98f,48.18f, 16.85f,48.35f, 16.88f,48.55f, 17.05f,48.72f, 17.15f,48.86f, 17.65f,48.85f, 17.95f,49.06f,
            18.30f,49.35f, 18.50f,49.47f, 18.85f,49.52f, 19.05f,49.40f, 19.45f,49.61f, 19.75f,49.35f, 20.05f,49.18f,
            20.35f,49.28f, 20.50f,49.40f, 20.80f,49.40f, 21.30f,49.42f, 21.70f,49.40f, 22.00f,49.25f, 22.56f,49.08f,
            22.45f,48.85f, 22.20f,48.55f, 22.15f,48.38f, 21.70f,48.37f, 21.55f,48.52f, 21.25f,48.55f, 20.85f,48.57f,
            20.55f,48.55f, 20.35f,48.30f, 19.65f,48.23f, 19.28f,48.08f, 18.85f,47.88f, 18.72f,47.78f, 18.12f,47.75f,
            17.78f,47.88f, 17.42f,47.95f, 17.07f,48.00f, 17.00f,48.12f
        };
        private const float MinLon = 16.83f, MaxLon = 22.58f, MinLat = 47.74f, MaxLat = 49.63f;
        private const float BratislavaLon = 17.11f, BratislavaLat = 48.15f;
        private static readonly float LonScale = Mathf.Cos(48.7f * Mathf.Deg2Rad);

        /// <summary>Width / height of the pictogram's unit box (longitude shrunk by cos of the mean latitude).</summary>
        public static readonly float Aspect = (MaxLon - MinLon) * Mathf.Cos(48.7f * Mathf.Deg2Rad) / (MaxLat - MinLat);

        public static Vector2[] Outline()
        {
            var points = new Vector2[LonLat.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = Project(LonLat[i * 2], LonLat[i * 2 + 1]);
            return points;
        }

        public static Vector2 Capital => Project(BratislavaLon, BratislavaLat);

        /// <summary>Unit box: x 0..Aspect, y 0..1 (down).</summary>
        private static Vector2 Project(float lon, float lat) => new Vector2(
            (lon - MinLon) * LonScale / (MaxLat - MinLat),
            (MaxLat - lat) / (MaxLat - MinLat));

        public static List<PencilStroke> Build(int seed = 1701)
        {
            var rng = new System.Random(seed);
            var outline = Outline();
            var strokes = new List<PencilStroke>(160);

            // Three overlapping outline passes, each starting at a different vertex so the joins do not line up.
            float[] widths = { .0105f, .0065f, .005f };
            float[] alphas = { .95f, .6f, .45f };
            float[] jitter = { .0022f, .0045f, .0065f };
            int[] startVertex = { 0, outline.Length / 3, 2 * outline.Length / 3 };
            for (int pass = 0; pass < 3; pass++)
            {
                var ring = new List<Vector2>(outline.Length + 2);
                for (int i = 0; i <= outline.Length; i++) ring.Add(outline[(startVertex[pass] + i) % outline.Length]);
                var wobbly = Wobble(Resample(ring, .016f), jitter[pass], rng);
                float begin = pass * .03f;
                strokes.Add(MakeStroke(wobbly, widths[pass], alphas[pass], begin, Mathf.Lerp(.55f, .62f, pass * .5f), false));
            }

            // Diagonal pencil hatching, clipped to the outline, drawn left to right after the outline is mostly down.
            const float spacing = .026f;
            float cos = Mathf.Cos(52f * Mathf.Deg2Rad), sin = Mathf.Sin(52f * Mathf.Deg2Rad);
            var dir = new Vector2(cos, -sin);          // up-right
            var normal = new Vector2(sin, cos);
            float reach = Aspect + 1f;
            var hits = new List<float>();
            for (float offset = -reach; offset <= reach; offset += spacing)
            {
                var origin = normal * offset + new Vector2(Aspect * .5f, .5f);
                hits.Clear();
                for (int i = 0; i < outline.Length; i++)
                {
                    Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                    if (Intersect(origin, dir, a, b, out float along)) hits.Add(along);
                }
                hits.Sort();
                for (int pair = 0; pair + 1 < hits.Count; pair += 2)
                {
                    float from = hits[pair] + .012f, to = hits[pair + 1] - .012f;
                    from += (float)rng.NextDouble() * .03f;
                    to -= (float)rng.NextDouble() * .03f;
                    if (to - from < .02f) continue;
                    var line = new List<Vector2> { origin + dir * from, origin + dir * to };
                    var pencil = Wobble(Resample(line, .02f), .0016f, rng);
                    float x = (pencil[0].x + pencil[pencil.Length - 1].x) * .5f / Aspect;
                    float start = Mathf.Lerp(.35f, .85f, Mathf.Clamp01(x));
                    strokes.Add(MakeStroke(pencil, .0042f, .16f + (float)rng.NextDouble() * .18f, start, Mathf.Min(1f, start + .14f), false));
                }
            }

            // Capital marker: a small hand-drawn ring and dot near Bratislava, drawn last and solid.
            var capital = Capital;
            var circle = new List<Vector2>();
            const int steps = 20;
            for (int i = 0; i <= steps; i++)
            {
                float angle = i / (float)steps * Mathf.PI * 2.2f + .4f;
                float radius = .026f + (float)Math.Sin(i * .9) * .0015f;
                circle.Add(capital + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            strokes.Add(MakeStroke(circle.ToArray(), .0075f, .95f, .88f, 1f, true));
            strokes.Add(MakeStroke(new[] { capital + new Vector2(-.004f, 0f), capital + new Vector2(.004f, .001f) }, .016f, .95f, .9f, 1f, true));
            return strokes;
        }

        /// <summary>Visible prefix of a stroke for a draw-on progress. Returns the number of points written, 0 if hidden.</summary>
        public static int VisiblePoints(PencilStroke stroke, float draw01, List<Vector2> buffer)
        {
            buffer.Clear();
            float span = Mathf.Max(.0001f, stroke.RevealEnd - stroke.RevealStart);
            float local = Mathf.Clamp01((draw01 - stroke.RevealStart) / span);
            if (local <= 0f) return 0;
            float target = stroke.Lengths[stroke.Lengths.Length - 1] * local;
            buffer.Add(stroke.Points[0]);
            for (int i = 1; i < stroke.Points.Length; i++)
            {
                if (stroke.Lengths[i] <= target) { buffer.Add(stroke.Points[i]); continue; }
                float segment = stroke.Lengths[i] - stroke.Lengths[i - 1];
                float k = segment <= 0f ? 0f : (target - stroke.Lengths[i - 1]) / segment;
                buffer.Add(Vector2.Lerp(stroke.Points[i - 1], stroke.Points[i], k));
                break;
            }
            return buffer.Count;
        }

        private static PencilStroke MakeStroke(Vector2[] points, float width, float alpha, float start, float end, bool accent)
        {
            var lengths = new float[points.Length];
            for (int i = 1; i < points.Length; i++) lengths[i] = lengths[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            return new PencilStroke { Points = points, Lengths = lengths, Width = width, Alpha = alpha, RevealStart = start, RevealEnd = end, IsAccent = accent };
        }

        private static Vector2[] Resample(List<Vector2> polyline, float step)
        {
            var result = new List<Vector2> { polyline[0] };
            for (int i = 1; i < polyline.Count; i++)
            {
                Vector2 a = polyline[i - 1], b = polyline[i];
                int pieces = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
                for (int p = 1; p <= pieces; p++) result.Add(Vector2.Lerp(a, b, p / (float)pieces));
            }
            return result.ToArray();
        }

        /// <summary>Smooth, low-frequency perpendicular drift plus a little per-point noise: a hand held line.</summary>
        private static Vector2[] Wobble(Vector2[] points, float amplitude, System.Random rng)
        {
            var result = new Vector2[points.Length];
            float phaseA = (float)rng.NextDouble() * 6.28f, phaseB = (float)rng.NextDouble() * 6.28f;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 prev = points[Mathf.Max(0, i - 1)], next = points[Mathf.Min(points.Length - 1, i + 1)];
                Vector2 tangent = next - prev;
                tangent = tangent.sqrMagnitude < 1e-8f ? Vector2.right : tangent.normalized;
                var perp = new Vector2(-tangent.y, tangent.x);
                float drift = Mathf.Sin(i * .21f + phaseA) * .6f + Mathf.Sin(i * .53f + phaseB) * .4f;
                float noise = ((float)rng.NextDouble() - .5f) * .5f;
                result[i] = points[i] + perp * ((drift + noise) * amplitude);
            }
            return result;
        }

        /// <summary>Ray (origin + dir*along) against segment a-b; true when it crosses the half-open segment.</summary>
        private static bool Intersect(Vector2 origin, Vector2 dir, Vector2 a, Vector2 b, out float along)
        {
            along = 0f;
            Vector2 edge = b - a;
            float denom = dir.x * edge.y - dir.y * edge.x;
            if (Mathf.Abs(denom) < 1e-7f) return false;
            Vector2 diff = a - origin;
            float t = (diff.x * edge.y - diff.y * edge.x) / denom;
            float u = (diff.x * dir.y - diff.y * dir.x) / denom;
            if (u < 0f || u >= 1f) return false;
            along = t;
            return true;
        }
    }
}
