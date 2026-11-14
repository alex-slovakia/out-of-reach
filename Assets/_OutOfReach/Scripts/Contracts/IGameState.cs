namespace OutOfReach.Contracts
{
    /// <summary>Core owns transitions. Presentation observes state and requests only cinematic completion.</summary>
    public interface IGameState
    {
        GamePhase Phase { get; }
        MissionOutcome Outcome { get; }
        double ElapsedSimulationSeconds { get; }
        CommandResult TryTransition(GamePhase next);

        /// <summary>Called once per simulation step by Core, with finite non-negative simulated seconds.</summary>
        void AdvanceTime(double simulationSeconds);
    }
}
