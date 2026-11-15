using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Simulation;

namespace OutOfReach.Tests.EditMode
{
    /// <summary>R27: launching over the 600 kg limit ends in an explained crash; launches within it are unchanged.</summary>
    public sealed class OverweightLaunchTests
    {
        private static IInteractable Get(MissionSession session, string id)
        {
            Assert.That(session.Interactions.TryGet(id, out IInteractable interactable), Is.True, id);
            return interactable;
        }

        [Test]
        public void StowingCasesPastTheLimit_ThenLaunchingFromTheConsole_EndsAsAnExplainedCrash()
        {
            using (var session = new MissionSession(AuroraMission.CreateDefinition(), null, AuroraMission.ScienceFocusPresetId, preflightWalk: true))
            {
                Assert.That(Get(session, InteractionIds.CargoSupply(AuroraMission.BatteryModule)).TryInteract().Succeeded, Is.True);
                Assert.That(Get(session, InteractionIds.CargoBay(AuroraMission.BatteryModule)).TryInteract().Succeeded, Is.True);
                Assert.That(session.Planner.DraftConfiguration.TotalMassKg, Is.EqualTo(601d).Within(1e-9));

                IInteractable console = Get(session, InteractionIds.CockpitConsole);
                Assert.That(console.Prompt, Is.EqualTo("Begin mission OVERWEIGHT: 601 of 600 kg, over the launch limit"));
                Assert.That(console.CanInteract, Is.True);
                Assert.That(console.TryInteract().Succeeded, Is.True);

                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.LaunchOverweight));
                Assert.That(session.State.OutcomeReason, Is.EqualTo(
                    "Launch mass 601 kg exceeded the 600 kg limit. The vehicle failed to reach orbit and the crew was lost."));

                MissionReport report = session.BuildReport();
                Assert.That(report.Outcome, Is.EqualTo(MissionOutcome.Lost));
                Assert.That(report.OutcomeReason, Is.EqualTo(session.State.OutcomeReason), "the Mission Record shows the reason");
                Assert.That(report.TryGetSection(MissionReportSections.Outcome, out MissionReportSection outcome), Is.True);
                bool found = false;
                foreach (MissionReportLine line in outcome.Lines)
                {
                    if (line.Label != "Cause") continue;
                    found = true;
                    Assert.That(line.Value, Does.StartWith("Launch overweight:"));
                }

                Assert.That(found, Is.True);
            }
        }

        [Test]
        public void AWithinLimitLaunch_IsUnchanged()
        {
            using (MissionSession session = Fixtures.LaunchedSession(AuroraMission.RedundancyPresetId))
            {
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Intro));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
                Assert.That(session.Director.LossCause, Is.EqualTo(SurvivalFailure.None));
            }
        }

        [Test]
        public void OverweightAndOverBudget_IsStillRefused()
        {
            using (MissionSession session = Fixtures.NewSession(AuroraMission.RedundancyPresetId))
            {
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 5).Succeeded, Is.True);
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SolarWing, 2).Succeeded, Is.True);

                CommandResult launch = session.Planner.TryLaunch();

                Assert.That(launch.Succeeded, Is.False, "budget stays a hard limit");
                Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Planning));
            }
        }

        [Test]
        public void AnOverweightDraftAtTheDeadline_StillLaunchesTheLastValidLoadout()
        {
            using (MissionSession session = Fixtures.NewSession(AuroraMission.RedundancyPresetId))
            {
                Assert.That(session.PlanningDeadline.TryEnable(30d).Succeeded, Is.True);
                Assert.That(session.Planner.TrySetEquipment(AuroraMission.SparePartsKit, 5).Succeeded, Is.True);
                Assert.That(session.Planner.DraftConfiguration.TotalMassKg, Is.GreaterThan(600d));

                session.Tick(30d);

                Assert.That(session.PlanningDeadline.UsedLastValidLoadoutAtDeadline, Is.True);
                Assert.That(session.Planner.LaunchedConfiguration.TotalMassKg, Is.LessThanOrEqualTo(600d));
                Assert.That(session.State.Outcome, Is.EqualTo(MissionOutcome.None));
            }
        }
    }
}
