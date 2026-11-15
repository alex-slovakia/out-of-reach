using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Presentation.Hands
{
    /// <summary>
    /// EVA-TETHER-01: small procedural mesh builders for the original tether asset (rope tube, carabiner loop,
    /// bracket post and base plate). Geometry only; no physics, no colliders.
    /// </summary>
    public static class EvaTetherMeshes
    {
        /// <summary>
        /// Tube along a centre line with rotation-minimizing frames (no twist), UV U around (0..1) and V along
        /// (metres / repeat). Both ends are capped.
        /// </summary>
        public static Mesh Tube(IReadOnlyList<Vector3> centres, IReadOnlyList<Vector3> tangents, float radius, int sides,
            float metresPerRepeat, string name)
        {
            var mesh = new Mesh { name = name };
            int count = centres.Count;
            if (count < 2)
            {
                return mesh;
            }

            sides = Mathf.Max(3, sides);
            var vertices = new List<Vector3>((count * (sides + 1)) + (2 * (sides + 2)));
            var normals = new List<Vector3>(vertices.Capacity);
            var uvs = new List<Vector2>(vertices.Capacity);
            var tangentsOut = new List<Vector4>(vertices.Capacity);
            var triangles = new List<int>(count * sides * 6);
            Vector3 t0 = tangents[0].normalized;
            Vector3 n = Vector3.ProjectOnPlane(Vector3.up, t0);
            if (n.sqrMagnitude < 1e-4f)
            {
                n = Vector3.ProjectOnPlane(Vector3.right, t0);
            }

            n.Normalize();
            float arc = 0f;
            Vector3 previousT = t0;
            for (int i = 0; i < count; i++)
            {
                Vector3 t = tangents[i].normalized;
                if (i > 0)
                {
                    arc += Vector3.Distance(centres[i - 1], centres[i]);
                    n = Quaternion.FromToRotation(previousT, t) * n;
                    n = Vector3.ProjectOnPlane(n, t).normalized;
                }

                Vector3 b = Vector3.Cross(t, n).normalized;
                for (int k = 0; k <= sides; k++)
                {
                    float angle = 2f * Mathf.PI * k / sides;
                    Vector3 direction = (n * Mathf.Cos(angle)) + (b * Mathf.Sin(angle));
                    vertices.Add(centres[i] + (direction * radius));
                    normals.Add(direction);
                    uvs.Add(new Vector2((float)k / sides, arc / Mathf.Max(0.001f, metresPerRepeat)));
                    tangentsOut.Add(new Vector4(t.x, t.y, t.z, 1f));
                }

                previousT = t;
            }

            int ring = sides + 1;
            for (int i = 0; i < count - 1; i++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int a = (i * ring) + k;
                    int c = ((i + 1) * ring) + k;
                    // Unity front faces: cross(b - a, c - a) points outward (towards the viewer).
                    triangles.Add(a);
                    triangles.Add(a + 1);
                    triangles.Add(c);
                    triangles.Add(a + 1);
                    triangles.Add(c + 1);
                    triangles.Add(c);
                }
            }

            Cap(vertices, normals, uvs, tangentsOut, triangles, 0, ring, sides, -tangents[0].normalized, centres[0]);
            Cap(vertices, normals, uvs, tangentsOut, triangles, (count - 1) * ring, ring, sides,
                tangents[count - 1].normalized, centres[count - 1]);
            mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTangents(tangentsOut);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Cap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<Vector4> tangents,
            List<int> triangles, int ringStart, int ring, int sides, Vector3 outward, Vector3 centre)
        {
            int centreIndex = vertices.Count;
            vertices.Add(centre);
            normals.Add(outward);
            uvs.Add(new Vector2(0.5f, 0f));
            tangents.Add(new Vector4(1f, 0f, 0f, 1f));
            int first = vertices.Count;
            for (int k = 0; k < sides; k++)
            {
                vertices.Add(vertices[ringStart + k]);
                normals.Add(outward);
                uvs.Add(new Vector2((float)k / sides, 0.02f));
                tangents.Add(new Vector4(1f, 0f, 0f, 1f));
            }

            for (int k = 0; k < sides; k++)
            {
                int a = first + k;
                int b = first + ((k + 1) % sides);
                Vector3 face = Vector3.Cross(vertices[a] - centre, vertices[b] - centre);
                if (Vector3.Dot(face, outward) >= 0f)
                {
                    triangles.Add(centreIndex);
                    triangles.Add(a);
                    triangles.Add(b);
                }
                else
                {
                    triangles.Add(centreIndex);
                    triangles.Add(b);
                    triangles.Add(a);
                }
            }
        }

        /// <summary>
        /// Adds a closed stadium-shaped loop (a carabiner body) to a mesh builder: centred at <paramref name="centre"/>,
        /// long axis <paramref name="longAxis"/>, loop plane spanned by the long and short axes.
        /// </summary>
        public static void AddLoop(MeshBuilder builder, Vector3 centre, Vector3 longAxis, Vector3 shortAxis,
            float halfLength, float halfWidth, float wireRadius, int pathSegments, int sides)
        {
            Vector3 l = longAxis.normalized;
            Vector3 s = Vector3.ProjectOnPlane(shortAxis, l).normalized;
            float straight = Mathf.Max(0f, halfLength - halfWidth);
            var path = new List<Vector3>();
            int arcSegments = Mathf.Max(4, pathSegments / 2);
            for (int i = 0; i <= arcSegments; i++)
            {
                float angle = Mathf.PI * i / arcSegments;
                path.Add(centre + (l * (straight + (halfWidth * Mathf.Sin(angle)))) + (s * (halfWidth * Mathf.Cos(angle))));
            }

            for (int i = 0; i <= arcSegments; i++)
            {
                float angle = Mathf.PI * i / arcSegments;
                path.Add(centre - (l * (straight + (halfWidth * Mathf.Sin(angle)))) - (s * (halfWidth * Mathf.Cos(angle))));
            }

            builder.AddClosedTube(path, wireRadius, sides);
        }

        /// <summary>Accumulates simple closed shapes into one mesh.</summary>
        public sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();

            public int VertexCount => vertices.Count;

            /// <summary>A closed tube along a looped path (last point joins the first).</summary>
            public void AddClosedTube(IReadOnlyList<Vector3> path, float radius, int sides)
            {
                int count = path.Count;
                if (count < 3)
                {
                    return;
                }

                int start = vertices.Count;
                Vector3 planeNormal = Vector3.zero;
                for (int i = 0; i < count; i++)
                {
                    planeNormal += Vector3.Cross(path[i], path[(i + 1) % count]);
                }

                planeNormal = planeNormal.sqrMagnitude > 1e-12f ? planeNormal.normalized : Vector3.up;
                for (int i = 0; i < count; i++)
                {
                    Vector3 t = (path[(i + 1) % count] - path[(i - 1 + count) % count]).normalized;
                    Vector3 n = Vector3.Cross(planeNormal, t).normalized;
                    for (int k = 0; k < sides; k++)
                    {
                        float angle = 2f * Mathf.PI * k / sides;
                        Vector3 direction = (n * Mathf.Cos(angle)) + (planeNormal * Mathf.Sin(angle));
                        vertices.Add(path[i] + (direction * radius));
                        normals.Add(direction);
                        uvs.Add(new Vector2((float)k / sides, (float)i / count));
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    for (int k = 0; k < sides; k++)
                    {
                        int k1 = (k + 1) % sides;
                        int a = start + (i * sides) + k;
                        int b = start + (i * sides) + k1;
                        int c = start + (next * sides) + k;
                        int d = start + (next * sides) + k1;
                        AddOriented(a, c, b, normals[a] + normals[b] + normals[c]);
                        AddOriented(b, c, d, normals[b] + normals[c] + normals[d]);
                    }
                }
            }

            /// <summary>A capped cylinder between two points.</summary>
            public void AddCylinder(Vector3 from, Vector3 to, float radius, int sides)
            {
                Vector3 axis = to - from;
                if (axis.sqrMagnitude < 1e-8f)
                {
                    return;
                }

                Vector3 t = axis.normalized;
                Vector3 n = Vector3.ProjectOnPlane(Vector3.up, t);
                if (n.sqrMagnitude < 1e-4f)
                {
                    n = Vector3.ProjectOnPlane(Vector3.right, t);
                }

                n.Normalize();
                Vector3 b = Vector3.Cross(t, n);
                int start = vertices.Count;
                for (int ringIndex = 0; ringIndex < 2; ringIndex++)
                {
                    Vector3 centre = ringIndex == 0 ? from : to;
                    for (int k = 0; k < sides; k++)
                    {
                        float angle = 2f * Mathf.PI * k / sides;
                        Vector3 direction = (n * Mathf.Cos(angle)) + (b * Mathf.Sin(angle));
                        vertices.Add(centre + (direction * radius));
                        normals.Add(direction);
                        uvs.Add(new Vector2((float)k / sides, ringIndex));
                    }
                }

                for (int k = 0; k < sides; k++)
                {
                    int k1 = (k + 1) % sides;
                    int a = start + k;
                    int bIndex = start + k1;
                    int c = start + sides + k;
                    int d = start + sides + k1;
                    AddOriented(a, c, bIndex, normals[a] + normals[bIndex] + normals[c]);
                    AddOriented(bIndex, c, d, normals[bIndex] + normals[c] + normals[d]);
                }

                AddDisc(from, -t, radius, sides);
                AddDisc(to, t, radius, sides);
            }

            /// <summary>A flat disc facing <paramref name="normal"/> (single-sided, outward).</summary>
            public void AddDisc(Vector3 centre, Vector3 normal, float radius, int sides)
            {
                Vector3 up = normal.normalized;
                Vector3 n = Vector3.ProjectOnPlane(Vector3.up, up);
                if (n.sqrMagnitude < 1e-4f)
                {
                    n = Vector3.ProjectOnPlane(Vector3.right, up);
                }

                n.Normalize();
                Vector3 b = Vector3.Cross(up, n);
                int centreIndex = vertices.Count;
                vertices.Add(centre);
                normals.Add(up);
                uvs.Add(new Vector2(0.5f, 0.5f));
                for (int k = 0; k < sides; k++)
                {
                    float angle = 2f * Mathf.PI * k / sides;
                    Vector3 direction = (n * Mathf.Cos(angle)) + (b * Mathf.Sin(angle));
                    vertices.Add(centre + (direction * radius));
                    normals.Add(up);
                    uvs.Add(new Vector2(0.5f + (0.5f * Mathf.Cos(angle)), 0.5f + (0.5f * Mathf.Sin(angle))));
                }

                for (int k = 0; k < sides; k++)
                {
                    int a = centreIndex + 1 + k;
                    int bIndex = centreIndex + 1 + ((k + 1) % sides);
                    AddOriented(centreIndex, a, bIndex, up);
                }
            }

            /// <summary>A thin plate (short capped cylinder) lying on a surface.</summary>
            public void AddPlate(Vector3 surfacePoint, Vector3 surfaceNormal, float radius, float thickness, int sides)
            {
                Vector3 n = surfaceNormal.normalized;
                AddCylinder(surfacePoint - (n * 0.002f), surfacePoint + (n * thickness), radius, sides);
            }

            private void AddOriented(int a, int b, int c, Vector3 outward)
            {
                Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                // Unity front faces: cross(b - a, c - a) points outward, towards the viewer.
                if (Vector3.Dot(face, outward) >= 0f)
                {
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                }
                else
                {
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                }
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
