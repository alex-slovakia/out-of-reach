using System;

namespace OutOfReach.Presentation.MarineOcean.FX
{
    /// <summary>Pure visual sample of root's supplied 22-second departure playhead. Never advances time.</summary>
    public static class MarineDepartureFxEnvelope
    {
        public const float IgnitionSeconds = 9f;
        public const float LiftoffSeconds = 10.5f;
        public const float EndSeconds = 22f;
        public const int SteamLobes = 4;
        public const float MaximumPlumeLength = 8f;

        public readonly struct Frame
        {
            public readonly float Seconds, Intensity, LengthMetres, RadiusMetres, Motion;
            public bool Visible => Intensity > .0001f;
            internal Frame(float seconds, float intensity, float length, float radius, float motion)
            { Seconds = seconds; Intensity = intensity; LengthMetres = length; RadiusMetres = radius; Motion = motion; }
        }

        public readonly struct SteamFrame
        {
            public readonly float Age, Opacity;
            public bool Visible => Opacity > .0001f;
            internal SteamFrame(float age, float opacity) { Age = age; Opacity = opacity; }
        }

        public static Frame Evaluate(float activeSeconds, bool reducedEffects)
        {
            if (!Finite(activeSeconds) || activeSeconds < 0f || activeSeconds >= EndSeconds) return default;
            float ignition = Smooth((activeSeconds - IgnitionSeconds) / .8f);
            float shutdown = 1f - Smooth((activeSeconds - 21.5f) / .5f);
            float intensity = ignition * shutdown * (reducedEffects ? .65f : 1f);
            float climb = Smooth((activeSeconds - LiftoffSeconds) / 3f);
            return new Frame(activeSeconds, intensity,
                (.85f + (MaximumPlumeLength - .85f) * climb) * (reducedEffects ? .8f : 1f),
                .70f, reducedEffects ? 0f : 1f);
        }

        public static SteamFrame Steam(float activeSeconds, int lobe, bool reducedEffects)
        {
            if (reducedEffects || lobe < 0 || lobe >= SteamLobes || !Finite(activeSeconds)) return default;
            float age = activeSeconds - (9.15f + lobe * .3f);
            if (age < 0 || age >= 3.8f) return default;
            // Four finite ignition puffs, with a soft arrival and a long clear-out. No ongoing emitter.
            float opacity = .13f * Smooth(age / .55f) * (1f - Smooth((age - 1.7f) / 2.1f));
            return new SteamFrame(age, opacity);
        }

        /// <summary>Analytic clipping against root's actual solid deck plane, not a physics/flight model.</summary>
        public static float ClipPlumeToDeck(float requestedLength, float deckDeltaDotNormal, float exhaustDirectionDotNormal)
        {
            if (!Finite(requestedLength) || !Finite(deckDeltaDotNormal) || !Finite(exhaustDirectionDotNormal)
                || requestedLength <= 0 || exhaustDirectionDotNormal >= -.1f) return 0;
            float intersection = deckDeltaDotNormal / exhaustDirectionDotNormal;
            return Math.Max(0f, Math.Min(Math.Min(requestedLength, MaximumPlumeLength), intersection - .15f));
        }

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Smooth(float value)
        { value = Math.Max(0f, Math.Min(1f, value)); return value * value * (3f - 2f * value); }
    }
}
