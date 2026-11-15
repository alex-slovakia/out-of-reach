using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.ModelTools
{
    /// <summary>
    /// Editor-only tools for the AURORA-7 exterior V2 (the remodelled hull seen in the departure orbit shots).
    /// Rebuild: configures the importers, creates the URP Lit materials from Source~/a7x_materials.json and
    /// assembles the prefabs. Install: swaps the prefabs into the open scene's orbital set and never saves.
    /// The prefabs hold renderers and LOD groups only: no camera, light, collider, audio or gameplay script.
    /// Batch: -executeMethod OutOfReach.ModelTools.Aurora7ExteriorV2Tools.BuildBatch | CaptureBatch (needs graphics).
    /// </summary>
    public static class Aurora7ExteriorV2Tools
    {
        private const string Root = "Assets/_OutOfReach/Visuals/Models/Aurora7ExteriorV2";
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string HullModel = Root + "/Meshes/A7X_Exterior.fbx";
        private const string AdapterModel = Root + "/Meshes/A7X_DockAdapter.fbx";
        public const string HullPrefab = Root + "/Prefabs/Aurora7ExteriorV2_Hull.prefab";
        public const string AdapterPrefab = Root + "/Prefabs/Aurora7ExteriorV2_DockAdapter.prefab";
        public const string CompletePrefab = Root + "/Prefabs/Aurora7ExteriorV2.prefab";
        private const string BootstrapScene = "Assets/_OutOfReach/Scenes/Bootstrap.unity";

        // Names owned by the reviewed departure authoring (LeadDepartureAuthoring); read, never renamed.
        private const string OrbitSetName = "Departure - AURORA-7 in Earth orbit";
        private const string HullGroupName = "Delivered AURORA-7 exterior renderers";
        private const string CollarGroupName = "Aft docking adapter and alignment lamps";
        private const string ReceiverName = "AURORA-7 docking contact plane";
        private const string ApproachName = "AURORA-7 approach camera pose";
        private const string DockName = "AURORA-7 collar camera pose";
        private const string InstalledHullName = "AURORA-7 exterior V2 hull";
        private const string InstalledAdapterName = "AURORA-7 exterior V2 docking adapter";
        private static readonly Vector3 AdapterOrigin = new Vector3(0f, 1.28f, 21.16f);

        [Serializable]
        private class Spec
        {
            public string name;
            public string tex;
            public string baseMap;
            public float[] tint;
            public float[] color;
            public float[] emission;
            public float smooth = 0.5f;
            public float metallic;
            public float estr;
            public bool alpha;
        }

        [Serializable]
        private class SpecFile
        {
            public Spec[] materials;
        }

        // ------------------------------------------------------------------ menu
        [MenuItem("OUT OF REACH/AURORA-7 Exterior V2/Rebuild materials and prefabs")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Prefabs");
            ConfigureTextures();
            Dictionary<string, Material> materials = CreateMaterials();
            ConfigureModel(HullModel, materials);
            ConfigureModel(AdapterModel, materials);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            BuildPrefabs();
            AssetDatabase.SaveAssets();
            WriteReport();
            Debug.Log("[A7X] BUILD_COMPLETE " + HullPrefab);
        }

        [MenuItem("OUT OF REACH/AURORA-7 Exterior V2/Install into open scene (no save)")]
        public static void InstallMenu()
        {
            Debug.Log("[A7X] " + Install(SceneManager.GetActiveScene()));
        }

        [MenuItem("OUT OF REACH/AURORA-7 Exterior V2/Uninstall from open scene (no save)")]
        public static void UninstallMenu()
        {
            Debug.Log("[A7X] " + Uninstall(SceneManager.GetActiveScene()));
        }

        public static void BuildBatch()
        {
            RunBatch(Build);
        }

        public static void CaptureBatch()
        {
            RunBatch(() =>
            {
                if (!File.Exists(HullPrefab))
                {
                    Build();
                }

                Capture();
            });
        }

        public static void BuildAndCaptureBatch()
        {
            RunBatch(() =>
            {
                Build();
                Capture();
            });
        }

        private static void RunBatch(Action action)
        {
            int code = 0;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                action();
                Debug.Log("[A7X] BATCH_COMPLETE");
            }
            catch (Exception exception)
            {
                Debug.LogError("[A7X] FAILED " + exception);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        // ------------------------------------------------------------------ import
        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void ConfigureTextures()
        {
            foreach (string file in Directory.GetFiles(Root + "/Textures", "*.png"))
            {
                string path = file.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool isNormal = name.EndsWith("_Normal", StringComparison.Ordinal);
                bool isMask = name.EndsWith("_Mask", StringComparison.Ordinal);
                bool isAtlas = name == "A7X_Markings";
                importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (!isNormal)
                {
                    importer.sRGBTexture = !isMask;
                }

                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = isAtlas;
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = isAtlas;
                importer.alphaTestReferenceValue = 0.5f;
                importer.anisoLevel = 8;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = isAtlas ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            string json = File.ReadAllText(Root + "/Source~/a7x_materials.json");
            SpecFile file = JsonUtility.FromJson<SpecFile>(json);
            Shader lit = Shader.Find(LitShader);
            if (!lit)
            {
                throw new InvalidOperationException("Missing shader " + LitShader);
            }

            var result = new Dictionary<string, Material>();
            foreach (Spec spec in file.materials)
            {
                string path = Root + "/Materials/" + spec.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material)
                {
                    material = new Material(lit) { name = spec.name };
                    AssetDatabase.CreateAsset(material, path);
                }

                Apply(material, spec, lit);
                EditorUtility.SetDirty(material);
                result[spec.name] = material;
            }

            return result;
        }

        private static Color Linear(float[] v, Color fallback)
        {
            return v != null && v.Length >= 3 ? new Color(v[0], v[1], v[2], 1f) : fallback;
        }

        private static void Apply(Material material, Spec spec, Shader lit)
        {
            material.shader = lit;
            material.shaderKeywords = Array.Empty<string>();
            material.SetOverrideTag("RenderType", string.Empty);
            material.renderQueue = -1;
            material.SetTexture("_BaseMap", null);
            material.SetTexture("_BumpMap", null);
            material.SetTexture("_MetallicGlossMap", null);
            material.SetTexture("_OcclusionMap", null);
            material.SetTexture("_EmissionMap", null);
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_WorkflowMode", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_OcclusionStrength", 1f);
            material.SetFloat("_BumpScale", 1f);
            material.SetShaderPassEnabled("ShadowCaster", true);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            material.enableInstancing = true;

            // The JSON carries linear values; non-HDR material colours are stored in sRGB.
            bool hasTint = spec.tint != null && spec.tint.Length >= 3;
            Color baseColour = hasTint ? Linear(spec.tint, Color.white) : Linear(spec.color, Color.white);
            material.SetColor("_BaseColor", baseColour.gamma);

            if (!string.IsNullOrEmpty(spec.tex))
            {
                material.SetTexture("_BaseMap", LoadTexture("A7X_" + spec.tex + "_BaseColor"));
                material.SetTexture("_BumpMap", LoadTexture("A7X_" + spec.tex + "_Normal"));
                material.EnableKeyword("_NORMALMAP");
                Texture2D mask = LoadTexture("A7X_" + spec.tex + "_Mask");
                material.SetTexture("_MetallicGlossMap", mask);
                material.SetTexture("_OcclusionMap", mask);
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Smoothness", 1f);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_OCCLUSIONMAP");
            }
            else
            {
                material.SetFloat("_Metallic", spec.metallic);
                material.SetFloat("_Smoothness", spec.smooth);
                if (!string.IsNullOrEmpty(spec.baseMap))
                {
                    material.SetTexture("_BaseMap", LoadTexture(Path.GetFileNameWithoutExtension(spec.baseMap)));
                    material.SetColor("_BaseColor", Color.white);
                }
            }

            if (spec.emission != null && spec.emission.Length >= 3 && spec.estr > 0f)
            {
                material.SetColor("_EmissionColor", Linear(spec.emission, Color.black) * spec.estr);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }

            if (spec.alpha)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
        }

        private static Texture2D LoadTexture(string name)
        {
            string path = Root + "/Textures/" + name + ".png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture)
            {
                throw new FileNotFoundException("Missing texture " + path);
            }

            return texture;
        }

        private static void ConfigureModel(string path, Dictionary<string, Material> materials)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (!importer)
            {
                throw new FileNotFoundException("Missing model " + path);
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.weldVertices = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.generateSecondaryUV = false;
            importer.addCollider = false;
            importer.indexFormat = ModelImporterIndexFormat.Auto;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SaveAndReimport();

            importer = (ModelImporter)AssetImporter.GetAtPath(path);
            foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
            {
                if (!materials.TryGetValue(embedded.name, out Material material))
                {
                    throw new InvalidOperationException(path + " uses material '" + embedded.name + "' with no entry in a7x_materials.json");
                }

                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), material);
            }

            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ prefabs
        private static void Dress(GameObject root)
        {
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool flat = renderer.name.Contains("Markings") || renderer.name.Contains("Lamps");
                renderer.shadowCastingMode = flat ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }
        }

        private static void BuildPrefabs()
        {
            var hullModel = AssetDatabase.LoadAssetAtPath<GameObject>(HullModel);
            var adapterModel = AssetDatabase.LoadAssetAtPath<GameObject>(AdapterModel);
            if (!hullModel || !adapterModel)
            {
                throw new FileNotFoundException("The exterior V2 models are not imported.");
            }

            // Hull: same origin, axes and scale as the delivered exterior; three LODs.
            var hull = new GameObject(InstalledHullName);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(hullModel, hull.transform);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
            Dress(hull);
            var lods = new List<LOD>();
            float[] heights = { 0.22f, 0.07f, 0.008f };
            for (int i = 0; i < 3; i++)
            {
                Transform level = model.transform.Find("LOD" + i);
                if (!level)
                {
                    throw new InvalidOperationException("A7X_Exterior.fbx has no LOD" + i + " group.");
                }

                level.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                lods.Add(new LOD(heights[i], level.GetComponentsInChildren<MeshRenderer>(true).Cast<Renderer>().ToArray()));
            }

            var group = hull.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(lods.ToArray());
            group.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(hull, HullPrefab);
            Object.DestroyImmediate(hull);

            // Docking adapter: origin on the aft bulkhead, +z along the docking axis, contact plane at z = 3.84.
            var adapter = new GameObject(InstalledAdapterName);
            var adapterInstance = (GameObject)PrefabUtility.InstantiatePrefab(adapterModel, adapter.transform);
            adapterInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            adapterInstance.transform.localScale = Vector3.one;
            foreach (Transform child in adapterInstance.GetComponentsInChildren<Transform>(true))
            {
                if (child != adapterInstance.transform && child.GetComponent<MeshRenderer>() == null)
                {
                    child.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
            }

            Dress(adapter);
            PrefabUtility.SaveAsPrefabAsset(adapter, AdapterPrefab);
            Object.DestroyImmediate(adapter);

            // Complete vehicle for stand-alone use: the two prefabs nested at the delivered datum.
            var complete = new GameObject("AURORA-7 exterior V2");
            var hullAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HullPrefab);
            var adapterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AdapterPrefab);
            PrefabUtility.InstantiatePrefab(hullAsset, complete.transform);
            var nested = (GameObject)PrefabUtility.InstantiatePrefab(adapterAsset, complete.transform);
            nested.transform.SetLocalPositionAndRotation(AdapterOrigin, Quaternion.identity);
            PrefabUtility.SaveAsPrefabAsset(complete, CompletePrefab);
            Object.DestroyImmediate(complete);
        }

        private static Bounds MeshBounds(IEnumerable<MeshRenderer> renderers, Matrix4x4 toSpace)
        {
            bool any = false;
            var bounds = new Bounds();
            foreach (MeshRenderer renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh)
                {
                    continue;
                }

                Bounds local = filter.sharedMesh.bounds;
                Matrix4x4 m = toSpace * renderer.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return bounds;
        }

        private static long Triangles(IEnumerable<MeshRenderer> renderers)
        {
            long total = 0;
            foreach (MeshRenderer renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter && filter.sharedMesh)
                {
                    for (int s = 0; s < filter.sharedMesh.subMeshCount; s++)
                    {
                        total += filter.sharedMesh.GetIndexCount(s) / 3;
                    }
                }
            }

            return total;
        }

        private static string V(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "[{0:0.###}, {1:0.###}, {2:0.###}]", v.x, v.y, v.z);
        }

        private static void WriteReport()
        {
            var complete = AssetDatabase.LoadAssetAtPath<GameObject>(CompletePrefab);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(complete);
            var text = new StringBuilder();
            text.Append("{\n \"unity\": \"").Append(Application.unityVersion).Append("\",\n");
            // Nested prefab instances carry their asset names, so take them in the order BuildPrefabs adds them.
            Transform hull = instance.transform.GetChild(0);
            Transform adapter = instance.transform.GetChild(1);
            for (int i = 0; i < 3; i++)
            {
                MeshRenderer[] renderers = hull.GetComponentsInChildren<Transform>(true).First(t => t.name == "LOD" + i)
                    .GetComponentsInChildren<MeshRenderer>(true);
                Bounds b = MeshBounds(renderers, Matrix4x4.identity);
                text.Append(" \"hull_LOD").Append(i).Append("\": {\"triangles\": ").Append(Triangles(renderers))
                    .Append(", \"renderers\": ").Append(renderers.Length)
                    .Append(", \"bounds_min\": ").Append(V(b.min)).Append(", \"bounds_max\": ").Append(V(b.max)).Append("},\n");
            }

            MeshRenderer[] adapterRenderers = adapter.GetComponentsInChildren<MeshRenderer>(true);
            Bounds ab = MeshBounds(adapterRenderers, Matrix4x4.identity);
            text.Append(" \"adapter\": {\"triangles\": ").Append(Triangles(adapterRenderers))
                .Append(", \"renderers\": ").Append(adapterRenderers.Length)
                .Append(", \"bounds_min\": ").Append(V(ab.min)).Append(", \"bounds_max\": ").Append(V(ab.max)).Append("},\n");
            int materials = instance.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Distinct().Count();
            bool clean = instance.GetComponentsInChildren<Collider>(true).Length == 0 &&
                         instance.GetComponentsInChildren<Camera>(true).Length == 0 &&
                         instance.GetComponentsInChildren<Light>(true).Length == 0 &&
                         instance.GetComponentsInChildren<AudioListener>(true).Length == 0 &&
                         instance.GetComponentsInChildren<MonoBehaviour>(true).Length == 0;
            text.Append(" \"distinct_materials\": ").Append(materials).Append(",\n");
            text.Append(" \"renderers_and_lod_groups_only\": ").Append(clean ? "true" : "false").Append("\n}\n");
            Object.DestroyImmediate(instance);
            Directory.CreateDirectory(Root + "/Evidence~");
            File.WriteAllText(Root + "/Evidence~/unity_import_report.json", text.ToString());
            Debug.Log("[A7X] import report\n" + text);
        }

        // ------------------------------------------------------------------ install
        private static Transform FindOrbit(Scene scene)
        {
            GameObject orbit = scene.GetRootGameObjects().SingleOrDefault(root => root.name == OrbitSetName);
            if (!orbit)
            {
                throw new InvalidOperationException("The open scene has no root '" + OrbitSetName + "'. Open Bootstrap first.");
            }

            return orbit.transform;
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (!child)
            {
                throw new InvalidOperationException("Missing '" + parent.name + "/" + name + "'.");
            }

            return child;
        }

        /// <summary>Swaps the V2 hull and docking adapter into the orbital set. Marks the scene dirty; never saves.</summary>
        public static string Install(Scene scene)
        {
            var hullAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HullPrefab);
            var adapterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AdapterPrefab);
            if (!hullAsset || !adapterAsset)
            {
                throw new InvalidOperationException("Prefabs missing: run 'Rebuild materials and prefabs' first.");
            }

            Transform orbit = FindOrbit(scene);
            Transform hullGroup = Child(orbit, HullGroupName);
            Transform collar = Child(orbit, CollarGroupName);
            Remove(hullGroup, InstalledHullName);
            Remove(collar, InstalledAdapterName);

            // The delivered renderers stay in the scene, switched off, so Uninstall restores them exactly.
            foreach (Transform parent in new[] { hullGroup, collar })
            {
                foreach (Transform child in parent)
                {
                    Undo.RecordObject(child.gameObject, "Install AURORA-7 exterior V2");
                    child.gameObject.SetActive(false);
                }
            }

            var hull = (GameObject)PrefabUtility.InstantiatePrefab(hullAsset, hullGroup);
            hull.name = InstalledHullName;
            hull.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(hull, "Install AURORA-7 exterior V2");
            var adapter = (GameObject)PrefabUtility.InstantiatePrefab(adapterAsset, collar);
            adapter.name = InstalledAdapterName;
            adapter.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(adapter, "Install AURORA-7 exterior V2");
            EditorSceneManager.MarkSceneDirty(scene);
            return "INSTALLED into '" + scene.path + "' (scene not saved). " + Validate(orbit);
        }

        public static string Uninstall(Scene scene)
        {
            Transform orbit = FindOrbit(scene);
            Transform hullGroup = Child(orbit, HullGroupName);
            Transform collar = Child(orbit, CollarGroupName);
            bool removed = Remove(hullGroup, InstalledHullName) | Remove(collar, InstalledAdapterName);
            foreach (Transform parent in new[] { hullGroup, collar })
            {
                foreach (Transform child in parent)
                {
                    Undo.RecordObject(child.gameObject, "Uninstall AURORA-7 exterior V2");
                    child.gameObject.SetActive(true);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return (removed ? "UNINSTALLED" : "NOTHING INSTALLED") + "; delivered renderers re-enabled (scene not saved).";
        }

        private static bool Remove(Transform parent, string name)
        {
            bool any = false;
            foreach (Transform child in parent.Cast<Transform>().Where(t => t.name == name).ToArray())
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                any = true;
            }

            return any;
        }

        /// <summary>The same geometric conditions LeadDepartureAuthoring.ValidateOrbit enforces on the orbital set.</summary>
        public static string Validate(Transform orbit)
        {
            Transform hullGroup = Child(orbit, HullGroupName);
            Transform receiver = Child(orbit, ReceiverName);
            MeshRenderer[] hull = hullGroup.GetComponentsInChildren<MeshRenderer>(true);
            Matrix4x4 toReceiver = receiver.worldToLocalMatrix;
            float maxAlong = MeshBounds(hull, toReceiver).max.z;
            Transform adapter = Child(orbit, CollarGroupName).Find(InstalledAdapterName);
            float adapterEnd = adapter
                ? MeshBounds(adapter.GetComponentsInChildren<MeshRenderer>(true), toReceiver).max.z
                : float.NaN;
            int foreign = orbit.GetComponentsInChildren<Camera>(true).Length +
                          orbit.GetComponentsInChildren<AudioListener>(true).Length +
                          orbit.GetComponentsInChildren<Collider>(true).Length;
            bool pass = hull.Length >= 3 && maxAlong <= -0.60f && foreign == 0 &&
                        (float.IsNaN(adapterEnd) || Mathf.Abs(adapterEnd) <= 0.002f);
            return string.Format(CultureInfo.InvariantCulture,
                "VALIDATE {0}: hull renderers {1}, hull max along docking axis {2:0.000} m (limit -0.600), " +
                "adapter contact face at {3:0.0000} m from the contact plane, cameras/listeners/colliders {4}.",
                pass ? "PASS" : "FAIL", hull.Length, maxAlong, adapterEnd, foreign);
        }

        // ------------------------------------------------------------------ evidence
        private static readonly (string name, Vector3 eye, Vector3 target, float verticalFov)[] ExtraViews =
        {
            ("port_quarter", new Vector3(27f, 11f, -15f), new Vector3(0f, 1.3f, 8.5f), 38f),
            ("aft_close", new Vector3(6.5f, 4.6f, 30.5f), new Vector3(0.3f, 1.2f, 21.6f), 44f),
            ("habitat_detail", new Vector3(9.5f, 7.5f, 3.5f), new Vector3(2.2f, 2.6f, 9.5f), 40f),
            ("profile", new Vector3(46f, 6f, 9.5f), new Vector3(0f, 1.3f, 9.5f), 34f),
        };

        /// <summary>Opens Bootstrap in memory, renders the two authored orbit poses before and after the swap,
        /// plus extra views of the installed vehicle, under the orbit lighting of I06MarineBindings. Never saves.</summary>
        public static void Capture()
        {
            ShaderUtil.allowAsyncCompilation = false;
            Scene scene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
            Transform orbit = FindOrbit(scene);
            bool wasActive = orbit.gameObject.activeSelf;
            orbit.gameObject.SetActive(true);
            Transform approach = Child(orbit, ApproachName);
            Transform dock = Child(orbit, DockName);

            // Orbit environment as I06MarineBindings.ApplyOrbitEnvironment sets it at runtime.
            Material sky = null;
            foreach (MonoBehaviour behaviour in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if (behaviour && behaviour.GetType().Name == "I06MarineBindings")
                {
                    sky = new SerializedObject(behaviour).FindProperty("orbitalSky")?.objectReferenceValue as Material;
                }
            }

            RenderSettings.skybox = sky;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.32f, .39f, .52f);
            RenderSettings.ambientEquatorColor = new Color(.20f, .25f, .35f);
            RenderSettings.ambientGroundColor = new Color(.10f, .14f, .22f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = .32f;
            Light sun = RenderSettings.sun;
            if (!sun)
            {
                sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .FirstOrDefault(l => l.type == LightType.Directional);
            }

            if (!sun)
            {
                sun = new GameObject("A7X capture sun") { hideFlags = HideFlags.DontSave }.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
            }

            sun.color = new Color(1f, .96f, .89f);
            sun.intensity = 2.25f;
            sun.transform.rotation = Quaternion.Euler(26f, 220f, 0f);

            var cameraObject = new GameObject("A7X capture camera") { hideFlags = HideFlags.DontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = sky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.003f, 0.004f, 0.008f, 1f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 140f;
            camera.allowHDR = true;
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;

            const int width = 1920;
            const int height = 1080;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var readback = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            Directory.CreateDirectory(Root + "/Evidence~");

            void Shoot(string name, Vector3 position, Quaternion rotation, float fov)
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.fieldOfView = fov;
                for (int warm = 0; warm < 3; warm++)
                {
                    camera.Render();
                }

                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply();
                RenderTexture.active = null;
                string path = Root + "/Evidence~/unity_" + name + ".png";
                File.WriteAllBytes(path, readback.EncodeToPNG());
                Debug.Log("[A7X] captured " + path);
            }

            Shoot("before_approach_pose", approach.position, approach.rotation, 47f);
            Shoot("before_collar_pose", dock.position, dock.rotation, 41f);
            string result = Install(scene);
            Debug.Log("[A7X] " + result);
            File.WriteAllText(Root + "/Evidence~/unity_install_validation.txt",
                "Unity " + Application.unityVersion + ", scene " + scene.path + " opened in memory, not saved.\n" + result + "\n");
            Shoot("after_approach_pose", approach.position, approach.rotation, 47f);
            Shoot("after_collar_pose", dock.position, dock.rotation, 41f);
            foreach (var view in ExtraViews)
            {
                Vector3 eye = orbit.TransformPoint(view.eye);
                Vector3 look = orbit.TransformPoint(view.target);
                Shoot("after_" + view.name, eye, Quaternion.LookRotation(look - eye, Vector3.up), view.verticalFov);
            }

            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(cameraObject);
            orbit.gameObject.SetActive(wasActive);
            // Leave nothing behind: reopen the saved scene so the in-memory edits are discarded.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
