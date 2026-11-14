using System;
using System.Globalization;
using OutOfReach.Contracts;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// Optional rest and, from step R2, the fictional recovery timeline of the post-contact Survival chapter
    /// (implements <see cref="ISurvivalRecovery"/>). It exists only when the survival extension is enabled.
    /// <para>
    /// A rest is an accelerated run of the one mission clock (<see cref="RestTimeScale"/>), never an instant skip:
    /// the session advances every service through the same step loop. While the crew rests, food and drinking
    /// water leave the ledger continuously at the BVAD crew-day rates (<see cref="CrewMetabolicAssumptions"/>),
    /// exactly as before contact, and the survival needs neither accrue nor reset. The awake manual portions are
    /// refused meanwhile, so nothing is charged twice. Oxygen, CO2 and power run as always.
    /// </para>
    /// <para>
    /// The step loop splits at the planned end of the rest and at the CO2 danger margin; reserve depletion
    /// boundaries come from the ledger. After every step the rest stops at the first danger (a store running out,
    /// power lost, cabin CO2 near the loss limit, the crop finishing or failing), so no loss is ever stepped across
    /// while the crew sleeps. A danger wins a tie with the planned end; a loss decided in the same step ends the
    /// rest as MissionEnded. The Survival phase means the crew is inside the ship, out of the suit.
    /// </para>
    /// </summary>
    public sealed class SurvivalRecovery : ISurvivalRecovery, IDisposable
    {
        /// <summary>
        /// The most one rest may last, simulated hours: NASA's 8.5 h sleep opportunity
        /// (https://www.nasa.gov/reference/6-0-medical-operations/). The duration does not imply restful sleep.
        /// </summary>
        public const double MaximumRestHours = 8.5d;

        /// <summary>GAME ASSUMPTION: simulated seconds per real second while resting (one simulated hour per second).</summary>
        public const double RestTimeScale = 3600d;

        /// <summary>GAME ASSUMPTION: a rest stops once cabin CO2 would reach the loss limit within this many simulated seconds.</summary>
        public const double CarbonDioxideDangerMarginSeconds = 2d * 3600d;

        private const double SecondsPerHour = 3600d;

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
        private bool resting;
        private double restTargetSeconds;
        private double restElapsedSeconds;
        private double restedBeforeSeconds;
        private GrowModuleStage cropStageAtRestStart;
        private bool disposed;

        internal SurvivalRecovery(int crewCount, MissionEventBus events, MissionGameState state, ResourceLedger resources,
            SimulationClock clock, CinematicCoordinator cinematics, SurvivalExtension survival, MissionCalendar calendar,
            Func<bool> powerLost, Action ratesChanged, Action<string> log)
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

        /// <summary>Recovery timeline: step R2, not implemented yet (always NotStarted).</summary>
        public RecoveryStage Stage => RecoveryStage.NotStarted;

        /// <summary>Recovery timeline: step R2, not implemented yet (always null).</summary>
        public double? ArrivalAtSimulationSeconds => null;

        /// <summary>+Infinity while no arrival is planned (step R2 is not implemented yet).</summary>
        public double ArrivalRemainingSimulationSeconds => double.PositiveInfinity;

        /// <summary>Recovery timeline: step R2, not implemented yet (always null).</summary>
        public double? BeaconWindowEndsAtSimulationSeconds => null;

        public event Action Changed;

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

            using (events.DeferDispatch())
            {
                resting = true;
                restTargetSeconds = hours * SecondsPerHour;
                restElapsedSeconds = 0d;
                cropStageAtRestStart = survival.GrowStage;
                LastRestStop = RestStopReason.None;
                RestCount++;
                survival.NeedsPaused = true;
                clock.TrySetTimeScale(RestTimeScale);
                ratesChanged?.Invoke();
                log?.Invoke(string.Format(CultureInfo.InvariantCulture,
                    "Rest began on mission day {0}: up to {1:0.##} h; food and water are drawn at the BVAD crew-day rates while the crew rests.",
                    MissionDay, hours));
                Notify();
            }

            return check;
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
            return Fail(CommandFailure.PreconditionsNotMet, "No recovery beacon has been requested.");
        }

        public CommandResult TryPowerBeacon()
        {
            return CheckPowerBeacon();
        }

        /// <summary>Simulated seconds to the planned end of the rest or the CO2 danger margin; +Infinity awake.</summary>
        internal double TimeToNextBoundary()
        {
            if (!IsResting)
            {
                return double.PositiveInfinity;
            }

            double boundary = restTargetSeconds - restElapsedSeconds;
            double untilDanger = resources.TimeUntilCarbonDioxideReaches(SurvivalRules.CarbonDioxideLossLimitPpm) - CarbonDioxideDangerMarginSeconds;
            if (untilDanger > 0d && untilDanger < boundary)
            {
                boundary = untilDanger;
            }

            return boundary > 0d ? boundary : double.PositiveInfinity;
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

        /// <summary>Called by the director after the survival rules of each step: stops the rest at the first reason.</summary>
        internal void EvaluateRest()
        {
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

            if (reason == RestStopReason.None && restElapsedSeconds >= restTargetSeconds - SurvivalRules.TimeToleranceSeconds)
            {
                reason = RestStopReason.Completed;
            }

            if (reason != RestStopReason.None)
            {
                StopRest(reason);
            }
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

        private CommandResult CheckControl()
        {
            if (disposed || state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (!survival.IsActive)
            {
                return Fail(CommandFailure.WrongPhase, "Rest belongs to the post-contact survival chapter.");
            }

            if (clock.IsPaused || cinematics.IsWaiting)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Resume crew control before resting.");
            }

            return Ok(string.Empty);
        }

        private RestStopReason CurrentDanger()
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
                survival.NeedsPaused = false;
                if (!disposed && state.IsSimulationRunning && state.Phase == GamePhase.Survival)
                {
                    clock.TrySetTimeScale(survival.Spec.TimeScale);
                }

                ratesChanged?.Invoke();
                log?.Invoke(string.Format(CultureInfo.InvariantCulture, "Rest ended on mission day {0} after {1:0.##} h: {2}.",
                    MissionDay, rested / SecondsPerHour, Describe(reason)));
                Notify();
            }
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
                    return "the recovery timeline needs the crew";
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
