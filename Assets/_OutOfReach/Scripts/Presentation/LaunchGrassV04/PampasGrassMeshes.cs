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

        /// <summary>Full clump: 11 stalks with crossed feather plumes and 6 arching leaves (~250 triangles).</summary>
        public static Mesh BuildNearClump()
        {
            var b = new Builder();
            const int stems = 11, leaves = 6;
            for (int i = 0; i < stems; i++)
            {
                float az = (i + Hash(i, 1) * .7f) / stems * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(az), 0, Mathf.Sin(az));
                Vector3 baseP = outward * (.02f + .12f * Hash(i, 2));
                float lean = Mathf.Lerp(.08f, .34f, Hash(i, 3));
                float height = Mathf.Lerp(.74f, 1f, Hash(i, 4));
                float rnd = Hash(i, 5);
                float droop = Mathf.Lerp(.04f, .17f, Hash(i, 6));
                float stalkTop = .55f;
                System.Func<float, Vector3> stemCurve = v =>
                {
                    float y = v * height;
                    return baseP + outward * (lean * v * height + droop * v * v * v) + Vector3.up * (y - droop * .4f * v * v * v);
                };
                Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;
                b.Ribbon(v => stemCurve(v * stalkTop), v => .011f * (1f - .4f * v), side, 3, PartStalk, rnd, 1);
                // Plume: two crossed ribbons from the stalk top to the tip.
                for (int q = 0; q < 2; q++)
                {
                    Vector3 plumeSide = q == 0 ? side : outward;
                    b.Ribbon(v => stemCurve(Mathf.Lerp(stalkTop - .03f, 1f, v)), v => .075f, plumeSide, 3, PartPlume, rnd, 1);
                }
            }
            for (int i = 0; i < leaves; i++)
            {
                float az = (i + Hash(i, 11)) / leaves * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(az), 0, Mathf.Sin(az));
                float length = Mathf.Lerp(.42f, .78f, Hash(i, 12));
                float rise = Mathf.Lerp(.30f, .55f, Hash(i, 13));
                float reach = Mathf.Lerp(.18f, .42f, Hash(i, 14));
                Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;
                // Arching leaf: rises, then droops back down under its own weight.
                b.Ribbon(v => outward * (reach * v * length / .6f) + Vector3.up * (rise * length / .6f * (v * 1.6f - .9f * v * v)),
                    v => .024f * (1f - v * v), side, 4, PartLeaf, Hash(i, 15), .55f);
            }
            return b.Build("Pampas clump V04 (near)");
        }

        /// <summary>Far card: three crossed vertical quads, three bend segments each (18 triangles).</summary>
        public static Mesh BuildFarCard()
        {
            var b = new Builder();
            for (int q = 0; q < 3; q++)
            {
                float az = q * Mathf.PI / 3f;
                Vector3 side = new Vector3(Mathf.Cos(az), 0, Mathf.Sin(az));
                b.Ribbon(v => Vector3.up * v, v => .5f, side, 3, PartCard, q / 3f + .1f, 1);
            }
            for (int i = 0; i < b.N.Count; i++) b.N[i] = Vector3.up;
            return b.Build("Pampas clump V04 (far card)");
        }

        public static int TriangleCount(Mesh mesh) => mesh ? (int)(mesh.GetIndexCount(0) / 3) : 0;

        /// <summary>Unit inverted hemisphere (radius 1, y >= -0.08) for the haze dome.</summary>
        public static Mesh BuildDome()
        {
            const int rings = 12, segments = 48;
            var p = new List<Vector3>(); var t = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float el = Mathf.Lerp(-.08f, Mathf.PI * .5f, r / (float)rings);
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
            var mesh = new Mesh { name = "Launch haze dome V04", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(p); mesh.SetTriangles(t, 0); mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
