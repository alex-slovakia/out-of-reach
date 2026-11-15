using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Inventory;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>Physical preflight loading: the planner's draft stays the only inventory.</summary>
    public sealed class CargoLoadingTests
    {
        private static MissionSession PreflightSession()
        {
            return new MissionSession(AuroraMission.CreateDefinition(), null, null, true);
        }

        private static IInteractable Get(MissionSession session, string interactionId)
        {
            Assert.That(session.Interactions.TryGet(interactionId, out IInteractable interactable), Is.True, interactionId);
            return interactable;
        }

        private static void Require(CommandResult result)
        {
            Assert.That(result.Succeeded, Is.True, result.Explanation);
        }

        [Test]
        public void PickingUp_PutsOneCrateInHands_AndLeavesTheDraftAlone()
        {
            using (var session = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            {
                CargoLoading cargo = session.Cargo;
                int changes = 0;
                cargo.Changed += () => changes++;
                IInteractable pallet = Get(session, InteractionIds.CargoSupply(AuroraMission.OxygenTank));
                Assert.That(cargo.CarriedEquipmentId, Is.Null);
                Assert.That(cargo.CarrySpeedFactor, Is.EqualTo(1d));
                Assert.That(cargo.GetSupplyRemaining(AuroraMission.OxygenTank), Is.EqualTo(4), "six at most, two aboard");
                Assert.That(pallet.Prompt, Is.EqualTo("Pick up oxygen tank: 30 kg, 40 credits, +12 kg oxygen (4 on the pallet)"));
                Assert.That(pallet.CanInteract, Is.True);

                Require(pallet.TryInteract());

                Assert.That(cargo.CarriedEquipmentId, Is.EqualTo(AuroraMission.OxygenTank));
                Assert.That(cargo.GetSupplyRemaining(AuroraMission.OxygenTank), Is.EqualTo(3));
                Assert.That(cargo.GetLoaded(AuroraMission.OxygenTank), Is.EqualTo(2), "a crate in hands is not aboard");
                Assert.That(session.Planner.Draft.TotalMassKg, Is.EqualTo(546d));
                Assert.That(cargo.CarrySpeedFactor, Is.EqualTo(1d - (30d / 180d)).Within(1e-6));
                ICargoLoading view = cargo;
                Assert.That(view.CarriedEquipmentId, Is.EqualTo(AuroraMission.OxygenTank));
                Assert.That(view.CarrySpeedFactor, Is.EqualTo(cargo.CarrySpeedFactor));
                Assert.That(view.GetSupplyRemaining(AuroraMission.OxygenTank), Is.EqualTo(3));
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(pallet.Prompt, Is.EqualTo("Put the oxygen tank back on the pallet"));

                Require(pallet.TryInteract());

                Assert.That(cargo.CarriedEquipmentId, Is.Null);
                Assert.That(cargo.GetSupplyRemaining(AuroraMission.OxygenTank), Is.EqualTo(4));
                Assert.That(changes, Is.EqualTo(2));
            }
        }

        [Test]
        public void LoadingAndUnloading_ChangeTheDraftByExactlyOne()
        {
            using (var session = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            {
                IInteractable pallet = Get(session, InteractionIds.CargoSupply(AuroraMission.OxygenTank));
                IInteractable stack = Get(session, InteractionIds.CargoBay(AuroraMission.OxygenTank));
                int draftChanges = 0;
                session.Planner.DraftChanged += () => draftChanges++;
                Assert.That(stack.Prompt, Is.EqualTo("Unload one oxygen tank (2 aboard)"));

                Require(pallet.TryInteract());
                Assert.That(stack.Prompt, Is.EqualTo("Load oxygen tank: payload 546 -> 576 of 600 kg"));
                CommandResult loaded = stack.TryInteract();

                Assert.That(loaded.Succeeded, Is.True, loaded.Explanation);
                Assert.That(loaded.Explanation, Is.EqualTo("Oxygen tank loaded."));
                Assert.That(session.Cargo.CarriedEquipmentId, Is.Null);
                Assert.That(session.Planner.Draft.TotalMassKg, Is.EqualTo(576d));
                Assert.That(session.Planner.Draft.TotalBudgetCredits, Is.EqualTo(760d));
                Assert.That(session.Planner.Draft.InitialResources.OxygenKg, Is.EqualTo(46d));
                Assert.That(session.Cargo.GetLoaded(AuroraMission.OxygenTank), Is.EqualTo(3));
                Assert.That(draftChanges, Is.EqualTo(1));

                Require(stack.TryInteract());

                Assert.That(session.Cargo.CarriedEquipmentId, Is.EqualTo(AuroraMission.OxygenTank), "taken out of the bay, now in hands");
                Assert.That(session.Planner.Draft.TotalMassKg, Is.EqualTo(546d));
                Assert.That(session.Cargo.GetSupplyRemaining(AuroraMission.OxygenTank), Is.EqualTo(3));
                Assert.That(draftChanges, Is.EqualTo(2));
            }
        }

        [Test]
        public void OnlyOneCrate_AndOnlyAtItsOwnPalletAndStack()
        {
            using (var session = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            {
                Require(session.Cargo.TryUseSupply(AuroraMission.OxygenTank));
                ResourceSnapshot before = session.Planner.Draft.InitialResources;

                CommandResult second = Get(session, InteractionIds.CargoSupply(AuroraMission.WaterTank)).TryInteract();
                CommandResult wrongStack = Get(session, InteractionIds.CargoBay(AuroraMission.WaterTank)).TryInteract();

                Assert.That(second.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(second.Explanation, Is.EqualTo("Hands full: carrying an oxygen tank"));
                Assert.That(wrongStack.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(wrongStack.Explanation, Is.EqualTo("This stack takes water tank; you are carrying an oxygen tank"));
                Assert.That(session.Cargo.CarriedEquipmentId, Is.EqualTo(AuroraMission.OxygenTank));
                Fixtures.AssertSnapshot(before, session.Planner.Draft.InitialResources, 0d);

                Assert.That(session.Cargo.TryUseSupply("no-such-crate").Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.Cargo.TryUseBay(null).Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.Cargo.GetSupplyRemaining("no-such-crate"), Is.EqualTo(0));
            }
        }

        [Test]
        public void EmptyPallets_AndEmptyStacks_SayWhy()
        {
            using (var session = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            {
                IInteractable instrumentPallet = Get(session, InteractionIds.CargoSupply(AuroraMission.Spectrometer));
                Assert.That(instrumentPallet.CanInteract, Is.False);
                Assert.That(instrumentPallet.Prompt, Is.EqualTo("The only atmospheric spectrometer is already aboard"));

                Require(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 5));
                Assert.That(Get(session, InteractionIds.CargoSupply(AuroraMission.SparePartsKit)).Prompt,
                    Is.EqualTo("No spare parts kit left on the pallet: all 5 are aboard"));

                Require(session.Planner.TrySetEquipment(AuroraMission.SolarWing, 0));
                IInteractable wingStack = Get(session, InteractionIds.CargoBay(AuroraMission.SolarWing));
                Assert.That(wingStack.CanInteract, Is.False);
                Assert.That(wingStack.Prompt, Is.EqualTo("No deployable solar wing aboard"));
                Assert.That(wingStack.TryInteract().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
            }
        }

        [Test]
        public void LoadingPastALimit_IsAllowed_AndBlocksTheLaunchLikeTheListPlanner()
        {
            using (var session = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            {
                IInteractable pallet = Get(session, InteractionIds.CargoSupply(AuroraMission.BatteryModule));
                IInteractable stack = Get(session, InteractionIds.CargoBay(AuroraMission.BatteryModule));
                Require(pallet.TryInteract());
                Assert.That(stack.Prompt, Is.EqualTo("Load battery module: payload 546 -> 601 of 600 kg (over the limit)"));

                CommandResult loaded = stack.TryInteract();

                const string violation = "Mass 601.0 kg exceeds the 600.0 kg launch limit by 1.0 kg.";
                Assert.That(loaded.Succeeded, Is.True, "the draft stays inspectable");
                Assert.That(loaded.Explanation, Is.EqualTo("Battery module loaded. " + violation));
                Assert.That(session.Planner.Draft.Validate().Explanation, Is.EqualTo(violation));
                using (var listPlanner = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
                {
                    Require(listPlanner.Planner.TrySetEquipment(AuroraMission.BatteryModule, 3));
                    Assert.That(listPlanner.Planner.Draft.Validate().Explanation, Is.EqualTo(violation), "the same rule and the same words");
                }
            }
        }

        [Test]
        public void SwappingCratesByHand_EqualsEditingTheList()
        {
            using (var byHand = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            using (var byList = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            {
                CargoLoading cargo = byHand.Cargo;
                Require(cargo.TryUseBay(AuroraMission.Spectrometer));
                Require(cargo.TryUseSupply(AuroraMission.Spectrometer));
                for (int i = 0; i < 2; i++)
                {
                    Require(cargo.TryUseSupply(AuroraMission.SparePartsKit));
                    Require(cargo.TryUseBay(AuroraMission.SparePartsKit));
                }

                Require(byList.Planner.TrySetEquipment(AuroraMission.Spectrometer, 0));
                Require(byList.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 4));

                IMissionConfiguration a = byHand.Planner.Draft;
                IMissionConfiguration b = byList.Planner.Draft;
                Assert.That(a.TotalMassKg, Is.EqualTo(b.TotalMassKg));
                Assert.That(a.TotalBudgetCredits, Is.EqualTo(b.TotalBudgetCredits));
                Assert.That(a.RequiredPowerWatts, Is.EqualTo(b.RequiredPowerWatts));
                Assert.That(a.AvailablePowerWatts, Is.EqualTo(b.AvailablePowerWatts));
                Fixtures.AssertSnapshot(b.InitialResources, a.InitialResources, 0d);
                Assert.That(a.Validate().Succeeded, Is.True);
                Assert.That(a.InitialResources.SparePartsCount, Is.EqualTo(13));
                Assert.That(byHand.Planner.DraftConfiguration.HasSciencePayload, Is.False);
            }
        }

        [Test]
        public void ACrateInHandsAtLaunch_IsNotAboard_AndTheBayIsSealedAfterwards()
        {
            using (var session = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            {
                Require(session.Cargo.TryUseSupply(AuroraMission.OxygenTank));
                int changes = 0;
                session.Cargo.Changed += () => changes++;

                Require(session.Planner.TryLaunch());

                Assert.That(session.Cargo.CarriedEquipmentId, Is.Null, "the crate stays behind");
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(session.Cargo.CarrySpeedFactor, Is.EqualTo(1d));
                Assert.That(session.Planner.LaunchedConfiguration.GetQuantity(AuroraMission.OxygenTank), Is.EqualTo(2));
                Assert.That(session.Resources.Snapshot.OxygenKg, Is.EqualTo(34d), "never counted");
                IReadOnlyList<MissionLogEntry> timeline = session.Director.Journal.Snapshot();
                Assert.That(timeline[timeline.Count - 1].Text, Does.Contain("crate was still in hand (an oxygen tank)"));
                Assert.That(timeline[timeline.Count - 1].AtSimulationSeconds, Is.EqualTo(0d));

                IInteractable pallet = Get(session, InteractionIds.CargoSupply(AuroraMission.OxygenTank));
                IInteractable stack = Get(session, InteractionIds.CargoBay(AuroraMission.OxygenTank));
                Assert.That(pallet.CanInteract, Is.False);
                Assert.That(pallet.Prompt, Is.EqualTo("The cargo bay is sealed after launch."));
                Assert.That(pallet.TryInteract().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(stack.TryInteract().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(session.Planner.LaunchedConfiguration.GetQuantity(AuroraMission.OxygenTank), Is.EqualTo(2), "frozen");
            }
        }

        [Test]
        public void TheDeadline_LaunchesTheLastValidLoadout_EvenWithACrateInHands()
        {
            using (var session = PreflightSession())
            {
                Require(session.PlanningDeadline.TryEnable(30d));
                Require(session.Cargo.TryUseBay(AuroraMission.SolarWing));
                Assert.That(session.Planner.Draft.Validate().Succeeded, Is.False, "no power for the spectrometer without the wing");

                session.Tick(30d);

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));
                Assert.That(session.PlanningDeadline.UsedLastValidLoadoutAtDeadline, Is.True);
                Assert.That(session.Planner.LaunchedConfiguration.GetQuantity(AuroraMission.SolarWing), Is.EqualTo(1), "the last valid loadout");
                Assert.That(session.Planner.LaunchedConfiguration.Validate().Succeeded, Is.True);
                Assert.That(session.Cargo.CarriedEquipmentId, Is.Null);
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(0d), "the launch frame spends no simulated time");
            }
        }

        [Test]
        public void AListEditWhileCarrying_CannotOverfillAType()
        {
            using (var session = Fixtures.NewSession(AuroraMission.ScienceFocusPresetId))
            {
                Require(session.Cargo.TryUseSupply(AuroraMission.OxygenTank));
                Require(session.Planner.TrySetEquipment(AuroraMission.OxygenTank, 6));
                Assert.That(session.Cargo.GetSupplyRemaining(AuroraMission.OxygenTank), Is.EqualTo(0));

                CommandResult overfill = session.Cargo.TryUseBay(AuroraMission.OxygenTank);

                Assert.That(overfill.Failure, Is.EqualTo(CommandFailure.InvalidInput));
                Assert.That(session.Cargo.GetLoaded(AuroraMission.OxygenTank), Is.EqualTo(6));
                Assert.That(session.Cargo.CarriedEquipmentId, Is.EqualTo(AuroraMission.OxygenTank), "still in hands");
                Require(session.Cargo.TryUseSupply(AuroraMission.OxygenTank));
                Assert.That(session.Cargo.CarriedEquipmentId, Is.Null);
            }
        }

        [TestCase(AuroraMission.SparePartsKit, 25d, 0.8611d)]
        [TestCase(AuroraMission.OxygenTank, 30d, 0.8333d)]
        [TestCase(AuroraMission.FoodLocker, 35d, 0.8056d)]
        [TestCase(AuroraMission.WaterTank, 48d, 0.7333d)]
        [TestCase(AuroraMission.BatteryModule, 55d, 0.6944d)]
        [TestCase(AuroraMission.SolarWing, 70d, 0.6111d)]
        [TestCase(AuroraMission.Spectrometer, 90d, 0.5d)]
        public void CarrySpeed_FollowsTheAuthoritativeUnitMass(string equipmentId, double massKg, double expectedFactor)
        {
            MissionDefinition definition = AuroraMission.CreateDefinition();
            Assert.That(definition.Catalog.TryGet(equipmentId, out EquipmentDefinition item), Is.True);
            Assert.That(item.UnitMassKg, Is.EqualTo(massKg));
            Assert.That(CargoLoading.SpeedFactorForMass(item.UnitMassKg), Is.EqualTo(expectedFactor).Within(5e-5));
        }

        [Test]
        public void CarrySpeed_IsBounded()
        {
            Assert.That(CargoLoading.SpeedFactorForMass(0d), Is.EqualTo(1d));
            Assert.That(CargoLoading.SpeedFactorForMass(-5d), Is.EqualTo(1d));
            Assert.That(CargoLoading.SpeedFactorForMass(double.NaN), Is.EqualTo(1d));
            Assert.That(CargoLoading.SpeedFactorForMass(500d), Is.EqualTo(0.5d));
        }

        [Test]
        public void EveryCatalogItem_HasAPalletAndAStack_WithItsEffectInThePrompt()
        {
            using (var session = Fixtures.NewSession(AuroraMission.RedundancyPresetId))
            {
                Assert.That(session.Interactions.All.Count, Is.EqualTo(11 + (2 * session.Definition.Catalog.Count)));
                foreach (EquipmentDefinition item in session.Definition.Catalog.Definitions)
                {
                    Assert.That(Get(session, "cargo-supply:" + item.Id).InteractionId, Is.EqualTo("cargo-supply:" + item.Id));
                    Assert.That(Get(session, "cargo-bay:" + item.Id).InteractionId, Is.EqualTo("cargo-bay:" + item.Id));
                }

                Assert.That(Get(session, InteractionIds.CargoSupply(AuroraMission.Spectrometer)).Prompt,
                    Is.EqualTo("Pick up atmospheric spectrometer: 90 kg, 220 credits, science payload, 300 W load (1 on the pallet)"));
                Assert.That(Get(session, InteractionIds.CargoSupply(AuroraMission.SparePartsKit)).Prompt,
                    Is.EqualTo("Pick up spare parts kit: 25 kg, 45 credits, +3 spare parts (1 on the pallet)"));
                Assert.That(Get(session, InteractionIds.CargoSupply(AuroraMission.SolarWing)).Prompt,
                    Is.EqualTo("Pick up deployable solar wing: 70 kg, 120 credits, +600 W generation (2 on the pallet)"));
            }
        }

        [Test]
        public void PlayerControlDuringPlanning_ExistsOnlyWithThePhysicalPreflight()
        {
            using (var list = Fixtures.NewSession())
            {
                Assert.That(list.PreflightWalkEnabled, Is.False);
                Assert.That(list.IsPlayerControlAllowed, Is.False, "the list planner has no walking before launch");
            }

            using (var preflight = PreflightSession())
            {
                Assert.That(preflight.PreflightWalkEnabled, Is.True);
                Assert.That(preflight.IsPlayerControlAllowed, Is.True);

                Require(preflight.Clock.Pause());
                Assert.That(preflight.IsPlayerControlAllowed, Is.False);
                Require(preflight.Clock.Resume());

                Require(preflight.Planner.TryLaunch());
                Assert.That(preflight.IsPlayerControlAllowed, Is.True, "Intro");
            }
        }

        [Test]
        public void TheConsole_LaunchesDuringPlanning_OnlyWithThePhysicalPreflight()
        {
            using (var preflight = PreflightSession())
            {
                IInteractable console = Get(preflight, InteractionIds.CockpitConsole);
                Assert.That(console.Prompt, Is.EqualTo("Begin mission: 546 of 600 kg, 720 of 800 credits"));
                Assert.That(console.CanInteract, Is.True);

                Require(preflight.Planner.TrySetEquipment(AuroraMission.SolarWing, 0));
                Assert.That(console.CanInteract, Is.False);
                Assert.That(console.Prompt, Is.EqualTo(preflight.Planner.Draft.Validate().Explanation));
                Assert.That(console.Prompt, Does.StartWith("Electrical load 1,400 W exceeds available generation 1,300 W"));
                Assert.That(console.TryInteract().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet));
                Assert.That(preflight.State.Phase, Is.EqualTo(GamePhase.Planning));

                Require(preflight.Planner.TrySetEquipment(AuroraMission.SolarWing, 1));
                Require(console.TryInteract());

                Assert.That(preflight.State.Phase, Is.EqualTo(GamePhase.Intro));
                Assert.That(console.Prompt, Is.EqualTo("Run cockpit systems check"));
                Assert.That(preflight.Director.AccidentTriggered, Is.False, "launching does not also run the check");
            }

            using (var list = Fixtures.NewSession())
            {
                IInteractable console = Get(list, InteractionIds.CockpitConsole);
                Assert.That(console.Prompt, Is.EqualTo("Console on standby until launch"));
                Assert.That(console.CanInteract, Is.False);
                Assert.That(console.TryInteract().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(list.State.Phase, Is.EqualTo(GamePhase.Planning));
            }
        }

        [Test]
        public void Restart_StartsWithEmptyHands()
        {
            var first = PreflightSession();
            Require(first.Cargo.TryUseSupply(AuroraMission.BatteryModule));
            first.Dispose();
            Assert.That(first.Cargo.TryUseSupply(AuroraMission.BatteryModule).Failure, Is.EqualTo(CommandFailure.WrongPhase));
            Assert.That(first.IsPlayerControlAllowed, Is.False);

            using (var second = PreflightSession())
            {
                Assert.That(second.Cargo.CarriedEquipmentId, Is.Null);
                Assert.That(second.Cargo.GetSupplyRemaining(AuroraMission.BatteryModule), Is.EqualTo(2));
                Assert.That(second.Planner.Draft.TotalMassKg, Is.EqualTo(546d));
            }
        }
    }
}
