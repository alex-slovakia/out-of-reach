using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI.Report
{
    /// <summary>Dated NASA heliocentric reference. One scale for mean-radius spheres and XY positions; no simulation authority.</summary>
    public sealed class ReferenceOrbitView : VisualElement
    {
        [Serializable] private sealed class Point { public double x, y, z; }
        [Serializable] private sealed class Body
        {
            public string name; public int horizonsId; public double radiusKm, x, y, z;
            public Point[] path;
        }
        [Serializable] private sealed class Dataset
        {
            public int schema, sampleStepDays; public string epochUtc, traceEndUtc, origin, referenceFrame, units, correction;
            public Body[] bodies;
        }
        private readonly VisualElement canvas;
        private readonly Label scaleLabel;
        private readonly Dataset data;
        private double centreX, centreY, halfHeightKm = 300000000d;
        private bool locators = true, dragging;
        private int pointerId = -1;
        private Vector2 lastPointer;
        private static readonly Color[] Colours = { new Color(1f,.76f,.35f), new Color(.41f,.75f,.89f), new Color(.86f,.44f,.28f) };

        public ReferenceOrbitView(bool compact = false)
        {
            name = "reference-orbit-view";
            AddToClassList("reference-orbits");
            EnableInClassList("reference-orbits-compact", compact);
            StyleSheet sheet = Resources.Load<StyleSheet>("OorReferenceOrbitStyle");
            if (sheet) styleSheets.Add(sheet);
            Add(Text("Sun · Earth · Mars", "reference-orbits-title"));
            try
            {
                TextAsset source = Resources.Load<TextAsset>("OorReferenceOrbits");
                if (!source) throw new InvalidOperationException("Committed orbital reference is missing.");
                data = JsonUtility.FromJson<Dataset>(source.text);
                Validate(data);
            }
            catch (Exception error)
            {
                Add(Text("Reference unavailable: " + error.Message, "reference-orbits-note"));
                return;
            }
            Add(Text(data.epochUtc + " · fixed reference epoch", "reference-orbits-note"));
            Add(Text("True scale · top-down ecliptic projection", "reference-orbits-note"));
            var controls = new VisualElement { name = "reference-orbits-controls" };
            controls.AddToClassList("reference-orbits-controls");
            controls.Add(Control("Reset", "orbit-reset", Reset));
            controls.Add(Control("+", "orbit-zoom-in", () => Zoom(2d, CanvasCentre)));
            controls.Add(Control("−", "orbit-zoom-out", () => Zoom(.5d, CanvasCentre)));
            for (int i = 0; i < data.bodies.Length; i++)
            {
                int index = i;
                Button button = Control("Focus " + data.bodies[i].name, "orbit-focus-" + data.bodies[i].name.ToLowerInvariant(), () => FocusBody(index));
                button.AddToClassList("orbit-body-" + i);
                controls.Add(button);
            }
            Add(controls);
            var marks = new Toggle("Locator crosses (not physical size)") { value = true, name = "orbit-locators" };
            marks.AddToClassList("reference-orbits-toggle");
            marks.RegisterValueChangedCallback(evt => { locators = evt.newValue; Repaint(); });
            Add(marks);
            canvas = new VisualElement { name = "reference-orbits-canvas", focusable = true };
            canvas.AddToClassList("reference-orbits-canvas");
            canvas.generateVisualContent += Draw;
            canvas.RegisterCallback<GeometryChangedEvent>(_ => Repaint());
            canvas.RegisterCallback<WheelEvent>(OnWheel);
            canvas.RegisterCallback<PointerDownEvent>(OnPointerDown);
            canvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            canvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
            canvas.RegisterCallback<PointerCaptureOutEvent>(_ => { dragging = false; pointerId = -1; });
            canvas.RegisterCallback<DetachFromPanelEvent>(_ => ReleasePointer());
            canvas.RegisterCallback<KeyDownEvent>(OnKeyDown);
            Add(canvas);
            scaleLabel = Text(string.Empty, "reference-orbits-scale");
            scaleLabel.name = "reference-orbits-scale";
            Add(scaleLabel);
            Add(Text("Drag to pan · wheel or +/− to zoom · Focus to inspect a body.", "reference-orbits-note"));
            var details = new Foldout { text = "Scale & data details", value = false, name = "reference-orbits-details" };
            details.AddToClassList("reference-orbits-details");
            details.Add(Text("One pixels-per-kilometre scale for positions and mean radii. XY projection omits depth; projected distance differs from 3D range.", "reference-orbits-note"));
            double dx = data.bodies[1].x - data.bodies[2].x, dy = data.bodies[1].y - data.bodies[2].y, dz = data.bodies[1].z - data.bodies[2].z;
            details.Add(Text("Earth–Mars: " + Km(Math.Sqrt(dx*dx + dy*dy + dz*dz)) + " km in 3D; "
                + Km(Math.Sqrt(dx*dx + dy*dy)) + " km in this projection.", "reference-orbits-note"));
            details.Add(Text("Mean radii: Sun 695,700 km · Earth 6,371.0084 km · Mars 3,389.5 km.", "reference-orbits-note"));
            details.Add(Text("Planets may be smaller than a pixel at orbital scale. Focus reveals their true scaled discs. Arrow keys pan when the view is focused.", "reference-orbits-note"));
            details.Add(Text("NASA/JPL Horizons · daily geometric samples to " + data.traceEndUtc
                + ". Lines join daily samples; spheres use NASA mean radii. Not live mission positions or a spacecraft trajectory.", "reference-orbits-note"));
            Add(details);
            if (compact)
            {
                var explore = Control("Explore true scale", "orbit-explore", () =>
                {
                    bool collapsed = ClassListContains("reference-orbits-compact");
                    EnableInClassList("reference-orbits-compact", !collapsed);
                    this.Q<Button>("orbit-explore").text = collapsed ? "Close orbit controls" : "Explore true scale";
                    if (!collapsed) Reset();
                });
                explore.AddToClassList("reference-orbits-explore");
                Add(explore);
                Add(Text("Sun · Earth · Mars / 14 Nov 2026 · Locator marks, bodies at true scale", "reference-orbits-preview-caption"));
            }
            Repaint();
        }

        private Vector2 CanvasCentre => canvas == null ? Vector2.zero : canvas.contentRect.center;
        private double PixelsPerKm => canvas == null ? 0d : canvas.contentRect.height / (2d * halfHeightKm);
        private static string Km(double value) => value.ToString("N0", CultureInfo.InvariantCulture);
        private static Label Text(string text, string css)
        {
            var label = new Label(text) { enableRichText = false, pickingMode = PickingMode.Ignore };
            label.AddToClassList(css); return label;
        }
        private static Button Control(string text, string name, Action action)
        {
            var button = new Button(action) { text = text, name = name };
            button.AddToClassList("reference-orbits-button"); return button;
        }
        private static bool Finite(double number) => !double.IsNaN(number) && !double.IsInfinity(number);
        private static void Validate(Dataset value)
        {
            if (value == null || value.schema != 1 || value.units != "km" || value.correction != "NONE"
                || value.bodies == null || value.bodies.Length != 3 || value.sampleStepDays != 1)
                throw new InvalidOperationException("Unsupported reference dataset.");
            int[] ids = { 10, 399, 499 };
            for (int i = 0; i < value.bodies.Length; i++)
            {
                Body body = value.bodies[i];
                if (body == null || body.horizonsId != ids[i] || !Finite(body.radiusKm) || body.radiusKm <= 0
                    || !Finite(body.x) || !Finite(body.y) || !Finite(body.z) || body.path == null
                    || (i > 0 && body.path.Length != 701)) throw new InvalidOperationException("Invalid body reference.");
                foreach (Point point in body.path)
                    if (point == null || !Finite(point.x) || !Finite(point.y) || !Finite(point.z))
                        throw new InvalidOperationException("Non-finite orbital sample.");
            }
            if (value.bodies[0].x != 0d || value.bodies[0].y != 0d || value.bodies[0].z != 0d)
                throw new InvalidOperationException("Reference origin must be the Sun.");
        }
        private void Reset() { centreX = centreY = 0d; halfHeightKm = 300000000d; Repaint(); }
        private void FocusBody(int index)
        {
            Body body = data.bodies[index]; centreX = body.x; centreY = body.y;
            halfHeightKm = body.radiusKm * 2.4d; Repaint();
        }
        private void Zoom(double factor, Vector2 anchor)
        {
            double oldScale = PixelsPerKm;
            if (oldScale <= 0d) return;
            Vector2 offset = anchor - CanvasCentre;
            double x = centreX + offset.x / oldScale, y = centreY - offset.y / oldScale;
            halfHeightKm = Math.Max(5d, Math.Min(1500000000d, halfHeightKm / factor));
            centreX = x - offset.x / PixelsPerKm;
            centreY = y + offset.y / PixelsPerKm;
            Repaint();
        }
        private void OnWheel(WheelEvent evt)
        {
            Zoom(Math.Exp(Math.Max(-2d, Math.Min(2d, -evt.delta.y * .16d))), evt.localMousePosition);
            evt.StopPropagation();
        }
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || dragging) return;
            dragging = true; pointerId = evt.pointerId; lastPointer = evt.localPosition;
            canvas.CapturePointer(pointerId); canvas.Focus(); evt.StopPropagation();
        }
        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!dragging || pointerId != evt.pointerId || PixelsPerKm <= 0d) return;
            Vector2 next = evt.localPosition, delta = next - lastPointer; lastPointer = next;
            centreX -= delta.x / PixelsPerKm; centreY += delta.y / PixelsPerKm;
            Repaint(); evt.StopPropagation();
        }
        private void OnPointerUp(PointerUpEvent evt)
        {
            if (pointerId != evt.pointerId) return;
            ReleasePointer(); evt.StopPropagation();
        }
        private void ReleasePointer()
        {
            int released = pointerId; pointerId = -1; dragging = false;
            if (canvas != null && released >= 0 && canvas.HasPointerCapture(released)) canvas.ReleasePointer(released);
        }
        private void OnKeyDown(KeyDownEvent evt)
        {
            double step = halfHeightKm * .18d;
            switch (evt.keyCode)
            {
                case KeyCode.LeftArrow: centreX -= step; break;
                case KeyCode.RightArrow: centreX += step; break;
                case KeyCode.UpArrow: centreY += step; break;
                case KeyCode.DownArrow: centreY -= step; break;
                default: return;
            }
            Repaint(); evt.StopPropagation();
        }
        private void Repaint()
        {
            if (canvas == null) return;
            double scale = PixelsPerKm;
            if (scaleLabel != null && scale > 0d)
                scaleLabel.text = "100 px = " + Km(100d / scale) + " km · body radii are not enlarged";
            canvas.MarkDirtyRepaint();
        }
        private void Project(double x, double y, out double px, out double py)
        {
            px = (x - centreX) * PixelsPerKm + CanvasCentre.x;
            py = -(y - centreY) * PixelsPerKm + CanvasCentre.y;
        }
        private void Draw(MeshGenerationContext context)
        {
            Rect rect = canvas.contentRect;
            if (rect.width < 1f || rect.height < 1f || data == null) return;
            Painter2D painter = context.painter2D;
            for (int i = 1; i < data.bodies.Length; i++)
            {
                Color trace = Colours[i]; trace.a = .43f;
                painter.strokeColor = trace; painter.lineWidth = 1f; painter.BeginPath();
                Point[] points = data.bodies[i].path;
                for (int j = 1; j < points.Length; j++)
                {
                    Project(points[j-1].x, points[j-1].y, out double x0, out double y0);
                    Project(points[j].x, points[j].y, out double x1, out double y1);
                    if (!ClipSegment(ref x0, ref y0, ref x1, ref y1, rect)) continue;
                    painter.MoveTo(new Vector2((float)x0, (float)y0)); painter.LineTo(new Vector2((float)x1, (float)y1));
                }
                painter.Stroke();
            }
            for (int i = 0; i < data.bodies.Length; i++)
            {
                Body body = data.bodies[i]; Project(body.x, body.y, out double x, out double y);
                DrawDisc(painter, x, y, body.radiusKm * PixelsPerKm, rect, Colours[i]);
                if (!locators || x < 8 || y < 8 || x > rect.width - 8 || y > rect.height - 8) continue;
                painter.strokeColor = Colours[i]; painter.lineWidth = 1f; painter.BeginPath();
                painter.MoveTo(new Vector2((float)x - 7, (float)y)); painter.LineTo(new Vector2((float)x - 3, (float)y));
                painter.MoveTo(new Vector2((float)x + 3, (float)y)); painter.LineTo(new Vector2((float)x + 7, (float)y));
                painter.MoveTo(new Vector2((float)x, (float)y - 7)); painter.LineTo(new Vector2((float)x, (float)y - 3));
                painter.MoveTo(new Vector2((float)x, (float)y + 3)); painter.LineTo(new Vector2((float)x, (float)y + 7));
                painter.Stroke();
            }
        }
        private static bool ClipSegment(ref double x0, ref double y0, ref double x1, ref double y1, Rect r)
        {
            double dx=x1-x0, dy=y1-y0, enter=0d, exit=1d;
            if (!Clip(-dx,x0-r.xMin,ref enter,ref exit) || !Clip(dx,r.xMax-x0,ref enter,ref exit)
                || !Clip(-dy,y0-r.yMin,ref enter,ref exit) || !Clip(dy,r.yMax-y0,ref enter,ref exit)) return false;
            x1=x0+exit*dx; y1=y0+exit*dy; x0+=enter*dx; y0+=enter*dy; return true;
        }
        private static bool Clip(double p,double q,ref double enter,ref double exit)
        {
            if (p==0d) return q>=0d;
            double t=q/p;
            if (p<0d) { if (t>exit) return false; enter=Math.Max(enter,t); }
            else { if (t<enter) return false; exit=Math.Min(exit,t); }
            return true;
        }
        // Intersect the actual circle with the viewport before converting to float/painter geometry.
        // Huge zoomed discs never send huge-radius arcs or out-of-range vertices to UI Toolkit.
        private static void DrawDisc(Painter2D painter,double x,double y,double radius,Rect r,Color colour)
        {
            if (radius<=0d || x+radius<r.xMin || x-radius>r.xMax || y+radius<r.yMin || y-radius>r.yMax) return;
            var angles=new List<double> { 0d,Math.PI*.5d,Math.PI,Math.PI*1.5d,Math.PI*2d };
            foreach (double edge in new[] {(double)r.xMin,r.xMax})
            {
                double c=(edge-x)/radius;
                if (c>=-1d&&c<=1d) { double a=Math.Acos(c); angles.Add(a); angles.Add(Math.PI*2d-a); }
            }
            foreach (double edge in new[] {(double)r.yMin,r.yMax})
            {
                double s=(edge-y)/radius;
                if (s>=-1d&&s<=1d) { double a=Math.Asin(s); angles.Add(a<0d?a+Math.PI*2d:a); angles.Add(Math.PI-a); }
            }
            angles.Sort(); var polygon=new List<Vector2>();
            double step=radius>.3d?2d*Math.Acos(Math.Max(-1d,1d-.3d/radius)):Math.PI/8d;
            for(int i=1;i<angles.Count;i++)
            {
                double start=angles[i-1],end=angles[i],mid=(start+end)*.5d;
                double mx=x+Math.Cos(mid)*radius,my=y+Math.Sin(mid)*radius;
                if(mx<r.xMin||mx>r.xMax||my<r.yMin||my>r.yMax)continue;
                int count=Math.Max(1,Math.Min(4096,(int)Math.Ceiling((end-start)/Math.Max(step,1e-7d))));
                for(int j=0;j<=count;j++)
                {
                    double angle=start+(end-start)*j/count;
                    polygon.Add(new Vector2((float)Math.Max(r.xMin,Math.Min(r.xMax,x+Math.Cos(angle)*radius)),
                        (float)Math.Max(r.yMin,Math.Min(r.yMax,y+Math.Sin(angle)*radius))));
                }
            }
            foreach(Vector2 corner in new[] {r.min,new Vector2(r.xMax,r.yMin),r.max,new Vector2(r.xMin,r.yMax)})
            {
                double dx=corner.x-x,dy=corner.y-y;
                if(dx*dx+dy*dy<=radius*radius)polygon.Add(corner);
            }
            if(polygon.Count<3)return;
            Vector2 centre=Vector2.zero; foreach(Vector2 point in polygon)centre+=point; centre/=polygon.Count;
            polygon.Sort((a,b)=>Math.Atan2(a.y-centre.y,a.x-centre.x).CompareTo(Math.Atan2(b.y-centre.y,b.x-centre.x)));
            painter.fillColor=colour;painter.BeginPath();painter.MoveTo(polygon[0]);
            for(int i=1;i<polygon.Count;i++)painter.LineTo(polygon[i]);
            painter.ClosePath();painter.Fill();
        }
    }
}

