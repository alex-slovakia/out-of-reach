using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OutOfReach.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.PlayMode
{
    public sealed class SprintExertionPlayTests
    {
        private const float FrameSeconds = 0.02f;
        private readonly List<GameObject> objects = new List<GameObject>();
        private FirstPersonController player;
        private SprintInput input;
        private bool allowed;

        private sealed class SprintInput : IPlayerInputSource, ISprintInputSource
        {
            public Vector2 Move { get; set; }
            public Vector2 LookDegrees { get; set; }
            public bool InteractPressedThisFrame => false;
            public bool PausePressedThisFrame => false;
            public bool SprintHeld { get; set; }
        }

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = FrameSeconds;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objects.Add(floor);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(200f, 1f, 200f);
            var rig = new GameObject("Sprint test player");
            objects.Add(rig);
            rig.transform.position = new Vector3(0f, 0.02f, 0f);
            CharacterController body = rig.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.3f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.skinWidth = 0.03f;
            body.minMoveDistance = 0.1f;
            var pivot = new GameObject("View");
            pivot.transform.SetParent(rig.transform, false);
            player = rig.AddComponent<FirstPersonController>();
            player.Configure(pivot.transform, null);
            input = new SprintInput();
            player.InputSource = input;
            allowed = true;
            player.ControlGate = () => allowed;
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject item in objects) Object.DestroyImmediate(item);
            objects.Clear();
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private void Tune(string field, float value)
        {
            var state = (PlayerExertion)typeof(FirstPersonController).GetField("exertion",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(player);
            typeof(PlayerExertion).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(state, value);
        }

        private TetheredRoute Route()
        {
            var root = new GameObject("Sprint test tether");
            objects.Add(root);
            var points = new Transform[3];
            Vector3[] positions = { new Vector3(0f, 1f, 0f), new Vector3(0f, 3f, 2f), new Vector3(0f, 3f, 5f) };
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new GameObject("Route point").transform;
                points[i].SetParent(root.transform, false);
                points[i].position = positions[i];
            }
            TetheredRoute route = root.AddComponent<TetheredRoute>();
            route.Configure(points, 0.25f, 1f);
            return route;
        }

        [UnityTest]
        public IEnumerator HeldSprint_IsOnePointFiveTimesWalk_AndOnlyActualTravelDrains()
        {
            yield return Frames(5);
            Assert.That(player.GetComponent<CharacterController>().minMoveDistance, Is.Zero);
            input.Move = Vector2.up;
            float start = player.transform.position.z;
            yield return Frames(25);
            float walk = player.transform.position.z - start;
            Assert.That(player.IsSprinting, Is.False);
            Assert.That(player.ExertionRemaining01, Is.EqualTo(1f), "ordinary walking has no extra cost");
            input.SprintHeld = true;
            start = player.transform.position.z;
            yield return Frames(25);
            Assert.That(player.transform.position.z - start, Is.EqualTo(walk * 1.5f).Within(0.03f));
            Assert.That(player.IsSprinting, Is.True);
            float spent = player.ExertionRemaining01;
            Assert.That(spent, Is.LessThan(1f));
            input.Move = Vector2.zero;
            yield return Frames(20);
            Assert.That(player.IsSprinting, Is.False, "holding Shift at rest is not sprinting");
            Assert.That(player.ExertionRemaining01, Is.GreaterThan(spent));
        }

        [UnityTest]
        public IEnumerator WallBlockedInput_DoesNotKeepSprintingOrSpendStamina()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objects.Add(wall);
            wall.transform.position = new Vector3(0f, 1.5f, 2f);
            wall.transform.localScale = new Vector3(8f, 3f, 1f);
            Physics.SyncTransforms();
            yield return Frames(5);
            input.Move = Vector2.up;
            input.SprintHeld = true;
            yield return Frames(40);
            Vector3 stopped = player.transform.position;
            float remaining = player.ExertionRemaining01;
            yield return Frames(20);
            Assert.That(Vector3.Distance(stopped, player.transform.position), Is.LessThan(0.001f));
            Assert.That(player.IsSprinting, Is.False);
            Assert.That(player.ExertionRemaining01, Is.GreaterThanOrEqualTo(remaining));
        }

        [UnityTest]
        public IEnumerator DeniedControl_FocusAndPauseFreezeExertion_RequireFreshSprintPress()
        {
            yield return Frames(5);
            input.Move = Vector2.up;
            input.SprintHeld = true;
            yield return Frames(10);
            Assert.That(player.IsSprinting, Is.True);
            allowed = false;
            Vector3 stopped = player.transform.position;
            float remaining = player.ExertionRemaining01;
            yield return Frames(10);
            Assert.That(player.IsSprinting, Is.False);
            Assert.That(player.transform.position, Is.EqualTo(stopped));
            Assert.That(player.ExertionRemaining01, Is.EqualTo(remaining));
            allowed = true;
            yield return Frames(5);
            Assert.That(player.IsSprinting, Is.False, "held sprint does not leak through a menu or cinematic");
            input.SprintHeld = false;
            yield return Frames(1);
            input.SprintHeld = true;
            yield return Frames(3);
            Assert.That(player.IsSprinting, Is.True);
            player.SendMessage("OnApplicationFocus", false);
            remaining = player.ExertionRemaining01;
            stopped = player.transform.position;
            yield return Frames(5);
            Assert.That(player.IsSprinting, Is.False);
            Assert.That(player.transform.position, Is.EqualTo(stopped));
            Assert.That(player.ExertionRemaining01, Is.EqualTo(remaining));
            player.SendMessage("OnApplicationFocus", true);
            yield return Frames(3);
            Assert.That(player.IsSprinting, Is.False);
            Time.timeScale = 0f;
            remaining = player.ExertionRemaining01;
            yield return Frames(5);
            Assert.That(player.ExertionRemaining01, Is.EqualTo(remaining));
            Assert.That(player.IsSprinting, Is.False);
        }

        [UnityTest]
        public IEnumerator EmptyStamina_StillWalks_AndHeldSprintCannotPulseBackOn()
        {
            Tune("sprintDrainPerSecond", 100000f);
            Tune("restRecoveryPerSecond", 100000f);
            yield return Frames(5);
            input.Move = Vector2.up;
            input.SprintHeld = true;
            yield return Frames(5);
            Assert.That(player.ExertionRemaining01, Is.Zero, "exhausted through actual sprint travel");
            Assert.That(player.IsSprinting, Is.False);
            float start = player.transform.position.z;
            yield return Frames(20);
            Assert.That(player.transform.position.z - start, Is.EqualTo(player.WalkSpeedMetresPerSecond * 20 * FrameSeconds).Within(0.03f));
            input.Move = Vector2.zero;
            yield return Frames(3);
            Assert.That(player.ExertionRemaining01, Is.EqualTo(1f));
            input.Move = Vector2.up;
            yield return Frames(3);
            Assert.That(player.IsSprinting, Is.False, "recovered stamina requires releasing the old sprint hold");
            Assert.That(player.ExertionRemaining01, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator ExhaustedTether_StaysContinuousReversibleAndReachesBothEnds()
        {
            Tune("tetherDrainPerSecond", 100000f);
            Tune("restRecoveryPerSecond", 0f);
            yield return Frames(5);
            TetheredRoute route = Route();
            player.EnterRoute(route);
            input.SprintHeld = true;
            input.Move = Vector2.up;
            input.LookDegrees = new Vector2(0f, -200f);
            yield return Frames(3);
            input.LookDegrees = Vector2.zero;
            Assert.That(player.ExertionRemaining01, Is.Zero);
            Assert.That(player.IsSprinting, Is.False, "EVA cannot sprint");
            float distance = player.RouteDistanceMetres;
            yield return Frames(50);
            Assert.That(player.RouteDistanceMetres - distance, Is.EqualTo(PlayerExertion.MinimumTraversalFactor).Within(0.025f),
                "traversal floor at empty");
            Assert.That(Vector3.Distance(player.transform.position, route.PointAt(player.RouteDistanceMetres)), Is.LessThan(0.0001f));
            yield return Frames(400);
            Assert.That(player.IsAtRouteEnd, Is.True);
            input.Move = -Vector2.up;
            yield return Frames(400);
            Assert.That(player.IsAtRouteStart, Is.True, "empty stamina can always return");
            Assert.That(player.ExertionRemaining01, Is.Zero);
            float remaining = player.ExertionRemaining01;
            player.ExitRoute(new Vector3(0f, 0.02f, 0f), 0f);
            Assert.That(player.ExertionRemaining01, Is.EqualTo(remaining), "EVA exit does not refill stamina");
        }

        [UnityTest]
        public IEnumerator TetherEndpointAndLookOnly_DoNotSpend_AndTeleportPreservesStamina()
        {
            Tune("restRecoveryPerSecond", 0f);
            yield return Frames(5);
            player.EnterRoute(Route());
            input.Move = -Vector2.up;
            input.SprintHeld = true;
            yield return Frames(10);
            Assert.That(player.ExertionRemaining01, Is.EqualTo(1f));
            input.Move = Vector2.up;
            yield return Frames(20);
            float remaining = player.ExertionRemaining01;
            Assert.That(remaining, Is.LessThan(1f));
            input.Move = Vector2.zero;
            input.LookDegrees = new Vector2(50f, 80f);
            Vector3 stopped = player.transform.position;
            yield return Frames(10);
            input.LookDegrees = Vector2.zero;
            Assert.That(player.transform.position, Is.EqualTo(stopped));
            Assert.That(player.ExertionRemaining01, Is.EqualTo(remaining));
            player.Teleport(new Vector3(0f, 0.02f, 0f), 0f);
            Assert.That(player.ExertionRemaining01, Is.EqualTo(remaining));
            player.ResetExertionForNewSession();
            Assert.That(player.ExertionRemaining01, Is.EqualTo(1f));
            input.Move = Vector2.up;
            yield return Frames(5);
            Assert.That(player.IsSprinting, Is.False, "restart cancels the old hold");
        }

        [Test]
        public void DefaultBindings_ReadHeldKeyboardShiftAndGamepadStickPress()
        {
            // A hidden test runner has no focused Game View. Route only this test's synthetic device events
            // to player actions using a temporary settings clone; never alter the project's settings asset.
            InputSettings previousSettings = InputSystem.settings;
            HideFlags previousFlags = previousSettings.hideFlags;
            InputSettings testSettings = Object.Instantiate(previousSettings);
            testSettings.hideFlags = HideFlags.HideAndDontSave;
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // Input System destroys its temporary HideAndDontSave defaults when settings are replaced.
            // Keep that original instance alive so teardown can restore both its identity and flags.
            previousSettings.hideFlags = HideFlags.None;
            InputSystem.settings = testSettings;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            using (var actions = new PlayerInputActions())
            {
                try
                {
                    actions.Enable();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                    InputSystem.Update();
                    Assert.That(keyboard.leftShiftKey.isPressed, Is.True, "synthetic event reaches the device");
                    Assert.That(actions.SprintHeld, Is.True);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightShift));
                    InputSystem.Update();
                    Assert.That(actions.SprintHeld, Is.True);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();
                    Assert.That(actions.SprintHeld, Is.False);
                    InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftStick));
                    InputSystem.Update();
                    Assert.That(actions.SprintHeld, Is.True);
                    InputSystem.QueueStateEvent(gamepad, new GamepadState());
                    InputSystem.Update();
                    Assert.That(actions.SprintHeld, Is.False);
                    actions.Disable();
                    Assert.That(actions.SprintHeld, Is.False);
                }
                finally
                {
                    InputSystem.RemoveDevice(keyboard);
                    InputSystem.RemoveDevice(gamepad);
                    InputSystem.settings = previousSettings;
                    previousSettings.hideFlags = previousFlags;
                    if (testSettings != null) Object.DestroyImmediate(testSettings);
                }
            }
        }
    }
}
