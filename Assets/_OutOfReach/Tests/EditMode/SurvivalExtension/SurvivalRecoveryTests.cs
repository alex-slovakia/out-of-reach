using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>
    /// C06-REST-RECOVERY R1: rest is an accelerated run of the one mission clock. While the crew rests, food and
    /// water leave the ledger at the BVAD crew-day rates, the needs neither accrue nor reset, and the rest stops at
    /// the first actual danger before any loss.
    /// </summary>
    public sealed class SurvivalRecoveryTests
    {
        private const double Hour = 3600d;

        private static void Require(CommandResult result)
        {
            Assert.That(result.Succeeded, Is.True, result.Explanation);
        }

        private static IInteractable Get(MissionSession session, string interactionId)
        {
            Assert.That(session.Interactions.TryGet(interactionId, out IInteractable interactable), Is.True, interactionId);
            return interactable;
        }

        /// <summary>The default loadout with the survival extension on, up to and (unless told otherwise) through contact.</summary>
        private static MissionSession Contact(bool receive = true, bool extraKit = false)
        {
            var session = new MissionSession(survivalExtension: true);
            if (extraKit)
            {
                Require(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 3));
            }

            Fixtures.Stabilize(session);
            Fixtures.CompleteEva(session);
            Fixtures.Transmit(session);
            if (receive)
            {
                Require(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
            }

            return session;
        }

        private static void SpendDown(MissionSession session, ResourceId id, double leave)
        {
            double cost = session.Resources.GetQuantity(id) - leave;
            Assert.That(cost, Is.GreaterThanOrEqualTo(0d));
            Require(session.Resources.TryConsume(new[] { new ResourceAmount(id, cost) }));
        }

        private static double FoodPerSecond(MissionSession session)
        {
            return CrewMetabolicAssumptions.FoodKgPerCrewDay * session.Definition.CrewCount / CrewMetabolicAssumptions.SecondsPerDay;
        }

        private static double WaterPerSecond(MissionSession session)
        {
            return CrewMetabolicAssumptions.WaterLitresPerCrewDay * session.Definition.CrewCount / CrewMetabolicAssumptions.SecondsPerDay;
        }

        [Test]
        public void Rest_BelongsToAnActiveUnpausedSurvivalChapter_AndEveryRefusalChangesNothing()
        {
            using (MissionSession plain = Fixtures.RecoveredSession())
            {
                Assert.That(plain.SurvivalRecovery, Is.Null, "extension disabled: no rest service");
                Assert.That(plain.Interactions.TryGet(InteractionIds.SurvivalSleep, out _), Is.False);
            }

            using (MissionSession session = Contact(receive: false))
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                Assert.That(recovery.CheckRest(8d).Failure, Is.EqualTo(CommandFailure.WrongPhase), "not before contact");
                Require(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds));

                ResourceSnapshot before = session.Resources.Snapshot;
                double scale = session.Clock.TimeScale;
                foreach (double hours in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d, 0d, 8.6d })
                {
                    Assert.That(recovery.TryBeginRest(hours).Failure, Is.EqualTo(CommandFailure.InvalidInput), hours.ToString());
                }

                Require(session.Clock.Pause());
                Assert.That(recovery.TryBeginRest(8d).Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "paused");
                Require(session.Clock.Resume());
                Assert.That(recovery.TryWake().Succeeded, Is.False, "nobody to wake");

                Assert.That(recovery.IsResting, Is.False);
                Assert.That(recovery.RestCount, Is.EqualTo(0));
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.None));
                Assert.That(session.Clock.TimeScale, Is.EqualTo(scale));
                Fixtures.AssertSnapshot(before, session.Resources.Snapshot, 0d);

                IInteractable sleep = Get(session, InteractionIds.SurvivalSleep);
                Assert.That(sleep.CanInteract, Is.True, sleep.Prompt);
                Require(sleep.TryInteract());
                Assert.That(recovery.IsResting, Is.True);
                Assert.That(recovery.RestRemainingSimulationSeconds, Is.EqualTo(SurvivalRecovery.MaximumRestHours * Hour).Within(1e-9));
                Require(sleep.TryInteract());
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.Woken), "the same position wakes the crew");
            }
        }

        [Test]
        public void ARest_RunsTheSameClockFaster_DrawsBvadFoodAndWater_AndKeepsTheNeedsItFound()
        {
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                Require(session.AdvanceSimulation(7d * Hour));
                double hunger = session.Survival.Hunger01;
                double thirst = session.Survival.Thirst01;
                Assert.That(hunger, Is.GreaterThan(0d));
                ResourceSnapshot before = session.Resources.Snapshot;
                double start = session.State.ElapsedSimulationSeconds;
                int changes = 0;
                recovery.Changed += () => changes++;

                Require(recovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
                Assert.That(recovery.IsResting, Is.True);
                Assert.That(session.Clock.TimeScale, Is.EqualTo(SurvivalRecovery.RestTimeScale));
                Assert.That(recovery.RestRemainingSimulationSeconds, Is.EqualTo(8.5d * Hour).Within(1e-9));
                Assert.That(recovery.TryBeginRest(1d).Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "no second rest lease");
                Assert.That(session.Survival.TryConsumeRation().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "no manual portion while resting");
                Assert.That(session.Survival.TryDrinkWater().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(changes, Is.EqualTo(1));

                Require(session.Tick(8.5d));

                Assert.That(recovery.IsResting, Is.False);
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.Completed));
                Assert.That(session.Clock.TimeScale, Is.EqualTo(session.Survival.Spec.TimeScale), "waking restores the Survival time scale");
                Assert.That(session.State.ElapsedSimulationSeconds - start, Is.EqualTo(8.5d * Hour).Within(1e-3), "8.5 real seconds at 3,600");
                Assert.That(session.Survival.Hunger01, Is.EqualTo(hunger).Within(1e-12), "the needs neither accrue nor reset");
                Assert.That(session.Survival.Thirst01, Is.EqualTo(thirst).Within(1e-12));

                ResourceSnapshot after = session.Resources.Snapshot;
                Assert.That(before.FoodKg - after.FoodKg, Is.EqualTo(FoodPerSecond(session) * 8.5d * Hour).Within(1e-9), "BVAD food draw");
                Assert.That(before.WaterLitres - after.WaterLitres, Is.EqualTo(WaterPerSecond(session) * 8.5d * Hour).Within(1e-9), "BVAD water draw");
                Assert.That(after.OxygenKg, Is.LessThan(before.OxygenKg), "oxygen keeps running");
                Assert.That(recovery.RestedSimulationSeconds, Is.EqualTo(8.5d * Hour).Within(1e-3));
                Assert.That(recovery.RestCount, Is.EqualTo(1));
                Assert.That(recovery.MissionDay, Is.EqualTo(session.Calendar.DayNumber));
                Assert.That(changes, Is.EqualTo(2));

                Require(session.AdvanceSimulation(Hour));
                Assert.That(session.Survival.Hunger01, Is.GreaterThan(hunger), "awake again, the needs accrue");
                Assert.That(session.Resources.Snapshot.FoodKg, Is.EqualTo(after.FoodKg), "awake, food is spent only by commands");
            }
        }

        [TestCase(ResourceId.FoodKg, RestStopReason.FoodStoreEmpty)]
        [TestCase(ResourceId.WaterLitres, RestStopReason.WaterStoreEmpty)]
        public void ARest_StopsExactlyWhenAStoreRunsOut_AndTheChapterGoesOn(ResourceId store, RestStopReason expected)
        {
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                double perSecond = store == ResourceId.FoodKg ? FoodPerSecond(session) : WaterPerSecond(session);
                SpendDown(session, store, perSecond * 3d * Hour);

                Require(recovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
                Require(session.AdvanceSimulation(SurvivalRecovery.MaximumRestHours * Hour));

                Assert.That(recovery.LastRestStop, Is.EqualTo(expected));
                Assert.That(recovery.RestedSimulationSeconds, Is.EqualTo(3d * Hour).Within(1e-3), "stopped at the depletion, not at the planned end");
                Assert.That(session.Resources.GetQuantity(store), Is.EqualTo(0d).Within(1e-9));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "an empty store wakes the crew; it is not a loss by itself");
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
                Assert.That(recovery.TryBeginRest(1d).Failure, Is.EqualTo(CommandFailure.InsufficientResources), "no rest on an empty store");
            }
        }

        [Test]
        public void ARest_StopsWhenTheStoredOxygenIsUsedUp_BeforeTheCabinAirRunsOut()
        {
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                double oxygenPerSecond = session.Resources.Rates.OxygenUseKgPerSecond;
                Assert.That(oxygenPerSecond, Is.GreaterThan(0d));
                SpendDown(session, ResourceId.OxygenKg, oxygenPerSecond * 2d * Hour);

                Require(recovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
                Require(session.AdvanceSimulation(2d * Hour));

                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.OxygenStoreEmpty));
                Assert.That(recovery.RestedSimulationSeconds, Is.EqualTo(2d * Hour).Within(1e-3));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "the crew wakes while the cabin air still holds");
                Assert.That(recovery.CheckRest(1d).Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
            }
        }

        [Test]
        public void ADangerWinsATieWithThePlannedEnd()
        {
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                SpendDown(session, ResourceId.FoodKg, FoodPerSecond(session) * 4d * Hour);

                Require(recovery.TryBeginRest(4d));
                Require(session.AdvanceSimulation(4d * Hour));

                Assert.That(recovery.IsResting, Is.False);
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.FoodStoreEmpty));
            }
        }

        [Test]
        public void ChainedRests_CarryTheCrewThroughTheCropCycle_AndTheHarvestWakesThem()
        {
            using (MissionSession session = Contact(extraKit: true))
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                Require(session.Survival.TryBuildGrowModule());
                Require(session.Survival.TryPlant());
                double growth = session.Survival.CropRemainingSimulationSeconds;
                double foodBefore = session.Resources.Snapshot.FoodKg;

                int rests = 0;
                while (session.Survival.GrowStage == GrowModuleStage.Growing && rests < 40)
                {
                    Require(recovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
                    Require(session.AdvanceSimulation(SurvivalRecovery.MaximumRestHours * Hour));
                    rests++;
                }

                Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.HarvestReady));
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.CropReady), "the finished crop wakes the crew");
                Assert.That(recovery.RestedSimulationSeconds, Is.EqualTo(growth).Within(1e-2), "rested exactly until the crop was ready");
                Assert.That(rests, Is.LessThanOrEqualTo(23), "about 23 rests of 8.5 real seconds, not an endless wait");
                Assert.That(foodBefore - session.Resources.Snapshot.FoodKg, Is.EqualTo(FoodPerSecond(session) * growth).Within(1e-6));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
                Require(session.Survival.TryHarvest());
            }
        }

        [Test]
        public void OneLongRest_EqualsManySmallTicks()
        {
            using (MissionSession whole = Contact())
            using (MissionSession pieces = Contact())
            {
                Require(whole.SurvivalRecovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
                Require(pieces.SurvivalRecovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));

                Require(whole.AdvanceSimulation(SurvivalRecovery.MaximumRestHours * Hour));
                for (int i = 0; i < 85; i++)
                {
                    Require(pieces.Tick(0.1d));
                }

                Assert.That(pieces.State.ElapsedSimulationSeconds, Is.EqualTo(whole.State.ElapsedSimulationSeconds).Within(1e-6));
                Fixtures.AssertSnapshot(whole.Resources.Snapshot, pieces.Resources.Snapshot, 1e-9);
                Assert.That(whole.SurvivalRecovery.LastRestStop, Is.EqualTo(RestStopReason.Completed));
                Assert.That(pieces.SurvivalRecovery.LastRestStop, Is.EqualTo(RestStopReason.Completed));
                Assert.That(pieces.SurvivalRecovery.RestedSimulationSeconds, Is.EqualTo(whole.SurvivalRecovery.RestedSimulationSeconds).Within(1e-6));
                Assert.That(pieces.Survival.Hunger01, Is.EqualTo(whole.Survival.Hunger01).Within(1e-9));
            }
        }

        [Test]
        public void PauseFreezesTheRest_WakeWorksOnce_AndNoRestOutlivesTheMission()
        {
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                int changes = 0;
                recovery.Changed += () => changes++;
                Require(recovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
                Require(session.Tick(1d));
                double remaining = recovery.RestRemainingSimulationSeconds;
                Assert.That(remaining, Is.EqualTo(7.5d * Hour).Within(1e-3));

                Require(session.Clock.Pause());
                session.Tick(5d);
                Assert.That(session.AdvanceSimulation(Hour).Succeeded, Is.False);
                Assert.That(recovery.RestRemainingSimulationSeconds, Is.EqualTo(remaining), "paused: the rest is frozen");
                Assert.That(recovery.TryWake().Succeeded, Is.False, "resume before waking");
                Require(session.Clock.Resume());

                Require(recovery.TryWake());
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.Woken));
                Assert.That(session.Clock.TimeScale, Is.EqualTo(session.Survival.Spec.TimeScale));
                int entries = session.Director.Journal.Snapshot().Count;
                Assert.That(recovery.TryWake().Succeeded, Is.False);
                Assert.That(session.Director.Journal.Snapshot().Count, Is.EqualTo(entries), "a second wake records nothing");
                Assert.That(changes, Is.EqualTo(2));

                Require(recovery.TryBeginRest(2d));
                Require(session.State.TryEndMission(MissionOutcome.Lost, "Test ending while the crew rests.", session.Resources.Snapshot));
                Assert.That(recovery.IsResting, Is.False, "an ended mission leaves no rest lease");
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.MissionEnded));
            }

            MissionSession disposed = Contact();
            SurvivalRecovery leftover = disposed.SurvivalRecovery;
            Require(leftover.TryBeginRest(2d));
            disposed.Dispose();
            Assert.That(leftover.IsResting, Is.False, "a disposed session leaves no rest lease");
        }
    }
}
