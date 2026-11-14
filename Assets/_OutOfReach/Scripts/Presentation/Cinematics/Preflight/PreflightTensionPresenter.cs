using System;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Cinematics.Preflight
{
    /// <summary>
    /// Optional timed-preflight sound decisions from Core's remaining active real seconds.
    /// Playback/mix ownership stays with explicitly injected callbacks to the existing audio presenter.
    /// No clock, AudioSource, phase transition, loading progress or gameplay command is owned here.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(130)]
    public sealed class PreflightTensionPresenter : MonoBehaviour
    {
        [Tooltip("Explicit existing short SFX clip; planning_tick.wav is the proposed original 0.115-second cue.")]
        [SerializeField] private AudioClip countdownTick;
        [SerializeField, Range(0f, 1f)] private float tickVolume = .2f;
        [SerializeField, Range(0f, 1f)] private float reducedTickVolume = .08f;
        [SerializeField] private bool reducedEffects;

        private IPlanningDeadline deadline;
        private IGameState state;
        private ISimulationClock clock;
        private Func<bool> timedPreflightAllowed;
        private Func<AudioClip, float, bool> playTick;
        private Func<bool> requestPulse;
        private Action releasePulse;
        private readonly PreflightTensionLogic logic = new PreflightTensionLogic();
        private bool bound;
        private bool pulseRequested;

        public bool IsBound => bound;
        public bool PulseRequested => pulseRequested;
        public float Urgency01 { get; private set; }
        /// <summary>Observed bucket, not a countdown. Missing/muted/rejected clips are never queued for retry.</summary>
        public int LastCueSecond { get; private set; }
        /// <summary>Accepted playback requests, not evidence that the player's speakers were audible.</summary>
        public int AcceptedCueCount { get; private set; }

        public void ConfigureCue(AudioClip existingShortTick) => countdownTick = existingShortTick;

        /// <summary>
        /// Query must permit only the chosen timed physical-loading mode for this exact bound session.
        /// Pulse callbacks request/release one owned music transition; they must respect the existing mix owner.
        /// A null pulse callback pair disables score changes while retaining optional tick presentation.
        /// </summary>
        public void Configure(IPlanningDeadline planningDeadline, IGameState gameState,
            ISimulationClock simulationClock, Func<bool> isTimedPreflightAllowed,
            Func<AudioClip, float, bool> playExistingTick,
            Func<bool> requestExistingTensionPulse = null, Action releaseOwnedTensionPulse = null)
        {
            ClearBinding();
            if (planningDeadline == null || gameState == null || simulationClock == null
                || isTimedPreflightAllowed == null || playExistingTick == null)
                throw new ArgumentNullException("Preflight tension requires explicit current state, eligibility and playback callbacks.");
            if ((requestExistingTensionPulse == null) != (releaseOwnedTensionPulse == null))
                throw new ArgumentException("Supply both pulse callbacks or neither.");
            deadline = planningDeadline;
            state = gameState;
            clock = simulationClock;
            timedPreflightAllowed = isTimedPreflightAllowed;
            playTick = playExistingTick;
            requestPulse = requestExistingTensionPulse;
            releasePulse = releaseOwnedTensionPulse;
            bound = true;
        }

        public void SetReducedEffects(bool reduced)
        {
            if (reducedEffects == reduced) return;
            reducedEffects = reduced;
            RefreshPresentation();
        }

        /// <summary>Safe immediate refresh after mode/phase changes; normal sampling happens after gameplay Update.</summary>
        public void RefreshPresentation()
        {
            if (!bound || !isActiveAndEnabled)
            {
                Urgency01 = 0f;
                ReleaseOwnedPulse();
                return;
            }
            // Phase exit releases our claim even if an external transition happened during a pause.
            if (state.Phase != GamePhase.Planning || !deadline.IsEnabled)
            {
                Urgency01 = 0f;
                ReleaseOwnedPulse();
                return;
            }
            // The existing audio owner freezes sources. Keep our claim/watermark; resuming cannot replay a bucket.
            if (clock.IsPaused || AudioListener.pause) return;
            PreflightTensionFrame frame = logic.Sample(state.Phase, timedPreflightAllowed(),
                deadline.IsEnabled, false, deadline.RemainingRealSeconds, reducedEffects);
            Urgency01 = frame.Urgency01;
            if (!frame.IsActive)
            {
                ReleaseOwnedPulse();
                return;
            }
            if (frame.WantsPulse)
            {
                if (!pulseRequested && requestPulse != null) pulseRequested = requestPulse();
            }
            else ReleaseOwnedPulse();

            if (frame.CueSecond == 0) return;
            LastCueSecond = frame.CueSecond;
            float gain = reducedEffects ? reducedTickVolume : tickVolume;
            if (countdownTick && playTick(countdownTick, Mathf.Clamp01(gain))) AcceptedCueCount++;
        }

        public void ClearBinding()
        {
            ReleaseOwnedPulse(); // Runs only if our request succeeded; never resets untouched music.
            bound = false;
            deadline = null;
            state = null;
            clock = null;
            timedPreflightAllowed = null;
            playTick = null;
            requestPulse = null;
            releasePulse = null;
            logic.Reset();
            Urgency01 = 0f;
            LastCueSecond = AcceptedCueCount = 0;
        }

        private void LateUpdate() => RefreshPresentation();
        private void OnDisable() { Urgency01 = 0f; ReleaseOwnedPulse(); }
        private void OnDestroy() => ClearBinding();

        private void ReleaseOwnedPulse()
        {
            if (!pulseRequested) return;
            pulseRequested = false;
            releasePulse?.Invoke();
        }
    }
}
