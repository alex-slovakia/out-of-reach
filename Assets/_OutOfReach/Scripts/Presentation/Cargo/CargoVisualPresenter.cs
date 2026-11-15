using System;
using System.Collections.Generic;
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
        private readonly Dictionary<GameObject, List<CaseColour>> caseColours = new Dictionary<GameObject, List<CaseColour>>();
        private readonly Dictionary<TextMesh, Color> labelColours = new Dictionary<TextMesh, Color>();
        private static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");

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
            RestoreColours();
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
            RestoreColours();
            cargo = null;
            planner = null;
            state = null;
            aboardDepartureVisible = false;
            displayedDraft = null;
            dirty = true;
            Hide();
        }

        private void OnEnable() { Subscribe(); dirty = true; Refresh(); }
        private void OnDisable() { Unsubscribe(); Hide(); RestoreColours(); }
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
                Color colour = EquipmentColour(slot.EquipmentId);
                ColourCase(slot.SupplyCase, colour);
                ColourCase(slot.AboardCase, colour);
                ColourLabel(slot.SupplyLabel, colour);
                ColourLabel(slot.AboardLabel, colour);
                int loaded = 0;
                foreach (EquipmentSelection selection in draft.Equipment)
                    if (selection.EquipmentId == slot.EquipmentId) { loaded = selection.Quantity; break; }
                int supply = planning ? cargo.GetSupplyRemaining(slot.EquipmentId) : 0;
                SetActive(slot.SupplyCase, planning && supply > 0);
                SetActive(slot.AboardCase, loaded > 0 && (planning ||
                    (aboardDepartureVisible && state.Phase == GamePhase.Intro)));
                if (slot.SupplyLabel) slot.SupplyLabel.text = slot.DisplayName + (supply > 0 ? "\nSUPPLY x" + supply : "\nEMPTY");
                if (slot.AboardLabel) slot.AboardLabel.text = slot.DisplayName + "\nABOARD x" + loaded;
                if (slot.SupplyLabel) slot.SupplyLabel.gameObject.SetActive(planning && showStationLabels);
                if (slot.AboardLabel) slot.AboardLabel.gameObject.SetActive(planning && loaded > 0 && showStationLabels);
            }

            SetActive(carriedCaseRoot, !string.IsNullOrEmpty(held));
            if (!string.IsNullOrEmpty(held))
            {
                Color colour = EquipmentColour(held);
                ColourCase(carriedCaseRoot, colour);
                ColourLabel(carriedLabel, colour);
            }
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

        private static Color EquipmentColour(string equipmentId)
        {
            switch (equipmentId)
            {
                case "water-tank": return new Color(.08f, .48f, .62f);
                case "food-locker": return new Color(.37f, .49f, .16f);
                case "battery-module": return new Color(.79f, .40f, .055f);
                case "oxygen-tank": return new Color(.68f, .76f, .77f);
                case "solar-wing": return new Color(.18f, .29f, .52f);
                case "spare-parts-kit": return new Color(.57f, .19f, .12f);
                case "spectrometer": return new Color(.39f, .28f, .49f);
                default: return new Color(.27f, .29f, .27f);
            }
        }

        private void ColourCase(GameObject root, Color colour)
        {
            if (!root) return;
            if (!caseColours.TryGetValue(root, out List<CaseColour> bindings))
            {
                bindings = new List<CaseColour>();
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material material = materials[i];
                        if (!material || (material.name != "MAT_Cargo_OffWhite" && material.name != "MAT_Cargo_Amber")) continue;
                        bindings.Add(new CaseColour(renderer, i, material.name == "MAT_Cargo_Amber"));
                    }
                }
                caseColours.Add(root, bindings);
            }
            foreach (CaseColour binding in bindings) binding.Set(colour);
        }

        private void ColourLabel(TextMesh label, Color colour)
        {
            if (!label) return;
            if (!labelColours.ContainsKey(label)) labelColours.Add(label, label.color);
            label.color = Color.Lerp(colour, Color.white, .72f);
        }

        private void RestoreColours()
        {
            foreach (List<CaseColour> bindings in caseColours.Values)
                foreach (CaseColour binding in bindings) binding.Restore();
            caseColours.Clear();
            foreach (KeyValuePair<TextMesh, Color> binding in labelColours)
                if (binding.Key) binding.Key.color = binding.Value;
            labelColours.Clear();
        }

        private sealed class CaseColour
        {
            private readonly Renderer renderer;
            private readonly int index;
            private readonly bool accent;
            private readonly MaterialPropertyBlock original = new MaterialPropertyBlock();
            private readonly MaterialPropertyBlock displayed = new MaterialPropertyBlock();

            public CaseColour(Renderer renderer, int index, bool accent)
            {
                this.renderer = renderer;
                this.index = index;
                this.accent = accent;
                renderer.GetPropertyBlock(original, index);
                renderer.GetPropertyBlock(displayed, index);
            }

            public void Set(Color colour)
            {
                if (!renderer) return;
                displayed.SetColor(BaseColourId, accent ? Color.Lerp(colour, Color.white, .24f) : colour);
                renderer.SetPropertyBlock(displayed, index);
            }

            public void Restore()
            {
                if (renderer) renderer.SetPropertyBlock(original.isEmpty ? null : original, index);
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target && target.activeSelf != active) target.SetActive(active);
        }
    }
}
