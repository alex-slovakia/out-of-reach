using OutOfReach.Presentation.Cinematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OutOfReach.Presentation.LaunchGrassV04
{
    /// <summary>
    /// LAUNCH-GRASS-V04 launch look. Passive: samples the actual <see cref="TransportDeparturePresenter"/> playhead
    /// (read-only: IsPlaying, IsSuspended, PresentedSeconds, ReducedMotion) and writes presentation globals only.
    /// - At ignition (TransportDepartureTimeline.IgnitionSeconds) a radial shock front leaves the pad and flattens
    ///   the pampas field outwards, followed by a sustained push that fades as the vehicle climbs away.
    /// - The launch Volume (warm grade, bloom, light grain) blends in for the departure's exterior launch shots and
    ///   out before the stationary site is switched off at UpperAirSeconds.
    /// - Launch haze: when the camera has a depth texture, one pass of distance fog with a height falloff over
    ///   every opaque surface (field, far land, mountains, low sky); otherwise a far horizon dome and the grass's
    ///   own aerial haze. Drawn only while the launch site (the field) is active.
    /// Pause holds every value. Reduced effects (the departure's ReducedMotion): half gust, no shiver, no grain,
    /// lighter bloom, thinner and shorter grass. No clock, audio, gameplay or simulation authority.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class LaunchGrassGustPresenter : MonoBehaviour
    {
        [SerializeField] private TransportDeparturePresenter departure;
        [SerializeField, Tooltip("Gust origin: the pad / flame deflector (world position).")]
        private Transform gustOrigin;
        [SerializeField] private PampasGrassField field;
        [SerializeField] private Volume launchVolume;
        [SerializeField] private Material hazeMaterial;
        [Header("Thruster gust")]
        [SerializeField, Range(5f, 120f)] private float frontSpeed = 46f;
        [SerializeField, Range(20f, 500f)] private float maxFrontRadius = 300f;
        [SerializeField, Range(2f, 60f)] private float frontWidth = 16f;
        [SerializeField, Range(10f, 300f)] private float sustainedReach = 230f;
        [SerializeField, Range(0f, 1f)] private float flutter = .8f;
        [SerializeField, Range(0f, 1f)] private float strength = 1f;
        [Header("Look weights")]
        [SerializeField, Range(0f, 1f), Tooltip("Volume weight outside the departure (walking the site).")]
        private float idleVolumeWeight = 0f;
        [SerializeField, Range(0f, 1f), Tooltip("Haze outside the departure (walking the site).")]
        private float idleHaze = .85f;
        [SerializeField, Range(.1f, 5f)] private float blendSeconds = 1.6f;
        [Header("Haze")]
        [SerializeField, Tooltip("Distance fog from the camera depth texture when the camera provides one.")]
        private bool depthFog = true;
        [SerializeField, Range(100f, 6000f), Tooltip("Radius of the horizon dome used without a depth texture.")]
        private float fallbackDomeRadius = 2200f;
        [Header("Editor preview (Edit mode only; < 0 = off)")]
        [SerializeField] private float previewSecondsAfterIgnition = -1f;

        private static readonly int GustId = Shader.PropertyToID("_OOR_GrassGust");
        private static readonly int GustWaveId = Shader.PropertyToID("_OOR_GrassGustWave");
        private static readonly int GrassHazeId = Shader.PropertyToID("_OOR_LaunchHaze");
        private static readonly int FogId = Shader.PropertyToID("_OOR_LaunchFog");
        private static readonly int FogGroundId = Shader.PropertyToID("_OOR_LaunchFogGround");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");
        private Mesh dome, sphere;
        private Material depthFogMaterial;
        private float volumeWeight;
        private bool reducedApplied, reducedKnown;
        private FilmGrain grain; private Bloom bloom; private float bloomIntensity;

        public float GustStrength { get; private set; }
        public float GustFrontRadius { get; private set; }
        public float VolumeWeight => volumeWeight;
        public float Haze { get; private set; }
        /// <summary>True when the last rendered game/scene camera used the depth fog pass (diagnostics).</summary>
        public bool LastUsedDepthFog { get; private set; }
        public bool DepthFog { get => depthFog; set => depthFog = value; }

        public void Configure(TransportDeparturePresenter sequence, Transform origin, PampasGrassField grass, Volume volume, Material haze)
        {
            departure = sequence; gustOrigin = origin; field = grass; launchVolume = volume; hazeMaterial = haze;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += DrawHaze;
            volumeWeight = idleVolumeWeight;
            Write(0f, 0f, false, idleHaze);
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= DrawHaze;
            Shader.SetGlobalVector(GustId, Vector4.zero);
            Shader.SetGlobalFloat(GrassHazeId, 0f);
            Shader.SetGlobalFloat(FogId, 0f);
            if (launchVolume) launchVolume.weight = 0f;
            Release(ref dome); Release(ref sphere);
            if (depthFogMaterial) { if (Application.isPlaying) Destroy(depthFogMaterial); else DestroyImmediate(depthFogMaterial); depthFogMaterial = null; }
        }

        private static void Release(ref Mesh mesh)
        {
            if (!mesh) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                if (previewSecondsAfterIgnition >= 0f) PreviewAt(previewSecondsAfterIgnition, false);
                else { volumeWeight = idleVolumeWeight; Write(0f, 0f, false, idleHaze); }
                return;
            }
            bool playing = departure && departure.IsPlaying;
            bool reduced = departure && departure.ReducedMotion;
            if (field) field.SetReducedEffects(reduced);
            ApplyReducedToVolume(reduced);
            if (playing && departure.IsSuspended) return;            // pause holds the presented frame
            float gust = 0f, radius = 0f, target = idleVolumeWeight;
            if (playing) Sample(departure.PresentedSeconds, reduced, out gust, out radius, out target);
            volumeWeight = Mathf.MoveTowards(volumeWeight, target, Time.unscaledDeltaTime / blendSeconds);
            if (playing && departure.PresentedSeconds >= TransportDepartureTimeline.UpperAirSeconds) volumeWeight = 0f;
            Write(gust, radius, reduced, Mathf.Lerp(idleHaze, 1f, volumeWeight));
        }

        /// <summary>Edit-mode preview / evidence capture: presents the look as at `secondsAfterIgnition` on the
        /// departure playhead (negative = before ignition). Not used by the Play-mode path.</summary>
        public void PreviewAt(float secondsAfterIgnition, bool reduced)
        {
            Sample(TransportDepartureTimeline.IgnitionSeconds + secondsAfterIgnition, reduced, out float g, out float r, out float w);
            volumeWeight = w;
            if (field) field.SetReducedEffects(reduced);
            Write(g, r, reduced, Mathf.Lerp(idleHaze, 1f, w));
        }

        /// <summary>Edit-mode preview / evidence capture: the look outside a departure (walking the site).</summary>
        public void PreviewIdle()
        {
            volumeWeight = idleVolumeWeight;
            if (field) field.SetReducedEffects(false);
            Write(0f, 0f, false, idleHaze);
        }

        /// <summary>Deterministic envelope for a departure time (seconds on the transport playhead).</summary>
        private void Sample(float seconds, bool reduced, out float gust, out float radius, out float lookWeight)
        {
            float age = seconds - TransportDepartureTimeline.IgnitionSeconds;
            float climb = seconds - TransportDepartureTimeline.LiftoffSeconds;
            gust = age <= 0f ? 0f : Smooth(age / .35f) * (1f - Smooth((climb - 3.5f) / 4f)) * strength * (reduced ? .5f : 1f);
            radius = age <= 0f ? 0f : maxFrontRadius * (1f - Mathf.Exp(-age * frontSpeed / maxFrontRadius));
            // Full grade through boarding, ignition and the pad/climb shots; out before the site is switched off.
            lookWeight = 1f - Smooth((seconds - (TransportDepartureTimeline.UpperAirSeconds - 2.2f)) / 2f);
        }

        private void Write(float gust, float radius, bool reduced, float haze)
        {
            GustStrength = gust; GustFrontRadius = radius; Haze = haze;
            Vector3 o = gustOrigin ? gustOrigin.position : transform.position;
            Shader.SetGlobalVector(GustId, new Vector4(o.x, o.y, o.z, gust));
            Shader.SetGlobalVector(GustWaveId, new Vector4(radius, frontWidth, sustainedReach, reduced ? 0f : flutter));
            if (launchVolume) launchVolume.weight = volumeWeight;
        }

        private void ApplyReducedToVolume(bool reduced)
        {
            if (!launchVolume || (reducedKnown && reduced == reducedApplied)) return;
            reducedKnown = true; reducedApplied = reduced;
            VolumeProfile profile = launchVolume.profile; // runtime instance (Play mode only), never the asset
            if (!grain) profile.TryGet(out grain);
            if (!bloom && profile.TryGet(out bloom)) bloomIntensity = bloom.intensity.value;
            if (grain) grain.active = !reduced;
            if (bloom) bloom.intensity.value = bloomIntensity * (reduced ? .55f : 1f);
        }

        private static bool HasDepthTexture(Camera camera)
        {
            if (camera.TryGetComponent(out UniversalAdditionalCameraData data)) return data.requiresDepthTexture;
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            return asset && asset.supportsCameraDepthTexture;
        }

        private void DrawHaze(ScriptableRenderContext context, Camera camera)
        {
            if (!isActiveAndEnabled) return;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            bool siteActive = !field || field.isActiveAndEnabled;      // site switched off: no launch haze
            bool useDepth = depthFog && siteActive && hazeMaterial && HasDepthTexture(camera);
            LastUsedDepthFog = useDepth;
            // With the depth pass every surface is fogged alike, so the grass must not add its own haze on top.
            Shader.SetGlobalFloat(GrassHazeId, useDepth || !siteActive ? 0f : Haze);
            Shader.SetGlobalFloat(FogId, siteActive ? Haze : 0f);
            float datum = field && field.IsReady ? field.MeanGroundY : gustOrigin ? gustOrigin.position.y : transform.position.y;
            Shader.SetGlobalVector(FogGroundId, new Vector4(datum, 0f, 0f, 0f));
            if (!hazeMaterial || !siteActive || Haze <= .001f) return;
            Vector3 eye = camera.transform.position;
            Material material; Mesh mesh; float radius;
            if (useDepth)
            {
                if (!depthFogMaterial)
                {
                    depthFogMaterial = new Material(hazeMaterial) { name = "Launch haze (depth fog)", hideFlags = HideFlags.DontSave };
                    depthFogMaterial.EnableKeyword("_DEPTH_FOG");
                    depthFogMaterial.SetFloat(ZTestId, (float)CompareFunction.Always);
                }
                if (!sphere) sphere = PampasGrassMeshes.BuildSphere();
                material = depthFogMaterial; mesh = sphere;
                radius = Mathf.Clamp(camera.nearClipPlane * 40f, 2f, camera.farClipPlane * .5f);
            }
            else
            {
                if (!dome) dome = PampasGrassMeshes.BuildDome();
                material = hazeMaterial; mesh = dome;
                radius = Mathf.Min(fallbackDomeRadius, camera.farClipPlane * .85f);
            }
            var rp = new RenderParams(material)
            {
                camera = camera, layer = gameObject.layer, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off, reflectionProbeUsage = ReflectionProbeUsage.Off,
                worldBounds = new Bounds(eye, Vector3.one * radius * 2.2f),
            };
            Graphics.RenderMesh(rp, mesh, 0, Matrix4x4.TRS(eye, Quaternion.identity, Vector3.one * radius));
        }

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    }
}
