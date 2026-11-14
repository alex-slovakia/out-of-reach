using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    public sealed class EvaTests
    {
        private const double OxygenKgPerSecond = CrewMetabolicAssumptions.OxygenKgPerCrewDay / CrewMetabolicAssumptions.SecondsPerDay;

        private static ResourceAmount Amount(ResourceId resource, double quantity)
        {
            return new ResourceAmount(resource, quantity);
        }

        private static IInteractable Get(MissionSession session, string interactionId)
        {
            Assert.That(session.Interactions.TryGet(interactionId, out IInteractable interactable), Is.True, interactionId);
            return interactable;
        }

        [Test]
        public void SuitAndAirlockAssumptions_AreDerivedAsDocumented()
        {
            var spec = new EvaSpec();

            Assert.That(CabinAtmosphere.CabinAirDensityKgPerCubicMetre(), Is.EqualTo(1.197d).Within(0.001d));
            Assert.That(spec.SuitPreparationOxygenKg, Is.EqualTo(0.84d).Within(Fixtures.Tolerance), "0.54 kg purge + 0.30 kg tank");
            Assert.That(spec.AirlockCycleOxygenLossKg, Is.EqualTo(0.1108d).Within(0.0005d), "10 % of the air in 4 m3");
            Assert.That(spec.SuitOxygenChargeKg / spec.SuitOxygenUseKgPerSecond, Is.EqualTo(3d * 3600d).Within(1e-6), "three hours of suit oxygen");
        }

        [Test]
        public void SuitUp_NeedsAStableShip_DrawsOxygenOnce_AndTakesThePreparationTime()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                Assert.That(session.Eva.TryPrepareSuit().Failure, Is.EqualTo(CommandFailure.WrongPhase), "not during the emergency");
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Assert.That(session.Eva.TryPrepareSuit().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "life support is still down");
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                Assert.That(session.Eva.IsSuitOn, Is.False);
                double oxygenBefore = session.Resources.Snapshot.OxygenKg;
                double elapsedBefore = session.State.ElapsedSimulationSeconds;
                EvaSpec spec = session.Eva.Spec;

                CommandResult suited = session.Eva.TryPrepareSuit();

                Assert.That(suited.Succeeded, Is.True, suited.Explanation);
                Assert.That(session.Eva.IsSuitOn, Is.True);
                Assert.That(session.Eva.IsSuitSealed, Is.False, "still breathing cabin air");
                Assert.That(session.Eva.SuitOxygenKg, Is.EqualTo(spec.SuitOxygenChargeKg));
                Assert.That(session.State.ElapsedSimulationSeconds - elapsedBefore, Is.EqualTo(spec.SuitPreparationSeconds).Within(1e-6), "2 h 20 min pass");
                double expectedOxygen = oxygenBefore - spec.SuitPreparationOxygenKg - (OxygenKgPerSecond * spec.SuitPreparationSeconds);
                Assert.That(session.Resources.Snapshot.OxygenKg, Is.EqualTo(expectedOxygen).Within(Fixtures.Tolerance));

                double oxygenAfter = session.Resources.Snapshot.OxygenKg;
                Assert.That(session.Eva.TryPrepareSuit().Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(session.Resources.Snapshot.OxygenKg, Is.EqualTo(oxygenAfter), "a second suit-up charges nothing");
            }
        }

        [Test]
        public void SuitUp_WithoutEnoughOxygen_ChangesNothing()
        {
            using (var session = Fixtures.StabilizedSession())
            {
                double oxygen = session.Resources.Snapshot.OxygenKg;
                Assert.That(session.Resources.TryConsume(new[] { Amount(ResourceId.OxygenKg, oxygen - 0.5d) }).Succeeded, Is.True);
                double elapsed = session.State.ElapsedSimulationSeconds;

                CommandResult result = session.Eva.TryPrepareSuit();

                Assert.That(result.Failure, Is.EqualTo(CommandFailure.InsufficientResources));
                Assert.That(session.Eva.IsSuitOn, Is.False);
                Assert.That(session.Resources.Snapshot.OxygenKg, Is.EqualTo(0.5d).Within(Fixtures.Tolerance));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(elapsed));
            }
        }

        [Test]
        public void AirlockCycle_PublishesItsStatesInOrder_AndTheHatchesAreNeverOpenTogether()
        {
            using (var session = Fixtures.StabilizedSession())
            {
                Fixtures.SuitUp(session);
                using (var recorder = new EventRecorder(session.Events))
                {
                    EvaOperations eva = session.Eva;
                    Assert.That(eva.IsInnerHatchPassable && !eva.IsOuterHatchPassable, Is.True, "inner open, outer shut");

                    Assert.That(eva.TryBeginEva().Succeeded, Is.True);
                    Assert.That(recorder.Order, Is.EqualTo(new[] { "ResourceChanged", "AirlockStateChanged", "AirlockStateChanged", "PhaseChanged" }));
                    Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Eva));
                    Assert.That(eva.IsInnerHatchPassable || eva.IsOuterHatchPassable, Is.False, "both shut while depressurizing");

                    session.AdvanceSimulation(599d);
                    Assert.That(eva.Airlock, Is.EqualTo(AirlockState.Depressurizing));
                    Assert.That(eva.AirlockTransitionRemainingSeconds, Is.EqualTo(1d).Within(1e-6));

                    session.AdvanceSimulation(3600d);
                    Assert.That(eva.Airlock, Is.EqualTo(AirlockState.OuterOpen), "a long step still opens the hatch at its time");
                    Assert.That(!eva.IsInnerHatchPassable && eva.IsOuterHatchPassable, Is.True, "outer open, inner shut");

                    Assert.That(eva.TryEndEva().Succeeded, Is.True);
                    Assert.That(eva.IsInnerHatchPassable || eva.IsOuterHatchPassable, Is.False, "both shut while repressurizing");
                    session.AdvanceSimulation(eva.Spec.RepressurizationSeconds);

                    Assert.That(recorder.Airlock, Is.EqualTo(new[]
                    {
                        AirlockState.Sealed, AirlockState.Depressurizing, AirlockState.OuterOpen, AirlockState.Repressurizing, AirlockState.InnerOpen,
                    }));
                    Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Repair), "back inside with the antenna still broken");
                    Assert.That(eva.IsSuitOn, Is.False);
                    Assert.That(eva.CompletedEvaCount, Is.EqualTo(1));
                }
            }
        }

        [Test]
        public void BeginEva_NeedsTheSuit_AndChargesTheAirlockCycleOnce()
        {
            using (var session = Fixtures.StabilizedSession())
            {
                ResourceSnapshot untouched = session.Resources.Snapshot;
                CommandResult unsuited = session.Eva.TryBeginEva();
                Assert.That(unsuited.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(unsuited.Explanation, Does.Contain("suit"));
                Assert.That(session.Eva.Airlock, Is.EqualTo(AirlockState.InnerOpen));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Repair));
                Fixtures.AssertSnapshot(untouched, session.Resources.Snapshot, 0d);

                Fixtures.SuitUp(session);
                ResourceSnapshot before = session.Resources.Snapshot;
                EvaSpec spec = session.Eva.Spec;

                Assert.That(session.Eva.TryBeginEva().Succeeded, Is.True);

                Assert.That(session.Resources.Snapshot.OxygenKg, Is.EqualTo(before.OxygenKg - spec.AirlockCycleOxygenLossKg).Within(Fixtures.Tolerance));
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(before.BatteryKWh - spec.AirlockCycleEnergyKWh).Within(Fixtures.Tolerance));
                ResourceSnapshot charged = session.Resources.Snapshot;
                Assert.That(session.Eva.TryBeginEva().Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Fixtures.AssertSnapshot(charged, session.Resources.Snapshot, 0d);
            }
        }

        [Test]
        public void BeginEva_WithoutPumpEnergy_ChangesNothing()
        {
            using (var session = Fixtures.StabilizedSession())
            {
                Fixtures.SuitUp(session);
                double battery = session.Resources.Snapshot.BatteryKWh;
                Assert.That(session.Resources.TryConsume(new[] { Amount(ResourceId.BatteryKWh, battery - 0.1d) }).Succeeded, Is.True);
                using (var recorder = new EventRecorder(session.Events))
                {
                    CommandResult result = session.Eva.TryBeginEva();

                    Assert.That(result.Failure, Is.EqualTo(CommandFailure.InsufficientResources));
                    Assert.That(session.Eva.Airlock, Is.EqualTo(AirlockState.InnerOpen));
                    Assert.That(session.Eva.IsSuitSealed, Is.False);
                    Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Repair));
                    Assert.That(recorder.Total, Is.EqualTo(0));
                }
            }
        }

        [Test]
        public void SealedSuit_BreathesSuitOxygen_NotTheShipReserve()
        {
            using (var session = Fixtures.StabilizedSession())
            {
                Fixtures.SuitUp(session);
                Assert.That(session.Eva.TryBeginEva().Succeeded, Is.True);
                double shipOxygen = session.Resources.Snapshot.OxygenKg;
                EvaSpec spec = session.Eva.Spec;

                session.AdvanceSimulation(3000d);

                Assert.That(session.Resources.Snapshot.OxygenKg, Is.EqualTo(shipOxygen).Within(1e-12), "nobody breathes cabin oxygen");
                Assert.That(session.Eva.SuitOxygenKg, Is.EqualTo(spec.SuitOxygenChargeKg - (spec.SuitOxygenUseKgPerSecond * 3000d)).Within(Fixtures.Tolerance));
                Assert.That(session.Resources.Snapshot.CarbonDioxidePpm, Is.EqualTo(AuroraMission.InitialCabinCarbonDioxidePpm).Within(Fixtures.Tolerance), "no crew CO2 in the cabin");

                Fixtures.ReturnAndRepressurize(session);
                double afterReturn = session.Resources.Snapshot.OxygenKg;
                session.AdvanceSimulation(3600d);
                Assert.That(session.Resources.Snapshot.OxygenKg, Is.EqualTo(afterReturn - (OxygenKgPerSecond * 3600d)).Within(Fixtures.Tolerance), "cabin breathing resumes");
            }
        }

        [Test]
        public void GoingOutside_NeedsTheOuterHatchOpen_AndRepressurizationIsRefusedWhileOutside()
        {
            using (var session = Fixtures.StabilizedSession())
            {
                EvaOperations eva = session.Eva;
                Fixtures.SuitUp(session);
                Assert.That(eva.TryGoOutside().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "no exit from a pressurized airlock");

                Assert.That(eva.TryBeginEva().Succeeded, Is.True);
                CommandResult early = eva.TryGoOutside();
                Assert.That(early.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(early.Explanation, Does.Contain("outer hatch is closed"));
                Assert.That(eva.TryEndEva().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "nothing to close yet");

                session.AdvanceSimulation(eva.Spec.DepressurizationSeconds);
                Assert.That(eva.TryGoOutside().Succeeded, Is.True);
                Assert.That(eva.IsOutside, Is.True);
                Assert.That(eva.TryGoOutside().Failure, Is.EqualTo(CommandFailure.AlreadyComplete));

                CommandResult lockedOut = eva.TryEndEva();
                Assert.That(lockedOut.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(lockedOut.Explanation, Does.Contain("still outside"));
                Assert.That(eva.Airlock, Is.EqualTo(AirlockState.OuterOpen), "the hatch stays open for the return");

                Assert.That(eva.TryReturnInside().Succeeded, Is.True);
                Assert.That(eva.TryReturnInside().Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(eva.TryEndEva().Succeeded, Is.True);
            }
        }

        [Test]
        public void AntennaRepairedOutside_ThenASafeReturn_EntersTransmission()
        {
            using (var session = Fixtures.StabilizedSession(AuroraMission.ScienceFocusPresetId))
            {
                Fixtures.SuitUp(session);
                Fixtures.OpenOuterHatch(session);
                Fixtures.GoOutside(session);
                Fixtures.RepairOrFail(session, ShipSystemId.Antenna);
                Fixtures.ReturnAndRepressurize(session);

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Transmission));
                Assert.That(session.Systems.Antenna.IsOperational, Is.True);
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(0));
                Assert.That(session.Eva.IsSuitOn, Is.False);
                Assert.That(session.Eva.IsSuitSealed, Is.False);
                Assert.That(session.Eva.SuitOxygenKg, Is.EqualTo(0d));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
                Assert.That(session.Eva.TryPrepareSuit().Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "no second EVA once the antenna works");
            }
        }

        [Test]
        public void AbortedEva_ReturnsToRepair_AndASecondAttemptCostsAgain()
        {
            using (var session = Fixtures.StabilizedSession())
            {
                Fixtures.SuitUp(session);
                Fixtures.OpenOuterHatch(session);
                Fixtures.ReturnAndRepressurize(session);

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Repair));
                Assert.That(session.Eva.IsSuitOn, Is.False);
                Assert.That(session.Eva.CompletedEvaCount, Is.EqualTo(1));
                double oxygen = session.Resources.Snapshot.OxygenKg;

                Fixtures.SuitUp(session);

                Assert.That(oxygen - session.Resources.Snapshot.OxygenKg, Is.GreaterThan(session.Eva.Spec.SuitPreparationOxygenKg), "purge and tank charge are paid again");
                Assert.That(session.Eva.TryBeginEva().Succeeded, Is.True);
            }
        }

        [Test]
        public void InterruptedAntennaRepair_SpendsNothing_AndTheCrewCanStillReturn()
        {
            using (var session = Fixtures.StabilizedSession(AuroraMission.ScienceFocusPresetId))
            {
                Assert.That(session.Resources.TryConsume(new[] { Amount(ResourceId.SparePartsCount, 1d) }).Succeeded, Is.True);
                Fixtures.SuitUp(session);
                Fixtures.OpenOuterHatch(session);
                Fixtures.GoOutside(session);
                ResourceSnapshot before = session.Resources.Snapshot;

                CommandResult repair = Fixtures.Repair(session, ShipSystemId.Antenna);

                Assert.That(repair.Failure, Is.EqualTo(CommandFailure.InsufficientResources));
                Fixtures.AssertSnapshot(before, session.Resources.Snapshot, 0d);
                Assert.That(session.Systems.Antenna.IsOperational, Is.False);

                Fixtures.ReturnAndRepressurize(session);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Repair));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
            }
        }

        [TestCase(true, "outside the ship")]
        [TestCase(false, "in the airlock")]
        public void SealedSuit_RunsOutAtTheExactTime_AndThatEndsTheMission(bool outside, string where)
        {
            using (var session = Fixtures.StabilizedSession())
            {
                Fixtures.SuitUp(session);
                double sealedAt = session.State.ElapsedSimulationSeconds;
                Fixtures.OpenOuterHatch(session);
                if (outside)
                {
                    Fixtures.GoOutside(session);
                }

                session.AdvanceSimulation(10d * 3600d);

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.SuitOxygenDepleted));
                Assert.That(session.State.EndedAtSimulationSeconds, Is.EqualTo(sealedAt + (3d * 3600d)).Within(1e-3), "three hours after the suit was sealed");
                Assert.That(session.State.OutcomeReason, Does.Contain(where));
                Assert.That(session.Eva.SuitOxygenKg, Is.EqualTo(0d));
            }
        }

        [TestCase(9400d, true)]
        [TestCase(9900d, false)]
        public void Repressurization_MustCompleteBeforeTheSuitRunsEmpty(double secondsOutside, bool safe)
        {
            using (var session = Fixtures.StabilizedSession())
            {
                Fixtures.SuitUp(session);
                double sealedAt = session.State.ElapsedSimulationSeconds;
                Fixtures.OpenOuterHatch(session);
                Fixtures.GoOutside(session);
                session.AdvanceSimulation(secondsOutside);

                Assert.That(session.Eva.TryReturnInside().Succeeded, Is.True);
                Assert.That(session.Eva.TryEndEva().Succeeded, Is.True);
                session.AdvanceSimulation(session.Eva.Spec.RepressurizationSeconds);

                if (safe)
                {
                    Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "back in cabin air with 200 s of suit oxygen left");
                    Assert.That(session.Eva.Airlock, Is.EqualTo(AirlockState.InnerOpen));
                    Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Repair));
                }
                else
                {
                    Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                    Assert.That(session.State.EndedAtSimulationSeconds, Is.EqualTo(sealedAt + (3d * 3600d)).Within(1e-3));
                    Assert.That(session.State.OutcomeReason, Does.Contain("before repressurization completed"));
                    Assert.That(session.Eva.Airlock, Is.EqualTo(AirlockState.Repressurizing));
                }
            }
        }

        [Test]
        public void Pause_FreezesTheSuitAndTheAirlock()
        {
            using (var session = Fixtures.StabilizedSession())
            {
                Fixtures.SuitUp(session);
                Assert.That(session.Eva.TryBeginEva().Succeeded, Is.True);
                Assert.That(session.Clock.Pause().Succeeded, Is.True);

                session.Tick(1000d);

                Assert.That(session.AdvanceSimulation(60d).Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(session.Eva.AirlockTransitionRemainingSeconds, Is.EqualTo(session.Eva.Spec.DepressurizationSeconds));
                Assert.That(session.Eva.SuitOxygenKg, Is.EqualTo(session.Eva.Spec.SuitOxygenChargeKg));
                Assert.That(session.Eva.Airlock, Is.EqualTo(AirlockState.Depressurizing));
            }
        }

        [Test]
        public void EvaCommands_AreRejected_OnceTheMissionHasEnded()
        {
            using (var session = Fixtures.StabilizedSession())
            {
                Fixtures.SuitUp(session);
                Fixtures.OpenOuterHatch(session);
                Assert.That(session.Director.TryEndMission(MissionOutcome.Lost, "test").Succeeded, Is.True);

                Assert.That(session.Eva.TryGoOutside().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(session.Eva.TryEndEva().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(session.Eva.TryPrepareSuit().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(session.Eva.IsOutside, Is.False);
                Assert.That(session.Eva.Airlock, Is.EqualTo(AirlockState.OuterOpen));
            }
        }

        [Test]
        public void EvaInteractions_PromptTheRealRequirementAtEveryStep()
        {
            using (var session = Fixtures.StabilizedSession(AuroraMission.ScienceFocusPresetId))
            {
                IInteractable locker = Get(session, InteractionIds.SuitLocker);
                IInteractable controls = Get(session, InteractionIds.AirlockControls);
                IInteractable exit = Get(session, InteractionIds.EvaExit);
                IInteractable reentry = Get(session, InteractionIds.EvaReturn);
                IInteractable antenna = Get(session, InteractionIds.RepairAntenna);

                Assert.That(locker.Prompt, Is.EqualTo("Put on EVA suit: 0.84 kg oxygen, 2 h 20 min of preparation."));
                Assert.That(controls.Prompt, Is.EqualTo("EVA suit required before depressurizing."));
                Assert.That(controls.CanInteract, Is.False);
                Assert.That(exit.CanInteract, Is.False);
                Assert.That(antenna.Prompt, Does.Contain("EVA"));

                Assert.That(locker.TryInteract().Succeeded, Is.True);
                Assert.That(locker.Prompt, Is.EqualTo("EVA suit on: 0.30 kg oxygen in the tank"));
                Assert.That(locker.CanInteract, Is.False);
                Assert.That(controls.Prompt, Is.EqualTo("Seal inner hatch and depressurize: 0.11 kg oxygen, 0.2 kWh."));

                Assert.That(controls.TryInteract().Succeeded, Is.True);
                Assert.That(controls.Prompt, Is.EqualTo("Depressurizing: 10 min left"));
                Assert.That(controls.CanInteract, Is.False);
                Assert.That(exit.Prompt, Is.EqualTo("The outer hatch is closed."));

                session.AdvanceSimulation(session.Eva.Spec.DepressurizationSeconds);
                Assert.That(controls.Prompt, Is.EqualTo("Close outer hatch and repressurize."));
                Assert.That(exit.Prompt, Is.EqualTo("Clip tether and go outside."));
                Assert.That(antenna.Prompt, Is.EqualTo("Go outside to reach the high-gain antenna."));

                Assert.That(exit.TryInteract().Succeeded, Is.True);
                Assert.That(antenna.Prompt, Is.EqualTo("Repair high-gain antenna: 3 spare parts, 0.5 kWh."));
                Assert.That(controls.Prompt, Is.EqualTo("You are still outside: enter the airlock first."));
                Assert.That(controls.CanInteract, Is.False);
                Assert.That(reentry.Prompt, Is.EqualTo("Enter the airlock."));

                Assert.That(antenna.TryInteract().Succeeded, Is.True);
                Assert.That(reentry.TryInteract().Succeeded, Is.True);
                Assert.That(controls.TryInteract().Succeeded, Is.True);
                Assert.That(controls.Prompt, Is.EqualTo("Repressurizing: 10 min left"));

                session.AdvanceSimulation(session.Eva.Spec.RepressurizationSeconds);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Transmission));
                Assert.That(locker.Prompt, Is.EqualTo("No EVA is needed: the antenna is operational."));
            }
        }
    }
}
