using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.Radio.Editor
{
    /// <summary>
    /// Builds the original RadioV01 handheld radio (STORY-RADIO-01) from code: graphite body, dark screen, speaker
    /// grille, amber PTT on the left side, amber LED, knob and rubber antenna. Metres; the front faces -Z so it
    /// looks at a first-person camera with no extra yaw. Deterministic; re-running overwrites its own assets.
    /// </summary>
    public static class RadioV01Authoring
    {
        public const string Root = "Assets/_OutOfReach/Visuals/Props/RadioV01";
        public const string PrefabPath = Root + "/RadioV01.prefab";
        public const string MeshAssetPath = Root + "/Meshes/RadioV01_Meshes.asset";

        private static readonly Vector3 Body = new Vector3(.062f, .135f, .036f);

        [MenuItem("Tools/OUT OF REACH/Radio V01 - rebuild prop")]
        public static GameObject Build()
        {
            EnsureFolder(Root + "/Meshes"); EnsureFolder(Root + "/Materials");
            Material graphite = Mat("MAT_RadioV01_Graphite", new Color(.105f, .11f, .118f), .42f, .1f, Color.black);
            Material rubber = Mat("MAT_RadioV01_Rubber", new Color(.04f, .042f, .046f), .2f, 0f, Color.black);
            Material amber = Mat("MAT_RadioV01_AmberPTT", new Color(.93f, .53f, .1f), .48f, 0f, Color.black);
            Material led = Mat("MAT_RadioV01_LED", new Color(.3f, .18f, .05f), .8f, 0f, Color.black);
            Material screen = Mat("MAT_RadioV01_Screen", new Color(.018f, .026f, .03f), .88f, 0f, new Color(.04f, .2f, .24f) * .55f);
            Material grille = Mat("MAT_RadioV01_Grille", new Color(.055f, .058f, .062f), .3f, 0f, Color.black);
            Material stripe = Mat("MAT_RadioV01_AmberStripe", new Color(.85f, .5f, .12f), .4f, 0f, Color.black);

            var meshes = new List<Mesh>();
            var specs = new List<(string name, Material material, Vector3 position)>();
            var root = new GameObject("RadioV01");
            try
            {
                void Part(GameObject owner, string name, Mesh mesh, Material material, Vector3 position, List<Mesh> list)
                {
                    mesh.name = "RadioV01_" + name.Replace(' ', '_');
                    list.Add(mesh);
                    specs.Add((name, material, position));
                }
                float top = Body.y * .5f, front = -Body.z * .5f;
                Part(root, "Body", RoundedBox(Body, .008f, 5), graphite, Vector3.zero, meshes);
                Part(root, "Grip L", RoundedBox(new Vector3(.006f, .07f, .03f), .0025f, 2), rubber, new Vector3(-.0315f, -.03f, 0), meshes);
                Part(root, "Grip R", RoundedBox(new Vector3(.006f, .07f, .03f), .0025f, 2), rubber, new Vector3(.0315f, -.03f, 0), meshes);
                Part(root, "Bezel", RoundedBox(new Vector3(.05f, .034f, .003f), .003f, 2), rubber, new Vector3(0, .03f, front - .0005f), meshes);
                Part(root, "Screen", RoundedBox(new Vector3(.043f, .026f, .002f), .0015f, 2), screen, new Vector3(0, .03f, front - .0019f), meshes);
                Part(root, "Grille", RoundedBox(new Vector3(.046f, .052f, .003f), .002f, 2), grille, new Vector3(0, -.033f, front - .0004f), meshes);
                for (int i = 0; i < 7; i++)
                    Part(root, "Grille slat " + i, RoundedBox(new Vector3(.04f, .0026f, .0016f), .0008f, 1), graphite,
                        new Vector3(0, -.055f + i * .0073f, front - .0021f), meshes);
                Part(root, "Stripe", RoundedBox(new Vector3(.05f, .004f, .0015f), .0007f, 1), stripe, new Vector3(0, .0085f, front - .0012f), meshes);
                Part(root, "PTT", RoundedBox(new Vector3(.0075f, .034f, .016f), .0032f, 3), amber, new Vector3(-.0335f, .018f, 0), meshes);
                Part(root, "Side key", RoundedBox(new Vector3(.006f, .011f, .011f), .0025f, 2), rubber, new Vector3(-.0333f, -.008f, 0), meshes);
                Part(root, "Antenna base", Cylinder(.0085f, .012f, 20), rubber, new Vector3(.017f, top + .006f, .004f), meshes);
                Part(root, "Antenna", Cylinder(.0052f, .092f, 16), rubber, new Vector3(.017f, top + .012f + .046f, .004f), meshes);
                Part(root, "Antenna tip", RoundedBox(new Vector3(.0128f, .0128f, .0128f), .0064f, 4), rubber, new Vector3(.017f, top + .012f + .092f, .004f), meshes);
                Part(root, "Knob", Cylinder(.0078f, .013f, 22), rubber, new Vector3(-.013f, top + .0065f, .002f), meshes);
                Part(root, "Knob cap", Cylinder(.0062f, .0025f, 22), graphite, new Vector3(-.013f, top + .0142f, .002f), meshes);
                Part(root, "LED", Cylinder(.0034f, .004f, 14), led, new Vector3(.0005f, top + .002f, -.008f), meshes);
                Part(root, "Belt clip", RoundedBox(new Vector3(.03f, .07f, .004f), .002f, 2), rubber, new Vector3(0, .005f, -front + .002f), meshes);

                SaveMeshes(meshes);
                for (int i = 0; i < specs.Count; i++)
                {
                    var go = new GameObject(specs[i].name);
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = specs[i].position;
                    go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = specs[i].material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (!prefab) throw new InvalidOperationException("RadioV01 prefab could not be saved.");
                int triangles = 0; foreach (Mesh m in meshes) triangles += (int)(m.GetIndexCount(0) / 3);
                Debug.Log("RADIO_V01_READY parts=" + meshes.Count + " triangles=" + triangles + " prefab=" + PrefabPath);
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        // One asset file holds every part mesh (main object + sub-assets); GUID and sub-ids stay stable on re-run.
        private static void SaveMeshes(List<Mesh> meshes)
        {
            var existing = new Dictionary<string, Mesh>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(MeshAssetPath))
                if (o is Mesh m) existing[m.name] = m;
            bool haveFile = existing.Count > 0;
            for (int i = 0; i < meshes.Count; i++)
            {
                Mesh fresh = meshes[i];
                if (existing.TryGetValue(fresh.name, out Mesh stored))
                {
                    stored.Clear();
                    stored.SetVertices(fresh.vertices); stored.SetNormals(fresh.normals); stored.SetUVs(0, fresh.uv);
                    stored.SetTriangles(fresh.triangles, 0); stored.RecalculateBounds(); stored.RecalculateTangents();
                    EditorUtility.SetDirty(stored);
                    meshes[i] = stored;
                    Object.DestroyImmediate(fresh);
                }
                else if (!haveFile) { AssetDatabase.CreateAsset(fresh, MeshAssetPath); haveFile = true; }
                else AssetDatabase.AddObjectToAsset(fresh, MeshAssetPath);
            }
            AssetDatabase.SaveAssets();
        }

        private static Mesh RoundedBox(Vector3 size, float radius, int segments)
        {
            Vector3 half = size * .5f;
            Vector3 inner = new Vector3(Mathf.Max(0, half.x - radius), Mathf.Max(0, half.y - radius), Mathf.Max(0, half.z - radius));
            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            int n = Mathf.Max(1, segments) * 2;
            foreach (Vector3 axis in axes)
            {
                Vector3 u = Mathf.Abs(axis.y) > .5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(axis, u);
                int start = verts.Count;
                for (int j = 0; j <= n; j++)
                    for (int i = 0; i <= n; i++)
                    {
                        Vector3 cube = axis + u * (i / (float)n * 2 - 1) + v * (j / (float)n * 2 - 1);
                        Vector3 p = Vector3.Scale(cube, half);
                        Vector3 core = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 dir = p - core;
                        Vector3 normal = dir.sqrMagnitude > 1e-12f ? dir.normalized : axis;
                        verts.Add(core + normal * radius); normals.Add(normal); uvs.Add(new Vector2(i / (float)n, j / (float)n));
                    }
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        int a = start + j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                        AddOutward(verts, normals, tris, a, c, b);
                        AddOutward(verts, normals, tris, b, c, d);
                    }
            }
            return Finish(verts, normals, uvs, tris);
        }

        private static Mesh Cylinder(float radius, float height, int segments)
        {
            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            float h = height * .5f;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 n = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                verts.Add(n * radius + Vector3.down * h); normals.Add(n); uvs.Add(new Vector2(i / (float)segments, 0));
                verts.Add(n * radius + Vector3.up * h); normals.Add(n); uvs.Add(new Vector2(i / (float)segments, 1));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                AddOutward(verts, normals, tris, a, b, c);
                AddOutward(verts, normals, tris, b, d, c);
            }
            foreach (float sign in new[] { 1f, -1f })
            {
                int centre = verts.Count;
                verts.Add(Vector3.up * h * sign); normals.Add(Vector3.up * sign); uvs.Add(new Vector2(.5f, .5f));
                int ring = verts.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * radius, h * sign, Mathf.Sin(a) * radius)); normals.Add(Vector3.up * sign);
                    uvs.Add(new Vector2(.5f + Mathf.Cos(a) * .5f, .5f + Mathf.Sin(a) * .5f));
                }
                for (int i = 0; i < segments; i++) AddOutward(verts, normals, tris, centre, ring + i, ring + i + 1);
            }
            return Finish(verts, normals, uvs, tris);
        }

        // Unity front faces: cross(b - a, c - a) points at the viewer. Orient every triangle along its normals.
        private static void AddOutward(List<Vector3> v, List<Vector3> n, List<int> tris, int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(face, n[a] + n[b] + n[c]) < 0f) { int t = b; b = c; c = t; }
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        private static Mesh Finish(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris)
        {
            var mesh = new Mesh();
            mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }

        private static Material Mat(string name, Color baseColor, float smoothness, float metallic, Color emission)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (!lit) throw new InvalidOperationException("URP Lit shader not found.");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(lit) { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            // Emission is on for every radio material so the presenter can light the LED through a property block.
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
