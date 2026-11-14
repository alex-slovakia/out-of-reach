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

namespace OutOfReach.ModelTools
{
    /// <summary>
    /// Editor-only import and preview tool for the AURORA-7 model deliverable (CODEX-006).
    /// It configures the texture and FBX importers, creates the URP Lit materials from
    /// Source/a7_materials.json, assembles Prefabs/Aurora7_Models.prefab (visual meshes, door leaves,
    /// props and separate collision proxies), checks axes and scale against the Blender report and
    /// renders preview stills. The prefab contains no camera, light, audio listener or gameplay script.
    /// Batch entry: -executeMethod OutOfReach.ModelTools.Aurora7ModelTools.BuildAndCapture (needs graphics).
    /// </summary>
    public static class Aurora7ModelTools
    {
        private const string Root = "Assets/_OutOfReach/Visuals/Models/Aurora7";
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string PrefabPath = Root + "/Prefabs/Aurora7_Models.prefab";
        private static readonly string[] Modules = { "Cockpit", "Engineering", "Storage", "Airlock", "Exterior" };

        [Serializable]
        private sealed class MaterialSpec
        {
            public string name;
            public string set;
            public string atlas;
            public string tint;
            public float tiling;
            public float normal;
            public string surface;
            public float alpha;
            public float metallic;
            public float smoothness;
            public string emission;
            public float emission_strength;
            public bool emission_from_atlas;
        }

        [Serializable]
        private sealed class MaterialFile
        {
            public List<MaterialSpec> materials;
        }

        [MenuItem("OUT OF REACH/Models/Rebuild AURORA-7 materials and prefab")]
        public static void BuildMenu()
        {
            Build();
        }

        [MenuItem("OUT OF REACH/Models/Capture AURORA-7 preview stills")]
        public static void CaptureMenu()
        {
            Capture();
        }

        public static void BuildAndCapture()
        {
            int code = 0;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                Build();
                Capture();
                Debug.Log("[A7] BUILD_AND_CAPTURE_COMPLETE");
            }
            catch (Exception exception)
            {
                Debug.LogError("[A7] FAILED " + exception);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        // ------------------------------------------------------------------ build
        public static void Build()
        {
            AssetDatabase.Refresh();
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(Root, "Previews");
            ConfigureTextures();
            Dictionary<string, Material> materials = CreateMaterials();
            foreach (string path in Directory.GetFiles(Root + "/Meshes", "*.fbx").Select(p => p.Replace('\\', '/')))
            {
                ConfigureModel(path, materials);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            BuildPrefab();
            AssetDatabase.SaveAssets();
            WriteReport();
        }

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
                bool isMask = name.EndsWith("_MODS", StringComparison.Ordinal);
                bool isAtlas = name == "A7_Decals" || name == "A7_Screens";
                importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (!isNormal)
                {
                    importer.sRGBTexture = !isMask;
                }

                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = name == "A7_Decals";
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = name == "A7_Decals";
                importer.alphaTestReferenceValue = 0.5f;
                importer.anisoLevel = 8;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = isAtlas ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                importer.maxTextureSize = name == "A7_Decals" ? 4096 : 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader lit = Shader.Find(LitShader);
            if (lit == null)
            {
                throw new InvalidOperationException("URP Lit shader not found: " + LitShader);
            }

            string json = File.ReadAllText(Root + "/Source/a7_materials.json");
            MaterialFile file = JsonUtility.FromJson<MaterialFile>(json);
            var result = new Dictionary<string, Material>();
            foreach (MaterialSpec spec in file.materials)
            {
                string path = Root + "/Materials/" + spec.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
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

        private static void Apply(Material material, MaterialSpec spec, Shader lit)
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
            material.SetShaderPassEnabled("ShadowCaster", true);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            Color tint = ParseHex(string.IsNullOrEmpty(spec.tint) ? "#FFFFFF" : spec.tint);
            float tiling = spec.tiling > 0f ? spec.tiling : 1f;
            material.SetColor("_BaseColor", tint);
            material.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));

            if (!string.IsNullOrEmpty(spec.set))
            {
                material.SetTexture("_BaseMap", LoadTexture(spec.set + "_BaseColor"));
                material.SetTexture("_BumpMap", LoadTexture(spec.set + "_Normal"));
                material.SetFloat("_BumpScale", spec.normal > 0f ? spec.normal : 1f);
                material.EnableKeyword("_NORMALMAP");
                Texture2D mask = LoadTexture(spec.set + "_MODS");
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
                material.SetFloat("_Smoothness", spec.smoothness);
            }

            if (!string.IsNullOrEmpty(spec.atlas))
            {
                Texture2D atlas = LoadTexture(spec.atlas);
                material.SetTextureScale("_BaseMap", Vector2.one);
                if (spec.emission_from_atlas)
                {
                    material.SetColor("_BaseColor", Color.black);
                    material.SetTexture("_EmissionMap", atlas);
                    float strength = spec.emission_strength > 0f ? spec.emission_strength : 1f;
                    material.SetColor("_EmissionColor", Color.white * strength);
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                else
                {
                    material.SetColor("_BaseColor", Color.white);
                    material.SetTexture("_BaseMap", atlas);
                }
            }

            if (!string.IsNullOrEmpty(spec.emission))
            {
                float strength = spec.emission_strength > 0f ? spec.emission_strength : 1f;
                material.SetColor("_EmissionColor", ParseHex(spec.emission).linear * strength);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }

            if (spec.surface == "cutout")
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
            else if (spec.surface == "transparent")
            {
                tint.a = spec.alpha > 0f ? spec.alpha : 0.1f;
                material.SetColor("_BaseColor", tint);
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
        }

        private static Texture2D LoadTexture(string name)
        {
            string path = Root + "/Textures/" + name + ".png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                throw new FileNotFoundException("Missing texture " + path);
            }

            return texture;
        }

        private static Color ParseHex(string text)
        {
            if (!ColorUtility.TryParseHtmlString(text, out Color colour))
            {
                throw new FormatException("Bad colour " + text);
            }

            return colour;
        }

        private static void ConfigureModel(string path, Dictionary<string, Material> materials)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
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
                if (materials.TryGetValue(embedded.name, out Material material))
                {
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), material);
                }
                else
                {
                    throw new InvalidOperationException(path + " uses material '" + embedded.name + "' with no entry in a7_materials.json");
                }
            }

            importer.SaveAndReimport();
        }

        private static void BuildPrefab()
        {
            var root = new GameObject("Aurora7_Models");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            const StaticEditorFlags staticFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                                  StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            foreach (string module in Modules)
            {
                GameObject instance = Instantiate(Root + "/Meshes/A7_" + module + ".fbx", visual.transform);
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, staticFlags);
                }
            }

            // Door leaves stay separate and movable: the integrator animates them, gameplay gates are not here.
            GameObject doors = Instantiate(Root + "/Meshes/A7_Doors.fbx", root.transform);
            doors.name = "DoorLeaves";

            // Repair-bay parts the integrator may switch or animate, plus helper points (no components).
            GameObject parts = Instantiate(Root + "/Meshes/A7_Parts.fbx", root.transform);
            parts.name = "EngineeringParts";
            Transform cover = parts.transform.Find("PowerCabinet_Cover");
            if (cover == null)
            {
                throw new InvalidOperationException("A7_Parts.fbx has no PowerCabinet_Cover");
            }

            // The fitted cover is delivered switched off, so the breaker cabinet reads as open (review-01 R02).
            cover.gameObject.SetActive(false);

            // Airlock parts: the suit, the glazed locker door, the pressure dial's pointer and a helper point.
            GameObject airlockParts = Instantiate(Root + "/Meshes/A7_AirlockParts.fbx", root.transform);
            airlockParts.name = "AirlockParts";

            // Antenna gimbal. The meshes keep ship axes; the two pivots give the integrator usable axes:
            // local Y is the hull normal at the pad (azimuth), local X is the trunnion (elevation).
            GameObject exteriorParts = Instantiate(Root + "/Meshes/A7_ExteriorParts.fbx", root.transform);
            exteriorParts.name = "ExteriorParts";
            // The gimbal objects are re-parented under pivots, which a connected model instance does not allow.
            PrefabUtility.UnpackPrefabInstance(exteriorParts, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform azimuth = exteriorParts.transform.Find("Antenna_Azimuth");
            Transform elevationMount = exteriorParts.transform.Find("Antenna_ElevationMount");
            if (azimuth == null || elevationMount == null)
            {
                throw new InvalidOperationException("A7_ExteriorParts.fbx lacks the antenna gimbal objects");
            }

            Vector3 padNormal = new Vector3(3.2f, 2.4f - 1.56f, 0f).normalized;
            Quaternion padRotation = Quaternion.LookRotation(Vector3.forward, padNormal);
            Transform azimuthPivot = new GameObject("Antenna_AzimuthPivot").transform;
            azimuthPivot.SetParent(exteriorParts.transform, false);
            azimuthPivot.SetPositionAndRotation(azimuth.position, padRotation);
            Transform elevationPivot = new GameObject("Antenna_ElevationPivot").transform;
            elevationPivot.SetParent(azimuthPivot, false);
            elevationPivot.SetPositionAndRotation(elevationMount.position, padRotation);
            azimuth.SetParent(azimuthPivot, true);
            UnityEngine.Object.DestroyImmediate(elevationMount.gameObject);

            // The reflector assembly is the prop author's part, authored in the elevation pivot's own frame
            // (X trunnion, Y hull normal, Z aft) with the delivered pose baked in.
            GameObject reflector = Instantiate(Root + "/Props/AntennaReflector/AntennaReflector.prefab", elevationPivot);
            reflector.name = "Antenna_Elevation";
            reflector.transform.localPosition = Vector3.zero;
            reflector.transform.localRotation = Quaternion.identity;

            var props = new GameObject("Props");
            props.transform.SetParent(root.transform, false);
            string panelPath = Root + "/Props/ServicePanel/ServicePanel.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(panelPath) != null)
            {
                // EPS-2 panel as the bus control panel on the wall plinth aft of the power rack:
                // pivot 0.55 m up, back plane on the plinth, front (+Z) into the room (+X).
                GameObject panel = Instantiate(panelPath, props.transform);
                panel.name = "ServicePanel_EpsControl";
                panel.transform.SetPositionAndRotation(new Vector3(-2.256f, 0.55f, 7.36f), Quaternion.Euler(0f, 90f, 0f));
                Transform panelCollider = panel.transform.Find("CollisionProxy");
                if (panelCollider != null)
                {
                    // Walkable space stays exactly the V01 envelope: only the COL_* proxies collide.
                    panelCollider.gameObject.SetActive(false);
                }
            }

            string arsPath = Root + "/Props/LifeSupportRack/LifeSupportRack.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(arsPath) != null)
            {
                // ARS-1 air revitalisation unit on the wall plinth forward of the scrubber rack:
                // pivot 0.45 m up, back plane on the plinth, front (+Z) into the room (-X).
                GameObject ars = Instantiate(arsPath, props.transform);
                ars.name = "LifeSupportRack_Ars1";
                ars.transform.SetPositionAndRotation(new Vector3(2.236f, 0.45f, 6.67f), Quaternion.Euler(0f, -90f, 0f));
                Transform arsCollider = ars.transform.Find("CollisionProxy");
                if (arsCollider != null)
                {
                    arsCollider.gameObject.SetActive(false);
                }
            }

            var collision = new GameObject("CollisionProxies");
            collision.transform.SetParent(root.transform, false);
            GameObject source = Instantiate(Root + "/Meshes/A7_Collision.fbx", null);
            foreach (MeshRenderer renderer in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                Bounds bounds = renderer.bounds;
                var proxy = new GameObject(renderer.name);
                proxy.transform.SetParent(collision.transform, false);
                proxy.transform.position = bounds.center;
                proxy.AddComponent<BoxCollider>().size = bounds.size;
            }

            UnityEngine.Object.DestroyImmediate(source);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static GameObject Instantiate(string path, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                throw new FileNotFoundException("Missing model " + path);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (parent != null)
            {
                instance.transform.SetParent(parent, false);
            }

            return instance;
        }

        // ------------------------------------------------------------------ validation and report
        private static void WriteReport()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var problems = new List<string>();
            var text = new StringBuilder();
            text.Append("{\n \"unity\": \"").Append(Application.unityVersion).Append("\",\n");
            text.Append(" \"pipeline\": \"").Append(GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "none").Append("\",\n");
            text.Append(" \"prefab\": \"").Append(PrefabPath).Append("\",\n \"objects\": [\n");

            long total = 0;
            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture>();
            var overall = new Bounds();
            bool first = true;
            MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in renderers)
            {
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                long triangles = 0;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    triangles += mesh.GetIndexCount(sub) / 3;
                }

                total += triangles;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null || material.shader.name.Contains("InternalError"))
                    {
                        problems.Add("missing or broken material on " + renderer.name);
                        continue;
                    }

                    materials.Add(material);
                    foreach (string property in material.GetTexturePropertyNames())
                    {
                        Texture texture = material.GetTexture(property);
                        if (texture != null)
                        {
                            textures.Add(texture);
                        }
                    }
                }

                Transform t = renderer.transform;
                if (Quaternion.Angle(t.rotation, Quaternion.identity) > 0.01f && !IsUnder(t, "Props") && !IsUnder(t, "Antenna_Elevation"))
                {
                    problems.Add(renderer.name + " is not at identity rotation: " + t.rotation.eulerAngles);
                }

                if ((t.lossyScale - Vector3.one).magnitude > 0.001f)
                {
                    problems.Add(renderer.name + " is not at unit scale: " + t.lossyScale);
                }

                if (first)
                {
                    overall = renderer.bounds;
                }
                else
                {
                    overall.Encapsulate(renderer.bounds);
                }

                text.Append(first ? string.Empty : ",\n");
                first = false;
                text.Append("  {\"name\": \"").Append(renderer.name).Append("\", \"triangles\": ").Append(triangles)
                    .Append(", \"materials\": ").Append(renderer.sharedMaterials.Length)
                    .Append(", \"bounds_min\": ").Append(Vec(renderer.bounds.min)).Append(", \"bounds_max\": ").Append(Vec(renderer.bounds.max)).Append('}');
            }

            // Axis and handedness check: these proxies sit at asymmetric, known positions.
            CheckProxy(instance, "COL_Engineering_PowerRack", new Vector3(-2.02f, 1.15f, 6.1f), problems);
            CheckProxy(instance, "COL_Engineering_Tank1", new Vector3(1.99f, 1.1f, 4.85f), problems);
            CheckProxy(instance, "COL_Cockpit_ForwardBulkhead", new Vector3(0f, 1.55f, -3.05f), problems);
            // Part pivots and helper points must survive the export: checked against the generator's numbers.
            CheckProxy(instance, "PowerCabinet_IsolatorHandle", new Vector3(-1.83f, 0.8036f, 5.85f), problems);
            CheckProxy(instance, "ScrubberCanister_A", new Vector3(1.855f, 0.8f, 8.24f), problems);
            CheckProxy(instance, "LightAnchor_PowerCabinetWork", new Vector3(-1.758f, 1.72f, 6.35f), problems);
            CheckProxy(instance, "AirlockGauge_Needle", new Vector3(1.45f, 1.7f, 20.832f), problems);
            CheckProxy(instance, "SuitLocker_Door", new Vector3(1.812f, 0.16f, 19.588f), problems);
            // The antenna's manual drive socket must sit on the AntennaRepair anchor.
            CheckProxy(instance, "Antenna_ServiceSocket", new Vector3(3.2f, 2.4f, 13f), problems);
            CheckProxy(instance, "Antenna_Elevation", new Vector3(3.83f, 2.565f, 12.72f), problems);
            int colliders = instance.GetComponentsInChildren<Collider>(true).Length;
            int activeColliders = instance.GetComponentsInChildren<Collider>(false).Length;
            if (instance.GetComponentsInChildren<Camera>(true).Length + instance.GetComponentsInChildren<AudioListener>(true).Length +
                instance.GetComponentsInChildren<Light>(true).Length + instance.GetComponentsInChildren<MonoBehaviour>(true).Length > 0)
            {
                problems.Add("prefab must not contain cameras, listeners, lights or scripts");
            }

            text.Append("\n ],\n \"triangles_total\": ").Append(total);
            text.Append(",\n \"renderers\": ").Append(renderers.Length);
            text.Append(",\n \"materials\": ").Append(materials.Count);
            text.Append(",\n \"textures\": ").Append(textures.Count);
            text.Append(",\n \"colliders\": ").Append(colliders);
            text.Append(",\n \"colliders_active\": ").Append(activeColliders);
            text.Append(",\n \"bounds_min\": ").Append(Vec(overall.min)).Append(",\n \"bounds_max\": ").Append(Vec(overall.max));
            text.Append(",\n \"problems\": [").Append(string.Join(", ", problems.Select(p => "\"" + p.Replace("\"", "'") + "\""))).Append("]\n}\n");
            File.WriteAllText(Root + "/Meshes/a7_unity_import_report.json", text.ToString());
            UnityEngine.Object.DestroyImmediate(instance);
            AssetDatabase.ImportAsset(Root + "/Meshes/a7_unity_import_report.json");
            Debug.Log("[A7] import report: " + total + " triangles, " + materials.Count + " materials, " + textures.Count + " textures, " +
                      colliders + " colliders (" + activeColliders + " active), problems: " + problems.Count);
            if (problems.Count > 0)
            {
                throw new InvalidOperationException("[A7] validation failed: " + string.Join("; ", problems));
            }
        }

        private static bool IsUnder(Transform transform, string ancestor)
        {
            for (Transform t = transform; t != null; t = t.parent)
            {
                if (t.name == ancestor)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CheckProxy(GameObject instance, string name, Vector3 expected, List<string> problems)
        {
            Transform proxy = instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (proxy == null)
            {
                problems.Add("collision proxy missing: " + name);
            }
            else if ((proxy.position - expected).magnitude > 0.01f)
            {
                problems.Add(name + " is at " + proxy.position.ToString("F3") + ", expected " + expected.ToString("F3") + " (axis or scale error)");
            }
        }

        private static string Vec(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "[{0:0.###}, {1:0.###}, {2:0.###}]", v.x, v.y, v.z);
        }

        // ------------------------------------------------------------------ preview stills (lighting here is preview-only)
        private static readonly (string name, Vector3 eye, Vector3 target, float horizontalFov)[] Views =
        {
            ("cockpit_from_spawn", new Vector3(0f, 1.65f, 0.8f), new Vector3(0f, 1.55f, -3f), 78f),
            ("cockpit_three_quarter", new Vector3(2.2f, 1.95f, 2.3f), new Vector3(-0.6f, 1.15f, -2.2f), 70f),
            // review-01 R01 framing at 1.65 m eye height
            ("cockpit_r01", new Vector3(-0.55f, 1.65f, 2.2f), new Vector3(0.9f, 1.15f, -2.3f), 85f),
            ("engineering_corridor", new Vector3(0.25f, 1.65f, 3.6f), new Vector3(0f, 1.35f, 11f), 76f),
            // review-01 R02 framing at 1.65 m eye height; 90 degrees horizontal is close to a 60 degree vertical game camera
            ("engineering_r02", new Vector3(0.6f, 1.65f, 3.4f), new Vector3(-0.9f, 1.3f, 11f), 90f),
            ("engineering_power", new Vector3(-0.25f, 1.55f, 6.75f), new Vector3(-2f, 1.2f, 6.05f), 72f),
            ("engineering_eclss", new Vector3(0.25f, 1.55f, 7.5f), new Vector3(2f, 1.2f, 8.15f), 72f),
            ("storage", new Vector3(0f, 1.65f, 11.5f), new Vector3(0f, 1.3f, 17f), 76f),
            // review-01 R03 framing at 1.65 m eye height
            ("storage_r03", new Vector3(0.7f, 1.65f, 11.25f), new Vector3(-0.9f, 1.15f, 17f), 90f),
            ("airlock", new Vector3(0.2f, 1.65f, 17.35f), new Vector3(-0.2f, 1.3f, 21f), 80f),
            // review-01 R04 framing at 1.65 m eye height
            ("airlock_r04", new Vector3(-0.75f, 1.65f, 17.55f), new Vector3(0.85f, 1.3f, 21f), 90f),
            ("exterior_finale", new Vector3(9f, 5f, 27f), new Vector3(0.5f, 1.6f, 12f), 58f),
            ("exterior_forward", new Vector3(-9.5f, 4.5f, -11f), new Vector3(0f, 1.6f, 4f), 55f),
            ("exterior_eva", new Vector3(2.2f, 1.6f, 24.6f), new Vector3(1.2f, 2.2f, 14f), 72f),
            // towards review-01 R06: the antenna work site from 0.6 m off the skin, aft of the pad, head away from the hull
            ("exterior_antenna", new Vector3(3.514f, 2.431f, 14.25f), new Vector3(3.434f, 2.462f, 12.9f), 90f),
        };

        private static readonly float[] FixtureZ = { -1.6f, 1.4f, 4.4f, 7.4f, 10.4f, 12.4f, 15.4f, 18.4f };

        public static void Capture()
        {
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var shipInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.085f, 0.078f, 0.07f);
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = RoomReflection();
            RenderSettings.reflectionIntensity = 1f;

            int index = 0;
            foreach (float z in FixtureZ)
            {
                var light = new GameObject("PreviewLight " + z.ToString("0.0", CultureInfo.InvariantCulture)).AddComponent<Light>();
                light.type = LightType.Spot;
                light.transform.SetPositionAndRotation(new Vector3(0f, 2.93f, z), Quaternion.Euler(90f, 0f, 0f));
                light.color = new Color(1f, 0.92f, 0.81f);
                light.intensity = 10f;
                light.range = 7.5f;
                light.spotAngle = 150f;
                light.innerSpotAngle = 110f;
                light.shadows = index % 2 == 0 ? LightShadows.Soft : LightShadows.None;
                light.shadowStrength = 0.75f;
                index++;
            }

            // Preview stand-ins for the amber work light in the breaker cabinet and the suit locker's interior
            // light (the prefab carries only the anchors).
            foreach (Transform anchor in shipInstance.GetComponentsInChildren<Transform>(true)
                         .Where(t => t.name == "LightAnchor_PowerCabinetWork" || t.name == "LightAnchor_SuitLocker"))
            {
                bool locker = anchor.name == "LightAnchor_SuitLocker";
                var work = new GameObject("Preview " + anchor.name).AddComponent<Light>();
                work.type = LightType.Point;
                work.transform.position = anchor.position;
                work.color = locker ? new Color(1f, 0.9f, 0.76f) : new Color(1f, 0.58f, 0.2f);
                work.intensity = locker ? 0.9f : 0.8f;
                work.range = locker ? 1.6f : 1.2f;
                work.shadows = LightShadows.Soft;
            }

            // Hard sunlight is switched on for the exterior views only.
            var sun = new GameObject("PreviewSun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(-0.45f, -0.62f, -0.64f));
            sun.color = new Color(1f, 0.98f, 0.95f);
            sun.intensity = 2.4f;
            sun.shadows = LightShadows.Soft;
            sun.enabled = false;

            var volumeObject = new GameObject("PreviewVolume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.6f);
            var colour = profile.Add<ColorAdjustments>(true);
            colour.postExposure.Override(0.35f);
            colour.contrast.Override(10f);
            colour.saturation.Override(-10f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.5f);
            volume.sharedProfile = profile;

            var cameraObject = new GameObject("PreviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.003f, 0.004f, 0.008f, 1f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            camera.allowHDR = true;
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;

            const int width = 1600;
            const int height = 900;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var readback = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            foreach (var view in Views)
            {
                sun.enabled = view.name.StartsWith("exterior", StringComparison.Ordinal);
                camera.transform.position = view.eye;
                // Outside, "up" for a crew member at the antenna is the hull normal there, not the ship's deck normal.
                Vector3 up = view.name == "exterior_antenna" ? new Vector3(3.2f, 2.4f - 1.56f, 0f).normalized : Vector3.up;
                camera.transform.LookAt(view.target, up);
                camera.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(view.horizontalFov * Mathf.Deg2Rad / 2f) * height / width) * Mathf.Rad2Deg;
                for (int warm = 0; warm < 3; warm++)
                {
                    camera.Render();
                }

                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply();
                RenderTexture.active = null;
                string path = Root + "/Previews/A7_unity_urp_" + view.name + ".png";
                File.WriteAllBytes(path, readback.EncodeToPNG());
                Debug.Log("[A7] captured " + path);
            }

            camera.targetTexture = null;
            target.Release();
            AssetDatabase.Refresh();
        }

        /// <summary>Soft warm box so metals reflect a lit room instead of a black sky. Preview only.</summary>
        private static Cubemap RoomReflection()
        {
            const int size = 32;
            var cube = new Cubemap(size, TextureFormat.RGBAHalf, true);
            foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
            {
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float value;
                        if (face == CubemapFace.PositiveY)
                        {
                            float stripe = Mathf.Abs(x - size / 2f) < size * 0.18f ? 2.6f : 0.9f;
                            value = stripe;
                        }
                        else if (face == CubemapFace.NegativeY)
                        {
                            value = 0.22f;
                        }
                        else
                        {
                            value = Mathf.Lerp(0.95f, 0.28f, y / (float)(size - 1));
                        }

                        value *= 0.6f;
                        pixels[y * size + x] = new Color(value, value * 0.95f, value * 0.88f, 1f);
                    }
                }

                cube.SetPixels(pixels, face);
            }

            cube.Apply(true);
            return cube;
        }
    }
}
