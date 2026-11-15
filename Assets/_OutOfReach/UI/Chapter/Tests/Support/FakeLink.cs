using OutOfReach.Contracts;

namespace OutOfReach.Tests.ChapterUi
{
    /// <summary>Test double of the link to Earth: the values the strip's NASA/JPL line reads, set by the test.</summary>
    public sealed class FakeLink : ICommunicationSystem
    {
        /// <summary>Row 0 of the committed Horizons snapshot (2026-11-14 08:00 UTC).</summary>
        public const double Day1DistanceKm = 197707975.990852982d;
        public const double Day1LightSeconds = 659.482821248d;

        /// <summary>Row 1 of the committed Horizons snapshot (2026-11-15 08:00 UTC).</summary>
        public const double Day2DistanceKm = 196434081.368594199d;
        public const double Day2LightSeconds = 655.233566178d;

        public CommunicationState State { get; set; } = CommunicationState.Offline;
        public double ReferenceDistanceKm { get; set; } = Day1DistanceKm;
        public double OneWayLightSeconds { get; set; } = Day1LightSeconds;
        public double EarthProcessingSeconds { get; set; } = 600d;
        public double ReplyRemainingSimulationSeconds { get; set; }
        public string AstronomicalReferenceId { get; set; } = "jpl-horizons-earth-mars-2026-v1";
        public int Reads { get; private set; }
        public bool Throws { get; set; }

        double ICommunicationSystem.ReferenceDistanceKm
        {
            get
            {
                Reads++;
                if (Throws) throw new System.InvalidOperationException("fake link failure");
                return ReferenceDistanceKm;
            }
        }

        public CommandResult TryTransmit() => new CommandResult(false, CommandFailure.InvalidInput, "fake");
        public void AdvanceTime(double simulationSeconds) { }
    }
}
