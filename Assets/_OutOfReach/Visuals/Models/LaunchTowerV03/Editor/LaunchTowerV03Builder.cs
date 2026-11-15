using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Visuals.LaunchTowerV03.Editor
{
    /// <summary>Rebuilds the LaunchTowerV03 texture/model import settings, the twelve URP Lit materials and the
    /// drop-in prefab from the Blender export. Idempotent. Never touches a scene.</summary>
    public static class LaunchTowerV03Builder
    {
        public const string Root = "Assets/_OutOfReach/Visuals/Models/LaunchTowerV03";
        public const string FbxPath = Root + "/Models/LaunchTowerV03.fbx";
        public const string PrefabPath = Root + "/Prefabs/LaunchTowerV03.prefab";
        public const string PrefabName = "LaunchTowerV03";
        public const string TowerName = "LTV3_Tower";
        public const string ArmName = "LTV3_AccessArm";
        public const string ArmCollidersName = "LTV3_ArmWalkColliders";
        // Same pivot as TransportLaunch AccessArmPivot / LaunchTowerV02 LTV2_ArmDetail (prologue root frame, metres).
        public static readonly Vector3 ArmPivot = new Vector3(-0.9f, 0f, -6.9f);

        // name -> (metallic, smoothness). Scalars on purpose: no metallic/smoothness map, so a missing or mis-imported
        // texture can never turn the paint into a mirror that reflects the sky.
        private static readonly Dictionary<string, Vector2> TexturedScalars = new Dictionary<string, Vector2>
        {
            ["LT3_SteelGraphite"] = new Vector2(0f, .38f), ["LT3_SafetyAmber"] = new Vector2(0f, .40f),
            ["LT3_Galvanized"] = new Vector2(.5f, .35f), ["LT3_Grating"] = new Vector2(.5f, .30f),
            ["LT3_WhiteRoom"] = new Vector2(0f, .35f), ["LT3_WarningRed"] = new Vector2(0f, .40f),
            ["LT3_Rubber"] = new Vector2(0f, .20f), ["LT3_Hazard"] = new Vector2(0f, .35f),
        };
        private static readonly string[] Textured = TexturedScalars.Keys.ToArray();
        private static readonly Dictionary<string, Color> Lamps = new Dictionary<string, Color>
        {
            ["LT3_LampWarm"] = new Color(1f, .8f, .55f) * 3f,
            ["LT3_LampRed"] = new Color(1f, .06f, .03f) * 4f,
            ["LT3_LampCyan"] = new Color(.2f, .9f, 1f) * 2.5f,
        };
        private const string Glass = "LT3_GlassDark";

        [MenuItem("OUT OF REACH/Launch Tower V03/Rebuild Materials and Prefab", priority = 40)]
        public static void BuildAll()
        {
            var report = new StringBuilder();
            ConfigureTextures(report);
            Dictionary<string, Material> materials = BuildMaterials(report);
            ConfigureModel(materials, report);
            BuildPrefab(report);
            AssetDatabase.SaveAssets();
            Debug.Log("LAUNCH_TOWER_V03_BUILD OK\n" + report);
        }

        private static void ConfigureTextures(StringBuilder report)
        {
            foreach (string path in Directory.GetFiles(Root + "/Textures", "*.png").Select(p => p.Replace('\\', '/')))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path)
                    ?? throw new InvalidOperationException("Texture not imported: " + path);
                bool normal = path.EndsWith("_Normal.png", StringComparison.Ordinal);
                bool linear = normal;
                bool changed = false;
                void Set<T>(T current, T wanted, Action<T> apply) { if (!EqualityComparer<T>.Default.Equals(current, wanted)) { apply(wanted); changed = true; } }
                Set(importer.textureType, normal ? TextureImporterType.NormalMap : TextureImporterType.Default, v => importer.textureType = v);
                Set(importer.sRGBTexture, !linear, v => importer.sRGBTexture = v);
                Set(importer.mipmapEnabled, true, v => importer.mipmapEnabled = v);
                Set(importer.anisoLevel, 4, v => importer.anisoLevel = v);
                Set(importer.wrapMode, TextureWrapMode.Repeat, v => importer.wrapMode = v);
                Set(importer.maxTextureSize, 1024, v => importer.maxTextureSize = v);
                Set(importer.alphaSource, TextureImporterAlphaSource.FromInput, v => importer.alphaSource = v);
                if (path.Contains("LT3_Grating_BaseColor"))
                    Set(importer.mipMapsPreserveCoverage, true, v => { importer.mipMapsPreserveCoverage = v; importer.alphaTestReferenceValue = .5f; });
                if (changed) importer.SaveAndReimport();
                report.AppendLine("texture " + Path.GetFileName(path) + (normal ? " normal" : linear ? " linear" : " sRGB") + (changed ? " (set)" : ""));
            }
        }

        private static Texture2D Tex(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + ".png");

        private static Dictionary<string, Material> BuildMaterials(StringBuilder report)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("URP Lit shader not found.");
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (string name in Textured.Concat(Lamps.Keys).Concat(new[] { Glass }))
            {
                string path = Root + "/Materials/" + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat)
                {
                    mat = new Material(lit) { name = name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = lit;
                mat.SetFloat("_WorkflowMode", 1f);
                mat.SetFloat("_Surface", 0f);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Cull", 2f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetTexture("_MetallicGlossMap", null);
                // One keyword set (_NORMALMAP + _EMISSION) for every LT3 material, so they share a single URP Lit
                // variant: the lamps never sit on a separate still-compiling (editor cyan placeholder) variant.
                mat.EnableKeyword("_NORMALMAP");
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_BumpMap", null);
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                mat.renderQueue = -1;
                if (Textured.Contains(name))
                {
                    Texture2D baseMap = Tex(name + "_BaseColor") ?? throw new InvalidOperationException("Missing texture " + name
                        + "_BaseColor.png (Git LFS pointer not smudged?)");
                    mat.SetTexture("_BaseMap", baseMap);
                    mat.SetTexture("_MainTex", baseMap);
                    mat.SetFloat("_Metallic", TexturedScalars[name].x);
                    mat.SetFloat("_Smoothness", TexturedScalars[name].y);
                    mat.SetFloat("_SmoothnessTextureChannel", 0f);
                    mat.SetTexture("_BumpMap", Tex(name + "_Normal"));
                    mat.SetFloat("_BumpScale", .8f);
                    if (name == "LT3_Grating")
                    {
                        mat.SetFloat("_AlphaClip", 1f);
                        mat.SetFloat("_Cutoff", .5f);
                        mat.SetFloat("_Cull", 0f);
                        mat.EnableKeyword("_ALPHATEST_ON");
                        mat.SetOverrideTag("RenderType", "TransparentCutout");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    }
                }
                else if (Lamps.TryGetValue(name, out Color emission))
                {
                    Color c = emission.maxColorComponent > 0 ? emission / emission.maxColorComponent : Color.white;
                    mat.SetColor("_BaseColor", c);
                    mat.SetFloat("_Metallic", 0f);
                    mat.SetFloat("_Smoothness", .85f);
                    mat.SetColor("_EmissionColor", emission);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                else
                {
                    mat.SetColor("_BaseColor", new Color(.02f, .025f, .03f, 1f));
                    mat.SetFloat("_Metallic", 0f);
                    mat.SetFloat("_Smoothness", .7f);
                }
                EditorUtility.SetDirty(mat);
                result.Add(name, mat);
            }
            report.AppendLine("materials " + result.Count + " URP Lit");
            return result;
        }

        private static void ConfigureModel(Dictionary<string, Material> materials, StringBuilder report)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath)
                ?? throw new InvalidOperationException("FBX not imported: " + FbxPath);
            importer.bakeAxisConversion = true;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.preserveHierarchy = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.weldVertices = true;
            importer.generateSecondaryUV = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
            report.AppendLine("model importer: bakeAxisConversion, scale 1, " + materials.Count + " material remaps");
        }

        private static void BuildPrefab(StringBuilder report)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath) ?? throw new InvalidOperationException("Missing " + FbxPath);
            Transform srcTower = model.transform.Find(TowerName) ?? throw new InvalidOperationException("FBX lacks " + TowerName);
            Transform srcArm = model.transform.Find(ArmName) ?? throw new InvalidOperationException("FBX lacks " + ArmName);
            if (Vector3.Distance(srcTower.localPosition, Vector3.zero) > .001f || Vector3.Distance(srcArm.localPosition, ArmPivot) > .001f
                || Quaternion.Angle(srcArm.localRotation, Quaternion.identity) > .01f || (srcArm.localScale - Vector3.one).sqrMagnitude > 1e-6f)
                throw new InvalidOperationException("Imported axes/pivots differ: tower " + srcTower.localPosition.ToString("F4")
                    + " arm " + srcArm.localPosition.ToString("F4") + " " + srcArm.localRotation.eulerAngles.ToString("F2"));

            Directory.CreateDirectory(Root + "/Prefabs");
            var root = new GameObject(PrefabName);
            try
            {
                Transform tower = Visual(srcTower, root.transform);
                Transform arm = Visual(srcArm, root.transform);
                Anchor("Anchor_AccessArmPivot", root.transform, ArmPivot);
                Anchor("Anchor_DeckJoint", arm, new Vector3(0f, 0f, -6.9f));
                Anchor("Anchor_HatchThreshold", arm, new Vector3(0f, 0f, -2.5f));

                // Walking-surface colliders, prologue-frame boxes (centre, size). They coincide with the existing
                // COL_ArmFloor / COL_ArmRail* boxes so standing height and rail line do not change, and add the
                // white-room walls, door jambs and ceiling. Nothing crosses the walkway (x +-0.93, y 0.02..2.2) or doorway.
                var cols = new GameObject(ArmCollidersName).transform;
                cols.SetParent(arm, false);
                Box(cols, "COL_V3_Floor", new Vector3(0f, -.03f, -4.76f), new Vector3(1.92f, .06f, 4.28f));
                Box(cols, "COL_V3_RailL", new Vector3(-.955f, .6f, -5.825f), new Vector3(.05f, 1.2f, 2.15f));
                Box(cols, "COL_V3_RailR", new Vector3(.955f, .6f, -5.825f), new Vector3(.05f, 1.2f, 2.15f));
                Box(cols, "COL_V3_WhiteRoomWallL", new Vector3(-1.11f, 1.32f, -3.685f), new Vector3(.08f, 2.64f, 2.13f));
                Box(cols, "COL_V3_WhiteRoomWallR", new Vector3(1.11f, 1.32f, -3.685f), new Vector3(.08f, 2.64f, 2.13f));
                Box(cols, "COL_V3_DoorJambL", new Vector3(-1.05f, 1.32f, -4.75f), new Vector3(.2f, 2.64f, .08f));
                Box(cols, "COL_V3_DoorJambR", new Vector3(1.05f, 1.32f, -4.75f), new Vector3(.2f, 2.64f, .08f));
                Box(cols, "COL_V3_Ceiling", new Vector3(0f, 2.54f, -4.76f), new Vector3(2.3f, .32f, 4.28f));

                int tris = new[] { tower, arm }.Sum(t => t.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);
                if (tower.GetComponent<MeshRenderer>().sharedMaterials.Concat(arm.GetComponent<MeshRenderer>().sharedMaterials)
                    .Any(m => !m || !m.name.StartsWith("LT3_", StringComparison.Ordinal)))
                    throw new InvalidOperationException("A V03 renderer has a missing or non-LT3 material.");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok) throw new InvalidOperationException("Could not save " + PrefabPath);
                report.AppendLine("prefab " + PrefabPath + " triangles=" + tris + " colliders=" + cols.childCount
                    + " armPivot=" + arm.localPosition.ToString("F3"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Transform Visual(Transform source, Transform parent)
        {
            var go = new GameObject(source.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = source.localPosition;
            go.transform.localRotation = source.localRotation;
            go.transform.localScale = source.localScale;
            go.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go.transform;
        }

        private static void Anchor(string name, Transform parent, Vector3 prologuePoint)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = prologuePoint - (parent.name == ArmName ? ArmPivot : Vector3.zero);
        }

        private static void Box(Transform parent, string name, Vector3 prologueCentre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = prologueCentre - ArmPivot;   // parent chain: arm at the pivot, identity rotation
            box.size = size;
        }
    }
}
