using System;
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
        private bool cursorApplied;
        private bool cursorCaptured;

        /// <summary>Input for this player. Assign a scripted source in tests; otherwise Input System actions are created.</summary>
        public IPlayerInputSource InputSource
        {
            get { return inputSource; }
            set { inputSource = value; }
        }

        /// <summary>Returns true while the player may move, look and interact. Null means always allowed.</summary>
        public Func<bool> ControlGate { get; set; }

        public bool ControlAllowed => isActiveAndEnabled && (ControlGate == null || ControlGate());

        /// <summary>What this controller asks of the cursor: captured only while control is allowed.</summary>
        public bool WantsCursorCaptured => captureCursor && ControlAllowed;

        /// <summary>Current look angle, degrees; positive looks up.</summary>
        public float PitchDegrees { get; private set; }

        public float WalkSpeedMetresPerSecond => walkSpeedMetresPerSecond;

        public float MaxPitchDegrees => maxPitchDegrees;

        public Transform ViewPivot => viewPivot;

        public InteractionResolver Interaction => interaction;

        /// <summary>Result of the most recent interact press, or null.</summary>
        public CommandResult? LastInteraction { get; private set; }

        /// <summary>Raised when the pause control is pressed (also while control is not allowed, so it can resume).</summary>
        public event Action PauseRequested;

        /// <summary>Explicit wiring for code-built rigs; scenes normally use the serialized fields.</summary>
        public void Configure(Transform pivot, InteractionResolver resolver)
        {
            viewPivot = pivot;
            interaction = resolver;
        }

        /// <summary>Places the feet at a position with a heading, level view and no residual velocity.</summary>
        public void Teleport(Vector3 feetPosition, float yawDegrees)
        {
            CharacterController controller = Body;
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(feetPosition, Quaternion.Euler(0f, yawDegrees, 0f));
            controller.enabled = wasEnabled;
            verticalVelocity = 0f;
            PitchDegrees = 0f;
            ApplyPitch();
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

            if (!allowed)
            {
                verticalVelocity = 0f;
                return;
            }

            Look(input.LookDegrees);
            Walk(input.Move, Time.deltaTime);
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

        private void Walk(Vector2 move, float deltaSeconds)
        {
            CharacterController controller = Body;
            if (!controller.enabled || deltaSeconds <= 0f)
            {
                return;
            }

            Vector2 intent = Vector2.ClampMagnitude(move, 1f);
            Vector3 horizontal = ((transform.right * intent.x) + (transform.forward * intent.y)) * walkSpeedMetresPerSecond;
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1f;
            }

            verticalVelocity -= gravityMetresPerSecondSquared * deltaSeconds;
            controller.Move((horizontal + (Vector3.up * verticalVelocity)) * deltaSeconds);
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
