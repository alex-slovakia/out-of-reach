using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Visuals.InteriorHeroV01.Editor
{
    /// <summary>Swaps the AURORA-7 interior hero props in the OPEN scene (normally Bootstrap) for INTERIOR-HERO-V01.
    /// Reversible and visual only: old separate props only get their renderers disabled, old props that are merged into
    /// a compartment mesh are cut out of a derived copy of that mesh (the source FBX is never changed), and the new
    /// prefab is added under the delivered model root at the ship origin. No InteractableTarget, collider, anchor,
    /// handhold or gameplay transform is touched. The scene is marked dirty, never saved: the integrator saves.</summary>
    public static class InteriorHeroV01Installer
    {
        public const string ShipModelRootName = "AURORA-7 delivered Blender models";
        public const string InstanceName = "INTERIOR-HERO-V01 props";
        private const string CarvedFolder = InteriorHeroV01Builder.Root + "/Carved";
        private const string CarvedSuffix = "_HeroCarved";
        private const string OriginalMeshFolder = "Assets/_OutOfReach/Visuals/Models/Aurora7/Meshes/";

        [MenuItem("OUT OF REACH/Interior Hero V01/Install into open scene (no save)", priority = 61)]
        public static void InstallMenu()
        {
            try { Debug.Log("INTERIOR_HERO_V01_INSTALL\n" + Install(SceneManager.GetActiveScene())); }
            catch (Exception e) { EditorUtility.DisplayDialog("Interior Hero V01", e.Message, "OK"); throw; }
        }

        [MenuItem("OUT OF REACH/Interior Hero V01/Uninstall from open scene (restore V01 props)", priority = 62)]
        public static void UninstallMenu() => Debug.Log("INTERIOR_HERO_V01_UNINSTALL\n" + Uninstall(SceneManager.GetActiveScene()));

        // Batch (saves): Unity -batchmode -quit -projectPath <clone> -executeMethod OutOfReach.Visuals.InteriorHeroV01.Editor.InteriorHeroV01Installer.InstallIntoBootstrapBatch
        public static void InstallIntoBootstrapBatch()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_OutOfReach/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            Debug.Log("INTERIOR_HERO_V01_INSTALL\n" + Install(scene));
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Bootstrap.");
        }

        public static string Install(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play mode first.");
            Transform ship = ShipRoot(scene);
            if (ship.Find(InstanceName)) return "already installed under " + ship.name + " (nothing changed)";
            if ((ship.lossyScale - Vector3.one).sqrMagnitude > 1e-6f || ship.position.sqrMagnitude > 1e-6f
                || Quaternion.Angle(ship.rotation, Quaternion.identity) > .01f)
                throw new InvalidOperationException("The delivered model root must sit at the ship origin with identity rotation and unit scale.");

            var report = new StringBuilder();
            report.AppendLine(InteriorHeroV01Builder.BuildAll().TrimEnd());
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteriorHeroV01Builder.PrefabPath)
                ?? throw new InvalidOperationException("Prefab build failed: " + InteriorHeroV01Builder.PrefabPath);
            InteriorHeroV01Builder.Report spec = InteriorHeroV01Builder.LoadReport();

            // Resolve every scene object first, so a missing name fails before anything changes.
            var hide = new List<Renderer>();
            var carve = new Dictionary<MeshFilter, List<Bounds>>();
            foreach (InteriorHeroV01Builder.PropSpec prop in spec.props)
            {
                foreach (string name in prop.hide ?? Array.Empty<string>())
                {
                    Transform target = Named(ship, name);
                    Renderer renderer = target.GetComponent<Renderer>()
                        ?? throw new InvalidOperationException("'" + name + "' has no renderer to hide.");
                    hide.Add(renderer);
                }
                foreach (InteriorHeroV01Builder.Carve cut in prop.carve ?? Array.Empty<InteriorHeroV01Builder.Carve>())
                {
                    MeshFilter filter = Named(ship, cut.target).GetComponent<MeshFilter>()
                        ?? throw new InvalidOperationException("'" + cut.target + "' has no mesh to carve.");
                    if (!carve.TryGetValue(filter, out List<Bounds> boxes)) carve.Add(filter, boxes = new List<Bounds>());
                    var box = new Bounds();
                    box.SetMinMax(V(cut.min), V(cut.max));
                    boxes.Add(box);
                }
            }

            Undo.SetCurrentGroupName("Install Interior Hero V01");
            int group = Undo.GetCurrentGroup();
            foreach (Renderer renderer in hide)
            {
                if (!renderer.enabled) { report.AppendLine("already hidden: " + renderer.name); continue; }
                Undo.RecordObject(renderer, "Install Interior Hero V01");
                renderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                report.AppendLine("renderer disabled: " + PathOf(renderer.transform));
            }
            if (!AssetDatabase.IsValidFolder(CarvedFolder)) AssetDatabase.CreateFolder(InteriorHeroV01Builder.Root, "Carved");
            foreach (KeyValuePair<MeshFilter, List<Bounds>> pair in carve)
            {
                MeshFilter filter = pair.Key;
                Mesh original = OriginalMesh(filter);
                Mesh carved = Carve(original, filter.transform, ship, pair.Value, out int removed, out int total);
                if (removed == 0) throw new InvalidOperationException("Nothing to carve from " + filter.name + ": the boxes miss the old props.");
                string path = CarvedFolder + "/" + filter.name + CarvedSuffix + ".asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(carved, path);
                Undo.RecordObject(filter, "Install Interior Hero V01");
                filter.sharedMesh = carved;
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                report.AppendLine("carved " + PathOf(filter.transform) + ": removed " + removed + " of " + total + " triangles -> " + path);
            }
            AssetDatabase.SaveAssets();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ship);
            Undo.RegisterCreatedObjectUndo(instance, "Install Interior Hero V01");
            instance.name = InstanceName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            foreach (Transform node in instance.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = ship.gameObject.layer;
            if (instance.GetComponentsInChildren<Collider>(true).Length != 0 || instance.GetComponentsInChildren<MonoBehaviour>(true).Length != 0
                || instance.GetComponentsInChildren<Light>(true).Length != 0 || instance.GetComponentsInChildren<Camera>(true).Length != 0)
                throw new InvalidOperationException("The hero prefab must stay geometry only.");
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            report.AppendLine("instantiated " + PathOf(instance.transform) + " with " + spec.props.Length + " props; scene dirty, NOT saved");
            return report.ToString();
        }

        public static string Uninstall(Scene scene)
        {
            Transform ship = ShipRoot(scene);
            var report = new StringBuilder();
            Transform instance = ship.Find(InstanceName);
            if (!instance) return "not installed (nothing changed)";
            InteriorHeroV01Builder.Report spec = InteriorHeroV01Builder.LoadReport();
            Undo.SetCurrentGroupName("Uninstall Interior Hero V01");
            int group = Undo.GetCurrentGroup();
            Undo.DestroyObjectImmediate(instance.gameObject);
            foreach (InteriorHeroV01Builder.PropSpec prop in spec.props)
            {
                foreach (string name in prop.hide ?? Array.Empty<string>())
                {
                    Renderer renderer = Named(ship, name).GetComponent<Renderer>();
                    if (!renderer || renderer.enabled) continue;
                    Undo.RecordObject(renderer, "Uninstall Interior Hero V01");
                    renderer.enabled = true;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    report.AppendLine("renderer restored: " + PathOf(renderer.transform));
                }
                foreach (InteriorHeroV01Builder.Carve cut in prop.carve ?? Array.Empty<InteriorHeroV01Builder.Carve>())
                {
                    MeshFilter filter = Named(ship, cut.target).GetComponent<MeshFilter>();
                    Mesh original = OriginalMesh(filter);
                    if (filter.sharedMesh == original) continue;
                    Undo.RecordObject(filter, "Uninstall Interior Hero V01");
                    filter.sharedMesh = original;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                    report.AppendLine("mesh restored: " + PathOf(filter.transform));
                }
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            report.AppendLine("uninstalled; scene dirty, NOT saved");
            return report.ToString();
        }

        /// <summary>Copy of <paramref name="source"/> without the triangles whose three vertices all lie inside one of the
        /// ship-space boxes. Vertex streams, sub-meshes and material order are kept exactly.</summary>
        private static Mesh Carve(Mesh source, Transform meshSpace, Transform ship, List<Bounds> boxes, out int removed, out int total)
        {
            Mesh copy = Object.Instantiate(source);
            copy.name = source.name + CarvedSuffix;
            Vector3[] vertices = copy.vertices;
            var inside = new bool[vertices.Length];
            Matrix4x4 toShip = ship.worldToLocalMatrix * meshSpace.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = toShip.MultiplyPoint3x4(vertices[i]);
                for (int b = 0; b < boxes.Count; b++)
                    if (boxes[b].Contains(p)) { inside[i] = true; break; }
            }
            removed = 0; total = 0;
            for (int s = 0; s < copy.subMeshCount; s++)
            {
                if (copy.GetTopology(s) != MeshTopology.Triangles) continue;
                int[] triangles = copy.GetTriangles(s);
                var kept = new List<int>(triangles.Length);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    total++;
                    // A triangle goes only when all three corners sit in the SAME box.
                    bool cut = false;
                    if (inside[triangles[i]] && inside[triangles[i + 1]] && inside[triangles[i + 2]])
                    {
                        Vector3 a = toShip.MultiplyPoint3x4(vertices[triangles[i]]), b2 = toShip.MultiplyPoint3x4(vertices[triangles[i + 1]]),
                            c = toShip.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                        for (int b = 0; b < boxes.Count && !cut; b++) cut = boxes[b].Contains(a) && boxes[b].Contains(b2) && boxes[b].Contains(c);
                    }
                    if (cut) { removed++; continue; }
                    kept.Add(triangles[i]); kept.Add(triangles[i + 1]); kept.Add(triangles[i + 2]);
                }
                copy.SetTriangles(kept, s, false);
            }
            copy.RecalculateBounds();
            return copy;
        }

        /// <summary>The untouched imported mesh for a compartment, even when the scene already shows a carved copy.</summary>
        private static Mesh OriginalMesh(MeshFilter filter)
        {
            Mesh current = filter.sharedMesh;
            if (current && !AssetDatabase.GetAssetPath(current).StartsWith(CarvedFolder, StringComparison.Ordinal)) return current;
            string path = OriginalMeshFolder + filter.name + ".fbx";
            Mesh original = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(m => m.name == filter.name);
            return original ? original : throw new InvalidOperationException("Cannot find the original mesh for " + filter.name + " at " + path);
        }

        private static Transform ShipRoot(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects().Where(r => r.name == ShipModelRootName).ToArray();
            if (roots.Length != 1) throw new InvalidOperationException("Open Bootstrap first: expected one root '" + ShipModelRootName + "'.");
            return roots[0].transform;
        }

        private static Transform Named(Transform root, string name)
        {
            Transform[] found = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
            if (found.Length != 1) throw new InvalidOperationException("Expected exactly one '" + name + "' under " + root.name + ", found " + found.Length);
            return found[0];
        }

        private static Vector3 V(float[] v) => v != null && v.Length == 3 ? new Vector3(v[0], v[1], v[2])
            : throw new InvalidOperationException("Invalid vector in the prop report.");

        private static string PathOf(Transform t)
        {
            string path = t.name;
            while (t.parent) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
