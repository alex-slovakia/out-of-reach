using System;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Motion
{
    /// <summary>Small real-time camera drift while the opening menu owns the view. No input or mission authority.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(130)]
    public sealed class OpeningMenuMotionPresenter : MonoBehaviour
    {
        [SerializeField, Range(12f, 20f)] private float cycleRealSeconds = 16f;
        [SerializeField, Range(0f, .018f)] private float lateralMetres = .016f;
        [SerializeField, Range(0f, .004f)] private float verticalMetres = .003f;
        [SerializeField, Range(0f, .75f)] private float yawDegrees = .5f;
        [SerializeField] private bool reducedMotion;

        private Transform offset;
        private Vector3 neutralPosition;
        private Quaternion neutralRotation;
        private IGameState state;
        private ISimulationClock clock;
        private Func<bool> menuViewAllowed;
        private double elapsedRealSeconds;
        private bool bound;

        public void Configure(Transform dedicatedOffset, IGameState gameState,
            ISimulationClock simulationClock, Func<bool> isMenuViewAllowed)
        {
            if (!dedicatedOffset) throw new ArgumentNullException(nameof(dedicatedOffset));
            if (!dedicatedOffset.parent || dedicatedOffset.GetComponent<Camera>()
                || dedicatedOffset.GetComponent<AudioListener>() || dedicatedOffset.GetComponent<Collider>()
                || dedicatedOffset.GetComponent<Rigidbody>())
                throw new ArgumentException("Supply a dedicated empty camera child pivot, not a camera or gameplay object.", nameof(dedicatedOffset));
            if (gameState == null) throw new ArgumentNullException(nameof(gameState));
            if (simulationClock == null) throw new ArgumentNullException(nameof(simulationClock));
            if (isMenuViewAllowed == null) throw new ArgumentNullException(nameof(isMenuViewAllowed));
            ClearBinding();
            offset = dedicatedOffset;
            neutralPosition = offset.localPosition;
            neutralRotation = offset.localRotation;
            state = gameState;
            clock = simulationClock;
            menuViewAllowed = isMenuViewAllowed;
            bound = true;
            ResetToNeutral();
        }

        public void SetReducedMotion(bool reduced)
        {
            reducedMotion = reduced;
            if (reduced) ResetToNeutral();
        }

        /// <summary>Call synchronously after a mode, pause or phase change, before gameplay can use the view.</summary>
        public void RefreshEligibility()
        {
            if (!CanAnimate()) ResetToNeutral();
        }

        public void ResetToNeutral()
        {
            elapsedRealSeconds = 0d;
            if (bound && offset) offset.SetLocalPositionAndRotation(neutralPosition, neutralRotation);
        }

        public void ClearBinding()
        {
            ResetToNeutral();
            bound = false;
            offset = null;
            state = null;
            clock = null;
            menuViewAllowed = null;
        }

        private void OnEnable() => ResetToNeutral();
        private void OnDisable() => ResetToNeutral();
        private void OnDestroy() => ClearBinding();

        private bool CanAnimate() => bound && offset && isActiveAndEnabled && !reducedMotion
            && (state.Phase == GamePhase.Planning || clock.IsPaused)
            && state.Phase != GamePhase.Outcome && state.Phase != GamePhase.Report && menuViewAllowed();

        private void LateUpdate()
        {
            if (!CanAnimate()) { ResetToNeutral(); return; }
            float step = Time.unscaledDeltaTime;
            if (float.IsNaN(step) || float.IsInfinity(step) || step <= 0f || step > .2f)
            {
                ResetToNeutral();
                return;
            }
            double cycle = clock.IsPaused ? 12d : Mathf.Clamp(cycleRealSeconds, 12f, 20f);
            elapsedRealSeconds = (elapsedRealSeconds + step) % cycle;
            // A slow out-and-back arc starts and turns at zero velocity. No FOV, roll or forward zoom.
            float drift = (float)((1d - Math.Cos(elapsedRealSeconds * Math.PI * 2d / cycle)) * .5d);
            if (clock.IsPaused) drift *= .8f; // Background only; no simulation advancement while paused.
            offset.SetLocalPositionAndRotation(neutralPosition + new Vector3(
                Mathf.Clamp(lateralMetres, 0f, .018f) * drift,
                Mathf.Clamp(verticalMetres, 0f, .004f) * drift, 0f),
                neutralRotation * Quaternion.AngleAxis(Mathf.Clamp(yawDegrees, 0f, .75f) * drift, Vector3.up));
        }
    }
}
