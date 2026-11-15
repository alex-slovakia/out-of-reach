using System;
using OutOfReach.Contracts;
using OutOfReach.Player;
using OutOfReach.Presentation.Hands;
using OutOfReach.Presentation.Motion;
using UnityEngine;
using Arms = OutOfReach.Presentation.Motion.EmbodiedBodyPresenter.BodyArmVisibility;

namespace OutOfReach.Composition
{
    /// <summary>
    /// Explicit composition adapter. One dispatched request reaches a real surface visually before the
    /// existing Player command runs. No input polling, automatic float entry or movement/renderer writer.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(300)]
    public sealed class I06MicrogravityBridge : MonoBehaviour
    {
        [SerializeField] BareRailGripPose workGripPose;
        public bool WorkGripAvailable => bound && workGripPose && workGripPose.IsValid && bare && bare.GripPoseConfigured;
        public void ConfigureWorkGripPose(BareRailGripPose pose)
        { CancelOwnedContact(); workGripPose = pose; if (bare) bare.ConfigureGripPose(pose); }
        const float WristPadding = .01f, PointTolerance = .004f;
        readonly RaycastHit[] hits = new RaycastHit[64];
        readonly Collider[] overlaps = new Collider[64];
        FirstPersonController player;
        EmbodiedBodyPresenter body;
        AstronautHandsPresenter gloves;
        BareArmContactPresenter bare;
        IEvaStatus suit;
        IGameState state;
        ISimulationClock clock;
        Func<bool> rootPolicy;
        bool bound, armed, pending, pendingSuited, actionSent, cancelling;
        bool gripReturning;
        int generation, bindingGeneration;
        GamePhase phase;
        ContactHand hand;
        HandContactKind kind;
        Collider surface, ownedGrip;
        HandContactTarget target;
        float pushStrength, requestStarted;
        Vector3 localPushDirection;
        PushContact secondPush;
        HandContactSample leftSample;
        Vector3 leftDirection;
        int leftContactFrame = -1;

        readonly struct PushContact
        {
            public readonly Collider Surface;
            public readonly HandContactTarget Target;
            public readonly Vector3 LocalDirection;

            public PushContact(Collider surface, HandContactTarget target, Vector3 localDirection)
            { Surface = surface; Target = target; LocalDirection = localDirection; }
        }

        public bool IsBound => bound;
        /// <summary>Explicit routing mode, not permission. A rejected request must not fall back to a decorative press.</summary>
        public bool ContactModeEnabled => bound && armed;
        public bool IsBusy => pending;
        public FloatActionResult? LastCommandResult { get; private set; }
        public string LastRefusal { get; private set; } = string.Empty;
        public event Action HandPushCompleted;

        /// <summary>
        /// Call after body.ConfigureSession and gloves.ConfigureSession. Root policy must read the current
        /// session identity and exclude cargo, rest, terminal leases, menus, cinematics and transitions.
        /// Configure does not enter float or enable contact mode. This adapter exclusively owns contact bindings.
        /// </summary>
        public void Configure(FirstPersonController controller, EmbodiedBodyPresenter bodyPresenter,
            AstronautHandsPresenter glovePresenter, BareArmContactPresenter barePresenter,
            IEvaStatus evaStatus, IGameState gameState, ISimulationClock simulationClock, Func<bool> contactPolicy)
        {
            int expectedBinding = bindingGeneration + 1;
            Clear();
            if (bindingGeneration != expectedBinding) return; // A synchronous newer root binding wins.
            if (!controller || !controller.ViewPivot || !bodyPresenter || !bodyPresenter.IsBound
                || !glovePresenter || !glovePresenter.IsBound || !barePresenter || evaStatus == null
                || gameState == null || simulationClock == null || contactPolicy == null)
                throw new ArgumentException("Bind the sole Player and existing body/glove sessions before the contact bridge.");
            player = controller; body = bodyPresenter; gloves = glovePresenter; bare = barePresenter;
            suit = evaStatus; state = gameState; clock = simulationClock; rootPolicy = contactPolicy;
            phase = state.Phase; bound = true;
            try
            {
                bare.Configure(WorkArm("L", body.WorkLeftArmRenderer), WorkArm("R", body.WorkRightArmRenderer),
                    player.ViewPivot, clock, WorkLeaseAllowed, ReadPoseRevision, ValidateContact);
                bare.ConfigureGripPose(workGripPose);
                gloves.ConfigureContacts(player.ViewPivot, GloveLeaseAllowed, ValidateContact);
                body.PoseWillReset += OnPoseWillReset;
                suit.Changed += OnSuitChanged;
                player.Released += OnPlayerReleased;
            }
            catch { Clear(); throw; }
        }

        public void SetContactModeEnabled(bool enabled)
        {
            bool next = bound && enabled;
            if (armed == next) return;
            armed = next;
            if (!armed) CancelOwnedContact();
        }

        /// <summary>True means a pending visible reach, never immediate physics success. Direction is from the real unbobbed eye.</summary>
        public bool TryPush(ContactHand selectedHand, Vector3 aimDirection, float strength01 = .35f)
        {
            if (!Finite(strength01) || strength01 <= 0f || strength01 > 1f) return Refuse("Invalid push strength.");
            return Begin(selectedHand, HandContactKind.Push, aimDirection, strength01);
        }

        /// <summary>
        /// One contact-only directional push. Already-away intent is retained; other intent is limited to an
        /// outward cone, smoothly approaching the normal for nearly inward intent. Direction is captured locally and dispatched
        /// only after the existing visible skin contact proof. Changing input during anticipation adds no force.
        /// </summary>
        public bool TryPushDirected(ContactHand selectedHand, Vector3 aimDirection, Vector3 desiredTravelWorld, float strength01 = .35f)
        {
            if (!Finite(strength01) || strength01 <= 0f || strength01 > 1f
                || !Finite(desiredTravelWorld) || !Finite(desiredTravelWorld.sqrMagnitude)
                || desiredTravelWorld.sqrMagnitude < .0001f)
                return Refuse("Invalid directional push request.");
            return Begin(selectedHand, HandContactKind.Push, aimDirection, strength01, desiredTravelWorld);
        }

        public bool TryPushPair(Vector3 aimDirection, Vector3 desiredTravelWorld, float strength01 = .35f)
        {
            if (!Finite(strength01) || strength01 <= 0f || strength01 > 1f
                || !Finite(desiredTravelWorld) || !Finite(desiredTravelWorld.sqrMagnitude))
                return Refuse("Invalid two-hand push request.");
            return Begin(ContactHand.Left, HandContactKind.Push, aimDirection, strength01, desiredTravelWorld, true);
        }

        /// <summary>Nearest hit must be a registered straight capsule rail with the correct radius for the current outfit.</summary>
        public bool TryGrab(ContactHand selectedHand, Vector3 aimDirection) => Begin(selectedHand, HandContactKind.Grip, aimDirection, 0f);

        /// <summary>True while this bridge's own verified grip holds a rail (the Player's held handhold).</summary>
        public bool HoldsRail => bound && pending && kind == HandContactKind.Grip && ownedGrip && player
            && player.HeldHandhold == ownedGrip && player.HeldGripPoint.HasValue;

        /// <summary>The rail held by this bridge, or null.</summary>
        public Collider HeldRail => HoldsRail ? ownedGrip : null;

        /// <summary>
        /// MOVE-MECH-01 selection, read-only: the surface a push would use. First the solid surface the view ray meets
        /// within reach (<paramref name="aimed"/> true); otherwise the nearest reachable solid surface around the eye
        /// (walls, ceiling, floor, slopes, solid props) along body-relative probe rays in the front hemisphere and to
        /// the sides, preferring the given hand's side. Owned colliders, triggers, small decorative colliders (less
        /// than 8 cm across in two dimensions) and, with bare WORK arms, points the real shoulder cannot reach never
        /// count. No force, no animation, no state change.
        /// </summary>
        public bool TrySelectPushSurface(Vector3 aimDirection, ContactHand? preferredHand, out RaycastHit hit, out bool aimed)
        {
            int count = CollectPushCandidates(aimDirection, preferredHand, false);
            hit = count > 0 ? candidates[0].Hit : default;
            aimed = count > 0 && candidates[0].Aimed;
            return count > 0;
        }

        /// <summary>
        /// One medium hand push from the selected surface (see <see cref="TrySelectPushSurface"/>). With an explicit
        /// travel intent the push follows it, limited to the outward cone of the real surface. Without one, an aimed
        /// surface is pushed straight away from, and a surface found around the body sends the body where the view
        /// points (outward cone). When the arm presenter refuses the best spot (pose, wrist, clearance), the next
        /// candidates are tried, up to <see cref="MaxPushAttempts"/>. The visible reach and the Player still decide.
        /// </summary>
        public bool TryPushContext(ContactHand selectedHand, Vector3 aimDirection, Vector3 desiredTravelWorld, float strength01)
        {
            LastRefusal = string.Empty;
            int count = CollectPushCandidates(aimDirection, selectedHand, false);
            if (count == 0) return Refuse(NoSurfaceReason());
            string first = null;
            for (int i = 0; i < count && i < MaxPushAttempts; i++)
            {
                PushCandidate c = candidates[i];
                Vector3 intent = ContextIntent(desiredTravelWorld, c.Aimed);
                Vector3 toward = c.Hit.point - player.ViewPivot.position;
                bool accepted = intent == Vector3.zero ? TryPush(selectedHand, toward, strength01)
                    : TryPushDirected(selectedHand, toward, intent, strength01);
                if (accepted) return true;
                if (first == null) first = LastRefusal;
            }
            return Refuse(first);
        }

        /// <summary>Both hands on the selected surface: one coherent paired push (see <see cref="TryPushContext"/>).</summary>
        public bool TryPushPairContext(Vector3 aimDirection, Vector3 desiredTravelWorld, float strength01)
        {
            LastRefusal = string.Empty;
            int count = CollectPushCandidates(aimDirection, null, true);
            if (count == 0) return Refuse(NoSurfaceReason());
            string first = null;
            for (int i = 0; i < count && i < MaxPushAttempts; i++)
            {
                PushCandidate c = candidates[i];
                if (TryPushPair(c.Hit.point - player.ViewPivot.position, ContextIntent(desiredTravelWorld, c.Aimed), strength01)) return true;
                if (first == null) first = LastRefusal;
            }
            return Refuse(first);
        }

        /// <summary>How many candidate spots one click may try when the arm presenter refuses the best one.</summary>
        public const int MaxPushAttempts = 4;
        const string NoSurfaceText = "No solid surface within reach. Drift closer to a wall, the floor or the ceiling.";
        const string OutOfViewText = "Turn towards the wall, ceiling or floor you want to push from: your hands push only where you can see them.";

        /// <summary>Readable reason for an empty selection: nothing in reach at all, or a surface in reach behind the view.</summary>
        public string NoSurfaceReason()
        {
            if (!bound || !player || !player.ViewPivot) return NoSurfaceText;
            Vector3 origin = player.ViewPivot.position;
            foreach (Vector3 axis in WorldAxes)
                if (Nearest(origin, axis, Reach, out RaycastHit hit) && Pushable(hit.collider)) return OutOfViewText;
            return NoSurfaceText;
        }

        static readonly Vector3[] WorldAxes = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

        struct PushCandidate
        {
            public RaycastHit Hit;
            public bool Aimed;
            public float Score;
        }

        readonly PushCandidate[] candidates = new PushCandidate[32];

        int CollectPushCandidates(Vector3 aimDirection, ContactHand? preferredHand, bool bothHands)
        {
            if (!bound || !player || !player.ViewPivot) return 0;
            Transform view = player.ViewPivot;
            Vector3 origin = view.position;
            int count = 0;
            if (Finite(aimDirection) && aimDirection.sqrMagnitude > .00001f
                && Nearest(origin, aimDirection.normalized, Reach, out RaycastHit aimedHit) && Pushable(aimedHit.collider)
                && ArmCanReach(aimedHit.point, preferredHand, bothHands))
                candidates[count++] = new PushCandidate { Hit = aimedHit, Aimed = true, Score = -1f };
            foreach (Vector3 local in ProbeDirections)
            {
                if (count >= candidates.Length) break;
                Vector3 direction = view.TransformDirection(local);
                if (!Nearest(origin, direction, Reach, out RaycastHit hit) || !Pushable(hit.collider)
                    || !ArmCanReach(hit.point, preferredHand, bothHands)) continue;
                bool duplicate = false;
                for (int i = 0; i < count && !duplicate; i++)
                    duplicate = candidates[i].Hit.collider == hit.collider && Vector3.Distance(candidates[i].Hit.point, hit.point) < .05f;
                if (duplicate) continue;
                float score = hit.distance;
                if (preferredHand.HasValue)
                {
                    float side = Vector3.Dot(hit.point - origin, view.right);
                    // Keep the other hand's side usable, but let the clicking hand's side win a near tie.
                    if ((preferredHand.Value == ContactHand.Left && side > .05f) || (preferredHand.Value == ContactHand.Right && side < -.05f))
                        score += .15f;
                }
                candidates[count++] = new PushCandidate { Hit = hit, Aimed = false, Score = score };
            }
            for (int i = 1; i < count; i++) // Insertion sort: at most 25 entries, no allocation.
            {
                PushCandidate item = candidates[i];
                int j = i - 1;
                while (j >= 0 && candidates[j].Score > item.Score) { candidates[j + 1] = candidates[j]; j--; }
                candidates[j + 1] = item;
            }
            return count;
        }

        // Bare WORK arms: the real shoulder must reach the palm spot with a bent elbow (the arm presenter refuses a
        // straight or over-stretched arm only after its reach animation has started). Gloves keep their own checks.
        // No hand given: a paired push needs both shoulders, a preview or selection either one.
        bool ArmCanReach(Vector3 point, ContactHand? hand, bool bothHands)
        {
            if (suit == null || suit.IsSuitOn || !bare) return true;
            if (hand.HasValue) return ShoulderReaches(hand.Value, point);
            return bothHands ? ShoulderReaches(ContactHand.Left, point) && ShoulderReaches(ContactHand.Right, point)
                : ShoulderReaches(ContactHand.Left, point) || ShoulderReaches(ContactHand.Right, point);
        }

        bool ShoulderReaches(ContactHand hand, Vector3 point)
        {
            if (!bare.TryGetPushGeometry(hand, out Vector3 shoulder, out float upperArm, out float forearm, out _)) return true;
            return Vector3.Distance(shoulder, point) <= (upperArm + forearm) * .93f;
        }

        Vector3 ContextIntent(Vector3 desiredTravelWorld, bool aimed)
        {
            if (Finite(desiredTravelWorld) && desiredTravelWorld.sqrMagnitude > .0001f) return desiredTravelWorld;
            return aimed ? Vector3.zero : player.ViewPivot.forward;
        }

        /// <summary>
        /// While this bridge holds a rail: push off from that rail in one action, towards the travel intent (or straight
        /// away without one), limited to the outward cone of the real rail surface at the held anchor. The Player
        /// verifies reach, line of sight and direction; on success the grip ends with the push and the hand lets go.
        /// A cooldown refusal keeps the grip.
        /// </summary>
        public bool TryPushOffHeldRail(Vector3 desiredTravelWorld, float strength01)
        {
            LastRefusal = string.Empty; LastCommandResult = null;
            if (!ModeAllowed()) return Refuse("Contact mode is unavailable.");
            if (!HoldsRail) return Refuse("No rail is held by this contact.");
            if (!Finite(strength01) || strength01 <= 0f || strength01 > 1f || !Finite(desiredTravelWorld))
                return Refuse("Invalid push request.");
            Vector3 eye = player.ViewPivot.position, anchor = player.HeldGripPoint.Value, delta = anchor - eye;
            float distance = delta.magnitude;
            if (distance < .001f || !Nearest(eye, delta / distance, distance + .02f, out RaycastHit hit) || hit.collider != ownedGrip)
                return Refuse("The held rail is not in a clear line of reach.");
            Vector3 intent = desiredTravelWorld.sqrMagnitude > .0001f ? desiredTravelWorld : hit.normal;
            if (!MicrogravityLocomotion.TryOutwardDirection(hit.normal, intent, player.Microgravity.MinimumAwayDot, out Vector3 direction))
                return Refuse("Invalid push direction.");
            Collider rail = ownedGrip;
            bool suited = pendingSuited;
            // The push releases the Player grip synchronously; this bridge already let go of it, so the Released
            // event must not cancel the hand that is about to return.
            ownedGrip = null;
            FloatActionResult result = player.TryPushOff(rail, hit.point, direction, strength01);
            LastCommandResult = result;
            if (result != FloatActionResult.Done)
            {
                if (player.HeldHandhold == rail) ownedGrip = rail; // Refused before anything changed: still holding.
                else CancelOwnedContact();
                return Refuse("Push off the rail refused: " + result + ".");
            }
            if (!suited && bare && bare.ContactStage == HandContactStage.Holding)
            { bare.CancelContact(); requestStarted = Time.unscaledTime; }
            else CancelOwnedContact();
            HandPushCompleted?.Invoke();
            return true;
        }

        // Body-relative probe rays for a surface around the eye: the front hemisphere and the sides, never behind the head.
        static readonly Vector3[] ProbeDirections = BuildProbeDirections();

        static Vector3[] BuildProbeDirections()
        {
            var list = new System.Collections.Generic.List<Vector3>();
            foreach (float z in new[] { 1f, 1f, 0f })
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                    {
                        if (x == 0 && y == 0) continue;
                        list.Add(new Vector3(x, y, z).normalized);
                    }
            // The first z = 1 pass is a near-view cone: halve its sideways and vertical part.
            for (int i = 0; i < 8; i++) list[i] = new Vector3(list[i].x * .5f, list[i].y * .5f, list[i].z).normalized;
            return list.ToArray();
        }

        bool Pushable(Collider c)
        {
            if (!ValidSurface(c)) return false;
            if (player.IsHandhold(c)) return true;
            Vector3 size = c.bounds.size;
            int wide = (size.x >= .08f ? 1 : 0) + (size.y >= .08f ? 1 : 0) + (size.z >= .08f ? 1 : 0);
            return wide >= 2;
        }

        /// <summary>Release dispatched by the same input owner. Cancels pending animation and only this bridge's held grip.</summary>
        public FloatActionResult Release()
        {
            bool held = player && ownedGrip && player.HeldHandhold == ownedGrip;
            bool floating = player && player.IsFloating;
            if (pending && !pendingSuited && kind == HandContactKind.Grip && bare
                && bare.ContactStage == HandContactStage.Holding && ModeAllowed())
            {
                Collider released = ownedGrip; ownedGrip = null;
                if (player && released && player.HeldHandhold == released) player.Release();
                bare.CancelContact(); requestStarted = Time.unscaledTime;
                return held ? FloatActionResult.Done : floating ? FloatActionResult.NotHolding : FloatActionResult.NotFloating;
            }
            CancelOwnedContact();
            return held ? FloatActionResult.Done : floating ? FloatActionResult.NotHolding : FloatActionResult.NotFloating;
        }

        public void Clear()
        {
            int token = ++bindingGeneration;
            armed = false;
            if (body) body.PoseWillReset -= OnPoseWillReset;
            if (suit != null) suit.Changed -= OnSuitChanged;
            if (player) player.Released -= OnPlayerReleased;
            CancelOwnedContact();
            if (token != bindingGeneration) return;
            if (bare) bare.ClearBinding();
            // Hands has no separate ClearContacts API. Replace only this optional binding with static fail-closed
            // delegates; keep the original glove session, model/mount and visibility owner intact.
            if (gloves && gloves.IsBound && player && player.ViewPivot)
                gloves.ConfigureContacts(player.ViewPivot, DisabledGate, DeniedContact);
            bound = false; player = null; body = null; gloves = null; bare = null;
            suit = null; state = null; clock = null; rootPolicy = null;
            LastCommandResult = null; LastRefusal = string.Empty;
        }

        void OnDisable() => Clear();
        void OnDestroy() => Clear();
        void Update()
        {
            if (!bound) return;
            if (phase != state.Phase)
            {
                phase = state.Phase;
                CancelOwnedContact();
            }
            if (pending && (!ModeAllowed() || pendingSuited != suit.IsSuitOn || !VisualLeaseAllowed(hand)
                || (secondPush.Surface && !VisualLeaseAllowed(ContactHand.Right)))) CancelOwnedContact();
        }
        void LateUpdate()
        {
            if (!pending) return;
            if (!ModeAllowed() || pendingSuited != suit.IsSuitOn) { CancelOwnedContact(); return; }
            HandContactStage stage = ContactStageFor(hand);
            if (secondPush.Surface)
            {
                HandContactStage other = ContactStageFor(ContactHand.Right);
                if (!actionSent && (!IsApproaching(stage) || !IsApproaching(other))) CancelOwnedContact();
                else if ((stage == HandContactStage.None && other == HandContactStage.None)
                    || Time.unscaledTime - requestStarted > 1.5f) CancelOwnedContact();
                return;
            }
            if (!pendingSuited && kind == HandContactKind.Grip && stage == HandContactStage.Returning && !gripReturning)
            { gripReturning = true; requestStarted = Time.unscaledTime; }
            if (stage == HandContactStage.None || (ownedGrip && stage != HandContactStage.Holding)
                || (stage != HandContactStage.Holding && Time.unscaledTime - requestStarted > 1.5f)) CancelOwnedContact();
        }

        HandContactStage ContactStageFor(ContactHand selectedHand) => pendingSuited
            ? gloves.ContactStageFor(selectedHand) : bare.ContactStageFor(selectedHand);

        static bool IsApproaching(HandContactStage stage) =>
            stage == HandContactStage.Reaching || stage == HandContactStage.PushContact;

        bool Begin(ContactHand selectedHand, HandContactKind requestedKind, Vector3 aimDirection, float strength,
            Vector3 desiredTravelWorld = default, bool twoHands = false)
        {
            LastRefusal = string.Empty; LastCommandResult = null;
            if (!ModeAllowed()) return Refuse("Contact mode is unavailable.");
            if (pending || player.HeldHandhold) return Refuse("Release the current contact first.");
            if (selectedHand != ContactHand.Left && selectedHand != ContactHand.Right) return Refuse("Invalid hand.");
            if (!VisualLeaseAllowed(selectedHand) || (twoHands && !VisualLeaseAllowed(ContactHand.Right)))
                return Refuse("Selected arm does not have the visual lease.");
            if (requestedKind == HandContactKind.Grip && !suit.IsSuitOn && !WorkGripAvailable)
                return Refuse("Bare-hand grip needs the verified authored 15mm pose asset.");
            if (!Finite(aimDirection) || aimDirection.sqrMagnitude < .00001f
                || !Nearest(player.ViewPivot.position, aimDirection.normalized, Reach, out RaycastHit hit))
                return Refuse("No unobstructed solid surface within reach.");
            if (!TryBuildContact(selectedHand, requestedKind, hit, desiredTravelWorld, twoHands, out PushContact first))
                return false;
            PushContact second = default;
            if (twoHands && !TryBuildContact(ContactHand.Right, requestedKind, hit, desiredTravelWorld, true, out second))
                return false;

            hand = selectedHand; kind = requestedKind; surface = first.Surface; target = first.Target;
            localPushDirection = first.LocalDirection; secondPush = second; leftContactFrame = -1;
            pushStrength = strength; pendingSuited = suit.IsSuitOn; actionSent = false; pending = true; gripReturning = false;
            requestStarted = Time.unscaledTime;
            generation++;

            bool accepted;
            if (twoHands)
                accepted = pendingSuited
                    ? gloves.TryReachPushPair(target, secondPush.Target, AtPairContact)
                    : bare.TryReachPushPair(target, secondPush.Target, AtPairContact);
            else if (pendingSuited)
                accepted = gloves.TryReachContact(hand, kind, target, AtContact, kind == HandContactKind.Grip ? ReadHeld : (Func<HandContactTarget?>)null);
            else
                accepted = kind == HandContactKind.Grip ? bare.TryReachGrip(hand, target, AtContact, ReadHeld)
                    : bare.TryReachPush(hand, target, AtContact);
            if (accepted) return true;
            string reason = !pendingSuited && bare && !string.IsNullOrEmpty(bare.LastRefusalReason)
                ? "WORK contact: " + (string.IsNullOrEmpty(bare.LastRefusalDiagnostic) ? bare.LastRefusalReason : bare.LastRefusalDiagnostic)
                : "The actual hand pose, arm reach or approach path refused this contact.";
            CancelOwnedContact();
            return Refuse(reason);
        }

        bool TryBuildContact(ContactHand selectedHand, HandContactKind requestedKind, RaycastHit hit,
            Vector3 desiredTravelWorld, bool twoHands, out PushContact contact)
        {
            contact = default;
            bool barePush = requestedKind == HandContactKind.Push && !suit.IsSuitOn;
            // Both hands need separate unobstructed spots on the aimed surface.
            if ((barePush || twoHands) && !TryPalmPoint(selectedHand, hit, barePush, out hit))
                return Refuse("No verified palm point on the aimed surface within reach.");
            // MOVE-MECH-01: a level view against a ceiling (or any surface whose normal is along view up) has no
            // tangent from view up; fall back to the view forward, then right, so the frame is never degenerate.
            Vector3 tangent = StableTangent(hit.normal);
            if (barePush && Vector3.Dot(hit.normal, Vector3.down) > .95f)
            {
                // One diagonal inward style keeps both palms in view without crossing the midline.
                Vector3 inward = Vector3.ProjectOnPlane(player.ViewPivot.right
                    * (selectedHand == ContactHand.Left ? 1f : -1f), hit.normal).normalized;
                Vector3 forward = Vector3.ProjectOnPlane(player.ViewPivot.forward, hit.normal).normalized;
                tangent = (inward * .8660254f + forward * .5f).normalized;
            }
            float radius = 0f;
            if (requestedKind == HandContactKind.Grip && !RailFrame(hit.collider, hit.point, hit.normal, out tangent, out radius))
                return Refuse("Grip needs a registered 12–17mm straight capsule rail.");
            if (requestedKind == HandContactKind.Grip && !suit.IsSuitOn && Mathf.Abs(radius - .015f) > .00001f)
                return Refuse("Authored bare-hand grip supports exactly15mm radius rails.");
            if (tangent.sqrMagnitude < .99f) return Refuse("No stable contact frame.");
            if (requestedKind == HandContactKind.Grip) tangent = suit.IsSuitOn
                ? OrientRailAxisAtRequest(tangent, player.ViewPivot) : bare.OrientGripAxis(selectedHand, tangent, hit.normal);
            Vector3 direction = hit.normal;
            // Pure request-direction math (shared with the Player tuning); force and validated dispatch stay Player-owned.
            if (requestedKind == HandContactKind.Push && desiredTravelWorld.sqrMagnitude > .0001f
                && !MicrogravityLocomotion.TryOutwardDirection(hit.normal, desiredTravelWorld, player.Microgravity.MinimumAwayDot, out direction))
                return Refuse("Invalid directional push geometry.");
            contact = new PushContact(hit.collider, Frame(hit.collider, hit.point, hit.normal, tangent, radius),
                hit.collider.transform.InverseTransformVector(direction).normalized);
            return true;
        }

        Vector3 StableTangent(Vector3 normal)
        {
            Transform view = player.ViewPivot;
            Vector3 tangent = Vector3.ProjectOnPlane(view.up, normal);
            if (tangent.sqrMagnitude < .01f) tangent = Vector3.ProjectOnPlane(view.forward, normal);
            if (tangent.sqrMagnitude < .01f) tangent = Vector3.ProjectOnPlane(view.right, normal);
            return tangent.sqrMagnitude < .0001f ? Vector3.zero : tangent.normalized;
        }

        bool TryPalmPoint(ContactHand selectedHand, RaycastHit aimedHit, bool work, out RaycastHit palmHit)
        {
            palmHit = default;
            Vector3 across = Vector3.ProjectOnPlane(player.ViewPivot.right, aimedHit.normal);
            // MOVE-MECH-01: a wall straight to the side has no across axis from view right; spread the palms along
            // the wall instead (view forward, then up). The re-ray and arm solver still decide whether they are real.
            if (across.sqrMagnitude < .01f) across = Vector3.ProjectOnPlane(player.ViewPivot.forward, aimedHit.normal);
            if (across.sqrMagnitude < .01f) across = Vector3.ProjectOnPlane(player.ViewPivot.up, aimedHit.normal);
            if (across.sqrMagnitude < .01f) return false;
            across.Normalize();
            float halfSpacing = .06f;
            if (work)
            {
                if (!bare.TryGetPushGeometry(selectedHand, out Vector3 shoulder, out float upperArm,
                    out float forearm, out _)) return false;
                // Shoulder-width contact zones keep thumbs, fingers and sleeves apart. Their exact
                // same-surface rays and complete posed arm envelopes still have to pass below.
                halfSpacing = Mathf.Abs(Vector3.Dot(shoulder - player.ViewPivot.position, across)) + (upperArm + forearm) * .125f;
            }
            float side = selectedHand == ContactHand.Left ? -1f : 1f;
            Vector3 proposed = aimedHit.point + across * (side * halfSpacing);
            // At the ceiling the reticle can be 30cm ahead while the real shoulder-to-wrist chain is
            // only48cm. Select a nearby natural palm placement on that SAME aimed plane; the existing
            // re-ray, whole-skin solver, wrist limit and Player validation still decide whether it is real.
            if (work && Vector3.Dot(aimedHit.normal, Vector3.down) > .95f)
            {
                Vector3 heading = Vector3.ProjectOnPlane(player.ViewPivot.forward, aimedHit.normal);
                if (heading.sqrMagnitude < .0001f) return false;
                heading.Normalize();
                Vector3 eyeOnPlane = player.ViewPivot.position - aimedHit.normal
                    * Vector3.Dot(player.ViewPivot.position - aimedHit.point, aimedHit.normal);
                if (!bare.TryGetPushGeometry(selectedHand, out _, out float upperArm, out float forearm,
                    out float reachDuration)) return false;
                // The same body-relative landing point is used at every look pitch. It leaves a
                // bent elbow with the actual side-facing palm, and scales with the bound arm.
                float armSpan = upperArm + forearm;
                halfSpacing = armSpan * .33f;
                // MOVE-MECH-01 native QA (d1512f7): with the head at the ceiling, armSpan*.135 put the palm only
                // 6.5 cm ahead of the eye and the arm presenter refuses targets under 10 cm ahead (it keeps the
                // reach in view). 12.5 cm still leaves the wrist, about 8 cm below the palm, within the elbow range.
                Vector3 naturalPoint = eyeOnPlane + heading * Mathf.Max(armSpan * .135f, .125f)
                    + across * (side * halfSpacing);
                // Anticipate only drift away from this forward ceiling patch, during REAL reach
                // seconds. Approaching motion already increases reach margin and needs no lead.
                Vector3 drift = Vector3.ProjectOnPlane(player.FloatVelocity, aimedHit.normal) * reachDuration;
                Vector3 anticipated = naturalPoint + drift;
                Vector3 aimedZone = aimedHit.point + across * (side * halfSpacing);
                if (Finite(drift) && Vector3.Dot(drift, heading) < -.0001f
                    && Vector3.Distance(anticipated, aimedZone) <= .25f
                    && player.ViewPivot.InverseTransformPoint(anticipated).z >= .10f)
                    naturalPoint = anticipated;
                // This does not search around an edge or rescue an unrelated distant aim.
                if (Vector3.Distance(naturalPoint, aimedZone) > .25f) return false;
                proposed = naturalPoint;
            }
            Vector3 delta = proposed - player.ViewPivot.position;
            float distance = delta.magnitude;
            if (!Finite(proposed) || distance < .001f || distance > Reach
                || !Nearest(player.ViewPivot.position, delta / distance, Mathf.Min(Reach, distance + PointTolerance), out palmHit)) return false;
            // Do not jump an edge, find a second object, expand the envelope, or apply force after refusal.
            return palmHit.collider == aimedHit.collider && Vector3.Dot(palmHit.normal, aimedHit.normal) > .999f
                && Vector3.Distance(palmHit.point, proposed) <= PointTolerance;
        }

        bool ModeAllowed()
        {
            if (!bound || !armed || !isActiveAndEnabled || !player || !body || !bare || !gloves
                || !player.IsFloating || player.IsOnRoute || !player.ControlAllowed || !player.ViewPivot
                || clock.IsPaused || suit.IsOutside || state.Outcome != MissionOutcome.None
                || state.Phase == GamePhase.Planning
                || state.Phase == GamePhase.Outcome || state.Phase == GamePhase.Report) return false;
            try
            {
                int token = bindingGeneration;
                bool allowed = rootPolicy != null && rootPolicy();
                return token == bindingGeneration && bound && armed && allowed;
            }
            catch (Exception error) { Debug.LogException(error, this); return false; }
        }
        bool WorkLeaseAllowed(ContactHand selectedHand) => ModeAllowed() && !suit.IsSuitOn
            && body.IsVisible && !body.IsEvaVisible && !gloves.IsVisible
            && body.CurrentPose == EmbodiedBodyPresenter.BodyPose.Floating
            && (body.VisibleArms & (selectedHand == ContactHand.Left ? Arms.Left : Arms.Right)) != 0;
        bool GloveLeaseAllowed() => ModeAllowed() && suit.IsSuitOn && gloves.IsVisible
            && body.IsEvaVisible && body.VisibleArms == Arms.None;
        bool VisualLeaseAllowed(ContactHand selectedHand) => suit.IsSuitOn ? GloveLeaseAllowed() : WorkLeaseAllowed(selectedHand);
        int ReadPoseRevision() => body ? body.PoseRevision : -1;
        float Reach => Mathf.Min(.8f, player.Microgravity.ReachMetres);

        bool ValidateContact(HandContactSample sample)
        {
            bool secondary = secondPush.Surface && sample.Hand == ContactHand.Right;
            Collider contactSurface = secondary ? secondPush.Surface : surface;
            HandContactTarget contactTarget = secondary ? secondPush.Target : target;
            ContactHand expectedHand = secondary ? ContactHand.Right : hand;
            if (!pending || !ModeAllowed() || pendingSuited != suit.IsSuitOn || !VisualLeaseAllowed(expectedHand)
                || sample.Hand != expectedHand || sample.Kind != kind || !ValidSurface(contactSurface)
                || sample.Surface != contactSurface.transform
                || !Finite(sample.Point) || !Finite(sample.Normal) || !Finite(sample.Tangent)
                || !Finite(sample.CurrentWristPoint) || !Finite(sample.WristPoint)) return false;
            Vector3 origin = player.ViewPivot.position, delta = sample.Point - origin;
            float distance = delta.magnitude;
            if (distance < .001f || distance > Reach || !Nearest(origin, delta / distance, distance + .02f, out RaycastHit hit)
                || hit.collider != contactSurface || Vector3.Distance(hit.point, sample.Point) > PointTolerance
                || Vector3.Dot(hit.normal, sample.Normal) < .98f) return false;
            Vector3 expectedPoint = contactTarget.Surface.TransformPoint(contactTarget.LocalPoint);
            if (Vector3.Distance(expectedPoint, sample.Point) > PointTolerance) return false;
            if (kind == HandContactKind.Grip)
            {
                if (!RailFrame(contactSurface, sample.Point, sample.Normal, out Vector3 axis, out float radius)
                    || Mathf.Abs(radius - sample.GripRadiusMetres) > .0005f || Mathf.Abs(Vector3.Dot(axis, sample.Tangent)) < .99f) return false;
            }
            return WristPathClear(sample, contactSurface);
        }

        bool AtPairContact(HandContactSample sample)
        {
            if (actionSent || !secondPush.Surface || !ValidateContact(sample) || !HasContactAccuracy(sample.Hand))
                return false;
            if (sample.Hand == ContactHand.Left)
            {
                leftSample = sample;
                leftContactFrame = Time.frameCount;
                return TryWorldPushDirection(surface, localPushDirection, sample.Normal, out leftDirection);
            }
            if (leftContactFrame != Time.frameCount || !ValidateContact(leftSample) || !HasContactAccuracy(ContactHand.Left)
                || !TryWorldPushDirection(secondPush.Surface, secondPush.LocalDirection, sample.Normal, out Vector3 rightDirection))
                return false;

            actionSent = true;
            int token = generation;
            FloatActionResult result = player.TryPushOffPair(surface, leftSample.Point, leftDirection, pushStrength,
                secondPush.Surface, sample.Point, rightDirection, pushStrength);
            if (token == generation)
            {
                LastCommandResult = result;
                if (result == FloatActionResult.Done) HandPushCompleted?.Invoke();
            }
            return result == FloatActionResult.Done;
        }

        bool HasContactAccuracy(ContactHand selectedHand)
        {
            float error = pendingSuited ? gloves.ContactErrorFor(selectedHand) : bare.ContactErrorFor(selectedHand);
            return Finite(error) && error <= (pendingSuited ? .002f : .003f);
        }

        bool TryWorldPushDirection(Collider collider, Vector3 localDirection, Vector3 normal, out Vector3 direction)
        {
            direction = collider.transform.TransformVector(localDirection).normalized;
            return Finite(direction) && direction.sqrMagnitude >= .99f
                && Vector3.Dot(direction, normal) >= player.Microgravity.MinimumAwayDot;
        }

        bool AtContact(HandContactSample sample)
        {
            if (actionSent || !ValidateContact(sample)) return false;
            if (!HasContactAccuracy(sample.Hand)) return false;
            Vector3 direction = Vector3.zero;
            if (kind == HandContactKind.Push && !TryWorldPushDirection(surface, localPushDirection, sample.Normal, out direction)) return false;
            actionSent = true; int token = generation;
            // Claim only the intended collider before Player emits its synchronous Grabbed event. A root
            // teardown inside that event can then release the just-created grip instead of leaking it.
            if (kind == HandContactKind.Grip) ownedGrip = surface;
            FloatActionResult result = kind == HandContactKind.Grip
                ? player.TryGrab(surface, sample.Point)
                : player.TryPushOff(surface, sample.Point, direction, pushStrength);
            if (token != generation) return result == FloatActionResult.Done;
            LastCommandResult = result;
            if (result == FloatActionResult.Done && kind == HandContactKind.Push) HandPushCompleted?.Invoke();
            if (result != FloatActionResult.Done) ownedGrip = null;
            if (result == FloatActionResult.Done && kind == HandContactKind.Grip)
            {
                ownedGrip = player.HeldHandhold;
                if (ownedGrip != surface || !player.HeldGripPoint.HasValue) { CancelOwnedContact(); return false; }
                // Player owns the actual closest-hit anchor; the visual target follows that exact stored point.
                target = Frame(surface, player.HeldGripPoint.Value, sample.Normal, sample.Tangent, sample.GripRadiusMetres);
            }
            return result == FloatActionResult.Done;
        }

        HandContactTarget? ReadHeld()
        {
            if (!pending || kind != HandContactKind.Grip || !ModeAllowed() || !ownedGrip
                || player.HeldHandhold != ownedGrip || !player.HeldGripPoint.HasValue) return null;
            Vector3 point = player.HeldGripPoint.Value, delta = point - player.ViewPivot.position;
            if (delta.magnitude > Reach || delta.sqrMagnitude < .000001f
                || !Nearest(player.ViewPivot.position, delta.normalized, delta.magnitude + .02f, out RaycastHit hit)
                || hit.collider != ownedGrip || Vector3.Distance(hit.point, point) > PointTolerance
                || !RailFrame(ownedGrip, point, hit.normal, out Vector3 axis, out float radius)) return null;
            // Keep the request's surface-local polarity; looking around must not choose a new grip.
            Vector3 retainedTangent = target.Surface.TransformVector(target.LocalTangent).normalized;
            if (Vector3.Dot(axis, retainedTangent) < 0f) axis = -axis;
            target = Frame(ownedGrip, point, hit.normal, axis, radius);
            return target;
        }

        bool WristPathClear(HandContactSample sample, Collider contactSurface)
        {
            // Both endpoints are tested because casts do not report a starting overlap reliably.
            if (!WristPointClear(sample.CurrentWristPoint, sample, contactSurface) || !WristPointClear(sample.WristPoint, sample, contactSurface)) return false;
            Vector3 path = sample.WristPoint - sample.CurrentWristPoint;
            float length = path.magnitude;
            if (length < .0001f) return true;
            int count = Physics.SphereCastNonAlloc(sample.CurrentWristPoint, WristPadding, path / length, hits, length,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count >= hits.Length) return false; // An incomplete set cannot prove nearest/clearance.
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (Owned(hit.collider)) continue;
                if (hit.collider != contactSurface || hit.distance < length - .025f
                    || Vector3.Distance(hit.point, sample.Point) > .10f || Vector3.Dot(hit.normal, sample.Normal) < .8f) return false;
            }
            return true;
        }
        bool WristPointClear(Vector3 point, HandContactSample sample, Collider contactSurface)
        {
            int count = Physics.OverlapSphereNonAlloc(point, WristPadding, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count >= overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlaps[i];
                if (Owned(collider)) continue;
                if (collider != contactSurface || Vector3.Distance(point, sample.WristPoint) > .025f
                    || Vector3.Dot(point - sample.Point, sample.Normal) < -.001f
                    || Vector3.Distance(collider.ClosestPoint(point), sample.Point) > .10f) return false;
            }
            return true;
        }
        bool Nearest(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            if (!Finite(origin) || !Finite(direction) || !Finite(distance) || distance <= 0) return false;
            int occupied = Physics.OverlapSphereNonAlloc(origin, .002f, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (occupied >= overlaps.Length) return false;
            for (int i = 0; i < occupied; i++) if (!Owned(overlaps[i])) return false;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count >= hits.Length) return false;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                if (!Owned(hits[i].collider) && hits[i].distance < closest)
                { closest = hits[i].distance; nearest = hits[i]; }
            return closest < float.PositiveInfinity && ValidSurface(nearest.collider);
        }

        static Vector3 OrientRailAxisAtRequest(Vector3 axis, Transform view)
        {
            float right = Vector3.Dot(axis, view.right), up = Vector3.Dot(axis, view.up);
            // Dominant screen-plane projection is stable near vertical/horizontal ambiguity;
            // exact ties prefer right. End-on axes use forward only as a deterministic fallback.
            float orientation = Mathf.Abs(right) >= Mathf.Abs(up) ? right : up;
            if (Mathf.Abs(orientation) < .001f) orientation = Vector3.Dot(axis, view.forward);
            return orientation < 0f ? -axis : axis;
        }

        bool RailFrame(Collider collider, Vector3 point, Vector3 normal, out Vector3 axis, out float radius)
        {
            axis = default; radius = 0;
            if (!(collider is CapsuleCollider rail) || !ValidSurface(rail) || !player.IsHandhold(rail)) return false;
            Transform t = rail.transform;
            Vector3 x = t.TransformVector(Vector3.right), y = t.TransformVector(Vector3.up), z = t.TransformVector(Vector3.forward);
            if (x.magnitude < .0001f || y.magnitude < .0001f || z.magnitude < .0001f
                || Mathf.Abs(Vector3.Dot(x.normalized, y.normalized)) > .001f
                || Mathf.Abs(Vector3.Dot(x.normalized, z.normalized)) > .001f
                || Mathf.Abs(Vector3.Dot(y.normalized, z.normalized)) > .001f) return false;
            Vector3 along = rail.direction == 0 ? x : rail.direction == 1 ? y : z;
            float a = rail.direction == 0 ? y.magnitude : x.magnitude;
            float b = rail.direction == 2 ? y.magnitude : z.magnitude;
            if (Mathf.Abs(a - b) > Mathf.Max(a, b) * .001f) return false; // No elliptical/sheared pretend rail.
            axis = along.normalized; radius = rail.radius * a;
            if (radius < .012f || radius > .017f) return false;
            Vector3 fromCentre = point - t.TransformPoint(rail.center);
            float axial = Vector3.Dot(fromCentre, axis);
            float straightHalf = rail.height * along.magnitude * .5f - radius;
            Vector3 radial = fromCentre - axis * axial;
            return straightHalf > .04f && Mathf.Abs(axial) <= straightHalf - .04f
                && Mathf.Abs(radial.magnitude - radius) <= .002f && Vector3.Dot(radial.normalized, normal) > .98f;
        }

        void OnPoseWillReset()
        {
            if (pending || ownedGrip) CancelOwnedContact();
            else if (bare) bare.CancelImmediately();
        }
        void OnSuitChanged() { if (pending && pendingSuited != suit.IsSuitOn) CancelOwnedContact(); }
        void OnPlayerReleased(Collider released) { if (released == ownedGrip) CancelOwnedContact(); }
        void CancelOwnedContact()
        {
            if (cancelling) return;
            // Preserve asynchronous presenter refusal before cancellation disposes its calibrated rig facts.
            if (pending && !pendingSuited && bare && !string.IsNullOrEmpty(bare.LastRefusalReason))
                LastRefusal = "WORK contact: " + (string.IsNullOrEmpty(bare.LastRefusalDiagnostic) ? bare.LastRefusalReason : bare.LastRefusalDiagnostic);
            cancelling = true;
            try
            {
                generation++; pending = false; actionSent = false; surface = null; target = default; localPushDirection = default;
                secondPush = default; leftSample = default; leftDirection = default; leftContactFrame = -1;
                Collider release = ownedGrip; ownedGrip = null;
                if (bare) bare.CancelImmediately();
                if (gloves) gloves.ResetToNeutral();
                if (player && release && player.HeldHandhold == release) player.Release();
            }
            finally { cancelling = false; }
        }
        BareArmContactRig WorkArm(string side, SkinnedMeshRenderer skin)
        {
            // These are profile semantic roles; GetWorkBone resolves explicit relative paths when bound.
            var fingers = new Transform[15];
            for (int digit = 0; digit < 5; digit++) for (int joint = 0; joint < 3; joint++)
                fingers[digit * 3 + joint] = body.GetWorkBone("finger" + (digit + 1) + "-" + (joint + 1) + "." + side);
            ContactHand selected = side == "L" ? ContactHand.Left : ContactHand.Right;
            return new BareArmContactRig(selected, body.GetWorkBone("upperarm01." + side),
                body.GetWorkBone("lowerarm01." + side), body.GetWorkBone("wrist." + side), fingers, skin,
                selected == ContactHand.Left ? body.WorkLeftPalmSubmeshes : body.WorkRightPalmSubmeshes);
        }
        bool ValidSurface(Collider c) => c && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && !Owned(c);
        bool Owned(Collider c) => c && player && (c.transform == player.transform || c.transform.IsChildOf(player.transform));
        static HandContactTarget Frame(Collider collider, Vector3 point, Vector3 normal, Vector3 tangent, float radius) =>
            new HandContactTarget(collider.transform, collider.transform.InverseTransformPoint(point),
                collider.transform.localToWorldMatrix.transpose.MultiplyVector(normal).normalized,
                collider.transform.InverseTransformVector(tangent).normalized, radius);
        bool Refuse(string reason) { LastRefusal = reason; return false; }
        static bool DisabledGate() => false;
        static bool DeniedContact(HandContactSample sample) => false;
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    }
}
