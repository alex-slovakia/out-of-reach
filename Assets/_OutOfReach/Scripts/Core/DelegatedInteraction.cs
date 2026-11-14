using System;
using OutOfReach.Contracts;

namespace OutOfReach.Core
{
    /// <summary>
    /// An <see cref="IInteractable"/> backed by one authoritative command and its side-effect-free check.
    /// The prompt is the check's explanation (the real requirement or the exact reason it is unavailable) unless
    /// a status line overrides it, for example while an airlock transition is running.
    /// </summary>
    public sealed class DelegatedInteraction : IInteractable
    {
        private readonly Func<CommandResult> check;
        private readonly Func<CommandResult> execute;
        private readonly Func<string> status;

        public DelegatedInteraction(string interactionId, Func<CommandResult> check, Func<CommandResult> execute, Func<string> status = null)
        {
            if (string.IsNullOrWhiteSpace(interactionId))
            {
                throw new ArgumentException("An interaction ID is required.", nameof(interactionId));
            }

            InteractionId = interactionId;
            this.check = check ?? throw new ArgumentNullException(nameof(check));
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.status = status;
        }

        public string InteractionId { get; }

        public string Prompt
        {
            get
            {
                string line = status != null ? status() : null;
                return !string.IsNullOrEmpty(line) ? line : check().Explanation;
            }
        }

        public bool CanInteract => check().Succeeded;

        public CommandResult TryInteract()
        {
            return execute();
        }
    }
}
