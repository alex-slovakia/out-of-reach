using System;
using System.Collections.Generic;
using OutOfReach.Contracts;

namespace OutOfReach.Core
{
    /// <summary>The session's interactables by stable ID. Scene targets are bound to these domain objects.</summary>
    public sealed class InteractionCatalog
    {
        private readonly Dictionary<string, IInteractable> byId = new Dictionary<string, IInteractable>(StringComparer.Ordinal);
        private readonly List<IInteractable> ordered = new List<IInteractable>();

        internal InteractionCatalog()
        {
            All = ordered.AsReadOnly();
        }

        public IReadOnlyList<IInteractable> All { get; }

        public bool TryGet(string interactionId, out IInteractable interactable)
        {
            if (interactionId == null)
            {
                interactable = null;
                return false;
            }

            return byId.TryGetValue(interactionId, out interactable);
        }

        internal void Add(IInteractable interactable)
        {
            if (interactable == null)
            {
                throw new ArgumentNullException(nameof(interactable));
            }

            byId.Add(interactable.InteractionId, interactable);
            ordered.Add(interactable);
        }
    }
}
