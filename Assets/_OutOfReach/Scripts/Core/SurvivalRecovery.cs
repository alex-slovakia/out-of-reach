using System;
using System.Globalization;
using OutOfReach.Contracts;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// Optional rest and the fictional recovery timeline of the post-contact Survival chapter (implements
    /// <see cref="ISurvivalRecovery"/>). It exists only when the survival extension is enabled.
    /// <para>
    /// REST. A rest is an accelerated run of the one mission clock (<see cref="RestTimeScale"/>), never an instant
    /// skip: the session advances every service through the same step loop. While the crew rests, food and drinking
    /// water leave the ledger continuously at the BVAD crew-day rates (<see cref="CrewMetabolicAssumptions"/>),
    /// exactly as before contact, and the survival needs neither accrue nor reset. The awake manual portions are
    /// refused meanwhile, so nothing is charged twice. Oxygen, CO2 and power run as always. The step loop splits at
    /// the planned end and at the CO2 danger margin; reserve depletion boundaries come from the ledger. After every
    /// step the rest stops at the first danger (a store running out, power lost, cabin CO2 near the loss limit, the
    /// crop finishing or failing), so no loss is stepped across while the crew sleeps; a danger wins a tie with the
    /// planned end. A recovery milestone also wakes the crew, and no rest begins while the beacon window is open (one
    /// more rest would sleep through it). The Survival phase means inside the ship, out of the suit.
    /// </para>
    /// <para>
    /// RECOVERY (fictional, GAME ASSUMPTIONS): with Earth's reply a nearby recovery craft that is already in transit
    /// plans a rendezvous <see cref="ArrivalAfterContactDays"/> days after contact. On day
    /// <see cref="BeaconRequestAfterContactDays"/> it asks for the ship's beacon; powering it within
    /// <see cref="BeaconWindowHours"/> h costs <see cref="BeaconEnergyKWh"/> kWh once, and a missed window delays the
    /// rendezvous once by <see cref="MissedBeaconDelayDays"/> days. None of this timing is derived from the NASA
    /// Earth-Mars range data. Reaching the rendezvous alive ends the mission as Recovered; a loss at the same instant
    /// wins. Paused time uses none of the window, because simulation time does not pass.
    /// </para>
    /// </summary>
    public sealed class SurvivalRecovery : ISurvivalRecovery, IDisposable
    {
        /// <summary>
        /// The most one rest may last, simulated hours: NASA's 8.5 h sleep opportunity
        /// (https://www.nasa.gov/reference/6-0-medical-operations/). The duration does not imply restful sleep.
        /// </summary>
        public const double MaximumRestHours = 8.5d;

        /// <summary>
        /// GAME ASSUMPTION: simulated seconds per real second while resting (one simulated hour per second). Any stop
        /// restores the rate the clock had when the rest began (the chapter's intended speed, 300 by default).
        /// </summary>
        public const double RestTimeScale = 3600d;

        /// <summary>GAME ASSUMPTION: a rest stops once cabin CO2 would reach the loss limit within this many simulated seconds.</summary>
        public const double CarbonDioxideDangerMarginSeconds = 2d * 3600d;

        /// <summary>GAME ASSUMPTION (fictional): rendezvous of the in-transit recovery craft, days after contact.</summary>
        public const double ArrivalAfterContactDays = 12d;

        /// <summary>GAME ASSUMPTION (fictional): the craft asks for the ship's beacon this many days after contact.</summary>
        public const double BeaconRequestAfterContactDays = 6d;

        /// <summary>
        /// GAME ASSUMPTION, a fictional pacing window: how long the beacon request stays open, simulated hours. Eight
        /// hours are about 96 real seconds at the Survival time scale (300), enough to reach the comms panel after the
        /// request wakes the crew; the earlier 2 hours (24 real seconds) were too short in review.
        /// </summary>
        public const double BeaconWindowHours = 8d;

        /// <summary>GAME ASSUMPTION (fictional): one delay of the rendezvous after a missed beacon window, days.</summary>
        public const double MissedBeaconDelayDays = 3d;

        /// <summary>GAME ASSUMPTION: battery energy to run the beacon through the window (about 200 W for 2 h), charged once.</summary>
        public const double BeaconEnergyKWh = 0.4d;

        private const double SecondsPerHour = 3600d;

        private static readonly ResourceAmount[] BeaconCost = { new ResourceAmount(ResourceId.BatteryKWh, BeaconEnergyKWh) };

        private readonly int crewCount;
        private readonly MissionEventBus events;
        private readonly MissionGameState state;
        private readonly ResourceLedger resources;
        private readonly SimulationClock clock;
        private readonly CinematicCoordinator cinematics;
        private readonly SurvivalExtension survival;
        private readonly MissionCalendar calendar;
        private readonly Func<bool> powerLost;
        private readonly Action ratesChanged;
        private readonly Action<string> log;
        private readonly Func<string, CommandResult> endRecovered;
        private bool resting;
        private double restTargetSeconds;
        private double restElapsedSeconds;
        private double restedBeforeSeconds;
        private GrowModuleStage cropStageAtRestStart;
        private double scaleBeforeRest;
        private bool restQuiet;
        private RecoveryStage stage = RecoveryStage.NotStarted;
        private double? contactAt;
        private double? beaconRequestAt;
        private double? beaconWindowEndsAt;
        private double? arrivalAt;
        private bool beaconRequestIssued;
        private bool disposed;

        internal SurvivalRecovery(int crewCount, MissionEventBus events, MissionGameState state, ResourceLedger resources,
            SimulationClock clock, CinematicCoordinator cinematics, SurvivalExtension survival, MissionCalendar calendar,
            Func<bool> powerLost, Action ratesChanged, Action<string> log, Func<string, CommandResult> endRecovered)
        {
            if (crewCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(crewCount), crewCount, "A crew has at least one member.");
            }

            this.crewCount = crewCount;
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.cinematics = cinematics ?? throw new ArgumentNullException(nameof(cinematics));
            this.survival = survival ?? throw new ArgumentNullException(nameof(survival));
            this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
            this.powerLost = powerLost ?? throw new ArgumentNullException(nameof(powerLost));
            this.endRecovered = endRecovered ?? throw new ArgumentNullException(nameof(endRecovered));
            this.ratesChanged = ratesChanged;
            this.log = log;
        }

        public bool IsResting => resting && !disposed && !state.IsClosed;

        public double RestRemainingSimulationSeconds => IsResting ? Math.Max(0d, restTargetSeconds - restElapsedSeconds) : 0d;

        public RestStopReason LastRestStop { get; private set; }

        /// <summary>One-based mission day from the mission calendar (simulation clock), not a separate counter.</summary>
        public int MissionDay => calendar.DayNumber;

        /// <summary>Rests begun so far.</summary>
        public int RestCount { get; private set; }

        /// <summary>Simulated time actually spent resting so far, all rests together, seconds.</summary>
        public double RestedSimulationSeconds => restedBeforeSeconds + (IsResting ? restElapsedSeconds : 0d);

        public RecoveryStage Stage => stage;

        public double? ArrivalAtSimulationSeconds => arrivalAt;

        /// <summary>Simulated seconds to the rendezvous; 0 once arrived; +Infinity while no recovery is planned.</summary>
        public double ArrivalRemainingSimulationSeconds => arrivalAt.HasValue
            ? Math.Max(0d, arrivalAt.Value - state.ElapsedSimulationSeconds)
            : double.PositiveInfinity;

        /// <summary>End of the open beacon window, simulation seconds; null unless the beacon is requested right now.</summary>
        public double? BeaconWindowEndsAtSimulationSeconds => stage == RecoveryStage.BeaconRequested ? beaconWindowEndsAt : null;

        /// <summary>Simulation time at which Earth's reply restored contact and the recovery plan began, or null.</summary>
        public double? ContactAtSimulationSeconds => contactAt;

        /// <summary>When the beacon was powered, or null.</summary>
        public double? BeaconPoweredAtSimulationSeconds { get; private set; }

        /// <summary>True once the beacon window passed without the beacon (the one delay was applied).</summary>
        public bool BeaconMissed { get; private set; }

        /// <summary>When the recovery craft docked and the mission ended as Recovered, or null.</summary>
        public double? RecoveredAtSimulationSeconds { get; private set; }

        public event Action Changed;

        /// <summary>
        /// Approved chapter loop: contact does not start the legacy +12-day plan; the chapter flow commits the rescue
        /// (<see cref="CommitChapterRescue"/>) after the day-14 dispatch. False keeps the legacy timeline.
        /// </summary>
        internal bool ChapterMode { get; set; }

        /// <summary>Called after <see cref="ReleaseForDisabledRuntime"/> so the chapter routine can stop as well.</summary>
        internal Action RuntimeReleased { get; set; }

        /// <summary>Chapter mode: ends the mission as Lost after the second missed beacon window (set by the director).</summary>
        internal Func<string, CommandResult> EndMissedRendezvous { get; set; }

        /// <summary>Chapter mode: true once the one second beacon window was opened after a miss.</summary>
        public bool SecondBeaconWindowOffered { get; private set; }

        /// <summary>Rests that ran to their planned end.</summary>
        public int CompletedRestCount { get; private set; }

        /// <summary>When the last rest ended, simulation seconds, or null before the first rest.</summary>
        public double? LastRestEndedAtSimulationSeconds { get; private set; }

        /// <summary>Food drawn from the ledger while resting, kg per simulated second (BVAD crew-day rate); 0 awake.</summary>
        internal double RestFoodKgPerSecond => IsResting
            ? CrewMetabolicAssumptions.FoodKgPerCrewDay * crewCount / CrewMetabolicAssumptions.SecondsPerDay
            : 0d;

        /// <summary>Drinking water drawn from the ledger while resting, L per simulated second (BVAD crew-day rate); 0 awake.</summary>
        internal double RestWaterLitresPerSecond => IsResting
            ? CrewMetabolicAssumptions.WaterLitresPerCrewDay * crewCount / CrewMetabolicAssumptions.SecondsPerDay
            : 0d;

        public CommandResult CheckRest(double hours)
        {
            CommandResult control = CheckControl();
            if (!control.Succeeded)
            {
                return control;
            }

            if (IsResting)
            {
                return Fail(CommandFailure.AlreadyComplete, "The crew is already resting.");
            }

            if (double.IsNaN(hours) || double.IsInfinity(hours) || hours <= 0d || hours > MaximumRestHours)
            {
                return Fail(CommandFailure.InvalidInput, string.Format(CultureInfo.InvariantCulture,
                    "A rest lasts more than 0 and at most {0:0.#} simulated hours.", MaximumRestHours));
            }

            RestStopReason danger = CurrentDanger();
            if (danger != RestStopReason.None)
            {
                CommandFailure failure = danger == RestStopReason.FoodStoreEmpty || danger == RestStopReason.WaterStoreEmpty
                    ? CommandFailure.InsufficientResources
                    : CommandFailure.PreconditionsNotMet;
                return Fail(failure, "No rest now: " + Describe(danger) + ".");
            }

            if (stage == RecoveryStage.BeaconRequested)
            {
                // The request wakes the crew so they can answer it. At the rest scale one more rest would sleep through
                // the whole window in a few real seconds and slip the rendezvous: power the beacon (or let it pass awake).
                double left = beaconWindowEndsAt.HasValue ? beaconWindowEndsAt.Value - state.ElapsedSimulationSeconds : 0d;
                return Fail(CommandFailure.PreconditionsNotMet, string.Format(CultureInfo.InvariantCulture,
                    "No rest now: the recovery craft is waiting for the beacon; power it first (the window closes in {0}).", FormatSpan(left)));
            }

            double seconds = hours * SecondsPerHour;
            return Ok(string.Format(CultureInfo.InvariantCulture,
                "Rest up to {0:0.##} h: about {1:0.###} kg food and {2:0.###} L water at the BVAD crew-day rates; any danger wakes the crew",
                hours,
                CrewMetabolicAssumptions.FoodKgPerCrewDay * crewCount * seconds / CrewMetabolicAssumptions.SecondsPerDay,
                CrewMetabolicAssumptions.WaterLitresPerCrewDay * crewCount * seconds / CrewMetabolicAssumptions.SecondsPerDay));
        }

        public CommandResult TryBeginRest(double hours)
        {
            CommandResult check = CheckRest(hours);
            if (!check.Succeeded)
            {
                return check;
            }

            BeginRest(hours, RestTimeScale, false);
            return check;
        }

        /// <summary>
        /// Starts an already checked rest at <paramref name="timeScale"/>. The chapter routine passes its own montage
        /// scale; the accounting (BVAD draw, paused needs, every danger stop) is the same as an ordinary rest.
        /// </summary>
        internal void BeginRest(double hours, double timeScale, bool quietJournal)
        {
            using (events.DeferDispatch())
            {
                resting = true;
                restTargetSeconds = hours * SecondsPerHour;
                restElapsedSeconds = 0d;
                cropStageAtRestStart = survival.GrowStage;
                LastRestStop = RestStopReason.None;
                RestCount++;
                restQuiet = quietJournal;
                survival.NeedsPaused = true;
                scaleBeforeRest = clock.TimeScale;
                clock.TrySetTimeScale(timeScale);
                ratesChanged?.Invoke();
                if (!restQuiet)
                {
                    log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                        "Rest began on mission day {0}: up to {1:0.##} h; food and water are drawn at the BVAD crew-day rates while the crew rests.",
                        MissionDay, hours));
                }

                Notify();
            }
        }

        public CommandResult CheckWake()
        {
            if (!IsResting)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "The crew is not resting.");
            }

            if (clock.IsPaused)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Resume before waking the crew.");
            }

            return Ok("Wake the crew");
        }

        public CommandResult TryWake()
        {
            CommandResult check = CheckWake();
            if (!check.Succeeded)
            {
                return check;
            }

            StopRest(RestStopReason.Woken);
            return Ok("The crew is awake.");
        }

        /// <summary>The sleep position: begins the longest rest, or wakes the resting crew.</summary>
        public CommandResult CheckSleepInteraction()
        {
            return IsResting ? CheckWake() : CheckRest(MaximumRestHours);
        }

        public CommandResult TrySleepInteraction()
        {
            return IsResting ? TryWake() : TryBeginRest(MaximumRestHours);
        }

        public CommandResult CheckPowerBeacon()
        {
            CommandResult control = CheckControl();
            if (!control.Succeeded)
            {
                return control;
            }

            if (BeaconPoweredAtSimulationSeconds.HasValue)
            {
                return Fail(CommandFailure.AlreadyComplete, "The beacon is already powered for the recovery craft.");
            }

            if (stage != RecoveryStage.BeaconRequested)
            {
                return Fail(CommandFailure.PreconditionsNotMet, BeaconMissed
                    ? "The beacon window has passed; the rendezvous has slipped."
                    : "The recovery craft has not asked for the beacon yet.");
            }

            if (IsResting)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "The crew is resting. Wake up first.");
            }

            CommandResult affordable = resources.CheckAffordable(BeaconCost);
            if (!affordable.Succeeded)
            {
                return affordable;
            }

            double left = beaconWindowEndsAt.HasValue ? beaconWindowEndsAt.Value - state.ElapsedSimulationSeconds : 0d;
            return Ok(string.Format(CultureInfo.InvariantCulture,
                "Power the recovery beacon: {0:0.##} kWh; the window closes in {1}", BeaconEnergyKWh, FormatSpan(left)));
        }

        public CommandResult TryPowerBeacon()
        {
            CommandResult check = CheckPowerBeacon();
            if (!check.Succeeded)
            {
                return check;
            }

            using (events.DeferDispatch())
            {
                CommandResult spent = resources.TryConsume(BeaconCost);
                if (!spent.Succeeded)
                {
                    return spent;
                }

                BeaconPoweredAtSimulationSeconds = state.ElapsedSimulationSeconds;
                stage = RecoveryStage.Inbound;
                beaconWindowEndsAt = null;
                ratesChanged?.Invoke();
                log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                    "Recovery beacon powered on mission day {0} ({1:0.##} kWh): the rendezvous stays on mission day {2}.",
                    MissionDay, BeaconEnergyKWh, DayAt(arrivalAt)));
                Notify();
            }

            return Ok("Recovery beacon powered: the rendezvous stays on schedule.");
        }

        /// <summary>The report's "What it means" line for a Recovered outcome.</summary>
        public string DescribeRecoveryForReport()
        {
            double since = RecoveredAtSimulationSeconds.HasValue && contactAt.HasValue ? RecoveredAtSimulationSeconds.Value - contactAt.Value : 0d;
            if (ChapterMode)
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "After the first rescue attempt failed, a fictional craft already in transit nearby docked on mission day {0} ({1}); the crew transferred aboard. This timeline is a game assumption, not derived from the NASA Earth-Mars data.",
                    DayAt(RecoveredAtSimulationSeconds),
                    BeaconMissed ? "3 days late after the missed beacon window" : "on schedule with the beacon powered");
            }

            return string.Format(CultureInfo.InvariantCulture,
                "A fictional recovery craft already in transit docked {0} after contact ({1}); the crew transferred aboard, and onward travel to Mars is a later chapter. This timeline is a game assumption, not derived from the NASA Earth-Mars data.",
                FormatSpan(since),
                BeaconMissed ? "3 days late after the missed beacon window" : "on schedule with the beacon powered");
        }

        /// <summary>The report's rest line.</summary>
        public string DescribeRestForReport()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} rest{1}, {2:0.#} h in total; food and water drawn at the BVAD crew-day rates while resting",
                RestCount, RestCount == 1 ? string.Empty : "s", RestedSimulationSeconds / SecondsPerHour);
        }

        /// <summary>The report's beacon line.</summary>
        public string DescribeBeaconForReport()
        {
            if (BeaconPoweredAtSimulationSeconds.HasValue)
            {
                return string.Format(CultureInfo.InvariantCulture, "Powered on mission day {0} ({1:0.##} kWh)",
                    DayAt(BeaconPoweredAtSimulationSeconds), BeaconEnergyKWh);
            }

            if (BeaconMissed)
            {
                return string.Format(CultureInfo.InvariantCulture, "Missed: the rendezvous slipped {0:0} days, to mission day {1}",
                    MissedBeaconDelayDays, DayAt(arrivalAt));
            }

            if (stage == RecoveryStage.BeaconRequested)
            {
                return "Requested, not powered yet";
            }

            return stage == RecoveryStage.NotStarted
                ? "No recovery plan yet"
                : string.Format(CultureInfo.InvariantCulture, "Not requested yet (mission day {0})", DayAt(beaconRequestAt));
        }

        /// <summary>Called by the director right after contact moves the mission into Survival: starts the one plan.</summary>
        internal void BeginRecovery()
        {
            if (disposed || ChapterMode || stage != RecoveryStage.NotStarted || !survival.ContactRestoredAtSimulationSeconds.HasValue)
            {
                return;
            }

            contactAt = survival.ContactRestoredAtSimulationSeconds.Value;
            beaconRequestAt = contactAt.Value + (BeaconRequestAfterContactDays * CrewMetabolicAssumptions.SecondsPerDay);
            arrivalAt = contactAt.Value + (ArrivalAfterContactDays * CrewMetabolicAssumptions.SecondsPerDay);
            stage = RecoveryStage.Inbound;
            log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                "Earth relays a recovery plan: a fictional recovery craft already in transit nearby can rendezvous {0:0} days after contact, on mission day {1}, and will ask for the ship's beacon on mission day {2}. This timeline is a game assumption, not derived from the NASA Earth-Mars data.",
                ArrivalAfterContactDays, DayAt(arrivalAt), DayAt(beaconRequestAt)));
            Notify();
        }

        /// <summary>
        /// Chapter mode, once: the already-in-transit craft commits to a rendezvous at <paramref name="arrivalAtSeconds"/>
        /// and asks for the beacon at <paramref name="beaconRequestAtSeconds"/>. The beacon cost, window and the one
        /// missed-window delay are the existing rules.
        /// </summary>
        internal bool CommitChapterRescue(double beaconRequestAtSeconds, double arrivalAtSeconds, string announcement)
        {
            if (disposed || !ChapterMode || stage != RecoveryStage.NotStarted || arrivalAtSeconds <= beaconRequestAtSeconds)
            {
                return false;
            }

            contactAt = survival.ContactRestoredAtSimulationSeconds ?? state.ElapsedSimulationSeconds;
            beaconRequestAt = beaconRequestAtSeconds;
            arrivalAt = arrivalAtSeconds;
            stage = RecoveryStage.Inbound;
            log?.Invoke(announcement);
            Notify();
            return true;
        }

        /// <summary>
        /// Simulated seconds to the next rest or recovery boundary: the planned end of a rest, the CO2 danger margin,
        /// the beacon request, the end of its window, or the rendezvous; +Infinity when none is ahead.
        /// </summary>
        internal double TimeToNextBoundary()
        {
            double boundary = double.PositiveInfinity;
            if (IsResting)
            {
                boundary = Earlier(boundary, restTargetSeconds - restElapsedSeconds);
                double untilDanger = resources.TimeUntilCarbonDioxideReaches(SurvivalRules.CarbonDioxideLossLimitPpm) - CarbonDioxideDangerMarginSeconds;
                boundary = Earlier(boundary, untilDanger);
            }

            if (!disposed && state.IsSimulationRunning)
            {
                double now = state.ElapsedSimulationSeconds;
                if (AwaitsBeaconRequest() && beaconRequestAt.HasValue)
                {
                    boundary = Earlier(boundary, beaconRequestAt.Value - now);
                }

                if (stage == RecoveryStage.BeaconRequested && beaconWindowEndsAt.HasValue)
                {
                    boundary = Earlier(boundary, beaconWindowEndsAt.Value - now);
                }

                if (CanArrive() && arrivalAt.HasValue)
                {
                    boundary = Earlier(boundary, arrivalAt.Value - now);
                }
            }

            return boundary;
        }

        /// <summary>Called by the director inside each step, before the survival rules: counts the time rested.</summary>
        internal void AdvanceTime(double seconds)
        {
            if (!IsResting || seconds <= 0d)
            {
                return;
            }

            restElapsedSeconds = Math.Min(restTargetSeconds, restElapsedSeconds + seconds);
        }

        /// <summary>
        /// Called by the director after the survival rules of each step (so a loss at the same instant wins): moves
        /// the recovery timeline on, which may end the mission as Recovered, then stops the rest at its first reason.
        /// </summary>
        internal void AfterStep()
        {
            bool milestone = AdvanceTimeline();
            if (!resting)
            {
                return;
            }

            if (disposed || state.IsClosed || !state.IsSimulationRunning || !survival.IsActive)
            {
                StopRest(RestStopReason.MissionEnded);
                return;
            }

            RestStopReason reason = CurrentDanger();
            if (reason == RestStopReason.None)
            {
                reason = CropChange();
            }

            if (reason == RestStopReason.None && milestone)
            {
                reason = RestStopReason.RecoveryMilestone;
            }

            if (reason == RestStopReason.None && restElapsedSeconds >= restTargetSeconds - SurvivalRules.TimeToleranceSeconds)
            {
                reason = RestStopReason.Completed;
            }

            if (reason != RestStopReason.None)
            {
                StopRest(reason);
            }
        }

        /// <summary>
        /// The runtime that drives the clock was disabled: a resting crew wakes (Woken) and the rate from before the
        /// rest returns, even while paused, so no accelerated rest outlives the runtime. Pause alone keeps a rest.
        /// Nothing happens when the crew is awake.
        /// </summary>
        public void ReleaseForDisabledRuntime()
        {
            StopRest(RestStopReason.Woken);
            RuntimeReleased?.Invoke();
        }

        /// <summary>The chapter routine stopped while the crew slept (a beat or the player's cancel): wake them now.</summary>
        internal void TryWakeForRoutine()
        {
            StopRest(RestStopReason.Woken);
        }

        internal void OnMissionEnded()
        {
            StopRest(RestStopReason.MissionEnded);
        }

        public void Dispose()
        {
            disposed = true;
            resting = false;
            survival.NeedsPaused = false;
            Changed = null;
        }

        private bool AdvanceTimeline()
        {
            if (disposed || !state.IsSimulationRunning || stage == RecoveryStage.NotStarted || stage == RecoveryStage.Arrived)
            {
                return false;
            }

            double now = state.ElapsedSimulationSeconds;
            double tolerance = SurvivalRules.TimeToleranceSeconds;
            if (AwaitsBeaconRequest() && beaconRequestAt.HasValue && now >= beaconRequestAt.Value - tolerance)
            {
                beaconRequestIssued = true;
                stage = RecoveryStage.BeaconRequested;
                beaconWindowEndsAt = beaconRequestAt.Value + (BeaconWindowHours * SecondsPerHour);
                log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                    "The recovery craft asks for the ship's beacon on mission day {0}: power it within {1:0} h ({2:0.##} kWh), or the rendezvous is delayed {3:0} days.",
                    MissionDay, BeaconWindowHours, BeaconEnergyKWh, MissedBeaconDelayDays));
                Notify();
                return true;
            }

            if (ChapterMode && stage == RecoveryStage.BeaconRequested && beaconWindowEndsAt.HasValue && now >= beaconWindowEndsAt.Value - tolerance)
            {
                return MissChapterBeaconWindow(now);
            }

            if (stage == RecoveryStage.BeaconRequested && beaconWindowEndsAt.HasValue && now >= beaconWindowEndsAt.Value - tolerance)
            {
                stage = RecoveryStage.Delayed;
                BeaconMissed = true;
                beaconWindowEndsAt = null;
                arrivalAt = arrivalAt.Value + (MissedBeaconDelayDays * CrewMetabolicAssumptions.SecondsPerDay);
                log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                    "Beacon window missed: the recovery craft's rendezvous is delayed {0:0} days, to mission day {1}.",
                    MissedBeaconDelayDays, DayAt(arrivalAt)));
                Notify();
                return true;
            }

            if (CanArrive() && arrivalAt.HasValue && now >= arrivalAt.Value - tolerance)
            {
                stage = RecoveryStage.Arrived;
                RecoveredAtSimulationSeconds = now;
                log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                    "The recovery craft docked on mission day {0}; the crew transfers aboard.", MissionDay));
                Notify();
                endRecovered(DescribeRecoveryForReport());
                return true;
            }

            return false;
        }

        /// <summary>A beacon request is still ahead: the first one, or (chapter mode) the one second window after a miss.</summary>
        private bool AwaitsBeaconRequest()
        {
            return !beaconRequestIssued && (stage == RecoveryStage.Inbound || (ChapterMode && stage == RecoveryStage.Delayed));
        }

        /// <summary>Legacy: the craft always docks on its date. Chapter mode: only with a powered beacon.</summary>
        private bool CanArrive()
        {
            return (stage == RecoveryStage.Inbound || stage == RecoveryStage.Delayed) && (!ChapterMode || BeaconPoweredAtSimulationSeconds.HasValue);
        }

        /// <summary>
        /// Chapter mode: the first missed window opens exactly one second explicit window <see cref="MissedBeaconDelayDays"/>
        /// days later (same length and cost), with the rendezvous moved by the same amount; a second miss means the craft
        /// cannot find the ship and the mission ends as Lost. Nothing docks without a powered beacon.
        /// </summary>
        private bool MissChapterBeaconWindow(double now)
        {
            BeaconMissed = true;
            beaconWindowEndsAt = null;
            if (!SecondBeaconWindowOffered)
            {
                SecondBeaconWindowOffered = true;
                double shift = MissedBeaconDelayDays * CrewMetabolicAssumptions.SecondsPerDay;
                beaconRequestAt = beaconRequestAt.Value + shift;
                arrivalAt = arrivalAt.Value + shift;
                beaconRequestIssued = false;
                stage = RecoveryStage.Delayed;
                log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                    "Beacon window missed: the recovery craft could not lock on. It will try one more pass and ask again on mission day {0}; without the beacon then it cannot find the ship.",
                    DayAt(beaconRequestAt)));
                Notify();
                return true;
            }

            stage = RecoveryStage.Delayed;
            log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                "Second beacon window missed on mission day {0}: the recovery craft could not find the ship and had to continue without it.", MissionDay));
            Notify();
            EndMissedRendezvous?.Invoke(string.Format(CultureInfo.InvariantCulture,
                "Missed rendezvous: the recovery craft asked twice for the ship's beacon and it was never powered, so it could not find the ship (mission day {0}). The crew was still alive.",
                MissionDay));
            return true;
        }

        private CommandResult CheckControl()
        {
            if (disposed || state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (!survival.IsActive)
            {
                return Fail(CommandFailure.WrongPhase, ChapterMode
                    ? "Rest begins once the accident is over."
                    : "This belongs to the post-contact survival chapter.");
            }

            if (clock.IsPaused || cinematics.IsWaiting)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Resume crew control first.");
            }

            string blocked = survival.CabinBlocker?.Invoke();
            if (blocked != null)
            {
                return Fail(CommandFailure.PreconditionsNotMet, blocked);
            }

            return Ok(string.Empty);
        }

        internal RestStopReason CurrentDanger()
        {
            ResourceSnapshot now = resources.Snapshot;
            if (now.FoodKg <= ResourceLedger.AffordabilityTolerance)
            {
                return RestStopReason.FoodStoreEmpty;
            }

            if (now.WaterLitres <= ResourceLedger.AffordabilityTolerance)
            {
                return RestStopReason.WaterStoreEmpty;
            }

            if (now.OxygenKg <= ResourceLedger.AffordabilityTolerance)
            {
                return RestStopReason.OxygenStoreEmpty;
            }

            if (powerLost())
            {
                return RestStopReason.PowerLost;
            }

            double untilLimit = resources.TimeUntilCarbonDioxideReaches(SurvivalRules.CarbonDioxideLossLimitPpm);
            return untilLimit <= CarbonDioxideDangerMarginSeconds + SurvivalRules.TimeToleranceSeconds
                ? RestStopReason.CarbonDioxideDanger
                : RestStopReason.None;
        }

        private RestStopReason CropChange()
        {
            GrowModuleStage now = survival.GrowStage;
            if (now == cropStageAtRestStart)
            {
                return RestStopReason.None;
            }

            if (now == GrowModuleStage.HarvestReady)
            {
                return RestStopReason.CropReady;
            }

            return now == GrowModuleStage.Failed ? RestStopReason.CropFailed : RestStopReason.None;
        }

        private void StopRest(RestStopReason reason)
        {
            if (!resting)
            {
                return;
            }

            using (events.DeferDispatch())
            {
                double rested = restElapsedSeconds;
                resting = false;
                restedBeforeSeconds += rested;
                restElapsedSeconds = 0d;
                restTargetSeconds = 0d;
                LastRestStop = reason;
                LastRestEndedAtSimulationSeconds = state.ElapsedSimulationSeconds;
                if (reason == RestStopReason.Completed)
                {
                    CompletedRestCount++;
                }

                survival.NeedsPaused = false;
                if (!disposed && state.IsSimulationRunning && (state.Phase == GamePhase.Survival || ChapterMode))
                {
                    clock.TrySetTimeScale(IsSupportedScale(scaleBeforeRest) ? scaleBeforeRest : survival.Spec.TimeScale);
                }

                ratesChanged?.Invoke();
                if (!restQuiet)
                {
                    log?.Invoke(string.Format(CultureInfo.InvariantCulture, "Rest ended on mission day {0} after {1:0.##} h: {2}.",
                        MissionDay, rested / SecondsPerHour, Describe(reason)));
                }

                restQuiet = false;
                Notify();
            }
        }

        private static bool IsSupportedScale(double scale)
        {
            return !double.IsNaN(scale) && scale > 0d && scale <= SimulationClock.MaximumTimeScale;
        }

        private static double Earlier(double boundary, double candidate)
        {
            return candidate > 0d && candidate < boundary ? candidate : boundary;
        }

        private static int DayAt(double? elapsedSeconds)
        {
            return MissionCalendar.DayNumberAt(elapsedSeconds ?? 0d);
        }

        private static string FormatSpan(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds))
            {
                return "an unknown time";
            }

            long minutes = (long)Math.Round(Math.Max(0d, seconds) / 60d);
            long days = minutes / 1440;
            long hours = (minutes % 1440) / 60;
            long rest = minutes % 60;
            if (days > 0)
            {
                return hours > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0} d {1} h", days, hours)
                    : string.Format(CultureInfo.InvariantCulture, "{0} d", days);
            }

            if (hours > 0)
            {
                return rest > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0} h {1} min", hours, rest)
                    : string.Format(CultureInfo.InvariantCulture, "{0} h", hours);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0} min", rest);
        }

        private static string Describe(RestStopReason reason)
        {
            switch (reason)
            {
                case RestStopReason.Completed:
                    return "the planned rest is complete";
                case RestStopReason.Woken:
                    return "the crew was woken";
                case RestStopReason.FoodStoreEmpty:
                    return "the food store is empty";
                case RestStopReason.WaterStoreEmpty:
                    return "the water store is empty";
                case RestStopReason.OxygenStoreEmpty:
                    return "the stored oxygen is used up; only the cabin air is left";
                case RestStopReason.PowerLost:
                    return "power is lost: the battery is empty and generation is below the load";
                case RestStopReason.CarbonDioxideDanger:
                    return "cabin CO2 is close to the loss limit";
                case RestStopReason.CropReady:
                    return "the crop is ready to harvest";
                case RestStopReason.CropFailed:
                    return "the grow module failed";
                case RestStopReason.RecoveryMilestone:
                    return "the recovery craft needs the crew";
                case RestStopReason.MissionEnded:
                    return "the mission ended";
                default:
                    return "no reason";
            }
        }

        private void Notify()
        {
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
