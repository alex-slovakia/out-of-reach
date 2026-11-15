using System;
using System.Collections.Generic;
using System.Globalization;
using OutOfReach.Contracts;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// The approved day 1-30 chapter loop (docs/APPROVED-CHAPTER-LOOP-2026-11-15.md), opt-in, implementing
    /// <see cref="IMissionChapterFlow"/>. It owns no second calendar, ledger or need: the beats are thresholds on the
    /// one mission clock (day N begins at (N - 1) x 86,400 simulated seconds, <see cref="MissionCalendar"/>).
    /// <para>
    /// ROUTINE. Between beats the session clock runs at <see cref="RoutineTimeScale"/> through the ordinary step loop, so
    /// oxygen, CO2, power, crop growth, communications and the needs advance exactly as in play. Core itself issues the
    /// real commands a crew day consists of: a stored ration when one is due (<see cref="SurvivalExtension.TryConsumeRation"/>),
    /// a water portion when one is due, and after <see cref="RoutineAwakeHours"/> awake an ordinary rest of at most
    /// <see cref="SurvivalRecovery.MaximumRestHours"/> h with the rest accounting (BVAD draw, needs paused). Nothing is
    /// granted at a boundary. The step loop splits exactly at each of these times, and after every step the routine
    /// stops synchronously, keeping the actual time, at the first of: a danger (power, CO2), a store running out or a
    /// portion that cannot be paid (resource shortage), the mission ending, a received dispatch, a time-critical
    /// request, the crop finishing or failing, or reaching the next beat. A danger wins a tie with anything else.
    /// </para>
    /// <para>
    /// STORY (fictional, GAME ASSUMPTIONS): Earth's reply on day 9 announces a first rescue attempt. A dispatch received
    /// on day 14 at 08:00 (or later, once a powered link exists; it left Earth one one-way light time earlier, from the
    /// packaged NASA/JPL sample) reports that attempt failed; acknowledging it commits a craft that is already in transit
    /// nearby to a rendezvous on day 30, which asks for the beacon (existing window, cost and missed-window delay).
    /// No Earth launch on day 14 reaches the ship.
    /// </para>
    /// </summary>
    public sealed class MissionChapterFlow : IMissionChapterFlow, IDisposable
    {
        /// <summary>GAME ASSUMPTION: montage speed, simulated seconds per real second (6 h per second; 3 days ~ 12 s).</summary>
        public const double RoutineTimeScale = 21600d;

        /// <summary>GAME ASSUMPTION: an ordinary crew day is 15.5 h awake and then one rest of at most 8.5 h.</summary>
        public const double RoutineAwakeHours = 24d - SurvivalRecovery.MaximumRestHours;

        /// <summary>GAME ASSUMPTION: every beat starts at this hour of its mission day.</summary>
        public const double BeatStartHour = 8d;

        /// <summary>GAME ASSUMPTION: the in-transit craft asks for the beacon this long before it docks.</summary>
        public const double BeaconRequestBeforeArrivalHours = 10d;

        /// <summary>
        /// GAME ASSUMPTION (Alex, 15 Nov 2026): after the day-14 news each routine step covers at most this many days
        /// (it ends at <see cref="BeatStartHour"/> of that day), so days 14 to 30 pass in short, observable steps.
        /// </summary>
        public const int MaximumStepDaysAfterNews = 3;

        /// <summary>
        /// GAME ASSUMPTION: a new night starts only after the crew has been up this long since it last woke (morning meal
        /// and systems checks). It also keeps repeated berth presses from chaining nights.
        /// </summary>
        public const double MinimumAwakeHoursBeforeNight = 1d;

        public const int AccidentDay = 1;
        public const int ConsequencesDay = 5;
        public const int ContactDay = 9;
        public const int RescueFailedDay = 14;
        public const int FinalDecisionDay = 20;
        public const int RendezvousDay = 30;

        /// <summary>Dispatch id of the day-14 message.</summary>
        public const string RescueFailedDispatchId = "rescue-1-failed";

        /// <summary>Presentation content id (subtitle/voice) of the day-14 message.</summary>
        public const string RescueFailedContentId = "dispatch.rescue-1-failed";

        private const double Hour = 3600d;
        private const double Day = CrewMetabolicAssumptions.SecondsPerDay;

        private readonly MissionEventBus events;
        private readonly MissionGameState state;
        private readonly ResourceLedger resources;
        private readonly SimulationClock clock;
        private readonly CinematicCoordinator cinematics;
        private readonly SurvivalExtension survival;
        private readonly SurvivalRecovery recovery;
        private readonly ShipSystems systems;
        private readonly EvaOperations eva;
        private readonly FieldModifications modifications;
        private readonly MissionCalendar calendar;
        private readonly Func<bool> linkPowered;
        private readonly Action<string> log;
        private bool running;
        private double scaleBeforeRoutine;
        private double routineTarget;
        private bool routineTargetIsBeat = true;
        private GrowModuleStage cropAtRoutineStart;
        private double? dispatchDueAt;
        private double dispatchSentAt;
        private bool disposed;

        internal MissionChapterFlow(MissionEventBus events, MissionGameState state, ResourceLedger resources, SimulationClock clock,
            CinematicCoordinator cinematics, SurvivalExtension survival, SurvivalRecovery recovery, ShipSystems systems,
            EvaOperations eva, FieldModifications modifications, MissionCalendar calendar, Func<bool> linkPowered,
            Action<string> log)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.cinematics = cinematics ?? throw new ArgumentNullException(nameof(cinematics));
            this.survival = survival ?? throw new ArgumentNullException(nameof(survival));
            this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
            this.systems = systems ?? throw new ArgumentNullException(nameof(systems));
            this.eva = eva ?? throw new ArgumentNullException(nameof(eva));
            this.modifications = modifications ?? throw new ArgumentNullException(nameof(modifications));
            this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
            this.linkPowered = linkPowered ?? throw new ArgumentNullException(nameof(linkPowered));
            this.log = log;
            survival.ChapterMode = true;
            recovery.ChapterMode = true;
            survival.CabinBlocker = DescribeCabinBlock;
            recovery.RuntimeReleased = () => StopRoutine(RoutineStopReason.PlayerCancelled, "the runtime that drives the clock was disabled");
        }

        public bool Enabled => true;

        public MissionChapter Chapter { get; private set; } = MissionChapter.Accident;

        public MissionChapter NextChapter => Next(Chapter);

        public double? NextChapterAtSimulationSeconds
        {
            get
            {
                if (Chapter == MissionChapter.Rendezvous)
                {
                    return recovery.ArrivalAtSimulationSeconds;
                }

                MissionChapter next = NextChapter;
                return next == MissionChapter.None ? (double?)null : RoutineTarget() ?? BeatStart(next);
            }
        }

        public bool IsRoutineRunning => running && !disposed && !state.IsClosed;

        public double? RoutineStartedAtSimulationSeconds { get; private set; }

        public double? RoutineStoppedAtSimulationSeconds { get; private set; }

        public RoutineStopReason LastRoutineStop { get; private set; }

        public string LastRoutineStopText { get; private set; } = string.Empty;

        public int RoutineRations { get; private set; }

        public int RoutineDrinks { get; private set; }

        public int RoutineRests { get; private set; }

        public ChapterDecision PendingDecision { get; private set; }

        /// <summary>
        /// True when the last routine stop was one step of the post-news pacing (<see cref="MaximumStepDaysAfterNews"/>):
        /// reported as <see cref="RoutineStopReason.ReachedChapter"/> with the chapter unchanged.
        /// </summary>
        public bool LastStopWasStep { get; private set; }

        public RescuePlan Rescue { get; private set; }

        public MissionDispatch LatestDispatch { get; private set; }

        public int Revision { get; private set; }

        /// <summary>
        /// How the day-5 decision was settled, or null while open: "Science instrument kept", "Science instrument salvaged",
        /// "Grow module built", "Reserves conserved", or "Grow module built on mission day N" (built by the crew before day 5).
        /// </summary>
        public string ConsequencesResolution { get; private set; }

        /// <summary>The choice taken on day 20, or None.</summary>
        public ChapterChoice FinalDecision { get; private set; }

        /// <summary>When each beat began, simulation seconds (one-time presentation cues and the report).</summary>
        public IReadOnlyDictionary<MissionChapter, double> ChapterStartedAt => chapterStartedAt;

        private readonly Dictionary<MissionChapter, double> chapterStartedAt = new Dictionary<MissionChapter, double> { { MissionChapter.Accident, 0d } };

        public event Action Changed;

        /// <summary>Simulation time a beat begins: <see cref="BeatStartHour"/> on its mission day.</summary>
        public static double BeatStart(MissionChapter chapter)
        {
            return ((DayOf(chapter) - 1) * Day) + (BeatStartHour * Hour);
        }

        public static int DayOf(MissionChapter chapter)
        {
            switch (chapter)
            {
                case MissionChapter.Accident: return AccidentDay;
                case MissionChapter.Consequences: return ConsequencesDay;
                case MissionChapter.Contact: return ContactDay;
                case MissionChapter.RescueFailed: return RescueFailedDay;
                case MissionChapter.FinalDecision: return FinalDecisionDay;
                case MissionChapter.Rendezvous: return RendezvousDay;
                default: return 0;
            }
        }

        /// <summary>
        /// The beat after <paramref name="chapter"/>. Once Earth's reply has arrived (contact made early), the day-9 stop has
        /// nothing left to do, so the routine from day 5 runs on to the day-14 news (Alex's pacing: second story sleep to 14).
        /// </summary>
        private MissionChapter Next(MissionChapter chapter)
        {
            switch (chapter)
            {
                case MissionChapter.Accident: return MissionChapter.Consequences;
                case MissionChapter.Consequences: return Rescue == RescuePlan.None ? MissionChapter.Contact : MissionChapter.RescueFailed;
                case MissionChapter.Contact: return MissionChapter.RescueFailed;
                case MissionChapter.RescueFailed: return MissionChapter.FinalDecision;
                case MissionChapter.FinalDecision: return MissionChapter.Rendezvous;
                default: return MissionChapter.None;
            }
        }

        public CommandResult CheckContinueRoutine()
        {
            CommandResult control = CheckControl();
            if (!control.Succeeded)
            {
                return control;
            }

            if (IsRoutineRunning)
            {
                return Fail(CommandFailure.AlreadyComplete, "The routine is already running.");
            }

            if (recovery.IsResting)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "The crew is resting. Wake up first.");
            }

            string cabin = DescribeCabinBlock();
            if (cabin != null)
            {
                return Fail(CommandFailure.PreconditionsNotMet, cabin);
            }

            if (PendingDecision != ChapterDecision.None)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Decide first: " + DescribeDecision(PendingDecision) + ".");
            }

            if (recovery.Stage == RecoveryStage.BeaconRequested)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "The recovery craft is waiting for the beacon; answer it first.");
            }

            if (recovery.Stage == RecoveryStage.Arrived)
            {
                return Fail(CommandFailure.AlreadyComplete, "The recovery craft has docked.");
            }

            RestStopReason danger = recovery.CurrentDanger();
            if (danger != RestStopReason.None)
            {
                return Fail(danger == RestStopReason.FoodStoreEmpty || danger == RestStopReason.WaterStoreEmpty || danger == RestStopReason.OxygenStoreEmpty
                    ? CommandFailure.InsufficientResources : CommandFailure.PreconditionsNotMet, "No routine now: " + DescribeDanger(danger) + ".");
            }

            CommandResult portion = CheckDuePortions();
            if (!portion.Succeeded)
            {
                return portion;
            }

            string missing = MissingPrerequisite();
            if (missing != null)
            {
                return Fail(CommandFailure.PreconditionsNotMet, missing);
            }

            double? target = RoutineTarget();
            if (!target.HasValue)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "No later chapter is scheduled.");
            }

            double now = state.ElapsedSimulationSeconds;
            return Ok(target.Value <= now
                ? string.Format(CultureInfo.InvariantCulture, "Continue to {0} (already due)", Describe(NextOrArrival()))
                : string.Format(CultureInfo.InvariantCulture, "Routine days until {0} on mission day {1}: {2} of meals, checks and ordinary sleep",
                    Describe(NextOrArrival()), MissionCalendar.DayNumberAt(target.Value), FormatSpan(target.Value - now)));
        }

        public CommandResult TryContinueRoutine()
        {
            return ContinueRoutine(false);
        }

        /// <summary>
        /// The berth in the chapter loop. When the routine may continue it is the STORY SLEEP: the night begins at once and
        /// the routine runs on to its next stop (one step of the pacing). Otherwise an ordinary rest, only at the end of an
        /// ordinary waking day. A resting crew is woken. A running routine refuses, so no press can skip time twice.
        /// </summary>
        public CommandResult CheckSleepInteraction()
        {
            if (IsRoutineRunning)
            {
                return Fail(CommandFailure.AlreadyComplete, "The routine is already under way.");
            }

            if (recovery.IsResting)
            {
                return recovery.CheckWake();
            }

            CommandResult routine = CheckContinueRoutine();
            double now0 = state.ElapsedSimulationSeconds;
            double awake = Math.Max(0d, now0 - AwakeSince());
            double? woke = recovery.LastRestEndedAtSimulationSeconds;
            if (routine.Succeeded)
            {
                // After a real wake the crew is up for the morning meal and checks before the next night: a repeated press
                // right after waking (or during the body's release) cannot chain nights.
                double sinceWake = woke.HasValue ? Math.Max(0d, now0 - woke.Value) : double.PositiveInfinity;
                if (sinceWake < MinimumAwakeHoursBeforeNight * Hour)
                {
                    return Fail(CommandFailure.PreconditionsNotMet, string.Format(CultureInfo.InvariantCulture,
                        "The crew woke {0} ago: morning meal and checks first; the next night can start in {1}.",
                        FormatSpan(sinceWake), FormatSpan((MinimumAwakeHoursBeforeNight * Hour) - sinceWake)));
                }

                CommandResult rest = recovery.CheckRest(SurvivalRecovery.MaximumRestHours);
                if (!rest.Succeeded)
                {
                    return rest;
                }

                double target = RoutineTarget().Value;
                double now = state.ElapsedSimulationSeconds;
                return Ok(string.Format(CultureInfo.InvariantCulture, "Sleep and continue to {0}, mission day {1} ({2} of meals, checks and ordinary sleep)",
                    IsBeatTarget(target) ? Describe(NextOrArrival()) : "the next routine stop",
                    MissionCalendar.DayNumberAt(target), FormatSpan(Math.Max(0d, target - now))));
            }

            if (awake < RoutineAwakeHours * Hour)
            {
                return Fail(CommandFailure.PreconditionsNotMet, string.Format(CultureInfo.InvariantCulture,
                    "Not tired yet: awake {0} of an ordinary {1:0.#} h day. {2}", FormatSpan(awake), RoutineAwakeHours, routine.Explanation));
            }

            return recovery.CheckRest(SurvivalRecovery.MaximumRestHours);
        }

        public CommandResult TrySleepInteraction()
        {
            CommandResult check = CheckSleepInteraction();
            if (!check.Succeeded)
            {
                return check;
            }

            if (recovery.IsResting)
            {
                return recovery.TryWake();
            }

            return CheckContinueRoutine().Succeeded
                ? ContinueRoutine(true)
                : recovery.TryBeginRest(SurvivalRecovery.MaximumRestHours);
        }

        private CommandResult ContinueRoutine(bool sleepNow)
        {
            CommandResult check = CheckContinueRoutine();
            if (!check.Succeeded)
            {
                return check;
            }

            using (events.DeferDispatch())
            {
                double now = state.ElapsedSimulationSeconds;
                double target = RoutineTarget().Value;
                RoutineRations = 0;
                RoutineDrinks = 0;
                RoutineRests = 0;
                LastStopWasStep = false;
                RoutineStartedAtSimulationSeconds = now;
                RoutineStoppedAtSimulationSeconds = null;
                LastRoutineStop = RoutineStopReason.None;
                LastRoutineStopText = string.Empty;
                if (target <= now)
                {
                    // Prerequisites were met late: the beat is already due. No time passes and nothing is skipped.
                    running = true;
                    scaleBeforeRoutine = clock.TimeScale;
                    StopRoutine(EnterNextBeat(), null);
                    return check;
                }

                running = true;
                routineTarget = target;
                routineTargetIsBeat = IsBeatTarget(target);
                cropAtRoutineStart = survival.GrowStage;
                scaleBeforeRoutine = clock.TimeScale;
                clock.TrySetTimeScale(RoutineTimeScale);
                log?.Invoke(string.Format(CultureInfo.InvariantCulture, "Routine began on mission day {0}: {1}.", MissionCalendar.DayNumberAt(now), check.Explanation));
                if (sleepNow && recovery.CheckRest(SurvivalRecovery.MaximumRestHours).Succeeded)
                {
                    // The story sleep: the berth's night is the routine's first night (same accounting as every night).
                    recovery.BeginRest(SurvivalRecovery.MaximumRestHours, clock.TimeScale, true);
                    RoutineRests++;
                }

                Notify();
                RunSchedule();
            }

            return check;
        }

        public CommandResult TryCancelRoutine()
        {
            if (!IsRoutineRunning)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "No routine is running.");
            }

            StopRoutine(RoutineStopReason.PlayerCancelled, "the crew stopped the routine");
            return Ok("Routine stopped at the current time.");
        }

        public CommandResult CheckChoose(ChapterChoice choice)
        {
            CommandResult control = CheckControl();
            if (!control.Succeeded)
            {
                return control;
            }

            if (IsRoutineRunning)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Stop the routine first.");
            }

            ChapterDecision needed = DecisionFor(choice);
            if (needed == ChapterDecision.None)
            {
                return Fail(CommandFailure.InvalidInput, "Unknown chapter choice.");
            }

            if (PendingDecision != needed)
            {
                return Fail(PendingDecision == ChapterDecision.None ? CommandFailure.AlreadyComplete : CommandFailure.PreconditionsNotMet,
                    PendingDecision == ChapterDecision.None ? "No decision is pending." : "Decide first: " + DescribeDecision(PendingDecision) + ".");
            }

            switch (choice)
            {
                case ChapterChoice.PreserveScience:
                    return Ok("Keep the science instrument: its load stays on the bus and no spare parts are recovered");
                case ChapterChoice.AcknowledgeRescueFailure:
                    return Ok("Acknowledge: the first rescue attempt has failed; reassess the reserves through day 30");
                case ChapterChoice.StopGrowForBeacon:
                    if (survival.GrowStage != GrowModuleStage.Growing)
                    {
                        return Fail(CommandFailure.PreconditionsNotMet, "No crop is growing; there is nothing to stop.");
                    }

                    double toArrival = Math.Max(0d, (recovery.ArrivalAtSimulationSeconds ?? state.ElapsedSimulationSeconds) - state.ElapsedSimulationSeconds);
                    double untilHarvest = survival.CropRemainingSimulationSeconds;
                    double saved = Math.Min(toArrival, untilHarvest);
                    return Ok(string.Format(CultureInfo.InvariantCulture,
                        "Stop the grow module: saves up to {0:0.##} kWh and {1:0.###} L until it would ripen; the crop is lost, nothing is refunded",
                        survival.Spec.GrowPowerWatts * saved / 3.6e6, survival.Spec.GrowWaterLitresPerDay * saved / Day));
                case ChapterChoice.KeepGrowRunning:
                    return Ok(survival.GrowStage == GrowModuleStage.Growing
                        ? "Keep the grow module running on the current power plan"
                        : "Keep the current power plan");
                case ChapterChoice.ConserveReserves:
                    return Ok(DescribeConserveReserves());
                default:
                    return Fail(CommandFailure.InvalidInput, "Unknown chapter choice.");
            }
        }

        public CommandResult TryChoose(ChapterChoice choice)
        {
            CommandResult check = CheckChoose(choice);
            if (!check.Succeeded)
            {
                return check;
            }

            using (events.DeferDispatch())
            {
                switch (choice)
                {
                    case ChapterChoice.PreserveScience:
                        ConsequencesResolution = "Science instrument kept";
                        log?.Invoke("Day-5 decision: the crew keeps the science instrument running.");
                        break;
                    case ChapterChoice.AcknowledgeRescueFailure:
                        CommitInTransitCraft();
                        break;
                    case ChapterChoice.StopGrowForBeacon:
                        CommandResult stopped = survival.StopGrowModule("Day-20 decision: the grow module is shut down to keep battery energy for the beacon. The crop is lost; nothing is credited or refunded.");
                        if (!stopped.Succeeded)
                        {
                            return stopped;
                        }

                        FinalDecision = choice;
                        break;
                    case ChapterChoice.KeepGrowRunning:
                        FinalDecision = choice;
                        log?.Invoke("Day-20 decision: the crew keeps the current power plan.");
                        break;
                    case ChapterChoice.ConserveReserves:
                        ConsequencesResolution = "Reserves conserved";
                        ResourceSnapshot kept = resources.Snapshot;
                        log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                            "Day-5 decision: the crew conserves its reserves ({0} spare part{1}, {2:0.##} kWh) for repairs and the beacon; no grow module is built.",
                            kept.SparePartsCount, kept.SparePartsCount == 1 ? string.Empty : "s", kept.BatteryKWh));
                        break;
                }

                PendingDecision = ChapterDecision.None;
                Notify();
            }

            return check;
        }

        /// <summary>
        /// Simulated seconds to the next routine boundary (a due portion, bedtime, the target, the CO2 danger margin) or
        /// a dispatch. The ledger supplies the store-depletion and battery (power loss) boundaries itself.
        /// </summary>
        internal double TimeToNextBoundary()
        {
            double boundary = double.PositiveInfinity;
            if (disposed || !state.IsSimulationRunning)
            {
                return boundary;
            }

            double now = state.ElapsedSimulationSeconds;
            if (dispatchDueAt.HasValue && !LatestDispatchReceived())
            {
                boundary = Earlier(boundary, dispatchDueAt.Value - now);
            }

            if (!IsRoutineRunning)
            {
                return boundary;
            }

            boundary = Earlier(boundary, routineTarget - now);

            // The CO2 warning is a stop of its own, awake as well as asleep: never step from outside the danger margin
            // straight to a later meal, bedtime or the loss limit.
            boundary = Earlier(boundary, resources.TimeUntilCarbonDioxideReaches(SurvivalRules.CarbonDioxideLossLimitPpm)
                - SurvivalRecovery.CarbonDioxideDangerMarginSeconds);
            if (!recovery.IsResting)
            {
                boundary = Earlier(boundary, survival.TimeUntilPortionDue(true));
                boundary = Earlier(boundary, survival.TimeUntilPortionDue(false));
                boundary = Earlier(boundary, AwakeSince() + (RoutineAwakeHours * Hour) - now);
            }

            return boundary;
        }

        /// <summary>The director's activation hook: the impact sequence ended (Emergency).</summary>
        internal void OnEmergencyBegan()
        {
            survival.ActivateForChapter();
        }

        /// <summary>The director's hook right after Earth's reply moved the mission into Survival (day-9 beat).</summary>
        internal void OnContact()
        {
            if (disposed || Rescue != RescuePlan.None)
            {
                return;
            }

            Rescue = RescuePlan.FirstRescuePlanned;
            double contactAt = state.ElapsedSimulationSeconds;
            // Fixed now, causally after Earth planned the rescue (its reply has already arrived): sent no earlier than
            // the reply's arrival, aimed to arrive on day 14; earliest receipt = send + one-way light time at the send epoch.
            double beat = BeatStart(MissionChapter.RescueFailed);
            dispatchSentAt = Math.Max(beat - OneWayLightSecondsAt(beat), contactAt);
            dispatchDueAt = dispatchSentAt + OneWayLightSecondsAt(dispatchSentAt);
            log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                "Earth relays concrete hope: a first rescue attempt is under way, an uncrewed fictional support vehicle already near Mars that will try to reach the ship. Mission control will report its progress (about mission day {0}).",
                MissionCalendar.DayNumberAt(dispatchDueAt.Value)));
            Notify();

            // Contact moved the mission into Survival and set its speed: a routine running meanwhile (the reply was
            // still travelling) stops here, at the actual time, so the news is seen.
            StopRoutine(RoutineStopReason.DispatchReceived, "Earth's reply arrived");
        }

        /// <summary>After a salvage or another field modification: the day-5 decision may be settled by the salvage itself.</summary>
        internal void OnFieldModified()
        {
            if (!disposed && PendingDecision == ChapterDecision.ScienceOrSalvage && modifications.SciencePayloadSalvaged)
            {
                ConsequencesResolution = "Science instrument salvaged";
                PendingDecision = ChapterDecision.None;
                log?.Invoke("Day-5 decision: the crew dismantles the science instrument for spare parts.");
                Notify();
            }
        }

        /// <summary>
        /// After any survival change (the director's hook): the existing paid grow-module build settles an open day-5
        /// GrowOrConserve decision. Nothing is charged here; the build command already charged its parts and energy.
        /// </summary>
        internal void OnSurvivalModified()
        {
            if (!disposed && PendingDecision == ChapterDecision.GrowOrConserve && survival.GrowStage != GrowModuleStage.Unbuilt)
            {
                ConsequencesResolution = "Grow module built";
                PendingDecision = ChapterDecision.None;
                log?.Invoke("Day-5 decision: the crew invests spare parts and battery energy in the grow module.");
                Notify();
            }
        }

        /// <summary>
        /// Day 5 always asks for a real decision on the actual reserves; it is never skipped:
        /// <list type="bullet">
        /// <item>a science payload that this ship can reach and salvage: <see cref="ChapterDecision.ScienceOrSalvage"/>
        /// (the existing salvage interaction, or <see cref="ChapterChoice.PreserveScience"/>);</item>
        /// <item>otherwise (no payload aboard, payload already salvaged, or not reachable in this ship):
        /// <see cref="ChapterDecision.GrowOrConserve"/> (the existing paid grow-module build, or
        /// <see cref="ChapterChoice.ConserveReserves"/>, which is always available);</item>
        /// <item>only if both investments were already made by the crew's own earlier commands (instrument salvaged or
        /// absent, and the grow module built before day 5) is the decision recorded as taken, with the day of that
        /// command, in <see cref="ConsequencesResolution"/> and the journal.</item>
        /// </list>
        /// </summary>
        private void OpenConsequencesDecision()
        {
            if (modifications.SalvageablePartsNow > 0)
            {
                PendingDecision = ChapterDecision.ScienceOrSalvage;
                return;
            }

            if (survival.GrowStage == GrowModuleStage.Unbuilt)
            {
                PendingDecision = ChapterDecision.GrowOrConserve;
                return;
            }

            int builtDay = MissionCalendar.DayNumberAt(survival.GrowModuleBuiltAtSimulationSeconds ?? state.ElapsedSimulationSeconds);
            ConsequencesResolution = string.Format(CultureInfo.InvariantCulture, "Grow module built on mission day {0}", builtDay);
            log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                "Day-5 decision already taken: the crew built the grow module on mission day {0}, before the inspection.", builtDay));
        }

        private string DescribeConserveReserves()
        {
            ResourceSnapshot now = resources.Snapshot;
            CommandResult build = survival.CheckBuildGrowModule();
            return string.Format(CultureInfo.InvariantCulture,
                "Conserve the reserves: keep {0} spare part{1} and {2:0.##} kWh for repairs and the beacon; no grow module ({3})",
                now.SparePartsCount, now.SparePartsCount == 1 ? string.Empty : "s", now.BatteryKWh,
                build.Succeeded
                    ? "the alternative is the grow module target: " + build.Explanation + ", then planting water, 70 W and a small food supplement later"
                    : "the grow module cannot be built now: " + build.Explanation);
        }

        /// <summary>
        /// Called by the director after every step, after the survival rules, the recovery timeline and the link: delivers
        /// a due dispatch, then stops the routine at its first reason or issues the crew day's next real command.
        /// </summary>
        internal void AfterStep()
        {
            if (disposed)
            {
                return;
            }

            bool dispatched = TryDeliverDispatch();
            if (!running)
            {
                return;
            }

            if (state.IsClosed || !state.IsSimulationRunning)
            {
                StopRoutine(RoutineStopReason.MissionEnded, "the mission ended");
                return;
            }

            RestStopReason danger = recovery.CurrentDanger();
            if (danger != RestStopReason.None)
            {
                StopRoutine(danger == RestStopReason.FoodStoreEmpty || danger == RestStopReason.WaterStoreEmpty || danger == RestStopReason.OxygenStoreEmpty
                    ? RoutineStopReason.ResourceShortage : RoutineStopReason.Danger, DescribeDanger(danger));
                return;
            }

            if (state.ElapsedSimulationSeconds >= routineTarget - SurvivalRules.TimeToleranceSeconds)
            {
                if (Chapter == MissionChapter.RescueFailed && Rescue != RescuePlan.InTransitCraftCommitted)
                {
                    // The routine of a late contact ran to the overdue message itself, not to the next beat.
                    if (dispatched)
                    {
                        StopRoutine(RoutineStopReason.DispatchReceived, "a message from Earth arrived");
                    }
                    else
                    {
                        StopRoutine(RoutineStopReason.PrerequisiteMissing, "Earth's message is due but the link cannot receive it (antenna or power)");
                    }
                }
                else if (!routineTargetIsBeat && (recovery.Stage == RecoveryStage.BeaconRequested || PendingDecision != ChapterDecision.None))
                {
                    // A time-critical request at the step's end (e.g. the second beacon window) is the stop, not the step.
                    StopRoutine(RoutineStopReason.DecisionRequired, recovery.Stage == RecoveryStage.BeaconRequested
                        ? "the recovery craft asks for the beacon" : DescribeDecision(PendingDecision));
                }
                else if (!routineTargetIsBeat)
                {
                    // One step of the post-news pacing ended; the beat itself is still ahead.
                    LastStopWasStep = true;
                    StopRoutine(RoutineStopReason.ReachedChapter, string.Format(CultureInfo.InvariantCulture,
                        "this routine step ended (at most {0} days per step); next: {1} on mission day {2}",
                        MaximumStepDaysAfterNews, Describe(NextOrArrival()), MissionCalendar.DayNumberAt(BeatTarget() ?? state.ElapsedSimulationSeconds)));
                }
                else
                {
                    StopRoutine(Chapter == MissionChapter.Rendezvous ? RoutineStopReason.ReachedChapter : EnterNextBeat(), null);
                }

                return;
            }

            if (dispatched)
            {
                StopRoutine(RoutineStopReason.DispatchReceived, "a message from Earth arrived");
                return;
            }

            if (recovery.Stage == RecoveryStage.BeaconRequested || PendingDecision != ChapterDecision.None)
            {
                StopRoutine(RoutineStopReason.DecisionRequired, recovery.Stage == RecoveryStage.BeaconRequested
                    ? "the recovery craft asks for the beacon" : DescribeDecision(PendingDecision));
                return;
            }

            GrowModuleStage crop = survival.GrowStage;
            if (crop != cropAtRoutineStart && crop == GrowModuleStage.HarvestReady)
            {
                StopRoutine(RoutineStopReason.CropReady, "the crop is ready to harvest");
                return;
            }

            if (crop != cropAtRoutineStart && crop == GrowModuleStage.Failed)
            {
                StopRoutine(RoutineStopReason.CropFailed, "the grow module failed");
                return;
            }

            RunSchedule();
        }

        /// <summary>The mission ended (any outcome): an active routine stops at that time.</summary>
        internal void OnMissionEnded()
        {
            StopRoutine(RoutineStopReason.MissionEnded, "the mission ended");
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            running = false;
            survival.CabinBlocker = null;
            recovery.RuntimeReleased = null;
            Changed = null;
        }

        /// <summary>One crew day's next real command, if it is due now: water, ration, or the night's ordinary rest.</summary>
        private void RunSchedule()
        {
            if (!IsRoutineRunning || recovery.IsResting)
            {
                return;
            }

            bool acted = false;
            survival.QuietJournal = true;
            try
            {
                acted = IssueDuePortions();
            }
            finally
            {
                survival.QuietJournal = false;
            }

            if (!IsRoutineRunning)
            {
                return;
            }

            double now = state.ElapsedSimulationSeconds;
            if (now >= AwakeSince() + (RoutineAwakeHours * Hour) - SurvivalRules.TimeToleranceSeconds)
            {
                CommandResult rest = recovery.CheckRest(SurvivalRecovery.MaximumRestHours);
                if (!rest.Succeeded)
                {
                    StopRoutine(RoutineStopReason.PrerequisiteMissing, "the night's rest could not begin: " + rest.Explanation);
                    return;
                }

                recovery.BeginRest(SurvivalRecovery.MaximumRestHours, clock.TimeScale, true);
                RoutineRests++;
                acted = true;
            }

            if (acted)
            {
                Notify();
            }
        }

        /// <summary>Issues every water portion and ration that is due now; stops the routine if one cannot be taken.</summary>
        private bool IssueDuePortions()
        {
            bool acted = false;
            for (int portion = 0; portion < 8 && survival.TimeUntilPortionDue(false) <= SurvivalRules.TimeToleranceSeconds; portion++)
            {
                CommandResult drink = survival.TryDrinkWater();
                if (!drink.Succeeded)
                {
                    StopRoutine(drink.Failure == CommandFailure.InsufficientResources ? RoutineStopReason.ResourceShortage : RoutineStopReason.PrerequisiteMissing,
                        "a water portion was due and could not be taken: " + drink.Explanation);
                    return acted;
                }

                RoutineDrinks++;
                acted = true;
            }

            for (int portion = 0; portion < 8 && survival.TimeUntilPortionDue(true) <= SurvivalRules.TimeToleranceSeconds; portion++)
            {
                CommandResult ration = survival.TryConsumeRation();
                if (!ration.Succeeded)
                {
                    StopRoutine(ration.Failure == CommandFailure.InsufficientResources ? RoutineStopReason.ResourceShortage : RoutineStopReason.PrerequisiteMissing,
                        "a ration was due and could not be eaten: " + ration.Explanation);
                    return acted;
                }

                RoutineRations++;
                acted = true;
            }

            return acted;
        }

        /// <summary>A routine cannot start on a portion that is due now and cannot be taken (store short of one portion).</summary>
        private CommandResult CheckDuePortions()
        {
            if (survival.TimeUntilPortionDue(false) <= SurvivalRules.TimeToleranceSeconds)
            {
                CommandResult drink = survival.CheckDrinkWater();
                if (!drink.Succeeded)
                {
                    return Fail(drink.Failure, "No routine now: a water portion is due and cannot be taken: " + drink.Explanation);
                }
            }

            if (survival.TimeUntilPortionDue(true) <= SurvivalRules.TimeToleranceSeconds)
            {
                CommandResult ration = survival.CheckConsumeRation();
                if (!ration.Succeeded)
                {
                    return Fail(ration.Failure, "No routine now: a ration is due and cannot be eaten: " + ration.Explanation);
                }
            }

            return Ok(string.Empty);
        }

        private double AwakeSince()
        {
            double since = survival.ActivatedAtSimulationSeconds ?? 0d;
            double? woke = recovery.LastRestEndedAtSimulationSeconds;
            return woke.HasValue && woke.Value > since ? woke.Value : since;
        }

        /// <summary>The routine reached the next beat: enter it and open its decision. Returns the stop reason.</summary>
        private RoutineStopReason EnterNextBeat()
        {
            MissionChapter next = Next(Chapter);
            if (next == MissionChapter.None)
            {
                return RoutineStopReason.ReachedChapter;
            }

            Chapter = next;
            chapterStartedAt[next] = state.ElapsedSimulationSeconds;
            switch (next)
            {
                case MissionChapter.Consequences:
                    log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                        "Mission day {0}: the crew inspects the repairs and the actual reserves. {1}",
                        Calendar(), DescribeReserves()));
                    OpenConsequencesDecision();
                    break;
                case MissionChapter.Contact:
                    log?.Invoke(string.Format(CultureInfo.InvariantCulture, state.Phase == GamePhase.Survival
                        ? "Mission day {0}: contact with Earth holds; the crew keeps to the routine and waits for news of the rescue."
                        : "Mission day {0}: time for the exterior work. Suit up, repair the antenna and call Earth.", Calendar()));
                    break;
                case MissionChapter.RescueFailed:
                    log?.Invoke(string.Format(CultureInfo.InvariantCulture, "Mission day {0}: waiting for news of the rescue attempt.", Calendar()));
                    break;
                case MissionChapter.FinalDecision:
                    PendingDecision = ChapterDecision.GrowOrBeacon;
                    log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                        "Mission day {0}: final power and supply decision before the rendezvous. {1}", Calendar(), DescribeReserves()));
                    break;
                case MissionChapter.Rendezvous:
                    log?.Invoke(string.Format(CultureInfo.InvariantCulture, "Mission day {0}: the recovery craft is close.", Calendar()));
                    break;
            }

            return RoutineStopReason.ReachedChapter;
        }

        private void CommitInTransitCraft()
        {
            double arrival = Math.Max(BeatStart(MissionChapter.Rendezvous) + (BeaconRequestBeforeArrivalHours * Hour),
                state.ElapsedSimulationSeconds + ((BeaconRequestBeforeArrivalHours + 1d) * Hour));
            double beaconAt = arrival - (BeaconRequestBeforeArrivalHours * Hour);
            Rescue = RescuePlan.InTransitCraftCommitted;
            recovery.CommitChapterRescue(beaconAt, arrival, string.Format(CultureInfo.InvariantCulture,
                "Acknowledged. A fictional cargo craft already in transit nearby is diverted to the ship: it will ask for the beacon on mission day {0} and dock on mission day {1} if the crew is still alive. No new launch from Earth could reach the ship in time. {2}",
                MissionCalendar.DayNumberAt(beaconAt), MissionCalendar.DayNumberAt(arrival), DescribeReserves()));
        }

        private bool TryDeliverDispatch()
        {
            if (!dispatchDueAt.HasValue || LatestDispatchReceived() || !state.IsSimulationRunning)
            {
                return false;
            }

            double now = state.ElapsedSimulationSeconds;
            if (now < dispatchDueAt.Value - SurvivalRules.TimeToleranceSeconds || !linkPowered())
            {
                return false;
            }

            // The send time was fixed when the dispatch was scheduled; a late receipt (link down) never moves it.
            LatestDispatch = new MissionDispatch(RescueFailedDispatchId, RescueFailedContentId, dispatchSentAt, now);
            Rescue = RescuePlan.FirstRescueFailed;
            PendingDecision = ChapterDecision.AcknowledgeRescueFailure;
            log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                "Message from Earth received on mission day {0}, sent {1} earlier: the first rescue attempt failed; the support vehicle was lost before it could reach the ship.",
                MissionCalendar.DayNumberAt(now), CommunicationSystem.FormatDuration(now - dispatchSentAt)));
            Notify();
            return true;
        }

        /// <summary>True in the day-14 beat while Earth's message has not been received yet.</summary>
        private bool AwaitingDispatch()
        {
            return Chapter == MissionChapter.RescueFailed && dispatchDueAt.HasValue && !LatestDispatchReceived();
        }

        /// <summary>One-way Earth-ship light time at a mission time, from the packaged NASA/JPL Horizons snapshot.</summary>
        private double OneWayLightSecondsAt(double simulationSeconds)
        {
            EarthMarsReference reference = EarthMarsReference.Embedded;
            DateTime epoch = calendar.EpochAtStartUtc.AddSeconds(simulationSeconds);
            if (reference.TrySelect(epoch, out EarthMarsSample sample, out _))
            {
                return sample.OneWayLightSeconds;
            }

            return (epoch > reference.LastEpochUtc ? reference.Samples[reference.Count - 1] : reference.Samples[0]).OneWayLightSeconds;
        }

        private bool LatestDispatchReceived()
        {
            return LatestDispatch.Id == RescueFailedDispatchId;
        }

        private void StopRoutine(RoutineStopReason reason, string text)
        {
            if (!running)
            {
                return;
            }

            using (events.DeferDispatch())
            {
                running = false;
                if (recovery.IsResting)
                {
                    recovery.TryWakeForRoutine();
                }

                if (!disposed && state.IsSimulationRunning && clock.TimeScale == RoutineTimeScale)
                {
                    clock.TrySetTimeScale(IsSupportedScale(scaleBeforeRoutine) ? scaleBeforeRoutine : survival.Spec.TimeScale);
                }

                double now = state.ElapsedSimulationSeconds;
                RoutineStoppedAtSimulationSeconds = now;
                LastRoutineStop = reason;
                LastRoutineStopText = text ?? (reason == RoutineStopReason.ReachedChapter ? "reached " + Describe(Chapter) : string.Empty);
                double elapsed = now - (RoutineStartedAtSimulationSeconds ?? now);
                log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                    "Routine stopped on mission day {0} after {1} ({2} ration{3}, {4} water portion{5}, {6} rest{7}): {8}.",
                    MissionCalendar.DayNumberAt(now), FormatSpan(elapsed), RoutineRations, RoutineRations == 1 ? string.Empty : "s",
                    RoutineDrinks, RoutineDrinks == 1 ? string.Empty : "s", RoutineRests, RoutineRests == 1 ? string.Empty : "s", LastRoutineStopText));
                Notify();
            }
        }

        private string MissingPrerequisite()
        {
            switch (Chapter)
            {
                case MissionChapter.Accident:
                    if (!systems.Power.IsOperational || !systems.LifeSupport.IsOperational)
                    {
                        return "Repair power and life support first.";
                    }

                    if (survival.RationsEaten < 1)
                    {
                        return "Prepare and eat the first stored ration first.";
                    }

                    // The first night is the story sleep itself (Alex's pacing): it carries the crew to day 5.
                    return null;
                case MissionChapter.Consequences:
                    return null;
                case MissionChapter.Contact:
                    return state.Phase == GamePhase.Survival ? null : "Repair the antenna outside, return, and reach Earth first.";
                case MissionChapter.RescueFailed:
                    return Rescue == RescuePlan.InTransitCraftCommitted || (AwaitingDispatch() && dispatchDueAt.Value > state.ElapsedSimulationSeconds)
                        ? null : "Earth's message about the rescue attempt is due; the link must be powered to receive it.";
                case MissionChapter.FinalDecision:
                    return null;
                case MissionChapter.Rendezvous:
                    return recovery.Stage == RecoveryStage.Inbound || recovery.Stage == RecoveryStage.Delayed
                        ? null : "Wait for the recovery craft's beacon request.";
                default:
                    return "No chapter is active.";
            }
        }

        /// <summary>The routine's next stop: the next beat, capped at one step after the day-14 news.</summary>
        private double? RoutineTarget()
        {
            double? beat = BeatTarget();
            if (!beat.HasValue)
            {
                return null;
            }

            double? step = StepLimit();
            return step.HasValue && step.Value < beat.Value ? step : beat;
        }

        private bool IsBeatTarget(double target)
        {
            double? beat = BeatTarget();
            return !beat.HasValue || target >= beat.Value - SurvivalRules.TimeToleranceSeconds;
        }

        /// <summary>
        /// After the day-14 news is acknowledged (the in-transit craft is committed) a step ends at most
        /// <see cref="MaximumStepDaysAfterNews"/> days later, at <see cref="BeatStartHour"/>; null before that.
        /// </summary>
        private double? StepLimit()
        {
            if (Rescue != RescuePlan.InTransitCraftCommitted)
            {
                return null;
            }

            double now = state.ElapsedSimulationSeconds;
            int day = MissionCalendar.DayNumberAt(now) + MaximumStepDaysAfterNews;
            double limit = ((day - 1) * Day) + (BeatStartHour * Hour);
            return limit > now ? limit : (double?)null;
        }

        /// <summary>The next beat's start (or the overdue message, or the docking), without the step cap.</summary>
        private double? BeatTarget()
        {
            if (Chapter == MissionChapter.Rendezvous)
            {
                return recovery.ArrivalAtSimulationSeconds;
            }

            if (AwaitingDispatch())
            {
                // Contact came late: the routine runs until Earth's overdue message arrives.
                return dispatchDueAt.Value;
            }

            MissionChapter next = NextChapter;
            if (next == MissionChapter.None)
            {
                return null;
            }

            if (next == MissionChapter.RescueFailed && dispatchDueAt.HasValue)
            {
                // The beat is the news itself: never before the message can physically arrive.
                return Math.Max(BeatStart(next), dispatchDueAt.Value);
            }

            if (next == MissionChapter.Rendezvous && recovery.ArrivalAtSimulationSeconds.HasValue)
            {
                // The rendezvous beat starts when the craft asks for the beacon.
                return recovery.ArrivalAtSimulationSeconds.Value - (BeaconRequestBeforeArrivalHours * Hour);
            }

            return BeatStart(next);
        }

        private MissionChapter NextOrArrival()
        {
            return Chapter == MissionChapter.Rendezvous ? MissionChapter.Rendezvous : NextChapter;
        }

        private CommandResult CheckControl()
        {
            if (disposed || state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (!state.IsSimulationRunning)
            {
                return Fail(CommandFailure.WrongPhase, $"No chapter routine during {state.Phase}.");
            }

            if (!survival.IsActive)
            {
                return Fail(CommandFailure.WrongPhase, "The chapters begin once the accident is over.");
            }

            if (clock.IsPaused || cinematics.IsWaiting)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Resume crew control first.");
            }

            return Ok(string.Empty);
        }

        private string DescribeCabinBlock()
        {
            if (state.Phase == GamePhase.Eva || eva.IsOutside || eva.Airlock != AirlockState.InnerOpen)
            {
                return "Return inside and close the airlock first.";
            }

            return eva.IsSuitOn ? "Take the EVA suit off first." : null;
        }

        private string DescribeReserves()
        {
            ResourceSnapshot now = resources.Snapshot;
            return string.Format(CultureInfo.InvariantCulture,
                "Reserves: {0:0.#} kg food, {1:0.#} L water, {2:0.#} kg oxygen, {3:0.#} kWh battery, {4} spare part{5}.",
                now.FoodKg, now.WaterLitres, now.OxygenKg, now.BatteryKWh, now.SparePartsCount, now.SparePartsCount == 1 ? string.Empty : "s");
        }

        private int Calendar()
        {
            return MissionCalendar.DayNumberAt(state.ElapsedSimulationSeconds);
        }

        private static ChapterDecision DecisionFor(ChapterChoice choice)
        {
            switch (choice)
            {
                case ChapterChoice.PreserveScience: return ChapterDecision.ScienceOrSalvage;
                case ChapterChoice.AcknowledgeRescueFailure: return ChapterDecision.AcknowledgeRescueFailure;
                case ChapterChoice.StopGrowForBeacon:
                case ChapterChoice.KeepGrowRunning: return ChapterDecision.GrowOrBeacon;
                case ChapterChoice.ConserveReserves: return ChapterDecision.GrowOrConserve;
                default: return ChapterDecision.None;
            }
        }

        private static string DescribeDecision(ChapterDecision decision)
        {
            switch (decision)
            {
                case ChapterDecision.ScienceOrSalvage: return "keep the science instrument or salvage it for spare parts";
                case ChapterDecision.AcknowledgeRescueFailure: return "acknowledge Earth's message about the failed rescue";
                case ChapterDecision.GrowOrBeacon: return "keep the grow module running or shut it down for the beacon";
                case ChapterDecision.GrowOrConserve: return "build the grow module with spare parts and battery energy, or conserve the reserves";
                default: return "nothing";
            }
        }

        private static string Describe(MissionChapter chapter)
        {
            switch (chapter)
            {
                case MissionChapter.Accident: return "the accident";
                case MissionChapter.Consequences: return "the inspection of the repairs and reserves";
                case MissionChapter.Contact: return "the exterior repair and the call to Earth";
                case MissionChapter.RescueFailed: return "news of the rescue attempt";
                case MissionChapter.FinalDecision: return "the final power and supply decision";
                case MissionChapter.Rendezvous: return "the rendezvous";
                default: return "nothing";
            }
        }

        private static string DescribeDanger(RestStopReason danger)
        {
            switch (danger)
            {
                case RestStopReason.FoodStoreEmpty: return "the food store is empty";
                case RestStopReason.WaterStoreEmpty: return "the water store is empty";
                case RestStopReason.OxygenStoreEmpty: return "the stored oxygen is used up";
                case RestStopReason.PowerLost: return "power is lost";
                case RestStopReason.CarbonDioxideDanger: return "cabin CO2 is close to the loss limit";
                default: return "danger";
            }
        }

        private static string FormatSpan(double seconds)
        {
            long minutes = (long)Math.Round(Math.Max(0d, seconds) / 60d);
            long days = minutes / 1440;
            long hours = (minutes % 1440) / 60;
            if (days > 0)
            {
                return hours > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0} d {1} h", days, hours)
                    : string.Format(CultureInfo.InvariantCulture, "{0} d", days);
            }

            return hours > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0} h {1} min", hours, minutes % 60)
                : string.Format(CultureInfo.InvariantCulture, "{0} min", minutes % 60);
        }

        private static bool IsSupportedScale(double scale)
        {
            return !double.IsNaN(scale) && scale > 0d && scale <= SimulationClock.MaximumTimeScale;
        }

        private static double Earlier(double boundary, double candidate)
        {
            return candidate > 0d && candidate < boundary ? candidate : boundary;
        }

        private void Notify()
        {
            Revision++;
            events.EnqueueObservation(() =>
            {
                if (disposed)
                {
                    return;
                }

                Action handlers = Changed;
                if (handlers == null)
                {
                    return;
                }

                foreach (Action handler in handlers.GetInvocationList())
                {
                    if (disposed)
                    {
                        break;
                    }

                    try
                    {
                        handler();
                    }
                    catch (Exception error)
                    {
                        events.ReportCallbackFailure(error);
                    }
                }
            });
        }

        private static CommandResult Ok(string text)
        {
            return new CommandResult(true, CommandFailure.None, text);
        }

        private static CommandResult Fail(CommandFailure failure, string text)
        {
            return new CommandResult(false, failure, text);
        }
    }
}
