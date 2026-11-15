using System;
using System.Collections.Generic;
using System.Linq;
using OutOfReach.Presentation.MarineOcean;
using OutOfReach.Presentation.MarineOcean.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Scene-instance composition only. Root owns deadline, cargo, boarding, controls and all mechanical transitions.</summary>
    public static class I06MarineAuthoring
    {
        public const string AssetRoot = "Assets/_OutOfReach/Visuals/Models/MarinePrologue";
        public const float SeaLevelLocalY = -34.6f;
        public static readonly Vector3 DockLocalPosition = new Vector3(0, -.021f, -9.90f);
        public static readonly string[] EquipmentIds = { "oxygen-tank", "water-tank", "food-locker", "battery-module", "solar-wing", "spare-parts-kit", "spectrometer" };
        private static readonly string[] BayNames = { "oxygen", "water", "food", "battery", "solar", "spares", "science" };

        public sealed class Result
        {
            public Transform Root, DockRoot, VehicleRoot, SiteRoot, Spawn, SeaLevel;
            public Transform ArrivalGateFrame, BoardingGateFrame, HatchFrame, AccessArmFrame;
            public Transform BoardingThreshold, SeatEntry, SeatedCamera, HatchLatch, EngineEmitter, DockingPort;
            public Transform[] CargoSupplyBases, CargoDropBases, CargoDropInteractionSurfaces, WaterlineContacts;
            public Dictionary<string, Transform> DockAnchors, TransportAnchors;
            public BoxCollider[] DockColliders, TransportColliders;
            public GameObject OptionalLanding;
            public Renderer HatchStatusLamp;
            public MarineOceanPresenter Ocean;
        }
#pragma warning disable 0649
        [Serializable] private sealed class Layout
        { public Anchor[] transportAnchors, dockAnchors; public Box[] transportBoxes, dockBoxes; public MaterialSpec[] materials; }
        [Serializable] private sealed class Anchor { public string name; public float[] point, forward; }
        [Serializable] private sealed class Box { public string name, parent; public float[] center, size; public float yaw_deg; }
        [Serializable] private sealed class MaterialSpec
        {
            public string name, group, baseMap, normalMap, metallicMap, occlusionMap;
            public float[] color, emission;
            public float metallic, smoothness, normalStrength, emissionStrength;
        }
#pragma warning restore 0649

        /// <summary>
        /// World origin must be deliberately isolated from AURORA-7; e.g. (10000, 100, 0), never its origin.
        /// Whole assembly can yaw only. No scene save, camera/light creation, player teleport or gameplay command.
        /// Optional ocean authoring creates its own mesh/material/prefab assets under this new subtree.
        /// </summary>
        public static Result Create(Transform parent, Vector3 worldOrigin, float worldYawDegrees = 0f, bool includeOcean = true)
        {
            if (!Finite(worldOrigin.x) || !Finite(worldOrigin.y) || !Finite(worldOrigin.z) || !Finite(worldYawDegrees)
                || new Vector2(worldOrigin.x, worldOrigin.z).magnitude < 256f)
                throw new ArgumentException("Provide an explicit marine origin at least 256 m horizontally from the original ship origin.");
            if (parent && ((parent.lossyScale - Vector3.one).sqrMagnitude > .000001f || EditorUtility.IsPersistent(parent.gameObject)))
                throw new ArgumentException("Use a metre-scale scene parent, not a prefab asset.");
            Layout layout = JsonUtility.FromJson<Layout>(Required<TextAsset>(AssetRoot + "/MarineLayout.json").text);
            if (layout?.transportBoxes?.Length != 44 || layout.dockBoxes?.Length != 23
                || layout.transportAnchors?.Length != 16 || layout.dockAnchors?.Length != 16)
                throw new InvalidOperationException("Use the pinned 855efbc 44-box/16-anchor transport and 23-box/16-anchor dock.");
            PrepareMaterials(layout.materials);
            GameObject transportAsset = Required<GameObject>(AssetRoot + "/Transport/Models/TransportLaunch.fbx");
            GameObject dockAsset = Required<GameObject>(AssetRoot + "/Dock/Models/MarineDock.fbx");
            ValidateAsset(transportAsset, 11); ValidateAsset(dockAsset, 385);
            if (includeOcean) MarineOceanBuilder.BuildAndValidate();
            Transform root = Child("I06 isolated marine prologue", parent);
            root.SetPositionAndRotation(worldOrigin, Quaternion.Euler(0, worldYawDegrees, 0));
            try
            {
                Transform vehicle = Child("Transport vehicle - root controls launch", root);
                Transform site = Child("Launch site - stationary", root);
                Transform transport = InstantiateUnpacked(transportAsset, root);
                Transform dockRoot = Child("Staging module at authored mate", site); dockRoot.localPosition = DockLocalPosition;
                Transform dock = InstantiateUnpacked(dockAsset, dockRoot);
                BindMaterials(transport); BindMaterials(dock);
                ApplyElevatedDeckVariant(dock);
                var sourceParts = transport.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, StringComparer.Ordinal);
                var transportAnchors = Frames(root, transport, layout.transportAnchors, "Transport anchor frames");
                var dockAnchors = Frames(dockRoot, dock, layout.dockAnchors, "Dock anchor frames");
                if (Vector3.Distance(root.InverseTransformPoint(transportAnchors["StagingModuleRoot"].position), DockLocalPosition) > .002f)
                    throw new InvalidOperationException("Dock placement disagrees with authored StagingModuleRoot.");
                foreach (string name in new[] { "CapsuleShell", "CapsuleInterior", "Hatch", "BoosterStack", "CabinLights", "HatchStatusLamp" })
                    sourceParts[name].SetParent(vehicle, true);
                foreach (string name in new[] { "LaunchBarge", "AccessMastTower", "AccessMastLanding", "AccessArm", "DeckLamps" })
                    sourceParts[name].SetParent(site, true);
                Transform hatch = ControlFrame("Hatch control +Y 0 closed to 160 open", vehicle, root,
                    V(layout.transportAnchors.Single(a => a.name == "HatchPivot").point), sourceParts["Hatch"]);
                Transform arm = ControlFrame("Access arm control +Y 0 connected to -90 retracted", site, root,
                    V(layout.transportAnchors.Single(a => a.name == "AccessArmPivot").point), sourceParts["AccessArm"]);
                Transform arrival = ControlFrame("Arrival gate control", dockRoot, dockRoot, new Vector3(-1.14f, 0, -2.9f), Named(dock, "ArrivalSafetyGateAssembly"));
                Transform boarding = ControlFrame("Boarding gate control", dockRoot, dockRoot, new Vector3(-1.14f, 0, 2.86f), Named(dock, "BoardingSafetyGateAssembly"));
                var owners = new Dictionary<string, Transform>(sourceParts, StringComparer.Ordinal) { ["Hatch"] = hatch, ["AccessArm"] = arm };
                BoxCollider[] transportBoxes = layout.transportBoxes.Select(b => Collider(b, owners[b.parent], root)).ToArray();
                Transform dockCollision = Child("Dock fixed proxy colliders", dockRoot);
                BoxCollider[] dockBoxes = layout.dockBoxes.Select(b => Collider(b,
                    b.parent == "ArrivalSafetyGateAssembly" ? arrival : b.parent == "BoardingSafetyGateAssembly" ? boarding : dockCollision, dockRoot)).ToArray();
                GameObject landing = sourceParts["AccessMastLanding"].gameObject;
                landing.SetActive(false); // Preserve its seven authored boxes inactive with the omitted optional mesh.
                foreach (var pair in transportAnchors)
                    pair.Value.SetParent(pair.Key == "HatchLatch" ? hatch : pair.Key == "AccessArmPivot" || pair.Key == "StagingModuleRoot" ? site : vehicle, true);
                // The continuation is physically installed here. Park the redundant dock transfer gate beside its cabin.
                // Moving colliders share these exact wrappers: there is no stationary duplicate across either doorway.
                hatch.localRotation = Quaternion.Euler(0, 160, 0);
                boarding.localRotation = Quaternion.Euler(0, 90, 0);
                Renderer status = sourceParts["HatchStatusLamp"].GetComponent<Renderer>();
                if (status) status.enabled = false; // Root may light it only after its real hatch-secured condition.
                // The delivered ceiling panel is emissive geometry only. Give that practical a real
                // local lamp so cargo remains readable after the hatch closes, without relighting AURORA.
                Transform cabinLamp = Child("Transport ceiling practical", vehicle);
                cabinLamp.localPosition = new Vector3(0, 2.36f, -.15f);
                cabinLamp.localRotation = Quaternion.Euler(90, 0, 0);
                Light lamp = cabinLamp.gameObject.AddComponent<Light>();
                lamp.type = LightType.Spot;
                lamp.color = new Color(1f, .93f, .84f);
                lamp.intensity = 6f;
                lamp.range = 4.2f;
                lamp.spotAngle = 135f;
                lamp.innerSpotAngle = 95f;
                lamp.shadows = LightShadows.Soft;
                lamp.shadowStrength = .75f;
                var drops = new Transform[7]; var targets = new Transform[7];
                for (int i = 0; i < BayNames.Length; i++)
                {
                    drops[i] = transportAnchors["CargoBay_" + BayNames[i]];
                    Transform target = Child("Cargo interaction surface " + EquipmentIds[i], vehicle);
                    Vector3 original = root.InverseTransformPoint(drops[i].position);
                    target.localPosition = new Vector3(original.x < 0 ? -.70f : .70f, .26f, original.z);
                    target.rotation = drops[i].rotation; targets[i] = target;
                }
                Transform sea = Child("Authored transport sea level", site); sea.localPosition = new Vector3(0, SeaLevelLocalY, -2);
                var contacts = new List<Transform>();
                foreach (float x in new[] { -18f, 18f }) foreach (float z in new[] { -8f, 4f })
                {
                    Transform contact = Child("Barge waterline " + x + " " + z, site);
                    contact.localPosition = new Vector3(x, SeaLevelLocalY, z); contacts.Add(contact);
                }
                MarineOceanPresenter ocean = null;
                if (includeOcean)
                {
                    Transform water = InstantiateUnpacked(Required<GameObject>(MarineOceanBuilder.PrefabPath), site);
                    water.SetPositionAndRotation(sea.position, root.rotation); water.localScale = Vector3.one;
                    ocean = water.GetComponent<MarineOceanPresenter>();
                    ocean.SetContacts(contacts.Select(t => new OceanContactPoint(t, .85f, .55f)).ToArray());
                    // No clock binding here; root binds its actual session and reduced-effects setting.
                }
                return new Result { Root = root, DockRoot = dockRoot, VehicleRoot = vehicle, SiteRoot = site,
                    Spawn = dockAnchors["GroundEntry"], SeaLevel = sea, ArrivalGateFrame = arrival, BoardingGateFrame = boarding,
                    HatchFrame = hatch, AccessArmFrame = arm, BoardingThreshold = transportAnchors["BoardingThreshold"],
                    SeatEntry = transportAnchors["SeatEntry"], SeatedCamera = transportAnchors["SeatedCamera"],
                    HatchLatch = transportAnchors["HatchLatch"], EngineEmitter = transportAnchors["EngineEmitter"], DockingPort = transportAnchors["DockingPort"],
                    CargoSupplyBases = new[] { "A", "B", "C", "D" }.Select(s => dockAnchors["CargoCaseReference_" + s]).ToArray(),
                    CargoDropBases = drops, CargoDropInteractionSurfaces = targets, WaterlineContacts = contacts.ToArray(),
                    DockAnchors = dockAnchors, TransportAnchors = transportAnchors, DockColliders = dockBoxes, TransportColliders = transportBoxes,
                    OptionalLanding = landing, HatchStatusLamp = status, Ocean = ocean };
            }
            catch { Object.DestroyImmediate(root.gameObject); throw; }
        }

        private static void ApplyElevatedDeckVariant(Transform dock)
        {
            // This deck is now supported by the launch mast, 34.6m above the barge
            // waterline. The original floating-dock pontoons and marine fenders do
            // not belong here. Disable only these explicit scene-instance meshes;
            // retain the original FBX, deck pan/girders and all walking colliders.
            Renderer[] seaLevelHardware = dock.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name.StartsWith("Support ", StringComparison.Ordinal)
                    || r.name.StartsWith("Marine fender", StringComparison.Ordinal)
                    || r.name.StartsWith("Fender rib", StringComparison.Ordinal)
                    || r.name.StartsWith("Fender chain restraint", StringComparison.Ordinal)).ToArray();
            if (seaLevelHardware.Length != 72)
                throw new InvalidOperationException("Expected 36 pontoon/support pieces and 36 sea-level fender pieces in the pinned dock asset.");
            foreach (Renderer renderer in seaLevelHardware) renderer.gameObject.SetActive(false);
        }

        private static Dictionary<string, Transform> Frames(Transform frameSpace, Transform imported, Anchor[] specs, string groupName)
        {
            var result = new Dictionary<string, Transform>(StringComparer.Ordinal); Transform group = Child(groupName, frameSpace);
            foreach (Anchor a in specs)
            {
                Vector3 point = V(a.point), forward = V(a.forward).normalized;
                Transform original = Named(imported, a.name);
                if (Vector3.Distance(frameSpace.InverseTransformPoint(original.position), point) > .005f)
                    throw new InvalidOperationException("Imported anchor axes/scale differ: " + a.name);
                Transform t = Child(a.name, group); t.localPosition = point;
                t.localRotation = Quaternion.LookRotation(forward, Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > .99f ? Vector3.forward : Vector3.up);
                result.Add(a.name, t);
            }
            return result;
        }
        private static Transform ControlFrame(string name, Transform parent, Transform canonicalSpace, Vector3 pivot, Transform originalPart)
        {
            Transform t = Child(name, parent); t.SetPositionAndRotation(canonicalSpace.TransformPoint(pivot), canonicalSpace.rotation);
            originalPart.SetParent(t, true); return t;
        }
        private static BoxCollider Collider(Box spec, Transform owner, Transform canonicalSpace)
        {
            if ((owner.lossyScale - Vector3.one).sqrMagnitude > .000001f) throw new InvalidOperationException("Non-metre collider owner: " + owner.name);
            Transform t = Child(spec.name.StartsWith("COL_", StringComparison.Ordinal) ? spec.name : "COL_" + spec.name, owner);
            t.SetPositionAndRotation(canonicalSpace.TransformPoint(V(spec.center)), canonicalSpace.rotation * Quaternion.Euler(0, spec.yaw_deg, 0));
            BoxCollider collider = t.gameObject.AddComponent<BoxCollider>(); collider.size = V(spec.size); collider.isTrigger = false; return collider;
        }
        private static Transform InstantiateUnpacked(GameObject asset, Transform parent)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return instance.transform; // Imported child axes/bind transforms are preserved, not zeroed.
        }
        private static void ValidateAsset(GameObject asset, int meshes)
        {
            if (asset.GetComponentsInChildren<MeshRenderer>(true).Length != meshes
                || asset.GetComponentsInChildren<Collider>(true).Length != 0 || asset.GetComponentsInChildren<Rigidbody>(true).Length != 0
                || asset.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 || asset.GetComponentsInChildren<Camera>(true).Length != 0
                || asset.GetComponentsInChildren<Light>(true).Length != 0)
                throw new InvalidOperationException("Expected original visual FBX meshes/anchors only: " + asset.name);
        }
        private static void BindMaterials(Transform root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string name = mats[i] ? mats[i].name : string.Empty;
                    string group = name.StartsWith("MD_", StringComparison.Ordinal) ? "Dock" : "Transport";
                    mats[i] = Required<Material>(AssetRoot + "/" + group + "/Materials/" + name + ".mat");
                }
                renderer.sharedMaterials = mats;
                if (mats.Any(m => m.name == "MD_Glass")) renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        private static void PrepareMaterials(MaterialSpec[] specs)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Existing URP Lit required.");
            foreach (MaterialSpec s in specs)
            {
                Material m = Required<Material>(AssetRoot + "/" + s.group + "/Materials/" + s.name + ".mat");
                m.shader = shader; m.shaderKeywords = Array.Empty<string>();
                // Source values are Blender linear colours; Unity's colour-property storage uses display/gamma values.
                Color color = new Color(s.color[0], s.color[1], s.color[2]).gamma; color.a = s.color[3];
                m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", s.metallic); m.SetFloat("_Smoothness", s.smoothness);
                m.SetFloat("_WorkflowMode", 1); m.SetFloat("_SmoothnessTextureChannel", 0); m.SetFloat("_Cull", 2);
                m.SetFloat("_Surface", 0); m.SetFloat("_AlphaClip", 0); m.SetFloat("_ZWrite", 1);
                m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
                m.SetOverrideTag("RenderType", "Opaque"); m.renderQueue = -1; m.SetShaderPassEnabled("ShadowCaster", true);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                foreach (string slot in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap" }) m.SetTexture(slot, null);
                m.SetColor("_EmissionColor", Color.black); m.SetTextureScale("_BaseMap", Vector2.one);
                if (!string.IsNullOrEmpty(s.baseMap)) m.SetTexture("_BaseMap", Texture(s.group, s.baseMap));
                if (!string.IsNullOrEmpty(s.normalMap))
                { m.SetTexture("_BumpMap", Texture(s.group, s.normalMap)); m.SetFloat("_BumpScale", s.normalStrength); m.EnableKeyword("_NORMALMAP"); }
                if (!string.IsNullOrEmpty(s.metallicMap))
                { m.SetTexture("_MetallicGlossMap", Texture(s.group, s.metallicMap)); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                if (!string.IsNullOrEmpty(s.occlusionMap))
                { m.SetTexture("_OcclusionMap", Texture(s.group, s.occlusionMap)); m.SetFloat("_OcclusionStrength", 1); m.EnableKeyword("_OCCLUSIONMAP"); }
                if (s.emissionStrength > 0)
                {
                    m.SetColor("_EmissionColor", new Color(s.emission[0], s.emission[1], s.emission[2]) * s.emissionStrength);
                    m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                if (color.a < .999f)
                {
                    m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_ZWrite", 0);
                    // Match URP's Alpha + Preserve Specular canonical blend state so the
                    // importer does not rewrite glass on every subsequent scene load.
                    m.SetFloat("_BlendModePreserveSpecular", 1);
                    m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent; m.SetShaderPassEnabled("ShadowCaster", false);
                }
                EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);
            }
        }
        private static Texture2D Texture(string group, string path) => Required<Texture2D>(AssetRoot + "/" + group + "/" + path);
        private static Transform Child(string name, Transform parent) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        private static Transform Named(Transform root, string name)
        {
            Transform[] found = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
            if (found.Length != 1) throw new InvalidOperationException("Expected exactly one imported node: " + name); return found[0];
        }
        private static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing marine asset: " + path);
        private static Vector3 V(float[] v) => v != null && v.Length == 3 ? new Vector3(v[0], v[1], v[2])
            : throw new InvalidOperationException("Invalid authored marine vector.");
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }
}
