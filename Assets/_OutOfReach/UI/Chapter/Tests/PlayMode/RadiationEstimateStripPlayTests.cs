using System.Collections;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Presentation.UI.Chapter;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.ChapterUi
{
    /// <summary>
    /// MISSION-DATA (optional line): the strip's radiation estimate follows the one mission clock and nothing else. It is
    /// display only: the presenter issues no command for it and it needs no link.
    /// </summary>
    public sealed class RadiationEstimateStripPlayTests
    {
        private ChapterUiTestPanel ui;
        private MissionChapterPresenter presenter;
        private FakeMission mission;
        private FakeChapterFlow flow;
        private FakeResources resources;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ui = new ChapterUiTestPanel();
            presenter = ui.Presenter;
            mission = new FakeMission { Phase = GamePhase.Emergency, ElapsedSimulationSeconds = FakeMission.At(1, 12) };
            flow = new FakeChapterFlow(mission);
            resources = new FakeResources();
            presenter.Configure(ui.Document, ui.Style, flow, mission, mission, resources, new FakeRecovery(),
                new FakeSurvival { IsActive = true }, () => true);
            yield return null;
        }

        [TearDown]
        public void TearDown() => ui.Destroy();

        private void PastOpeningBeat()
        {
            presenter.Tick(.1f);
            for (float t = 0f; t < MissionChapterPresenter.BeatSeconds + .2f; t += .1f) presenter.Tick(.1f);
            Assert.That(ui.Shown("chapter-strip"), Is.True);
        }

        [Test]
        public void TheStrip_ShowsTheEstimate_AndItGrowsWithTheMissionClock()
        {
            PastOpeningBeat();
            Assert.That(ui.Shown("chapter-strip-dose"), Is.True);
            Assert.That(ui.Text("chapter-strip-dose"),
                Is.EqualTo("Radiation since Day 1 ≈ 0.9 mSv (est. at 1.8 mSv/day, NASA MSL RAD cruise measurement)"));

            mission.ElapsedSimulationSeconds = FakeMission.At(15, 0);
            presenter.Tick(.1f);
            Assert.That(ui.Text("chapter-strip-dose"),
                Is.EqualTo("Radiation since Day 1 ≈ 25 mSv (est. at 1.8 mSv/day, NASA MSL RAD cruise measurement)"));
        }

        [Test]
        public void TheEstimate_IsDisplayOnly()
        {
            ResourceSnapshot before = resources.Snapshot;
            PastOpeningBeat();
            mission.ElapsedSimulationSeconds = FakeMission.At(20, 0);
            for (int i = 0; i < 20; i++) presenter.Tick(.1f);
            Assert.That(flow.ContinueCalls, Is.Zero);
            Assert.That(flow.CancelCalls, Is.Zero);
            Assert.That(flow.Chosen, Is.Empty);
            Assert.That(resources.Snapshot, Is.EqualTo(before));
            Assert.That(mission.Outcome, Is.EqualTo(MissionOutcome.None));
        }

        [Test]
        public void TheEstimate_NeedsNoLink_AndSitsUnderTheHorizonsLine()
        {
            PastOpeningBeat();
            Assert.That(ui.Shown("chapter-strip-data"), Is.False, "no link was supplied");
            Assert.That(ui.Shown("chapter-strip-dose"), Is.True);

            presenter.ConfigureMissionData(new FakeLink());
            presenter.Tick(.1f);
            Assert.That(ui.Text("chapter-strip-data"), Is.EqualTo("Earth 197,708,000 km · signal 10 min 59 s one way"));
            Assert.That(presenter.View.Strip.IndexOf(presenter.View.StripDose), Is.GreaterThan(presenter.View.Strip.IndexOf(presenter.View.StripSource)));
        }
    }
}
