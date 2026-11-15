using System;
using UnityEngine;

namespace OutOfReach.Presentation.UI.Startup
{
    /// <summary>
    /// Original, fully procedural pencil-on-paper scratch (no recorded or downloaded audio). The sound is rendered from
    /// the SAME schedule as the picture: for every sample it asks where the pen is (sketch progress, active shape, pen
    /// speed) and shapes band-passed noise accordingly, so every stroke is a scratch with a pressure envelope and the
    /// pen lifts are silent. After the last stroke a softer, wider "rub" follows the colour fill.
    /// Signal path: white noise -> state-variable band-pass (centre 2.2-5.3 kHz rising with pen speed, Q 1.8) ->
    /// amplitude by pressure envelope (attack 8 % / release 15 % of the stroke), pen speed, and paper grain
    /// (|low-passed noise|, ~120 Hz bursts) -> one-pole high-pass 600 Hz -> soft clip -> normalise.
    /// Deterministic for a given art, timing and seed. See PENCIL-SOUND-PROVENANCE.md.
    /// </summary>
    public static class PencilScratchSynth
    {
        public const int SampleRate = 44100;
        public const int DefaultSeed = 20261114;
        private const float TailSeconds = .12f;

        /// <summary>Mono samples covering the sketch, the fill and a short tail. Empty when there is nothing to draw.</summary>
        public static float[] Render(VectorArt art, StartupTiming timing, int seed = DefaultSeed)
        {
            if (art == null || art.IsEmpty || timing.DrawSeconds <= 0f) return new float[0];
            float duration = timing.DrawSeconds + timing.FillSeconds + TailSeconds;
            int count = Mathf.CeilToInt(duration * SampleRate);
            var samples = new float[count];
            var rng = new System.Random(seed);
            var shapes = art.Shapes;

            // filter and envelope state
            float low = 0f, band = 0f, fillLow = 0f, fillBand = 0f;
            float grain = 0f, grainRub = 0f, gain = 0f;
            float hpPrevIn = 0f, hpPrevOut = 0f;
            const float hpCoef = .918f;                      // ~600 Hz at 44.1 kHz
            float grainCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 120f / SampleRate);
            float gainCoef = 1f - Mathf.Exp(-1f / (.003f * SampleRate));
            int active = 0, lastShape = -1;
            float tickEnergy = 0f;
            float peak = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float n1 = (float)(rng.NextDouble() * 2.0 - 1.0);
                float n2 = (float)(rng.NextDouble() * 2.0 - 1.0);
                float n3 = (float)(rng.NextDouble() * 2.0 - 1.0);
                grain += grainCoef * (n2 - grain);
                grainRub += grainCoef * .5f * (n3 - grainRub);

                float target = 0f, centre = 3000f;
                float sketch = 0f, speed = 0f;
                if (t < timing.DrawSeconds)
                {
                    float x = t / timing.DrawSeconds;
                    sketch = StartupSequence.EaseDraw(x);
                    float eps = .004f;
                    float dp = (StartupSequence.EaseDraw(Mathf.Min(1f, x + eps)) - StartupSequence.EaseDraw(Mathf.Max(0f, x - eps)))
                        / (Mathf.Min(1f, x + eps) - Mathf.Max(0f, x - eps)) / timing.DrawSeconds;
                    while (active < shapes.Count && sketch > shapes[active].RevealEnd) active++;
                    if (active < shapes.Count && sketch >= shapes[active].RevealStart)
                    {
                        var shape = shapes[active];
                        float span = Mathf.Max(.0001f, shape.RevealEnd - shape.RevealStart);
                        float u = Mathf.Clamp01((sketch - shape.RevealStart) / span);
                        speed = dp / span * shape.Length;
                        float pressure = Smooth(u / .08f) * Smooth((1f - u) / .15f);
                        float variation = .8f + .2f * Mathf.Sin(2f * Mathf.PI * 7f * t + active * 1.7f);
                        float perStroke = .72f + .28f * Hash01(active);
                        float speedGain = .35f + .65f * Mathf.Clamp01(speed / 1.2f);
                        target = pressure * variation * perStroke * speedGain;
                        centre = 2200f + 2600f * Mathf.Clamp01(speed / 1.5f) + 500f * pressure;
                        if (active != lastShape) { lastShape = active; tickEnergy = .35f + .25f * Hash01(active + 91); }
                    }
                }
                gain += gainCoef * (target - gain);

                // band-pass noise (Chamberlin state-variable filter)
                float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Clamp(centre, 400f, 9000f) / SampleRate);
                float high = n1 - low - band / 1.8f;
                band += f * high;
                low += f * band;
                float am = .3f + .7f * Mathf.Min(1.5f, Mathf.Abs(grain) * 18f);
                float scratch = band * .55f * am * gain;

                // the small tick as the pencil lands
                float tick = 0f;
                if (tickEnergy > .001f) { tick = n1 * tickEnergy; tickEnergy *= .985f; }

                // soft rub while the colour fills
                float rub = 0f;
                if (t >= timing.DrawSeconds && timing.FillSeconds > 0f)
                {
                    float q = Mathf.Clamp01((t - timing.DrawSeconds) / timing.FillSeconds);
                    float bell = Mathf.Sin(Mathf.PI * q);
                    bell *= bell;
                    float swipes = .6f + .4f * Mathf.Cos(2f * Mathf.PI * 2.7f * q);
                    float ff = 2f * Mathf.Sin(Mathf.PI * 1800f / SampleRate);
                    float fh = n3 - fillLow - fillBand / .9f;
                    fillBand += ff * fh;
                    fillLow += ff * fillBand;
                    rub = fillBand * .3f * bell * swipes * (.4f + .6f * Mathf.Min(1.5f, Mathf.Abs(grainRub) * 28f));
                }

                float mix = scratch + tick * .5f + rub;
                float hp = hpCoef * (hpPrevOut + mix - hpPrevIn);
                hpPrevIn = mix; hpPrevOut = hp;
                float clipped = (float)Math.Tanh(hp * 1.6f);
                samples[i] = clipped;
                peak = Mathf.Max(peak, Mathf.Abs(clipped));
            }

            float norm = peak > .0001f ? .9f / peak : 1f;
            int fade = Mathf.Min(count, Mathf.CeilToInt(.02f * SampleRate));
            for (int i = 0; i < count; i++)
            {
                samples[i] *= norm;
                if (i >= count - fade) samples[i] *= (count - i) / (float)fade;
            }
            return samples;
        }

        /// <summary>Linear 0..1 smoothstep.</summary>
        private static float Smooth(float value)
        {
            float x = Mathf.Clamp01(value);
            return x * x * (3f - 2f * x);
        }

        /// <summary>Deterministic 0..1 hash of an integer (stroke index).</summary>
        private static float Hash01(int n)
        {
            unchecked
            {
                uint h = (uint)n * 2654435761u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}
