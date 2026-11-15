#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Presentation.LaunchGrassV04
{
    /// <summary>
    /// LAUNCH-TERRAIN-V04: pure geometry for the reworked launch landscape (no Unity object APIs, so it also runs
    /// outside the Editor for offline checks). Fictional Earth scenery; all coordinates are site-local metres.
    ///
    /// The launch site keeps its ground exactly: every triangle of the existing LT_TerrainInner within
    /// <see cref="CutRadius"/> is copied unchanged (pad hole, apron, crawlerway, micro relief, the pampas field).
    /// Beyond that ring a new surface is stitched vertex-for-vertex to the cut loop and replaces both the outer part
    /// of LT_TerrainInner and the TERRAIN-V03 far land:
    ///   0.5-2.6 km  rolling steppe with long low ridges, shallow draws and a few rocky knolls;
    ///   a dry braided river that winds past the site 0.7-1.6 km to the east, with banks, a terrace and gravel bars,
    ///   and leaves through gaps in the ranges;
    ///   2.3-5.5 km  alluvial fans rising into spurred foothills;
    ///   5-10.4 km   a front range, a long valley and a higher main range (layered ridgelines in the haze).
    /// The sector around the crawlerway and the assembly building (towards -Z, out to 1.3 km) keeps the old
    /// heights, sampled from the old surface, so nothing that stands there is buried or left hanging.
    /// </summary>
    public static class LaunchTerrainV04Builder
    {
        public const float CentreX = 0f, CentreZ = 5f, Datum = -36.6f;
        public const float CutRadius = 470f, OuterRadius = 10400f;
        private const float MorphRadius = 560f, RangeRadius = 5000f, OldSampleRadius = 1700f, OldCell = 3f;
        private const int MorphRings = 8, MidCount = 768, FarCount = 1536, Sectors = 16;

        public sealed class Part
        {
            public string Name; public Vector3[] Vertices; public Vector3[] Normals; public int[] Triangles;
        }

        public sealed class Result
        {
            public Part[] Parts;
            public int NearTriangles, LandTriangles, LandVertices, LoopVertices, Rings, OldSamplesMissing;
            public float MinY, MaxY, MaxSeamError;
            public string Report =>
                "parts=" + Parts.Length + " nearTris=" + NearTriangles + " landTris=" + LandTriangles + " landVerts=" + LandVertices
                + " loop=" + LoopVertices + " rings=" + Rings + " y=" + MinY.ToString("F1") + ".." + MaxY.ToString("F1")
                + " oldSamplesMissing=" + OldSamplesMissing + " maxSeamError=" + MaxSeamError.ToString("F4");
        }

        // ------------------------------------------------------------------ noise

        private static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)s * 0xcb1ab31fu;
                h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12; h *= 0x297a2d39u; h ^= h >> 15;
                return (h & 0xffffff) / 16777216f;
            }
        }

        private static float Grad(int ix, int iz, int seed, float dx, float dz)
        {
            float a = Hash(ix, iz, seed) * 6.2831853f;
            return Mathf.Cos(a) * dx + Mathf.Sin(a) * dz;
        }

        /// <summary>Gradient noise, 0..1, mean .5.</summary>
        private static float Noise(float x, float z, int seed)
        {
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
            float fx = x - ix, fz = z - iz;
            float u = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f), v = fz * fz * fz * (fz * (fz * 6f - 15f) + 10f);
            float n00 = Grad(ix, iz, seed, fx, fz), n10 = Grad(ix + 1, iz, seed, fx - 1f, fz);
            float n01 = Grad(ix, iz + 1, seed, fx, fz - 1f), n11 = Grad(ix + 1, iz + 1, seed, fx - 1f, fz - 1f);
            return Mathf.Clamp01(Mathf.Lerp(Mathf.Lerp(n00, n10, u), Mathf.Lerp(n01, n11, u), v) * .7071f + .5f);
        }

        private static float Fbm(float x, float z, int octaves, int seed)
        {
            float sum = 0, amp = .5f, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Noise(x, z, seed + i * 17); norm += amp;
                float nx = x * 1.62f - z * 1.18f + 19.3f, nz = x * 1.18f + z * 1.62f + 7.1f;
                x = nx; z = nz; amp *= .5f;
            }
            return sum / norm;
        }

        /// <summary>Fbm re-centred and stretched to about -1..1 (gradient fbm clusters tightly around .5).</summary>
        private static float Bi(float fbm) => Mathf.Clamp((fbm - .5f) * 3.6f, -1f, 1f);

        private static float Ridged(float x, float z, int octaves, int seed)
        {
            float sum = 0, amp = .5f, norm = 0, weight = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = Mathf.Clamp(.5f + (Noise(x, z, seed + i * 13) - .5f) * 1.9f, 0f, 1f);
                float s = 1f - Mathf.Abs(2f * n - 1f);
                s = s * s * weight;
                weight = Mathf.Clamp01(s * 1.8f);
                sum += s * amp; norm += amp;
                float nx = x * 2.07f - z * .31f + 29.7f, nz = x * .31f + z * 2.07f + 3.9f;
                x = nx; z = nz; amp *= .52f;
            }
            return Mathf.Clamp01(sum / norm * 1.35f);
        }

        private static float S(float low, float high, float value)
        {
            float t = Mathf.Clamp01((value - low) / (high - low));
            return t * t * (3f - 2f * t);
        }

        private static float SmoothMax(float a, float b, float k) => .5f * (a + b + Mathf.Sqrt((a - b) * (a - b) + k * k));

        // ------------------------------------------------------------------ landforms

        // Rocky hogback ridges on the plain: distance (m), bearing (degrees from +X towards +Z), height, half-length
        // along the regional grain, half-width across it, seed. None inside the kept sector towards -Z.
        private static readonly float[,] Knolls =
        {
            { 1250f, 205f, 185f, 520f, 250f, 1f }, { 1700f, 160f, 230f, 760f, 320f, 2f }, { 1500f, 118f, 175f, 480f, 240f, 3f },
            { 2300f, 135f, 260f, 900f, 400f, 4f }, { 1900f, 232f, 170f, 640f, 290f, 5f }, { 2600f, 15f, 220f, 820f, 370f, 6f },
            { 3000f, 60f, 240f, 900f, 410f, 7f }, { 2450f, 318f, 210f, 700f, 320f, 8f }, { 2100f, 185f, 140f, 560f, 260f, 9f },
            { 2000f, 338f, 200f, 700f, 320f, 10f }, { 1650f, 52f, 170f, 600f, 280f, 11f }, { 2700f, 290f, 240f, 880f, 400f, 12f },
        };
        private const float GrainCos = .8192f, GrainSin = .5736f;      // regional grain: ridges trend 35 degrees from +X towards +Z
        private const float BasinX = 900f, BasinZ = 300f;              // the basin's centre lies east of the site: hills are closer in the west

        /// <summary>Distance (m) from the dry river's centre line. Public for the installer's report and checks.</summary>
        public static float RiverDistance(float x, float z)
        {
            float X = x - CentreX, Z = z - CentreZ;
            float wx = X + Bi(Fbm(X / 700f + 31f, Z / 700f + 17f, 2, 21)) * 130f;
            float wz = Z + Bi(Fbm(X / 700f + 3f, Z / 700f + 77f, 2, 22)) * 130f;
            const float u0 = .2496f, u1 = .9683f;                      // downstream axis: north-north-east to south-south-west
            float a = wx * u0 + wz * u1, b = wx * u1 - wz * u0;
            float centre = 980f + 300f * Mathf.Sin(a / 900f + 1.3f) + 130f * Mathf.Sin(a / 370f + .4f);
            return Mathf.Abs(b - centre);
        }

        /// <summary>Height above the datum (m) of the new landscape at a site-local position.</summary>
        public static float Height(float x, float z)
        {
            float X = x - CentreX, Z = z - CentreZ;
            float r = Mathf.Sqrt(X * X + Z * Z);
            float ax = X * GrainCos + Z * GrainSin, az = -X * GrainSin + Z * GrainCos;

            // Steppe: long low swells and ridges along the grain, finer undulation near the site.
            float swell = Bi(Fbm(ax / 2300f + 3.1f, az / 950f + 7.7f, 4, 11));
            float crest = 1f - Mathf.Abs(Bi(Fbm(ax / 1500f + 9.3f, az / 520f + 1.9f, 3, 12)));
            crest = Mathf.Pow(Mathf.Clamp01((crest - .68f) / .32f), 1.4f);
            // The plain also rises gently away from the site, so its forms tilt towards a viewer on the platform
            // and the river can cut 30 m without reaching the old terrain's lowest ground.
            float h = 14f * S(700f, 1600f, r) + 24f * swell * S(540f, 1700f, r) + 42f * crest * S(700f, 2000f, r)
                + 4f * Bi(Fbm(X / 330f + 5f, Z / 330f + 2f, 3, 13)) * S(520f, 1300f, r)
                + 1.1f * Bi(Fbm(X / 70f + 1.7f, Z / 70f + 8.8f, 3, 14)) * S(480f, 800f, r);

            // Dry braided river: broad valley, incised bed with banks, one terrace, gravel bars.
            float dist = RiverDistance(x, z);
            float bedHalf = 55f + 30f * Fbm((X * .2496f + Z * .9683f) / 400f + 5f, 1.5f, 2, 23);
            float valley = Mathf.Pow(1f - S(0f, 600f, dist), 1.5f);
            float channel = 1f - S(bedHalf, bedHalf + 20f, dist);
            float terrace = 1f - S(bedHalf + 75f, bedHalf + 92f, dist);
            float bars = Mathf.Clamp01((Fbm(X / 42f + 3f, Z / 42f + 9f, 3, 24) - .52f) * 4f) * 2.4f * channel;
            h -= (26f * valley + 8f * channel + 3f * terrace - bars) * S(560f, 900f, r);
            // Shallow draws that drain the plain.
            float draw = Bi(Fbm(X / 520f + 11f, Z / 520f + 5f, 3, 31));
            h -= (1f - S(0f, .14f, Mathf.Abs(draw))) * 4.5f * S(700f, 1500f, r) * (1f - channel);

            // Hogback ridges (the river always wins where they would meet).
            float corridor = 1f - S(0f, 320f, dist);
            for (int k = 0; k < Knolls.GetLength(0); k++)
            {
                float bearing = Knolls[k, 1] * Mathf.Deg2Rad, seed = Knolls[k, 5];
                float dx = X - Knolls[k, 0] * Mathf.Cos(bearing), dz = Z - Knolls[k, 0] * Mathf.Sin(bearing);
                float along = (dx * GrainCos + dz * GrainSin) / Knolls[k, 3], across = (-dx * GrainSin + dz * GrainCos) / Knolls[k, 4];
                float d2 = along * along + across * across;
                if (d2 > 4.5f) continue;
                float rid = Ridged(along * 1.5f + seed * 7.3f, across * 1.5f + seed * 3.1f, 3, 40 + (int)seed);
                // Asymmetric: a steep scarp on one side, a long dip slope on the other.
                float scarp = Mathf.Exp(-d2 * 1.9f) * (1f - .35f * Mathf.Clamp(across, -1f, 1f));
                h += Knolls[k, 2] * scarp * (.62f + .38f * rid) * (1f - .9f * corridor);
            }

            // The ranges ring a basin whose centre lies east of the site, so they stand closer in the west and north.
            float bX = X - BasinX, bZ = Z - BasinZ;
            float rb = Mathf.Sqrt(bX * bX + bZ * bZ);
            float inv = rb > 1e-3f ? 1f / rb : 0f, ca = bX * inv, sa = bZ * inv;
            float env = Bi(Fbm(ca * 1.5f + 71.3f, sa * 1.5f + 23.7f, 3, 51)) * .5f + .5f;
            float front = 4300f + Bi(Fbm(ca * 2.2f + 5.1f, sa * 2.2f + 91.4f, 2, 52)) * 800f;
            // Alluvial fans and dissected foothills (dendritic, not radial).
            float bajada = 170f * Mathf.Pow(S(front - 2200f, front + 500f, rb), 1.4f);
            float wsx = X + Bi(Fbm(X / 1700f + 3.3f, Z / 1700f + 9.1f, 3, 55)) * 520f;
            float wsz = Z + Bi(Fbm(X / 1700f + 13.3f, Z / 1700f + 2.1f, 3, 56)) * 520f;
            float spurs = Ridged(wsx / 1500f + 40.3f, wsz / 1500f + 12.9f, 4, 57);
            float foothills = bajada * (.3f + .7f * spurs) + 60f * S(front - 1800f, front, rb) * Bi(Fbm(X / 900f + 7f, Z / 900f + 3f, 4, 58));
            // Two ranges with a long valley between them; ridge lines follow the regional grain.
            float massif = Mathf.Exp(-2f * (1f + ca) / .25f);
            float rangeHeight = 380f + 1200f * S(.25f, .8f, env) + 420f * massif;
            float rise1 = S(front - 900f, front + 700f, rb);
            float mainFront = front + 2300f + Bi(Fbm(ca * 3.1f + 9.9f, sa * 3.1f + 4.4f, 2, 59)) * 450f;
            float rise2 = S(mainFront - 1100f, mainFront + 900f, rb);
            float tr = (rb - (front + 1500f)) / 520f;
            float trough = 1f - .5f * Mathf.Exp(-tr * tr);
            float gx = ax + Bi(Fbm(X / 5200f + 13.3f, Z / 5200f + 7.7f, 2, 60)) * 1200f;
            float gz = az + Bi(Fbm(X / 5200f + 41.9f, Z / 5200f + 63.1f, 2, 61)) * 1200f;
            float ridges = Ridged(gx / 6200f + 101.7f, gz / 2700f + 55.3f, 6, 62);
            float ridges2 = Ridged(gx / 1500f + 31.7f, gz / 1100f + 15.3f, 4, 63);
            float mountains = (.5f * rise1 + .5f * rise2) * trough * rangeHeight * (.14f + .68f * ridges + .18f * ridges * ridges2);
            float fall = 1f - .55f * S(9600f, OuterRadius, r);
            float gap = Mathf.Pow(1f - S(0f, 420f + .1f * r, dist), 1.2f);  // the river leaves through gaps in the ranges
            h += (foothills + mountains) * fall * (1f - .88f * gap);

            // Other sets of the scene lie 7.8 km towards -X below the datum: keep a rough massif over them.
            float ox = X + 7800f, oz = Z - 150f;
            float bury = 1f - S(500f, 1500f, Mathf.Sqrt(ox * ox + oz * oz));
            h = SmoothMax(h, (300f + 260f * ridges2) * bury - 2000f * (1f - bury), 30f);
            return SmoothMax(h, -20f, 3f);                                  // never below the old terrain's lowest ground
        }

        // ------------------------------------------------------------------ old surface

        private sealed class OldSurface
        {
            private readonly float[] h; private readonly int size; private readonly float originX, originZ;

            public OldSurface(Vector3[] v, int[] t)
            {
                size = Mathf.CeilToInt(OldSampleRadius * 2f / OldCell) + 2;
                originX = CentreX - OldSampleRadius; originZ = CentreZ - OldSampleRadius;
                h = new float[size * size];
                for (int i = 0; i < h.Length; i++) h[i] = float.NaN;
                for (int k = 0; k + 2 < t.Length; k += 3) Raster(v[t[k]], v[t[k + 1]], v[t[k + 2]]);
            }

            private void Raster(Vector3 a, Vector3 b, Vector3 c)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - originX) / OldCell));
                int x1 = Mathf.Min(size - 1, Mathf.CeilToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - originX) / OldCell));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - originZ) / OldCell));
                int z1 = Mathf.Min(size - 1, Mathf.CeilToInt((Mathf.Max(a.z, Mathf.Max(b.z, c.z)) - originZ) / OldCell));
                if (x1 < x0 || z1 < z0) return;
                float det = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(det) < 1e-9f) return;
                for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    float px = originX + x * OldCell, pz = originZ + z * OldCell;
                    float w0 = ((b.z - c.z) * (px - c.x) + (c.x - b.x) * (pz - c.z)) / det;
                    float w1 = ((c.z - a.z) * (px - c.x) + (a.x - c.x) * (pz - c.z)) / det;
                    float w2 = 1f - w0 - w1;
                    if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                    h[z * size + x] = w0 * a.y + w1 * b.y + w2 * c.y;
                }
            }

            public bool Sample(float x, float z, out float y)
            {
                y = 0f;
                float gx = (x - originX) / OldCell, gz = (z - originZ) / OldCell;
                int ix = Mathf.FloorToInt(gx), iz = Mathf.FloorToInt(gz);
                if (ix < 0 || iz < 0 || ix >= size - 1 || iz >= size - 1) return false;
                float fx = gx - ix, fz = gz - iz;
                int i = iz * size + ix;
                float a = h[i], b = h[i + 1], c = h[i + size], d = h[i + size + 1];
                if (float.IsNaN(a) || float.IsNaN(b) || float.IsNaN(c) || float.IsNaN(d)) return false;
                y = Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
                return true;
            }
        }

        // ------------------------------------------------------------------ build

        private static float Radius(Vector3 p) { float x = p.x - CentreX, z = p.z - CentreZ; return Mathf.Sqrt(x * x + z * z); }
        private static float Angle(Vector3 p) => Mathf.Atan2(p.z - CentreZ, p.x - CentreX);

        /// <summary>How much of the new landscape applies at a point beyond the cut loop (0 = old heights, 1 = new).</summary>
        private static float NewWeight(float angle, float radius, float loopRadius)
        {
            float fromLoop = S(0f, 280f, radius - loopRadius);
            float off = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, -90f));   // crawlerway and assembly building: -Z
            float outsideSector = Mathf.Max(S(17f, 30f, off), S(1320f, 1560f, radius));
            return fromLoop * outsideSector;
        }

        /// <param name="oldVertices">LT_TerrainInner vertices in site-local space.</param>
        /// <param name="oldNormals">Their normals in site-local space.</param>
        /// <param name="oldTriangles">Triangle list (upward facing).</param>
        public static Result Build(Vector3[] oldVertices, Vector3[] oldNormals, int[] oldTriangles)
        {
            if (oldVertices == null || oldNormals == null || oldTriangles == null || oldVertices.Length != oldNormals.Length)
                throw new ArgumentException("The old terrain needs vertices, normals and triangles.");
            var result = new Result();
            var parts = new List<Part>();

            // ---- 1. the unchanged near ground, and its outer boundary loop
            var radius = new float[oldVertices.Length];
            float maxRadius = 0f;
            for (int i = 0; i < radius.Length; i++) { radius[i] = Radius(oldVertices[i]); maxRadius = Mathf.Max(maxRadius, radius[i]); }
            if (maxRadius < 2500f) throw new InvalidOperationException("Unexpected LT_TerrainInner extent: r=" + maxRadius);
            var nearRemap = new Dictionary<int, int>();
            var nearVertices = new List<Vector3>(); var nearNormals = new List<Vector3>(); var nearTriangles = new List<int>();
            var edgeUse = new Dictionary<long, int>();
            for (int k = 0; k + 2 < oldTriangles.Length; k += 3)
            {
                int a = oldTriangles[k], b = oldTriangles[k + 1], c = oldTriangles[k + 2];
                if (radius[a] >= CutRadius || radius[b] >= CutRadius || radius[c] >= CutRadius) continue;
                foreach (int v in new[] { a, b, c })
                {
                    if (!nearRemap.TryGetValue(v, out int mapped))
                    {
                        mapped = nearVertices.Count; nearRemap.Add(v, mapped);
                        nearVertices.Add(oldVertices[v]); nearNormals.Add(oldNormals[v]);
                    }
                    nearTriangles.Add(mapped);
                }
                CountEdge(edgeUse, a, b); CountEdge(edgeUse, b, c); CountEdge(edgeUse, c, a);
            }
            parts.Add(new Part { Name = "LTV4 near ground", Vertices = nearVertices.ToArray(), Normals = nearNormals.ToArray(), Triangles = nearTriangles.ToArray() });
            result.NearTriangles = nearTriangles.Count / 3;

            var next = new Dictionary<int, List<int>>();
            foreach (KeyValuePair<long, int> e in edgeUse)
            {
                if (e.Value != 1) continue;
                int a = (int)(e.Key >> 32), b = (int)(e.Key & 0xffffffff);
                if (radius[a] < CutRadius * .5f || radius[b] < CutRadius * .5f) continue;       // the pad hole's loop
                Link(next, a, b); Link(next, b, a);
            }
            if (next.Count < 64) throw new InvalidOperationException("Unexpected cut loop: " + next.Count + " vertices.");
            int start = -1;
            foreach (KeyValuePair<int, List<int>> e in next)
            {
                if (e.Value.Count != 2) throw new InvalidOperationException("The cut at " + CutRadius + " m is not a simple loop.");
                if (start < 0) start = e.Key;
            }
            var loop = new List<int> { start };
            for (int previous = -1, current = start; ;)
            {
                List<int> n = next[current];
                int step = n[0] != previous ? n[0] : n[1];
                if (step == start) break;
                loop.Add(step); previous = current; current = step;
                if (loop.Count > next.Count) throw new InvalidOperationException("The cut loop does not close.");
            }
            if (loop.Count != next.Count) throw new InvalidOperationException("The cut is more than one loop.");
            // Counter-clockwise by angle, starting at the smallest angle.
            int first = 0;
            for (int i = 1; i < loop.Count; i++) if (Angle(oldVertices[loop[i]]) < Angle(oldVertices[loop[first]])) first = i;
            bool ccw = Mathf.DeltaAngle(Angle(oldVertices[loop[first]]) * Mathf.Rad2Deg, Angle(oldVertices[loop[(first + 1) % loop.Count]]) * Mathf.Rad2Deg) > 0f;
            var ring0 = new int[loop.Count];
            for (int i = 0; i < loop.Count; i++) ring0[i] = loop[((first + (ccw ? i : -i)) % loop.Count + loop.Count) % loop.Count];
            var loopAngle = new float[ring0.Length]; var loopRadius = new float[ring0.Length];
            for (int i = 0; i < ring0.Length; i++)
            {
                loopAngle[i] = Angle(oldVertices[ring0[i]]); loopRadius[i] = radius[ring0[i]];
                if (i > 0 && loopAngle[i] <= loopAngle[i - 1]) throw new InvalidOperationException("The cut loop is not star-shaped around the site centre.");
            }
            result.LoopVertices = ring0.Length;

            // ---- 2. rings of the new surface
            var old = new OldSurface(oldVertices, OldTrianglesWithin(oldVertices, oldTriangles, radius));
            var vertices = new List<Vector3>(300000); var fixedNormal = new List<bool>(300000); var normals = new List<Vector3>(300000);
            var ringStart = new List<int>(); var ringCount = new List<int>(); var ringAngles = new List<float[]>();
            ringStart.Add(0); ringCount.Add(ring0.Length); ringAngles.Add(loopAngle);
            for (int i = 0; i < ring0.Length; i++) { vertices.Add(oldVertices[ring0[i]]); normals.Add(oldNormals[ring0[i]]); fixedNormal.Add(true); }

            var radii = new List<float>(); var counts = new List<int>();
            for (int k = 1; k <= MorphRings; k++) { radii.Add(-k / (float)MorphRings); counts.Add(MidCount); }   // negative: morph fraction
            for (float r = MorphRadius * 1.013f; r < RangeRadius; r *= 1.013f) { radii.Add(r); counts.Add(MidCount); }
            for (float r = RangeRadius; r <= OuterRadius + 1f; r += 70f) { radii.Add(r); counts.Add(FarCount); }
            int missing = 0;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int ring = 0; ring < radii.Count; ring++)
            {
                int count = counts[ring];
                var angles = new float[count];
                ringStart.Add(vertices.Count); ringCount.Add(count); ringAngles.Add(angles);
                for (int j = 0; j < count; j++)
                {
                    float angle = -Mathf.PI + (j + .5f) * Mathf.PI * 2f / count;
                    angles[j] = angle;
                    float edge = LoopValue(loopAngle, loopRadius, angle);
                    float r = radii[ring] < 0f ? Mathf.Lerp(edge, MorphRadius, -radii[ring]) : radii[ring];
                    float x = CentreX + Mathf.Cos(angle) * r, z = CentreZ + Mathf.Sin(angle) * r;
                    float w = NewWeight(angle, r, edge);
                    float y = Datum + Height(x, z);
                    if (w < .9995f)
                    {
                        if (old.Sample(x, z, out float oldY)) y = Mathf.Lerp(oldY, y, w);
                        else missing++;
                    }
                    vertices.Add(new Vector3(x, y, z)); normals.Add(Vector3.zero); fixedNormal.Add(false);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            }
            result.Rings = radii.Count + 1; result.OldSamplesMissing = missing; result.MinY = minY; result.MaxY = maxY;
            result.LandVertices = vertices.Count;

            // ---- 3. triangles, ring to ring, upward facing
            var triangles = new List<int>(600000);
            var triangleRing = new List<int>(200000);
            for (int ring = 0; ring + 1 < ringStart.Count; ring++)
            {
                int before = triangles.Count;
                Zip(vertices, ringStart[ring], ringAngles[ring], ringStart[ring + 1], ringAngles[ring + 1], triangles);
                for (int k = before; k < triangles.Count; k += 3) triangleRing.Add(ring);
            }
            for (int k = 0; k < triangles.Count; k += 3)
            {
                Vector3 a = vertices[triangles[k]], b = vertices[triangles[k + 1]], c = vertices[triangles[k + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.y < 0f) { int swap = triangles[k + 1]; triangles[k + 1] = triangles[k + 2]; triangles[k + 2] = swap; n = -n; }
                for (int q = 0; q < 3; q++) if (!fixedNormal[triangles[k + q]]) normals[triangles[k + q]] += n;
            }
            for (int i = 0; i < normals.Count; i++)
                if (!fixedNormal[i]) normals[i] = normals[i].sqrMagnitude > 1e-12f ? normals[i].normalized : Vector3.up;
            result.LandTriangles = triangles.Count / 3;

            // ---- 4. split into sectors (plain and ranges) for culling
            int rangeRing = 0;
            for (int ring = 0; ring < radii.Count; ring++) if (radii[ring] >= RangeRadius) { rangeRing = ring + 1; break; }
            var buckets = new List<int>[Sectors * 2];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<int>();
            for (int k = 0; k < triangles.Count; k += 3)
            {
                Vector3 centroid = (vertices[triangles[k]] + vertices[triangles[k + 1]] + vertices[triangles[k + 2]]) / 3f;
                int sector = Mathf.Clamp((int)((Angle(centroid) + Mathf.PI) / (Mathf.PI * 2f) * Sectors), 0, Sectors - 1);
                int band = triangleRing[k / 3] >= rangeRing ? 1 : 0;
                List<int> bucket = buckets[band * Sectors + sector];
                bucket.Add(triangles[k]); bucket.Add(triangles[k + 1]); bucket.Add(triangles[k + 2]);
            }
            for (int i = 0; i < buckets.Length; i++)
            {
                if (buckets[i].Count == 0) continue;
                var remap = new Dictionary<int, int>(); var pv = new List<Vector3>(); var pn = new List<Vector3>(); var pt = new int[buckets[i].Count];
                for (int k = 0; k < pt.Length; k++)
                {
                    int v = buckets[i][k];
                    if (!remap.TryGetValue(v, out int mapped)) { mapped = pv.Count; remap.Add(v, mapped); pv.Add(vertices[v]); pn.Add(normals[v]); }
                    pt[k] = mapped;
                }
                parts.Add(new Part
                {
                    Name = (i < Sectors ? "LTV4 plain sector " : "LTV4 ranges sector ") + (i % Sectors).ToString("00"),
                    Vertices = pv.ToArray(), Normals = pn.ToArray(), Triangles = pt,
                });
            }

            // The seam: ring 0 is the old loop itself.
            float seam = 0f;
            for (int i = 0; i < ring0.Length; i++) seam = Mathf.Max(seam, (vertices[i] - oldVertices[ring0[i]]).magnitude);
            result.MaxSeamError = seam;
            result.Parts = parts.ToArray();
            return result;
        }

        private static int[] OldTrianglesWithin(Vector3[] v, int[] t, float[] radius)
        {
            var list = new List<int>();
            for (int k = 0; k + 2 < t.Length; k += 3)
                if (radius[t[k]] < OldSampleRadius + 60f || radius[t[k + 1]] < OldSampleRadius + 60f || radius[t[k + 2]] < OldSampleRadius + 60f)
                { list.Add(t[k]); list.Add(t[k + 1]); list.Add(t[k + 2]); }
            return list.ToArray();
        }

        private static void CountEdge(Dictionary<long, int> use, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            use.TryGetValue(key, out int n); use[key] = n + 1;
        }

        private static void Link(Dictionary<int, List<int>> next, int a, int b)
        {
            if (!next.TryGetValue(a, out List<int> list)) next.Add(a, list = new List<int>(2));
            list.Add(b);
        }

        /// <summary>Linear interpolation of a per-loop-vertex value at an angle (the loop is angle-sorted and closed).</summary>
        private static float LoopValue(float[] angles, float[] values, float angle)
        {
            int n = angles.Length;
            int hi = Array.BinarySearch(angles, angle);
            if (hi >= 0) return values[hi];
            hi = ~hi;
            int lo = (hi - 1 + n) % n; hi %= n;
            float a0 = angles[lo], a1 = angles[hi];
            if (a1 <= a0) a1 += Mathf.PI * 2f;
            float a = angle < a0 ? angle + Mathf.PI * 2f : angle;
            return Mathf.Lerp(values[lo], values[hi], Mathf.Clamp01((a - a0) / (a1 - a0)));
        }

        /// <summary>Stitches two closed, angle-sorted rings (inner A, outer B) into a triangle strip.</summary>
        private static void Zip(List<Vector3> vertices, int startA, float[] angleA, int startB, float[] angleB, List<int> triangles)
        {
            int na = angleA.Length, nb = angleB.Length, i = 0, j = 0;
            const float Tau = Mathf.PI * 2f;
            while (i < na || j < nb)
            {
                // Angle of the next vertex on each ring (the wrap-around vertex is the first one plus a full turn).
                float nextA = i < na ? (i + 1 < na ? angleA[i + 1] : angleA[0] + Tau) : float.MaxValue;
                float nextB = j < nb ? (j + 1 < nb ? angleB[j + 1] : angleB[0] + Tau) : float.MaxValue;
                int a = startA + i % na, b = startB + j % nb;
                if (nextA <= nextB)
                {
                    triangles.Add(a); triangles.Add(startA + (i + 1) % na); triangles.Add(b);
                    i++;
                }
                else
                {
                    triangles.Add(a); triangles.Add(startB + (j + 1) % nb); triangles.Add(b);
                    j++;
                }
            }
        }
    }
}
#endif
