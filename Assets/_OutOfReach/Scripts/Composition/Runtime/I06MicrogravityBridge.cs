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
        int generation, bindingGeneration;
        GamePhase phase;
        ContactHand hand;
        HandContactKind kind;
        Collider surface, ownedGrip;
        HandContactTarget target;
        float pushStrength, requestStarted;
        Vector3 localPushDirection;

        public bool IsBound => bound;
        /// <summary>Explicit routing mode, not permission. A rejected request must not fall back to a decorative press.</summary>
        public bool ContactModeEnabled => bound && armed;
        public bool IsBusy => pending;
        public FloatActionResult? LastCommandResult { get; private set; }
        public string LastRefusal { get; private set; } = string.Empty;

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

        /// <summary>Suited glove only; nearest hit must be a registered straight capsule rail with actual 12â€“17mm radius.</summary>
        public bool TryGrab(ContactHand selectedHand, Vector3 aimDirection) => Begin(selectedHand, HandContactKind.Grip, aimDirection, 0f);

        /// <summary>Release dispatched by the same input owner. Cancels pending animation and only this bridge's held grip.</summary>
        public FloatActionResult Release()
        {
            bool held = player && ownedGrip && player.HeldHandhold == ownedGrip;
            bool floating = player && player.IsFloating;
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
            if (pending && (!ModeAllowed() || pendingSuited != suit.IsSuitOn || !VisualLeaseAllowed(hand))) CancelOwnedContact();
        }
        void LateUpdate()
        {
            if (!pending) return;
            if (!ModeAllowed() || pendingSuited != suit.IsSuitOn) { CancelOwnedContact(); return; }
            HandContactStage stage = pendingSuited ? gloves.ContactStage : bare.ContactStage;
            if (stage == HandContactStage.None || (ownedGrip && stage != HandContactStage.Holding)
                || (stage != HandContactStage.Holding && Time.unscaledTime - requestStarted > 1.5f)) CancelOwnedContact();
        }

        bool Begin(ContactHand selectedHand, HandContactKind requestedKind, Vector3 aimDirection, float strength, Vector3 desiredTravelWorld = default)
        {
            LastRefusal = string.Empty; LastCommandResult = null;
            if (!ModeAllowed()) return Refuse("Contact mode is unavailable.");
            if (pending || player.HeldHandhold) return Refuse("Release the current contact first.");
            if (selectedHand != ContactHand.Left && selectedHand != ContactHand.Right) return Refuse("Invalid hand.");
            if (!VisualLeaseAllowed(selectedHand)) return Refuse("Selected arm does not have the visual lease.");
            if (requestedKind == HandContactKind.Grip && !suit.IsSuitOn) return Refuse("Bare-hand grip has no authored pose.");
            if (!Finite(aimDirection) || aimDirection.sqrMagnitude < .00001f
                || !Nearest(player.ViewPivot.position, aimDirection.normalized, Reach, out RaycastHit hit))
                return Refuse("No unobstructed solid surface within reach.");
            // WORK crosshair names the intended surface; the selected palm lands 6cm toward that hand.
            // This is a new real point on the same aimed plane, never a skin/arm offset or a larger reach.
            if (requestedKind == HandContactKind.Push && !suit.IsSuitOn
                && !TryWorkPalmPoint(selectedHand, hit, out hit))
                return Refuse("No verified same-surface WORK palm point within the original reach.");
            Vector3 tangent = Vector3.ProjectOnPlane(player.ViewPivot.up, hit.normal).normalized;
            // Looking up projects eye.up backward across a ceiling. WORK fingers need a stable forward
            // tangent there; retain the existing frame for every wall, glove and rail Grip.
            if (requestedKind == HandContactKind.Push && !suit.IsSuitOn && Vector3.Dot(hit.normal, Vector3.down) > .95f)
                tangent = Vector3.ProjectOnPlane(player.ViewPivot.forward, hit.normal).normalized;
            float radius = 0f;
            if (requestedKind == HandContactKind.Grip && !RailFrame(hit.collider, hit.point, hit.normal, out tangent, out radius))
                return Refuse("Grip needs a registered 12â€“17mm straight capsule rail.");
            if (tangent.sqrMagnitude < .99f) return Refuse("No stable contact frame.");
            // A cylinder axis is an unoriented line. Pick the authored grip hemisphere once,
            // rather than letting an equivalent +axis/-axis model flip the wrist upside down.
            if (requestedKind == HandContactKind.Grip) tangent = OrientRailAxisAtRequest(tangent, player.ViewPivot);
            Vector3 direction = hit.normal;
            if (requestedKind == HandContactKind.Push && desiredTravelWorld.sqrMagnitude > .0001f
                && !TryDirectedPushDirection(hit.normal, desiredTravelWorld, player.Microgravity.MinimumAwayDot, out direction))
                return Refuse("Invalid directional push geometry.");
            hand = selectedHand; kind = requestedKind; surface = hit.collider;
            target = Frame(surface, hit.point, hit.normal, tangent, radius);
            localPushDirection = surface.transform.InverseTransformVector(direction).normalized;
            pushStrength = strength; pendingSuited = suit.IsSuitOn; actionSent = false; pending = true;
            requestStarted = Time.unscaledTime; // Presentation watchdog only; never advances simulation or physics.
            generation++;
            bool accepted = pendingSuited
                ? gloves.TryReachContact(hand, kind, target, AtContact, kind == HandContactKind.Grip ? ReadHeld : (Func<HandContactTarget?>)null)
                : bare.TryReachPush(hand, target, AtContact);
            if (accepted) return true;
            string reason = !pendingSuited && bare && !string.IsNullOrEmpty(bare.LastRefusalReason)
                ? "WORK contact: " + (string.IsNullOrEmpty(bare.LastRefusalDiagnostic) ? bare.LastRefusalReason : bare.LastRefusalDiagnostic)
                : "The actual hand pose, arm reach or approach path refused this contact.";
            CancelOwnedContact();
            return Refuse(reason);
        }

        // Pure request-direction math only. Force magnitude and validated contact dispatch remain Player-owned.
        static bool TryDirectedPushDirection(Vector3 surfaceNormal, Vector3 travelIntent, float minimumAwayDot, out Vector3 direction)
        {
            direction = Vector3.zero;
            float normalSquared = surfaceNormal.sqrMagnitude, intentSquared = travelIntent.sqrMagnitude;
            if (!Finite(surfaceNormal) || !Finite(travelIntent) || !Finite(minimumAwayDot)
                || !Finite(normalSquared) || !Finite(intentSquared) || normalSquared < .0001f || intentSquared < .0001f) return false;
            Vector3 normal = surfaceNormal / Mathf.Sqrt(normalSquared), intent = travelIntent / Mathf.Sqrt(intentSquared);
            float away = Mathf.Clamp(Mathf.Max(.25f, minimumAwayDot + .025f), 0f, 1f);
            float outward = Vector3.Dot(intent, normal);
            if (outward >= away) { direction = intent; return true; }
            Vector3 tangent = intent - normal * outward;
            float tangentLength = tangent.magnitude;
            if (tangentLength == 0f) { direction = normal; return true; }
            Vector3 tangentDirection = tangent / tangentLength;
            if (tangentLength >= .25f)
                direction = normal * away + tangentDirection * Mathf.Sqrt(1f - away * away);
            else
            {
                // A hard .25 cutoff would jump75.52 degrees at the default cone. Blend the angle instead.
                float ratio = Mathf.Clamp01(tangentLength / .25f);
                float angle = ratio * ratio * (3f - 2f * ratio) * Mathf.Acos(away);
                direction = normal * Mathf.Cos(angle) + tangentDirection * Mathf.Sin(angle);
            }
            return true;
        }

        bool TryWorkPalmPoint(ContactHand selectedHand, RaycastHit aimedHit, out RaycastHit palmHit)
        {
            palmHit = default;
            Vector3 across = Vector3.ProjectOnPlane(player.ViewPivot.right, aimedHit.normal);
            if (across.sqrMagnitude < .01f) return false;
            Vector3 proposed = aimedHit.point + across.normalized * (selectedHand == ContactHand.Left ? -.06f : .06f);
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
            if (!pending || !ModeAllowed() || pendingSuited != suit.IsSuitOn || !VisualLeaseAllowed(hand)
                || sample.Hand != hand || sample.Kind != kind || !ValidSurface(surface) || sample.Surface != surface.transform
                || !Finite(sample.Point) || !Finite(sample.Normal) || !Finite(sample.Tangent)
                || !Finite(sample.CurrentWristPoint) || !Finite(sample.WristPoint)) return false;
            Vector3 origin = player.ViewPivot.position, delta = sample.Point - origin;
            float distance = delta.magnitude;
            if (distance < .001f || distance > Reach || !Nearest(origin, delta / distance, distance + .02f, out RaycastHit hit)
                || hit.collider != surface || Vector3.Distance(hit.point, sample.Point) > PointTolerance
                || Vector3.Dot(hit.normal, sample.Normal) < .98f) return false;
            Vector3 expectedPoint = target.Surface.TransformPoint(target.LocalPoint);
            if (Vector3.Distance(expectedPoint, sample.Point) > PointTolerance) return false;
            if (kind == HandContactKind.Grip)
            {
                if (!RailFrame(surface, sample.Point, sample.Normal, out Vector3 axis, out float radius)
                    || Mathf.Abs(radius - sample.GripRadiusMetres) > .0005f || Mathf.Abs(Vector3.Dot(axis, sample.Tangent)) < .99f) return false;
            }
            return WristPathClear(sample);
        }

        bool AtContact(HandContactSample sample)
        {
            if (actionSent || !ValidateContact(sample)) return false;
            float error = pendingSuited ? gloves.ContactErrorMetres : bare.ContactErrorMetres;
            if (!Finite(error) || error > (pendingSuited ? .002f : .003f)) return false;
            Vector3 direction = surface.transform.TransformVector(localPushDirection).normalized;
            if (kind == HandContactKind.Push && (!Finite(direction) || direction.sqrMagnitude < .99f
                || Vector3.Dot(direction, sample.Normal) < player.Microgravity.MinimumAwayDot)) return false;
            actionSent = true; int token = generation;
            // Claim only the intended collider before Player emits its synchronous Grabbed event. A root
            // teardown inside that event can then release the just-created grip instead of leaking it.
            if (kind == HandContactKind.Grip) ownedGrip = surface;
            FloatActionResult result = kind == HandContactKind.Grip
                ? player.TryGrab(surface, sample.Point)
                : player.TryPushOff(surface, sample.Point, direction, pushStrength);
            if (token != generation) return result == FloatActionResult.Done;
            LastCommandResult = result;
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

        bool WristPathClear(HandContactSample sample)
        {
            // Both endpoints are tested because casts do not report a starting overlap reliably.
            if (!WristPointClear(sample.CurrentWristPoint, sample) || !WristPointClear(sample.WristPoint, sample)) return false;
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
                if (hit.collider != surface || hit.distance < length - .025f
                    || Vector3.Distance(hit.point, sample.Point) > .10f || Vector3.Dot(hit.normal, sample.Normal) < .8f) return false;
            }
            return true;
        }
        bool WristPointClear(Vector3 point, HandContactSample sample)
        {
            int count = Physics.OverlapSphereNonAlloc(point, WristPadding, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count >= overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider collider = overlaps[i];
                if (Owned(collider)) continue;
                if (collider != surface || Vector3.Distance(point, sample.WristPoint) > .025f
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
                Collider release = ownedGrip; ownedGrip = null;
                if (bare) bare.CancelImmediately();
                if (gloves) gloves.ResetToNeutral();
                if (player && release && player.HeldHandhold == release) player.Release();
            }
            finally { cancelling = false; }
        }
        BareArmContactRig WorkArm(string side, SkinnedMeshRenderer skin) => new BareArmContactRig(
            body.GetWorkBone("upperarm01." + side), body.GetWorkBone("lowerarm01." + side), body.GetWorkBone("wrist." + side),
            body.GetWorkBone("finger2-1." + side), body.GetWorkBone("finger3-1." + side), body.GetWorkBone("finger5-1." + side), body.GetWorkBone("finger1-1." + side), skin);
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
