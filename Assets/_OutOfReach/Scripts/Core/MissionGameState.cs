using System;
using OutOfReach.Contracts;

namespace OutOfReach.Core
{
    /// <summary>
    /// Authoritative mission phase, outcome and elapsed simulation time. Every transition is validated before it
    /// mutates anything and happens once; a rejected or repeated request publishes nothing. Launch enters Intro
    /// only through <see cref="MissionPlanner.TryLaunch"/>, and only Core ends a mission (<see cref="TryEndMission"/>).
    /// </summary>
    public sealed class MissionGameState : IGameState
    {
        private readonly IMissionEventBus events;
        private Func<GamePhase, GamePhase, CommandResult> transitionGuard;

        public MissionGameState(IMissionEventBus events)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
        }

        /// <summary>True after the owning session was disposed: every command is rejected and time stands still.</summary>
        public bool IsClosed { get; private set; }

        public GamePhase Phase { get; private set; } = GamePhase.Planning;

        public MissionOutcome Outcome { get; private set; } = MissionOutcome.None;

        public double ElapsedSimulationSeconds { get; private set; }

        /// <summary>Causal explanation recorded with the outcome (empty while the mission runs).</summary>
        public string OutcomeReason { get; private set; } = string.Empty;

        /// <summary>Simulation time at which the outcome was decided, or null while the mission runs.</summary>
        public double? EndedAtSimulationSeconds { get; private set; }

        public bool IsSimulationRunning => !IsClosed && MissionPhaseRules.IsSimulationRunning(Phase);

        public CommandResult TryTransition(GamePhase next)
        {
            if (IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (!Enum.IsDefined(typeof(GamePhase), next))
            {
                return Fail(CommandFailure.InvalidInput, $"Unknown phase value {(int)next}.");
            }

            if (next == Phase)
            {
                return Fail(CommandFailure.AlreadyComplete, $"Already in {Phase}.");
            }

            if (Phase == GamePhase.Planning && next == GamePhase.Intro)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Launch through IMissionPlanner.TryLaunch so the loadout is validated and frozen.");
            }

            if (next == GamePhase.Outcome)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Only Core decides a mission outcome.");
            }

            if (!MissionPhaseRules.IsLegal(Phase, next))
            {
                return Fail(CommandFailure.WrongPhase, $"{Phase} cannot transition to {next}.");
            }

            if (transitionGuard != null)
            {
                CommandResult allowed = transitionGuard(Phase, next);
                if (!allowed.Succeeded)
                {
                    return allowed;
                }
            }

            return Commit(next);
        }

        /// <summary>
        /// Mission prerequisites for legal transitions (set by the director): a legal edge is still rejected until
        /// its gameplay conditions hold, whoever asks. Null removes the guard.
        /// </summary>
        internal void SetTransitionGuard(Func<GamePhase, GamePhase, CommandResult> guard)
        {
            transitionGuard = guard;
        }

        /// <summary>Marks the session as ended. Irreversible; a restart builds a new session.</summary>
        internal void Close()
        {
            IsClosed = true;
        }

        /// <summary>Planning to Intro, used only by the planner after validation and freezing.</summary>
        internal CommandResult TryCommitLaunch()
        {
            if (IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (Phase != GamePhase.Planning)
            {
                return Fail(CommandFailure.WrongPhase, $"Launch requires Planning; current phase is {Phase}.");
            }

            return Commit(GamePhase.Intro);
        }

        /// <summary>
        /// Decides the mission result once, from any running phase: commits Outcome, then publishes PhaseChanged
        /// and MissionEnded. A second call is rejected with AlreadyComplete and publishes nothing.
        /// </summary>
        public CommandResult TryEndMission(MissionOutcome outcome, string reason, ResourceSnapshot finalResources)
        {
            if (IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (!Enum.IsDefined(typeof(MissionOutcome), outcome) || outcome == MissionOutcome.None)
            {
                return Fail(CommandFailure.InvalidInput, "A mission ending must be RecoveryPossible or Lost.");
            }

            if (Outcome != MissionOutcome.None)
            {
                return Fail(CommandFailure.AlreadyComplete, $"The mission already ended ({Outcome}).");
            }

            if (!MissionPhaseRules.IsSimulationRunning(Phase))
            {
                return Fail(CommandFailure.WrongPhase, $"A mission cannot end during {Phase}.");
            }

            GamePhase previous = Phase;
            Phase = GamePhase.Outcome;
            Outcome = outcome;
            OutcomeReason = reason ?? string.Empty;
            EndedAtSimulationSeconds = ElapsedSimulationSeconds;
            events.Publish(new PhaseChanged(previous, GamePhase.Outcome));
            events.Publish(new MissionEnded(outcome, OutcomeReason, finalResources));
            return Ok($"Mission ended: {outcome}.");
        }

        /// <summary>
        /// Accumulates simulated seconds while a running phase is active (Core calls this once per step).
        /// Invalid time throws before mutation; frozen phases ignore valid time.
        /// </summary>
        public void AdvanceTime(double simulationSeconds)
        {
            if (double.IsNaN(simulationSeconds) || double.IsInfinity(simulationSeconds) || simulationSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationSeconds), simulationSeconds, "Simulation time must be finite and non-negative.");
            }

            if (!IsSimulationRunning)
            {
                return;
            }

            double advanced = ElapsedSimulationSeconds + simulationSeconds;
            if (double.IsInfinity(advanced))
            {
                throw new ArgumentOutOfRangeException(nameof(simulationSeconds), simulationSeconds, "Elapsed simulation time would overflow.");
            }

            ElapsedSimulationSeconds = advanced;
        }

        private CommandResult Commit(GamePhase next)
        {
            GamePhase previous = Phase;
            Phase = next;
            events.Publish(new PhaseChanged(previous, next));
            return Ok($"{previous} -> {next}.");
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
