using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI
{
    /// <summary>
    /// Pause-menu controls card: key caps and mouse pictograms with a short action label, replacing the old wall of
    /// text. It only draws; <see cref="PauseControlsModel"/> decides the rows from the real bindings and the player's
    /// movement mode. Read-only: no rebinding, no focusable children, never takes pointer input.
    /// </summary>
    public sealed class PauseControlsView : VisualElement
    {
        /// <summary>Resources name of the pause-only style sheet (Assets/_OutOfReach/UI/Resources/PauseControls.uss).</summary>
        public const string StyleSheetResource = "PauseControls";
        public const string ViewName = "pause-controls";

        private readonly Label modeLabel;
        private readonly VisualElement rowsHost;
        private string builtSignature;
        private IReadOnlyList<PauseControlRow> rows = Array.Empty<PauseControlRow>();

        public PauseControlsView(StyleSheet sheet = null)
        {
            name = ViewName;
            pickingMode = PickingMode.Ignore;
            focusable = false;
            AddToClassList("pc-card");
            sheet = sheet ? sheet : Resources.Load<StyleSheet>(StyleSheetResource);
            if (sheet) styleSheets.Add(sheet);

            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("pc-head");
            var title = new Label("CONTROLS") { pickingMode = PickingMode.Ignore };
            title.AddToClassList("pc-title");
            modeLabel = new Label { pickingMode = PickingMode.Ignore };
            modeLabel.AddToClassList("pc-mode");
            head.Add(title);
            head.Add(modeLabel);
            Add(head);
            rowsHost = new VisualElement { pickingMode = PickingMode.Ignore };
            rowsHost.AddToClassList("pc-rows");
            Add(rowsHost);
        }

        /// <summary>The rows currently drawn, for tests and inspection.</summary>
        public IReadOnlyList<PauseControlRow> Rows => rows;

        /// <summary>Shows the controls for the player's current mode, rebuilding only when something changed.</summary>
        public void SetControls(PauseControlsMode mode, bool railGrip, PauseBindings bindings)
        {
            bindings = bindings ?? PauseBindings.Fallback();
            string signature = mode + "|" + railGrip + "|" + bindings.Signature;
            if (signature == builtSignature) return;
            builtSignature = signature;
            modeLabel.text = PauseControlsModel.ModeLabel(mode).ToUpperInvariant();
            rows = PauseControlsModel.Build(mode, railGrip, bindings);
            rowsHost.Clear();
            foreach (var row in rows) rowsHost.Add(BuildRow(row));
        }

        /// <summary>Convenience for composition: reads the live map (null map uses the documented defaults).</summary>
        public void SetControls(PauseControlsMode mode, bool railGrip, InputActionMap map)
            => SetControls(mode, railGrip, PauseBindings.FromMap(map));

        private static VisualElement BuildRow(PauseControlRow row)
        {
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("pc-row");
            var glyphs = new VisualElement { pickingMode = PickingMode.Ignore };
            glyphs.AddToClassList("pc-glyphs");
            for (int a = 0; a < row.Alternatives.Count; a++)
            {
                if (a > 0) glyphs.Add(Separator("/", "pc-or"));
                var chord = row.Alternatives[a];
                for (int g = 0; g < chord.Count; g++)
                {
                    if (g > 0) glyphs.Add(Separator("+", "pc-plus"));
                    glyphs.Add(BuildGlyph(chord[g]));
                }
            }
            line.Add(glyphs);
            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("pc-text");
            var label = new Label(row.Label) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("pc-action");
            text.Add(label);
            if (!string.IsNullOrEmpty(row.Note))
            {
                var note = new Label(row.Note) { pickingMode = PickingMode.Ignore };
                note.AddToClassList("pc-note");
                text.Add(note);
            }
            line.Add(text);
            return line;
        }

        private static Label Separator(string text, string cssClass)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("pc-sep");
            label.AddToClassList(cssClass);
            return label;
        }

        private static VisualElement BuildGlyph(PauseGlyph glyph)
        {
            switch (glyph.Kind)
            {
                case PauseGlyphKind.Mouse: return MouseElement(glyph.Left, glyph.Right, false);
                case PauseGlyphKind.MouseLook: return MouseElement(false, false, true);
                case PauseGlyphKind.Arrow: return ArrowKey(glyph.Text);
                case PauseGlyphKind.Cluster: return Cluster(glyph);
                default: return KeyCap(glyph.Text);
            }
        }

        private static Label KeyCap(string text)
        {
            var cap = new Label(text) { pickingMode = PickingMode.Ignore, name = "pc-key-" + text.ToLowerInvariant() };
            cap.AddToClassList("pc-key");
            if (text.Length > 1) cap.AddToClassList("pc-key-wide");
            return cap;
        }

        private static VisualElement Cluster(PauseGlyph cluster)
        {
            var host = new VisualElement { pickingMode = PickingMode.Ignore };
            host.AddToClassList("pc-cluster");
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.AddToClassList("pc-cluster-row");
            var bottom = new VisualElement { pickingMode = PickingMode.Ignore };
            bottom.AddToClassList("pc-cluster-row");
            AddGlyph(top, cluster.Cluster[0]);
            AddGlyph(bottom, cluster.Cluster[1]);
            AddGlyph(bottom, cluster.Cluster[2]);
            AddGlyph(bottom, cluster.Cluster[3]);
            host.Add(top);
            host.Add(bottom);
            return host;
        }

        private static void AddGlyph(VisualElement parent, PauseGlyph glyph)
        {
            if (glyph != null) parent.Add(BuildGlyph(glyph));
        }

        private static VisualElement ArrowKey(string direction)
        {
            var cap = new VisualElement { pickingMode = PickingMode.Ignore, name = "pc-key-arrow-" + direction };
            cap.AddToClassList("pc-key");
            cap.AddToClassList("pc-arrow");
            cap.generateVisualContent += context =>
            {
                float w = cap.contentRect.width, h = cap.contentRect.height;
                if (w <= 0f || h <= 0f) return;
                float cx = w * .5f, cy = h * .5f, r = Mathf.Min(w, h) * .26f;
                Vector2 tip, a, b;
                switch (direction)
                {
                    case "up": tip = new Vector2(cx, cy - r); a = new Vector2(cx - r, cy + r * .8f); b = new Vector2(cx + r, cy + r * .8f); break;
                    case "down": tip = new Vector2(cx, cy + r); a = new Vector2(cx - r, cy - r * .8f); b = new Vector2(cx + r, cy - r * .8f); break;
                    case "left": tip = new Vector2(cx - r, cy); a = new Vector2(cx + r * .8f, cy - r); b = new Vector2(cx + r * .8f, cy + r); break;
                    default: tip = new Vector2(cx + r, cy); a = new Vector2(cx - r * .8f, cy - r); b = new Vector2(cx - r * .8f, cy + r); break;
                }
                var painter = context.painter2D;
                painter.fillColor = KeyInk;
                painter.BeginPath();
                painter.MoveTo(tip);
                painter.LineTo(a);
                painter.LineTo(b);
                painter.ClosePath();
                painter.Fill();
            };
            return cap;
        }

        private static readonly Color32 KeyInk = new Color32(238, 237, 225, 255);
        private static readonly Color32 MouseLit = new Color32(245, 188, 116, 255);
        private static readonly Color32 MouseLine = new Color32(245, 237, 222, 255);
        private static readonly Color32 LookInk = new Color32(120, 205, 214, 255);

        private static VisualElement MouseElement(bool left, bool right, bool look)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore, name = look ? "pc-mouse-look" : "pc-mouse" };
            element.AddToClassList("pc-mouse");
            if (look) element.AddToClassList("pc-mouse-look");
            element.generateVisualContent += context => DrawMouse(context.painter2D, element.contentRect, left, right, look);
            return element;
        }

        // Designed on a 24 x 29 body (the in-flight mouse pictogram's shape); look adds motion marks either side.
        private static void DrawMouse(Painter2D painter, Rect rect, bool left, bool right, bool look)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            float s = rect.height / 29f;
            float ox = (rect.width - 24f * s) * .5f;
            Vector2 P(float x, float y) => new Vector2(ox + x * s, y * s);

            painter.fillColor = MouseLit;
            painter.BeginPath();
            if (left)
            {
                painter.MoveTo(P(3, 13));
                painter.LineTo(P(3, 9));
                painter.QuadraticCurveTo(P(3, 3), P(11, 2));
                painter.LineTo(P(11, 13));
                painter.ClosePath();
            }
            if (right)
            {
                painter.MoveTo(P(13, 13));
                painter.LineTo(P(13, 2));
                painter.QuadraticCurveTo(P(21, 3), P(21, 9));
                painter.LineTo(P(21, 13));
                painter.ClosePath();
            }
            painter.Fill();

            painter.strokeColor = MouseLine;
            painter.lineWidth = Mathf.Max(1.25f, 1.5f * s);
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            painter.MoveTo(P(12, 2));
            painter.QuadraticCurveTo(P(3, 2), P(3, 10));
            painter.LineTo(P(3, 19));
            painter.QuadraticCurveTo(P(3, 27), P(12, 27));
            painter.QuadraticCurveTo(P(21, 27), P(21, 19));
            painter.LineTo(P(21, 10));
            painter.QuadraticCurveTo(P(21, 2), P(12, 2));
            painter.ClosePath();
            painter.Stroke();
            painter.BeginPath();
            painter.MoveTo(P(3, 13));
            painter.LineTo(P(21, 13));
            painter.Stroke();
            painter.BeginPath();
            painter.MoveTo(P(12, 2));
            painter.LineTo(P(12, 13));
            painter.Stroke();

            if (!look) return;
            // Motion marks: small cyan chevrons either side of the body.
            painter.fillColor = LookInk;
            painter.BeginPath();
            painter.MoveTo(P(-2, 16));
            painter.LineTo(P(-8, 12));
            painter.LineTo(P(-8, 20));
            painter.ClosePath();
            painter.MoveTo(P(26, 16));
            painter.LineTo(P(32, 12));
            painter.LineTo(P(32, 20));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
