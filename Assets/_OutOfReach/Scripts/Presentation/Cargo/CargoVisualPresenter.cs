using System;
using System.Globalization;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Cargo
{
    /// <summary>Read-only cargo display. Core owns quantities, costs, deadlines and movement.</summary>
    [DisallowMultipleComponent]
    public sealed class CargoVisualPresenter : MonoBehaviour
    {
        [Serializable]
        public sealed class Slot
        {
            public string EquipmentId;
            public string DisplayName;
            public GameObject SupplyCase;
            public GameObject AboardCase;
            public TextMesh SupplyLabel;
            public TextMesh AboardLabel;
            public Transform DestinationAnchor;
        }

        [SerializeField] private GameObject temporarySupplyRoot;
        [SerializeField] private GameObject carriedCaseRoot;
        [SerializeField] private TextMesh carriedLabel;
        [SerializeField] private Slot[] slots = Array.Empty<Slot>();
        [SerializeField] private Collider[] cargoTargetColliders = Array.Empty<Collider>();
        private ICargoLoading cargo;
        private IMissionPlanner planner;
        private IGameState state;
        private ICargoLoading subscribedCargo;
        private IMissionConfiguration displayedDraft;
        private GamePhase displayedPhase;
        private bool dirty = true;
        private bool menuOverlayVisible;
        private bool aboardDepartureVisible;

        // Root leases only the already loaded visual cases during the transport departure.
        // Quantities still come from Core's sealed draft; supplies and targets remain closed.
        public void SetAboardDepartureVisible(bool visible)
        {
            if (aboardDepartureVisible == visible) return;
            aboardDepartureVisible = visible;
            dirty = true;
            Refresh();
        }

        public bool TryGetCarriedDestination(out Vector3 position)
        {
            position = default;
            if (!isActiveAndEnabled || menuOverlayVisible || cargo == null || state == null
                || state.Phase != GamePhase.Planning || string.IsNullOrEmpty(cargo.CarriedEquipmentId)) return false;
            foreach (Slot slot in slots)
            {
                if (slot == null || slot.EquipmentId != cargo.CarriedEquipmentId || !slot.DestinationAnchor) continue;
                position = slot.DestinationAnchor.position;
                return true;
            }
            return false;
        }

        public void SetMenuOverlayVisible(bool visible)
        {
            if (menuOverlayVisible == visible) return;
            menuOverlayVisible = visible;
            dirty = true;
            Refresh();
        }

        public void ConfigureScene(GameObject supplyRoot, GameObject carriedRoot, TextMesh heldLabel,
            Slot[] visualSlots, Collider[] targetColliders)
        {
            temporarySupplyRoot = supplyRoot;
            carriedCaseRoot = carriedRoot;
            carriedLabel = heldLabel;
            slots = visualSlots ?? Array.Empty<Slot>();
            cargoTargetColliders = targetColliders ?? Array.Empty<Collider>();
            dirty = true;
        }

        public void Configure(ICargoLoading cargoView, IMissionPlanner missionPlanner, IGameState gameState)
        {
            Unsubscribe();
            cargo = cargoView;
            planner = missionPlanner;
            state = gameState;
            aboardDepartureVisible = false;
            displayedDraft = null;
            dirty = true;
            if (isActiveAndEnabled) { Subscribe(); Refresh(); }
        }

        public void ClearBinding()
        {
            Unsubscribe();
            cargo = null;
            planner = null;
            state = null;
            aboardDepartureVisible = false;
            displayedDraft = null;
            dirty = true;
            Hide();
        }

        private void OnEnable() { Subscribe(); dirty = true; Refresh(); }
        private void OnDisable() { Unsubscribe(); Hide(); }
        private void OnDestroy() => ClearBinding();
        private void OnCargoChanged() => dirty = true;

        private void Subscribe()
        {
            if (subscribedCargo != null || cargo == null) return;
            subscribedCargo = cargo;
            subscribedCargo.Changed += OnCargoChanged;
        }

        private void Unsubscribe()
        {
            if (subscribedCargo != null) subscribedCargo.Changed -= OnCargoChanged;
            subscribedCargo = null;
        }

        private void LateUpdate()
        {
            if (cargo == null || planner == null || state == null) return;
            // Phase is polled because IGameState has no event; immutable Draft is an invalidation token only.
            // Core replaces Draft on deadline fallback before publishing the launch phase.
            if (dirty || displayedPhase != state.Phase || !ReferenceEquals(displayedDraft, planner.Draft)) Refresh();
        }

        private void Refresh()
        {
            if (cargo == null || planner == null || state == null) { Hide(); return; }
            IMissionConfiguration draft = planner.Draft;
            if (draft == null) { Hide(); return; }
            bool planning = state.Phase == GamePhase.Planning;
            string held = planning ? cargo.CarriedEquipmentId : null;
            bool showStationLabels = string.IsNullOrEmpty(held) && !menuOverlayVisible;
            SetActive(temporarySupplyRoot, planning);
            SetTargets(planning);
            foreach (Slot slot in slots)
            {
                if (slot == null) continue;
                int loaded = 0;
                foreach (EquipmentSelection selection in draft.Equipment)
                    if (selection.EquipmentId == slot.EquipmentId) { loaded = selection.Quantity; break; }
                int supply = planning ? cargo.GetSupplyRemaining(slot.EquipmentId) : 0;
                SetActive(slot.SupplyCase, planning && supply > 0);
                SetActive(slot.AboardCase, loaded > 0 && (planning ||
                    (aboardDepartureVisible && state.Phase == GamePhase.Intro)));
                if (slot.SupplyLabel) slot.SupplyLabel.text = slot.DisplayName + "\nSUPPLY x" + supply;
                if (slot.AboardLabel) slot.AboardLabel.text = slot.DisplayName + "\nABOARD x" + loaded;
                if (slot.SupplyLabel) slot.SupplyLabel.gameObject.SetActive(planning && supply > 0 && showStationLabels);
                if (slot.AboardLabel) slot.AboardLabel.gameObject.SetActive(planning && loaded > 0 && showStationLabels);
            }

            SetActive(carriedCaseRoot, !string.IsNullOrEmpty(held));
            if (carriedLabel && !string.IsNullOrEmpty(held))
            {
                double mass = 0;
                foreach (EquipmentSelection item in planner.Catalog)
                    if (item.EquipmentId == held) { mass = item.UnitMassKg; break; }
                string name = held;
                foreach (Slot slot in slots)
                    if (slot != null && slot.EquipmentId == held) { name = slot.DisplayName; break; }
                carriedLabel.text = string.Format(CultureInfo.InvariantCulture,
                    "{0}\n{1:0} kg", name, mass);
            }
            displayedDraft = draft;
            displayedPhase = state.Phase;
            dirty = false;
        }

        private void Hide()
        {
            SetActive(temporarySupplyRoot, false);
            SetActive(carriedCaseRoot, false);
            SetTargets(false);
            foreach (Slot slot in slots)
            {
                if (slot == null) continue;
                SetActive(slot.SupplyCase, false);
                SetActive(slot.AboardCase, false);
                if (slot.AboardLabel) slot.AboardLabel.gameObject.SetActive(false);
            }
        }

        private void SetTargets(bool enabled)
        {
            foreach (Collider target in cargoTargetColliders) if (target) target.enabled = enabled;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target && target.activeSelf != active) target.SetActive(active);
        }
    }
}
