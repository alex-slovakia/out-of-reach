using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace OutOfReach.Visuals.CargoCasesV02
{
    /// <summary>
    /// Display only. Sits on a delivered PreflightCargo_Visual instance whose own renderers the CargoCasesV02 installer
    /// switched off, and shows the V02 container model instead. On the rack and aboard there is one fixed model. The
    /// single carried case shows the model of whatever the presenter says is being carried: it reads the first line of
    /// the carried manifest label that CargoVisualPresenter already writes (never Core state). The V02 models carry
    /// their own baked stencil, so the runtime "Case name plate (legible)" overlay of the old label window stays hidden.
    /// </summary>
    [DefaultExecutionOrder(560)] // after CargoVisualPresenter (0), CargoSignText (520) and CargoCaseNamePlate (540)
    [DisallowMultipleComponent]
    public sealed class CargoCaseV02Skin : MonoBehaviour
    {
        public const string ModelPrefix = "CC2 case model - ";
        private const string OldNamePlate = "Case name plate (legible)";
        private static readonly Regex Tags = new Regex("<[^>]*>");

        [Serializable]
        public struct Variant
        {
            public string EquipmentId;
            [Tooltip("Presenter display name, e.g. WATER TANK")] public string DisplayName;
            [Tooltip("Sign short name, e.g. WATER")] public string ShortName;
            public GameObject Model;
        }

        [SerializeField] private Variant[] variants = Array.Empty<Variant>();
        [Tooltip("Carried case only: the manifest label the presenter writes the carried item into.")]
        [SerializeField] private TextMesh nameSource;

        private int shown = -1;
        private string lastText;
        private Renderer[] oldPlate;

        public string ShownEquipmentId => shown >= 0 && shown < variants.Length ? variants[shown].EquipmentId : null;

        public void Configure(Variant[] models, TextMesh carriedLabel)
        {
            variants = models ?? Array.Empty<Variant>();
            nameSource = carriedLabel;
            shown = -1;
            lastText = null;
        }

        private void OnEnable() { lastText = null; Refresh(); }
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            HideOldNamePlate();
            if (variants.Length == 0) return;
            int wanted = shown < 0 ? 0 : shown;
            if (nameSource)
            {
                string text = nameSource.text;
                if (text != lastText)
                {
                    lastText = text;
                    int match = Match(text);
                    if (match >= 0) wanted = match;
                }
            }
            if (wanted == shown) return;
            shown = wanted;
            for (int i = 0; i < variants.Length; i++)
            {
                GameObject model = variants[i].Model;
                if (model && model.activeSelf != (i == shown)) model.SetActive(i == shown);
            }
        }

        private int Match(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return -1;
            string first = Tags.Replace(raw, string.Empty).Split('\n')[0].Trim();
            for (int i = 0; i < variants.Length; i++)
                if (string.Equals(first, variants[i].DisplayName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(first, variants[i].ShortName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(first, variants[i].EquipmentId, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private void HideOldNamePlate()
        {
            if (oldPlate == null)
            {
                Transform plate = transform.Find(OldNamePlate);
                if (!plate) return; // attached by PreflightRackDressing after scene load; look again next frame
                oldPlate = plate.GetComponentsInChildren<Renderer>(true);
            }
            foreach (Renderer r in oldPlate) if (r && r.enabled) r.enabled = false;
        }
    }
}
