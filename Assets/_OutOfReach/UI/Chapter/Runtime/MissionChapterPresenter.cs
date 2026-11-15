using System;
using System.Collections.Generic;
using System.Globalization;
using OutOfReach.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI.Chapter
{
    /// <summary>What the chapter UI presents in the current frame.</summary>
    public enum ChapterScreen { None, Beat, Montage, Report, Message, Decision, Beacon }

    /// <summary>
    /// CHAPTER-UI-01: the approved day 1-30 chapter loop made visible and playable through <see cref="IMissionChapterFlow"/>
    /// only. It reads Core every frame and issues only Core's own commands (TryContinueRoutine, TryCancelRoutine,
    /// TryChoose). It keeps no clock, ledger, day counter or story state of its own; real seconds drive only how long a
    /// card stays on screen. Pause and the composition's presentation gate hide everything and freeze card timers;
    /// a new binding (restart) or the mission's end clears every queued card.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(270)]
    public sealed class MissionChapterPresenter : MonoBehaviour
    {
        public const float BeatSeconds = 5.4f;
        public const float ReportSeconds = 7f;
        public const float DangerReportSeconds = 10f;
        public const float FeedbackSeconds = 4.5f;
        private const float BeatFadeIn = .45f, BeatFadeOut = .8f, ReportFade = .35f, ModalFade = .22f, MontageFade = .35f;
        private const float CheckInterval = .25f;

        [SerializeField] private UIDocument document;
        [SerializeField] private StyleSheet styleSheet;
        [Tooltip("R continues or stops the routine, 1/2 pick a card option, Enter confirms or dismisses a card.")]
        [SerializeField] private bool keyboardShortcuts = true;

        private IMissionChapterFlow flow;
        private IGameState state;
        private ISimulationClock clock;
        private IResourceSystem resources;
        private ISurvivalRecovery recovery;
        private ISurvivalExtension survival;
        private Func<bool> presentationGate;
        private Func<bool> reducedMotion;
        private ChapterView view;
        private readonly ChapterCueObserver observer = new ChapterCueObserver();
        private ChapterCue current;
        private float currentSeconds, modalSeconds, montageSeconds;
        private ChapterScreen screen;
        private bool presentable;
        private readonly HashSet<ChapterDecision> deferred = new HashSet<ChapterDecision>();
        private ChapterDecision lastPending;
        private bool beaconDeferred;
        private RecoveryStage lastStage;
        private ChapterOptionCopy[] cardOptions = Array.Empty<ChapterOptionCopy>();
        private string cardFeedback = string.Empty;
        private float cardCheckIn;
        private ChapterDecision cardDecision;
        private ChapterScreen cardScreen;
        private ChapterCue cardCue;

        // Montage: read-only observation of the running segment.
        private bool montageObserved;
        private double montageStart, montageTarget;
        private ResourceSnapshot montageBaseline;
        private bool baselineKnown;
        private int seenRations, seenRests, feedDay;
        private bool seenResting;
        private readonly List<string> feed = new List<string>();
        private readonly List<VisualElement> rulerTicks = new List<VisualElement>();

        // Next-routine strip.
        private float checkIn;
        private int checkedRevision = -1;
        private int checkedStateKey = -1;
        private CommandResult lastCheck;
        private bool hasCheck;
        private string stripFeedback = string.Empty;
        private float stripFeedbackSeconds;
        private bool routineSeen;

        /// <summary>The screen presented in the last frame (None while hidden).</summary>
        public ChapterScreen CurrentScreen => presentable ? screen : ChapterScreen.None;

        /// <summary>
        /// True while a modal message or decision card is on screen. The composition adds this to the player's control
        /// gate so the cursor is free for the card; pause, the menus and cinematics hide the card and release it.
        /// </summary>
        public bool BlocksPlayerControl => presentable && isActiveAndEnabled
            && (screen == ChapterScreen.Message || screen == ChapterScreen.Decision || screen == ChapterScreen.Beacon);

        /// <summary>
        /// True from the first frame this presenter saw a running routine until the frame after it stopped. The montage
        /// owns rest presentation meanwhile: the composition hands this to SurvivalChapterPresenter, whose LateUpdate (order
        /// 260) runs before this one (270), so the routine's own final wake is covered too and no DAY title is replayed.
        /// </summary>
        public bool RoutineOwnsRestPresentation => routineSeen && isActiveAndEnabled;

        public bool IsBound => flow != null;
        public ChapterView View => view;
        public ChapterCue CurrentCue => current;
        public IReadOnlyList<string> MontageFeed => feed;
        public IReadOnlyList<ChapterCue> QueuedCues => observer.Pending;

        /// <summary>Binds one mission session. Every argument is a Core contract; null optional services hide their parts.</summary>
        /// <param name="canPresent">The composition's gate (session match, menus, cinematics, terminal). Null fails closed.</param>
        public void Configure(UIDocument ui, StyleSheet sheet, IMissionChapterFlow chapterFlow, IGameState gameState,
            ISimulationClock simulationClock, IResourceSystem resourceSystem, ISurvivalRecovery recoveryService,
            ISurvivalExtension survivalService, Func<bool> canPresent, Func<bool> reducedMotionSetting = null)
        {
            ClearBinding();
            if (ui) document = ui;
            if (sheet) styleSheet = sheet;
            flow = chapterFlow;
            state = gameState;
            clock = simulationClock;
            resources = resourceSystem;
            recovery = recoveryService;
            survival = survivalService;
            presentationGate = canPresent;
            reducedMotion = reducedMotionSetting;
            lastStage = recovery?.Stage ?? RecoveryStage.NotStarted;
        }

        /// <summary>Releases the session: hides and forgets every card, deferral and observation (restart, unbind).</summary>
        public void ClearBinding()
        {
            flow = null;
            state = null;
            clock = null;
            resources = null;
            recovery = null;
            survival = null;
            presentationGate = null;
            reducedMotion = null;
            routineSeen = false;
            observer.Reset();
            ResetTransient();
            view?.HideAll();
        }

        public void SetKeyboardShortcuts(bool enabled) => keyboardShortcuts = enabled;

        private void OnDisable()
        {
            presentable = false;
            screen = ChapterScreen.None;
            view?.HideAll();
        }

        private void OnDestroy()
        {
            ClearBinding();
            view?.Detach();
            view = null;
        }

        /// <summary>False lets a test or a native check drive <see cref="Tick"/> with explicit real seconds.</summary>
        public bool DriveFromLateUpdate { get; set; } = true;

        private void LateUpdate()
        {
            if (DriveFromLateUpdate) Tick(Mathf.Clamp(Time.unscaledDeltaTime, 0f, .1f));
        }

        /// <summary>One presentation frame: observe Core, choose the screen, fill it, read shortcuts.</summary>
        public void Tick(float realSeconds)
        {
            bool routineNow = flow != null && flow.Enabled && flow.IsRoutineRunning;
            try
            {
                TickPresentation(realSeconds);
            }
            finally
            {
                routineSeen = routineNow;
            }
        }

        private void TickPresentation(float realSeconds)
        {
            if (float.IsNaN(realSeconds) || realSeconds < 0f) realSeconds = 0f;
            bool bound = flow != null && flow.Enabled && state != null && clock != null;
            bool ended = bound && (state.Outcome != MissionOutcome.None || state.Phase == GamePhase.Outcome
                || state.Phase == GamePhase.Report);
            bool active = bound && !ended && IsChapterPhase(state.Phase)
                && (survival == null || (survival.Enabled && survival.IsActive));
            double now = bound ? clock.ElapsedSimulationSeconds : 0d;
            if (bound)
                observer.Observe(flow, active, ended, now, recovery?.Stage ?? RecoveryStage.NotStarted, recovery?.ArrivalAtSimulationSeconds);
            if (ended || !bound) ResetTransient();

            presentable = active && isActiveAndEnabled && !clock.IsPaused && Gate() && EnsureView();
            if (!presentable)
            {
                screen = ChapterScreen.None;
                view?.HideAll();
                return;
            }

            view.SetReducedMotion(reducedMotion?.Invoke() ?? false);
            ObserveDecisionState();
            screen = ChooseScreen(realSeconds);
            Present(now, realSeconds);
            if (keyboardShortcuts) ReadShortcuts();
        }

        // ---------- commands (buttons, shortcuts, tests) ----------

        /// <summary>The strip's action: continue the routine, or reopen a deferred decision/beacon card first.</summary>
        public CommandResult RequestContinueRoutine()
        {
            if (!presentable || flow == null) return Refused("The chapter screen is not active.");
            if (flow.IsRoutineRunning) return Refused("The routine is already running.");
            if (flow.PendingDecision != ChapterDecision.None && deferred.Remove(flow.PendingDecision))
                return new CommandResult(false, CommandFailure.PreconditionsNotMet, "Decide first.");
            if (recovery != null && recovery.Stage == RecoveryStage.BeaconRequested && beaconDeferred)
            {
                beaconDeferred = false;
                return new CommandResult(false, CommandFailure.PreconditionsNotMet, "Answer the beacon request first.");
            }

            CommandResult result = flow.TryContinueRoutine();
            checkIn = 0f;
            if (result.Succeeded)
            {
                observer.DropStaleReports();
                if (current != null && current.Kind == ChapterCueKind.BeatCard) current = null;
                stripFeedback = string.Empty;
            }
            else
            {
                stripFeedback = ChapterCopy.Sentence(result.Explanation);
                stripFeedbackSeconds = FeedbackSeconds;
            }

            return result;
        }

        /// <summary>The montage's Stop: Core stops at the actual committed time.</summary>
        public CommandResult RequestStopRoutine()
        {
            if (!presentable || flow == null) return Refused("The chapter screen is not active.");
            return flow.TryCancelRoutine();
        }

        /// <summary>Option 0 or 1 of the open card. Command options call Core; physical ones close the card.</summary>
        public CommandResult ChooseOption(int index)
        {
            if (!presentable || flow == null || (screen != ChapterScreen.Decision && screen != ChapterScreen.Message))
                return Refused("No decision is on screen.");
            if (index < 0 || index >= cardOptions.Length) return Refused("No such option.");
            ChapterOptionCopy option = cardOptions[index];
            if (option.Kind == ChapterOptionKind.Physical)
            {
                deferred.Add(cardDecision);
                stripFeedback = option.Station;
                stripFeedbackSeconds = FeedbackSeconds * 2f;
                return new CommandResult(true, CommandFailure.None, option.Station);
            }

            CommandResult result = flow.TryChoose(option.Choice);
            if (result.Succeeded)
            {
                cardFeedback = string.Empty;
                if (current != null && current.Kind == ChapterCueKind.FailureDispatch) current = null;
            }
            else
            {
                cardFeedback = ChapterCopy.Sentence(result.Explanation);
            }

            cardCheckIn = 0f;
            return result;
        }

        /// <summary>Closes the open card: an information card is done, a decision or beacon request is deferred.</summary>
        public void DismissCard()
        {
            if (!presentable) return;
            switch (screen)
            {
                case ChapterScreen.Beat:
                case ChapterScreen.Report:
                case ChapterScreen.Message:
                    if (current != null && current.Kind == ChapterCueKind.FailureDispatch
                        && flow.PendingDecision == ChapterDecision.AcknowledgeRescueFailure)
                        deferred.Add(ChapterDecision.AcknowledgeRescueFailure);
                    current = null;
                    break;
                case ChapterScreen.Decision:
                    deferred.Add(cardDecision);
                    break;
                case ChapterScreen.Beacon:
                    beaconDeferred = true;
                    stripFeedback = "Power the recovery beacon at the cockpit console.";
                    stripFeedbackSeconds = FeedbackSeconds * 2f;
                    break;
            }

            cardFeedback = string.Empty;
        }

        // ---------- frame logic ----------

        private ChapterScreen ChooseScreen(float realSeconds)
        {
            if (flow.IsRoutineRunning)
            {
                // A running routine owns the screen; non-modal cards that were waiting are superseded by it.
                if (current != null && (current.Kind == ChapterCueKind.BeatCard || current.Kind == ChapterCueKind.StopReport))
                    current = null;
                return ChapterScreen.Montage;
            }

            if (current == null)
            {
                current = observer.Dequeue();
                currentSeconds = 0f;
                cardFeedback = string.Empty;
            }

            if (current != null)
            {
                currentSeconds += realSeconds;
                switch (current.Kind)
                {
                    case ChapterCueKind.BeatCard:
                        if (currentSeconds < BeatSeconds) return ChapterScreen.Beat;
                        break;
                    case ChapterCueKind.StopReport:
                        if (currentSeconds < (ChapterCopy.StopTone(current.Stop) == ChapterTone.Danger ? DangerReportSeconds : ReportSeconds))
                            return ChapterScreen.Report;
                        break;
                    case ChapterCueKind.FailureDispatch:
                        // The message stays until it is acknowledged (Core clears the decision) or deferred.
                        if (flow.PendingDecision == ChapterDecision.AcknowledgeRescueFailure
                            && !deferred.Contains(ChapterDecision.AcknowledgeRescueFailure))
                            return ChapterScreen.Message;
                        break;
                    default:
                        return ChapterScreen.Message;
                }

                current = null;
                return ChooseScreen(0f);
            }

            ChapterDecision pending = flow.PendingDecision;
            if (pending != ChapterDecision.None && !deferred.Contains(pending)) return ChapterScreen.Decision;
            if (recovery != null && recovery.Stage == RecoveryStage.BeaconRequested && !beaconDeferred) return ChapterScreen.Beacon;
            return ChapterScreen.None;
        }

        private void Present(double now, float realSeconds)
        {
            bool reduced = reducedMotion?.Invoke() ?? false;
            if (screen == ChapterScreen.Montage) montageSeconds += realSeconds; else montageSeconds = 0f;
            bool modal = screen == ChapterScreen.Message || screen == ChapterScreen.Decision || screen == ChapterScreen.Beacon;
            if (modal) modalSeconds += realSeconds; else modalSeconds = 0f;

            ObserveMontage(now);
            ChapterView.Show(view.Montage, screen == ChapterScreen.Montage);
            if (screen == ChapterScreen.Montage) FillMontage(now, reduced);

            ChapterView.Show(view.Beat, screen == ChapterScreen.Beat);
            if (screen == ChapterScreen.Beat) FillBeat(reduced);

            ChapterView.Show(view.Report, screen == ChapterScreen.Report);
            if (screen == ChapterScreen.Report) FillReport();

            ChapterView.Show(view.Modal, modal);
            if (modal)
            {
                view.Modal.style.opacity = reduced ? 1f : Mathf.Clamp01(modalSeconds / ModalFade);
                if (screen != cardScreen || !ReferenceEquals(current, cardCue)
                    || (screen == ChapterScreen.Decision && flow.PendingDecision != cardDecision))
                {
                    cardScreen = screen;
                    cardCue = current;
                    cardCheckIn = 0f;
                    cardFeedback = string.Empty;
                }

                cardCheckIn -= realSeconds;
                if (cardCheckIn <= 0f)
                {
                    cardCheckIn = .5f;
                    FillCard(now);
                }
                else
                {
                    ChapterView.SetText(view.CardFeedback, cardFeedback);
                }
            }
            else
            {
                cardScreen = ChapterScreen.None;
                cardCue = null;
            }

            bool strip = (screen == ChapterScreen.None || screen == ChapterScreen.Beat) && FillStrip(now, realSeconds);
            ChapterView.Show(view.Strip, strip);
        }

        private void ObserveDecisionState()
        {
            ChapterDecision pending = flow.PendingDecision;
            if (pending != lastPending)
            {
                if (lastPending != ChapterDecision.None) deferred.Remove(lastPending);
                lastPending = pending;
            }

            deferred.RemoveWhere(decision => decision != pending);
            RecoveryStage stage = recovery?.Stage ?? RecoveryStage.NotStarted;
            if (stage != lastStage)
            {
                if (stage != RecoveryStage.BeaconRequested) beaconDeferred = false;
                lastStage = stage;
            }
        }

        // ---------- beat card ----------

        private void FillBeat(bool reduced)
        {
            float t = currentSeconds;
            float alpha = Ease(t / BeatFadeIn) * (1f - Ease((t - (BeatSeconds - BeatFadeOut)) / BeatFadeOut));
            view.Beat.style.opacity = alpha;
            view.Beat.style.translate = new Translate(0, reduced ? 0f : -18f * (1f - Ease(t / 1.1f)));
            bool contactHolds = state.Phase == GamePhase.Survival;
            ChapterView.SetText(view.BeatDays, ChapterCopy.BeatDays(current.Chapter, current.Day));
            ChapterView.SetText(view.BeatTitle, ChapterCopy.BeatTitle(current.Chapter).ToUpperInvariant());
            ChapterView.SetText(view.BeatLine, ChapterCopy.BeatLine(current.Chapter, contactHolds));
            ChapterView.SetText(view.BeatSummary, current.HasRoutineSummary && current.RoutineStartedAt.HasValue && current.RoutineStoppedAt.HasValue
                ? "After " + ChapterCopy.Span(current.RoutineStoppedAt.Value - current.RoutineStartedAt.Value) + " of routine: "
                  + ChapterCopy.Tally(current.Rations, current.Drinks, current.Rests) + "."
                : string.Empty);
        }

        // ---------- routine report ----------

        private void FillReport()
        {
            float t = currentSeconds;
            float total = ChapterCopy.StopTone(current.Stop) == ChapterTone.Danger ? DangerReportSeconds : ReportSeconds;
            view.Report.style.opacity = Ease(t / ReportFade) * (1f - Ease((t - (total - ReportFade * 2f)) / (ReportFade * 2f)));
            ChapterTone tone = ChapterCopy.StopTone(current.Stop);
            ChapterView.SetTone(view.Report, tone);
            ChapterView.SetText(view.ReportKicker, ChapterCopy.StopLabel(current.Stop));
            ChapterView.SetText(view.ReportTime, current.RoutineStoppedAt.HasValue
                ? "Stopped at " + ChapterCopy.DayAndClock(current.RoutineStoppedAt.Value) + " (mission clock)"
                : string.Empty);
            ChapterView.SetText(view.ReportReason, ChapterCopy.Sentence(current.StopText));
            ChapterView.SetText(view.ReportSummary, current.RoutineStartedAt.HasValue && current.RoutineStoppedAt.HasValue
                ? "After " + ChapterCopy.Span(current.RoutineStoppedAt.Value - current.RoutineStartedAt.Value) + " of routine: "
                  + ChapterCopy.Tally(current.Rations, current.Drinks, current.Rests) + "."
                : string.Empty);
        }

        // ---------- montage ----------

        private void ObserveMontage(double now)
        {
            if (!flow.IsRoutineRunning)
            {
                montageObserved = false;
                return;
            }

            if (!montageObserved)
            {
                montageObserved = true;
                montageStart = flow.RoutineStartedAtSimulationSeconds ?? now;
                montageTarget = flow.NextChapterAtSimulationSeconds ?? montageStart;
                baselineKnown = resources != null;
                if (baselineKnown) montageBaseline = resources.Snapshot;
                // Bound mid-segment: only what happens from now on is logged, the tally is Core's own.
                seenRations = flow.RoutineRations;
                seenRests = flow.RoutineRests;
                seenResting = recovery != null && recovery.IsResting;
                feed.Clear();
                feedDay = ChapterCopy.DayAt(now);
                AddFeed(montageStart, "Routine begins: ordinary crew days until " + ChapterCopy.DayAndClock(montageTarget) + ".");
                BuildRulerTicks();
            }

            if (ChapterCopy.DayAt(now) != feedDay)
            {
                feedDay = ChapterCopy.DayAt(now);
                feed.Add("— Day " + feedDay.ToString(CultureInfo.InvariantCulture) + " —");
            }

            for (; seenRations < flow.RoutineRations; seenRations++)
                AddFeed(now, ChapterCopy.MealName(now) + " · one stored ration eaten");
            bool resting = recovery != null && recovery.IsResting;
            if (resting != seenResting)
            {
                seenResting = resting;
                if (resting)
                {
                    AddFeed(now, "Lights out · ordinary sleep, at most " + ChapterCopy.NightLimit);
                }
                else if (resources != null)
                {
                    ResourceSnapshot s = resources.Snapshot;
                    AddFeed(now, "Wake · systems check: CO₂ " + ChapterCopy.Ppm(s.CarbonDioxidePpm) + ", battery " + ChapterCopy.Energy(s.BatteryKWh));
                }
                else
                {
                    AddFeed(now, "Wake · systems check");
                }
            }

            seenRests = Math.Max(seenRests, flow.RoutineRests);
            while (feed.Count > 40) feed.RemoveAt(0);
        }

        private void AddFeed(double at, string text)
        {
            feed.Add(ChapterCopy.ClockAt(at) + "  " + text);
        }

        private void BuildRulerTicks()
        {
            foreach (VisualElement tick in rulerTicks) tick.RemoveFromHierarchy();
            rulerTicks.Clear();
            double span = montageTarget - montageStart;
            if (!(span > 0d) || !ChapterCopy.Finite(span)) return;
            int firstDay = ChapterCopy.DayAt(montageStart) + 1;
            int lastDay = ChapterCopy.DayAt(montageTarget);
            for (int day = firstDay; day <= lastDay && rulerTicks.Count < 40; day++)
            {
                double boundary = (day - 1) * ChapterCopy.SecondsPerDay;
                float percent = (float)Math.Max(0d, Math.Min(1d, (boundary - montageStart) / span)) * 100f;
                var tick = new VisualElement { name = "chapter-ruler-tick-" + day, pickingMode = PickingMode.Ignore };
                tick.AddToClassList("chapter-ruler-tick");
                tick.style.left = Length.Percent(percent);
                var label = new Label("Day " + day.ToString(CultureInfo.InvariantCulture)) { pickingMode = PickingMode.Ignore, enableRichText = false };
                label.AddToClassList("chapter-ruler-label");
                tick.Add(label);
                view.Ruler.Add(tick);
                rulerTicks.Add(tick);
            }
        }

        private void FillMontage(double now, bool reduced)
        {
            view.Montage.style.opacity = reduced ? 1f : Mathf.Clamp01(montageSeconds / MontageFade);
            double span = montageTarget - montageStart;
            double done = now - montageStart;
            float progress = span > 0d && ChapterCopy.Finite(span) ? (float)Math.Max(0d, Math.Min(1d, done / span)) : 1f;
            int startDay = ChapterCopy.DayAt(montageStart), targetDay = ChapterCopy.DayAt(montageTarget);
            int crossed = ChapterCopy.CalendarDaysCrossed(montageStart, montageTarget);
            ChapterView.SetText(view.MontageKicker, "ROUTINE · DAY " + startDay.ToString(CultureInfo.InvariantCulture)
                + " → DAY " + targetDay.ToString(CultureInfo.InvariantCulture) + " · "
                + ChapterCopy.Plural(crossed, "CALENDAR DAY", "CALENDAR DAYS") + " OF ORDINARY LIFE");
            ChapterView.SetText(view.MontageDay, "DAY " + ChapterCopy.DayAt(now).ToString(CultureInfo.InvariantCulture));
            ChapterView.SetText(view.MontageClock, ChapterCopy.ClockAt(now));
            string targetName = flow.Chapter == MissionChapter.Rendezvous ? "docking of the recovery craft"
                : ChapterCopy.NextName(flow.NextChapter).ToLowerInvariant();
            ChapterView.SetText(view.MontageRange, ChapterCopy.DayAndClock(montageStart) + "  →  "
                + ChapterCopy.DayAndClock(montageTarget) + " · " + targetName);
            view.RulerFill.style.width = Length.Percent(progress * 100f);
            view.RulerMarker.style.left = Length.Percent(progress * 100f);
            ChapterView.SetText(view.MontageElapsed, ChapterCopy.Span(done) + " of " + ChapterCopy.Span(span)
                + " · meals, systems checks and normal sleep of at most " + ChapterCopy.NightLimit + " a night. Not hibernation.");
            bool resting = recovery != null && recovery.IsResting;
            ChapterView.SetText(view.MontageActivity, resting
                ? "Now: asleep in the secured sleeping bag · night " + Math.Max(1, flow.RoutineRests).ToString(CultureInfo.InvariantCulture)
                : "Now: awake · meals, water and systems checks");
            view.MontageActivity.EnableInClassList("chapter-asleep", resting);
            view.TallyMeals.text = flow.RoutineRations.ToString(CultureInfo.InvariantCulture);
            view.TallyWater.text = flow.RoutineDrinks.ToString(CultureInfo.InvariantCulture);
            view.TallyNights.text = flow.RoutineRests.ToString(CultureInfo.InvariantCulture);
            FillReserves();
            view.SetFeed(feed);
        }

        private void FillReserves()
        {
            bool known = resources != null;
            ResourceSnapshot now = known ? resources.Snapshot : default;
            SetReserve(0, known, ChapterCopy.Mass(now.FoodKg), now.FoodKg - montageBaseline.FoodKg, "0.0");
            SetReserve(1, known, ChapterCopy.Litres(now.WaterLitres), now.WaterLitres - montageBaseline.WaterLitres, "0.0");
            SetReserve(2, known, ChapterCopy.Mass(now.OxygenKg), now.OxygenKg - montageBaseline.OxygenKg, "0.0");
            SetReserve(3, known, ChapterCopy.Energy(now.BatteryKWh), now.BatteryKWh - montageBaseline.BatteryKWh, "0.00");
            SetReserve(4, known, ChapterCopy.Ppm(now.CarbonDioxidePpm), now.CarbonDioxidePpm - montageBaseline.CarbonDioxidePpm, "0");
        }

        private void SetReserve(int index, bool known, string value, double change, string format)
        {
            ChapterView.SetText(view.ReserveValues[index], known ? value : "—");
            ChapterView.SetText(view.ReserveDeltas[index], known && baselineKnown ? ChapterCopy.Delta(change, format) : string.Empty);
        }

        // ---------- modal card ----------

        private void FillCard(double now)
        {
            ResetCardOptions();
            ChapterView.SetText(view.CardMeta, string.Empty);
            ChapterView.SetText(view.CardFacts, string.Empty);
            ChapterView.SetText(view.CardNote, string.Empty);
            ChapterView.Show(view.CardDismiss, false);
            ChapterView.SetTone(view.Card, ChapterTone.Neutral);
            if (screen == ChapterScreen.Message && current != null)
            {
                FillMessage(now);
            }
            else if (screen == ChapterScreen.Decision)
            {
                FillDecision(flow.PendingDecision, now);
            }
            else if (screen == ChapterScreen.Beacon)
            {
                FillBeacon(now);
            }

            ChapterView.SetText(view.CardFeedback, cardFeedback);
        }

        private void FillMessage(double now)
        {
            switch (current.Kind)
            {
                case ChapterCueKind.HopeMessage:
                    ChapterView.SetTone(view.Card, ChapterTone.Info);
                    ChapterView.SetText(view.CardKicker, "MESSAGE FROM EARTH · RECEIVED " + ChapterCopy.DayAndClock(current.ObservedAt).ToUpperInvariant());
                    ChapterView.SetText(view.CardTitle, "Help is coming");
                    ChapterView.SetText(view.CardBody, ChapterCopy.HopeText(current.ReportDay));
                    ChapterView.SetText(view.CardNote, ChapterCopy.FictionNote);
                    SetDismiss("Understood");
                    break;
                case ChapterCueKind.FailureDispatch:
                    cardDecision = ChapterDecision.AcknowledgeRescueFailure;
                    ChapterView.SetTone(view.Card, ChapterTone.Danger);
                    ChapterView.SetText(view.CardKicker, "MESSAGE FROM EARTH · DELAYED BY LIGHT TIME");
                    ChapterView.SetText(view.CardTitle, ChapterCopy.DecisionTitle(ChapterDecision.AcknowledgeRescueFailure));
                    ChapterView.SetText(view.CardMeta, "Sent " + ChapterCopy.DayAndClock(current.TransmittedAt) + " · received "
                        + ChapterCopy.DayAndClock(current.ReceivedAt) + " · " + ChapterCopy.Transit(current.ReceivedAt - current.TransmittedAt) + " on the way");
                    ChapterView.SetText(view.CardBody, ChapterCopy.DispatchText(current.ContentId) + " Acknowledge it, then reassess the reserves through day 30.");
                    ChapterView.SetText(view.CardFacts, ReservesLine());
                    ChapterView.SetText(view.CardNote, ChapterCopy.FictionNote);
                    SetOptions(ChapterDecision.AcknowledgeRescueFailure);
                    SetDismiss("Later");
                    break;
                case ChapterCueKind.RescueCommitted:
                    ChapterView.SetTone(view.Card, ChapterTone.Info);
                    ChapterView.SetText(view.CardKicker, "NEW PLAN · " + ChapterCopy.DayAndClock(current.ObservedAt).ToUpperInvariant());
                    ChapterView.SetText(view.CardTitle, "A craft already in transit");
                    ChapterView.SetText(view.CardBody, ChapterCopy.CommittedText(current.DockDay));
                    ChapterView.SetText(view.CardFacts, ReservesLine());
                    ChapterView.SetText(view.CardNote, ChapterCopy.FictionNote);
                    SetDismiss("Understood");
                    break;
                case ChapterCueKind.BeaconDelayed:
                    ChapterView.SetTone(view.Card, ChapterTone.Danger);
                    ChapterView.SetText(view.CardKicker, "RENDEZVOUS DELAYED");
                    ChapterView.SetText(view.CardTitle, "The beacon window was missed");
                    ChapterView.SetText(view.CardBody, "The craft could not find the ship. It will ask for the beacon once more"
                        + (current.DockDay.HasValue ? " and dock on mission day " + current.DockDay.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)
                        + " if the crew is alive. A second missed window ends the mission.");
                    ChapterView.SetText(view.CardFacts, ReservesLine());
                    SetDismiss("Understood");
                    break;
            }
        }

        private void FillDecision(ChapterDecision decision, double now)
        {
            cardDecision = decision;
            ChapterView.SetTone(view.Card, decision == ChapterDecision.AcknowledgeRescueFailure ? ChapterTone.Danger : ChapterTone.Attention);
            ChapterView.SetText(view.CardKicker, ChapterCopy.DecisionKicker(decision) + " · " + ChapterCopy.DayAndClock(now).ToUpperInvariant());
            ChapterView.SetText(view.CardTitle, ChapterCopy.DecisionTitle(decision));
            if (decision == ChapterDecision.AcknowledgeRescueFailure && flow.LatestDispatch.Id != null)
            {
                MissionDispatch dispatch = flow.LatestDispatch;
                ChapterView.SetText(view.CardMeta, "Sent " + ChapterCopy.DayAndClock(dispatch.TransmittedAtSimulationSeconds) + " · received "
                    + ChapterCopy.DayAndClock(dispatch.ReceivedAtSimulationSeconds) + " · "
                    + ChapterCopy.Transit(dispatch.ReceivedAtSimulationSeconds - dispatch.TransmittedAtSimulationSeconds) + " on the way");
                ChapterView.SetText(view.CardBody, ChapterCopy.DispatchText(dispatch.ContentId) + " Acknowledge it, then reassess the reserves through day 30.");
            }
            else
            {
                ChapterView.SetText(view.CardBody, ChapterCopy.DecisionBody(decision));
            }

            string facts = ReservesLine();
            if (decision == ChapterDecision.GrowOrBeacon && recovery?.ArrivalAtSimulationSeconds is double arrival)
                facts += "\nRendezvous: the craft docks " + ChapterCopy.DayAndClock(arrival) + " if the crew is alive.";
            ChapterView.SetText(view.CardFacts, facts);
            SetOptions(decision);
            SetDismiss("Decide later");
        }

        private void FillBeacon(double now)
        {
            ChapterView.SetTone(view.Card, ChapterTone.Attention);
            ChapterView.SetText(view.CardKicker, "RENDEZVOUS · " + ChapterCopy.DayAndClock(now).ToUpperInvariant());
            ChapterView.SetText(view.CardTitle, "The recovery craft asks for the beacon");
            ChapterView.SetText(view.CardMeta, ChapterCopy.BeaconWindow(recovery.BeaconWindowEndsAtSimulationSeconds, now));
            CommandResult check = recovery.CheckPowerBeacon();
            ChapterView.SetText(view.CardBody, "Power the recovery beacon at the cockpit console. Without it the craft cannot find the ship."
                + (string.IsNullOrEmpty(check.Explanation) ? string.Empty : "\n" + ChapterCopy.Sentence(check.Explanation)));
            ChapterView.SetText(view.CardFacts, ReservesLine());
            ChapterView.SetText(view.CardNote, ChapterCopy.FictionNote);
            SetDismiss("Go to the cockpit console");
        }

        private void SetOptions(ChapterDecision decision)
        {
            cardOptions = ChapterCopy.Options(decision);
            for (int i = 0; i < view.CardOptions.Length; i++)
            {
                bool shown = i < cardOptions.Length;
                ChapterView.Show(view.CardOptions[i], shown);
                if (!shown) continue;
                ChapterOptionCopy option = cardOptions[i];
                string detail;
                bool enabled = true;
                if (option.Kind == ChapterOptionKind.Choose)
                {
                    CommandResult check = flow.CheckChoose(option.Choice);
                    detail = ChapterCopy.Sentence(check.Explanation);
                    enabled = check.Succeeded;
                }
                else
                {
                    detail = option.Station;
                }

                ChapterView.SetText(view.CardOptionTitles[i], option.Title);
                ChapterView.SetText(view.CardOptionDetails[i], detail);
                view.CardOptions[i].SetEnabled(enabled);
                view.CardOptions[i].EnableInClassList("chapter-option-physical", option.Kind == ChapterOptionKind.Physical);
            }
        }

        private void ResetCardOptions()
        {
            cardOptions = Array.Empty<ChapterOptionCopy>();
            foreach (Button option in view.CardOptions) ChapterView.Show(option, false);
        }

        private void SetDismiss(string text)
        {
            ChapterView.Show(view.CardDismiss, true);
            view.CardDismissText.text = text;
        }

        private string ReservesLine()
        {
            if (resources == null) return string.Empty;
            ResourceSnapshot s = resources.Snapshot;
            return "Now aboard: food " + ChapterCopy.Mass(s.FoodKg) + " · water " + ChapterCopy.Litres(s.WaterLitres)
                + " · oxygen " + ChapterCopy.Mass(s.OxygenKg) + " · battery " + ChapterCopy.Energy(s.BatteryKWh)
                + " · " + ChapterCopy.Plural(s.SparePartsCount, "spare part", "spare parts");
        }

        // ---------- next-routine strip ----------

        private bool FillStrip(double now, float realSeconds)
        {
            if (state.Phase == GamePhase.Eva) return false;
            if (stripFeedbackSeconds > 0f) stripFeedbackSeconds = Mathf.Max(0f, stripFeedbackSeconds - realSeconds);
            string feedback = stripFeedbackSeconds > 0f ? stripFeedback : string.Empty;
            ChapterDecision pending = flow.PendingDecision;
            if (pending != ChapterDecision.None)
            {
                ChapterView.SetTone(view.Strip, ChapterTone.Attention);
                ChapterView.SetText(view.StripKicker, "DECISION PENDING");
                ChapterView.SetText(view.StripTitle, ChapterCopy.DecisionTitle(pending));
                ChapterView.SetText(view.StripDetail, feedback.Length > 0 ? feedback : "The routine waits for this decision.");
                view.StripActionText.text = "Review the decision";
                view.StripAction.SetEnabled(true);
                return true;
            }

            if (recovery != null && recovery.Stage == RecoveryStage.BeaconRequested)
            {
                ChapterView.SetTone(view.Strip, ChapterTone.Attention);
                ChapterView.SetText(view.StripKicker, "BEACON REQUESTED");
                ChapterView.SetText(view.StripTitle, "Power the beacon at the cockpit console");
                ChapterView.SetText(view.StripDetail, ChapterCopy.BeaconWindow(recovery.BeaconWindowEndsAtSimulationSeconds, now));
                view.StripActionText.text = "Show the request";
                view.StripAction.SetEnabled(true);
                return true;
            }

            // Core's answer is re-read at most every 0.25 s, and at once when anything it depends on visibly changes.
            int stateKey = ((int)state.Phase * 4) + (recovery != null && recovery.IsResting ? 1 : 0)
                + (survival != null && survival.IsActive ? 2 : 0) + ((int)(recovery?.Stage ?? RecoveryStage.NotStarted) * 64);
            checkIn -= realSeconds;
            if (!hasCheck || checkIn <= 0f || checkedRevision != flow.Revision || checkedStateKey != stateKey)
            {
                lastCheck = flow.CheckContinueRoutine();
                hasCheck = true;
                checkIn = CheckInterval;
                checkedRevision = flow.Revision;
                checkedStateKey = stateKey;
            }

            double? target = flow.NextChapterAtSimulationSeconds;
            bool rendezvous = flow.Chapter == MissionChapter.Rendezvous;
            if (!target.HasValue || (!rendezvous && flow.NextChapter == MissionChapter.None)) return false;
            if (rendezvous && recovery != null && recovery.Stage == RecoveryStage.Arrived) return false;
            ChapterView.SetTone(view.Strip, lastCheck.Succeeded ? ChapterTone.Neutral : ChapterTone.Attention);
            ChapterView.SetText(view.StripKicker, rendezvous ? "NEXT · DOCKING" : "NEXT CHAPTER");
            ChapterView.SetText(view.StripTitle, rendezvous
                ? ChapterCopy.DayAndClock(target.Value) + " — the recovery craft docks"
                : "Day " + ChapterCopy.DayAt(target.Value).ToString(CultureInfo.InvariantCulture) + " — " + ChapterCopy.NextName(flow.NextChapter));
            string detail = lastCheck.Succeeded
                ? (target.Value > now
                    ? ChapterCopy.Span(target.Value - now) + " of routine: meals, systems checks and ordinary sleep."
                    : "Due now.")
                : ChapterCopy.Sentence(lastCheck.Explanation);
            ChapterView.SetText(view.StripDetail, feedback.Length > 0 ? feedback : detail);
            view.StripActionText.text = "Continue the routine";
            view.StripAction.SetEnabled(lastCheck.Succeeded);
            return true;
        }

        // ---------- input ----------

        private void ReadShortcuts()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            bool r = keyboard.rKey.wasPressedThisFrame;
            bool one = keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
            bool two = keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
            bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            switch (screen)
            {
                case ChapterScreen.Montage:
                    if (r) RequestStopRoutine();
                    break;
                case ChapterScreen.Decision:
                case ChapterScreen.Message:
                    if (one && cardOptions.Length > 0) ChooseOption(0);
                    else if (two && cardOptions.Length > 1) ChooseOption(1);
                    else if (enter)
                    {
                        if (screen == ChapterScreen.Message && cardOptions.Length == 1) ChooseOption(0);
                        else if (screen == ChapterScreen.Message) DismissCard();
                    }
                    break;
                case ChapterScreen.Beacon:
                    if (enter) DismissCard();
                    break;
                case ChapterScreen.Report:
                case ChapterScreen.Beat:
                    if (enter) DismissCard();
                    else if (r && screen == ChapterScreen.Beat) RequestContinueRoutine();
                    break;
                default:
                    if (r) RequestContinueRoutine();
                    break;
            }
        }

        // ---------- plumbing ----------

        private bool EnsureView()
        {
            if (!document) return false;
            VisualElement root = document.rootVisualElement;
            if (root == null) return false;
            if (view == null)
            {
                view = new ChapterView();
                view.StripActionClicked += () => RequestContinueRoutine();
                view.MontageStopClicked += () => RequestStopRoutine();
                view.ReportDismissClicked += DismissCard;
                view.CardOptionClicked += index => ChooseOption(index);
                view.CardDismissClicked += DismissCard;
            }

            // Inside the flight board when it exists (below the session menu and the mission record), else the root.
            VisualElement host = root.Q("mission-flight-board") ?? root;
            view.Attach(host, styleSheet);
            return view.Layer.panel != null;
        }

        private bool Gate()
        {
            if (presentationGate == null) return false;
            try
            {
                return presentationGate();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                return false;
            }
        }

        private void ResetTransient()
        {
            current = null;
            currentSeconds = modalSeconds = montageSeconds = 0f;
            screen = ChapterScreen.None;
            deferred.Clear();
            lastPending = ChapterDecision.None;
            beaconDeferred = false;
            cardOptions = Array.Empty<ChapterOptionCopy>();
            cardFeedback = string.Empty;
            cardScreen = ChapterScreen.None;
            cardCue = null;
            cardDecision = ChapterDecision.None;
            montageObserved = false;
            baselineKnown = false;
            feed.Clear();
            hasCheck = false;
            checkedRevision = -1;
            checkedStateKey = -1;
            stripFeedback = string.Empty;
            stripFeedbackSeconds = 0f;
        }

        private static bool IsChapterPhase(GamePhase phase) =>
            phase == GamePhase.Emergency || phase == GamePhase.Repair || phase == GamePhase.Eva
            || phase == GamePhase.Transmission || phase == GamePhase.Survival;

        private static CommandResult Refused(string text) => new CommandResult(false, CommandFailure.PreconditionsNotMet, text);

        private static float Ease(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - (2f * t));
        }
    }
}
