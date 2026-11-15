using System;
using System.Linq;
using OutOfReach.Presentation.Cinematics.Recovery;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Integrator-invoked additive scene authoring. Existing meshes/materials stay read-only.
    /// This does not save a scene, run a builder, generate a camera/light or bind a domain session.</summary>
    public static class I06RecoveryAuthoring
    {
        const string Model = "Assets/_OutOfReach/Visuals/Models/MarinePrologue/Transport/Models/TransportLaunch.fbx";
        const string Materials = "Assets/_OutOfReach/Visuals/Models/MarinePrologue/Transport/Materials/";
        const string PlanetShader = "Assets/_OutOfReach/Scripts/Presentation/Cinematics/Recovery/RecoveryMars.shader";
        const string MarsAssets = "Assets/_OutOfReach/Visuals/Celestial/RecoveryMars";
        const string MarsMap = MarsAssets + "/Mars_NASA_Viking.jpg";

        public static RecoveryShotProvider Create(Transform presentationParent, Transform actualAuroraRoot)
        {
            if (!presentationParent || !actualAuroraRoot || EditorUtility.IsPersistent(presentationParent) || EditorUtility.IsPersistent(actualAuroraRoot)
                || (presentationParent.lossyScale - Vector3.one).sqrMagnitude > .000001f || (actualAuroraRoot.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                throw new ArgumentException("Supply the actual metre-scale scene presentation and Aurora roots.");
            if (!actualAuroraRoot.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name == "A7_Exterior"))
                throw new InvalidOperationException("The first shot requires the actual delivered Aurora exterior in this scene.");
            if (presentationParent.GetComponentInChildren<RecoveryShotProvider>(true))
                throw new InvalidOperationException("A recovery provider already exists under this presentation owner.");
            GameObject asset = Required<GameObject>(Model); Shader shader = Required<Shader>(PlanetShader);
            Texture2D marsMap = PrepareMarsMap(); Mesh marsMesh = PrepareMarsSphere();
            Transform owner = Child("Recovery cinematic provider", presentationParent);
            Transform arrival = null;
            try
            {
                var provider = owner.gameObject.AddComponent<RecoveryShotProvider>();
                // Owned markers live in actual ship coordinates. The existing hull is never copied or moved.
                arrival = Child("RecoveryArrival - owned visual group", actualAuroraRoot);
                Transform courier = Courier("RecoveryArrival_Courier", arrival, asset);
                Transform courierStart = Marker("RecoveryArrival_CourierStart", arrival, new Vector3(15, 5, 36), Quaternion.Euler(-90, 0, 12));
                Transform courierEnd = Marker("RecoveryArrival_CourierEnd", arrival, new Vector3(8, 3, 30), Quaternion.Euler(-90, 0, 4));
                Transform cameraStart = View("RecoveryArrival_CameraStart", arrival, new Vector3(30, 15, -24), new Vector3(2, 2, 11));
                Transform cameraEnd = View("RecoveryArrival_CameraEnd", arrival, new Vector3(28, 13, -16), new Vector3(2, 2, 11));
                courier.SetPositionAndRotation(courierStart.position, courierStart.rotation);

                // Deliberately isolated illustration set; its distances are framing units, not astronomical scale.
                Transform mars = Child("MarsDeparture - later illustrative tableau", owner);
                mars.SetPositionAndRotation(actualAuroraRoot.TransformPoint(new Vector3(2200, 0, 0)), actualAuroraRoot.rotation);
                Transform marsCourier = Courier("MarsDeparture_Courier", mars, asset);
                Transform marsStart = Marker("MarsDeparture_CourierStart", mars, new Vector3(-10, -2, 18), Quaternion.Euler(-20, 145, -15));
                Transform marsEnd = Marker("MarsDeparture_CourierEnd", mars, new Vector3(-8, -2, 25), Quaternion.Euler(-18, 152, -10));
                Transform marsCameraStart = View("MarsDeparture_CameraStart", mars, new Vector3(-16, 5, -8), new Vector3(4, 0, 160));
                Transform marsCameraEnd = View("MarsDeparture_CameraEnd", mars, new Vector3(-14, 5, -7), new Vector3(4, 0, 160));
                marsCourier.SetPositionAndRotation(marsStart.position, marsStart.rotation);
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "MarsCentre - NASA Viking map, cinematic framing"; sphere.transform.SetParent(mars, false);
                sphere.transform.localPosition = new Vector3(26, 0, 330); sphere.transform.localScale = Vector3.one * 230f;
                Object.DestroyImmediate(sphere.GetComponent<Collider>());
                sphere.GetComponent<MeshFilter>().sharedMesh = marsMesh;
                Renderer surface = sphere.GetComponent<Renderer>();
                surface.sharedMaterial = Required<Material>(Materials + "TL_Exterior.mat");
                surface.shadowCastingMode = ShadowCastingMode.Off; surface.receiveShadows = false;
                surface.lightProbeUsage = LightProbeUsage.Off; surface.reflectionProbeUsage = ReflectionProbeUsage.Off;
                // The source material is retained until the provider creates its owned transient material at runtime.
                var fields = new SerializedObject(provider);
                Set(fields, "arrivalGroup", arrival.gameObject); Set(fields, "marsGroup", mars.gameObject);
                Set(fields, "arrivalCraft", courier); Set(fields, "arrivalCraftStart", courierStart); Set(fields, "arrivalCraftEnd", courierEnd);
                Set(fields, "arrivalCameraStart", cameraStart); Set(fields, "arrivalCameraEnd", cameraEnd);
                Set(fields, "marsCraft", marsCourier); Set(fields, "marsCraftStart", marsStart); Set(fields, "marsCraftEnd", marsEnd);
                Set(fields, "marsCameraStart", marsCameraStart); Set(fields, "marsCameraEnd", marsCameraEnd);
                Set(fields, "marsSurface", surface); Set(fields, "marsShader", shader); Set(fields, "marsColorMap", marsMap); fields.ApplyModifiedPropertiesWithoutUndo();
                arrival.gameObject.SetActive(false); mars.gameObject.SetActive(false);
                return provider;
            }
            catch
            {
                if (arrival) Object.DestroyImmediate(arrival.gameObject);
                Object.DestroyImmediate(owner.gameObject); throw;
            }
        }

        static Texture2D PrepareMarsMap()
        {
            var importer = AssetImporter.GetAtPath(MarsMap) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Import the pinned NASA/JPL Viking image and provenance first.");
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(MarsMap))).Replace("-", "").ToLowerInvariant();
                if (hash != "12ec6bf02ebd42a246edc778cb2ce8c595b4d9f0892badf0bfff8abb2303c780")
                    throw new InvalidOperationException("Mars image bytes differ from the verified official release.");
            }
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.mipmapEnabled = true; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            importer.wrapModeU = TextureWrapMode.Repeat; importer.wrapModeV = TextureWrapMode.Clamp;
            importer.alphaSource = TextureImporterAlphaSource.None; importer.isReadable = false;
            importer.SaveAndReimport();
            Texture2D texture = Required<Texture2D>(MarsMap);
            if (texture.width != 1440 || texture.height != 720 || texture.mipmapCount < 2)
                throw new InvalidOperationException("Preserve native 1440x720 Mars source with mipmaps; do not upscale.");
            return texture;
        }

        static Mesh PrepareMarsSphere()
        {
            const int longitude = 128, latitude = 64;
            var vertices = new Vector3[(longitude + 1) * (latitude + 1)];
            var normals = new Vector3[vertices.Length]; var uv = new Vector2[vertices.Length];
            var triangles = new int[longitude * (latitude - 1) * 6]; int write = 0;
            for (int row = 0; row <= latitude; row++) for (int col = 0; col <= longitude; col++)
            {
                float v = row / (float)latitude, u = col / (float)longitude;
                double phi = (v - .5) * Math.PI, theta = (u - .5) * Math.PI * 2;
                int i = row * (longitude + 1) + col;
                // Native equirectangular source north at V=1; U increases toward +Z from +X.
                Vector3 n = row == 0 ? Vector3.down : row == latitude ? Vector3.up
                    : new Vector3((float)(Math.Cos(phi) * Math.Cos(theta)), (float)Math.Sin(phi), (float)(Math.Cos(phi) * Math.Sin(theta)));
                vertices[i] = n * .5f; normals[i] = n; uv[i] = new Vector2(u, v);
                if (row == latitude || col == longitude) continue;
                int a = i, b = i + longitude + 1, c = i + 1, d = b + 1;
                if (row != 0) { triangles[write++] = a; triangles[write++] = b; triangles[write++] = c; }
                if (row != latitude - 1) { triangles[write++] = c; triangles[write++] = b; triangles[write++] = d; }
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) <= 0)
                    throw new InvalidOperationException("Reversed or degenerate Mars sphere triangle.");
            }
            var generated = new Mesh { name = "Original 128x64 mathematical sphere - no elevation exaggeration" };
            generated.vertices = vertices; generated.normals = normals; generated.uv = uv; generated.triangles = triangles; generated.RecalculateBounds();
            string path = MarsAssets + "/MarsSphere.asset"; Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!existing) { AssetDatabase.CreateAsset(generated, path); existing = generated; }
            else { EditorUtility.CopySerialized(generated, existing); Object.DestroyImmediate(generated); EditorUtility.SetDirty(existing); }
            AssetDatabase.SaveAssetIfDirty(existing); return existing;
        }

        static Transform Courier(string name, Transform parent, GameObject asset)
        {
            Transform frame = Child(name, parent);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset, frame);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // Preserve actual importer axis/bind transforms; only three exact existing authored meshes survive.
            string[] allowed = { "CapsuleShell", "CapsuleInterior", "Hatch" };
            var selected = model.GetComponentsInChildren<MeshRenderer>(true).Where(r => allowed.Contains(r.name)).ToArray();
            if (selected.Length != allowed.Length || allowed.Any(n => selected.Count(r => r.name == n) != 1))
                throw new InvalidOperationException("Expected exactly the three reviewed recovery-courier meshes.");
            var keep = new System.Collections.Generic.HashSet<Transform> { model.transform };
            foreach (var renderer in selected)
                for (Transform t = renderer.transform; t; t = t.parent) { keep.Add(t); if (t == model.transform) break; }
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true).Reverse())
                if (child && !keep.Contains(child)) Object.DestroyImmediate(child.gameObject);
            foreach (var renderer in selected)
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Required<Material>(Materials + m.name + ".mat")).ToArray();
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }
            if (model.GetComponentsInChildren<Collider>(true).Length != 0 || model.GetComponentsInChildren<MonoBehaviour>(true).Length != 0
                || model.GetComponentsInChildren<Camera>(true).Length != 0 || model.GetComponentsInChildren<Light>(true).Length != 0)
                throw new InvalidOperationException("Recovery courier must contain visual meshes only.");
            return frame;
        }
        static Transform Child(string name, Transform parent) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
        static Transform Marker(string name, Transform parent, Vector3 position, Quaternion rotation)
        { Transform t = Child(name, parent); t.SetLocalPositionAndRotation(position, rotation); return t; }
        static Transform View(string name, Transform parent, Vector3 position, Vector3 lookAt)
            => Marker(name, parent, position, Quaternion.LookRotation(lookAt - position, Vector3.up));
        static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing reviewed recovery source: " + path);
        static void Set(SerializedObject fields, string name, Object value) => fields.FindProperty(name).objectReferenceValue = value;
    }
}
