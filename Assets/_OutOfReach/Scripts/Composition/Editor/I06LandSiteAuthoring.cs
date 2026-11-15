using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Adds the pinned passive land kit to the stationary prologue site. Does not replace marine content.</summary>
    public static class I06LandSiteAuthoring
    {
        public const string AssetRoot = "Assets/_OutOfReach/Visuals/Models/LandLaunchSite";
        public const string ModelPath = AssetRoot + "/Models/LS_LandLaunchSite.fbx";
        public const string InstanceName = "Land launch site - 2f8dd22";
        private const string ShipMaterials = "Assets/_OutOfReach/Visuals/Models/Aurora7/Materials/";
        private static readonly string[] CollisionModules =
            { "LS_PadDeck_Optional", "LS_LaunchFoundation", "LS_FlameTrench", "LS_LaunchMount", "LS_Barriers" };

        public sealed class Result
        {
            public GameObject Root;
            public Dictionary<string, Transform> Anchors;
            public MeshCollider[] FoundationColliders;
            public Transform PadPlane, GroundPlane, DeflectorContactPlane, TrenchExit;
        }

#pragma warning disable 0649
        [Serializable] private sealed class Layout
        { public Module[] modules; public Anchor[] anchors; public Map[] materials; public SourceFile[] files; }
        [Serializable] private sealed class Module
        { public string name; public int triangles, trianglesUnity; public float[] min, max; public string[] materials; }
        [Serializable] private sealed class Anchor { public string name; public float[] point; }
        [Serializable] private sealed class Map { public string name, set, tint; public float tiling, normal; public bool atlas; }
        [Serializable] private sealed class SourceFile { public string path, sha256; }
#pragma warning restore 0649

        public static Result Create(Transform siteRoot)
        {
            RequireSite(siteRoot);
            if (siteRoot.Find(InstanceName)) throw new InvalidOperationException("The land site already exists; do not stack instances.");
            Layout layout = ReadLayout();
            PrepareAssets(layout);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(Required<GameObject>(ModelPath), siteRoot);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = InstanceName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            try
            {
                var meshes = instance.GetComponentsInChildren<MeshFilter>(true).ToDictionary(f => f.name, StringComparer.Ordinal);
                var result = new Result { Root = instance, Anchors = new Dictionary<string, Transform>(StringComparer.Ordinal) };
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        string name = slots[i] ? slots[i].name : string.Empty;
                        if (name.StartsWith("MAT_", StringComparison.Ordinal)) name = name.Substring(4);
                        slots[i] = Required<Material>((name.StartsWith("A7_", StringComparison.Ordinal)
                            ? ShipMaterials : AssetRoot + "/Materials/") + name + ".mat");
                    }
                    renderer.sharedMaterials = slots;
                }
                foreach (Anchor a in layout.anchors)
                {
                    Transform[] matches = instance.GetComponentsInChildren<Transform>(true).Where(t => t.name == a.name).ToArray();
                    if (matches.Length != 1) throw new InvalidOperationException("Expected one imported anchor: " + a.name);
                    result.Anchors.Add(a.name, matches[0]);
                }
                var colliders = new List<MeshCollider>();
                foreach (string name in CollisionModules)
                {
                    MeshFilter mesh = meshes[name];
                    var collision = mesh.gameObject.AddComponent<MeshCollider>();
                    collision.convex = false;
                    collision.sharedMesh = mesh.sharedMesh;
                    colliders.Add(collision);
                }
                result.FoundationColliders = colliders.ToArray();
                result.PadPlane = Frame(instance.transform, "Land pad top plane", new Vector3(0, -32.6f, 0));
                result.GroundPlane = Frame(instance.transform, "Land ground datum plane", new Vector3(0, -36.6f, 0));
                result.TrenchExit = result.Anchors["LS_TrenchExit_Ground"];
                Physics.SyncTransforms();
                if (!DownHit(result, siteRoot, 0, 0, out RaycastHit hit))
                    throw new InvalidOperationException("The actual deflector has no upward collision surface beneath the nozzle.");
                result.DeflectorContactPlane = Frame(instance.transform, "Actual deflector contact plane", Vector3.zero);
                result.DeflectorContactPlane.SetPositionAndRotation(hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal));
                Validate(result, siteRoot);
                return result;
            }
            catch { Object.DestroyImmediate(instance); throw; }
        }

        public static void Validate(Result result, Transform siteRoot)
        {
            RequireSite(siteRoot);
            if (result?.Root == null || result.Root.transform.parent != siteRoot)
                throw new InvalidOperationException("Land kit must remain under the stationary SiteRoot.");
            Transform root = result.Root.transform;
            if (root.localPosition.sqrMagnitude > .000001f || Quaternion.Angle(root.localRotation, Quaternion.identity) > .001f
                || (root.localScale - Vector3.one).sqrMagnitude > .000001f)
                throw new InvalidOperationException("Land kit root must be identity in site-local coordinates.");
            Layout layout = ReadLayout();
            MeshFilter[] meshes = root.GetComponentsInChildren<MeshFilter>(true);
            if (meshes.Length != 11 || root.GetComponentsInChildren<Renderer>(true).Length != 11)
                throw new InvalidOperationException("Pinned land FBX must contain exactly 11 mesh/render modules.");
            int triangles = 0;
            foreach (Module spec in layout.modules)
            {
                MeshFilter filter = meshes.Single(f => f.name == spec.name);
                Mesh mesh = filter.sharedMesh;
                if (!mesh || mesh.triangles.Length / 3 != spec.trianglesUnity) throw new InvalidOperationException("Unity triangle mismatch: " + spec.name);
                triangles += mesh.triangles.Length / 3;
                Vector3[] vertices = mesh.vertices;
                Matrix4x4 matrix = siteRoot.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds actual = new Bounds(matrix.MultiplyPoint3x4(vertices[0]), Vector3.zero);
                foreach (Vector3 vertex in vertices) actual.Encapsulate(matrix.MultiplyPoint3x4(vertex));
                if (Vector3.Distance(actual.min, V(spec.min)) > .01f || Vector3.Distance(actual.max, V(spec.max)) > .01f)
                    throw new InvalidOperationException("Metre/axis/bounds mismatch: " + spec.name + " " + actual);
                string[] materialNames = filter.GetComponent<Renderer>().sharedMaterials.Select(m => m ? m.name : string.Empty).OrderBy(n => n).ToArray();
                if (!materialNames.SequenceEqual(spec.materials.OrderBy(n => n))) throw new InvalidOperationException("Material mismatch: " + spec.name);
            }
            // Unity's vertex welding removes exactly the 104 sub-micron pole slivers.
            // Source and native counts are separately pinned by ef000ea triangle-level evidence.
            if (triangles != 6458 || layout.modules.Sum(m => m.triangles) != 6562 || result.Anchors.Count != 16)
                throw new InvalidOperationException("Pinned mesh/anchor budget changed.");
            foreach (Anchor a in layout.anchors)
                if (Vector3.Distance(siteRoot.InverseTransformPoint(result.Anchors[a.name].position), V(a.point)) > .01f)
                    throw new InvalidOperationException("Imported anchor mismatch: " + a.name);
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
                if (!c || !(c is Transform || c is MeshFilter || c is MeshRenderer || c is MeshCollider))
                    throw new InvalidOperationException("Unexpected behaviour, physics body, camera or light in passive land kit.");
            if (result.FoundationColliders.Length != 5 || root.GetComponentsInChildren<Collider>(true).Length != 5
                || result.FoundationColliders.Any(c => !c || c.convex || c.isTrigger || !c.enabled))
                throw new InvalidOperationException("Use only the five exact static non-convex land meshes as collision.");
            Physics.SyncTransforms();
            foreach (float z in new[] { 0f, 10f, 40f, 71f })
            {
                if (!DownHit(result, siteRoot, 0, z, out RaycastHit hit)) throw new InvalidOperationException("Missing trench floor at z=" + z);
                float y = siteRoot.InverseTransformPoint(hit.point).y;
                float expected = z <= 6 ? y : -39.6f + (z - 6) * (3f / 66f);
                if (y > -34.8f || (z > 6 && Mathf.Abs(y - expected) > .015f))
                    throw new InvalidOperationException("A collider covers the exhaust slot or the real outlet ramp differs at z=" + z);
            }
            foreach (Vector2 p in new[] { new Vector2(8, 0), new Vector2(-8, 0), new Vector2(0, -10), new Vector2(30, 30) })
                if (!DownHit(result, siteRoot, p.x, p.y, out RaycastHit h) || Mathf.Abs(siteRoot.InverseTransformPoint(h.point).y + 32.6f) > .015f)
                    throw new InvalidOperationException("Missing real pad/apron top at " + p);
        }

        private static bool DownHit(Result result, Transform site, float x, float z, out RaycastHit nearest)
        {
            Ray ray = new Ray(site.TransformPoint(new Vector3(x, -29f, z)), -site.up);
            nearest = default; float distance = float.PositiveInfinity;
            foreach (MeshCollider collider in result.FoundationColliders)
                if (collider.Raycast(ray, out RaycastHit hit, 15f) && hit.distance < distance)
                { nearest = hit; distance = hit.distance; }
            return !float.IsPositiveInfinity(distance);
        }

        private static void PrepareAssets(Layout layout)
        {
            foreach (SourceFile file in layout.files)
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(AssetRoot + "/" + file.path))
                    if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != file.sha256)
                        throw new InvalidOperationException("Pinned land source hash differs: " + file.path);
            foreach (string path in Directory.GetFiles(AssetRoot + "/Textures", "*.png"))
            {
                var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
                if (!importer) throw new InvalidOperationException("Missing texture importer: " + path);
                bool normal = path.EndsWith("_Normal.png", StringComparison.Ordinal);
                bool mods = path.EndsWith("_MODS.png", StringComparison.Ordinal);
                bool atlas = path.EndsWith("LS_Markings.png", StringComparison.Ordinal);
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.sRGBTexture = !normal && !mods;
                importer.convertToNormalmap = false; importer.flipGreenChannel = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = atlas;
                importer.mipmapEnabled = true; importer.maxTextureSize = 1024;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
                importer.wrapMode = atlas ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
            EnsureFolder(AssetRoot + "/Materials");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Existing URP Lit is required.");
            foreach (Map spec in layout.materials)
            {
                string path = AssetRoot + "/Materials/" + spec.name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material = new Material(shader) { name = spec.name }; AssetDatabase.CreateAsset(material, path); }
                material.shader = shader; material.shaderKeywords = Array.Empty<string>();
                material.SetFloat("_Surface", 0); material.SetFloat("_Cull", 2); material.SetFloat("_ZWrite", 1);
                material.SetFloat("_WorkflowMode", 1); material.SetFloat("_SmoothnessTextureChannel", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.One); material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", spec.atlas ? .25f : 1f);
                material.SetColor("_EmissionColor", Color.black); material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                ColorUtility.TryParseHtmlString(spec.tint, out Color color); material.SetColor("_BaseColor", color);
                material.SetTexture("_BaseMap", Required<Texture2D>(AssetRoot + "/Textures/" + (spec.atlas ? "LS_Markings.png" : spec.set + "_BaseColor.png")));
                material.SetTextureScale("_BaseMap", Vector2.one * (spec.atlas ? 1f : spec.tiling));
                material.SetFloat("_AlphaClip", spec.atlas ? 1 : 0); material.SetFloat("_Cutoff", .5f);
                material.SetOverrideTag("RenderType", spec.atlas ? "TransparentCutout" : "Opaque");
                material.renderQueue = spec.atlas ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
                if (spec.atlas) material.EnableKeyword("_ALPHATEST_ON");
                else
                {
                    material.SetTexture("_BumpMap", Required<Texture2D>(AssetRoot + "/Textures/" + spec.set + "_Normal.png"));
                    material.SetFloat("_BumpScale", spec.normal); material.EnableKeyword("_NORMALMAP");
                    Texture2D mods = Required<Texture2D>(AssetRoot + "/Textures/" + spec.set + "_MODS.png");
                    material.SetTexture("_MetallicGlossMap", mods); material.SetTexture("_OcclusionMap", mods);
                    material.SetFloat("_OcclusionStrength", 1); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP");
                }
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            }
            var model = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (!model) throw new InvalidOperationException("Missing model importer.");
            // This exact FBX was authored with the ship pipeline: bake the axis
            // conversion so its geometry and anchors match the canonical manifest.
            model.globalScale = 1; model.useFileScale = true; model.bakeAxisConversion = true;
            model.preserveHierarchy = false; model.animationType = ModelImporterAnimationType.None; model.importAnimation = false;
            model.importCameras = false; model.importLights = false; model.addCollider = false;
            model.isReadable = true; model.meshCompression = ModelImporterMeshCompression.Off; model.weldVertices = true;
            model.importNormals = ModelImporterNormals.Import; model.importTangents = ModelImporterTangents.CalculateMikk;
            model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            model.SaveAndReimport();
        }

        private static Layout ReadLayout()
        {
            Layout layout = JsonUtility.FromJson<Layout>(Required<TextAsset>(AssetRoot + "/LandSiteLayout.json").text);
            if (layout?.modules?.Length != 11 || layout.anchors?.Length != 16 || layout.materials?.Length != 5 || layout.files?.Length != 8)
                throw new InvalidOperationException("Use the pinned 2f8dd22 integration layout.");
            return layout;
        }
        private static void RequireSite(Transform site)
        {
            if (!site || EditorUtility.IsPersistent(site.gameObject) || (site.lossyScale - Vector3.one).sqrMagnitude > .000001f
                || Vector3.Dot(site.up, Vector3.up) < .99999f)
                throw new ArgumentException("Use the existing metre-scale, yaw-only stationary SiteRoot.");
        }
        private static Transform Frame(Transform parent, string name, Vector3 local)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = local; return t; }
        private static Vector3 V(float[] p) => new Vector3(p[0], p[1], p[2]);
        private static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing required asset: " + path);
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/'); string parent = path.Substring(0, split);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }
    }
}
