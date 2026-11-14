using NUnit.Framework;
using OutOfReach.Player;

namespace OutOfReach.Tests.Editor
{
    public sealed class ExertionTests
    {
        [TestCase(ExertionActivity.Sprinting, 0.5f)]
        [TestCase(ExertionActivity.Carrying, 0.875f)]
        [TestCase(ExertionActivity.TetherTraversal, 0.9f)]
        [TestCase(ExertionActivity.Walking, 1f)]
        public void FiveActiveSeconds_HaveIndependentGameTunedCosts(ExertionActivity activity, float expected)
        {
            var state = new PlayerExertion();
            state.Advance(5f, activity, 1f);
            Assert.That(state.Remaining01, Is.EqualTo(expected).Within(0.00001f));
        }

        [Test]
        public void ActualDistanceFraction_AccountsForAnalogMovementAndPartialRouteEnd()
        {
            var full = new PlayerExertion();
            var half = new PlayerExertion();
            full.Advance(4f, ExertionActivity.Carrying, 1f);
            half.Advance(4f, ExertionActivity.Carrying, 0.5f);
            Assert.That(1f - half.Remaining01, Is.EqualTo((1f - full.Remaining01) * 0.5f).Within(1e-6f));
            half.Advance(100f, ExertionActivity.Sprinting, 0f);
            Assert.That(half.Remaining01, Is.EqualTo(0.95f).Within(1e-6f), "blocked effort is not travel");
        }

        [Test]
        public void Exhaustion_ClampsAndRequiresRealRecovery_WithoutBlockingTraversal()
        {
            var state = new PlayerExertion();
            state.Advance(100f, ExertionActivity.Sprinting, 1f);
            Assert.That(state.Remaining01, Is.Zero);
            Assert.That(state.CanSprint, Is.False);
            Assert.That(state.TraversalSpeedFactor, Is.EqualTo(0.8f));
            state.Advance(1f, ExertionActivity.Resting, 0f);
            Assert.That(state.Remaining01, Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(state.CanSprint, Is.False, "no exhausted/resting oscillation every frame");
            Assert.That(state.TraversalSpeedFactor, Is.EqualTo(0.9f).Within(1e-6f));
            state.Advance(1f, ExertionActivity.Resting, 0f);
            Assert.That(state.CanSprint, Is.True);
            Assert.That(state.TraversalSpeedFactor, Is.EqualTo(1f));
            state.Advance(10000f, ExertionActivity.Resting, 0f);
            Assert.That(state.Remaining01, Is.EqualTo(1f));
        }

        [Test]
        public void TraversalTaper_IsContinuousBoundedAndDoesNotAffectFreshTravel()
        {
            var state = new PlayerExertion();
            for (int i = 0; i < 600; i++)
            {
                float before = state.TraversalSpeedFactor;
                state.Advance(0.1f, ExertionActivity.TetherTraversal, 1f);
                Assert.That(state.TraversalSpeedFactor, Is.InRange(0.8f, 1f));
                Assert.That(before - state.TraversalSpeedFactor, Is.InRange(0f, 0.00201f));
                if (state.Remaining01 >= 0.2f)
                {
                    Assert.That(state.TraversalSpeedFactor, Is.EqualTo(1f));
                }
            }
        }

        [Test]
        public void RestRestores_WalkingDoesNot_AndResetStartsFull()
        {
            var state = new PlayerExertion();
            state.Advance(5f, ExertionActivity.Sprinting, 1f);
            state.Advance(10f, ExertionActivity.Walking, 1f);
            Assert.That(state.Remaining01, Is.EqualTo(0.5f));
            state.Advance(2f, ExertionActivity.Resting, 0f);
            Assert.That(state.Remaining01, Is.EqualTo(0.7f).Within(1e-6f));
            state.Reset();
            Assert.That(state.Remaining01, Is.EqualTo(1f));
            Assert.That(state.CanSprint, Is.True);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidOrPausedTime_DoesNotChangeStamina(float seconds)
        {
            var state = new PlayerExertion();
            state.Advance(5f, ExertionActivity.Sprinting, 1f);
            state.Advance(seconds, ExertionActivity.Sprinting, 1f);
            state.Advance(seconds, ExertionActivity.Resting, 0f);
            Assert.That(state.Remaining01, Is.EqualTo(0.5f));
        }

        [Test]
        public void FramePartition_DoesNotChangeExertion()
        {
            var single = new PlayerExertion();
            var frames = new PlayerExertion();
            single.Advance(7f, ExertionActivity.Sprinting, 0.75f);
            for (int i = 0; i < 700; i++)
            {
                frames.Advance(0.01f, ExertionActivity.Sprinting, 0.75f);
            }
            Assert.That(frames.Remaining01, Is.EqualTo(single.Remaining01).Within(0.0001f));
        }
    }
}
