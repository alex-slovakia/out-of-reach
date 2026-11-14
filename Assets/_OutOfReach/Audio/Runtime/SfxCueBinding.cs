using System;
using UnityEngine;

namespace OutOfReach.Presentation.Audio
{
    [Serializable]
    public sealed class SfxCueBinding
    {
        [SerializeField] private string cueId;
        [SerializeField] private AudioClip clip;
        [Range(0, 1)] [SerializeField] private float volume = 1f;

        public string CueId => cueId;
        public AudioClip Clip => clip;
        public float Volume => volume;

        public SfxCueBinding(string cueId, AudioClip clip, float volume = 1f)
        {
            this.cueId = cueId;
            this.clip = clip;
            this.volume = volume;
        }
    }
}
