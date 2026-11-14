using System;

namespace OutOfReach.Core
{
    /// <summary>
    /// Read-only view of the mission clock for the HUD: pause state, disclosed acceleration and elapsed simulated
    /// time. Elapsed time is delegated to the game state, so there is no second counter. Member names match the
    /// proposed shared contract ISimulationClock (COORD-DECISIONS-01), so adopting it later is a one-line change.
    /// </summary>
    public sealed class SimulationClockView
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
