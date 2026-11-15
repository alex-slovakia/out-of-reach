using UnityEngine;

namespace OutOfReach.Presentation.UI.Startup
{
    /// <summary>
    /// Plays one procedurally synthesised clip (<see cref="PencilScratchSynth"/>) on a hidden, self-owned AudioSource and
    /// destroys it with the card. It needs no mixer, scene object or asset; non-diegetic, 2D, unaffected by listener pause.
    /// </summary>
    public sealed class StartupPencilAudio
    {
        private GameObject host;
        private AudioSource source;
        private AudioClip clip;

        private StartupPencilAudio() { }

        public static StartupPencilAudio Create(float[] samples)
        {
            if (samples == null || samples.Length == 0 || !Application.isPlaying) return null;
            var audio = new StartupPencilAudio();
            audio.clip = AudioClip.Create("startup-pencil-scratch", samples.Length, 1, PencilScratchSynth.SampleRate, false);
            audio.clip.SetData(samples, 0);
            audio.host = new GameObject("StartupPencilAudio") { hideFlags = HideFlags.HideAndDontSave };
            audio.source = audio.host.AddComponent<AudioSource>();
            audio.source.clip = audio.clip;
            audio.source.playOnAwake = false;
            audio.source.loop = false;
            audio.source.spatialBlend = 0f;
            audio.source.bypassReverbZones = true;
            audio.source.ignoreListenerPause = true;
            audio.source.volume = 0f;
            return audio;
        }

        public bool Play()
        {
            if (!source) return false;
            source.Play();
            return true;
        }

        public void SetGain(float gain)
        {
            if (source) source.volume = Mathf.Clamp01(gain);
        }

        /// <summary>Re-seats the playhead when it has drifted from the picture by more than a tenth of a second.</summary>
        public void SyncTo(float seconds)
        {
            if (!source || !source.isPlaying || clip == null) return;
            if (seconds < 0f || seconds >= clip.length - .05f) return;
            if (Mathf.Abs(source.time - seconds) > .1f) source.time = seconds;
        }

        public void Dispose()
        {
            if (source) source.Stop();
            if (host) Object.Destroy(host);
            if (clip) Object.Destroy(clip);
            host = null; source = null; clip = null;
        }
    }
}
