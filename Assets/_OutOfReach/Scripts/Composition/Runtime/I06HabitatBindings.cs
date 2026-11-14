using System;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Presentation.Habitat;
using UnityEngine;

namespace OutOfReach.Composition
{
    /// <summary>Places the existing survival commands at physical habitat surfaces. No resource or time authority.</summary>
    [DisallowMultipleComponent]
    public sealed class I06HabitatBindings : MonoBehaviour
    {
        [SerializeField] private HabitatStatusPresenter status;
        [SerializeField] private InteractableTarget growTarget;
        [SerializeField] private InteractableTarget[] targets = Array.Empty<InteractableTarget>();
        private MissionRuntime runtime;
        private ISurvivalExtension survival;
        private ISurvivalRecovery recovery;
        private bool controlsVisible;

        public void ConfigureViews(HabitatStatusPresenter view, InteractableTarget grow, InteractableTarget[] physicalTargets)
        {
            ClearBinding();
            status = view; growTarget = grow; targets = physicalTargets ?? Array.Empty<InteractableTarget>();
        }

        public void ConfigureSession(MissionRuntime actualRuntime)
        {
            ClearBinding();
            if (!actualRuntime || actualRuntime.Survival == null || actualRuntime.SurvivalRecovery == null) return;
            runtime = actualRuntime; survival = runtime.Survival; recovery = runtime.SurvivalRecovery;
            if (status) status.ConfigureSession(survival, recovery);
            survival.Changed += Refresh;
            recovery.Changed += Refresh;
            Refresh();
        }

        private void Update()
        {
            // Phase can change without a need/resource change. Only refresh on that boundary.
            if (runtime && controlsVisible != ShouldExposeControls()) Refresh();
        }

        private bool ShouldExposeControls() => isActiveAndEnabled && runtime && survival != null && survival.Enabled
            && runtime.GameState != null && runtime.GameState.Phase == GamePhase.Survival;

        private void Refresh()
        {
            if (!runtime || survival == null) return;
            // One physical grow target, one actual registered domain action, no overlapping triggers.
            string growId = survival.GrowStage == GrowModuleStage.Unbuilt ? InteractionIds.BuildGrowModule
                : survival.GrowStage == GrowModuleStage.ReadyToPlant ? InteractionIds.PlantSeedlings : InteractionIds.HarvestCrop;
            if (growTarget) growTarget.InteractionId = growId;
            controlsVisible = ShouldExposeControls();
            foreach (InteractableTarget target in targets)
            {
                if (!target) continue;
                if (runtime.TryGetInteractable(target.InteractionId, out IInteractable action)) target.Bind(action);
                else target.Unbind();
                foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
                    collider.enabled = controlsVisible;
            }
        }

        public void ClearBinding()
        {
            if (survival != null) survival.Changed -= Refresh;
            if (recovery != null) recovery.Changed -= Refresh;
            survival = null; recovery = null; runtime = null; controlsVisible = false;
            if (status) status.ClearBinding();
            foreach (InteractableTarget target in targets)
            {
                if (!target) continue;
                target.Unbind();
                foreach (Collider collider in target.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            }
            if (growTarget) growTarget.InteractionId = InteractionIds.BuildGrowModule;
        }

        private void OnDisable() => ClearBinding();
        private void OnDestroy() => ClearBinding();
    }
}
