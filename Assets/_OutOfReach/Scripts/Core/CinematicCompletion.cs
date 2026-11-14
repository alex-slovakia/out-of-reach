using OutOfReach.Contracts;

namespace OutOfReach.Core
{
    /// <summary>How a requested cinematic ended: acknowledged by presentation (possibly skipped) or by timeout.</summary>
    public readonly struct CinematicCompletion
    {
        public CinematicCompletion(CinematicId cinematic, int sequenceId, bool wasSkipped, bool timedOut)
        {
            Cinematic = cinematic;
            SequenceId = sequenceId;
            WasSkipped = wasSkipped;
            TimedOut = timedOut;
        }

        public CinematicId Cinematic { get; }

        public int SequenceId { get; }

        public bool WasSkipped { get; }

        /// <summary>True when Core's fallback fired because no matching acknowledgement arrived in time.</summary>
        public bool TimedOut { get; }
    }
}
