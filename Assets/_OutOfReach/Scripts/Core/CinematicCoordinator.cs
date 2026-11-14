using System;
using System.Threading;
using OutOfReach.Contracts;

namespace OutOfReach.Core
{
    /// <summary>
    /// Core side of the cinematic handshake. Core publishes CinematicRequested with a sequence ID that is unique in
    /// the process (so an acknowledgement left over from a previous session can never match). Presentation answers
    /// with CinematicFinished carrying the same cinematic and sequence ID. Stale, duplicate and mismatched
    /// acknowledgements are ignored. If no acknowledgement arrives within the fallback timeout (unpaused real
    /// seconds) the request completes with TimedOut = true; with no presentation listener at all it completes on
    /// the next tick. Exactly one completion callback runs per request. Dispose unsubscribes and cancels.
    /// </summary>
    public sealed class CinematicCoordinator : IDisposable
    {
        /// <summary>Fallback when presentation never acknowledges. Long enough for any planned cinematic.</summary>
        public const double DefaultTimeoutRealSeconds = 45d;

        private static int lastIssuedSequenceId;

        private readonly MissionEventBus events;
        private Action<CinematicCompletion> pendingCallback;
        private double timeoutRealSeconds;
        private double waitedRealSeconds;
        private bool disposed;

        public CinematicCoordinator(MissionEventBus events)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            events.CinematicFinished += OnCinematicFinished;
        }

        public bool IsWaiting { get; private set; }

        public CinematicId ActiveCinematic { get; private set; }

        /// <summary>Sequence ID of the active (or most recent) request; 0 before the first request.</summary>
        public int ActiveSequenceId { get; private set; }

        /// <summary>Acknowledgements ignored as stale, duplicate or mismatched.</summary>
        public int IgnoredAcknowledgements { get; private set; }

        public CinematicCompletion? LastCompletion { get; private set; }

        /// <summary>
        /// Starts one cinematic. Rejected while another is active. The callback runs exactly once, after the
        /// coordinator has already cleared the request, so it may safely request the next cinematic.
        /// </summary>
        public CommandResult TryRequest(CinematicId cinematic, Action<CinematicCompletion> onCompleted, double fallbackTimeoutRealSeconds = DefaultTimeoutRealSeconds)
        {
            if (disposed)
            {
                return new CommandResult(false, CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (!Enum.IsDefined(typeof(CinematicId), cinematic))
            {
                return new CommandResult(false, CommandFailure.InvalidInput, $"Unknown cinematic value {(int)cinematic}.");
            }

            if (double.IsNaN(fallbackTimeoutRealSeconds) || double.IsInfinity(fallbackTimeoutRealSeconds) || fallbackTimeoutRealSeconds < 0d)
            {
                return new CommandResult(false, CommandFailure.InvalidInput, "The fallback timeout must be finite and non-negative.");
            }

            if (IsWaiting)
            {
                return new CommandResult(false, CommandFailure.PreconditionsNotMet, $"Cinematic {ActiveCinematic} (sequence {ActiveSequenceId}) is still active.");
            }

            int sequenceId = Interlocked.Increment(ref lastIssuedSequenceId);
            IsWaiting = true;
            ActiveCinematic = cinematic;
            ActiveSequenceId = sequenceId;
            pendingCallback = onCompleted;
            waitedRealSeconds = 0d;
            bool presentationListening = events.GetSubscriberCount(MissionEventKind.CinematicRequested) > 0;
            timeoutRealSeconds = presentationListening ? fallbackTimeoutRealSeconds : 0d;
            events.Publish(new CinematicRequested(cinematic, sequenceId));
            return new CommandResult(true, CommandFailure.None, $"Requested {cinematic} (sequence {sequenceId}).");
        }

        /// <summary>Advances the fallback timer by unpaused real seconds. Invalid time throws without mutation.</summary>
        public void TickRealTime(double realSeconds)
        {
            if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(realSeconds), realSeconds, "Real time must be finite and non-negative.");
            }

            if (!IsWaiting)
            {
                return;
            }

            waitedRealSeconds += realSeconds;
            if (waitedRealSeconds >= timeoutRealSeconds)
            {
                Complete(false, true);
            }
        }

        /// <summary>Drops the active request without running its callback (restart or teardown).</summary>
        public bool Cancel()
        {
            if (!IsWaiting)
            {
                return false;
            }

            IsWaiting = false;
            pendingCallback = null;
            waitedRealSeconds = 0d;
            return true;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Cancel();
            events.CinematicFinished -= OnCinematicFinished;
        }

        private void OnCinematicFinished(CinematicFinished message)
        {
            if (!IsWaiting || message.SequenceId != ActiveSequenceId || message.Cinematic != ActiveCinematic)
            {
                IgnoredAcknowledgements++;
                return;
            }

            Complete(message.WasSkipped, false);
        }

        private void Complete(bool wasSkipped, bool timedOut)
        {
            var completion = new CinematicCompletion(ActiveCinematic, ActiveSequenceId, wasSkipped, timedOut);
            Action<CinematicCompletion> callback = pendingCallback;
            IsWaiting = false;
            pendingCallback = null;
            waitedRealSeconds = 0d;
            LastCompletion = completion;
            if (callback == null)
            {
                return;
            }

            try
            {
                callback(completion);
            }
            catch (Exception exception)
            {
                // Same isolation on the acknowledgement and the timeout path: a failing continuation is reported
                // and never interrupts the frame's simulation step.
                events.ReportCallbackFailure(exception);
            }
        }
    }
}
