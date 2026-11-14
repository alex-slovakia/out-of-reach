using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// Pure C# composition root for one mission. It owns the single event bus, phase state, planner, resource
    /// ledger, simulation clock and cinematic handshake, and it is the only driver that advances simulated time.
    /// A Unity component calls <see cref="Tick"/> once per frame; chapter jumps call <see cref="AdvanceSimulation"/>.
    /// Presentation binds to the shared contracts these services implement: Events (IMissionEventBus),
    /// State (IGameState), Planner (IMissionPlanner), Resources (IResourceSystem), Configuration.
    /// Restart = Dispose this session and construct a new one; nothing carries over between missions.
    /// </summary>
    public sealed class MissionSession : IDisposable
    {
        /// <summary>Safety bound on depletion splits within one advance (each reserve can deplete only once).</summary>
        private const int MaxSplitsPerAdvance = 16;

        private static readonly CommandResult Advanced = new CommandResult(true, CommandFailure.None, "Simulation advanced.");
        private static readonly CommandResult PausedResult = new CommandResult(true, CommandFailure.None, "Paused: real time discarded.");
        private static readonly CommandResult FrozenResult = new CommandResult(true, CommandFailure.None, "Simulation clock is stopped in this phase.");

        public MissionSession(MissionDefinition definition = null, Action<Exception> subscriberErrorSink = null, string startingPresetId = null)
        {
            Definition = definition ?? AuroraMission.CreateDefinition();
            Events = new MissionEventBus(subscriberErrorSink);
            State = new MissionGameState(Events);
            Resources = new ResourceLedger(Events);
            Clock = new SimulationClock();
            Cinematics = new CinematicCoordinator(Events);

            string presetId = startingPresetId ?? Definition.DefaultPresetId;
            IReadOnlyDictionary<string, int> startingLoadout = presetId == null ? null : Definition.GetPreset(presetId);
            Planner = new MissionPlanner(Definition, State, Resources, Events, startingLoadout);
        }

        public MissionDefinition Definition { get; }

        public MissionEventBus Events { get; }

        public MissionGameState State { get; }

        public MissionPlanner Planner { get; }

        public ResourceLedger Resources { get; }

        public SimulationClock Clock { get; }

        public CinematicCoordinator Cinematics { get; }

        public bool IsDisposed { get; private set; }

        /// <summary>Frozen configuration after launch, otherwise the current draft.</summary>
        public IMissionConfiguration Configuration => (IMissionConfiguration)Planner.LaunchedConfiguration ?? Planner.Draft;

        /// <summary>
        /// Per-frame driver entry with unscaled real seconds. Paused: nothing happens and the time is discarded.
        /// Otherwise the cinematic fallback timer advances by real time and, in running phases, the simulation
        /// advances by real time x TimeScale.
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

            Cinematics.TickRealTime(realSeconds);
            if (!State.IsSimulationRunning)
            {
                return FrozenResult;
            }

            return AdvanceSimulation(Clock.ToSimulationSeconds(realSeconds));
        }

        /// <summary>
        /// Advances every simulation service by the same simulated seconds, in a fixed order, splitting the step
        /// exactly where a reserve reaches zero so long steps and chapter jumps match many small frames. Events
        /// are delivered after the whole advance has been committed. Rejected outside running phases.
        /// </summary>
        public CommandResult AdvanceSimulation(double simulationSeconds)
        {
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

            using (Events.DeferDispatch())
            {
                double remaining = simulationSeconds;
                int splits = 0;
                while (remaining > 0d && State.IsSimulationRunning)
                {
                    double step = remaining;
                    if (splits < MaxSplitsPerAdvance)
                    {
                        double untilDepletion = Resources.TimeToNextDepletion();
                        if (untilDepletion < step)
                        {
                            step = untilDepletion;
                            splits++;
                        }
                    }

                    Resources.AdvanceTime(step);
                    State.AdvanceTime(step);
                    remaining -= step;
                }
            }

            return Advanced;
        }

        /// <summary>Ends this mission's lifetime: cancels cinematics and removes every event subscription.</summary>
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            Cinematics.Dispose();
            Events.ClearAllSubscriptions();
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }
    }
}
