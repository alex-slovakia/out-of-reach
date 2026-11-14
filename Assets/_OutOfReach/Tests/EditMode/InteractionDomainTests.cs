using System.Linq;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;

namespace OutOfReach.Tests.EditMode
{
    public sealed class InteractionDomainTests
    {
        private static IInteractable Get(MissionSession session, string interactionId)
        {
            Assert.That(session.Interactions.TryGet(interactionId, out IInteractable interactable), Is.True, interactionId);
            return interactable;
        }

        [Test]
        public void Catalog_ExposesEveryInteraction_ByStableId()
        {
            using (var session = Fixtures.NewSession())
            {
                Assert.That(session.Interactions.All.Select(item => item.InteractionId), Is.EqualTo(new[]
                {
                    InteractionIds.CockpitConsole, InteractionIds.RepairPower, InteractionIds.RepairLifeSupport,
                    InteractionIds.RepairAntenna, InteractionIds.SuitLocker, InteractionIds.AirlockControls,
                    InteractionIds.EvaExit, InteractionIds.EvaReturn,
                }));
                Assert.That(session.Interactions.TryGet("unknown", out _), Is.False);
                Assert.That(session.Interactions.TryGet(null, out _), Is.False);
            }
        }

        [Test]
        public void CockpitConsole_RunsTheCheckOnce_AndThenOnlyReportsStatus()
        {
            using (var session = Fixtures.NewSession())
            {
                IInteractable console = Get(session, InteractionIds.CockpitConsole);
                Assert.That(console.CanInteract, Is.False, "nothing to do before launch");
                Assert.That(console.TryInteract().Failure, Is.EqualTo(CommandFailure.WrongPhase));

                Assert.That(session.Planner.TryLaunch().Succeeded, Is.True);
                Assert.That(console.CanInteract, Is.True);
                Assert.That(console.Prompt, Is.EqualTo("Run cockpit systems check"));

                Assert.That(console.TryInteract().Succeeded, Is.True);
                Assert.That(console.CanInteract, Is.False);
                Assert.That(console.Prompt, Is.EqualTo("Impact detected"));

                session.Tick(0d);

                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Emergency));
                Assert.That(console.Prompt, Is.EqualTo("Communications offline: antenna damaged"));
                Assert.That(console.CanInteract, Is.False);
                CommandResult afterImpact = console.TryInteract();
                Assert.That(afterImpact.Failure, Is.EqualTo(CommandFailure.PreconditionsNotMet), "after the impact the console is the communications panel");
                Assert.That(afterImpact.Explanation, Is.EqualTo("Communications offline: antenna damaged"));
                Assert.That(session.Director.TryCompleteCockpitCheck().Failure, Is.EqualTo(CommandFailure.AlreadyComplete), "the impact cannot be triggered again");
                Assert.That(session.Systems.Power.Condition01, Is.EqualTo(0.2d), "no second damage");
            }
        }

        [Test]
        public void RepairPoint_PromptStatesTheCost_OrTheExactReasonItIsUnavailable()
        {
            using (var session = Fixtures.LaunchedSession(AuroraMission.ScienceFocusPresetId))
            {
                IInteractable power = Get(session, InteractionIds.RepairPower);
                IInteractable lifeSupport = Get(session, InteractionIds.RepairLifeSupport);
                Assert.That(power.Prompt, Is.EqualTo("Power distribution: nominal"));
                Assert.That(power.CanInteract, Is.False);

                Fixtures.TriggerImpact(session);

                Assert.That(power.Prompt, Is.EqualTo("Repair power distribution: 2 spare parts."));
                Assert.That(lifeSupport.Prompt, Is.EqualTo("Repair life support: 2 spare parts, 1 kWh."));
                Assert.That(power.CanInteract, Is.True);
                Assert.That(lifeSupport.CanInteract, Is.True);

                Assert.That(session.Resources.TryConsume(new[] { new ResourceAmount(ResourceId.SparePartsCount, 6d) }).Succeeded, Is.True);

                Assert.That(power.CanInteract, Is.False);
                Assert.That(power.Prompt, Is.EqualTo("Cannot repair power distribution: needs 2 spare parts, 1 spare part available."));
                Assert.That(power.TryInteract().Failure, Is.EqualTo(CommandFailure.InsufficientResources));
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void RepairPoint_RepairsThroughTheContract_Once()
        {
            using (var session = Fixtures.EmergencySession(AuroraMission.ScienceFocusPresetId))
            {
                IInteractable power = Get(session, InteractionIds.RepairPower);

                Assert.That(power.TryInteract().Succeeded, Is.True);

                Assert.That(session.Systems.Power.IsOperational, Is.True);
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(5));
                Assert.That(power.Prompt, Is.EqualTo("Power distribution: repaired"));
                Assert.That(power.CanInteract, Is.False);
                Assert.That(power.TryInteract().Failure, Is.EqualTo(CommandFailure.AlreadyComplete));
                Assert.That(session.Resources.Snapshot.SparePartsCount, Is.EqualTo(5), "spent once");
            }
        }
    }
}
