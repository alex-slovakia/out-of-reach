using System;
using OutOfReach.Composition;
using OutOfReach.Core.Runtime;
using OutOfReach.Player;
using OutOfReach.Presentation.Rescue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.CinematicSkip
{
    /// <summary>
    /// Hold Space or Enter for 0.6 s to skip the departure flight. Presentation only and fail open:
    /// - it installs itself, finds the scene's own marine bindings and calls their existing I06MarineBindings.Skip;
    /// - it is live only while the departure plays and the player's control is locked, never in gameplay
    ///   (Space is push-off there), never while paused, never before a mode is chosen and never during the rescue
    ///   ending, which keeps its own hold-to-skip;
    /// - a hold must start inside the cinematic, and one hold skips one sequence;
    /// - a missing binding or any failure turns it off with one warning; the game runs as before.
    /// </summary>
    public sealed class CinematicSkipInput : MonoBehaviour
    {
        public const string HintName = "cinematic-skip-hint";
        public const string HintText = "Hold Space to skip";
        public const string SubtitleLayerClass = "subtitle-overlay";

        private const float RescanSeconds = .5f, HintSeconds = 4f, HintLingerSeconds = 1.5f, HintFadeSeconds = .25f;
        private static readonly Color Ink = new Color(.898f, .902f, .871f, 1f);
        private static readonly Color Outline = new Color(.059f, .059f, .047f, 1f);

        /// <summary>Kill switch for the integrator and for tests: false never skips and shows nothing.</summary>
        public static bool Enabled = true;

        public static CinematicSkipInput Instance { get; private set; }

        private readonly CinematicSkipHold hold = new CinematicSkipHold();
        private MissionRuntime runtime;
        private I01MissionComposition composition;
        private I06MarineBindings marine;
        private RescueEndingPresenter rescue;
        private VisualElement hint, fill;
        private float rescanIn, hostScanIn, windowSeconds, lingerSeconds, hintOpacity;
        private bool bound, dormant, faulted, warned;

        /// <summary>What a completed hold would skip this frame.</summary>
        public CinematicSkipTarget ActiveTarget { get; private set; }
        public CinematicSkipTarget LastSkipped { get; private set; }
        public int SkipCount { get; private set; }
        public float HoldProgress01 => hold.Progress01;
        public bool HintVisible => hint != null && hint.panel != null && hintOpacity > 0f;
        public bool IsBound => bound;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Instance || !Enabled) return;
            var go = new GameObject("CinematicSkipInput");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<CinematicSkipInput>();
        }

        private void OnDisable() => Unbind();

        private void OnDestroy()
        {
            Unbind();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (faulted) return;
            try
            {
                Keyboard keyboard = Keyboard.current;
                bool held = keyboard != null && (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed);
                Tick(Time.unscaledDeltaTime, held);
            }
            catch (Exception exception)
            {
                // Fail open: the departure keeps playing to its end exactly as before.
                faulted = true;
                Debug.LogWarning("[CinematicSkip] disabled after an unexpected failure: " + exception.Message);
                try { Unbind(); } catch (Exception) { /* nothing left to release safely */ }
                enabled = false;
            }
        }

        /// <summary>One frame. Public so a test or a native check can drive it with explicit input and real seconds.</summary>
        public void Tick(float realSeconds, bool keyHeld)
        {
            if (!Enabled)
            {
                Unbind();
                return;
            }

            if (float.IsNaN(realSeconds) || float.IsInfinity(realSeconds) || realSeconds < 0f) realSeconds = 0f;
            if (!runtime)
            {
                if (bound || dormant) Unbind();
                rescanIn -= realSeconds;
                if (rescanIn > 0f) return;
                rescanIn = RescanSeconds;
                runtime = FindFirstObjectByType<MissionRuntime>();
                if (!runtime) return;
                Bind();
            }

            if (dormant) return;

            FirstPersonController player = runtime.Player;
            var context = new CinematicSkipContext
            {
                Paused = runtime.IsPaused,
                ModeChosen = !composition || composition.ModeChosen,
                RescueEndingPlaying = rescue && rescue.IsPlaying,
                ControlAllowed = !player || player.ControlAllowed,
                DeparturePlaying = marine && marine.IsDeparturePlaying,
            };
            CinematicSkipTarget target = CinematicSkipHold.Resolve(context);
            ActiveTarget = target;
            bool skippable = target != CinematicSkipTarget.None;
            windowSeconds = skippable ? windowSeconds + realSeconds : 0f;
            lingerSeconds = skippable && keyHeld ? HintLingerSeconds : Mathf.Max(0f, lingerSeconds - realSeconds);

            bool fired = hold.Tick(skippable, keyHeld, realSeconds);
            if (fired) Skip(target);
            PresentHint(skippable && !fired && (windowSeconds < HintSeconds || lingerSeconds > 0f), realSeconds);
        }

        private void Bind()
        {
            composition = FindFirstObjectByType<I01MissionComposition>();
            marine = FindFirstObjectByType<I06MarineBindings>();
            rescue = FindFirstObjectByType<RescueEndingPresenter>(FindObjectsInactive.Include);
            if (!marine)
            {
                // Nothing to skip in this scene. Stay quiet until another mission runtime appears.
                dormant = true;
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning("[CinematicSkip] no departure bindings found; hold-to-skip is off.");
                }
                return;
            }

            bound = true;
        }

        private void Unbind()
        {
            runtime = null;
            composition = null;
            marine = null;
            rescue = null;
            bound = dormant = false;
            windowSeconds = lingerSeconds = hintOpacity = 0f;
            ActiveTarget = CinematicSkipTarget.None;
            hold.Reset();
            if (hint != null) hint.RemoveFromHierarchy();
            hint = fill = null;
        }

        private void Skip(CinematicSkipTarget target)
        {
            if (target != CinematicSkipTarget.Departure || !marine) return;
            float at = marine.PresentedSeconds;
            if (!marine.Skip()) return;
            SkipCount++;
            LastSkipped = target;
            Debug.Log("[CinematicSkip] departure skipped by a " + hold.HoldSeconds.ToString("0.0")
                + " s hold at " + at.ToString("0.0") + " s.");
        }

        private void PresentHint(bool show, float realSeconds)
        {
            if (show && !EnsureHint(realSeconds)) show = false;
            if (hint == null) { hintOpacity = 0f; return; }
            if (hint.panel == null)
            {
                // The document that held the hint is gone (scene reload). A later departure finds the new one.
                hint = fill = null;
                hintOpacity = 0f;
                return;
            }

            hintOpacity = Mathf.MoveTowards(hintOpacity, show ? 1f : 0f, realSeconds / HintFadeSeconds);
            hint.style.display = hintOpacity > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            hint.style.opacity = hintOpacity;
            fill.style.width = Length.Percent(hold.Progress01 * 100f);
        }

        private bool EnsureHint(float realSeconds)
        {
            if (hint != null && hint.panel != null) return true;
            hint = fill = null;
            hostScanIn -= realSeconds;
            if (hostScanIn > 0f) return false;
            hostScanIn = RescanSeconds;
            foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (!document || !document.isActiveAndEnabled) continue;
                VisualElement root = document.rootVisualElement;
                VisualElement subtitles = root?.Q<VisualElement>(null, SubtitleLayerClass);
                if (subtitles?.parent == null) continue;
                BuildHint(subtitles.parent);
                return true;
            }

            return false;   // no subtitle layer: the hold still works, there is just no label
        }

        private void BuildHint(VisualElement host)
        {
            hint = new VisualElement { name = HintName, pickingMode = PickingMode.Ignore };
            hint.style.position = Position.Absolute;
            hint.style.right = Length.Percent(3f);
            hint.style.bottom = Length.Percent(4f);
            hint.style.alignItems = Align.FlexEnd;
            hint.style.display = DisplayStyle.None;
            hint.style.opacity = 0f;

            var label = new Label(HintText) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = 14f;
            label.style.color = Ink;
            label.style.unityTextAlign = TextAnchor.MiddleRight;
            label.style.unityTextOutlineWidth = .6f;
            label.style.unityTextOutlineColor = Outline;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0f;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0f;

            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.style.width = 132f;
            track.style.height = 2f;
            track.style.marginTop = 4f;
            track.style.backgroundColor = new Color(Ink.r, Ink.g, Ink.b, .28f);

            fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.style.height = Length.Percent(100f);
            fill.style.width = Length.Percent(0f);
            fill.style.backgroundColor = Ink;

            track.Add(fill);
            hint.Add(label);
            hint.Add(track);
            host.Add(hint);
        }
    }
}
