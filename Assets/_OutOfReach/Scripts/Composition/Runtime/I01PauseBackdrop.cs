using OutOfReach.Contracts;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OutOfReach.Composition
{
    /// <summary>Camera-only pause focus. The mission clock and gameplay remain untouched.</summary>
    public sealed class I01PauseBackdrop : MonoBehaviour
    {
        private ISimulationClock clock;
        private IGameState state;
        private UniversalAdditionalCameraData cameraData;
        private Volume volume;
        private VolumeProfile profile;
        private bool originalPostProcessing;

        public void Configure(Camera camera, ISimulationClock simulationClock, IGameState gameState)
        {
            ClearBinding();
            if (!camera || simulationClock == null || gameState == null) return;
            clock = simulationClock;
            state = gameState;
            cameraData = camera.GetUniversalAdditionalCameraData();
            originalPostProcessing = cameraData.renderPostProcessing;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Transient pause focus";
            var focus = profile.Add<DepthOfField>(true);
            focus.mode.Override(DepthOfFieldMode.Gaussian);
            focus.gaussianStart.Override(.2f);
            focus.gaussianEnd.Override(1f);
            focus.gaussianMaxRadius.Override(1.15f);
            focus.highQualitySampling.Override(true);
            var holder = new GameObject("Pause background focus");
            holder.transform.SetParent(transform, false);
            holder.layer = 0;
            volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000;
            volume.weight = 0;
            volume.sharedProfile = profile;
        }

        private void LateUpdate()
        {
            if (!volume || !cameraData || clock == null || state == null) return;
            bool pausedView = state.Phase == GamePhase.Report
                || (clock.IsPaused && state.Phase != GamePhase.Outcome);
            // Resume restores crisp gameplay immediately; only the menu entrance eases in.
            float weight = state.Phase == GamePhase.Report ? .5f : 1f;
            volume.weight = pausedView ? Mathf.MoveTowards(volume.weight, weight, Time.unscaledDeltaTime * 5f) : 0f;
            cameraData.renderPostProcessing = originalPostProcessing || volume.weight > 0f;
        }

        public void ClearBinding()
        {
            if (cameraData) cameraData.renderPostProcessing = originalPostProcessing;
            if (volume)
            {
                volume.weight = 0;
                volume.sharedProfile = null;
                Destroy(volume.gameObject);
            }
            if (profile)
            {
                foreach (var effect in profile.components) if (effect) Destroy(effect);
                Destroy(profile);
            }
            volume = null;
            profile = null;
            cameraData = null;
            clock = null;
            state = null;
        }

        private void OnDisable() => ClearBinding();
        private void OnDestroy() => ClearBinding();
    }
}
