using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OutOfReach.Player
{
    /// <summary>
    /// Player bindings defined in code with the existing Input System package, so the shared project input asset
    /// is never modified. Keyboard and mouse: WASD or arrows to walk, mouse to look, E or left mouse button to
    /// interact, held Shift to sprint, Escape to pause. Gamepad: left stick, right stick, south button,
    /// held left-stick press to sprint, Start to pause.
    /// </summary>
    public sealed class PlayerInputActions : IPlayerInputSource, ISprintInputSource, IDisposable
    {
        public const string MapName = "OutOfReach.Player";

        private readonly InputActionMap map;
        private readonly InputAction move;
        private readonly InputAction lookPointer;
        private readonly InputAction lookStick;
        private readonly InputAction interact;
        private readonly InputAction pause;
        private readonly InputAction sprint;

        public PlayerInputActions()
        {
            map = new InputActionMap(MapName);

            move = map.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            lookPointer = map.AddAction("LookPointer", InputActionType.Value, "<Mouse>/delta");
            lookStick = map.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

            interact = map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            interact.AddBinding("<Mouse>/leftButton");
            interact.AddBinding("<Gamepad>/buttonSouth");

            pause = map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");

            sprint = map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            sprint.AddBinding("<Keyboard>/rightShift");
            sprint.AddBinding("<Gamepad>/leftStickPress");
        }

        /// <summary>Mouse look sensitivity, degrees per pixel of pointer movement.</summary>
        public float MouseDegreesPerPixel { get; set; } = 0.12f;

        /// <summary>Gamepad look speed at full stick deflection, degrees per second.</summary>
        public float StickDegreesPerSecond { get; set; } = 120f;

        /// <summary>The action map, for inspecting or rebinding the controls.</summary>
        public InputActionMap Map => map;

        public Vector2 Move => Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);

        public Vector2 LookDegrees
        {
            get
            {
                Vector2 pointer = lookPointer.ReadValue<Vector2>() * MouseDegreesPerPixel;
                Vector2 stick = lookStick.ReadValue<Vector2>() * (StickDegreesPerSecond * Time.unscaledDeltaTime);
                return pointer + stick;
            }
        }

        public bool InteractPressedThisFrame => interact.WasPressedThisFrame();

        public bool PausePressedThisFrame => pause.WasPressedThisFrame();

        public bool SprintHeld => sprint.IsPressed();

        public void Enable()
        {
            map.Enable();
        }

        public void Disable()
        {
            map.Disable();
        }

        public void Dispose()
        {
            map.Disable();
            map.Dispose();
        }
    }
}
