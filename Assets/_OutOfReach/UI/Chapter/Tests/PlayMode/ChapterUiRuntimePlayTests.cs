using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Interaction;
using OutOfReach.Presentation.UI.Chapter;
using OutOfReach.Simulation;
using OutOfReach.Tests.PlayMode;
using UnityEngine;
using UnityEngine.TestTools;

namespace OutOfReach.Tests.ChapterUi
{
    /// <summary>
    /// CHAPTER-UI-01 against the REAL Core: the grey-box harness runs MissionRuntime with ConfigureChapterLoop(true) in
    /// real-time frames (0.1 s), and the chapter UI on a real panel is the only thing that requests routines and takes the
    /// chapter decisions (through IMissionChapterFlow). Every beat of the approved loop is reached and shown, from the
    /// accident to the docking. EXPLICIT SHORTCUTS, and only these: the impact and the two interior repairs
    /// (RepairLoopHarness.ReachStabilized), the exterior repair and the call (the same domain commands as
    /// RepairLoopHarness.ReachContact, from the day-9 cabin), and the beacon power-up (the cockpit console's own command).
    /// The first meal and the first rest go through the harness targets with E.
    /// </summary>
    public sealed class ChapterUiRuntimePlayTests
    {
        private const float FrameSeconds = 0.02f;
        private const double RealStep = 0.1d;

        private RepairLoopHarness harness;
        private ChapterUiTestPanel ui;
        private MissionChapterPresenter presenter;
        private readonly List<string> seenMontageDays = new List<string>();

        private MissionSession Session => harness.Session;
        private IMissionChapterFlow Flow => harness.Runtime.ChapterFlow;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = FrameSeconds;
            harness = RepairLoopHarness.Build(survival: true);
            harness.Runtime.ConfigureChapterLoop(true);
            harness.Runtime.StartSession();
            harness.Runtime.DriveFromUpdate = false;
            ui = new ChapterUiTestPanel();
            presenter = ui.Presenter;
            presenter.Configure(ui.Document, ui.Style, harness.Runtime.ChapterFlow, harness.Runtime.GameState, harness.Runtime.Clock,
                harness.Runtime.Resources, harness.Runtime.SurvivalRecovery, harness.Runtime.Survival, () => true);
            presenter.ConfigureMissionData(harness.Runtime.Communications, () => harness.Runtime.Calendar?.NowUtc);
        }

        [TearDown]
        public void TearDown()
        {
            ui.Destroy();
            harness.Destroy();
            Time.captureDeltaTime = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        /// <summary>One real 0.1 s frame of the runtime, then one presentation frame of the chapter UI.</summary>
        private void Frame()
        {
            harness.Runtime.Step(RealStep);
            presenter.Tick((float)RealStep);
            if (presenter.CurrentScreen == ChapterScreen.Montage)
            {
                string day = ui.Text("chapter-montage-day");
                if (seenMontageDays.Count == 0 || seenMontageDays[seenMontageDays.Count - 1] != day) seenMontageDays.Add(day);
            }
        }

        private IEnumerator Present(float seconds)
        {
            for (float t = 0f; t < seconds; t += (float)RealStep)
            {
                presenter.Tick((float)RealStep);
            }

            yield return null;
        }

        private IEnumerator Use(InteractableTarget target)
        {
            Vector3 at = target.transform.position;
            harness.PlacePlayerFacing(at.x - Mathf.Sign(at.x), at.z, at);
            yield return Frames(3);
            harness.Resolver.Refresh();
            Assert.That(harness.Resolver.CurrentTarget, Is.SameAs(target), "aimed at " + target.name);
            Assert.That(harness.Resolver.CurrentCanInteract, Is.True, target.name + " prompt: " + harness.Resolver.CurrentPrompt);
            harness.Input.PressInteract();
            yield return Frames(3);
            CommandResult? last = harness.Player.LastInteraction;
            Assert.That(last.HasValue && last.Value.Succeeded, Is.True, target.name + ": " + (last.HasValue ? last.Value.Explanation : "no interaction"));
        }

        private IEnumerator RunUntil(Func<bool> done, double maxRealSeconds, string what)
        {
            int frames = (int)Math.Ceiling(maxRealSeconds / RealStep);
            for (int i = 0; i < frames && !done(); i++)
            {
                Frame();
                if (i % 10 == 9) yield return null;
            }

            Assert.That(done(), Is.True, what + " within " + maxRealSeconds.ToString("0.#") + " real seconds");
        }

        /// <summary>The chapter UI requests the routine; the montage owns the screen until Core stops it.</summary>
        private IEnumerator RoutineThroughTheUi(string what)
        {
            presenter.Tick(0f);
            Assert.That(ui.Shown("chapter-strip"), Is.True, "strip before " + what);
            Assert.That(presenter.View.StripAction.enabledSelf, Is.True, "strip action before " + what + ": " + ui.Text("chapter-strip-detail"));
            CommandResult started = presenter.RequestContinueRoutine();
            Assert.That(started.Succeeded, Is.True, what + ": " + started.Explanation);
            seenMontageDays.Clear();
            presenter.Tick(0f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Montage), what);
            int montageFrames = 0;
            yield return RunUntil(() =>
            {
                if (Flow.IsRoutineRunning && presenter.CurrentScreen == ChapterScreen.Montage) montageFrames++;
                return !Flow.IsRoutineRunning;
            }, 240d, what);
            Assert.That(montageFrames, Is.GreaterThan(10), "the montage was on screen while the routine ran: " + what);
            Assert.That(Flow.LastRoutineStop, Is.Not.EqualTo(RoutineStopReason.Danger).And.Not.EqualTo(RoutineStopReason.ResourceShortage),
                what + ": " + Flow.LastRoutineStopText);
        }

        /// <summary>MISSION-DATA: the strip repeats the real link's NASA/JPL sample in Core's own format.</summary>
        private string ExpectMissionData(bool transmitted, string when)
        {
            presenter.Tick(0f);
            ICommunicationSystem link = harness.Runtime.Communications;
            string expected = "Earth " + CommunicationSystem.FormatDistance(link.ReferenceDistanceKm) + " · signal "
                + CommunicationSystem.FormatDuration(link.OneWayLightSeconds) + " one way";
            Assert.That(ui.Shown("chapter-strip-data"), Is.True, "NASA/JPL line " + when);
            Assert.That(ui.Text("chapter-strip-data"), Is.EqualTo(expected), when);
            Assert.That(ui.Text("chapter-strip-source"), Is.EqualTo(ChapterCopy.EarthLineSource(transmitted)), when);
            string dsn = ChapterCopy.DsnLine(harness.Runtime.Calendar.NowUtc);
            Assert.That(dsn, Does.StartWith("DSN: ").And.EndsWith("(line of sight)"), "the mission date is inside the snapshot " + when);
            Assert.That(ui.Shown("chapter-strip-dsn"), Is.True, "DSN line " + when);
            Assert.That(ui.Text("chapter-strip-dsn"), Is.EqualTo(dsn), "the calculator's line for the real mission date " + when);
            return expected;
        }

        private IEnumerator ExpectBeat(string title)
        {
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Beat), "beat card " + title);
            Assert.That(ui.Text("chapter-beat-title"), Is.EqualTo(title));
            yield return Present(MissionChapterPresenter.BeatSeconds + .2f);
        }

        [UnityTest]
        public IEnumerator TheWholeApprovedLoop_IsPlayedThroughTheChapterUi_FromTheAccidentToTheDocking()
        {
            Assert.That(Flow, Is.Not.Null, "the runtime built a chapter session");
            presenter.Tick(.1f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None), "planning: no chapter UI");

            // Days 1-2: the accident beat, then the routine waits for Core's real prerequisites.
            harness.ReachStabilized();
            presenter.Tick(.1f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Beat));
            Assert.That(ui.Text("chapter-beat-title"), Is.EqualTo("THE ACCIDENT"));
            yield return Present(MissionChapterPresenter.BeatSeconds + .2f);
            Assert.That(presenter.View.StripAction.enabledSelf, Is.False);
            Assert.That(ui.Text("chapter-strip-detail"), Is.EqualTo("Prepare and eat the first stored ration first."));
            string dayOneData = ExpectMissionData(false, "day 1");
            Assert.That(dayOneData, Is.EqualTo("Earth 197,708,000 km · signal 10 min 59 s one way"), "row 0 of the Horizons snapshot");
            yield return Use(harness.Ration);
            yield return Use(harness.Sleep);
            yield return RunUntil(() => !Session.SurvivalRecovery.IsResting, 30d, "the first rest completes");

            // Days 2 -> 5.
            yield return RoutineThroughTheUi("days 2 to 5");
            Assert.That(Flow.Chapter, Is.EqualTo(MissionChapter.Consequences));
            Assert.That(seenMontageDays, Is.SupersetOf(new[] { "DAY 3", "DAY 4" }), "the montage counted the days: " + string.Join(", ", seenMontageDays));
            Assert.That(ui.Text("chapter-beat-summary"), Is.EqualTo("After "
                + ChapterCopy.Span(Flow.RoutineStoppedAtSimulationSeconds.Value - Flow.RoutineStartedAtSimulationSeconds.Value) + " of routine: "
                + ChapterCopy.Tally(Flow.RoutineRations, Flow.RoutineDrinks, Flow.RoutineRests) + "."), "Core's own tally and times");
            Assert.That(Flow.RoutineRests, Is.GreaterThanOrEqualTo(3), "ordinary nights, not one long sleep");
            string dayFiveData = ExpectMissionData(false, "day 5");
            Assert.That(dayFiveData, Is.Not.EqualTo(dayOneData), "Core moved to the sample of the new mission date");
            yield return ExpectBeat("CONSEQUENCES");
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Decision));
            Assert.That(Flow.PendingDecision, Is.EqualTo(ChapterDecision.ScienceOrSalvage));
            Assert.That(ui.Text("chapter-option-detail-0"), Does.StartWith("Keep the science instrument"), "Core's own CheckChoose text");
            Assert.That(presenter.ChooseOption(0).Succeeded, Is.True);
            Assert.That(Flow.PendingDecision, Is.EqualTo(ChapterDecision.None));

            // Days 5 -> 9, then the exterior repair and the call; Earth's reply brings concrete hope.
            yield return RoutineThroughTheUi("days 5 to 9");
            Assert.That(Flow.Chapter, Is.EqualTo(MissionChapter.Contact));
            Assert.That(ui.Text("chapter-beat-line"), Is.EqualTo("Finish the exterior repair and call Earth."));
            yield return ExpectBeat("CONTACT");
            Assert.That(presenter.View.StripAction.enabledSelf, Is.False, "contact first");
            CallEarthFromTheCabin();
            presenter.Tick(.1f);
            Assert.That(Flow.Rescue, Is.EqualTo(RescuePlan.FirstRescuePlanned));
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Message));
            Assert.That(ui.Text("chapter-card-title"), Is.EqualTo("Help is coming"));
            Assert.That(ui.Text("chapter-card-body"), Does.Contain("about mission day 14"));
            presenter.DismissCard();
            string sentData = ExpectMissionData(true, "after the call on day 9");
            Assert.That(sentData, Is.Not.EqualTo(dayFiveData), "the call went out with the day-9 sample");

            // Days 9 -> 14: the causally delayed failure, acknowledged through the card.
            yield return RoutineThroughTheUi("days 9 to 14");
            Assert.That(Flow.LatestDispatch.Id, Is.EqualTo(ChapterCopy.RescueFailedDispatchId));
            Assert.That(ExpectMissionData(true, "day 14"), Is.EqualTo(sentData), "Core keeps the transmitted sample; nothing is interpolated");
            yield return ExpectBeat("THE MESSAGE");
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Message));
            MissionDispatch dispatch = Flow.LatestDispatch;
            Assert.That(ui.Text("chapter-card-meta"), Is.EqualTo("Sent " + ChapterCopy.DayAndClock(dispatch.TransmittedAtSimulationSeconds)
                + " · received " + ChapterCopy.DayAndClock(dispatch.ReceivedAtSimulationSeconds) + " · "
                + ChapterCopy.Transit(dispatch.ReceivedAtSimulationSeconds - dispatch.TransmittedAtSimulationSeconds) + " on the way"));
            Assert.That(dispatch.ReceivedAtSimulationSeconds - dispatch.TransmittedAtSimulationSeconds, Is.GreaterThan(60d), "light time");
            Assert.That(presenter.ChooseOption(0).Succeeded, Is.True);
            Assert.That(Flow.Rescue, Is.EqualTo(RescuePlan.InTransitCraftCommitted));
            presenter.Tick(.1f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Message));
            Assert.That(ui.Text("chapter-card-body"), Does.Contain("dock on mission day 30"));
            presenter.DismissCard();

            // Days 14 -> 20: the final power and supply decision.
            yield return RoutineThroughTheUi("days 14 to 20");
            yield return ExpectBeat("LAST RESERVES");
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Decision));
            Assert.That(presenter.View.CardOptions[0].enabledSelf, Is.False, "nothing grows: Core refuses the shutdown");
            Assert.That(presenter.ChooseOption(1).Succeeded, Is.True);

            // Days 20 -> 30: the beacon request and the docking.
            yield return RoutineThroughTheUi("days 20 to 30");
            yield return ExpectBeat("RENDEZVOUS");
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Beacon));
            Assert.That(ui.Text("chapter-card-meta"), Is.EqualTo("Window closes Day 30, 16:00 (mission clock)."));
            presenter.DismissCard();
            Assert.That(harness.Runtime.SurvivalRecovery.TryPowerBeacon().Succeeded, Is.True, "the cockpit console's command");
            presenter.Tick(.1f);
            Assert.That(ui.Text("chapter-strip-kicker"), Is.EqualTo("NEXT · DOCKING"));
            yield return RoutineThroughTheUi("the docking");
            Assert.That(Session.State.Outcome, Is.EqualTo(MissionOutcome.Recovered));
            presenter.Tick(.1f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None), "the ending and the report own the screen");
            Assert.That(presenter.QueuedCues, Is.Empty);
        }

        [UnityTest]
        public IEnumerator StopFromTheMontage_EndsAtTheCommittedTime_AndRestartClearsTheUi()
        {
            harness.ReachStabilized();
            presenter.Tick(.1f);
            yield return Present(MissionChapterPresenter.BeatSeconds + .2f);
            yield return Use(harness.Ration);
            yield return Use(harness.Sleep);
            yield return RunUntil(() => !Session.SurvivalRecovery.IsResting, 30d, "the first rest completes");
            Assert.That(presenter.RequestContinueRoutine().Succeeded, Is.True);
            for (int i = 0; i < 25; i++) Frame();
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Montage));
            double before = Session.State.ElapsedSimulationSeconds;
            Assert.That(presenter.RequestStopRoutine().Succeeded, Is.True);
            presenter.Tick(.1f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.Report));
            Assert.That(Flow.RoutineStoppedAtSimulationSeconds, Is.EqualTo(before), "stopped at the committed time, not the target");
            Assert.That(ui.Text("chapter-report-time"), Is.EqualTo("Stopped at " + ChapterCopy.DayAndClock(before) + " (mission clock)"));
            Assert.That(Flow.Chapter, Is.EqualTo(MissionChapter.Accident));

            harness.Runtime.RestartMission();
            presenter.Configure(ui.Document, ui.Style, harness.Runtime.ChapterFlow, harness.Runtime.GameState, harness.Runtime.Clock,
                harness.Runtime.Resources, harness.Runtime.SurvivalRecovery, harness.Runtime.Survival, () => true);
            presenter.Tick(.1f);
            Assert.That(presenter.CurrentScreen, Is.EqualTo(ChapterScreen.None), "the new mission is in planning");
            Assert.That(presenter.QueuedCues, Is.Empty);
        }

        /// <summary>The exterior repair and the call from the day-9 cabin, by the same domain commands as RepairLoopHarness.ReachContact.</summary>
        private void CallEarthFromTheCabin()
        {
            MissionSession session = Session;
            Require(session.Eva.TryPrepareSuit(), "suit");
            Require(session.Eva.TryBeginEva(), "begin EVA");
            Require(session.AdvanceSimulation(session.Eva.Spec.DepressurizationSeconds), "depressurize");
            Require(session.Eva.TryGoOutside(), "go outside");
            Require(session.Systems.Antenna.TryRepair(session.Resources), "antenna repair");
            Require(session.Eva.TryReturnInside(), "return inside");
            Require(session.Eva.TryEndEva(), "end EVA");
            Require(session.AdvanceSimulation(session.Eva.Spec.RepressurizationSeconds), "repressurize");
            Require(session.Director.TryRunLinkDiagnostic(), "link diagnostic");
            FinishWaitingCinematic(session);
            Require(session.Communications.TryTransmit(), "transmit");
            Require(session.AdvanceSimulation(session.Communications.ReplyDelaySimulationSeconds), "reply");
            FinishWaitingCinematic(session);
            Assert.That(session.State.Phase, Is.EqualTo(GamePhase.Survival));
        }

        private static void FinishWaitingCinematic(MissionSession session)
        {
            if (session.Cinematics.IsWaiting)
                session.Events.Publish(new CinematicFinished(session.Cinematics.ActiveCinematic, session.Cinematics.ActiveSequenceId, false));
        }

        private static void Require(CommandResult result, string what)
        {
            Assert.That(result.Succeeded, Is.True, what + ": " + result.Explanation);
        }
    }
}
