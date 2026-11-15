using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI
{
    public sealed class SurvivalWaterLocator : VisualElement
    {
        private readonly Label caption;
        private Vector2 anchor, direction;
        public bool IsEdgeMarker { get; private set; }
        public bool IsBehind { get; private set; }
        public float DistanceMetres { get; private set; }
        public Vector2 MarkerPosition => anchor;

        public SurvivalWaterLocator()
        {
            name = "survival-water-locator";
            pickingMode = PickingMode.Ignore;
            AddToClassList("survival-water-locator");
            caption = new Label { name = "survival-water-distance", enableRichText = false, pickingMode = PickingMode.Ignore };
            caption.AddToClassList("survival-water-distance");
            Add(caption);
            generateVisualContent += Draw;
        }

        public void Present(Camera camera, Transform target, bool visible)
        {
            bool available = visible && camera && camera.isActiveAndEnabled && target && target.gameObject.activeInHierarchy;
            EnableInClassList("survival-hidden", !available);
            if (!available || contentRect.width <= 1f || contentRect.height <= 1f)
            { caption.text = string.Empty; MarkDirtyRepaint(); return; }
            Vector3 viewport = camera.WorldToViewportPoint(target.position);
            if (!Finite(viewport)) { EnableInClassList("survival-hidden", true); return; }
            float w = contentRect.width, h = contentRect.height;
            Vector2 center = new Vector2(w * .5f, h * .5f);
            Rect rect = camera.rect;
            Vector2 point = new Vector2((rect.x + viewport.x * rect.width) * w,
                (1f - rect.y - viewport.y * rect.height) * h);
            direction = point - center;
            IsBehind = viewport.z <= 0f;
            if (IsBehind) direction = -direction;
            if (direction.sqrMagnitude < .0001f) direction = Vector2.down;
            float insetX = Mathf.Min(95f, w * .18f), insetY = Mathf.Min(65f, h * .18f);
            IsEdgeMarker = IsBehind || point.x < insetX || point.x > w - insetX || point.y < insetY || point.y > h - insetY;
            if (IsEdgeMarker)
            {
                float scaleX = (center.x - insetX) / Mathf.Max(.0001f, Mathf.Abs(direction.x));
                float scaleY = (center.y - insetY) / Mathf.Max(.0001f, Mathf.Abs(direction.y));
                anchor = center + direction * Mathf.Min(scaleX, scaleY);
            }
            else anchor = point;
            direction.Normalize();
            DistanceMetres = Vector3.Distance(camera.transform.position, target.position);
            caption.text = "WATER  ·  " + DistanceMetres.ToString("0.0", CultureInfo.InvariantCulture) + " m"
                + (IsBehind ? "\nBEHIND YOU" : string.Empty);
            caption.style.left = anchor.x - 85f;
            caption.style.top = anchor.y + 22f;
            EnableInClassList("survival-hidden", false);
            MarkDirtyRepaint();
        }

        private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);

        private void Draw(MeshGenerationContext context)
        {
            if (string.IsNullOrEmpty(caption.text)) return;
            Painter2D p = context.painter2D;
            Color cyan = new Color(.49f, .86f, .89f, .92f);
            p.strokeColor = new Color(.14f, .35f, .39f, .65f);
            p.lineWidth = 4f;
            p.BeginPath(); p.Arc(anchor, 17f, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            p.strokeColor = cyan; p.lineWidth = 1f;
            p.BeginPath(); p.Arc(anchor, 17f, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            if (IsEdgeMarker)
            {
                Vector2 normal = new Vector2(-direction.y, direction.x);
                p.fillColor = cyan; p.BeginPath(); p.MoveTo(anchor + direction * 10f);
                p.LineTo(anchor - direction * 5f + normal * 6f);
                p.LineTo(anchor - direction * 5f - normal * 6f); p.ClosePath(); p.Fill();
            }
            else
            {
                p.fillColor = cyan; p.BeginPath(); p.MoveTo(anchor + new Vector2(0, -10));
                p.BezierCurveTo(anchor + new Vector2(15, 7), anchor + new Vector2(3, 14), anchor + new Vector2(0, 10));
                p.BezierCurveTo(anchor + new Vector2(-12, 14), anchor + new Vector2(-12, 5), anchor + new Vector2(0, -10));
                p.ClosePath(); p.Fill();
            }
        }
    }
}
