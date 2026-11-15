using System;
using System.IO;
using System.Linq;
using System.Text;
using OutOfReach.Composition;
using OutOfReach.Presentation.Cinematics;
using OutOfReach.Presentation.LaunchFXV03;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Presentation.LaunchVfxMax.Editor
{
    /// <summary>Edit-mode evidence: replays the departure playhead through LaunchFxV03 and LaunchVfxMax, moves the
    /// vehicle exactly as TransportDeparturePresenter does, and renders the game's own pad/climb/upper-air shots plus
    /// a fixed wide camera. Nothing is saved: the scene is reopened afterwards.</summary>
    public static class LaunchVfxMaxEvidence
    {
        private const string ScenePath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
        private const string Folder = "Assets/_OutOfReach/Scripts/Presentation/LaunchVfxMax/Evidence~";
        private const int Width = 1600, Height = 900;
        private static readonly float[] Times = { 9.3f, 10f, 11f, 12f, 13f, 14f, 15.5f, 17f, 19f };

        /// <summary>Unity -batchmode -projectPath . -executeMethod OutOfReach.Presentation.LaunchVfxMax.Editor.LaunchVfxMaxEvidence.RenderBatch</summary>
        public static void RenderBatch()
        {
            int code = 0;
            try { Render(); }
            catch (Exception exception) { Debug.LogError("LAUNCH_VFX_MAX_EVIDENCE_FAILED " + exception); code = 1; }
            EditorApplication.Exit(code);
        }

        [MenuItem("Out of Reach/Launch/Render Launch VFX MAX evidence (scene not saved)")]
        public static void Render()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var presenter = LaunchVfxMaxInstaller.Install(false);
            var binding = UnityEngine.Object.FindObjectsByType<I06MarineBindings>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var so = new SerializedObject(binding);
            var vehicle = Ref<Transform>(so, "vehicle");
            var marineRoot = Ref<Transform>(so, "marineRoot");
            var site = Ref<Transform>(so, "stationarySite");
            var nozzle = Ref<Transform>(so, "nozzleFrame");
            var deflector = Ref<Transform>(so, "launchDeflector");
            var pad = Ref<Transform>(so, "launchPad");
            var exit = Ref<Transform>(so, "launchTrenchExit");
            var launchFx = Ref<LaunchFxV03Presenter>(so, "launchFx");
            var sky = Ref<Material>(so, "marineSky");
            var playerCamera = Ref<Camera>(so, "playerCamera");
            var orbitalSet = Ref<GameObject>(so, "orbitalSet");
            var booster = Ref<Transform>(so, "booster");
            if (!vehicle || !marineRoot || !playerCamera) throw new InvalidOperationException("Marine bindings are incomplete.");

            marineRoot.gameObject.SetActive(true);
            if (site) site.gameObject.SetActive(true);
            if (orbitalSet) orbitalSet.SetActive(false);
            if (booster) booster.gameObject.SetActive(true);
            ApplyLaunchSiteEnvironment(sky);

            bool v03 = false;
            if (launchFx)
            {
                try { launchFx.Configure(nozzle, deflector, pad, exit); v03 = launchFx.IsBound; }
                catch (Exception exception) { Debug.LogWarning("Evidence: LaunchFxV03 could not bind in edit mode: " + exception.Message); }
            }

            Vector3 startPosition = vehicle.position;
            Quaternion startRotation = vehicle.rotation;
            var cameraObject = new GameObject("Launch VFX MAX evidence camera") { hideFlags = HideFlags.DontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(playerCamera);
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = 10500f;
            CopyUniversalCameraData(playerCamera, camera);
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            Directory.CreateDirectory(Folder);
            var report = new StringBuilder();
            report.AppendLine($"{LaunchVfxMaxPresenter.Version} evidence, {DateTime.Now:yyyy-MM-dd HH:mm}, Unity {Application.unityVersion}, LaunchFxV03 bound: {v03}");

            try
            {
                foreach (bool reduced in new[] { false, true })
                {
                    presenter.PreviewClear();
                    if (v03) launchFx.ResetEffects();
                    float t = TransportDepartureTimeline.IgnitionSeconds - .5f;
                    foreach (float time in Times)
                    {
                        if (reduced && time != 11f && time != 14f) continue;
                        for (; t < time; t += 1f / 30f) Present(t);
                        Present(time);
                        var frame = TransportDepartureTimeline.Evaluate(time, false);
                        string tag = (reduced ? "reduced-" : "") + "t" + time.ToString("00.0").Replace('.', '_');
                        // The game's own shot for this playhead (the seat shot is replaced by the pad shot before 9.75 s).
                        GameShot(time, frame, startPosition, startRotation, vehicle, out Vector3 position, out Quaternion rotation, out float fov);
                        Shoot(camera, target, readback, position, rotation, fov, $"{Folder}/V02-game-{tag}.jpg");
                        if (!reduced)
                        {
                            Vector3 wide = startPosition + startRotation * new Vector3(62f, -23f, -108f);
                            Vector3 look = startPosition + startRotation * new Vector3(0f, -12f + frame.HeightMetres * .5f, 0f);
                            Shoot(camera, target, readback, wide, Quaternion.LookRotation(look - wide, Vector3.up), 52f, $"{Folder}/V02-wide-{tag}.jpg");
                        }
                        report.AppendLine($"{tag}: live particles {presenter.LiveParticleCount}, height {frame.HeightMetres:0.0} m, cue {frame.Cue}");
                    }

                    void Present(float seconds)
                    {
                        var f = TransportDepartureTimeline.Evaluate(seconds, false);
                        Vector3 displacement = Vector3.up * f.HeightMetres + startRotation * Vector3.forward * (260f * f.AtmosphereBlend * f.AtmosphereBlend);
                        vehicle.SetPositionAndRotation(startPosition + displacement, startRotation * Quaternion.Euler(26f * f.AtmosphereBlend, 0f, 0f));
                        // The game hides the stationary site once the upper-air shot starts.
                        if (site) site.gameObject.SetActive(seconds < TransportDepartureTimeline.UpperAirSeconds);
                        if (v03) launchFx.Present(seconds, f.Cue, true, false, reduced);
                        presenter.PreviewSample(seconds, reduced);
                    }
                }
            }
            finally
            {
                File.WriteAllText($"{Folder}/V02-evidence.txt", report.ToString());
                presenter.PreviewClear();
                if (v03) launchFx.ClearBinding();
                vehicle.SetPositionAndRotation(startPosition, startRotation);
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(readback);
                // Discard every temporary change.
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            Debug.Log("LAUNCH_VFX_MAX_EVIDENCE_DONE " + Folder);
        }

        private static void GameShot(float seconds, TransportDepartureTimeline.Frame frame, Vector3 startPosition, Quaternion startRotation,
            Transform vehicle, out Vector3 position, out Quaternion rotation, out float fov)
        {
            if (seconds < TransportDepartureTimeline.ClimbCutSeconds)
            {
                position = startPosition + startRotation * new Vector3(29f, -27f, -36f);
                rotation = Look(position, startPosition + startRotation * new Vector3(0f, -21f + frame.HeightMetres * .65f, 0f));
                fov = 43f;
            }
            else if (seconds < TransportDepartureTimeline.UpperAirSeconds)
            {
                float tracking = Mathf.InverseLerp(TransportDepartureTimeline.ClimbCutSeconds, TransportDepartureTimeline.UpperAirSeconds, seconds);
                position = vehicle.position + startRotation * Vector3.Lerp(new Vector3(39f, -29f, -26f), new Vector3(49f, -35f, -18f), tracking);
                rotation = Look(position, vehicle.TransformPoint(new Vector3(0f, -15f, 0f)));
                fov = 49f;
            }
            else
            {
                position = vehicle.position + startRotation * new Vector3(-55f, 1f, -32f);
                rotation = Look(position, vehicle.TransformPoint(new Vector3(0f, -12f, 0f)));
                fov = 45f;
            }
        }

        private static Quaternion Look(Vector3 position, Vector3 target) => Quaternion.LookRotation(target - position, Vector3.up);

        private static void Shoot(Camera camera, RenderTexture target, Texture2D readback, Vector3 position, Quaternion rotation, float fov, string path)
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = fov;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, readback.EncodeToJPG(88));
        }

        /// <summary>Same values as I06MarineBindings.ApplyMarineEnvironment (launch-site look).</summary>
        private static void ApplyLaunchSiteEnvironment(Material sky)
        {
            if (sky) RenderSettings.skybox = sky;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.65f, .69f, .73f); RenderSettings.fogDensity = .00013f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.45f, .54f, .63f);
            RenderSettings.ambientEquatorColor = new Color(.34f, .39f, .43f);
            RenderSettings.ambientGroundColor = new Color(.28f, .27f, .23f);
            RenderSettings.ambientIntensity = 1f; RenderSettings.reflectionIntensity = .45f;
            var sun = RenderSettings.sun;
            if (sun) { sun.color = new Color(1f, .87f, .69f); sun.intensity = 2.1f; sun.transform.rotation = Quaternion.Euler(18f, -48f, 0f); }
        }

        /// <summary>Copies the player camera's URP settings (post-processing, renderer, volume mask) without a URP assembly reference.</summary>
        private static void CopyUniversalCameraData(Camera from, Camera to)
        {
            var type = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (type == null) return;
            var source = from.GetComponent(type);
            var destination = to.GetComponent(type);
            if (!destination) destination = to.gameObject.AddComponent(type);
            if (source) EditorUtility.CopySerialized(source, destination);
            else type.GetProperty("renderPostProcessing")?.SetValue(destination, true);
        }

        private static T Ref<T>(SerializedObject source, string name) where T : UnityEngine.Object
        {
            var property = source.FindProperty(name);
            return property == null ? null : property.objectReferenceValue as T;
        }
    }
}
