using System;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Cinematics
{
    /// <summary>One camera, an explicitly leased offset, and the real marine transport. No gameplay commands.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(400)]
    public sealed class TransportDeparturePresenter : MonoBehaviour
    {
        [SerializeField] private Transform cameraOffsetPivot;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Transform vehicleRoot;
        [SerializeField] private Transform hatchFrame;
        [SerializeField] private Transform accessArmFrame;
        [SerializeField] private Transform seatedCamera;
        [SerializeField] private bool reducedMotion;

        private IGameState state;
        private ISimulationClock clock;
        private bool armed, active, suspended, notifying;
        private float elapsed;
        private double lastSimulationSeconds;
        private Snapshot pivotStart, vehicleStart, hatchStart, armStart;
        private Snapshot pivotFrame, vehicleFrame, hatchFramePose, armFrame;
        private Snapshot[] cameraChain = Array.Empty<Snapshot>();
        private Vector3 cameraStartPosition, cameraRelativePosition;
        private Quaternion cameraStartRotation, cameraRelativeRotation;
        private TransportDepartureCue lastCue;

        public event Action<TransportDepartureCue> CueRaised;
        public event Action<string> CaptionChanged;
        public event Action<float> ProgressChanged;
        /// <summary>All owned transforms are restored before this callback. Only root decides what happens next.</summary>
        public event Action<TransportDepartureEnd> Finished;
        public bool IsPlaying => active;
        public bool IsSuspended => active && suspended;
        public float Progress01 => elapsed / TransportDepartureTimeline.DurationSeconds;
        public string CurrentCaption { get; private set; } = string.Empty;
        public bool ReducedMotion { get => reducedMotion; set { reducedMotion = value; } }

        /// <summary>Configure during Planning. These are the exact neutral-axis frames returned by I06MarineAuthoring.</summary>
        public void Configure(IGameState gameState, ISimulationClock simulationClock, Transform dedicatedCameraOffset,
            Camera existingCamera, Transform transportVehicleRoot, Transform transportHatchFrame,
            Transform transportAccessArmFrame, Transform actualSeatedCamera)
        {
            if (notifying) throw new InvalidOperationException("Do not reconfigure departure from its completion callback.");
            Stop(TransportDepartureEnd.Cancelled);
            state = gameState ?? throw new ArgumentNullException(nameof(gameState));
            clock = simulationClock ?? throw new ArgumentNullException(nameof(simulationClock));
            cameraOffsetPivot = dedicatedCameraOffset;
            sceneCamera = existingCamera;
            vehicleRoot = transportVehicleRoot;
            hatchFrame = transportHatchFrame;
            accessArmFrame = transportAccessArmFrame;
            seatedCamera = actualSeatedCamera;
            armed = state.Phase == GamePhase.Planning;
            elapsed = 0f;
        }

        /// <summary>Root calls once, only after a successful Planning-to-Intro launch and acquiring every transform/control lease.</summary>
        public bool Begin()
        {
            if (active || notifying || !isActiveAndEnabled || !armed || state == null || clock == null || clock.IsPaused ||
                state.Phase != GamePhase.Intro || state.Outcome != MissionOutcome.None || !ValidScene()) return false;
            if (!Finite(clock.ElapsedSimulationSeconds)) return false;
            if (Quaternion.Angle(hatchFrame.localRotation, Quaternion.Euler(0f, 160f, 0f)) > 0.25f ||
                Quaternion.Angle(accessArmFrame.localRotation, Quaternion.identity) > 0.25f) return false;
            pivotStart = new Snapshot(cameraOffsetPivot);
            vehicleStart = new Snapshot(vehicleRoot);
            hatchStart = new Snapshot(hatchFrame);
            armStart = new Snapshot(accessArmFrame);
            cameraStartPosition = sceneCamera.transform.position;
            cameraStartRotation = sceneCamera.transform.rotation;
            CaptureCameraChain();
            lastSimulationSeconds = clock.ElapsedSimulationSeconds;
            elapsed = 0f;
            suspended = false;
            armed = false;
            active = true;
            lastCue = TransportDepartureCue.Boarding;
            ApplyFrame();
            PublishFrame(true);
            return active;
        }

        public bool Skip() { if (!active) return false; Stop(TransportDepartureEnd.Skipped); return true; }
        public void Cancel() => Stop(TransportDepartureEnd.Cancelled);
        public void Unbind()
        {
            Stop(TransportDepartureEnd.Cancelled);
            state = null; clock = null; armed = false;
        }
        private void OnDisable() { armed = false; Stop(TransportDepartureEnd.Cancelled); }
        private void OnDestroy() => Unbind();

        private void LateUpdate() => AdvanceFrame(Time.unscaledDeltaTime);

        // A bounded visual playhead, never a simulation clock. Paused time is never accumulated.
        private void AdvanceFrame(float activeRealDelta)
        {
            if (!active)
            {
                if (state != null && state.Phase == GamePhase.Planning) armed = true;
                return;
            }
            if (state == null || clock == null || state.Phase != GamePhase.Intro || state.Outcome != MissionOutcome.None ||
                !ValidScene() || !ParentsUnchanged() || !Finite(clock.ElapsedSimulationSeconds) ||
                clock.ElapsedSimulationSeconds + 0.000001 < lastSimulationSeconds)
            { Stop(TransportDepartureEnd.Cancelled); return; }
            lastSimulationSeconds = clock.ElapsedSimulationSeconds;
            if (clock.IsPaused)
            {
                // Core may already have relocated the camera's player parent. Hold the last
                // presented world shot, not the captured gameplay-local offset or a new frame.
                vehicleFrame.RestoreWorld(); hatchFramePose.RestoreWorld();
                armFrame.RestoreWorld(); pivotFrame.RestoreWorld();
                suspended = true;
                return;
            }
            // Resume at the held playhead for one frame, with no pause-sized delta or extra cue.
            if (suspended) { suspended = false; ApplyFrame(); return; }
            if (!Finite(activeRealDelta) || activeRealDelta < 0f) return;
            elapsed = Mathf.Min(TransportDepartureTimeline.DurationSeconds, elapsed + activeRealDelta);
            ApplyFrame();
            PublishFrame(false);
            if (active && elapsed >= TransportDepartureTimeline.DurationSeconds) Stop(TransportDepartureEnd.Completed);
        }

        private void ApplyFrame()
        {
            var f = TransportDepartureTimeline.Evaluate(elapsed, reducedMotion);
            vehicleRoot.SetPositionAndRotation(vehicleStart.WorldPosition + Vector3.up * f.HeightMetres, vehicleStart.WorldRotation);
            hatchFrame.localRotation = Quaternion.Euler(0f, f.HatchDegrees, 0f);
            accessArmFrame.localRotation = Quaternion.Euler(0f, f.ArmDegrees, 0f);
            Vector3 position;
            Quaternion rotation;
            if (f.Exterior)
            {
                // A deliberate camera cut, never a flight through the capsule wall. Full 32 m booster stays in frame.
                position = vehicleRoot.TransformPoint(new Vector3(16f, 7f, -26f));
                rotation = Quaternion.LookRotation(vehicleRoot.TransformPoint(new Vector3(0f, -10f, 0f)) - position, Vector3.up);
            }
            else
            {
                position = Vector3.Lerp(cameraStartPosition, seatedCamera.position, f.SeatBlend);
                rotation = Quaternion.Slerp(cameraStartRotation, seatedCamera.rotation, f.SeatBlend);
                Vector3 vibration = new Vector3(Mathf.Sin(elapsed * 39f), Mathf.Sin(elapsed * 47f), 0f) * (0.002f * f.Shake);
                position += rotation * vibration;
                rotation *= Quaternion.Euler(0.10f * f.Shake * Mathf.Sin(elapsed * 31f), 0f, 0.08f * f.Shake * Mathf.Sin(elapsed * 29f));
            }
            Quaternion pivotRotation = rotation * Quaternion.Inverse(cameraRelativeRotation);
            cameraOffsetPivot.SetPositionAndRotation(position - pivotRotation * cameraRelativePosition, pivotRotation);
            vehicleFrame = new Snapshot(vehicleRoot);
            hatchFramePose = new Snapshot(hatchFrame);
            armFrame = new Snapshot(accessArmFrame);
            pivotFrame = new Snapshot(cameraOffsetPivot);
        }

        private void PublishFrame(bool first)
        {
            var frame = TransportDepartureTimeline.Evaluate(elapsed, reducedMotion);
            if (first || frame.Cue != lastCue)
            {
                lastCue = frame.Cue;
                CurrentCaption = TransportDepartureTimeline.Caption(frame.Cue);
                CueRaised?.Invoke(frame.Cue);
                if (!active) return;
                CaptionChanged?.Invoke(CurrentCaption);
                if (!active) return;
            }
            ProgressChanged?.Invoke(frame.Progress);
        }

        private void Stop(TransportDepartureEnd reason)
        {
            if (!active) return;
            active = false;
            suspended = false;
            Restore();
            cameraChain = Array.Empty<Snapshot>();
            CurrentCaption = string.Empty;
            notifying = true;
            try { CaptionChanged?.Invoke(string.Empty); Finished?.Invoke(reason); }
            finally { notifying = false; }
        }

        private void Restore()
        {
            vehicleStart.Restore(); hatchStart.Restore(); armStart.Restore(); pivotStart.Restore();
        }

        private bool ParentsUnchanged() => pivotStart.HasOriginalParent && vehicleStart.HasOriginalParent &&
            hatchStart.HasOriginalParent && armStart.HasOriginalParent && CameraChainUnchanged();

        private void CaptureCameraChain()
        {
            int count = 0;
            for (Transform node = sceneCamera.transform; node != cameraOffsetPivot; node = node.parent) count++;
            cameraChain = new Snapshot[count];
            cameraRelativePosition = Vector3.zero;
            cameraRelativeRotation = Quaternion.identity;
            int index = 0;
            for (Transform node = sceneCamera.transform; node != cameraOffsetPivot; node = node.parent)
            {
                cameraChain[index++] = new Snapshot(node);
                // ValidScene requires unit scale. Compose local poses without subtracting 10 km world positions.
                cameraRelativePosition = node.localPosition + node.localRotation * cameraRelativePosition;
                cameraRelativeRotation = node.localRotation * cameraRelativeRotation;
            }
        }

        private bool CameraChainUnchanged()
        {
            foreach (Snapshot node in cameraChain)
                if (!node.HasOriginalLocalPose) return false;
            return cameraChain.Length > 0;
        }

        private bool ValidScene()
        {
            if (!cameraOffsetPivot || !sceneCamera || !vehicleRoot || !hatchFrame || !accessArmFrame || !seatedCamera) return false;
            if (!sceneCamera.transform.IsChildOf(cameraOffsetPivot) || sceneCamera.transform == cameraOffsetPivot ||
                !hatchFrame.IsChildOf(vehicleRoot) || hatchFrame == vehicleRoot ||
                !seatedCamera.IsChildOf(vehicleRoot) || seatedCamera == hatchFrame ||
                accessArmFrame.IsChildOf(vehicleRoot) || vehicleRoot.IsChildOf(accessArmFrame) ||
                cameraOffsetPivot.IsChildOf(vehicleRoot) || vehicleRoot.IsChildOf(cameraOffsetPivot) ||
                cameraOffsetPivot.IsChildOf(accessArmFrame) || accessArmFrame.IsChildOf(cameraOffsetPivot)) return false;
            return ValidTransform(cameraOffsetPivot) && ValidTransform(sceneCamera.transform) && ValidTransform(vehicleRoot) &&
                ValidTransform(hatchFrame) && ValidTransform(accessArmFrame) && ValidTransform(seatedCamera);
        }

        private static bool ValidTransform(Transform value)
        {
            if (!value.gameObject.activeInHierarchy || !Finite(value.position) || !Finite(value.rotation)) return false;
            for (Transform p = value; p; p = p.parent)
                if ((p.localScale - Vector3.one).sqrMagnitude > 0.00000001f || !Finite(p.localScale)) return false;
            return true;
        }
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        private static bool Finite(Quaternion v) => Finite(v.x) && Finite(v.y) && Finite(v.z) && Finite(v.w);

        private readonly struct Snapshot
        {
            private readonly Transform target, parent;
            private readonly Vector3 position, scale;
            private readonly Quaternion rotation;
            public readonly Vector3 WorldPosition;
            public readonly Quaternion WorldRotation;
            public bool HasOriginalParent => target && target.parent == parent;
            public bool HasOriginalLocalPose => HasOriginalParent && target.localPosition.Equals(position) &&
                target.localRotation.Equals(rotation) && target.localScale.Equals(scale);
            public Snapshot(Transform value)
            {
                target = value; parent = value.parent; position = value.localPosition; rotation = value.localRotation;
                scale = value.localScale; WorldPosition = value.position; WorldRotation = value.rotation;
            }
            public void Restore()
            {
                if (!target) return;
                target.SetParent(parent, false);
                target.localPosition = position; target.localRotation = rotation; target.localScale = scale;
            }
            public void RestoreWorld()
            {
                if (target) target.SetPositionAndRotation(WorldPosition, WorldRotation);
            }
        }
    }
}
