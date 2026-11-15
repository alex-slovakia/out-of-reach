namespace OutOfReach.Presentation.CinematicSkip
{
    /// <summary>What a completed hold would skip right now.</summary>
    public enum CinematicSkipTarget
    {
        None = 0,
        /// <summary>The transport departure and transfer flight (I06MarineBindings.Skip).</summary>
        Departure = 1,
    }

    /// <summary>One frame of scene facts. Plain data so the rules are testable without a scene.</summary>
    public struct CinematicSkipContext
    {
        /// <summary>The mission clock is paused (pause menu).</summary>
        public bool Paused;
        /// <summary>The player has chosen a mode: the startup cards and the mode menu are gone.</summary>
        public bool ModeChosen;
        /// <summary>The rescue ending is on screen. It owns its own hold-to-skip.</summary>
        public bool RescueEndingPlaying;
        /// <summary>The player can move. Space means push-off then, never skip.</summary>
        public bool ControlAllowed;
        public bool DeparturePlaying;
    }

    /// <summary>Pure rules of the cinematic hold-to-skip: when a hold counts, and when it fires.</summary>
    public sealed class CinematicSkipHold
    {
        public const float DefaultHoldSeconds = .6f;
        /// <summary>A stalled frame never completes a hold on its own.</summary>
        public const float MaximumStepSeconds = .1f;

        private readonly float holdSeconds;
        private float held;
        private bool armed;

        public CinematicSkipHold(float holdSeconds = DefaultHoldSeconds)
        {
            this.holdSeconds = holdSeconds > 0f && !float.IsNaN(holdSeconds) && !float.IsInfinity(holdSeconds)
                ? holdSeconds : DefaultHoldSeconds;
        }

        public float HoldSeconds => holdSeconds;
        public float HeldSeconds => held;
        /// <summary>0..1 of the current counting hold.</summary>
        public float Progress01 => held <= 0f ? 0f : held >= holdSeconds ? 1f : held / holdSeconds;
        /// <summary>True once the key has been seen released inside the current skippable window.</summary>
        public bool IsArmed => armed;

        /// <summary>
        /// The departure is the only target, and only while the player's control is locked.
        /// Gameplay, pause, menus, startup cards and the rescue ending are never a target. Core's own cinematics are
        /// not a target either: Impact leaves the player in control (Space is push-off there).
        /// </summary>
        public static CinematicSkipTarget Resolve(in CinematicSkipContext context)
        {
            if (context.Paused || !context.ModeChosen || context.RescueEndingPlaying || context.ControlAllowed)
                return CinematicSkipTarget.None;
            return context.DeparturePlaying ? CinematicSkipTarget.Departure : CinematicSkipTarget.None;
        }

        /// <summary>
        /// Advance by one frame. Returns true exactly once per completed hold.
        /// A hold only counts when it starts inside the skippable window: a key that is already down when the window
        /// opens (held from gameplay, or the same hold that just skipped another sequence) must be released first.
        /// </summary>
        public bool Tick(bool skippable, bool keyHeld, float realSeconds)
        {
            if (!skippable)
            {
                armed = false;
                held = 0f;
                return false;
            }

            if (!keyHeld)
            {
                armed = true;
                held = 0f;
                return false;
            }

            if (!armed) return false;
            if (float.IsNaN(realSeconds) || float.IsInfinity(realSeconds) || realSeconds <= 0f) return false;
            held += realSeconds < MaximumStepSeconds ? realSeconds : MaximumStepSeconds;
            if (held < holdSeconds) return false;
            armed = false;   // one skip per hold: release before the next one counts
            held = 0f;
            return true;
        }

        public void Reset()
        {
            armed = false;
            held = 0f;
        }
    }
}
