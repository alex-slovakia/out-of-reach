using System;
using System.Globalization;
using OutOfReach.Contracts;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// Authoritative suit, airlock and outside state for the single constrained EVA.
    /// <list type="bullet">
    /// <item>Airlock states follow the shared <see cref="AirlockState"/>: InnerOpen, Sealed, Depressurizing,
    /// OuterOpen, Repressurizing. The two hatches are distinct states, so they can never be open together.</item>
    /// <item>Suit-up draws purge and tank oxygen from the ship once and takes the preparation time as a disclosed
    /// time jump. Starting the EVA seals the inner hatch, charges the airlock loss and pump energy, and the crew
    /// breathes suit oxygen from that moment until repressurization completes.</item>
    /// <item>Going outside needs the outer hatch open and a sealed suit; repressurization is refused while the
    /// crew is outside. A door animation can never override these gates.</item>
    /// <item>Returning is always possible and costs nothing, so resources can never trap the crew outside.
    /// Running out of suit oxygen while sealed is a deterministic ending decided by Core.</item>
    /// </list>
    /// Every rejected command changes nothing and publishes nothing.
    /// </summary>
    public sealed class EvaOperations
    {
        private const double Tolerance = 1e-9;

        private readonly EvaSpec spec;
        private readonly MissionEventBus events;
        private readonly MissionGameState state;
        private readonly ResourceLedger resources;
        private readonly ShipSystems systems;
        private readonly Action ratesChanged;
        private Func<double, CommandResult> advanceSimulation;

        internal EvaOperations(EvaSpec spec, MissionEventBus events, MissionGameState state, ResourceLedger resources, ShipSystems systems, Action ratesChanged)
        {
            this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            this.systems = systems ?? throw new ArgumentNullException(nameof(systems));
            this.ratesChanged = ratesChanged;
        }

        public EvaSpec Spec => spec;

        public AirlockState Airlock { get; private set; } = AirlockState.InnerOpen;

        /// <summary>Simulated seconds left in the running depressurization or repressurization; 0 otherwise.</summary>
        public double AirlockTransitionRemainingSeconds { get; private set; }

        public bool IsInnerHatchPassable => Airlock == AirlockState.InnerOpen;

        public bool IsOuterHatchPassable => Airlock == AirlockState.OuterOpen;

        public bool IsSuitOn { get; private set; }

        /// <summary>True while the crew breathes suit oxygen: from depressurization start until repressurization ends.</summary>
        public bool IsSuitSealed { get; private set; }

        public double SuitOxygenKg { get; private set; }

        public double SuitOxygenCapacityKg => spec.SuitOxygenChargeKg;

        /// <summary>Simulated seconds of suit oxygen left at the current use rate.</summary>
        public double SuitOxygenSecondsRemaining => SuitOxygenKg / spec.SuitOxygenUseKgPerSecond;

        /// <summary>True while the crew is on the tethered route outside the hull.</summary>
        public bool IsOutside { get; private set; }

        /// <summary>Completed airlock cycles (out and back).</summary>
        public int CompletedEvaCount { get; private set; }

        /// <summary>Raised after any suit, airlock or outside change, for views without a contract event.</summary>
        public event Action Changed;

        internal void BindSimulationAdvance(Func<double, CommandResult> advance)
        {
            advanceSimulation = advance;
        }

        public CommandResult CheckPrepareSuit()
        {
            CommandResult gate = CheckOpen();
            if (!gate.Succeeded)
            {
                return gate;
            }

            if (IsSuitOn)
            {
                return Fail(CommandFailure.AlreadyComplete, "The EVA suit is already on.");
            }

            if (systems.Antenna.IsOperational)
            {
                return Fail(CommandFailure.AlreadyComplete, "No EVA is needed: the antenna is operational.");
            }

            if (state.Phase != GamePhase.Repair)
            {
                return Fail(CommandFailure.WrongPhase, "Stabilize the ship before preparing an EVA.");
            }

            if (!systems.Power.IsOperational || !systems.LifeSupport.IsOperational)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Power and life support must be operational before an EVA.");
            }

            if (Airlock != AirlockState.InnerOpen)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "The airlock is not ready.");
            }

            CommandResult affordable = resources.CheckAffordable(new[] { new ResourceAmount(ResourceId.OxygenKg, spec.SuitPreparationOxygenKg) });
            if (!affordable.Succeeded)
            {
                return affordable;
            }

            return Ok(string.Format(CultureInfo.InvariantCulture, "Put on EVA suit: {0:0.##} kg oxygen, {1} of preparation.",
                spec.SuitPreparationOxygenKg, FormatDuration(spec.SuitPreparationSeconds)));
        }

        /// <summary>Suit-up: draws the oxygen once, puts the suit on and spends the preparation time.</summary>
        public CommandResult TryPrepareSuit()
        {
            CommandResult check = CheckPrepareSuit();
            if (!check.Succeeded)
            {
                return check;
            }

            using (events.DeferDispatch())
            {
                CommandResult spent = resources.TryConsume(new[] { new ResourceAmount(ResourceId.OxygenKg, spec.SuitPreparationOxygenKg) });
                if (!spent.Succeeded)
                {
                    return spent;
                }

                IsSuitOn = true;
                SuitOxygenKg = spec.SuitOxygenChargeKg;
                RaiseChanged();
                if (advanceSimulation != null && spec.SuitPreparationSeconds > 0d)
                {
                    advanceSimulation(spec.SuitPreparationSeconds);
                }
            }

            return Ok("EVA suit on. Tank: " + SuitOxygenKg.ToString("0.##", CultureInfo.InvariantCulture) + " kg oxygen.");
        }

        public CommandResult CheckBeginEva()
        {
            CommandResult gate = CheckOpen();
            if (!gate.Succeeded)
            {
                return gate;
            }

            if (state.Phase == GamePhase.Eva)
            {
                return Fail(CommandFailure.AlreadyComplete, "The EVA is already under way.");
            }

            if (state.Phase != GamePhase.Repair)
            {
                return Fail(CommandFailure.WrongPhase, "The airlock can only be cycled once the ship is stabilized.");
            }

            if (!IsSuitOn)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "EVA suit required before depressurizing.");
            }

            if (!systems.Power.IsOperational || !systems.LifeSupport.IsOperational)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Power and life support must be operational before an EVA.");
            }

            if (Airlock != AirlockState.InnerOpen && Airlock != AirlockState.Sealed)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "The airlock is not pressurized and idle.");
            }

            CommandResult affordable = resources.CheckAffordable(CycleCosts());
            if (!affordable.Succeeded)
            {
                return affordable;
            }

            return Ok(string.Format(CultureInfo.InvariantCulture, "Seal inner hatch and depressurize: {0:0.##} kg oxygen, {1:0.##} kWh.",
                spec.AirlockCycleOxygenLossKg, spec.AirlockCycleEnergyKWh));
        }

        /// <summary>Seals the inner hatch and starts depressurization. The mission enters the Eva phase.</summary>
        public CommandResult TryBeginEva()
        {
            CommandResult check = CheckBeginEva();
            if (!check.Succeeded)
            {
                return check;
            }

            using (events.DeferDispatch())
            {
                CommandResult spent = resources.TryConsume(CycleCosts());
                if (!spent.Succeeded)
                {
                    return spent;
                }

                if (Airlock != AirlockState.Sealed)
                {
                    SetAirlock(AirlockState.Sealed);
                }

                AirlockTransitionRemainingSeconds = spec.DepressurizationSeconds;
                SetAirlock(AirlockState.Depressurizing);
                IsSuitSealed = true;
                CommandResult entered = state.TryTransition(GamePhase.Eva);
                if (!entered.Succeeded)
                {
                    throw new InvalidOperationException("EVA phase transition failed after the airlock was cycled: " + entered.Explanation);
                }

                NotifyRatesChanged();
                RaiseChanged();
            }

            return Ok("Inner hatch sealed. Depressurizing.");
        }

        public CommandResult CheckGoOutside()
        {
            CommandResult gate = CheckOpen();
            if (!gate.Succeeded)
            {
                return gate;
            }

            if (IsOutside)
            {
                return Fail(CommandFailure.AlreadyComplete, "Already outside.");
            }

            if (state.Phase != GamePhase.Eva || !IsSuitSealed)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "A sealed suit and a depressurized airlock are required to go outside.");
            }

            if (Airlock != AirlockState.OuterOpen)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "The outer hatch is closed.");
            }

            return Ok("Clip tether and go outside.");
        }

        public CommandResult TryGoOutside()
        {
            CommandResult check = CheckGoOutside();
            if (!check.Succeeded)
            {
                return check;
            }

            IsOutside = true;
            RaiseChanged();
            return Ok("Outside on the tether.");
        }

        public CommandResult CheckReturnInside()
        {
            CommandResult gate = CheckOpen();
            if (!gate.Succeeded)
            {
                return gate;
            }

            return IsOutside ? Ok("Enter the airlock.") : Fail(CommandFailure.AlreadyComplete, "Already inside.");
        }

        /// <summary>Back into the open airlock. Never costs anything, so a return is always possible.</summary>
        public CommandResult TryReturnInside()
        {
            CommandResult check = CheckReturnInside();
            if (!check.Succeeded)
            {
                return check;
            }

            IsOutside = false;
            RaiseChanged();
            return Ok("Inside the airlock.");
        }

        public CommandResult CheckEndEva()
        {
            CommandResult gate = CheckOpen();
            if (!gate.Succeeded)
            {
                return gate;
            }

            if (state.Phase != GamePhase.Eva || Airlock != AirlockState.OuterOpen)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "The outer hatch is not open.");
            }

            if (IsOutside)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "You are still outside: enter the airlock first.");
            }

            return Ok("Close outer hatch and repressurize.");
        }

        /// <summary>Closes the outer hatch and starts repressurization. Refused while the crew is outside.</summary>
        public CommandResult TryEndEva()
        {
            CommandResult check = CheckEndEva();
            if (!check.Succeeded)
            {
                return check;
            }

            AirlockTransitionRemainingSeconds = spec.RepressurizationSeconds;
            SetAirlock(AirlockState.Repressurizing);
            RaiseChanged();
            return Ok("Outer hatch closed. Repressurizing.");
        }

        /// <summary>Simulated seconds until an airlock transition completes or the sealed suit runs empty; +Infinity if neither.</summary>
        internal double TimeToNextBoundary()
        {
            double boundary = double.PositiveInfinity;
            if (AirlockTransitionRemainingSeconds > 0d)
            {
                boundary = AirlockTransitionRemainingSeconds;
            }

            if (IsSuitSealed && SuitOxygenKg > 0d)
            {
                boundary = Math.Min(boundary, SuitOxygenSecondsRemaining);
            }

            return boundary;
        }

        /// <summary>Called by the session inside each simulation sub-step, after reserves and game time advanced.</summary>
        internal void AdvanceTime(double simulationSeconds)
        {
            if (simulationSeconds <= 0d)
            {
                return;
            }

            if (IsSuitSealed && SuitOxygenKg > 0d)
            {
                double left = SuitOxygenKg - (spec.SuitOxygenUseKgPerSecond * simulationSeconds);
                SuitOxygenKg = left > Tolerance ? left : 0d;
            }

            if (AirlockTransitionRemainingSeconds > 0d)
            {
                double left = AirlockTransitionRemainingSeconds - simulationSeconds;
                if (left > Tolerance)
                {
                    AirlockTransitionRemainingSeconds = left;
                }
                else
                {
                    AirlockTransitionRemainingSeconds = 0d;
                    CompleteTransition();
                }
            }
        }

        private void CompleteTransition()
        {
            if (Airlock == AirlockState.Depressurizing)
            {
                SetAirlock(AirlockState.OuterOpen);
                RaiseChanged();
                return;
            }

            if (Airlock != AirlockState.Repressurizing)
            {
                return;
            }

            SetAirlock(AirlockState.InnerOpen);
            IsSuitSealed = false;
            IsSuitOn = false;
            SuitOxygenKg = 0d;
            CompletedEvaCount++;
            if (state.Phase == GamePhase.Eva)
            {
                state.TryTransition(systems.Antenna.IsOperational ? GamePhase.Transmission : GamePhase.Repair);
            }

            NotifyRatesChanged();
            RaiseChanged();
        }

        private ResourceAmount[] CycleCosts()
        {
            return new[]
            {
                new ResourceAmount(ResourceId.OxygenKg, spec.AirlockCycleOxygenLossKg),
                new ResourceAmount(ResourceId.BatteryKWh, spec.AirlockCycleEnergyKWh),
            };
        }

        private void SetAirlock(AirlockState next)
        {
            Airlock = next;
            events.Publish(new AirlockStateChanged(next));
        }

        private CommandResult CheckOpen()
        {
            if (state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            return state.IsSimulationRunning
                ? Ok(string.Empty)
                : Fail(CommandFailure.WrongPhase, $"Not available during {state.Phase}.");
        }

        private void NotifyRatesChanged()
        {
            if (ratesChanged != null)
            {
                ratesChanged();
            }
        }

        private void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }

        internal static string FormatDuration(double seconds)
        {
            int minutes = (int)Math.Ceiling(seconds / 60d - 1e-9);
            return minutes >= 60
                ? string.Format(CultureInfo.InvariantCulture, "{0} h {1:00} min", minutes / 60, minutes % 60)
                : string.Format(CultureInfo.InvariantCulture, "{0} min", minutes);
        }

        private static CommandResult Ok(string explanation)
        {
            return new CommandResult(true, CommandFailure.None, explanation);
        }

        private static CommandResult Fail(CommandFailure failure, string explanation)
        {
            return new CommandResult(false, failure, explanation);
        }
    }
}
