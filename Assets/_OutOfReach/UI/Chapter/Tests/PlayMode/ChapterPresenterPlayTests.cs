using System.Collections;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Presentation.UI.Chapter;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.ChapterUi
{
    /// <summary>
    /// CHAPTER-UI-01 on a real UIDocument panel with a scripted IMissionChapterFlow: every state of the approved loop is
    /// shown, every option issues exactly Core's own command, and pause, the presentation gate, restart and mission loss
    /// interrupt safely. The scripted flow stands in for Core; ChapterUiRuntimePlayTests drives the real one.
    /// </summary>
    public sealed class ChapterPresenterPlayTests
    {
        private ChapterUiTestPanel ui;
        private MissionChapterPresenter presenter;
        private FakeMission mission;
        private FakeChapterFlow flow;
        private FakeResources resources;
        private FakeRecovery recovery;
        private FakeSurvival survival;
        private bool gate;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ui = new ChapterUiTestPanel();
            presenter = ui.Presenter;
            mission = new FakeMission { Phase = GamePhase.Emergency, ElapsedSimulationSeconds = FakeMission.At(1, 7, 10) };
            flow = new FakeChapterFlow(mission);
            resources = new FakeResources();
            recovery = new FakeRecovery();
            survival = new FakeSurvival { IsActive = true };
            gate = true;
            Bind();
            yield return null;
        }

        [TearDown]
        public void TearDown() => ui.Destroy();

        private void Bind() => presenter.Configure(ui.Document, ui.Style, flow, mission, mission, resources, recovery, survival, () => gate);

        private void Tick(float seconds = .1f) => presenter.Tick(seconds);

        private void TickFor(float seconds)
        {
            for (float t = 0f; t < seconds; t += .1f) Tick(.1f);
        }

        /// <summary>Opening beat shown and done; the routine strip is next.</summary>
        private void PastOpeningBeat()
        {
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Beat));
            TickFor(MissionChapterPresenter.BeatSeconds + .2f);
        }

        [Test]
        public void TheOpeningBeat_ShowsDays1To2_ThenTheStripExplainsCoresRefusal()
        {
            mission.Phase = GamePhase.Planning;
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None), "nothing before the accident");
            mission.Phase = GamePhase.Emergency;
            flow.ContinueCheck = new CommandResult(false, CommandFailure.PreconditionsNotMet, "Prepare and eat the first stored ration first.");
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Beat));
            Assert.That(ui.Text("chapter-beat-days"), Is.EqualTo("MISSION DAYS 1–2"));
            Assert.That(ui.Text("chapter-beat-title"), Is.EqualTo("THE ACCIDENT"));
            Assert.That(ui.Text("chapter-beat-line"), Does.Contain("first stored meal"));
            Assert.That(presenter.BlocksPlayerControl, Is.False, "a title card never takes control");
            TickFor(MissionChapterPresenter.BeatSeconds + .2f);

            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None));
            Assert.That(ui.Shown("chapter-strip"), Is.True);
            Assert.That(ui.Text("chapter-strip-title"), Is.EqualTo("Day 5 — Inspection of the repairs and reserves"));
            Assert.That(ui.Text("chapter-strip-detail"), Is.EqualTo("Prepare and eat the first stored ration first."));
            Assert.That(presenter.View.StripAction.enabledSelf, Is.False);
            Assert.That(presenter.RequestContinueRoutine().Succeeded, Is.False);
            Assert.That(flow.ContinueCalls, Is.EqualTo(1), "the request is Core's, which refuses it");
            Assert.That(flow.IsRoutineRunning, Is.False);
        }

        [Test]
        public void TheRoutine_ShowsExplicitDays_CoresTally_LiveReserves_AndStopsThroughCore()
        {
            PastOpeningBeat();
            mission.Phase = GamePhase.Repair;
            mission.ElapsedSimulationSeconds = FakeMission.At(2, 8, 30);
            Tick();
            Assert.That(presenter.View.StripAction.enabledSelf, Is.True);
            Assert.That(ui.Text("chapter-strip-detail"), Does.StartWith("2 d 23 h of routine"));
            Assert.That(presenter.RequestContinueRoutine().Succeeded, Is.True);
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Montage));
            Assert.That(ui.Shown("chapter-strip"), Is.False);
            Assert.That(ui.Text("chapter-montage-kicker"), Is.EqualTo("ROUTINE · DAY 2 → DAY 5 · 3 CALENDAR DAYS OF ORDINARY LIFE"));
            Assert.That(ui.Text("chapter-montage-day"), Is.EqualTo("DAY 2"));
            Assert.That(ui.Text("chapter-montage-elapsed"), Does.Contain("Not hibernation"));
            Assert.That(presenter.View.Ruler.childCount, Is.EqualTo(2 + 3), "fill, marker and one tick per new calendar day (3, 4, 5)");

            mission.ElapsedSimulationSeconds = FakeMission.At(3, 7, 15);
            flow.RoutineRations = 3;
            flow.RoutineDrinks = 8;
            flow.RoutineRests = 1;
            resources.Snapshot = new ResourceSnapshot(40.6d, 113.1d, 38.7d, 3.95d, 3, 960d);
            recovery.IsResting = false;
            Tick();
            Assert.That(ui.Text("chapter-montage-day"), Is.EqualTo("DAY 3"));
            Assert.That(ui.Text("chapter-montage-clock"), Is.EqualTo("07:15"));
            Assert.That(ui.Text("chapter-tally-meals"), Is.EqualTo("3"));
            Assert.That(ui.Text("chapter-tally-water"), Is.EqualTo("8"));
            Assert.That(ui.Text("chapter-tally-nights"), Is.EqualTo("1"));
            Assert.That(ui.Text("chapter-reserve-value-0"), Is.EqualTo("38.7 kg"));
            Assert.That(ui.Text("chapter-reserve-delta-0"), Is.EqualTo("−1.3"), "food delta since the routine began");
            Assert.That(ui.Text("chapter-reserve-value-3"), Is.EqualTo("3.95 kWh"));
            Assert.That(presenter.MontageFeed, Has.Some.Contains("Breakfast · one stored ration eaten"));
            Assert.That(presenter.MontageFeed, Has.Some.EqualTo("— Day 3 —"));
            Assert.That(presenter.RoutineOwnsRestPresentation, Is.True);

            Assert.That(presenter.RequestStopRoutine().Succeeded, Is.True);
            Assert.That(flow.CancelCalls, Is.EqualTo(1));
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Report));
            Assert.That(ui.Text("chapter-report-kicker"), Is.EqualTo("ROUTINE STOPPED BY THE CREW"));
            Assert.That(ui.Text("chapter-report-time"), Is.EqualTo("Stopped at Day 3, 07:15 (mission clock)"));
            Assert.That(presenter.RoutineOwnsRestPresentation, Is.False);
        }

        [Test]
        public void ADangerStop_ReportsCoresActualStopTimeAndReason_InRed()
        {
            PastOpeningBeat();
            mission.ElapsedSimulationSeconds = FakeMission.At(2, 9);
            flow.StartRoutine();
            Tick();
            mission.ElapsedSimulationSeconds = FakeMission.At(4, 13, 42, 30d);
            flow.RoutineRations = 6;
            flow.RoutineDrinks = 17;
            flow.RoutineRests = 2;
            flow.StopRoutine(RoutineStopReason.Danger, "cabin CO2 is close to the loss limit");
            mission.ElapsedSimulationSeconds += 2d;
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Report));
            Assert.That(ui.Text("chapter-report-kicker"), Is.EqualTo("ROUTINE STOPPED · DANGER"));
            Assert.That(ui.Text("chapter-report-time"), Is.EqualTo("Stopped at Day 4, 13:42 (mission clock)"));
            Assert.That(ui.Text("chapter-report-reason"), Is.EqualTo("Cabin CO2 is close to the loss limit."));
            Assert.That(ui.Text("chapter-report-summary"), Is.EqualTo("After 2 d 4 h of routine: 6 meals · 17 water portions · 2 nights of ordinary sleep."));
            Assert.That(presenter.View.Report.ClassListContains("chapter-tone-danger"), Is.True);
            TickFor(MissionChapterPresenter.DangerReportSeconds + .2f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None));
        }

        [Test]
        public void Day5_BeatThenScienceDecision_KeepIssuesPreserveScience_SalvageSendsThePlayerToTheRack()
        {
            PastOpeningBeat();
            mission.Phase = GamePhase.Repair;
            mission.ElapsedSimulationSeconds = FakeMission.At(2, 8, 30);
            flow.StartRoutine();
            Tick();
            mission.ElapsedSimulationSeconds = FakeMission.At(5, 8);
            flow.RoutineRations = 9;
            flow.RoutineDrinks = 26;
            flow.RoutineRests = 3;
            flow.Chapter = MissionChapter.Consequences;
            flow.NextChapter = MissionChapter.Contact;
            flow.NextChapterAtSimulationSeconds = FakeMission.At(9, 8);
            flow.PendingDecision = ChapterDecision.ScienceOrSalvage;
            flow.StopRoutine(RoutineStopReason.ReachedChapter, "reached the inspection of the repairs and reserves");
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Beat));
            Assert.That(ui.Text("chapter-beat-title"), Is.EqualTo("CONSEQUENCES"));
            Assert.That(ui.Text("chapter-beat-summary"), Is.EqualTo("After 2 d 23 h of routine: 9 meals · 26 water portions · 3 nights of ordinary sleep."));
            Assert.That(ui.Shown("chapter-strip"), Is.True, "the strip already says a decision is pending");
            Assert.That(ui.Text("chapter-strip-kicker"), Is.EqualTo("DECISION PENDING"));
            TickFor(MissionChapterPresenter.BeatSeconds + .2f);

            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Decision));
            Assert.That(presenter.BlocksPlayerControl, Is.True, "the card frees the cursor through the composition gate");
            Assert.That(ui.Text("chapter-card-title"), Is.EqualTo("Keep the science instrument, or salvage it?"));
            Assert.That(ui.Text("chapter-option-title-0"), Is.EqualTo("Keep the instrument"));
            Assert.That(ui.Text("chapter-option-detail-0"), Is.EqualTo("Core explains PreserveScience."), "the option shows Core's own CheckChoose text");
            Assert.That(ui.Text("chapter-option-title-1"), Is.EqualTo("Salvage it for spare parts"));
            Assert.That(ui.Text("chapter-card-facts"), Does.Contain("battery 4.20 kWh"));

            Assert.That(presenter.ChooseOption(1).Succeeded, Is.True);
            Assert.That(flow.Chosen, Is.Empty, "salvage is the existing physical interaction, never a UI shortcut");
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None));
            Assert.That(presenter.BlocksPlayerControl, Is.False);
            Assert.That(ui.Text("chapter-strip-detail"), Does.Contain("science payload rack"));

            Assert.That(presenter.RequestContinueRoutine().Succeeded, Is.False, "R reopens the decision instead");
            Assert.That(flow.ContinueCalls, Is.EqualTo(0));
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Decision));
            Assert.That(presenter.ChooseOption(0).Succeeded, Is.True);
            Assert.That(flow.Chosen, Is.EqualTo(new[] { ChapterChoice.PreserveScience }));
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None));
            Assert.That(ui.Text("chapter-strip-title"), Is.EqualTo("Day 9 — Exterior repair and the call to Earth"));
        }

        [Test]
        public void Day5_WithoutPayload_ConserveIssuesConserveReserves()
        {
            PastOpeningBeat();
            flow.Chapter = MissionChapter.Consequences;
            flow.PendingDecision = ChapterDecision.GrowOrConserve;
            Tick();
            TickFor(MissionChapterPresenter.BeatSeconds + .2f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Decision));
            Assert.That(ui.Text("chapter-option-title-0"), Is.EqualTo("Conserve the reserves"));
            Assert.That(ui.Text("chapter-option-title-1"), Is.EqualTo("Build the grow module"));
            Assert.That(presenter.ChooseOption(0).Succeeded, Is.True);
            Assert.That(flow.Chosen, Is.EqualTo(new[] { ChapterChoice.ConserveReserves }));
        }

        [Test]
        public void Day9_ConcreteHope_ThenDay14_TheDelayedFailure_WithBothTimes_AcknowledgedOnce()
        {
            PastOpeningBeat();
            mission.Phase = GamePhase.Survival;
            mission.ElapsedSimulationSeconds = FakeMission.At(9, 15, 20);
            flow.Chapter = MissionChapter.Contact;
            flow.NextChapter = MissionChapter.RescueFailed;
            flow.NextChapterAtSimulationSeconds = FakeMission.At(14, 8);
            flow.Rescue = RescuePlan.FirstRescuePlanned;
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Beat));
            Assert.That(ui.Text("chapter-beat-line"), Does.Contain("Contact with Earth holds"));
            TickFor(MissionChapterPresenter.BeatSeconds + .2f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Message));
            Assert.That(ui.Text("chapter-card-title"), Is.EqualTo("Help is coming"));
            Assert.That(ui.Text("chapter-card-body"), Does.Contain("about mission day 14"));
            Assert.That(ui.Text("chapter-card-kicker"), Is.EqualTo("MESSAGE FROM EARTH · RECEIVED DAY 9, 15:20"));
            presenter.DismissCard();
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None));

            mission.ElapsedSimulationSeconds = FakeMission.At(14, 8);
            flow.Chapter = MissionChapter.RescueFailed;
            flow.NextChapter = MissionChapter.FinalDecision;
            flow.NextChapterAtSimulationSeconds = FakeMission.At(20, 8);
            flow.LatestDispatch = new MissionDispatch(ChapterCopy.RescueFailedDispatchId, ChapterCopy.RescueFailedContentId,
                FakeMission.At(14, 7, 46, 8d), FakeMission.At(14, 8));
            flow.Rescue = RescuePlan.FirstRescueFailed;
            flow.PendingDecision = ChapterDecision.AcknowledgeRescueFailure;
            Tick();
            TickFor(MissionChapterPresenter.BeatSeconds + .2f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Message));
            Assert.That(ui.Text("chapter-card-kicker"), Is.EqualTo("MESSAGE FROM EARTH · DELAYED BY LIGHT TIME"));
            Assert.That(ui.Text("chapter-card-meta"), Is.EqualTo("Sent Day 14, 07:46 · received Day 14, 08:00 · 13 min 52 s on the way"));
            Assert.That(ui.Text("chapter-card-title"), Is.EqualTo("The first rescue attempt failed"));
            Assert.That(ui.Text("chapter-card-body"), Does.StartWith("The support vehicle was lost before it could reach the ship."));
            Assert.That(ui.Text("chapter-option-title-0"), Is.EqualTo("Acknowledge"));
            Assert.That(presenter.BlocksPlayerControl, Is.True);
            flow.Revision += 3;
            Tick();
            Assert.That(presenter.QueuedCues, Is.Empty, "the same dispatch Id is never queued twice");

            Assert.That(presenter.ChooseOption(0).Succeeded, Is.True);
            Assert.That(flow.Chosen, Is.EqualTo(new[] { ChapterChoice.AcknowledgeRescueFailure }));
            flow.Rescue = RescuePlan.InTransitCraftCommitted;
            recovery.Stage = RecoveryStage.Inbound;
            recovery.ArrivalAtSimulationSeconds = FakeMission.At(30, 18);
            Tick();
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Message));
            Assert.That(ui.Text("chapter-card-title"), Is.EqualTo("A craft already in transit"));
            Assert.That(ui.Text("chapter-card-body"), Does.Contain("dock on mission day 30"));
            presenter.DismissCard();
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None));
        }

        [Test]
        public void Day20_FinalDecision_UsesCoresNumbers_AndRefusedOptionsAreDisabled()
        {
            PastOpeningBeat();
            mission.Phase = GamePhase.Survival;
            mission.ElapsedSimulationSeconds = FakeMission.At(20, 8);
            recovery.Stage = RecoveryStage.Inbound;
            recovery.ArrivalAtSimulationSeconds = FakeMission.At(30, 18);
            flow.ChooseCheck = choice => choice == ChapterChoice.StopGrowForBeacon
                ? new CommandResult(false, CommandFailure.PreconditionsNotMet, "No crop is growing; there is nothing to stop.")
                : new CommandResult(true, CommandFailure.None, "Keep the current power plan");
            flow.Chapter = MissionChapter.FinalDecision;
            flow.PendingDecision = ChapterDecision.GrowOrBeacon;
            Tick();
            Assert.That(ui.Text("chapter-beat-title"), Is.EqualTo("LAST RESERVES"));
            TickFor(MissionChapterPresenter.BeatSeconds + .2f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Decision));
            Assert.That(presenter.View.CardOptions[0].enabledSelf, Is.False);
            Assert.That(ui.Text("chapter-option-detail-0"), Is.EqualTo("No crop is growing; there is nothing to stop."));
            Assert.That(ui.Text("chapter-card-facts"), Does.Contain("docks Day 30, 18:00"));
            Assert.That(presenter.ChooseOption(0).Succeeded, Is.False, "Core refuses, nothing changes");
            Assert.That(flow.Chosen, Is.Empty);
            Assert.That(presenter.ChooseOption(1).Succeeded, Is.True);
            Assert.That(flow.Chosen, Is.EqualTo(new[] { ChapterChoice.KeepGrowRunning }));
        }

        [Test]
        public void Day30_BeaconRequest_IsACard_ThenAStripWithTheRealWindow()
        {
            PastOpeningBeat();
            mission.Phase = GamePhase.Survival;
            mission.ElapsedSimulationSeconds = FakeMission.At(30, 8);
            flow.Chapter = MissionChapter.Rendezvous;
            flow.NextChapter = MissionChapter.None;
            flow.NextChapterAtSimulationSeconds = FakeMission.At(30, 18);
            recovery.Stage = RecoveryStage.BeaconRequested;
            recovery.ArrivalAtSimulationSeconds = FakeMission.At(30, 18);
            recovery.BeaconWindowEndsAtSimulationSeconds = FakeMission.At(30, 16);
            Tick();
            Assert.That(ui.Text("chapter-beat-title"), Is.EqualTo("RENDEZVOUS"));
            TickFor(MissionChapterPresenter.BeatSeconds + .2f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Beacon));
            Assert.That(ui.Text("chapter-card-meta"), Is.EqualTo("Window closes Day 30, 16:00 (mission clock)."));
            Assert.That(ui.Text("chapter-card-facts"), Does.StartWith("Power the beacon: 0.4 kWh from the battery."), "Core's own CheckPowerBeacon text");
            presenter.DismissCard();
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None));
            Assert.That(ui.Text("chapter-strip-kicker"), Is.EqualTo("BEACON REQUESTED"));

            recovery.Stage = RecoveryStage.Inbound;
            Tick();
            Assert.That(ui.Text("chapter-strip-kicker"), Is.EqualTo("NEXT · DOCKING"));
            Assert.That(ui.Text("chapter-strip-title"), Is.EqualTo("Day 30, 18:00 — the recovery craft docks"));
        }

        [Test]
        public void PauseAndTheGate_HideEverything_AndFreezeTheCardTimer()
        {
            Tick(1f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Beat));
            mission.IsPaused = true;
            TickFor(10f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None));
            Assert.That(ui.Shown("chapter-beat"), Is.False);
            mission.IsPaused = false;
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Beat), "paused time does not use up the card");

            flow.Chapter = MissionChapter.Consequences;
            flow.PendingDecision = ChapterDecision.ScienceOrSalvage;
            TickFor(MissionChapterPresenter.BeatSeconds * 2f + .5f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Decision));
            Assert.That(presenter.BlocksPlayerControl, Is.True);
            gate = false;
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None));
            Assert.That(presenter.BlocksPlayerControl, Is.False, "menus and cinematics release the cursor");
            Assert.That(presenter.ChooseOption(0).Succeeded, Is.False, "a hidden card issues nothing");
            Assert.That(flow.Chosen, Is.Empty);
        }

        [Test]
        public void MissionLoss_HidesAndClearsEveryCard_AndRestartStartsClean()
        {
            PastOpeningBeat();
            flow.Chapter = MissionChapter.Consequences;
            flow.PendingDecision = ChapterDecision.ScienceOrSalvage;
            Tick();
            mission.Outcome = MissionOutcome.Lost;
            mission.Phase = GamePhase.Outcome;
            TickFor(MissionChapterPresenter.BeatSeconds + 1f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None));
            Assert.That(presenter.QueuedCues, Is.Empty);
            Assert.That(presenter.BlocksPlayerControl, Is.False);
            Assert.That(ui.Shown("chapter-ui-layer") && (ui.Shown("chapter-modal") || ui.Shown("chapter-beat")), Is.False);

            mission = new FakeMission { Phase = GamePhase.Emergency, ElapsedSimulationSeconds = 120d };
            flow = new FakeChapterFlow(mission);
            Bind();
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Beat));
            Assert.That(ui.Text("chapter-beat-title"), Is.EqualTo("THE ACCIDENT"), "the restarted mission opens with its own first beat");
        }

        [Test]
        public void ARunningRoutine_SupersedesAWaitingTitleCard_AndOwnsRestPresentationUntilItStops()
        {
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Beat));
            Assert.That(presenter.RequestContinueRoutine().Succeeded, Is.True);
            Tick();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Montage));
            Assert.That(presenter.RoutineOwnsRestPresentation, Is.True);
            recovery.IsResting = true;
            Tick();
            Assert.That(ui.Text("chapter-montage-activity"), Does.StartWith("Now: asleep"));
            Assert.That(presenter.MontageFeed, Has.Some.Contains("Lights out · ordinary sleep, at most 8.5 h"));
            recovery.IsResting = false;
            flow.StopRoutine(RoutineStopReason.ResourceShortage, "the water store is empty");
            Tick();
            Assert.That(presenter.RoutineOwnsRestPresentation, Is.False);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Report));
            Assert.That(ui.Text("chapter-report-kicker"), Is.EqualTo("ROUTINE STOPPED · SUPPLIES"));
        }

        [Test]
        public void TheHudObjective_NamesTheChaptersRealNextStep_FromCoreOnly()
        {
            PastOpeningBeat();
            Assert.That(presenter.ObjectiveTitle, Is.Empty, "Emergency keeps root's phase copy");

            mission.Phase = GamePhase.Repair;
            flow.ContinueCheck = new CommandResult(false, CommandFailure.PreconditionsNotMet, "Prepare and eat the first stored ration first.");
            Tick();
            Assert.That(presenter.ObjectiveTitle, Is.EqualTo("Get through the first days"));
            Assert.That(presenter.ObjectiveDetail, Is.EqualTo("Prepare and eat the first stored ration first."), "Core's own refusal");

            flow.ContinueCheck = new CommandResult(true, CommandFailure.None, "Routine days until the inspection");
            Tick(.3f);
            Assert.That(presenter.ObjectiveTitle, Is.EqualTo("Continue the routine"));
            Assert.That(presenter.ObjectiveDetail, Is.EqualTo("Next: day 5, inspection of the repairs and reserves · press R"));

            flow.StartRoutine();
            Tick();
            Assert.That(presenter.ObjectiveTitle, Is.EqualTo("Routine: ordinary crew days"));
            Assert.That(presenter.ObjectiveDetail, Is.EqualTo("Until Day 5, 08:00 · R stops the routine"));

            flow.Chapter = MissionChapter.Consequences;
            flow.PendingDecision = ChapterDecision.ScienceOrSalvage;
            flow.StopRoutine(RoutineStopReason.ReachedChapter, "reached the inspection of the repairs and reserves");
            Tick();
            Assert.That(presenter.ObjectiveTitle, Is.EqualTo("Decide: keep or salvage the instrument"));

            flow.PendingDecision = ChapterDecision.None;
            flow.Chapter = MissionChapter.Contact;
            flow.NextChapter = MissionChapter.RescueFailed;
            Tick();
            Assert.That(presenter.ObjectiveTitle, Is.Empty, "before contact the spacewalk is the step: root's copy stays");
            mission.Phase = GamePhase.Eva;
            Tick();
            Assert.That(presenter.ObjectiveTitle, Is.Empty);

            mission.Phase = GamePhase.Survival;
            flow.Chapter = MissionChapter.Rendezvous;
            recovery.Stage = RecoveryStage.BeaconRequested;
            recovery.BeaconWindowEndsAtSimulationSeconds = FakeMission.At(30, 16);
            Tick();
            Assert.That(presenter.ObjectiveTitle, Is.EqualTo("Power the recovery beacon"));
            Assert.That(presenter.ObjectiveDetail, Is.EqualTo("Cockpit console · window closes Day 30, 16:00"));

            mission.Outcome = MissionOutcome.Lost;
            mission.Phase = GamePhase.Outcome;
            Tick();
            Assert.That(presenter.ObjectiveTitle, Is.Empty, "a decided mission keeps root's outcome copy");
        }
    }
}
