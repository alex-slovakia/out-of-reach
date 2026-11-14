using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Interaction
{
    /// <summary>
    /// Marks the colliders on this object (and its children) as one interaction point. It holds no gameplay state:
    /// the composition root binds it to the authoritative domain <see cref="IInteractable"/> with the same stable ID,
    /// and rebinds it when a new mission session starts. Place it on a gameplay child at a visual anchor; a trigger
    /// collider keeps the point targetable without blocking movement.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractableTarget : MonoBehaviour
    {
        [Tooltip("Stable interaction ID, for example cockpit-console, repair-power or repair-life-support.")]
        [SerializeField] private string interactionId = string.Empty;

        public string InteractionId
        {
            get { return interactionId; }
            set { interactionId = value ?? string.Empty; }
        }

        /// <summary>The bound domain object, or null while no mission session is bound.</summary>
        public IInteractable Interactable { get; private set; }

        public bool IsBound => Interactable != null;

        public void Bind(IInteractable interactable)
        {
            Interactable = interactable;
        }

        public void Unbind()
        {
            Interactable = null;
        }
    }
}
