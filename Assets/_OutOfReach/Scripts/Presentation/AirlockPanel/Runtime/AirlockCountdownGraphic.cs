using UnityEngine;
using UnityEngine.UI;

namespace OutOfReach.Presentation.AirlockPanel
{
    /// <summary>Original seven-segment mm:ss geometry. No digit font, texture, timer or animation.</summary>
    [AddComponentMenu("")]
    public sealed class AirlockCountdownGraphic : Graphic
    {
        private static readonly int[] Masks = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f };
        private static readonly float[] DigitX = { 0, 86, 207, 293 };
        private int displayedSeconds = -1;
        private readonly Color inactive = new Color(.028f, .035f, .039f, 1);

        public void SetDisplay(int seconds, Color tint)
        {
            seconds = seconds >= 0 && seconds <= 5999 ? seconds : -1;
            if (displayedSeconds == seconds && color == tint) return;
            displayedSeconds = seconds;
            color = tint;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float scale = Mathf.Min(rect.width / 363f, rect.height / 120f);
            Vector2 origin = rect.center - new Vector2(363, 120) * (scale * .5f);
            int minutes = displayedSeconds < 0 ? 0 : displayedSeconds / 60;
            int seconds = displayedSeconds < 0 ? 0 : displayedSeconds % 60;
            int[] digits = { minutes / 10, minutes % 10, seconds / 10, seconds % 10 };
            for (int digit = 0; digit < 4; digit++)
            {
                int mask = displayedSeconds < 0 ? 0x40 : Masks[digits[digit]];
                for (int segment = 0; segment < 7; segment++)
                {
                    Vector2 center;
                    bool vertical = segment == 1 || segment == 2 || segment == 4 || segment == 5;
                    switch (segment)
                    {
                        case 0: center = new Vector2(35, 112); break;
                        case 1: center = new Vector2(63, 86); break;
                        case 2: center = new Vector2(63, 34); break;
                        case 3: center = new Vector2(35, 8); break;
                        case 4: center = new Vector2(7, 34); break;
                        case 5: center = new Vector2(7, 86); break;
                        default: center = new Vector2(35, 60); break;
                    }
                    center.x += DigitX[digit];
                    AddSegment(vh, origin, scale, center, vertical, (mask & (1 << segment)) != 0 ? color : inactive);
                }
            }
            AddDot(vh, origin, scale, new Vector2(189.5f, 80));
            AddDot(vh, origin, scale, new Vector2(189.5f, 40));
        }

        private static void AddSegment(VertexHelper vh, Vector2 origin, float scale, Vector2 center, bool vertical, Color tint)
        {
            const float halfLength = 23, halfThickness = 4;
            Vector2[] points = { new Vector2(-halfLength, 0), new Vector2(-halfLength + halfThickness, halfThickness),
                new Vector2(halfLength - halfThickness, halfThickness), new Vector2(halfLength, 0),
                new Vector2(halfLength - halfThickness, -halfThickness), new Vector2(-halfLength + halfThickness, -halfThickness) };
            int start = vh.currentVertCount;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 p = vertical ? new Vector2(-points[i].y, points[i].x) : points[i];
                vh.AddVert(origin + (center + p) * scale, tint, Vector2.zero);
            }
            for (int i = 1; i < 5; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }

        private void AddDot(VertexHelper vh, Vector2 origin, float scale, Vector2 center)
        {
            int start = vh.currentVertCount;
            foreach (Vector2 p in new[] { new Vector2(-3, -3), new Vector2(-3, 3), new Vector2(3, 3), new Vector2(3, -3) })
                vh.AddVert(origin + (center + p) * scale, displayedSeconds < 0 ? inactive : color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
