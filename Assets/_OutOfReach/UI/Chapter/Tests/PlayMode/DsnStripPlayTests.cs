using System;
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Presentation.UI.Chapter;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.ChapterUi
{
    /// <summary>
    /// MISSION-DATA: the Deep Space Network line of the next-routine strip on a real UIDocument panel. It follows the
    /// mission date the composition supplies, in ten-minute steps, and hides outside the committed Horizons snapshot.
    /// ChapterUiRuntimePlayTests checks it against the real mission calendar.
    /// </summary>
    public sealed class DsnStripPlayTests
    {
        private const string Madrid = "DSN: Madrid has Mars in view · el. 59° (line of sight)";
        private const string Goldstone = "DSN: Goldstone has Mars in view · el. 69° (line of sight)";

        private ChapterUiTestPanel ui;
        private MissionChapterPresenter presenter;
        private FakeMission mission;
        private FakeChapterFlow flow;
        private FakeLink link;
        private DateTime? date;
        private bool dateThrows;
        private int dateReads;

        private static DateTime Utc(int month, int day, int hour, int minute = 0) => new DateTime(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ui = new ChapterUiTestPanel();
            presenter = ui.Presenter;
            mission = new FakeMission { Phase = GamePhase.Emergency, ElapsedSimulationSeconds = FakeMission.At(1, 7, 10) };
            flow = new FakeChapterFlow(mission);
            link = new FakeLink();
            date = Utc(11, 14, 8);
            dateThrows = false;
            dateReads = 0;
            presenter.Configure(ui.Document, ui.Style, flow, mission, mission, new FakeResources(), new FakeRecovery(),
                new FakeSurvival { IsActive = true }, () => true);
            presenter.ConfigureMissionData(link, MissionDate);
            yield return null;
        }

        [TearDown]
        public void TearDown() => ui.Destroy();

        private DateTime? MissionDate()
        {
            dateReads++;
            if (dateThrows) throw new InvalidOperationException("fake calendar failure");
            return date;
        }

        private void PastOpeningBeat()
        {
            presenter.Tick(.1f);
            for (float t = 0f; t < MissionChapterPresenter.BeatSeconds + .2f; t += .1f) presenter.Tick(.1f);
            Assert.That(ui.Shown("chapter-strip"), Is.True);
        }

        [Test]
        public void TheStrip_NamesTheComplexThatHasMarsInView_AndFollowsTheMissionDate()
        {
            PastOpeningBeat();
            Assert.That(ui.Shown("chapter-strip-dsn"), Is.True);
            Assert.That(ui.Text("chapter-strip-dsn"), Is.EqualTo(Madrid));

            date = Utc(11, 14, 8, 9);
            presenter.Tick(.1f);
            Assert.That(ui.Text("chapter-strip-dsn"), Is.EqualTo(Madrid), "held for the ten-minute step");

            date = Utc(11, 14, 14);
            presenter.Tick(.1f);
            Assert.That(ui.Text("chapter-strip-dsn"), Is.EqualTo(Goldstone));

            date = Utc(11, 14, 10);
            presenter.Tick(.1f);
            Assert.That(ui.Text("chapter-strip-dsn"), Is.EqualTo("DSN: Madrid + Goldstone have Mars in view (line of sight)"), "the handover overlap");
        }

        [Test]
        public void TheLine_SitsBetweenTheHorizonsNumbersAndTheirSource()
        {
            PastOpeningBeat();
            VisualElementOrder(out int data, out int dsn, out int source);
            Assert.That(dsn, Is.GreaterThan(data));
            Assert.That(source, Is.GreaterThan(dsn));
            Assert.That(ui.Text("chapter-strip-data"), Is.EqualTo("Earth 197,708,000 km · signal 10 min 59 s one way"));
        }

        private void VisualElementOrder(out int data, out int dsn, out int source)
        {
            data = presenter.View.Strip.IndexOf(presenter.View.StripData);
            dsn = presenter.View.Strip.IndexOf(presenter.View.StripDsn);
            source = presenter.View.Strip.IndexOf(presenter.View.StripSource);
        }

        [Test]
        public void OutsideTheSnapshot_TheLineIsHidden_AndTheRestStays()
        {
            date = Utc(12, 16, 8);
            PastOpeningBeat();
            Assert.That(ui.Shown("chapter-strip-dsn"), Is.False);
            Assert.That(ui.Shown("chapter-strip-data"), Is.True);

            date = Utc(12, 1, 10, 40);
            presenter.Tick(.1f);
            Assert.That(ui.Shown("chapter-strip-dsn"), Is.True);
            Assert.That(ui.Text("chapter-strip-dsn"), Is.EqualTo(ChapterCopy.DsnLine(Utc(12, 1, 10, 40))));
        }

        [Test]
        public void WithoutAMissionDate_TheLineIsHidden()
        {
            date = null;
            PastOpeningBeat();
            Assert.That(ui.Shown("chapter-strip-dsn"), Is.False, "no session date yet");

            presenter.ConfigureMissionData(link);
            presenter.Tick(.1f);
            Assert.That(ui.Shown("chapter-strip-dsn"), Is.False, "the composition supplied no date");
            Assert.That(ui.Text("chapter-strip-data"), Is.EqualTo("Earth 197,708,000 km · signal 10 min 59 s one way"));
        }

        [Test]
        public void AFailingCalendar_HidesOnlyTheDsnLine()
        {
            PastOpeningBeat();
            dateThrows = true;
            LogAssert.Expect(LogType.Exception, new Regex("fake calendar failure"));
            presenter.Tick(.1f);
            Assert.That(ui.Shown("chapter-strip"), Is.True);
            Assert.That(ui.Shown("chapter-strip-dsn"), Is.False);
            Assert.That(ui.Shown("chapter-strip-data"), Is.True, "the Horizons line is independent");
            int reads = dateReads;
            presenter.Tick(.1f);
            Assert.That(dateReads, Is.EqualTo(reads), "a failed calendar is not asked again");
        }

        [Test]
        public void AFailingLink_KeepsTheDsnLine()
        {
            PastOpeningBeat();
            link.Throws = true;
            link.ReferenceDistanceKm = FakeLink.Day2DistanceKm;
            LogAssert.Expect(LogType.Exception, new Regex("fake link failure"));
            presenter.Tick(.1f);
            Assert.That(ui.Shown("chapter-strip-data"), Is.False);
            Assert.That(ui.Shown("chapter-strip-dsn"), Is.True);
            Assert.That(ui.Text("chapter-strip-dsn"), Is.EqualTo(Madrid));
        }
    }
}
