using System.Collections.Generic;
using OutOfReach.Presentation.Cinematics;
using UnityEngine;

namespace OutOfReach.Presentation.LaunchGrassV04
{
    /// <summary>
    /// Launch shot fix (Alex's playtest: a dashed white near-vertical line in the sky beside the climbing vehicle).
    /// The pad's lightning protection (LC1_Lightning: 40 m lattice masts with amber/white bands and thin catenary
    /// wires) is thinner than a pixel from the departure cameras, so only its white bands survive and read as a
    /// dashed line. While the departure plays, those renderers are switched off; they come back afterwards.
    /// Self-installing at runtime (no scene edit), presentation only: no collider, object or gameplay state is
    /// touched, and only renderers this component switched off are switched back on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LaunchShotThinGeometryHider : MonoBehaviour
    {
        /// <summary>Objects whose name starts with one of these are hidden during the departure.</summary>
        private static readonly string[] NamePrefixes = { "LC1_Lightning" };

        private TransportDeparturePresenter departure;
        private readonly List<Renderer> targets = new List<Renderer>();
        private readonly List<Renderer> hidden = new List<Renderer>();
        private float nextSearch;
        private bool hiding;

        public int TargetCount => targets.Count;
        public bool IsHiding => hiding;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<LaunchShotThinGeometryHider>(FindObjectsInactive.Include)) return;
            var go = new GameObject("Launch shot thin geometry hider (runtime)") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<LaunchShotThinGeometryHider>();
        }

        private void Search()
        {
            nextSearch = Time.unscaledTime + 2f;
            if (!departure) departure = FindFirstObjectByType<TransportDeparturePresenter>(FindObjectsInactive.Include);
            targets.Clear();
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                for (Transform t = renderer.transform; t; t = t.parent)
                {
                    bool match = false;
                    foreach (string prefix in NamePrefixes)
                        if (t.name.StartsWith(prefix, System.StringComparison.Ordinal)) { match = true; break; }
                    if (!match) continue;
                    targets.Add(renderer);
                    break;
                }
            }
        }

        private void LateUpdate()
        {
            if ((!departure || targets.Count == 0) && Time.unscaledTime >= nextSearch && !hiding) Search();
            bool hide = departure && departure.IsPlaying;
            if (hide == hiding) return;
            hiding = hide;
            if (hide)
            {
                hidden.Clear();
                foreach (Renderer renderer in targets)
                    if (renderer && renderer.enabled) { renderer.enabled = false; hidden.Add(renderer); }
            }
            else Restore();
        }

        private void Restore()
        {
            foreach (Renderer renderer in hidden) if (renderer) renderer.enabled = true;
            hidden.Clear();
        }

        private void OnDisable()
        {
            Restore();
            hiding = false;
        }
    }
}
