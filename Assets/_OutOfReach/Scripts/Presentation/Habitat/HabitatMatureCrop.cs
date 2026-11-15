using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Presentation.Habitat
{
    /// <summary>
    /// R23 presentation only: a mature leafy-green crop built at runtime over the authored seedling positions.
    /// It reads nothing and decides nothing; HabitatStatusPresenter shows it only for Core's HarvestReady stage.
    /// The geometry is original procedural work that reuses the habitat's own HB_Leaf material.
    /// </summary>
    public static class HabitatMatureCrop
    {
        public const string ObjectName = "Mature crop (shown at authoritative HarvestReady)";

        // hb_kit.py grow_cassette(): three plants per row at x -0.27/0/+0.27, rows at z 0.17/0.35 (metres).
        private const float RowSpacing = .27f, ColumnHalfSpacing = .09f;
        private const float PlantHeight = .22f;
        private static Mesh sharedMesh;

        /// <summary>Builds the crop beside the authored seedlings, inactive. Returns null if they have no mesh.</summary>
        public static GameObject Build(GameObject seedlings, Vector3 worldUp)
        {
            if (!seedlings) return null;
            MeshFilter filter = seedlings.GetComponentInChildren<MeshFilter>(true);
            var source = filter ? filter.GetComponent<MeshRenderer>() : null;
            if (!filter || !filter.sharedMesh || !source) return null;

            // Find the tray plane from the seedling mesh bounds; the importer bakes axes, but the placed kit may rotate.
            Bounds local = filter.sharedMesh.bounds;
            Transform frame = filter.transform;
            Vector3 up = worldUp.sqrMagnitude > 0f ? worldUp.normalized : Vector3.up;
            var axes = new[] { frame.right, frame.up, frame.forward };
            var extents = new[]
            {
                local.extents.x * frame.lossyScale.x, local.extents.y * frame.lossyScale.y, local.extents.z * frame.lossyScale.z
            };
            int upAxis = 0;
            for (int i = 1; i < 3; i++)
                if (Mathf.Abs(Vector3.Dot(axes[i], up)) > Mathf.Abs(Vector3.Dot(axes[upAxis], up))) upAxis = i;
            int rowAxis = -1, columnAxis = -1;
            for (int i = 0; i < 3; i++)
            {
                if (i == upAxis) continue;
                if (rowAxis < 0 || Mathf.Abs(extents[i]) > Mathf.Abs(extents[rowAxis])) { columnAxis = rowAxis; rowAxis = i; }
                else columnAxis = i;
            }
            Vector3 row = Vector3.ProjectOnPlane(axes[rowAxis], up).normalized;
            Vector3 column = Vector3.ProjectOnPlane(axes[columnAxis], up).normalized;
            Vector3 centre = frame.TransformPoint(local.center);
            float upExtent = Mathf.Abs(extents[upAxis]);
            Vector3 trayBase = centre - up * upExtent;
            float rowExtent = Mathf.Abs(extents[rowAxis]);
            float spacing = rowExtent * 2f > RowSpacing * 2f ? RowSpacing : rowExtent * .7f;

            var root = new GameObject(ObjectName);
            root.SetActive(false);
            root.transform.SetParent(seedlings.transform.parent, false);
            root.transform.SetPositionAndRotation(trayBase, Quaternion.LookRotation(column, up));
            root.transform.localScale = Vector3.one;
            root.layer = seedlings.layer;

            Mesh mesh = SharedMesh();
            int n = 0;
            for (int c = -1; c <= 1; c += 2)
            for (int r = -1; r <= 1; r++, n++)
            {
                var plant = new GameObject("Mature plant " + n);
                plant.layer = seedlings.layer;
                plant.transform.SetParent(root.transform, false);
                plant.transform.position = trayBase + row * (r * spacing) + column * (c * ColumnHalfSpacing);
                // Deterministic variety so the six heads do not look stamped.
                plant.transform.localRotation = Quaternion.Euler(0f, 37f * n + 11f, 0f);
                float s = .9f + .07f * ((n * 5) % 4);
                plant.transform.localScale = new Vector3(s, s * (.95f + .05f * (n % 3)), s);
                plant.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = plant.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.shadowCastingMode = source.shadowCastingMode;
                renderer.receiveShadows = source.receiveShadows;
            }
            return root;
        }

        /// <summary>Romaine-like rosette: three whorls of spoon-shaped, slightly curled leaves (UVs fill HB_Leaf).</summary>
        private static Mesh SharedMesh()
        {
            if (sharedMesh) return sharedMesh;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            const int along = 6;
            // whorl: leaf count, length, width, tilt from vertical at the base (deg), outward droop
            var whorls = new[]
            {
                (count: 7, length: PlantHeight * 1.05f, width: .075f, tilt: 38f, droop: .55f),
                (count: 6, length: PlantHeight * .95f, width: .065f, tilt: 22f, droop: .35f),
                (count: 4, length: PlantHeight * .75f, width: .05f, tilt: 8f, droop: .15f),
            };
            int leaf = 0;
            foreach (var w in whorls)
            {
                for (int k = 0; k < w.count; k++, leaf++)
                {
                    float yaw = 360f * k / w.count + leaf * 17.3f;
                    float jitter = .88f + .24f * Hash(leaf);
                    AddLeaf(vertices, uvs, triangles, Quaternion.Euler(0f, yaw, 0f), w.length * jitter, w.width * jitter,
                        w.tilt + 8f * (Hash(leaf + 31) - .5f), w.droop, along);
                }
            }
            sharedMesh = new Mesh { name = "R23 mature leafy crop" };
            sharedMesh.SetVertices(vertices);
            sharedMesh.SetUVs(0, uvs);
            sharedMesh.SetTriangles(triangles, 0);
            sharedMesh.RecalculateNormals();
            sharedMesh.RecalculateTangents();
            sharedMesh.RecalculateBounds();
            return sharedMesh;
        }

        private static void AddLeaf(List<Vector3> v, List<Vector2> uv, List<int> t, Quaternion yaw, float length, float width,
            float tiltDegrees, float droop, int along)
        {
            int start = v.Count;
            float tilt = tiltDegrees * Mathf.Deg2Rad;
            for (int i = 0; i <= along; i++)
            {
                float s = i / (float)along;
                // Spine rises from the crown, leans outward and droops near the tip.
                float angle = tilt + droop * s * s * 1.4f;
                Vector3 spine = SpinePoint(s, length, tilt, droop);
                float halfWidth = width * .5f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(.12f + s * .88f)) * (s < .15f ? .4f + 4f * s : 1f);
                // Cupped blade: the edges lift along the blade normal, toward the plant axis and up.
                Vector3 side = Vector3.right * halfWidth;
                Vector3 cup = new Vector3(0f, Mathf.Sin(angle), -Mathf.Cos(angle)) * (halfWidth * .35f);
                v.Add(yaw * (spine - side + cup)); uv.Add(new Vector2(0f, s));
                v.Add(yaw * spine); uv.Add(new Vector2(.5f, s));
                v.Add(yaw * (spine + side + cup)); uv.Add(new Vector2(1f, s));
            }
            for (int i = 0; i < along; i++)
            {
                int a = start + i * 3, b = a + 3;
                t.Add(a); t.Add(b); t.Add(a + 1); t.Add(a + 1); t.Add(b); t.Add(b + 1);
                t.Add(a + 1); t.Add(b + 1); t.Add(a + 2); t.Add(a + 2); t.Add(b + 1); t.Add(b + 2);
            }
        }

        private static Vector3 SpinePoint(float s, float length, float tilt, float droop)
        {
            // Integrate the bending spine in a few steps (outward = +z in leaf space before yaw).
            Vector3 p = Vector3.zero;
            const int steps = 12;
            for (int i = 0; i < steps; i++)
            {
                float u = (i + .5f) / steps * s;
                float angle = tilt + droop * u * u * 1.4f;
                p += new Vector3(0f, Mathf.Cos(angle), Mathf.Sin(angle)) * (length * s / steps);
            }
            return p;
        }

        private static float Hash(int i)
        {
            uint x = (uint)(i * 747796405 + 2891336453);
            x = ((x >> (int)((x >> 28) + 4)) ^ x) * 277803737;
            return ((x >> 22) ^ x) / (float)uint.MaxValue;
        }
    }
}
