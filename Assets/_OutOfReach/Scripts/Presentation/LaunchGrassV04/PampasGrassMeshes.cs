using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Presentation.LaunchGrassV04
{
    /// <summary>
    /// Original procedural meshes for one pampas clump at unit height (scaled per instance in the shader).
    /// Vertex colour: r = bend weight, g = part (0 leaf, .33 stalk, .66 plume, 1 card), b = per-stem random.
    /// UV: u across the ribbon 0..1, v along the part 0..1.
    /// </summary>
    public static class PampasGrassMeshes
    {
        public const float PartLeaf = 0f, PartStalk = .33f, PartPlume = .66f, PartCard = 1f;

        private sealed class Builder
        {
            public readonly List<Vector3> P = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> U = new List<Vector2>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();

            /// <summary>A ribbon along a curve with a given half-width profile; facing the given side axis.</summary>
            public void Ribbon(System.Func<float, Vector3> curve, System.Func<float, float> halfWidth, Vector3 side,
                int segments, float part, float random, float bendScale)
            {
                int start = P.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float v = i / (float)segments;
                    Vector3 c = curve(v);
                    Vector3 tangent = (curve(Mathf.Min(1f, v + .02f)) - curve(Mathf.Max(0f, v - .02f))).normalized;
                    Vector3 normal = Vector3.Cross(side, tangent).normalized;
                    if (normal.y < 0) normal = -normal;
                    float w = halfWidth(v);
                    for (int s = 0; s < 2; s++)
                    {
                        P.Add(c + side * (s == 0 ? -w : w));
                        N.Add(normal);
                        U.Add(new Vector2(s, v));
                        C.Add(new Color(Mathf.Clamp01(bendScale), part, random, 1));
                    }
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = start + i * 2;
                    T.Add(a); T.Add(a + 2); T.Add(a + 1);
                    T.Add(a + 1); T.Add(a + 2); T.Add(a + 3);
                }
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
                mesh.SetVertices(P); mesh.SetNormals(N); mesh.SetUVs(0, U); mesh.SetColors(C);
                mesh.SetTriangles(T, 0);
                mesh.bounds = new Bounds(new Vector3(0, .6f, 0), new Vector3(2.4f, 1.6f, 2.4f));
                mesh.UploadMeshData(true);
                return mesh;
            }
        }

        private static float Hash(int i, int salt)
        {
            uint h = (uint)(i * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffffff) / 16777216f;
        }

        /// <summary>
        /// Full clump (~380 triangles): a fountain of 22 arching, drooping leaves and 9 bare stalks of different
        /// heights, each carrying a seed head as two crossed quads (aspect 1:2, one cell of PampasPlumesV04.png).
        /// </summary>
        public static Mesh BuildNearClump()
        {
            var b = new Builder();
            const int stems = 9, leaves = 22;
            for (int i = 0; i < stems; i++)
            {
                float az = (i + Hash(i, 1) * .8f) / stems * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(az), 0, Mathf.Sin(az));
                Vector3 baseP = outward * (.02f + .08f * Hash(i, 2));
                float lean = Mathf.Lerp(.05f, .34f, Hash(i, 3));
                float height = i == 0 ? 1f : Mathf.Lerp(.60f, .96f, Hash(i, 4));
                float rnd = Hash(i, 5);
                float droop = Mathf.Lerp(.03f, .12f, Hash(i, 6));
                float headLength = Mathf.Lerp(.30f, .40f, Hash(i, 7));          // in clump heights
                float stalkTop = 1f - headLength / height;                       // where the head starts along the stem
                System.Func<float, Vector3> stemCurve = v =>
                {
                    float y = v * height;
                    return baseP + outward * (lean * v * height + droop * v * v * v) + Vector3.up * (y - droop * .4f * v * v * v);
                };
                Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;
                b.Ribbon(v => stemCurve(v * (stalkTop + .04f)), v => .008f * (1f - .3f * v), side, 3, PartStalk, rnd, 1);
                for (int q = 0; q < 2; q++)
                {
                    Vector3 headSide = q == 0 ? side : outward;
                    b.Ribbon(v => stemCurve(Mathf.Lerp(stalkTop, 1f, v)), v => headLength * .25f, headSide, 3, PartPlume, rnd, 1);
                }
            }
            for (int i = 0; i < leaves; i++)
            {
                float az = (i + Hash(i, 11)) / leaves * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(az), 0, Mathf.Sin(az));
                float reach = Mathf.Lerp(.26f, .60f, Hash(i, 12));               // horizontal reach of the tip
                float rise = Mathf.Lerp(.28f, .54f, Hash(i, 13));                // height of the arch
                Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;
                // Fountain leaf: climbs steeply, arches over at ~58 % of its length and droops to ~45 % of its peak.
                b.Ribbon(v => outward * (reach * (.35f * v + .65f * v * v)) + Vector3.up * (rise * (3.46f * v - 3f * v * v)),
                    v => .02f * (1f - v * v * v), side, 5, PartLeaf, Hash(i, 15), .5f);
            }
            return b.Build("Pampas clump V04 (near)");
        }

        /// <summary>Far card: two crossed vertical quads, three bend segments each (12 triangles).</summary>
        public static Mesh BuildFarCard()
        {
            var b = new Builder();
            for (int q = 0; q < 2; q++)
            {
                float az = q * Mathf.PI / 2f + .4f;
                Vector3 side = new Vector3(Mathf.Cos(az), 0, Mathf.Sin(az));
                b.Ribbon(v => Vector3.up * v, v => .5f, side, 3, PartCard, q / 2f + .1f, 1);
            }
            for (int i = 0; i < b.N.Count; i++) b.N[i] = Vector3.up;
            return b.Build("Pampas clump V04 (far card)");
        }

        public static int TriangleCount(Mesh mesh) => mesh ? (int)(mesh.GetIndexCount(0) / 3) : 0;

        /// <summary>Unit sphere around the camera for the depth fog pass (covers the whole screen).</summary>
        public static Mesh BuildSphere() => BuildDome(-Mathf.PI * .5f, "Launch haze sphere V04");

        /// <summary>Unit hemisphere (radius 1, elevation >= -0.08 rad) for the fallback horizon dome.</summary>
        public static Mesh BuildDome() => BuildDome(-.08f, "Launch haze dome V04");

        private static Mesh BuildDome(float lowestElevation, string name)
        {
            const int rings = 14, segments = 48;
            var p = new List<Vector3>(); var t = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float el = Mathf.Lerp(lowestElevation, Mathf.PI * .5f, r / (float)rings);
                for (int s = 0; s <= segments; s++)
                {
                    float az = s / (float)segments * Mathf.PI * 2f;
                    p.Add(new Vector3(Mathf.Cos(el) * Mathf.Cos(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(az)));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s, c = a + segments + 1;
                    t.Add(a); t.Add(c); t.Add(a + 1); t.Add(a + 1); t.Add(c); t.Add(c + 1);
                }
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(p); mesh.SetTriangles(t, 0); mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
