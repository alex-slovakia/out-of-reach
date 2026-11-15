using System;
using UnityEngine;

namespace OutOfReach.Presentation.Rescue
{
    /// <summary>
    /// Original deterministic synthesis (oscillators + seeded noise) for the rescue ending: radio squelch, the
    /// docking contact and hard-dock clunks and latch knocks carried through the structure, and the pressure hiss
    /// heard inside the airlock. No samples, no external library. Space itself stays silent: thrusters make no sound.
    /// </summary>
    public static class RescueEndingSfx
    {
        public const int Rate = 24000;

        public static AudioClip Squelch() => Make("rescue-radio-squelch", .16f, (t, noise) =>
            (float)(Env(t, .004f, .16f) * (.28f * noise + .22f * Math.Sin(2 * Math.PI * 1150 * t) * Math.Exp(-t * 60))));

        public static AudioClip Clunk(bool hard) => Make(hard ? "rescue-hard-dock-clunk" : "rescue-contact-clunk", hard ? 1.6f : .9f, (t, noise) =>
        {
            double gain = hard ? 1.0 : .55;
            double thump = Math.Exp(-t * (hard ? 4.6 : 7.0)) * Math.Sin(2 * Math.PI * (34 + 36 * Math.Exp(-t * 9)) * t);
            double ring = Math.Exp(-t * (hard ? 2.8 : 5.0)) * (Math.Sin(2 * Math.PI * 183 * t) + .6 * Math.Sin(2 * Math.PI * 417 * t) + .35 * Math.Sin(2 * Math.PI * 691 * t));
            double hit = Math.Exp(-t * 55) * noise;
            return (float)(gain * (.9 * thump + .22 * ring + .45 * hit));
        });

        public static AudioClip Latch() => Make("rescue-latch-knock", .3f, (t, noise) =>
            (float)(.5 * Math.Exp(-t * 38) * (Math.Sin(2 * Math.PI * 950 * t) + .5 * Math.Sin(2 * Math.PI * 1730 * t))
                + .45 * Math.Exp(-t * 22) * Math.Sin(2 * Math.PI * 88 * t) + .3 * Math.Exp(-t * 90) * noise));

        public static AudioClip Hiss(float seconds) => Make("rescue-pressure-hiss", seconds, (t, noise) =>
            (float)(.22 * Env(t, .35f, seconds) * noise));

        private static double Env(double t, float attack, float length)
        {
            double a = Math.Min(1.0, t / Math.Max(.001, attack));
            double r = Math.Min(1.0, Math.Max(0.0, (length - t) / Math.Max(.001, length * .45)));
            return a * r;
        }

        private static AudioClip Make(string name, float seconds, Func<double, float, float> sample)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
            var data = new float[count];
            var random = new System.Random(name.Length * 7919 + count);
            float low = 0f, peak = 1e-6f;
            for (int i = 0; i < count; i++)
            {
                // Gently low-passed white noise: less fizz, more body.
                low += ((float)(random.NextDouble() * 2 - 1) - low) * .35f;
                data[i] = sample(i / (double)Rate, low * 2.2f);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            float scale = peak > .92f ? .92f / peak : 1f;
            for (int i = 0; i < count; i++) data[i] *= scale;
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.DontSave;
            return clip;
        }
    }
}
