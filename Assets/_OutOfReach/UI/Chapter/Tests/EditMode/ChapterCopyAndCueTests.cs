using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Presentation.UI.Chapter;

namespace OutOfReach.Tests.ChapterUi
{
    /// <summary>CHAPTER-UI-01 pure rules: Core's day rule, real stop times, one cue per beat / dispatch Id / segment.</summary>
    public sealed class ChapterCopyAndCueTests
    {
        private FakeMission mission;
        private FakeChapterFlow flow;
        private ChapterCueObserver observer;

        [SetUp]
        public void SetUp()
        {
            mission = new FakeMission();
            flow = new FakeChapterFlow(mission);
            observer = new ChapterCueObserver();
        }

        private void Observe(bool active = true, bool ended = false, RecoveryStage stage = RecoveryStage.NotStarted, double? arrival = null) =>
            observer.Observe(flow, active, ended, mission.ElapsedSimulationSeconds, stage, arrival);

        [Test]
        public void DayAndClock_FollowCoresCalendar_AndNeverRoundUp()
        {
            Assert.That(ChapterCopy.DayAt(0d), Is.EqualTo(1));
            Assert.That(ChapterCopy.DayAt(86399.9d), Is.EqualTo(1));
            Assert.That(ChapterCopy.DayAt(86400d), Is.EqualTo(2));
            Assert.That(ChapterCopy.DayAt(FakeMission.At(5, 8)), Is.EqualTo(5));
            Assert.That(ChapterCopy.ClockAt(FakeMission.At(4, 13, 42, 59.9d)), Is.EqualTo("13:42"));
            Assert.That(ChapterCopy.DayAndClock(FakeMission.At(14, 8)), Is.EqualTo("Day 14, 08:00"));
            Assert.That(ChapterCopy.Span(FakeMission.At(5, 8) - FakeMission.At(2, 8, 30)), Is.EqualTo("2 d 23 h"));
            Assert.That(ChapterCopy.Span(5 * 3600 + 12 * 60 + 59), Is.EqualTo("5 h 12 min"));
            Assert.That(ChapterCopy.Transit(832.4d), Is.EqualTo("13 min 52 s"));
            Assert.That(ChapterCopy.CalendarDaysCrossed(FakeMission.At(2, 8, 30), FakeMission.At(5, 8)), Is.EqualTo(3),
                "day 2 to day 5 is three days of life, never one three-day sleep");
            Assert.That(ChapterCopy.Sentence("the food store is empty"), Is.EqualTo("The food store is empty."));
            Assert.That(ChapterCopy.Delta(-4.54d, "0.0"), Is.EqualTo("−4.5"));
            Assert.That(ChapterCopy.Delta(0.01d, "0.0"), Is.EqualTo("±0"));
            Assert.That(ChapterCopy.Tally(1, 3, 1), Is.EqualTo("1 meal · 3 water portions · 1 night of ordinary sleep"));
        }

        [Test]
        public void EveryDecision_HasItsCoreChoices_AndPhysicalOptionsNameTheExistingInteraction()
        {
            ChapterOptionCopy[] science = ChapterCopy.Options(ChapterDecision.ScienceOrSalvage);
            Assert.That(science[0].Choice, Is.EqualTo(ChapterChoice.PreserveScience));
            Assert.That(science[1].Kind, Is.EqualTo(ChapterOptionKind.Physical));
            ChapterOptionCopy[] grow = ChapterCopy.Options(ChapterDecision.GrowOrConserve);
            Assert.That(grow[0].Choice, Is.EqualTo(ChapterChoice.ConserveReserves));
            Assert.That(grow[1].Kind, Is.EqualTo(ChapterOptionKind.Physical));
            Assert.That(ChapterCopy.Options(ChapterDecision.AcknowledgeRescueFailure)[0].Choice, Is.EqualTo(ChapterChoice.AcknowledgeRescueFailure));
            ChapterOptionCopy[] last = ChapterCopy.Options(ChapterDecision.GrowOrBeacon);
            Assert.That(last[0].Choice, Is.EqualTo(ChapterChoice.StopGrowForBeacon));
            Assert.That(last[1].Choice, Is.EqualTo(ChapterChoice.KeepGrowRunning));
            foreach (MissionChapter chapter in new[] { MissionChapter.Accident, MissionChapter.Consequences, MissionChapter.Contact,
                MissionChapter.RescueFailed, MissionChapter.FinalDecision, MissionChapter.Rendezvous })
            {
                Assert.That(ChapterCopy.BeatTitle(chapter), Is.Not.Empty, chapter.ToString());
                Assert.That(ChapterCopy.BeatLine(chapter, false), Is.Not.Empty, chapter.ToString());
            }

            Assert.That(ChapterCopy.BeatDays(MissionChapter.Accident, 1), Is.EqualTo("MISSION DAYS 1–2"));
            Assert.That(ChapterCopy.BeatDays(MissionChapter.RescueFailed, 15), Is.EqualTo("MISSION DAY 15"), "a late message shows its actual day");
        }

        [Test]
        public void OpeningBeat_WaitsForTheChapterToBecomeActive_AndIsQueuedOnce()
        {
            Observe(active: false);
            Assert.That(observer.Pending, Is.Empty, "nothing during planning or the impact");
            mission.ElapsedSimulationSeconds = 300d;
            Observe();
            Observe();
            flow.Revision++;
            Observe();
            Assert.That(observer.Pending.Count, Is.EqualTo(1));
            Assert.That(observer.Peek().Kind, Is.EqualTo(ChapterCueKind.BeatCard));
            Assert.That(observer.Peek().Chapter, Is.EqualTo(MissionChapter.Accident));
        }

        [Test]
        public void ARoutineThatReachesTheBeat_GivesOneBeatCardWithItsRealSummary_NotTwoCards()
        {
            mission.ElapsedSimulationSeconds = FakeMission.At(2, 8, 30);
            Observe();
            observer.Dequeue();
            flow.StartRoutine();
            Observe();
            mission.ElapsedSimulationSeconds = FakeMission.At(5, 8);
            flow.RoutineRations = 9;
            flow.RoutineDrinks = 26;
            flow.RoutineRests = 3;
            flow.Chapter = MissionChapter.Consequences;
            flow.PendingDecision = ChapterDecision.ScienceOrSalvage;
            flow.StopRoutine(RoutineStopReason.ReachedChapter, "reached the inspection of the repairs and reserves");
            Observe();
            Observe();

            Assert.That(observer.Pending.Count, Is.EqualTo(1));
            ChapterCue beat = observer.Peek();
            Assert.That(beat.Kind, Is.EqualTo(ChapterCueKind.BeatCard));
            Assert.That(beat.Chapter, Is.EqualTo(MissionChapter.Consequences));
            Assert.That(beat.Day, Is.EqualTo(5));
            Assert.That(beat.HasRoutineSummary, Is.True);
            Assert.That(beat.RoutineStartedAt, Is.EqualTo(FakeMission.At(2, 8, 30)));
            Assert.That(beat.RoutineStoppedAt, Is.EqualTo(FakeMission.At(5, 8)));
            Assert.That(beat.Rations, Is.EqualTo(9));
        }

        [Test]
        public void ADangerStop_KeepsCoresActualStopTime_NotTheTarget()
        {
            Observe();
            observer.Dequeue();
            mission.ElapsedSimulationSeconds = FakeMission.At(2, 9);
            flow.StartRoutine();
            Observe();
            double stoppedAt = FakeMission.At(4, 13, 42, 30d);
            mission.ElapsedSimulationSeconds = stoppedAt;
            flow.RoutineRations = 6;
            flow.StopRoutine(RoutineStopReason.Danger, "cabin CO2 is close to the loss limit");
            mission.ElapsedSimulationSeconds = stoppedAt + 7d;
            Observe();

            ChapterCue report = observer.Dequeue();
            Assert.That(report.Kind, Is.EqualTo(ChapterCueKind.StopReport));
            Assert.That(report.RoutineStoppedAt, Is.EqualTo(stoppedAt), "the committed stop time, never rounded to day 5 08:00");
            Assert.That(report.Day, Is.EqualTo(4));
            Assert.That(report.Stop, Is.EqualTo(RoutineStopReason.Danger));
            Assert.That(report.Rations, Is.EqualTo(6));
            Observe();
            Assert.That(observer.Pending, Is.Empty, "one report per segment");
        }

        [Test]
        public void ASegmentThatStartsAndStopsInOneFrame_IsStillReported()
        {
            Observe();
            observer.Dequeue();
            flow.StartRoutine();
            flow.StopRoutine(RoutineStopReason.PrerequisiteMissing, "the night's rest could not begin");
            Observe();
            Assert.That(observer.Peek()?.Kind, Is.EqualTo(ChapterCueKind.StopReport));
        }

        [Test]
        public void TheDispatch_IsCuedOncePerStableId_NotPerRevision_AndAgainInANewSession()
        {
            Observe();
            observer.Dequeue();
            mission.ElapsedSimulationSeconds = FakeMission.At(14, 8);
            flow.LatestDispatch = new MissionDispatch(ChapterCopy.RescueFailedDispatchId, ChapterCopy.RescueFailedContentId,
                FakeMission.At(14, 7, 46, 8d), FakeMission.At(14, 8));
            flow.Rescue = RescuePlan.FirstRescueFailed;
            Observe();
            flow.Revision += 5;
            Observe();
            Assert.That(observer.Pending.Count, Is.EqualTo(1));
            ChapterCue message = observer.Dequeue();
            Assert.That(message.Kind, Is.EqualTo(ChapterCueKind.FailureDispatch));
            Assert.That(message.ReceivedAt - message.TransmittedAt, Is.EqualTo(832d).Within(1e-6), "light time from Core's own times");

            observer.Reset();
            mission.ElapsedSimulationSeconds = 10d;
            flow.LatestDispatch = default;
            flow.Rescue = RescuePlan.None;
            Observe();
            observer.Dequeue();
            flow.LatestDispatch = new MissionDispatch(ChapterCopy.RescueFailedDispatchId, ChapterCopy.RescueFailedContentId, 1d, 2d);
            Observe();
            Assert.That(observer.Peek()?.Kind, Is.EqualTo(ChapterCueKind.FailureDispatch), "a restarted mission shows its own message");
        }

        [Test]
        public void ConcreteHope_NamesCoresScheduledReportDay_AndTheCommittedCraftItsDockingDay()
        {
            Observe();
            observer.Dequeue();
            flow.Chapter = MissionChapter.Contact;
            flow.NextChapter = MissionChapter.RescueFailed;
            flow.NextChapterAtSimulationSeconds = FakeMission.At(14, 8);
            flow.Rescue = RescuePlan.FirstRescuePlanned;
            Observe();
            ChapterCue beat = observer.Dequeue();
            Assert.That(beat.Kind, Is.EqualTo(ChapterCueKind.BeatCard));
            ChapterCue hope = observer.Dequeue();
            Assert.That(hope.Kind, Is.EqualTo(ChapterCueKind.HopeMessage));
            Assert.That(hope.ReportDay, Is.EqualTo(14));
            Assert.That(ChapterCopy.HopeText(hope.ReportDay), Does.Contain("about mission day 14"));

            flow.Rescue = RescuePlan.InTransitCraftCommitted;
            Observe(arrival: FakeMission.At(30, 18));
            ChapterCue plan = observer.Dequeue();
            Assert.That(plan.Kind, Is.EqualTo(ChapterCueKind.RescueCommitted));
            Assert.That(plan.DockDay, Is.EqualTo(30));
        }

        [Test]
        public void MissionEnd_ClearsEverythingQueued_AndNothingFollows()
        {
            Observe();
            flow.Chapter = MissionChapter.Consequences;
            flow.PendingDecision = ChapterDecision.ScienceOrSalvage;
            Observe();
            Assert.That(observer.Pending.Count, Is.EqualTo(2));
            Observe(ended: true);
            Assert.That(observer.Pending, Is.Empty);
            flow.Chapter = MissionChapter.Contact;
            Observe();
            Assert.That(observer.Pending, Is.Empty, "a decided mission shows no further chapter card");
        }

        [Test]
        public void BindingMidSession_ReplaysNothing_ButLaterChangesAreCued()
        {
            flow.Chapter = MissionChapter.FinalDecision;
            flow.RoutineStartedAtSimulationSeconds = FakeMission.At(14, 9);
            flow.RoutineStoppedAtSimulationSeconds = FakeMission.At(20, 8);
            flow.LastRoutineStop = RoutineStopReason.ReachedChapter;
            flow.LatestDispatch = new MissionDispatch(ChapterCopy.RescueFailedDispatchId, ChapterCopy.RescueFailedContentId, 1d, 2d);
            flow.Rescue = RescuePlan.InTransitCraftCommitted;
            mission.ElapsedSimulationSeconds = FakeMission.At(20, 9);
            Observe();
            Assert.That(observer.Pending, Is.Empty);
            flow.Chapter = MissionChapter.Rendezvous;
            Observe();
            Assert.That(observer.Peek()?.Chapter, Is.EqualTo(MissionChapter.Rendezvous));
        }

        [Test]
        public void AMissedBeaconWindow_IsCuedOnce()
        {
            Observe(stage: RecoveryStage.BeaconRequested);
            observer.Dequeue();
            Observe(stage: RecoveryStage.Delayed, arrival: FakeMission.At(33, 18));
            Observe(stage: RecoveryStage.Delayed, arrival: FakeMission.At(33, 18));
            Assert.That(observer.Pending.Count, Is.EqualTo(1));
            Assert.That(observer.Peek().Kind, Is.EqualTo(ChapterCueKind.BeaconDelayed));
            Assert.That(observer.Peek().DockDay, Is.EqualTo(33));
        }
    }
}
