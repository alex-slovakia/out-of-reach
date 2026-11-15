using System;

namespace OutOfReach.Presentation.Cinematics
{
    public enum TransportDepartureCue
    {
        Boarding, HatchClosing, ArmRetracting, Acceleration, Ascent, Exterior, LaterAboard,
        PadIgnition, UpperAtmosphere, OrbitalApproach, Docked, BoardingAurora, Docking
    }

    public enum TransportDepartureEnd { Completed, Skipped, Cancelled }

    internal enum TransportDepartureShot { Seat, Pad, Climb, UpperAir, Approach, Collar, Transfer }

    /// <summary>Time-compressed artistic presentation. Heights and paths are authored scene coordinates.</summary>
    public static class TransportDepartureTimeline
    {
        public const float IgnitionSeconds = 9f;
        public const float PadCutSeconds = 9.75f;
        public const float LiftoffSeconds = 10.5f;
        public const float ClimbCutSeconds = 13.25f;
        public const float UpperAirSeconds = 17.5f;
        public const float OrbitSeconds = 22f;
        public const float DockingSeconds = 27.5f;
        public const float DockContactSeconds = 29.15f;
        public const float BoardingAuroraSeconds = 30f;
        public const float AboardSeconds = 33.5f;
        public const float DurationSeconds = 34f;
        public const float AscentMetres = 1180f;

        public readonly struct Frame
        {
            public readonly float Progress, SeatBlend, HatchDegrees, ArmDegrees, HeightMetres, Shake;
            public readonly float AtmosphereBlend, Cover, BoardingTravel, PortDistance, ApproachOffset;
            public readonly TransportDepartureCue Cue;
            internal readonly TransportDepartureShot Shot;
            public bool Exterior => Shot != TransportDepartureShot.Seat && Shot != TransportDepartureShot.Transfer;
            internal Frame(float seconds, float height, float shake, float cover, TransportDepartureCue cue, TransportDepartureShot shot)
            {
                Progress = seconds / DurationSeconds;
                SeatBlend = Smooth(seconds / 3f);
                HatchDegrees = 160f * (1f - Smooth((seconds - 3f) / 3f));
                ArmDegrees = -90f * Smooth((seconds - 6f) / 3f);
                HeightMetres = height;
                Shake = shake;
                AtmosphereBlend = Smooth((seconds - ClimbCutSeconds) / (21.55f - ClimbCutSeconds));
                Cover = cover;
                BoardingTravel = Smooth((seconds - 30.25f) / 3.25f);
                PortDistance = seconds < DockingSeconds
                    ? 1.35f + 17.65f * (1f - Smooth((seconds - OrbitSeconds) / (DockingSeconds - OrbitSeconds)))
                    : 1.35f * (1f - Smooth((seconds - DockingSeconds) / (DockContactSeconds - DockingSeconds)));
                ApproachOffset = 3f * (1f - Smooth((seconds - OrbitSeconds) / 4.2f));
                Cue = cue;
                Shot = shot;
            }
        }

        public static Frame Evaluate(float activeRealSeconds, bool reducedMotion) =>
            Evaluate(activeRealSeconds, reducedMotion, true);

        internal static Frame Evaluate(float activeRealSeconds, bool reducedMotion, bool hasOrbitalViews)
        {
            float duration = hasOrbitalViews ? DurationSeconds : OrbitSeconds;
            float t = float.IsNaN(activeRealSeconds) || float.IsInfinity(activeRealSeconds) ? 0f : Clamp(activeRealSeconds, 0f, duration);
            float height = t < ClimbCutSeconds
                ? Rise(t, LiftoffSeconds, ClimbCutSeconds, 0f, 23f, 0f, 15f)
                : t < UpperAirSeconds
                    ? Rise(t, ClimbCutSeconds, UpperAirSeconds, 23f, 175f, 15f, 65f)
                    : Rise(t, UpperAirSeconds, OrbitSeconds, 175f, AscentMetres, 65f, 350f);

            float ignition = Smooth((t - IgnitionSeconds) / .28f) * (1f - Smooth((t - 10.85f) / 3f));
            float vibration = reducedMotion ? 0f : ignition;
            float upperCover = CutCover(t, UpperAirSeconds, .22f, .30f);
            float orbitCover = hasOrbitalViews ? CutCover(t, OrbitSeconds, .48f, .45f)
                : Smooth((t - (OrbitSeconds - .48f)) / .48f);
            float boardingCover = hasOrbitalViews ? CutCover(t, BoardingAuroraSeconds, .42f, .50f) : 0f;
            float handoffCover = hasOrbitalViews ? Smooth((t - 33f) / .5f) : 0f;
            float cover = Math.Max(Math.Max(upperCover, orbitCover), Math.Max(boardingCover, handoffCover));

            if (t < 3f) return new Frame(t, height, vibration, cover, TransportDepartureCue.Boarding, TransportDepartureShot.Seat);
            if (t < 6f) return new Frame(t, height, vibration, cover, TransportDepartureCue.HatchClosing, TransportDepartureShot.Seat);
            if (t < IgnitionSeconds) return new Frame(t, height, vibration, cover, TransportDepartureCue.ArmRetracting, TransportDepartureShot.Seat);
            if (t < PadCutSeconds) return new Frame(t, height, vibration, cover, TransportDepartureCue.Acceleration, TransportDepartureShot.Seat);
            if (t < LiftoffSeconds) return new Frame(t, height, vibration, cover, TransportDepartureCue.PadIgnition, TransportDepartureShot.Pad);
            if (t < ClimbCutSeconds) return new Frame(t, height, vibration, cover, TransportDepartureCue.Ascent, TransportDepartureShot.Pad);
            if (t < UpperAirSeconds) return new Frame(t, height, vibration, cover, TransportDepartureCue.Exterior, TransportDepartureShot.Climb);
            if (t < OrbitSeconds || !hasOrbitalViews) return new Frame(t, height, vibration, cover, TransportDepartureCue.UpperAtmosphere, TransportDepartureShot.UpperAir);
            if (t < DockingSeconds) return new Frame(t, height, vibration, cover, TransportDepartureCue.OrbitalApproach, TransportDepartureShot.Approach);
            if (t < BoardingAuroraSeconds) return new Frame(t, height, vibration, cover,
                t < DockContactSeconds ? TransportDepartureCue.Docking : TransportDepartureCue.Docked, TransportDepartureShot.Collar);
            return new Frame(t, height, vibration, cover, t < AboardSeconds ? TransportDepartureCue.BoardingAurora : TransportDepartureCue.LaterAboard,
                TransportDepartureShot.Transfer);
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
                case TransportDepartureCue.UpperAtmosphere: return "UPPER ATMOSPHERE · FLIGHT CONDENSED";
                case TransportDepartureCue.OrbitalApproach: return "LATER IN EARTH ORBIT · AURORA-7 RENDEZVOUS";
                case TransportDepartureCue.Docked: return "DOCKING COLLARS MATED";
                case TransportDepartureCue.BoardingAurora: return "CREW TRANSFER · AURORA-7";
                case TransportDepartureCue.LaterAboard: return "LATER · ABOARD AURORA-7";
                default: return string.Empty;
            }
        }

        private static float CutCover(float t, float cut, float fadeOut, float fadeIn) =>
            t <= cut ? Smooth((t - (cut - fadeOut)) / fadeOut) : 1f - Smooth((t - cut) / fadeIn);

        private static float Rise(float t, float start, float end, float from, float to, float startRate, float endRate)
        {
            float duration = end - start;
            float u = Clamp((t - start) / duration, 0f, 1f);
            return from + (to - from) * Smooth(u)
                + duration * u * (1f - u) * (startRate * (1f - u) - endRate * u);
        }

        private static float Clamp(float v, float min, float max) => Math.Max(min, Math.Min(max, v));
        private static float Smooth(float v) { v = Clamp(v, 0f, 1f); return v * v * (3f - 2f * v); }
    }
}
