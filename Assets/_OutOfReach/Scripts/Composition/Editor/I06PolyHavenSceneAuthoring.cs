using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Places licensed static details without creating resource sources or gameplay controls.</summary>
    public static class I06PolyHavenSceneAuthoring
    {
        private const string Group = "Curated CC0 service details";
        private static readonly List<string> Evidence = new List<string>();

        public static void ApplyToSavedBootstrap()
        {
            if (Application.isPlaying || Application.unityVersion != "6000.3.28f1"
                || !Application.dataPath.Replace('\\', '/').Contains("/.local/"))
                throw new InvalidOperationException("Use the isolated Unity 6000.3.28f1 checkout in Edit mode.");
            PolyHavenAssetAuthoring.BuildAssets();
            I06SurfaceFinishAuthoring.EnsureMaterials();
            var scene = EditorSceneManager.OpenScene(I01BootstrapBuilder.ScenePath, OpenSceneMode.Single);
            var composition = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<I01MissionComposition>(true)).Single();
            var serializedComposition = new SerializedObject(composition);
            serializedComposition.FindProperty("restWakeCue").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_OutOfReach/Audio/Production/RestWakeV01/rest-wake-cinematic.wav");
            serializedComposition.ApplyModifiedPropertiesWithoutUndo();
            var marine = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<I06MarineBindings>(true)).Single();
            Transform site = marine.MarineRoot.Find("Launch site - stationary");
            Transform dock = site.Find("Staging module at authored mate");
            Transform ship = scene.GetRootGameObjects().Single(root => root.name == "AURORA-7 interior").transform;
            if (!site || !dock) throw new InvalidOperationException("Authored launch frames missing.");
            Evidence.Clear();
            I06GroundFinishAuthoring.Apply(site);
            Transform groundDetails = ResetGroup(site), dockDetails = ResetGroup(dock), shipDetails = ResetGroup(ship);
            float padY = site.InverseTransformPoint(site.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Land pad top plane").position).y;

            // Outer service apron: away from the ascent corridor, cargo lanes and flame trench.
            Put("portable_generator", groundDetails, new Vector3(24, padY, -28), new Vector3(0, 35, 0));
            Put("flood_lights", groundDetails, new Vector3(26, padY, -30), new Vector3(0, -35, 0));
            Put("flood_lights", groundDetails, new Vector3(-25, padY, -30), new Vector3(0, 35, 0));
            Put("metal_toolbox", groundDetails, new Vector3(23, padY, -27.6f), Vector3.zero);

            // Close-range service corner outside the 2.3 m arrival/boarding lane.
            Put("flood_lights", dockDetails, new Vector3(3.15f, .025f, -1.15f), new Vector3(0, -45, 0));
            Put("metal_toolbox", dockDetails, new Vector3(2.85f, 1.305f, .88f), new Vector3(0, 180, 0));

            // Wall fixtures occupy shallow side bays, never the central floating corridor.
            Mount("vintage_spacecraft_instrument", shipDetails, new Vector3(2.13f, 1.25f, -1.15f), Vector3.zero, .85f);
            Mount("binoculars", shipDetails, new Vector3(-2.13f, .76f, -.65f), new Vector3(90, 0, 0), 1f);
            Mount("medical_box", shipDetails, new Vector3(2.13f, 1.32f, 12.1f), new Vector3(90, 0, 0), 1f);
            Mount("long_life_food", shipDetails, new Vector3(-1.89f, 1.60f, 15.20f), Vector3.zero, 1f);
            Mount("combination_wrench", shipDetails, new Vector3(1.90f, 2.10f, 6.55f), new Vector3(90, 0, 0), 1f);
            Mount("adjustable_wrench", shipDetails, new Vector3(1.90f, 2.10f, 6.78f), Vector3.zero, 1f);
            Mount("pliers", shipDetails, new Vector3(1.90f, 2.10f, 6.98f), Vector3.zero, 1f);
            Mount("screwdrivers_02", shipDetails, new Vector3(1.90f, 2.10f, 7.22f), new Vector3(90, 0, 0), 1f);

            // Terrain heights are sampled on the current authored mesh; temporary colliders never survive saving.
            var temporary = new List<MeshCollider>();
            try
            {
                Transform terrain = site.Find("Coastal relief and launch pad detail");
                foreach (MeshFilter filter in terrain.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.Contains("Terrain") && f.sharedMesh))
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh; temporary.Add(collider);
                }
                Physics.SyncTransforms();
                var points = new[] { new Vector3(-82,0,-45), new Vector3(-94,0,-19), new Vector3(83,0,-39), new Vector3(97,0,12), new Vector3(-87,0,39), new Vector3(92,0,58) };
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 p = points[i];
                    var ray = new Ray(site.TransformPoint(new Vector3(p.x, 300, p.z)), -site.up);
                    var hits = new List<RaycastHit>();
                    foreach (MeshCollider c in temporary) if (c.Raycast(ray, out RaycastHit hit, 800)) hits.Add(hit);
                    if (hits.Count == 0) throw new InvalidOperationException("No actual terrain at rock location " + p);
                    p.y = site.InverseTransformPoint(hits.OrderBy(h => h.distance).First().point).y - .12f;
                    Put(i % 2 == 0 ? "coast_land_rocks_04" : "namaqualand_boulder_02", groundDetails, p,
                        new Vector3(0, i * 61, 0), i % 2 == 0 ? .75f : 1.5f, false);
                }
            }
            finally { foreach (MeshCollider c in temporary) if (c) Object.DestroyImmediate(c); }
            I01BootstrapBuilder.ValidateScene(scene);
            Validate(shipDetails, true); Validate(dockDetails, false); Validate(groundDetails, false);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Bootstrap save failed.");
            Directory.CreateDirectory("docs/art-direction/polyhaven-import-20261115");
            File.WriteAllLines("docs/art-direction/polyhaven-import-20261115/scene-placement.txt", Evidence);
            Debug.Log("POLY_HAVEN_SCENE_PASS " + Evidence.Count + " static assets mounted; canonical gameplay unchanged.");
        }

        public static void ApplyReviewedFinish()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/.local/") || Application.isPlaying)
                throw new InvalidOperationException("Isolated Edit mode only.");
            I06NasaDeepSkyAuthoring.EnsureAssets();
            var scene = EditorSceneManager.OpenScene(I01BootstrapBuilder.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var ship = roots.Single(r => r.name == "AURORA-7 interior").transform;
            Transform group = ship.Find(Group);
            foreach (string id in new[] { "combination_wrench", "adjustable_wrench", "pliers", "screwdrivers_02" })
            {
                Transform mount = group.Find("Secured " + id);
                mount.localPosition = new Vector3(1.90f, 2.10f, mount.localPosition.z);
            }
            group.Find("Secured long_life_food").localPosition = new Vector3(-1.89f, 1.60f, 15.20f);
            var marine = roots.SelectMany(r => r.GetComponentsInChildren<I06MarineBindings>(true)).Single();
            Transform site = marine.MarineRoot.Find("Launch site - stationary");
            foreach (Transform child in site.GetComponentsInChildren<Transform>(true))
                if (child.name == "LSS_Scrub_v02") child.gameObject.SetActive(false);
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(I06NasaDeepSkyAuthoring.MaterialPath);
            RenderSettings.skybox = sky;
            var serializedMarine = new SerializedObject(marine);
            serializedMarine.FindProperty("orbitalSky").objectReferenceValue = sky;
            serializedMarine.ApplyModifiedPropertiesWithoutUndo();
            Validate(group, true);
            I01BootstrapBuilder.ValidateScene(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Reviewed scene save failed.");
            Debug.Log("REVIEWED_FINISH_PASS NASA sky bound; tool/ration mount positions adjusted after native inspection.");
        }

        private static Transform ResetGroup(Transform parent)
        {
            Transform previous = parent.Find(Group);
            if (previous) Object.DestroyImmediate(previous.gameObject);
            var group = new GameObject(Group).transform; group.SetParent(parent, false); return group;
        }

        private static GameObject Put(string id, Transform parent, Vector3 position, Vector3 rotation, float scale = 1, bool collision = true)
        {
            string path = PolyHavenAssetAuthoring.Root + "/Prefabs/PH_" + id + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) throw new InvalidOperationException("Curated prefab missing: " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = "PH " + id;
            instance.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(rotation));
            instance.transform.localScale = Vector3.one * scale;
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) c.enabled = collision;
            Evidence.Add(id + " | " + parent.parent.name + " | " + position + " | scale " + scale);
            return instance;
        }

        private static void Mount(string id, Transform parent, Vector3 position, Vector3 objectRotation, float scale)
        {
            var mount = new GameObject("Secured " + id).transform;
            mount.SetParent(parent, false);
            mount.localPosition = position;
            mount.localRotation = Quaternion.LookRotation(position.x > 0 ? Vector3.left : Vector3.right, Vector3.up);
            GameObject instance = Put(id, mount, Vector3.zero, objectRotation, scale, false);
            Bounds bounds = LocalBounds(instance, mount);
            instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - .025f);
            bounds = LocalBounds(instance, mount);
            Material dark = I06SurfaceFinishAuthoring.PowderCoat;
            if (!dark) throw new InvalidOperationException("Existing mount material missing.");
            Box("Recessed mounting plate", mount, new Vector3(0, 0, .006f), new Vector3(bounds.size.x + .08f, bounds.size.y + .08f, .012f), dark);
            foreach (float side in new[] { -1f, 1f })
                Box("Brushed mounting ear", mount, new Vector3(side * (bounds.size.x * .5f + .025f), 0, .014f),
                    new Vector3(.025f, bounds.size.y * .6f, .014f), I06SurfaceFinishAuthoring.BrushedSteel);
            // Visible captive bands explain why tools and stores cannot float loose.
            float bandWidth = Mathf.Min(.025f, bounds.size.x * .2f);
            foreach (float x in new[] { -bounds.size.x * .28f, bounds.size.x * .28f })
                Box("Retention band", mount, new Vector3(x, 0, bounds.max.z + .002f), new Vector3(bandWidth, bounds.size.y + .015f, .007f), dark);
        }

        private static void Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
            obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = size;
            Object.DestroyImmediate(obj.GetComponent<Collider>()); obj.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Bounds LocalBounds(GameObject root, Transform frame)
        {
            bool first = true; Bounds bounds = default;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Bounds b = filter.sharedMesh.bounds;
                for (int n = 0; n < 8; n++)
                {
                    Vector3 p = frame.InverseTransformPoint(filter.transform.TransformPoint(b.center + Vector3.Scale(b.extents,
                        new Vector3((n & 1) == 0 ? -1 : 1, (n & 2) == 0 ? -1 : 1, (n & 4) == 0 ? -1 : 1))));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        private static void Validate(Transform group, bool ship)
        {
            foreach (Renderer renderer in group.GetComponentsInChildren<Renderer>(true))
                if (renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(m => !m || m.shader.name == "Hidden/InternalErrorShader"))
                    throw new InvalidOperationException("Missing intended material on " + renderer.name);
            if (ship)
            {
                foreach (Transform mount in group)
                {
                    Bounds b = LocalBounds(mount.gameObject, group.parent);
                    if (b.min.x < .95f && b.max.x > -.95f) throw new InvalidOperationException("Detail enters protected corridor: " + mount.name);
                }
            }
        }
    }
}
