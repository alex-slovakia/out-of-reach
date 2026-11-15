using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OutOfReach.Player;
using OutOfReach.Presentation.Hands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>
    /// EVA-TETHER-01 installer for the integrator. Adds the visible tether rope (original braided asset under
    /// <see cref="AssetRoot"/>) to the existing EVA route, binds it to the existing astronaut hands presenter for the
    /// hand-over-hand climb, adds the upper arm sleeves (EvaTetherArmSleeves), bakes the bracket feet onto the visible hull
    /// and turns off the old amber route guide line it replaces. Idempotent: running it again
    /// updates the same rope object. It edits no scene other than the one holding the given route and never touches
    /// gameplay wiring: the route, waypoints, gates, player and mission composition stay as they are.
    /// <para>
    /// Integrator call, in the builder after the route and hands exist: <c>EvaTetherAuthoring.Install(route, hands);</c>
    /// or, on the saved scene, menu OUT OF REACH/EVA Tether V02/Install into open scene (then save), or batch
    /// <c>-executeMethod OutOfReach.Composition.Editor.EvaTetherAuthoring.InstallIntoBootstrapBatch</c>.
    /// </para>
    /// </summary>
    public static class EvaTetherAuthoring
    {
        public const string AssetRoot = "Assets/_OutOfReach/Visuals/Props/EvaTetherV02";
        public const string RopeObjectName = "EVA tether rope V02";
        public const string BootstrapScenePath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
        public const string RopeMaterialPath = AssetRoot + "/Materials/EvaTetherV02_Rope.mat";
        public const string BracketMaterialPath = AssetRoot + "/Materials/EvaTetherV02_Bracket.mat";
        public const string ClipMaterialPath = AssetRoot + "/Materials/EvaTetherV02_Clip.mat";
        public const string SleeveMaterialPath = AssetRoot + "/Materials/EvaTetherV02_Sleeve.mat";
        public const string SleevesObjectName = "EVA tether arm sleeves V02";
        public const string BaseColorPath = AssetRoot + "/Textures/EvaTetherV02_Rope_BaseColor.png";
        public const string NormalPath = AssetRoot + "/Textures/EvaTetherV02_Rope_Normal.png";

        /// <summary>
        /// Installs or updates the rope on the route and binds it to the hands presenter. Returns the rope component.
        /// </summary>
        public static EvaTetherRope Install(TetheredRoute route, AstronautHandsPresenter hands)
        {
            if (!route || !hands)
            {
                throw new ArgumentException("The EVA tether needs the existing TetheredRoute and AstronautHandsPresenter.");
            }

            if (!route.IsValid)
            {
                throw new InvalidOperationException("The EVA route needs at least two waypoints before the tether follows it.");
            }

            Material rope = Required<Material>(RopeMaterialPath);
            Material bracket = Required<Material>(BracketMaterialPath);
            Material clip = Required<Material>(ClipMaterialPath);
            Transform existing = route.transform.Find(RopeObjectName);
            GameObject ropeObject = existing ? existing.gameObject : new GameObject(RopeObjectName);
            if (!existing)
            {
                Undo.RegisterCreatedObjectUndo(ropeObject, "Install EVA tether");
                ropeObject.transform.SetParent(route.transform, false);
            }

            ropeObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ropeObject.transform.localScale = Vector3.one;
            EvaTetherRope tether = ropeObject.GetComponent<EvaTetherRope>();
            if (!tether)
            {
                tether = Undo.AddComponent<EvaTetherRope>(ropeObject);
            }

            Undo.RecordObject(tether, "Configure EVA tether");
            tether.Configure(route.Waypoints.ToArray(), route.HandStrokeMetres, route.NearReachMetres, rope, bracket, clip);
            // Bracket feet on the visible hull: temporary mesh colliders on the hull meshes, removed again.
            Transform player = hands.MotionMount ? hands.MotionMount.root : null;
            int onHull = WithHullProbes(tether, player, () => tether.BakeBrackets());
            Debug.Log("EVA tether: " + onHull + " of " + tether.AnchorParams.Count + " anchor brackets stand on a visible surface.", tether);
            EditorUtility.SetDirty(tether);

            var handsFields = new SerializedObject(hands);
            SerializedProperty ropeField = handsFields.FindProperty("tetherRope");
            if (ropeField == null)
            {
                throw new InvalidOperationException("AstronautHandsPresenter has no tetherRope field (EVA-TETHER-01 code missing).");
            }

            ropeField.objectReferenceValue = tether;
            handsFields.ApplyModifiedProperties();

            InstallSleeves(hands);

            // The amber route guide is replaced by the real rope; keep the component, only stop drawing it.
            LineRenderer guide = route.GetComponent<LineRenderer>();
            if (guide && guide.enabled)
            {
                Undo.RecordObject(guide, "Hide old EVA route guide");
                guide.enabled = false;
                EditorUtility.SetDirty(guide);
            }

            if (route.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(route.gameObject.scene);
            }

            return tether;
        }

        /// <summary>
        /// Upper sleeves that join the climbing glove forearms to the suit shoulders: a child of the hands presenter,
        /// bound to the actual EVA shoulder bones when the scene has the tool sleeve's skeleton, else view shoulders.
        /// </summary>
        public static EvaTetherArmSleeves InstallSleeves(AstronautHandsPresenter hands)
        {
            Transform existing = hands.transform.Find(SleevesObjectName);
            GameObject sleevesObject = existing ? existing.gameObject : new GameObject(SleevesObjectName);
            if (!existing)
            {
                Undo.RegisterCreatedObjectUndo(sleevesObject, "Install EVA tether sleeves");
                sleevesObject.transform.SetParent(hands.transform, false);
            }

            EvaTetherArmSleeves sleeves = sleevesObject.GetComponent<EvaTetherArmSleeves>();
            if (!sleeves)
            {
                sleeves = Undo.AddComponent<EvaTetherArmSleeves>(sleevesObject);
            }

            Transform right = null;
            Transform left = null;
            EvaToolSleevePresenter tool = Object.FindObjectsByType<EvaToolSleevePresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.Shoulder);
            if (tool)
            {
                right = tool.Shoulder;
                left = tool.Shoulder.root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "upperarm01.L");
                if (!left)
                {
                    right = null; // both actual bones or both view shoulders, never one of each
                }
            }

            Undo.RecordObject(sleeves, "Configure EVA tether sleeves");
            sleeves.Configure(hands, right, left, Required<Material>(SleeveMaterialPath));
            EditorUtility.SetDirty(sleeves);
            return sleeves;
        }

        [MenuItem("OUT OF REACH/EVA Tether V02/Install into open scene")]
        public static void InstallIntoOpenScene()
        {
            TetheredRoute route = ExactlyOne<TetheredRoute>();
            AstronautHandsPresenter hands = ExactlyOne<AstronautHandsPresenter>();
            EvaTetherRope rope = Install(route, hands);
            Debug.Log("EVA tether installed on '" + route.name + "' (" + rope.Waypoints.Count + " waypoints). Save the scene to keep it.", rope);
        }

        /// <summary>Batch entry: opens the saved Bootstrap, installs and saves it.</summary>
        public static void InstallIntoBootstrapBatch()
        {
            try
            {
                CreateOrUpdateAssets();
                Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
                InstallIntoOpenScene();
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException("Could not save " + BootstrapScenePath);
                }

                Debug.Log("EVA-TETHER-01: installed into " + BootstrapScenePath);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Creates or updates the three materials and the texture import settings of the original tether asset.
        /// Safe to run repeatedly; it writes only under <see cref="AssetRoot"/>.
        /// </summary>
        [MenuItem("OUT OF REACH/EVA Tether V02/Create or update assets")]
        public static void CreateOrUpdateAssets()
        {
            ConfigureTexture(BaseColorPath, false);
            ConfigureTexture(NormalPath, true);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (!lit)
            {
                throw new InvalidOperationException("URP Lit shader is unavailable.");
            }

            Material rope = MaterialAt(RopeMaterialPath, lit);
            rope.SetTexture("_BaseMap", Required<Texture2D>(BaseColorPath));
            rope.SetColor("_BaseColor", Color.white);
            rope.SetTexture("_BumpMap", Required<Texture2D>(NormalPath));
            rope.SetFloat("_BumpScale", 1f);
            rope.EnableKeyword("_NORMALMAP");
            rope.SetFloat("_Metallic", 0f);
            rope.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(rope);

            Material bracket = MaterialAt(BracketMaterialPath, lit);
            bracket.SetColor("_BaseColor", new Color(0.30f, 0.31f, 0.33f, 1f));
            bracket.SetFloat("_Metallic", 0.8f);
            bracket.SetFloat("_Smoothness", 0.5f);
            EditorUtility.SetDirty(bracket);

            Material clip = MaterialAt(ClipMaterialPath, lit);
            clip.SetColor("_BaseColor", new Color(0.86f, 0.50f, 0.10f, 1f));
            clip.SetFloat("_Metallic", 0.55f);
            clip.SetFloat("_Smoothness", 0.62f);
            EditorUtility.SetDirty(clip);

            Material sleeve = MaterialAt(SleeveMaterialPath, lit);
            sleeve.SetColor("_BaseColor", new Color(0.78f, 0.77f, 0.74f, 1f));
            sleeve.SetFloat("_Metallic", 0f);
            sleeve.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(sleeve);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Batch entry for <see cref="CreateOrUpdateAssets"/>.</summary>
        public static void CreateOrUpdateAssetsBatch()
        {
            try
            {
                CreateOrUpdateAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Clearance of the built rope, clips and brackets against every collider and every renderer mesh of the open
        /// scene except the player rig and the tether itself (renderer meshes are tested through temporary mesh
        /// colliders that are destroyed afterwards). Returns one line per finding; empty means clear.
        /// </summary>
        public static List<string> ClearanceReport(EvaTetherRope rope, Transform ignoreRoot, float marginMetres = 0.005f)
        {
            var findings = new List<string>();
            if (!rope || !rope.Build())
            {
                findings.Add("rope could not be built");
                return findings;
            }

            var temporary = new List<Object>();
            try
            {
                temporary.AddRange(AddHullProbes(rope, ignoreRoot, 0.6f));
                Physics.SyncTransforms();
                float radius = rope.RadiusMetres + marginMetres;
                float step = 0.02f;
                for (float d = rope.MinParam; d <= rope.MaxParam; d += step)
                {
                    Vector3 point = rope.PointAt(d);
                    foreach (Collider hit in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (IsUnder(hit.transform, rope.transform) || (ignoreRoot && IsUnder(hit.transform, ignoreRoot)))
                        {
                            continue;
                        }

                        findings.Add($"rope d={d:F2} m at {point} touches {HierarchyPath(hit.transform)}"
                            + (temporary.Contains(hit) ? " (visual mesh)" : " (collider)"));
                    }
                }
            }
            finally
            {
                foreach (Object probe in temporary)
                {
                    if (probe)
                    {
                        Object.DestroyImmediate(probe);
                    }
                }

                Physics.SyncTransforms();
            }

            return findings.Distinct().ToList();
        }

        /// <summary>Runs an action while every visible renderer mesh near the rope has a temporary mesh collider.</summary>
        public static T WithHullProbes<T>(EvaTetherRope rope, Transform ignoreRoot, Func<T> action)
        {
            List<MeshCollider> probes = AddHullProbes(rope, ignoreRoot, 1.8f);
            try
            {
                Physics.SyncTransforms();
                return action();
            }
            finally
            {
                foreach (MeshCollider probe in probes)
                {
                    if (probe)
                    {
                        Object.DestroyImmediate(probe);
                    }
                }

                Physics.SyncTransforms();
            }
        }

        private static List<MeshCollider> AddHullProbes(EvaTetherRope rope, Transform ignoreRoot, float margin)
        {
            var probes = new List<MeshCollider>();
            foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!filter || !filter.sharedMesh || filter.GetComponent<Collider>() || IsUnder(filter.transform, rope.transform)
                    || (ignoreRoot && IsUnder(filter.transform, ignoreRoot)))
                {
                    continue;
                }

                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !renderer.enabled || !NearRope(rope, renderer.bounds, margin))
                {
                    continue;
                }

                var probe = filter.gameObject.AddComponent<MeshCollider>();
                probe.sharedMesh = filter.sharedMesh;
                probes.Add(probe);
            }

            return probes;
        }

        private static bool NearRope(EvaTetherRope rope, Bounds bounds, float margin)
        {
            bounds.Expand(margin * 2f);
            for (float d = rope.MinParam; d <= rope.MaxParam; d += 0.1f)
            {
                if (bounds.Contains(rope.PointAt(d)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUnder(Transform node, Transform root) => node && root && (node == root || node.IsChildOf(root));

        private static string HierarchyPath(Transform node)
        {
            var parts = new List<string>();
            for (Transform t = node; t; t = t.parent)
            {
                parts.Insert(0, t.name);
            }

            return string.Join("/", parts);
        }

        private static void ConfigureTexture(string path, bool normal)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer)
            {
                throw new InvalidOperationException("Missing texture " + path);
            }

            bool changed = false;
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type)
            {
                importer.textureType = type;
                changed = true;
            }

            if (importer.sRGBTexture != !normal)
            {
                importer.sRGBTexture = !normal;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }

            if (importer.anisoLevel != 4)
            {
                importer.anisoLevel = 4;
                changed = true;
            }

            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                changed = true;
            }

            if (importer.maxTextureSize != 512)
            {
                importer.maxTextureSize = 512;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static Material MaterialAt(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material)
            {
                if (material.shader != shader)
                {
                    material.shader = shader;
                }

                return material;
            }

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? AssetRoot);
            material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static T Required<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset)
            {
                throw new InvalidOperationException("Missing EVA tether asset " + path + " (run Create or update assets).");
            }

            return asset;
        }

        private static T ExactlyOne<T>() where T : Component
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length != 1)
            {
                throw new InvalidOperationException("Expected exactly one " + typeof(T).Name + " in the open scene, found " + found.Length + ".");
            }

            return found[0];
        }
    }
}
