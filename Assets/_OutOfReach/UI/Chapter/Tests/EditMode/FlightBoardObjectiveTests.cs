using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Presentation.UI;
using UnityEngine.UIElements;

namespace OutOfReach.Tests.ChapterUi
{
    /// <summary>
    /// CHAPTER-UI-02 on root's real FlightBoardView: the chapter loop's next step replaces the phase-only HUD objective only
    /// after the interior repairs and in Survival; every other phase and the outcome keep root's copy.
    /// </summary>
    public sealed class FlightBoardObjectiveTests
    {
        [Test]
        public void TheChapterStep_ReplacesThePhaseCopy_OnlyAfterTheRepairsAndInSurvival()
        {
            var host = new VisualElement();
            using (var view = new FlightBoardView(host, null))
            {
                Label phase = host.Q<Label>(className: "hud-phase");
                Label objective = host.Q<Label>(className: "hud-objective");
                view.SetPhase(GamePhase.Repair, MissionOutcome.None, true);
                Assert.That(phase.text, Is.EqualTo("Prepare for a spacewalk"), "root's copy before any override");

                view.SetObjectiveOverride("Get through the first days", "Prepare and eat the first stored ration first.");
                Assert.That(phase.text, Is.EqualTo("Get through the first days"), "applied at once in the current phase");
                Assert.That(objective.text, Is.EqualTo("Prepare and eat the first stored ration first."));

                view.SetPhase(GamePhase.Repair, MissionOutcome.None, false);
                Assert.That(phase.text, Is.EqualTo("Restore power and life support"), "pending repairs keep root's copy");
                view.SetPhase(GamePhase.Emergency, MissionOutcome.None, false);
                Assert.That(phase.text, Is.EqualTo("Restore power"));
                Assert.That(phase.ClassListContains("constraint-exceeded"), Is.True, "Emergency keeps its red emphasis");
                view.SetPhase(GamePhase.Eva, MissionOutcome.None, true);
                Assert.That(phase.text, Is.EqualTo("Repair the antenna"));
                view.SetPhase(GamePhase.Transmission, MissionOutcome.None, true);
                Assert.That(phase.text, Is.EqualTo("Restore contact with Earth"));

                view.SetPhase(GamePhase.Survival, MissionOutcome.None, false);
                Assert.That(phase.text, Is.EqualTo("Get through the first days"));
                view.SetPhase(GamePhase.Outcome, MissionOutcome.Lost, false);
                Assert.That(phase.text, Is.EqualTo("Mission lost"), "the outcome is never overridden");

                view.SetPhase(GamePhase.Survival, MissionOutcome.None, false);
                view.SetObjectiveOverride(null, null);
                Assert.That(phase.text, Is.EqualTo("Survive until rescue"), "no chapter step restores the phase copy");
                Assert.That(objective.text, Is.Empty);
            }
        }
    }
}
