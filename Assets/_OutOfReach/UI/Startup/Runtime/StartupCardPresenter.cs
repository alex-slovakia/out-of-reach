using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI.Startup
{
    /// <summary>
    /// Self-contained startup presenter. Card 1: black, gold vector art (goat with a Slovak double cross, imported from
    /// <c>Resources/StartupCards/startup-art.txt</c> as SVG) sketched stroke by stroke like pencil, then filled with its
    /// colours, with a procedural pencil-scratch sound synced to the pen and the team name beneath. Card 2: the event
    /// card (text only). Then a fade into the opening menu underneath.
    /// It owns only its overlay: it never reads or writes mission state. Plays once per application launch
    /// (<see cref="HasPlayedThisLaunch"/>), fades in and out, skips with any key / mouse button / gamepad button, honours
    /// reduced effects (shorter, no sketch, no fill animation, no scratch sound), and swallows pointer, key and
    /// navigation input while visible so the menu behind cannot be activated by the skipping press.
    /// </summary>
    public sealed class StartupCardPresenter : VisualElement
    {
        public const string EventLine = "NASA Space Apps Challenge · Nov 14–15 2026";
        public const string TeamName = "Polish Alex Goats";
        public const string ResourcesFolder = "StartupCards";
        public const string ArtResource = ResourcesFolder + "/startup-art";
        public const string StyleResource = ResourcesFolder + "/startup-cards";
        /// <summary>Presses during this many real seconds after start are ignored (a held key from launching the game).</summary>
        public const float SkipGraceSeconds = .35f;
        /// <summary>The pencil sound starts this much before the first stroke to cover audio output latency.</summary>
        private const float AudioLeadSeconds = .05f;
        private const float AudioVolume = .55f;

        private static readonly Color Black = new Color(.02f, .022f, .024f, 1f);
        private static readonly Color Gold = new Color(.91f, .71f, .33f, 1f);
        private static readonly Color Paper = new Color(.93f, .91f, .86f, 1f);

        private static bool played;

        private readonly StartupSequence sequence;
        private readonly VectorArt art;
        private readonly VisualElement pictogramCard;
        private readonly VisualElement artElement;
        private readonly Label teamLabel;
        private readonly VisualElement eventCard;
        private readonly Action finished;
        private readonly List<List<Vector2>> strokeScratch = new List<List<Vector2>>();
        private readonly Stack<List<Vector2>> pool = new Stack<List<Vector2>>();
        private IVisualElementScheduledItem tick;
        private IDisposable anyButtonSubscription;
        private VisualElement trickleTarget;
        private StartupPencilAudio audio;
        private bool audioStarted;
        private float lastRealtime;
        private float sinceStart;
        private float drawShown = -1f, fillShown = -1f;
        private bool completed;

        /// <summary>True once a startup sequence has begun in this application launch; a restart must not replay it.</summary>
        public static bool HasPlayedThisLaunch => played;

        public bool IsPlaying => !completed;
        public StartupSequence Sequence => sequence;
        public VectorArt Art => art;
        public VisualElement PictogramCard => pictogramCard;
        public VisualElement EventCard => eventCard;
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
            artElement.style.width = Length.Percent(40f);
            artElement.style.height = Length.Percent(52f);
            artElement.style.display = art.IsEmpty ? DisplayStyle.None : DisplayStyle.Flex;
            artElement.generateVisualContent += DrawArt;
            pictogramCard.Add(artElement);
            teamLabel = MakeLabel("startup-team-name", TeamName, 58, Gold, 6f, "startup-team");
            teamLabel.style.marginTop = art.IsEmpty ? 0 : 26;
            pictogramCard.Add(teamLabel);
            Add(pictogramCard);

            eventCard = CenterColumn("startup-event-card");
            eventCard.Add(HairLine("startup-rule-top", 0, 26));
            eventCard.Add(MakeLabel("startup-event-line", EventLine, 34, Paper, 9f, "startup-event"));
            eventCard.Add(HairLine("startup-rule-bottom", 26, 0));
            Add(eventCard);

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
            eventCard.style.opacity = frame.EventAlpha;
            eventCard.style.display = frame.EventAlpha > .001f ? DisplayStyle.Flex : DisplayStyle.None;
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
            bool wobble = !sequence.ReducedEffects;
            foreach (var shape in art.Shapes)
            {
                if (shape.HasFill && fill > .002f) DrawFill(painter, shape, fill, origin, scale);
                StrokePass(painter, shape, false, sketch, shape.StrokeWidth, 1f, origin, scale);
                if (wobble) StrokePass(painter, shape, true, sketch, shape.StrokeWidth * .55f, .45f, origin, scale);
            }
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

        private void StrokePass(Painter2D painter, ArtShape shape, bool wobbled, float sketch, float width, float alpha, Vector2 origin, float scale)
        {
            VectorArt.VisibleSubpaths(shape, wobbled, sketch, strokeScratch, pool);
            if (strokeScratch.Count == 0) return;
            painter.lineWidth = Mathf.Max(1f, width * scale);
            var color = shape.Stroke;
            color.a *= alpha;
            painter.strokeColor = color;
            foreach (var points in strokeScratch)
            {
                painter.BeginPath();
                painter.MoveTo(origin + points[0] * scale);
                for (int i = 1; i < points.Count; i++) painter.LineTo(origin + points[i] * scale);
                painter.Stroke();
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
