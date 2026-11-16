using System;
using UnityEngine;

namespace OutOfReach.Presentation.Meal
{
    /// <summary>
    /// The strip torn off the top of the meal pouch (R35): the pouch's own top band as one foil ribbon, re-shaped every
    /// frame from <see cref="MealStripShape"/>. Its cross-section is the sealed pouch's top (two walls that meet in the
    /// flat seal) and its texture coordinates continue the pouch's own, so the swap to the opened variant is not seen.
    /// While the pouch is being torn it stands on the cut where it still holds and peels to the fingers where it has come
    /// off; once free it is carried in the fingers until the hand has left the view. A procedural mesh with the pouch's
    /// own foil material: no new asset, never a collider, presentation only.
    /// </summary>
    internal sealed class MealTearStrip : IDisposable
    {
        public const int Segments = 24;
        private const int Stations = Segments + 1;
        private const int Ring = 3;      // per wall: lower edge, start of the seal, top edge
        private const int Surfaces = 4;  // label-side wall outside and inside, back wall outside and inside

        private readonly MealPouchShape shape;
        private readonly GameObject host;
        private readonly Mesh mesh;
        private readonly Vector3[] vertices, normals;
        // Each wall from its lower edge to the top, in the strip's own (height above the middle line, along the face
        // normal) plane, with the wall's outward direction in the same plane.
        private readonly Vector2[,] wall = new Vector2[2, Ring];
        private readonly Vector2[,] outward = new Vector2[2, Ring];

        public MealTearStrip(Transform parent, Material foil, MealPouchShape pouchShape)
        {
            shape = pouchShape;
            var profile = new Vector2[5];
            MealStripShape.Profile(shape, profile);
            for (int r = 0; r < Ring; r++) { wall[0, r] = profile[r]; wall[1, r] = profile[4 - r]; }
            for (int w = 0; w < 2; w++)
            {
                // Perpendicular of each wall segment, pointing away from the other wall.
                var segment = new Vector2[Ring - 1];
                for (int r = 0; r < Ring - 1; r++)
                {
                    Vector2 d = wall[w, r + 1] - wall[w, r];
                    Vector2 n = new Vector2(-d.y, d.x);
                    if ((w == 0) != (n.y > 0f)) n = -n;
                    segment[r] = n.normalized;
                }
                outward[w, 0] = segment[0];
                outward[w, 1] = (segment[0] + segment[1]).normalized;
                outward[w, 2] = segment[1];
            }

            host = new GameObject("Meal pouch tear strip (presentation only)");
            host.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Meal pouch tear strip" };
            mesh.MarkDynamic();
            int count = Surfaces * Stations * Ring;
            vertices = new Vector3[count];
            normals = new Vector3[count];
            var uv = new Vector2[count];
            var triangles = new int[Surfaces * Segments * (Ring - 1) * 6];
            int t = 0;
            for (int s = 0; s < Surfaces; s++)
            {
                int w = s / 2;
                // Label-side wall outside (0) and back wall inside (3) face the label side; the other two face away.
                bool towardsLabel = s == 0 || s == 3;
                for (int i = 0; i < Stations; i++)
                    for (int r = 0; r < Ring; r++)
                    {
                        // The foil is mapped flat over the pouch in metres (u against x, v along y): the strip continues it.
                        float x = -shape.HalfWidth + i / (float)Segments * shape.Width;
                        uv[Index(s, i, r)] = new Vector2(-x, shape.StripMiddle + wall[w, r].x);
                    }
                for (int i = 0; i < Segments; i++)
                    for (int r = 0; r < Ring - 1; r++)
                    {
                        int a = Index(s, i, r), b = Index(s, i, r + 1), c = Index(s, i + 1, r), d = Index(s, i + 1, r + 1);
                        if (towardsLabel)
                        {
                            triangles[t++] = a; triangles[t++] = d; triangles[t++] = b;
                            triangles[t++] = a; triangles[t++] = c; triangles[t++] = d;
                        }
                        else
                        {
                            triangles[t++] = a; triangles[t++] = b; triangles[t++] = d;
                            triangles[t++] = a; triangles[t++] = d; triangles[t++] = c;
                        }
                    }
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            host.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = foil;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            host.SetActive(false);
        }

        public bool IsVisible => host && host.activeSelf;
        public Transform Transform => host ? host.transform : null;

        /// <summary>Distance of the ribbon's held point from the fingers after the last <c>Present</c> call (0 = in the fingers).</summary>
        public float LastPinchErrorMetres { get; private set; }

        /// <summary>The direction from the fingers to the far (right) end and the strip's height direction, in the world,
        /// after the last call: what a hand carries on when the strip comes free.</summary>
        public Vector3 LastAlong { get; private set; } = Vector3.right;
        public Vector3 LastHeight { get; private set; } = Vector3.up;

        /// <summary>
        /// The strip on a pouch at <paramref name="pouch"/>, torn by <paramref name="tear"/> (0..1) towards the fingers at
        /// <paramref name="pinch"/> (world): it stands on the cut where it still holds and peels to the fingers where not.
        /// </summary>
        public void Present(Pose pouch, float tear, Vector3 pinch)
        {
            if (!host) return;
            if (!host.activeSelf) host.SetActive(true);
            // Aim the torn part at the fingers so it always ends in them; the default peel direction is the ideal.
            Vector3 peel = MealStripShape.PeelDirection;
            Vector3 local = Quaternion.Inverse(pouch.rotation) * (pinch - pouch.position) - Vector3.up * MealStripShape.PinchHeight(shape);
            Vector3 front = MealStripShape.TearFront(shape, tear);
            float torn = Mathf.Clamp01(tear) * shape.Width - MealStripShape.PinchInset;
            if (torn > .010f)
            {
                Vector3 aim = local - (front + Vector3.right * (MealStripShape.BendLength * .5f));
                if (aim.sqrMagnitude > 1e-6f) peel = Vector3.Slerp(peel, aim.normalized, Mathf.Clamp01((torn - .010f) / .020f));
            }
            for (int i = 0; i < Stations; i++)
            {
                MealStripShape.Frame(shape, tear, i / (float)Segments, peel, out Vector3 centre, out _, out Vector3 height, out Vector3 normal);
                Write(i, pouch.position + pouch.rotation * centre, pouch.rotation * height, pouch.rotation * normal);
            }
            float heldAt = 1f - MealStripShape.PinchInset / shape.Width;
            MealStripShape.Frame(shape, tear, heldAt, peel, out Vector3 heldCentre, out _, out Vector3 heldHeight, out _);
            Vector3 held = pouch.position + pouch.rotation * (heldCentre + heldHeight * MealStripShape.PinchHeight(shape));
            LastPinchErrorMetres = Vector3.Distance(held, pinch);
            Vector3 far = MealStripShape.Centre(shape, tear, 0f, peel, out _);
            Vector3 along = pouch.rotation * (far - heldCentre);
            LastAlong = along.sqrMagnitude > 1e-8f ? along.normalized : pouch.rotation * Vector3.left;
            LastHeight = pouch.rotation * heldHeight;
            Commit();
        }

        /// <summary>
        /// The strip free of the pouch: a straight ribbon held near its left end at <paramref name="pinch"/> (world),
        /// running along <paramref name="along"/> with its height along <paramref name="height"/> (world directions).
        /// </summary>
        public void PresentFree(Vector3 pinch, Vector3 along, Vector3 height)
        {
            if (!host) return;
            if (!host.activeSelf) host.SetActive(true);
            along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.right;
            height -= along * Vector3.Dot(height, along);
            height = height.sqrMagnitude > 1e-6f ? height.normalized : Vector3.Cross(along, Vector3.forward).normalized;
            Vector3 normal = Vector3.Cross(-along, height).normalized;
            Vector3 heldCentre = pinch - height * MealStripShape.PinchHeight(shape);
            for (int i = 0; i < Stations; i++)
            {
                float fromLeft = (1f - i / (float)Segments) * shape.Width;
                Write(i, heldCentre + along * (fromLeft - MealStripShape.PinchInset), height, normal);
            }
            LastPinchErrorMetres = 0f;
            LastAlong = along;
            LastHeight = height;
            Commit();
        }

        public void Hide()
        {
            if (host && host.activeSelf) host.SetActive(false);
        }

        public void Dispose()
        {
            if (host) { if (Application.isPlaying) UnityEngine.Object.Destroy(host); else UnityEngine.Object.DestroyImmediate(host); }
            if (mesh) { if (Application.isPlaying) UnityEngine.Object.Destroy(mesh); else UnityEngine.Object.DestroyImmediate(mesh); }
        }

        private static int Index(int surface, int station, int ring) => (surface * Stations + station) * Ring + ring;

        /// <summary>One cross-section of the strip at a world centre, height direction and face normal.</summary>
        private void Write(int station, Vector3 centre, Vector3 height, Vector3 normal)
        {
            Transform t = host.transform;
            for (int w = 0; w < 2; w++)
                for (int r = 0; r < Ring; r++)
                {
                    Vector3 point = t.InverseTransformPoint(centre + height * wall[w, r].x + normal * wall[w, r].y);
                    Vector3 away = t.InverseTransformDirection(height * outward[w, r].x + normal * outward[w, r].y).normalized;
                    int outside = Index(w * 2, station, r), inside = Index(w * 2 + 1, station, r);
                    vertices[outside] = vertices[inside] = point;
                    normals[outside] = away;
                    normals[inside] = -away;
                }
        }

        private void Commit()
        {
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }
    }
}
