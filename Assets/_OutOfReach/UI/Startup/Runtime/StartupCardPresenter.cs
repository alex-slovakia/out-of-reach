using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI.Startup
{
    /// <summary>
    /// Self-contained startup presenter: a black card with a gold pencil-drawn Slovakia pictogram, then a second card
    /// with the event wording and team name, then a fade into whatever is underneath (the opening menu). It owns
    /// nothing but its own overlay: it never reads or writes mission state. Plays once per application launch
    /// (<see cref="HasPlayedThisLaunch"/>), fades in and out, is skippable with any key, mouse button or gamepad
    /// button, and honours reduced effects (shorter, fade only, no pencil draw-on). While it is on screen it swallows
    /// pointer, key and navigation input so the menu behind it cannot be activated by the skipping press.
    /// </summary>
    public sealed class StartupCardPresenter : VisualElement
    {
        public const string EventLine = "NASA Space Apps Challenge · Nov 14–15 2026";
        public const string TeamName = "Polish Alex Goats";
        /// <summary>Presses during this many real seconds after start are ignored (a held key from launching the game).</summary>
        public const float SkipGraceSeconds = .35f;

        private static readonly Color Black = new Color(.02f, .022f, .024f, 1f);
        private static readonly Color Gold = new Color(.91f, .71f, .33f, 1f);
        private static readonly Color Paper = new Color(.93f, .91f, .86f, 1f);

        private static bool played;

        private readonly StartupSequence sequence;
        private readonly VisualElement pictogramCard;
        private readonly VisualElement pictogram;
        private readonly VisualElement eventCard;
        private readonly List<PencilStroke> strokes = SlovakiaPictogram.Build();
        private readonly List<Vector2> scratch = new List<Vector2>(256);
        private readonly Action finished;
        private IVisualElementScheduledItem tick;
        private IDisposable anyButtonSubscription;
        private VisualElement trickleTarget;
        private float lastRealtime;
        private float sinceStart;
        private float drawShown = -1f;
        private bool completed;

        /// <summary>True once a startup sequence has begun in this application launch; a restart must not replay it.</summary>
        public static bool HasPlayedThisLaunch => played;

        public bool IsPlaying => !completed;
        public StartupSequence Sequence => sequence;
        public VisualElement PictogramCard => pictogramCard;
        public VisualElement EventCard => eventCard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => played = false;

        /// <summary>
        /// Plays the cards over <paramref name="host"/> (the UIDocument root the opening menu lives in) unless they have
        /// already played this launch. Returns null when it did not start; <paramref name="onFinished"/> is then not called.
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

        public StartupCardPresenter(bool reducedEffects, Font font = null, StyleSheet styleSheet = null, Action onFinished = null)
        {
            finished = onFinished;
            sequence = new StartupSequence(reducedEffects);
            name = "startup-cards";
            pickingMode = PickingMode.Position;
            focusable = false;
            AddToClassList("startup-cards");
            if (styleSheet) styleSheets.Add(styleSheet);
            Fill(this);
            style.backgroundColor = Black;
            style.opacity = 1f;
            if (font) style.unityFont = font;

            pictogramCard = CenterColumn("startup-pictogram-card");
            pictogram = new VisualElement { name = "startup-pictogram", pickingMode = PickingMode.Ignore };
            pictogram.style.width = Length.Percent(46f);
            pictogram.style.height = Length.Percent(40f);
            pictogram.generateVisualContent += DrawPictogram;
            pictogramCard.Add(pictogram);
            Add(pictogramCard);

            eventCard = CenterColumn("startup-event-card");
            var eventLabel = MakeLabel("startup-event-line", EventLine, 26, Paper, 5f);
            var rule = new VisualElement { name = "startup-rule", pickingMode = PickingMode.Ignore };
            rule.style.width = 180; rule.style.height = 2; rule.style.marginTop = 22; rule.style.marginBottom = 22;
            rule.style.backgroundColor = new Color(Gold.r, Gold.g, Gold.b, .85f);
            var teamLabel = MakeLabel("startup-team-name", TeamName, 54, Gold, 3f);
            eventCard.Add(eventLabel); eventCard.Add(rule); eventCard.Add(teamLabel);
            Add(eventCard);

            Apply(sequence.Evaluate());
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        /// <summary>Starts the real-time clock and the skip input. Called by <see cref="PlayOnce"/>.</summary>
        public void Begin()
        {
            if (completed) return;
            played = true;
            lastRealtime = Time.realtimeSinceStartup;
            sinceStart = 0f;
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
            Apply(sequence.Evaluate());
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

        private void Apply(StartupFrame frame)
        {
            style.opacity = frame.OverlayAlpha;
            pictogramCard.style.opacity = frame.PictogramAlpha;
            pictogramCard.style.display = frame.PictogramAlpha > .001f ? DisplayStyle.Flex : DisplayStyle.None;
            eventCard.style.opacity = frame.EventAlpha;
            eventCard.style.display = frame.EventAlpha > .001f ? DisplayStyle.Flex : DisplayStyle.None;
            if (!Mathf.Approximately(frame.Draw01, drawShown))
            {
                drawShown = frame.Draw01;
                pictogram.MarkDirtyRepaint();
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
            style.display = DisplayStyle.None;
            pictogram.generateVisualContent -= DrawPictogram;
            RemoveFromHierarchy();
            finished?.Invoke();
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
            trickleTarget.RegisterCallback<PointerDownEvent>(SwallowPointer, TrickleDown.TrickleDown);
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
                trickleTarget.UnregisterCallback<PointerDownEvent>(SwallowPointer, TrickleDown.TrickleDown);
                trickleTarget.UnregisterCallback<PointerUpEvent>(Swallow, TrickleDown.TrickleDown);
                trickleTarget.UnregisterCallback<ClickEvent>(Swallow, TrickleDown.TrickleDown);
                trickleTarget.UnregisterCallback<NavigationSubmitEvent>(Swallow, TrickleDown.TrickleDown);
                trickleTarget.UnregisterCallback<NavigationCancelEvent>(Swallow, TrickleDown.TrickleDown);
                trickleTarget.UnregisterCallback<NavigationMoveEvent>(Swallow, TrickleDown.TrickleDown);
                trickleTarget = null;
            }
            if (!completed)
            {
                // Removed from the panel before it finished (menu rebuilt): stop quietly, never leave a dead overlay.
                completed = true;
                tick?.Pause();
                UnsubscribeSkipInput();
            }
        }

        private void Swallow(EventBase evt)
        {
            if (completed) return;
            evt.StopPropagation();
        }

        private void SwallowPointer(PointerDownEvent evt)
        {
            if (completed) return;
            evt.StopPropagation();
        }

        // ---- drawing ----------------------------------------------------------------------------------------

        private void DrawPictogram(MeshGenerationContext context)
        {
            Rect rect = pictogram.contentRect;
            if (rect.width < 4f || rect.height < 4f) return;
            float scale = Mathf.Min(rect.width / SlovakiaPictogram.Aspect, rect.height);
            var origin = new Vector2(rect.x + (rect.width - SlovakiaPictogram.Aspect * scale) * .5f,
                rect.y + (rect.height - scale) * .5f);
            var painter = context.painter2D;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            float draw = Mathf.Clamp01(drawShown < 0f ? 1f : drawShown);
            for (int i = 0; i < strokes.Count; i++)
            {
                var stroke = strokes[i];
                if (SlovakiaPictogram.VisiblePoints(stroke, draw, scratch) < 2) continue;
                painter.lineWidth = Mathf.Max(1f, stroke.Width * scale);
                painter.strokeColor = new Color(Gold.r, Gold.g, Gold.b, stroke.Alpha);
                painter.BeginPath();
                painter.MoveTo(origin + scratch[0] * scale);
                for (int p = 1; p < scratch.Count; p++) painter.LineTo(origin + scratch[p] * scale);
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

        private static Label MakeLabel(string elementName, string text, int size, Color color, float spacing)
        {
            var label = new Label(text) { name = elementName, pickingMode = PickingMode.Ignore, enableRichText = false };
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
