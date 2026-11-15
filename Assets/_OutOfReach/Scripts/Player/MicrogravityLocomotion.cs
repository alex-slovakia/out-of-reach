using System;
using UnityEngine;

namespace OutOfReach.Player
{
    /// <summary>Outcome of a floating command. Every refusal leaves position, velocity and grip unchanged.</summary>
    public enum FloatActionResult
    {
        Done,
        NotFloating,
        ControlDenied,
        InvalidInput,
        NoSurface,
        OutOfReach,
        Occluded,
        IntoSurface,
        Cooldown,
        NotAHandhold,
        AlreadyHolding,
        NotHolding,
        OnRoute,
    }

    /// <summary>
    /// Tuning and exact math of the optional microgravity mode of <see cref="FirstPersonController"/>, which stays the
    /// only movement authority: this class holds plain values and pure functions, and the controller does the
    /// collision queries and moves the body. Every value is GAME TUNING, not measured astronaut performance.
    /// There is no thrust and no hidden force: speed only comes from pushes off verified solid surfaces, by hand
    /// within reach or by foot from an actual contact, and with the default zero drag a push carries the body on
    /// until it meets a surface.
    /// </summary>
    [Serializable]
    public sealed class MicrogravityLocomotion
    {
        [Tooltip("Hand reach from the eye point, metres. Entry, hand push, brace and grip targets must be within it; a grip holds the body within it.")]
        [SerializeField, Min(0.1f)] private float reachMetres = 0.85f;

        [Tooltip("How far below the capsule's bottom (beyond the skin width) a surface still counts as touching the feet, metres.")]
        [SerializeField, Min(0.005f)] private float footContactMetres = 0.05f;

        [Tooltip("Foot support must face up: minimum dot product of the contact normal with the body's up axis (side walls and bare edges do not count).")]
        [SerializeField, Range(0f, 1f)] private float minimumFootSupportDot = 0.8f;

        // MOVE-MECH-01 tuning (game values, not measured astronaut performance): one medium hand push of strength
        // 0.55 gives 0.825 m/s, a paired or leg push gives one full push of 1.5 m/s, the drift is capped at 2.2 m/s.
        [Tooltip("Largest speed change one full push gives, metres per second.")]
        [SerializeField, Min(0f)] private float maxPushSpeed = 1.5f;

        [Tooltip("Speed cap while floating, metres per second.")]
        [SerializeField, Min(0.1f)] private float maxSpeed = 2.2f;

        [Tooltip("Minimum time between pushes (hand or foot), seconds, so a held button cannot accelerate continuously.")]
        [SerializeField, Min(0f)] private float pushCooldownSeconds = 0.25f;

        [Tooltip("Largest view lean (roll) while floating, degrees. A and D only tilt the view: no thrust, no push intent.")]
        [SerializeField, Range(0f, 60f)] private float maxLeanDegrees = 30f;

        [Tooltip("How fast the view leans while A or D is held, degrees per second.")]
        [SerializeField, Min(1f)] private float leanDegreesPerSecond = 80f;

        [Tooltip("How fast the view returns level after A or D is released or float ends, degrees per second.")]
        [SerializeField, Min(1f)] private float leanReturnDegreesPerSecond = 120f;

        [Tooltip("Exponential damping per second. Default 0: no hidden force, a push carries on until a surface. Any value above 0 is a disclosed gameplay compromise.")]
        [SerializeField, Min(0f)] private float dragPerSecond;

        [Tooltip("Time constant with which a gripped handhold stops the body, seconds (the arms absorb the motion).")]
        [SerializeField, Min(0.01f)] private float gripSettleSeconds = 0.15f;

        [Tooltip("A push must point away from the surface: minimum dot product with the surface normal.")]
        [SerializeField, Range(0f, 1f)] private float minimumAwayDot = 0.2f;

        [Tooltip("Diagnostic only: stranded after this long with no surface within reach and almost no speed, seconds.")]
        [SerializeField, Min(0f)] private float strandedSeconds = 3f;

        [Tooltip("Diagnostic only: stranded below this speed, metres per second.")]
        [SerializeField, Min(0f)] private float strandedSpeed = 0.05f;

        public float ReachMetres => reachMetres;

        public float FootContactMetres => footContactMetres;

        public float MinimumFootSupportDot => minimumFootSupportDot;

        public float MaxPushSpeed => maxPushSpeed;

        public float MaxSpeed => maxSpeed;

        public float PushCooldownSeconds => pushCooldownSeconds;

        public float DragPerSecond => dragPerSecond;

        public float GripSettleSeconds => gripSettleSeconds;

        public float MinimumAwayDot => minimumAwayDot;

        public float StrandedSeconds => strandedSeconds;

        public float StrandedSpeed => strandedSpeed;

        public float MaxLeanDegrees => maxLeanDegrees;

        /// <summary>
        /// The next view lean after one frame, degrees; positive leans left (counter-clockwise as seen by the player).
        /// A held lean input (-1 = A, +1 = D) moves towards its lean at the lean rate; no input returns level at the
        /// return rate. Pure orientation: it never changes velocity or position.
        /// </summary>
        public float StepLean(float currentDegrees, float leanInput, float deltaSeconds)
        {
            if (float.IsNaN(currentDegrees) || float.IsInfinity(currentDegrees))
            {
                return 0f;
            }

            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            {
                return currentDegrees;
            }

            float input = float.IsNaN(leanInput) ? 0f : Mathf.Clamp(leanInput, -1f, 1f);
            bool held = Mathf.Abs(input) >= 0.1f;
            float target = held ? -input * maxLeanDegrees : 0f;
            float rate = held ? leanDegreesPerSecond : leanReturnDegreesPerSecond;
            return Mathf.MoveTowards(Mathf.Clamp(currentDegrees, -maxLeanDegrees, maxLeanDegrees), target, rate * deltaSeconds);
        }

        /// <summary>
        /// Direction of a push that should send the body along a travel intent off a surface with this normal. An intent
        /// already pointing away from the surface by at least the away cone (the larger of 0.25 and the minimum away dot
        /// plus 0.025) is kept; any other intent keeps its direction along the surface and leaves at the cone, blending
        /// smoothly towards the normal for an intent almost straight into the surface. Pure math: force magnitude and the
        /// verified contact stay with the controller.
        /// </summary>
        public static bool TryOutwardDirection(Vector3 surfaceNormal, Vector3 travelIntent, float minimumAwayDot, out Vector3 direction)
        {
            direction = Vector3.zero;
            float normalSquared = surfaceNormal.sqrMagnitude, intentSquared = travelIntent.sqrMagnitude;
            if (!IsFinite(surfaceNormal) || !IsFinite(travelIntent) || float.IsNaN(minimumAwayDot) || float.IsInfinity(minimumAwayDot)
                || float.IsNaN(normalSquared) || float.IsInfinity(normalSquared) || float.IsNaN(intentSquared) || float.IsInfinity(intentSquared)
                || normalSquared < 0.0001f || intentSquared < 0.0001f)
            {
                return false;
            }

            Vector3 normal = surfaceNormal / Mathf.Sqrt(normalSquared), intent = travelIntent / Mathf.Sqrt(intentSquared);
            float away = Mathf.Clamp(Mathf.Max(0.25f, minimumAwayDot + 0.025f), 0f, 1f);
            float outward = Vector3.Dot(intent, normal);
            if (outward >= away)
            {
                direction = intent;
                return true;
            }

            Vector3 tangent = intent - (normal * outward);
            float tangentLength = tangent.magnitude;
            if (tangentLength == 0f)
            {
                direction = normal;
                return true;
            }

            Vector3 tangentDirection = tangent / tangentLength;
            if (tangentLength >= 0.25f)
            {
                direction = (normal * away) + (tangentDirection * Mathf.Sqrt(1f - (away * away)));
            }
            else
            {
                // A hard 0.25 cutoff would jump 75.52 degrees at the default cone: blend the angle instead.
                float ratio = Mathf.Clamp01(tangentLength / 0.25f);
                float angle = ratio * ratio * (3f - (2f * ratio)) * Mathf.Acos(away);
                direction = (normal * Mathf.Cos(angle)) + (tangentDirection * Mathf.Sin(angle));
            }

            return true;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        /// <summary>The velocity after one push along a direction: capped per push and in total.</summary>
        public Vector3 Push(Vector3 velocity, Vector3 direction, float strength01)
        {
            if (direction.sqrMagnitude < 1e-8f || float.IsNaN(strength01))
            {
                return velocity;
            }

            Vector3 pushed = velocity + (direction.normalized * (maxPushSpeed * Mathf.Clamp01(strength01)));
            return Vector3.ClampMagnitude(pushed, maxSpeed);
        }

        /// <summary>
        /// The velocity after one simultaneous two-hand push: each hand contributes like a single push (capped by its
        /// strength), the combined change is capped to one full push (<see cref="MaxPushSpeed"/>) and the speed to
        /// <see cref="MaxSpeed"/>. Two hands are stronger than one partial push, never stronger than one full push.
        /// </summary>
        public Vector3 PushPair(Vector3 velocity, Vector3 directionA, float strengthA, Vector3 directionB, float strengthB)
        {
            Vector3 change = Vector3.ClampMagnitude(Contribution(directionA, strengthA) + Contribution(directionB, strengthB), maxPushSpeed);
            return Vector3.ClampMagnitude(velocity + change, maxSpeed);
        }

        private Vector3 Contribution(Vector3 direction, float strength01)
        {
            return direction.sqrMagnitude < 1e-8f || float.IsNaN(strength01)
                ? Vector3.zero
                : direction.normalized * (maxPushSpeed * Mathf.Clamp01(strength01));
        }

        /// <summary>
        /// Exact displacement over one interval, and the velocity at its end, for the combined exponential decay of
        /// drag and (while gripping) the arm's settling. The closed form makes a free drift and a grip settle cover the
        /// same path at 30 and at 240 frames per second.
        /// </summary>
        public Vector3 Integrate(ref Vector3 velocity, float deltaSeconds, bool gripped = false)
        {
            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds))
            {
                return Vector3.zero;
            }

            float rate = dragPerSecond + (gripped ? 1f / gripSettleSeconds : 0f);
            if (rate <= 1e-6f)
            {
                return velocity * deltaSeconds;
            }

            float decay = Mathf.Exp(-rate * deltaSeconds);
            Vector3 displacement = velocity * ((1f - decay) / rate);
            velocity *= decay;
            return displacement;
        }

        /// <summary>Removes the part of the velocity that points into a surface: an inelastic contact that keeps sliding.</summary>
        public static Vector3 RemoveInto(Vector3 velocity, Vector3 surfaceNormal)
        {
            float into = Vector3.Dot(velocity, surfaceNormal);
            return into < 0f ? velocity - (surfaceNormal * into) : velocity;
        }
    }
}
