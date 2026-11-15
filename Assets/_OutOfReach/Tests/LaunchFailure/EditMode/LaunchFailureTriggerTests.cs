using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Presentation.LaunchFailure;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.LaunchFailure
{
    /// <summary>The launch-failure cinematic plays for Core's overweight-launch loss only.</summary>
    public sealed class LaunchFailureTriggerTests
    {
        private const string OverweightText =
            "Launch mass 616 kg exceeded the 600 kg limit. The vehicle failed to reach orbit and the crew was lost.";

        [Test]
        public void ItPlays_ForTheRealOverweightLaunchLoss()
        {
            using (var session = new MissionSession(AuroraMission.CreateDefinition(), null, AuroraMission.RedundancyPresetId))
            {
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 5).Succeeded, Is.True);
                MissionEnded? ended = null;
                session.Events.MissionEnded += message => ended = message;

                Assert.That(session.Planner.TryLaunch().Succeeded, Is.True);

                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.LaunchOverweight));
                Assert.That(ended.HasValue, Is.True, "Core published the existing mission-ended signal");
                Assert.That(LaunchFailureTrigger.ShouldPlay(ended.Value), Is.True, ended.Value.Reason);
            }
        }

        [Test]
        public void ItNeverPlays_ForOtherLosses()
        {
            using (var session = new MissionSession(AuroraMission.CreateDefinition(), null, AuroraMission.ScienceFocusPresetId))
            {
                MissionEnded? ended = null;
                session.Events.MissionEnded += message => ended = message;
                Assert.That(session.Planner.TryLaunch().Succeeded, Is.True);
                Assert.That(session.Director.TryEndMission(MissionOutcome.Lost, "Cabin CO2 reached the emergency level.").Succeeded, Is.True);

                Assert.That(ended.HasValue, Is.True);
                Assert.That(LaunchFailureTrigger.ShouldPlay(ended.Value), Is.False);
            }

            Assert.That(LaunchFailureTrigger.ShouldPlay(MissionOutcome.Lost, "Suit oxygen ran out outside the ship."), Is.False);
            Assert.That(LaunchFailureTrigger.ShouldPlay(MissionOutcome.Lost, "Unmet food demand reached the game viability limit."), Is.False);
            Assert.That(LaunchFailureTrigger.ShouldPlay(MissionOutcome.Lost, null), Is.False);
            Assert.That(LaunchFailureTrigger.ShouldPlay(MissionOutcome.Lost, string.Empty), Is.False);
        }

        [TestCase(MissionOutcome.Recovered)]
        [TestCase(MissionOutcome.RecoveryPossible)]
        [TestCase(MissionOutcome.None)]
        public void ItNeverPlays_ForWinsOrNoOutcome_EvenWithTheSameText(MissionOutcome outcome)
        {
            Assert.That(LaunchFailureTrigger.ShouldPlay(MissionOutcome.Lost, OverweightText), Is.True);
            Assert.That(LaunchFailureTrigger.ShouldPlay(outcome, OverweightText), Is.False);
        }
    }
}
