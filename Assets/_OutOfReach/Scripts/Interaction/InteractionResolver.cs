using System;
using OutOfReach.Contracts;
using OutOfReach.Localization;
using UnityEngine;
using UnityEngine.Events;

namespace OutOfReach.Interaction
{
    /// <summary>
    /// Short-range interaction focus for the first-person view. Each frame it casts one ray from the view origin:
    /// the nearest hit decides. An <see cref="InteractableTarget"/> within reach becomes the focus; a solid collider
    /// in front of it blocks the interaction; trigger volumes that are not targets are ignored. The focused target's
    /// prompt and availability are exposed for a prompt view (properties, C# events and serialized UnityEvents),
    /// so no UI lives here. The action itself always re-resolves the focus first and the domain target revalidates.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionResolver : MonoBehaviour
    {
        private const float PromptRefreshSeconds = 0.1f;

        [Tooltip("Ray origin and direction (the camera pivot). Defaults to this transform.")]
        [SerializeField] private Transform viewOrigin;

        [Tooltip("Colliders under this root (the player) are ignored.")]
        [SerializeField] private Transform ignoreRoot;

        [Tooltip("Maximum interaction distance from the view origin, metres.")]
        [SerializeField] private float reachMetres = 2f;

        [Tooltip("Layers that can be targeted or can block the view.")]
        [SerializeField] private LayerMask blockingLayers = Physics.DefaultRaycastLayers;

        [Tooltip("Raised when the focus, its prompt or its availability changes: prompt text (empty = nothing in focus), can interact.")]
        [SerializeField] private UnityEvent<string, bool> promptChanged = new UnityEvent<string, bool>();

        [Tooltip("Raised after every interaction attempt: succeeded, explanation.")]
        [SerializeField] private UnityEvent<bool, string> interactionAttempted = new UnityEvent<bool, string>();

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private float nextPromptRefreshTime;

        /// <summary>The target in focus, or null.</summary>
        public InteractableTarget CurrentTarget { get; private set; }

        /// <summary>The focused domain interactable, or null.</summary>
        public IInteractable Current => CurrentTarget != null ? CurrentTarget.Interactable : null;

        /// <summary>Prompt of the focused target; empty when nothing is in focus.</summary>
        public string CurrentPrompt { get; private set; } = string.Empty;

        public bool CurrentCanInteract { get; private set; }

        public CommandResult? LastResult { get; private set; }

        /// <summary>False while the player has no control: the focus is cleared and nothing can be targeted.</summary>
        public bool ResolutionEnabled { get; set; } = true;

        public float ReachMetres => reachMetres;

        /// <summary>Raised when the focus, its prompt or its availability changes.</summary>
        public event Action FocusChanged;

        /// <summary>Raised after every interaction attempt with the authoritative result.</summary>
        public event Action<CommandResult> Interacted;

        public UnityEvent<string, bool> PromptChanged => promptChanged;

        public UnityEvent<bool, string> InteractionAttempted => interactionAttempted;

        /// <summary>Explicit wiring for code-built rigs; scenes normally use the serialized fields.</summary>
        public void Configure(Transform origin, Transform ignoredRoot, float reach)
        {
            viewOrigin = origin;
            ignoreRoot = ignoredRoot;
            reachMetres = Mathf.Max(0f, reach);
        }

        /// <summary>Re-resolves the focus and its prompt immediately.</summary>
        public void Refresh()
        {
            nextPromptRefreshTime = Time.unscaledTime + PromptRefreshSeconds;
            InteractableTarget target = ResolutionEnabled ? FindTarget() : null;
            IInteractable interactable = target != null ? target.Interactable : null;
            // LANG-01: the domain prompt is English; it is shown in the player's language.
            string prompt = interactable != null ? GameText.Translate(interactable.Prompt ?? string.Empty) : string.Empty;
            bool canInteract = interactable != null && interactable.CanInteract;
            if (target == CurrentTarget && canInteract == CurrentCanInteract && string.Equals(prompt, CurrentPrompt, StringComparison.Ordinal))
            {
                return;
            }

            CurrentTarget = target;
            CurrentPrompt = prompt;
            CurrentCanInteract = canInteract;
            FocusChanged?.Invoke();
            promptChanged.Invoke(prompt, canInteract);
        }

        /// <summary>
        /// Performs the interaction on the target that is in reach and in sight right now. Nothing in focus is a
        /// rejected command; an unavailable target returns its own explanation and changes nothing.
        /// </summary>
        public CommandResult TryInteract()
        {
            Refresh();
            IInteractable interactable = Current;
            CommandResult result = interactable != null
                ? interactable.TryInteract()
                : new CommandResult(false, CommandFailure.PreconditionsNotMet, "Nothing to interact with is in reach.");
            LastResult = result;
            Interacted?.Invoke(result);
            interactionAttempted.Invoke(result.Succeeded, GameText.Translate(result.Explanation));
            Refresh();
            return result;
        }

        private void Update()
        {
            // The focus changes with the view every frame; prompt text of an unchanged focus is refreshed at 10 Hz.
            InteractableTarget target = ResolutionEnabled ? FindTarget() : null;
            if (target != CurrentTarget || Time.unscaledTime >= nextPromptRefreshTime)
            {
                Refresh();
            }
        }

        private void OnEnable()
        {
            GameText.Changed += OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            if (isActiveAndEnabled)
            {
                Refresh();
            }
        }

        private void OnDisable()
        {
            GameText.Changed -= OnLanguageChanged;
            if (CurrentTarget == null && CurrentPrompt.Length == 0)
            {
                return;
            }

            CurrentTarget = null;
            CurrentPrompt = string.Empty;
            CurrentCanInteract = false;
            FocusChanged?.Invoke();
            promptChanged.Invoke(string.Empty, false);
        }

        private InteractableTarget FindTarget()
        {
            Transform origin = viewOrigin != null ? viewOrigin : transform;
            int count = Physics.RaycastNonAlloc(origin.position, origin.forward, hits, reachMetres, blockingLayers, QueryTriggerInteraction.Collide);
            SortByDistance(count);
            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i].collider;
                if (hit == null || (ignoreRoot != null && hit.transform.IsChildOf(ignoreRoot)))
                {
                    continue;
                }

                InteractableTarget target = hit.GetComponentInParent<InteractableTarget>();
                if (target != null && target.isActiveAndEnabled)
                {
                    return target;
                }

                if (!hit.isTrigger)
                {
                    return null;
                }
            }

            return null;
        }

        private void SortByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit current = hits[i];
                int j = i - 1;
                while (j >= 0 && hits[j].distance > current.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }

                hits[j + 1] = current;
            }
        }
    }
}
