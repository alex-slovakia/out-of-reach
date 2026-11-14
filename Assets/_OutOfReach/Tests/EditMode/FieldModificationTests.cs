using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>Payload salvage and the CO2 absorber cartridge: conversions of what was carried, nothing created.</summary>
    public sealed class FieldModificationTests
    {
        private const double TwelveHours = 43200d;

        private static IInteractable Get(MissionSession session, string interactionId)
        {
            Assert.That(session.Interactions.TryGet(interactionId, out IInteractable interactable), Is.True, interactionId);
            return interactable;
        }

        private static void Require(CommandResult result)
        {
            Assert.That(result.Succeeded, Is.True, result.Explanation);
        }

        private static ResourceAmount[] Cost(ResourceId resource, double quantity)
        {
            return new[] { new ResourceAmount(resource, quantity) };
        }

        private static MissionSession Emergency(string presetId, bool salvage, bool absorber)
        {
            MissionSession session = Fixtures.EmergencySession(presetId);
            session.EnableFieldModifications(salvage, absorber);
            return session;
        }

        private static void BuildAndInstall(MissionSession session)
        {
            Require(session.FieldModifications.TryBuildCartridge());
            Require(session.FieldModifications.TryInstallCartridge());
        }

        private static double SecondsToCarbonDioxideLimit()
        {
            return (SurvivalRules.CarbonDioxideLossLimitPpm - AuroraMission.InitialCabinCarbonDioxidePpm) / CabinAtmosphere.CrewCarbonDioxidePpmPerSecond(1);
        }

        [Test]
        public void ByDefault_NothingIsOffered_SoAShipWithoutTheTargetsPlaysAsBefore()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                FieldModifications mods = session.FieldModifications;
                ResourceSnapshot before = session.Resources.Snapshot;

                Assert.That(mods.TrySalvagePayload().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(mods.TryBuildCartridge().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(mods.TryInstallCartridge().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(mods.SalvageablePartsNow, Is.EqualTo(0));
                Fixtures.AssertSnapshot(before, session.Resources.Snapshot, 0d);

                IFieldModifications view = mods;
                Assert.That(view.SciencePayloadSalvaged, Is.False);
                Assert.That(view.AbsorberCartridgesCarried, Is.EqualTo(0));
                Assert.That(view.AbsorberInstalled, Is.False);
                Assert.That(view.AbsorberRemainingSimulationSeconds, Is.EqualTo(0d));
            }
        }

        [Test]
        public void Salvage_GivesPartsOnce_RemovesTheLoadOnce_AndAbandonsScience()
        {
            using (var session = Emergency(AuroraMission.ScienceFocusPresetId, true, false))
            using (var recorder = new EventRecorder(session.Events))
            {
                FieldModifications mods = session.FieldModifications;
                IInteractable rack = Get(session, InteractionIds.SalvagePayload);
                int changes = 0;
                mods.Changed += () => changes++;
                double battery = session.Resources.Snapshot.BatteryKWh;
                Assert.That(session.Director.PowerState.LoadWatts, Is.EqualTo(1100d).Within(1e-9), "1,400 W minus the failed life support's 300 W");
                Assert.That(mods.SalvageablePartsNow, Is.EqualTo(3));
                Assert.That(rack.CanInteract, Is.True);
                Assert.That(rack.Prompt, Is.EqualTo("Dismantle science instrument: +3 spare parts for 0.2 kWh. The science objective is abandoned for good."));

                CommandResult salvaged = rack.TryInteract();

                Assert.That(salvaged.Succeeded, Is.True, salvaged.Explanation);
                Assert.That(salvaged.Explanation, Is.EqualTo("Science instrument dismantled: +3 spare parts. Its 300 W load is off the bus."));
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(10));
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(battery - 0.2d).Within(1e-9));
                Assert.That(recorder.Resources, Has.Count.EqualTo(1), "one atomic exchange, one event");
                Assert.That(mods.SciencePayloadSalvaged, Is.True);
                Assert.That(mods.RemovedLoadWatts, Is.EqualTo(300d));
                Assert.That(session.Director.PowerState.LoadWatts, Is.EqualTo(800d).Within(1e-9));
                Assert.That(session.Resources.Rates.NetLoadWatts, Is.EqualTo(800d - 380d).Within(1e-9), "the battery now drains slower");
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(mods.SalvageablePartsNow, Is.EqualTo(0));

                CommandResult again = rack.TryInteract();

                Assert.That(again.Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(rack.Prompt, Is.EqualTo("Science instrument dismantled"));
                Assert.That(rack.CanInteract, Is.False);
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(10), "parts once");
                Assert.That(session.Director.PowerState.LoadWatts, Is.EqualTo(800d).Within(1e-9), "300 W removed exactly once");
                Assert.That(recorder.Resources, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void Salvage_IsRefused_WithoutAPayload_OutsideTheShip_OrWithoutEnergy()
        {
            using (var noPayload = Emergency(AuroraMission.RedundancyPresetId, true, false))
            {
                CommandResult result = noPayload.FieldModifications.TrySalvagePayload();
                Assert.That(result.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(result.Explanation, Is.EqualTo("No science payload aboard"));
                Assert.That(noPayload.FieldModifications.SalvageablePartsNow, Is.EqualTo(0));
            }

            using (var beforeImpact = Fixtures.LaunchedSession(AuroraMission.ScienceFocusPresetId))
            {
                beforeImpact.EnableFieldModifications(true, true);
                Assert.That(beforeImpact.FieldModifications.TrySalvagePayload().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(beforeImpact.FieldModifications.TryBuildCartridge().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(Get(beforeImpact, InteractionIds.Workbench).Prompt, Is.EqualTo("Not needed before the emergency."));
            }

            using (var outside = Fixtures.StabilizedSession(AuroraMission.ScienceFocusPresetId))
            {
                outside.EnableFieldModifications(true, true);
                Fixtures.SuitUp(outside);
                Fixtures.OpenOuterHatch(outside);
                CommandResult result = outside.FieldModifications.TrySalvagePayload();
                Assert.That(result.Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(result.Explanation, Is.EqualTo("Not possible during an EVA."));
                Assert.That(outside.FieldModifications.TryBuildCartridge().Failure, Is.EqualTo(CommandFailure.WrongPhase));
            }

            using (var flat = Emergency(AuroraMission.ScienceFocusPresetId, true, false))
            {
                Require(flat.Resources.TryConsume(Cost(ResourceId.BatteryKWh, flat.Resources.Snapshot.BatteryKWh - 0.1d)));
                ResourceSnapshot before = flat.Resources.Snapshot;

                CommandResult result = flat.FieldModifications.TrySalvagePayload();

                Assert.That(result.Failure, Is.EqualTo(CommandFailure.InsufficientResources));
                Assert.That(flat.FieldModifications.SciencePayloadSalvaged, Is.False);
                Fixtures.AssertSnapshot(before, flat.Resources.Snapshot, 0d);
                Assert.That(flat.Director.PowerState.LoadWatts, Is.EqualTo(1100d).Within(1e-9));
            }
        }

        [Test]
        public void WithSalvageAvailable_AStarvedShipIsNotLost_AndCanTradeItsScienceForContact()
        {
            using (var session = Fixtures.NewSession())
            {
                Require(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 1));
                session.EnableFieldModifications(true, false);
                Require(session.Planner.TryLaunch());
                Fixtures.TriggerImpact(session);
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);

                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(0));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "the instrument can still be dismantled");
                Assert.That(session.AdvanceSimulation(3600d).Succeeded, Is.True);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));

                Require(session.FieldModifications.TrySalvagePayload());
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(3));
                Fixtures.CompleteEva(session);
                Fixtures.Transmit(session);
                Assert.That(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds).Succeeded, Is.True);

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible));
                MissionReport report = session.Report;
                Assert.That(report.SciencePayloadSalvaged, Is.True);
                Assert.That(report.ScienceDataReturned, Is.False, "contact was bought with the science");
                Assert.That(report.LoadoutName, Is.EqualTo("custom"));
                Assert.That(report.TryGetSection(MissionReportSections.Science, out MissionReportSection science), Is.True);
                Assert.That(science.Lines[science.Lines.Count - 1].Value,
                    Does.StartWith("Abandoned: the instrument was dismantled for spare parts at T+1 h 00 min."));
            }
        }

        [Test]
        public void OnceTheInstrumentIsGone_TheUnrecoverableRuleAppliesAgain_AndTheBenchWarnsFirst()
        {
            using (var session = Fixtures.StabilizedSession(AuroraMission.ScienceFocusPresetId))
            {
                session.EnableFieldModifications(true, true);
                FieldModifications mods = session.FieldModifications;
                IInteractable bench = Get(session, InteractionIds.Workbench);
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(3));
                Assert.That(bench.Prompt, Is.EqualTo("Build CO2 absorber cartridge: 1 spare part, 0.2 kWh."), "salvage still covers the antenna");

                Require(bench.TryInteract());
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None), "2 parts in hand, 3 more in the instrument");

                Require(mods.TrySalvagePayload());
                Require(mods.TryBuildCartridge());
                Require(mods.TryBuildCartridge());
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(3));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
                Assert.That(bench.Prompt, Is.EqualTo("Build CO2 absorber cartridge: 1 spare part, 0.2 kWh. Warning: that leaves 2 spare parts; the antenna needs 3."));

                CommandResult oneTooMany = bench.TryInteract();

                Assert.That(oneTooMany.Succeeded, Is.True, "the choice is the player's");
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.ContactImpossible));
                Assert.That(session.State.OutcomeReason, Does.Contain("needs 3 spare parts and only 2 remain,"));
            }
        }

        [Test]
        public void ACartridge_IsBuiltFromAPartAndEnergy_AndCarriedUntilInstalled()
        {
            using (var session = Fixtures.StabilizedSession(AuroraMission.RedundancyPresetId))
            {
                session.EnableFieldModifications(false, true);
                FieldModifications mods = session.FieldModifications;
                IFieldModifications view = mods;
                IInteractable bench = Get(session, InteractionIds.Workbench);
                IInteractable bay = Get(session, InteractionIds.AbsorberBay);
                int changes = 0;
                view.Changed += () => changes++;
                ResourceSnapshot before = session.Resources.Snapshot;
                Assert.That(before.SparePartsCount, Is.EqualTo(9));
                Assert.That(bay.CanInteract, Is.False);
                Assert.That(bay.Prompt, Is.EqualTo("Empty absorber bay. Build a cartridge at the workbench."));
                Assert.That(bay.TryInteract().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));

                Require(bench.TryInteract());

                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(8));
                Assert.That(session.Resources.Snapshot.BatteryKWh, Is.EqualTo(before.BatteryKWh - 0.2d).Within(1e-9));
                Assert.That(view.AbsorberCartridgesCarried, Is.EqualTo(1));
                Assert.That(view.AbsorberInstalled, Is.False);
                Assert.That(bay.Prompt, Is.EqualTo("Install CO2 absorber cartridge: cabin CO2 stops rising for up to 12 h 00 min"));

                Require(bay.TryInteract());

                Assert.That(view.AbsorberCartridgesCarried, Is.EqualTo(0));
                Assert.That(view.AbsorberInstalled, Is.True);
                Assert.That(view.AbsorberRemainingSimulationSeconds, Is.EqualTo(TwelveHours).Within(1e-6));
                Assert.That(changes, Is.EqualTo(2));
                Assert.That(bay.CanInteract, Is.False);
                Assert.That(bay.Prompt, Is.EqualTo("CO2 absorber working: 12 h 00 min of capacity left"));
                Assert.That(bay.TryInteract().Failure, Is.EqualTo(CommandFailure.AlreadyComplete));

                Require(session.Resources.TryConsume(Cost(ResourceId.SparePartsCount, 8d)));
                ResourceSnapshot empty = session.Resources.Snapshot;
                Assert.That(bench.TryInteract().Failure, Is.EqualTo(CommandFailure.InsufficientResources));
                Fixtures.AssertSnapshot(empty, session.Resources.Snapshot, 0d);
                Assert.That(mods.CartridgesBuilt, Is.EqualTo(1));
            }
        }

        [Test]
        public void AnInstalledCartridge_HoldsCabinCo2_ForExactlyItsCapacity_WithoutPowerOrScrubbers()
        {
            using (var session = Emergency(AuroraMission.RedundancyPresetId, false, true))
            {
                FieldModifications mods = session.FieldModifications;
                BuildAndInstall(session);
                ResourceSnapshot start = session.Resources.Snapshot;
                double crewRate = CabinAtmosphere.CrewCarbonDioxidePpmPerSecond(1);
                Assert.That(session.Systems.LifeSupport.IsOperational, Is.False);
                Assert.That(mods.IsAbsorbing, Is.True);
                Assert.That(session.Resources.Rates.CarbonDioxideChangePpmPerSecond, Is.EqualTo(0d).Within(1e-12), "CO2 stops rising");

                Assert.That(session.AdvanceSimulation(6d * 3600d).Succeeded, Is.True);

                Assert.That(session.Resources.Snapshot.CarbonDioxidePpm, Is.EqualTo(start.CarbonDioxidePpm).Within(1e-6));
                Assert.That(mods.AbsorberRemainingSimulationSeconds, Is.EqualTo(6d * 3600d).Within(1e-3));
                Assert.That(start.OxygenKg - session.Resources.Snapshot.OxygenKg, Is.EqualTo((0.84d + 2d) * 6d / 24d).Within(1e-9),
                    "breathing and the leak go on: the cartridge is not a life-support repair");
                Assert.That(session.Systems.LifeSupport.IsOperational, Is.False);

                Assert.That(session.AdvanceSimulation(10d * 3600d).Succeeded, Is.True, "one jump across the end of the cartridge");

                Assert.That(mods.AbsorberInstalled, Is.False);
                Assert.That(mods.AbsorbedCarbonDioxideKg, Is.EqualTo(0.505d).Within(1e-9), "12 crew-hours at 1.01 kg per crew-day");
                Assert.That(session.Resources.Snapshot.CarbonDioxidePpm, Is.EqualTo(start.CarbonDioxidePpm + (crewRate * 4d * 3600d)).Within(1e-3),
                    "rising again for exactly the four hours after it was spent");

                IReadOnlyList<MissionLogEntry> timeline = session.Director.Journal.Snapshot();
                MissionLogEntry spent = default(MissionLogEntry);
                foreach (MissionLogEntry entry in timeline)
                {
                    if (entry.Text == "CO2 absorber cartridge spent.")
                    {
                        spent = entry;
                    }
                }

                Assert.That(spent.AtSimulationSeconds, Is.EqualTo(TwelveHours).Within(1e-3));
            }
        }

        [Test]
        public void TheCo2Loss_ComesExactlyOneCartridgeLater()
        {
            using (var session = Emergency(AuroraMission.RedundancyPresetId, false, true))
            {
                BuildAndInstall(session);

                Assert.That(session.AdvanceSimulation(100d * 3600d).Succeeded, Is.True);

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.CarbonDioxideLimit));
                Assert.That(session.State.EndedAtSimulationSeconds, Is.EqualTo(SecondsToCarbonDioxideLimit() + TwelveHours).Within(1e-3));
                Assert.That(session.State.OutcomeReason, Does.Contain("Life support was not repaired"));
            }
        }

        [Test]
        public void TheCartridge_IsNotUsedUp_WhileTheScrubbersCope()
        {
            using (var session = Fixtures.StabilizedSession(AuroraMission.RedundancyPresetId))
            {
                session.EnableFieldModifications(false, true);
                BuildAndInstall(session);

                Assert.That(session.FieldModifications.IsAbsorbing, Is.False);
                Assert.That(session.AdvanceSimulation(5d * 3600d).Succeeded, Is.True);

                Assert.That(session.FieldModifications.AbsorberRemainingSimulationSeconds, Is.EqualTo(TwelveHours).Within(1e-6));
                Assert.That(session.FieldModifications.AbsorbedCarbonDioxideKg, Is.EqualTo(0d));
                Assert.That(session.FieldModifications.AbsorberInstalled, Is.True);
            }
        }

        [Test]
        public void ASpentCartridge_CanBeReplaced_ButAWorkingOneIsLeftAlone()
        {
            using (var session = Emergency(AuroraMission.RedundancyPresetId, false, true))
            {
                FieldModifications mods = session.FieldModifications;
                IInteractable bay = Get(session, InteractionIds.AbsorberBay);
                Require(mods.TryBuildCartridge());
                Require(mods.TryBuildCartridge());
                Require(mods.TryInstallCartridge());
                Assert.That(mods.AbsorberCartridgesCarried, Is.EqualTo(1));
                Assert.That(mods.TryInstallCartridge().Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "no waste of a working cartridge");

                Assert.That(session.AdvanceSimulation(TwelveHours).Succeeded, Is.True);
                Assert.That(mods.AbsorberInstalled, Is.False);
                Assert.That(bay.CanInteract, Is.True);

                Require(bay.TryInteract());

                Assert.That(mods.AbsorberCartridgesCarried, Is.EqualTo(0));
                Assert.That(mods.CartridgesInstalled, Is.EqualTo(2));
                Assert.That(mods.AbsorberRemainingSimulationSeconds, Is.EqualTo(TwelveHours).Within(1e-6));

                Assert.That(session.AdvanceSimulation(TwelveHours).Succeeded, Is.True);
                Assert.That(bay.Prompt, Is.EqualTo("Cartridge spent. Build a new one at the workbench."));
                Assert.That(mods.AbsorbedCarbonDioxideKg, Is.EqualTo(1.01d).Within(1e-9));
            }
        }

        [Test]
        public void Report_DisclosesTheCartridge_AndStillAddsUp()
        {
            using (var session = Emergency(AuroraMission.RedundancyPresetId, false, true))
            {
                BuildAndInstall(session);
                Assert.That(session.AdvanceSimulation(3600d).Succeeded, Is.True);
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                Fixtures.CompleteEva(session);
                Fixtures.Transmit(session);
                Assert.That(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds).Succeeded, Is.True);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible));

                MissionReport report = session.Report;

                Assert.That(report.AbsorberCartridgesBuilt, Is.EqualTo(1));
                Assert.That(report.AbsorberCartridgesInstalled, Is.EqualTo(1));
                Assert.That(report.AbsorbedCarbonDioxideKg, Is.EqualTo(1.01d / 24d).Within(1e-9), "one hour before life support was repaired");
                string text = report.ToPlainText();
                Assert.That(text, Does.Contain("Field modifications: 1 CO2 absorber cartridge built; net 1 spare part used, 0.20 kWh"));
                Assert.That(text, Does.Contain("GAME ASSUMPTION: 0.505 kg per cartridge, stated as 12 crew-hours of the cited 1.01 kg per crew-day; not a rated scrubber."));
                Assert.That(text, Does.Contain("it is not sized from or rated like a real lithium-hydroxide canister"));
                AssertExpendituresAddUp(report, expectedModificationParts: 1, expectedModificationEnergy: 0.2d);
            }
        }

        [Test]
        public void Report_DisclosesTheSalvage_AndStillAddsUp()
        {
            using (var session = Emergency(AuroraMission.ScienceFocusPresetId, true, false))
            {
                Require(session.FieldModifications.TrySalvagePayload());
                Fixtures.RepairOrFail(session, ShipSystemId.Power);
                Fixtures.RepairOrFail(session, ShipSystemId.LifeSupport);
                Fixtures.CompleteEva(session);
                Fixtures.Transmit(session);
                Assert.That(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds).Succeeded, Is.True);

                MissionReport report = session.Report;

                Assert.That(report.Outcome, Is.EqualTo(MissionOutcome.RecoveryPossible));
                Assert.That(report.ScienceDataReturned, Is.False);
                Assert.That(report.ResourcesAtEnd.SparePartsCount, Is.EqualTo(3), "7 carried + 3 salvaged - 7 for the repairs");
                Assert.That(report.ToPlainText(), Does.Contain("Field modifications: science instrument dismantled for spare parts; net +3 spare parts, 0.20 kWh"));
                AssertExpendituresAddUp(report, expectedModificationParts: -3, expectedModificationEnergy: 0.2d);
            }
        }

        private static void AssertExpendituresAddUp(MissionReport report, int expectedModificationParts, double expectedModificationEnergy)
        {
            double oxygen = 0d;
            double water = 0d;
            double food = 0d;
            double battery = 0d;
            int parts = 0;
            ReportExpenditure modifications = default(ReportExpenditure);
            foreach (ReportExpenditure e in report.Expenditures)
            {
                oxygen += e.OxygenKg;
                water += e.WaterLitres;
                food += e.FoodKg;
                battery += e.BatteryKWh;
                parts += e.SpareParts;
                if (e.Purpose == "field modifications")
                {
                    modifications = e;
                }
            }

            ResourceSnapshot start = report.ResourcesAtLaunch;
            ResourceSnapshot end = report.ResourcesAtEnd;
            Assert.That(oxygen, Is.EqualTo(start.OxygenKg - end.OxygenKg).Within(1e-9));
            Assert.That(water, Is.EqualTo(start.WaterLitres - end.WaterLitres).Within(1e-9));
            Assert.That(food, Is.EqualTo(start.FoodKg - end.FoodKg).Within(1e-9));
            Assert.That(battery, Is.EqualTo(start.BatteryKWh - end.BatteryKWh).Within(1e-9));
            Assert.That(parts, Is.EqualTo(start.SparePartsCount - end.SparePartsCount));
            Assert.That(modifications.SpareParts, Is.EqualTo(expectedModificationParts));
            Assert.That(modifications.BatteryKWh, Is.EqualTo(expectedModificationEnergy).Within(1e-12));
        }

        [Test]
        public void Restart_ForgetsEveryModification()
        {
            var first = Emergency(AuroraMission.ScienceFocusPresetId, true, true);
            Require(first.FieldModifications.TrySalvagePayload());
            BuildAndInstall(first);
            first.Dispose();
            Assert.That(first.FieldModifications.TryBuildCartridge().Failure, Is.EqualTo(CommandFailure.WrongPhase));

            using (var second = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                FieldModifications mods = second.FieldModifications;
                Assert.That(mods.SalvageEnabled, Is.False);
                Assert.That(mods.AbsorberEnabled, Is.False);
                Assert.That(mods.SciencePayloadSalvaged, Is.False);
                Assert.That(mods.AbsorberInstalled, Is.False);
                Assert.That(mods.CartridgesBuilt, Is.EqualTo(0));
                Assert.That(mods.RemovedLoadWatts, Is.EqualTo(0d));
                Assert.That(second.Resources.Snapshot.SparePartsCount, Is.EqualTo(7));
                Assert.That(second.Director.PowerState.LoadWatts, Is.EqualTo(1100d).Within(1e-9));
            }
        }

        [Test]
        public void Spec_RejectsInvalidAssumptions_AndDerivesTheCapacity()
        {
            Assert.That(new FieldModificationSpec().CartridgeCapacityKg, Is.EqualTo(0.505d).Within(1e-12));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new FieldModificationSpec(salvageSpareParts: 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new FieldModificationSpec(cartridgeCrewHours: 0d));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new FieldModificationSpec(cartridgeEnergyKWh: double.NaN));
        }
    }
}
