using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Visuals.CargoCasesV02.Editor
{
    /// <summary>
    /// Swaps the preflight cargo visuals in the OPEN scene (normally Bootstrap) for CargoCasesV02. Additive and
    /// reversible: every delivered PreflightCargo_Visual instance, its "Additive cargo side grips" with both hand grip
    /// frames, every InteractableTarget, collider, manifest sign and presenter binding stays exactly where it is. Only
    /// the old renderers are switched off, the V02 model is parented under the same case root (same floor-centre frame,
    /// so the grips land on the modelled handles), and the thin rack sticks are deactivated in favour of the V02 rack.
    /// The scene is marked dirty, never saved: the integrator saves.
    /// </summary>
    public static class CargoCasesV02Installer
    {
        public const string RackName = "CC2 supply rack frame";
        public const string CellPrefix = "CC2 rack cell - ";
        private const string CaseName = "PreflightCargo_Visual";
        // Old rack visuals (I06MarineCargoAuthoring.AddStationStand, I06CargoPlacardAuthoring): kept, only deactivated.
        private static readonly string[] OldRackParts = { "Aligned supply cells V02", "Source station label upright", "Source station label rail" };
        // Rack frame origin in the dock frame: between the two cell columns, on the crate row centre line.
        private static readonly Vector3 RackOrigin = new Vector3(-2.655f, 0f, -1.10f);
        private static readonly float[] CellColumns = { -2.97f, -2.34f };
        private const float CellFloor0 = .195f, CellPitch = .40f, CellZ = -1.10f;

        [MenuItem("OUT OF REACH/Cargo Cases V02/Install into open scene", priority = 51)]
        public static void InstallMenu()
        {
            try { Debug.Log("CARGO_CASES_V02_INSTALL\n" + Install(SceneManager.GetActiveScene())); }
            catch (Exception e) { EditorUtility.DisplayDialog("Cargo Cases V02", e.Message, "OK"); throw; }
        }

        [MenuItem("OUT OF REACH/Cargo Cases V02/Uninstall from open scene (restore delivered cases)", priority = 52)]
        public static void UninstallMenu() => Debug.Log("CARGO_CASES_V02_UNINSTALL\n" + Uninstall(SceneManager.GetActiveScene()));

        // Batch: Unity -batchmode -quit -projectPath <clone> -executeMethod OutOfReach.Visuals.CargoCasesV02.Editor.CargoCasesV02Installer.InstallIntoBootstrapBatch
        public static void InstallIntoBootstrapBatch()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_OutOfReach/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            Debug.Log("CARGO_CASES_V02_INSTALL\n" + Install(scene));
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Bootstrap.");
        }

        public static string Install(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play mode first.");
            if (!CargoCasesV02Builder.PrefabsReady()) CargoCasesV02Builder.BuildAll();
            var report = new StringBuilder();
            SerializedObject presenter = Presenter(scene);
            SerializedProperty slots = presenter.FindProperty("slots");
            var supply = presenter.FindProperty("temporarySupplyRoot").objectReferenceValue as GameObject;
            var carriedRoot = presenter.FindProperty("carriedCaseRoot").objectReferenceValue as GameObject;
            var carriedLabel = presenter.FindProperty("carriedLabel").objectReferenceValue as TextMesh;
            if (!supply || !carriedRoot || slots.arraySize == 0) throw new InvalidOperationException("The cargo presenter has no authored scene bindings.");

            Undo.SetCurrentGroupName("Install Cargo Cases V02");
            int group = Undo.GetCurrentGroup();
            Transform dock = supply.transform.parent ? supply.transform.parent : supply.transform;
            for (int i = 0; i < slots.arraySize; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                string id = slot.FindPropertyRelative("EquipmentId").stringValue;
                CargoCasesV02Builder.CaseDef def = CargoCasesV02Builder.Cases.FirstOrDefault(c => c.EquipmentId == id)
                    ?? throw new InvalidOperationException("No V02 container for equipment '" + id + "'.");
                var supplyCase = slot.FindPropertyRelative("SupplyCase").objectReferenceValue as GameObject;
                var aboardCase = slot.FindPropertyRelative("AboardCase").objectReferenceValue as GameObject;
                if (supplyCase)
                {
                    Swap(supplyCase.transform, new[] { def }, null, report);
                    AddCell(supplyCase.transform, def, dock, report);
                }
                if (aboardCase) Swap(aboardCase.transform, new[] { def }, null, report);
            }
            Transform carriedCase = carriedRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == CaseName)
                ?? throw new InvalidOperationException("No " + CaseName + " under '" + carriedRoot.name + "'.");
            Swap(carriedCase, CargoCasesV02Builder.Cases, carriedLabel, report);
            InstallRack(supply.transform, dock, report);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            report.AppendLine("scene marked dirty, not saved");
            return report.ToString();
        }

        public static string Uninstall(Scene scene)
        {
            var report = new StringBuilder();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (CargoCaseV02Skin skin in root.GetComponentsInChildren<CargoCaseV02Skin>(true).ToList())
                {
                    Transform caseRoot = skin.transform;
                    foreach (Transform child in caseRoot.Cast<Transform>().ToList())
                        if (IsOurs(child)) Undo.DestroyObjectImmediate(child.gameObject);
                    foreach (Renderer r in caseRoot.GetComponentsInChildren<Renderer>(true))
                    {
                        Undo.RecordObject(r, "Uninstall Cargo Cases V02");
                        r.enabled = true;
                    }
                    report.AppendLine("restored " + PathOf(caseRoot));
                    Undo.DestroyObjectImmediate(skin);
                }
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true).ToList())
                {
                    if (!t) continue;
                    if (t.name == RackName) { report.AppendLine("removed " + PathOf(t)); Undo.DestroyObjectImmediate(t.gameObject); }
                    else if (OldRackParts.Contains(t.name) && !t.gameObject.activeSelf)
                    {
                        Undo.RecordObject(t.gameObject, "Uninstall Cargo Cases V02");
                        t.gameObject.SetActive(true);
                        report.AppendLine("reactivated " + PathOf(t));
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return report.ToString();
        }

        private static SerializedObject Presenter(Scene scene)
        {
            // Found by type name and read through its serialized fields: no compile-time coupling to the cargo assembly.
            MonoBehaviour[] found = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(m => m && m.GetType().Name == "CargoVisualPresenter").ToArray();
            if (found.Length != 1) throw new InvalidOperationException("Expected exactly one CargoVisualPresenter in the open scene, found " + found.Length + ".");
            return new SerializedObject(found[0]);
        }

        private static bool IsOurs(Transform t) =>
            t.name.StartsWith(CargoCaseV02Skin.ModelPrefix, StringComparison.Ordinal) || t.name.StartsWith(CellPrefix, StringComparison.Ordinal);

        private static void Swap(Transform caseRoot, IReadOnlyList<CargoCasesV02Builder.CaseDef> defs, TextMesh carriedLabel, StringBuilder report)
        {
            if (caseRoot.GetComponent<CargoCaseV02Skin>()) { report.AppendLine("already installed: " + PathOf(caseRoot)); return; }
            if ((caseRoot.lossyScale - Vector3.one).sqrMagnitude > 1e-6f) throw new InvalidOperationException("Scaled case root: " + PathOf(caseRoot));
            int off = 0;
            foreach (Renderer r in caseRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                Undo.RecordObject(r, "Install Cargo Cases V02");
                r.enabled = false;
                off++;
            }
            var variants = new List<CargoCaseV02Skin.Variant>();
            foreach (CargoCasesV02Builder.CaseDef def in defs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.PrefabPath) ?? throw new InvalidOperationException("Missing prefab " + def.PrefabPath);
                GameObject model = Place(prefab, caseRoot, CargoCaseV02Skin.ModelPrefix + def.EquipmentId);
                // The CC2 FBX import puts the stencilled front on local -Z; the delivered case has its labelled face on
                // +Z. Both grips are symmetric about the yaw axis, so a half turn keeps them on the modelled handles.
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                // The carried case holds all seven; the skin shows the one the presenter reports. Rack/aboard: one, shown.
                if (defs.Count > 1) model.SetActive(false);
                variants.Add(new CargoCaseV02Skin.Variant { EquipmentId = def.EquipmentId, DisplayName = def.DisplayName, ShortName = def.ShortName, Model = model });
            }
            var skin = Undo.AddComponent<CargoCaseV02Skin>(caseRoot.gameObject);
            skin.Configure(variants.ToArray(), defs.Count > 1 ? carriedLabel : null);
            EditorUtility.SetDirty(skin);
            report.AppendLine((defs.Count > 1 ? "carried x" + defs.Count : defs[0].Key) + " -> " + PathOf(caseRoot) + " (" + off + " old renderers off)");
        }

        private static void AddCell(Transform supplyCase, CargoCasesV02Builder.CaseDef def, Transform dock, StringBuilder report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.CellPrefabPath);
            if (!prefab || supplyCase.Find(CellPrefix + def.EquipmentId)) return;
            // The cell (cradle, tie-down straps, sign stand) is a child of its crate, so it leaves with the crate (R02).
            Vector3 local = dock.InverseTransformPoint(supplyCase.position);
            bool column = CellColumns.Any(x => Mathf.Abs(local.x - x) < .01f);
            float row = (local.y - CellFloor0) / CellPitch;
            if (!column || Mathf.Abs(row - Mathf.Round(row)) > .03f || Mathf.Abs(local.z - CellZ) > .01f)
                report.AppendLine("WARNING: " + def.Key + " supply case is not on an authored rack cell: " + local.ToString("F3"));
            // Same half turn as the case model: the cell is authored in its container's own frame.
            Place(prefab, supplyCase, CellPrefix + def.EquipmentId).transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            report.AppendLine("rack cell " + def.Key + " -> " + PathOf(supplyCase));
        }

        private static void InstallRack(Transform supply, Transform dock, StringBuilder report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CargoCasesV02Builder.RackPrefabPath);
            if (!prefab) { report.AppendLine("rack prefab not built: old rack kept"); return; }
            if (!supply.Find(RackName))
            {
                GameObject rack = Place(prefab, supply, RackName);
                // The frame is authored in dock axes (its front, local -Z, is the aisle side at dock -Z): no yaw.
                rack.transform.SetPositionAndRotation(dock.TransformPoint(RackOrigin), dock.rotation);
                report.AppendLine("rack frame -> " + PathOf(rack.transform));
            }
            foreach (Transform child in supply)
            {
                if (!OldRackParts.Contains(child.name) || !child.gameObject.activeSelf) continue;
                Undo.RecordObject(child.gameObject, "Install Cargo Cases V02");
                child.gameObject.SetActive(false);
                report.AppendLine("deactivated " + child.name);
            }
        }

        private static GameObject Place(GameObject prefab, Transform parent, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Install Cargo Cases V02");
            instance.name = name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            // Same layer as the case it replaces (the carried view is on Ignore Raycast): visuals never take rays.
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = parent.gameObject.layer;
            return instance;
        }

        private static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (Transform c = t; c; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts.Skip(Math.Max(0, parts.Count - 3)));
        }
    }
}
