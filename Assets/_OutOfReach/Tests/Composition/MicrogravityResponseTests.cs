using System;
using NUnit.Framework;
using OutOfReach.Composition;
using OutOfReach.Player;

namespace OutOfReach.Tests.Composition
{
    /// <summary>CLAUDE-EVA-ROPE-04 increment 1: readable refusals and the agreed float response tuning.</summary>
    public sealed class MicrogravityResponseTests
    {
        [Test]
        public void EveryFootRefusal_HasAReadableReason_AndSuccessHasNone()
        {
            foreach (FloatActionResult result in Enum.GetValues(typeof(FloatActionResult)))
            {
                string reason = I06MicrogravityInputBindings.FootRefusalReason(result);
                if (result == FloatActionResult.Done) Assert.That(reason, Is.Empty);
                else Assert.That(reason, Is.Not.Empty, result.ToString());
            }
            StringAssert.Contains("deck", I06MicrogravityInputBindings.FootRefusalReason(FloatActionResult.NoSurface));
            StringAssert.Contains("Release the rail", I06MicrogravityInputBindings.FootRefusalReason(FloatActionResult.AlreadyHolding));
            StringAssert.Contains("not available", I06MicrogravityInputBindings.BareGripUnavailable);
        }

        [Test]
        public void FloatTuning_UsesTheFasterAgreedResponse()
        {
            var tuning = new MicrogravityLocomotion();
            Assert.That(tuning.MaxPushSpeed, Is.EqualTo(1.3f));
            Assert.That(tuning.MaxSpeed, Is.EqualTo(2f));
            Assert.That(tuning.PushCooldownSeconds, Is.EqualTo(0.25f));
            Assert.That(tuning.DragPerSecond, Is.Zero, "still no hidden drag force");
        }
    }
}
