using System;
using OutOfReach.Contracts;

namespace OutOfReach.Core
{
    /// <summary>
    /// Read-only view of the mission clock for the HUD (implements the shared <see cref="ISimulationClock"/>):
    /// pause state, disclosed acceleration and elapsed simulated time. TimeScale keeps its value while paused.
    /// Elapsed time is delegated to the game state, so there is no second counter.
    /// </summary>
    public sealed class SimulationClockView : ISimulationClock
    {
        private readonly SimulationClock clock;
        private readonly MissionGameState state;

        internal SimulationClockView(SimulationClock clock, MissionGameState state)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public bool IsPaused => clock.IsPaused;

        /// <summary>Simulated seconds per real second. Any value other than 1 must be shown to the player.</summary>
        public double TimeScale => clock.TimeScale;

        public double ElapsedSimulationSeconds => state.ElapsedSimulationSeconds;
    }
}
