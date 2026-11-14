using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Inventory;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    public sealed class PlannerTests
    {
        [Test]
        public void Catalog_HasAuthoritativeUnitValues_AndCannotBeMutated()
        {
            using (var session = Fixtures.NewSession())
            {
                IReadOnlyList<EquipmentSelection> catalog = session.Planner.Catalog;

                Assert.That(catalog, Has.Count.EqualTo(7));
                Assert.That(catalog.All(entry => entry.Quantity == 1), Is.True, "catalog entries describe one unit");
                EquipmentSelection spectrometer = catalog.Single(entry => entry.EquipmentId == AuroraMission.Spectrometer);
                Assert.That(spectrometer.UnitPowerWatts, Is.EqualTo(300d));
                Assert.That(spectrometer.UnitMassKg, Is.EqualTo(90d));

                var asList = (IList<EquipmentSelection>)catalog;
                Assert.That(asList.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => asList[0] = new EquipmentSelection("cheap-hack", 1, 0d, 0d, 0d));
            }
        }

        [Test]
        public void BothStartingLoadouts_AreViable_WithGenuineTradeOffs()
        {
            using (var science = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            using (var redundancy = Fixtures.NewSession(AuroraMission.RedundancyPresetId))
            {
                MissionConfiguration s = science.Planner.DraftConfiguration;
                MissionConfiguration r = redundancy.Planner.DraftConfiguration;

                Assert.That(s.Validate().Succeeded, Is.True, s.Validate().Explanation);
                Assert.That(r.Validate().Succeeded, Is.True, r.Validate().Explanation);

                // Science focus: spectrometer data, paid for with power, mass and budget.
                Assert.That(s.HasSciencePayload, Is.True);
                Assert.That(s.TotalMassKg, Is.EqualTo(546d).Within(Fixtures.Tolerance));
                Assert.That(s.TotalBudgetCredits, Is.EqualTo(720d).Within(Fixtures.Tolerance));
                Assert.That(s.RequiredPowerWatts, Is.EqualTo(1400d).Within(Fixtures.Tolerance));
                Assert.That(s.AvailablePowerWatts, Is.EqualTo(1900d).Within(Fixtures.Tolerance));
                Assert.That(s.InitialResources.SparePartsCount, Is.EqualTo(7));
                Assert.That(s.InitialResources.BatteryKWh, Is.EqualTo(28d).Within(Fixtures.Tolerance));

                // Redundancy: no science, but more parts, energy and oxygen for the emergency.
                Assert.That(r.HasSciencePayload, Is.False);
                Assert.That(r.TotalMassKg, Is.EqualTo(591d).Within(Fixtures.Tolerance));
                Assert.That(r.TotalBudgetCredits, Is.EqualTo(690d).Within(Fixtures.Tolerance));
                Assert.That(r.InitialResources.SparePartsCount, Is.EqualTo(13));
                Assert.That(r.InitialResources.BatteryKWh, Is.EqualTo(38d).Within(Fixtures.Tolerance));
                Assert.That(r.InitialResources.OxygenKg, Is.GreaterThan(s.InitialResources.OxygenKg));
                Assert.That(r.RequiredPowerWatts, Is.LessThan(s.RequiredPowerWatts));
            }
        }

        [Test]
        public void OverMassDraft_StaysVisible_ButCannotLaunch()
        {
            using (var session = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            using (var recorder = new EventRecorder(session.Events))
            {
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.BatteryModule, 3).Succeeded, Is.True);
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 3).Succeeded, Is.True);

                IMissionConfiguration draft = session.Planner.Draft;
                CommandResult validation = draft.Validate();
                CommandResult launch = session.Planner.TryLaunch();

                Assert.That(draft.TotalMassKg, Is.EqualTo(626d).Within(Fixtures.Tolerance));
                Assert.That(validation.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(validation.Explanation, Does.Contain("Mass 626.0 kg exceeds the 600.0 kg launch limit by 26.0 kg."));
                AssertLaunchRejectedWithoutMutation(session, recorder, launch);
            }
        }

        [Test]
        public void OverBudgetDraft_CannotLaunch()
        {
            using (var session = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            using (var recorder = new EventRecorder(session.Events))
            {
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 4).Succeeded, Is.True);

                CommandResult validation = session.Planner.Draft.Validate();
                CommandResult launch = session.Planner.TryLaunch();

                Assert.That(session.Planner.Draft.TotalMassKg, Is.EqualTo(596d).Within(Fixtures.Tolerance), "mass alone is still legal");
                Assert.That(validation.Explanation, Does.Contain("Budget 810 credits exceeds the 800-credit limit by 10 credits."));
                Assert.That(validation.Explanation, Does.Not.Contain("Mass"));
                AssertLaunchRejectedWithoutMutation(session, recorder, launch);
            }
        }

        [Test]
        public void SpectrometerWithoutSolarWing_FailsThePowerConstraint()
        {
            using (var session = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            using (var recorder = new EventRecorder(session.Events))
            {
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SolarWing, 0).Succeeded, Is.True);

                CommandResult validation = session.Planner.Draft.Validate();
                CommandResult launch = session.Planner.TryLaunch();

                Assert.That(validation.Explanation, Does.Contain("Electrical load 1,400 W exceeds available generation 1,300 W by 100 W."));
                AssertLaunchRejectedWithoutMutation(session, recorder, launch);
            }
        }

        [Test]
        public void MissingOxygenSupplies_CannotLaunch()
        {
            using (var session = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            using (var recorder = new EventRecorder(session.Events))
            {
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.OxygenTank, 0).Succeeded, Is.True);

                CommandResult validation = session.Planner.Draft.Validate();
                CommandResult launch = session.Planner.TryLaunch();

                Assert.That(validation.Explanation, Does.Contain("Oxygen reserve 10.0 kg is below the 25.2 kg required for 30 planned crew-days."));
                AssertLaunchRejectedWithoutMutation(session, recorder, launch);
            }
        }

        [Test]
        public void InvalidEquipmentCommands_LeaveTheDraftUntouched()
        {
            using (var session = Fixtures.NewSession())
            {
                IMissionConfiguration before = session.Planner.Draft;

                Assert.That(session.Planner.TrySetEquipment(null, 1).Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.Planner.TrySetEquipment(string.Empty, 1).Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.Planner.TrySetEquipment("warp-drive", 1).Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.Planner.TrySetEquipment("Spectrometer", 1).Failure, Is.EqualTo(CommandFailure.InvalidInput), "IDs are case-sensitive");
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.OxygenTank, -1).Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.Spectrometer, 2).Failure, Is.EqualTo(CommandFailure.InvalidInput), "above the item limit");

                Assert.That(session.Planner.Draft, Is.SameAs(before));
            }
        }

        [Test]
        public void SetEquipment_ReplacesTheCount_AndZeroRemovesTheItem()
        {
            using (var session = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            {
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 5).Succeeded, Is.True);
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 1).Succeeded, Is.True);
                Assert.That(session.Planner.DraftConfiguration.GetQuantity(AuroraMission.SparePartsKit), Is.EqualTo(1), "replace, not add");

                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 0).Succeeded, Is.True);

                Assert.That(session.Planner.DraftConfiguration.GetQuantity(AuroraMission.SparePartsKit), Is.EqualTo(0));
                Assert.That(session.Planner.Draft.Equipment.Any(item => item.EquipmentId == AuroraMission.SparePartsKit), Is.False);
            }
        }

        [Test]
        public void ValidLaunch_FreezesConfiguration_LoadsReserves_AndEntersIntroOnce()
        {
            using (var session = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            using (var recorder = new EventRecorder(session.Events))
            {
                MissionConfiguration draft = session.Planner.DraftConfiguration;

                CommandResult launch = session.Planner.TryLaunch();

                Assert.That(launch.Succeeded, Is.True, launch.Explanation);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));
                Assert.That(recorder.Order, Is.EqualTo(new[] { "ResourceChanged", "PhaseChanged" }), "reserves are committed and announced before the phase change");
                Fixtures.AssertSnapshot(draft.InitialResources, session.Resources.Snapshot);

                MissionConfiguration launched = session.Planner.LaunchedConfiguration;
                Assert.That(launched, Is.Not.SameAs(draft), "launch freezes a fresh defensive copy");
                Assert.That(launched.TotalMassKg, Is.EqualTo(draft.TotalMassKg));
                Assert.That(launched.AstronomicalReferenceId, Is.EqualTo("jpl-horizons-earth-mars-2026-v1"));

                CommandResult again = session.Planner.TryLaunch();
                CommandResult edit = session.Planner.TrySetEquipment(AuroraMission.OxygenTank, 6);

                Assert.That(again.Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(edit.Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(launched.GetQuantity(AuroraMission.OxygenTank), Is.EqualTo(2));
                Assert.That(recorder.Total, Is.EqualTo(2), "no duplicate launch events");

                var equipment = (IList<EquipmentSelection>)launched.Equipment;
                Assert.Throws<NotSupportedException>(() => equipment.Add(new EquipmentSelection(AuroraMission.OxygenTank, 9, 0d, 0d, 0d)));
            }
        }

        [Test]
        public void StartingLoadoutCollection_ChangedLater_DoesNotAffectDraftOrLaunch()
        {
            MissionDefinition definition = AuroraMission.CreateDefinition();
            var loadout = new Dictionary<string, int>();
            foreach (KeyValuePair<string, int> entry in definition.GetPreset(AuroraMission.ScienceFocusPresetId))
            {
                loadout[entry.Key] = entry.Value;
            }

            var bus = new MissionEventBus();
            var planner = new MissionPlanner(definition, new MissionGameState(bus), new ResourceLedger(bus), bus, loadout);

            loadout[AuroraMission.SparePartsKit] = 5;
            loadout.Remove(AuroraMission.Spectrometer);

            Assert.That(planner.DraftConfiguration.GetQuantity(AuroraMission.SparePartsKit), Is.EqualTo(2));
            Assert.That(planner.DraftConfiguration.GetQuantity(AuroraMission.Spectrometer), Is.EqualTo(1));
            Assert.That(planner.TryLaunch().Succeeded, Is.True);

            loadout[AuroraMission.OxygenTank] = 6;

            Assert.That(planner.LaunchedConfiguration.GetQuantity(AuroraMission.OxygenTank), Is.EqualTo(2));
            Assert.That(planner.LaunchedConfiguration.GetQuantity(AuroraMission.Spectrometer), Is.EqualTo(1));
        }

        [Test]
        public void SupplyMinimums_FollowTheDocumentedCrewAssumptions()
        {
            MissionParameters parameters = AuroraMission.CreateDefinition().Parameters;
            double crewDays = parameters.PlannedCrewDays * CrewMetabolicAssumptions.CrewCount;

            Assert.That(parameters.MinimumOxygenKg, Is.EqualTo(crewDays * CrewMetabolicAssumptions.OxygenKgPerCrewDay).Within(Fixtures.Tolerance));
            Assert.That(parameters.MinimumWaterLitres, Is.EqualTo(crewDays * CrewMetabolicAssumptions.WaterLitresPerCrewDay).Within(Fixtures.Tolerance));
            Assert.That(parameters.MinimumFoodKg, Is.EqualTo(crewDays * CrewMetabolicAssumptions.FoodKgPerCrewDay).Within(Fixtures.Tolerance));
        }

        private static void AssertLaunchRejectedWithoutMutation(MissionSession session, EventRecorder recorder, CommandResult launch)
        {
            Assert.That(launch.Succeeded, Is.False);
            Assert.That(launch.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning));
            Assert.That(session.Resources.IsInitialized, Is.False);
            Assert.That(session.Planner.HasLaunched, Is.False);
            Assert.That(recorder.Total, Is.EqualTo(0));
        }
    }
}
