using System;
using OutOfReach.Contracts;

namespace OutOfReach.Presentation.Cinematics.Preflight
{
    /// <summary>Decision-only sampling. The integer watermark suppresses duplicate cues; it is not a timer.</summary>
    internal sealed class PreflightTensionLogic
    {
        internal const double PulseLeadSeconds = 30d;
        private int lowestCuedSecond = 11;

        internal void Reset() => lowestCuedSecond = 11;

        internal PreflightTensionFrame Sample(GamePhase phase, bool modeChosen, bool deadlineEnabled,
            bool paused, double remainingRealSeconds, bool reducedEffects)
        {
            if (phase != GamePhase.Planning || !modeChosen || !deadlineEnabled || paused
                || double.IsNaN(remainingRealSeconds) || double.IsInfinity(remainingRealSeconds)
                || remainingRealSeconds <= 0d)
                return default;

            float urgency = (float)Math.Max(0d, Math.Min(1d,
                (PulseLeadSeconds - remainingRealSeconds) / PulseLeadSeconds));
            int cue = 0;
            if (remainingRealSeconds <= 10d)
            {
                int second = (int)Math.Ceiling(remainingRealSeconds);
                if (second < lowestCuedSecond)
                {
                    lowestCuedSecond = second;
                    cue = second; // One observed bucket only; skipped seconds never produce a catch-up burst.
                }
            }
            return new PreflightTensionFrame(true,
                !reducedEffects && remainingRealSeconds <= PulseLeadSeconds, urgency, cue);
        }
    }

    internal readonly struct PreflightTensionFrame
    {
        internal readonly bool IsActive;
        internal readonly bool WantsPulse;
        internal readonly float Urgency01;
        internal readonly int CueSecond;

        internal PreflightTensionFrame(bool active, bool pulse, float urgency, int cue)
        {
            IsActive = active;
            WantsPulse = pulse;
            Urgency01 = urgency;
            CueSecond = cue;
        }
    }
}
