using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace OutOfReach.Presentation.UI.Startup
{
    /// <summary>One drawable shape of the startup art, in the unit box (height 1, width <see cref="VectorArt.Aspect"/>, y down).</summary>
    public sealed class ArtShape
    {
        public readonly List<Vector2[]> Subpaths = new List<Vector2[]>();
        public readonly List<bool> Closed = new List<bool>();
        public float[][] Cumulative;                                       // per subpath cumulative length
        public float Length;                                               // total length of all subpaths
        public bool HasFill;
        public Color Fill;
        public bool EvenOdd;
        public Color Stroke;
        public float StrokeWidth;                                          // fraction of the unit-box height
        public float Taper;                                                // fraction of an open stroke tapered at each end (0 = none)
        public bool SketchedFromFill;                                      // no stroke in the art: outlined in pencil gold
        public float RevealStart, RevealEnd;                               // sketch progress window, 0..1
    }

    /// <summary>
    /// Ordered, flattened vector art for the startup card, imported from SVG. Document order is stroke order, so the art
    /// reveals shape by shape like a pencil sketch and then fills with its colours. Drop-in contract: replace
    /// <c>Resources/StartupCards/startup-art.txt</c> with the final SVG; nothing else changes.
    /// Supported: path (M L H V C S Q T A Z, absolute and relative), rect, circle, ellipse, line, polyline, polygon, g,
    /// transform (matrix translate scale rotate skewX skewY), viewBox, fill/stroke/stroke-width/opacity/fill-opacity/
    /// stroke-opacity/fill-rule as attributes or style. Ignored: gradients, clip paths, masks, filters, text, use, images.
    /// A fill that is a gradient reference falls back to pencil gold.
    /// </summary>
    public sealed class VectorArt
    {
        public static readonly Color PencilGold = new Color(.91f, .71f, .33f, 1f);
        public const float DefaultStrokeWidth = .0058f;
        private const float GapBetweenStrokes = .012f;
        private const float MaxTotalGap = .1f;
        public const float DefaultTaper = .28f;

        public float Aspect = 1f;
        public readonly List<ArtShape> Shapes = new List<ArtShape>();
        public bool IsEmpty => Shapes.Count == 0;
        public float TotalLength { get; private set; }

        // ------------------------------------------------------------------ public API

        public static VectorArt Parse(string svg)
        {
            var art = new VectorArt();
            if (string.IsNullOrWhiteSpace(svg)) return art;
            XDocument document;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, IgnoreComments = true };
                using (var reader = XmlReader.Create(new StringReader(svg), settings)) document = XDocument.Load(reader);
            }
            catch (XmlException) { return art; }
            XElement root = document.Root;
            if (root == null || root.Name.LocalName != "svg") return art;

            float vbx = 0f, vby = 0f, vbw = 0f, vbh = 0f;
            bool hasViewBox = TryViewBox(Attr(root, "viewBox"), out vbx, out vby, out vbw, out vbh);
            if (!hasViewBox)
            {
                float w = ParseLength(Attr(root, "width")), h = ParseLength(Attr(root, "height"));
                if (w > 0f && h > 0f) { vbx = 0f; vby = 0f; vbw = w; vbh = h; hasViewBox = true; }
            }
            var raw = new List<RawShape>();
            var context = new ParseContext { Tolerance = (hasViewBox ? vbh : 400f) * .012f };
            Walk(root, Style.Default, raw, context);
            if (raw.Count == 0) return art;

            if (!hasViewBox)
            {
                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);
                foreach (var shape in raw)
                    foreach (var sub in shape.Subpaths)
                        foreach (var p in sub) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
                float pad = Mathf.Max(max.x - min.x, max.y - min.y) * .04f;
                vbx = min.x - pad; vby = min.y - pad; vbw = max.x - min.x + pad * 2f; vbh = max.y - min.y + pad * 2f;
            }
            if (vbw <= 0f || vbh <= 0f) return art;
            art.Aspect = vbw / vbh;
            art.Build(raw, vbx, vby, vbh);
            return art;
        }

        /// <summary>Visible prefix of a shape for a sketch progress 0..1. Writes each visible subpath into <paramref name="output"/>.</summary>
        public static void VisibleSubpaths(ArtShape shape, float sketch01, List<List<Vector2>> output, Stack<List<Vector2>> pool)
        {
            for (int i = 0; i < output.Count; i++) { output[i].Clear(); pool.Push(output[i]); }
            output.Clear();
            float span = Mathf.Max(.0001f, shape.RevealEnd - shape.RevealStart);
            float local = Mathf.Clamp01((sketch01 - shape.RevealStart) / span);
            if (local <= 0f || shape.Length <= 0f) return;
            float target = shape.Length * local, consumed = 0f;
            var source = shape.Subpaths;
            for (int s = 0; s < source.Count && consumed < target; s++)
            {
                var points = source[s];
                if (points.Length < 2) continue;
                var cumulative = shape.Cumulative[s];
                float subLength = cumulative[cumulative.Length - 1];
                var list = pool.Count > 0 ? pool.Pop() : new List<Vector2>(64);
                list.Add(points[0]);
                float remaining = target - consumed;
                for (int i = 1; i < points.Length; i++)
                {
                    if (cumulative[i] <= remaining) { list.Add(points[i]); continue; }
                    float seg = cumulative[i] - cumulative[i - 1];
                    float k = seg <= 0f ? 0f : (remaining - cumulative[i - 1]) / seg;
                    list.Add(Vector2.Lerp(points[i - 1], points[i], Mathf.Clamp01(k)));
                    break;
                }
                consumed += subLength;
                if (list.Count >= 2) output.Add(list); else { list.Clear(); pool.Push(list); }
            }
        }

        // ------------------------------------------------------------------ build

        private void Build(List<RawShape> raw, float vbx, float vby, float vbh)
        {
            var stroked = new List<ArtShape>();
            foreach (var r in raw)
            {
                bool hasStroke = r.StrokeColor.a > .01f && r.StrokeWidth > 0f;
                bool hasFill = r.FillColor.a > .01f;
                if (!hasStroke && !hasFill) continue;
                var shape = new ArtShape { HasFill = hasFill, Fill = r.FillColor, EvenOdd = r.EvenOdd, Taper = r.Taper };
                if (hasStroke)
                {
                    shape.Stroke = r.StrokeColor;
                    shape.StrokeWidth = Mathf.Max(.0025f, r.StrokeWidth / vbh);
                }
                else
                {
                    shape.Stroke = PencilGold;
                    shape.StrokeWidth = DefaultStrokeWidth;
                    shape.SketchedFromFill = true;
                }
                foreach (var sub in r.Subpaths)
                {
                    if (sub.Count < 2) continue;
                    var points = new List<Vector2>(sub.Count + 1);
                    foreach (var p in sub) points.Add(new Vector2((p.x - vbx) / vbh, (p.y - vby) / vbh));
                    bool closed = r.Closed[r.Subpaths.IndexOf(sub)];
                    if (closed && (points[0] - points[points.Count - 1]).sqrMagnitude > 1e-10f) points.Add(points[0]);
                    shape.Subpaths.Add(points.ToArray());
                    shape.Closed.Add(closed);
                }
                if (shape.Subpaths.Count == 0) continue;
                shape.Cumulative = new float[shape.Subpaths.Count][];
                for (int s = 0; s < shape.Subpaths.Count; s++)
                {
                    var points = shape.Subpaths[s];
                    var cumulative = new float[points.Length];
                    for (int i = 1; i < points.Length; i++) cumulative[i] = cumulative[i - 1] + Vector2.Distance(points[i - 1], points[i]);
                    shape.Cumulative[s] = cumulative;
                    shape.Length += cumulative[cumulative.Length - 1];
                }
                if (shape.Length < .004f) continue;
                Shapes.Add(shape);
                stroked.Add(shape);
            }
            // Sketch windows: long shapes take longer (sub-linear so small details are not instant), a pen lift between them.
            float weightSum = 0f;
            foreach (var shape in stroked) weightSum += Weight(shape);
            float gap = Mathf.Min(GapBetweenStrokes, MaxTotalGap / Mathf.Max(1, stroked.Count - 1));
            float usable = Mathf.Max(.1f, 1f - gap * Mathf.Max(0, stroked.Count - 1));
            float cursor = 0f;
            foreach (var shape in stroked)
            {
                float width = usable * Weight(shape) / Mathf.Max(.0001f, weightSum);
                shape.RevealStart = cursor;
                shape.RevealEnd = cursor + width;
                cursor += width + gap;
                TotalLength += shape.Length;
            }
        }

        private static float Weight(ArtShape shape) => Mathf.Pow(shape.Length, .75f) + .02f;

        // ------------------------------------------------------------------ SVG walk

        private sealed class ParseContext { public float Tolerance; }

        private sealed class RawShape
        {
            public readonly List<List<Vector2>> Subpaths = new List<List<Vector2>>();
            public readonly List<bool> Closed = new List<bool>();
            public Color FillColor, StrokeColor;
            public float StrokeWidth;
            public float Taper;
            public bool EvenOdd;
        }

        private struct Style
        {
            public string Fill, Stroke;
            public float StrokeWidth, Opacity, FillOpacity, StrokeOpacity, Taper;
            public bool EvenOdd;
            public Affine Transform;
            public static Style Default => new Style
            {
                Fill = "#000000", Stroke = null, StrokeWidth = 1f, Opacity = 1f, FillOpacity = 1f, StrokeOpacity = 1f, Taper = DefaultTaper,
                Transform = Affine.Identity
            };
        }

        private static void Walk(XElement element, Style inherited, List<RawShape> output, ParseContext context)
        {
            string tag = element.Name.LocalName;
            if (tag == "defs" || tag == "clipPath" || tag == "mask" || tag == "style" || tag == "title" || tag == "desc"
                || tag == "linearGradient" || tag == "radialGradient" || tag == "pattern" || tag == "filter" || tag == "symbol")
                return;
            Style style = Resolve(element, inherited);
            switch (tag)
            {
                case "path": AddPath(element, style, output, context); return;
                case "rect": AddRect(element, style, output); return;
                case "circle": AddEllipse(element, style, output, true); return;
                case "ellipse": AddEllipse(element, style, output, false); return;
                case "line": AddLine(element, style, output); return;
                case "polyline": AddPoly(element, style, output, false); return;
                case "polygon": AddPoly(element, style, output, true); return;
            }
            foreach (var child in element.Elements()) Walk(child, style, output, context);
        }

        private static Style Resolve(XElement element, Style parent)
        {
            Style s = parent;
            var declarations = new Dictionary<string, string>();
            foreach (var attribute in element.Attributes())
                if (attribute.Name.NamespaceName.Length == 0) declarations[attribute.Name.LocalName] = attribute.Value.Trim();
            string css = Attr(element, "style");
            if (!string.IsNullOrEmpty(css))
                foreach (var part in css.Split(';'))
                {
                    int colon = part.IndexOf(':');
                    if (colon > 0) declarations[part.Substring(0, colon).Trim()] = part.Substring(colon + 1).Trim();
                }
            string value;
            if (declarations.TryGetValue("fill", out value)) s.Fill = value;
            if (declarations.TryGetValue("stroke", out value)) s.Stroke = value;
            if (declarations.TryGetValue("stroke-width", out value)) s.StrokeWidth = ParseLength(value, parent.StrokeWidth);
            if (declarations.TryGetValue("opacity", out value)) s.Opacity = parent.Opacity * Mathf.Clamp01(ParseLength(value, 1f));
            if (declarations.TryGetValue("fill-opacity", out value)) s.FillOpacity = Mathf.Clamp01(ParseLength(value, 1f));
            if (declarations.TryGetValue("stroke-opacity", out value)) s.StrokeOpacity = Mathf.Clamp01(ParseLength(value, 1f));
            if (declarations.TryGetValue("fill-rule", out value)) s.EvenOdd = value == "evenodd";
            if (declarations.TryGetValue("data-taper", out value)) s.Taper = Mathf.Clamp(ParseLength(value, DefaultTaper), 0f, .5f);
            if (declarations.TryGetValue("transform", out value)) s.Transform = parent.Transform.Multiply(Affine.Parse(value));
            return s;
        }

        private static RawShape NewShape(Style style)
        {
            var shape = new RawShape { EvenOdd = style.EvenOdd, Taper = style.Taper };
            shape.FillColor = ResolveColor(style.Fill, style.Opacity * style.FillOpacity);
            shape.StrokeColor = ResolveColor(style.Stroke, style.Opacity * style.StrokeOpacity);
            shape.StrokeWidth = style.StrokeWidth * style.Transform.UniformScale;
            return shape;
        }

        private static Color ResolveColor(string value, float opacity)
        {
            if (string.IsNullOrEmpty(value) || value == "none" || value == "transparent") return new Color(0f, 0f, 0f, 0f);
            Color color;
            if (value == "currentColor" || value.StartsWith("url(", StringComparison.Ordinal)) color = PencilGold;
            else if (!TryColor(value, out color)) color = PencilGold;
            color.a *= opacity;
            return color;
        }

        private static readonly Dictionary<string, string> NamedColors = new Dictionary<string, string>
        {
            { "black", "#000000" }, { "white", "#FFFFFF" }, { "red", "#FF0000" }, { "green", "#008000" }, { "blue", "#0000FF" },
            { "yellow", "#FFFF00" }, { "orange", "#FFA500" }, { "gray", "#808080" }, { "grey", "#808080" }, { "silver", "#C0C0C0" },
            { "brown", "#A52A2A" }, { "purple", "#800080" }, { "cyan", "#00FFFF" }, { "aqua", "#00FFFF" }, { "magenta", "#FF00FF" },
            { "fuchsia", "#FF00FF" }, { "lime", "#00FF00" }, { "navy", "#000080" }, { "teal", "#008080" }, { "maroon", "#800000" },
            { "olive", "#808000" }, { "gold", "#FFD700" }, { "goldenrod", "#DAA520" }, { "darkgoldenrod", "#B8860B" },
            { "khaki", "#F0E68C" }, { "wheat", "#F5DEB3" }, { "tan", "#D2B48C" }, { "dimgray", "#696969" }, { "dimgrey", "#696969" },
            { "darkgray", "#A9A9A9" }, { "darkgrey", "#A9A9A9" }, { "lightgray", "#D3D3D3" }, { "lightgrey", "#D3D3D3" },
            { "ivory", "#FFFFF0" }, { "beige", "#F5F5DC" }, { "sienna", "#A0522D" }, { "peru", "#CD853F" }, { "chocolate", "#D2691E" },
            { "darkorange", "#FF8C00" }, { "orangered", "#FF4500" }, { "crimson", "#DC143C" }
        };

        private static bool TryColor(string value, out Color color)
        {
            color = Color.black;
            value = value.Trim();
            if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                int open = value.IndexOf('('), close = value.IndexOf(')');
                if (open < 0 || close < open) return false;
                var parts = value.Substring(open + 1, close - open - 1).Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) return false;
                var channels = new float[4] { 0f, 0f, 0f, 1f };
                for (int i = 0; i < Mathf.Min(4, parts.Length); i++)
                {
                    string part = parts[i];
                    bool percent = part.EndsWith("%", StringComparison.Ordinal);
                    if (percent) part = part.Substring(0, part.Length - 1);
                    float number;
                    if (!float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return false;
                    channels[i] = i == 3 ? (percent ? number / 100f : number) : (percent ? number / 100f : number / 255f);
                }
                color = new Color(Mathf.Clamp01(channels[0]), Mathf.Clamp01(channels[1]), Mathf.Clamp01(channels[2]), Mathf.Clamp01(channels[3]));
                return true;
            }
            string named;
            if (NamedColors.TryGetValue(value.ToLowerInvariant(), out named)) value = named;
            if (value.Length < 4 || value[0] != '#') return false;
            string hex = value.Substring(1);
            if (hex.Length == 3 || hex.Length == 4)
            {
                var expanded = new System.Text.StringBuilder();
                foreach (char c in hex) { expanded.Append(c); expanded.Append(c); }
                hex = expanded.ToString();
            }
            if (hex.Length != 6 && hex.Length != 8) return false;
            int rgba;
            var channel = new float[4] { 0f, 0f, 0f, 1f };
            for (int i = 0; i < hex.Length / 2; i++)
            {
                if (!int.TryParse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgba)) return false;
                channel[i] = rgba / 255f;
            }
            color = new Color(channel[0], channel[1], channel[2], channel[3]);
            return true;
        }

        // ------------------------------------------------------------------ primitives

        private static void AddRect(XElement e, Style style, List<RawShape> output)
        {
            float x = Num(e, "x"), y = Num(e, "y"), w = Num(e, "width"), h = Num(e, "height");
            if (w <= 0f || h <= 0f) return;
            var shape = NewShape(style);
            var pts = new List<Vector2> { new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h) };
            AddTransformed(shape, pts, true, style.Transform);
            output.Add(shape);
        }

        private static void AddEllipse(XElement e, Style style, List<RawShape> output, bool circle)
        {
            float cx = Num(e, "cx"), cy = Num(e, "cy");
            float rx = circle ? Num(e, "r") : Num(e, "rx"), ry = circle ? rx : Num(e, "ry");
            if (rx <= 0f || ry <= 0f) return;
            var shape = NewShape(style);
            int steps = 48;
            var pts = new List<Vector2>(steps);
            for (int i = 0; i < steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                pts.Add(new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry));
            }
            AddTransformed(shape, pts, true, style.Transform);
            output.Add(shape);
        }

        private static void AddLine(XElement e, Style style, List<RawShape> output)
        {
            var shape = NewShape(style);
            shape.FillColor = new Color(0f, 0f, 0f, 0f);
            var pts = new List<Vector2> { new Vector2(Num(e, "x1"), Num(e, "y1")), new Vector2(Num(e, "x2"), Num(e, "y2")) };
            AddTransformed(shape, pts, false, style.Transform);
            output.Add(shape);
        }

        private static void AddPoly(XElement e, Style style, List<RawShape> output, bool closed)
        {
            var numbers = Numbers(Attr(e, "points"));
            if (numbers.Count < 4) return;
            var shape = NewShape(style);
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < numbers.Count; i += 2) pts.Add(new Vector2(numbers[i], numbers[i + 1]));
            if (!closed) shape.FillColor = new Color(0f, 0f, 0f, 0f);
            AddTransformed(shape, pts, closed, style.Transform);
            output.Add(shape);
        }

        private static void AddTransformed(RawShape shape, List<Vector2> points, bool closed, Affine transform)
        {
            var list = new List<Vector2>(points.Count);
            foreach (var p in points) list.Add(transform.Apply(p));
            shape.Subpaths.Add(list);
            shape.Closed.Add(closed);
        }

        private static void AddPath(XElement e, Style style, List<RawShape> output, ParseContext context)
        {
            string d = Attr(e, "d");
            if (string.IsNullOrWhiteSpace(d)) return;
            var shape = NewShape(style);
            foreach (var sub in PathFlattener.Flatten(d, context.Tolerance))
            {
                var list = new List<Vector2>(sub.Points.Count);
                foreach (var p in sub.Points) list.Add(style.Transform.Apply(p));
                shape.Subpaths.Add(list);
                shape.Closed.Add(sub.Closed);
            }
            if (shape.Subpaths.Count > 0) output.Add(shape);
        }

        // ------------------------------------------------------------------ helpers

        private static string Attr(XElement e, string name)
        {
            var a = e.Attribute(name);
            return a == null ? null : a.Value;
        }

        private static float Num(XElement e, string name) => ParseLength(Attr(e, name), 0f);

        private static float ParseLength(string text, float fallback = 0f)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            text = text.Trim();
            int end = text.Length;
            while (end > 0 && !(char.IsDigit(text[end - 1]) || text[end - 1] == '.')) end--;
            float value;
            return end > 0 && float.TryParse(text.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static List<float> Numbers(string text)
        {
            var result = new List<float>();
            if (string.IsNullOrEmpty(text)) return result;
            foreach (Match match in Regex.Matches(text, @"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?"))
            {
                float v;
                if (float.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) result.Add(v);
            }
            return result;
        }

        private static bool TryViewBox(string text, out float x, out float y, out float w, out float h)
        {
            x = y = w = h = 0f;
            var n = Numbers(text);
            if (n.Count != 4 || n[2] <= 0f || n[3] <= 0f) return false;
            x = n[0]; y = n[1]; w = n[2]; h = n[3];
            return true;
        }

        /// <summary>2D affine matrix: x' = A x + C y + E, y' = B x + D y + F.</summary>
        public readonly struct Affine
        {
            public readonly float A, B, C, D, E, F;
            public Affine(float a, float b, float c, float d, float e, float f) { A = a; B = b; C = c; D = d; E = e; F = f; }
            public static Affine Identity => new Affine(1f, 0f, 0f, 1f, 0f, 0f);
            public float UniformScale => Mathf.Sqrt(Mathf.Abs(A * D - B * C));
            public Vector2 Apply(Vector2 p) => new Vector2(A * p.x + C * p.y + E, B * p.x + D * p.y + F);
            /// <summary>This applied after <paramref name="m"/>.</summary>
            public Affine Multiply(Affine m) => new Affine(
                A * m.A + C * m.B, B * m.A + D * m.B, A * m.C + C * m.D, B * m.C + D * m.D,
                A * m.E + C * m.F + E, B * m.E + D * m.F + F);

            public static Affine Parse(string text)
            {
                Affine result = Identity;
                if (string.IsNullOrWhiteSpace(text)) return result;
                foreach (Match match in Regex.Matches(text, @"(\w+)\s*\(([^)]*)\)"))
                {
                    var a = Numbers(match.Groups[2].Value);
                    Affine t = Identity;
                    switch (match.Groups[1].Value)
                    {
                        case "matrix": if (a.Count >= 6) t = new Affine(a[0], a[1], a[2], a[3], a[4], a[5]); break;
                        case "translate": if (a.Count >= 1) t = new Affine(1f, 0f, 0f, 1f, a[0], a.Count > 1 ? a[1] : 0f); break;
                        case "scale": if (a.Count >= 1) t = new Affine(a[0], 0f, 0f, a.Count > 1 ? a[1] : a[0], 0f, 0f); break;
                        case "rotate":
                            if (a.Count >= 1)
                            {
                                float r = a[0] * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
                                var rot = new Affine(cs, sn, -sn, cs, 0f, 0f);
                                if (a.Count >= 3)
                                    t = new Affine(1f, 0f, 0f, 1f, a[1], a[2]).Multiply(rot).Multiply(new Affine(1f, 0f, 0f, 1f, -a[1], -a[2]));
                                else t = rot;
                            }
                            break;
                        case "skewX": if (a.Count >= 1) t = new Affine(1f, 0f, Mathf.Tan(a[0] * Mathf.Deg2Rad), 1f, 0f, 0f); break;
                        case "skewY": if (a.Count >= 1) t = new Affine(1f, Mathf.Tan(a[0] * Mathf.Deg2Rad), 0f, 1f, 0f, 0f); break;
                    }
                    result = result.Multiply(t);
                }
                return result;
            }
        }
    }

    /// <summary>SVG path data to flattened polylines (curves subdivided by length, arcs by angle).</summary>
    public static class PathFlattener
    {
        public sealed class Subpath
        {
            public readonly List<Vector2> Points = new List<Vector2>();
            public bool Closed;
        }

        public static List<Subpath> Flatten(string d, float tolerance)
        {
            var result = new List<Subpath>();
            int i = 0;
            char cmd = ' ';
            Vector2 cur = Vector2.zero, start = Vector2.zero, lastCubic = Vector2.zero, lastQuad = Vector2.zero;
            char previous = ' ';
            Subpath path = null;
            bool first = true;

            Action<Vector2> begin = p => { path = new Subpath(); path.Points.Add(p); result.Add(path); };
            Action ensure = () => { if (path == null) begin(cur); };

            while (true)
            {
                SkipSeparators(d, ref i);
                if (i >= d.Length) break;
                char ch = d[i];
                if (char.IsLetter(ch)) { cmd = ch; i++; }
                else if (cmd == ' ') break;
                else if (cmd == 'M') cmd = 'L';
                else if (cmd == 'm') cmd = 'l';
                else if (cmd == 'Z' || cmd == 'z') break;

                bool relative = char.IsLower(cmd);
                char upper = char.ToUpperInvariant(cmd);
                float x, y, x1, y1, x2, y2;
                switch (upper)
                {
                    case 'M':
                        if (!Read(d, ref i, out x) || !Read(d, ref i, out y)) return result;
                        cur = relative && !first ? cur + new Vector2(x, y) : new Vector2(x, y);
                        start = cur; path = null; begin(cur);
                        break;
                    case 'L':
                        if (!Read(d, ref i, out x) || !Read(d, ref i, out y)) return result;
                        ensure(); cur = relative ? cur + new Vector2(x, y) : new Vector2(x, y); path.Points.Add(cur);
                        break;
                    case 'H':
                        if (!Read(d, ref i, out x)) return result;
                        ensure(); cur = new Vector2(relative ? cur.x + x : x, cur.y); path.Points.Add(cur);
                        break;
                    case 'V':
                        if (!Read(d, ref i, out y)) return result;
                        ensure(); cur = new Vector2(cur.x, relative ? cur.y + y : y); path.Points.Add(cur);
                        break;
                    case 'C':
                    case 'S':
                    {
                        Vector2 c1;
                        if (upper == 'C')
                        {
                            if (!Read(d, ref i, out x1) || !Read(d, ref i, out y1)) return result;
                            c1 = relative ? cur + new Vector2(x1, y1) : new Vector2(x1, y1);
                        }
                        else c1 = previous == 'C' || previous == 'S' ? cur * 2f - lastCubic : cur;
                        if (!Read(d, ref i, out x2) || !Read(d, ref i, out y2) || !Read(d, ref i, out x) || !Read(d, ref i, out y)) return result;
                        Vector2 c2 = relative ? cur + new Vector2(x2, y2) : new Vector2(x2, y2);
                        Vector2 end = relative ? cur + new Vector2(x, y) : new Vector2(x, y);
                        ensure(); Cubic(path.Points, cur, c1, c2, end, tolerance);
                        lastCubic = c2; cur = end;
                        break;
                    }
                    case 'Q':
                    case 'T':
                    {
                        Vector2 c1;
                        if (upper == 'Q')
                        {
                            if (!Read(d, ref i, out x1) || !Read(d, ref i, out y1)) return result;
                            c1 = relative ? cur + new Vector2(x1, y1) : new Vector2(x1, y1);
                        }
                        else c1 = previous == 'Q' || previous == 'T' ? cur * 2f - lastQuad : cur;
                        if (!Read(d, ref i, out x) || !Read(d, ref i, out y)) return result;
                        Vector2 end = relative ? cur + new Vector2(x, y) : new Vector2(x, y);
                        ensure(); Quadratic(path.Points, cur, c1, end, tolerance);
                        lastQuad = c1; cur = end;
                        break;
                    }
                    case 'A':
                    {
                        float rx, ry, rot;
                        bool large, sweep;
                        if (!Read(d, ref i, out rx) || !Read(d, ref i, out ry) || !Read(d, ref i, out rot)
                            || !ReadFlag(d, ref i, out large) || !ReadFlag(d, ref i, out sweep)
                            || !Read(d, ref i, out x) || !Read(d, ref i, out y)) return result;
                        Vector2 end = relative ? cur + new Vector2(x, y) : new Vector2(x, y);
                        ensure(); Arc(path.Points, cur, rx, ry, rot, large, sweep, end);
                        cur = end;
                        break;
                    }
                    case 'Z':
                        if (path != null) { path.Closed = true; cur = start; path = null; }
                        break;
                    default:
                        return result; // unknown command: keep what was read
                }
                previous = upper;
                first = false;
            }
            return result;
        }

        private static void Cubic(List<Vector2> pts, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float tolerance)
        {
            float length = (p1 - p0).magnitude + (p2 - p1).magnitude + (p3 - p2).magnitude;
            int steps = Mathf.Clamp(Mathf.CeilToInt(length / Mathf.Max(.0001f, tolerance)), 4, 64);
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps, u = 1f - t;
                pts.Add(u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3);
            }
        }

        private static void Quadratic(List<Vector2> pts, Vector2 p0, Vector2 p1, Vector2 p2, float tolerance)
        {
            float length = (p1 - p0).magnitude + (p2 - p1).magnitude;
            int steps = Mathf.Clamp(Mathf.CeilToInt(length / Mathf.Max(.0001f, tolerance)), 3, 48);
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps, u = 1f - t;
                pts.Add(u * u * p0 + 2f * u * t * p1 + t * t * p2);
            }
        }

        private static void Arc(List<Vector2> pts, Vector2 p0, float rx, float ry, float phiDegrees, bool large, bool sweep, Vector2 p1)
        {
            if ((p0 - p1).sqrMagnitude < 1e-12f) return;
            rx = Mathf.Abs(rx); ry = Mathf.Abs(ry);
            if (rx < 1e-6f || ry < 1e-6f) { pts.Add(p1); return; }
            float phi = phiDegrees * Mathf.Deg2Rad, cp = Mathf.Cos(phi), sp = Mathf.Sin(phi);
            float dx = (p0.x - p1.x) * .5f, dy = (p0.y - p1.y) * .5f;
            float x1 = cp * dx + sp * dy, y1 = -sp * dx + cp * dy;
            float lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lambda > 1f) { float s = Mathf.Sqrt(lambda); rx *= s; ry *= s; }
            float num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            float den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            float coef = (large == sweep ? -1f : 1f) * Mathf.Sqrt(Mathf.Max(0f, num / Mathf.Max(1e-12f, den)));
            float cxp = coef * rx * y1 / ry, cyp = -coef * ry * x1 / rx;
            float cx = cp * cxp - sp * cyp + (p0.x + p1.x) * .5f, cy = sp * cxp + cp * cyp + (p0.y + p1.y) * .5f;
            float theta1 = Mathf.Atan2((y1 - cyp) / ry, (x1 - cxp) / rx);
            float theta2 = Mathf.Atan2((-y1 - cyp) / ry, (-x1 - cxp) / rx);
            float delta = theta2 - theta1;
            if (!sweep && delta > 0f) delta -= Mathf.PI * 2f;
            else if (sweep && delta < 0f) delta += Mathf.PI * 2f;
            int steps = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(delta) / (Mathf.PI / 18f)));
            for (int s = 1; s <= steps; s++)
            {
                float a = theta1 + delta * s / steps, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                pts.Add(s == steps ? p1 : new Vector2(cx + rx * ca * cp - ry * sa * sp, cy + rx * ca * sp + ry * sa * cp));
            }
        }

        private static void SkipSeparators(string s, ref int i)
        {
            while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ',')) i++;
        }

        private static bool ReadFlag(string s, ref int i, out bool flag)
        {
            SkipSeparators(s, ref i);
            flag = false;
            if (i >= s.Length || (s[i] != '0' && s[i] != '1')) return false;
            flag = s[i] == '1';
            i++;
            return true;
        }

        private static bool Read(string s, ref int i, out float value)
        {
            SkipSeparators(s, ref i);
            value = 0f;
            int begin = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            bool digits = false, dot = false;
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsDigit(c)) { digits = true; i++; }
                else if (c == '.' && !dot) { dot = true; i++; }
                else break;
            }
            if (!digits) { i = begin; return false; }
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                int save = i;
                i++;
                if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
                int expDigits = i;
                while (i < s.Length && char.IsDigit(s[i])) i++;
                if (i == expDigits) i = save;
            }
            return float.TryParse(s.Substring(begin, i - begin), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
