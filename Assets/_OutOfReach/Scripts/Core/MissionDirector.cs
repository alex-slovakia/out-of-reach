using System;
using System.Collections.Generic;
using System.Globalization;
using OutOfReach.Contracts;
using OutOfReach.Inventory;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// Authoritative mission orchestration. Presentation never damages, repairs, drains or decides anything; it
    /// only observes the events this class causes.
    /// <list type="bullet">
    /// <item>Cockpit check (Intro) triggers the scripted impact exactly once: Core requests the Impact cinematic,
    /// damages the systems and re-evaluates the resource rates. When the cinematic is acknowledged, skipped or
    /// times out, the mission enters Emergency.</item>
    /// <item>The first interior repair moves Emergency to Repair. An EVA needs power and life support operational.</item>
    /// <item>After every simulation step and every repair the rates are recomputed from one model
    /// (<see cref="EmergencyResourceModel"/>), so a battery that runs flat stops the scrubbers at that exact moment.</item>
    /// <item>Survival rules end the mission as Lost with a causal explanation; nothing is skipped across long steps.</item>
    /// </list>
    /// </summary>
    public sealed class MissionDirector : IDisposable
    {
        private readonly MissionDefinition definition;
        private readonly MissionEventBus events;
        private readonly MissionGameState state;
        private readonly MissionPlanner planner;
        private readonly ResourceLedger resources;
        private readonly SimulationClock clock;
        private readonly CinematicCoordinator cinematics;
        private bool impactSequenceFinished;
        private bool disposed;

        public MissionDirector(
            MissionDefinition definition,
            MissionEventBus events,
            MissionGameState state,
            MissionPlanner planner,
            ResourceLedger resources,
            SimulationClock clock,
            CinematicCoordinator cinematics)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.planner = planner ?? throw new ArgumentNullException(nameof(planner));
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.cinematics = cinematics ?? throw new ArgumentNullException(nameof(cinematics));

            var systems = new List<ShipSystem>();
            foreach (ShipSystemSpec spec in definition.Scenario.Systems)
            {
                systems.Add(new ShipSystem(spec.SystemId, spec.DisplayName, spec.RepairCosts, events, CheckRepairPrerequisites, events.DeferDispatch, OnSystemRepaired));
            }

            Systems = new ShipSystems(systems);
            state.SetTransitionGuard(CheckTransition);
            events.PhaseChanged += OnPhaseChanged;
        }

        public ShipSystems Systems { get; }

        /// <summary>True once the scripted impact has been applied. It can never be applied twice.</summary>
        public bool AccidentTriggered { get; private set; }

        public double? AccidentAtSimulationSeconds { get; private set; }

        /// <summary>Latest evaluation of the power and life-support model (generation, load, brownout).</summary>
        public EmergencyState PowerState { get; private set; }

        public bool IsBrownout => PowerState.Brownout;

        /// <summary>Loss cause decided by the survival rules, or None.</summary>
        public SurvivalFailure LossCause { get; private set; }

        /// <summary>
        /// Completes the routine cockpit check and triggers the impact. Intro only, once. Delivers
        /// CinematicRequested(Impact) first, then one SystemConditionChanged per damaged system.
        /// </summary>
        public CommandResult TryCompleteCockpitCheck()
        {
            if (disposed || state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (AccidentTriggered)
            {
                return Fail(CommandFailure.AlreadyComplete, "The impact has already happened.");
            }

            if (state.Phase != GamePhase.Intro)
            {
                return Fail(CommandFailure.WrongPhase, $"The cockpit check belongs to Intro; current phase is {state.Phase}.");
            }

            if (cinematics.IsWaiting)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Another sequence is still playing.");
            }

            using (events.DeferDispatch())
            {
                AccidentTriggered = true;
                AccidentAtSimulationSeconds = state.ElapsedSimulationSeconds;
                CommandResult requested = cinematics.TryRequest(CinematicId.Impact, OnImpactSequenceFinished, definition.Scenario.ImpactCinematicTimeoutRealSeconds);
                foreach (ShipSystemSpec spec in definition.Scenario.Systems)
                {
                    if (spec.ConditionAfterAccident01 < 1d)
                    {
                        Systems.Get(spec.SystemId).TryDamage(spec.ConditionAfterAccident01);
                    }
                }

                RefreshRates();
                if (!requested.Succeeded)
                {
                    // No cinematic could be requested: never leave the mission stuck in Intro.
                    OnImpactSequenceFinished(default(CinematicCompletion));
                }
            }

            return Ok("Cockpit check complete. Impact detected.");
        }

        /// <summary>Ends the mission with the reserves as they are now. Only Core calls this.</summary>
        public CommandResult TryEndMission(MissionOutcome outcome, string reason)
        {
            return state.TryEndMission(outcome, reason, resources.Snapshot);
        }

        /// <summary>
        /// Simulated seconds until a rule boundary the step loop must not jump over (cabin CO2 reaching the loss
        /// limit); +Infinity when there is none ahead.
        /// </summary>
        public double TimeToNextBoundary()
        {
            double boundary = double.PositiveInfinity;
            double untilLimit = resources.TimeUntilCarbonDioxideReaches(SurvivalRules.CarbonDioxideLossLimitPpm);
            if (untilLimit > 0d && untilLimit < boundary)
            {
                boundary = untilLimit;
            }

            double? oxygenDepletedAt = resources.GetDepletionTime(ResourceId.OxygenKg);
            if (oxygenDepletedAt.HasValue)
            {
                double untilBufferEnds = oxygenDepletedAt.Value + CabinAtmosphere.OxygenBufferSeconds(definition.CrewCount) - resources.ElapsedSimulationSeconds;
                if (untilBufferEnds > 0d && untilBufferEnds < boundary)
                {
                    boundary = untilBufferEnds;
                }
            }

            return boundary;
        }

        /// <summary>Called by the session after each simulation sub-step: keeps rates and survival consistent.</summary>
        public void AfterSimulationStep()
        {
            RefreshRates();
            EvaluateSurvival();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            events.PhaseChanged -= OnPhaseChanged;
            state.SetTransitionGuard(null);
        }

        private void OnImpactSequenceFinished(CinematicCompletion completion)
        {
            impactSequenceFinished = true;
            if (state.Phase == GamePhase.Intro)
            {
                state.TryTransition(GamePhase.Emergency);
            }
        }

        private void OnSystemRepaired(ShipSystem system)
        {
            RefreshRates();
            bool interior = !definition.Scenario.GetSystem(system.SystemId).RepairRequiresEva;
            if (interior && state.Phase == GamePhase.Emergency)
            {
                state.TryTransition(GamePhase.Repair);
            }

            EvaluateSurvival();
        }

        private void OnPhaseChanged(PhaseChanged message)
        {
            clock.TrySetTimeScale(definition.Scenario.Pacing.GetTimeScale(message.Current));
            if (message.Previous == GamePhase.Planning && message.Current == GamePhase.Intro)
            {
                MissionConfiguration launched = planner.LaunchedConfiguration;
                if (launched != null)
                {
                    resources.TrySetCarbonDioxideFloor(launched.InitialResources.CarbonDioxidePpm);
                }

                RefreshRates();
            }
        }

        private CommandResult CheckRepairPrerequisites(ShipSystem system)
        {
            if (disposed || state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            bool requiresEva = definition.Scenario.GetSystem(system.SystemId).RepairRequiresEva;
            if (requiresEva)
            {
                return state.Phase == GamePhase.Eva
                    ? Ok("Exterior repair allowed.")
                    : Fail(CommandFailure.WrongPhase, $"The {system.DisplayName} can only be repaired outside, during an EVA.");
            }

            return state.Phase == GamePhase.Emergency || state.Phase == GamePhase.Repair
                ? Ok("Interior repair allowed.")
                : Fail(CommandFailure.WrongPhase, $"The {system.DisplayName} cannot be repaired during {state.Phase}.");
        }

        private CommandResult CheckTransition(GamePhase from, GamePhase to)
        {
            if (from == GamePhase.Intro && to == GamePhase.Emergency && !impactSequenceFinished)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "The emergency begins only after the impact sequence.");
            }

            if (from == GamePhase.Emergency && to == GamePhase.Repair && Systems.Power.RepairCount + Systems.LifeSupport.RepairCount == 0)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Stabilize power or life support first.");
            }

            if (to == GamePhase.Eva && !(Systems.Power.IsOperational && Systems.LifeSupport.IsOperational))
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Power and life support must be operational before an EVA.");
            }

            return Ok("Transition allowed.");
        }

        private void RefreshRates()
        {
            MissionConfiguration launched = planner.LaunchedConfiguration;
            if (launched == null || !resources.IsInitialized)
            {
                return;
            }

            var inputs = new ShipPowerInputs(
                definition.CrewCount,
                launched.RequiredPowerWatts,
                launched.AvailablePowerWatts,
                Systems.Power.Condition01,
                Systems.LifeSupport.Condition01,
                resources.Snapshot.BatteryKWh <= 0d);
            PowerState = EmergencyResourceModel.Evaluate(inputs);
            resources.TrySetRates(PowerState.Rates);
        }

        private void EvaluateSurvival()
        {
            if (!state.IsSimulationRunning)
            {
                return;
            }

            SurvivalFailure failure = SurvivalRules.Evaluate(
                resources.Snapshot,
                resources.GetDepletionTime(ResourceId.OxygenKg),
                resources.ElapsedSimulationSeconds,
                definition.CrewCount);
            if (failure == SurvivalFailure.None)
            {
                return;
            }

            LossCause = failure;
            TryEndMission(MissionOutcome.Lost, DescribeLoss(failure));
        }

        private string DescribeLoss(SurvivalFailure failure)
        {
            string elapsed = FormatDuration(state.ElapsedSimulationSeconds);
            bool lifeSupportDown = !Systems.LifeSupport.IsOperational;
            bool powerDown = !Systems.Power.IsOperational;
            switch (failure)
            {
                case SurvivalFailure.OxygenDepleted:
                    double depletedAt = resources.GetDepletionTime(ResourceId.OxygenKg) ?? state.ElapsedSimulationSeconds;
                    string buffer = FormatDuration(CabinAtmosphere.OxygenBufferSeconds(definition.CrewCount));
                    return $"Stored oxygen ran out {FormatDuration(depletedAt)} after launch. The cabin's own air stayed above the impairment level for another {buffer}; then the mission was lost."
                        + (lifeSupportDown ? " The damaged life-support loop kept leaking stored oxygen because it was never repaired." : string.Empty);
                case SurvivalFailure.CarbonDioxideLimit:
                    string limit = SurvivalRules.CarbonDioxideLossLimitPpm.ToString("#,0", CultureInfo.InvariantCulture);
                    if (lifeSupportDown)
                    {
                        return $"Cabin CO2 reached the {limit} ppm emergency level {elapsed} after launch. Life support was not repaired, so nothing removed the crew's CO2.";
                    }

                    return $"Cabin CO2 reached the {limit} ppm emergency level {elapsed} after launch."
                        + (powerDown
                            ? " The battery ran flat because power distribution was not repaired, so the repaired scrubbers stopped."
                            : " The battery ran flat and the scrubbers stopped.");
                case SurvivalFailure.WaterDepleted:
                    return $"Water reserve exhausted {elapsed} after launch.";
                case SurvivalFailure.FoodDepleted:
                    return $"Food reserve exhausted {elapsed} after launch.";
                default:
                    return "Mission lost.";
            }
        }

        private static string FormatDuration(double seconds)
        {
            double hours = seconds / 3600d;
            return hours < 48d
                ? string.Format(CultureInfo.InvariantCulture, "{0:0.0} h", hours)
                : string.Format(CultureInfo.InvariantCulture, "{0:0.0} days", hours / 24d);
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
