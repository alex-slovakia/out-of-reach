namespace OutOfReach.Presentation.Audio
{
    public readonly struct DialogueSubtitle
    {
        public string CueId { get; }
        public string Text { get; }
        public bool IsVisible { get; }

        public DialogueSubtitle(string cueId, string text, bool isVisible)
        {
            CueId = cueId ?? string.Empty;
            Text = text ?? string.Empty;
            IsVisible = isVisible;
        }
    }
}
