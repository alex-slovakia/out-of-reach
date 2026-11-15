using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutOfReach.Presentation.Rescue
{
    /// <summary>
    /// The one owner of the deep-view star field in the mission scene. Bootstrap's saved space skybox is
    /// NasaDeepSky.mat (NASA SVS Deep Star Maps 2020) at exposure 0.15, where the stars are effectively invisible: the
    /// Earth-orbit shot of the departure and the whole EVA looked out on plain black. Rule, with no game state at all:
    /// while that saved sky is the active skybox, a runtime copy of it at the existing deep-view exposure 0.45
    /// (NasaDeepSky_DeepView.mat) is shown instead.
    ///
    /// Same mechanism as the rescue ending (<see cref="RescueEndingSky"/>): only RenderSettings.skybox is swapped; no
    /// asset, scene, light, ambient or reflection setting is touched (Bootstrap's ambient is flat colour). Any other
    /// skybox (the launch site on Earth, the upper-atmosphere transit) or none is left alone, so a restart needs no
    /// handling. There is no transition, so pause and reduced effects need none either.
    ///
    /// The rescue ending asks this owner first (<see cref="HoldNow"/>) and then finds the sky already at the deep
    /// view, so its own copy is never created while this one holds: the two never write in the same run.
    ///
    /// Installs itself in the Bootstrap scene only (no installer, no scene edit). Fails open.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class SpaceSkyKeeper : MonoBehaviour
    {
        public const string MissionSceneName = "Bootstrap";

        /// <summary>Kill switch and QA comparison toggle: false puts the saved sky back on the next frame.</summary>
        public static bool Enabled = true;
        public static SpaceSkyKeeper Instance { get; private set; }

        private readonly RescueEndingSky sky = new RescueEndingSky();
        private bool faulted;

        /// <summary>True while this owner's copy is the active skybox.</summary>
        public bool IsHolding => sky.IsRaised;
        /// <summary>Exposure shown by the copy (0 when not holding).</summary>
        public float Exposure => sky.Exposure;
        /// <summary>The saved skybox the copy stands in for (null when not holding).</summary>
        public Material SavedSky => sky.SavedSky;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Enabled = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SceneManager.GetActiveScene().name == MissionSceneName) Create();
        }

        /// <summary>Creates the single instance (the mission scene does this by itself; tests call it directly).</summary>
        public static SpaceSkyKeeper Create()
        {
            if (Instance) return Instance;
            var host = new GameObject("SpaceSkyKeeper (NASA deep sky)");
            DontDestroyOnLoad(host);
            Instance = host.AddComponent<SpaceSkyKeeper>();
            return Instance;
        }

        /// <summary>Applies the rule immediately. The rescue ending calls this before it looks at the sky.</summary>
        public static void HoldNow()
        {
            if (Instance) Instance.Hold();
        }

        private void LateUpdate() => Hold();

        private void Hold()
        {
            if (faulted) return;
            try
            {
                if (!Enabled || !isActiveAndEnabled) { sky.Keep(false); return; }
                // Raise() is a no-op while the copy is up, re-raises after another system assigned the saved sky
                // again (orbit entry, the environment restore after boarding) and refuses every other skybox.
                if (sky.Raise()) sky.SetBlend(1f);
            }
            catch (Exception exception)
            {
                // Fail open: the mission keeps its saved sky.
                faulted = true;
                Debug.LogWarning("[SpaceSkyKeeper] disabled after an unexpected failure: " + exception.Message);
                try { sky.Lower(); } catch (Exception) { /* nothing left to put back safely */ }
                enabled = false;
            }
        }

        private void OnDisable() => sky.Lower();

        private void OnDestroy()
        {
            sky.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}
