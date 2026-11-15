using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI.Startup
{
    /// <summary>
    /// Self-contained startup presenter, an "ident" in two cards. Card 1: black, gold line-art logo (a goat and a Slovak
    /// double cross, imported from <c>Resources/StartupCards/startup-art.txt</c> as SVG) drawn on stroke by stroke with
    /// calligraphic, tapering ribbons and a glowing pen tip, a procedural pencil-scratch sound synced to the pen, then a
    /// glow sweep across the finished mark, and the team name in tracked-out capitals beneath. It holds, then
    /// cross-dissolves to card 2, the event card (text only), and fades into the opening menu underneath. A very subtle
    /// film grain lies over everything.
    /// It owns only its overlay: it never reads or writes mission state. Plays once per application launch
    /// (<see cref="HasPlayedThisLaunch"/>), skips with any key / mouse button / gamepad button, honours reduced effects
    /// (shorter, finished logo straight away, no sketch, glow sweep, grain or scratch sound), and swallows pointer, key
    /// and navigation input while visible so the menu behind cannot be activated by the skipping press.
    /// </summary>
    public sealed class StartupCardPresenter : VisualElement
    {
        public const string EventLine = "NASA Space Apps Challenge · Nov 14–15 2026";
        public const string TeamName = "Polish Alex Goats";
        /// <summary>Shown tracked out in capitals under the logo.</summary>
        public static readonly string TeamNameDisplay = TeamName.ToUpperInvariant();
        public const string ResourcesFolder = "StartupCards";
        public const string ArtResource = ResourcesFolder + "/startup-art";
        public const string StyleResource = ResourcesFolder + "/startup-cards";
        /// <summary>Presses during this many real seconds after start are ignored (a held key from launching the game).</summary>
        public const float SkipGraceSeconds = .35f;
        /// <summary>The pencil sound starts this much before the first stroke to cover audio output latency.</summary>
        private const float AudioLeadSeconds = .05f;
        private const float AudioVolume = .55f;
        private const float GrainOpacity = .05f;
        private const int GrainSize = 192;

        private static readonly Color Black = new Color(0f, 0f, 0f, 1f);
        private static readonly Color Gold = new Color(.961f, .737f, .455f, 1f);      // #F5BC74
        private static readonly Color GoldHot = new Color(1f, .93f, .80f, 1f);
        private static readonly Color Paper = new Color(.93f, .91f, .86f, 1f);

        private static bool played;

        private readonly StartupSequence sequence;
        private readonly VectorArt art;
        private readonly VisualElement pictogramCard;
        private readonly VisualElement artElement;
        private readonly Label teamLabel;
        private readonly VisualElement eventCard;
        private readonly VisualElement grain;
        private readonly Action finished;
        private readonly List<List<Vector2>> strokeScratch = new List<List<Vector2>>();
        private readonly Stack<List<Vector2>> pool = new Stack<List<Vector2>>();
        private readonly List<Vector2> ribbon = new List<Vector2>(512);
        private readonly System.Random grainRandom = new System.Random(24);
        private IVisualElementScheduledItem tick;
        private IDisposable anyButtonSubscription;
        private VisualElement trickleTarget;
        private StartupPencilAudio audio;
        private Texture2D grainTexture;
        private bool audioStarted;
        private float lastRealtime;
        private float sinceStart;
        private float nextGrainJitter;
        private float drawShown = -1f, fillShown = -1f;
        private bool completed;

        /// <summary>True once a startup sequence has begun in this application launch; a restart must not replay it.</summary>
        public static bool HasPlayedThisLaunch => played;

        public bool IsPlaying => !completed;
        public StartupSequence Sequence => sequence;
        public VectorArt Art => art;
        public VisualElement PictogramCard => pictogramCard;
        public VisualElement EventCard => eventCard;
        public VisualElement Grain => grain;
        /// <summary>Set false (tests, muted contexts) before <see cref="Begin"/> to skip the pencil sound.</summary>
        public bool PlayAudio { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => played = false;

        /// <summary>Loads the shipped art; an empty art (missing or invalid file) leaves the team name alone on card 1.</summary>
        public static VectorArt LoadArt()
        {
            var asset = Resources.Load<TextAsset>(ArtResource);
            return asset ? VectorArt.Parse(asset.text) : new VectorArt();
        }

        /// <summary>
        /// Plays the cards over <paramref name="host"/> (the UIDocument root the opening menu lives in) unless they have
        /// already played this launch. Returns null when it did not start; <paramref name="onFinished"/> is then not called.
        /// <paramref name="font"/> is only a fallback for when the shipped style sheet cannot be loaded.
        /// </summary>
        public static StartupCardPresenter PlayOnce(VisualElement host, bool reducedEffects, Font font = null,
            StyleSheet styleSheet = null, Action onFinished = null)
        {
            if (host == null || played) return null;
            var presenter = new StartupCardPresenter(reducedEffects, font, styleSheet, onFinished);
            host.Add(presenter);
            presenter.Begin();
            return presenter;
        }

        public StartupCardPresenter(bool reducedEffects, Font font = null, StyleSheet styleSheet = null, Action onFinished = null,
            VectorArt customArt = null)
        {
            finished = onFinished;
            art = customArt ?? LoadArt();
            sequence = new StartupSequence(reducedEffects);
            name = "startup-cards";
            pickingMode = PickingMode.Position;
            focusable = false;
            AddToClassList("startup-cards");
            StyleSheet sheet = styleSheet ? styleSheet : Resources.Load<StyleSheet>(StyleResource);
            if (sheet) styleSheets.Add(sheet);
            else if (font) style.unityFont = font;
            Fill(this);
            style.backgroundColor = Black;
            style.opacity = 1f;

            pictogramCard = CenterColumn("startup-pictogram-card");
            artElement = new VisualElement { name = "startup-art", pickingMode = PickingMode.Ignore };
            artElement.style.width = Length.Percent(62f);
            artElement.style.height = Length.Percent(62f);
            artElement.style.display = art.IsEmpty ? DisplayStyle.None : DisplayStyle.Flex;
            artElement.generateVisualContent += DrawArt;
            pictogramCard.Add(artElement);
            teamLabel = MakeLabel("startup-team-name", TeamNameDisplay, 30, Gold, 16f, "startup-team");
            teamLabel.style.marginTop = art.IsEmpty ? 0 : 14;
            pictogramCard.Add(teamLabel);
            Add(pictogramCard);

            eventCard = CenterColumn("startup-event-card");
            eventCard.Add(HairLine("startup-rule-top", 0, 28));
            eventCard.Add(MakeLabel("startup-event-line", EventLine, 34, Paper, 9f, "startup-event"));
            eventCard.Add(HairLine("startup-rule-bottom", 28, 0));
            Add(eventCard);

            grain = new VisualElement { name = "startup-grain", pickingMode = PickingMode.Ignore };
            Fill(grain);
            grain.style.opacity = 0f;
            grain.style.display = reducedEffects ? DisplayStyle.None : DisplayStyle.Flex;
            Add(grain);
            if (!reducedEffects) BuildGrain();

            Apply(sequence.Evaluate());
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        /// <summary>Starts the real-time clock, the pencil sound and the skip input. Called by <see cref="PlayOnce"/>.</summary>
        public void Begin()
        {
            if (completed) return;
            played = true;
            lastRealtime = Time.realtimeSinceStartup;
            sinceStart = 0f;
            if (PlayAudio && !sequence.ReducedEffects && !art.IsEmpty)
                audio = StartupPencilAudio.Create(PencilScratchSynth.Render(art, sequence.Timing));
            BringToFront();
            tick?.Pause();
            tick = schedule.Execute(OnTick).Every(16);
            SubscribeSkipInput();
        }

        /// <summary>Skip with a quick fade. Ignored during the grace window and once already skipping.</summary>
        public void RequestSkip()
        {
            if (completed || sinceStart < SkipGraceSeconds) return;
            sequence.Skip();
        }

        /// <summary>Advance by real seconds (also the test hook). Applies the frame and finishes when the timeline ends.</summary>
        public void Advance(float seconds)
        {
            if (completed) return;
            sinceStart += Mathf.Max(0f, seconds);
            sequence.Advance(seconds);
            var frame = sequence.Evaluate();
            Apply(frame);
            UpdateAudio(frame);
            JitterGrain(seconds);
            if (sequence.IsFinished) Complete();
        }

        private void OnTick(TimerState state)
        {
            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Max(0f, now - lastRealtime);
            lastRealtime = now;
            BringToFront(); // the session menu re-raises itself; the cards stay above it until done
            Advance(dt);
        }

        private void UpdateAudio(StartupFrame frame)
        {
            if (audio == null) return;
            if (!audioStarted && !sequence.IsSkipped && sequence.Elapsed >= sequence.Timing.DrawStart - AudioLeadSeconds)
            {
                audioStarted = audio.Play();
                if (!audioStarted) { audio.Dispose(); audio = null; return; }
            }
            if (!audioStarted) return;
            // The visual clock clamps hitches; keep the sound on the pen if the two ever drift apart.
            if (!sequence.IsSkipped) audio.SyncTo(sequence.Elapsed - (sequence.Timing.DrawStart - AudioLeadSeconds));
            audio.SetGain(AudioVolume * frame.AudioGain);
        }

        private void Apply(StartupFrame frame)
        {
            style.opacity = frame.OverlayAlpha;
            pictogramCard.style.opacity = frame.PictogramAlpha;
            pictogramCard.style.display = frame.PictogramAlpha > .001f ? DisplayStyle.Flex : DisplayStyle.None;
            teamLabel.style.opacity = frame.Fill01;
            if (!sequence.ReducedEffects) teamLabel.style.letterSpacing = Mathf.Lerp(24f, 16f, frame.Fill01);
            eventCard.style.opacity = frame.EventAlpha;
            eventCard.style.display = frame.EventAlpha > .001f ? DisplayStyle.Flex : DisplayStyle.None;
            if (grain.style.display != DisplayStyle.None) grain.style.opacity = GrainOpacity * Mathf.Clamp01(frame.OverlayAlpha * 1.2f);
            if (!Mathf.Approximately(frame.Draw01, drawShown) || !Mathf.Approximately(frame.Fill01, fillShown))
            {
                drawShown = frame.Draw01;
                fillShown = frame.Fill01;
                artElement.MarkDirtyRepaint();
            }
            // Stay opaque to the pointer for the whole sequence; the last fade frames still block a stray click.
            pickingMode = PickingMode.Position;
        }

        private void Complete()
        {
            if (completed) return;
            completed = true;
            tick?.Pause();
            UnsubscribeSkipInput();
            ReleaseAudio();
            ReleaseGrain();
            style.display = DisplayStyle.None;
            artElement.generateVisualContent -= DrawArt;
            RemoveFromHierarchy();
            finished?.Invoke();
        }

        private void ReleaseAudio()
        {
            audio?.Dispose();
            audio = null;
        }

        // ---- film grain -------------------------------------------------------------------------------------

        private void BuildGrain()
        {
            var pixels = new Color32[GrainSize * GrainSize];
            var rng = new System.Random(1966);
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)rng.Next(0, 256);
                pixels[i] = new Color32(v, v, v, (byte)rng.Next(90, 256));
            }
            grainTexture = new Texture2D(GrainSize, GrainSize, TextureFormat.RGBA32, false)
            {
                name = "startup-grain", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave
            };
            grainTexture.SetPixels32(pixels);
            grainTexture.Apply(false, true);
            grain.style.backgroundImage = new StyleBackground(grainTexture);
            grain.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(GrainSize, GrainSize));
            grain.style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat));
            grain.style.opacity = GrainOpacity;
        }

        private void JitterGrain(float seconds)
        {
            if (grainTexture == null) return;
            nextGrainJitter -= seconds;
            if (nextGrainJitter > 0f) return;
            nextGrainJitter = 1f / 18f;                       // film cadence, not display cadence
            float x = (float)grainRandom.NextDouble() * GrainSize, y = (float)grainRandom.NextDouble() * GrainSize;
            grain.style.backgroundPositionX = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Left, x));
            grain.style.backgroundPositionY = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Top, y));
        }

        private void ReleaseGrain()
        {
            if (grainTexture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(grainTexture);
            else UnityEngine.Object.DestroyImmediate(grainTexture);
            grainTexture = null;
        }

        // ---- skip input -------------------------------------------------------------------------------------

        private void SubscribeSkipInput()
        {
            UnsubscribeSkipInput();
            // Any keyboard key, mouse button or gamepad button. Event-driven, so a press is never missed between ticks.
            anyButtonSubscription = InputSystem.onAnyButtonPress.Call(control => RequestSkip());
        }

        private void UnsubscribeSkipInput()
        {
            anyButtonSubscription?.Dispose();
            anyButtonSubscription = null;
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            // Trickle-down on the panel root so keys and clicks never reach the menu underneath while the cards play.
            trickleTarget = evt.destinationPanel?.visualTree;
            if (trickleTarget == null) return;
            trickleTarget.RegisterCallback<KeyDownEvent>(Swallow, TrickleDown.TrickleDown);
            trickleTarget.RegisterCallback<KeyUpEvent>(Swallow, TrickleDown.TrickleDown);
            trickleTarget.RegisterCallback<PointerDownEvent>(Swallow, TrickleDown.TrickleDown);
            trickleTarget.RegisterCallback<PointerUpEvent>(Swallow, TrickleDown.TrickleDown);
            trickleTarget.RegisterCallback<ClickEvent>(Swallow, TrickleDown.TrickleDown);
            trickleTarget.RegisterCallback<NavigationSubmitEvent>(Swallow, TrickleDown.TrickleDown);
            trickleTarget.RegisterCallback<NavigationCancelEvent>(Swallow, TrickleDown.TrickleDown);
            trickleTarget.RegisterCallback<NavigationMoveEvent>(Swallow, TrickleDown.TrickleDown);
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            if (trickleTarget != null)
            {
                trickleTarget.UnregisterCallback<KeyDownEvent>(Swallow, TrickleDown.TrickleDown);
                trickleTarget.UnregisterCallback<KeyUpEvent>(Swallow, TrickleDown.TrickleDown);
                trickleTarget.UnregisterCallback<PointerDownEvent>(Swallow, TrickleDown.TrickleDown);
                trickleTarget.UnregisterCallback<PointerUpEvent>(Swallow, TrickleDown.TrickleDown);
                trickleTarget.UnregisterCallback<ClickEvent>(Swallow, TrickleDown.TrickleDown);
                trickleTarget.UnregisterCallback<NavigationSubmitEvent>(Swallow, TrickleDown.TrickleDown);
                trickleTarget.UnregisterCallback<NavigationCancelEvent>(Swallow, TrickleDown.TrickleDown);
                trickleTarget.UnregisterCallback<NavigationMoveEvent>(Swallow, TrickleDown.TrickleDown);
                trickleTarget = null;
            }
            if (!completed)
            {
                // Removed from the panel before it finished (menu rebuilt): stop quietly, never leave a dead overlay or sound.
                completed = true;
                tick?.Pause();
                UnsubscribeSkipInput();
                ReleaseAudio();
                ReleaseGrain();
            }
        }

        private void Swallow(EventBase evt)
        {
            if (completed) return;
            evt.StopPropagation();
        }

        // ---- drawing ----------------------------------------------------------------------------------------

        private void DrawArt(MeshGenerationContext context)
        {
            Rect rect = artElement.contentRect;
            if (art.IsEmpty || rect.width < 4f || rect.height < 4f) return;
            float scale = Mathf.Min(rect.width / art.Aspect, rect.height);
            var origin = new Vector2(rect.x + (rect.width - art.Aspect * scale) * .5f, rect.y + (rect.height - scale) * .5f);
            var painter = context.painter2D;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            float sketch = Mathf.Clamp01(drawShown < 0f ? 0f : drawShown);
            float fill = Mathf.Clamp01(fillShown < 0f ? 0f : fillShown);
            bool animated = !sequence.ReducedEffects;
            bool sketching = animated && sketch < .9999f;
            Vector2? penTip = null;

            // glow underneath the lines: steady once drawn, plus the travelling sweep
            if (sketch >= .9999f) DrawGlow(painter, origin, scale, fill, animated);

            foreach (var shape in art.Shapes)
            {
                if (shape.HasFill && fill > .002f) DrawFill(painter, shape, fill, origin, scale);
                VectorArt.VisibleSubpaths(shape, sketch, strokeScratch, pool);
                for (int i = 0; i < strokeScratch.Count; i++)
                {
                    var points = strokeScratch[i];
                    var cumulative = shape.Cumulative[i];
                    float full = cumulative[cumulative.Length - 1];
                    if (!CalligraphicStroke.Build(points, full, shape.StrokeWidth, shape.Taper, shape.Closed[i], ribbon)) continue;
                    painter.fillColor = shape.Stroke;
                    painter.BeginPath();
                    painter.MoveTo(origin + ribbon[0] * scale);
                    for (int p = 1; p < ribbon.Count; p++) painter.LineTo(origin + ribbon[p] * scale);
                    painter.ClosePath();
                    painter.Fill(FillRule.NonZero);
                    if (sketching && sketch > shape.RevealStart && sketch < shape.RevealEnd) penTip = points[points.Count - 1];
                }
                // Brighten the stroke where the sweep passes (a hot core), drawn over the line itself.
                if (fill > .002f && fill < .999f && animated) DrawSweepCore(painter, shape, origin, scale, fill);
            }
            if (penTip.HasValue) DrawPenTip(painter, origin + penTip.Value * scale, scale);
        }

        private static void DrawFill(Painter2D painter, ArtShape shape, float fill, Vector2 origin, float scale)
        {
            var color = shape.Fill;
            color.a *= fill;
            painter.fillColor = color;
            painter.BeginPath();
            for (int s = 0; s < shape.Subpaths.Count; s++)
            {
                var points = shape.Subpaths[s];
                if (points.Length < 3) continue;
                painter.MoveTo(origin + points[0] * scale);
                for (int i = 1; i < points.Length; i++) painter.LineTo(origin + points[i] * scale);
                painter.ClosePath();
            }
            painter.Fill(shape.EvenOdd ? FillRule.OddEven : FillRule.NonZero);
        }

        /// <summary>Position of a unit-box point along the diagonal the sweep travels (0 = upper left, 1 = lower right).</summary>
        private float SweepCoordinate(Vector2 unit) => Mathf.Clamp01(unit.x / Mathf.Max(.01f, art.Aspect)) * .78f + Mathf.Clamp01(unit.y) * .22f;

        private void DrawGlow(Painter2D painter, Vector2 origin, float scale, float fill, bool animated)
        {
            // Bloom: wide, faint strokes under the lines. Steady level after the sweep, stronger inside the moving band.
            float steady = animated ? .35f * Mathf.SmoothStep(0f, 1f, fill) : .35f;
            float centre = Mathf.Lerp(-.3f, 1.3f, fill);
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            foreach (var shape in art.Shapes)
            {
                for (int s = 0; s < shape.Subpaths.Count; s++)
                {
                    var points = shape.Subpaths[s];
                    const int chunk = 7;
                    for (int start = 0; start < points.Length - 1; start += chunk - 1)
                    {
                        int end = Mathf.Min(points.Length - 1, start + chunk - 1);
                        float v = SweepCoordinate(points[(start + end) / 2]);
                        float band = animated && fill < .999f ? Mathf.Exp(-Mathf.Pow((v - centre) / .13f, 2f)) : 0f;
                        float level = Mathf.Clamp01(steady + band * .9f);
                        if (level < .01f) continue;
                        for (int pass = 0; pass < 2; pass++)
                        {
                            painter.lineWidth = shape.StrokeWidth * scale * (pass == 0 ? 4.2f : 2.3f);
                            painter.strokeColor = new Color(Gold.r, Gold.g, Gold.b, level * (pass == 0 ? .05f : .09f));
                            painter.BeginPath();
                            painter.MoveTo(origin + points[start] * scale);
                            for (int i = start + 1; i <= end; i++) painter.LineTo(origin + points[i] * scale);
                            painter.Stroke();
                        }
                    }
                }
            }
        }

        private void DrawSweepCore(Painter2D painter, ArtShape shape, Vector2 origin, float scale, float fill)
        {
            float centre = Mathf.Lerp(-.3f, 1.3f, fill);
            for (int s = 0; s < shape.Subpaths.Count; s++)
            {
                var points = shape.Subpaths[s];
                const int chunk = 5;
                for (int start = 0; start < points.Length - 1; start += chunk - 1)
                {
                    int end = Mathf.Min(points.Length - 1, start + chunk - 1);
                    float v = SweepCoordinate(points[(start + end) / 2]);
                    float band = Mathf.Exp(-Mathf.Pow((v - centre) / .09f, 2f));
                    if (band < .04f) continue;
                    painter.lineWidth = Mathf.Max(1f, shape.StrokeWidth * scale * .55f);
                    painter.strokeColor = new Color(GoldHot.r, GoldHot.g, GoldHot.b, band * .85f);
                    painter.BeginPath();
                    painter.MoveTo(origin + points[start] * scale);
                    for (int i = start + 1; i <= end; i++) painter.LineTo(origin + points[i] * scale);
                    painter.Stroke();
                }
            }
        }

        private static void DrawPenTip(Painter2D painter, Vector2 centre, float scale)
        {
            // A small bloom where the line is being laid down.
            float[] radius = { .022f, .012f, .0055f };
            float[] alpha = { .07f, .16f, .6f };
            for (int i = 0; i < radius.Length; i++)
            {
                painter.fillColor = new Color(GoldHot.r, GoldHot.g, GoldHot.b, alpha[i]);
                painter.BeginPath();
                painter.Arc(centre, radius[i] * scale, Angle.Degrees(0f), Angle.Degrees(360f));
                painter.ClosePath();
                painter.Fill(FillRule.NonZero);
            }
        }

        // ---- construction helpers ---------------------------------------------------------------------------

        private static void Fill(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = 0; element.style.top = 0; element.style.right = 0; element.style.bottom = 0;
        }

        private static VisualElement CenterColumn(string elementName)
        {
            var column = new VisualElement { name = elementName, pickingMode = PickingMode.Ignore };
            Fill(column);
            column.style.alignItems = Align.Center;
            column.style.justifyContent = Justify.Center;
            column.style.display = DisplayStyle.None;
            return column;
        }

        private static VisualElement HairLine(string elementName, float top, float bottom)
        {
            var rule = new VisualElement { name = elementName, pickingMode = PickingMode.Ignore };
            rule.style.width = 240; rule.style.height = 1;
            rule.style.marginTop = top; rule.style.marginBottom = bottom;
            rule.style.backgroundColor = new Color(Gold.r, Gold.g, Gold.b, .75f);
            return rule;
        }

        private static Label MakeLabel(string elementName, string text, int size, Color color, float spacing, string className)
        {
            var label = new Label(text) { name = elementName, pickingMode = PickingMode.Ignore, enableRichText = false };
            label.AddToClassList(className);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.letterSpacing = spacing;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.maxWidth = Length.Percent(90f);
            label.style.marginLeft = 0; label.style.marginRight = 0; label.style.marginTop = 0; label.style.marginBottom = 0;
            label.style.paddingLeft = 0; label.style.paddingRight = 0; label.style.paddingTop = 0; label.style.paddingBottom = 0;
            return label;
        }
    }
}
