using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Cinematics
{
    /// <summary>Temporary visual response to confirmed Core requests. Never advances or mutates mission state.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class MissionCinematicPresenter : MonoBehaviour
    {
        private const float MaximumDurationSeconds = 40f;

        [Header("Explicit presentation references; all are optional")]
        [Tooltip("Dedicated presentation offset below the player/look rig and above its existing camera. No other writer may animate this transform.")]
        [SerializeField] private Transform cameraOffsetPivot;
        [Tooltip("Target world pose for the offset pivot during EndingPullback. Leave empty to retain the current frame.")]
        [SerializeField] private Transform endingFramingAnchor;
        [Tooltip("Only lights reserved for temporary cinematic modulation. Values are restored after each sequence.")]
        [SerializeField] private Light[] effectLights = Array.Empty<Light>();
        [Tooltip("Black fade artwork. Configure this group as non-interactive; this presenter changes alpha only.")]
        [SerializeField] private CanvasGroup blackoutOverlay;
        [Tooltip("Restrained red-tinted artwork. Configure as non-interactive; alpha only is animated.")]
        [SerializeField] private CanvasGroup emergencyOverlay;

        [Header("Real seconds; pauses follow the optional authoritative clock")]
        [SerializeField, Range(0.25f, MaximumDurationSeconds)] private float impactSeconds = 4.5f;
        [SerializeField, Range(0.25f, MaximumDurationSeconds)] private float falseSignalSeconds = 5f;
        [SerializeField, Range(0.25f, MaximumDurationSeconds)] private float communicationRestoredSeconds = 4f;
        [SerializeField, Range(0.25f, MaximumDurationSeconds)] private float endingPullbackSeconds = 8f;
        [SerializeField] private bool reducedEffects;

        private IMissionEventBus eventBus;
        private IGameState gameState;
        private ISimulationClock simulationClock;
        private bool subscribed;
        private bool active;
        private int configurationGeneration;
        private int subscriptionGeneration;
        private IMissionEventBus subscribedBus;
        private Action<CinematicRequested> requestHandler;
        private Action<PhaseChanged> phaseHandler;
        private Action<MissionEnded> outcomeHandler;
        private int highestSequenceId;
        private CinematicRequested activeRequest;
        private IMissionEventBus activeRequestBus;
        private float elapsedRealSeconds;
        private float durationSeconds;
        private MissionOutcome outcomeAtStart;

        private Transform capturedPivot;
        private Vector3 pivotLocalPosition;
        private Quaternion pivotLocalRotation;
        private Vector3 pivotWorldPosition;
        private Quaternion pivotWorldRotation;
        private bool hasEndingFrame;
        private Vector3 endingWorldPosition;
        private Quaternion endingWorldRotation;
        private CanvasGroup capturedBlackout;
        private CanvasGroup capturedEmergency;
        private float blackoutAlpha;
        private float emergencyAlpha;
        private readonly List<LightSnapshot> capturedLights = new List<LightSnapshot>();

        private struct LightSnapshot
        {
            public Light Light;
            public Color Color;
            public float Intensity;
            public bool Enabled;
        }

        public event Action<CinematicRequested> SequenceStarted;
        public event Action<CinematicFinished> SequenceEnded;
        public event Action<string> CaptionChanged;

        public bool IsPlaying => active;
        public int ActiveSequenceId => active ? activeRequest.SequenceId : 0;
        public string CurrentCaption { get; private set; } = string.Empty;
        public bool ReducedEffects
        {
            get => reducedEffects;
            set
            {
                reducedEffects = value;
                if (active) ApplyFrame();
            }
        }

        /// <summary>Bind explicitly from the composition root. Reconfiguration cancels/restores the previous sequence.</summary>
        public void Configure(IMissionEventBus events, IGameState state, ISimulationClock clock = null)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (state == null) throw new ArgumentNullException(nameof(state));
            int generation = ++configurationGeneration;
            Unsubscribe();
            CompleteActive(true);
            // Completion listeners may bind another session. The latest explicit Configure wins.
            if (generation != configurationGeneration) return;
            // A listener may also have re-enabled this component and resubscribed the old binding.
            Unsubscribe();
            eventBus = events;
            gameState = state;
            simulationClock = clock;
            Subscribe();
        }

        /// <summary>Cancel the current visual sequence and acknowledge it as skipped exactly once.</summary>
        public bool SkipActive()
        {
            if (!active) return false;
            CompleteActive(true);
            return true;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            CompleteActive(true);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            CompleteActive(true);
        }

        private void Subscribe()
        {
            if (subscribed || !isActiveAndEnabled || eventBus == null || gameState == null) return;
            int generation = ++subscriptionGeneration;
            subscribedBus = eventBus;
            requestHandler = request => { if (subscribed && generation == subscriptionGeneration) OnRequested(request); };
            phaseHandler = phase => { if (subscribed && generation == subscriptionGeneration) OnPhaseChanged(phase); };
            outcomeHandler = outcome => { if (subscribed && generation == subscriptionGeneration) OnMissionEnded(outcome); };
            subscribedBus.CinematicRequested += requestHandler;
            subscribedBus.PhaseChanged += phaseHandler;
            subscribedBus.MissionEnded += outcomeHandler;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;
            subscriptionGeneration++;
            subscribedBus.CinematicRequested -= requestHandler;
            subscribedBus.PhaseChanged -= phaseHandler;
            subscribedBus.MissionEnded -= outcomeHandler;
            subscribedBus = null;
            requestHandler = null;
            phaseHandler = null;
            outcomeHandler = null;
        }

        private void OnRequested(CinematicRequested request)
        {
            // C01 issues positive, process-wide increasing IDs. Keep this high-water mark across Configure.
            if (!subscribed || !isActiveAndEnabled || request.SequenceId <= highestSequenceId) return;
            highestSequenceId = request.SequenceId;
            var requestedBus = eventBus;
            var requestedState = gameState;
            int requestedGeneration = configurationGeneration;
            CompleteActive(true);
            // Completing an earlier request can synchronously trigger a newer request or a reconfiguration.
            if (!subscribed || !isActiveAndEnabled || request.SequenceId != highestSequenceId ||
                configurationGeneration != requestedGeneration || eventBus != requestedBus || gameState != requestedState ||
                !CanPresent(request.Cinematic, gameState.Phase, gameState.Outcome))
            {
                PublishFinished(requestedBus, new CinematicFinished(request.Cinematic, request.SequenceId, true));
                return;
            }

            activeRequest = request;
            activeRequestBus = requestedBus;
            elapsedRealSeconds = 0;
            durationSeconds = DurationFor(request.Cinematic);
            outcomeAtStart = gameState.Outcome;
            CaptureVisuals();
            active = true;
            Notify(SequenceStarted, request);
            // An outward subscriber may synchronously skip/disable/reconfigure this presenter.
            if (active && activeRequest.SequenceId == request.SequenceId) ApplyFrame();
        }

        private void LateUpdate()
        {
            if (!active) return;
            if (!CanPresent(activeRequest.Cinematic, gameState.Phase, gameState.Outcome) ||
                (outcomeAtStart != MissionOutcome.Lost && gameState.Outcome == MissionOutcome.Lost))
            {
                CompleteActive(true);
                return;
            }
            if (simulationClock != null && simulationClock.IsPaused) return;
            float delta = Time.unscaledDeltaTime;
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0) return;
            elapsedRealSeconds = Mathf.Min(durationSeconds, elapsedRealSeconds + delta);
            int sequence = activeRequest.SequenceId;
            ApplyFrame();
            if (active && activeRequest.SequenceId == sequence && elapsedRealSeconds >= durationSeconds)
                CompleteActive(false);
        }

        private void OnPhaseChanged(PhaseChanged phase)
        {
            if (active && !CanPresent(activeRequest.Cinematic, phase.Current, gameState.Outcome)) CompleteActive(true);
        }

        private void OnMissionEnded(MissionEnded outcome)
        {
            // End the old shot before Core optionally requests an outcome-specific ending shot.
            CompleteActive(true);
        }

        private static bool CanPresent(CinematicId cinematic, GamePhase phase, MissionOutcome outcome)
        {
            if (outcome == MissionOutcome.Lost && cinematic != CinematicId.EndingPullback) return false;
            switch (cinematic)
            {
                case CinematicId.Impact:
                    return phase == GamePhase.Intro || phase == GamePhase.Emergency;
                case CinematicId.FalseSignal:
                    return phase == GamePhase.Repair || phase == GamePhase.Eva || phase == GamePhase.Transmission;
                case CinematicId.CommunicationRestored:
                    return phase == GamePhase.Transmission || phase == GamePhase.Survival
                        || (phase == GamePhase.Outcome && outcome == MissionOutcome.RecoveryPossible);
                case CinematicId.RecoveryArrival:
                case CinematicId.MarsDeparture:
                    return phase == GamePhase.Outcome && outcome == MissionOutcome.Recovered;
                case CinematicId.EndingPullback:
                    return phase == GamePhase.Outcome && outcome != MissionOutcome.None;
                default:
                    return false;
            }
        }

        private float DurationFor(CinematicId cinematic)
        {
            float value;
            switch (cinematic)
            {
                case CinematicId.Impact: value = impactSeconds; break;
                case CinematicId.FalseSignal: value = falseSignalSeconds; break;
                case CinematicId.CommunicationRestored: value = communicationRestoredSeconds; break;
                default: value = endingPullbackSeconds; break;
            }
            return float.IsNaN(value) || float.IsInfinity(value) ? 4f : Mathf.Clamp(value, 0.25f, MaximumDurationSeconds);
        }

        private void CaptureVisuals()
        {
            capturedPivot = cameraOffsetPivot;
            if (capturedPivot)
            {
                pivotLocalPosition = capturedPivot.localPosition;
                pivotLocalRotation = capturedPivot.localRotation;
                pivotWorldPosition = capturedPivot.position;
                pivotWorldRotation = capturedPivot.rotation;
            }
            hasEndingFrame = capturedPivot && endingFramingAnchor;
            if (hasEndingFrame)
            {
                endingWorldPosition = endingFramingAnchor.position;
                endingWorldRotation = endingFramingAnchor.rotation;
            }
            capturedBlackout = blackoutOverlay;
            capturedEmergency = emergencyOverlay;
            blackoutAlpha = capturedBlackout ? capturedBlackout.alpha : 0;
            emergencyAlpha = capturedEmergency ? capturedEmergency.alpha : 0;
            capturedLights.Clear();
            var uniqueLights = new HashSet<Light>();
            foreach (var light in effectLights ?? Array.Empty<Light>())
                if (light && uniqueLights.Add(light))
                    capturedLights.Add(new LightSnapshot { Light = light, Color = light.color, Intensity = light.intensity, Enabled = light.enabled });
        }

        private void ApplyFrame()
        {
            if (!active) return;
            float t = Mathf.Clamp01(elapsedRealSeconds / durationSeconds);
            RestoreVisualValues();
            string caption;
            switch (activeRequest.Cinematic)
            {
                case CinematicId.Impact:
                    ApplyImpact(t);
                    caption = "Trajectory deviation detected. Stabilize power and life support.";
                    break;
                case CinematicId.FalseSignal:
                    caption = t < 0.45f ? "Local communications buffer playback." : "Archived transmission. This is not a new Earth reply.";
                    break;
                case CinematicId.CommunicationRestored:
                    float glow = reducedEffects ? 0 : Mathf.Sin(t * Mathf.PI) * 0.12f;
                    foreach (var snapshot in capturedLights)
                        if (snapshot.Light) snapshot.Light.color = Color.Lerp(snapshot.Color, new Color(0.65f, 0.83f, 0.88f), glow);
                    caption = gameState.Outcome == MissionOutcome.RecoveryPossible
                        ? "Contact restored. Recovery planning is possible."
                        : "Communications channel restored.";
                    break;
                default:
                    if (hasEndingFrame && capturedPivot && !reducedEffects)
                        capturedPivot.SetPositionAndRotation(Vector3.Lerp(pivotWorldPosition, endingWorldPosition, Smooth(t)),
                            Quaternion.Slerp(pivotWorldRotation, endingWorldRotation, Smooth(t)));
                    caption = activeRequest.Cinematic == CinematicId.RecoveryArrival
                        ? "Recovery craft has arrived. Prepare for crew transfer."
                        : activeRequest.Cinematic == CinematicId.MarsDeparture
                        ? "LATER · ONWARD TO MARS"
                        : gameState.Outcome == MissionOutcome.Lost
                        ? "Mission lost. Review the mission report."
                        : "Contact restored. Recovery takes time.";
                    break;
            }
            SetCaption(caption);
        }

        private void ApplyImpact(float t)
        {
            float emergency = Smooth(Mathf.InverseLerp(0.16f, 0.65f, t));
            // One smooth dip, never an alternating flash pattern. Reduced mode removes the dip and motion.
            float dip = reducedEffects ? 0 : Smooth(Mathf.InverseLerp(0, 0.14f, t)) * (1 - Smooth(Mathf.InverseLerp(0.14f, 0.4f, t)));
            foreach (var snapshot in capturedLights)
            {
                if (!snapshot.Light) continue;
                snapshot.Light.intensity = snapshot.Intensity * (1 - 0.65f * dip) * (1 - emergency * (reducedEffects ? 0.12f : 0.3f));
                snapshot.Light.color = Color.Lerp(snapshot.Color, new Color(0.72f, 0.09f, 0.045f), emergency * (reducedEffects ? 0.22f : 0.65f));
            }
            if (capturedBlackout) capturedBlackout.alpha = Mathf.Lerp(blackoutAlpha, 1, dip * 0.7f);
            if (capturedEmergency) capturedEmergency.alpha = Mathf.Lerp(emergencyAlpha, 1, emergency * 0.12f);
            if (capturedPivot && !reducedEffects)
            {
                float envelope = Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI) * Mathf.Exp(-t * 5);
                float time = elapsedRealSeconds;
                capturedPivot.localPosition = pivotLocalPosition + new Vector3(Mathf.Sin(time * 31), Mathf.Sin(time * 23), 0) * (0.018f * envelope);
                capturedPivot.localRotation = pivotLocalRotation * Quaternion.Euler(Mathf.Sin(time * 27) * envelope, Mathf.Sin(time * 19) * 0.7f * envelope, 0);
            }
        }

        private static float Smooth(float value) => value * value * (3 - 2 * value);

        private void RestoreVisualValues()
        {
            if (capturedPivot) capturedPivot.SetLocalPositionAndRotation(pivotLocalPosition, pivotLocalRotation);
            foreach (var snapshot in capturedLights)
            {
                if (!snapshot.Light) continue;
                snapshot.Light.color = snapshot.Color;
                snapshot.Light.intensity = snapshot.Intensity;
                snapshot.Light.enabled = snapshot.Enabled;
            }
            if (capturedBlackout) capturedBlackout.alpha = blackoutAlpha;
            if (capturedEmergency) capturedEmergency.alpha = emergencyAlpha;
        }

        private void CompleteActive(bool skipped)
        {
            if (!active) return;
            var completion = new CinematicFinished(activeRequest.Cinematic, activeRequest.SequenceId, skipped);
            var completionBus = activeRequestBus;
            active = false;
            activeRequestBus = null;
            RestoreVisualValues();
            capturedPivot = null;
            capturedBlackout = null;
            capturedEmergency = null;
            capturedLights.Clear();
            hasEndingFrame = false;
            SetCaption(string.Empty);
            Notify(SequenceEnded, completion);
            PublishFinished(completionBus, completion);
        }

        private void SetCaption(string caption)
        {
            if (CurrentCaption == caption) return;
            CurrentCaption = caption;
            Notify(CaptionChanged, caption);
        }

        private void PublishFinished(IMissionEventBus targetBus, CinematicFinished completion)
        {
            // This is the only domain-facing publication. Never retry an acknowledgement after a listener error.
            try { targetBus?.Publish(completion); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void Notify<T>(Action<T> handlers, T message)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                try { handler(message); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
    }
}
