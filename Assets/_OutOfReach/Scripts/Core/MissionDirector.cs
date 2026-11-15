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
    /// <item>Survival rules end the mission as Lost with a causal explanation; nothing is skipped across long steps.
    /// A stabilized ship that can never repair its antenna (not even by salvaging the science payload) is also
    /// Lost: no contact, no recovery plan.</item>
    /// <item>Field modifications feed the same model: a salvaged instrument's load leaves the bus, and an installed
    /// CO2 absorber cartridge takes up a rising cabin CO2 rate until its capacity is used, split exactly in time.</item>
    /// <item>In Transmission the console first runs a link diagnostic, once: Core requests the FalseSignal
    /// cinematic (the damaged receive buffer replays an old local recording). It grants nothing and is not a reply.
    /// Only then can the distress call be sent, once; Earth's reply ends the default slice as RecoveryPossible.
    /// The opt-in extension instead enters Survival and keeps the same ledger and clock running.
    /// If a loss and the reply fall on the same instant, the loss wins.</item>
    /// <item>After any ending Core requests the ending cinematics (CommunicationRestored then EndingPullback after
    /// a recovery, RecoveryArrival then MarsDeparture after the optional chapter's fictional rescue, EndingPullback
    /// after a loss) and then enters Report by itself.</item>
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
        private readonly Dictionary<ShipSystemId, double> repairedAt = new Dictionary<ShipSystemId, double>();
        private bool impactSequenceFinished;
        private bool disposed;

        public MissionDirector(
            MissionDefinition definition,
            MissionEventBus events,
            MissionGameState state,
            MissionPlanner planner,
            ResourceLedger resources,
            SimulationClock clock,
            CinematicCoordinator cinematics, bool survivalExtension = false, SurvivalExtensionSpec survivalSpec = null, bool chapterLoop = false)
        {
            if (chapterLoop && !survivalExtension)
            {
                throw new ArgumentException("The chapter loop needs the survival extension.", nameof(chapterLoop));
            }

            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.planner = planner ?? throw new ArgumentNullException(nameof(planner));
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.cinematics = cinematics ?? throw new ArgumentNullException(nameof(cinematics));

            EarthMarsReference reference = EarthMarsReference.Embedded;
            if (!string.Equals(definition.Parameters.AstronomicalReferenceId, reference.DatasetId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The mission names astronomical reference '{definition.Parameters.AstronomicalReferenceId}' but the packaged dataset is '{reference.DatasetId}'.");
            }

            DateTime epochAtStart = definition.Scenario.MissionEpochAtStartUtc ?? reference.FirstEpochUtc;
            if (epochAtStart < reference.FirstEpochUtc || epochAtStart > reference.LastEpochUtc)
            {
                throw new InvalidOperationException(
                    $"Mission clock T0 {MissionCalendar.Format(epochAtStart)} is outside the packaged reference ({MissionCalendar.Format(reference.FirstEpochUtc)} to {MissionCalendar.Format(reference.LastEpochUtc)}).");
            }

            Journal = new MissionJournal(() => this.state.ElapsedSimulationSeconds);
            Calendar = new MissionCalendar(epochAtStart, () => this.state.ElapsedSimulationSeconds);

            var systems = new List<ShipSystem>();
            foreach (ShipSystemSpec spec in definition.Scenario.Systems)
            {
                systems.Add(new ShipSystem(spec.SystemId, spec.DisplayName, spec.RepairCosts, events, CheckRepairPrerequisites, events.DeferDispatch, OnSystemRepaired));
            }

            Systems = new ShipSystems(systems);
            Eva = new EvaOperations(definition.Scenario.Eva, events, state, resources, Systems, RefreshRates, Journal.Record);
            FieldModifications = new FieldModifications(definition.Scenario.FieldModifications, definition, events, state, planner,
                resources, Systems, OnFieldModified, Journal.Record);
            Communications = new CommunicationSystem(
                events,
                resources,
                reference,
                definition.Scenario.Communication,
                () => Calendar.NowUtc,
                () => this.state.ElapsedSimulationSeconds,
                CheckTransmitPrerequisites,
                IsLinkPowered,
                events.DeferDispatch);
            Survival = new SurvivalExtension(survivalExtension, survivalSpec, definition.CrewCount, events, state,
                resources, clock, cinematics, HasGrowingConditions, OnSurvivalModified, Journal.Record);
            SurvivalRecovery = survivalExtension
                ? new SurvivalRecovery(definition.CrewCount, events, state, resources, clock, cinematics, Survival, Calendar,
                    () => IsBrownout, RefreshRates, Journal.Record, reason => TryEndMission(MissionOutcome.Recovered, reason))
                : null;
            if (chapterLoop)
            {
                SurvivalRecovery.EndMissedRendezvous = reason => TryEndMission(MissionOutcome.Lost, reason);
            }

            ChapterFlow = chapterLoop
                ? new MissionChapterFlow(events, state, resources, clock, cinematics, Survival, SurvivalRecovery, Systems, Eva,
                    FieldModifications, Calendar, IsLinkPowered, Journal.Record)
                : null;
            state.SetTransitionGuard(CheckTransition);
            events.PhaseChanged += OnPhaseChanged;
            events.CommunicationChanged += OnCommunicationChanged;
            events.MissionEnded += OnMissionEnded;
        }

        public ShipSystems Systems { get; }

        /// <summary>Suit, airlock and outside state for the EVA.</summary>
        public EvaOperations Eva { get; }

        /// <summary>Payload salvage and the CO2 absorber cartridge.</summary>
        public FieldModifications FieldModifications { get; }

        public SurvivalExtension Survival { get; }

        /// <summary>Rest and the fictional recovery timeline; null when the survival extension is disabled.</summary>
        public SurvivalRecovery SurvivalRecovery { get; }

        /// <summary>The approved day 1-30 chapter loop; null unless enabled.</summary>
        public MissionChapterFlow ChapterFlow { get; }

        /// <summary>The link to Earth: reference distance, light time, the one transmission and the reply timer.</summary>
        public CommunicationSystem Communications { get; }

        /// <summary>Mission date for the simulation clock (T0 + elapsed simulated time).</summary>
        public MissionCalendar Calendar { get; }

        /// <summary>What happened and when, for the report's timeline.</summary>
        public MissionJournal Journal { get; }

        /// <summary>True once the scripted impact has been applied. It can never be applied twice.</summary>
        public bool AccidentTriggered { get; private set; }

        public double? AccidentAtSimulationSeconds { get; private set; }

        /// <summary>Latest evaluation of the power and life-support model (generation, load, brownout).</summary>
        public EmergencyState PowerState { get; private set; }

        public bool IsBrownout => PowerState.Brownout;

        /// <summary>Loss cause decided by the survival rules, or None.</summary>
        public SurvivalFailure LossCause { get; private set; }

        /// <summary>Highest cabin CO2 concentration since launch, ppm.</summary>
        public double PeakCarbonDioxidePpm { get; private set; }

        /// <summary>True once the console's link diagnostic was started (the false-signal beat). It runs only once.</summary>
        public bool LinkDiagnosticRequested { get; private set; }

        /// <summary>True once the FalseSignal sequence was acknowledged, skipped or timed out: transmitting is possible.</summary>
        public bool LinkDiagnosticCompleted { get; private set; }

        /// <summary>Simulation time of a system's first repair, or null if it was never repaired.</summary>
        public double? GetRepairTime(ShipSystemId systemId)
        {
            return repairedAt.TryGetValue(systemId, out double at) ? at : (double?)null;
        }

        /// <summary>
        /// Completes the routine cockpit check and triggers the impact. Intro only, once. Delivers
        /// CinematicRequested(Impact) first, then one SystemConditionChanged per damaged system, then
        /// CommunicationChanged(Offline).
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
                var damage = new List<string>();
                foreach (ShipSystemSpec spec in definition.Scenario.Systems)
                {
                    if (spec.ConditionAfterAccident01 < 1d)
                    {
                        Systems.Get(spec.SystemId).TryDamage(spec.ConditionAfterAccident01);
                        damage.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1:0} %", spec.DisplayName, spec.ConditionAfterAccident01 * 100d));
                    }
                }

                Journal.Record("Debris impact. Condition after the strike: " + string.Join(", ", damage) + ".");
                RefreshRates();
                Communications.RefreshState();
                if (!requested.Succeeded)
                {
                    // No cinematic could be requested: never leave the mission stuck in Intro.
                    OnImpactSequenceFinished(default(CinematicCompletion));
                }
            }

            return Ok("Cockpit check complete. Impact detected.");
        }

        /// <summary>Explains whether the link diagnostic can run now, without changing anything.</summary>
        public CommandResult CheckLinkDiagnostic()
        {
            if (disposed || state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (LinkDiagnosticRequested)
            {
                return Fail(CommandFailure.AlreadyComplete, LinkDiagnosticCompleted
                    ? "The link diagnostic has already run."
                    : "The link diagnostic is still running.");
            }

            CommandResult hardware = CheckLinkHardware();
            if (!hardware.Succeeded)
            {
                return hardware;
            }

            if (cinematics.IsWaiting)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Another sequence is still playing.");
            }

            return Ok("Run communications diagnostic");
        }

        /// <summary>
        /// The false-hope beat, owned by Core: the first console action after the return. The damaged receive buffer
        /// replays an old local recording and Core requests the FalseSignal cinematic (acknowledge, skip or
        /// fallback timeout). It is not a reply from Earth: the link state, the reserves and the light-time model
        /// are untouched, nothing is published on CommunicationChanged, and it can never run twice.
        /// </summary>
        public CommandResult TryRunLinkDiagnostic()
        {
            CommandResult check = CheckLinkDiagnostic();
            if (!check.Succeeded)
            {
                return check;
            }

            using (events.DeferDispatch())
            {
                LinkDiagnosticRequested = true;
                Journal.Record("Link diagnostic: the damaged receive buffer replayed an old local recording. It was not a reply from Earth.");
                CommandResult requested = cinematics.TryRequest(CinematicId.FalseSignal, OnFalseSignalFinished, CinematicCoordinator.DefaultTimeoutRealSeconds);
                if (!requested.Succeeded)
                {
                    OnFalseSignalFinished(default(CinematicCompletion));
                }
            }

            return Ok("Link diagnostic: a signal is in the receive buffer. It is an old local recording replaying, not a reply from Earth.");
        }

        /// <summary>Ends the mission with the reserves as they are now. Only Core calls this.</summary>
        public CommandResult TryEndMission(MissionOutcome outcome, string reason)
        {
            return state.TryEndMission(outcome, reason, resources.Snapshot);
        }

        /// <summary>
        /// Simulated seconds until a rule boundary the step loop must not jump over (cabin CO2 reaching the loss
        /// limit, the cabin oxygen buffer ending, an airlock or suit boundary, Earth's reply becoming due);
        /// +Infinity when there is none ahead.
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

            boundary = Math.Min(boundary, Eva.TimeToNextBoundary());
            boundary = Math.Min(boundary, FieldModifications.TimeToNextBoundary());
            boundary = Math.Min(boundary, Survival.TimeToNextBoundary());
            if (SurvivalRecovery != null)
            {
                boundary = Math.Min(boundary, SurvivalRecovery.TimeToNextBoundary());
            }

            if (ChapterFlow != null)
            {
                boundary = Math.Min(boundary, ChapterFlow.TimeToNextBoundary());
            }

            return Math.Min(boundary, Communications.TimeToNextBoundary());
        }

        /// <summary>
        /// Called by the session after each simulation sub-step: keeps rates consistent, applies the survival rules
        /// and only then lets the link receive a due reply, so a loss at the same instant is never skipped.
        /// </summary>
        public void AfterSimulationStep(double stepSeconds = 0d)
        {
            RefreshRates();
            Survival.AdvanceTime(stepSeconds);
            SurvivalRecovery?.AdvanceTime(stepSeconds);
            TrackPeakCarbonDioxide();
            EvaluateSurvival();
            SurvivalRecovery?.AfterStep();
            ChapterFlow?.AfterStep();
            if (!state.IsSimulationRunning)
            {
                return;
            }

            Communications.AdvanceTime(stepSeconds);
            if (Communications.State == CommunicationState.ReplyReceived && state.Outcome == MissionOutcome.None)
            {
                if (Survival.Enabled)
                {
                    if (state.Phase == GamePhase.Transmission && state.TryTransition(GamePhase.Survival).Succeeded)
                    {
                        Survival.Begin();
                        clock.TrySetTimeScale(Survival.Spec.TimeScale);
                        SurvivalRecovery?.BeginRecovery();
                        ChapterFlow?.OnContact();
                    }
                }
                else TryEndMission(MissionOutcome.RecoveryPossible, DescribeRecovery());
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            ChapterFlow?.Dispose();
            Survival.Dispose();
            SurvivalRecovery?.Dispose();
            events.PhaseChanged -= OnPhaseChanged;
            events.CommunicationChanged -= OnCommunicationChanged;
            events.MissionEnded -= OnMissionEnded;
            state.SetTransitionGuard(null);
        }

        /// <summary>After a survival change (portion accounting started, module built/planted/stopped, crop event).</summary>
        private void OnSurvivalModified()
        {
            RefreshRates();
            ChapterFlow?.OnSurvivalModified();
        }

        /// <summary>After a salvage, a built or an installed cartridge: same model, same rules, immediately.</summary>
        private void OnFieldModified()
        {
            RefreshRates();
            EvaluateSurvival();
            ChapterFlow?.OnFieldModified();
        }

        private void OnFalseSignalFinished(CinematicCompletion completion)
        {
            LinkDiagnosticCompleted = true;
        }

        private void OnImpactSequenceFinished(CinematicCompletion completion)
        {
            impactSequenceFinished = true;
            if (state.Phase == GamePhase.Intro && state.TryTransition(GamePhase.Emergency).Succeeded)
            {
                ChapterFlow?.OnEmergencyBegan();
            }
        }

        private void OnSystemRepaired(ShipSystem system)
        {
            if (!repairedAt.ContainsKey(system.SystemId))
            {
                repairedAt[system.SystemId] = state.ElapsedSimulationSeconds;
            }

            Journal.Record(Capitalize(system.DisplayName) + " repaired: " + system.DescribeCosts() + ".");
            RefreshRates();
            Communications.RefreshState();
            bool interior = !definition.Scenario.GetSystem(system.SystemId).RepairRequiresEva;
            if (interior && state.Phase == GamePhase.Emergency)
            {
                state.TryTransition(GamePhase.Repair);
            }

            EvaluateSurvival();
        }

        private void OnPhaseChanged(PhaseChanged message)
        {
            clock.TrySetTimeScale(message.Current == GamePhase.Survival ? Survival.Spec.TimeScale : definition.Scenario.Pacing.GetTimeScale(message.Current));
            if (message.Previous == GamePhase.Planning && message.Current == GamePhase.Intro)
            {
                MissionConfiguration launched = planner.LaunchedConfiguration;
                if (launched != null)
                {
                    resources.TrySetCarbonDioxideFloor(launched.InitialResources.CarbonDioxidePpm);
                    PeakCarbonDioxidePpm = launched.InitialResources.CarbonDioxidePpm;
                    Journal.Record(0d, string.Format(CultureInfo.InvariantCulture,
                        "Launch on {0}: {1:0} kg payload, {2:0} credits, {3:0} W load on {4:0} W generation.",
                        MissionCalendar.Format(Calendar.EpochAtStartUtc), launched.TotalMassKg, launched.TotalBudgetCredits,
                        launched.RequiredPowerWatts, launched.AvailablePowerWatts));
                }

                RefreshRates();
                if (OverweightLaunch.IsOverweight(launched))
                {
                    // R27: an overweight manifest launched; the ascent fails through the normal Lost outcome path.
                    LossCause = SurvivalFailure.LaunchOverweight;
                    TryEndMission(MissionOutcome.Lost, OverweightLaunch.DescribeLoss(launched));
                }
            }

            if (message.Current == GamePhase.Report)
            {
                // The report was reached (by Core or by a skip): no ending cinematic may still hold the session.
                cinematics.Cancel();
            }
        }

        private void OnCommunicationChanged(CommunicationChanged message)
        {
            if (message.State == CommunicationState.AwaitingReply && Communications.TransmittedAtSimulationSeconds.HasValue)
            {
                EarthMarsSample sample = Communications.ReferenceSample;
                Journal.Record(Communications.TransmittedAtSimulationSeconds.Value, string.Format(CultureInfo.InvariantCulture,
                    "Distress call transmitted on mission day {0}. Reference sample {1}: Earth {2} away, one-way light time {3}, earliest reply after {4}.",
                    MissionCalendar.DayNumberAt(Communications.TransmittedAtSimulationSeconds.Value),
                    MissionCalendar.Format(sample.EpochUtc),
                    CommunicationSystem.FormatDistance(sample.DistanceKm),
                    CommunicationSystem.FormatDuration(sample.OneWayLightSeconds),
                    CommunicationSystem.FormatDuration(Communications.ReplyDelaySimulationSeconds)));
            }
            else if (message.State == CommunicationState.ReplyReceived && Communications.ReplyReceivedAtSimulationSeconds.HasValue)
            {
                Journal.Record(Communications.ReplyReceivedAtSimulationSeconds.Value, "Reply from Earth received.");
            }
        }

        private void OnMissionEnded(MissionEnded message)
        {
            SurvivalRecovery?.OnMissionEnded();
            ChapterFlow?.OnMissionEnded();
            double endedAt = state.EndedAtSimulationSeconds ?? state.ElapsedSimulationSeconds;
            Journal.Record(endedAt, message.Outcome == MissionOutcome.RecoveryPossible
                ? "Mission outcome: recovery possible."
                : message.Outcome == MissionOutcome.Recovered
                    ? "Mission outcome: recovered by the fictional recovery craft."
                    : "Mission outcome: lost.");
            BeginEndingSequence(message.Outcome);
        }

        /// <summary>
        /// Ending cinematics, then Report. An unfinished earlier sequence (for example the Impact) is superseded;
        /// its late acknowledgement is ignored as stale. Each request has the scenario's fallback timeout and
        /// completes immediately when no presentation listens, so the session can never hang in Outcome.
        /// </summary>
        private void BeginEndingSequence(MissionOutcome outcome)
        {
            if (disposed || state.Phase != GamePhase.Outcome)
            {
                return;
            }

            cinematics.Cancel();
            if (outcome == MissionOutcome.RecoveryPossible)
            {
                RequestEndingCinematic(CinematicId.CommunicationRestored, () => RequestEndingCinematic(CinematicId.EndingPullback, EnterReport));
            }
            else if (outcome == MissionOutcome.Recovered)
            {
                RequestEndingCinematic(CinematicId.RecoveryArrival, () => RequestEndingCinematic(CinematicId.MarsDeparture, EnterReport));
            }
            else
            {
                RequestEndingCinematic(CinematicId.EndingPullback, EnterReport);
            }
        }

        private void RequestEndingCinematic(CinematicId cinematic, Action next)
        {
            if (disposed || state.Phase != GamePhase.Outcome)
            {
                return;
            }

            CommandResult requested = cinematics.TryRequest(cinematic, completion => next(), definition.Scenario.EndingCinematicTimeoutRealSeconds);
            if (!requested.Succeeded)
            {
                next();
            }
        }

        private void EnterReport()
        {
            if (!disposed && state.Phase == GamePhase.Outcome)
            {
                state.TryTransition(GamePhase.Report);
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
                if (state.Phase != GamePhase.Eva)
                {
                    return Fail(CommandFailure.WrongPhase, $"The {system.DisplayName} can only be repaired outside, during an EVA.");
                }

                return Eva.IsOutside
                    ? Ok("Exterior repair allowed.")
                    : Fail(CommandFailure.PreconditionsNotMet, $"Go outside to reach the {system.DisplayName}.");
            }

            return state.Phase == GamePhase.Emergency || state.Phase == GamePhase.Repair
                ? Ok("Interior repair allowed.")
                : Fail(CommandFailure.WrongPhase, $"The {system.DisplayName} cannot be repaired during {state.Phase}.");
        }

        /// <summary>Operational prerequisites for sending the distress call (the link supplies date and energy checks).</summary>
        private CommandResult CheckTransmitPrerequisites()
        {
            if (disposed || state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            CommandResult hardware = CheckLinkHardware();
            if (!hardware.Succeeded)
            {
                return hardware;
            }

            if (!LinkDiagnosticRequested)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Run the communications diagnostic at the console first.");
            }

            if (!LinkDiagnosticCompleted)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "The link diagnostic is still running.");
            }

            return Ok("Link ready.");
        }

        /// <summary>Phase, antenna and power conditions shared by the diagnostic and the transmission.</summary>
        private CommandResult CheckLinkHardware()
        {
            if (!state.IsSimulationRunning)
            {
                return Fail(CommandFailure.WrongPhase, $"The link cannot be used during {state.Phase}.");
            }

            if (!AccidentTriggered)
            {
                return Fail(CommandFailure.WrongPhase, "Communications nominal: no distress call is needed.");
            }

            if (!Systems.Antenna.IsOperational)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Communications offline: antenna damaged");
            }

            if (!Systems.Power.IsOperational)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Communications offline: power distribution damaged");
            }

            if (PowerState.Brownout)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Communications offline: brownout, the battery is empty");
            }

            if (state.Phase != GamePhase.Transmission)
            {
                return Fail(CommandFailure.WrongPhase, "Antenna repaired. Return through the airlock before transmitting.");
            }

            return Ok("Link hardware ready.");
        }

        /// <summary>True while the antenna and the power bus can carry a signal in either direction.</summary>
        private bool IsLinkPowered()
        {
            return !disposed && !state.IsClosed && Systems.Antenna.IsOperational && Systems.Power.IsOperational && !PowerState.Brownout;
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

            if (to == GamePhase.Eva && (!Eva.IsSuitOn || Eva.Airlock != AirlockState.Depressurizing))
            {
                return Fail(CommandFailure.PreconditionsNotMet, "An EVA starts at the airlock controls with the suit on.");
            }

            if (from == GamePhase.Eva && (to == GamePhase.Repair || to == GamePhase.Transmission) && Eva.Airlock != AirlockState.InnerOpen)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Return through the airlock and repressurize first.");
            }

            if (to == GamePhase.Transmission && !Systems.Antenna.IsOperational)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "The antenna must be repaired before transmitting.");
            }

            if (to == GamePhase.Survival && (!Survival.Enabled || Communications.State != CommunicationState.ReplyReceived))
                return Fail(CommandFailure.PreconditionsNotMet, "The optional survival chapter starts only after actual restored contact.");

            return Ok("Transition allowed.");
        }

        private bool HasGrowingConditions()
        {
            if (!Systems.Power.IsOperational || !Systems.LifeSupport.IsOperational || PowerState.Brownout) return false;
            // Check the added light/fan load before accepting planting, including a battery already at zero.
            double extra = Survival.GrowStage == GrowModuleStage.Growing ? 0d : Survival.Spec.GrowPowerWatts;
            return resources.Snapshot.BatteryKWh > 0d || PowerState.GenerationWatts >= PowerState.LoadWatts + extra;
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
                Math.Max(0d, launched.RequiredPowerWatts - FieldModifications.RemovedLoadWatts) + (Survival != null ? Survival.GrowLoadWatts : 0d),
                launched.AvailablePowerWatts,
                Systems.Power.Condition01,
                Systems.LifeSupport.Condition01,
                resources.Snapshot.BatteryKWh <= 0d,
                Eva.IsSuitSealed);
            PowerState = EmergencyResourceModel.Evaluate(inputs);
            ResourceRates rates = PowerState.Rates;
            double absorbed = FieldModifications.AbsorbPpmPerSecond(rates.CarbonDioxideChangePpmPerSecond);
            if (absorbed > 0d)
            {
                rates = new ResourceRates(rates.OxygenUseKgPerSecond, rates.WaterUseLitresPerSecond, rates.FoodUseKgPerSecond,
                    rates.NetLoadWatts, rates.CarbonDioxideChangePpmPerSecond - absorbed);
            }

            if (Survival != null && Survival.IsActive)
            {
                // Awake, food and drinking water are spent only by crew commands; while the crew rests they are drawn
                // continuously at the BVAD crew-day rates instead (SurvivalRecovery), never both.
                double restFood = SurvivalRecovery != null ? SurvivalRecovery.RestFoodKgPerSecond : 0d;
                double restWater = SurvivalRecovery != null ? SurvivalRecovery.RestWaterLitresPerSecond : 0d;
                rates = new ResourceRates(rates.OxygenUseKgPerSecond, Survival.GrowWaterLitresPerSecond + restWater, restFood,
                    rates.NetLoadWatts, rates.CarbonDioxideChangePpmPerSecond);
            }
            resources.TrySetRates(rates);
        }

        private void TrackPeakCarbonDioxide()
        {
            if (resources.IsInitialized)
            {
                PeakCarbonDioxidePpm = Math.Max(PeakCarbonDioxidePpm, resources.Snapshot.CarbonDioxidePpm);
            }
        }

        private void EvaluateSurvival()
        {
            if (!state.IsSimulationRunning)
            {
                return;
            }

            if (Eva.IsSuitSealed && Eva.SuitOxygenKg <= 0d)
            {
                LossCause = SurvivalFailure.SuitOxygenDepleted;
                string where = Eva.IsOutside ? "outside the ship" : "in the airlock";
                string before = Eva.IsOutside ? "before the return to the airlock" : "before repressurization completed";
                TryEndMission(MissionOutcome.Lost, $"Suit oxygen ran out {where} {FormatDuration(state.ElapsedSimulationSeconds)} after launch, {before}.");
                return;
            }

            SurvivalFailure failure = SurvivalRules.Evaluate(
                resources.Snapshot,
                resources.GetDepletionTime(ResourceId.OxygenKg),
                resources.ElapsedSimulationSeconds,
                definition.CrewCount, !Survival.IsActive);
            if (failure == SurvivalFailure.None && Survival.IsActive) failure = Survival.NeedsFailure;
            if (failure != SurvivalFailure.None)
            {
                LossCause = failure;
                TryEndMission(MissionOutcome.Lost, DescribeLoss(failure) + DescribePendingReply());
                return;
            }

            if (IsContactImpossible(out int needed, out int available))
            {
                LossCause = SurvivalFailure.ContactImpossible;
                TryEndMission(MissionOutcome.Lost, DescribeContactImpossible(needed, available));
            }
        }

        /// <summary>
        /// The stable-but-doomed state: back inside with power and life support running, the antenna still
        /// damaged and fewer spare parts than its repair needs. Parts are never replenished, so contact can never be
        /// restored; without this rule the mission would only end weeks of simulated time later.
        /// </summary>
        private bool IsContactImpossible(out int needed, out int available)
        {
            needed = 0;
            foreach (ResourceAmount cost in Systems.Antenna.RepairCosts)
            {
                if (cost.Resource == ResourceId.SparePartsCount)
                {
                    needed += (int)cost.Quantity;
                }
            }

            available = resources.Snapshot.SparePartsCount;
            return state.Phase == GamePhase.Repair
                && !Systems.Antenna.IsOperational
                && Systems.Power.IsOperational
                && Systems.LifeSupport.IsOperational
                && available + FieldModifications.SalvageablePartsNow < needed;
        }

        private string DescribeContactImpossible(int needed, int available)
        {
            HoldingEstimate holding = HoldingEstimate.Compute(resources.Snapshot, resources.Rates);
            string reserves = holding.IsBounded
                ? string.Format(CultureInfo.InvariantCulture, " Reserves cover about {0:0} more days.", Math.Floor(holding.Days))
                : string.Empty;
            return string.Format(CultureInfo.InvariantCulture,
                "Mission objective unrecoverable: the {0} needs {1} spare parts and only {2} remain{3}, so contact with Earth can never be restored. The crew is alive and the ship is stable, but no recovery can be planned.{4}",
                Systems.Antenna.DisplayName, needed, available, available == 1 ? "s" : string.Empty, reserves);
        }

        private string DescribeRecovery()
        {
            EarthMarsSample sample = Communications.ReferenceSample;
            double sentAt = Communications.TransmittedAtSimulationSeconds ?? state.ElapsedSimulationSeconds;
            double receivedAt = Communications.ReplyReceivedAtSimulationSeconds ?? state.ElapsedSimulationSeconds;
            HoldingEstimate holding = HoldingEstimate.Compute(resources.Snapshot, resources.Rates);
            string reserves = holding.IsBounded
                ? string.Format(CultureInfo.InvariantCulture, " At current use the reserves cover about {0:0} days ({1} runs out first).", Math.Floor(holding.Days), holding.LimitedBy)
                : string.Empty;
            return string.Format(CultureInfo.InvariantCulture,
                "Earth answered {0} after the distress call: twice the {1} light time across {2}, plus {3} on Earth. Contact is restored and a recovery trajectory can be assessed. Rescue has not arrived; the ship must hold.{4}",
                CommunicationSystem.FormatDuration(receivedAt - sentAt),
                CommunicationSystem.FormatDuration(sample.OneWayLightSeconds),
                CommunicationSystem.FormatDistance(sample.DistanceKm),
                CommunicationSystem.FormatDuration(Communications.EarthProcessingSeconds),
                reserves);
        }

        /// <summary>Extra sentence for a loss that happened while the reply was still travelling.</summary>
        private string DescribePendingReply()
        {
            if (Communications.State != CommunicationState.AwaitingReply)
            {
                return string.Empty;
            }

            double remaining = Communications.ReplyRemainingSimulationSeconds;
            return remaining > 0d
                ? " The distress call had been sent; Earth's reply could not arrive for another " + CommunicationSystem.FormatDuration(remaining, true) + "."
                : " The distress call had been sent, but the unpowered link could not receive Earth's reply.";
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
                case SurvivalFailure.HungerLimit:
                    return $"Unmet food demand reached the scenario's game viability limit {elapsed} after launch. This is a mission failure assumption, not a medical starvation threshold.";
                case SurvivalFailure.ThirstLimit:
                    return $"Unmet water demand reached the scenario's game viability limit {elapsed} after launch. This is a mission failure assumption, not a medical dehydration threshold.";
                case SurvivalFailure.WaterDepleted:
                    return $"Water reserve exhausted {elapsed} after launch.";
                case SurvivalFailure.FoodDepleted:
                    return $"Food reserve exhausted {elapsed} after launch.";
                default:
                    return "Mission lost.";
            }
        }

        private static string Capitalize(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
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
