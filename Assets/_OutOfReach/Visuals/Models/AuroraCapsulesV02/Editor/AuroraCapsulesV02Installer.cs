using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Visuals.AuroraCapsulesV02.Editor
{
    /// <summary>Swaps the delivered CapsuleShell/Hatch meshes of the launch transport and of the recovery couriers
    /// for the AURORA capsules V02, in the open scene. Visual only: every GameObject, anchor, collider, hinge frame
    /// and script reference stays; no scene is saved. Uninstall restores the TransportLaunch meshes and materials.</summary>
    public static class AuroraCapsulesV02Installer
    {
        public const string Tag = " (AC2)";
        public const string VehicleName = "Transport vehicle - root controls launch";
        public const string ArrivalCourierName = "RecoveryArrival_Courier";
        public const string MarsCourierName = "MarsDeparture_Courier";
        private const string OriginalMaterials = "Assets/_OutOfReach/Visuals/Models/MarinePrologue/Transport/Materials/";

        [MenuItem("OUT OF REACH/Aurora Capsules V02/Install into open scene (transport + rescue craft)", priority = 61)]
        public static void InstallMenu() => Debug.Log("AURORA_CAPSULES_V02_INSTALL\n" + Install(SceneManager.GetActiveScene(), true, true, true));

        [MenuItem("OUT OF REACH/Aurora Capsules V02/Install crew transport only", priority = 62)]
        public static void InstallTransportMenu() => Debug.Log("AURORA_CAPSULES_V02_INSTALL\n" + Install(SceneManager.GetActiveScene(), true, false, true));

        [MenuItem("OUT OF REACH/Aurora Capsules V02/Install rescue craft only", priority = 63)]
        public static void InstallRescueMenu() => Debug.Log("AURORA_CAPSULES_V02_INSTALL\n" + Install(SceneManager.GetActiveScene(), false, true, true));

        [MenuItem("OUT OF REACH/Aurora Capsules V02/Install rescue craft without service module", priority = 64)]
        public static void InstallRescueBareMenu() => Debug.Log("AURORA_CAPSULES_V02_INSTALL\n" + Install(SceneManager.GetActiveScene(), false, true, false));

        [MenuItem("OUT OF REACH/Aurora Capsules V02/Uninstall (restore TransportLaunch meshes)", priority = 80)]
        public static void UninstallMenu() => Debug.Log("AURORA_CAPSULES_V02_UNINSTALL\n" + Uninstall(SceneManager.GetActiveScene()));

        public static string Install(Scene scene, bool transport, bool rescue, bool serviceModule)
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open the Bootstrap scene first.");
            if (!AssetDatabase.LoadAssetAtPath<Material>(AuroraCapsulesV02Builder.Root + "/Materials/CAP_Kit.mat"))
                AuroraCapsulesV02Builder.BuildAll();
            var log = new StringBuilder();
            int done = 0;
            foreach (Target target in Targets(scene))
            {
                if (target.Rescue ? !rescue : !transport) continue;
                Undo.RegisterFullObjectHierarchyUndo(target.Shell, "Install Aurora Capsules V02");
                Undo.RegisterFullObjectHierarchyUndo(target.Hatch, "Install Aurora Capsules V02");
                Apply(target.Shell, target.Hatch, target.Rescue, target.Rescue && serviceModule, true, log);
                log.AppendLine("installed " + (target.Rescue ? "rescue craft" : "crew transport") + " on " + Path(target.Shell.transform));
                if (!target.Rescue) log.AppendLine(DatumCheck(target.Root, target.Shell));
                done++;
            }
            if (done == 0) throw new InvalidOperationException("No '" + VehicleName + "' or recovery courier with CapsuleShell/Hatch found in " + scene.name);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("scene marked dirty, NOT saved. Check it in the viewport, then save yourself.");
            return log.ToString();
        }

        public static string Uninstall(Scene scene)
        {
            var log = new StringBuilder();
            Dictionary<string, Mesh> original = AuroraCapsulesV02Builder.Meshes(AuroraCapsulesV02Builder.OriginalFbx);
            var materials = new[] { "TL_Exterior", "TL_Labels" }.Select(n => AssetDatabase.LoadAssetAtPath<Material>(OriginalMaterials + n + ".mat")).ToArray();
            if (materials.Any(m => !m)) throw new InvalidOperationException("Delivered TL_Exterior/TL_Labels materials not found.");
            foreach (Target target in Targets(scene))
            {
                foreach (GameObject part in new[] { target.Shell, target.Hatch })
                {
                    Undo.RegisterFullObjectHierarchyUndo(part, "Uninstall Aurora Capsules V02");
                    Strip(part, true);
                    part.GetComponent<MeshFilter>().sharedMesh = original[part.name];
                    part.GetComponent<MeshRenderer>().sharedMaterials = materials;
                }
                log.AppendLine("restored " + Path(target.Shell.transform));
            }
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("scene marked dirty, NOT saved.");
            return log.ToString();
        }

        /// <summary>Shared by the scene install and by the reference prefabs.</summary>
        public static void Apply(GameObject shell, GameObject hatch, bool rescue, bool serviceModule, bool undo, StringBuilder log)
        {
            string fbx = rescue ? AuroraCapsulesV02Builder.RescueFbx : AuroraCapsulesV02Builder.TransportFbx;
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx) ?? throw new InvalidOperationException("Missing " + fbx);
            Dictionary<string, Mesh> meshes = AuroraCapsulesV02Builder.Meshes(fbx);
            Material[] Mats(string node)
            {
                Transform t = AuroraCapsulesV02Builder.FindDeep(model.transform, node) ?? throw new InvalidOperationException(fbx + " lacks " + node);
                Material[] m = t.GetComponent<MeshRenderer>().sharedMaterials;
                if (m.Any(x => !x || !x.name.StartsWith(rescue ? "RC_" : "CT_", StringComparison.Ordinal) && !x.name.StartsWith("CAP_", StringComparison.Ordinal)))
                    throw new InvalidOperationException(node + " is not remapped to the V02 materials; run Rebuild Materials and Prefabs.");
                return m;
            }
            void Set(GameObject go, string node)
            {
                go.GetComponent<MeshFilter>().sharedMesh = meshes[node];
                go.GetComponent<MeshRenderer>().sharedMaterials = Mats(node);
            }
            MeshRenderer Extra(GameObject parent, string node)
            {
                var go = new GameObject(node + Tag) { layer = parent.layer };
                if (undo) Undo.RegisterCreatedObjectUndo(go, "Install Aurora Capsules V02");
                go.transform.SetParent(parent.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[node];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = Mats(node);
                var source = parent.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = source.shadowCastingMode;
                renderer.receiveShadows = source.receiveShadows;
                renderer.lightProbeUsage = source.lightProbeUsage;
                renderer.reflectionProbeUsage = source.reflectionProbeUsage;
                renderer.motionVectorGenerationMode = source.motionVectorGenerationMode;
                renderer.renderingLayerMask = source.renderingLayerMask;
                return renderer;
            }

            Strip(shell, undo);
            Strip(hatch, undo);
            Set(shell, "CapsuleShell_LOD0");
            Set(hatch, "Hatch_LOD0");
            var lod0 = new List<Renderer> { shell.GetComponent<MeshRenderer>() };
            var lod1 = new List<Renderer> { Extra(shell, "CapsuleShell_LOD1") };
            var lod2 = new List<Renderer> { Extra(shell, "CapsuleShell_LOD2") };
            if (serviceModule)
            {
                lod0.Add(Extra(shell, "ServiceModule_LOD0"));
                lod1.Add(Extra(shell, "ServiceModule_LOD1"));
                lod2.Add(Extra(shell, "ServiceModule_LOD2"));
            }
            LODGroup group = shell.GetComponent<LODGroup>();
            if (!group) group = undo ? Undo.AddComponent<LODGroup>(shell) : shell.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(new[] { new LOD(.12f, lod0.ToArray()), new LOD(.04f, lod1.ToArray()), new LOD(.002f, lod2.ToArray()) });
            group.RecalculateBounds();

            LODGroup door = hatch.GetComponent<LODGroup>();
            if (!door) door = undo ? Undo.AddComponent<LODGroup>(hatch) : hatch.AddComponent<LODGroup>();
            door.fadeMode = LODFadeMode.None;
            door.SetLODs(new[] { new LOD(.06f, new Renderer[] { hatch.GetComponent<MeshRenderer>() }),
                new LOD(.002f, new Renderer[] { Extra(hatch, "Hatch_LOD1") }) });
            door.RecalculateBounds();
            log?.AppendLine("  " + (rescue ? "RC" : "CT") + " LOD0/1/2 triangles: " + Triangles(lod0) + " / " + Triangles(lod1) + " / " + Triangles(lod2)
                + ", hatch " + Triangles(new[] { hatch.GetComponent<MeshRenderer>() }));
        }

        private static int Triangles(IEnumerable<Renderer> renderers) => renderers.Sum(r =>
        {
            Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
            long indices = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) indices += mesh.GetIndexCount(s);
            return (int)(indices / 3);
        });

        private static void Strip(GameObject part, bool undo)
        {
            foreach (Transform child in part.transform.Cast<Transform>().Where(c => c.name.EndsWith(Tag, StringComparison.Ordinal)).ToArray())
            {
                if (undo) Undo.DestroyObjectImmediate(child.gameObject); else Object.DestroyImmediate(child.gameObject);
            }
            LODGroup group = part.GetComponent<LODGroup>();
            if (group) { if (undo) Undo.DestroyObjectImmediate(group); else Object.DestroyImmediate(group); }
        }

        /// <summary>Same measure LeadDepartureAuthoring.ValidateOrbit applies: the shell must reach 0.15 m past the docking datum.</summary>
        private static string DatumCheck(Transform vehicle, GameObject shell)
        {
            Transform port = vehicle.Cast<Transform>().FirstOrDefault(t => t.name == "DockingPort");
            if (!port) return "  docking datum: no DockingPort anchor under the vehicle, not checked";
            Bounds bounds = shell.GetComponent<MeshRenderer>().bounds;
            Vector3 axis = port.forward, abs = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
            float reach = Vector3.Dot(bounds.center - port.position, axis) + Vector3.Dot(bounds.extents, abs);
            return "  docking datum: shell reaches " + reach.ToString("F3") + " m past DockingPort (required 0.15 +- 0.04): "
                + (Mathf.Abs(reach - .15f) <= .04f ? "PASS" : "FAIL");
        }

        private struct Target
        {
            public Transform Root;
            public GameObject Shell, Hatch;
            public bool Rescue;
        }

        private static IEnumerable<Target> Targets(Scene scene)
        {
            // Resolve every target root before anything is changed: a re-install destroys the earlier '(AC2)' children,
            // and a lazily evaluated query would then touch destroyed transforms.
            Transform[] roots = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == VehicleName || t.name == ArrivalCourierName || t.name == MarsCourierName).ToArray();
            foreach (Transform root in roots)
            {
                if (!root) continue;
                Transform[] parts = root.GetComponentsInChildren<Transform>(true);
                Transform shell = parts.FirstOrDefault(t => t.name == "CapsuleShell" && t.GetComponent<MeshFilter>() && t.GetComponent<MeshRenderer>());
                Transform hatch = parts.FirstOrDefault(t => t.name == "Hatch" && t.GetComponent<MeshFilter>() && t.GetComponent<MeshRenderer>());
                if (!shell || !hatch) continue;
                yield return new Target { Root = root, Shell = shell.gameObject, Hatch = hatch.gameObject, Rescue = root.name != VehicleName };
            }
        }

        private static string Path(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
