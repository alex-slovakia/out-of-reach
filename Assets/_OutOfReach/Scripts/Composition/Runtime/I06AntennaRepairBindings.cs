using System;
using System.Linq;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Presentation.Hands;
using UnityEngine;

namespace OutOfReach.Composition
{
    /// <summary>One visual work sequence around the current real antenna interaction. No repair/resource authority.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(130)]
    public sealed class I06AntennaRepairBindings : MonoBehaviour
    {
        public const string MoveCloserMessage = "Too far from the antenna. Move closer and press E to repair.";
        private const float MaximumEyeReachMetres = .8f;
        private const float MaximumArmReachMetres = .72f;
        [SerializeField] private InteractableTarget target;
        [SerializeField] private Transform motionRoot;
        [SerializeField] private Transform gripFrame;
        [SerializeField] private GameObject damaged, repaired;
        [SerializeField] private Renderer[] wrenchRenderers = Array.Empty<Renderer>();
        [SerializeField] private AnimationClip workClip;
        private MissionRuntime runtime;
        private MissionSession session;
        private IRepairable antenna;
        private AstronautHandsPresenter hands;
        private Transform eye, ignoreRoot;
        private Func<bool> contextAllowed;
        private TimedInteractionGate gate;
        private Action started;
        private Action<double> progressed;
        private Action<CommandResult> finished;
        private Action<string> cancelled;
        private bool focused, ownsLease, workVisible;
        private float equipWait;
        public bool IsWorking => gate != null && gate.State == TimedWorkState.Working;
        // Acquiring the lease must succeed BEFORE the root closes its movement gate.
        public bool BlocksMovement => IsWorking && ownsLease;
        public double Progress01 => gate != null ? gate.Progress01 : 0d;
        public TimedInteractionGate WorkGate => gate;
        public Transform GripFrame => gripFrame;
        /// <summary>Read-only explanation using the same distances as the existing worksite gate.</summary>
        public bool IsBeyondWorkReach
        {
            get
            {
                if (!eye || !gripFrame) return false;
                Vector3 shoulder = eye.position + eye.rotation * new Vector3(.18f, -.28f, -.08f);
                Vector3 wrist = gripFrame.position - gripFrame.rotation * AstronautHandsPresenter.ToolGripCentreInHand;
                return Vector3.Distance(eye.position, gripFrame.position) > MaximumEyeReachMetres
                    || Vector3.Distance(shoulder, wrist) > MaximumArmReachMetres;
            }
        }
        public event Action<CommandResult> WorkFinished;
        public event Action<string> WorkCancelled;

        public void ConfigureViews(InteractableTarget physicalTarget, Transform actualMotionRoot,
            Transform actualGripFrame, AnimationClip actualClip, GameObject damagedState, GameObject repairedState,
            Renderer[] actualWrenchRenderers)
        {
            ClearBinding();
            target = physicalTarget; motionRoot = actualMotionRoot; gripFrame = actualGripFrame; workClip = actualClip;
            damaged = damagedState; repaired = repairedState; wrenchRenderers = actualWrenchRenderers ?? Array.Empty<Renderer>();
            StopVisuals();
        }

        /// <summary>
        /// Call after the existing hands and optional contact bridge have bound their current session.
        /// Context must exclude cinematics/terminal views, but allow the SAME valid context while paused.
        /// Do not feed Player.ControlAllowed here: this work deliberately owns a temporary movement lock.
        /// </summary>
        public bool ConfigureSession(MissionRuntime currentRuntime, AstronautHandsPresenter existingHands,
            Transform interactionEye, Transform playerCollisionRoot, Func<bool> validViewContext)
        {
            ClearBinding();
            if (!isActiveAndEnabled || !currentRuntime || currentRuntime.Session == null || !existingHands || !existingHands.IsBound
                || !target || target.InteractionId != InteractionIds.RepairAntenna || !motionRoot || !gripFrame || !workClip
                || !damaged || !repaired || !interactionEye || !playerCollisionRoot || validViewContext == null
                || Mathf.Abs(workClip.length - 3f) > .0001f || wrenchRenderers.Length == 0) return false;
            if (!currentRuntime.TryGetInteractable(InteractionIds.RepairAntenna, out IInteractable actual)) return false;
            runtime = currentRuntime; session = runtime.Session; hands = existingHands; eye = interactionEye;
            ignoreRoot = playerCollisionRoot; contextAllowed = validViewContext; focused = Application.isFocused;
            antenna = runtime.Repairables.Single(x => x.SystemId == ShipSystemId.Antenna);
            MissionSession captured = session;
            var created = new TimedInteractionGate(actual, 3d,
                () => ReferenceEquals(session, captured) && IsWorkStillValid(), () => captured.Clock.IsPaused);
            gate = created;
            started = () => OnStarted(created, captured);
            progressed = p => OnProgressed(created, captured, p);
            finished = result => OnFinished(created, captured, result);
            cancelled = reason => OnCancelled(created, captured, reason);
            gate.Started += started; gate.Progressed += progressed; gate.Finished += finished; gate.Cancelled += cancelled;
            target.Bind(gate);
            runtime.SessionEnding += OnSessionEnding;
            RefreshCondition();
            return true;
        }

        private void Update()
        {
            if (gate == null) return;
            if (!CurrentSession()) { ClearBinding(); return; }
            if (!IsWorking) { RefreshCondition(); return; }
            // Even before the hands settle and while paused, a stale session/focus must cancel without a command.
            gate.Advance(0d);
            if (!IsWorking || session.Clock.IsPaused) return;
            if (!hands || !hands.IsToolHoldActive) { gate.Cancel("Tool contact was interrupted."); return; }
            if (!hands.IsToolHoldSettled)
            {
                // Visual acquisition watchdog only; this does not advance mission time or the work gate.
                equipWait += Mathf.Min(Time.unscaledDeltaTime, .25f);
                if (equipWait > 2f) gate.Cancel("The tool could not reach the worksite.");
                return;
            }
            if (!workVisible)
            {
                workVisible = true;
                foreach (Renderer renderer in wrenchRenderers) if (renderer) renderer.enabled = true;
            }
            // One authoritative presentation timer: the decorator. Discard large visual hitches.
            gate.Advance(Math.Min(.1d, Math.Max(0d, Time.unscaledDeltaTime)));
        }

        private void OnStarted(TimedInteractionGate expectedGate, MissionSession expectedSession)
        {
            if (!Matches(expectedGate, expectedSession)) return;
            workVisible = ownsLease = false; equipWait = 0f;
            damaged.SetActive(false); repaired.SetActive(false); motionRoot.gameObject.SetActive(true);
            workClip.SampleAnimation(motionRoot.gameObject, 0f);
            foreach (Renderer renderer in wrenchRenderers) if (renderer) renderer.enabled = false;
            // No resource command occurs here. Tool visibility starts only at actual settled contact.
            if (!hands.TryBeginToolHold(gripFrame, IsToolLeaseContextValid))
            { gate.Cancel("The tool cannot reach the worksite."); return; }
            ownsLease = true;
        }

        private void OnProgressed(TimedInteractionGate expectedGate, MissionSession expectedSession, double progress)
        {
            if (!Matches(expectedGate, expectedSession) || !IsWorking || !workVisible) return;
            workClip.SampleAnimation(motionRoot.gameObject, (float)(progress * 3d));
            // The existing owner applies the just-sampled frame synchronously, with no timer advancement.
            // The gate checks actual contact again AFTER this event before making its only real command.
            if (!hands.RefreshToolHoldFrame()) gate.Cancel("Tool contact was interrupted.");
        }

        private void OnFinished(TimedInteractionGate expectedGate, MissionSession expectedSession, CommandResult result)
        {
            if (!Matches(expectedGate, expectedSession)) return;
            StopVisuals(); RefreshCondition(); WorkFinished?.Invoke(result);
        }
        private void OnCancelled(TimedInteractionGate expectedGate, MissionSession expectedSession, string reason)
        {
            if (!Matches(expectedGate, expectedSession)) return;
            StopVisuals(); RefreshCondition(); WorkCancelled?.Invoke(reason);
        }

        private bool CurrentSession() => isActiveAndEnabled && runtime && runtime.isActiveAndEnabled
            && session != null && !session.IsDisposed && ReferenceEquals(runtime.Session, session);
        private bool Matches(TimedInteractionGate expectedGate, MissionSession expectedSession)
            => ReferenceEquals(gate, expectedGate) && ReferenceEquals(session, expectedSession);
        private bool BaseContextValid()
        {
            if (!CurrentSession() || !focused || !eye || !ignoreRoot || !hands || !hands.IsBound || !target || !target.isActiveAndEnabled
                || !motionRoot || !gripFrame || !workClip || session.State.Phase != GamePhase.Eva
                || !runtime.EvaStatus.IsOutside || !runtime.EvaStatus.IsSuitOn || session.Cinematics.IsWaiting
                || target.Interactable != gate || contextAllowed == null) return false;
            try { return contextAllowed(); }
            catch (Exception error) { Debug.LogException(error, this); return false; }
        }

        private bool IsWorkStillValid()
        {
            if (!BaseContextValid() || !TargetInView() || !WorksitePathsClear()) return false;
            if (!IsWorking) return !session.Clock.IsPaused;
            if (!ownsLease || !hands.IsToolHoldActive) return false;
            if (!workVisible) return true; // The original .20 s visual reach has not started work time.
            return ActualSettledContact();
        }

        private bool IsToolLeaseContextValid()
            => BaseContextValid() && IsWorking && motionRoot.gameObject.activeInHierarchy
                && gripFrame.gameObject.activeInHierarchy && TargetInView() && WorksitePathsClear();

        private bool ActualSettledContact()
        {
            if (!hands.IsToolHoldSettled || !gripFrame) return false;
            Transform wrist = FindRightWrist();
            return wrist && Vector3.Distance(wrist.TransformPoint(AstronautHandsPresenter.ToolGripCentreInHand), gripFrame.position) <= .002f;
        }
        private Transform FindRightWrist()
        {
            // Exact scoped authored path; no scene search or alternative rig ownership.
            return hands && hands.ModelRoot ? hands.ModelRoot.Find("R_Arm/R_Hand") : null;
        }

        private bool TargetInView()
        {
            float reach = runtime.Player && runtime.Player.Interaction ? runtime.Player.Interaction.ReachMetres : 2f;
            RaycastHit[] hits = Physics.RaycastAll(eye.position, eye.forward, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                Collider collider = hit.collider;
                if (!collider || collider.transform.IsChildOf(ignoreRoot)) continue;
                InteractableTarget nearest = collider.GetComponentInParent<InteractableTarget>();
                if (nearest && nearest.isActiveAndEnabled) return nearest == target;
                if (!collider.isTrigger) return false;
            }
            return false;
        }

        private bool WorksitePathsClear()
        {
            Vector3 point = gripFrame.position, fromEye = point - eye.position;
            if (fromEye.magnitude > MaximumEyeReachMetres || Vector3.Dot(fromEye, eye.forward) < .12f) return false;
            if (Blocked(Physics.RaycastAll(eye.position, fromEye.normalized, fromEye.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))) return false;
            Vector3 shoulder = eye.position + eye.rotation * new Vector3(.18f, -.28f, -.08f);
            Vector3 wrist = point - gripFrame.rotation * AstronautHandsPresenter.ToolGripCentreInHand;
            Vector3 path = wrist - shoulder;
            if (path.magnitude < .18f || path.magnitude > MaximumArmReachMetres) return false;
            return !Blocked(Physics.SphereCastAll(shoulder, .01f, path.normalized, path.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore));
        }
        private bool Blocked(RaycastHit[] hits)
        {
            foreach (RaycastHit hit in hits)
                if (hit.collider && !hit.collider.transform.IsChildOf(ignoreRoot)) return true;
            return false;
        }

        private void RefreshCondition()
        {
            if (IsWorking) return;
            bool fixedByDomain = antenna != null && antenna.Condition01 >= 1d;
            if (damaged) damaged.SetActive(!fixedByDomain);
            if (repaired) repaired.SetActive(fixedByDomain);
        }
        private void StopVisuals()
        {
            ownsLease = workVisible = false; equipWait = 0f;
            if (hands) hands.EndToolHold();
            if (motionRoot) motionRoot.gameObject.SetActive(false);
            foreach (Renderer renderer in wrenchRenderers) if (renderer) renderer.enabled = false;
        }
        public void CancelWork(string reason) { if (gate != null) gate.Cancel(reason); }
        public void ClearBinding()
        {
            TimedInteractionGate oldGate = gate;
            if (oldGate != null)
            {
                oldGate.Started -= started; oldGate.Progressed -= progressed; oldGate.Finished -= finished; oldGate.Cancelled -= cancelled;
                if (target && ReferenceEquals(target.Interactable, oldGate)) target.Unbind();
            }
            if (runtime) runtime.SessionEnding -= OnSessionEnding;
            StopVisuals();
            gate = null; session = null; runtime = null; antenna = null; hands = null; eye = ignoreRoot = null; contextAllowed = null;
            started = null; progressed = null; finished = null; cancelled = null;
            if (damaged) damaged.SetActive(true);
            if (repaired) repaired.SetActive(false);
            // All old ownership is released before an external Cancelled observer can start another session.
            if (oldGate != null) oldGate.Cancel("The worksite session ended.");
        }
        private void OnSessionEnding(MissionSession ending) { if (ReferenceEquals(ending, session)) ClearBinding(); }
        private void OnApplicationFocus(bool value) { focused = value; if (!value) CancelWork("Focus was lost."); }
        private void OnApplicationPause(bool value) { if (value) { focused = false; CancelWork("The application was suspended."); } }
        private void OnDisable() => ClearBinding();
        private void OnDestroy() => ClearBinding();
    }
}
