#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OutOfReach.Presentation.AirlockPanel.Editor
{
    /// <summary>Creates only an additive world-space screen beneath an explicitly supplied mount. Never edits model assets.</summary>
    public static class AirlockPanelBuilder
    {
        public static AirlockStatusPanel Create(Transform mount, Font labelFont, float widthMetres = .23f)
        {
            if (!mount || !labelFont || !float.IsFinite(widthMetres) || widthMetres <= 0)
                throw new ArgumentException("A scene mount, licensed project font and finite positive width are required.");
            if (EditorUtility.IsPersistent(mount)) throw new ArgumentException("Use a scene mount, not an imported model/prefab asset.");
            if (mount.Find("Airlock digital screen")) throw new InvalidOperationException("The supplied mount already has an airlock screen.");
            var root = new GameObject("Airlock digital screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(AirlockStatusPanel));
            Undo.RegisterCreatedObjectUndo(root, "Create physical airlock display");
            RectTransform rect = (RectTransform)root.transform;
            rect.SetParent(mount, false);
            rect.sizeDelta = new Vector2(1000, 700);
            rect.localScale = Vector3.one * (widthMetres / 1000f);
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4;
            // No GraphicRaycaster, collider, audio source or input handler. The existing physical control remains authoritative.
            AddImage("Screen glass", rect, 0, 0, 1000, 700, new Color(.015f, .020f, .025f, 1));
            AddText("Unit", rect, labelFont, "AIRLOCK / CYCLE", 48, 25, 904, 45, 35, new Color(.48f, .54f, .58f));
            Image rule = AddImage("State rule", rect, 48, 83, 904, 3, Color.white);
            Text state = AddText("State", rect, labelFont, "NO SIGNAL", 48, 103, 904, 104, 81, Color.white);
            var digitsObject = new GameObject("Countdown mm ss", typeof(RectTransform), typeof(CanvasRenderer), typeof(AirlockCountdownGraphic));
            Place((RectTransform)digitsObject.transform, rect, 80, 232, 840, 230);
            var digits = digitsObject.GetComponent<AirlockCountdownGraphic>();
            digits.raycastTarget = false;
            Text caption = AddText("Time basis", rect, labelFont, "SIM TIME UNAVAILABLE", 48, 480, 904, 46, 32, Color.gray);
            caption.alignment = TextAnchor.MiddleCenter;
            AddImage("Seal separator", rect, 48, 547, 904, 2, new Color(.16f, .19f, .21f));
            Image innerLamp = AddImage("Inner state lamp", rect, 48, 577, 12, 12, Color.gray);
            Image outerLamp = AddImage("Outer state lamp", rect, 523, 577, 12, 12, Color.gray);
            Text inner = AddText("Inner seal", rect, labelFont, "INNER UNKNOWN", 78, 559, 420, 52, 37, Color.white);
            Text outer = AddText("Outer seal", rect, labelFont, "OUTER UNKNOWN", 553, 559, 400, 52, 37, Color.white);
            Text detail = AddText("State detail", rect, labelFont, "STATE NOT CONNECTED", 48, 633, 904, 38, 28, Color.gray);
            var panel = root.GetComponent<AirlockStatusPanel>();
            panel.ConfigureView(state, caption, inner, outer, detail, rule, innerLamp, outerLamp, digits);
            return panel;
        }

        private static void Place(RectTransform rect, Transform parent, float x, float y, float width, float height)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Image AddImage(string name, Transform parent, float x, float y, float width, float height, Color tint)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Place((RectTransform)child.transform, parent, x, y, width, height);
            var image = child.GetComponent<Image>(); image.color = tint; image.raycastTarget = false;
            return image;
        }

        private static Text AddText(string name, Transform parent, Font font, string value,
            float x, float y, float width, float height, int size, Color tint)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            Place((RectTransform)child.transform, parent, x, y, width, height);
            var text = child.GetComponent<Text>();
            text.font = font; text.text = value; text.fontSize = size; text.color = tint;
            text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
#endif
