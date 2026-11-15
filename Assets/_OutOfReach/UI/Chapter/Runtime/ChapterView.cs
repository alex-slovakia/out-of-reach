using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI.Chapter
{
    /// <summary>
    /// The chapter UI element tree: beat title card, routine montage, routine report, modal message/decision card and
    /// the next-routine strip. Display only; the presenter fills it from Core. Elements are named for tests and QA.
    /// Visibility uses inline display styles, so it does not depend on the style sheet being present.
    /// </summary>
    public sealed class ChapterView
    {
        public const string LayerName = "chapter-ui-layer";
        public const int FeedLines = 5;

        public VisualElement Layer { get; }

        // Beat title card
        public VisualElement Beat { get; }
        public Label BeatDays { get; }
        public Label BeatTitle { get; }
        public Label BeatLine { get; }
        public Label BeatSummary { get; }

        // Routine montage
        public VisualElement Montage { get; }
        public Label MontageKicker { get; }
        public Label MontageDay { get; }
        public Label MontageClock { get; }
        public Label MontageRange { get; }
        public VisualElement Ruler { get; }
        public VisualElement RulerFill { get; }
        public VisualElement RulerMarker { get; }
        public Label MontageElapsed { get; }
        public Label MontageActivity { get; }
        public Label TallyMeals { get; }
        public Label TallyWater { get; }
        public Label TallyNights { get; }
        public Label[] ReserveValues { get; } = new Label[5];
        public Label[] ReserveDeltas { get; } = new Label[5];
        public Label[] Feed { get; } = new Label[FeedLines];
        public Button MontageStop { get; }

        // Routine report
        public VisualElement Report { get; }
        public Label ReportKicker { get; }
        public Label ReportTime { get; }
        public Label ReportReason { get; }
        public Label ReportSummary { get; }
        public Button ReportDismiss { get; }

        // Modal card
        public VisualElement Modal { get; }
        public VisualElement Card { get; }
        public Label CardKicker { get; }
        public Label CardTitle { get; }
        public Label CardMeta { get; }
        public Label CardBody { get; }
        public Label CardFacts { get; }
        public Label CardNote { get; }
        public Button[] CardOptions { get; } = new Button[2];
        public Label[] CardOptionTitles { get; } = new Label[2];
        public Label[] CardOptionDetails { get; } = new Label[2];
        public Label[] CardOptionKeys { get; } = new Label[2];
        public Button CardDismiss { get; }
        public Label CardDismissText { get; }
        public Label CardFeedback { get; }

        // Next routine strip
        public VisualElement Strip { get; }
        public Label StripKicker { get; }
        public Label StripTitle { get; }
        public Label StripDetail { get; }
        public Button StripAction { get; }
        public Label StripActionText { get; }

        public event Action StripActionClicked;
        public event Action MontageStopClicked;
        public event Action ReportDismissClicked;
        public event Action<int> CardOptionClicked;
        public event Action CardDismissClicked;

        private static readonly string[] ReserveNames = { "Food", "Water", "Oxygen", "Battery", "Cabin CO₂" };

        public ChapterView()
        {
            Layer = Box(LayerName, "chapter-layer");

            Beat = Box("chapter-beat", "chapter-beat");
            BeatDays = Text("chapter-beat-days", "chapter-beat-days");
            BeatTitle = Text("chapter-beat-title", "chapter-beat-title");
            var rule = Box("chapter-beat-rule", "chapter-beat-rule");
            BeatLine = Text("chapter-beat-line", "chapter-beat-line");
            BeatSummary = Text("chapter-beat-summary", "chapter-beat-summary");
            Beat.Add(BeatDays);
            Beat.Add(BeatTitle);
            Beat.Add(rule);
            Beat.Add(BeatLine);
            Beat.Add(BeatSummary);

            Montage = Box("chapter-montage", "chapter-montage");
            var montageColumn = Box("chapter-montage-column", "chapter-montage-column");
            MontageKicker = Text("chapter-montage-kicker", "chapter-montage-kicker");
            var dayRow = Box("chapter-montage-dayrow", "chapter-montage-dayrow");
            MontageDay = Text("chapter-montage-day", "chapter-montage-day");
            MontageClock = Text("chapter-montage-clock", "chapter-montage-clock");
            dayRow.Add(MontageDay);
            dayRow.Add(MontageClock);
            MontageRange = Text("chapter-montage-range", "chapter-montage-range");
            Ruler = Box("chapter-ruler", "chapter-ruler");
            RulerFill = Box("chapter-ruler-fill", "chapter-ruler-fill");
            RulerMarker = Box("chapter-ruler-marker", "chapter-ruler-marker");
            Ruler.Add(RulerFill);
            Ruler.Add(RulerMarker);
            MontageElapsed = Text("chapter-montage-elapsed", "chapter-montage-elapsed");
            MontageActivity = Text("chapter-montage-activity", "chapter-montage-activity");
            var tally = Box("chapter-montage-tally", "chapter-montage-tally");
            TallyMeals = TallyCell(tally, "chapter-tally-meals", "Meals");
            TallyWater = TallyCell(tally, "chapter-tally-water", "Water portions");
            TallyNights = TallyCell(tally, "chapter-tally-nights", "Nights of sleep");
            var lower = Box("chapter-montage-lower", "chapter-montage-lower");
            var reserves = Box("chapter-montage-reserves", "chapter-montage-reserves");
            reserves.Add(Text("chapter-reserves-title", "chapter-section-title", "Reserves · the ship's real ledger"));
            for (int i = 0; i < ReserveNames.Length; i++)
            {
                var row = Box("chapter-reserve-" + i, "chapter-reserve-row");
                row.Add(Text(null, "chapter-reserve-name", ReserveNames[i]));
                ReserveValues[i] = Text("chapter-reserve-value-" + i, "chapter-reserve-value");
                ReserveDeltas[i] = Text("chapter-reserve-delta-" + i, "chapter-reserve-delta");
                row.Add(ReserveValues[i]);
                row.Add(ReserveDeltas[i]);
                reserves.Add(row);
            }

            var feed = Box("chapter-montage-feed", "chapter-montage-feed");
            feed.Add(Text("chapter-feed-title", "chapter-section-title", "Crew log"));
            for (int i = 0; i < FeedLines; i++)
            {
                Feed[i] = Text("chapter-feed-" + i, "chapter-feed-line");
                feed.Add(Feed[i]);
            }

            lower.Add(reserves);
            lower.Add(feed);
            MontageStop = Action("chapter-montage-stop", "chapter-montage-stop", "R", "Stop the routine", out _);
            MontageStop.clicked += () => MontageStopClicked?.Invoke();
            montageColumn.Add(MontageKicker);
            montageColumn.Add(dayRow);
            montageColumn.Add(MontageRange);
            montageColumn.Add(Ruler);
            montageColumn.Add(MontageElapsed);
            montageColumn.Add(MontageActivity);
            montageColumn.Add(tally);
            montageColumn.Add(lower);
            montageColumn.Add(MontageStop);
            Montage.Add(montageColumn);

            Report = Box("chapter-report", "chapter-report");
            var reportCard = Box("chapter-report-card", "chapter-report-card");
            ReportKicker = Text("chapter-report-kicker", "chapter-report-kicker");
            ReportTime = Text("chapter-report-time", "chapter-report-time");
            ReportReason = Text("chapter-report-reason", "chapter-report-reason");
            ReportSummary = Text("chapter-report-summary", "chapter-report-summary");
            ReportDismiss = Action("chapter-report-dismiss", "chapter-report-dismiss", "Enter", "Back to the ship", out _);
            ReportDismiss.clicked += () => ReportDismissClicked?.Invoke();
            reportCard.Add(ReportKicker);
            reportCard.Add(ReportTime);
            reportCard.Add(ReportReason);
            reportCard.Add(ReportSummary);
            reportCard.Add(ReportDismiss);
            Report.Add(reportCard);

            Modal = Box("chapter-modal", "chapter-modal");
            Modal.pickingMode = PickingMode.Position; // the backdrop keeps clicks from reaching the HUD below
            Card = Box("chapter-card", "chapter-card");
            CardKicker = Text("chapter-card-kicker", "chapter-card-kicker");
            CardTitle = Text("chapter-card-title", "chapter-card-title");
            CardMeta = Text("chapter-card-meta", "chapter-card-meta");
            CardBody = Text("chapter-card-body", "chapter-card-body");
            CardFacts = Text("chapter-card-facts", "chapter-card-facts");
            var options = Box("chapter-card-options", "chapter-card-options");
            for (int i = 0; i < CardOptions.Length; i++)
            {
                int index = i;
                var button = new Button(() => CardOptionClicked?.Invoke(index))
                {
                    name = "chapter-option-" + i, focusable = false, pickingMode = PickingMode.Position,
                };
                button.AddToClassList("chapter-option");
                CardOptionKeys[i] = Text("chapter-option-key-" + i, "chapter-key", (i + 1).ToString());
                var copy = Box("chapter-option-copy-" + i, "chapter-option-copy");
                CardOptionTitles[i] = Text("chapter-option-title-" + i, "chapter-option-title");
                CardOptionDetails[i] = Text("chapter-option-detail-" + i, "chapter-option-detail");
                copy.Add(CardOptionTitles[i]);
                copy.Add(CardOptionDetails[i]);
                button.Add(CardOptionKeys[i]);
                button.Add(copy);
                CardOptions[i] = button;
                options.Add(button);
            }

            CardDismiss = Action("chapter-card-dismiss", "chapter-card-dismiss", "Enter", string.Empty, out Label dismissText);
            CardDismissText = dismissText;
            CardDismiss.clicked += () => CardDismissClicked?.Invoke();
            CardFeedback = Text("chapter-card-feedback", "chapter-card-feedback");
            CardNote = Text("chapter-card-note", "chapter-card-note");
            Card.Add(CardKicker);
            Card.Add(CardTitle);
            Card.Add(CardMeta);
            Card.Add(CardBody);
            Card.Add(CardFacts);
            Card.Add(options);
            Card.Add(CardDismiss);
            Card.Add(CardFeedback);
            Card.Add(CardNote);
            Modal.Add(Card);

            Strip = Box("chapter-strip", "chapter-strip");
            StripKicker = Text("chapter-strip-kicker", "chapter-strip-kicker");
            StripTitle = Text("chapter-strip-title", "chapter-strip-title");
            StripDetail = Text("chapter-strip-detail", "chapter-strip-detail");
            StripAction = Action("chapter-strip-action", "chapter-strip-action", "R", string.Empty, out Label stripText);
            StripActionText = stripText;
            StripAction.clicked += () => StripActionClicked?.Invoke();
            Strip.Add(StripKicker);
            Strip.Add(StripTitle);
            Strip.Add(StripDetail);
            Strip.Add(StripAction);

            // Back to front: strip and beat under the montage; the report and the modal card on top.
            Layer.Add(Strip);
            Layer.Add(Beat);
            Layer.Add(Montage);
            Layer.Add(Report);
            Layer.Add(Modal);
            HideAll();
        }

        public void Attach(VisualElement host, StyleSheet sheet)
        {
            if (host == null) return;
            if (sheet != null && !Layer.styleSheets.Contains(sheet)) Layer.styleSheets.Add(sheet);
            if (Layer.parent != host)
            {
                Layer.RemoveFromHierarchy();
                host.Add(Layer);
            }
        }

        public void Detach()
        {
            HideAll();
            Layer.RemoveFromHierarchy();
        }

        public void HideAll()
        {
            Show(Beat, false);
            Show(Montage, false);
            Show(Report, false);
            Show(Modal, false);
            Show(Strip, false);
        }

        public void SetReducedMotion(bool reduced) => Layer.EnableInClassList("chapter-reduced-motion", reduced);

        public static void Show(VisualElement element, bool visible)
        {
            if (element == null) return;
            DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display != display) element.style.display = display;
        }

        public static bool IsShown(VisualElement element) =>
            element != null && element.style.display != DisplayStyle.None && element.panel != null;

        public static void SetTone(VisualElement element, ChapterTone tone)
        {
            element.EnableInClassList("chapter-tone-info", tone == ChapterTone.Info);
            element.EnableInClassList("chapter-tone-attention", tone == ChapterTone.Attention);
            element.EnableInClassList("chapter-tone-danger", tone == ChapterTone.Danger);
        }

        public static void SetText(Label label, string text)
        {
            text ??= string.Empty;
            if (label.text != text) label.text = text;
            Show(label, text.Length > 0);
        }

        public void SetFeed(IReadOnlyList<string> lines)
        {
            for (int i = 0; i < Feed.Length; i++)
            {
                int source = lines.Count - Feed.Length + i;
                string line = source >= 0 && source < lines.Count ? lines[source] : string.Empty;
                SetText(Feed[i], line);
                Feed[i].EnableInClassList("chapter-feed-old", i < Feed.Length - 2);
            }
        }

        public void ClickStripAction() => StripActionClicked?.Invoke();
        public void ClickMontageStop() => MontageStopClicked?.Invoke();
        public void ClickReportDismiss() => ReportDismissClicked?.Invoke();
        public void ClickCardOption(int index) => CardOptionClicked?.Invoke(index);
        public void ClickCardDismiss() => CardDismissClicked?.Invoke();

        private static Label TallyCell(VisualElement parent, string name, string caption)
        {
            var cell = Box(name + "-cell", "chapter-tally-cell");
            var value = Text(name, "chapter-tally-value", "0");
            cell.Add(value);
            cell.Add(Text(null, "chapter-tally-caption", caption));
            parent.Add(cell);
            return value;
        }

        private static Button Action(string name, string css, string key, string caption, out Label text)
        {
            var button = new Button { name = name, focusable = false, pickingMode = PickingMode.Position };
            button.AddToClassList("chapter-action");
            button.AddToClassList(css);
            button.Add(Text(name + "-key", "chapter-key", key));
            text = Text(name + "-text", "chapter-action-text", caption);
            button.Add(text);
            return button;
        }

        private static VisualElement Box(string name, string css)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            element.AddToClassList(css);
            return element;
        }

        private static Label Text(string name, string css, string content = "")
        {
            var label = new Label(content) { enableRichText = false, pickingMode = PickingMode.Ignore };
            if (!string.IsNullOrEmpty(name)) label.name = name;
            label.AddToClassList(css);
            return label;
        }
    }
}
