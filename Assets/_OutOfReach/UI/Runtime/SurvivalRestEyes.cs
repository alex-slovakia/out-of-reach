using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI
{
    public sealed class SurvivalRestEyes : VisualElement
    {
        private bool resting, reduced;
        private float elapsed, wakeFrom;
        public float Closure01 { get; private set; }

        public SurvivalRestEyes()
        {
            name = "survival-rest-eyes";
            pickingMode = PickingMode.Ignore;
            AddToClassList("survival-rest-eyes");
            generateVisualContent += Draw;
        }

        public void Observe(bool isResting, bool reducedMotion)
        {
            reduced = reducedMotion;
            if (resting == isResting) return;
            resting = isResting;
            elapsed = 0f;
            wakeFrom = Closure01;
        }

        public void Advance(float seconds, bool canPresent)
        {
            if (canPresent) elapsed += Mathf.Clamp(seconds, 0f, .1f);
            Closure01 = resting ? (reduced ? Ease(elapsed / 2f) : DrowsyClosure(elapsed))
                : wakeFrom * (1f - Ease(elapsed / 1.2f));
            EnableInClassList("survival-hidden", !canPresent || Closure01 <= .0001f);
            MarkDirtyRepaint();
        }

        public void Reset()
        {
            resting = false; elapsed = wakeFrom = Closure01 = 0f;
            EnableInClassList("survival-hidden", true);
            MarkDirtyRepaint();
        }

        private static float DrowsyClosure(float t)
        {
            if (t < .75f) return .78f * Ease(t / .75f);
            if (t < 1.35f) return Mathf.Lerp(.78f, .10f, Ease((t - .75f) / .60f));
            if (t < 2.15f) return Mathf.Lerp(.10f, .94f, Ease((t - 1.35f) / .80f));
            if (t < 2.85f) return Mathf.Lerp(.94f, .22f, Ease((t - 2.15f) / .70f));
            return Mathf.Lerp(.22f, 1f, Ease((t - 2.85f) / 1.15f));
        }

        private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        private void Draw(MeshGenerationContext context)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w < 1f || h < 1f || Closure01 <= 0f) return;
            Painter2D p = context.painter2D;
            if (reduced)
            {
                p.fillColor = new Color(.006f, .008f, .009f, Closure01);
                p.BeginPath(); p.MoveTo(Vector2.zero); p.LineTo(new Vector2(w, 0));
                p.LineTo(new Vector2(w, h)); p.LineTo(new Vector2(0, h)); p.ClosePath(); p.Fill();
                return;
            }
            float edge = h * .5f * Closure01;
            float curve = h * .14f * Mathf.Sin(Mathf.PI * Closure01);
            for (int layer = 3; layer >= 0; layer--)
            {
                float feather = layer * 3f * Mathf.Sin(Mathf.PI * Closure01);
                p.fillColor = new Color(.006f, .008f, .009f, layer == 0 ? 1f : .10f);
                float corner = edge + curve + feather, control = edge - curve / 3f + feather;
                p.BeginPath(); p.MoveTo(Vector2.zero); p.LineTo(new Vector2(w, 0));
                p.LineTo(new Vector2(w, corner));
                p.BezierCurveTo(new Vector2(w * .66f, control), new Vector2(w * .34f, control), new Vector2(0, corner));
                p.ClosePath(); p.Fill();
                p.BeginPath(); p.MoveTo(new Vector2(0, h)); p.LineTo(new Vector2(w, h));
                p.LineTo(new Vector2(w, h - corner));
                p.BezierCurveTo(new Vector2(w * .66f, h - control), new Vector2(w * .34f, h - control), new Vector2(0, h - corner));
                p.ClosePath(); p.Fill();
            }
        }
    }
}
