using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>
    /// C06-REVIEW-FIX (review 6880158546, findings 2 and 5): after contact, in the optional Survival chapter, the
    /// existing inside-ship field modifications still work with every other guard intact, and the cockpit console
    /// reports the restored contact instead of a transmit refusal and never offers a second distress call.
    /// </summary>
    public sealed class SurvivalInteractionReviewTests
    {
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
        /// The default loadout with the survival extension on: stabilized, antenna repaired on the EVA, distress call
        /// sent and, unless told otherwise, Earth's reply received (Transmission to Survival).
        /// </summary>
        private static MissionSession Contact(bool fieldTargetsBound, bool receive = true)
        {
            var session = new MissionSession(survivalExtension: true);
            session.EnableFieldModifications(fieldTargetsBound, fieldTargetsBound);
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

        private static void SpendAllSpareParts(MissionSession session)
        {
            double parts = session.Resources.GetQuantity(ResourceId.SparePartsCount);
            if (parts > 0d)
            {
                Require(session.Resources.TryConsume(new[] { new ResourceAmount(ResourceId.SparePartsCount, parts) }));
            }

            Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(0));
        }

        [Test]
        public void AfterContact_SalvageStillWorks_SoACrewWithNoSparePartsCanStillBuildTheGrowModule()
        {
            using (MissionSession session = Contact(fieldTargetsBound: true))
            {
                SpendAllSpareParts(session);
                CommandResult noParts = session.Survival.CheckBuildGrowModule();
                Assert.That(noParts.Failure, Is.EqualTo(CommandFailure.InsufficientResources), "the grow module costs a spare part");

                int yield = session.FieldModifications.SalvageablePartsNow;
                Assert.That(yield, Is.GreaterThan(0), "the science instrument can still be dismantled after contact");
                IInteractable salvage = Get(session, InteractionIds.SalvagePayload);
                Assert.That(salvage.CanInteract, Is.True, salvage.Prompt);
                Require(salvage.TryInteract());

                Assert.That(session.FieldModifications.SciencePayloadSalvaged, Is.True);
                Assert.That(session.FieldModifications.RemovedLoadWatts, Is.GreaterThan(0d), "its load is off the bus");
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(yield));
                Assert.That(salvage.TryInteract().Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "salvage stays once only");

                Require(session.Survival.TryBuildGrowModule());
                Assert.That(session.Survival.GrowStage, Is.EqualTo(GrowModuleStage.ReadyToPlant));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
            }
        }

        [Test]
        public void AfterContact_TheCo2CartridgeIsBuiltAtTheBench_AndInstalledInTheBay_WithRealParts()
        {
            using (MissionSession session = Contact(fieldTargetsBound: true))
            {
                FieldModifications mods = session.FieldModifications;
                Require(Get(session, InteractionIds.SalvagePayload).TryInteract());
                int partsBefore = session.Resources.Snapshot.SparePartsCount;

                IInteractable bench = Get(session, InteractionIds.Workbench);
                Assert.That(bench.CanInteract, Is.True, bench.Prompt);
                Require(bench.TryInteract());
                Assert.That(mods.CartridgesBuilt, Is.EqualTo(1));
                Assert.That(mods.AbsorberCartridgesCarried, Is.EqualTo(1));
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.LessThan(partsBefore), "the bench spends real spare parts");

                IInteractable bay = Get(session, InteractionIds.AbsorberBay);
                Assert.That(bay.CanInteract, Is.True, bay.Prompt);
                Require(bay.TryInteract());
                Assert.That(mods.CartridgesInstalled, Is.EqualTo(1));
                Assert.That(mods.AbsorberCartridgesCarried, Is.EqualTo(0));
                Assert.That(mods.AbsorberInstalled, Is.True);
                Assert.That(mods.AbsorberRemainingSimulationSeconds, Is.GreaterThan(0d));
                Assert.That(bay.TryInteract().Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "a working cartridge is left alone");

                Require(session.AdvanceSimulation(3600d));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival), "the chapter keeps running with the cartridge in place");
            }
        }

        [Test]
        public void AfterContact_UnboundTargets_AndAnEndedMission_StillRefuse_AndChangeNothing()
        {
            using (MissionSession session = Contact(fieldTargetsBound: false))
            {
                ResourceSnapshot before = session.Resources.Snapshot;
                foreach (string id in new[] { InteractionIds.SalvagePayload, InteractionIds.Workbench, InteractionIds.AbsorberBay })
                {
                    IInteractable target = Get(session, id);
                    Assert.That(target.CanInteract, Is.False, id);
                    Assert.That(target.TryInteract().Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), id);
                }

                Fixtures.AssertSnapshot(before, session.Resources.Snapshot, 0d);
                Assert.That(session.FieldModifications.SciencePayloadSalvaged, Is.False);
                Assert.That(session.FieldModifications.CartridgesBuilt, Is.EqualTo(0));
            }

            using (MissionSession session = Contact(fieldTargetsBound: true))
            {
                // Nobody drinks, so the thirst limit ends the mission; nothing can be dismantled or built afterwards.
                session.AdvanceSimulation(session.Survival.Spec.ThirstLimitSeconds);
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost), session.State.OutcomeReason);

                ResourceSnapshot after = session.Resources.Snapshot;
                Assert.That(session.FieldModifications.TrySalvagePayload().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(session.FieldModifications.TryBuildCartridge().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Assert.That(session.FieldModifications.TryInstallCartridge().Failure, Is.EqualTo(CommandFailure.WrongPhase));
                Fixtures.AssertSnapshot(after, session.Resources.Snapshot, 0d);
                Assert.That(session.FieldModifications.SciencePayloadSalvaged, Is.False);
            }
        }

        [Test]
        public void TheConsole_AfterContact_ReportsTheRestoredContact_AndNeverOffersASecondDistressCall()
        {
            using (MissionSession session = Contact(fieldTargetsBound: false, receive: false))
            {
                IInteractable console = Get(session, InteractionIds.CockpitConsole);
                Assert.That(console.Prompt, Does.StartWith("Awaiting reply from Earth"), "unchanged while the reply travels");

                Require(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
                Assert.That(console.Prompt, Is.EqualTo(CockpitConsoleInteraction.ContactRestoredPrompt));
                Assert.That(console.Prompt, Does.Not.Contain("send").IgnoreCase);
                Assert.That(console.Prompt, Does.Not.Contain("transmit").IgnoreCase);
                Assert.That(console.Prompt, Does.Not.Contain("distress").IgnoreCase);
                Assert.That(console.CanInteract, Is.False);

                ResourceSnapshot before = session.Resources.Snapshot;
                CommandResult again = console.TryInteract();
                Assert.That(again.Succeeded, Is.False);
                Assert.That(again.Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(again.Explanation, Does.Contain("already restored"));
                Fixtures.AssertSnapshot(before, session.Resources.Snapshot, 0d);
                Assert.That(session.Communications.State, Is.EqualTo(CommunicationState.ReplyReceived));
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
            }

            using (MissionSession recovered = Fixtures.RecoveredSession())
            {
                Assert.That(Get(recovered, InteractionIds.CockpitConsole).Prompt, Is.EqualTo("Contact with Earth restored"),
                    "the default ending at contact is unchanged");
            }
        }
    }
}
