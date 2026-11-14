using System;
using UnityEngine;
using OutOfReach.Contracts;

namespace OutOfReach.Presentation.Audio
{
    /// <summary>
    /// Original boot/deck contact presentation for the explicitly selected grounded walking mode.
    /// Walking inside a microgravity spacecraft is a game simplification. Never represents EVA/free-float sound.
    /// Reads the existing controller after movement; owns no input, movement, resources, stamina or AudioSource.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FootstepAudioPresenter : MonoBehaviour
    {
        [SerializeField] private AudioClip[] deckSteps = Array.Empty<AudioClip>();
        [Min(.2f)] [SerializeField] private float metresPerStep = 1.1f;
        [Range(0, 1)] [SerializeField] private float stepGain = .55f;

        private CharacterController controller;
        private IGameState state;
        private ISimulationClock clock;
        private IEvaStatus evaStatus;
        private Func<bool> canPresentGroundedSteps;
        private Func<AudioClip, float, bool> playContact;
        private readonly FootstepDistanceTracker distance = new FootstepDistanceTracker();
        private Vector3 previousPosition;
        private bool hasPosition;
        private int nextVariation;

        public bool IsBound => controller && state != null && clock != null && evaStatus != null
            && canPresentGroundedSteps != null && playContact != null;
        public int PlayedStepCount { get; private set; }

        /// <summary>
        /// Required predicate must explicitly select grounded mode AND allow player control,
        /// excluding menu, rest and blocking cinematics. Unknown/missing mode permission is silent.
        /// playContact normally delegates to the single MissionAudioPresenter.PlaySfx owner.
        /// </summary>
        public void Configure(CharacterController body, IGameState gameState, ISimulationClock simulationClock,
            IEvaStatus eva, Func<bool> groundedModeAndControlAllowed, Func<AudioClip, float, bool> playContactCue)
        {
            ClearBinding();
            controller = body;
            state = gameState;
            clock = simulationClock;
            evaStatus = eva;
            canPresentGroundedSteps = groundedModeAndControlAllowed;
            playContact = playContactCue;
        }

        public void ConfigureClips(AudioClip[] originalDeckSteps)
        {
            deckSteps = originalDeckSteps == null ? Array.Empty<AudioClip>() : (AudioClip[])originalDeckSteps.Clone();
            ResetTracking();
        }

        public void ClearBinding()
        {
            controller = null;
            state = null;
            clock = null;
            evaStatus = null;
            canPresentGroundedSteps = null;
            playContact = null;
            PlayedStepCount = 0;
            nextVariation = 0;
            ResetTracking();
        }

        private void OnEnable() => ResetTracking();
        private void OnDisable() => ClearBinding();

        /// <summary>Root may call after an explicit teleport; automatic displacement checks also reject jumps.</summary>
        public void ResetTracking()
        {
            hasPosition = false;
            distance.Reset();
        }

        private void LateUpdate()
        {
            if (!IsBound || !controller.enabled || !controller.gameObject.activeInHierarchy)
            {
                ResetTracking();
                return;
            }

            Vector3 position = controller.transform.position;
            Vector3 travelled = position - previousPosition;
            previousPosition = position;
            if (!hasPosition)
            {
                hasPosition = true;
                return;
            }

            bool eligible = !clock.IsPaused && !AudioListener.pause && Time.timeScale > 0f
                && state.Outcome == MissionOutcome.None && IsWalkingPhase(state.Phase)
                && !evaStatus.IsOutside && canPresentGroundedSteps() && controller.isGrounded;
            travelled.y = 0;
            Vector3 velocity = controller.velocity;
            velocity.y = 0;
            float motionAgreement = Vector3.Dot(travelled, velocity);
            if (!distance.Sample(eligible, Time.unscaledDeltaTime, travelled.magnitude,
                velocity.magnitude, motionAgreement, metresPerStep)) return;

            if (deckSteps == null || deckSteps.Length == 0) return;
            AudioClip clip = deckSteps[nextVariation % deckSteps.Length];
            if (clip && playContact(clip, Mathf.Clamp01(stepGain)))
            {
                nextVariation = (nextVariation + 1) % deckSteps.Length;
                PlayedStepCount++;
            }
        }

        internal static bool IsWalkingPhase(GamePhase phase)
        {
            return phase == GamePhase.Planning || phase == GamePhase.Intro || phase == GamePhase.Emergency
                || phase == GamePhase.Repair || phase == GamePhase.Transmission || phase == GamePhase.Survival;
        }
    }

    // Pure distance accumulator, kept alongside the adapter so cadence can be checked without running Unity.
    internal sealed class FootstepDistanceTracker
    {
        private float accumulatedMetres;
        public void Reset() => accumulatedMetres = 0f;

        public bool Sample(bool eligible, float realSeconds, float actualMetres, float velocityMetresPerSecond,
            float motionAgreement, float stepMetres)
        {
            // Reject hitches/teleports, blocked input and movement not explained by CharacterController.Move.
            if (!eligible || !Finite(realSeconds) || realSeconds <= 0 || realSeconds > .25f
                || !Finite(actualMetres) || actualMetres <= .0001f || actualMetres > .6f
                || !Finite(velocityMetresPerSecond) || velocityMetresPerSecond < .08f
                || !Finite(motionAgreement) || motionAgreement <= 0
                || actualMetres > velocityMetresPerSecond * realSeconds * 1.35f + .01f
                || !Finite(stepMetres) || stepMetres < .2f)
            {
                Reset();
                return false;
            }

            accumulatedMetres += actualMetres;
            if (accumulatedMetres + .00001f < stepMetres) return false;
            // Emit at most one contact per frame. Never replay a backlog after suppression or a hitch.
            accumulatedMetres = Math.Max(0f, accumulatedMetres - stepMetres) % stepMetres;
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
