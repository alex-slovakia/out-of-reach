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

        // Unity -batchmode -quit -projectPath <clone> -executeMethod OutOfReach.Composition.Editor.ClaudeIntegrationAuthoring.ApplySet3
        public static void ApplySet3()
        {
            if (Application.unityVersion != "6000.3.28f1" || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run on Unity 6000.3.28f1 outside Play mode.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            var report = new StringBuilder();
            bool changed = ApplyLaunchFxV03(roots, report);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Bootstrap.");
            }
            Debug.Log("CLAUDE_INTEGRATION_SET3 " + (changed ? "SAVED" : "VERIFIED") + "\n" + report);
        }

        // Unity -batchmode -quit -projectPath <clone> -executeMethod OutOfReach.Composition.Editor.ClaudeIntegrationAuthoring.ApplySet4
        public static void ApplySet4()
        {
            if (Application.unityVersion != "6000.3.28f1" || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run on Unity 6000.3.28f1 outside Play mode.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            var report = new StringBuilder();
            bool changed = ApplyRestBerth(roots, report);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Bootstrap.");
            }
            Debug.Log("CLAUDE_INTEGRATION_SET4 " + (changed ? "SAVED" : "VERIFIED") + "\n" + report);
        }

        // BODY-REST-HOOK berth: SleepBodyFrame beside the habitat's SleepCamera (the body writer's own authoring math),
        // plus the composition's two references for the rest hook and the sleep camera lease. No camera, anchor or
        // collider is moved.
        private static bool ApplyRestBerth(GameObject[] roots, StringBuilder report)
        {
            var composition = ExactlyOne<I01MissionComposition>(roots);
            // The habitat FBX also imports an empty named SleepCamera; use root's authored anchor frame (HabitatLayout.json).
            Transform[] all = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "SleepCamera").ToArray();
            Transform[] cameras = all.Where(t => t.parent && t.parent.name == "Interaction anchor frames - root binds commands").ToArray();
            if (cameras.Length != 1) throw new InvalidOperationException("Expected one authored habitat SleepCamera anchor, found " + cameras.Length
                + " (" + string.Join("; ", all.Select(Path)) + ")");
            Transform sleepCamera = cameras[0];
            foreach (Transform other in all.Where(t => t != sleepCamera)) report.AppendLine("ignored imported " + Path(other));
            var serialized = new SerializedObject(composition);
            SerializedProperty bodyProperty = serialized.FindProperty("sleepBodyFrame") ?? throw new InvalidOperationException("No sleepBodyFrame field.");
            SerializedProperty cameraProperty = serialized.FindProperty("sleepCameraAnchor") ?? throw new InvalidOperationException("No sleepCameraAnchor field.");
            Transform existing = sleepCamera.parent ? sleepCamera.parent.Find(OutOfReach.Presentation.Motion.Editor.ModelGaitBerthAuthoring.FrameName) : null;
            Vector3 before = existing ? existing.localPosition : Vector3.positiveInfinity;
            Quaternion beforeRotation = existing ? existing.localRotation : Quaternion.identity;
            Transform frame = OutOfReach.Presentation.Motion.Editor.ModelGaitBerthAuthoring.CreateOrUpdate(sleepCamera);
            bool frameChanged = !existing || (frame.localPosition - before).sqrMagnitude > 1e-10f || Quaternion.Angle(frame.localRotation, beforeRotation) > .001f;
            bool referencesChanged = bodyProperty.objectReferenceValue != frame || cameraProperty.objectReferenceValue != sleepCamera;
            if (referencesChanged)
            {
                bodyProperty.objectReferenceValue = frame;
                cameraProperty.objectReferenceValue = sleepCamera;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Vector3 habitatLocal = sleepCamera.parent ? sleepCamera.parent.InverseTransformPoint(frame.position) : frame.position;
            float yaw = (Quaternion.Inverse(sleepCamera.parent ? sleepCamera.parent.rotation : Quaternion.identity) * frame.rotation).eulerAngles.y;
            // Body writer's measured berth convention: (-2.2326, 0.3086, 13.03), yaw 90 in habitat space.
            if (Vector3.Distance(habitatLocal, new Vector3(-2.2326f, .3086f, 13.03f)) > .03f || Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) > 1f)
                throw new InvalidOperationException("SleepBodyFrame differs from the measured berth convention: " + habitatLocal.ToString("F4") + " yaw " + yaw.ToString("F2"));
            report.AppendLine("sleepBodyFrame=" + Path(frame) + " habitat " + habitatLocal.ToString("F4") + " yaw " + yaw.ToString("F2")
                + (frameChanged ? " (created/updated)" : " (already)") + "; sleepCameraAnchor=" + Path(sleepCamera)
                + (referencesChanged ? " (assigned)" : " (already)"));
            return frameChanged || referencesChanged;
        }

        // Unity -batchmode -quit -projectPath <clone> -executeMethod OutOfReach.Composition.Editor.ClaudeIntegrationAuthoring.ApplySet6
        // Alex P1 (#7 6896392077): the BUFFER DIAGNOSTIC (FalseSignal) is a brief intentional shot, not an 11.5 s lock.
        // Core's cinematic handshake blocks player control only while the presenter plays; the diagnostic voice line
        // and its subtitle keep playing as ordinary dialogue after control returns.
        public const float FalseSignalShotSeconds = 1.5f;
        public static void ApplySet6()
        {
            if (Application.unityVersion != "6000.3.28f1" || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run on Unity 6000.3.28f1 outside Play mode.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            var presenter = ExactlyOne<OutOfReach.Presentation.Cinematics.MissionCinematicPresenter>(roots);
            var serialized = new SerializedObject(presenter);
            SerializedProperty seconds = serialized.FindProperty("falseSignalSeconds") ?? throw new InvalidOperationException("No falseSignalSeconds field.");
            float before = seconds.floatValue;
            bool changed = Mathf.Abs(before - FalseSignalShotSeconds) > .0001f;
            if (changed)
            {
                seconds.floatValue = FalseSignalShotSeconds;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Bootstrap.");
            }
            Debug.Log("CLAUDE_INTEGRATION_SET6 " + (changed ? "SAVED" : "VERIFIED") + " falseSignalSeconds " + before.ToString("F2") + " -> " + FalseSignalShotSeconds.ToString("F2"));
        }

        // Unity -batchmode -quit -projectPath <clone> -executeMethod OutOfReach.Composition.Editor.ClaudeIntegrationAuthoring.ApplySet7
        // Alex P1 item 2 (visible rails vs registered 30 mm capsules), from the native per-rail probe frames on aec862b:
        // 22 of 23 registered capsules sit inside a visible bar. "Existing ECLSS vertical bar" has its stand-offs but no
        // visible shaft, so it gets the same amber shaft root's rails use (no collider moved). The Earth terminal's
        // visible "Left handhold" had no capsule; it gets one fitted to its mesh and joins the registered rails.
        public const string AmberPath = "Assets/_OutOfReach/Visuals/Models/Aurora7/Materials/A7_PaintAmber.mat";
        private const string EclssBarName = "Existing ECLSS vertical bar";
        private const string VisibleShaftName = "Visible shaft (claude/integration)";
        private const string TerminalHandholdPath = "MissionTerminal/Hardware/Left handhold";
        private const string TerminalCapsuleName = "Handhold capsule (claude/integration)";

        public static void ApplySet7()
        {
            if (Application.unityVersion != "6000.3.28f1" || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run on Unity 6000.3.28f1 outside Play mode.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            var report = new StringBuilder();
            bool changed = ApplyEclssShaft(roots, report);
            changed |= ApplyTerminalHandhold(roots, report);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Bootstrap.");
            }
            Debug.Log("CLAUDE_INTEGRATION_SET7 " + (changed ? "SAVED" : "VERIFIED") + "\n" + report);
        }

        private static bool ApplyEclssShaft(GameObject[] roots, StringBuilder report)
        {
            var handholds = ExactlyOne<I06InteriorHandholds>(roots);
            Transform bar = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Single(t => t.name == EclssBarName);
            var capsule = bar.GetComponent<CapsuleCollider>() ?? throw new InvalidOperationException("ECLSS bar has no capsule.");
            if (!handholds.Rails.Contains(capsule)) throw new InvalidOperationException("ECLSS bar capsule is not a registered rail.");
            if (bar.Find(VisibleShaftName)) { report.AppendLine("eclssShaft=already"); return false; }
            var amber = Required<Material>(AmberPath);
            float length = capsule.height - capsule.radius * 2f;
            Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(shaft.GetComponent<Collider>());
            shaft.name = VisibleShaftName;
            shaft.transform.SetParent(bar, false);
            shaft.transform.localPosition = capsule.center;
            shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
            shaft.transform.localScale = new Vector3(capsule.radius * 2f, length * .5f, capsule.radius * 2f);
            var renderer = shaft.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = amber; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            report.AppendLine("eclssShaft=added amber shaft r=" + capsule.radius.ToString("F3") + " length=" + length.ToString("F3") + " at " + bar.TransformPoint(capsule.center).ToString("F3"));
            return true;
        }

        private static bool ApplyTerminalHandhold(GameObject[] roots, StringBuilder report)
        {
            var handholds = ExactlyOne<I06InteriorHandholds>(roots);
            Transform[] all = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform[] matches = all.Where(t => Path(t).EndsWith("/" + TerminalHandholdPath, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Expected one " + TerminalHandholdPath + ", found " + matches.Length);
            Transform handle = matches[0];
            var filter = handle.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) throw new InvalidOperationException("Terminal handhold has no mesh.");
            Transform probe = handle.Find(TerminalCapsuleName);
            CapsuleCollider capsule = probe ? probe.GetComponent<CapsuleCollider>() : null;
            bool changed = false;
            if (!capsule)
            {
                Bounds local = filter.sharedMesh.bounds;
                if ((handle.lossyScale - Vector3.one).sqrMagnitude > 1e-4f) throw new InvalidOperationException("Terminal handhold is not metre scale: " + handle.lossyScale);
                Vector3 size = local.size;
                int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
                var go = new GameObject(TerminalCapsuleName);
                go.layer = handle.gameObject.layer;
                go.transform.SetParent(handle, false);
                capsule = go.AddComponent<CapsuleCollider>();
                capsule.center = local.center; capsule.direction = axis; capsule.isTrigger = false;
                capsule.radius = .015f;
                capsule.height = (axis == 0 ? local.size.x : axis == 1 ? local.size.y : local.size.z);
                changed = true;
            }
            if (!handholds.Rails.Contains(capsule))
            {
                var serialized = new SerializedObject(handholds);
                SerializedProperty rails = serialized.FindProperty("rails");
                rails.arraySize++;
                rails.GetArrayElementAtIndex(rails.arraySize - 1).objectReferenceValue = capsule;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            report.AppendLine("terminalHandhold=" + Path(handle) + " capsule world " + capsule.bounds.center.ToString("F3") + " size " + capsule.bounds.size.ToString("F3")
                + " registered rails " + handholds.Rails.Count + (changed ? " (added)" : " (already)"));
            return changed;
        }

        public const string LaunchFxPrefabPath = "Assets/_OutOfReach/Visuals/LaunchFX_V03/LaunchFX_V03.prefab";
        private const string TrenchExitName = "LS_TrenchExit_Ground";

        // Same binding as the LaunchFX writer's builder commit 0331c91, applied to the saved scene: the prefab under
        // the presentation root, bound to the old FX's own deflector and pad planes plus the land kit's trench outlet.
        private static bool ApplyLaunchFxV03(GameObject[] roots, StringBuilder report)
        {
            var marine = ExactlyOne<I06MarineBindings>(roots);
            Transform presentationRoot = ExactlyOne<OutOfReach.Presentation.Cinematics.TransportDeparturePresenter>(roots).transform;
            var serialized = new SerializedObject(marine);
            var nozzle = (Transform)serialized.FindProperty("nozzleFrame").objectReferenceValue;
            var deflector = (Transform)serialized.FindProperty("deckPlane").objectReferenceValue;
            var pad = (Transform)serialized.FindProperty("dustPlane").objectReferenceValue;
            Transform site = Child(marine.MarineRoot, SiteName);
            Transform exit = Descendants(site, TrenchExitName).Single();
            if (!nozzle || nozzle.name != "EngineEmitter" || !deflector || deflector.name != "Actual deflector contact plane"
                || !pad || pad.name != "Land pad top plane")
                throw new InvalidOperationException("Saved FX anchors differ: " + Path(nozzle) + " | " + Path(deflector) + " | " + Path(pad));
            float padY = marine.MarineRoot.InverseTransformPoint(pad.position).y;
            if (Mathf.Abs(padY + 32.6f) > .01f) throw new InvalidOperationException("Pad plane is not at prologue -32.6 m: " + padY);
            var current = serialized.FindProperty("launchFx").objectReferenceValue as OutOfReach.Presentation.LaunchFXV03.LaunchFxV03Presenter;
            bool bound = current && serialized.FindProperty("launchDeflector").objectReferenceValue == deflector
                && serialized.FindProperty("launchPad").objectReferenceValue == pad
                && serialized.FindProperty("launchTrenchExit").objectReferenceValue == exit;
            if (bound)
            {
                report.AppendLine("launchFx=already " + Path(current.transform));
                return false;
            }
            if (current) throw new InvalidOperationException("A different LaunchFX binding exists: " + Path(current.transform));
            var prefab = Required<GameObject>(LaunchFxPrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, presentationRoot);
            var presenter = instance.GetComponent<OutOfReach.Presentation.LaunchFXV03.LaunchFxV03Presenter>()
                ?? throw new InvalidOperationException("LaunchFX_V03.prefab has no presenter.");
            marine.ConfigureLaunchFxV03(presenter, deflector, pad, exit);
            EditorUtility.SetDirty(marine);
            report.AppendLine("launchFx=" + Path(instance.transform) + " nozzle=" + Path(nozzle) + " deflector=" + Path(deflector)
                + " pad=" + Path(pad) + " (y " + padY.ToString("F2") + ") exit=" + Path(exit));
            return true;
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
