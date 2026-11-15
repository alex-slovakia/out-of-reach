using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Composition;
using OutOfReach.Presentation.UI.Report;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace OutOfReach.Tests.Composition
{
    /// <summary>QUIT-TO-DESKTOP-01: the two-step confirmation on its own, with explicit times.</summary>
    public sealed class SessionQuitConfirmationTests
    {
        [Test]
        public void TheFirstPressArmsAndASecondPressInsideTheWindowConfirms()
        {
            var confirmation = new SessionQuitConfirmation();

            Assert.That(confirmation.Press(10f, 3f), Is.False, "one press never confirms");
            Assert.That(confirmation.IsArmed, Is.True);
            Assert.That(confirmation.Press(13f, 3f), Is.True, "the window includes its last instant");
            Assert.That(confirmation.IsArmed, Is.False);
            Assert.That(confirmation.Press(13.5f, 3f), Is.False, "after a confirmation the next press starts over");
        }

        [Test]
        public void APressAfterTheWindowArmsAgainInsteadOfConfirming()
        {
            var confirmation = new SessionQuitConfirmation();
            confirmation.Press(10f, 3f);

            Assert.That(confirmation.Press(13.01f, 3f), Is.False);
            Assert.That(confirmation.IsArmed, Is.True, "the late press arms a new window");
            Assert.That(confirmation.Press(15f, 3f), Is.True, "the new window counts from the late press");
        }

        [Test]
        public void ExpireDisarmsOnlyOnceTheWindowHasPassed()
        {
            var confirmation = new SessionQuitConfirmation();
            Assert.That(confirmation.Expire(0f, 3f), Is.False, "nothing is armed");

            confirmation.Press(1f, 3f);
            Assert.That(confirmation.Expire(4f, 3f), Is.False);
            Assert.That(confirmation.IsArmed, Is.True);
            Assert.That(confirmation.Expire(4.5f, 3f), Is.True);
            Assert.That(confirmation.IsArmed, Is.False);
            Assert.That(confirmation.Expire(9f, 3f), Is.False, "reported once");
        }

        [Test]
        public void DisarmAndTimeRunningBackwardsNeverConfirm()
        {
            var confirmation = new SessionQuitConfirmation();
            confirmation.Press(5f, 3f);
            confirmation.Disarm();

            Assert.That(confirmation.Press(5.5f, 3f), Is.False, "a disarmed confirmation only arms again");
            Assert.That(confirmation.Press(4f, 3f), Is.False, "an earlier time is outside the window");
            Assert.That(confirmation.IsArmed, Is.True);
        }

        [Test]
        public void InvalidTimeOrWindowIsRejectedAndChangesNothing()
        {
            var confirmation = new SessionQuitConfirmation();

            Assert.Throws<ArgumentOutOfRangeException>(() => confirmation.Press(float.NaN, 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => confirmation.Press(1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => confirmation.Expire(float.PositiveInfinity, 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => confirmation.Expire(1f, float.NaN));
            Assert.That(confirmation.IsArmed, Is.False);
        }
    }

    /// <summary>The pause help names only controls that exist in the player's actual movement mode (root 6885223398).</summary>
    public sealed class PauseControlsHintTests
    {
        [Test]
        public void WalkingNamesWasdAndSprint()
        {
            string hint = I01SessionControls.ControlsHintFor(false, false, false);

            Assert.That(hint, Does.Contain("WASD move"));
            Assert.That(hint, Does.Contain("Shift sprint"));
            Assert.That(hint, Does.Contain("E interact"));
            Assert.That(hint, Does.Contain("Esc resume"));
        }

        [Test]
        public void TheTetherRouteHasNoSprint()
        {
            string hint = I01SessionControls.ControlsHintFor(false, true, true);

            Assert.That(hint, Does.Contain("W/S move along the tether"));
            Assert.That(hint, Does.Not.Contain("sprint"));
            Assert.That(hint, Does.Not.Contain("rail"));
        }

        [Test]
        public void FloatingPromisesPushesButNoWasdThrustNoSprintAndNoBareGrip()
        {
            string hint = I01SessionControls.ControlsHintFor(true, false, false);

            Assert.That(hint, Does.Contain("click a surface in reach: push off"));
            Assert.That(hint, Does.Contain("Hold W + Space: push off"));
            Assert.That(hint, Does.Contain("A/D: lean"));
            Assert.That(hint, Does.Contain("E use equipment"));
            Assert.That(hint, Does.Contain("Esc resume"));
            Assert.That(hint, Does.Not.Contain("WASD move"));
            Assert.That(hint, Does.Not.Contain("sprint"));
            Assert.That(hint, Does.Not.Contain("grip"), "a bare-hand grip is not authored");
        }

        [Test]
        public void OnlyASuitedFloatingPlayerIsToldAboutRailGrips()
        {
            Assert.That(I01SessionControls.ControlsHintFor(true, false, true), Does.Contain(I01SessionControls.SuitedRailHint));
            Assert.That(I01SessionControls.ControlsHintFor(false, false, true), Does.Not.Contain("rail"));
            Assert.That(I01SessionControls.ControlsHintFor(true, true, false),
                Is.EqualTo(I01SessionControls.ControlsHintFor(true, false, false)), "floating wins over a tether route");
        }
    }

    /// <summary>
    /// QUIT-TO-DESKTOP-01 in a real runtime UI panel: the menu button and the mission record button go through actual
    /// UI Toolkit submit events, with an injected quit action so nothing closes. No mission composition is bound.
    /// </summary>
    public sealed class QuitToDesktopUiTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i]) Object.Destroy(created[i]);
            }

            created.Clear();
        }

        [Test]
        public void QuitIsTheThirdSessionMenuAction()
        {
            Build(out UIDocument document);

            List<Button> actions = document.rootVisualElement.Query<Button>(className: "session-menu-action").ToList();
            Assert.That(actions.Count, Is.EqualTo(3), "primary, secondary and quit");
            Button quit = actions[2];
            Assert.That(quit.name, Is.EqualTo(I01SessionControls.QuitButtonName));
            Assert.That(quit.text, Is.EqualTo(I01SessionControls.QuitLabel));
            Assert.That(quit.focusable, Is.True, "keyboard and gamepad navigation reach it");
            Assert.That(quit.enabledSelf, Is.True);
            Assert.That(quit.parent, Is.SameAs(actions[1].parent), "the same menu panel");
            Assert.That(quit.parent.IndexOf(quit), Is.EqualTo(quit.parent.IndexOf(actions[1]) + 1), "right after the second action");
        }

        [Test]
        public void TheFirstPressOnlyArmsAndTheSecondQuitsExactlyOnce()
        {
            I01SessionControls controls = Build(out UIDocument document);
            int quits = 0;
            controls.ConfigureQuitAction(() => quits++);
            Button quit = document.rootVisualElement.Q<Button>(I01SessionControls.QuitButtonName);

            Submit(quit);
            Assert.That(quits, Is.Zero, "one press never quits");
            Assert.That(controls.IsQuitArmed, Is.True);
            Assert.That(quit.text, Is.EqualTo(I01SessionControls.QuitConfirmLabel));

            Submit(quit);
            Assert.That(quits, Is.EqualTo(1));
            Assert.That(controls.QuitRequested, Is.True);
            Assert.That(quit.text, Is.EqualTo(I01SessionControls.QuittingLabel));
            Assert.That(quit.enabledSelf, Is.False, "nothing to press while quitting");

            Submit(quit);
            Submit(quit);
            Assert.That(quits, Is.EqualTo(1), "a requested quit runs once");
        }

        [UnityTest]
        public IEnumerator AnArmedQuitExpiresAfterItsWindow()
        {
            I01SessionControls controls = Build(out UIDocument document);
            int quits = 0;
            controls.ConfigureQuitAction(() => quits++);
            controls.QuitConfirmSeconds = .25f;
            Button quit = document.rootVisualElement.Q<Button>(I01SessionControls.QuitButtonName);

            Submit(quit);
            Assert.That(controls.IsQuitArmed, Is.True);
            float giveUpAt = Time.unscaledTime + 2f;
            while (controls.IsQuitArmed && Time.unscaledTime < giveUpAt) yield return null;

            Assert.That(controls.IsQuitArmed, Is.False, "the window passed in real time");
            Assert.That(quit.text, Is.EqualTo(I01SessionControls.QuitLabel));
            Submit(quit);
            Assert.That(quits, Is.Zero, "a press after the window arms again; it does not quit");
            Assert.That(controls.IsQuitArmed, Is.True);
        }

        [Test]
        public void AClosedMenuIgnoresItsQuitButton()
        {
            I01SessionControls controls = Build(out UIDocument document);
            int quits = 0;
            controls.ConfigureQuitAction(() => quits++);
            Button quit = document.rootVisualElement.Q<Button>(I01SessionControls.QuitButtonName);
            VisualElement menu = document.rootVisualElement.Q("i01-mode-pause-overlay");
            Assert.That(menu, Is.Not.Null);

            // The menu closes (Esc resume) while its quit button may still hold keyboard focus.
            menu.style.display = DisplayStyle.None;
            Submit(quit);
            Submit(quit);
            Assert.That(controls.IsQuitArmed, Is.False, "a closed menu's button does not arm");
            Assert.That(quits, Is.Zero, "two presses into a closed menu never quit");

            menu.style.display = DisplayStyle.Flex;
            Submit(quit);
            Assert.That(controls.IsQuitArmed, Is.True, "the shown menu works again");
            Assert.That(quits, Is.Zero);
        }

        [Test]
        public void AClosedMenuIgnoresItsReducedEffectsToggle()
        {
            Build(out UIDocument document);
            Toggle reduced = document.rootVisualElement.Q<Toggle>(className: "session-reduced");
            VisualElement menu = document.rootVisualElement.Q("i01-mode-pause-overlay");
            Assert.That(reduced, Is.Not.Null);
            bool before = reduced.value;

            // The menu closes (Esc or Start resumes) while the toggle may still hold keyboard/gamepad focus.
            menu.style.display = DisplayStyle.None;
            Submit(reduced);
            Assert.That(reduced.value, Is.EqualTo(before), "a stray submit into a closed menu changes nothing");

            menu.style.display = DisplayStyle.Flex;
            Submit(reduced);
            Assert.That(reduced.value, Is.Not.EqualTo(before), "the shown menu's toggle still works");
        }

        [Test]
        public void TheToggleGuardKeepsTheProgrammaticValue()
        {
            Build(out UIDocument document);
            Toggle reduced = document.rootVisualElement.Q<Toggle>(className: "session-reduced");
            document.rootVisualElement.Q("i01-mode-pause-overlay").style.display = DisplayStyle.None;

            // Refresh mirrors the real setting with SetValueWithoutNotify, also while the menu is hidden.
            reduced.SetValueWithoutNotify(true);
            Assert.That(reduced.value, Is.True);
            Submit(reduced);
            Assert.That(reduced.value, Is.True, "a stray submit cannot flip the mirrored setting");
            reduced.SetValueWithoutNotify(false);
            Assert.That(reduced.value, Is.False, "programmatic updates still apply");
        }

        [UnityTest]
        public IEnumerator BlurFocusWithinReleasesOnlyFocusInsideTheMenu()
        {
            Build(out UIDocument document);
            VisualElement menu = document.rootVisualElement.Q("i01-mode-pause-overlay");
            Button quit = document.rootVisualElement.Q<Button>(I01SessionControls.QuitButtonName);
            var outside = new Button { name = "outside-focus-probe", text = "probe" };
            document.rootVisualElement.Add(outside);

            quit.Focus();
            yield return null;
            Assert.That(quit.focusController.focusedElement, Is.SameAs(quit), "the menu button holds focus");
            I01SessionControls.BlurFocusWithin(menu);
            Assert.That(quit.focusController.focusedElement, Is.Not.SameAs(quit), "focus inside the menu is released");

            outside.Focus();
            yield return null;
            Assert.That(outside.focusController.focusedElement, Is.SameAs(outside));
            I01SessionControls.BlurFocusWithin(menu);
            Assert.That(outside.focusController.focusedElement, Is.SameAs(outside), "focus outside the menu is left alone");
        }

        [Test]
        public void ReleasingTheMenuDisarmsTheQuit()
        {
            I01SessionControls controls = Build(out UIDocument document);
            int quits = 0;
            controls.ConfigureQuitAction(() => quits++);
            Submit(document.rootVisualElement.Q<Button>(I01SessionControls.QuitButtonName));
            Assert.That(controls.IsQuitArmed, Is.True);

            controls.enabled = false;
            Assert.That(controls.IsQuitArmed, Is.False, "a released menu keeps no armed quit");

            controls.enabled = true;
            controls.Configure(null);
            Button rebuilt = document.rootVisualElement.Q<Button>(I01SessionControls.QuitButtonName);
            Assert.That(rebuilt.text, Is.EqualTo(I01SessionControls.QuitLabel));
            Submit(rebuilt);
            Assert.That(quits, Is.Zero, "the first press in the rebuilt menu only arms");
        }

        [Test]
        public void TheConfirmWindowIsClamped()
        {
            I01SessionControls controls = Build(out _);
            Assert.That(controls.QuitConfirmSeconds, Is.EqualTo(I01SessionControls.DefaultQuitConfirmSeconds));

            controls.QuitConfirmSeconds = 0f;
            Assert.That(controls.QuitConfirmSeconds, Is.EqualTo(.25f));
            controls.QuitConfirmSeconds = 60f;
            Assert.That(controls.QuitConfirmSeconds, Is.EqualTo(10f));
            controls.QuitConfirmSeconds = float.NaN;
            Assert.That(controls.QuitConfirmSeconds, Is.EqualTo(I01SessionControls.DefaultQuitConfirmSeconds));
        }

        [Test]
        public void TheMissionRecordEndCardGetsQuitAfterPlanAgain()
        {
            Build(out UIDocument document);
            var sheet = ScriptableObject.CreateInstance<StyleSheet>();
            created.Add(sheet);
            var host = new VisualElement();
            document.rootVisualElement.Add(host);
            var record = new FlightRecorderPresenter(host, sheet);
            try
            {
                int quits = 0;
                int cues = 0;
                Button button = I01SessionControls.AddRecordQuitButton(record.Root, () => quits++, () => cues++);

                VisualElement actions = record.Root.Q<VisualElement>(I01SessionControls.RecordActionsName);
                Assert.That(actions, Is.Not.Null, "the record still names its action column");
                Assert.That(button.parent, Is.SameAs(actions));
                Button restart = actions.Q<Button>("reportRestartButton");
                Assert.That(restart, Is.Not.Null);
                Assert.That(actions.IndexOf(button), Is.EqualTo(actions.IndexOf(restart) + 1), "right after Plan again");
                Assert.That(button.ClassListContains("recorder-button"), Is.True, "the record's own button style");
                Assert.That(button.name, Is.EqualTo(I01SessionControls.RecordQuitButtonName));
                Assert.That(button.text, Is.EqualTo(I01SessionControls.QuitLabel));
                Assert.That(button.focusable, Is.True);

                Submit(button);
                Assert.That(quits, Is.EqualTo(1), "it runs the quit handler it was given");
            }
            finally
            {
                record.Dispose();
            }
        }

        private I01SessionControls Build(out UIDocument document)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            created.Add(settings);
            var owner = new GameObject("QuitToDesktopTestUi");
            created.Add(owner);
            owner.SetActive(false);
            document = owner.AddComponent<UIDocument>();
            document.panelSettings = settings;
            var controls = owner.AddComponent<I01SessionControls>();
            controls.ConfigureDocument(document, null);
            owner.SetActive(true);
            controls.Configure(null);
            Assert.That(document.rootVisualElement, Is.Not.Null, "the runtime panel exists");
            Assert.That(document.rootVisualElement.Q<Button>(I01SessionControls.QuitButtonName), Is.Not.Null, "the menu view was built");
            return controls;
        }

        private static void Submit(VisualElement target)
        {
            Assert.That(target.panel, Is.Not.Null, "the element is in a live panel");
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = target;
                target.SendEvent(submit);
            }
        }
    }
}
