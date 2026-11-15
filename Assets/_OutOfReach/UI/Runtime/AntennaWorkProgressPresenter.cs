using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI
{
    /// <summary>Read-only antenna work feedback below the existing objective. Root owns work and visibility.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(280)]
    public sealed class AntennaWorkProgressPresenter : MonoBehaviour
    {
        private UIDocument document;
        private Font font;
        private Func<bool> activeQuery, contextQuery, pausedQuery;
        private Func<double> progressQuery;
        private VisualElement documentRoot, host, row, fill;
        private int configurationVersion;
        private bool shown;
        private float displayedProgress = -1f;

        /// <param name="readProgress">Actual service progress, normalized 0..1; no time is inferred.</param>
        /// <param name="visibleContext">Root's current-session gate, including menu/cinematic/terminal visibility.</param>
        /// <param name="isPaused">When supplied, true hides the row. Root still owns pause and progress.</param>
        public void Configure(UIDocument ui, Func<bool> readActive, Func<double> readProgress,
            Func<bool> visibleContext, Font sourceFont, Func<bool> isPaused = null)
        {
            ClearBinding();
            if (!ui) throw new ArgumentNullException(nameof(ui));
            if (!sourceFont) throw new ArgumentNullException(nameof(sourceFont));
            activeQuery = readActive ?? throw new ArgumentNullException(nameof(readActive));
            progressQuery = readProgress ?? throw new ArgumentNullException(nameof(readProgress));
            contextQuery = visibleContext ?? throw new ArgumentNullException(nameof(visibleContext));
            document = ui;
            font = sourceFont;
            pausedQuery = isPaused;
            RefreshPresentation();
        }

        public void ClearBinding()
        {
            configurationVersion++;
            ReleaseView();
            document = null;
            font = null;
            activeQuery = contextQuery = pausedQuery = null;
            progressQuery = null;
        }

        private void OnEnable() => RefreshPresentation();
        private void OnDisable() { configurationVersion++; ReleaseView(); }
        private void OnDestroy() => ClearBinding();
        private void LateUpdate() => RefreshPresentation();

        public void RefreshPresentation()
        {
            if (!isActiveAndEnabled || !document || !document.isActiveAndEnabled || !font
                || activeQuery == null || progressQuery == null || contextQuery == null)
            {
                ReleaseView();
                return;
            }

            VisualElement nextRoot = document.rootVisualElement;
            if (row != null && (nextRoot == null || documentRoot != nextRoot || row.parent != host
                || host == null || !nextRoot.Contains(host))) ReleaseView();

            int version = configurationVersion;
            bool allowed = contextQuery();
            if (version != configurationVersion) return;
            if (!allowed) { Hide(); return; }
            bool paused = pausedQuery != null && pausedQuery();
            if (version != configurationVersion) return;
            if (paused) { Hide(); return; }
            bool active = activeQuery();
            if (version != configurationVersion) return;
            if (!active) { Hide(); return; }
            double progress = progressQuery();
            if (version != configurationVersion) return;
            if (double.IsNaN(progress) || double.IsInfinity(progress)) { Hide(); return; }
            if (!EnsureView(nextRoot)) return;

            float value = (float)Math.Max(0d, Math.Min(1d, progress));
            if (displayedProgress != value)
            {
                fill.style.width = Length.Percent(value * 100f);
                displayedProgress = value;
            }
            if (!shown) { row.style.display = DisplayStyle.Flex; shown = true; }
        }

        private bool EnsureView(VisualElement root)
        {
            if (row != null) return true;
            // Query only when initially attaching or when the existing document/board has rebuilt.
            VisualElement nextHost = root?.Q("mission-flight-board")?.Q(className: "hud-header");
            if (nextHost == null) return false;
            documentRoot = root;
            host = nextHost;
            row = Passive("antennaWorkProgress");
            row.style.display = DisplayStyle.None;
            row.style.width = 220f;
            row.style.maxWidth = Length.Percent(100f);
            row.style.flexShrink = 0f;
            row.style.marginTop = 12f;
            row.style.paddingLeft = row.style.paddingRight = 8f;
            row.style.paddingTop = 5f;
            row.style.paddingBottom = 7f;
            row.style.backgroundColor = new Color(.025f, .032f, .035f, .72f);
            var label = new Label("Repairing antenna")
            {
                name = "antennaWorkLabel", enableRichText = false,
                pickingMode = PickingMode.Ignore, focusable = false
            };
            label.style.unityFont = font;
            label.style.fontSize = 16f;
            label.style.color = new Color(.95f, .95f, .91f, 1f);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 5f;
            row.Add(label);
            VisualElement track = Passive("antennaWorkTrack");
            track.style.height = 3f;
            track.style.flexShrink = 0f;
            track.style.backgroundColor = new Color(.8f, .82f, .79f, .22f);
            fill = Passive("antennaWorkFill");
            fill.style.height = Length.Percent(100f);
            fill.style.width = Length.Percent(0f);
            fill.style.backgroundColor = new Color(.83f, .71f, .47f, 1f);
            track.Add(fill);
            row.Add(track);
            host.Add(row);
            return true;
        }

        private static VisualElement Passive(string name) => new VisualElement
        {
            name = name, pickingMode = PickingMode.Ignore, focusable = false
        };

        private void Hide()
        {
            if (row != null && shown) row.style.display = DisplayStyle.None;
            shown = false;
        }

        private void ReleaseView()
        {
            row?.RemoveFromHierarchy();
            documentRoot = host = row = fill = null;
            shown = false;
            displayedProgress = -1f;
        }
    }
}
