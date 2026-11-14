using System;
using System.Collections.Generic;
using System.Linq;
using OutOfReach.Presentation;
using OutOfReach.Presentation.Habitat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Explicit scene-instance installation of pinned HABITAT-FIT-02. Never saves a source prefab or scene.</summary>
    public static class I06HabitatAuthoring
    {
        public const string AssetRoot = "Assets/_OutOfReach/Visuals/Models/Habitat";
        public const string SceneRootName = "I06 authored Storage habitat";
        private const string ShipMaterials = "Assets/_OutOfReach/Visuals/Models/Aurora7/Materials/";
        private const string ShipTextures = "Assets/_OutOfReach/Visuals/Models/Aurora7/Textures/";

        public sealed class Result
        {
            public Transform Root, StorageShell, PlacedKit;
            public Transform SleepEntry, SleepCamera, PersonalStowage, RationPickup, WaterDispenser, GrowInteraction, GrowTray;
            public Transform RationInteractionSurface, WaterInteractionSurface;
            public Transform GalleyTable, GrowLid, GrowTrayMesh, GrowSeedlings;
            public BoxCollider[] Colliders;
            public HabitatStatusPresenter Status;
            public TextMesh MissionDayReadout, GrowReadout;
        }

#pragma warning disable 0649
        [Serializable] private sealed class Layout { public Anchor[] anchors; public Box[] colliders; public MaterialSpec[] materials; }
        [Serializable] private sealed class Anchor { public string name; public float[] point, forward; }
        [Serializable] private sealed class Box { public string name; public float[] centre, size; }
        [Serializable] private sealed class MaterialSpec
        {
            public string name, set, atlas, tint, surface, emission;
            public float tiling, normal, alpha, metallic, smoothness, emission_strength;
        }
#pragma warning restore 0649

        /// <summary>
        /// Supply the delivered-model ship-space root, its exact old Storage object and the two old locker colliders.
        /// Installs ship-space assets at zero/identity/unit. Only the explicit old scene renderers/colliders are disabled.
        /// Ration/water/rest/grow commands, C05 relocation and interaction targets remain root's responsibility.
        /// </summary>
        public static Result Create(Transform shipSpace, Transform oldStorage, Collider oldLockersL, Collider oldLockersR, Font font)
        {
            if (!shipSpace || !oldStorage || !font || oldStorage.name != "A7_Storage" || !oldStorage.IsChildOf(shipSpace)
                || !oldLockersL || !oldLockersR || oldLockersL.name != "COL_Storage_LockersL" || oldLockersR.name != "COL_Storage_LockersR"
                || !oldLockersL.transform.IsChildOf(shipSpace) || !oldLockersR.transform.IsChildOf(shipSpace))
                throw new ArgumentException("Pass the explicit delivered ship, A7_Storage and both named locker collider scene instances.");
            if (EditorUtility.IsPersistent(shipSpace.gameObject) || EditorUtility.IsPersistent(oldStorage.gameObject)
                || shipSpace.Find(SceneRootName)) throw new InvalidOperationException("Use an unmodified scene instance; habitat already present or prefab asset passed.");
            if ((shipSpace.lossyScale - Vector3.one).sqrMagnitude > .000001f
                || shipSpace.InverseTransformPoint(oldStorage.position).sqrMagnitude > .000001f
                || Quaternion.Angle(shipSpace.rotation, oldStorage.rotation) > .01f)
                throw new InvalidOperationException("Storage replacement requires the authored ship-space origin, axes and metre scale.");
            Layout layout = JsonUtility.FromJson<Layout>(Required<TextAsset>(AssetRoot + "/HabitatLayout.json").text);
            if (layout?.anchors?.Length != 7 || layout.colliders?.Length != 5 || layout.materials == null)
                throw new InvalidOperationException("Use the pinned seven-anchor/five-box habitat manifest.");
            PrepareAssets(layout);
            GameObject shellAsset = Required<GameObject>(AssetRoot + "/Meshes/A7_Storage_Habitat.fbx");
            GameObject kitAsset = Required<GameObject>(AssetRoot + "/Meshes/HB_Habitat_Placed.fbx");
            ValidateGeometry(shellAsset, 1, 25690); ValidateGeometry(kitAsset, 7, 37210);
            Transform root = Child(SceneRootName, shipSpace);
            try
            {
                Transform shell = Instantiate(shellAsset, root), kit = Instantiate(kitAsset, root);
                BindMaterials(shell); BindMaterials(kit);
                var anchors = new Dictionary<string, Transform>(StringComparer.Ordinal);
                Transform anchorRoot = Child("Interaction anchor frames - root binds commands", root);
                foreach (Anchor spec in layout.anchors)
                {
                    Transform original = Named(kit, spec.name);
                    Vector3 point = V(spec.point), forward = V(spec.forward).normalized;
                    if (Vector3.Distance(root.InverseTransformPoint(original.position), point) > .002f)
                        throw new InvalidOperationException("Imported habitat anchor disagrees with manifest: " + spec.name);
                    Transform frame = Child(spec.name, anchorRoot);
                    frame.localPosition = point;
                    frame.localRotation = Quaternion.LookRotation(forward, Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > .99f ? Vector3.forward : Vector3.up);
                    anchors.Add(spec.name, frame);
                }
                Transform collisionRoot = Child("Authored habitat collision boxes", root);
                var boxes = new List<BoxCollider>();
                foreach (Box spec in layout.colliders)
                {
                    Transform t = Child(spec.name, collisionRoot); t.localPosition = V(spec.centre);
                    BoxCollider box = t.gameObject.AddComponent<BoxCollider>(); box.size = V(spec.size); box.isTrigger = false;
                    boxes.Add(box);
                }
                // Imported art is already deployed: table and drawer-open boxes must stay enabled with those meshes.
                TextMesh day = Label(root, "Authoritative mission day", new Vector3(-2.08f, 1.30f, 13.0205f),
                    Vector3.forward, .0025f, font);
                TextMesh grow = Label(root, "Authoritative crop state", new Vector3(1.872f, 1.96f, 14.75f),
                    Vector3.left, .003f, font);
                Transform seedlings = Named(kit, "HB_GrowSeedlings");
                HabitatStatusPresenter status = root.gameObject.AddComponent<HabitatStatusPresenter>();
                status.ConfigureScene(seedlings.gameObject, day, grow);
                // Source points sit within the conservative cabinet/drawer boxes. Expose separate root-owned
                // targeting locations 2 cm outside their aisle faces; never move the authored source anchors.
                Transform rationSurface = Child("RationInteractionSurface", anchorRoot);
                rationSurface.localPosition = new Vector3(-1.64f, 1.72f, 14.395f);
                rationSurface.localRotation = anchors["RationPickup"].localRotation;
                Transform waterSurface = Child("WaterInteractionSurface", anchorRoot);
                waterSurface.localPosition = new Vector3(-1.89f, 1.13f, 15.195f);
                waterSurface.localRotation = anchors["WaterDispenser"].localRotation;
                var result = new Result { Root = root, StorageShell = shell, PlacedKit = kit, Status = status,
                    SleepEntry = anchors["SleepEntry"], SleepCamera = anchors["SleepCamera"], PersonalStowage = anchors["PersonalStowage"],
                    RationPickup = anchors["RationPickup"], WaterDispenser = anchors["WaterDispenser"], GrowInteraction = anchors["GrowInteraction"],
                    GrowTray = anchors["GrowTray"], GalleyTable = Named(kit, "HB_GalleyTable"), GrowLid = Named(kit, "HB_GrowLid"),
                    GrowTrayMesh = Named(kit, "HB_GrowTray"), GrowSeedlings = seedlings, Colliders = boxes.ToArray(),
                    MissionDayReadout = day, GrowReadout = grow, RationInteractionSurface = rationSurface, WaterInteractionSurface = waterSurface };
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(node.gameObject,
                        GameObjectUtility.GetStaticEditorFlags(node.gameObject) & ~StaticEditorFlags.BatchingStatic);
                // Commit the reversible scene-instance replacement only after new assets/anchors passed validation.
                foreach (Renderer renderer in oldStorage.GetComponentsInChildren<Renderer>(true))
                { renderer.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); }
                oldLockersL.enabled = false; oldLockersR.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(oldLockersL);
                PrefabUtility.RecordPrefabInstancePropertyModifications(oldLockersR);
                return result;
            }
            catch { Object.DestroyImmediate(root.gameObject); throw; }
        }

        private static void PrepareAssets(Layout layout)
        {
            foreach (MaterialSpec spec in layout.materials)
            {
                Material material = Required<Material>(AssetRoot + "/Materials/" + spec.name + ".mat");
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("The existing URP Lit shader is required.");
                material.shader = shader; material.shaderKeywords = Array.Empty<string>();
                Color tint = Hex(string.IsNullOrEmpty(spec.tint) ? "#FFFFFF" : spec.tint);
                material.SetOverrideTag("RenderType", "Opaque"); material.renderQueue = -1;
                material.SetFloat("_Surface", 0); material.SetFloat("_AlphaClip", 0); material.SetFloat("_ZWrite", 1);
                material.SetFloat("_SrcBlend", (float)BlendMode.One); material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); material.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
                material.SetFloat("_WorkflowMode", 1); material.SetFloat("_SmoothnessTextureChannel", 0);
                material.SetFloat("_Cull", spec.name == "HB_Leaf" ? 0 : 2);
                material.SetFloat("_Metallic", spec.metallic); material.SetFloat("_Smoothness", spec.smoothness);
                material.SetTexture("_BaseMap", null); material.SetTexture("_BumpMap", null);
                material.SetTexture("_MetallicGlossMap", null); material.SetTexture("_OcclusionMap", null);
                material.SetTexture("_EmissionMap", null); material.SetColor("_EmissionColor", Color.black);
                material.SetShaderPassEnabled("ShadowCaster", true);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                float tiling = spec.tiling > 0 ? spec.tiling : 1;
                material.SetTextureScale("_BaseMap", Vector2.one * tiling);
                if (!string.IsNullOrEmpty(spec.set))
                {
                    material.SetTexture("_BaseMap", Texture(spec.set + "_BaseColor"));
                    material.SetTexture("_BumpMap", Texture(spec.set + "_Normal")); material.SetFloat("_BumpScale", spec.normal);
                    Texture2D mask = Texture(spec.set + "_MODS");
                    material.SetTexture("_MetallicGlossMap", mask); material.SetTexture("_OcclusionMap", mask);
                    material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1); material.SetFloat("_OcclusionStrength", 1);
                    material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP");
                }
                if (!string.IsNullOrEmpty(spec.atlas)) material.SetTexture("_BaseMap", Texture(spec.atlas));
                if (!string.IsNullOrEmpty(spec.emission))
                {
                    material.SetColor("_EmissionColor", Hex(spec.emission).linear * spec.emission_strength);
                    material.EnableKeyword("_EMISSION"); material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                if (spec.surface == "cutout")
                {
                    material.SetFloat("_AlphaClip", 1); material.SetFloat("_Cutoff", .5f); material.EnableKeyword("_ALPHATEST_ON");
                    material.SetOverrideTag("RenderType", "TransparentCutout"); material.renderQueue = (int)RenderQueue.AlphaTest;
                    // URP Lit validates opaque cutouts with the shadow pass enabled.
                    // Keep authoring consistent with that validator instead of dirtying on every import.
                }
                if (spec.surface == "transparent")
                {
                    tint.a = spec.alpha; material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
                    // Match installed URP Lit's specular-preserving alpha blend setup.
                    material.SetFloat("_BlendModePreserveSpecular", 1);
                    material.SetFloat("_SrcBlend", (float)BlendMode.One); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha); material.SetFloat("_ZWrite", 0);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.SetOverrideTag("RenderType", "Transparent");
                    material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = (int)RenderQueue.Transparent; material.SetShaderPassEnabled("ShadowCaster", false);
                }
                material.SetColor("_BaseColor", tint); EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            }
        }

        private static void BindMaterials(Transform instance)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string name = materials[i] ? materials[i].name : string.Empty;
                    if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("Missing imported habitat material slot.");
                    materials[i] = Required<Material>((name.StartsWith("HB_", StringComparison.Ordinal)
                        ? AssetRoot + "/Materials/" : ShipMaterials) + name + ".mat");
                }
                renderer.sharedMaterials = materials;
            }
        }
        private static void ValidateGeometry(GameObject asset, int expectedMeshes, ulong expectedTriangles)
        {
            MeshFilter[] meshes = asset.GetComponentsInChildren<MeshFilter>(true);
            if (meshes.Length != expectedMeshes || asset.GetComponentsInChildren<Collider>(true).Length != 0
                || asset.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 || asset.GetComponentsInChildren<Camera>(true).Length != 0
                || asset.GetComponentsInChildren<Light>(true).Length != 0) throw new InvalidOperationException("Habitat import must remain geometry/anchors only.");
            ulong triangles = 0;
            foreach (MeshFilter filter in meshes)
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++) triangles += filter.sharedMesh.GetIndexCount(i) / 3;
            if (triangles != expectedTriangles) throw new InvalidOperationException("Habitat imported triangle count differs from the authored report.");
        }
        private static TextMesh Label(Transform parent, string name, Vector3 point, Vector3 facing, float size, Font font)
        {
            Transform t = Child(name, parent); t.localPosition = point; t.localRotation = Quaternion.LookRotation(-facing, Vector3.up);
            TextMesh text = t.gameObject.AddComponent<TextMesh>(); text.font = font; text.fontSize = 48; text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = new Color(.72f, .9f, .91f);
            t.gameObject.AddComponent<ShipLabelAtlasBinder>().Configure(text, t.GetComponent<MeshRenderer>(), font,
                Required<Material>("Assets/_OutOfReach/Visuals/Ship/Shaders/ShipDepthLabel.mat"));
            return text;
        }
        private static Transform Instantiate(GameObject asset, Transform parent)
        {
            Transform t = ((GameObject)PrefabUtility.InstantiatePrefab(asset, parent)).transform;
            t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one; return t;
        }
        private static Transform Named(Transform root, string name)
        {
            Transform[] found = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
            if (found.Length != 1) throw new InvalidOperationException("Expected exactly one habitat node: " + name);
            return found[0];
        }
        private static Transform Child(string name, Transform parent) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        private static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Required habitat dependency missing: " + path);
        private static Texture2D Texture(string name) => Required<Texture2D>((name.StartsWith("HB_", StringComparison.Ordinal)
            ? AssetRoot + "/Textures/" : ShipTextures) + name + ".png");
        private static Color Hex(string value) => ColorUtility.TryParseHtmlString(value, out Color color) ? color
            : throw new InvalidOperationException("Invalid habitat color: " + value);
        private static Vector3 V(float[] values) => values != null && values.Length == 3
            ? new Vector3(values[0], values[1], values[2]) : throw new InvalidOperationException("Invalid habitat vector.");
    }
}
