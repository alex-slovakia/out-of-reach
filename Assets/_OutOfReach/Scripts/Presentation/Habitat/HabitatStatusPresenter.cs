using System;
using System.Globalization;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Habitat
{
    /// <summary>Read-only habitat displays and authored seedlings. No calendar, resource, growth or recovery authority.</summary>
    [DisallowMultipleComponent]
    public sealed class HabitatStatusPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject seedlings;
        [SerializeField] private TextMesh dayReadout, growReadout;
        private ShipLabelAtlasBinder dayAtlas, growAtlas;
        // R23: runtime-built mature crop beside the authored seedlings; shown only at Core's HarvestReady stage.
        private GameObject matureCrop, matureCropFor;
        private ISurvivalExtension survival;
        private ISurvivalRecovery recovery;
        private bool subscribed;
        public bool IsBound => survival != null && recovery != null;

        public void ConfigureScene(GameObject authoredSeedlings, TextMesh missionDayText, TextMesh cropStateText)
        {
            ClearBinding();
            if (!authoredSeedlings || !missionDayText || !cropStateText)
                throw new ArgumentException("Bind authored seedlings and the two dedicated habitat readouts.");
            seedlings = authoredSeedlings; dayReadout = missionDayText; growReadout = cropStateText;
            dayAtlas = dayReadout.GetComponent<ShipLabelAtlasBinder>();
            growAtlas = growReadout.GetComponent<ShipLabelAtlasBinder>();
            ResetPresentation();
        }

        public void ConfigureSession(ISurvivalExtension actualSurvival, ISurvivalRecovery actualRecovery)
        {
            ClearBinding();
            if (actualSurvival == null || actualRecovery == null || !seedlings || !dayReadout || !growReadout)
                throw new ArgumentException("Bind scene references and the actual survival/recovery views.");
            survival = actualSurvival; recovery = actualRecovery;
            if (isActiveAndEnabled) { Subscribe(); Refresh(); }
        }

        public void ClearBinding()
        {
            Unsubscribe(); survival = null; recovery = null; ResetPresentation();
        }
        private void Awake() => ResetPresentation();
        private void OnEnable() { if (IsBound) { Subscribe(); Refresh(); } else ResetPresentation(); }
        private void OnDisable() => ClearBinding();
        private void OnDestroy() => ClearBinding();
        private void Subscribe()
        {
            if (subscribed || !IsBound) return;
            survival.Changed += Refresh; recovery.Changed += Refresh; subscribed = true;
        }
        private void Unsubscribe()
        {
            if (!subscribed) return;
            survival.Changed -= Refresh; recovery.Changed -= Refresh; subscribed = false;
        }

        public void Refresh()
        {
            if (!IsBound || !isActiveAndEnabled) return;
            SetText(dayReadout, dayAtlas, "MISSION DAY " + recovery.MissionDay.ToString(CultureInfo.InvariantCulture));
            bool growing = survival.Enabled && survival.GrowStage == GrowModuleStage.Growing;
            bool mature = survival.Enabled && survival.GrowStage == GrowModuleStage.HarvestReady;
            GameObject crop = mature ? MatureCrop() : matureCrop;
            // Without a buildable mature model the seedlings stay as the HarvestReady fallback (previous behaviour).
            bool showSeedlings = growing || (mature && !crop);
            if (seedlings && seedlings.activeSelf != showSeedlings) seedlings.SetActive(showSeedlings);
            if (crop && crop.activeSelf != mature) crop.SetActive(mature);
            string text = !survival.Enabled ? "GROW CASSETTE\nNOT AVAILABLE" : Describe(survival);
            SetText(growReadout, growAtlas, text);
        }

        private static string Describe(ISurvivalExtension state)
        {
            switch (state.GrowStage)
            {
                case GrowModuleStage.Unbuilt: return "GROW CASSETTE\nASSEMBLY REQUIRED";
                case GrowModuleStage.ReadyToPlant: return "GROW CASSETTE\nREADY TO PLANT";
                case GrowModuleStage.Growing:
                    double seconds = state.CropRemainingSimulationSeconds;
                    return "GROWING / AGE " + Days(state.CropAgeSimulationDays)
                        + "\nHARVEST IN " + Days(seconds / 86400d);
                case GrowModuleStage.HarvestReady: return "CROP READY\nHARVEST AVAILABLE";
                case GrowModuleStage.Harvested: return "CROP HARVESTED\nSCENARIO CROP COMPLETE";
                case GrowModuleStage.Failed: return "CROP FAILED\nGROWTH ENDED";
                default: return "GROW STATUS UNAVAILABLE";
            }
        }
        private static string Days(double value) => double.IsNaN(value) || double.IsInfinity(value) || value < 0d
            ? "— SIM DAYS" : value.ToString("0.0", CultureInfo.InvariantCulture) + " SIM DAYS";
        private GameObject MatureCrop()
        {
            if (!seedlings) return null;
            if (matureCropFor != seedlings)
            {
                if (matureCrop) Destroy(matureCrop);
                matureCrop = HabitatMatureCrop.Build(seedlings, transform.up);
                matureCropFor = seedlings;
            }
            return matureCrop;
        }

        private void ResetPresentation()
        {
            if (seedlings) seedlings.SetActive(false);
            if (matureCrop) matureCrop.SetActive(false);
            SetText(dayReadout, dayAtlas, "MISSION DAY —");
            SetText(growReadout, growAtlas, "GROW STATUS UNAVAILABLE");
        }
        private static void SetText(TextMesh text, ShipLabelAtlasBinder atlas, string value)
        {
            if (!text || text.text == value) return;
            text.text = value;
            if (atlas) atlas.RefreshAtlas();
        }
    }
}
