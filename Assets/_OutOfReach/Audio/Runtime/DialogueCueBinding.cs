using System;
using UnityEngine;

namespace OutOfReach.Presentation.Audio
{
    [Serializable]
    public sealed class DialogueCueBinding
    {
        [SerializeField] private string cueId;
        [Tooltip("Explicit clean take. No radio effect or fallback is applied by this presenter.")]
        [SerializeField] private AudioClip cleanClip;
        [TextArea(2, 5)] [SerializeField] private string subtitleText;

        public string CueId => cueId;
        public AudioClip CleanClip => cleanClip;
        public string SubtitleText => subtitleText;

        public DialogueCueBinding(string cueId, AudioClip cleanClip, string subtitleText)
        {
            this.cueId = cueId;
            this.cleanClip = cleanClip;
            this.subtitleText = subtitleText;
        }
    }
}
