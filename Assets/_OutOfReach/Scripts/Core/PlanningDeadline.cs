using System;
using OutOfReach.Contracts;
using OutOfReach.Inventory;

namespace OutOfReach.Core
{
    /// <summary>
    /// Optional launch deadline for mission planning, counted in unpaused REAL seconds (never simulated time).
    /// Disabled by default (practice planning). Once enabled, when the time reaches zero the current draft is
    /// launched if it is valid; otherwise the last valid loadout the player had is restored and launched, and
    /// <see cref="UsedLastValidLoadoutAtDeadline"/> records that for the report. This happens once and atomically;
    /// a default preset is never forced silently. Enabling fails if no valid loadout exists to fall back to.
    /// Implements the shared read-only <see cref="IPlanningDeadline"/>; presentation only displays it.
    /// A manual launch and the expiry can never both launch: whichever commits first leaves Planning, and the
    /// other is rejected or ignored.
    /// </summary>
    public sealed class PlanningDeadline : IPlanningDeadline
    {
        public const double DefaultDurationRealSeconds = 60d;

        private readonly MissionPlanner planner;
        private readonly MissionGameState state;
        private MissionConfiguration lastValid;

        internal PlanningDeadline(MissionPlanner planner, MissionGameState state)
        {
            this.planner = planner ?? throw new ArgumentNullException(nameof(planner));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            planner.DraftChanged += CaptureDraftIfValid;
            CaptureDraftIfValid();
        }

        /// <summary>True once timed planning was started for this mission.</summary>
        public bool IsEnabled { get; private set; }

        public double DurationRealSeconds { get; private set; }

        /// <summary>Unpaused real seconds left; 0 after expiry and while the deadline is not enabled.</summary>
        public double RemainingRealSeconds { get; private set; }

        /// <summary>Immutable snapshot of the most recent valid draft; this is what an expired deadline falls back to.</summary>
        public IMissionConfiguration LastValidConfiguration => lastValid;

        /// <summary>True when the deadline launched the last valid loadout because the draft at zero was invalid.</summary>
        public bool UsedLastValidLoadoutAtDeadline { get; private set; }

        /// <summary>True once the deadline reached zero and performed its single launch attempt.</summary>
        public bool HasExpired { get; private set; }

        /// <summary>Result of the launch the deadline performed, or null if it has not expired.</summary>
        public CommandResult? DeadlineLaunchResult { get; private set; }

        /// <summary>Starts timed planning. Planning phase only, once, and only with a valid loadout to fall back to.</summary>
        public CommandResult TryEnable(double durationRealSeconds = DefaultDurationRealSeconds)
        {
            if (state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (state.Phase != GamePhase.Planning || planner.HasLaunched)
            {
                return Fail(CommandFailure.WrongPhase, "Timed planning can only start during Planning.");
            }

            if (IsEnabled)
            {
                return Fail(CommandFailure.AlreadyComplete, "Timed planning is already running.");
            }

            if (double.IsNaN(durationRealSeconds) || double.IsInfinity(durationRealSeconds) || durationRealSeconds <= 0d)
            {
                return Fail(CommandFailure.InvalidInput, "The planning deadline must be a finite number of seconds greater than zero.");
            }

            if (lastValid == null)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Timed planning needs a valid starting loadout to fall back to.");
            }

            IsEnabled = true;
            DurationRealSeconds = durationRealSeconds;
            RemainingRealSeconds = durationRealSeconds;
            return new CommandResult(true, CommandFailure.None, "Timed planning started.");
        }

        /// <summary>
        /// Counts unpaused real seconds during Planning. At zero it performs the single deadline launch.
        /// Invalid time throws before any mutation. The caller must not also advance simulated time for the same
        /// frame, so the launch frame spends no post-launch simulation time.
        /// </summary>
        public void TickRealTime(double realSeconds)
        {
            if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(realSeconds), realSeconds, "Real time must be finite and non-negative.");
            }

            if (!IsEnabled || HasExpired || state.IsClosed || state.Phase != GamePhase.Planning)
            {
                return;
            }

            RemainingRealSeconds = Math.Max(0d, RemainingRealSeconds - realSeconds);
            if (RemainingRealSeconds > 0d)
            {
                return;
            }

            HasExpired = true;
            if (planner.DraftConfiguration.Validate().Succeeded)
            {
                DeadlineLaunchResult = planner.TryLaunch();
                return;
            }

            UsedLastValidLoadoutAtDeadline = true;
            DeadlineLaunchResult = planner.TryLaunchConfiguration(lastValid);
        }

        private void CaptureDraftIfValid()
        {
            MissionConfiguration draft = planner.DraftConfiguration;
            if (draft != null && draft.Validate().Succeeded)
            {
                lastValid = draft;
            }
        }

        private static CommandResult Fail(CommandFailure failure, string explanation)
        {
            return new CommandResult(false, failure, explanation);
        }
    }
}
