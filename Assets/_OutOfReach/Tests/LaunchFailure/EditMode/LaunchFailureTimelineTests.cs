using System;
using NUnit.Framework;
using OutOfReach.Presentation.LaunchFailure;

namespace OutOfReach.Tests.LaunchFailure
{
    /// <summary>The launch-failure timing as a pure state machine.</summary>
    public sealed class LaunchFailureTimelineTests
    {
        private static LaunchFailureTimeline RunTo(float seconds, bool reduced = false)
        {
            var timeline = new LaunchFailureTimeline(reduced);
            while (timeline.Elapsed < seconds - 1e-4f && !timeline.IsFinished)
                timeline.Advance(Math.Min(.05f, seconds - timeline.Elapsed), false);
            return timeline;
        }

        [Test]
        public void TheStages_RunInOrder_AndFinishAfterAboutFiveSeconds()
        {
            Assert.That(RunTo(.5f).Stage, Is.EqualTo(LaunchFailureStage.Liftoff));
            Assert.That(RunTo(2f).Stage, Is.EqualTo(LaunchFailureStage.Stutter));
            Assert.That(RunTo(3f).Stage, Is.EqualTo(LaunchFailureStage.Cut));
            Assert.That(RunTo(4f).Stage, Is.EqualTo(LaunchFailureStage.Card));
            Assert.That(RunTo(LaunchFailureTimeline.Duration).Stage, Is.EqualTo(LaunchFailureStage.Finished));
            Assert.That(LaunchFailureTimeline.Duration, Is.InRange(4.5f, 6f));
        }

        [Test]
        public void Liftoff_ShowsNothing_ThenTheStutterShakesAndShowsTheStrip()
        {
            LaunchFailureTimeline liftoff = RunTo(.6f);
            Assert.That(liftoff.StripVisible, Is.False);
            Assert.That(liftoff.BlackAlpha, Is.Zero);
            Assert.That(liftoff.ShakeX, Is.Zero);

            bool shook = false;
            var stutter = new LaunchFailureTimeline(false);
            while (stutter.Elapsed < LaunchFailureTimeline.StutterEnd - .05f)
            {
                stutter.Advance(.05f, false);
                if (stutter.Stage != LaunchFailureStage.Stutter) continue;
                Assert.That(stutter.StripVisible, Is.True);
                Assert.That(Math.Abs(stutter.ShakeX), Is.LessThanOrEqualTo(LaunchFailureTimeline.ShakePixels));
                shook |= stutter.ShakeX != 0f || stutter.ShakeY != 0f;
            }

            Assert.That(shook, Is.True);
        }

        [Test]
        public void TheCut_GoesToBlack_AndTheCardStaysOnBlack()
        {
            LaunchFailureTimeline card = RunTo(4.5f);
            Assert.That(card.BlackAlpha, Is.EqualTo(1f));
            Assert.That(card.CardAlpha, Is.EqualTo(1f));
            Assert.That(card.StripVisible, Is.False);
            Assert.That(RunTo(3.0f).BlackAlpha, Is.InRange(.01f, .99f), "a short fade without reduced effects");
        }

        [Test]
        public void ReducedEffects_NoShakeNoFlicker_AndAStraightCut()
        {
            var timeline = new LaunchFailureTimeline(true);
            while (!timeline.IsFinished)
            {
                Assert.That(timeline.ShakeX, Is.Zero);
                Assert.That(timeline.ShakeY, Is.Zero);
                if (timeline.StripVisible) Assert.That(timeline.StripAlpha, Is.EqualTo(1f));
                if (timeline.Elapsed >= LaunchFailureTimeline.StutterEnd) Assert.That(timeline.BlackAlpha, Is.EqualTo(1f));
                if (timeline.Elapsed >= LaunchFailureTimeline.CutEnd) Assert.That(timeline.CardAlpha, Is.EqualTo(1f));
                timeline.Advance(.05f, false);
            }
        }

        [Test]
        public void Pause_HoldsTheClock_AndBadStepsAreIgnored_AndHitchesAreCapped()
        {
            var timeline = new LaunchFailureTimeline(false);
            timeline.Advance(.05f, true);
            timeline.Advance(-1f, false);
            timeline.Advance(float.NaN, false);
            timeline.Advance(float.PositiveInfinity, false);
            Assert.That(timeline.Elapsed, Is.Zero);

            timeline.Advance(3f, false);
            Assert.That(timeline.Elapsed, Is.EqualTo(LaunchFailureTimeline.MaxStepSeconds).Within(1e-6f));
        }

        [Test]
        public void Skip_FinishesAtOnce_AndClearsEverything()
        {
            LaunchFailureTimeline timeline = RunTo(2f);
            timeline.Skip();

            Assert.That(timeline.IsFinished, Is.True);
            Assert.That(timeline.Stage, Is.EqualTo(LaunchFailureStage.Finished));
            Assert.That(timeline.StripVisible, Is.False);
            Assert.That(timeline.BlackAlpha, Is.Zero);
            Assert.That(timeline.CardAlpha, Is.Zero);
            Assert.That(timeline.ShakeX, Is.Zero);
        }
    }
}
