using System;
using System.Collections.Generic;
using System.Linq;
using OutOfReach.Presentation.Cinematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.LaunchGrassV04.Editor
{
    /// <summary>
    /// LAUNCH-GRASS-V04 installer. Places the pampas field under "Launch site - stationary" and the launch look
    /// (Volume + gust/haze presenter) beside the site, switches the V03 shell meadow off (kept, not deleted) and
    /// creates/updates the owned material and Volume profile assets. Idempotent; never adds a collider; asserts
    /// that the active collider set is unchanged. The menu item does not save the scene; the integrator's batch
    /// entry point <see cref="InstallIntoBootstrap"/> does.
    /// </summary>
    public static class LaunchGrassV04Installer
    {
        public const string Folder = "Assets/_OutOfReach/Visuals/LaunchGrassV04";
        public const string GrassShaderPath = Folder + "/PampasGrassV04.shader";
        public const string HazeShaderPath = Folder + "/LaunchHazeDomeV04.shader";
        public const string GrassMaterialPath = Folder + "/PampasGrassV04.mat";
        public const string HazeMaterialPath = Folder + "/LaunchHazeDomeV04.mat";
        public const string ProfilePath = Folder + "/LaunchCinematicV04.asset";
        public const string PlumeTexturePath = Folder + "/PampasPlumesV04.png";
        public const string ClumpTexturePath = Folder + "/PampasClumpsV04.png";
        public const string TopTexturePath = Folder + "/PampasTopsV04.png";
        public const string FieldName = "Launch grass V04 - pampas field";
        public const string LookName = "Launch look V04 - grade, haze and thruster gust";
        public const string ShellMeadowName = "Launch grass V03 - dense meadow";
        public const string BootstrapPath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
        private const string Relief = "Coastal relief and launch pad detail";
        private static readonly string[] IgnoredGroups =
        {
            Relief + "/LT_LandTerrain", "Launch terrain V03 - far land and mountains", ShellMeadowName, FieldName,
            "Launch landscape V02 - ridges and dry scrub", "Launch vegetation V03 - photographed foreground",
        };

        public static string LastReport { get; private set; }

        [MenuItem("Tools/OUT OF REACH/Launch Grass V04 - install into open scene (no save)")]
        public static void InstallMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            Apply(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Launch Grass V04", "Installed. Review the launch site, then save the scene.\n\n" + LastReport, "OK");
        }

        [MenuItem("Tools/OUT OF REACH/Launch Grass V04 - log cost for the Scene view")]
        public static void LogCostMenu()
        {
            var field = Object.FindObjectsByType<PampasGrassField>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (!field) { Debug.LogWarning("LAUNCH_GRASS_V04: no field in the open scene."); return; }
            Debug.Log(CostLine(field));
        }

        public static string CostLine(PampasGrassField f) =>
            "LAUNCH_GRASS_V04_COST instances=" + f.InstanceCount + " chunks=" + f.ChunkCount + " nearTris=" + f.NearTriangles
            + " cardTris=" + f.CardTriangles + " buildMs=" + f.BuildMilliseconds.ToString("F1") + " lastDrawCalls=" + f.LastDrawCalls
            + " lastNearInstances=" + f.LastNearInstances + " lastCardInstances=" + f.LastCardInstances + " lastTriangles=" + f.LastTriangles
            + " reduced=" + f.ReducedEffects;

        /// <summary>Integrator batch entry: -executeMethod OutOfReach.Presentation.LaunchGrassV04.Editor.LaunchGrassV04Installer.InstallIntoBootstrap</summary>
        public static void InstallIntoBootstrap()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            Apply(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Bootstrap could not be saved.");
            Debug.Log("LAUNCH_GRASS_V04_INSTALLED " + BootstrapPath + " " + LastReport);
        }

        public static GameObject Apply(Scene scene)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Install in Edit mode.");
            Transform site = FindSite(scene);
            if ((site.lossyScale - Vector3.one).sqrMagnitude > .0001f) throw new InvalidOperationException("The launch site must keep metre scale 1.");
            Transform innerNode = site.Find(Relief + "/LT_LandTerrain/LT_TerrainInner");
            MeshFilter inner = innerNode ? innerNode.GetComponent<MeshFilter>() : null;
            if (!inner || !inner.sharedMesh || !inner.sharedMesh.isReadable)
                throw new InvalidOperationException("The readable LT_TerrainInner ground mesh is required.");
            var collidersBefore = ActiveColliders(scene);

            Material grass = EnsureMaterial(GrassMaterialPath, GrassShaderPath);
            grass.SetTexture("_PlumeTex", EnsureTexture(PlumeTexturePath));
            grass.SetTexture("_ClumpTex", EnsureTexture(ClumpTexturePath));
            grass.SetTexture("_TopTex", EnsureTexture(TopTexturePath));
            Material haze = EnsureMaterial(HazeMaterialPath, HazeShaderPath);
            VolumeProfile profile = EnsureProfile();
            Vector4[] footprints = Footprints(site, inner);

            Transform old = site.Find(FieldName);
            if (old) Object.DestroyImmediate(old.gameObject);
            var fieldGo = new GameObject(FieldName);
            fieldGo.transform.SetParent(site, false);
            var field = fieldGo.AddComponent<PampasGrassField>();
            field.Configure(grass, inner, footprints);

            var switchedOff = new List<string>();
            Transform shells = site.Find(ShellMeadowName);
            if (shells && shells.gameObject.activeSelf) { shells.gameObject.SetActive(false); switchedOff.Add(ShellMeadowName); }

            // The look lives beside the site (not under it) so the grade can blend out before the departure
            // switches the stationary site off at UpperAirSeconds.
            Transform lookParent = site.parent;
            foreach (Transform t in (lookParent ? lookParent.Cast<Transform>() : scene.GetRootGameObjects().Select(g => g.transform)).ToArray())
                if (t.name == LookName) Object.DestroyImmediate(t.gameObject);
            var look = new GameObject(LookName);
            if (lookParent) look.transform.SetParent(lookParent, false);
            else SceneManager.MoveGameObjectToScene(look, scene);
            look.layer = 0; // the player camera's volume mask is the Default layer
            var volume = look.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 20; volume.weight = 0f; volume.sharedProfile = profile;
            var presenter = look.AddComponent<LaunchGrassGustPresenter>();
            var departures = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TransportDeparturePresenter>(true)).ToArray();
            TransportDeparturePresenter departure = departures.Length == 1 ? departures[0] : null;
            Transform origin = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "Land pad top plane") ?? site;
            presenter.Configure(departure, origin, field, volume, haze);

            if (!collidersBefore.SequenceEqual(ActiveColliders(scene)))
                throw new InvalidOperationException("Installer changed the set of active colliders.");
            LastReport = "field=" + FieldName + " instances=" + field.InstanceCount + " chunks=" + field.ChunkCount
                + " nearTris=" + field.NearTriangles + " cardTris=" + field.CardTriangles + " buildMs=" + field.BuildMilliseconds.ToString("F1")
                + " footprintsCleared=" + footprints.Length + " departureBound=" + (departure ? departure.name : "NONE(" + departures.Length + ")")
                + " gustOrigin=" + origin.name + " switchedOff=[" + string.Join("; ", switchedOff) + "] collidersChanged=0";
            Debug.Log("LAUNCH_GRASS_V04_READY " + LastReport);
            return fieldGo;
        }

        public static Transform FindSite(Scene scene)
        {
            var sites = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == "Launch site - stationary").ToArray();
            if (sites.Length != 1) throw new InvalidOperationException("Expected exactly one 'Launch site - stationary' in " + scene.path + ", found " + sites.Length);
            return sites[0];
        }

        private static List<int> ActiveColliders(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Collider>(true)).Where(c => c.enabled && c.gameObject.activeInHierarchy)
            .Select(c => c.GetInstanceID()).OrderBy(i => i).ToList();

        /// <summary>Site-local XZ rectangles of standing facility geometry outside the authored clear areas.</summary>
        private static Vector4[] Footprints(Transform site, MeshFilter inner)
        {
            var ignored = IgnoredGroups.Select(site.Find).Where(t => t).ToArray();
            float groundTop = site.InverseTransformPoint(inner.GetComponent<Renderer>().bounds.center).y + 15f;
            var rects = new List<Vector4>();
            foreach (Renderer r in site.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer || ignored.Any(g => r.transform.IsChildOf(g))) continue;
                Bounds b = r.bounds;
                Vector3 lo = site.InverseTransformPoint(b.min), hi = site.InverseTransformPoint(b.max);
                Vector3 mn = Vector3.Min(lo, hi), mx = Vector3.Max(lo, hi);
                if (mx.x - mn.x > 220f || mx.z - mn.z > 220f || mx.y - mn.y < .5f || mn.y > groundTop) continue;
                var rect = new Vector4(mn.x - 1.5f, mn.z - 1.5f, mx.x + 1.5f, mx.z + 1.5f);
                if (Inside(rect, new Vector4(-78, -68, 78, 78)) || Inside(rect, new Vector4(-24, -915, 24, -55)) || Inside(rect, new Vector4(-30, 62, 30, 335))) continue;
                rects.Add(rect);
            }
            return rects.OrderByDescending(v => (v.z - v.x) * (v.w - v.y)).Take(256).ToArray();
        }

        private static bool Inside(Vector4 a, Vector4 b) => a.x >= b.x && a.y >= b.y && a.z <= b.z && a.w <= b.w;

        private static Material EnsureMaterial(string path, string shaderPath)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Shader missing or has errors: " + shaderPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            // The installer owns these materials: every install resets them to the committed shader defaults.
            var defaults = new Material(shader);
            material.shader = shader;
            material.CopyPropertiesFromMaterial(defaults);
            Object.DestroyImmediate(defaults);
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Alpha-tested atlas import: mip-maps that preserve coverage, clamped, sRGB.</summary>
        private static Texture2D EnsureTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing texture: " + path);
            bool ok = importer.mipmapEnabled && importer.mipMapsPreserveCoverage && Mathf.Abs(importer.alphaTestReferenceValue - .4f) < .001f
                && importer.wrapMode == TextureWrapMode.Clamp && importer.alphaIsTransparency && importer.sRGBTexture
                && importer.anisoLevel == 4 && importer.textureCompression == TextureImporterCompression.CompressedHQ;
            if (!ok)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = true; importer.mipMapsPreserveCoverage = true; importer.alphaTestReferenceValue = .4f;
                importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true; importer.sRGBTexture = true;
                importer.anisoLevel = 4; importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidOperationException("Texture did not import: " + path);
        }

        /// <summary>Warm, restrained launch grade over the existing ACES "AURORA restrained film grade" (priority 10).</summary>
        public static VolumeProfile EnsureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Launch cinematic V04";
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            T Effect<T>() where T : VolumeComponent
            {
                if (profile.TryGet(out T existing)) return existing;
                T effect = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(effect, profile);
                return effect;
            }
            Effect<Tonemapping>().mode.Override(TonemappingMode.ACES);            // same mode as the base grade: no pop
            var bloom = Effect<Bloom>();
            bloom.threshold.Override(1.05f); bloom.intensity.Override(.26f); bloom.scatter.Override(.72f);
            bloom.tint.Override(new Color(1f, .86f, .64f));
            var white = Effect<WhiteBalance>();
            white.temperature.Override(14f); white.tint.Override(2f);
            var adjust = Effect<ColorAdjustments>();
            adjust.postExposure.Override(.35f); adjust.contrast.Override(9f); adjust.saturation.Override(-6f);
            adjust.colorFilter.Override(new Color(1f, .97f, .92f));
            var split = Effect<SplitToning>();
            split.shadows.Override(new Color(.40f, .45f, .50f));                  // graphite
            split.highlights.Override(new Color(.84f, .63f, .38f));               // amber
            split.balance.Override(18f);
            var grain = Effect<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(.13f); grain.response.Override(.8f);
            var vignette = Effect<Vignette>();
            vignette.intensity.Override(.2f); vignette.smoothness.Override(.55f);
            foreach (VolumeComponent c in profile.components) EditorUtility.SetDirty(c);
            EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}
