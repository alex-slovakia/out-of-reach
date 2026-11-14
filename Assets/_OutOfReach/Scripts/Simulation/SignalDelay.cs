using System;

namespace OutOfReach.Simulation
{
    /// <summary>
    /// DERIVED PHYSICS for the communication delay. Distance comes from the NASA/JPL reference sample; nothing here
    /// is tuned to fit dialogue.
    /// <list type="bullet">
    /// <item>one-way light time (s) = distance (km) / 299,792.458 km/s (vacuum speed of light);</item>
    /// <item>earliest reply (s) = 2 x one-way light time + ground processing time, under the frozen-distance
    /// approximation: both bodies are treated as fixed for the minutes the signal travels.</item>
    /// </list>
    /// Link availability, occultation, acquisition and the transmission time of the message are not modelled.
    /// </summary>
    public static class SignalDelay
    {
        /// <summary>Vacuum speed of light, km/s (exact by definition of the metre).</summary>
        public const double SpeedOfLightKmPerSecond = 299792.458d;

        /// <summary>One-way light travel time in seconds for a distance in kilometres.</summary>
        public static double OneWaySeconds(double distanceKm)
        {
            if (double.IsNaN(distanceKm) || double.IsInfinity(distanceKm) || distanceKm < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(distanceKm), distanceKm, "Distance must be finite and non-negative.");
            }

            return distanceKm / SpeedOfLightKmPerSecond;
        }

        /// <summary>
        /// Earliest time, in seconds after transmission, at which a reply can be received: the signal travels out,
        /// the ground processes it, and the answer travels back.
        /// </summary>
        public static double EarliestReplySeconds(double oneWaySeconds, double processingSeconds)
        {
            if (double.IsNaN(oneWaySeconds) || double.IsInfinity(oneWaySeconds) || oneWaySeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(oneWaySeconds), oneWaySeconds, "Light time must be finite and non-negative.");
            }

            if (double.IsNaN(processingSeconds) || double.IsInfinity(processingSeconds) || processingSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(processingSeconds), processingSeconds, "Processing time must be finite and non-negative.");
            }

            return (2d * oneWaySeconds) + processingSeconds;
        }
    }
}
