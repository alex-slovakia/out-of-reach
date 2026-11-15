using UnityEngine;

namespace OutOfReach.Presentation.Cargo
{
    /// <summary>
    /// Puts a large short name ("WATER" / "48 kg") over the case's printed label window, whose modelled text
    /// ("REPAIR SUPPLY", 13 mm) is generic and unreadable. The name comes from the nearest manifest sign beside the
    /// same case (rack, aboard or carried), so it always matches what the presenter shows. Display only.
    /// </summary>
    [DefaultExecutionOrder(540)]
    [DisallowMultipleComponent]
    public sealed class CargoCaseNamePlate : MonoBehaviour
    {
        public const string CaseName = "PreflightCargo_Visual";
        private const string PlateObjectName = "Case name plate (legible)";
        // PreflightCargo build script: front label recess centre (0, .121) m, .170 x .082 m, plate face at z .154 (+Z).
        private static readonly Vector3 WindowCentre = new Vector3(0f, .121f, .158f);
        private static readonly Vector2 WindowSize = new Vector2(.164f, .078f);

        private TextMesh source;
        private CargoSignText sourceSign;
        private TextMesh text;
        private Renderer[] renderers;
        private string shown;

        public static void Attach(Transform caseRoot)
        {
            if (!caseRoot || caseRoot.GetComponent<CargoCaseNamePlate>() || !caseRoot.parent) return;
            TextMesh nearest = null;
            float best = .8f;
            foreach (Transform sibling in caseRoot.parent)
            {
                if (sibling.name != PreflightRackDressing.LabelName) continue;
                var label = sibling.GetComponent<TextMesh>();
                float d = Vector3.Distance(sibling.position, caseRoot.position);
                if (label && sibling.Find(PreflightRackDressing.PlateName) && d < best) { best = d; nearest = label; }
            }
            if (!nearest) return;
            var plate = caseRoot.gameObject.AddComponent<CargoCaseNamePlate>();
            plate.source = nearest;
            plate.Build(nearest);
        }

        private void Build(TextMesh template)
        {
            // Clone the authored sign: same font, depth-tested label material, atlas binder and graphite plate.
            GameObject sign = Instantiate(template.gameObject, transform);
            sign.name = PlateObjectName;
            sign.SetActive(true);
            var formatter = sign.GetComponent<CargoSignText>();
            if (formatter) { formatter.enabled = false; Destroy(formatter); }
            foreach (Collider c in sign.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Transform t in sign.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
            // Text faces its local -z; the case's label faces +Z.
            sign.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            sign.transform.localPosition = WindowCentre;
            sign.transform.localScale = Vector3.one;
            Transform plate = sign.transform.Find(PreflightRackDressing.PlateName);
            if (plate)
            {
                plate.localPosition = new Vector3(0f, 0f, .0025f);
                plate.localScale = new Vector3(WindowSize.x, WindowSize.y, .002f);
            }
            text = sign.GetComponent<TextMesh>();
            CargoSignText.Style(text, .022f);
            renderers = sign.GetComponentsInChildren<Renderer>(true);
            sourceSign = source ? source.GetComponent<CargoSignText>() : null;
            shown = null;
            SetVisible(false);
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (!text) return;
            string name = null; int mass = 0;
            if (sourceSign && !string.IsNullOrEmpty(sourceSign.ShortName)) { name = sourceSign.ShortName; mass = sourceSign.MassKg; }
            else if (source && CargoSignText.TryParse(source.text, out string parsed, out int kg, out _)) { name = parsed; mass = kg; }
            string value = name == null ? null : CargoSignText.Format(name, mass, 0);
            if (value == shown) return;
            shown = value;
            if (value != null) text.text = value;
            SetVisible(value != null);
        }

        private void SetVisible(bool visible)
        {
            if (renderers == null) return;
            foreach (Renderer r in renderers) if (r) r.enabled = visible;
        }
    }
}
