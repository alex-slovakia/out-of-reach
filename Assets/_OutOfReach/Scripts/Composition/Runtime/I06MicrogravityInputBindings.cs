using System;
using OutOfReach.Contracts;
using OutOfReach.Player;
using OutOfReach.Presentation.Hands;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OutOfReach.Composition
{
    /// <summary>Explicit input routing only. The existing bridge owns hand contact and Player owns all forces.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(50)]
    public sealed class I06MicrogravityInputBindings : MonoBehaviour
    {
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
        Vector3 pendingTravel;

        /// <summary>Root must suppress decorative L/R and successful-E gestures while true, including refusal/pause.</summary>
        public bool ContactRoutingRequested => armed;
        public bool HasInputLease => armed && !faulted && player && ReferenceEquals(player.InputSource, source) && lease.IsOwned;
        public bool IsFaulted => faulted;
        public bool HasRailGrip => bound && (suit.IsSuitOn || bridge.WorkGripAvailable);
        public bool AimAtReachableRail() => HasInputLease && HasRailGrip && !bridge.IsBusy && PreferRailGrip();
        public string LastRefusal { get; private set; } = string.Empty;
        public FloatActionResult? LastFootResult { get; private set; }

        /// <param name="sessionIsCurrent">Identity check for the exact session/services supplied here.</param>
        /// <param name="canContact">Current root policy: excludes cargo, rest, menus, terminal, cinematics and transfers.</param>
        public void Configure(FirstPersonController actualPlayer, I06MicrogravityBridge contactBridge,
            Transform unbobbedEye, IEvaStatus evaStatus, IGameState gameState, ISimulationClock simulationClock,
            Func<bool> sessionIsCurrent, Func<bool> canContact, float pushStrength01 = .35f, float footStrength01 = .35f)
        {
            ClearBinding();
            if (!actualPlayer || !contactBridge || !unbobbedEye || unbobbedEye != actualPlayer.ViewPivot
                || evaStatus == null || gameState == null || simulationClock == null
                || sessionIsCurrent == null || canContact == null || !ValidStrength(pushStrength01) || !ValidStrength(footStrength01))
                throw new ArgumentException("Use the current Player, bound contact bridge and its exact unbobbed ViewPivot with explicit session/policy gates.");
            player = actualPlayer; bridge = contactBridge; eye = unbobbedEye; suit = evaStatus;
            state = gameState; clock = simulationClock; currentSession = sessionIsCurrent; contactPermission = canContact;
            handStrength = pushStrength01; footStrength = footStrength01; bound = true;
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
            if (bridge) bridge.SetContactModeEnabled(false);
            lease.Release(); source = null;
        }

        public void ClearBinding()
        {
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
            if (!ValidateOwnership()) return;
            if (!Permission() || !source.Map.enabled)
            {
                if (permissionWasAllowed || bridge.IsBusy) bridge.Release();
                permissionWasAllowed = false; leftReady = rightReady = footReady = handClickPending = false;
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
            bool foot = keyboard != null && footReady && keyboard.spaceKey.wasPressedThisFrame;
            if (left) leftReady = false;
            if (right) rightReady = false;
            if (foot) footReady = false;
            bool grip = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
            if (HandleHandInput(mouse, left, right, grip)) return;
            if (foot) DispatchFoot();
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
                    DispatchHand(pendingHand, false, true, ReadTravelIntent());
                }
                else if (!firstHeld || Time.unscaledTime >= pendingUntil)
                {
                    handClickPending = false;
                    DispatchHand(pendingHand, false, false, pendingTravel);
                }
                return true;
            }
            if (!left && !right) return false;
            ContactHand hand = left ? ContactHand.Left : ContactHand.Right;
            if ((left && right) || grip || player.HeldHandhold)
                DispatchHand(hand, grip && !(left && right), left && right, ReadTravelIntent());
            else
            {
                handClickPending = true; pendingHand = hand;
                pendingTravel = ReadTravelIntent();
                pendingUntil = Time.unscaledTime + ChordSeconds;
            }
            return true;
        }

        Vector3 ReadTravelIntent()
        {
            Vector2 move = source.Move;
            return move.sqrMagnitude > .01f
                ? player.transform.TransformDirection(new Vector3(move.x, 0f, move.y)) : Vector3.zero;
        }

        void DispatchHand(ContactHand hand, bool grip, bool pair, Vector3 travel)
        {
            LastRefusal = string.Empty;
            if (player.HeldHandhold)
            {
                FloatActionResult result = bridge.Release();
                if (result != FloatActionResult.Done) Refuse("The held grip is not owned by this contact bridge.");
                return;
            }
            if (bridge.IsBusy) { Refuse("The current hand contact is still in progress."); return; }
            if (grip)
            {
                if (!bridge.TryGrab(hand, eye.forward)) Refuse(bridge.LastRefusal);
                return;
            }
            bool accepted = pair ? bridge.TryPushPair(eye.forward, travel, handStrength)
                : travel == Vector3.zero ? bridge.TryPush(hand, eye.forward, handStrength)
                    : bridge.TryPushDirected(hand, eye.forward, travel, handStrength);
            if (!accepted) Refuse(bridge.LastRefusal);
        }

        void DispatchFoot()
        {
            LastFootResult = null; LastRefusal = string.Empty;
            if (bridge.IsBusy || player.HeldHandhold) { Refuse("Release the hand contact before pushing from foot support."); return; }
            Vector2 move = source.Move;
            Vector3 up = player.transform.up, direction = up;
            if (move.sqrMagnitude > .01f)
            {
                Vector3 tangent = player.transform.TransformDirection(new Vector3(move.x, 0f, move.y)).normalized;
                // Flat-deck tuning: at default MinimumAwayDot=.2 this is .225 upward, .974 tangent.
                // Actual Player still proves foot contact and validates the real support normal; slopes may refuse.
                float away = Mathf.Clamp(Mathf.Max(.225f, player.Microgravity.MinimumAwayDot + .025f), 0f, 1f);
                direction = up * away + tangent * Mathf.Sqrt(1f - away * away);
            }
            LastFootResult = player.TryPushFromFootContact(direction, footStrength);
            if (LastFootResult != FloatActionResult.Done) Refuse("Foot push refused: " + LastFootResult + ".");
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
            if (bridge) bridge.SetContactModeEnabled(false);
            lease.Release(); source = null; LastRefusal = reason;
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
        bool PreferRailGrip()
        {
            // Dispatch selection only. The bridge re-proves nearest surface, visibility, authored pose and contact.
            int count = Physics.RaycastNonAlloc(eye.position, eye.forward, hits, Mathf.Min(.8f, player.Microgravity.ReachMetres),
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            RaycastHit nearest = default; float distance = float.PositiveInfinity;
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
        bool Refuse(string reason) { LastRefusal = reason ?? "Contact request refused."; return false; }
        static bool ValidStrength(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f && value <= 1f;
    }
}
