using System;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Lighting
{
    /// <summary>One authored helmet work light, observing the actual suit/exterior session. No power/resource authority.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(290)]
    public sealed class EvaWorkLampPresenter : MonoBehaviour
    {
        [SerializeField] private Light workLight;
        [SerializeField] private Camera firstPersonCamera;
        private IEvaStatus eva;
        private IGameState state;
        private ISimulationClock clock;
        private Func<bool> contextAllowed;
        private Vector3 authoredLocalPosition, lastWorldPosition;
        private Quaternion authoredLocalRotation, lastWorldRotation;
        private bool hasPresented, frozen;
        private int bindingVersion;
        public bool IsLit => workLight && workLight.enabled;

        public void ConfigureViews(Light authoredSpot, Camera actualFirstPersonCamera)
        {
            ClearBinding();
            if (!authoredSpot || !actualFirstPersonCamera || authoredSpot.type != LightType.Spot
                || authoredSpot.transform.parent != actualFirstPersonCamera.transform
                || authoredSpot.range <= 0f || authoredSpot.range > 3f)
                throw new ArgumentException("Use one authored spotlight directly below the actual camera, range at most 3 metres.");
            workLight = authoredSpot; firstPersonCamera = actualFirstPersonCamera;
            authoredLocalPosition = authoredSpot.transform.localPosition;
            authoredLocalRotation = authoredSpot.transform.localRotation;
            workLight.enabled = false;
        }

        /// <summary>Root supplies a current-session first-person context that remains valid on an ordinary pause.</summary>
        public void ConfigureSession(IEvaStatus actualEva, IGameState actualState, ISimulationClock actualClock, Func<bool> validContext)
        {
            ClearBinding();
            if (!workLight || !firstPersonCamera || actualEva == null || actualState == null || actualClock == null || validContext == null) return;
            eva = actualEva; state = actualState; clock = actualClock; contextAllowed = validContext;
            eva.Changed += Refresh;
            Refresh();
        }

        public void ClearBinding()
        {
            bindingVersion++;
            if (eva != null) eva.Changed -= Refresh;
            eva = null; state = null; clock = null; contextAllowed = null;
            Hide();
        }

        private void Awake()
        {
            if (workLight)
            {
                authoredLocalPosition = workLight.transform.localPosition;
                authoredLocalRotation = workLight.transform.localRotation;
                workLight.enabled = false;
            }
        }
        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();
        private void OnDisable() => ClearBinding();
        private void OnDestroy() => ClearBinding();
        private void Refresh()
        {
            if (!isActiveAndEnabled || !workLight || !firstPersonCamera || !firstPersonCamera.isActiveAndEnabled
                || eva == null || state == null || clock == null || !eva.IsSuitOn || !eva.IsSuitSealed || !eva.IsOutside
                || state.Phase != GamePhase.Eva || state.Outcome != MissionOutcome.None || contextAllowed == null)
            { Hide(); return; }
            int version = bindingVersion; bool valid;
            try { valid = contextAllowed(); }
            catch (Exception error) { Debug.LogException(error, this); valid = false; }
            if (version != bindingVersion) return;
            if (!valid) { Hide(); return; }
            if (clock.IsPaused)
            {
                // A pause cannot turn on a previously hidden lamp. Existing illumination and world pose freeze.
                if (!hasPresented) { workLight.enabled = false; return; }
                frozen = true;
                workLight.transform.SetPositionAndRotation(lastWorldPosition, lastWorldRotation);
                return;
            }
            if (frozen) workLight.transform.SetLocalPositionAndRotation(authoredLocalPosition, authoredLocalRotation);
            frozen = false; workLight.enabled = true; hasPresented = true;
            lastWorldPosition = workLight.transform.position; lastWorldRotation = workLight.transform.rotation;
        }
        private void Hide()
        {
            if (workLight)
            {
                workLight.enabled = false;
                if (frozen) workLight.transform.SetLocalPositionAndRotation(authoredLocalPosition, authoredLocalRotation);
            }
            hasPresented = frozen = false;
        }
    }
}
