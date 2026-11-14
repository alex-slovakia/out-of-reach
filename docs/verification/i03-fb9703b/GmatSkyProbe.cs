#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Verification.Editor
{
    // Temporary Editor probe only. No gameplay camera, saved scene or project setting is changed.
    public static class GmatSkyProbe
    {
        private const string AssetRoot = "Assets/_OutOfReach/Visuals/Space/";
        private const int Size = 512;
        private static readonly string[] Faces = { "Front", "Back", "Left", "Right", "Up", "Down" };
        private static readonly Vector3[] Directions = {
            Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down
        };
        private static readonly string[] Guids = {
            "288d1d0a6e1223242ad13f29a766469f", "9188c4fd2c3afb64e92e34ffb3b5861b",
            "6df8e368146db5943b61cdad402bbd6f", "a4f5652dc64847641944fa5de5d6647e",
            "0a05c8a0e9bf2f04dba95ad16fbd2bdc", "3ed15f160363dd243941e818645d7745"
        };
        private static readonly string[] TextureHashes = {
            "444ebec0669a735569b5365b4ba0aaff04c87c5926e502fd6d2cbcf018769a3c",
            "217d9dfcc4f5b1a3d97dc22536a2e3f787d83abec4cee9da4e395815e8ba5045",
            "37a37abb3cfb6dbcac5d33b0d14768c65c12a13686e282ad846493c7e4294ad2",
            "b320f070342026f1dbf52a9947ab149c85043e7e8e968390ba024b0c9499c81f",
            "db65a505b927cf0d96e7b530c5cf4ab51c72509a32eb7b8454ee7192a04c641c",
            "e8f4e23bb5bf3c6703c62db3a2db35385323ea1ca1a1f9d590ef4842cb115490"
        };

        [Serializable]
        public sealed class FaceResult
        {
            public string face, property, guid, sourcePngSha256, rgbSha256;
            public int litPixels, maxChannel, chromaticPixels;
        }

        [Serializable]
        public sealed class Report
        {
            public string result = "FAIL", executedAtUtc, unityVersion, pipeline, graphicsDevice;
            public string error, capture, capturePngSha256, materialSha256;
            public string renderApi = "Camera.Render(), matching I01BootstrapBuilder.CaptureIntegrated";
            public int width = Size, height = Size;
            public float fovDegrees = 90, materialExposure, materialRotation;
            public Vector3 firstPosition = Vector3.zero;
            public Vector3 secondPosition = new Vector3(37.25f, -12.5f, 91.75f);
            public Vector3 comparisonLookDirection = Vector3.right;
            public int changedPixelsAfterTranslation = -1, maxChannelDifferenceAfterTranslation = -1;
            public int changedPixelsRepeatFirstPosition = -1;
            public bool previousActiveSceneRestored, temporarySceneClosed;
            public FaceResult[] faces = new FaceResult[6];
            public string limits = "Temporary empty Editor scene; actual GPU sky rendering and fixed-orientation translation only. " +
                "No gameplay camera, walking/playthrough, precise-star astrometry, common celestial attitude, planetary geometry, " +
                "frame/epoch accuracy, or post-processing acceptance is established. No scene/settings/assets saved.";
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Sky probe requires Edit Mode in the isolated verification project.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!project.EndsWith(Path.Combine(".local", "i01-integration"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This temporary probe is scoped to .local/i01-integration.");
            string output = Path.GetFullPath(Path.Combine(project, "../verification/GmatSkyboxV01/UnityProbe/Output"));
            Directory.CreateDirectory(output);
            var report = new Report {
                executedAtUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName,
                pipeline = GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.GetType().FullName : "Built-in"
            };
            // A fresh batch editor starts on an untitled scene; Unity rejects additive creation there.
            // Only the isolated batch process may replace that startup scene with the saved Bootstrap.
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/_OutOfReach/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            Scene originalScene = SceneManager.GetActiveScene();
            Scene temporaryScene = default;
            GameObject probeObject = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            RenderTexture oldActive = RenderTexture.active;
            bool oldAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            Exception failure = null;
            try
            {
                Require(Application.unityVersion == "6000.3.28f1", "Expected Unity 6000.3.28f1.");
                Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "A graphics device is required; omit -nographics.");
                Require(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset, "Expected the installed URP asset.");
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "DistantStars.mat");
                Require(material && material.shader && material.shader.name == "Skybox/6 Sided", "Existing sky material/shader missing.");
                report.materialSha256 = Hash(File.ReadAllBytes(Path.Combine(project, AssetRoot + "DistantStars.mat")));
                report.materialExposure = material.GetFloat("_Exposure");
                report.materialRotation = material.GetFloat("_Rotation");
                for (int i = 0; i < Faces.Length; i++)
                {
                    string path = AssetRoot + "Stars" + Faces[i] + ".png";
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    string hash = Hash(File.ReadAllBytes(Path.Combine(project, path)));
                    Require(texture && texture.width == 1024 && texture.height == 1024, "Missing/incorrect face: " + Faces[i]);
                    Require(guid == Guids[i] && hash == TextureHashes[i], "Face GUID/source bytes changed: " + Faces[i]);
                    Require(material.GetTexture("_" + Faces[i] + "Tex") == texture, "Material binding mismatch: " + Faces[i]);
                    report.faces[i] = new FaceResult { face = Faces[i], property = "_" + Faces[i] + "Tex", guid = guid, sourcePngSha256 = hash };
                }

                temporaryScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(temporaryScene);
                probeObject = new GameObject("Temporary GMAT sky probe") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(probeObject, temporaryScene);
                var camera = probeObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 0;
                camera.fieldOfView = 90;
                camera.aspect = 1;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.allowDynamicResolution = false;
                camera.useOcclusionCulling = false;
                probeObject.AddComponent<Skybox>().material = material;
                var additional = probeObject.AddComponent<UniversalAdditionalCameraData>();
                additional.renderPostProcessing = false;
                additional.antialiasing = AntialiasingMode.None;
                additional.volumeLayerMask = 0;
                additional.requiresColorTexture = false;
                additional.requiresDepthTexture = false;
                target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.antiAliasing = 1;
                Require(target.Create(), "Cannot create sky probe target.");
                pixels = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                camera.targetTexture = target;
                ShaderUtil.allowAsyncCompilation = false;
                byte[] first = null;
                for (int i = 0; i < Faces.Length; i++)
                {
                    Vector3 up = i == 4 ? Vector3.back : i == 5 ? Vector3.forward : Vector3.up;
                    camera.transform.SetPositionAndRotation(report.firstPosition, Quaternion.LookRotation(Directions[i], up));
                    byte[] rgb = Capture(camera, target, pixels);
                    FillMetrics(report.faces[i], rgb);
                    for (int previous = 0; previous < i; previous++)
                        Require(report.faces[i].rgbSha256 != report.faces[previous].rgbSha256,
                            "Two cardinal views are identical; verify that a fresh directional frame rendered.");
                    Require(report.faces[i].litPixels > 10 && report.faces[i].litPixels < Size*Size/10
                        && report.faces[i].maxChannel >= 16 && report.faces[i].chromaticPixels == 0,
                        "Expected sparse neutral stars on black; render failed for " + Faces[i]);
                    if (i == 2)
                    {
                        first = rgb;
                        byte[] png = pixels.EncodeToPNG();
                        report.capture = Path.Combine(output, "actual-sky-positive-x.png");
                        File.WriteAllBytes(report.capture, png);
                        report.capturePngSha256 = Hash(png);
                    }
                }
                Quaternion sameRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
                camera.transform.SetPositionAndRotation(report.secondPosition, sameRotation);
                byte[] second = Capture(camera, target, pixels);
                report.changedPixelsAfterTranslation = ChangedPixels(first, second, out report.maxChannelDifferenceAfterTranslation);
                camera.transform.SetPositionAndRotation(report.firstPosition, sameRotation);
                byte[] repeated = Capture(camera, target, pixels);
                report.changedPixelsRepeatFirstPosition = ChangedPixels(first, repeated, out _);
                Require(report.changedPixelsAfterTranslation == 0 && report.changedPixelsRepeatFirstPosition == 0,
                    "Fixed-orientation sky pixels differ after translation or on the repeated control frame.");
                report.result = "PASS";
            }
            catch (Exception error)
            {
                failure = error;
                report.error = error.ToString();
            }
            finally
            {
                RenderTexture.active = oldActive;
                ShaderUtil.allowAsyncCompilation = oldAsyncCompilation;
                if (probeObject) Object.DestroyImmediate(probeObject);
                if (target) { target.Release(); Object.DestroyImmediate(target); }
                if (pixels) Object.DestroyImmediate(pixels);
                if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
                if (temporaryScene.IsValid() && temporaryScene.isLoaded) EditorSceneManager.CloseScene(temporaryScene, true);
                report.previousActiveSceneRestored = SceneManager.GetActiveScene() == originalScene;
                report.temporarySceneClosed = !temporaryScene.IsValid() || !temporaryScene.isLoaded;
                if (!report.previousActiveSceneRestored || !report.temporarySceneClosed)
                {
                    report.result = "FAIL";
                    if (failure == null) failure = new InvalidOperationException("Temporary scene cleanup did not restore the previous scene.");
                    report.error = failure.ToString();
                }
                File.WriteAllText(Path.Combine(output, "sky-probe-report.json"), JsonUtility.ToJson(report, true) + "\n");
            }
            if (failure != null) throw new InvalidOperationException("GMAT_SKY_PROBE_FAIL; see " + output, failure);
            Debug.Log("GMAT_SKY_PROBE_PASS | six source/binding checks; six nonblack sky views; exact fixed-orientation translation invariance | " + output);
        }

        private static byte[] Capture(Camera camera, RenderTexture target, Texture2D pixels)
        {
            for (int warmup = 0; warmup < 3; warmup++) camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            pixels.Apply();
            Color32[] colors = pixels.GetPixels32();
            byte[] rgb = new byte[colors.Length*3];
            for (int i = 0; i < colors.Length; i++)
            { rgb[i*3] = colors[i].r; rgb[i*3+1] = colors[i].g; rgb[i*3+2] = colors[i].b; }
            return rgb;
        }

        private static void FillMetrics(FaceResult result, byte[] rgb)
        {
            result.rgbSha256 = Hash(rgb);
            for (int i = 0; i < rgb.Length; i += 3)
            {
                int maximum = Math.Max(rgb[i], Math.Max(rgb[i+1], rgb[i+2]));
                if (maximum > 0) result.litPixels++;
                result.maxChannel = Math.Max(result.maxChannel, maximum);
                if (rgb[i] != rgb[i+1] || rgb[i+1] != rgb[i+2]) result.chromaticPixels++;
            }
        }

        private static int ChangedPixels(byte[] a, byte[] b, out int maxDifference)
        {
            Require(a != null && b != null && a.Length == b.Length, "Missing comparison frames.");
            int changed = 0;
            maxDifference = 0;
            for (int i = 0; i < a.Length; i += 3)
            {
                int difference = Math.Max(Math.Abs(a[i]-b[i]), Math.Max(Math.Abs(a[i+1]-b[i+1]), Math.Abs(a[i+2]-b[i+2])));
                if (difference != 0) changed++;
                maxDifference = Math.Max(maxDifference, difference);
            }
            return changed;
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
