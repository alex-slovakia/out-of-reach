using System;

namespace OutOfReach.Presentation.LaunchFailure
{
    public enum LaunchFailureStage { Liftoff, Stutter, Cut, Card, Finished }

    /// <summary>
    /// The launch-failure sequence as a pure clock (about 5 s, real time): the liftoff view runs on, then engine
    /// stutter with shake and the red "THRUST BELOW WEIGHT" strip, a cut to black, then the "LAUNCH FAILURE" card.
    /// Reduced effects: no shake or flicker and a straight cut. Paused time does not advance; skip finishes at once.
    /// Presentation values only; it never touches mission state.
    /// </summary>
    public sealed class LaunchFailureTimeline
    {
        public const float LiftoffEnd = 1.2f;
        public const float StutterEnd = 2.8f;
        public const float CutEnd = 3.2f;
        public const float Duration = 5.2f;
        public const float MaxStepSeconds = .1f;
        public const float ShakePixels = 14f;

        public LaunchFailureTimeline(bool reducedEffects)
        {
            ReducedEffects = reducedEffects;
        }

        public bool ReducedEffects { get; }

        public float Elapsed { get; private set; }

        public bool Skipped { get; private set; }

        public bool IsFinished => Skipped || Elapsed >= Duration;

        public LaunchFailureStage Stage =>
            IsFinished ? LaunchFailureStage.Finished
            : Elapsed < LiftoffEnd ? LaunchFailureStage.Liftoff
            : Elapsed < StutterEnd ? LaunchFailureStage.Stutter
            : Elapsed < CutEnd ? LaunchFailureStage.Cut
            : LaunchFailureStage.Card;

        /// <summary>Advances by real seconds; a pause, a negative or non-finite step changes nothing; a hitch is capped.</summary>
        public void Advance(float realSeconds, bool paused)
        {
            if (IsFinished || paused || float.IsNaN(realSeconds) || float.IsInfinity(realSeconds) || realSeconds <= 0f) return;
            Elapsed = Math.Min(Duration, Elapsed + Math.Min(realSeconds, MaxStepSeconds));
        }

        public void Skip() => Skipped = true;

        /// <summary>The red warning strip: from the stutter until the cut is black.</summary>
        public bool StripVisible => !IsFinished && Elapsed >= LiftoffEnd && Elapsed < CutEnd;

        /// <summary>Strip flicker 0..1 (steady 1 with reduced effects).</summary>
        public float StripAlpha
        {
            get
            {
                if (!StripVisible) return 0f;
                if (ReducedEffects) return 1f;
                return (int)(Elapsed * 8f) % 2 == 0 ? 1f : .55f;
            }
        }

        /// <summary>Engine-stutter shake offset in pixels; always 0 with reduced effects. Deterministic in time.</summary>
        public float ShakeX => Shake(23.0, 0d);

        public float ShakeY => Shake(31.0, 1.7d);

        /// <summary>Black over the view: rises through the cut (instant with reduced effects) and stays under the card.</summary>
        public float BlackAlpha
        {
            get
            {
                if (IsFinished || Elapsed < StutterEnd) return 0f;
                if (ReducedEffects) return 1f;
                return Math.Min(1f, (Elapsed - StutterEnd) / (CutEnd - StutterEnd));
            }
        }

        /// <summary>The "LAUNCH FAILURE" card: fades in over .4 s (instant with reduced effects).</summary>
        public float CardAlpha
        {
            get
            {
                if (IsFinished || Elapsed < CutEnd) return 0f;
                if (ReducedEffects) return 1f;
                return Math.Min(1f, (Elapsed - CutEnd) / .4f);
            }
        }

        private float Shake(double frequency, double phase)
        {
            if (ReducedEffects || IsFinished || Elapsed < LiftoffEnd || Elapsed >= StutterEnd) return 0f;
            double ramp = (Elapsed - LiftoffEnd) / (StutterEnd - LiftoffEnd);
            double wave = Math.Sin(Elapsed * frequency + phase) * Math.Sin(Elapsed * frequency * .37 + phase);
            return (float)(wave * ShakePixels * (.35 + .65 * ramp));
        }
    }
}
