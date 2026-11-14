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
    /// There is no thrust and no hidden force: speed only comes from pushes off verified solid surfaces within reach,
    /// and with the default zero drag a push carries the body on until it meets a surface.
    /// </summary>
    [Serializable]
    public sealed class MicrogravityLocomotion
    {
        [Tooltip("Hand reach from the eye point, metres. Entry, push, brace and grip targets must be within it.")]
        [SerializeField, Min(0.1f)] private float reachMetres = 0.85f;

        [Tooltip("Largest speed change one full push gives, metres per second.")]
        [SerializeField, Min(0f)] private float maxPushSpeed = 1f;

        [Tooltip("Speed cap while floating, metres per second.")]
        [SerializeField, Min(0.1f)] private float maxSpeed = 1.5f;

        [Tooltip("Minimum time between pushes, seconds, so a held button cannot accelerate continuously.")]
        [SerializeField, Min(0f)] private float pushCooldownSeconds = 0.35f;

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

        public float MaxPushSpeed => maxPushSpeed;

        public float MaxSpeed => maxSpeed;

        public float PushCooldownSeconds => pushCooldownSeconds;

        public float DragPerSecond => dragPerSecond;

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
        /// Exact displacement over one interval, and the velocity at its end. With drag it uses the exact exponential
        /// solution, so a drift covers the same path at 30 and at 240 frames per second either way.
        /// </summary>
        public Vector3 Integrate(ref Vector3 velocity, float deltaSeconds)
        {
            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds))
            {
                return Vector3.zero;
            }

            if (dragPerSecond <= 1e-6f)
            {
                return velocity * deltaSeconds;
            }

            float decay = Mathf.Exp(-dragPerSecond * deltaSeconds);
            Vector3 displacement = velocity * ((1f - decay) / dragPerSecond);
            velocity *= decay;
            return displacement;
        }

        /// <summary>A gripped handhold removes the velocity with the grip time constant (exact for any interval).</summary>
        public Vector3 Settle(Vector3 velocity, float deltaSeconds)
        {
            return deltaSeconds > 0f ? velocity * Mathf.Exp(-deltaSeconds / gripSettleSeconds) : velocity;
        }

        /// <summary>Removes the part of the velocity that points into a surface: an inelastic contact that keeps sliding.</summary>
        public static Vector3 RemoveInto(Vector3 velocity, Vector3 surfaceNormal)
        {
            float into = Vector3.Dot(velocity, surfaceNormal);
            return into < 0f ? velocity - (surfaceNormal * into) : velocity;
        }
    }
}
