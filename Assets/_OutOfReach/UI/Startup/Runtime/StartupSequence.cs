using UnityEngine;

namespace OutOfReach.Presentation.UI.Startup
{
    /// <summary>
    /// Durations (real seconds) of the two startup cards. Card 1 timeline, from the card's own start:
    /// fade in, <see cref="DrawDelay"/>, pencil sketch for <see cref="DrawSeconds"/>, glow sweep and team name for
    /// <see cref="FillSeconds"/>, hold for <see cref="FillHold"/>, fade out. A negative <see cref="Gap"/> makes card 2
    /// cross-dissolve in while card 1 fades out. Reduced effects: no sketch, no sweep.
    /// </summary>
    public readonly struct StartupTiming
    {
        public readonly float Lead, PictogramIn, DrawDelay, DrawSeconds, FillSeconds, FillHold, PictogramOut, Gap;
        public readonly float EventIn, EventHold, EventOut, Reveal, SkipFade;

        public StartupTiming(float lead, float pictogramIn, float drawDelay, float drawSeconds, float fillSeconds, float fillHold,
            float pictogramOut, float gap, float eventIn, float eventHold, float eventOut, float reveal, float skipFade)
        {
            Lead = lead; PictogramIn = pictogramIn; DrawDelay = drawDelay; DrawSeconds = drawSeconds; FillSeconds = fillSeconds;
            FillHold = fillHold; PictogramOut = pictogramOut; Gap = gap; EventIn = eventIn; EventHold = eventHold;
            EventOut = eventOut; Reveal = reveal; SkipFade = skipFade;
        }

        public static readonly StartupTiming Normal = new StartupTiming(.3f, .7f, .4f, 4f, 1.5f, 3.6f, .9f, -.85f, .9f, 3f, .9f, .6f, .3f);
        public static readonly StartupTiming ReducedEffects = new StartupTiming(.15f, .4f, 0f, 0f, 0f, 2.2f, .5f, -.5f, .5f, 2.2f, .4f, .3f, .15f);

        public float PictogramStart => Lead;
        /// <summary>Time (from sequence start) the first stroke begins; the pencil sound starts here.</summary>
        public float DrawStart => PictogramStart + DrawDelay;
        public float FillStart => DrawStart + DrawSeconds;
        public float PictogramHold => Mathf.Max(0f, DrawDelay + DrawSeconds + FillSeconds + FillHold - PictogramIn);
        public float PictogramEnd => PictogramStart + PictogramIn + PictogramHold + PictogramOut;
        public float EventStart => PictogramEnd + Gap;
        public float EventEnd => EventStart + EventIn + EventHold + EventOut;
        public float Total => EventEnd + Reveal;
    }

    /// <summary>What is visible at one instant.</summary>
    public readonly struct StartupFrame
    {
        public readonly float PictogramAlpha, Draw01, Fill01, EventAlpha, OverlayAlpha, AudioGain;
        public StartupFrame(float pictogramAlpha, float draw01, float fill01, float eventAlpha, float overlayAlpha, float audioGain)
        {
            PictogramAlpha = pictogramAlpha; Draw01 = draw01; Fill01 = fill01; EventAlpha = eventAlpha;
            OverlayAlpha = overlayAlpha; AudioGain = audioGain;
        }
    }

    /// <summary>
    /// Pure, clock-agnostic timeline of the startup cards. The presenter feeds it real (unscaled) seconds. Any skip fades
    /// what is on screen (and the pencil sound) to the menu in <see cref="StartupTiming.SkipFade"/> seconds instead of cutting.
    /// </summary>
    public sealed class StartupSequence
    {
        /// <summary>Easing of the pencil sketch progress. Shared with <see cref="PencilScratchSynth"/> so sound follows the pen.</summary>
        public static float EaseDraw(float linear01) => Smooth01(linear01, .3f);

        private readonly StartupTiming timing;
        private float elapsed;
        private float skipClock;
        private float skippedAt;

        public StartupSequence(bool reducedEffects)
        {
            ReducedEffects = reducedEffects;
            timing = reducedEffects ? StartupTiming.ReducedEffects : StartupTiming.Normal;
        }

        public bool ReducedEffects { get; }
        public bool IsSkipped { get; private set; }
        public float Elapsed => elapsed;
        public float TotalSeconds => timing.Total;
        public StartupTiming Timing => timing;
        public bool IsFinished => IsSkipped ? skipClock >= timing.SkipFade : elapsed >= timing.Total;

        public void Advance(float seconds)
        {
            float dt = Mathf.Clamp(seconds, 0f, .25f);
            if (IsFinished) return;
            if (IsSkipped) skipClock += dt;
            else elapsed = Mathf.Min(timing.Total, elapsed + dt);
        }

        public void Skip()
        {
            if (IsSkipped || IsFinished) return;
            IsSkipped = true;
            skippedAt = elapsed;
            skipClock = 0f;
        }

        public StartupFrame Evaluate()
        {
            if (IsFinished) return new StartupFrame(0f, 1f, 1f, 0f, 0f, 0f);
            float t = IsSkipped ? skippedAt : elapsed;
            float pic = Envelope(t, timing.PictogramStart, timing.PictogramIn, timing.PictogramHold, timing.PictogramOut);
            float ev = Envelope(t, timing.EventStart, timing.EventIn, timing.EventHold, timing.EventOut);
            float draw = timing.DrawSeconds <= 0f ? 1f : EaseDraw((t - timing.DrawStart) / timing.DrawSeconds);
            float fill = timing.FillSeconds <= 0f ? 1f : Smooth01((t - timing.FillStart) / timing.FillSeconds, 1f);
            float overlay = t < timing.EventEnd ? 1f : 1f - Smooth01((t - timing.EventEnd) / timing.Reveal, 1f);
            float gain = 1f;
            if (IsSkipped)
            {
                float keep = 1f - Mathf.Clamp01(skipClock / timing.SkipFade);
                pic *= keep; ev *= keep; overlay *= keep; gain = keep;
            }
            return new StartupFrame(pic, draw, fill, ev, overlay, gain);
        }

        private static float Envelope(float t, float start, float fadeIn, float hold, float fadeOut)
        {
            float local = t - start;
            if (local <= 0f) return 0f;
            if (local < fadeIn) return Smooth01(local / fadeIn, 1f);
            local -= fadeIn;
            if (local < hold) return 1f;
            local -= hold;
            return local < fadeOut ? 1f - Smooth01(local / fadeOut, 1f) : 0f;
        }

        /// <summary>Smoothstep blended with linear by <paramref name="ease"/> (0 linear .. 1 full smoothstep).</summary>
        private static float Smooth01(float value, float ease)
        {
            float x = Mathf.Clamp01(value);
            return Mathf.Lerp(x, x * x * (3f - 2f * x), ease);
        }
    }
}
