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
                : wakeFrom * (1f - Ease(elapsed / (reduced ? 1.2f : 1.6f)));
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
            if (t < .24f) return .92f * Ease(t / .24f);
            if (t < .70f) return Mathf.Lerp(.92f, .04f, Ease((t - .24f) / .46f));
            if (t < 1.60f) return Mathf.Lerp(.04f, .12f, Ease((t - .70f) / .90f));
            if (t < 2.10f) return Mathf.Lerp(.12f, .80f, Ease((t - 1.60f) / .50f));
            if (t < 2.60f) return Mathf.Lerp(.80f, .25f, Ease((t - 2.10f) / .50f));
            return Mathf.Lerp(.25f, 1f, Ease((t - 2.60f) / 1.50f));
        }

        private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        private void Draw(MeshGenerationContext context)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w < 1f || h < 1f || Closure01 <= 0f) return;
            Painter2D p = context.painter2D;
            if (reduced || Closure01 >= .999f)
            {
                p.fillColor = new Color(.003f, .004f, .005f, reduced ? Closure01 : 1f);
                p.BeginPath(); p.MoveTo(Vector2.zero); p.LineTo(new Vector2(w, 0));
                p.LineTo(new Vector2(w, h)); p.LineTo(new Vector2(0, h)); p.ClosePath(); p.Fill();
                return;
            }
            float lens = Mathf.Sin(Mathf.PI * Closure01);
            float upper = h * .58f * Closure01;
            float lower = h * (1f - .42f * Closure01);
            float curve = h * .24f * lens;
            float softness = h * .045f * lens;
            for (int layer = 18; layer >= 0; layer--)
            {
                float feather = softness * layer / 18f;
                p.fillColor = new Color(.003f, .004f, .005f, layer == 0 ? 1f : .085f);
                p.BeginPath(); p.MoveTo(Vector2.zero); p.LineTo(new Vector2(w, 0));
                p.LineTo(new Vector2(w, upper + curve * .88f + feather));
                p.BezierCurveTo(new Vector2(w * .70f, upper - curve * .40f + feather),
                    new Vector2(w * .28f, upper - curve * .30f + feather), new Vector2(0, upper + curve + feather));
                p.ClosePath(); p.Fill();
                p.BeginPath(); p.MoveTo(new Vector2(0, h)); p.LineTo(new Vector2(w, h));
                p.LineTo(new Vector2(w, lower - curve * .75f - feather));
                p.BezierCurveTo(new Vector2(w * .68f, lower + curve * .27f - feather),
                    new Vector2(w * .32f, lower + curve * .34f - feather), new Vector2(0, lower - curve * .82f - feather));
                p.ClosePath(); p.Fill();
            }
        }
    }
}
