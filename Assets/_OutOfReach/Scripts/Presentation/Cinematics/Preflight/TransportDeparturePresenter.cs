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
        [SerializeField] private Transform capsulePort, booster, auroraReceiver;
        [SerializeField] private Transform approachCamera, dockCamera, boardingStartCamera, boardingEndCamera;

        private IGameState state;
        private ISimulationClock clock;
        private bool armed, active, suspended, notifying, orbitalViewsLeased, boosterWasActive;
        private float elapsed;
        private double lastSimulationSeconds;
        private Snapshot pivotStart, vehicleStart, hatchStart, armStart;
        private Snapshot pivotFrame, vehicleFrame, hatchFramePose, armFrame;
        private Snapshot[] cameraChain = Array.Empty<Snapshot>();
        private Vector3 cameraStartPosition, cameraRelativePosition, portLocalPosition;
        private Quaternion cameraStartRotation, cameraRelativeRotation, portLocalRotation;
        private float cameraStartFov;
        private TransportDepartureTimeline.Frame currentFrame;
        private TransportDepartureCue lastCue;

        public event Action<TransportDepartureCue> CueRaised;
        public event Action<string> CaptionChanged;
        public event Action<float> ProgressChanged;
        /// <summary>All owned transforms are restored before this callback. Only root decides what happens next.</summary>
        public event Action<TransportDepartureEnd> Finished;
        public bool IsPlaying => active;
        public bool IsSuspended => active && suspended;
        public float Progress01 => elapsed / TransportDepartureTimeline.DurationSeconds;
        public float PresentedSeconds => elapsed;
        public float Cover01 => active ? currentFrame.Cover : 0f;
        public float AtmosphereBlend01 => active ? currentFrame.AtmosphereBlend : 0f;
        public float BoardingTravel01 => IsBoardingAurora ? currentFrame.BoardingTravel : 0f;
        public bool IsInOrbit => active && orbitalViewsLeased && elapsed >= TransportDepartureTimeline.OrbitSeconds &&
            elapsed < TransportDepartureTimeline.BoardingAuroraSeconds;
        public bool IsBoardingAurora => active && orbitalViewsLeased && elapsed >= TransportDepartureTimeline.BoardingAuroraSeconds;
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

        /// <summary>Bind the two mating planes (+Z outward) and four camera-pose anchors.
        /// Root owns Aurora geometry, lighting and transfer body. The actual capsule and booster are leased.</summary>
        public void ConfigureOrbitalViews(Transform actualCapsulePort, Transform actualBooster, Transform receivingPort,
            Transform approachView, Transform dockingView, Transform transferStartView, Transform transferEndView)
        {
            if (active || notifying) throw new InvalidOperationException("Configure orbital views before departure.");
            capsulePort = actualCapsulePort;
            booster = actualBooster;
            auroraReceiver = receivingPort;
            approachCamera = approachView;
            dockCamera = dockingView;
            boardingStartCamera = transferStartView;
            boardingEndCamera = transferEndView;
        }

        /// <summary>Root calls once, only after a successful Planning-to-Intro launch and acquiring every transform/control lease.</summary>
        public bool Begin()
        {
            if (active || notifying || !isActiveAndEnabled || !armed || state == null || clock == null || clock.IsPaused ||
                state.Phase != GamePhase.Intro || state.Outcome != MissionOutcome.None || !ValidScene()) return false;
            if (!Finite(clock.ElapsedSimulationSeconds)) return false;
            bool hasOrbitalBindings = capsulePort || booster || auroraReceiver || approachCamera || dockCamera ||
                boardingStartCamera || boardingEndCamera;
            if (hasOrbitalBindings && !ValidOrbitalViews()) return false;
            if (Quaternion.Angle(hatchFrame.localRotation, Quaternion.Euler(0f, 160f, 0f)) > 0.25f ||
                Quaternion.Angle(accessArmFrame.localRotation, Quaternion.identity) > 0.25f) return false;
            pivotStart = new Snapshot(cameraOffsetPivot);
            vehicleStart = new Snapshot(vehicleRoot);
            hatchStart = new Snapshot(hatchFrame);
            armStart = new Snapshot(accessArmFrame);
            cameraStartPosition = sceneCamera.transform.position;
            cameraStartRotation = sceneCamera.transform.rotation;
            cameraStartFov = sceneCamera.fieldOfView;
            orbitalViewsLeased = hasOrbitalBindings;
            if (orbitalViewsLeased)
            {
                boosterWasActive = booster.gameObject.activeSelf;
                portLocalPosition = vehicleRoot.InverseTransformPoint(capsulePort.position);
                portLocalRotation = Quaternion.Inverse(vehicleRoot.rotation) * capsulePort.rotation;
            }
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
                !ValidScene(orbitalViewsLeased && elapsed >= TransportDepartureTimeline.UpperAirSeconds) ||
                !ParentsUnchanged() || (orbitalViewsLeased && !ValidOrbitalViews()) ||
                !Finite(clock.ElapsedSimulationSeconds) ||
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
            float duration = orbitalViewsLeased ? TransportDepartureTimeline.DurationSeconds : TransportDepartureTimeline.OrbitSeconds;
            // A stalled render can hold the shot, but it must not jump past a shot or its cue.
            elapsed = Mathf.Min(duration, elapsed + Mathf.Min(activeRealDelta, .10f));
            ApplyFrame();
            PublishFrame(false);
            if (active && elapsed >= duration) Stop(TransportDepartureEnd.Completed);
        }

        private void ApplyFrame()
        {
            currentFrame = TransportDepartureTimeline.Evaluate(elapsed, reducedMotion, orbitalViewsLeased);
            var frame = currentFrame;
            if (IsInOrbit || IsBoardingAurora)
            {
                if (booster.gameObject.activeSelf) booster.gameObject.SetActive(false);
                // Match the capsule's outward docking normal with the opposite receiving normal.
                Quaternion portFacing = Quaternion.LookRotation(-auroraReceiver.forward, auroraReceiver.up);
                Quaternion capsuleRotation = portFacing * Quaternion.Inverse(portLocalRotation);
                Vector3 portPosition = auroraReceiver.position + auroraReceiver.forward * frame.PortDistance +
                    auroraReceiver.right * frame.ApproachOffset;
                vehicleRoot.SetPositionAndRotation(portPosition - capsuleRotation * portLocalPosition, capsuleRotation);
            }
            else
            {
                Quaternion launchRotation = vehicleStart.WorldRotation;
                Vector3 displacement = Vector3.up * frame.HeightMetres + launchRotation * Vector3.forward *
                    (260f * frame.AtmosphereBlend * frame.AtmosphereBlend);
                vehicleRoot.SetPositionAndRotation(vehicleStart.WorldPosition + displacement,
                    launchRotation * Quaternion.Euler(26f * frame.AtmosphereBlend, 0f, 0f));
            }
            hatchFrame.localRotation = Quaternion.Euler(0f, frame.HatchDegrees, 0f);
            accessArmFrame.localRotation = Quaternion.Euler(0f, frame.ArmDegrees, 0f);

            EvaluateCamera(frame, out Vector3 position, out Quaternion rotation, out float fov);
            AddCameraVibration(frame.Shake, frame.Shot == TransportDepartureShot.Seat, ref position, ref rotation);
            Quaternion pivotRotation = rotation * Quaternion.Inverse(cameraRelativeRotation);
            cameraOffsetPivot.SetPositionAndRotation(position - pivotRotation * cameraRelativePosition, pivotRotation);
            sceneCamera.fieldOfView = fov;
            vehicleFrame = new Snapshot(vehicleRoot);
            hatchFramePose = new Snapshot(hatchFrame);
            armFrame = new Snapshot(accessArmFrame);
            pivotFrame = new Snapshot(cameraOffsetPivot);
        }

        private void EvaluateCamera(TransportDepartureTimeline.Frame frame, out Vector3 position, out Quaternion rotation, out float fov)
        {
            Quaternion launchRotation = vehicleStart.WorldRotation;
            switch (frame.Shot)
            {
                case TransportDepartureShot.Pad:
                    // The camera stays near the deck while the engine and airframe visibly leave the fixed pad.
                    position = vehicleStart.WorldPosition + launchRotation * new Vector3(29f, -27f, -36f);
                    rotation = LookAt(position, vehicleStart.WorldPosition + launchRotation *
                        new Vector3(0f, -21f + frame.HeightMetres * .65f, 0f));
                    fov = 43f;
                    return;
                case TransportDepartureShot.Climb:
                    float tracking = reducedMotion ? 0f : Mathf.InverseLerp(TransportDepartureTimeline.ClimbCutSeconds,
                        TransportDepartureTimeline.UpperAirSeconds, elapsed);
                    position = vehicleRoot.position + launchRotation * Vector3.Lerp(new Vector3(39f, -29f, -26f),
                        new Vector3(49f, -35f, -18f), tracking);
                    rotation = LookAt(position, vehicleRoot.TransformPoint(new Vector3(0f, -15f, 0f)));
                    fov = 49f;
                    return;
                case TransportDepartureShot.UpperAir:
                    position = vehicleRoot.position + launchRotation * new Vector3(-55f, 1f, -32f);
                    rotation = LookAt(position, vehicleRoot.TransformPoint(new Vector3(0f, -12f, 0f)));
                    fov = 45f;
                    return;
                case TransportDepartureShot.Approach:
                    position = approachCamera.position;
                    rotation = approachCamera.rotation;
                    fov = 47f;
                    return;
                case TransportDepartureShot.Collar:
                    position = dockCamera.position;
                    rotation = dockCamera.rotation;
                    fov = 41f;
                    return;
                case TransportDepartureShot.Transfer:
                    position = Vector3.Lerp(boardingStartCamera.position, boardingEndCamera.position, frame.BoardingTravel);
                    rotation = Quaternion.Slerp(boardingStartCamera.rotation, boardingEndCamera.rotation, frame.BoardingTravel);
                    fov = Mathf.Lerp(60f, cameraStartFov, frame.BoardingTravel);
                    return;
                default:
                    position = Vector3.Lerp(cameraStartPosition, seatedCamera.position, frame.SeatBlend);
                    rotation = Quaternion.Slerp(cameraStartRotation, seatedCamera.rotation, frame.SeatBlend);
                    fov = cameraStartFov;
                    return;
            }
        }

        private static Quaternion LookAt(Vector3 position, Vector3 target) => Quaternion.LookRotation(target - position, Vector3.up);

        private void AddCameraVibration(float strength, bool cabin, ref Vector3 position, ref Quaternion rotation)
        {
            if (strength <= 0f) return;
            float t = elapsed - TransportDepartureTimeline.IgnitionSeconds;
            // Layer a short engine kick over bounded rumble. Use real-time sampling so a pause holds exactly.
            float kick = Mathf.Sin(t * 13f) * Mathf.Exp(-5.5f * t);
            Vector3 jitter = new Vector3(.65f * Mathf.Sin(t * 34f) + .35f * Mathf.Sin(t * 16f),
                .7f * Mathf.Sin(t * 29f) + .3f * Mathf.Sin(t * 49f), .45f * Mathf.Sin(t * 19f));
            position += rotation * (jitter * ((cabin ? .029f : .11f) * strength));
            float angle = (cabin ? 1.8f : .85f) * strength;
            rotation *= Quaternion.Euler(angle * (.65f * Mathf.Sin(t * 27f) + kick),
                angle * .35f * Mathf.Sin(t * 21f), angle * .52f * Mathf.Sin(t * 33f));
        }

        private void PublishFrame(bool first)
        {
            if (first || currentFrame.Cue != lastCue)
            {
                lastCue = currentFrame.Cue;
                CurrentCaption = TransportDepartureTimeline.Caption(currentFrame.Cue);
                CueRaised?.Invoke(currentFrame.Cue);
                if (!active) return;
                CaptionChanged?.Invoke(CurrentCaption);
                if (!active) return;
            }
            ProgressChanged?.Invoke(currentFrame.Progress);
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
            if (sceneCamera) sceneCamera.fieldOfView = cameraStartFov;
            if (orbitalViewsLeased && booster) booster.gameObject.SetActive(boosterWasActive);
            orbitalViewsLeased = false;
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

        private bool ValidScene(bool allowHiddenAccessArm = false)
        {
            if (!cameraOffsetPivot || !sceneCamera || !vehicleRoot || !hatchFrame || !accessArmFrame || !seatedCamera) return false;
            if (!sceneCamera.transform.IsChildOf(cameraOffsetPivot) || sceneCamera.transform == cameraOffsetPivot ||
                !hatchFrame.IsChildOf(vehicleRoot) || hatchFrame == vehicleRoot ||
                !seatedCamera.IsChildOf(vehicleRoot) || seatedCamera == hatchFrame ||
                accessArmFrame.IsChildOf(vehicleRoot) || vehicleRoot.IsChildOf(accessArmFrame) ||
                cameraOffsetPivot.IsChildOf(vehicleRoot) || vehicleRoot.IsChildOf(cameraOffsetPivot) ||
                cameraOffsetPivot.IsChildOf(accessArmFrame) || accessArmFrame.IsChildOf(cameraOffsetPivot)) return false;
            return ValidTransform(cameraOffsetPivot) && ValidTransform(sceneCamera.transform) && ValidTransform(vehicleRoot) &&
                ValidTransform(hatchFrame) && ValidTransform(accessArmFrame, !allowHiddenAccessArm) && ValidTransform(seatedCamera);
        }

        private bool ValidOrbitalViews() =>
            ValidTransform(capsulePort) && ValidTransform(booster, false) &&
            capsulePort != vehicleRoot && booster != vehicleRoot &&
            capsulePort.IsChildOf(vehicleRoot) && booster.IsChildOf(vehicleRoot) &&
            !capsulePort.IsChildOf(booster) && !hatchFrame.IsChildOf(booster) && !seatedCamera.IsChildOf(booster) &&
            ValidOrbitalAnchor(auroraReceiver) && ValidOrbitalAnchor(approachCamera) && ValidOrbitalAnchor(dockCamera) &&
            ValidOrbitalAnchor(boardingStartCamera) && ValidOrbitalAnchor(boardingEndCamera);

        private bool ValidOrbitalAnchor(Transform value) => ValidTransform(value, false) &&
            value != vehicleRoot && value != accessArmFrame && value != cameraOffsetPivot &&
            !value.IsChildOf(vehicleRoot) && !value.IsChildOf(accessArmFrame) && !value.IsChildOf(cameraOffsetPivot);

        private static bool ValidTransform(Transform value, bool requireActive = true)
        {
            if (!value || (requireActive && !value.gameObject.activeInHierarchy) || !Finite(value.position) ||
                !Finite(value.rotation)) return false;
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
