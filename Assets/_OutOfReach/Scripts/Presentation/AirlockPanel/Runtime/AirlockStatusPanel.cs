using OutOfReach.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace OutOfReach.Presentation.AirlockPanel
{
    /// <summary>Physical display only: reads contracts each frame, never advances time or changes a seal.</summary>
    public sealed class AirlockStatusPanel : MonoBehaviour
    {
        [SerializeField] private Text stateText, captionText, innerText, outerText, detailText;
        [SerializeField] private Image stateRule, innerLamp, outerLamp;
        [SerializeField] private AirlockCountdownGraphic countdown;
        private IEvaStatus status;
        private ISimulationClock clock;
        private static readonly Color White = new Color(.91f, .94f, .96f, 1);
        private static readonly Color Amber = new Color(.95f, .68f, .30f, 1);
        private static readonly Color Cyan = new Color(.36f, .84f, .88f, 1);
        private static readonly Color Muted = new Color(.35f, .41f, .46f, 1);

        public void ConfigureView(Text state, Text caption, Text inner, Text outer, Text detail,
            Image rule, Image innerIndicator, Image outerIndicator, AirlockCountdownGraphic digits)
        {
            stateText = state; captionText = caption; innerText = inner; outerText = outer; detailText = detail;
            stateRule = rule; innerLamp = innerIndicator; outerLamp = outerIndicator; countdown = digits;
            Refresh();
        }

        public void Configure(IEvaStatus evaStatus, ISimulationClock simulationClock)
        { status = evaStatus; clock = simulationClock; Refresh(); }
        public void ClearBinding() { status = null; clock = null; Refresh(); }
        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            AirlockPanelReadout view = AirlockPanelReadout.Read(status, clock);
            Color tone = view.Tone == AirlockPanelTone.Cycling ? Amber : view.Tone == AirlockPanelTone.Ready ? Cyan
                : view.Tone == AirlockPanelTone.Unavailable ? Muted : White;
            SetText(stateText, view.Status, tone);
            SetText(captionText, view.TimerCaption, Muted);
            SetText(detailText, view.Detail, Muted);
            SetText(innerText, "INNER  " + view.InnerSeal, view.HasState ? White : Muted);
            SetText(outerText, "OUTER  " + view.OuterSeal, view.HasState ? White : Muted);
            if (stateRule) stateRule.color = tone;
            if (innerLamp) innerLamp.color = !view.HasState ? Muted : view.InnerOpen ? Cyan : White;
            if (outerLamp) outerLamp.color = !view.HasState ? Muted : view.OuterOpen ? Cyan : White;
            if (countdown) countdown.SetDisplay(view.CountdownSeconds, tone);
        }

        private static void SetText(Text label, string value, Color tint)
        {
            if (!label) return;
            if (label.text != value) label.text = value;
            if (label.color != tint) label.color = tint;
        }
    }
}
