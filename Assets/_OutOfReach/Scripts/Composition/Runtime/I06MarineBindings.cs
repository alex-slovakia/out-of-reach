using System;
using OutOfReach.Contracts;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Presentation.Cinematics;
using OutOfReach.Presentation.MarineOcean;
using OutOfReach.Presentation.MarineOcean.FX;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Composition
{
    /// <summary>Integrator-owned location, seat target and presentation lease. Never grants cargo or advances mission time.</summary>
    [DefaultExecutionOrder(450)]
    public sealed class I06MarineBindings : MonoBehaviour
    {
        [SerializeField] private Transform marineRoot, vehicle, hatch, arm, seatCamera, seatInteraction;
        [SerializeField] private Transform departureOffset;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InteractableTarget console;
        [SerializeField] private MarineOceanPresenter ocean;
        [SerializeField] private TransportDeparturePresenter departure;
        [SerializeField] private Material marineSky;
        [SerializeField] private CanvasGroup transferFade;
        [SerializeField] private MarineDepartureFxPresenter departureFx;
        [SerializeField] private Transform nozzleFrame, deckPlane;
        [SerializeField] private Material fxExhaust, fxSteam;
        private MissionRuntime runtime;
        private Transform consoleParent;
        private Vector3 consolePosition, consoleScale;
        private Quaternion consoleRotation;
        private Material originalSky;
        private AmbientMode ambientMode;
        private Color ambientLight, ambientSky, ambientEquator, ambientGround;
        private float ambientIntensity, reflectionIntensity, cameraFar, sunIntensity;
        private bool fog, environmentCaptured, bound, lease, finishing, suppressEnd;
        private Light sun;
        private Color sunColor;
        private Quaternion sunRotation;
        private float fadeRemaining;
        public event Action<string> CaptionChanged;
        public event Action<TransportDepartureCue> CueRaised;
        public event Action Transferred;
        public bool OwnsPresentation => lease || finishing;
        public bool IsMarineVisible => marineRoot && marineRoot.gameObject.activeSelf;
        public Transform SeatInteraction => seatInteraction;
        public Transform MarineRoot => marineRoot;

        public void ConfigureLaunchEffects(MarineDepartureFxPresenter effects, Transform nozzle, Transform deck,
            Material exhaust, Material steam)
        {
            if (bound) throw new InvalidOperationException("Configure launch effects before a session.");
            departureFx = effects; nozzleFrame = nozzle; deckPlane = deck; fxExhaust = exhaust; fxSteam = steam;
        }

        public void ConfigureViews(Transform root, Transform transport, Transform hatchFrame, Transform armFrame,
            Transform seatView, Transform seatTarget, Transform offset, Camera camera, InteractableTarget cockpit,
            MarineOceanPresenter water, TransportDeparturePresenter sequence, Material sky, CanvasGroup fade)
        {
            if (bound) throw new InvalidOperationException("Configure marine views before a session.");
            marineRoot = root; vehicle = transport; hatch = hatchFrame; arm = armFrame;
            seatCamera = seatView; seatInteraction = seatTarget; departureOffset = offset;
            playerCamera = camera; console = cockpit; ocean = water; departure = sequence;
            marineSky = sky; transferFade = fade;
        }

        public void Bind(MissionRuntime owner, bool reducedMotion)
        {
            Unbind();
            if (!owner || owner.Session == null || !marineRoot || !departure || !console || !seatInteraction || !marineSky)
                throw new InvalidOperationException("Marine scene requires its actual runtime, seat, camera, sky and departure bindings.");
            runtime = owner; bound = true;
            consoleParent = console.transform.parent; consolePosition = console.transform.localPosition;
            consoleRotation = console.transform.localRotation; consoleScale = console.transform.localScale;
            CaptureEnvironment();
            departure.CaptionChanged += ForwardCaption; departure.CueRaised += ForwardCue; departure.Finished += OnDepartureEnd;
            departure.Configure(owner.GameState, owner.Clock, departureOffset, playerCamera, vehicle, hatch, arm, seatCamera);
            SetReducedMotion(reducedMotion);
            if (ocean) ocean.BindClock(owner.Clock);
            if (departureFx) departureFx.ConfigureViews(nozzleFrame, deckPlane, fxExhaust, fxSteam);
            if (owner.GameState.Phase == GamePhase.Planning)
            {
                marineRoot.gameObject.SetActive(true); ApplyMarineEnvironment();
                console.transform.SetParent(vehicle, true);
                console.transform.SetPositionAndRotation(seatInteraction.position, seatInteraction.rotation);
                console.transform.localScale = Vector3.one;
                Physics.SyncTransforms();
            }
            else marineRoot.gameObject.SetActive(false);
        }

        // Called from the confirmed Planning -> Intro event, before other camera owners restore their pivots.
        public bool BeginDeparture()
        {
            if (!bound || runtime.GameState.Phase != GamePhase.Intro || OwnsPresentation) return false;
            lease = true;
            if (departure.Begin()) return true;
            Debug.LogError("Marine departure refused its actual scene bindings. Applying covered location fallback, not a launch animation.");
            CompleteTransfer(false);
            return false;
        }

        public bool Skip() => departure && departure.Skip();
        public void SetReducedMotion(bool reduced)
        {
            if (departure) departure.ReducedMotion = reduced;
            if (ocean) ocean.SetReducedMotion(reduced);
        }
        private void ForwardCaption(string caption) => CaptionChanged?.Invoke(caption);
        private void ForwardCue(TransportDepartureCue cue) => CueRaised?.Invoke(cue);
        private void OnDepartureEnd(TransportDepartureEnd reason)
        {
            if (suppressEnd) return;
            CompleteTransfer(reason == TransportDepartureEnd.Completed || reason == TransportDepartureEnd.Skipped);
        }
        private void CompleteTransfer(bool completed)
        {
            if (departureFx) departureFx.ResetEffects();
            // The actual runtime already moved the player to AURORA on leaving Planning.
            // Cover restoration of the independent camera offset and the location/environment change.
            if (transferFade) transferFade.alpha = 1f;
            RestoreConsole(); RestoreEnvironment();
            if (marineRoot) marineRoot.gameObject.SetActive(false);
            if (departureOffset) departureOffset.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            lease = false; finishing = true; fadeRemaining = .8f;
            CaptionChanged?.Invoke("LATER · ABOARD AURORA-7");
        }
        private void LateUpdate()
        {
            if (bound && departureFx && departure && runtime)
                departureFx.Present(departure.Progress01 * TransportDepartureTimeline.DurationSeconds,
                    departure.IsPlaying && OwnsPresentation, runtime.IsPaused, departure.ReducedMotion);
            if (!finishing || !runtime || runtime.IsPaused) return;
            fadeRemaining = Mathf.Max(0, fadeRemaining - Time.unscaledDeltaTime);
            if (transferFade) transferFade.alpha = Mathf.Clamp01(fadeRemaining / .8f);
            if (fadeRemaining > 0) return;
            finishing = false; CaptionChanged?.Invoke(string.Empty); Transferred?.Invoke();
        }

        private void CaptureEnvironment()
        {
            originalSky = RenderSettings.skybox; ambientMode = RenderSettings.ambientMode;
            ambientLight = RenderSettings.ambientLight; ambientSky = RenderSettings.ambientSkyColor;
            ambientEquator = RenderSettings.ambientEquatorColor; ambientGround = RenderSettings.ambientGroundColor;
            ambientIntensity = RenderSettings.ambientIntensity; reflectionIntensity = RenderSettings.reflectionIntensity;
            fog = RenderSettings.fog; cameraFar = playerCamera.farClipPlane; sun = RenderSettings.sun;
            if (sun) { sunColor = sun.color; sunIntensity = sun.intensity; sunRotation = sun.transform.rotation; }
            environmentCaptured = true;
        }
        private void ApplyMarineEnvironment()
        {
            RenderSettings.skybox = marineSky; RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.29f, .36f, .43f);
            RenderSettings.ambientEquatorColor = new Color(.20f, .24f, .27f);
            RenderSettings.ambientGroundColor = new Color(.12f, .13f, .14f);
            RenderSettings.ambientIntensity = 1; RenderSettings.reflectionIntensity = .45f;
            if (sun) { sun.color = new Color(1, .82f, .61f); sun.intensity = 1.8f; sun.transform.rotation = Quaternion.Euler(16, -48, 0); }
            playerCamera.farClipPlane = 5000;
        }
        private void RestoreEnvironment()
        {
            if (!environmentCaptured) return;
            RenderSettings.skybox = originalSky; RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambientLight; RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator; RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = ambientIntensity; RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.fog = fog;
            if (playerCamera) playerCamera.farClipPlane = cameraFar;
            if (sun) { sun.color = sunColor; sun.intensity = sunIntensity; sun.transform.rotation = sunRotation; }
        }
        private void RestoreConsole()
        {
            if (!console) return;
            console.transform.SetParent(consoleParent, false);
            console.transform.SetLocalPositionAndRotation(consolePosition, consoleRotation);
            console.transform.localScale = consoleScale;
            Physics.SyncTransforms();
        }
        public void Unbind()
        {
            if (!bound) return;
            suppressEnd = true;
            if (departure)
            {
                departure.Unbind(); departure.CaptionChanged -= ForwardCaption;
                departure.CueRaised -= ForwardCue; departure.Finished -= OnDepartureEnd;
            }
            suppressEnd = false;
            if (departureFx) departureFx.ClearBinding();
            RestoreConsole(); RestoreEnvironment();
            if (ocean) ocean.ClearBinding();
            if (marineRoot) marineRoot.gameObject.SetActive(false);
            if (transferFade) transferFade.alpha = 0;
            environmentCaptured = bound = lease = finishing = false; runtime = null;
        }
        private void OnDisable() => Unbind();
    }
}
