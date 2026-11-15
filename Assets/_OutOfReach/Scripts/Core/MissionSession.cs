using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using OutOfReach.Inventory;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// Pure C# composition root for one mission. It owns the single event bus, phase state, planner, resource
    /// ledger, ship systems, director, interactables, simulation clock and cinematic handshake, and it is the only
    /// driver that advances simulated time. A Unity component calls <see cref="Tick"/> once per frame; chapter jumps
    /// call <see cref="AdvanceSimulation"/>.
    /// Presentation binds to the shared contracts these services implement: Events (IMissionEventBus),
    /// State (IGameState), Planner (IMissionPlanner), Resources (IResourceSystem), Configuration,
    /// Systems.Repairables (IRepairable), Communications (ICommunicationSystem) and Interactions (IInteractable
    /// by ID). The debrief is <see cref="Report"/>.
    /// Restart = Dispose this session and construct a new one; nothing carries over between missions, and a
    /// disposed session rejects every command.
    /// </summary>
    public sealed class MissionSession : IDisposable
    {
        /// <summary>Safety bound on boundary splits within one advance (each boundary occurs at most a few times).</summary>
        private const int MaxSplitsPerAdvance = 32;
        private const int MaxScaleSegmentsPerTick = 8;

        private static readonly CommandResult Advanced = new CommandResult(true, CommandFailure.None, "Simulation advanced.");
        private static readonly CommandResult PausedResult = new CommandResult(true, CommandFailure.None, "Paused: real time discarded.");
        private static readonly CommandResult FrozenResult = new CommandResult(true, CommandFailure.None, "Simulation clock is stopped in this phase.");

        private MissionReport cachedReport;
        private int cachedReportJournalCount = -1;
        private GamePhase cachedReportPhase;

        /// <param name="definition">Mission to play; null means AURORA-7.</param>
        /// <param name="subscriberErrorSink">Receives exceptions thrown by event subscribers.</param>
        /// <param name="startingPresetId">Loadout the draft starts with; null means the mission default.</param>
        /// <param name="preflightWalk">
        /// True for the physical preflight: the player may move and interact during Planning (to carry crates) and
        /// the cockpit console launches the mission. False keeps planning a pure list: no control before launch.
        /// </param>
        /// <param name="boardAfterLoadingDeadline">
        /// C05-BOARDING-01, only with <paramref name="preflightWalk"/>: an expiring timed deadline seals the loading and
        /// the mission launches only from the cockpit console (the transport seat). False keeps the immediate launch.
        /// </param>
        public MissionSession(MissionDefinition definition = null, Action<Exception> subscriberErrorSink = null, string startingPresetId = null, bool preflightWalk = false, bool allowMovementDuringImpact = false, bool survivalExtension = false, SurvivalExtensionSpec survivalSpec = null, bool boardAfterLoadingDeadline = false, bool chapterLoop = false)
        {
            PreflightWalkEnabled = preflightWalk;
            BoardingAfterDeadlineEnabled = preflightWalk && boardAfterLoadingDeadline;
            AllowMovementDuringImpact = allowMovementDuringImpact;
            Definition = definition ?? AuroraMission.CreateDefinition();
            Events = new MissionEventBus(subscriberErrorSink);
            State = new MissionGameState(Events);
            Resources = new ResourceLedger(Events, () => State.IsSimulationRunning);
            Clock = new SimulationClock();
            Cinematics = new CinematicCoordinator(Events);

            string presetId = startingPresetId ?? Definition.DefaultPresetId;
            IReadOnlyDictionary<string, int> startingLoadout = presetId == null ? null : Definition.GetPreset(presetId);
            Planner = new MissionPlanner(Definition, State, Resources, Events, startingLoadout);
            PlanningDeadline = new PlanningDeadline(Planner, State, BoardingAfterDeadlineEnabled);
            ClockView = new SimulationClockView(Clock, State);
            Director = new MissionDirector(Definition, Events, State, Planner, Resources, Clock, Cinematics, survivalExtension, survivalSpec, chapterLoop);
            Director.Eva.BindSimulationAdvance(AdvanceSimulation);

            Interactions = new InteractionCatalog();
            Interactions.Add(new CockpitConsoleInteraction(Director, State, Director.Communications, Planner, preflightWalk));
            Interactions.Add(new RepairInteraction(InteractionIds.RepairPower, Director.Systems.Power, Resources));
            Interactions.Add(new RepairInteraction(InteractionIds.RepairLifeSupport, Director.Systems.LifeSupport, Resources));
            Interactions.Add(new RepairInteraction(InteractionIds.RepairAntenna, Director.Systems.Antenna, Resources));

            EvaOperations eva = Director.Eva;
            Interactions.Add(new DelegatedInteraction(InteractionIds.SuitLocker, eva.CheckPrepareSuit, eva.TryPrepareSuit,
                () => eva.IsSuitOn ? "EVA suit on: " + eva.SuitOxygenKg.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " kg oxygen in the tank" : null));
            Interactions.Add(new DelegatedInteraction(InteractionIds.AirlockControls,
                () => eva.Airlock == AirlockState.OuterOpen ? eva.CheckEndEva() : eva.CheckBeginEva(),
                () => eva.Airlock == AirlockState.OuterOpen ? eva.TryEndEva() : eva.TryBeginEva(),
                () => DescribeAirlockTransition(eva)));
            Interactions.Add(new DelegatedInteraction(InteractionIds.EvaExit, eva.CheckGoOutside, eva.TryGoOutside));
            Interactions.Add(new DelegatedInteraction(InteractionIds.EvaReturn, eva.CheckReturnInside, eva.TryReturnInside));

            FieldModifications mods = Director.FieldModifications;
            Interactions.Add(new DelegatedInteraction(InteractionIds.SalvagePayload, mods.CheckSalvagePayload, mods.TrySalvagePayload));
            Interactions.Add(new DelegatedInteraction(InteractionIds.Workbench, mods.CheckBuildCartridge, mods.TryBuildCartridge));
            Interactions.Add(new DelegatedInteraction(InteractionIds.AbsorberBay, mods.CheckInstallCartridge, mods.TryInstallCartridge));

            if (survivalExtension)
            {
                SurvivalExtension survival = Director.Survival;
                Interactions.Add(new DelegatedInteraction(InteractionIds.ConsumeRation, survival.CheckConsumeRation, survival.TryConsumeRation));
                Interactions.Add(new DelegatedInteraction(InteractionIds.DrinkWater, survival.CheckDrinkWater, survival.TryDrinkWater));
                Interactions.Add(new DelegatedInteraction(InteractionIds.BuildGrowModule, survival.CheckBuildGrowModule, survival.TryBuildGrowModule));
                Interactions.Add(new DelegatedInteraction(InteractionIds.PlantSeedlings, survival.CheckPlant, survival.TryPlant));
                Interactions.Add(new DelegatedInteraction(InteractionIds.HarvestCrop, survival.CheckHarvest, survival.TryHarvest));
                SurvivalRecovery recovery = Director.SurvivalRecovery;
                Interactions.Add(new DelegatedInteraction(InteractionIds.SurvivalSleep, recovery.CheckSleepInteraction, recovery.TrySleepInteraction));
                Interactions.Add(new DelegatedInteraction(InteractionIds.RecoveryBeacon, recovery.CheckPowerBeacon, recovery.TryPowerBeacon));
            }

            Cargo = new CargoLoading(Definition, Planner, State, Events, text => Director.Journal.Record(0d, text));
            foreach (EquipmentDefinition item in Definition.Catalog.Definitions)
            {
                string equipmentId = item.Id;
                Interactions.Add(new DelegatedInteraction(InteractionIds.CargoSupply(equipmentId),
                    () => Cargo.CheckSupply(equipmentId), () => Cargo.TryUseSupply(equipmentId)));
                Interactions.Add(new DelegatedInteraction(InteractionIds.CargoBay(equipmentId),
                    () => Cargo.CheckBay(equipmentId), () => Cargo.TryUseBay(equipmentId)));
            }
        }

        public MissionDefinition Definition { get; }

        public MissionEventBus Events { get; }

        public MissionGameState State { get; }

        public MissionPlanner Planner { get; }

        public ResourceLedger Resources { get; }

        public SimulationClock Clock { get; }

        /// <summary>Read-only clock for the HUD (pause, disclosed time scale, elapsed simulated time).</summary>
        public SimulationClockView ClockView { get; }

        /// <summary>Optional real-time launch deadline for planning; disabled unless timed planning is started.</summary>
        public PlanningDeadline PlanningDeadline { get; }

        public CinematicCoordinator Cinematics { get; }

        public MissionDirector Director { get; }

        public ShipSystems Systems => Director.Systems;

        /// <summary>Suit, airlock and outside state for the EVA.</summary>
        public EvaOperations Eva => Director.Eva;

        /// <summary>The link to Earth (implements ICommunicationSystem): NASA/JPL reference distance, light time, reply timer.</summary>
        public CommunicationSystem Communications => Director.Communications;

        /// <summary>Mission date on the simulation clock.</summary>
        public MissionCalendar Calendar => Director.Calendar;

        /// <summary>Payload salvage and the CO2 absorber cartridge (implements IFieldModifications).</summary>
        public FieldModifications FieldModifications => Director.FieldModifications;

        public SurvivalExtension Survival => Director.Survival;

        /// <summary>
        /// Rest and the fictional recovery timeline (implements ISurvivalRecovery). Null when the survival extension
        /// is disabled, so callers must handle null.
        /// </summary>
        public SurvivalRecovery SurvivalRecovery => Director.SurvivalRecovery;

        /// <summary>
        /// The approved day 1-30 chapter loop (implements IMissionChapterFlow): beats, interruptible routine, decisions
        /// and the day-14 dispatch. Null unless the session was built with chapterLoop (which needs survivalExtension).
        /// </summary>
        public MissionChapterFlow ChapterFlow => Director.ChapterFlow;

        /// <summary>Preflight crate handling: the crate in hands and the carry speed. The bay itself is the planner's draft.</summary>
        public CargoLoading Cargo { get; }

        /// <summary>True when the player walks and loads crates during Planning (physical preflight).</summary>
        public bool PreflightWalkEnabled { get; }

        /// <summary>
        /// True when an expiring timed deadline seals the loading and waits for the seat (C05-BOARDING-01). Only with
        /// the physical preflight: without player control nobody could reach the seat, so the legacy launch applies.
        /// </summary>
        public bool BoardingAfterDeadlineEnabled { get; }

        /// <summary>Opt-in playable impact. Other cinematic locks and pause still block control.</summary>
        public bool AllowMovementDuringImpact { get; }

        public InteractionCatalog Interactions { get; }

        /// <summary>Unpaused real seconds this session was driven through flight phases by <see cref="Tick"/>.</summary>
        public double PlayedRealSeconds { get; private set; }

        /// <summary>Simulated seconds that passed in chapter jumps (<see cref="AdvanceSimulation"/>) instead of frames.</summary>
        public double JumpedSimulationSeconds { get; private set; }

        /// <summary>
        /// The final mission report once an outcome is decided, otherwise null. Immutable; the same instance is
        /// returned until the mission records something new. Use <see cref="BuildReport"/> for a mid-mission preview.
        /// </summary>
        public MissionReport Report
        {
            get
            {
                if (State.Outcome == MissionOutcome.None)
                {
                    return null;
                }

                if (cachedReport == null || cachedReportJournalCount != Director.Journal.Count || cachedReportPhase != State.Phase)
                {
                    cachedReport = MissionReportBuilder.Build(this);
                    cachedReportJournalCount = Director.Journal.Count;
                    cachedReportPhase = State.Phase;
                }

                return cachedReport;
            }
        }

        /// <summary>
        /// Says which field modifications this ship physically offers. Both are off until this is called: a scene
        /// without the instrument target, or without the workbench and the absorber bay, plays exactly as before.
        /// The Unity runtime calls it from the scene bindings.
        /// </summary>
        public void EnableFieldModifications(bool payloadSalvage, bool absorberCartridge)
        {
            Director.FieldModifications.SalvageEnabled = payloadSalvage;
            Director.FieldModifications.AbsorberEnabled = absorberCartridge;
        }

        /// <summary>Builds a report from the current state at any time (IsFinal is false while the mission runs).</summary>
        public MissionReport BuildReport()
        {
            return MissionReportBuilder.Build(this);
        }

        public bool IsDisposed { get; private set; }

        /// <summary>Frozen configuration after launch, otherwise the current draft.</summary>
        public IMissionConfiguration Configuration => (IMissionConfiguration)Planner.LaunchedConfiguration ?? Planner.Draft;

        /// <summary>
        /// True while the player may move and interact: a running phase (or Planning, when the physical preflight
        /// is on), not paused and no blocking cinematic. AllowMovementDuringImpact opts out of the Impact lock
        /// only; every other sequence retains it. The handshake has a fallback timeout.
        /// </summary>
        public bool IsPlayerControlAllowed
        {
            get
            {
                if (IsDisposed || State.IsClosed || Clock.IsPaused || (ChapterFlow != null && ChapterFlow.IsRoutineRunning)
                    || (Cinematics.IsWaiting && !(AllowMovementDuringImpact && Cinematics.ActiveCinematic == CinematicId.Impact)))
                {
                    return false;
                }

                return State.IsSimulationRunning || (PreflightWalkEnabled && State.Phase == GamePhase.Planning);
            }
        }

        /// <summary>
        /// Frame entry for a Unity driver. During Planning the full unscaled frame time feeds the planning deadline,
        /// so a hitch cannot stretch the 60 real seconds; in every other phase the frame time is clamped first, so a
        /// hitch or alt-tab cannot cause a catch-up drain of simulated reserves.
        /// </summary>
        public CommandResult TickFrame(double unscaledRealSeconds, double hitchClampRealSeconds)
        {
            if (!IsFiniteNonNegative(unscaledRealSeconds) || !IsFiniteNonNegative(hitchClampRealSeconds))
            {
                return new CommandResult(false, CommandFailure.InvalidInput, "Frame time and hitch clamp must be finite and non-negative.");
            }

            bool planning = State.Phase == GamePhase.Planning;
            return Tick(planning ? unscaledRealSeconds : Math.Min(unscaledRealSeconds, hitchClampRealSeconds));
        }

        /// <summary>
        /// Per-frame driver entry with unscaled real seconds. Paused: nothing happens and the time is discarded.
        /// Otherwise the cinematic fallback timer advances by real time. During Planning the planning deadline
        /// advances by real time and no simulated time is spent, even on the frame in which the deadline launches.
        /// In running phases the simulation advances by real time x TimeScale; when a step changes the scale (a
        /// rest beginning or ending, contact), the rest of the frame continues at the new scale. Invalid or unrepresentable time is
        /// rejected before anything changes.
        /// </summary>
        public CommandResult Tick(double realSeconds)
        {
            if (!IsFiniteNonNegative(realSeconds))
            {
                return new CommandResult(false, CommandFailure.InvalidInput, "Real time must be finite and non-negative.");
            }

            if (IsDisposed)
            {
                return new CommandResult(false, CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (Clock.IsPaused)
            {
                return PausedResult;
            }

            double simulationSeconds = Clock.ToSimulationSeconds(realSeconds);
            if (!IsFiniteNonNegative(simulationSeconds) || !IsFiniteNonNegative(State.ElapsedSimulationSeconds + simulationSeconds))
            {
                return new CommandResult(false, CommandFailure.InvalidInput, "The scaled simulation time is too large to represent.");
            }

            bool planning = State.Phase == GamePhase.Planning;
            Cinematics.TickRealTime(realSeconds);
            if (planning)
            {
                PlanningDeadline.TickRealTime(realSeconds);
                return FrozenResult;
            }

            if (!State.IsSimulationRunning)
            {
                return FrozenResult;
            }

            PlayedRealSeconds += realSeconds;
            return AdvanceRealTime(realSeconds);
        }

        /// <summary>
        /// Advances every simulation service by the same simulated seconds, in a fixed order. The step is split
        /// exactly where a reserve reaches zero or cabin CO2 reaches the loss limit; after every part the director
        /// synchronously recomputes the rates and applies the survival rules (this does not depend on any event
        /// listener). One long jump therefore equals many small frames, and a failure inside the jump ends the
        /// mission at its exact time; the rest of the jump is discarded. Events are delivered after the whole
        /// advance has been committed. Rejected outside running phases, while paused, and when time would overflow.
        /// This public entry is the chapter jump (suit preparation, a chapter card skip); the time that actually
        /// passed is added to <see cref="JumpedSimulationSeconds"/> for the report. A chapter routine that stops inside
        /// the jump ends it at that exact time; the rest of the jump is discarded.
        /// </summary>
        public CommandResult AdvanceSimulation(double simulationSeconds)
        {
            double before = State.ElapsedSimulationSeconds;
            CommandResult result = Advance(simulationSeconds);
            if (result.Succeeded)
            {
                JumpedSimulationSeconds += State.ElapsedSimulationSeconds - before;
            }

            return result;
        }

        private CommandResult Advance(double simulationSeconds)
        {
            return Advance(simulationSeconds, double.NaN, out double unused, out bool routineStopped);
        }

        /// <summary>
        /// The step loop. With a finite <paramref name="stopWhenScaleLeaves"/> it also stops right after a step that
        /// moved the clock's time scale away from that value; <paramref name="spentSimulationSeconds"/> is the part of
        /// the budget actually used. A chapter routine that was running when the advance began and stops inside it
        /// always ends the advance right there (<paramref name="routineStopped"/>): its stop is a point where the player
        /// must see the reason first, so no remaining budget may be spent at the restored speed.
        /// </summary>
        private CommandResult Advance(double simulationSeconds, double stopWhenScaleLeaves, out double spentSimulationSeconds, out bool routineStopped)
        {
            spentSimulationSeconds = 0d;
            routineStopped = false;
            if (!IsFiniteNonNegative(simulationSeconds))
            {
                return new CommandResult(false, CommandFailure.InvalidInput, "Simulation time must be finite and non-negative.");
            }

            if (IsDisposed)
            {
                return new CommandResult(false, CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (!State.IsSimulationRunning)
            {
                return new CommandResult(false, CommandFailure.WrongPhase, $"The simulation clock does not run during {State.Phase}.");
            }

            if (Clock.IsPaused)
            {
                return new CommandResult(false, CommandFailure.PreconditionsNotMet, "The simulation is paused; resume before advancing time.");
            }

            if (!IsFiniteNonNegative(State.ElapsedSimulationSeconds + simulationSeconds))
            {
                return new CommandResult(false, CommandFailure.InvalidInput, "Elapsed simulation time would overflow.");
            }

            using (Events.DeferDispatch())
            {
                MissionChapterFlow routine = Director.ChapterFlow != null && Director.ChapterFlow.IsRoutineRunning ? Director.ChapterFlow : null;
                double remaining = simulationSeconds;
                int splits = 0;
                while (remaining > 0d && State.IsSimulationRunning)
                {
                    double step = remaining;
                    if (splits >= MaxSplitsPerAdvance && Director.ChapterFlow != null && Director.ChapterFlow.IsRoutineRunning)
                    {
                        // A routine never relies on the unsplit fallback: it yields the rest of this budget instead.
                        break;
                    }

                    if (splits < MaxSplitsPerAdvance)
                    {
                        double boundary = Math.Min(Resources.TimeToNextDepletion(), Director.TimeToNextBoundary());
                        if (boundary < step)
                        {
                            step = boundary;
                            splits++;
                        }
                    }

                    Resources.AdvanceTime(step);
                    State.AdvanceTime(step);
                    Director.Eva.AdvanceTime(step);
                    Director.FieldModifications.AdvanceTime(step);
                    remaining -= step;
                    Director.AfterSimulationStep(step);
                    if (routine != null && !routine.IsRoutineRunning)
                    {
                        routineStopped = true;
                        break;
                    }

                    if (!double.IsNaN(stopWhenScaleLeaves) && Clock.TimeScale != stopWhenScaleLeaves)
                    {
                        break;
                    }
                }

                spentSimulationSeconds = simulationSeconds - remaining;
            }

            return Advanced;
        }

        /// <summary>
        /// Spends a frame's real-time budget at the clock's current scale and converts what is left again whenever a
        /// step changes the scale (a rest beginning or ending, contact moving the mission into Survival), so one long
        /// frame advances exactly as far as many short ones. When a chapter routine stops inside the frame, the rest of
        /// the frame is discarded: the mission waits at the routine's actual stop time for the player (and the UI,
        /// which receives the events after the frame). Events are delivered after the whole frame.
        /// </summary>
        private CommandResult AdvanceRealTime(double realSeconds)
        {
            using (Events.DeferDispatch())
            {
                double remainingReal = realSeconds;
                for (int segment = 0; remainingReal > 0d && State.IsSimulationRunning && segment < MaxScaleSegmentsPerTick; segment++)
                {
                    double scale = Clock.TimeScale;
                    double budget = remainingReal * scale;
                    CommandResult result = Advance(budget, scale, out double spent, out bool routineStopped);
                    if (!result.Succeeded)
                    {
                        return result;
                    }

                    remainingReal = routineStopped || spent >= budget ? 0d : Math.Max(0d, remainingReal - (spent / scale));
                }
            }

            return Advanced;
        }

        /// <summary>
        /// Ends this mission's lifetime: closes the state so every later command is rejected, cancels cinematics
        /// and removes every event subscription.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            State.Close();
            Director.Dispose();
            Cinematics.Dispose();
            Events.ClearAllSubscriptions();
        }

        private static string DescribeAirlockTransition(EvaOperations eva)
        {
            if (eva.Airlock == AirlockState.Depressurizing)
            {
                return "Depressurizing: " + EvaOperations.FormatDuration(eva.AirlockTransitionRemainingSeconds) + " left";
            }

            return eva.Airlock == AirlockState.Repressurizing
                ? "Repressurizing: " + EvaOperations.FormatDuration(eva.AirlockTransitionRemainingSeconds) + " left"
                : null;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }
    }
}
