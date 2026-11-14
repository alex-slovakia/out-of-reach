using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>
    /// C06-ACCEPTANCE-AUDIT (root 6885878756): the post-contact survival chapter played in real frames at the clock's
    /// own scales (300 awake, 3,600 resting), by a scripted crew pressing E on the registered interactions, the same
    /// path the resolver uses. Every command must change the ledger by exactly its cost, a second press in the same
    /// frame must change nothing, the continuous draws must reconcile with the rests actually taken, simulated time
    /// must keep moving until a terminal outcome, and neither food nor rescue may be instant.
    /// </summary>
    public sealed class C06AcceptanceTests
    {
        private const double Hour = 3600d;
        private const double Day = CrewMetabolicAssumptions.SecondsPerDay;
        private const double FrameRealSeconds = 0.1d;
        private const double AwakeHoursPerDay = 15.5d;
        private const double Tolerance = 1e-6;

        private static void Require(CommandResult result)
        {
            Assert.That(result.Succeeded, Is.True, result.Explanation);
        }

        private static IInteractable Get(MissionSession session, string interactionId)
        {
            Assert.That(session.Interactions.TryGet(interactionId, out IInteractable interactable), Is.True, interactionId);
            return interactable;
        }

        /// <summary>
        /// The default loadout (optionally with three more spare parts, the only way to keep one for the grow module
        /// after the repairs) with the survival extension on, through Earth's reply into the Survival phase.
        /// </summary>
        private static MissionSession Contact(bool extraKit = false)
        {
            var session = new MissionSession(survivalExtension: true);
            if (extraKit)
            {
                Require(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 3));
            }

            Fixtures.Stabilize(session);
            Fixtures.CompleteEva(session);
            Fixtures.Transmit(session);
            Require(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds));
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
            return session;
        }

        /// <summary>
        /// One E on an available target: it must succeed and change the ledger by exactly the given amounts. A second
        /// E in the same frame (and E on <paramref name="alsoOffering"/>, another target that offers the same action)
        /// must change nothing. Returns 1 when it pressed, 0 when the target was not available.
        /// </summary>
        private static int Press(MissionSession session, IInteractable target, double food = 0d, double water = 0d,
            double battery = 0d, int parts = 0, IInteractable alsoOffering = null)
        {
            if (!target.CanInteract)
            {
                return 0;
            }

            ResourceSnapshot before = session.Resources.Snapshot;
            CommandResult result = target.TryInteract();
            Assert.That(result.Succeeded, Is.True, target.InteractionId + ": " + result.Explanation);
            ResourceSnapshot after = session.Resources.Snapshot;
            Assert.That(after.FoodKg - before.FoodKg, Is.EqualTo(food).Within(Tolerance), target.InteractionId + " food");
            Assert.That(after.WaterLitres - before.WaterLitres, Is.EqualTo(water).Within(Tolerance), target.InteractionId + " water");
            Assert.That(after.BatteryKWh - before.BatteryKWh, Is.EqualTo(battery).Within(Tolerance), target.InteractionId + " battery");
            Assert.That(after.SparePartsCount - before.SparePartsCount, Is.EqualTo(parts), target.InteractionId + " parts");

            Assert.That(target.TryInteract().Succeeded, Is.False, target.InteractionId + " pressed twice in one frame");
            if (alsoOffering != null)
            {
                alsoOffering.TryInteract();
            }

            Fixtures.AssertSnapshot(after, session.Resources.Snapshot, 0d);
            return 1;
        }

        private sealed class Run
        {
            public int Rations;
            public int Drinks;
            public int Builds;
            public int Plants;
            public int Harvests;
            public int Beacons;
            public double PlantedAt = double.NaN;
            public double HarvestedAt = double.NaN;
            public ResourceSnapshot Start;
            public double ContactAt;
        }

        /// <summary>
        /// Plays the chapter frame by frame until an outcome: build, plant, harvest, power the beacon (if
        /// <paramref name="powerBeacon"/>), eat and drink whenever offered, and rest after a realistic waking day.
        /// </summary>
        private static Run Play(MissionSession session, bool powerBeacon)
        {
            SurvivalExtension survival = session.Survival;
            SurvivalRecovery recovery = session.SurvivalRecovery;
            IInteractable ration = Get(session, InteractionIds.ConsumeRation);
            IInteractable drink = Get(session, InteractionIds.DrinkWater);
            IInteractable build = Get(session, InteractionIds.BuildGrowModule);
            IInteractable plant = Get(session, InteractionIds.PlantSeedlings);
            IInteractable harvest = Get(session, InteractionIds.HarvestCrop);
            IInteractable sleep = Get(session, InteractionIds.SurvivalSleep);
            IInteractable beacon = Get(session, InteractionIds.RecoveryBeacon);
            IInteractable console = Get(session, InteractionIds.CockpitConsole);
            var run = new Run
            {
                Start = session.Resources.Snapshot,
                ContactAt = recovery.ContactAtSimulationSeconds.Value,
            };

            double awakeSince = session.State.ElapsedSimulationSeconds;
            double previous = awakeSince;
            bool wasResting = false;
            for (int frame = 0; session.State.IsSimulationRunning; frame++)
            {
                Assert.That(frame, Is.LessThan(60000), "the chapter reaches an outcome");
                double now = session.State.ElapsedSimulationSeconds;
                if (!recovery.IsResting)
                {
                    if (wasResting)
                    {
                        awakeSince = now;
                    }

                    run.Builds += Press(session, build, battery: -survival.Spec.BuildEnergyKWh, parts: -survival.Spec.BuildSpareParts);
                    if (Press(session, plant, water: -survival.Spec.PlantingWaterLitres) == 1)
                    {
                        run.Plants++;
                        run.PlantedAt = now;
                    }

                    if (Press(session, harvest, food: survival.Spec.HarvestFoodEquivalentKg) == 1)
                    {
                        run.Harvests++;
                        run.HarvestedAt = now;
                    }

                    if (powerBeacon)
                    {
                        run.Beacons += Press(session, beacon, battery: -SurvivalRecovery.BeaconEnergyKWh, alsoOffering: console);
                    }

                    run.Drinks += Press(session, drink, water: -survival.DrinkWaterLitres);
                    run.Rations += Press(session, ration, food: -survival.RationFoodKg);
                    if (now - awakeSince >= AwakeHoursPerDay * Hour && recovery.Stage != RecoveryStage.BeaconRequested && sleep.CanInteract)
                    {
                        Require(sleep.TryInteract());
                        Assert.That(recovery.IsResting, Is.True);
                    }
                }

                wasResting = recovery.IsResting;
                Assert.That(session.Clock.TimeScale,
                    Is.EqualTo(recovery.IsResting ? SurvivalRecovery.RestTimeScale : survival.Spec.TimeScale), "no stuck clock rate");
                Require(session.Tick(FrameRealSeconds));
                double after = session.State.ElapsedSimulationSeconds;
                Assert.That(after, Is.GreaterThan(previous), "simulated time keeps moving until an outcome: no softlock");
                previous = after;
            }

            return run;
        }

        /// <summary>Every continuous draw and every command must add up to the ledger, so nothing was charged twice.</summary>
        private static void AssertLedgerReconciles(MissionSession session, Run run)
        {
            SurvivalExtension survival = session.Survival;
            SurvivalRecovery recovery = session.SurvivalRecovery;
            int crew = session.Definition.CrewCount;
            double rested = recovery.RestedSimulationSeconds;
            double restFood = CrewMetabolicAssumptions.FoodKgPerCrewDay * crew * rested / Day;
            double restWater = CrewMetabolicAssumptions.WaterLitresPerCrewDay * crew * rested / Day;
            double growing = run.Harvests == 1 ? survival.Spec.GrowthSeconds : 0d;
            double irrigation = survival.Spec.GrowWaterLitresPerDay * growing / Day;
            ResourceSnapshot end = session.Resources.Snapshot;

            Assert.That(end.FoodKg, Is.EqualTo(run.Start.FoodKg - (run.Rations * survival.RationFoodKg) - restFood
                + (run.Harvests * survival.Spec.HarvestFoodEquivalentKg)).Within(Tolerance),
                "food = rations + rests at the BVAD rate - harvest; awake time draws nothing else");
            Assert.That(end.WaterLitres, Is.EqualTo(run.Start.WaterLitres - (run.Drinks * survival.DrinkWaterLitres) - restWater
                - (run.Plants * survival.Spec.PlantingWaterLitres) - irrigation).Within(Tolerance),
                "water = drinks + rests at the BVAD rate + planting + irrigation for the growing days only");
            Assert.That(end.SparePartsCount, Is.EqualTo(run.Start.SparePartsCount - (run.Builds * survival.Spec.BuildSpareParts)));
        }

        [TestCase(true, TestName = "AFullChapterInRealFrames_WithASparePartForTheGrowModule")]
        [TestCase(false, TestName = "AFullChapterInRealFrames_DefaultLoadout_NoPartLeftForTheGrowModule")]
        public void AFullChapterInRealFrames_ChargesEveryCommandOnce_ReconcilesTheLedger_AndArrivesOnSchedule(bool extraKit)
        {
            using (MissionSession session = Contact(extraKit))
            {
                IInteractable build = Get(session, InteractionIds.BuildGrowModule);
                if (!extraKit)
                {
                    // The repairs use the default loadout's spare parts: the crop is an honest, explained refusal.
                    Assert.That(session.Resources.Snapshot.SparePartsCount, Is.Zero);
                    Assert.That(build.CanInteract, Is.False);
                    Assert.That(build.Prompt, Does.Contain("spare part"));
                }

                Run run = Play(session, powerBeacon: true);
                SurvivalRecovery recovery = session.SurvivalRecovery;

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Recovered), session.State.OutcomeReason);
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.None));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(run.ContactAt + (12d * Day)).Within(1e-3),
                    "the rendezvous is 12 simulated days after contact, never sooner");
                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.Arrived));
                Assert.That(recovery.BeaconMissed, Is.False);
                Assert.That(run.Beacons, Is.EqualTo(1), "the beacon is powered once");
                int crop = extraKit ? 1 : 0;
                Assert.That(run.Builds, Is.EqualTo(crop));
                Assert.That(run.Plants, Is.EqualTo(crop));
                Assert.That(run.Harvests, Is.EqualTo(crop));
                if (extraKit)
                {
                    Assert.That(run.HarvestedAt - run.PlantedAt, Is.GreaterThanOrEqualTo(session.Survival.Spec.GrowthSeconds - Tolerance),
                        "no instant food: the crop takes its growing days");
                    Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.Harvested));
                }
                Assert.That(run.Rations, Is.GreaterThanOrEqualTo(20), "the awake portions were really exercised");
                Assert.That(run.Drinks, Is.GreaterThanOrEqualTo(20));
                Assert.That(recovery.RestCount, Is.GreaterThanOrEqualTo(10));
                AssertLedgerReconciles(session, run);
            }
        }

        [Test]
        public void AMissedBeaconInRealFrames_SlipsTheRendezvousOnce_AndTheLedgerStillReconciles()
        {
            using (MissionSession session = Contact(extraKit: true))
            {
                Run run = Play(session, powerBeacon: false);
                SurvivalRecovery recovery = session.SurvivalRecovery;

                Assert.That(recovery.BeaconMissed, Is.True);
                Assert.That(run.Beacons, Is.Zero);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Recovered), session.State.OutcomeReason);
                Assert.That(session.State.ElapsedSimulationSeconds,
                    Is.EqualTo(run.ContactAt + ((12d + SurvivalRecovery.MissedBeaconDelayDays) * Day)).Within(1e-3), "one delay of 3 days");
                AssertLedgerReconciles(session, run);
            }
        }

        [Test]
        public void TheBeaconRequestWakesTheCrew_AndNoRestCanSleepThroughTheOpenWindow()
        {
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                IInteractable sleep = Get(session, InteractionIds.SurvivalSleep);
                for (int frame = 0; recovery.Stage == RecoveryStage.Inbound; frame++)
                {
                    Assert.That(frame, Is.LessThan(20000), "the request comes on day 6");
                    if (!recovery.IsResting)
                    {
                        Require(sleep.TryInteract());
                    }

                    Require(session.Tick(FrameRealSeconds));
                }

                Assert.That(recovery.Stage, Is.EqualTo(RecoveryStage.BeaconRequested));
                Assert.That(recovery.LastRestStop, Is.EqualTo(RestStopReason.RecoveryMilestone), "the request wakes the crew");

                // At 3,600 the 8 h window would pass in 8 real seconds: one more E on the bed must not sleep through it.
                Assert.That(sleep.CanInteract, Is.False, "no rest while the recovery craft waits for the beacon");
                Assert.That(sleep.Prompt, Does.Contain("beacon"));
                Assert.That(sleep.TryInteract().Succeeded, Is.False);
                Assert.That(recovery.IsResting, Is.False);
                Assert.That(recovery.TryBeginRest(1d).Succeeded, Is.False, "no shorter rest either");

                Require(Get(session, InteractionIds.RecoveryBeacon).TryInteract());
                Assert.That(sleep.CanInteract, Is.True, "once the beacon is powered the crew may rest again");
            }
        }

        [Test]
        public void AnUnpoweredWindowStillPasses_AwakeInAboutNinetySixRealSeconds_ThenRestIsOfferedAgain()
        {
            using (MissionSession session = Contact())
            {
                SurvivalRecovery recovery = session.SurvivalRecovery;
                IInteractable sleep = Get(session, InteractionIds.SurvivalSleep);
                for (int frame = 0; recovery.Stage == RecoveryStage.Inbound; frame++)
                {
                    Assert.That(frame, Is.LessThan(20000));
                    if (!recovery.IsResting)
                    {
                        Require(sleep.TryInteract());
                    }

                    Require(session.Tick(FrameRealSeconds));
                }

                int frames = 0;
                for (; recovery.Stage == RecoveryStage.BeaconRequested; frames++)
                {
                    Assert.That(frames, Is.LessThan(2000));
                    Assert.That(sleep.CanInteract, Is.False);
                    Require(session.Tick(FrameRealSeconds));
                }

                Assert.That(frames * FrameRealSeconds, Is.EqualTo(SurvivalRecovery.BeaconWindowHours * Hour / session.Survival.Spec.TimeScale).Within(0.2d),
                    "the choice not to power it costs the awake window, about 96 real seconds, then the one delay");
                Assert.That(recovery.BeaconMissed, Is.True);
                Assert.That(sleep.CanInteract, Is.True);
            }
        }

        [Test]
        public void AnUnattendedCrew_IsLostAtTheThirstLimit_AndTheGameEndsInsteadOfHanging()
        {
            using (MissionSession session = Contact())
            {
                double contact = session.SurvivalRecovery.ContactAtSimulationSeconds.Value;
                double previous = contact;
                for (int frame = 0; session.State.IsSimulationRunning; frame++)
                {
                    Assert.That(frame, Is.LessThan(5000), "the unattended chapter ends");
                    Require(session.Tick(FrameRealSeconds));
                    Assert.That(session.State.ElapsedSimulationSeconds, Is.GreaterThan(previous));
                    previous = session.State.ElapsedSimulationSeconds;
                }

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.ThirstLimit));
                Assert.That(session.State.ElapsedSimulationSeconds - contact,
                    Is.EqualTo(session.Survival.Spec.ThirstLimitSeconds).Within(1e-3), "exactly at the limit, not a frame later");
            }
        }
    }
}
