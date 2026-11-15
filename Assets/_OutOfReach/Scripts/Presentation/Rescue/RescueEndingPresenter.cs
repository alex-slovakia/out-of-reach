using System;
using System.Globalization;
using OutOfReach.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OutOfReach.Presentation.Rescue
{
    /// <summary>
    /// RESCUE-ENDING-01 (backlog R42): the first-person rescue arrival that answers Core's confirmed
    /// <see cref="CinematicId.RecoveryArrival"/> request. Core already ends a survived mission with Recovered and then
    /// requests RecoveryArrival → MarsDeparture → Report; this presenter only replaces the RecoveryArrival picture and
    /// publishes the single matching CinematicFinished. It never advances time, charges resources or changes phase.
    ///
    /// Shots: the cabin window (approach, braking burns, floodlight, VO24a), the aft docking camera (centreline
    /// closing, visible contact, capture, hard dock; VO24b only after the contact), then inside the airlock (latches,
    /// pressure, the hatch opens and light floods in, fade). Pause freezes the playhead and audio; hold Space/Enter or
    /// <see cref="SkipActive"/> skips; reduced effects remove shake, glide and push; a lost or changed mission ends it
    /// at once and acknowledges it as skipped exactly once. Hand the existing cinematic presenter
    /// <see cref="FilterFor"/>'s bus so that only one presenter answers the request.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(101)]
    public sealed class RescueEndingPresenter : MonoBehaviour
    {
        public const float SkipHoldSeconds = .6f;
        private const float WindowFov = 56f, DockingFov = 52f, HatchFov = 62f;

        [Header("Existing camera chain (MissionCinematicPresenter's offset pivot and the gameplay camera)")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Camera viewCamera;
        [Header("Authored camera poses in the ship")]
        [SerializeField] private Transform windowView;
        [SerializeField] private Transform dockingCameraView;
        [SerializeField] private Transform hatchView;
        [Header("Owned visuals, inactive outside the sequence")]
        [SerializeField] private GameObject visualSet;
        [SerializeField] private RescueCraftV01 craft;
        [SerializeField] private Transform craftFar, craftNear, craftAside, craftDockStart, craftDocked;
        [SerializeField] private Transform hatchGlow;
        [SerializeField] private Renderer hatchGlowRenderer;
        [SerializeField] private Light hatchLight;
        [Tooltip("Aurora's outer hatch leaves (visual only). Slid open during the hatch shot after the door view's own write, restored at the end.")]
        [SerializeField] private Transform[] hatchLeaves = Array.Empty<Transform>();
        [SerializeField] private float hatchLeafTravel = .95f;
        [SerializeField] private Light approachLight;
        [SerializeField] private Light dockingLight;
        [SerializeField] private Renderer[] latchLamps = Array.Empty<Renderer>();
        [SerializeField] private Material lampAmber, lampCyan;
        [SerializeField] private Transform reticle;
        [SerializeField] private TextMesh reticleReadout;
        [SerializeField] private CanvasGroup blackout;
        [Header("Audio: Alex-approved Story02 masters VO24a/VO24b, played dry")]
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip approachVoice;
        [SerializeField] private AudioClip dockedVoice;
        [SerializeField] private bool reducedEffects;

        private IMissionEventBus eventBus;
        private IGameState gameState;
        private ISimulationClock simulationClock;
        private bool subscribed;
        private int highestSequenceId;
        private int claimedSequenceId;
        private bool active;
        private CinematicRequested activeRequest;
        private IMissionEventBus activeBus;
        private float elapsed, previousElapsed;
        private bool glide, audioPaused;
        private float skipHeld;

        private Vector3 pivotLocalPosition, cameraRelativePosition, startEyePosition;
        private Quaternion pivotLocalRotation, cameraRelativeRotation, startEyeRotation;
        private float cameraFov, cameraFar, cameraNear, blackoutAlpha;
        private bool visualSetWasActive;
        private Vector3 craftLocalPosition;
        private Quaternion craftLocalRotation;
        private MaterialPropertyBlock glowBlock;
        private Vector3[] leafLocalPositions = Array.Empty<Vector3>();
        private Vector3[] leafWorldPositions = Array.Empty<Vector3>();
        private AudioClip squelch, contactClunk, hardClunk, latch, hiss;

        public event Action<string> CaptionChanged;
        /// <summary>Start, end and every caption change: refresh HUD hiding and subtitles.</summary>
        public event Action PresentationChanged;
        public event Action<CinematicFinished> SequenceEnded;

        public bool IsPlaying => active;
        public int ActiveSequenceId => active ? activeRequest.SequenceId : 0;
        public float ElapsedSeconds => active ? elapsed : 0f;
        public RescueShot CurrentShot => RescueEndingTimeline.ShotAt(elapsed);
        public string CurrentCaption { get; private set; } = string.Empty;
        public string CurrentSpeaker { get; private set; } = string.Empty;
        public bool IsConfigured => eventBus != null && gameState != null;

        public bool ReducedEffects
        {
            get => reducedEffects;
            set { reducedEffects = value; if (active) ApplyFrame(); }
        }

        /// <summary>Bind explicitly from the composition root, BEFORE the existing cinematic presenter is configured
        /// with <see cref="FilterFor"/>. Reconfiguration ends (skips) any active sequence first.</summary>
        public void Configure(IMissionEventBus events, IGameState state, ISimulationClock clock = null)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (state == null) throw new ArgumentNullException(nameof(state));
            Complete(true);
            Unsubscribe();
            eventBus = events; gameState = state; simulationClock = clock;
            Subscribe();
        }

        /// <summary>Unbind on session end/teardown. Acknowledges an active sequence as skipped once.</summary>
        public void Release()
        {
            Complete(true);
            Unsubscribe();
            eventBus = null; gameState = null; simulationClock = null;
        }

        /// <summary>The bus to hand to the existing MissionCinematicPresenter: identical, except a RecoveryArrival
        /// request this presenter has taken over is not shown to it. Fails open.</summary>
        public IMissionEventBus FilterFor(IMissionEventBus inner) => new RescueEndingBusFilter(inner, TakeOver);

        /// <summary>Scene wiring check without side effects (installer, tests, composition logging).</summary>
        public bool HasValidViews => cameraPivot && viewCamera && viewCamera.transform.IsChildOf(cameraPivot)
            && (cameraPivot.lossyScale - Vector3.one).sqrMagnitude < .0001f
            && windowView && dockingCameraView && hatchView && visualSet && craft
            && craftFar && craftNear && craftAside && craftDockStart && craftDocked;

        public bool SkipActive()
        {
            if (!active) return false;
            Complete(true);
            return true;
        }

        private void Awake()
        {
            squelch = RescueEndingSfx.Squelch();
            contactClunk = RescueEndingSfx.Clunk(false);
            hardClunk = RescueEndingSfx.Clunk(true);
            latch = RescueEndingSfx.Latch();
            hiss = RescueEndingSfx.Hiss(RescueEndingTimeline.EqualizeEnd - RescueEndingTimeline.EqualizeStart);
            glowBlock = new MaterialPropertyBlock();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Complete(true);
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Complete(true);
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed || !isActiveAndEnabled || eventBus == null) return;
            eventBus.CinematicRequested += OnRequested;
            eventBus.PhaseChanged += OnPhaseChanged;
            eventBus.MissionEnded += OnMissionEnded;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;
            eventBus.CinematicRequested -= OnRequested;
            eventBus.PhaseChanged -= OnPhaseChanged;
            eventBus.MissionEnded -= OnMissionEnded;
        }

        private bool TakeOver(CinematicRequested request)
        {
            if (request.Cinematic != CinematicId.RecoveryArrival) return false;
            if (request.SequenceId == claimedSequenceId && claimedSequenceId != 0) return true;
            return TryBegin(request);
        }

        private void OnRequested(CinematicRequested request)
        {
            if (request.SequenceId <= highestSequenceId) return;
            if (request.Cinematic == CinematicId.RecoveryArrival) { TryBegin(request); return; }
            // Any newer request supersedes an active rescue sequence (it cannot normally happen before our ack).
            if (active) Complete(true);
        }

        private void OnPhaseChanged(PhaseChanged phase)
        {
            if (active && !(phase.Current == GamePhase.Outcome && gameState != null && gameState.Outcome == MissionOutcome.Recovered))
                Complete(true);
        }

        private void OnMissionEnded(MissionEnded ended)
        {
            // Core confirms the outcome before it requests RecoveryArrival; another end during the sequence is new.
            if (active) Complete(true);
        }

        private bool CanPresentNow() => isActiveAndEnabled && subscribed && IsConfigured && HasValidViews
            && gameState.Phase == GamePhase.Outcome && gameState.Outcome == MissionOutcome.Recovered;

        private bool TryBegin(CinematicRequested request)
        {
            if (request.SequenceId <= highestSequenceId) return active && request.SequenceId == claimedSequenceId;
            if (!CanPresentNow()) return false;
            Complete(true);
            highestSequenceId = claimedSequenceId = request.SequenceId;
            activeRequest = request;
            activeBus = eventBus;
            Capture();
            active = true;
            elapsed = previousElapsed = 0f;
            skipHeld = 0f;
            audioPaused = false;
            ApplyFrame();
            NotifyPresentation();
            return true;
        }

        private void Capture()
        {
            pivotLocalPosition = cameraPivot.localPosition;
            pivotLocalRotation = cameraPivot.localRotation;
            Transform cam = viewCamera.transform;
            cameraRelativePosition = cameraPivot.InverseTransformPoint(cam.position);
            cameraRelativeRotation = Quaternion.Inverse(cameraPivot.rotation) * cam.rotation;
            startEyePosition = cam.position;
            startEyeRotation = cam.rotation;
            cameraFov = viewCamera.fieldOfView; cameraFar = viewCamera.farClipPlane; cameraNear = viewCamera.nearClipPlane;
            blackoutAlpha = blackout ? blackout.alpha : 0f;
            visualSetWasActive = visualSet.activeSelf;
            craftLocalPosition = craft.transform.localPosition;
            craftLocalRotation = craft.transform.localRotation;
            glide = !reducedEffects && Vector3.Distance(startEyePosition, windowView.position) <= RescueEndingTimeline.GlideMaximumMetres;
            int leafCount = hatchLeaves != null ? hatchLeaves.Length : 0;
            leafLocalPositions = new Vector3[leafCount];
            leafWorldPositions = new Vector3[leafCount];
            for (int i = 0; i < leafCount; i++)
                if (hatchLeaves[i]) { leafLocalPositions[i] = hatchLeaves[i].localPosition; leafWorldPositions[i] = hatchLeaves[i].position; }
            visualSet.SetActive(true);
            craft.Build();
        }

        private void Update()
        {
            if (!active || IsPaused) { skipHeld = 0f; return; }
            Keyboard keyboard = Keyboard.current;
            bool held = keyboard != null && (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed);
            skipHeld = held ? skipHeld + Time.unscaledDeltaTime : 0f;
            if (skipHeld >= SkipHoldSeconds) SkipActive();
        }

        private bool IsPaused => simulationClock != null && simulationClock.IsPaused;

        private void LateUpdate()
        {
            if (!active) return;
            if (!CanPresentNow()) { Complete(true); return; }
            if (IsPaused)
            {
                if (!audioPaused) { PauseAudio(true); audioPaused = true; }
                ApplyFrame();   // hold the frame; another view may restore the rig while paused
                return;
            }
            if (audioPaused) { PauseAudio(false); audioPaused = false; }
            float delta = Time.unscaledDeltaTime;
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0f) delta = 0f;
            previousElapsed = elapsed;
            elapsed = Mathf.Min(RescueEndingTimeline.Duration, elapsed + Mathf.Min(delta, .1f));
            FireCues(previousElapsed, elapsed);
            if (!active) return;
            ApplyFrame();
            if (elapsed >= RescueEndingTimeline.Duration) Complete(false);
        }

        /// <summary>Test/QA hook: advance the playhead by unpaused presentation seconds exactly as LateUpdate does.</summary>
        public void Advance(float seconds)
        {
            if (!active || IsPaused || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            float target = Mathf.Min(RescueEndingTimeline.Duration, elapsed + seconds);
            while (active && elapsed < target)
            {
                previousElapsed = elapsed;
                elapsed = Mathf.Min(target, elapsed + .05f);
                FireCues(previousElapsed, elapsed);
            }
            if (!active) return;
            ApplyFrame();
            if (elapsed >= RescueEndingTimeline.Duration) Complete(false);
        }

        private static bool Crossed(float from, float to, float at) => from < at && to >= at;

        private void FireCues(float from, float to)
        {
            if (Crossed(from, to, RescueEndingTimeline.VoiceApproachStart)) PlayVoice(approachVoice);
            if (Crossed(from, to, RescueEndingTimeline.Contact)) Sfx(contactClunk, .9f);
            if (Crossed(from, to, RescueEndingTimeline.VoiceDockedStart)) PlayVoice(dockedVoice);
            if (Crossed(from, to, RescueEndingTimeline.Capture)) Sfx(latch, .7f);
            if (Crossed(from, to, RescueEndingTimeline.HardDock)) Sfx(hardClunk, 1f);
            foreach (float at in RescueEndingTimeline.LatchTimes) if (Crossed(from, to, at)) Sfx(latch, .55f);
            if (Crossed(from, to, RescueEndingTimeline.EqualizeStart)) Sfx(hiss, .8f);
            if (Crossed(from, to, RescueEndingTimeline.HatchOpenStart)) Sfx(contactClunk, .45f);
        }

        private void PlayVoice(AudioClip clip)
        {
            if (sfxSource && squelch) sfxSource.PlayOneShot(squelch, .5f);
            if (!voiceSource || !clip) return;
            voiceSource.Stop();
            voiceSource.clip = clip;
            voiceSource.Play();
        }

        private void Sfx(AudioClip clip, float volume)
        {
            if (sfxSource && clip) sfxSource.PlayOneShot(clip, volume);
        }

        private void PauseAudio(bool pause)
        {
            foreach (AudioSource source in new[] { voiceSource, sfxSource })
            {
                if (!source) continue;
                if (pause) source.Pause(); else source.UnPause();
            }
        }

        private void ApplyFrame()
        {
            if (!active) return;
            float t = elapsed;
            RescueShot shot = RescueEndingTimeline.ShotAt(t);
            bool reduced = reducedEffects;

            // Craft.
            Transform c = craft.transform;
            if (shot == RescueShot.Window)
            {
                if (t < RescueEndingTimeline.ApproachEnd)
                {
                    float a = RescueEndingTimeline.Approach(t);
                    c.SetPositionAndRotation(Vector3.Lerp(craftFar.position, craftNear.position, a), Quaternion.Slerp(craftFar.rotation, craftNear.rotation, a));
                }
                else
                {
                    float a = RescueEndingTimeline.Aside(t);
                    c.SetPositionAndRotation(Vector3.Lerp(craftNear.position, craftAside.position, a), Quaternion.Slerp(craftNear.rotation, craftAside.rotation, a));
                }
            }
            else
            {
                Vector3 axis = (craftDockStart.position - craftDocked.position).normalized;
                Vector3 contact = craftDocked.position + axis * .05f;
                float closing = RescueEndingTimeline.DockClosing(t);
                Vector3 position = Vector3.Lerp(craftDockStart.position, contact, closing);
                position = Vector3.Lerp(position, craftDocked.position, RescueEndingTimeline.HardDockRetract(t));
                c.SetPositionAndRotation(position, Quaternion.Slerp(craftDockStart.rotation, craftDocked.rotation, closing));
            }
            craft.Pose(RescueEndingTimeline.RetroBurn(t), RescueEndingTimeline.SidePuff(t), RescueEndingTimeline.Captured(t),
                RescueEndingTimeline.Floodlight(t), reduced, t);

            // Camera: the gameplay camera is placed at the authored pose through its dedicated offset pivot.
            Vector3 eye; Quaternion look; float fov;
            switch (shot)
            {
                case RescueShot.Window:
                    float blend = RescueEndingTimeline.WindowBlend(t, glide);
                    eye = Vector3.Lerp(startEyePosition, windowView.position, blend);
                    look = Quaternion.Slerp(startEyeRotation, windowView.rotation, blend);
                    fov = Mathf.Lerp(cameraFov, WindowFov, blend);
                    if (!reduced)
                        look *= Quaternion.Euler((Mathf.PerlinNoise(t * .31f, 1.7f) - .5f) * .7f, (Mathf.PerlinNoise(3.1f, t * .27f) - .5f) * .7f, 0f);
                    break;
                case RescueShot.DockingCamera:
                    eye = dockingCameraView.position; look = dockingCameraView.rotation; fov = DockingFov;
                    break;
                default:
                    eye = hatchView.position + hatchView.forward * (.35f * RescueEndingTimeline.HatchPush(t, reduced));
                    look = hatchView.rotation; fov = HatchFov;
                    break;
            }
            float shake = RescueEndingTimeline.Shake(t, reduced);
            if (shake > 0f)
            {
                float n1 = Mathf.PerlinNoise(t * 23f, .3f) - .5f, n2 = Mathf.PerlinNoise(.7f, t * 29f) - .5f, n3 = Mathf.PerlinNoise(t * 19f, 5.1f) - .5f;
                eye += look * new Vector3(n1, n2, 0f) * (.05f * shake);
                look *= Quaternion.Euler(n2 * 2.4f * shake, n1 * 2.4f * shake, n3 * 1.6f * shake);
            }
            Quaternion pivotRotation = look * Quaternion.Inverse(cameraRelativeRotation);
            cameraPivot.SetPositionAndRotation(eye - pivotRotation * cameraRelativePosition, pivotRotation);
            viewCamera.fieldOfView = fov;
            viewCamera.farClipPlane = Mathf.Max(cameraFar, 600f);
            viewCamera.nearClipPlane = Mathf.Min(cameraNear, .05f);

            // Docking-camera reticle and range readout.
            if (reticle)
            {
                bool show = shot == RescueShot.DockingCamera;
                if (reticle.gameObject.activeSelf != show) reticle.gameObject.SetActive(show);
                if (show)
                {
                    reticle.SetPositionAndRotation(eye + look * Vector3.forward * .5f, look);
                    if (reticleReadout)
                    {
                        Vector3 dockedCollar = craftDocked.position + craftDocked.forward * RescueCraftV01.CollarFrontZ;
                        float range = Vector3.Distance(c.position + c.forward * RescueCraftV01.CollarFrontZ, dockedCollar);
                        reticleReadout.text = t >= RescueEndingTimeline.Capture ? "CAPTURE · LATCHED" : t >= RescueEndingTimeline.Contact ? "CONTACT" : "RANGE " + range.ToString("0.0", CultureInfo.InvariantCulture) + " m";
                    }
                }
            }

            // Hatch light: seam, then the doorway widens and floods the airlock.
            float seam = RescueEndingTimeline.Seam(t), open = RescueEndingTimeline.HatchOpen(t);
            if (hatchGlow && hatchGlowRenderer)
            {
                bool glow = shot == RescueShot.Hatch && open > 0f;
                hatchGlowRenderer.enabled = glow;
                if (glow)
                {
                    hatchGlow.localScale = Vector3.one;
                    hatchGlowRenderer.GetPropertyBlock(glowBlock);
                    glowBlock.SetColor("_BaseColor", new Color(1f, .9f, .74f) * (1.2f + 9f * open));
                    hatchGlowRenderer.SetPropertyBlock(glowBlock);
                }
            }
            if (hatchLight)
            {
                hatchLight.intensity = shot == RescueShot.Hatch ? .5f * seam + 10f * open : 0f;
                hatchLight.enabled = hatchLight.intensity > .001f;
            }
            // Outer hatch: Aurora's own leaves slide apart (the door view wrote this frame first; we restore at the end).
            if (shot == RescueShot.Hatch)
                for (int i = 0; i < leafWorldPositions.Length; i++)
                    if (hatchLeaves[i]) hatchLeaves[i].position = leafWorldPositions[i] + Vector3.right * ((i % 2 == 0 ? -1f : 1f) * hatchLeafTravel * open);
            if (approachLight) { approachLight.enabled = shot == RescueShot.Window; approachLight.intensity = shot == RescueShot.Window ? 5f : 0f; }
            if (dockingLight) { dockingLight.enabled = shot == RescueShot.DockingCamera; dockingLight.intensity = shot == RescueShot.DockingCamera ? 2.6f : 0f; }
            int latched = RescueEndingTimeline.LatchesClosed(t);
            for (int i = 0; i < latchLamps.Length; i++)
                if (latchLamps[i]) latchLamps[i].sharedMaterial = i < latched ? lampCyan : lampAmber;

            if (blackout) blackout.alpha = Mathf.Max(blackoutAlpha, RescueEndingTimeline.Blackout(t, !glide));

            int line = RescueEndingTimeline.LineAt(t);
            if (line < 0) SetCaption(string.Empty, string.Empty);
            else SetCaption(RescueEndingTimeline.Lines[line].Text, RescueEndingTimeline.Lines[line].Speaker);
        }

        private void Restore()
        {
            if (cameraPivot) cameraPivot.SetLocalPositionAndRotation(pivotLocalPosition, pivotLocalRotation);
            if (viewCamera) { viewCamera.fieldOfView = cameraFov; viewCamera.farClipPlane = cameraFar; viewCamera.nearClipPlane = cameraNear; }
            if (blackout) blackout.alpha = blackoutAlpha;
            if (craft)
            {
                craft.Pose(0f, 0f, false, 0f, true);
                craft.transform.SetLocalPositionAndRotation(craftLocalPosition, craftLocalRotation);
            }
            if (hatchGlowRenderer) hatchGlowRenderer.enabled = false;
            if (hatchLight) { hatchLight.intensity = 0f; hatchLight.enabled = false; }
            if (approachLight) { approachLight.intensity = 0f; approachLight.enabled = false; }
            if (dockingLight) { dockingLight.intensity = 0f; dockingLight.enabled = false; }
            for (int i = 0; i < leafLocalPositions.Length && hatchLeaves != null && i < hatchLeaves.Length; i++)
                if (hatchLeaves[i]) hatchLeaves[i].localPosition = leafLocalPositions[i];
            foreach (Renderer lamp in latchLamps) if (lamp) lamp.sharedMaterial = lampAmber;
            if (reticle) reticle.gameObject.SetActive(false);
            if (voiceSource) { voiceSource.Stop(); voiceSource.clip = null; }
            if (sfxSource) sfxSource.Stop();
            if (visualSet) visualSet.SetActive(visualSetWasActive);
        }

        /// <summary>Restores every captured value, then acknowledges once on the bus the request came from.</summary>
        private void Complete(bool skipped)
        {
            if (!active) return;
            active = false;
            var finished = new CinematicFinished(activeRequest.Cinematic, activeRequest.SequenceId, skipped);
            IMissionEventBus bus = activeBus;
            activeBus = null;
            Restore();
            SetCaption(string.Empty, string.Empty);
            NotifyPresentation();
            try { SequenceEnded?.Invoke(finished); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            // The only domain-facing publication. Values are restored first, so the next request (MarsDeparture)
            // snapshots clean visuals in this same dispatch.
            try { bus?.Publish(finished); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void SetCaption(string caption, string speaker)
        {
            if (CurrentCaption == caption && CurrentSpeaker == speaker) return;
            CurrentCaption = caption ?? string.Empty;
            CurrentSpeaker = speaker ?? string.Empty;
            try { CaptionChanged?.Invoke(CurrentCaption); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            NotifyPresentation();
        }

        private void NotifyPresentation()
        {
            try { PresentationChanged?.Invoke(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }
}
