using System;

namespace OutOfReach.Presentation.MarineOcean.FX
{
    /// <summary>Pure visual sample of root's supplied launch playhead. Never advances time.</summary>
    public static class MarineDepartureFxEnvelope
    {
        public const float IgnitionSeconds = 9f;
        public const float LiftoffSeconds = 10.5f;
        public const float EndSeconds = 22f;
        public const int SteamLobes = 40;
        public const float MaximumPlumeLength = 31f;

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
            float ignition = Smooth((activeSeconds - IgnitionSeconds) / .35f);
            float shutdown = 1f - Smooth((activeSeconds - 21.15f) / .65f);
            float intensity = ignition * shutdown * (reducedEffects ? .74f : 1f);
            float climb = Smooth((activeSeconds - LiftoffSeconds) / 3.6f);
            float length = 8f + (MaximumPlumeLength - 8f) * climb;
            return new Frame(activeSeconds, intensity, length * (reducedEffects ? .8f : 1f),
                1.16f + .40f * climb, reducedEffects ? 0f : 1f);
        }

        public static SteamFrame Steam(float activeSeconds, int lobe, bool reducedEffects)
        {
            if (reducedEffects || lobe < 0 || lobe >= SteamLobes || !Finite(activeSeconds)) return default;
            int wave = lobe / 8;
            float age = activeSeconds - (9.03f + (wave == 0 ? 0f : .12f + (wave - 1) * .42f) + (lobe % 8) * .045f);
            float lifetime = wave == 0 ? 6.8f : 10.0f;
            if (age < 0 || age >= lifetime) return default;
            // Dust settles early; the heavier directed steam cloud remains at the stationary trench.
            float opacity = (wave == 0 ? .62f : .92f) * Smooth(age / .45f)
                * (1f - Smooth((age - (wave == 0 ? 2.8f : 5f)) / (wave == 0 ? 4f : 5f)));
            return new SteamFrame(age, opacity);
        }

        /// <summary>Visible dust front driven by acoustic onset; stylized dust travel, not sound propagation speed.</summary>
        public static float PressureOpacity(float seconds,bool reducedEffects)
        {
            if(reducedEffects||!Finite(seconds))return 0;
            float age=seconds-LiftoffSeconds;if(age<0||age>=3.4f)return 0;
            return .63f*Smooth(age/.13f)*(1f-Smooth((age-.6f)/2.8f));
        }
        public static float DelugeOpacity(float seconds,bool reducedEffects)
        {
            if(!Finite(seconds))return 0;
            return (reducedEffects?.13f:.88f)*Smooth((seconds-8.8f)/.5f)*(1f-Smooth((seconds-16f)/1.5f));
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
