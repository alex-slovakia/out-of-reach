using System;

namespace OutOfReach.Presentation.Rescue
{
    public enum RescueShot { Window, DockingCamera, Hatch }

    /// <summary>One subtitle line. Times are presentation seconds from the start of the ending.</summary>
    public readonly struct RescueLine
    {
        public RescueLine(float start, float end, string speaker, string text)
        { Start = start; End = end; Speaker = speaker; Text = text; }
        public float Start { get; }
        public float End { get; }
        public string Speaker { get; }
        public string Text { get; }
    }

    /// <summary>
    /// Pure timing of the rescue ending (RESCUE-ENDING-01, backlog R42). No Unity objects, clock or mission state:
    /// the presenter owns the single playhead (real seconds, frozen while the authoritative clock is paused) and
    /// samples these curves. Voice timing follows the Alex-approved Story02 masters: VO24a (10.28 s) starts with the
    /// approach; VO24b (12.16 s) starts only after the visible docking contact. Subtitle chunks follow the measured
    /// speech segments of those exact files (20 ms RMS envelope).
    /// </summary>
    public static class RescueEndingTimeline
    {
        public const float Duration = 37.5f;
        public const string StatusSpeaker = "SHIP STATUS";
        public const string RadioSpeaker = "RESCUE CREW · RADIO";
        public const string DockingCameraSpeaker = "AFT HULL CAMERA";

        // Opening: a short dip and cut to the cabin window (never fly the real camera through bulkheads) or,
        // when the eye already floats near that window, a short glide.
        public const float OpeningCut = .55f, OpeningClear = 1.35f, GlideEnd = 2.4f, GlideMaximumMetres = 3.2f;
        // Shot 1, the window: far → near with braking burns, floodlight, then the craft slides aside and aft.
        public const float ApproachStart = .4f, ApproachEnd = 10.2f, AsideEnd = 13.6f, FloodOn = 4.4f;
        public const float VoiceApproachStart = 1.6f;   // VO24a
        // Shot 2, the aft docking camera: centreline closing, visible contact, capture, hard dock.
        public const float CutOut1 = 13.4f, Cut1 = 14.1f, CutIn1 = 14.8f;
        public const float DockApproachStart = 14.1f, Contact = 19.4f, Capture = 21.35f, HardDockStart = 22.9f, HardDock = 23.25f;
        public const float VoiceDockedStart = 19.75f;   // VO24b, only after the visible contact
        // Shot 3, inside the airlock: latches, pressure, the hatch opens and light floods in, fade.
        public const float CutOut2 = 24.2f, Cut2 = 24.9f, CutIn2 = 25.6f;
        public static readonly float[] LatchTimes = { 25.9f, 26.4f, 26.9f };
        public const float EqualizeStart = 25.4f, EqualizeEnd = 28.4f;
        public const float SeamGlow = 27.9f, HatchOpenStart = 28.9f, HatchOpenEnd = 31.8f;
        public const float FadeOutStart = 33.6f, FadeOutEnd = 36.4f;

        public const float VoiceApproachSeconds = 10.28f, VoiceDockedSeconds = 12.16f;

        private static readonly (float start, float end, float strength)[] RetroBurns =
            { (3.9f, 5.2f, 1f), (6.9f, 7.9f, .85f), (9.0f, 9.7f, .6f) };
        private static readonly (float start, float end)[] SidePuffs =
            { (10.5f, 11.0f), (12.3f, 12.8f), (15.6f, 16.0f), (17.6f, 17.95f) };

        /// <summary>Subtitles in order. Radio chunks follow VO24a/VO24b word for word.</summary>
        public static readonly RescueLine[] Lines =
        {
            new RescueLine(.5f, 1.8f, StatusSpeaker, "Recovery craft on approach. Hold Space to skip."),
            new RescueLine(VoiceApproachStart + .25f, VoiceApproachStart + 2.9f, RadioSpeaker, "Aurora Seven, rescue crew."),
            new RescueLine(VoiceApproachStart + 2.9f, VoiceApproachStart + 4.85f, RadioSpeaker, "We have you in sight."),
            new RescueLine(VoiceApproachStart + 4.85f, VoiceApproachStart + 7.0f, RadioSpeaker, "You've held on long enough."),
            new RescueLine(VoiceApproachStart + 7.0f, VoiceApproachStart + 8.85f, RadioSpeaker, "We're coming alongside."),
            new RescueLine(VoiceApproachStart + 8.85f, VoiceApproachStart + 10.6f, RadioSpeaker, "Stand by for docking."),
            new RescueLine(Cut1 + .2f, VoiceDockedStart + .25f, DockingCameraSpeaker, "Docking port in view."),
            new RescueLine(VoiceDockedStart + .25f, VoiceDockedStart + 1.6f, RadioSpeaker, "Contact."),
            new RescueLine(VoiceDockedStart + 1.6f, VoiceDockedStart + 3.45f, RadioSpeaker, "Capture confirmed."),
            new RescueLine(VoiceDockedStart + 3.45f, VoiceDockedStart + 5.0f, RadioSpeaker, "Hard dock."),
            new RescueLine(VoiceDockedStart + 5.0f, VoiceDockedStart + 8.9f, RadioSpeaker, "Stay clear while we equalize the hatch."),
            new RescueLine(VoiceDockedStart + 8.9f, VoiceDockedStart + 12.4f, RadioSpeaker, "Aurora Seven... you're not alone anymore."),
            new RescueLine(34.0f, Duration, StatusSpeaker, "RESCUED"),
        };

        public static float Clamp(float t) => float.IsNaN(t) ? 0f : Math.Max(0f, Math.Min(Duration, t));

        public static RescueShot ShotAt(float t)
        {
            t = Clamp(t);
            return t < Cut1 ? RescueShot.Window : t < Cut2 ? RescueShot.DockingCamera : RescueShot.Hatch;
        }

        /// <summary>0 at the start pose, 1 at the window pose.</summary>
        public static float WindowBlend(float t, bool glide)
        {
            t = Clamp(t);
            if (!glide) return t >= OpeningCut ? 1f : 0f;
            return Smooth(InverseLerp(0f, GlideEnd, t));
        }

        /// <summary>Far → near, decelerating to rest (cubic ease-out = braking), 0..1.</summary>
        public static float Approach(float t)
        {
            float r = 1f - InverseLerp(ApproachStart, ApproachEnd, Clamp(t));
            return 1f - r * r * r;
        }

        /// <summary>Near → aside, the translation out of the window towards the aft hatch, 0..1.</summary>
        public static float Aside(float t) => Smooth(InverseLerp(ApproachEnd, AsideEnd, Clamp(t)));

        /// <summary>Centreline closing on the docking camera: start → contact, slowing to a gentle touch, 0..1.</summary>
        public static float DockClosing(float t)
        {
            float u = InverseLerp(DockApproachStart, Contact, Clamp(t));
            float r = 1f - u;
            return 1f - r * r;
        }

        /// <summary>Last centimetres pulled in by the hard-dock latches, 0..1.</summary>
        public static float HardDockRetract(float t) => Smooth(InverseLerp(HardDockStart, HardDock, Clamp(t)));

        public static bool Captured(float t) => Clamp(t) >= Capture;

        public static float RetroBurn(float t)
        {
            t = Clamp(t);
            float value = 0f;
            foreach (var burn in RetroBurns) value = Math.Max(value, Pulse(t, burn.start, burn.end, .18f) * burn.strength);
            return value;
        }

        public static float SidePuff(float t)
        {
            t = Clamp(t);
            float value = 0f;
            foreach (var puff in SidePuffs) value = Math.Max(value, Pulse(t, puff.start, puff.end, .08f));
            return value;
        }

        /// <summary>The craft's floodlight, on from the approach until the hatch shot, 0..1.</summary>
        public static float Floodlight(float t)
        {
            t = Clamp(t);
            if (t >= Cut2) return 0f;
            return Smooth(InverseLerp(FloodOn, FloodOn + 1.1f, t));
        }

        /// <summary>Black overlay 0..1: opening dip (cut mode only), the two cuts, the final fade.</summary>
        public static float Blackout(float t, bool openingCut)
        {
            t = Clamp(t);
            float value = 0f;
            if (openingCut) value = Math.Max(value, t < OpeningCut ? Smooth(InverseLerp(0f, OpeningCut, t)) : 1f - Smooth(InverseLerp(OpeningCut, OpeningClear, t)));
            value = Math.Max(value, t < Cut1 ? Smooth(InverseLerp(CutOut1, Cut1, t)) : 1f - Smooth(InverseLerp(Cut1, CutIn1, t)));
            value = Math.Max(value, t < Cut2 ? Smooth(InverseLerp(CutOut2, Cut2, t)) : 1f - Smooth(InverseLerp(Cut2, CutIn2, t)));
            value = Math.Max(value, Smooth(InverseLerp(FadeOutStart, FadeOutEnd, t)));
            return value;
        }

        /// <summary>Shake envelope 0..1: contact, hard dock, latch knocks. Zero with reduced effects.</summary>
        public static float Shake(float t, bool reducedEffects)
        {
            if (reducedEffects) return 0f;
            t = Clamp(t);
            float value = 0f;
            if (t >= Contact) value = Math.Max(value, (float)Math.Exp(-(t - Contact) * 6f) * .4f);
            if (t >= HardDock) value = Math.Max(value, (float)Math.Exp(-(t - HardDock) * 3.1f));
            foreach (float latch in LatchTimes)
                if (t >= latch) value = Math.Max(value, (float)Math.Exp(-(t - latch) * 9f) * .22f);
            return value;
        }

        public static int LatchesClosed(float t)
        {
            t = Clamp(t);
            int count = 0;
            foreach (float latch in LatchTimes) if (t >= latch) count++;
            return count;
        }

        public static float Seam(float t) => Smooth(InverseLerp(SeamGlow, SeamGlow + .8f, Clamp(t)));

        public static float HatchOpen(float t) => Smooth(InverseLerp(HatchOpenStart, HatchOpenEnd, Clamp(t)));

        public static float HatchPush(float t, bool reducedEffects)
            => reducedEffects ? 0f : Smooth(InverseLerp(HatchOpenStart, FadeOutEnd, Clamp(t)));

        public static int LineAt(float t)
        {
            t = Clamp(t);
            for (int i = 0; i < Lines.Length; i++) if (t >= Lines[i].Start && t < Lines[i].End) return i;
            return -1;
        }

        public static float InverseLerp(float a, float b, float v)
        {
            if (b <= a) return v >= b ? 1f : 0f;
            return Math.Max(0f, Math.Min(1f, (v - a) / (b - a)));
        }

        public static float Smooth(float x) => x * x * (3f - 2f * x);

        private static float Pulse(float t, float start, float end, float edge)
        {
            if (t <= start || t >= end) return 0f;
            return Math.Min(Smooth(InverseLerp(start, start + edge, t)), 1f - Smooth(InverseLerp(end - edge, end, t)));
        }
    }
}
