namespace OutOfReach.Contracts
{
    public interface ICommunicationSystem
    {
        CommunicationState State { get; }
        double ReferenceDistanceKm { get; }
        double OneWayLightSeconds { get; }
        double EarthProcessingSeconds { get; }
        double ReplyRemainingSimulationSeconds { get; }
        string AstronomicalReferenceId { get; }

        /// <summary>
        /// Requires repaired antenna and power; rejected/repeated sends cannot restart a pending timer.
        /// Core connects operational prerequisites. Reply is no earlier than 2 * one-way light time
        /// plus Earth processing, under the documented fixed-distance approximation.
        /// </summary>
        CommandResult TryTransmit();

        /// <summary>Core supplies simulated seconds; presentation must disclose any accelerated clock.</summary>
        void AdvanceTime(double simulationSeconds);
    }
}
