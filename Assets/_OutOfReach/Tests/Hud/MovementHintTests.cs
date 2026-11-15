using System.Linq;
using NUnit.Framework;
using OutOfReach.Presentation.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Tests.Hud
{
    /// <summary>
    /// MOVEMENT-HINT-01 (Alex 6896797024 item 1): the first in-flight movement hint teaches the delivered controls,
    /// stays readable, then leaves once per session by sliding and fading to the right (fade only with reduced motion).
    /// The real view is built without a panel; the exit is inline style written by the view's own presentation step.
    /// </summary>
    public sealed class MovementHintTests
    {
        private FlightBoardView view;

        [SetUp]
        public void SetUp() => view = new FlightBoardView(new VisualElement(), null);

        [TearDown]
        public void TearDown() => view.Dispose();

        private VisualElement Hint => view.MicrogravityHintElement;
        private float Opacity => Hint.style.opacity.value;
        private float OffsetRight => Hint.style.translate.value.x.value;
        private bool Hidden => Hint.ClassListContains("is-hidden");

        private void Run(float seconds, bool paused = false)
        {
            for (float t = 0f; t < seconds - 1e-5f; t += .05f) view.AdvancePresentation(Mathf.Min(.05f, seconds - t), paused);
        }

        [Test]
        public void TeachesTheDeliveredControlsWithWAndSpaceFirst()
        {
            string[] lines = Hint.Query<Label>(className: "control-hint-action").ToList().Select(l => l.text).ToArray();
            Assert.That(lines, Is.EqualTo(FlightBoardView.MicrogravityHintLines));
            Assert.That(lines[0], Does.StartWith("Hold W + press Space: push off"));
            Assert.That(lines[0], Does.Contain("legs on the deck, hands near walls"));
            Assert.That(lines[1], Does.Contain("On a handrail: grab it, click again to push off"));
            Assert.That(lines[2], Does.Contain("strong two-hand push"));
            Assert.That(lines[3], Does.Contain("lean and side-step off a nearby surface"));
            Assert.That(Hint.Q<Label>(className: "microgravity-title").text, Is.EqualTo("MOVING IN MICROGRAVITY"));
            string[] keys = Hint.Query<Label>(className: "control-key").ToList().Select(l => l.text).ToArray();
            Assert.That(keys, Is.EqualTo(new[] { "W", "SPACE", "A", "D" }), "W + Space are taught first, A / D last");
            Assert.That(Hidden, Is.True, "nothing shows before the root allows it");
        }

        [Test]
        public void StaysReadableThenSlidesOutToTheRight()
        {
            view.SetMicrogravityPrompt(true, false);
            Assert.That(view.IsMicrogravityHintVisible && !Hidden, Is.True);
            Run(FlightBoardView.MicrogravityHintHoldSeconds - .1f);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Showing));
            Assert.That(Opacity, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(OffsetRight, Is.EqualTo(0f).Within(1e-4f));
            Run(.2f);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Exiting));
            Run(.8f);
            Assert.That(OffsetRight, Is.GreaterThan(30f).And.LessThan(FlightBoardView.MicrogravityHintExitPixels), "moving right");
            Assert.That(Opacity, Is.GreaterThan(.1f).And.LessThan(.9f), "fading");
            Run(1f);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Done));
            Assert.That(Hidden && !view.IsMicrogravityHintVisible, Is.True);
            Assert.That(OffsetRight, Is.EqualTo(FlightBoardView.MicrogravityHintExitPixels).Within(1e-3f));
        }

        [Test]
        public void TheFirstPushLeavesAfterTheMinimumReadingTimeOnly()
        {
            view.SetMicrogravityPrompt(true, false);
            Run(2f);
            view.CompleteMicrogravityTutorial();
            Run(FlightBoardView.MicrogravityHintMinimumSeconds - 2f - .1f);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Showing), "still readable");
            Run(.2f);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Exiting));
            Run(FlightBoardView.MicrogravityHintExitSeconds + .1f);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Done));
        }

        [Test]
        public void ReducedMotionFadesWithoutMoving()
        {
            view.SetReducedMotion(true);
            view.SetMicrogravityPrompt(true, false);
            Run(FlightBoardView.MicrogravityHintHoldSeconds + .1f);
            Run(.2f);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Exiting));
            Assert.That(OffsetRight, Is.EqualTo(0f).Within(1e-4f), "no motion");
            Assert.That(Opacity, Is.LessThan(1f));
            Run(FlightBoardView.MicrogravityHintReducedExitSeconds);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Done));
            Assert.That(OffsetRight, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void SuspendedOrPausedTimeDoesNotCount()
        {
            view.SetMicrogravityPrompt(true, false);
            Run(5f);
            view.SetMicrogravityPrompt(false, false); // menu, cinematic or rest: the root hides the controls
            Assert.That(Hidden, Is.True);
            Run(30f);
            view.SetMicrogravityPrompt(true, false);
            Run(30f, paused: true);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Showing));
            Assert.That(!Hidden && Opacity > .999f, Is.True, "it returns unchanged");
            Run(FlightBoardView.MicrogravityHintHoldSeconds - 5f + .1f);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Exiting));
        }

        [Test]
        public void LeavesOncePerSessionAndAReplacementSessionTeachesAgain()
        {
            view.SetMicrogravityPrompt(true, false);
            Run(FlightBoardView.MicrogravityHintHoldSeconds + FlightBoardView.MicrogravityHintExitSeconds + .5f);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Done));
            view.SetMicrogravityPrompt(true, true);
            Run(5f);
            Assert.That(Hidden, Is.True, "never shown again in the same session");
            view.ResetMicrogravityHint();
            view.SetMicrogravityPrompt(true, false);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Showing));
            Assert.That(!Hidden && Opacity > .999f && Mathf.Abs(OffsetRight) < 1e-4f, Is.True);
        }

        [Test]
        public void AContextLossDuringTheExitFinishesItHidden()
        {
            view.SetMicrogravityPrompt(true, false);
            Run(FlightBoardView.MicrogravityHintHoldSeconds + .5f);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Exiting));
            view.SetMicrogravityPrompt(false, false);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Done));
            Assert.That(Hidden, Is.True);
        }

        [Test]
        public void ThePushBeforeTheHintAppearsSkipsIt()
        {
            view.CompleteMicrogravityTutorial();
            view.SetMicrogravityPrompt(true, false);
            Assert.That(view.MicrogravityHint, Is.EqualTo(FlightBoardView.MicrogravityHintPhase.Done));
            Assert.That(Hidden, Is.True);
        }
    }
}
