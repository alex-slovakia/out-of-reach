using System;
using OutOfReach.Contracts;

namespace OutOfReach.Presentation.LaunchFailure
{
    /// <summary>
    /// Decides, from the published <see cref="MissionEnded"/> signal only, whether the launch-failure cinematic plays:
    /// a Lost outcome whose reason is Core's overweight-launch text ("Launch mass N kg exceeded the M kg limit. The
    /// vehicle failed to reach orbit..."). Every other loss, and every win, plays nothing. Pure; reads no Core type.
    /// </summary>
    public static class LaunchFailureTrigger
    {
        public const string ReasonPrefix = "Launch mass ";
        public const string ReasonMarker = " kg limit. The vehicle failed to reach orbit";

        public static bool ShouldPlay(MissionOutcome outcome, string reason)
        {
            return outcome == MissionOutcome.Lost
                && !string.IsNullOrEmpty(reason)
                && reason.StartsWith(ReasonPrefix, StringComparison.Ordinal)
                && reason.IndexOf(ReasonMarker, StringComparison.Ordinal) > 0;
        }

        public static bool ShouldPlay(MissionEnded ended) => ShouldPlay(ended.Outcome, ended.Reason);
    }
}
