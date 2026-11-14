using System.Globalization;
using OutOfReach.Contracts;

namespace OutOfReach.Core
{
    /// <summary>
    /// Pause state and time acceleration for the single simulation clock. While paused, real time is discarded
    /// (no catch-up drain on resume) and cinematic fallback timers also stop. TimeScale is the number of simulated
    /// seconds per real second; any value other than 1 must be disclosed by the HUD.
    /// </summary>
    public sealed class SimulationClock
    {
        public bool IsPaused { get; private set; }

        public double TimeScale { get; private set; } = 1d;

        public CommandResult Pause()
        {
            if (IsPaused)
            {
                return new CommandResult(false, CommandFailure.AlreadyComplete, "The simulation is already paused.");
            }

            IsPaused = true;
            return new CommandResult(true, CommandFailure.None, "Simulation paused.");
        }

        public CommandResult Resume()
        {
            if (!IsPaused)
            {
                return new CommandResult(false, CommandFailure.AlreadyComplete, "The simulation is not paused.");
            }

            IsPaused = false;
            return new CommandResult(true, CommandFailure.None, "Simulation resumed.");
        }

        public CommandResult TrySetTimeScale(double simulatedSecondsPerRealSecond)
        {
            if (double.IsNaN(simulatedSecondsPerRealSecond) || double.IsInfinity(simulatedSecondsPerRealSecond) || simulatedSecondsPerRealSecond <= 0d)
            {
                return new CommandResult(false, CommandFailure.InvalidInput, "Time scale must be finite and greater than zero; use Pause to stop time.");
            }

            TimeScale = simulatedSecondsPerRealSecond;
            return new CommandResult(true, CommandFailure.None, string.Format(CultureInfo.InvariantCulture, "Time scale {0:0.###}x.", simulatedSecondsPerRealSecond));
        }

        /// <summary>Simulated seconds for a real interval: zero while paused, otherwise real x TimeScale.</summary>
        public double ToSimulationSeconds(double realSeconds)
        {
            return IsPaused ? 0d : realSeconds * TimeScale;
        }
    }
}
