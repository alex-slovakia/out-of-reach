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

        [Tooltip("Largest speed change one full push gives, metres per second.")]
        [SerializeField, Min(0f)] private float maxPushSpeed = 1.3f;

        [Tooltip("Speed cap while floating, metres per second.")]
        [SerializeField, Min(0.1f)] private float maxSpeed = 2f;

        [Tooltip("Minimum time between pushes (hand or foot), seconds, so a held button cannot accelerate continuously.")]
        [SerializeField, Min(0f)] private float pushCooldownSeconds = 0.25f;

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
