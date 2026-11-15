using System;
using OutOfReach.Contracts;
using OutOfReach.Core.Runtime;
using OutOfReach.Presentation.Cinematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.LaunchFailure
{
    /// <summary>
    /// R27 follow-up, presentation only: a short launch-failure beat for Core's overweight-launch loss. Core rules are
    /// unchanged; Core still ends the mission as Lost with the explained reason and then shows the Mission Record.
    /// This overlay watches the existing <see cref="IMissionEventBus.MissionEnded"/> signal and, only when
    /// <see cref="LaunchFailureTrigger"/> matches, covers the screen for about 5 s (<see cref="LaunchFailureTimeline"/>):
    /// the view runs on, engine stutter (overlay shake) with a red "THRUST BELOW WEIGHT" strip, a cut to black and a
    /// "LAUNCH FAILURE" card with Core's exact reason. Then it removes itself, uncovering the normal Mission Record.
    /// <list type="bullet">
    /// <item>Self-installing (like PreflightRackDressing); nothing in the scene or composition changes.</item>
    /// <item>Reduced effects (MissionCinematicPresenter.ReducedEffects): no shake or flicker, a straight cut.</item>
    /// <item>Skip: hold Space or Enter for <see cref="SkipHoldSeconds"/>, the existing cinematic skip input.</item>
    /// <item>Pause hides the overlay and holds its clock, so the pause menu stays usable.</item>
    /// <item>Fails open: no runtime, no UI panel to clone, a new session or any exception removes the overlay and
    /// leaves the report exactly as it would be without this component. Silent: no clean public audio hook.</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(900)]
    public sealed class LaunchFailureCinematic : MonoBehaviour
    {
        public const float SkipHoldSeconds = .6f;
        public const string Title = "LAUNCH FAILURE";
        public const string Warning = "THRUST BELOW WEIGHT";
        private const float FindIntervalSeconds = .5f;

        private static LaunchFailureCinematic instance;

        private MissionRuntime runtime;
        private IMissionEventBus boundBus;
        private float nextFind;
        private string pendingReason;
        private LaunchFailureTimeline timeline;
        private float skipHeld;
        private GameObject overlayObject;
        private PanelSettings overlaySettings;
        private VisualElement root, shakeLayer, wash, strip, black, card;
        private bool warned;

        /// <summary>True while the overlay is on screen (for diagnostics).</summary>
        public bool IsPlaying => timeline != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterFirstLoad()
        {
            if (instance) return;
            var host = new GameObject("Launch failure cinematic (runtime)");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<LaunchFailureCinematic>();
        }

        private void OnDestroy()
        {
            Unbind();
            Teardown();
            if (instance == this) instance = null;
        }

        private void Update()
        {
            try
            {
                Bind();
                if (pendingReason != null)
                {
                    string reason = pendingReason;
                    pendingReason = null;
                    Begin(reason);
                }

                if (timeline != null) Tick();
            }
            catch (Exception exception)
            {
                Teardown();
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning("Launch failure cinematic skipped (fail open): " + exception.Message);
                }
            }
        }

        private void Bind()
        {
            if (!runtime && Time.unscaledTime >= nextFind)
            {
                nextFind = Time.unscaledTime + FindIntervalSeconds;
                runtime = FindAnyObjectByType<MissionRuntime>();
            }

            IMissionEventBus bus = runtime ? runtime.Events : null;
            if (ReferenceEquals(bus, boundBus)) return;
            Unbind();
            Teardown();   // a replacement session never inherits an overlay
            pendingReason = null;
            boundBus = bus;
            if (boundBus != null) boundBus.MissionEnded += OnMissionEnded;
        }

        private void Unbind()
        {
            if (boundBus != null) boundBus.MissionEnded -= OnMissionEnded;
            boundBus = null;
        }

        // Called during the bus dispatch: only record it; the overlay is built in Update.
        private void OnMissionEnded(MissionEnded ended)
        {
            if (LaunchFailureTrigger.ShouldPlay(ended)) pendingReason = ended.Reason;
        }

        private void Begin(string reason)
        {
            Teardown();
            var cinematics = FindAnyObjectByType<MissionCinematicPresenter>();
            bool reduced = cinematics && cinematics.ReducedEffects;
            if (!BuildOverlay(reason)) return;   // fail open: no themed panel to draw on
            timeline = new LaunchFailureTimeline(reduced);
            skipHeld = 0f;
            Apply();
        }

        private void Tick()
        {
            bool paused = runtime && runtime.IsPaused;
            Keyboard keyboard = Keyboard.current;
            bool held = !paused && keyboard != null && (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed);
            skipHeld = held ? skipHeld + Time.unscaledDeltaTime : 0f;
            if (skipHeld >= SkipHoldSeconds) timeline.Skip();
            timeline.Advance(Time.unscaledDeltaTime, paused);
            if (timeline.IsFinished)
            {
                Teardown();
                return;
            }

            root.style.display = paused ? DisplayStyle.None : DisplayStyle.Flex;
            Apply();
        }

        private void Apply()
        {
            shakeLayer.style.translate = new Translate(timeline.ShakeX, timeline.ShakeY);
            float stripAlpha = timeline.StripAlpha;
            strip.style.opacity = stripAlpha;
            wash.style.opacity = timeline.ReducedEffects ? 0f : stripAlpha * .22f;
            black.style.opacity = timeline.BlackAlpha;
            card.style.opacity = timeline.CardAlpha;
        }

        private bool BuildOverlay(string reason)
        {
            PanelSettings source = null;
            float topOrder = 0f;
            foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                PanelSettings settings = document ? document.panelSettings : null;
                if (!settings) continue;
                topOrder = Mathf.Max(topOrder, settings.sortingOrder);
                if (!source && settings.themeStyleSheet) source = settings;
            }

            if (!source) return false;
            overlaySettings = Instantiate(source);
            overlaySettings.name = "Launch failure overlay panel";
            overlaySettings.sortingOrder = topOrder + 100f;

            overlayObject = new GameObject("Launch failure overlay");
            overlayObject.transform.SetParent(transform, false);
            overlayObject.SetActive(false);
            var overlay = overlayObject.AddComponent<UIDocument>();
            overlay.panelSettings = overlaySettings;
            overlayObject.SetActive(true);

            root = overlay.rootVisualElement;
            Fill(root);
            root.pickingMode = PickingMode.Position;   // the report underneath takes no clicks while covered

            shakeLayer = Fill(new VisualElement());
            shakeLayer.pickingMode = PickingMode.Ignore;
            root.Add(shakeLayer);

            wash = Fill(new VisualElement());
            wash.style.backgroundColor = new Color(.62f, .07f, .04f, 1f);
            wash.pickingMode = PickingMode.Ignore;
            shakeLayer.Add(wash);

            strip = new VisualElement { pickingMode = PickingMode.Ignore };
            strip.style.position = Position.Absolute;
            strip.style.left = 0f;
            strip.style.right = 0f;
            strip.style.top = Length.Percent(40f);
            strip.style.height = 72f;
            strip.style.backgroundColor = new Color(.58f, .06f, .04f, .94f);
            strip.style.borderTopColor = strip.style.borderBottomColor = new Color(1f, .35f, .25f, 1f);
            strip.style.borderTopWidth = strip.style.borderBottomWidth = 2f;
            strip.style.justifyContent = Justify.Center;
            strip.style.alignItems = Align.Center;
            strip.Add(MakeLabel(Warning, 34f, Color.white, true));
            shakeLayer.Add(strip);

            black = Fill(new VisualElement());
            black.style.backgroundColor = Color.black;
            black.pickingMode = PickingMode.Ignore;
            root.Add(black);

            card = Fill(new VisualElement());
            card.pickingMode = PickingMode.Ignore;
            card.style.justifyContent = Justify.Center;
            card.style.alignItems = Align.Center;
            card.Add(MakeLabel(Title, 58f, new Color(.93f, .27f, .18f, 1f), true));
            Label reasonLabel = MakeLabel(reason, 22f, new Color(.86f, .86f, .84f, 1f), false);
            reasonLabel.style.marginTop = 18f;
            reasonLabel.style.maxWidth = Length.Percent(64f);
            reasonLabel.style.whiteSpace = WhiteSpace.Normal;
            reasonLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(reasonLabel);
            Label hint = MakeLabel("Hold Space or Enter to continue", 14f, new Color(.55f, .55f, .53f, 1f), false);
            hint.style.marginTop = 30f;
            card.Add(hint);
            root.Add(card);
            return true;
        }

        private static VisualElement Fill(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = 0f;
            element.style.top = 0f;
            element.style.right = 0f;
            element.style.bottom = 0f;
            return element;
        }

        private static Label MakeLabel(string text, float size, Color color, bool bold)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            if (bold) label.style.letterSpacing = 4f;
            return label;
        }

        private void Teardown()
        {
            timeline = null;
            skipHeld = 0f;
            root = shakeLayer = wash = strip = black = card = null;
            if (overlayObject) Destroy(overlayObject);
            overlayObject = null;
            if (overlaySettings) Destroy(overlaySettings);
            overlaySettings = null;
        }
    }
}
