using System;

namespace OutOfReach.Presentation.Cinematics
{
    public enum TransportDepartureCue { Boarding, HatchClosing, ArmRetracting, Acceleration, Ascent, Exterior, LaterAboard }
    public enum TransportDepartureEnd { Completed, Skipped, Cancelled }

    /// <summary>Authored presentation, not flight dynamics or elapsed mission time.</summary>
    public static class TransportDepartureTimeline
    {
        public const float DurationSeconds = 22f;
        public const float AscentMetres = 90f;

        public readonly struct Frame
        {
            public readonly float Progress, SeatBlend, HatchDegrees, ArmDegrees, HeightMetres, Shake;
            public readonly TransportDepartureCue Cue;
            public bool Exterior => Cue >= TransportDepartureCue.Exterior;
            internal Frame(float progress, float seat, float hatch, float arm, float height, float shake, TransportDepartureCue cue)
            { Progress = progress; SeatBlend = seat; HatchDegrees = hatch; ArmDegrees = arm; HeightMetres = height; Shake = shake; Cue = cue; }
        }

        public static Frame Evaluate(float activeRealSeconds, bool reducedMotion)
        {
            float t = float.IsNaN(activeRealSeconds) || float.IsInfinity(activeRealSeconds) ? 0f : Clamp(activeRealSeconds, 0f, DurationSeconds);
            float ascent = Clamp((t - 10.5f) / 11.5f, 0f, 1f);
            float vibration = t >= 9f && t < 13.5f ? (float)Math.Pow(Math.Sin(Math.PI * (t - 9f) / 4.5f), 2) : 0f;
            var cue = t < 3f ? TransportDepartureCue.Boarding : t < 6f ? TransportDepartureCue.HatchClosing :
                t < 9f ? TransportDepartureCue.ArmRetracting : t < 10.5f ? TransportDepartureCue.Acceleration :
                t < 18f ? TransportDepartureCue.Ascent : t < 21f ? TransportDepartureCue.Exterior : TransportDepartureCue.LaterAboard;
            return new Frame(t / DurationSeconds, Smooth(t / 3f), 160f * (1f - Smooth((t - 3f) / 3f)),
                -90f * Smooth((t - 6f) / 3f), AscentMetres * ascent * ascent,
                reducedMotion ? 0f : vibration, cue);
        }

        public static string Caption(TransportDepartureCue cue)
        {
            switch (cue)
            {
                case TransportDepartureCue.Boarding: return "Secure for departure.";
                case TransportDepartureCue.HatchClosing: return "Sealing the cabin.";
                case TransportDepartureCue.ArmRetracting: return "Hatch secured. Clearing the access arm.";
                case TransportDepartureCue.Acceleration: return "Ignition.";
                case TransportDepartureCue.Ascent: return "Liftoff.";
                case TransportDepartureCue.Exterior: return string.Empty;
                default: return "LATER · ABOARD AURORA-7";
            }
        }

        private static float Clamp(float v, float min, float max) => Math.Max(min, Math.Min(max, v));
        private static float Smooth(float v) { v = Clamp(v, 0f, 1f); return v * v * (3f - 2f * v); }
    }
}
