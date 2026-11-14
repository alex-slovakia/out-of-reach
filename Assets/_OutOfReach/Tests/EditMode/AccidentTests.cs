using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    public sealed class AccidentTests
    {
        [Test]
        public void CockpitCheck_TriggersTheImpactOnce_CinematicFirstThenDamage()
        {
            using (var session = Fixtures.LaunchedSession())
            using (var recorder = new EventRecorder(session.Events))
            {
                CommandResult first = session.Director.TryCompleteCockpitCheck();

                Assert.That(first.Succeeded, Is.True, first.Explanation);
                Assert.That(recorder.Order, Is.EqualTo(new[]
                {
                    "CinematicRequested", "SystemConditionChanged", "SystemConditionChanged", "SystemConditionChanged",
                }));
                Assert.That(recorder.CinematicRequests[0].Cinematic, Is.EqualTo(CinematicId.Impact));
                Assert.That(session.Systems.Power.Condition01, Is.EqualTo(0.2d));
                Assert.That(session.Systems.LifeSupport.Condition01, Is.EqualTo(0d));
                Assert.That(session.Systems.Antenna.Condition01, Is.EqualTo(0d));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro), "the emergency starts when the impact sequence ends");

                CommandResult duringCinematic = session.Director.TryCompleteCockpitCheck();
                Assert.That(duringCinematic.Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(recorder.Total, Is.EqualTo(4), "no duplicate damage or second cinematic");

                Fixtures.AcknowledgeActiveCinematic(session);

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Emergency));
                Assert.That(recorder.Order.GetRange(4, 2), Is.EqualTo(new[] { "CinematicFinished", "PhaseChanged" }));
                Assert.That(session.Director.TryCompleteCockpitCheck().Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(session.Systems.Power.Condition01, Is.EqualTo(0.2d), "damage is never applied twice");
                Assert.That(recorder.Total, Is.EqualTo(6));
            }
        }

        [Test]
        public void ImpactWithoutPresentation_StillReachesEmergency_OnTheNextTick()
        {
            using (var session = Fixtures.LaunchedSession())
            {
                Assert.That(session.Director.TryCompleteCockpitCheck().Succeeded, Is.True);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));

                session.Tick(0d);

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Emergency));
                Assert.That(session.IsPlayerControlAllowed, Is.True, "a missing cinematic can never strand the player");
            }
        }

        [Test]
        public void InstantlySkippedCinematic_AppliesTheSameConsequences()
        {
            using (var session = Fixtures.LaunchedSession())
            {
                session.Events.CinematicRequested += request =>
                    session.Events.Publish(new CinematicFinished(request.Cinematic, request.SequenceId, true));

                Assert.That(session.Director.TryCompleteCockpitCheck().Succeeded, Is.True);

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Emergency));
                Assert.That(session.Systems.Power.IsOperational, Is.False);
                Assert.That(session.Systems.LifeSupport.IsOperational, Is.False);
                Assert.That(session.Systems.Antenna.IsOperational, Is.False);
                Assert.That(session.Cinematics.LastCompletion.Value.WasSkipped, Is.True);
            }
        }

        [Test]
        public void CockpitCheck_IsRejectedOutsideIntro_WithoutDamage()
        {
            using (var session = Fixtures.NewSession())
            using (var recorder = new EventRecorder(session.Events))
            {
                CommandResult result = session.Director.TryCompleteCockpitCheck();

                Assert.That(result.Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(session.Director.AccidentTriggered, Is.False);
                Assert.That(session.Systems.Power.IsOperational, Is.True);
                Assert.That(recorder.Total, Is.EqualTo(0));
            }
        }

        [Test]
        public void Accident_ChangesTheRates_ConsistentlyWithTheDamagedSystems()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                EmergencyState power = session.Director.PowerState;
                ResourceRates rates = session.Resources.Rates;

                Assert.That(power.GenerationWatts, Is.EqualTo(380d).Within(Fixtures.Tolerance), "20 % of 1,900 W reaches the bus");
                Assert.That(power.LoadWatts, Is.EqualTo(1100d).Within(Fixtures.Tolerance), "failed life support sheds its 300 W");
                Assert.That(rates.NetLoadWatts, Is.EqualTo(720d).Within(Fixtures.Tolerance));
                Assert.That(power.Brownout, Is.False);
                Assert.That(rates.CarbonDioxideChangePpmPerSecond, Is.EqualTo(CabinAtmosphere.CrewCarbonDioxidePpmPerSecond(1)).Within(1e-12), "no scrubbing");
                Assert.That(rates.OxygenUseKgPerSecond, Is.EqualTo((0.84d + 2d) / 86400d).Within(1e-12), "breathing plus the leak");
            }
        }
    }
}
