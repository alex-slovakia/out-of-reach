using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Inventory;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>
    /// C05-BOARDING-01: with the physical preflight and the opt-in switch, an expiring loading deadline seals the
    /// manifest and the mission waits for the seat (the cockpit console). Legacy behavior stays exactly as it was.
    /// </summary>
    public sealed class BoardingWindowTests
    {
        private static MissionSession NewSession(string presetId, bool preflight = true, bool boarding = true)
        {
            return new MissionSession(AuroraMission.CreateDefinition(), null, presetId, preflight, false, false, null, boarding);
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
        public void Expiry_SealsWithoutLaunching_ClosesCargoAndTheList_AndNeverReArms()
        {
            using (MissionSession session = NewSession(AuroraMission.RedundancyPresetId))
            {
                PlanningDeadline deadline = session.PlanningDeadline;
                Assert.That(session.BoardingAfterDeadlineEnabled, Is.True);
                Assert.That(deadline.BoardingAfterExpiry, Is.True);
                Require(deadline.TryEnable());
                int parts = session.Planner.DraftConfiguration.GetQuantity(AuroraMission.SparePartsKit);

                session.Tick(60d);

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning), "no silent launch");
                Assert.That(deadline.HasExpired, Is.True);
                Assert.That(deadline.IsAwaitingBoarding, Is.True);
                Assert.That(deadline.DeadlineLaunchResult, Is.Null, "the deadline launched nothing");
                Assert.That(deadline.UsedLastValidLoadoutAtDeadline, Is.False, "the draft itself was valid");
                Assert.That(deadline.RemainingRealSeconds, Is.EqualTo(0d));
                Assert.That(session.Planner.IsSealed, Is.True);
                Assert.That(session.Planner.HasLaunched, Is.False);
                Assert.That(session.Resources.IsInitialized, Is.False, "no reserve before the seat launch");
                Assert.That(session.IsPlayerControlAllowed, Is.True, "the player can still walk to the seat");

                IInteractable pallet = Get(session, InteractionIds.CargoSupply(AuroraMission.OxygenTank));
                IInteractable stack = Get(session, InteractionIds.CargoBay(AuroraMission.OxygenTank));
                Assert.That(pallet.CanInteract, Is.False);
                Assert.That(stack.CanInteract, Is.False);
                Assert.That(pallet.Prompt, Is.EqualTo(PlanningDeadline.LoadingClosedMessage));
                Assert.That(stack.Prompt, Is.EqualTo(PlanningDeadline.LoadingClosedMessage));
                Assert.That(pallet.TryInteract().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(stack.TryInteract().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(session.Cargo.CarriedEquipmentId, Is.Null);

                CommandResult edit = session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, parts - 1);
                Assert.That(edit.Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(edit.Explanation, Is.EqualTo(PlanningDeadline.LoadingClosedMessage));
                CommandResult launch = session.Planner.TryLaunch();
                Assert.That(launch.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "only the seat launches");
                Assert.That(launch.Explanation, Is.EqualTo(PlanningDeadline.LoadingClosedMessage));

                Assert.That(deadline.TryEnable().Succeeded, Is.False, "no second 60 seconds");
                session.Tick(600d);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning), "the window has no timer");
                Assert.That(deadline.IsAwaitingBoarding, Is.True);
                Assert.That(session.Planner.DraftConfiguration.GetQuantity(AuroraMission.SparePartsKit), Is.EqualTo(parts), "the sealed manifest is unchanged");
            }
        }

        [Test]
        public void TheSeat_LaunchesTheSealedManifestOnce_WithExactlyItsReserves()
        {
            using (MissionSession session = NewSession(AuroraMission.RedundancyPresetId))
            {
                Require(session.PlanningDeadline.TryEnable());
                session.Tick(60d);
                MissionConfiguration sealedManifest = session.Planner.DraftConfiguration;

                IInteractable seat = Get(session, InteractionIds.CockpitConsole);
                Assert.That(seat.Prompt, Is.EqualTo(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Secure your seat and launch: {0:0} of {1:0} kg, {2:0} of {3:0} credits", sealedManifest.TotalMassKg,
                    sealedManifest.MassLimitKg, sealedManifest.TotalBudgetCredits, sealedManifest.BudgetLimitCredits)));
                Assert.That(seat.CanInteract, Is.True);

                Require(seat.TryInteract());

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));
                Assert.That(session.PlanningDeadline.IsAwaitingBoarding, Is.False);
                Assert.That(session.Planner.LaunchedConfiguration.GetQuantity(AuroraMission.SparePartsKit), Is.EqualTo(4));
                Assert.That(session.Planner.LaunchedConfiguration.TotalMassKg, Is.EqualTo(sealedManifest.TotalMassKg));
                ResourceSnapshot loaded = session.Resources.Snapshot;
                Assert.That(loaded.OxygenKg, Is.EqualTo(sealedManifest.InitialResources.OxygenKg), "no grant");
                Assert.That(loaded.WaterLitres, Is.EqualTo(sealedManifest.InitialResources.WaterLitres));
                Assert.That(loaded.FoodKg, Is.EqualTo(sealedManifest.InitialResources.FoodKg));
                Assert.That(loaded.SparePartsCount, Is.EqualTo(sealedManifest.InitialResources.SparePartsCount));
                Assert.That(session.State.ElapsedSimulationSeconds, Is.EqualTo(0d), "the launch spends no simulated time");
                Assert.That(session.Planner.TryLaunch().Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "one launch");
            }
        }

        [Test]
        public void ACrateInHandsAtTheSeal_StaysBehind_AndIsNeverCounted()
        {
            using (MissionSession session = NewSession(AuroraMission.ScienceFocusPresetId))
            {
                Require(session.PlanningDeadline.TryEnable());
                int aboard = session.Cargo.GetLoaded(AuroraMission.OxygenTank);
                int onPallet = session.Cargo.GetSupplyRemaining(AuroraMission.OxygenTank);
                Require(session.Cargo.TryUseSupply(AuroraMission.OxygenTank));
                Assert.That(session.Cargo.CarriedEquipmentId, Is.EqualTo(AuroraMission.OxygenTank));
                int changes = 0;
                session.Cargo.Changed += () => changes++;

                session.Tick(60d);

                Assert.That(session.Cargo.CarriedEquipmentId, Is.Null, "left behind at the seal");
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(session.Cargo.CarrySpeedFactor, Is.EqualTo(1f));
                Assert.That(session.Cargo.GetSupplyRemaining(AuroraMission.OxygenTank), Is.EqualTo(onPallet), "back on its pallet");
                Assert.That(session.Planner.DraftConfiguration.GetQuantity(AuroraMission.OxygenTank), Is.EqualTo(aboard));
                Assert.That(session.Director.Journal.Snapshot().Any(entry =>
                    entry.Text.Contains("Loading closed with a crate still in hand (an oxygen tank)")), Is.True);

                Require(Get(session, InteractionIds.CockpitConsole).TryInteract());
                Assert.That(session.Planner.LaunchedConfiguration.GetQuantity(AuroraMission.OxygenTank), Is.EqualTo(aboard), "never counted");
                Assert.That(session.Resources.Snapshot.OxygenKg,
                    Is.EqualTo(session.Planner.LaunchedConfiguration.InitialResources.OxygenKg), "no oxygen grant for the crate");
            }
        }

        [Test]
        public void AnInvalidDraftAtTheSeal_BecomesTheLastValidLoadout_WhichTheSeatLaunches()
        {
            using (MissionSession session = NewSession(AuroraMission.ScienceFocusPresetId))
            {
                Require(session.PlanningDeadline.TryEnable(30d));
                Require(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 3));
                Require(session.Planner.TrySetEquipment(AuroraMission.SolarWing, 0));
                Assert.That(session.Planner.Draft.Validate().Succeeded, Is.False, "no power for the spectrometer");
                IMissionConfiguration lastValid = session.PlanningDeadline.LastValidConfiguration;

                session.Tick(30d);

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning));
                Assert.That(session.PlanningDeadline.IsAwaitingBoarding, Is.True);
                Assert.That(session.PlanningDeadline.UsedLastValidLoadoutAtDeadline, Is.True);
                Assert.That(session.Planner.Draft.Validate().Succeeded, Is.True, "the sealed manifest is the last valid loadout");
                Assert.That(session.Planner.DraftConfiguration.GetQuantity(AuroraMission.SparePartsKit), Is.EqualTo(3), "the player's choice, not a preset");
                Assert.That(session.Planner.DraftConfiguration.GetQuantity(AuroraMission.SolarWing), Is.EqualTo(1));

                Require(Get(session, InteractionIds.CockpitConsole).TryInteract());

                Assert.That(session.Planner.LaunchedConfiguration.TotalMassKg, Is.EqualTo(lastValid.TotalMassKg));
                Assert.That(session.Planner.LaunchedConfiguration.GetQuantity(AuroraMission.SparePartsKit), Is.EqualTo(3));
                Assert.That(session.Planner.LaunchedConfiguration.GetQuantity(AuroraMission.SolarWing), Is.EqualTo(1));
            }
        }

        [Test]
        public void Pause_ChangesNothingInTheWindow_AndARestartedSessionStartsOpen()
        {
            using (MissionSession session = NewSession(AuroraMission.RedundancyPresetId))
            {
                Require(session.PlanningDeadline.TryEnable());
                session.Tick(30d);
                Require(session.Clock.Pause());
                session.Tick(500d);
                Assert.That(session.PlanningDeadline.RemainingRealSeconds, Is.EqualTo(30d).Within(Fixtures.Tolerance), "paused time does not count");
                Require(session.Clock.Resume());
                session.Tick(30d);
                Assert.That(session.PlanningDeadline.IsAwaitingBoarding, Is.True);

                Require(session.Clock.Pause());
                Assert.That(session.IsPlayerControlAllowed, Is.False, "pause still locks control");
                session.Tick(1000d);
                Assert.That(session.PlanningDeadline.IsAwaitingBoarding, Is.True);
                Require(session.Clock.Resume());
                session.Tick(1d);
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning), "resuming launches nothing");
                Assert.That(session.PlanningDeadline.IsAwaitingBoarding, Is.True);
                Assert.That(session.IsPlayerControlAllowed, Is.True);
            }

            // A restart builds a new session from the same switches: nothing of the old window survives.
            using (MissionSession restarted = NewSession(AuroraMission.RedundancyPresetId))
            {
                Assert.That(restarted.BoardingAfterDeadlineEnabled, Is.True);
                Assert.That(restarted.Planner.IsSealed, Is.False);
                Assert.That(restarted.PlanningDeadline.IsEnabled, Is.False, "practice until timed planning starts again");
                Assert.That(restarted.PlanningDeadline.HasExpired, Is.False);
                Assert.That(restarted.PlanningDeadline.IsAwaitingBoarding, Is.False);
                Assert.That(restarted.PlanningDeadline.UsedLastValidLoadoutAtDeadline, Is.False);
                Require(restarted.Cargo.TryUseSupply(AuroraMission.OxygenTank));
            }
        }

        [Test]
        public void BeforeExpiry_TheConsoleLaunchesEarly_AsBefore()
        {
            using (MissionSession session = NewSession(AuroraMission.RedundancyPresetId))
            {
                Require(session.PlanningDeadline.TryEnable());
                session.Tick(10d);
                IInteractable console = Get(session, InteractionIds.CockpitConsole);
                Assert.That(console.Prompt, Does.StartWith("Begin mission: "));

                Require(console.TryInteract());

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));
                Assert.That(session.Planner.IsSealed, Is.False);
                session.Tick(100d);
                Assert.That(session.PlanningDeadline.HasExpired, Is.False, "a launched mission has no deadline left");
                Assert.That(session.PlanningDeadline.IsAwaitingBoarding, Is.False);
            }
        }

        [Test]
        public void LegacyMode_ExpiryLaunchesAtOnce_AndTheSwitchNeedsThePhysicalPreflight()
        {
            using (MissionSession legacy = NewSession(AuroraMission.RedundancyPresetId, preflight: true, boarding: false))
            {
                Assert.That(legacy.PlanningDeadline.BoardingAfterExpiry, Is.False);
                Require(legacy.PlanningDeadline.TryEnable());
                legacy.Tick(60d);
                Assert.That(legacy.State.Phase, Is.EqualTo(GamePhase.Intro));
                Assert.That(legacy.PlanningDeadline.DeadlineLaunchResult.Value.Succeeded, Is.True);
                Assert.That(legacy.Planner.IsSealed, Is.False);
                Assert.That(legacy.PlanningDeadline.IsAwaitingBoarding, Is.False);
            }

            using (MissionSession list = NewSession(AuroraMission.RedundancyPresetId, preflight: false, boarding: true))
            {
                Assert.That(list.BoardingAfterDeadlineEnabled, Is.False, "without player control nobody could reach the seat");
                Require(list.PlanningDeadline.TryEnable());
                list.Tick(60d);
                Assert.That(list.State.Phase, Is.EqualTo(GamePhase.Intro));
                Assert.That(list.Planner.IsSealed, Is.False);
            }
        }
    }
}
