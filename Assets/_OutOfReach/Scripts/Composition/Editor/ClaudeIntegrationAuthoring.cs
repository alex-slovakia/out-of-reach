using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OutOfReach.Presentation.Hands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>CLAUDE-INTEGRATION scene steps on claude/integration. Each step is idempotent, validated and logged.
    /// No existing collider, camera, anchor or gameplay binding is moved; every change is additive.</summary>
    public static class ClaudeIntegrationAuthoring
    {
        public const string ScenePath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
        public const string WorkGripPosePath = "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/WorkGripV01/WORK_GripRail_V01.asset";
        public const string TowerModelPath = "Assets/_OutOfReach/Visuals/Models/LaunchTowerV02/Models/LaunchTowerV02.fbx";
        public const string TowerInstanceName = "Launch tower V02 detail - b330226";
        public const string ProxyName = "Collision proxy (claude/integration)";
        private const string SiteName = "Launch site - stationary";
        private const string ArmControlName = "Access arm control +Y 0 connected to -90 retracted";
        private const string ArmDetailName = "LTV2_ArmDetail";
        private static readonly string[] TowerStatic = { "LTV2_TowerFacade", "LTV2_DeckSupport", "LTV2_Deluge" };
        private const int TowerTriangles = 54942;
        // Visual-only props that protrude from the cockpit and engineering walls (lead's native inventory 9bc7bbf).
        private static readonly string[] ProxyTargets =
        {
            "DP02_Cockpit/DP02_CKP_FrameLoom_L_Z000", "DP02_Cockpit/DP02_CKP_FrameLoom_R_Z150",
            "DP02_Engineering/DP02_ENG_FrameLoom_L_Z460", "LifeSupportRack_Ars1/Body",
            "DP02_Cockpit/DP02_CKP_AccessPanel_AftL", "DP02_Cockpit/DP02_CKP_AccessPanel_AftR",
        };

        // Unity -batchmode -quit -projectPath <clone> -executeMethod OutOfReach.Composition.Editor.ClaudeIntegrationAuthoring.ApplySet2
        public static void ApplySet2()
        {
            if (Application.unityVersion != "6000.3.28f1" || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run on Unity 6000.3.28f1 outside Play mode.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            var report = new StringBuilder();
            bool changed = false;
            changed |= ApplyWorkGripPose(roots, report);
            changed |= ApplyTowerV02(roots, report);
            changed |= ApplyCollisionProxies(roots, report);
            Validate(roots, report);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Bootstrap.");
            }
            Debug.Log("CLAUDE_INTEGRATION_SET2 " + (changed ? "SAVED" : "VERIFIED") + "\n" + report);
        }

        private static bool ApplyWorkGripPose(GameObject[] roots, StringBuilder report)
        {
            var pose = AssetDatabase.LoadAssetAtPath<BareRailGripPose>(WorkGripPosePath);
            if (!pose || !pose.IsValid) throw new InvalidOperationException("WORK grip pose asset missing or invalid: " + WorkGripPosePath);
            var bridge = ExactlyOne<I06MicrogravityBridge>(roots);
            var serialized = new SerializedObject(bridge);
            SerializedProperty property = serialized.FindProperty("workGripPose")
                ?? throw new InvalidOperationException("I06MicrogravityBridge.workGripPose is not serialized.");
            bool changed = property.objectReferenceValue != pose;
            if (changed) { property.objectReferenceValue = pose; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            report.AppendLine("workGripPose=" + AssetDatabase.GetAssetPath(property.objectReferenceValue) + (changed ? " (assigned)" : " (already)"));
            return changed;
        }

        private static bool ApplyTowerV02(GameObject[] roots, StringBuilder report)
        {
            var marine = ExactlyOne<I06MarineBindings>(roots);
            Transform prologue = marine.MarineRoot ?? throw new InvalidOperationException("Marine prologue root is not bound.");
            Transform site = Child(prologue, SiteName);
            Transform armControl = Descendants(site, ArmControlName).Single();
            Transform existing = Descendants(site, TowerInstanceName).SingleOrDefault();
            if (existing)
            {
                report.AppendLine("towerV02=already under " + Path(existing.parent));
                return false;
            }
            if (Quaternion.Angle(armControl.localRotation, Quaternion.identity) > .25f)
                throw new InvalidOperationException("The access arm must be saved in its connected pose before parenting the arm detail.");
            var asset = Required<GameObject>(TowerModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, prologue);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = TowerInstanceName;
            // Same frame as TransportLaunch.fbx: identity under the prologue root, then kept with the stationary site.
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.transform.SetParent(site, true);
            Transform armDetail = Descendants(instance.transform, ArmDetailName).Single();
            armDetail.SetParent(armControl, true);
            foreach (string name in TowerStatic) Descendants(instance.transform, name).Single();
            report.AppendLine("towerV02=instantiated under " + Path(site) + "; " + ArmDetailName + " under " + Path(armControl));
            return true;
        }

        private static bool ApplyCollisionProxies(GameObject[] roots, StringBuilder report)
        {
            bool changed = false;
            Transform[] all = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach (string target in ProxyTargets)
            {
                Transform[] matches = all.Where(t => Path(t).EndsWith("/" + target, StringComparison.Ordinal)).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("Expected one proxy target " + target + ", found " + matches.Length);
                Transform prop = matches[0];
                MeshFilter filter = prop.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) throw new InvalidOperationException("Proxy target has no mesh: " + target);
                if (prop.GetComponentsInChildren<Collider>(true).Any(c => c.name != ProxyName))
                    throw new InvalidOperationException("Target already has its own collider; no proxy needed: " + target);
                Transform proxy = prop.Find(ProxyName);
                Bounds local = filter.sharedMesh.bounds;
                if (!proxy)
                {
                    proxy = new GameObject(ProxyName).transform;
                    proxy.gameObject.layer = prop.gameObject.layer;
                    proxy.SetParent(prop, false);
                    proxy.localPosition = Vector3.zero; proxy.localRotation = Quaternion.identity; proxy.localScale = Vector3.one;
                    var box = proxy.gameObject.AddComponent<BoxCollider>();
                    box.center = local.center;
                    // Thin flush panels still get a 3 cm solid so a fast body cannot tunnel the face.
                    box.size = Vector3.Max(local.size, Vector3.one * .03f);
                    box.isTrigger = false;
                    changed = true;
                }
                var collider = proxy.GetComponent<BoxCollider>();
                Bounds world = collider.bounds;
                report.AppendLine("proxy " + target + " layer=" + LayerMask.LayerToName(proxy.gameObject.layer)
                    + " worldCenter=" + world.center.ToString("F3") + " worldSize=" + world.size.ToString("F3"));
            }
            return changed;
        }

        private static void Validate(GameObject[] roots, StringBuilder report)
        {
            var marine = ExactlyOne<I06MarineBindings>(roots);
            Transform site = Child(marine.MarineRoot, SiteName);
            Transform tower = Descendants(site, TowerInstanceName).Single();
            Transform armControl = Descendants(site, ArmControlName).Single();
            var renderers = tower.GetComponentsInChildren<MeshRenderer>(true).ToList();
            renderers.AddRange(Descendants(armControl, ArmDetailName).Single().GetComponentsInChildren<MeshRenderer>(true));
            int triangles = renderers.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);
            if (renderers.Count != 4 || triangles != TowerTriangles)
                throw new InvalidOperationException("Tower V02 renderers/triangles changed: " + renderers.Count + " / " + triangles);
            if (renderers.Any(r => r.sharedMaterials.Length == 0 || r.sharedMaterials.Any(m => !m || !m.name.StartsWith("TW_", StringComparison.Ordinal))))
                throw new InvalidOperationException("Tower V02 has a missing or non-TW material.");
            if (tower.GetComponentsInChildren<Collider>(true).Any()) throw new InvalidOperationException("Tower V02 must stay collider-free.");
            Vector3 relative = marine.MarineRoot.InverseTransformPoint(tower.position);
            if (relative.sqrMagnitude > 1e-8f || Quaternion.Angle(Quaternion.Inverse(marine.MarineRoot.rotation) * tower.rotation, Quaternion.identity) > .01f)
                throw new InvalidOperationException("Tower V02 must keep the prologue identity frame, found " + relative.ToString("F4"));
            var bridge = ExactlyOne<I06MicrogravityBridge>(roots);
            if (new SerializedObject(bridge).FindProperty("workGripPose").objectReferenceValue == null)
                throw new InvalidOperationException("WORK grip pose is not assigned.");
            report.AppendLine("validated: tower renderers=4 triangles=" + triangles + ", collider-free, identity prologue frame; grip pose assigned; " + ProxyTargets.Length + " proxies");
        }

        private static IEnumerable<Transform> Descendants(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name);
        private static Transform Child(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++) if (root.GetChild(i).name == name) return root.GetChild(i);
            throw new InvalidOperationException("Missing child " + name + " under " + root.name);
        }
        private static T ExactlyOne<T>(GameObject[] roots) where T : Component
        {
            T[] found = roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            if (found.Length != 1) throw new InvalidOperationException("Expected exactly one " + typeof(T).Name + ", found " + found.Length);
            return found[0];
        }
        private static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing required asset: " + path);
        private static string Path(Transform t) => t ? (t.parent ? Path(t.parent) + "/" : "") + t.name : "<none>";
    }
}
