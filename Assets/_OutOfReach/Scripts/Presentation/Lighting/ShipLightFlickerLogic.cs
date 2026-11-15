using System;

namespace OutOfReach.Presentation.Lighting
{
    /// <summary>
    /// Pure, deterministic brightness multipliers for damaged-power ship lighting (R28). Everything is
    /// deliberately slow and shallow: no value changes faster than a brownout every 1.25 s and no pulse
    /// is above 0.9 Hz, well below the three-flashes-per-second photosensitivity guideline. The presenter
    /// blends these toward 1 and bypasses them entirely when reduced effects is on.
    /// </summary>
    public static class ShipLightFlickerLogic
    {
        /// <summary>Brownout windows per second for working lights.</summary>
        public const float BrownoutRate = 0.8f;
        public const float BrownoutDepth = 0.60f;
        public const float EmergencyDepth = 0.20f;
        public const float EmergencyPeriodSeconds = 2.6f;
        public const float IndicatorDepth = 0.45f;
        public const float IndicatorHz = 0.9f;
        public const float MinWorking = 1f - BrownoutDepth - ShimmerDepth;
        public const float MaxWorking = 1f + ShimmerDepth;
        private const float ShimmerDepth = 0.05f;
        private const float BrownoutChance = 0.22f;

        /// <summary>Multiplier for a working fixture sputtering on failing power: mostly steady with rare, soft dips.</summary>
        public static float Working(float seconds, float seed)
        {
            seconds = Finite(seconds);
            seed = Finite(seed);
            float shimmer = ShimmerDepth * (0.6f * (float)Math.Sin(seconds * 8.1f + seed * 6.3f)
                                          + 0.4f * (float)Math.Sin(seconds * 3.7f + seed * 2.1f));
            float cursor = seconds * BrownoutRate + seed;
            float cell = (float)Math.Floor(cursor);
            float dip = 0f;
            if (Hash01(cell) > 1f - BrownoutChance)
            {
                // The dip occupies the first third of its window and rises and falls on a half sine.
                float phase = (cursor - cell) * 3f;
                if (phase < 1f) dip = (float)Math.Sin(phase * Math.PI);
            }
            return 1f + shimmer - BrownoutDepth * dip;
        }

        /// <summary>Slow beacon swell for emergency lights; never dimmer than 1 - EmergencyDepth.</summary>
        public static float Emergency(float seconds)
        {
            float wave = 0.5f + 0.5f * (float)Math.Sin(Finite(seconds) * (2f * (float)Math.PI / EmergencyPeriodSeconds));
            return 1f - EmergencyDepth * (1f - wave);
        }

        /// <summary>Breathing of status-LED emission; never dimmer than 1 - IndicatorDepth.</summary>
        public static float Indicator(float seconds, float seed)
        {
            float wave = 0.5f + 0.5f * (float)Math.Sin(Finite(seconds) * (2f * (float)Math.PI * IndicatorHz) + Finite(seed) * 6.3f);
            return 1f - IndicatorDepth * (1f - wave);
        }

        /// <summary>Blend a flicker multiplier toward steady 1. Zero amount (reduced effects, healthy power) returns exactly 1.</summary>
        public static float Blend(float multiplier, float amount)
        {
            amount = float.IsNaN(amount) ? 0f : Math.Min(Math.Max(amount, 0f), 1f);
            return amount <= 0f || float.IsNaN(multiplier) || float.IsInfinity(multiplier) ? 1f : 1f + (multiplier - 1f) * amount;
        }

        private static float Hash01(float cell)
        {
            double x = Math.Sin(cell * 12.9898 + 78.233) * 43758.5453;
            return (float)(x - Math.Floor(x));
        }

        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }
}
