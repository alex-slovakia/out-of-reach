using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using OutOfReach.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace OutOfReach.Player
{
    /// <summary>
    /// First-person walking controller: yaw on the body, clamped pitch on the view pivot, CharacterController
    /// collisions, and one interact action routed to the <see cref="InteractionResolver"/>.
    /// It owns no camera: the single gameplay camera and audio listener are parented under the view pivot by the
    /// scene composition. It owns no gameplay state either: whether the player may act comes from
    /// <see cref="ControlGate"/> (phase, pause and cinematics, supplied by the composition root). While control is
    /// not allowed the player stands still, the focus is cleared and the cursor is released.
    /// Units: metres, seconds, degrees. Movement uses real frame time, never simulated time.
    /// <para>
    /// Optional microgravity mode (off until <see cref="EnterFloat"/>, which needs a solid surface within reach):
    /// no gravity and no stepping; the body keeps its momentum through collision-aware moves and, with the default
    /// zero drag, a push carries it on until it meets a surface. Speed only comes from pushes off verified solid
    /// surfaces within reach and in line of sight (<see cref="TryPushOff"/>); <see cref="TryBrace"/> stops an
    /// approach against such a surface; only registered handholds can be gripped (<see cref="TryGrab"/>). WASD and
    /// sprint do nothing while floating (no thrust, no hidden force); look stays upright. The composition root
    /// registers the handholds and binds the hand inputs to these calls; tuning is in <see cref="MicrogravityLocomotion"/>.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("View")]
        [Tooltip("Pitch pivot at eye height (1.65 m above the feet). The gameplay camera is parented under it.")]
        [SerializeField] private Transform viewPivot;

        [Tooltip("Maximum look angle up or down, degrees.")]
        [SerializeField] private float maxPitchDegrees = 80f;

        [Header("Movement")]
        [Tooltip("Walking speed, metres per second.")]
        [SerializeField] private float walkSpeedMetresPerSecond = 2.5f;

        [Tooltip("Downward acceleration, metres per second squared.")]
        [SerializeField] private float gravityMetresPerSecondSquared = 9.81f;

        [Tooltip("Held sprint speed multiplier while walking unencumbered. Game tuning, not physiological data.")]
        [SerializeField, Range(1f, 2f)] private float sprintSpeedMultiplier = 1.5f;

        [SerializeField] private PlayerExertion exertion = new PlayerExertion();

        [Header("Microgravity (optional, off until EnterFloat)")]
        [SerializeField] private MicrogravityLocomotion microgravity = new MicrogravityLocomotion();

        [Header("Interaction")]
        [SerializeField] private InteractionResolver interaction;

        [Header("Cursor")]
        [Tooltip("Lock and hide the cursor while the player has control; always released otherwise.")]
        [SerializeField] private bool captureCursor = true;

        [Tooltip("Raised when the pause control is pressed. The composition root toggles the mission pause.")]
        [SerializeField] private UnityEvent pauseRequested = new UnityEvent();

        private CharacterController body;
        private PlayerInputActions ownedInput;
        private IPlayerInputSource inputSource;
        private float verticalVelocity;
        private float speedMultiplier = 1f;
        private float routeDistance;
        private int routeDirection = 1;
        private bool cursorApplied;
        private bool cursorCaptured;
        private bool applicationFocused = true;
        private bool applicationPaused;
        private bool sprintReleaseRequired;
        private bool sprinting;
        private bool wasMovementActive;
        private bool floating;
        private Vector3 floatVelocity;
        private Collider heldHandhold;
        private float floatClock;
        private float lastPushAt = float.NegativeInfinity;
        private float strandedFor;
        private float savedStepOffset;
        private readonly List<FloatContactPoint> contacts = new List<FloatContactPoint>(4);
        private readonly HashSet<Collider> handholds = new HashSet<Collider>();

        private static readonly Vector3[] ProbeDirections = BuildProbeDirections();

        private struct FloatContactPoint
        {
            public Collider Collider;
            public Vector3 Normal;
        }

        /// <summary>Input for this player. Assign a scripted source in tests; otherwise Input System actions are created.</summary>
        public IPlayerInputSource InputSource
        {
            get { return inputSource; }
            set { inputSource = value; CancelSprint(); }
        }

        /// <summary>Returns true while the player may move, look and interact. Null means always allowed.</summary>
        public Func<bool> ControlGate { get; set; }

        public bool ControlAllowed => isActiveAndEnabled && applicationFocused && !applicationPaused &&
            (ControlGate == null || ControlGate());

        /// <summary>What this controller asks of the cursor: captured only while control is allowed.</summary>
        public bool WantsCursorCaptured => captureCursor && ControlAllowed;

        /// <summary>Current look angle, degrees; positive looks up.</summary>
        public float PitchDegrees { get; private set; }

        public float WalkSpeedMetresPerSecond => walkSpeedMetresPerSecond;

        /// <summary>True only after actual unencumbered sprint movement, while control remains allowed.</summary>
        public bool IsSprinting => sprinting && ControlAllowed && Time.timeScale > 0f;

        /// <summary>Player stamina in [0, 1], independent of ship battery and suit oxygen. Game tuning.</summary>
        public float ExertionRemaining01 => exertion.Remaining01;

        /// <summary>
        /// Share of the walk speed currently available, 0 to 1 (1 = unhindered). The composition root sets it from
        /// gameplay state, for example while carrying a crate. It does not affect the constrained EVA route.
        /// </summary>
        public float SpeedMultiplier
        {
            get { return speedMultiplier; }
            set { speedMultiplier = float.IsNaN(value) ? 1f : Mathf.Clamp01(value); }
        }

        public float MaxPitchDegrees => maxPitchDegrees;

        public Transform ViewPivot => viewPivot;

        public InteractionResolver Interaction => interaction;

        /// <summary>The constrained route the player is clipped to, or null while walking.</summary>
        public TetheredRoute ActiveRoute { get; private set; }

        /// <summary>Forward or back input below this is ignored on the route, so a resting stick never creeps.</summary>
        public const float RouteInputDeadZone = 0.1f;

        /// <summary>
        /// How clearly the player must face along the route (cosine) before W changes its sense. Inside this band,
        /// for example when looking sideways or along a vertical stretch, W keeps leading the way it led last.
        /// </summary>
        public const float RouteHeadingDeadBand = 0.35f;

        /// <summary>Within this distance, metres, of an end the player counts as at that end.</summary>
        public const float RouteEndToleranceMetres = 0.01f;

        /// <summary>Distance of the feet from the route start along the tether, metres; 0 while walking.</summary>
        public float RouteDistanceMetres => ActiveRoute != null ? routeDistance : 0f;

        /// <summary>+1 while W leads away from the hatch towards the work site, -1 while it leads back.</summary>
        public int RouteDirection => routeDirection;

        /// <summary>True on a route at its start, by the outer hatch.</summary>
        public bool IsAtRouteStart => ActiveRoute != null && routeDistance <= RouteEndToleranceMetres;

        /// <summary>True on a route at its end, the work site.</summary>
        public bool IsAtRouteEnd => ActiveRoute != null && routeDistance >= ActiveRoute.LengthMetres - RouteEndToleranceMetres;

        public bool IsOnRoute => ActiveRoute != null;

        /// <summary>Result of the most recent interact press, or null.</summary>
        public CommandResult? LastInteraction { get; private set; }

        /// <summary>Raised when the pause control is pressed (also while control is not allowed, so it can resume).</summary>
        public event Action PauseRequested;

        /// <summary>True in the optional microgravity mode: no gravity, no stepping, no walking bob or footsteps.</summary>
        public bool IsFloating => floating;

        /// <summary>Current drift velocity while floating, metres per second; zero otherwise.</summary>
        public Vector3 FloatVelocity => floating ? floatVelocity : Vector3.zero;

        /// <summary>The handhold currently gripped while floating, or null.</summary>
        public Collider HeldHandhold => floating ? heldHandhold : null;

        /// <summary>Microgravity tuning (read-only view for presentation and tests).</summary>
        public MicrogravityLocomotion Microgravity => microgravity;

        /// <summary>
        /// Diagnostic only, no force: a floating player is stranded after the stranded time with no solid surface
        /// within reach while almost still. Entry near a surface and zero drag keep this from happening in normal play;
        /// it can follow a ClearMotion away from every surface. Any recovery assist is a separate, disclosed proposal.
        /// </summary>
        public bool IsStranded => floating && heldHandhold == null && floatVelocity.magnitude < microgravity.StrandedSpeed
            && strandedFor >= microgravity.StrandedSeconds;

        /// <summary>Registers a physical grippable handhold collider (the composition root does this at scene bind).</summary>
        public void RegisterHandhold(Collider handhold)
        {
            if (handhold != null)
            {
                handholds.Add(handhold);
            }
        }

        /// <summary>Removes a handhold from the registry; a grip on it ends.</summary>
        public void UnregisterHandhold(Collider handhold)
        {
            if (handhold != null && handholds.Remove(handhold) && heldHandhold == handhold)
            {
                ReleaseHold();
            }
        }

        /// <summary>True for a registered grippable handhold.</summary>
        public bool IsHandhold(Collider candidate)
        {
            return candidate != null && handholds.Contains(candidate);
        }

        /// <summary>Raised after a successful push with the new velocity.</summary>
        public event Action<Vector3> Pushed;

        /// <summary>Raised when a handhold is gripped.</summary>
        public event Action<Collider> Grabbed;

        /// <summary>Raised when the grip on a handhold ends (release, push, mode change or a vanished handhold).</summary>
        public event Action<Collider> Released;

        /// <summary>Raised when the floating body meets a surface, with the impact speed into it (m/s).</summary>
        public event Action<Collider, float> FloatContact;

        /// <summary>Explicit wiring for code-built rigs; scenes normally use the serialized fields.</summary>
        public void Configure(Transform pivot, InteractionResolver resolver)
        {
            viewPivot = pivot;
            interaction = resolver;
        }

        /// <summary>
        /// Places the feet at a position with a heading, level view and no residual velocity. Always ends in
        /// walking mode: any constrained route is released.
        /// </summary>
        public void Teleport(Vector3 feetPosition, float yawDegrees)
        {
            ExitFloat();
            CancelSprint();
            wasMovementActive = false;
            CharacterController controller = Body;
            ActiveRoute = null;
            controller.enabled = false;
            transform.SetPositionAndRotation(feetPosition, Quaternion.Euler(0f, yawDegrees, 0f));
            controller.enabled = true;
            verticalVelocity = 0f;
            PitchDegrees = 0f;
            ApplyPitch();
        }

        /// <summary>
        /// Clips the player to a constrained route: the feet move to its start, gravity and collisions are off, and
        /// until <see cref="ExitRoute"/> or <see cref="Teleport"/> the feet stay on the route line. W and S move along
        /// it towards the end the player roughly faces (heading only; looking up or down never moves the player),
        /// A and D do nothing, and both ends stop the player.
        /// </summary>
        public void EnterRoute(TetheredRoute route)
        {
            if (route == null || !route.IsValid)
            {
                return;
            }

            ExitFloat();
            CancelSprint();
            Body.enabled = false;
            ActiveRoute = route;
            verticalVelocity = 0f;
            routeDistance = 0f;
            routeDirection = 1;
            transform.position = route.StartPosition;
        }

        /// <summary>
        /// Starts the optional microgravity mode where the player is: no gravity, no stepping, zero velocity. Only at a
        /// safe entry with a solid surface within reach (NoSurface otherwise), never on the EVA route (OnRoute), which
        /// keeps its own movement. Nothing else changes until this is called.
        /// </summary>
        public FloatActionResult EnterFloat()
        {
            if (ActiveRoute != null)
            {
                return FloatActionResult.OnRoute;
            }

            if (floating)
            {
                return FloatActionResult.Done;
            }

            if (!SurfaceWithinReach())
            {
                return FloatActionResult.NoSurface;
            }

            CancelSprint();
            CharacterController controller = Body;
            savedStepOffset = controller.stepOffset;
            controller.stepOffset = 0f;
            floating = true;
            verticalVelocity = 0f;
            floatVelocity = Vector3.zero;
            heldHandhold = null;
            strandedFor = 0f;
            floatClock = 0f;
            lastPushAt = float.NegativeInfinity;
            return FloatActionResult.Done;
        }

        /// <summary>Ends the microgravity mode: lets go, drops the velocity and walks again with gravity and stepping.</summary>
        public void ExitFloat()
        {
            if (!floating)
            {
                return;
            }

            ReleaseHold();
            floating = false;
            floatVelocity = Vector3.zero;
            verticalVelocity = 0f;
            strandedFor = 0f;
            Body.stepOffset = savedStepOffset;
        }

        /// <summary>Stops any drift and lets go (for rest, a body lease or a cinematic hand-over); stays floating.</summary>
        public void ClearMotion()
        {
            floatVelocity = Vector3.zero;
            strandedFor = 0f;
            ReleaseHold();
        }

        /// <summary>
        /// Pushes off a solid surface the hand can reach. The surface is verified, not trusted: a non-trigger collider
        /// that is the closest hit of a ray from the eye within reach (nothing in between), and the direction must
        /// point away from it. One push changes the
        /// speed by at most the per-push limit (scaled by strength), the total stays capped, and a cooldown stops a
        /// held button from accelerating continuously. A push lets go of a held handhold.
        /// </summary>
        public FloatActionResult TryPushOff(Collider surface, Vector3 surfacePoint, Vector3 direction, float strength01 = 1f)
        {
            if (!floating)
            {
                return FloatActionResult.NotFloating;
            }

            if (!ControlAllowed)
            {
                return FloatActionResult.ControlDenied;
            }

            if (!IsFinite(surfacePoint) || !IsFinite(direction) || direction.sqrMagnitude < 1e-6f
                || float.IsNaN(strength01) || strength01 <= 0f)
            {
                return FloatActionResult.InvalidInput;
            }

            if (floatClock - lastPushAt < microgravity.PushCooldownSeconds)
            {
                return FloatActionResult.Cooldown;
            }

            FloatActionResult verified = VerifySurface(surface, surfacePoint, out Vector3 normal);
            if (verified != FloatActionResult.Done)
            {
                return verified;
            }

            Vector3 away = direction.normalized;
            if (Vector3.Dot(away, normal) < microgravity.MinimumAwayDot)
            {
                return FloatActionResult.IntoSurface;
            }

            ReleaseHold();
            floatVelocity = microgravity.Push(floatVelocity, away, strength01);
            lastPushAt = floatClock;
            strandedFor = 0f;
            Pushed?.Invoke(floatVelocity);
            return FloatActionResult.Done;
        }

        /// <summary>
        /// Grips a registered handhold the hand can reach in line of sight (same verification as a push); a smooth wall
        /// is not grippable (NotAHandhold, use TryBrace). The body is never moved to the handhold; while held, the
        /// drift settles to rest.
        /// </summary>
        public FloatActionResult TryGrab(Collider handhold, Vector3 gripPoint)
        {
            if (!floating)
            {
                return FloatActionResult.NotFloating;
            }

            if (!ControlAllowed)
            {
                return FloatActionResult.ControlDenied;
            }

            if (!IsFinite(gripPoint))
            {
                return FloatActionResult.InvalidInput;
            }

            if (heldHandhold != null)
            {
                return FloatActionResult.AlreadyHolding;
            }

            if (!IsHandhold(handhold))
            {
                return FloatActionResult.NotAHandhold;
            }

            FloatActionResult verified = VerifySurface(handhold, gripPoint, out Vector3 unusedNormal);
            if (verified != FloatActionResult.Done)
            {
                return verified;
            }

            heldHandhold = handhold;
            strandedFor = 0f;
            Grabbed?.Invoke(handhold);
            return FloatActionResult.Done;
        }

        /// <summary>Lets go of the held handhold; the body keeps whatever little velocity it still has.</summary>
        public FloatActionResult Release()
        {
            if (!floating)
            {
                return FloatActionResult.NotFloating;
            }

            if (heldHandhold == null)
            {
                return FloatActionResult.NotHolding;
            }

            ReleaseHold();
            return FloatActionResult.Done;
        }

        /// <summary>
        /// Braces against a solid surface the hand can reach in line of sight (same verification as a push): the part
        /// of the drift towards that surface stops; sliding along it continues, because a smooth wall gives no grip.
        /// </summary>
        public FloatActionResult TryBrace(Collider surface, Vector3 surfacePoint)
        {
            if (!floating)
            {
                return FloatActionResult.NotFloating;
            }

            if (!ControlAllowed)
            {
                return FloatActionResult.ControlDenied;
            }

            if (!IsFinite(surfacePoint))
            {
                return FloatActionResult.InvalidInput;
            }

            FloatActionResult verified = VerifySurface(surface, surfacePoint, out Vector3 normal);
            if (verified != FloatActionResult.Done)
            {
                return verified;
            }

            floatVelocity = MicrogravityLocomotion.RemoveInto(floatVelocity, normal);
            strandedFor = 0f;
            return FloatActionResult.Done;
        }

        /// <summary>Releases the route and stands the player at a walking position.</summary>
        public void ExitRoute(Vector3 feetPosition, float yawDegrees)
        {
            Teleport(feetPosition, yawDegrees);
        }

        private CharacterController Body
        {
            get
            {
                if (body == null)
                {
                    body = GetComponent<CharacterController>();
                }

                return body;
            }
        }

        /// <summary>
        /// Session-start hook for an in-place restart. Ordinary teleports and EVA return preserve stamina.
        /// A scene reload already creates a fresh full stamina state. Held sprint must be released afterwards.
        /// </summary>
        public void ResetExertionForNewSession()
        {
            exertion.Reset();
            CancelSprint();
            wasMovementActive = false;
        }

        private void Awake()
        {
            Body.minMoveDistance = 0f;
        }

        private void OnEnable()
        {
            if (inputSource == null)
            {
                ownedInput = new PlayerInputActions();
                inputSource = ownedInput;
            }

            if (ownedInput != null)
            {
                ownedInput.Enable();
            }
        }

        private void OnDisable()
        {
            CancelSprint();
            wasMovementActive = false;
            ClearMotion();
            if (ownedInput != null)
            {
                ownedInput.Disable();
            }

            if (interaction != null)
            {
                interaction.ResolutionEnabled = false;
            }

            ApplyCursor(false);
        }

        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!focused)
            {
                CancelSprint();
                wasMovementActive = false;
                ApplyCursor(false);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused)
            {
                CancelSprint();
                wasMovementActive = false;
            }
        }

        private void CancelSprint()
        {
            sprinting = false;
            sprintReleaseRequired = true;
        }

        private void OnDestroy()
        {
            if (ownedInput != null)
            {
                if (ReferenceEquals(inputSource, ownedInput))
                {
                    inputSource = null;
                }

                ownedInput.Dispose();
                ownedInput = null;
            }
        }

        private void Update()
        {
            IPlayerInputSource input = inputSource;
            if (input == null)
            {
                CancelSprint();
                wasMovementActive = false;
                return;
            }

            if (input.PausePressedThisFrame)
            {
                PauseRequested?.Invoke();
                pauseRequested.Invoke();
            }

            bool allowed = ControlAllowed;
            ApplyCursor(captureCursor && allowed);
            if (interaction != null)
            {
                interaction.ResolutionEnabled = allowed;
            }

            bool sprintHeld = input is ISprintInputSource sprintInput && sprintInput.SprintHeld;
            if (!sprintHeld)
            {
                sprintReleaseRequired = false;
            }

            if (!allowed || Time.deltaTime <= 0f)
            {
                CancelSprint();
                wasMovementActive = false;
                verticalVelocity = 0f;
                return;
            }

            Look(input.LookDegrees);
            sprinting = false;
            float motionFraction;
            ExertionActivity activity;
            if (floating)
            {
                // No thrust: WASD and sprint do nothing while floating; only pushes, grips and contacts change motion.
                Float(Time.deltaTime);
                motionFraction = 0f;
                activity = ExertionActivity.Resting;
            }
            else if (ActiveRoute != null)
            {
                motionFraction = MoveAlongRoute(input.Move, Time.deltaTime);
                activity = ExertionActivity.TetherTraversal;
            }
            else
            {
                bool carrying = speedMultiplier < 1f;
                bool requestSprint = sprintHeld && !sprintReleaseRequired && !carrying &&
                    exertion.CanSprint && Body.isGrounded;
                motionFraction = Walk(input.Move, Time.deltaTime, requestSprint, carrying);
                sprinting = requestSprint && motionFraction > 0.001f;
                activity = sprinting ? ExertionActivity.Sprinting :
                    carrying ? ExertionActivity.Carrying : ExertionActivity.Walking;
            }

            // Do not charge the interval spanning a control/focus break. Bound hitch time to the same maximum
            // frame duration used by movement, while remaining independent of the mission simulation clock.
            float activeRealSeconds = wasMovementActive ? Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime) : 0f;
            wasMovementActive = true;
            exertion.Advance(activeRealSeconds,
                motionFraction > 0.001f ? activity : ExertionActivity.Resting, motionFraction);
            if (!exertion.CanSprint)
            {
                CancelSprint();
            }

            if (input.InteractPressedThisFrame && interaction != null)
            {
                LastInteraction = interaction.TryInteract();
            }
        }

        private void Look(Vector2 degrees)
        {
            if (degrees.x != 0f)
            {
                transform.Rotate(0f, degrees.x, 0f, Space.Self);
            }

            if (degrees.y != 0f)
            {
                PitchDegrees = Mathf.Clamp(PitchDegrees + degrees.y, -maxPitchDegrees, maxPitchDegrees);
                ApplyPitch();
            }
        }

        private void ApplyPitch()
        {
            if (viewPivot != null)
            {
                viewPivot.localRotation = Quaternion.Euler(-PitchDegrees, 0f, 0f);
            }
        }

        private float Walk(Vector2 move, float deltaSeconds, bool requestSprint, bool carrying)
        {
            CharacterController controller = Body;
            if (!controller.enabled || deltaSeconds <= 0f)
            {
                return 0f;
            }

            Vector2 intent = Vector2.ClampMagnitude(move, 1f);
            float speed = walkSpeedMetresPerSecond * speedMultiplier *
                (requestSprint ? Mathf.Clamp(sprintSpeedMultiplier, 1f, 2f) : 1f) *
                (carrying ? exertion.TraversalSpeedFactor : 1f);
            Vector3 direction = (transform.right * intent.x) + (transform.forward * intent.y);
            Vector3 horizontal = direction * speed;
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1f;
            }

            verticalVelocity -= gravityMetresPerSecondSquared * deltaSeconds;
            Vector3 before = transform.position;
            controller.Move((horizontal + (Vector3.up * verticalVelocity)) * deltaSeconds);
            Vector3 travelled = transform.position - before;
            travelled.y = 0f;
            float intendedTravel = Mathf.Max(0f, Vector3.Dot(travelled, direction.normalized));
            return speed > 0f ? Mathf.Clamp01(intendedTravel / (speed * deltaSeconds)) : 0f;
        }

        /// <summary>
        /// Moves the feet by distance along the route line, so the position is always on the line and can never
        /// jump between segments. The sense of W comes from the body heading compared with the route's heading
        /// (pitch is ignored); inside the dead band the previous sense is kept, so looking sideways or along a
        /// vertical stretch never reverses the player. S always moves the other way.
        /// </summary>
        private float MoveAlongRoute(Vector2 move, float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return 0f;
            }

            float along = Mathf.Clamp(move.y, -1f, 1f);
            if (Mathf.Abs(along) < RouteInputDeadZone)
            {
                return 0f;
            }

            Vector3 heading = ActiveRoute.HeadingAt(routeDistance);
            Vector3 facing = transform.forward;
            facing.y = 0f;
            if (heading.sqrMagnitude > 0f && facing.sqrMagnitude > 1e-6f)
            {
                float alignment = Vector3.Dot(facing.normalized, heading);
                if (alignment > RouteHeadingDeadBand)
                {
                    routeDirection = 1;
                }
                else if (alignment < -RouteHeadingDeadBand)
                {
                    routeDirection = -1;
                }
            }

            float length = ActiveRoute.LengthMetres;
            float speed = ActiveRoute.SpeedMetresPerSecond * exertion.TraversalSpeedFactor;
            float before = routeDistance;
            float step = routeDirection * along * speed * deltaSeconds;
            routeDistance = Mathf.Clamp(routeDistance + step, 0f, length);
            transform.position = ActiveRoute.PointAt(routeDistance);
            return speed > 0f ? Mathf.Clamp01(Mathf.Abs(routeDistance - before) / (speed * deltaSeconds)) : 0f;
        }

        /// <summary>One frame of floating: grip settling, exact drift, collision-aware move, inelastic contacts.</summary>
        private void Float(float deltaSeconds)
        {
            CharacterController controller = Body;
            if (!controller.enabled || deltaSeconds <= 0f)
            {
                return;
            }

            floatClock += deltaSeconds;
            if (!ReferenceEquals(heldHandhold, null) && (heldHandhold == null || !heldHandhold.enabled || !heldHandhold.gameObject.activeInHierarchy))
            {
                ReleaseHold();
            }

            if (heldHandhold != null)
            {
                floatVelocity = microgravity.Settle(floatVelocity, deltaSeconds);
            }

            Vector3 displacement = microgravity.Integrate(ref floatVelocity, deltaSeconds);
            if (displacement.sqrMagnitude > 0f)
            {
                contacts.Clear();
                controller.Move(displacement);
                foreach (FloatContactPoint contact in contacts)
                {
                    float impact = -Vector3.Dot(floatVelocity, contact.Normal);
                    floatVelocity = MicrogravityLocomotion.RemoveInto(floatVelocity, contact.Normal);
                    if (impact > 0.01f)
                    {
                        FloatContact?.Invoke(contact.Collider, impact);
                    }
                }

                contacts.Clear();
            }

            bool stranded = heldHandhold == null && floatVelocity.magnitude < microgravity.StrandedSpeed && !SurfaceWithinReach();
            strandedFor = stranded ? strandedFor + deltaSeconds : 0f;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (floating && hit.collider != null && contacts.Count < 8)
            {
                contacts.Add(new FloatContactPoint { Collider = hit.collider, Normal = hit.normal });
            }
        }

        private Vector3 ReachOrigin => viewPivot != null ? viewPivot.position : transform.position + (Vector3.up * 1.5f);

        private FloatActionResult VerifySurface(Collider surface, Vector3 point, out Vector3 normal)
        {
            normal = Vector3.zero;
            if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy || surface.isTrigger)
            {
                return FloatActionResult.NoSurface;
            }

            Vector3 origin = ReachOrigin;
            if (!IsFinite(origin))
            {
                return FloatActionResult.NoSurface;
            }

            Vector3 toPoint = point - origin;
            float distance = toPoint.magnitude;
            if (distance > microgravity.ReachMetres)
            {
                return FloatActionResult.OutOfReach;
            }

            Vector3 ray = distance > 1e-4f ? toPoint / distance : (surface.bounds.center - origin).normalized;
            if (ray.sqrMagnitude < 0.5f || !ClosestHit(origin, ray, microgravity.ReachMetres + 0.02f, out RaycastHit hit))
            {
                return FloatActionResult.NoSurface;
            }

            if (hit.collider != surface)
            {
                return FloatActionResult.Occluded;
            }

            if (hit.distance > microgravity.ReachMetres)
            {
                return FloatActionResult.OutOfReach;
            }

            normal = hit.normal;
            return FloatActionResult.Done;
        }

        /// <summary>Closest solid, non-trigger hit along a ray, ignoring the player own capsule.</summary>
        private bool ClosestHit(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit closest)
        {
            closest = default(RaycastHit);
            float nearest = float.PositiveInfinity;
            CharacterController own = Body;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != own && hit.distance < nearest)
                {
                    nearest = hit.distance;
                    closest = hit;
                }
            }

            return nearest < float.PositiveInfinity;
        }

        private bool SurfaceWithinReach()
        {
            return NearestSurfaceDirection(microgravity.ReachMetres, out Vector3 unused);
        }

        /// <summary>Nearest solid, non-trigger surface along 14 probe rays from the eye (own collider ignored).</summary>
        private bool NearestSurfaceDirection(float maxDistance, out Vector3 direction)
        {
            direction = Vector3.zero;
            float nearest = float.PositiveInfinity;
            Vector3 origin = ReachOrigin;
            CharacterController own = Body;
            foreach (Vector3 probe in ProbeDirections)
            {
                RaycastHit[] hits = Physics.RaycastAll(origin, probe, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider == own || hit.distance >= nearest)
                    {
                        continue;
                    }

                    nearest = hit.distance;
                    direction = probe;
                }
            }

            return nearest < float.PositiveInfinity;
        }

        private void ReleaseHold()
        {
            if (ReferenceEquals(heldHandhold, null))
            {
                return;
            }

            Collider released = heldHandhold;
            heldHandhold = null;
            Released?.Invoke(released);
        }

        private static Vector3[] BuildProbeDirections()
        {
            var directions = new List<Vector3>
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
            };
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        directions.Add(new Vector3(x, y, z).normalized);
                    }
                }
            }

            return directions.ToArray();
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
                && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        private void ApplyCursor(bool capture)
        {
            if (cursorApplied && cursorCaptured == capture)
            {
                return;
            }

            cursorApplied = true;
            cursorCaptured = capture;
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }
    }
}
