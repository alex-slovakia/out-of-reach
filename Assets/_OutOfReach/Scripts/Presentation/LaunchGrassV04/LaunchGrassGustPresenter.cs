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
    /// - A camera-centred haze dome adds a warm, sun-ward horizon band (the grass applies matching aerial haze).
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
        [SerializeField, Range(0f, 1f)] private float idleHaze = .55f;
        [SerializeField, Range(.1f, 5f)] private float blendSeconds = 1.6f;
        [SerializeField, Range(100f, 6000f)] private float hazeRadius = 2200f;
        [Header("Editor preview (Edit mode only; < 0 = off)")]
        [SerializeField] private float previewSecondsAfterIgnition = -1f;

        private static readonly int GustId = Shader.PropertyToID("_OOR_GrassGust");
        private static readonly int GustWaveId = Shader.PropertyToID("_OOR_GrassGustWave");
        private static readonly int HazeId = Shader.PropertyToID("_OOR_LaunchHaze");
        private Mesh dome;
        private float volumeWeight;
        private bool reducedApplied, reducedKnown;
        private FilmGrain grain; private Bloom bloom; private float bloomIntensity;

        public float GustStrength { get; private set; }
        public float GustFrontRadius { get; private set; }
        public float VolumeWeight => volumeWeight;
        public float Haze { get; private set; }

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
            Shader.SetGlobalFloat(HazeId, 0f);
            if (launchVolume) launchVolume.weight = 0f;
            if (dome) { if (Application.isPlaying) Destroy(dome); else DestroyImmediate(dome); dome = null; }
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
            Shader.SetGlobalFloat(HazeId, haze);
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

        private void DrawHaze(ScriptableRenderContext context, Camera camera)
        {
            if (!hazeMaterial || Haze <= .001f || !isActiveAndEnabled) return;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            if (field && !field.isActiveAndEnabled) return;            // site switched off: no launch haze
            if (!dome) dome = PampasGrassMeshes.BuildDome();
            float r = Mathf.Min(hazeRadius, camera.farClipPlane * .85f);
            Vector3 eye = camera.transform.position;
            var rp = new RenderParams(hazeMaterial)
            {
                camera = camera, layer = gameObject.layer, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off, reflectionProbeUsage = ReflectionProbeUsage.Off,
                worldBounds = new Bounds(eye, Vector3.one * r * 2.2f),
            };
            Graphics.RenderMesh(rp, dome, 0, Matrix4x4.TRS(eye, Quaternion.identity, Vector3.one * r));
        }

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    }
}
