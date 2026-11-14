using System;
using System.Globalization;
using OutOfReach.Contracts;

namespace OutOfReach.Presentation.AirlockPanel
{
    public enum AirlockPanelTone { Neutral, Cycling, Ready, Unavailable }

    /// <summary>Formatting only. Reads the authoritative state and remaining simulation seconds.</summary>
    public readonly struct AirlockPanelReadout
    {
        public readonly string Status, TimerCaption, InnerSeal, OuterSeal, Detail;
        public readonly int CountdownSeconds;
        public readonly AirlockPanelTone Tone;
        public readonly bool InnerOpen, OuterOpen, HasState;

        private AirlockPanelReadout(string status, string caption, string inner, string outer, string detail,
            int seconds, AirlockPanelTone tone, bool innerOpen, bool outerOpen, bool hasState)
        {
            Status = status; TimerCaption = caption; InnerSeal = inner; OuterSeal = outer; Detail = detail;
            CountdownSeconds = seconds; Tone = tone; InnerOpen = innerOpen; OuterOpen = outerOpen; HasState = hasState;
        }

        public static AirlockPanelReadout Read(IEvaStatus eva, ISimulationClock clock)
        {
            if (eva == null) return Unknown();
            AirlockState state = eva.Airlock;
            string status, detail;
            AirlockPanelTone tone;
            switch (state)
            {
                case AirlockState.InnerOpen: status = "INNER OPEN"; detail = "CABIN ACCESS"; tone = AirlockPanelTone.Neutral; break;
                case AirlockState.Sealed: status = "SEALED"; detail = "BOTH HATCHES SEALED"; tone = AirlockPanelTone.Neutral; break;
                case AirlockState.Depressurizing: status = "DEPRESSURIZING"; detail = "BOTH HATCHES SEALED"; tone = AirlockPanelTone.Cycling; break;
                case AirlockState.OuterOpen: status = "SAFE TO EXIT"; detail = "OUTER HATCH OPEN"; tone = AirlockPanelTone.Ready; break;
                case AirlockState.Repressurizing: status = "REPRESSURIZING"; detail = "BOTH HATCHES SEALED"; tone = AirlockPanelTone.Cycling; break;
                default: return Unknown();
            }
            int seconds = -1;
            string caption = "NO ACTIVE CYCLE";
            if (tone == AirlockPanelTone.Cycling)
            {
                double remaining = eva.AirlockTransitionRemainingSeconds;
                if (double.IsNaN(remaining) || double.IsInfinity(remaining) || remaining < 0)
                    caption = "SIM TIME UNAVAILABLE";
                else if (remaining > 5999)
                    caption = "OVER 99:59 SIM TIME";
                else
                {
                    seconds = (int)Math.Ceiling(remaining);
                    caption = "SIM TIME REMAINING";
                }
            }
            if (clock != null)
            {
                if (clock.IsPaused) caption += "  /  PAUSED";
                else if (!double.IsNaN(clock.TimeScale) && !double.IsInfinity(clock.TimeScale) && clock.TimeScale > 1)
                    caption += "  /  x" + clock.TimeScale.ToString("0.##", CultureInfo.InvariantCulture);
            }
            bool innerOpen = state == AirlockState.InnerOpen, outerOpen = state == AirlockState.OuterOpen;
            return new AirlockPanelReadout(status, caption, innerOpen ? "OPEN" : "SEALED",
                outerOpen ? "OPEN" : "SEALED", detail, seconds, tone, innerOpen, outerOpen, true);
        }

        private static AirlockPanelReadout Unknown() => new AirlockPanelReadout("NO SIGNAL", "SIM TIME UNAVAILABLE",
            "UNKNOWN", "UNKNOWN", "STATE NOT CONNECTED", -1, AirlockPanelTone.Unavailable, false, false, false);
    }
}
