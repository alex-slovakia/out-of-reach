using UnityEngine;

namespace OutOfReach.Presentation.Radio
{
    /// <summary>Original procedural radio sounds (no recordings, no downloads), fixed seeds so every run is identical.</summary>
    public static class RadioSynth
    {
        public const int Rate = 44100;

        /// <summary>Two-tone incoming-call chirp, twice.</summary>
        public static AudioClip IncomingChirp()
        {
            int n = (int)(Rate * .62f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float local = t % .31f;
                float f = local < .12f ? 1480f : (local < .24f ? 1975f : 0f);
                float env = f > 0f ? Mathf.Clamp01(Mathf.Min(local % .12f, .12f - local % .12f) * 120f) : 0f;
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * .32f * env;
            }
            return Make("radio-incoming-chirp", data);
        }

        /// <summary>Key-up: a short click and a rising blip, as the PTT closes or a carrier opens.</summary>
        public static AudioClip KeyUp()
        {
            int n = (int)(Rate * .11f);
            var data = new float[n];
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float click = t < .006f ? (float)(rng.NextDouble() * 2 - 1) * (1f - t / .006f) * .6f : 0f;
                float blip = t > .02f ? Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1100f, 1650f, (t - .02f) / .09f) * t) * .18f * (1f - (t - .02f) / .09f) : 0f;
                data[i] = click + blip;
            }
            return Make("radio-key-up", data);
        }

        /// <summary>Squelch tail when a transmission ends: a short burst of band-limited noise with a fast decay.</summary>
        public static AudioClip SquelchTail()
        {
            int n = (int)(Rate * .2f);
            var data = new float[n];
            var rng = new System.Random(11);
            float low = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float white = (float)(rng.NextDouble() * 2 - 1);
                low += .22f * (white - low);               // one-pole low-pass
                float band = low - prev; prev = low;          // crude high-pass → radio band
                data[i] = (band * 3.2f + low * .5f) * Mathf.Exp(-t * 22f) * .55f;
            }
            return Make("radio-squelch-tail", data);
        }

        /// <summary>Loopable static bed with sparse crackles, played quietly under every line.</summary>
        public static AudioClip StaticBed()
        {
            int n = Rate * 2;
            var data = new float[n];
            var rng = new System.Random(23);
            float low = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)(rng.NextDouble() * 2 - 1);
                low += .35f * (white - low);
                float band = low - prev; prev = low;
                float crackle = rng.NextDouble() < .0009 ? (float)(rng.NextDouble() * 2 - 1) * 2.5f : 0f;
                data[i] = (band * 1.6f + low * .35f) * .32f + crackle * .25f;
            }
            // Crossfade the ends so the loop has no click.
            int fade = Rate / 20;
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                data[i] = data[i] * w + data[n - fade + i] * (1f - w);
            }
            return Make("radio-static-bed", data);
        }

        private static AudioClip Make(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
