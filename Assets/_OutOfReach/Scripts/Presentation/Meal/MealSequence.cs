using System;
using System.Globalization;
using OutOfReach.Contracts;

namespace OutOfReach.Presentation.Meal
{
    /// <summary>How the taken pouch is prepared before it is opened (NASA ready-to-eat, thermostabilised, rehydratable).</summary>
    public enum MealPreparation { ReadyToEat, Warmed, Rehydrated }

    /// <summary>Visible meal stages. <see cref="Eat"/> begins with the one real ration command (the first bite).</summary>
    public enum MealStage { Idle, Take, Prepare, Open, Eat, Stow }

    /// <summary>
    /// Real-time pacing of one meal (unpaused real seconds). Presentation pacing only: it is not simulation time,
    /// not the amount eaten and not the moment anything is awarded except through <see cref="CommitAt"/>, where the
    /// sequence asks Core once.
    /// </summary>
    public readonly struct MealTimeline
    {
        public MealTimeline(double take, double prepare, double open, double eat, double stow)
        {
            Take = Positive(take, nameof(take));
            Prepare = NonNegative(prepare, nameof(prepare));
            Open = Positive(open, nameof(open));
            Eat = Positive(eat, nameof(eat));
            Stow = Positive(stow, nameof(stow));
        }

        public double Take { get; }
        public double Prepare { get; }
        public double Open { get; }
        public double Eat { get; }
        public double Stow { get; }
        public double Total => Take + Prepare + Open + Eat + Stow;

        /// <summary>The first bite: the start of <see cref="MealStage.Eat"/>.</summary>
        public double CommitAt => Take + Prepare + Open;

        /// <summary>The reviewed default pacing per preparation; <paramref name="labelCheck"/> adds the restrained repeat beat.</summary>
        public static MealTimeline For(MealPreparation kind, bool labelCheck = false)
        {
            double open = labelCheck ? 1.7d : 1.0d;
            switch (kind)
            {
                case MealPreparation.Warmed: return new MealTimeline(1.4d, 2.6d, open, 2.8d, 1.0d);
                case MealPreparation.Rehydrated: return new MealTimeline(1.4d, 2.8d, open, 2.8d, 1.0d);
                default: return new MealTimeline(1.4d, 0.8d, open, 2.8d, 1.0d);
            }
        }

        public MealStage StageAt(double elapsed)
        {
            if (double.IsNaN(elapsed) || elapsed < 0d) return MealStage.Take;
            if (elapsed < Take) return MealStage.Take;
            if (elapsed < Take + Prepare) return MealStage.Prepare;
            if (elapsed < CommitAt) return MealStage.Open;
            if (elapsed < CommitAt + Eat) return MealStage.Eat;
            return MealStage.Stow;
        }

        /// <summary>0..1 within the stage that contains <paramref name="elapsed"/>.</summary>
        public double StageProgress(double elapsed)
        {
            MealStage stage = StageAt(elapsed);
            double start = StageStart(stage), length = StageLength(stage);
            return length <= 0d ? 1d : Math.Max(0d, Math.Min(1d, (elapsed - start) / length));
        }

        public double StageStart(MealStage stage)
        {
            switch (stage)
            {
                case MealStage.Prepare: return Take;
                case MealStage.Open: return Take + Prepare;
                case MealStage.Eat: return CommitAt;
                case MealStage.Stow: return CommitAt + Eat;
                default: return 0d;
            }
        }

        public double StageLength(MealStage stage)
        {
            switch (stage)
            {
                case MealStage.Take: return Take;
                case MealStage.Prepare: return Prepare;
                case MealStage.Open: return Open;
                case MealStage.Eat: return Eat;
                case MealStage.Stow: return Stow;
                default: return 0d;
            }
        }

        private static double Positive(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d
            ? value : throw new ArgumentOutOfRangeException(name, value, "A meal stage needs a finite duration above zero.");
        private static double NonNegative(double value, string name) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d
            ? value : throw new ArgumentOutOfRangeException(name, value, "A meal stage needs a finite non-negative duration.");
    }

    /// <summary>
    /// MEAL-PRES-01: an opt-in decorator around ONE real interactable, the existing <c>survival-ration</c> command, that turns
    /// E into a visible meal: take a stored pouch, prepare it by type, open it and eat. It follows the reviewed
    /// <c>TimedInteractionGate</c> pattern (antenna repair) with one difference: the real command runs at the first bite,
    /// not at the very end, so the rest of the eating and the stowing present a ration Core has already debited.
    /// <para>
    /// Authority stays in Core. E only starts the presentation: nothing is spent. At <see cref="MealTimeline.CommitAt"/> the
    /// real availability and the caller's policy are checked again and the captured real command runs EXACTLY ONCE per
    /// meal; its own result is published. A refusal at that point ends the meal with nothing spent. Repeated E, duplicate
    /// stage/animation notifications, re-entrant handlers and replayed or oversized frames can never call the command a
    /// second time or award anything: there is no other path to the inner command than the single guarded commit.
    /// Pause freezes the meal. A stale session, an invalid policy (cinematic, hazard, rest, suit, mission end) or an
    /// explicit <see cref="Cancel"/> stops it; before the commit nothing is spent, after it the ration stays eaten.
    /// </para>
    /// </summary>
    public sealed class MealSequence : IInteractable
    {
        public const string AlreadyEatingMessage = "Meal in progress";
        public const string InterruptedMessage = "The meal was interrupted. Nothing was eaten.";
        public const string InterruptedAfterEatingMessage = "The meal was interrupted. The ration was already eaten.";

        private readonly Func<bool> stillValid;
        private readonly Func<bool> paused;
        private readonly Func<string> blocker;
        private readonly Func<int, MealPreparation> chooseKind;
        private readonly Func<int, bool> labelCheck;
        private bool committing, committed;
        private int attempt;

        /// <param name="inner">The real interactable of the current session (the existing <c>survival-ration</c> action).</param>
        /// <param name="stillValid">Caller's policy, checked on start, on every advance and right before the commit. Null = always.</param>
        /// <param name="paused">True while time must not count (pause menu). Null = never paused.</param>
        /// <param name="blocker">A presentation refusal shown instead of starting (for example "move closer"), or null.</param>
        /// <param name="chooseKind">Preparation for the meal with this zero-based index in the binding. Null = the default rotation.</param>
        /// <param name="labelCheck">True when this meal's menu repeats and the restrained label-reading beat plays. Null = never.</param>
        public MealSequence(IInteractable inner, Func<bool> stillValid = null, Func<bool> paused = null, Func<string> blocker = null,
            Func<int, MealPreparation> chooseKind = null, Func<int, bool> labelCheck = null)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.stillValid = stillValid;
            this.paused = paused;
            this.blocker = blocker;
            this.chooseKind = chooseKind ?? DefaultRotation;
            this.labelCheck = labelCheck ?? (index => false);
        }

        public IInteractable Inner { get; }
        public string InteractionId => Inner.InteractionId;
        public MealStage Stage { get; private set; }
        public MealPreparation Preparation { get; private set; }
        public MealTimeline Timeline { get; private set; } = MealTimeline.For(MealPreparation.ReadyToEat);
        public bool IsWorking => Stage != MealStage.Idle;
        public bool IsLabelCheck { get; private set; }

        /// <summary>The real command already succeeded for the current meal (its food is debited).</summary>
        public bool HasEaten => IsWorking && committed;

        /// <summary>Unpaused real seconds of the current meal; 0 when idle.</summary>
        public double ElapsedActiveSeconds { get; private set; }
        public double Progress01 => IsWorking ? Math.Min(1d, ElapsedActiveSeconds / Timeline.Total) : 0d;
        public double StageProgress01 => IsWorking ? Timeline.StageProgress(ElapsedActiveSeconds) : 0d;

        /// <summary>Meals whose real command succeeded through this decorator (selects the next preparation).</summary>
        public int MealsEaten { get; private set; }

        /// <summary>Times this decorator invoked the inner command (one per meal that reached its first bite).</summary>
        public int CommandsIssued { get; private set; }

        public CommandResult? LastCommandResult { get; private set; }

        /// <summary>E started a meal (nothing spent). The argument is its preparation.</summary>
        public event Action<MealPreparation> Started;

        /// <summary>The visible stage changed (informational; it never issues a command).</summary>
        public event Action<MealStage> StageChanged;

        /// <summary>The real command ran once at the first bite and succeeded; its own result.</summary>
        public event Action<CommandResult> Eaten;

        /// <summary>The whole presentation finished after a successful real command.</summary>
        public event Action<CommandResult> Finished;

        /// <summary>The meal stopped early. Arguments: why, and whether the real command had already succeeded.</summary>
        public event Action<string, bool> Cancelled;

        public string Prompt
        {
            get
            {
                if (IsWorking) return StatusLine;
                string refusal = blocker?.Invoke();
                return !string.IsNullOrEmpty(refusal) ? refusal : Inner.Prompt;
            }
        }

        public bool CanInteract => !IsWorking && string.IsNullOrEmpty(blocker?.Invoke()) && Inner.CanInteract && IsStillValid();

        /// <summary>What the crew member is doing now, for the existing prompt line.</summary>
        public string StatusLine
        {
            get
            {
                switch (Stage)
                {
                    case MealStage.Take: return "Taking a stored meal pouch from the galley drawer";
                    case MealStage.Prepare:
                        return Preparation == MealPreparation.Warmed ? "Warming the thermostabilised pouch in the food warmer"
                            : Preparation == MealPreparation.Rehydrated ? "Rehydrating the pouch at the water dispenser"
                            : "Ready to eat: no preparation needed";
                    case MealStage.Open: return IsLabelCheck ? "Same menu again. Opening the pouch" : "Opening the pouch";
                    case MealStage.Eat: return "Eating the ration";
                    case MealStage.Stow: return "Stowing the empty pouch";
                    default: return string.Empty;
                }
            }
        }

        /// <summary>
        /// E: starts a meal when the real ration is available (nothing spent). Otherwise its refusal (or the presentation
        /// blocker), without calling it. While a meal runs, a repeated E is refused and changes nothing.
        /// </summary>
        public CommandResult TryInteract()
        {
            if (IsWorking) return Refuse(AlreadyEatingMessage);
            string refusal = blocker?.Invoke();
            if (!string.IsNullOrEmpty(refusal)) return Refuse(refusal);
            if (!Inner.CanInteract) return Refuse(string.IsNullOrEmpty(Inner.Prompt) ? "Not available now." : Inner.Prompt);
            if (!IsStillValid()) return Refuse("The meal cannot start now.");

            MealPreparation kind = chooseKind(MealsEaten);
            bool check = labelCheck(MealsEaten);
            Preparation = kind;
            IsLabelCheck = check;
            Timeline = MealTimeline.For(kind, check);
            ElapsedActiveSeconds = 0d;
            committed = false;
            Stage = MealStage.Take;
            int started = ++attempt;
            Started?.Invoke(kind);
            if (started != attempt || !IsWorking) return Refuse("Meal cancelled.");
            StageChanged?.Invoke(Stage);
            if (started != attempt || !IsWorking) return Refuse("Meal cancelled.");
            return new CommandResult(true, CommandFailure.None,
                string.Format(CultureInfo.InvariantCulture, "Meal started: {0}, {1:0.0} s", Describe(kind), Timeline.Total));
        }

        /// <summary>
        /// Adds unpaused real time while a meal runs (call it every presented frame). Ignored while paused; an invalid
        /// policy cancels even while paused. Crossing the first bite runs the real command once, after a fresh check.
        /// </summary>
        public void Advance(double activeRealSeconds)
        {
            if (double.IsNaN(activeRealSeconds) || double.IsInfinity(activeRealSeconds) || activeRealSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(activeRealSeconds), activeRealSeconds, "Real time must be finite and non-negative.");
            if (!IsWorking || committing) return;
            if (!IsStillValid()) { Cancel(committed ? InterruptedAfterEatingMessage : InterruptedMessage); return; }
            if (paused != null && paused()) return;

            int current = attempt;
            double next = Math.Min(Timeline.Total, ElapsedActiveSeconds + activeRealSeconds);
            if (!committed && next >= Timeline.CommitAt)
            {
                // Stop exactly at the first bite for this frame; nothing after it is shown before Core answers.
                ElapsedActiveSeconds = Timeline.CommitAt;
                Commit(current);
                if (current != attempt || !IsWorking) return;
            }
            else ElapsedActiveSeconds = next;

            MealStage stage = Timeline.StageAt(ElapsedActiveSeconds);
            if (stage != Stage)
            {
                Stage = stage;
                StageChanged?.Invoke(stage);
                if (current != attempt || !IsWorking) return;
            }

            if (committed && ElapsedActiveSeconds >= Timeline.Total) Complete(current);
        }

        /// <summary>Stops the meal early (restart, new session, hazard, view lost). Before the first bite nothing is spent.</summary>
        public void Cancel(string reason)
        {
            if (!IsWorking || committing) return;
            bool eaten = committed;
            Reset();
            Cancelled?.Invoke(string.IsNullOrEmpty(reason) ? (eaten ? InterruptedAfterEatingMessage : InterruptedMessage) : reason, eaten);
        }

        /// <summary>The default reviewed rotation: the first packaged meal is warmed, then rehydrated, then ready to eat.</summary>
        public static MealPreparation DefaultRotation(int mealIndex)
        {
            switch (((mealIndex % 3) + 3) % 3)
            {
                case 0: return MealPreparation.Warmed;
                case 1: return MealPreparation.Rehydrated;
                default: return MealPreparation.ReadyToEat;
            }
        }

        public static string Describe(MealPreparation kind) => kind == MealPreparation.Warmed ? "thermostabilised, warmed"
            : kind == MealPreparation.Rehydrated ? "rehydratable, water added" : "ready to eat";

        private void Commit(int current)
        {
            committing = true;
            CommandResult result;
            try
            {
                // Revalidate at the first bite: phase, food store, rest, cinematics and the policy may have changed.
                if (!IsStillValid()) result = new CommandResult(false, CommandFailure.PreconditionsNotMet, InterruptedMessage);
                else if (!Inner.CanInteract)
                    result = new CommandResult(false, CommandFailure.PreconditionsNotMet, string.IsNullOrEmpty(Inner.Prompt) ? "Not available now." : Inner.Prompt);
                else
                {
                    CommandsIssued++;
                    result = Inner.TryInteract();
                }
            }
            finally { committing = false; }

            LastCommandResult = result;
            if (current != attempt || !IsWorking) return;
            if (!result.Succeeded)
            {
                Reset();
                Cancelled?.Invoke(string.IsNullOrEmpty(result.Explanation) ? InterruptedMessage : result.Explanation, false);
                return;
            }

            committed = true;
            MealsEaten++;
            Eaten?.Invoke(result);
        }

        private void Complete(int current)
        {
            if (current != attempt) return;
            CommandResult result = LastCommandResult ?? new CommandResult(true, CommandFailure.None, "Meal finished.");
            Reset();
            Finished?.Invoke(result);
        }

        private bool IsStillValid() => stillValid == null || stillValid();

        private void Reset()
        {
            Stage = MealStage.Idle;
            ElapsedActiveSeconds = 0d;
            committed = false;
            IsLabelCheck = false;
            attempt++;
        }

        private static CommandResult Refuse(string text) => new CommandResult(false, CommandFailure.PreconditionsNotMet, text);
    }
}
