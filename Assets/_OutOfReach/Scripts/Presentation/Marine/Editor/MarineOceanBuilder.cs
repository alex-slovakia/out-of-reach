using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Presentation.MarineOcean.Editor
{
    /// <summary>Asset-only authoring. Does not open/save scenes, create cameras/lights, or alter project settings.</summary>
    public static class MarineOceanBuilder
    {
        public const string AssetRoot = "Assets/_OutOfReach/Visuals/Models/MarinePrologue/Ocean";
        public const string PrefabPath = AssetRoot + "/MarineOcean.prefab";
        private const int Segments = 96;
        private const float NearHalfExtent = 96;
        // Beyond the marine camera's 5000m far plane in every azimuth; same mesh topology.
        private const float OuterHalfExtent = 8192;

        [MenuItem("Out Of Reach/Marine Ocean/Build And Validate")]
        public static void BuildAndValidate()
        {
            Require(Application.unityVersion == "6000.3.28f1", "Use Unity 6000.3.28f1.");
            EnsureFolder(AssetRoot + "/Meshes"); EnsureFolder(AssetRoot + "/Materials");
            var waterShader = Shader.Find("OutOfReach/Marine Ocean");
            var splashShader = Shader.Find("OutOfReach/Marine Contact Splash");
            Require(waterShader && splashShader, "Import both supplied shaders first.");
            Require(!ShaderUtil.ShaderHasError(waterShader) && !ShaderUtil.ShaderHasError(splashShader), "Shader reports an import error.");
            var mesh = PersistMesh(BuildMesh(), AssetRoot + "/Meshes/MarineOceanSurface.asset");
            var material = MaterialAsset("MarineWater", waterShader);
            material.SetColor("_DeepColor", new Color(.045f, .20f, .27f));
            material.SetColor("_SkyColor", new Color(.30f, .43f, .56f));
            material.SetColor("_HorizonColor", new Color(.66f, .68f, .60f));
            material.SetColor("_FoamColor", new Color(.55f, .63f, .64f));
            material.SetFloat("_Amplitude", .075f); material.SetFloat("_Roughness", .30f);
            material.SetFloat("_NearRadius", 90); material.SetFloat("_DistanceFadeStart", 180);
            material.SetFloat("_DistanceFadeEnd", 1800); material.SetFloat("_WaveStrength", 1);
            material.SetVector("_OceanPhase", Vector4.zero); material.SetVector("_WaterOrigin", Vector4.zero);
            material.SetInteger("_ContactCount", 0); EditorUtility.SetDirty(material);
            var splashMaterial = MaterialAsset("ContactSplash", splashShader);
            var root = new GameObject("MarineOcean");
            root.transform.position = new Vector3(0, -1.05f, 0);
            try
            {
                var surfaceObject = new GameObject("OceanSurface"); surfaceObject.transform.SetParent(root.transform, false);
                var filter = surfaceObject.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
                var renderer = surfaceObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
                var splashObject = new GameObject("ContactSplashes"); splashObject.transform.SetParent(root.transform, false);
                var splashes = splashObject.AddComponent<ParticleSystem>();
                splashes.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = splashes.main;
                main.playOnAwake = false; main.loop = true; main.maxParticles = 96;
                main.duration = 6.4f; main.startLifetime = .5f; main.startSpeed = 0;
                main.startSize = .05f; main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.useUnscaledTime = true; main.gravityModifier = .6f;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var emission = splashes.emission; emission.enabled = false;
                var shape = splashes.shape; shape.enabled = false;
                var collision = splashes.collision; collision.enabled = false;
                var trails = splashes.trails; trails.enabled = false;
                var color = splashes.colorOverLifetime; color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(0, 1) });
                color.color = gradient;
                var splashRenderer = splashes.GetComponent<ParticleSystemRenderer>();
                splashRenderer.sharedMaterial = splashMaterial; splashRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                splashRenderer.shadowCastingMode = ShadowCastingMode.Off; splashRenderer.receiveShadows = false;
                splashRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                var contacts = new List<OceanContactPoint>(4);
                foreach (float x in new[] { -2.75f, 2.75f })
                    foreach (float z in new[] { -1.85f, 1.85f })
                    {
                        var anchor = new GameObject("Contact_" + (x < 0 ? "Port" : "Starboard") + (z < 0 ? "Aft" : "Forward")).transform;
                        anchor.SetParent(root.transform, false); anchor.localPosition = new Vector3(x, 0, z);
                        contacts.Add(new OceanContactPoint(anchor, .85f, .8f));
                    }
                var presenter = root.AddComponent<MarineOceanPresenter>();
                presenter.ConfigureAssets(renderer, splashes); presenter.SetContacts(contacts.ToArray());
                Validate(root, mesh, presenter);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved); Require(saved, "Prefab save failed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            var persisted = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(persisted && persisted.GetComponent<MarineOceanPresenter>(), "Persisted presenter missing.");
            Debug.Log("MARINE_OCEAN_V01_AUTHORING_PASS | one 9793-vertex mesh / 19200 triangles | 4 explicit contacts | 96 particle cap | zero cameras/listeners/colliders | bind ISimulationClock for motion. GPU/runtime validation still required.");
        }

        private static Mesh BuildMesh()
        {
            int side = Segments + 1, gridCount = side * side, perimeter = Segments * 4;
            var vertices = new Vector3[gridCount + perimeter]; var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Segments * Segments * 6 + perimeter * 6];
            int cursor = 0;
            for (int z = 0; z <= Segments; ++z)
                for (int x = 0; x <= Segments; ++x)
                {
                    int i = z * side + x;
                    vertices[i] = new Vector3(-NearHalfExtent + x * (2 * NearHalfExtent / Segments), 0,
                                             -NearHalfExtent + z * (2 * NearHalfExtent / Segments));
                    if (x == Segments || z == Segments) continue;
                    triangles[cursor++] = i; triangles[cursor++] = i + side; triangles[cursor++] = i + 1;
                    triangles[cursor++] = i + 1; triangles[cursor++] = i + side; triangles[cursor++] = i + side + 1;
                }
            var boundary = new int[perimeter]; int b = 0;
            for (int x = 0; x < Segments; ++x) boundary[b++] = x;
            for (int z = 0; z < Segments; ++z) boundary[b++] = z * side + Segments;
            for (int x = Segments; x > 0; --x) boundary[b++] = Segments * side + x;
            for (int z = Segments; z > 0; --z) boundary[b++] = z * side;
            for (int i = 0; i < perimeter; ++i)
            {
                vertices[gridCount + i] = vertices[boundary[i]] * (OuterHalfExtent / NearHalfExtent);
                int next = (i + 1) % perimeter;
                triangles[cursor++] = boundary[i]; triangles[cursor++] = boundary[next]; triangles[cursor++] = gridCount + i;
                triangles[cursor++] = boundary[next]; triangles[cursor++] = gridCount + next; triangles[cursor++] = gridCount + i;
            }
            for (int i = 0; i < vertices.Length; ++i) { normals[i] = Vector3.up; uv[i] = new Vector2(vertices[i].x, vertices[i].z); }
            var mesh = new Mesh { name = "MarineOceanSurface", vertices = vertices, normals = normals, uv = uv, triangles = triangles };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(OuterHalfExtent * 2, 1, OuterHalfExtent * 2));
            return mesh;
        }
        private static void Validate(GameObject root, Mesh mesh, MarineOceanPresenter presenter)
        {
            Require(root.GetComponentsInChildren<Camera>(true).Length == 0 && root.GetComponentsInChildren<AudioListener>(true).Length == 0, "Unexpected camera/listener.");
            Require(root.GetComponentsInChildren<Collider>(true).Length == 0 && root.GetComponentsInChildren<Rigidbody>(true).Length == 0, "Water must not own physics.");
            Require(!presenter.IsClockBound && presenter.ContactCount == 4, "Prefab contact/configuration mismatch.");
            Require(mesh.vertexCount == 9793 && mesh.triangles.Length / 3 == 19200, "Unexpected mesh budget.");
            var v = mesh.vertices; var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
                Require(Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).y > 0, "Back-facing/degenerate water triangle.");
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Missing script.");
        }
        private static Mesh PersistMesh(Mesh generated, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!existing) { AssetDatabase.CreateAsset(generated, path); return generated; }
            EditorUtility.CopySerialized(generated, existing); UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing); return existing;
        }
        private static Material MaterialAsset(string name, Shader shader)
        {
            string path = AssetRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
            else material.shader = shader;
            EditorUtility.SetDirty(material); return material;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Marine ocean: " + message); }
    }
}
