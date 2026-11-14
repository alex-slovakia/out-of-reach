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
            bool liveCrop = survival.Enabled && (survival.GrowStage == GrowModuleStage.Growing
                || survival.GrowStage == GrowModuleStage.HarvestReady);
            if (seedlings && seedlings.activeSelf != liveCrop) seedlings.SetActive(liveCrop);
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
        private void ResetPresentation()
        {
            if (seedlings) seedlings.SetActive(false);
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
