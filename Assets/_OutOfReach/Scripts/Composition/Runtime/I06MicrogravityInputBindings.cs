using System;
using OutOfReach.Contracts;
using OutOfReach.Player;
using OutOfReach.Presentation.Hands;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OutOfReach.Composition
{
    /// <summary>
    /// Explicit input routing only. The existing bridge owns hand contact and Player owns all forces.
    /// MOVE-MECH-01 (corrected contract #7 6893683804 + 6893693954):
    /// <list type="bullet">
    /// <item>LMB = left-hand push, RMB = right-hand push (medium); both within a short chord = ONE stronger paired push.</item>
    /// <item>A plain LMB/RMB aimed at a reachable registered rail GRIPS it with that hand (Ctrl stays an alias).
    /// While holding, a click pushes off the held rail towards where you look, in one action.</item>
    /// <item>The push surface is the aimed one in reach, otherwise the nearest reachable solid surface around the
    /// body (walls, ceiling, floor, slopes, solid props); a surface found around the body sends you where you look.</item>
    /// <item>Space = strong push: legs from the deck when the feet touch it, otherwise both hands. W/S add the travel
    /// intent (towards / away from the view). A/D never push: the Player uses them to lean the view.</item>
    /// <item>One press made during a hand return or the push cooldown waits up to <see cref="QueueSeconds"/>.</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(50)]
    public sealed class I06MicrogravityInputBindings : MonoBehaviour
    {
        /// <summary>What a click would do now, for the HUD highlight and the controls hint (read-only, 10 Hz).</summary>
        public enum ContactPreview
        {
            None,
            Grip,
            Push,
            PushOffRail,
        }

        /// <summary>How long one press made while the hand is busy or the push is cooling down still waits, seconds.</summary>
        public const float QueueSeconds = .5f;

        /// <summary>HUD text when a rail click cannot grip with bare WORK hands.</summary>
        public const string BareGripUnavailable = "Bare-hand rail grip is not available (no verified WORK grip pose). Click another surface to push off.";

        readonly MicroInputLease lease = new MicroInputLease();
        readonly RaycastHit[] hits = new RaycastHit[64];
        FirstPersonController player;
        I06MicrogravityBridge bridge;
        Transform eye;
        IEvaStatus suit;
        IGameState state;
        ISimulationClock clock;
        Func<bool> currentSession, contactPermission;
        PlayerInputActions source;
        bool bound, armed, faulted, subscribed, permissionWasAllowed;
        bool leftReady, rightReady, footReady;
        bool handClickPending;
        ContactHand pendingHand;
        float pendingUntil;
        const float ChordSeconds = .075f;
        int armedFrame;
        float handStrength, footStrength;
        bool hasQueued, queuedGrip, queuedPair, queuedStrong;
        ContactHand queuedHand;
        string queuedReason = string.Empty;
        float queuedUntil, nextPreviewAt;
        Vector3 lastVisibleTravel;
        float lastVisibleStrength;

        /// <summary>HUD text when a click finds no solid surface within hand reach (after its short wait).</summary>
        public const string NoSurfaceInReach = "No solid surface within reach. Drift closer to a wall, the floor or the ceiling.";

        /// <summary>Root must suppress decorative L/R and successful-E gestures while true, including refusal/pause.</summary>
        public bool ContactRoutingRequested => armed;
        public bool HasInputLease => armed && !faulted && player && ReferenceEquals(player.InputSource, source) && lease.IsOwned;
        public bool IsFaulted => faulted;
        public bool HasRailGrip => bound && (suit.IsSuitOn || bridge.WorkGripAvailable);
        public bool AimAtReachableRail() => HasInputLease && HasRailGrip && !bridge.IsBusy && PreferRailGrip();
        public string LastRefusal { get; private set; } = string.Empty;
        /// <summary>Increments on every refusal, so a HUD can show <see cref="LastRefusal"/> again when the text repeats.</summary>
        public int RefusalRevision { get; private set; }
        public FloatActionResult? LastFootResult { get; private set; }

        /// <summary>What a click would do now: grip an aimed rail, push off a surface, push off the held rail, or nothing.</summary>
        public ContactPreview Preview { get; private set; }
        /// <summary>The rail or surface of <see cref="Preview"/> (highlight target), or null.</summary>
        public Collider PreviewSurface { get; private set; }
        /// <summary>World point of <see cref="Preview"/> on its surface.</summary>
        public Vector3 PreviewPoint { get; private set; }
        /// <summary>True when the preview surface is the one the view ray meets (false: found around the body).</summary>
        public bool PreviewAimed { get; private set; }
        /// <summary>True while Space would push with the legs from the deck (feet on real support); else both hands.</summary>
        public bool StrongPushUsesLegs { get; private set; }
        /// <summary>Raised after an accepted leg push from the deck, with its world direction (for the body animation).</summary>
        public event Action<Vector3> LegPushPerformed;

        /// <param name="sessionIsCurrent">Identity check for the exact session/services supplied here.</param>
        /// <param name="canContact">Current root policy: excludes cargo, rest, menus, terminal, cinematics and transfers.</param>
        /// <param name="pushStrength01">One medium hand push (share of one full push); both hands add up to one full push.</param>
        /// <param name="footStrength01">The strong push: legs from the deck, or one hand when two palms do not fit.</param>
        public void Configure(FirstPersonController actualPlayer, I06MicrogravityBridge contactBridge,
            Transform unbobbedEye, IEvaStatus evaStatus, IGameState gameState, ISimulationClock simulationClock,
            Func<bool> sessionIsCurrent, Func<bool> canContact, float pushStrength01 = .55f, float footStrength01 = 1f)
        {
            ClearBinding();
            if (!actualPlayer || !contactBridge || !unbobbedEye || unbobbedEye != actualPlayer.ViewPivot
                || evaStatus == null || gameState == null || simulationClock == null
                || sessionIsCurrent == null || canContact == null || !ValidStrength(pushStrength01) || !ValidStrength(footStrength01))
                throw new ArgumentException("Use the current Player, bound contact bridge and its exact unbobbed ViewPivot with explicit session/policy gates.");
            player = actualPlayer; bridge = contactBridge; eye = unbobbedEye; suit = evaStatus;
            state = gameState; clock = simulationClock; currentSession = sessionIsCurrent; contactPermission = canContact;
            handStrength = pushStrength01; footStrength = footStrength01; bound = true;
            bridge.PushReachGaveUp += OnPushReachGaveUp;
            if (isActiveAndEnabled) Subscribe();
        }

        /// <summary>
        /// Root calls only after its explicit validated float entry. Acquire before the next Input System update,
        /// never in response to an already-processed LMB Interact edge. No automatic float entry or body teleport.
        /// </summary>
        public bool Arm()
        {
            if (armed) return HasInputLease && bridge && bridge.ContactModeEnabled;
            LastRefusal = string.Empty; LastFootResult = null;
            if (!bound || !isActiveAndEnabled || !SessionCurrent() || !ModeValid() || !Permission())
                return Refuse("Current floating session/contact policy does not permit arming.");
            source = player.InputSource as PlayerInputActions;
            if (source == null || !source.Map.enabled) { source = null; return Refuse("Contact routing requires the enabled default PlayerInputActions source."); }
            // An override cannot undo an Interact edge already processed this frame.
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                && source.InteractPressedThisFrame) { source = null; return Refuse("Arm before a new input update, after the existing click has been released."); }
            if (!lease.TryAcquire(source)) { source = null; return Refuse("Default LMB Interact binding is unavailable or already owned."); }
            if (ConflictingControls()) { lease.Release(); source = null; return Refuse("Another player binding uses LMB, RMB or Space."); }
            armed = true; faulted = false; armedFrame = Time.frameCount;
            PrimeFromReleasedButtons(); permissionWasAllowed = true;
            bridge.SetContactModeEnabled(true);
            if (!bridge.ContactModeEnabled) { Disarm(); return Refuse("Contact bridge is not bound."); }
            return true;
        }

        /// <summary>Only releases this bridge's contact and this lease's unchanged binding; never exits float.</summary>
        public void Disarm()
        {
            armed = false; faulted = false; permissionWasAllowed = false;
            leftReady = rightReady = footReady = handClickPending = false;
            hasQueued = false; ClearPreview();
            if (bridge) bridge.SetContactModeEnabled(false);
            lease.Release(); source = null;
        }

        public void ClearBinding()
        {
            if (bridge) bridge.PushReachGaveUp -= OnPushReachGaveUp;
            Disarm(); Unsubscribe(); bound = false;
            player = null; bridge = null; eye = null; suit = null; state = null; clock = null;
            currentSession = contactPermission = null; LastRefusal = string.Empty; LastFootResult = null;
        }
        void OnEnable() { if (bound) Subscribe(); }
        void OnDisable() => ClearBinding();
        void OnDestroy() => ClearBinding();
        void Subscribe() { if (subscribed) return; InputSystem.onBeforeUpdate += BeforeInput; subscribed = true; }
        void Unsubscribe() { if (!subscribed) return; InputSystem.onBeforeUpdate -= BeforeInput; subscribed = false; }

        void BeforeInput()
        {
            // Detect an input/session replacement before its next device event is processed.
            if (armed) ValidateOwnership();
        }
        bool ValidateOwnership()
        {
            if (!armed || faulted) return false;
            if (!SessionCurrent() || !ModeValid()) { Disarm(); return false; }
            if (!ReferenceEquals(player.InputSource, source) || !lease.IsOwned || ConflictingControls())
            { Fault("Input source or binding ownership changed. Explicit Disarm/rebind is required."); return false; }
            return true;
        }
        void Update()
        {
            if (!ValidateOwnership()) { ClearPreview(); return; }
            if (!Permission() || !source.Map.enabled)
            {
                if (permissionWasAllowed || bridge.IsBusy) bridge.Release();
                permissionWasAllowed = false; leftReady = rightReady = footReady = handClickPending = false;
                hasQueued = false; ClearPreview();
                return; // Keep the LMB lease through pause/menu/rest; no resume-click fallback.
            }
            permissionWasAllowed = true;
            if (Time.frameCount == armedFrame) return; // Never replay the ownership-transition frame.
            Mouse mouse = Mouse.current; Keyboard keyboard = Keyboard.current;
            if (mouse == null) leftReady = rightReady = false;
            else
            {
                if (!mouse.leftButton.isPressed) leftReady = true;
                if (!mouse.rightButton.isPressed) rightReady = true;
            }
            if (keyboard == null) footReady = false;
            else if (!keyboard.spaceKey.isPressed) footReady = true;
            bool left = mouse != null && leftReady && mouse.leftButton.wasPressedThisFrame;
            bool right = mouse != null && rightReady && mouse.rightButton.wasPressedThisFrame;
            bool strong = keyboard != null && footReady && keyboard.spaceKey.wasPressedThisFrame;
            if (left) leftReady = false;
            if (right) rightReady = false;
            if (strong) footReady = false;
            bool grip = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
            RefreshPreview();
            if (HandleHandInput(mouse, left, right, grip)) return;
            if (strong) { hasQueued = false; DispatchStrong(); return; }
            RetryQueued();
        }

        bool HandleHandInput(Mouse mouse, bool left, bool right, bool grip)
        {
            if (mouse == null) { handClickPending = false; return false; }
            if (handClickPending)
            {
                bool firstHeld = pendingHand == ContactHand.Left ? mouse.leftButton.isPressed : mouse.rightButton.isPressed;
                bool secondPressed = pendingHand == ContactHand.Left ? right : left;
                if (firstHeld && secondPressed && Time.unscaledTime <= pendingUntil)
                {
                    handClickPending = false;
                    DispatchHand(pendingHand, false, true);
                }
                else if (!firstHeld || Time.unscaledTime >= pendingUntil)
                {
                    handClickPending = false;
                    DispatchHand(pendingHand, false, false);
                }
                return true;
            }
            if (!left && !right) return false;
            hasQueued = false; // A new press replaces a queued one.
            ContactHand hand = left ? ContactHand.Left : ContactHand.Right;
            bool both = left && right;
            if (player.HeldHandhold) { DispatchHand(hand, false, both); return true; }
            // A plain click aimed at a reachable registered rail grips it with that hand (Ctrl stays an alias).
            // Selection only: the bridge re-proves the rail, reach, pose and contact.
            bool gripRail = !both && (grip || (!bridge.IsBusy && HasRailGrip && PreferRailGrip(out RaycastHit railAim)
                && bridge.CanArmReach(railAim.point, hand)));
            if (both || gripRail) DispatchHand(hand, gripRail, both);
            else
            {
                handClickPending = true; pendingHand = hand;
                pendingUntil = Time.unscaledTime + ChordSeconds;
            }
            return true;
        }

        /// <summary>
        /// MOVE-MECH-02 (Alex's playtest 6895779523): W/S choose towards/away from the view and A/D choose left/right
        /// (the view's own right axis, which the A/D lean tilts), for every push: one hand, both hands, Space legs or
        /// hands, and a push off a held rail. Still no thrust: nothing moves until a real contact push.
        /// </summary>
        Vector3 ReadTravelIntent()
        {
            Vector2 move = source.Move;
            Vector3 intent = Vector3.zero;
            if (Mathf.Abs(move.y) > .3f) intent += eye.forward * Mathf.Sign(move.y);
            if (Mathf.Abs(move.x) > .3f) intent += eye.right * Mathf.Sign(move.x);
            return intent.sqrMagnitude > .01f ? intent.normalized : Vector3.zero;
        }

        void DispatchHand(ContactHand hand, bool grip, bool pair)
        {
            LastRefusal = string.Empty;
            Vector3 travel = ReadTravelIntent();
            if (player.HeldHandhold)
            {
                if (!bridge.HoldsRail)
                {
                    FloatActionResult released = bridge.Release();
                    if (released != FloatActionResult.Done) Refuse("The held grip is not owned by this contact bridge.");
                    return;
                }
                PushOffHeldRail(hand, travel, pair);
                return;
            }
            if (bridge.IsBusy || (!grip && player.PushCooldownRemaining > 0f)) { Queue(hand, grip, pair, false); return; }
            if (grip)
            {
                if (!suit.IsSuitOn && !bridge.WorkGripAvailable) { Refuse(BareGripUnavailable); return; }
                if (!bridge.TryGrab(hand, eye.forward)) Refuse(bridge.LastRefusal);
                return;
            }
            float strength = pair ? Mathf.Min(1f, handStrength * 2f) : handStrength;
            if (!bridge.TrySelectPushSurface(eye.forward, pair ? (ContactHand?)null : hand, out _, out _))
            {
                // MOVE-MECH-02: a surface behind or above the view (the ceiling while looking down where you want to
                // go) is pushed off without a visible hand; only with nothing in reach at all does the click wait.
                if (bridge.TryPushUnseen(travel, strength)) return;
                Queue(hand, false, pair, false, bridge.NoSurfaceReason());
                return;
            }
            lastVisibleTravel = travel; lastVisibleStrength = strength;
            bool accepted = pair ? bridge.TryPushPairContext(eye.forward, travel, handStrength)
                : bridge.TryPushContext(hand, eye.forward, travel, handStrength);
            string refusal = accepted ? null : bridge.LastRefusal;
            if (!accepted && pair)
            {
                // Two separate palm spots did not fit (edge, strut, reach): one strong push with a single hand.
                accepted = bridge.TryPushContext(ContactHand.Left, eye.forward, travel, strength)
                    || bridge.TryPushContext(ContactHand.Right, eye.forward, travel, strength);
            }
            // The visible arm could not take any spot in view (pose, wrist, reach): push off the nearest surface
            // anyway, out of view, so a click next to a wall or the ceiling never does nothing.
            if (!accepted) accepted = bridge.TryPushUnseen(travel, strength);
            if (!accepted) Refuse(refusal ?? bridge.LastRefusal);
        }

        // The visible reach was accepted but the arm presenter gave up mid-reach: push off unseen instead.
        void OnPushReachGaveUp()
        {
            if (!HasInputLease || !Permission() || player.HeldHandhold) return;
            if (!bridge.TryPushUnseen(lastVisibleTravel, lastVisibleStrength > 0f ? lastVisibleStrength : handStrength))
                Refuse(bridge.LastRefusal);
        }

        void PushOffHeldRail(ContactHand hand, Vector3 travel, bool strong)
        {
            if (player.PushCooldownRemaining > 0f) { Queue(hand, false, strong, false); return; }
            // Go where you look; W/S choose towards or away from the view. The bridge limits it to the outward cone.
            Vector3 intent = travel != Vector3.zero ? travel : eye.forward;
            if (bridge.TryPushOffHeldRail(intent, strong ? footStrength : handStrength)) return;
            string reason = bridge.LastRefusal;
            if (bridge.LastCommandResult == FloatActionResult.Cooldown) { Refuse(reason); return; }
            // A click must never leave the player stuck on a rail: let go instead.
            Refuse(bridge.Release() == FloatActionResult.Done ? reason + " Let go of the rail instead." : reason);
        }

        void DispatchStrong()
        {
            LastFootResult = null; LastRefusal = string.Empty;
            if (player.HeldHandhold) { DispatchHand(ContactHand.Left, false, true); return; }
            if (bridge.IsBusy || player.PushCooldownRemaining > 0f) { Queue(ContactHand.Left, false, true, true); return; }
            Vector3 travel = ReadTravelIntent();
            if (player.TryGetFootSupport(out Vector3 deck))
            {
                // Legs from the deck: towards the W/S intent within the deck's outward cone, else straight off it.
                Vector3 intent = travel != Vector3.zero ? travel : deck;
                if (!MicrogravityLocomotion.TryOutwardDirection(deck, intent, player.Microgravity.MinimumAwayDot, out Vector3 direction))
                { Refuse("The leg push needs a valid direction."); return; }
                LastFootResult = player.TryPushFromFootContact(direction, footStrength);
                if (LastFootResult == FloatActionResult.Done) { LegPushPerformed?.Invoke(direction); return; }
                Refuse(FootRefusalReason(LastFootResult.Value));
                return;
            }
            // No deck under the feet: both hands on the aimed or the nearest reachable surface.
            DispatchHand(ContactHand.Left, false, true);
        }

        void Queue(ContactHand hand, bool grip, bool pair, bool strong, string reason = "Still recovering from the last push; press again.")
        {
            if (!hasQueued) queuedUntil = Time.unscaledTime + QueueSeconds; // A retry never extends its own wait.
            hasQueued = true; queuedHand = hand; queuedGrip = grip; queuedPair = pair; queuedStrong = strong; queuedReason = reason;
        }

        void RetryQueued()
        {
            if (!hasQueued) return;
            if (Time.unscaledTime > queuedUntil) { hasQueued = false; Refuse(queuedReason); return; }
            if (!player.HeldHandhold && bridge.IsBusy) return;
            if (!queuedGrip && player.PushCooldownRemaining > 0f) return;
            hasQueued = false;
            float until = queuedUntil;
            if (queuedStrong) DispatchStrong(); else DispatchHand(queuedHand, queuedGrip, queuedPair);
            if (hasQueued) queuedUntil = until; // A re-queued press keeps its original deadline.
        }

        void RefreshPreview()
        {
            if (Time.unscaledTime < nextPreviewAt) return;
            nextPreviewAt = Time.unscaledTime + .1f;
            ClearPreview();
            if (bridge.HoldsRail)
            {
                Preview = ContactPreview.PushOffRail; PreviewSurface = bridge.HeldRail;
                PreviewPoint = player.HeldGripPoint ?? Vector3.zero; PreviewAimed = true;
                return;
            }
            if (player.HeldHandhold) return;
            StrongPushUsesLegs = player.TryGetFootSupport(out _);
            if (HasRailGrip && !bridge.IsBusy && PreferRailGrip(out RaycastHit rail) && bridge.CanArmReach(rail.point, null))
            {
                Preview = ContactPreview.Grip; PreviewSurface = rail.collider; PreviewPoint = rail.point; PreviewAimed = true;
                return;
            }
            if (bridge.TrySelectPushSurface(eye.forward, null, out RaycastHit hit, out bool aimed))
            {
                Preview = ContactPreview.Push; PreviewSurface = hit.collider; PreviewPoint = hit.point; PreviewAimed = aimed;
            }
        }

        void ClearPreview()
        {
            Preview = ContactPreview.None; PreviewSurface = null; PreviewPoint = Vector3.zero;
            PreviewAimed = false; StrongPushUsesLegs = false;
        }

        bool ModeValid() => player && bridge && bridge.IsBound && eye && eye == player.ViewPivot && player.IsFloating
            && !player.IsOnRoute && !suit.IsOutside && state.Outcome == MissionOutcome.None
            && state.Phase != GamePhase.Planning
            && state.Phase != GamePhase.Outcome && state.Phase != GamePhase.Report;
        bool SessionCurrent()
        {
            try { return bound && currentSession != null && currentSession(); }
            catch (Exception error) { Fault("Session policy failed: " + error.Message); return false; }
        }
        bool Permission()
        {
            try { return !faulted && player && player.ControlAllowed && !clock.IsPaused && Time.timeScale > 0f && contactPermission(); }
            catch (Exception error) { Fault("Contact policy failed: " + error.Message); return false; }
        }
        void Fault(string reason)
        {
            faulted = true; permissionWasAllowed = false; leftReady = rightReady = footReady = handClickPending = false;
            hasQueued = false; ClearPreview();
            if (bridge) bridge.SetContactModeEnabled(false);
            lease.Release(); source = null; LastRefusal = reason; RefusalRevision++;
            // Keep requested=true until explicit Disarm so root cannot fall back to decorative click/E gestures.
        }
        bool ConflictingControls()
        {
            Mouse mouse = Mouse.current; Keyboard keyboard = Keyboard.current;
            foreach (InputAction action in source.Map.actions)
                foreach (var control in action.controls)
                    if ((mouse != null && (control == mouse.leftButton || control == mouse.rightButton))
                        || (keyboard != null && control == keyboard.spaceKey)) return true;
            return false;
        }
        void PrimeFromReleasedButtons()
        {
            Mouse mouse = Mouse.current; Keyboard keyboard = Keyboard.current;
            leftReady = mouse != null && !mouse.leftButton.isPressed;
            rightReady = mouse != null && !mouse.rightButton.isPressed;
            footReady = keyboard != null && !keyboard.spaceKey.isPressed;
        }
        bool PreferRailGrip() => PreferRailGrip(out _);
        bool PreferRailGrip(out RaycastHit nearest)
        {
            // Dispatch selection only. The bridge re-proves nearest surface, visibility, authored pose and contact.
            nearest = default;
            int count = Physics.RaycastNonAlloc(eye.position, eye.forward, hits, Mathf.Min(.8f, player.Microgravity.ReachMetres),
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(player.transform) && hits[i].distance < distance)
                { distance = hits[i].distance; nearest = hits[i]; }
            if (!(nearest.collider is CapsuleCollider rail) || !player.IsHandhold(rail)) return false;
            Transform t = rail.transform;
            Vector3 x = t.TransformVector(Vector3.right), y = t.TransformVector(Vector3.up), z = t.TransformVector(Vector3.forward);
            if (Mathf.Min(x.magnitude, Mathf.Min(y.magnitude, z.magnitude)) < .0001f
                || Mathf.Abs(Vector3.Dot(x.normalized, y.normalized)) > .001f
                || Mathf.Abs(Vector3.Dot(x.normalized, z.normalized)) > .001f
                || Mathf.Abs(Vector3.Dot(y.normalized, z.normalized)) > .001f) return false;
            Vector3 axis = (rail.direction == 0 ? x : rail.direction == 1 ? y : z);
            float a = rail.direction == 0 ? y.magnitude : x.magnitude;
            float b = rail.direction == 2 ? y.magnitude : z.magnitude;
            float radius = rail.radius * a;
            if (Mathf.Abs(a - b) > Mathf.Max(a, b) * .001f || radius < .012f || radius > .017f) return false;
            Vector3 delta = nearest.point - t.TransformPoint(rail.center);
            float axial = Vector3.Dot(delta, axis.normalized), straightHalf = rail.height * axis.magnitude * .5f - radius;
            return straightHalf > .04f && Mathf.Abs(axial) <= straightHalf - .04f;
        }
        bool Refuse(string reason) { LastRefusal = string.IsNullOrEmpty(reason) ? "Contact request refused." : reason; RefusalRevision++; return false; }

        /// <summary>Readable HUD text for a refused leg push from the deck.</summary>
        public static string FootRefusalReason(FloatActionResult result)
        {
            switch (result)
            {
                case FloatActionResult.Done: return string.Empty;
                case FloatActionResult.NoSurface: return "Your feet are not on the deck any more; Space now pushes with both hands.";
                case FloatActionResult.IntoSurface: return "That leg push would drive into the deck. Look or press W away from it.";
                case FloatActionResult.Cooldown: return "Still recovering from the last push.";
                case FloatActionResult.AlreadyHolding: return "Let go of the rail first, or click to push off it.";
                case FloatActionResult.ControlDenied: return "Controls are locked right now.";
                case FloatActionResult.NotFloating: return "The leg push works only while floating.";
                case FloatActionResult.OnRoute: return "On the tether, Space does not push off.";
                case FloatActionResult.InvalidInput: return "The leg push needs a valid direction.";
                default: return "Leg push refused: " + result + ".";
            }
        }
        static bool ValidStrength(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f && value <= 1f;
    }
}
