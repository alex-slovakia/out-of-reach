using System;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Motion
{
    /// <summary>
    /// Optional millimetre-scale visual walk motion. Reads actual controller movement and read-only mission
    /// contracts; owns only the explicitly supplied visual offsets. Never moves the actor or drives gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(120)]
    public sealed class FirstPersonMotionPresenter : MonoBehaviour
    {
        [Header("Comfort")]
        [SerializeField] private bool motionEnabled = true;
        [Tooltip("Suppresses all walk motion, including optional hands; the view stays at its authored neutral pose.")]
        [SerializeField] private bool reducedMotion;

        [Header("Very small camera offsets; metres / degrees")]
        [SerializeField, Range(0f, .008f)] private float cameraVerticalMetres = .006f;
        [SerializeField, Range(0f, .005f)] private float cameraLateralMetres = .0015f;
        [SerializeField, Range(0f, .15f)] private float cameraRollDegrees = .06f;

        [Header("Gentle directional response; visual only")]
        [SerializeField] private bool strafeLeanEnabled = true;
        [SerializeField] private bool turnResponseEnabled = true;
        [SerializeField, Range(0f, .85f)] private float strafeLeanDegrees = .65f;
        [SerializeField, Range(0f, .4f)] private float turnResponseDegrees = .3f;
        [Tooltip("Actual parent yaw speed for full visual turn lag; never changes look sensitivity.")]
        [SerializeField, Range(30f, 360f)] private float fullTurnResponseAtDegreesPerSecond = 150f;
        [SerializeField, Range(5f, 24f)] private float directionalResponsePerSecond = 10f;

        [Header("Optional existing hand visuals; no hands are created")]
        [SerializeField, Range(0f, .015f)] private float handsVerticalMetres = .004f;
        [SerializeField, Range(0f, .015f)] private float handsLateralMetres = .003f;
        [SerializeField, Range(0f, .75f)] private float handsRollDegrees = .25f;

        [Header("Visual response only; these never change walking speed")]
        [Tooltip("Travel distance for one left/right motion cycle. Phase advances only from actual grounded travel.")]
        [SerializeField, Range(1f, 3f)] private float cycleDistanceMetres = 2f;
        [Tooltip("Actual speed that reaches the small configured visual amplitude. Not a player movement setting.")]
        [SerializeField, Min(.1f)] private float fullAmplitudeAtMetresPerSecond = 2.5f;
        [SerializeField, Range(5f, 24f)] private float responsePerSecond = 14f;

        private CharacterController body;
        private Transform cameraPivot;
        private Transform handsPivot;
        private IGameState gameState;
        private ISimulationClock clock;
        private IEvaStatus eva;
        private Func<bool> viewControlAllowed;
        private bool wasEligibleLastFrame;
        private bool bound;
        // Fail closed until composition explicitly supplies its combined menu/cinematic suppression state.
        private bool suppressed = true;
        private bool hasPreviousPosition;
        private Vector3 previousPosition;
        private Vector3 previousHeading;
        private bool hasPreviousHeading;
        private Vector3 cameraNeutralPosition, handsNeutralPosition;
        private Quaternion cameraNeutralRotation, handsNeutralRotation;
        private Vector3 cameraOffset, handsOffset;
        private float cameraRoll, handsRoll, phaseRadians;
        private float strafeRoll, turnYaw;

        public bool IsBound => bound;
        public bool IsSuppressed => suppressed;
        public bool MotionEnabled => motionEnabled;
        public bool ReducedMotion => reducedMotion;
        public bool StrafeLeanEnabled => strafeLeanEnabled;
        public bool TurnResponseEnabled => turnResponseEnabled;

        public void SetStrafeLeanEnabled(bool value) { strafeLeanEnabled = value; ResetToNeutral(); }
        public void SetTurnResponseEnabled(bool value) { turnResponseEnabled = value; ResetToNeutral(); }

        /// <summary>
        /// Integrator supplies dedicated offsets below the existing look/planning/cinematic pivots. Neither
        /// offset may be a gameplay transform, camera itself, or physics-bearing object. The optional hands
        /// offset may sit beneath the existing camera; it must be separate from the camera-motion offset.
        /// Configure restores the previous binding before capturing the newly supplied authored neutral poses.
        /// isViewControlAllowed is a read-only query supplied by composition. It must include the actual player
        /// control gate, route state and cinematic authority; it is evaluated here after cinematic LateUpdate.
        /// Configure starts suppressed; composition releases suppression only after completing session wiring.
        /// </summary>
        public void Configure(CharacterController actor, Transform cameraMotionPivot,
            IGameState state, ISimulationClock simulationClock, IEvaStatus evaStatus,
            Func<bool> isViewControlAllowed,
            Transform handsMotionPivot = null)
        {
            if (!actor) throw new ArgumentNullException(nameof(actor));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (simulationClock == null) throw new ArgumentNullException(nameof(simulationClock));
            if (evaStatus == null) throw new ArgumentNullException(nameof(evaStatus));
            if (isViewControlAllowed == null) throw new ArgumentNullException(nameof(isViewControlAllowed));
            ValidateVisualPivot(actor, cameraMotionPivot, nameof(cameraMotionPivot));
            if (handsMotionPivot)
            {
                ValidateVisualPivot(actor, handsMotionPivot, nameof(handsMotionPivot));
                if (handsMotionPivot == cameraMotionPivot || cameraMotionPivot.IsChildOf(handsMotionPivot))
                    throw new ArgumentException("Hand offset must be separate and must not be an ancestor of the camera offset.", nameof(handsMotionPivot));
            }

            ClearBinding();
            body = actor;
            cameraPivot = cameraMotionPivot;
            handsPivot = handsMotionPivot;
            gameState = state;
            clock = simulationClock;
            eva = evaStatus;
            viewControlAllowed = isViewControlAllowed;
            cameraNeutralPosition = cameraPivot.localPosition;
            cameraNeutralRotation = cameraPivot.localRotation;
            if (handsPivot)
            {
                handsNeutralPosition = handsPivot.localPosition;
                handsNeutralRotation = handsPivot.localRotation;
            }
            bound = true;
            ResetToNeutral();
        }

        /// <summary>Menu setting. Disabling restores the exact authored pose immediately.</summary>
        public void SetMotionEnabled(bool enabled)
        {
            motionEnabled = enabled;
            if (!enabled) ResetToNeutral();
        }

        /// <summary>Comfort setting: reduced motion means no head or hand walk motion.</summary>
        public void SetReducedMotion(bool reduced)
        {
            reducedMotion = reduced;
            if (reduced) ResetToNeutral();
        }

        /// <summary>
        /// Optional explicit presentation suppression. True resets immediately; false resumes only when the
        /// injected view-control query also permits it. Neither caller can clear the other gate.
        /// </summary>
        public void SetSuppressed(bool value)
        {
            if (suppressed == value) return;
            suppressed = value;
            wasEligibleLastFrame = false;
            ResetToNeutral();
        }

        /// <summary>Call after a deliberate teleport or rig reframing. Does not alter the actor or other pivots.</summary>
        public void ResetToNeutral()
        {
            cameraOffset = handsOffset = Vector3.zero;
            cameraRoll = handsRoll = phaseRadians = strafeRoll = turnYaw = 0f;
            if (bound)
            {
                if (cameraPivot) cameraPivot.SetLocalPositionAndRotation(cameraNeutralPosition, cameraNeutralRotation);
                if (handsPivot) handsPivot.SetLocalPositionAndRotation(handsNeutralPosition, handsNeutralRotation);
            }
            SamplePosition();
        }

        /// <summary>Restores owned poses and drops all session references. No subscriptions are retained.</summary>
        public void ClearBinding()
        {
            ResetToNeutral();
            bound = false;
            suppressed = true;
            wasEligibleLastFrame = false;
            viewControlAllowed = null;
            body = null;
            cameraPivot = handsPivot = null;
            gameState = null;
            clock = null;
            eva = null;
            hasPreviousPosition = hasPreviousHeading = false;
        }

        private void OnEnable()
        {
            wasEligibleLastFrame = false;
            ResetToNeutral();
        }

        private void OnDisable()
        {
            wasEligibleLastFrame = false;
            ResetToNeutral();
        }
        private void OnDestroy() => ClearBinding();

        private void LateUpdate()
        {
            if (!bound) return;
            if (!body || !cameraPivot)
            {
                ClearBinding();
                return;
            }
            Vector3 position = body.transform.position;
            Vector3 displacement = hasPreviousPosition ? position - previousPosition : Vector3.zero;
            previousPosition = position;
            hasPreviousPosition = true;
            // Sample the existing parent, never this owned offset: visual turn lag cannot feed itself.
            Vector3 heading = ViewHeading();
            float yawDelta = hasPreviousHeading && heading.sqrMagnitude > .001f
                ? Vector3.SignedAngle(previousHeading, heading, Vector3.up) : 0f;
            previousHeading = heading;
            hasPreviousHeading = heading.sqrMagnitude > .001f;

            // No idle breathing, footstep bob in a suit/EVA, paused animation, or motion over menu/cinematics.
            if (!motionEnabled || reducedMotion || suppressed || clock.IsPaused || !body.enabled
                || !body.gameObject.activeInHierarchy || !IsWalkingPhase(gameState.Phase)
                || eva.IsOutside || eva.IsSuitSealed || viewControlAllowed == null || !viewControlAllowed())
            {
                wasEligibleLastFrame = false;
                ResetToNeutral();
                return;
            }
            if (!wasEligibleLastFrame)
            {
                // A cinematic may release control after this frame's player Update. Discard its old velocity
                // and displacement before a fresh walking sample; resume/rebind never manufactures a step.
                wasEligibleLastFrame = true;
                ResetToNeutral();
                return;
            }

            float frameSeconds = Time.deltaTime; // Same time basis used by the actual CharacterController adapter.
            float visualSeconds = Time.unscaledDeltaTime;
            if (!IsFinite(frameSeconds) || !IsFinite(visualSeconds)
                || frameSeconds <= 0f || visualSeconds <= 0f || visualSeconds > .15f
                || !IsFinite(yawDelta) || Mathf.Abs(yawDelta) > 35f)
            {
                // A hitch, suspended frame or large single-frame heading snap must not produce catch-up motion.
                ResetToNeutral();
                return;
            }
            Vector3 planarTravel = Vector3.ProjectOnPlane(displacement, Vector3.up);
            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.velocity, Vector3.up);
            float distance = planarTravel.magnitude;
            float velocitySpeed = planarVelocity.magnitude;
            Vector3 controllerTravel = planarVelocity * frameSeconds;
            float mismatch = (planarTravel - controllerTravel).magnitude;
            float travelTolerance = Mathf.Max(.002f, controllerTravel.magnitude * .25f);
            if (!IsFinite(distance) || !IsFinite(velocitySpeed) || !IsFinite(mismatch) || mismatch > travelTolerance)
            {
                ResetToNeutral(); // Teleport, scene replacement or invalid sample; never invent a footfall.
                return;
            }

            // Both displacement and controller velocity must confirm real travel. Pressing into a wall or
            // rotating on the spot cannot run a walk loop. Airborne motion eases to neutral without new phase.
            float actualSpeed = Mathf.Min(distance / frameSeconds, velocitySpeed);
            bool walking = body.isGrounded && actualSpeed > .05f && distance > .00001f;
            float strength = walking ? Mathf.Clamp01(actualSpeed / Mathf.Max(.1f, fullAmplitudeAtMetresPerSecond)) : 0f;
            if (walking)
                phaseRadians = Mathf.Repeat(phaseRadians + distance / Mathf.Max(1f, cycleDistanceMetres) * Mathf.PI * 2f, Mathf.PI * 2f);
            float side = Mathf.Sin(phaseRadians);
            float rise = Mathf.Sin(phaseRadians * 2f);
            Vector3 cameraTarget = new Vector3(side * Mathf.Clamp(cameraLateralMetres, 0f, .005f),
                rise * Mathf.Clamp(cameraVerticalMetres, 0f, .008f), 0f) * strength;
            Vector3 handsTarget = new Vector3(-side * Mathf.Clamp(handsLateralMetres, 0f, .015f),
                -rise * Mathf.Clamp(handsVerticalMetres, 0f, .015f), 0f) * strength;
            float blend = 1f - Mathf.Exp(-Mathf.Clamp(responsePerSecond, 5f, 24f) * visualSeconds);
            cameraOffset = Vector3.Lerp(cameraOffset, cameraTarget, blend);
            handsOffset = Vector3.Lerp(handsOffset, handsTarget, blend);
            cameraRoll = Mathf.Lerp(cameraRoll, -side * Mathf.Clamp(cameraRollDegrees, 0f, .15f) * strength, blend);
            handsRoll = Mathf.Lerp(handsRoll, side * Mathf.Clamp(handsRollDegrees, 0f, .75f) * strength, blend);

            // Lean follows confirmed sideways travel, not held keys. Turning can respond while standing,
            // but never while airborne. Both stop at the same authoritative presentation gates above.
            float lateralSpeed = Vector3.Dot(planarVelocity, body.transform.right);
            float strafeTarget = strafeLeanEnabled && walking
                ? -Mathf.Clamp(lateralSpeed / Mathf.Max(.1f, fullAmplitudeAtMetresPerSecond), -1f, 1f)
                    * Mathf.Clamp(strafeLeanDegrees, 0f, .85f) : 0f;
            float yawSpeed = yawDelta / visualSeconds;
            bool turning = turnResponseEnabled && body.isGrounded && Mathf.Abs(yawSpeed) > .5f;
            float turnTarget = turning
                ? -Mathf.Clamp(yawSpeed / Mathf.Clamp(fullTurnResponseAtDegreesPerSecond, 30f, 360f), -1f, 1f)
                    * Mathf.Clamp(turnResponseDegrees, 0f, .4f) : 0f;
            float directionalBlend = 1f - Mathf.Exp(-Mathf.Clamp(directionalResponsePerSecond, 5f, 24f) * visualSeconds);
            strafeRoll = Mathf.Lerp(strafeRoll, strafeTarget, directionalBlend);
            turnYaw = Mathf.Lerp(turnYaw, turnTarget, directionalBlend);
            if (!walking && !turning && cameraOffset.sqrMagnitude < 1e-10f && handsOffset.sqrMagnitude < 1e-10f
                && Mathf.Abs(cameraRoll) < .001f && Mathf.Abs(handsRoll) < .001f
                && Mathf.Abs(strafeRoll) < .001f && Mathf.Abs(turnYaw) < .001f)
            {
                ResetToNeutral();
                return;
            }
            // Combined yaw/roll stays within one degree, including gait roll, even with maximum tunings.
            Vector2 cameraAngles = Vector2.ClampMagnitude(new Vector2(turnYaw, cameraRoll + strafeRoll), 1f);
            cameraPivot.SetLocalPositionAndRotation(cameraNeutralPosition + cameraOffset,
                cameraNeutralRotation * Quaternion.Euler(0f, cameraAngles.x, cameraAngles.y));
            if (handsPivot)
                handsPivot.SetLocalPositionAndRotation(handsNeutralPosition + handsOffset,
                    handsNeutralRotation * Quaternion.AngleAxis(handsRoll, Vector3.forward));
        }

        private void SamplePosition()
        {
            hasPreviousPosition = body;
            if (body) previousPosition = body.transform.position;
            previousHeading = ViewHeading();
            hasPreviousHeading = previousHeading.sqrMagnitude > .001f;
        }

        private Vector3 ViewHeading() => cameraPivot && cameraPivot.parent
            ? Vector3.ProjectOnPlane(cameraPivot.parent.forward, Vector3.up) : Vector3.zero;

        private static bool IsWalkingPhase(GamePhase phase) => phase == GamePhase.Planning || phase == GamePhase.Intro
            || phase == GamePhase.Emergency || phase == GamePhase.Repair || phase == GamePhase.Transmission;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ValidateVisualPivot(CharacterController actor, Transform pivot, string argument)
        {
            if (!pivot) throw new ArgumentNullException(argument);
            if (pivot == actor.transform || !pivot.IsChildOf(actor.transform))
                throw new ArgumentException("Visual offset must be a dedicated child of the actor, never its body or an external gameplay transform.", argument);
            if (pivot.GetComponent<Camera>() || pivot.GetComponent<AudioListener>() || pivot.GetComponent<Collider>() || pivot.GetComponent<Rigidbody>())
                throw new ArgumentException("Supply an empty visual offset, not the camera/listener or a physics object.", argument);
        }
    }
}
