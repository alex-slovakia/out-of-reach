using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI
{
    public sealed class SurvivalWakeTitle : VisualElement
    {
        private readonly Label title;
        private float elapsed;
        public bool IsPlaying { get; private set; }
        public int DisplayedDay { get; private set; }
        public float Progress01 => Mathf.Clamp01(elapsed / 3f);

        public SurvivalWakeTitle()
        {
            name = "survival-wake-title";
            pickingMode = PickingMode.Ignore;
            AddToClassList("survival-wake-title");
            title = new Label { name = "survival-wake-day", enableRichText = false, pickingMode = PickingMode.Ignore };
            title.AddToClassList("survival-wake-day");
            Add(title);
            Reset();
        }

        public void Begin(int missionDay)
        {
            DisplayedDay = missionDay;
            title.text = missionDay > 0 ? "DAY " + missionDay.ToString(CultureInfo.InvariantCulture) : "DAY —";
            elapsed = 0f;
            IsPlaying = true;
            title.style.opacity = 0f;
        }

        public void Advance(float seconds, bool canPresent, bool reducedMotion)
        {
            if (IsPlaying && canPresent) elapsed += Mathf.Clamp(seconds, 0f, .1f);
            if (elapsed >= 3f) IsPlaying = false;
            EnableInClassList("survival-hidden", !IsPlaying || !canPresent);
            if (!IsPlaying) return;
            float arrival = Ease(elapsed / .22f);
            float departure = 1f - Ease((elapsed - 1.45f) / 1.55f);
            title.style.opacity = arrival * departure;
            title.style.top = reducedMotion ? 0f : -30f * (1f - Ease(elapsed / 1.05f));
        }

        public void Reset()
        {
            elapsed = 0f; DisplayedDay = 0; IsPlaying = false;
            title.text = string.Empty;
            title.style.opacity = 0f;
            title.style.top = 0f;
            EnableInClassList("survival-hidden", true);
        }

        private static float Ease(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
