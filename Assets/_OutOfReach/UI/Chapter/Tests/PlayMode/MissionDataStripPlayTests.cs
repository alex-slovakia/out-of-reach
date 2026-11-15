using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Presentation.UI.Chapter;
using OutOfReach.Simulation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OutOfReach.Tests.ChapterUi
{
    /// <summary>
    /// MISSION-DATA: the NASA/JPL Horizons line of the next-routine strip on a real UIDocument panel. The numbers are the
    /// link's own (ICommunicationSystem.ReferenceDistanceKm / OneWayLightSeconds), formatted exactly like Core formats
    /// them, and the label says whether the sample follows the mission date or is the transmitted one.
    /// ChapterUiRuntimePlayTests checks the same line against the real Core from day 1 to day 14.
    /// </summary>
    public sealed class MissionDataStripPlayTests
    {
        private const string Day1Line = "Earth 197,708,000 km · signal 10 min 59 s one way";
        private const string Day2Line = "Earth 196,434,000 km · signal 10 min 55 s one way";
        private const string TodaySource = "NASA/JPL Horizons · today's Earth–Mars range, used as a Mars-vicinity proxy";
        private const string SentSource = "NASA/JPL Horizons · Earth–Mars range when the call was sent, used as a Mars-vicinity proxy";

        private ChapterUiTestPanel ui;
        private MissionChapterPresenter presenter;
        private FakeMission mission;
        private FakeChapterFlow flow;
        private FakeLink link;
        private bool reduced;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ui = new ChapterUiTestPanel();
            presenter = ui.Presenter;
            mission = new FakeMission { Phase = GamePhase.Emergency, ElapsedSimulationSeconds = FakeMission.At(1, 7, 10) };
            flow = new FakeChapterFlow(mission);
            link = new FakeLink();
            reduced = false;
            Bind();
            presenter.ConfigureMissionData(link);
            yield return null;
        }

        [TearDown]
        public void TearDown() => ui.Destroy();

        private void Bind() => presenter.Configure(ui.Document, ui.Style, flow, mission, mission, new FakeResources(), new FakeRecovery(),
            new FakeSurvival { IsActive = true }, () => true, () => reduced);

        private void PastOpeningBeat()
        {
            presenter.Tick(.1f);
            for (float t = 0f; t < MissionChapterPresenter.BeatSeconds + .2f; t += .1f) presenter.Tick(.1f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None));
            Assert.That(ui.Shown("chapter-strip"), Is.True);
        }

        [Test]
        public void TheStrip_ShowsCoresHorizonsSample_AndFollowsItFromDayToDay()
        {
            PastOpeningBeat();
            Assert.That(ui.Shown("chapter-strip-data"), Is.True);
            Assert.That(ui.Text("chapter-strip-data"), Is.EqualTo(Day1Line));
            Assert.That(ui.Shown("chapter-strip-source"), Is.True);
            Assert.That(ui.Text("chapter-strip-source"), Is.EqualTo(TodaySource));

            // Core selects the next daily sample when the mission date passes it; the strip only repeats it.
            mission.ElapsedSimulationSeconds = FakeMission.At(2, 8);
            link.ReferenceDistanceKm = FakeLink.Day2DistanceKm;
            link.OneWayLightSeconds = FakeLink.Day2LightSeconds;
            presenter.Tick(.1f);
            Assert.That(ui.Text("chapter-strip-data"), Is.EqualTo(Day2Line));
            Assert.That(ui.Text("chapter-strip-source"), Is.EqualTo(TodaySource));

            // No clock of its own: the mission time alone changes nothing.
            mission.ElapsedSimulationSeconds = FakeMission.At(4, 8);
            presenter.Tick(.1f);
            Assert.That(ui.Text("chapter-strip-data"), Is.EqualTo(Day2Line));
        }

        [Test]
        public void TheLine_IsAlsoThereWhileTheBeatCardIsUp()
        {
            presenter.Tick(.1f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Beat));
            Assert.That(ui.Shown("chapter-strip-data"), Is.True);
            Assert.That(ui.Text("chapter-strip-data"), Is.EqualTo(Day1Line));
        }

        [Test]
        public void AfterTheCall_TheLabelSaysTheSampleIsTheTransmittedOne()
        {
            PastOpeningBeat();
            link.State = CommunicationState.Ready;
            presenter.Tick(.1f);
            Assert.That(ui.Text("chapter-strip-source"), Is.EqualTo(TodaySource));

            link.State = CommunicationState.AwaitingReply;
            presenter.Tick(.1f);
            Assert.That(ui.Text("chapter-strip-data"), Is.EqualTo(Day1Line), "Core keeps the transmitted sample");
            Assert.That(ui.Text("chapter-strip-source"), Is.EqualTo(SentSource));

            link.State = CommunicationState.ReplyReceived;
            presenter.Tick(.1f);
            Assert.That(ui.Text("chapter-strip-source"), Is.EqualTo(SentSource));
        }

        [Test]
        public void TheLine_StaysOnTheDecisionAndBeaconStrips()
        {
            PastOpeningBeat();
            flow.PendingDecision = ChapterDecision.ScienceOrSalvage;
            presenter.Tick(.1f);
            presenter.DismissCard();
            presenter.Tick(.1f);
            Assert.That(ui.Text("chapter-strip-kicker"), Is.EqualTo("DECISION PENDING"));
            Assert.That(ui.Shown("chapter-strip-data"), Is.True);
            Assert.That(ui.Text("chapter-strip-data"), Is.EqualTo(Day1Line));
        }

        [Test]
        public void WithoutALink_TheStripIsExactlyAsBefore()
        {
            presenter.ConfigureMissionData(null);
            PastOpeningBeat();
            Assert.That(ui.Shown("chapter-strip-data"), Is.False);
            Assert.That(ui.Shown("chapter-strip-source"), Is.False);
            Assert.That(ui.Text("chapter-strip-title"), Is.EqualTo("Day 5 — Inspection of the repairs and reserves"));
        }

        [Test]
        public void ANewBinding_ForgetsTheLink_UntilTheCompositionSuppliesItAgain()
        {
            PastOpeningBeat();
            Assert.That(ui.Shown("chapter-strip-data"), Is.True);
            Bind();
            PastOpeningBeat();
            Assert.That(ui.Shown("chapter-strip-data"), Is.False, "Configure clears the link of the previous session");
            presenter.ConfigureMissionData(link);
            presenter.Tick(.1f);
            Assert.That(ui.Text("chapter-strip-data"), Is.EqualTo(Day1Line));
        }

        [Test]
        public void ReducedEffects_ShowTheSameStaticText()
        {
            reduced = true;
            PastOpeningBeat();
            Assert.That(ui.Text("chapter-strip-data"), Is.EqualTo(Day1Line));
            Assert.That(ui.Text("chapter-strip-source"), Is.EqualTo(TodaySource));
            Assert.That(presenter.View.StripData.style.opacity.keyword, Is.EqualTo(StyleKeyword.Null), "no animated style on the line");
        }

        [Test]
        public void AnUnusableSample_HidesTheLine()
        {
            link.ReferenceDistanceKm = double.NaN;
            PastOpeningBeat();
            Assert.That(ui.Shown("chapter-strip-data"), Is.False);
            Assert.That(ui.Shown("chapter-strip-source"), Is.False);
        }

        [Test]
        public void AFailingLink_HidesTheLine_AndTheStripSurvives()
        {
            PastOpeningBeat();
            link.Throws = true;
            link.ReferenceDistanceKm = FakeLink.Day2DistanceKm;
            LogAssert.Expect(LogType.Exception, new Regex("fake link failure"));
            presenter.Tick(.1f);
            Assert.That(ui.Shown("chapter-strip"), Is.True, "the routine strip is still there");
            Assert.That(ui.Shown("chapter-strip-data"), Is.False);
            int reads = link.Reads;
            presenter.Tick(.1f);
            Assert.That(link.Reads, Is.EqualTo(reads), "a failed link is not asked again");
        }

        [Test]
        public void EveryCommittedSample_IsFormattedExactlyLikeCore()
        {
            EarthMarsReference reference = EarthMarsReference.Embedded;
            Assert.That(reference.Count, Is.EqualTo(32), "the committed NASA/JPL Horizons snapshot");
            string previous = null;
            foreach (EarthMarsSample sample in reference.Samples)
            {
                string expected = "Earth " + CommunicationSystem.FormatDistance(sample.DistanceKm) + " · signal "
                    + CommunicationSystem.FormatDuration(sample.OneWayLightSeconds) + " one way";
                string line = ChapterCopy.EarthLine(sample.DistanceKm, sample.OneWayLightSeconds);
                Assert.That(line, Is.EqualTo(expected), "sample " + sample.Index);
                Assert.That(line, Is.Not.EqualTo(previous), "each mission day reads differently: sample " + sample.Index);
                previous = line;
            }

            Assert.That(ChapterCopy.EarthLine(reference.Samples[0].DistanceKm, reference.Samples[0].OneWayLightSeconds), Is.EqualTo(Day1Line));
            Assert.That(ChapterCopy.EarthLine(reference.Samples[1].DistanceKm, reference.Samples[1].OneWayLightSeconds), Is.EqualTo(Day2Line));
        }
    }
}
