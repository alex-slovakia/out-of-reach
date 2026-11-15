using UnityEngine;

namespace OutOfReach.Presentation.UI.Startup
{
    /// <summary>Durations (real seconds) of the two startup cards. Reduced effects: shorter, fade only, no pencil draw-on.</summary>
    public readonly struct StartupTiming
    {
        public readonly float Lead, PictogramIn, PictogramHold, PictogramOut, Gap, EventIn, EventHold, EventOut, Reveal;
        public readonly float DrawDelay, DrawSeconds, SkipFade;

        public StartupTiming(float lead, float pictogramIn, float pictogramHold, float pictogramOut, float gap,
            float eventIn, float eventHold, float eventOut, float reveal, float drawDelay, float drawSeconds, float skipFade)
        {
            Lead = lead; PictogramIn = pictogramIn; PictogramHold = pictogramHold; PictogramOut = pictogramOut; Gap = gap;
            EventIn = eventIn; EventHold = eventHold; EventOut = eventOut; Reveal = reveal;
            DrawDelay = drawDelay; DrawSeconds = drawSeconds; SkipFade = skipFade;
        }

        public static readonly StartupTiming Normal = new StartupTiming(.3f, .9f, 3f, .8f, .25f, .8f, 3.2f, .9f, .6f, .35f, 2.6f, .3f);
        public static readonly StartupTiming ReducedEffects = new StartupTiming(.15f, .4f, 1.8f, .4f, .1f, .4f, 2.4f, .4f, .3f, 0f, 0f, .15f);

        public float PictogramStart => Lead;
        public float PictogramEnd => Lead + PictogramIn + PictogramHold + PictogramOut;
        public float EventStart => PictogramEnd + Gap;
        public float EventEnd => EventStart + EventIn + EventHold + EventOut;
        public float Total => EventEnd + Reveal;
    }

    /// <summary>What is visible at one instant: card opacities, pictogram draw-on progress and the black overlay opacity.</summary>
    public readonly struct StartupFrame
    {
        public readonly float PictogramAlpha, Draw01, EventAlpha, OverlayAlpha;
        public StartupFrame(float pictogramAlpha, float draw01, float eventAlpha, float overlayAlpha)
        {
            PictogramAlpha = pictogramAlpha; Draw01 = draw01; EventAlpha = eventAlpha; OverlayAlpha = overlayAlpha;
        }
    }

    /// <summary>
    /// Pure, clock-agnostic timeline of the startup cards. The presenter feeds it real (unscaled) seconds; it holds
    /// no Unity state, so it is testable without a panel. Any skip fades what is on screen to the menu in
    /// <see cref="StartupTiming.SkipFade"/> seconds instead of cutting.
    /// </summary>
    public sealed class StartupSequence
    {
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
            if (IsFinished) return new StartupFrame(0f, 1f, 0f, 0f);
            float t = IsSkipped ? skippedAt : elapsed;
            float pic = Envelope(t, timing.PictogramStart, timing.PictogramIn, timing.PictogramHold, timing.PictogramOut);
            float ev = Envelope(t, timing.EventStart, timing.EventIn, timing.EventHold, timing.EventOut);
            float draw = timing.DrawSeconds <= 0f ? 1f
                : Smooth01((t - (timing.PictogramStart + timing.DrawDelay)) / timing.DrawSeconds, .35f);
            float overlay = t < timing.EventEnd ? 1f : 1f - Smooth01((t - timing.EventEnd) / timing.Reveal, 1f);
            if (IsSkipped)
            {
                float keep = 1f - Mathf.Clamp01(skipClock / timing.SkipFade);
                pic *= keep; ev *= keep; overlay *= keep;
            }
            return new StartupFrame(pic, draw, ev, overlay);
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
