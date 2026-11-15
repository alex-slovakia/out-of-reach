using System;
using UnityEngine;

namespace OutOfReach.Player
{
    public enum ExertionActivity { Resting, Walking, Sprinting, Carrying, TetherTraversal }

    /// <summary>
    /// Controller-owned stamina, independent of ship/suit resources and the simulation clock. All rates and
    /// thresholds are GAME TUNING, not NASA physiological data. Only the controller reports actual movement.
    /// </summary>
    [Serializable]
    public sealed class PlayerExertion
    {
        [Tooltip("Stamina used per active real second of full-speed sprinting. Game tuning.")]
        [SerializeField, Min(0f)] private float sprintDrainPerSecond = 0.1f;
        [Tooltip("Stamina used per active real second of full-speed cargo travel. Game tuning.")]
        [SerializeField, Min(0f)] private float cargoDrainPerSecond = 0.025f;
        [Tooltip("Stamina used per active real second of full-speed tether traversal. Game tuning.")]
        [SerializeField, Min(0f)] private float tetherDrainPerSecond = 0.02f;
        [Tooltip("Stamina restored per active real second at rest; never while paused or control-denied.")]
        [SerializeField, Min(0f)] private float restRecoveryPerSecond = 0.1f;

        public const float RecoveryThreshold01 = 0.2f;
        public const float MinimumTraversalFactor = 0.9f;

        private float remaining01 = 1f;
        private bool needsRecovery;

        public float Remaining01 => remaining01;
        public bool CanSprint => !needsRecovery && remaining01 > 0f;

        /// <summary>Cargo and tether speed only: unchanged above 20%, smoothly falls to 90% at empty.</summary>
        public float TraversalSpeedFactor => Mathf.Lerp(MinimumTraversalFactor, 1f,
            Mathf.Clamp01(remaining01 / RecoveryThreshold01));

        /// <summary>
        /// Advances active REAL seconds. Motion fraction is actual distance / full-input distance this frame,
        /// so stick deflection, blocked movement and a partially reached route end cannot charge full effort.
        /// Walking without a load neither drains nor restores stamina; restoring requires rest.
        /// </summary>
        public void Advance(float activeRealSeconds, ExertionActivity activity, float motionFraction01)
        {
            if (float.IsNaN(activeRealSeconds) || float.IsInfinity(activeRealSeconds) || activeRealSeconds <= 0f)
            {
                return;
            }

            float motion = float.IsNaN(motionFraction01) ? 0f : Mathf.Clamp01(motionFraction01);
            float rate;
            switch (activity)
            {
                case ExertionActivity.Resting: rate = -Mathf.Max(0f, restRecoveryPerSecond); break;
                case ExertionActivity.Sprinting: rate = Mathf.Max(0f, sprintDrainPerSecond) * motion; break;
                case ExertionActivity.Carrying: rate = Mathf.Max(0f, cargoDrainPerSecond) * motion; break;
                case ExertionActivity.TetherTraversal: rate = Mathf.Max(0f, tetherDrainPerSecond) * motion; break;
                default: rate = 0f; break;
            }

            remaining01 = Mathf.Clamp01(remaining01 - (rate * activeRealSeconds));
            if (remaining01 <= 0f)
            {
                needsRecovery = true;
            }
            else if (remaining01 >= RecoveryThreshold01)
            {
                needsRecovery = false;
            }
        }

        public void Reset()
        {
            remaining01 = 1f;
            needsRecovery = false;
        }
    }
}
