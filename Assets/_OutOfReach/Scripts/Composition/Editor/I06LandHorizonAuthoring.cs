using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Passive original land only. Root owns the covering launch kit and all scene transitions.</summary>
    public static class I06LandHorizonAuthoring
    {
        public const string AssetRoot = "Assets/_OutOfReach/Scripts/Presentation/LandHorizon";
        public const string MeshPath = AssetRoot + "/Generated/LandHorizonSurface.asset";
        public const string MaterialPath = AssetRoot + "/Generated/LandHorizon.mat";
        public const string ShaderPath = AssetRoot + "/LandHorizon.shader";
        public const string ObjectName = "I06 flat coastal land horizon";
        public const float GroundLocalY = -36.6f;
        public const float HalfExtentMetres = 8000f;
        public const float MaximumReliefMetres = .85f;
        public const float FlatRadiusMetres = 200f;
        public const float KitMinX = -72f, KitMaxX = 72f, KitMinZ = -62f, KitMaxZ = 72f;
        public const float ExitHalfWidthMetres = 3f;
        public const float FacilityMinX = -55f, FacilityMaxX = 95f, FacilityMinZ = -1020f, FacilityMaxZ = -50f;
        public const float FacilityBlendMetres = 30f;

        /// <summary>
        /// Use the actual stationary SiteRoot with the covering land kit, not VehicleRoot.
        /// Local Y=-36.6 is the kit's slope-foot/outlet datum; SiteRoot origin is the prologue origin.
        /// The full 144x134m kit footprint is excluded. Optional 6m-wide outlet notch extends +Z only.
        /// No scene save, ocean disabling, collider, camera, light, runtime component or time source.
        /// </summary>
        public static GameObject Create(Transform siteRoot, float exitNotchLengthMetres = 0f)
        {
            ValidateSite(siteRoot); ValidateNotch(exitNotchLengthMetres);
            for (int i = 0; i < siteRoot.childCount; i++)
                Require(siteRoot.GetChild(i).name != ObjectName, "Ground already exists under this SiteRoot; do not stack duplicate surfaces.");
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Require(shader && !ShaderUtil.ShaderHasError(shader), "Import the owned LandHorizon shader without compilation errors first.");
            EnsureFolder(AssetRoot + "/Generated");
            BuildSurfaceData(exitNotchLengthMetres, out Vector3[] vertices, out int[] triangles);
            ValidateSurface(vertices, triangles, exitNotchLengthMetres);
            var uvs = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
            var generated = new Mesh { name = "Original flat coastal grassland - launch kit excluded", indexFormat = IndexFormat.UInt16 };
            generated.vertices = vertices; generated.uv = uvs; generated.triangles = triangles;
            generated.RecalculateNormals(); generated.RecalculateBounds();
            Mesh mesh = PersistMesh(generated);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material)
            {
                Require(!AssetDatabase.LoadMainAssetAtPath(MaterialPath), "Owned material path is occupied by another asset type.");
                material = new Material(shader) { name = "Original muted coastal sage and soil" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetColor("_SageColor", new Color(.31f, .34f, .25f, 1));
            material.SetColor("_SoilColor", new Color(.32f, .29f, .23f, 1));
            material.SetColor("_DryColor", new Color(.39f, .38f, .29f, 1));
            material.SetFloat("_Smoothness", .12f);
            material.enableInstancing = false;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);
            GameObject ground = new GameObject(ObjectName);
            try
            {
                ground.transform.SetParent(siteRoot, false);
                ground.transform.localPosition = new Vector3(0, GroundLocalY, 0);
                ground.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = ground.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                Validate(ground, siteRoot, exitNotchLengthMetres);
                AssetDatabase.SaveAssetIfDirty(mesh); AssetDatabase.SaveAssetIfDirty(material);
                Debug.Log("LAND_HORIZON_AUTHORED vertices=" + vertices.Length + " triangles=" + triangles.Length / 3
                    + " bounds=16000m square relief<=0.85m flat>=200m kitHole=144x134m notch=" + exitNotchLengthMetres
                    + "m; passive only, GPU art acceptance pending.");
                return ground;
            }
            catch { Object.DestroyImmediate(ground); throw; }
        }

        /// <summary>Validation of owned instance only; does not alter the supplied scene.</summary>
        public static void Validate(GameObject ground, Transform siteRoot, float exitNotchLengthMetres = 0f)
        {
            ValidateSite(siteRoot); ValidateNotch(exitNotchLengthMetres);
            Require(ground && ground.transform.parent == siteRoot, "Ground must be an immediate SiteRoot child.");
            Require((ground.transform.localPosition - new Vector3(0, GroundLocalY, 0)).sqrMagnitude < 1e-10f
                && Quaternion.Angle(ground.transform.localRotation, Quaternion.identity) < .001f
                && (ground.transform.localScale - Vector3.one).sqrMagnitude < 1e-10f, "Ground must retain exact site-local datum, metre scale and axes.");
            Require(ground.transform.childCount == 0 && ground.GetComponents<Component>().Length == 3,
                "Passive Transform, MeshFilter and MeshRenderer only; no runtime, collider or service components.");
            MeshFilter filter = ground.GetComponent<MeshFilter>(); MeshRenderer renderer = ground.GetComponent<MeshRenderer>();
            Require(filter && filter.sharedMesh && renderer && renderer.sharedMaterial, "Missing generated surface or material.");
            Require(AssetDatabase.GetAssetPath(filter.sharedMesh) == MeshPath && AssetDatabase.GetAssetPath(renderer.sharedMaterial) == MaterialPath,
                "Instance must use the persisted owned assets.");
            Require(renderer.sharedMaterial.shader == AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath), "Unexpected ground shader.");
            ValidateSurface(filter.sharedMesh.vertices, filter.sharedMesh.triangles, exitNotchLengthMetres);
        }

        // Pure managed data generation is exposed so the staged offline probe exercises this exact geometry code.
        // Rows/columns contain exact kit and optional outlet boundaries; no triangle can bridge either hole.
        public static void BuildSurfaceData(float exitNotchLengthMetres, out Vector3[] vertices, out int[] triangles)
        {
            ValidateNotch(exitNotchLengthMetres);
            float[] x = Coordinates(false, exitNotchLengthMetres), z = Coordinates(true, exitNotchLengthMetres);
            var points = new List<Vector3>(x.Length * z.Length); var indices = new List<int>(x.Length * z.Length * 6);
            var remap = new int[x.Length * z.Length]; for (int i = 0; i < remap.Length; i++) remap[i] = -1;
            for (int row = 0; row < z.Length - 1; row++) for (int col = 0; col < x.Length - 1; col++)
            {
                if (OverlapsHole(x[col], x[col + 1], z[row], z[row + 1], exitNotchLengthMetres)) continue;
                int a = Vertex(col, row, x, z, remap, points), b = Vertex(col, row + 1, x, z, remap, points);
                int c = Vertex(col + 1, row, x, z, remap, points), d = Vertex(col + 1, row + 1, x, z, remap, points);
                indices.Add(a); indices.Add(b); indices.Add(c); indices.Add(c); indices.Add(b); indices.Add(d);
            }
            vertices = points.ToArray(); triangles = indices.ToArray();
        }

        public static void ValidateSurface(Vector3[] vertices, int[] triangles, float exitNotchLengthMetres)
        {
            ValidateNotch(exitNotchLengthMetres);
            Require(vertices != null && vertices.Length > 1000 && vertices.Length < 10000
                && triangles != null && triangles.Length % 3 == 0 && triangles.Length / 3 < 20000, "Unexpected geometry budget.");
            bool west = false, east = false, south = false, north = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                Require(Finite(p.x) && Finite(p.y) && Finite(p.z) && Math.Abs(p.y) <= MaximumReliefMetres + 1e-5,
                    "Non-finite or excessive terrain relief.");
                Require(Math.Abs(p.x) <= HalfExtentMetres && Math.Abs(p.z) <= HalfExtentMetres, "Geometry exceeds owned bounds.");
                if (p.x * p.x + p.z * p.z <= FlatRadiusMetres * FlatRadiusMetres)
                    Require(p.y == 0, "Ground must be exactly flat inside 200m.");
                if ((p.x == KitMinX || p.x == KitMaxX) && p.z >= KitMinZ && p.z <= KitMaxZ
                    || (p.z == KitMinZ || p.z == KitMaxZ) && p.x >= KitMinX && p.x <= KitMaxX)
                    Require(p.y == 0, "Kit slope-foot perimeter must meet exact ground datum.");
                if (p.x >= FacilityMinX && p.x <= FacilityMaxX && p.z >= FacilityMinZ && p.z <= FacilityMaxZ)
                    Require(p.y == 0, "Crawlerway and assembly building must meet the exact ground datum.");
                west |= p.x == -HalfExtentMetres; east |= p.x == HalfExtentMetres;
                south |= p.z == -HalfExtentMetres; north |= p.z == HalfExtentMetres;
            }
            Require(west && east && south && north, "Horizon must cover all four azimuths.");
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                Require(ia >= 0 && ia < vertices.Length && ib >= 0 && ib < vertices.Length && ic >= 0 && ic < vertices.Length, "Invalid mesh index.");
                Vector3 a = vertices[ia], b = vertices[ib], c = vertices[ic];
                float upArea = (b.z - a.z) * (c.x - a.x) - (b.x - a.x) * (c.z - a.z);
                Require(upArea > 0, "Reversed or degenerate ground winding.");
                Require(!OverlapsHole(Math.Min(a.x, Math.Min(b.x, c.x)), Math.Max(a.x, Math.Max(b.x, c.x)),
                    Math.Min(a.z, Math.Min(b.z, c.z)), Math.Max(a.z, Math.Max(b.z, c.z)), exitNotchLengthMetres),
                    "A ground triangle spans the covering kit or optional trench-outlet exclusion.");
                if (Math.Min(a.x, Math.Min(b.x, c.x)) < FacilityMaxX && Math.Max(a.x, Math.Max(b.x, c.x)) > FacilityMinX
                    && Math.Min(a.z, Math.Min(b.z, c.z)) < FacilityMaxZ && Math.Max(a.z, Math.Max(b.z, c.z)) > FacilityMinZ)
                    Require(a.y == 0 && b.y == 0 && c.y == 0, "Interpolated relief enters the protected facility corridor.");
                // All changed-height triangles must lie entirely outside the conservative inner 200m square.
                if (a.y != 0 || b.y != 0 || c.y != 0)
                    Require(Math.Min(a.x, Math.Min(b.x, c.x)) >= FlatRadiusMetres || Math.Max(a.x, Math.Max(b.x, c.x)) <= -FlatRadiusMetres
                        || Math.Min(a.z, Math.Min(b.z, c.z)) >= FlatRadiusMetres || Math.Max(a.z, Math.Max(b.z, c.z)) <= -FlatRadiusMetres,
                        "Interpolated relief reaches the flat protected area.");
            }
        }

        static float[] Coordinates(bool zAxis, float notch)
        {
            var values = new SortedSet<float>();
            for (int p = -256; p <= 256; p += 16) values.Add(p);
            foreach (float p in new[] { 320f, 400, 520, 680, 900, 1200, 1600, 2200, 3000, 4000, 5400, 6800, HalfExtentMetres })
            { values.Add(-p); values.Add(p); }
            foreach (float p in new[] { KitMinX, KitMaxX, KitMinZ, KitMaxZ, -ExitHalfWidthMetres, ExitHalfWidthMetres, -200, 200 }) values.Add(p);
            float facilityMin = zAxis ? FacilityMinZ : FacilityMinX, facilityMax = zAxis ? FacilityMaxZ : FacilityMaxX;
            foreach (float p in new[] { facilityMin - FacilityBlendMetres, facilityMin - FacilityBlendMetres * .5f,
                facilityMin, facilityMax, facilityMax + FacilityBlendMetres * .5f, facilityMax + FacilityBlendMetres }) values.Add(p);
            if (zAxis && notch > 0) values.Add(KitMaxZ + notch);
            var result = new float[values.Count]; values.CopyTo(result); return result;
        }
        static int Vertex(int col, int row, float[] x, float[] z, int[] remap, List<Vector3> points)
        {
            int key = row * x.Length + col;
            if (remap[key] >= 0) return remap[key];
            remap[key] = points.Count;
            points.Add(new Vector3(x[col], Height(x[col], z[row]), z[row])); return remap[key];
        }
        static float Height(float x, float z)
        {
            double radius = Math.Sqrt(x * (double)x + z * (double)z);
            // Extra flat margin prevents interpolation from carrying distant relief inside the promised 200m zone.
            double t = Math.Min(1, Math.Max(0, (radius - 320) / 480)); t = t * t * (3 - 2 * t);
            double wave = .5 * Math.Sin(x / 410d + z / 633d)
                + .3 * Math.Sin((x * .36 - z * .92) / 580 + 1.7) + .2 * Math.Sin((x + z) / 970d - .4);
            double dx = Math.Max(0d, Math.Max(FacilityMinX - x, x - FacilityMaxX));
            double dz = Math.Max(0d, Math.Max(FacilityMinZ - z, z - FacilityMaxZ));
            double corridor = Math.Min(1d, Math.Sqrt(dx * dx + dz * dz) / FacilityBlendMetres);
            corridor = corridor * corridor * (3d - 2d * corridor);
            return (float)(MaximumReliefMetres * t * wave * corridor);
        }
        static bool OverlapsHole(float minX, float maxX, float minZ, float maxZ, float notch) =>
            minX < KitMaxX && maxX > KitMinX && minZ < KitMaxZ && maxZ > KitMinZ
            || notch > 0 && minX < ExitHalfWidthMetres && maxX > -ExitHalfWidthMetres && minZ < KitMaxZ + notch && maxZ > KitMaxZ;
        static void ValidateNotch(float value) => Require(Finite(value) && value >= 0 && value <= 32, "Optional outlet notch must be 0..32 metres; zero is the approved default.");
        static void ValidateSite(Transform site)
        {
            Require(site && site.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(site.gameObject), "Use the existing scene SiteRoot, not an asset.");
            Require((site.lossyScale - Vector3.one).sqrMagnitude < 1e-8f && Vector3.Dot(site.up, Vector3.up) > .999999f,
                "Stationary SiteRoot must be metre-scale and yaw-only, with +Y up.");
        }
        static Mesh PersistMesh(Mesh generated)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (!existing)
            {
                Require(!AssetDatabase.LoadMainAssetAtPath(MeshPath), "Owned mesh path is occupied by another asset type.");
                AssetDatabase.CreateAsset(generated, MeshPath); return generated;
            }
            EditorUtility.CopySerialized(generated, existing); Object.DestroyImmediate(generated); EditorUtility.SetDirty(existing); return existing;
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Require(AssetDatabase.IsValidFolder(parent), "Import the owned LandHorizon folder before generating assets.");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Land horizon: " + message); }
    }
}
