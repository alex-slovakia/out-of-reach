using System;
using UnityEngine;

namespace OutOfReach.Presentation.Radio
{
    public enum RadioPhase { Idle, Incoming, Raise, Gap, Line, Lower, Done }
    public enum RadioLed { Off, Transmit, Receive }

    /// <summary>
    /// Deterministic timeline of one radio beat (pure C#, unit-tested). Time only advances in Tick while not paused;
    /// Skip lowers the radio from wherever it is. Reduced motion shortens raise/lower and removes the hand sway.
    /// </summary>
    public sealed class RadioBeatTimeline
    {
        public const float IncomingSeconds = .7f;
        public const float RaiseSeconds = .5f, LowerSeconds = .45f;
        public const float ReducedRaiseSeconds = .2f, ReducedLowerSeconds = .2f;
        public const float GapSeconds = .3f, TailSeconds = .25f;

        private float[] lineStart = Array.Empty<float>();
        private float raiseStart, linesEnd, lowerStart, lowerFrom = 1f, raise, lower;
        private float time;
        private bool lowering;

        public RadioBeat Beat { get; private set; }
        public bool ReducedMotion { get; private set; }
        public float Time => time;
        public RadioPhase Phase { get; private set; } = RadioPhase.Idle;
        public int LineIndex { get; private set; } = -1;
        public bool Skipped { get; private set; }
        public bool IsActive => Phase != RadioPhase.Idle && Phase != RadioPhase.Done;
        public float TotalSeconds => lowerStart + lower;

        public void Start(RadioBeat beat, bool reducedMotion)
        {
            Beat = beat ?? throw new ArgumentNullException(nameof(beat));
            ReducedMotion = reducedMotion;
            raise = reducedMotion ? ReducedRaiseSeconds : RaiseSeconds;
            lower = reducedMotion ? ReducedLowerSeconds : LowerSeconds;
            raiseStart = beat.IncomingCall ? IncomingSeconds : 0f;
            float t = raiseStart + raise;
            lineStart = new float[beat.Lines.Count];
            for (int i = 0; i < lineStart.Length; i++) { t += GapSeconds; lineStart[i] = t; t += beat.Lines[i].Seconds; }
            linesEnd = t;
            lowerStart = t + TailSeconds;
            time = 0f; lowering = false; lowerFrom = 1f; Skipped = false; LineIndex = -1;
            Evaluate();
        }

        /// <summary>Advance by real (unscaled) seconds. Paused: nothing moves, nothing is spoken.</summary>
        public void Tick(float deltaSeconds, bool paused)
        {
            if (!IsActive || paused || deltaSeconds <= 0f) return;
            time += deltaSeconds;
            Evaluate();
        }

        /// <summary>Ends the lines now and lowers the radio from its present height.</summary>
        public void Skip()
        {
            if (!IsActive || Phase == RadioPhase.Lower) return;
            lowerFrom = PoseWeight;
            Skipped = true;
            lowering = true;
            lowerStart = time;
            Evaluate();
        }

        /// <summary>Stops at once (session replaced, presenter disabled): no lowering, nothing more is spoken.</summary>
        public void Abort()
        {
            if (Beat == null) return;
            lowering = true; Skipped = true; LineIndex = -1;
            Phase = RadioPhase.Done;
        }

        /// <summary>0 = hidden below the view, 1 = held up near the face.</summary>
        public float PoseWeight
        {
            get
            {
                switch (Phase)
                {
                    case RadioPhase.Raise: return Ease(Mathf.Clamp01((time - raiseStart) / raise));
                    case RadioPhase.Gap: case RadioPhase.Line: return 1f;
                    case RadioPhase.Lower: return lowerFrom * (1f - Ease(Mathf.Clamp01((time - lowerStart) / lower)));
                    default: return 0f;
                }
            }
        }

        public RadioLine? CurrentLine => Phase == RadioPhase.Line && LineIndex >= 0 ? Beat.Lines[LineIndex] : (RadioLine?)null;
        public bool PttPressed => CurrentLine.HasValue && CurrentLine.Value.Kind == RadioLineKind.Transmit;
        public RadioLed Led => !CurrentLine.HasValue ? RadioLed.Off
            : CurrentLine.Value.Kind == RadioLineKind.Transmit ? RadioLed.Transmit : RadioLed.Receive;
        /// <summary>Seconds since the current line started (0 outside lines).</summary>
        public float LineSeconds => CurrentLine.HasValue ? time - lineStart[LineIndex] : 0f;
        /// <summary>Hand sway amplitude: zero with reduced motion.</summary>
        public float SwayAmplitude => ReducedMotion ? 0f : 1f;

        private void Evaluate()
        {
            if (Beat == null) { Phase = RadioPhase.Idle; return; }
            if (lowering || time >= lowerStart)
            {
                LineIndex = -1;
                Phase = time >= lowerStart + lower ? RadioPhase.Done : RadioPhase.Lower;
                return;
            }
            if (time < raiseStart) { Phase = RadioPhase.Incoming; LineIndex = -1; return; }
            if (time < raiseStart + raise) { Phase = RadioPhase.Raise; LineIndex = -1; return; }
            for (int i = lineStart.Length - 1; i >= 0; i--)
                if (time >= lineStart[i])
                {
                    bool inLine = time < lineStart[i] + Beat.Lines[i].Seconds;
                    Phase = inLine ? RadioPhase.Line : RadioPhase.Gap;
                    LineIndex = inLine ? i : -1;
                    return;
                }
            Phase = RadioPhase.Gap; LineIndex = -1;
        }

        private static float Ease(float t) => t * t * (3f - 2f * t);
    }
}
