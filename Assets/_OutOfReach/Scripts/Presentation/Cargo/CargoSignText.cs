using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace OutOfReach.Presentation.Cargo
{
    /// <summary>
    /// Legibility for the cargo manifest signs. CargoVisualPresenter keeps writing its own text ("WATER TANK\nSUPPLY x2",
    /// "...\nABOARD x1", carried "...\n48 kg"); after it, this rewrites the sign as one short bold name in large type and
    /// the mass below, white on the graphite plate. Counts and the carried mass are taken from the presenter's text.
    /// Display only: no cargo state is read from or written to Core here.
    /// </summary>
    [DefaultExecutionOrder(520)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMesh))]
    public sealed class CargoSignText : MonoBehaviour
    {
        public const string DockLegendName = "Cargo rack legend";
        public const float PlateHeight = .17f;
        public const int BaseFontSize = 128, NameFontSize = 192;
        private static readonly Color Ink = new Color(.97f, .97f, .94f);
        private static readonly Regex Tags = new Regex("<[^>]*>");
        private static readonly Regex Count = new Regex(@"[x×]\s*(\d+)");
        private static readonly Regex Kg = new Regex(@"(\d+(?:\.\d+)?)\s*kg", RegexOptions.IgnoreCase);

        /// <summary>Presenter display name -> short sign name and unit mass (AuroraMission catalogue, kg).</summary>
        private static readonly Dictionary<string, (string Short, int Kg)> Items = new Dictionary<string, (string, int)>
        {
            { "OXYGEN TANK", ("OXYGEN", 30) }, { "WATER TANK", ("WATER", 48) }, { "FOOD LOCKER", ("FOOD", 35) },
            { "BATTERY MODULE", ("BATTERY", 55) }, { "SOLAR WING", ("SOLAR", 70) },
            { "SPARE PARTS KIT", ("SPARES", 25) }, { "SPECTROMETER", ("SCIENCE", 90) },
        };

        private TextMesh text;
        private string written;

        /// <summary>The last item this sign showed; read by the case name plate.</summary>
        public string ShortName { get; private set; }
        public int MassKg { get; private set; }

        /// <summary>Bold, white, lineSpaced type whose base line has the given cap-ish em height in metres.</summary>
        public static void Style(TextMesh label, float baseEmMetres)
        {
            label.richText = true;
            label.fontStyle = FontStyle.Bold;
            label.fontSize = BaseFontSize;
            label.characterSize = baseEmMetres * 10f / BaseFontSize;
            label.lineSpacing = .9f;
            label.color = Ink;
        }

        /// <summary>Parses presenter text or an already formatted sign. False when it is not a cargo item.</summary>
        public static bool TryParse(string raw, out string shortName, out int massKg, out int count)
        {
            shortName = null; massKg = 0; count = 0;
            if (string.IsNullOrEmpty(raw)) return false;
            string[] lines = Tags.Replace(raw, string.Empty).Split('\n');
            string first = lines[0].Trim();
            bool found = false;
            foreach (KeyValuePair<string, (string Short, int Kg)> item in Items)
                if (item.Key == first || item.Value.Short == first)
                { shortName = item.Value.Short; massKg = item.Value.Kg; found = true; break; }
            if (!found) return false;
            for (int i = 1; i < lines.Length; i++)
            {
                Match kg = Kg.Match(lines[i]);
                if (kg.Success) massKg = (int)Math.Round(double.Parse(kg.Groups[1].Value, CultureInfo.InvariantCulture));
                Match c = Count.Match(lines[i]);
                if (c.Success) count = int.Parse(c.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            return true;
        }

        public static string Format(string shortName, int massKg, int count)
        {
            string mass = massKg.ToString(CultureInfo.InvariantCulture) + " kg";
            if (count > 1) mass += "   ×" + count.ToString(CultureInfo.InvariantCulture);
            return "<size=" + NameFontSize.ToString(CultureInfo.InvariantCulture) + ">" + shortName + "</size>\n" + mass;
        }

        private void Awake()
        {
            text = GetComponent<TextMesh>();
            bool carried = transform.parent && transform.parent.name == "Supported cargo view - no physics";
            // Rack/aboard: 7.5 cm names at 2-3 m. The carried sign sits under the camera, so smaller.
            Style(text, carried ? .033f : .05f);
            Transform plate = transform.Find(PreflightRackDressing.PlateName);
            if (plate && plate.localScale.y < PlateHeight)
                plate.localScale = new Vector3(Mathf.Max(plate.localScale.x, .46f), PlateHeight, plate.localScale.z);
            Rewrite();
        }

        private void LateUpdate()
        {
            if (text && text.text != written) Rewrite();
        }

        private void Rewrite()
        {
            if (!text || !enabled) return;
            if (!TryParse(text.text, out string shortName, out int mass, out int count)) { written = text.text; return; }
            ShortName = shortName; MassKg = mass;
            written = Format(shortName, mass, count);
            text.text = written;
        }

        /// <summary>Covers the dock's small modelled legend with a large, two-sided "CARGO LOADING" plate.</summary>
        public static void OverlayLegend(Transform legend)
        {
            if (!legend || legend.Find("Legible legend overlay")) return;
            var filter = legend.GetComponent<MeshFilter>();
            var renderer = legend.GetComponent<MeshRenderer>();
            if (!filter || !filter.sharedMesh || !renderer) return;
            TextMesh template = null;
            foreach (TextMesh candidate in legend.root.GetComponentsInChildren<TextMesh>(true))
                if (candidate.name == PreflightRackDressing.LabelName && candidate.transform.Find(PreflightRackDressing.PlateName))
                { template = candidate; break; }
            if (!template) return;
            Bounds b = filter.sharedMesh.bounds;
            // The thinnest axis is the sign normal; the other two span the face.
            Vector3 size = b.size;
            int normal = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            int up = normal == 1 ? 2 : 1;
            int across = 3 - normal - up;
            Vector3 n = Vector3.zero; n[normal] = 1f;
            Vector3 u = Vector3.zero; u[up] = 1f;
            float width = size[across] + .006f, height = Mathf.Max(size[up] + .006f, .12f);
            var root = new GameObject("Legible legend overlay").transform;
            root.SetParent(legend, false);
            root.gameObject.layer = legend.gameObject.layer;
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject sign = Instantiate(template.gameObject, root);
                sign.name = "Legend face " + (side > 0 ? "+" : "-");
                var formatter = sign.GetComponent<CargoSignText>();
                if (formatter) { formatter.enabled = false; Destroy(formatter); }
                // Text faces its local -z; place it just outside this face of the legend.
                Vector3 outward = n * side;
                sign.transform.localRotation = Quaternion.LookRotation(-outward, u);
                sign.transform.localPosition = b.center + outward * (size[normal] * .5f + .011f);
                Transform plate = sign.transform.Find(PreflightRackDressing.PlateName);
                if (plate)
                {
                    plate.localPosition = new Vector3(0f, 0f, .006f);
                    plate.localScale = new Vector3(width, height, .006f);
                }
                var label = sign.GetComponent<TextMesh>();
                Style(label, Mathf.Min(height * .5f, width / (13f * .62f)));
                label.text = "CARGO LOADING";
            }
        }
    }
}
