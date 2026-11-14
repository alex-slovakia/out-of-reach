using System;
using System.Globalization;
using OutOfReach.Contracts;

namespace OutOfReach.Interaction
{
    public enum TimedWorkState
    {
        Idle,
        Working,
    }

    /// <summary>
    /// REPAIR-PRESENTATION-GATE-01 (Codex 6884609399): an opt-in decorator around ONE real interactable (the antenna
    /// repair) that turns E into visible work of a fixed active duration. E only starts the work: nothing is spent and
    /// no domain command runs. Presentation advances it with unpaused real time while its tool visual plays; when the
    /// required time is reached the real availability is checked again and the captured real command runs exactly once,
    /// so its own result (success, or a refusal with nothing spent) is what gets published. Pause freezes the elapsed
    /// time; a cancel, an invalid session/focus (the caller's <c>stillValid</c> policy) or a new session spends nothing.
    /// It is not a second repair system and never awards a result on a timer: the domain decides at the end.
    /// Interactables that are not wrapped keep their instant behavior.
    /// </summary>
    public sealed class TimedInteractionGate : IInteractable
    {
        /// <summary>The refusal of a repeated E while the work runs.</summary>
        public const string AlreadyWorkingMessage = "Work already in progress";

        private readonly Func<bool> stillValid;
        private readonly Func<bool> paused;
        private bool completing;

        /// <param name="inner">The real interactable from the current session (for example the antenna repair).</param>
        /// <param name="activeSecondsRequired">Unpaused real seconds of visible work before the real command runs.</param>
        /// <param name="stillValid">Caller's policy, checked on start and on every advance: current session, focus, control. Null = always.</param>
        /// <param name="paused">True while time must not count (pause menu). Null = never paused.</param>
        public TimedInteractionGate(IInteractable inner, double activeSecondsRequired, Func<bool> stillValid = null, Func<bool> paused = null)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (double.IsNaN(activeSecondsRequired) || double.IsInfinity(activeSecondsRequired) || activeSecondsRequired <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(activeSecondsRequired), activeSecondsRequired, "The work needs a finite duration above zero.");
            }

            ActiveSecondsRequired = activeSecondsRequired;
            this.stillValid = stillValid;
            this.paused = paused;
        }

        public IInteractable Inner { get; }

        public string InteractionId => Inner.InteractionId;

        public double ActiveSecondsRequired { get; }

        public TimedWorkState State { get; private set; }

        /// <summary>Unpaused real seconds of the current work; 0 when idle.</summary>
        public double ElapsedActiveSeconds { get; private set; }

        public double Progress01 => State == TimedWorkState.Working ? Math.Min(1d, ElapsedActiveSeconds / ActiveSecondsRequired) : 0d;

        /// <summary>E started the work (nothing spent yet).</summary>
        public event Action Started;

        /// <summary>The work advanced; the argument is the progress 0..1.</summary>
        public event Action<double> Progressed;

        /// <summary>The work reached its duration and the real command ran once; its own result.</summary>
        public event Action<CommandResult> Finished;

        /// <summary>The work stopped before the end; nothing was spent. The argument says why.</summary>
        public event Action<string> Cancelled;

        public string Prompt => State == TimedWorkState.Working
            ? string.Format(CultureInfo.InvariantCulture, "Working: {0:0}%", Progress01 * 100d)
            : Inner.Prompt;

        public bool CanInteract => State == TimedWorkState.Idle && Inner.CanInteract && IsStillValid();

        /// <summary>
        /// E: starts the work when the real interactable is available (success "Work started: 3.0 s"); otherwise its
        /// refusal, without calling it. While the work runs, a repeated E is refused and changes nothing.
        /// </summary>
        public CommandResult TryInteract()
        {
            if (State == TimedWorkState.Working)
            {
                return new CommandResult(false, CommandFailure.PreconditionsNotMet, AlreadyWorkingMessage);
            }

            if (!Inner.CanInteract)
            {
                return new CommandResult(false, CommandFailure.PreconditionsNotMet, string.IsNullOrEmpty(Inner.Prompt) ? "Not available now." : Inner.Prompt);
            }

            if (!IsStillValid())
            {
                return new CommandResult(false, CommandFailure.PreconditionsNotMet, "The work cannot start now.");
            }

            State = TimedWorkState.Working;
            ElapsedActiveSeconds = 0d;
            Started?.Invoke();
            return new CommandResult(true, CommandFailure.None,
                string.Format(CultureInfo.InvariantCulture, "Work started: {0:0.0} s", ActiveSecondsRequired));
        }

        /// <summary>
        /// Adds unpaused real time while the work runs (call it every frame the tool visual plays). Ignored while
        /// paused. An invalid session or focus cancels the work with nothing spent. At the required time the real
        /// availability is checked again and the real command runs once.
        /// </summary>
        public void Advance(double activeRealSeconds)
        {
            if (double.IsNaN(activeRealSeconds) || double.IsInfinity(activeRealSeconds) || activeRealSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(activeRealSeconds), activeRealSeconds, "Real time must be finite and non-negative.");
            }

            if (State != TimedWorkState.Working || completing)
            {
                return;
            }

            if (paused != null && paused())
            {
                return;
            }

            if (!IsStillValid())
            {
                Cancel("The work was interrupted.");
                return;
            }

            ElapsedActiveSeconds = Math.Min(ActiveSecondsRequired, ElapsedActiveSeconds + activeRealSeconds);
            Progressed?.Invoke(Progress01);
            if (ElapsedActiveSeconds >= ActiveSecondsRequired)
            {
                Complete();
            }
        }

        /// <summary>Stops the work before the end (look away, walk away, disable, restart, new session). Nothing is spent.</summary>
        public void Cancel(string reason)
        {
            if (State != TimedWorkState.Working || completing)
            {
                return;
            }

            Reset();
            Cancelled?.Invoke(string.IsNullOrEmpty(reason) ? "Work cancelled." : reason);
        }

        private void Complete()
        {
            completing = true;
            CommandResult result;
            try
            {
                // Revalidate at the end: parts, energy, phase and reach may have changed during the work.
                result = Inner.CanInteract
                    ? Inner.TryInteract()
                    : new CommandResult(false, CommandFailure.PreconditionsNotMet, string.IsNullOrEmpty(Inner.Prompt) ? "Not available now." : Inner.Prompt);
            }
            finally
            {
                completing = false;
                Reset();
            }

            Finished?.Invoke(result);
        }

        private bool IsStillValid()
        {
            return stillValid == null || stillValid();
        }

        private void Reset()
        {
            State = TimedWorkState.Idle;
            ElapsedActiveSeconds = 0d;
        }
    }
}
