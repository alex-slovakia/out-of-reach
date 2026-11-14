using System;
using OutOfReach.Player;
using UnityEngine.InputSystem;

namespace OutOfReach.Composition
{
    /// <summary>Root-owned lease of the default mouse Interact binding; never owns a controller or input map.</summary>
    public sealed class MicroInputLease : IDisposable
    {
        private const string MousePath = "<Mouse>/leftButton";
        private PlayerInputActions source;
        private InputActionMap map;
        private InputAction interact;
        private InputBinding previous;
        private bool acquired, lost;

        /// <summary>False if another binding writer has replaced any leased value. Does not reacquire a lost lease.</summary>
        public bool IsOwned
        {
            get
            {
                if (!acquired || lost) return false;
                if (OwnedIndex() >= 0) return true;
                lost = true;
                return false;
            }
        }

        /// <summary>
        /// Call at the root's ownership transition, before the next input update. Unknown/scripted input is refused.
        /// Repeated calls for the same source are idempotent; lost ownership needs an explicit Release first.
        /// </summary>
        public bool TryAcquire(IPlayerInputSource currentSource)
        {
            var candidate = currentSource as PlayerInputActions;
            if (ReferenceEquals(candidate, source) && acquired) return IsOwned;
            Release();
            if (candidate == null || candidate.Map == null || candidate.Map.name != PlayerInputActions.MapName) return false;
            InputAction action = candidate.Map.FindAction("Interact", false);
            if (action == null || action.type != InputActionType.Button) return false;
            int index = -1;
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];
                if (!SamePath(binding.path, MousePath))
                {
                    // A second left-click Interact route would defeat exclusive contact ownership.
                    if (SamePath(binding.effectivePath, MousePath)) return false;
                    continue;
                }
                if (index >= 0 || binding.isComposite || binding.isPartOfComposite || binding.id == Guid.Empty ||
                    !SamePath(binding.effectivePath, MousePath)) return false;
                index = i;
            }
            if (index < 0) return false;
            previous = bindings[index];
            InputBinding disabled = previous;
            disabled.overridePath = string.Empty;
            source = candidate; map = candidate.Map; interact = action; acquired = true; lost = false;
            try
            {
                // Indexed InputBinding overload preserves processors/interactions, including null versus empty.
                // Input System 1.20.1 permits this while enabled; no Enable/Disable call or binding-array edit.
                interact.ApplyBindingOverride(index, disabled);
                return IsOwned;
            }
            catch
            {
                Release();
                throw;
            }
        }

        /// <summary>Restore only this binding if its identity and all three override strings are still ours.</summary>
        public void Release()
        {
            int index = acquired && !lost ? OwnedIndex() : -1;
            InputAction oldAction = interact;
            InputBinding restore = previous;
            // Clear ownership before Input System can deliver synchronous cancellation/re-resolution callbacks.
            acquired = lost = false; source = null; map = null; interact = null; previous = default;
            if (index >= 0) oldAction.ApplyBindingOverride(index, restore);
        }

        public void Dispose() => Release();

        private int OwnedIndex()
        {
            if (source == null || !ReferenceEquals(source.Map, map) || interact == null ||
                !ReferenceEquals(map.FindAction("Interact", false), interact)) return -1;
            int found = -1;
            var bindings = interact.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding current = bindings[i];
                if (current.id != previous.id) continue;
                if (found >= 0 || !SameBase(current, previous) || current.overridePath != string.Empty ||
                    current.overrideProcessors != previous.overrideProcessors ||
                    current.overrideInteractions != previous.overrideInteractions) return -1;
                found = i;
            }
            return found;
        }

        private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        private static bool SameBase(InputBinding a, InputBinding b) => a.path == b.path && a.action == b.action &&
            a.name == b.name && a.groups == b.groups && a.processors == b.processors && a.interactions == b.interactions &&
            a.isComposite == b.isComposite && a.isPartOfComposite == b.isPartOfComposite;
    }
}
