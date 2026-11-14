using System;
using OutOfReach.Contracts;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// A repair point for one ship system (implements <see cref="IInteractable"/>). The prompt always states the
    /// real requirement or the exact reason the repair is unavailable; the action itself revalidates everything.
    /// </summary>
    public sealed class RepairInteraction : IInteractable
    {
        private readonly ShipSystem system;
        private readonly IResourceSystem resources;

        public RepairInteraction(string interactionId, ShipSystem system, IResourceSystem resources)
        {
            if (string.IsNullOrWhiteSpace(interactionId))
            {
                throw new ArgumentException("An interaction ID is required.", nameof(interactionId));
            }

            InteractionId = interactionId;
            this.system = system ?? throw new ArgumentNullException(nameof(system));
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        public string InteractionId { get; }

        /// <summary>The system this point repairs, for diagnostics views.</summary>
        public IRepairable Repairable => system;

        public string Prompt
        {
            get
            {
                if (system.IsOperational)
                {
                    return system.WasDamaged ? Capitalize(system.DisplayName) + ": repaired" : Capitalize(system.DisplayName) + ": nominal";
                }

                return system.CheckRepair(resources).Explanation;
            }
        }

        public bool CanInteract => system.CheckRepair(resources).Succeeded;

        public CommandResult TryInteract()
        {
            return system.TryRepair(resources);
        }

        private static string Capitalize(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }
}
