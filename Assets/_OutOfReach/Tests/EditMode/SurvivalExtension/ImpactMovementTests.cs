using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;

namespace OutOfReach.Tests.EditMode
{
    public sealed class ImpactMovementTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ImpactMovementIsOptInAndPauseStillWins(bool enabled)
        {
            using (var session = new MissionSession(allowMovementDuringImpact: enabled))
            {
                session.Events.CinematicRequested += _ => { };
                Assert.That(session.Planner.TryLaunch().Succeeded, Is.True);
                Assert.That(session.Director.TryCompleteCockpitCheck().Succeeded, Is.True);
                Assert.That(session.Cinematics.IsWaiting, Is.True);
                Assert.That(session.Cinematics.ActiveCinematic, Is.EqualTo(CinematicId.Impact));
                Assert.That(session.IsPlayerControlAllowed, Is.EqualTo(enabled));
                Assert.That(session.Director.TryCompleteCockpitCheck().Succeeded, Is.False);
                session.Clock.Pause();
                Assert.That(session.IsPlayerControlAllowed, Is.False);
                session.Clock.Resume();
                Assert.That(session.IsPlayerControlAllowed, Is.EqualTo(enabled));
                Fixtures.AcknowledgeActiveCinematic(session);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Emergency));
                Assert.That(session.IsPlayerControlAllowed, Is.True);
                session.Dispose();
                Assert.That(session.IsPlayerControlAllowed, Is.False);
            }
        }

        [Test]
        public void ImpactOptInDoesNotUnlockEndingCinematic()
        {
            using (var session = new MissionSession(allowMovementDuringImpact: true))
            {
                session.Events.CinematicRequested += _ => { };
                Assert.That(session.Planner.TryLaunch().Succeeded, Is.True);
                session.Director.TryEndMission(MissionOutcome.Lost, "Test ending lock.");
                Assert.That(session.Cinematics.IsWaiting, Is.True);
                Assert.That(session.Cinematics.ActiveCinematic, Is.EqualTo(CinematicId.EndingPullback));
                Assert.That(session.IsPlayerControlAllowed, Is.False);
            }
        }
    }
}
