using System;
using OutOfReach.Contracts;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Presentation.Cinematics;
using OutOfReach.Presentation.MarineOcean;
using OutOfReach.Presentation.MarineOcean.FX;
using OutOfReach.Presentation.Motion;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Composition
{
    /// <summary>Integrator-owned location, seat target and presentation lease. Never grants cargo or advances mission time.</summary>
    [DefaultExecutionOrder(450)]
    public sealed class I06MarineBindings : MonoBehaviour
    {
        private const float TransferRevealSeconds = .8f;
        private static readonly int DepartureAltitude = Shader.PropertyToID("_DepartureAltitude");

        private enum DepartureLocation { LaunchSite, UpperAtmosphere, Orbit, Interior }

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
        [SerializeField] private Transform stationarySite, dustPlane;
        [SerializeField] private Transform capsulePort, booster, auroraReceiver;
        [SerializeField] private Transform approachView, dockView, boardingStart, boardingEnd;
        [SerializeField] private GameObject orbitalSet;
        [SerializeField] private Material orbitalSky;
        [SerializeField] private Shader transitSkyShader;
        [SerializeField] private EmbodiedBodyPresenter seatedBody;
        [SerializeField] private Transform seatedBodyFrame;

        private MissionRuntime runtime;
        private Transform consoleParent;
        private Vector3 consolePosition, consoleScale;
        private Quaternion consoleRotation;
        private Material originalSky;
        private AmbientMode ambientMode;
        private Color ambientLight, ambientSky, ambientEquator, ambientGround;
        private float ambientIntensity, reflectionIntensity, cameraFar, sunIntensity;
        private bool fog, environmentCaptured, bound, lease, finishing, unbinding;
        private bool siteCaptured, siteWasActive, seatRequested;
        private FogMode fogMode;
        private Color fogColor;
        private float fogDensity, fogStart, fogEnd;
        private Light sun;
        private Color sunColor;
        private Quaternion sunRotation;
        private CameraClearFlags cameraClearFlags;
        private DepartureLocation departureLocation;
        private Material transitSky;
        private float fadeRemaining;
        public event Action<string> CaptionChanged;
        public event Action<TransportDepartureCue> CueRaised;
        public event Action<TransportDepartureEnd> DepartureEnded;
        public event Action Transferred;
        public bool OwnsPresentation => lease || finishing;
        public bool IsMarineVisible => marineRoot && marineRoot.gameObject.activeSelf;
        public bool IsDeparturePlaying => lease && departure && departure.IsPlaying;
        public bool IsSeatedDeparture => IsDeparturePlaying && PresentedSeconds < TransportDepartureTimeline.PadCutSeconds;
        public bool IsBoardingAurora => IsDeparturePlaying && departure.IsBoardingAurora;
        public float PresentedSeconds => departure ? departure.PresentedSeconds : 0f;
        public float BoardingTravel01 => IsBoardingAurora ? departure.BoardingTravel01 : 0f;
        public Transform Vehicle => vehicle;
        public Transform SeatCamera => seatCamera;
        public Transform SeatInteraction => seatInteraction;
        public Transform MarineRoot => marineRoot;
        public Transform BoardingStart => boardingStart;
        public Transform BoardingEnd => boardingEnd;

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

        public void ConfigureDepartureSite(Transform site, Transform padSurface)
        {
            if (bound) throw new InvalidOperationException("Configure the departure site before a session.");
            if (!marineRoot || !vehicle || !site || !padSurface || !padSurface.IsChildOf(site) || !site.IsChildOf(marineRoot) ||
                site == marineRoot || vehicle.IsChildOf(site))
                throw new ArgumentException("Bind the stationary launch site and its actual pad surface.");
            stationarySite = site;
            dustPlane = padSurface;
        }

        public void ConfigureDepartureSeat(EmbodiedBodyPresenter body, Transform frame)
        {
            if (bound) throw new InvalidOperationException("Configure the departure seat before a session.");
            if (!vehicle || !body || !frame || frame == vehicle || !frame.IsChildOf(vehicle))
                throw new ArgumentException("Bind the body presenter and its actual transport seat frame.");
            seatedBody = body;
            seatedBodyFrame = frame;
        }

        public void ConfigureOrbitalViews(Transform transportPort, Transform boosterStack, Transform receiver,
            Transform approachCamera, Transform dockingCamera, Transform transferStart, Transform transferEnd,
            GameObject orbit, Material stars, Shader upperSky)
        {
            if (bound) throw new InvalidOperationException("Configure orbital views before a session.");
            if (!transportPort || !boosterStack || !receiver || !approachCamera || !dockingCamera ||
                !transferStart || !transferEnd || !orbit || !stars || !upperSky)
                throw new ArgumentException("Bind both docking ports, four shot anchors and the orbital environment.");

            capsulePort = transportPort;
            booster = boosterStack;
            auroraReceiver = receiver;
            approachView = approachCamera;
            dockView = dockingCamera;
            boardingStart = transferStart;
            boardingEnd = transferEnd;
            orbitalSet = orbit;
            orbitalSky = stars;
            transitSkyShader = upperSky;
        }

        public void Bind(MissionRuntime owner, bool reducedMotion)
        {
            Unbind();
            if (!owner || owner.Session == null || !marineRoot || !vehicle || !departure || !console ||
                !seatInteraction || !marineSky || !playerCamera || !departureOffset)
                throw new InvalidOperationException("Marine scene requires its actual runtime, seat, camera, sky and departure bindings.");

            runtime = owner;
            bound = true;
            consoleParent = console.transform.parent;
            consolePosition = console.transform.localPosition;
            consoleRotation = console.transform.localRotation;
            consoleScale = console.transform.localScale;
            try
            {
                departure.Configure(owner.GameState, owner.Clock, departureOffset, playerCamera, vehicle, hatch, arm, seatCamera);
                departure.ConfigureOrbitalViews(capsulePort, booster, auroraReceiver, approachView, dockView, boardingStart, boardingEnd);
                departure.CaptionChanged += ForwardCaption;
                departure.CueRaised += ForwardCue;
                departure.Finished += OnDepartureEnd;
                SetReducedMotion(reducedMotion);
                if (ocean) ocean.BindClock(owner.Clock);
                if (departureFx)
                {
                    departureFx.ConfigureDustPlane(dustPlane);
                    departureFx.ConfigureViews(nozzleFrame, deckPlane, fxExhaust, fxSteam);
                }
                if (orbitalSet) orbitalSet.SetActive(false);
                if (owner.GameState.Phase == GamePhase.Planning)
                {
                    CaptureEnvironment();
                    if (stationarySite)
                    {
                        siteWasActive = stationarySite.gameObject.activeSelf;
                        siteCaptured = true;
                    }
                    departureLocation = DepartureLocation.LaunchSite;
                    marineRoot.gameObject.SetActive(true);
                    ApplyMarineEnvironment();
                    console.transform.SetParent(vehicle, true);
                    console.transform.SetPositionAndRotation(seatInteraction.position, seatInteraction.rotation);
                    console.transform.localScale = Vector3.one;
                    Physics.SyncTransforms();
                }
                else marineRoot.gameObject.SetActive(false);
            }
            catch
            {
                Unbind();
                throw;
            }
        }

        // Called from the confirmed Planning -> Intro event, before other camera owners restore their pivots.
        public bool BeginDeparture()
        {
            if (!bound || runtime.GameState.Phase != GamePhase.Intro || OwnsPresentation) return false;
            seatRequested = false;
            lease = true;
            if (departure.Begin()) return true;
            if (!lease) return false; // A cue subscriber may already have ended the lease.
            Debug.LogError("Transport departure could not start with the scene bindings. Applying covered location transfer.");
            CompleteTransfer(false);
            DepartureEnded?.Invoke(TransportDepartureEnd.Cancelled);
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
            if (!lease || unbinding) return;
            CompleteTransfer(reason == TransportDepartureEnd.Completed || reason == TransportDepartureEnd.Skipped);
            DepartureEnded?.Invoke(reason);
        }
        private void CompleteTransfer(bool announceArrival)
        {
            ReleaseSeat();
            if (departureFx) departureFx.ResetEffects();
            // Core moved the player to AURORA on leaving Planning. The camera lease has already returned.
            if (transferFade) transferFade.alpha = 1f;
            RestoreConsole();
            RestoreDepartureEnvironment();
            if (marineRoot) marineRoot.gameObject.SetActive(false);
            lease = false;
            finishing = true;
            fadeRemaining = TransferRevealSeconds;
            CaptionChanged?.Invoke(announceArrival ? "LATER · ABOARD AURORA-7" : string.Empty);
        }
        private void LateUpdate()
        {
            if (!bound || !runtime) return;
            if (IsDeparturePlaying)
            {
                if (transferFade) transferFade.alpha = departure.Cover01;
                PresentDepartureEnvironment();
                if (IsSeatedDeparture) PresentSeat();
                else ReleaseSeat();
            }
            if (departureFx && departure)
                departureFx.Present(PresentedSeconds, IsDeparturePlaying, runtime.IsPaused, departure.ReducedMotion);
            if (!finishing || runtime.IsPaused) return;
            fadeRemaining = Mathf.Max(0, fadeRemaining - Time.unscaledDeltaTime);
            if (transferFade) transferFade.alpha = Mathf.Clamp01(fadeRemaining / TransferRevealSeconds);
            if (fadeRemaining > 0) return;
            finishing = false; CaptionChanged?.Invoke(string.Empty); Transferred?.Invoke();
        }

        private void PresentSeat()
        {
            if (!seatedBody) return;
            if (!seatRequested && !runtime.IsPaused)
            {
                seatRequested = true;
                if (!seatedBody.TryBeginDepartureSeat(seatedBodyFrame))
                    Debug.LogWarning("Transport seat could not acquire its body presentation.", this);
            }
            if (!seatedBody.PresentDepartureSeat(PresentedSeconds)) return;

            float lookDown = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.30f, 2.26f, PresentedSeconds));
            float lookUp = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(3.23f, 4.38f, PresentedSeconds));
            float glance = lookDown * lookUp;
            if (glance <= 0f) return;

            // Lean past the neck toward the hardware. The departure presenter restores its base
            // camera pose before each sample, including while the playhead is suspended.
            Transform eye = playerCamera.transform;
            Vector3 leaningEye = eye.position + (seatedBodyFrame.forward * .13f - seatedBodyFrame.up * .105f) * glance;
            Vector3 buckle = seatedBodyFrame.TransformPoint(new Vector3(0f, .665f, .315f));
            Quaternion target = Quaternion.LookRotation(buckle - leaningEye, seatedBodyFrame.up);
            Quaternion tilt = Quaternion.Slerp(Quaternion.identity, target * Quaternion.Inverse(eye.rotation), glance);
            Vector3 pivot = leaningEye + tilt * (departureOffset.position - eye.position);
            departureOffset.SetPositionAndRotation(pivot, tilt * departureOffset.rotation);
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, 55f, glance);
        }

        private void ReleaseSeat()
        {
            if (seatedBody && seatedBody.HasDepartureSeat) seatedBody.EndDepartureSeat();
        }

        private void PresentDepartureEnvironment()
        {
            if (!orbitalSet) return;
            if (departure.IsBoardingAurora)
            {
                if (departureLocation == DepartureLocation.Interior) return;
                departureLocation = DepartureLocation.Interior;
                orbitalSet.SetActive(false);
                RestoreEnvironment();
                return;
            }
            if (departure.IsInOrbit)
            {
                if (departureLocation == DepartureLocation.Orbit) return;
                departureLocation = DepartureLocation.Orbit;
                if (stationarySite) stationarySite.gameObject.SetActive(false);
                orbitalSet.SetActive(true);
                ApplyOrbitEnvironment();
                return;
            }
            if (PresentedSeconds < TransportDepartureTimeline.UpperAirSeconds) return;
            if (departureLocation != DepartureLocation.UpperAtmosphere)
            {
                departureLocation = DepartureLocation.UpperAtmosphere;
                if (stationarySite) stationarySite.gameObject.SetActive(false);
                if (!transitSky)
                    transitSky = new Material(transitSkyShader) { name = "Departure upper atmosphere", hideFlags = HideFlags.DontSave };
                RenderSettings.skybox = transitSky;
                RenderSettings.fog = false;
                playerCamera.clearFlags = CameraClearFlags.Skybox;
                playerCamera.farClipPlane = 1600f;
            }
            transitSky.SetFloat(DepartureAltitude, departure.AtmosphereBlend01);
        }

        private void CaptureEnvironment()
        {
            originalSky = RenderSettings.skybox; ambientMode = RenderSettings.ambientMode;
            ambientLight = RenderSettings.ambientLight; ambientSky = RenderSettings.ambientSkyColor;
            ambientEquator = RenderSettings.ambientEquatorColor; ambientGround = RenderSettings.ambientGroundColor;
            ambientIntensity = RenderSettings.ambientIntensity; reflectionIntensity = RenderSettings.reflectionIntensity;
            fog = RenderSettings.fog; cameraFar = playerCamera.farClipPlane; sun = RenderSettings.sun;
            cameraClearFlags = playerCamera.clearFlags;
            fogMode = RenderSettings.fogMode; fogColor = RenderSettings.fogColor;
            fogDensity = RenderSettings.fogDensity; fogStart = RenderSettings.fogStartDistance; fogEnd = RenderSettings.fogEndDistance;
            if (sun) { sunColor = sun.color; sunIntensity = sun.intensity; sunRotation = sun.transform.rotation; }
            environmentCaptured = true;
        }
        private void ApplyMarineEnvironment()
        {
            RenderSettings.skybox = marineSky;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.60f, .68f, .74f); RenderSettings.fogDensity = .00045f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.45f, .54f, .63f);
            RenderSettings.ambientEquatorColor = new Color(.34f, .39f, .43f);
            RenderSettings.ambientGroundColor = new Color(.22f, .24f, .22f);
            RenderSettings.ambientIntensity = 1; RenderSettings.reflectionIntensity = .45f;
            if (sun) { sun.color = new Color(1, .93f, .82f); sun.intensity = 2f; sun.transform.rotation = Quaternion.Euler(28, -48, 0); }
            playerCamera.farClipPlane = 10500;
            playerCamera.clearFlags = CameraClearFlags.Skybox;
        }
        private void ApplyOrbitEnvironment()
        {
            RenderSettings.skybox = orbitalSky;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.32f, .39f, .52f);
            RenderSettings.ambientEquatorColor = new Color(.20f, .25f, .35f);
            RenderSettings.ambientGroundColor = new Color(.10f, .14f, .22f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = .32f;
            if (sun)
            {
                sun.color = new Color(1f, .96f, .89f);
                sun.intensity = 2.25f;
                sun.transform.rotation = Quaternion.Euler(26f, 220f, 0f);
            }
            playerCamera.clearFlags = CameraClearFlags.Skybox;
            playerCamera.farClipPlane = 140f;
        }
        private void RestoreDepartureEnvironment()
        {
            if (orbitalSet) orbitalSet.SetActive(false);
            if (siteCaptured && stationarySite) stationarySite.gameObject.SetActive(siteWasActive);
            siteCaptured = false;
            RestoreEnvironment();
            if (!transitSky) return;
            if (Application.isPlaying) Destroy(transitSky);
            else DestroyImmediate(transitSky);
            transitSky = null;
        }
        private void RestoreEnvironment()
        {
            if (!environmentCaptured) return;
            RenderSettings.skybox = originalSky; RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambientLight; RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator; RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = ambientIntensity; RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity; RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
            if (playerCamera)
            {
                playerCamera.farClipPlane = cameraFar;
                playerCamera.clearFlags = cameraClearFlags;
            }
            if (sun) { sun.color = sunColor; sun.intensity = sunIntensity; sun.transform.rotation = sunRotation; }
            environmentCaptured = false; // Transfer consumes the lease; later Unbind must not overwrite ship-owned changes.
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
            if (!bound || unbinding) return;
            bool wasPlaying = IsDeparturePlaying;
            bool wasFinishing = finishing;
            unbinding = true;
            try
            {
                ReleaseSeat();
                if (departure)
                {
                    departure.CaptionChanged -= ForwardCaption;
                    departure.CueRaised -= ForwardCue;
                    departure.Finished -= OnDepartureEnd;
                    departure.Unbind();
                }
                if (departureFx) departureFx.ClearBinding();
                RestoreConsole();
                RestoreDepartureEnvironment();
                if (ocean) ocean.ClearBinding();
                if (marineRoot) marineRoot.gameObject.SetActive(false);
                if (transferFade) transferFade.alpha = 0;
            }
            finally
            {
                unbinding = bound = lease = finishing = false;
                runtime = null;
            }
            if (wasPlaying || wasFinishing) CaptionChanged?.Invoke(string.Empty);
            if (wasPlaying) DepartureEnded?.Invoke(TransportDepartureEnd.Cancelled);
        }
        private void OnDisable() => Unbind();
    }
}
