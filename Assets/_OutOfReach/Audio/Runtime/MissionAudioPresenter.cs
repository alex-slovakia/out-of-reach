using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace OutOfReach.Presentation.Audio
{
    /// <summary>
    /// Owns local playback, crossfades, music ducking and subtitles. It never publishes domain events,
    /// evaluates resource thresholds, chooses an outcome, advances signal time or creates an AudioListener.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionAudioPresenter : MonoBehaviour
    {
        [Header("Explicitly selected clean dialogue and unchanged SFX")]
        [SerializeField] private DialogueCueBinding[] dialogueCues = Array.Empty<DialogueCueBinding>();
        [SerializeField] private SfxCueBinding[] sfxCues = Array.Empty<SfxCueBinding>();

        [Header("Original score: listening review pending; no automatic playback")]
        [SerializeField] private AudioClip isolationMusic;
        [SerializeField] private AudioClip tensionMusic;
        [SerializeField] private AudioClip emergencyMusic;
        [Min(0)] [SerializeField] private float defaultCrossfadeSeconds = 1.25f;

        [Header("Presentation mix")]
        [Range(0, 1)] [SerializeField] private float masterVolume = 1f;
        [Range(0, 1)] [SerializeField] private float dialogueVolume = 1f;
        [Range(0, 1)] [SerializeField] private float musicVolume = .55f;
        [Range(0, 1)] [SerializeField] private float sfxVolume = .8f;
        [Range(-40, 0)] [SerializeField] private float dialogueMusicDuckDb = -14f;
        [Min(.001f)] [SerializeField] private float duckAttackSeconds = .08f;
        [Min(.001f)] [SerializeField] private float duckReleaseSeconds = .65f;

        [Header("Optional existing mixer groups; direct source mixing works without them")]
        [SerializeField] private AudioMixerGroup dialogueOutput;
        [SerializeField] private AudioMixerGroup musicOutput;
        [SerializeField] private AudioMixerGroup sfxOutput;

        private Transform channelRoot;
        private AudioSource dialogueSource;
        private AudioSource sfxSource;
        private AudioSource menuSource;
        private float lastMenuCueTime = -10f;
        private readonly AudioSource[] musicSources = new AudioSource[3];
        private readonly bool[] musicStarted = new bool[3];
        private readonly float[] musicWeights = new float[3];
        private readonly float[] fadeStartWeights = new float[3];
        private float fadeElapsed;
        private float fadeDuration;
        private float duckGain = 1f;
        private bool dialogueActive;
        private bool presentationPaused;
        private DialogueSubtitle currentSubtitle;

        public event Action<DialogueSubtitle> SubtitleChanged;

        public bool IsDialoguePlaying => dialogueActive;
        public string ActiveCueId => dialogueActive ? currentSubtitle.CueId : string.Empty;
        public bool IsPresentationPaused => presentationPaused;
        /// <summary>The requested presentation mood, including while its crossfade is in progress.</summary>
        public MusicMood CurrentMood { get; private set; } = MusicMood.Silent;
        public DialogueSubtitle CurrentSubtitle => currentSubtitle;
        public string LastRejection { get; private set; } = string.Empty;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            presentationPaused = false;
            EnsureSources();
            StopAllPresentation();
        }

        private void OnDisable()
        {
            StopAllPresentation();
            presentationPaused = false;
        }

        private void OnDestroy()
        {
            if (channelRoot)
            {
                if (Application.isPlaying) Destroy(channelRoot.gameObject);
                else DestroyImmediate(channelRoot.gameObject);
            }
        }

        private void Update()
        {
            if (presentationPaused || AudioListener.pause) return;
            if (dialogueActive && dialogueSource && !dialogueSource.isPlaying) FinishDialogue();
            float delta = Time.unscaledDeltaTime;
            if (!Finite(delta) || delta < 0) return;

            fadeElapsed = Mathf.Min(fadeElapsed + delta, fadeDuration);
            float blend = fadeDuration <= 0 ? 1f : Mathf.SmoothStep(0, 1, fadeElapsed / fadeDuration);
            int target = (int)CurrentMood - 1;
            for (int i = 0; i < musicWeights.Length; i++)
            {
                musicWeights[i] = Mathf.Lerp(fadeStartWeights[i], i == target ? 1f : 0f, blend);
                if (blend >= 1f && i != target && musicSources[i] && musicStarted[i])
                {
                    musicSources[i].Stop();
                    musicStarted[i] = false;
                }
            }

            float duckTarget = dialogueActive ? Mathf.Pow(10, SafeDuckDb() / 20f) : 1f;
            float seconds = dialogueActive ? Positive(duckAttackSeconds, .08f) : Positive(duckReleaseSeconds, .65f);
            duckGain = Mathf.Lerp(duckGain, duckTarget, 1f - Mathf.Exp(-delta / seconds));
            ApplyVolumes();
        }

        /// <summary>Replaces an active line. Repeating the same active clip/id is rejected rather than restarted.</summary>
        public bool PlayDialogue(AudioClip clip, string subtitleText, string cueId)
        {
            if (!CanPlay()) return false;
            if (string.IsNullOrWhiteSpace(cueId)) return Reject("Dialogue requires an explicit cue id.");
            if (!PrepareClip(clip)) return false;
            EnsureSources();
            if (dialogueActive && currentSubtitle.CueId == cueId && dialogueSource.clip == clip)
                return Reject("This dialogue cue is already playing.");
            StopDialogue();
            dialogueSource.clip = clip;
            dialogueSource.volume = Safe01(masterVolume) * Safe01(dialogueVolume);
            dialogueSource.Play();
            if (!dialogueSource.isPlaying) return Reject("Dialogue playback did not start.");
            dialogueActive = true;
            currentSubtitle = new DialogueSubtitle(cueId, subtitleText, !string.IsNullOrEmpty(subtitleText));
            LastRejection = string.Empty;
            NotifySubtitle(currentSubtitle);
            return true;
        }

        public bool TryPlayDialogue(string cueId)
        {
            DialogueCueBinding match = null;
            foreach (DialogueCueBinding cue in dialogueCues ?? Array.Empty<DialogueCueBinding>())
            {
                if (cue == null || cue.CueId != cueId) continue;
                if (match != null) return Reject("Duplicate configured dialogue id: " + cueId);
                match = cue;
            }
            return match == null ? Reject("Dialogue cue is not configured: " + cueId)
                : PlayDialogue(match.CleanClip, match.SubtitleText, match.CueId);
        }

        public void StopDialogue()
        {
            if (dialogueSource) dialogueSource.Stop();
            FinishDialogue();
        }

        public bool SetMood(MusicMood mood, float fadeSeconds = -1f)
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return Reject("Audio presenter is not active in Play mode.");
            if ((int)mood < 0 || (int)mood > 3) return Reject("Unknown music mood.");
            if (!Finite(fadeSeconds)) return Reject("Crossfade duration must be finite.");
            AudioClip requestedClip = MusicClip(mood);
            if (mood != MusicMood.Silent && !PrepareClip(requestedClip)) return false;
            EnsureSources();
            if (mood == CurrentMood) { LastRejection = string.Empty; return true; }

            int target = (int)mood - 1;
            if (target >= 0 && !musicStarted[target])
            {
                musicSources[target].clip = requestedClip;
                musicSources[target].volume = 0;
                musicSources[target].Play();
                if (!musicSources[target].isPlaying) return Reject("Music playback did not start.");
                musicStarted[target] = true;
                if (presentationPaused) musicSources[target].Pause();
            }
            for (int i = 0; i < musicWeights.Length; i++) fadeStartWeights[i] = musicWeights[i];
            CurrentMood = mood;
            fadeDuration = fadeSeconds < 0 ? Nonnegative(defaultCrossfadeSeconds, 1.25f) : fadeSeconds;
            fadeElapsed = 0;
            if (fadeDuration == 0)
            {
                for (int i = 0; i < musicWeights.Length; i++)
                {
                    musicWeights[i] = i == target ? 1f : 0f;
                    if (i != target && musicSources[i])
                    {
                        musicSources[i].Stop();
                        musicStarted[i] = false;
                    }
                }
                ApplyVolumes();
            }
            LastRejection = string.Empty;
            return true;
        }

        /// <summary>Two-dimensional UI/suit/radio/contact presentation only; never an airborne sound in vacuum.</summary>
        public bool PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (!CanPlay()) return false;
            if (!Finite(volume)) return Reject("SFX volume must be finite.");
            if (!PrepareClip(clip)) return false;
            EnsureSources();
            sfxSource.volume = Safe01(masterVolume) * Safe01(sfxVolume);
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
            LastRejection = string.Empty;
            return true;
        }

        public bool TryPlaySfx(string cueId)
        {
            SfxCueBinding match = null;
            foreach (SfxCueBinding cue in sfxCues ?? Array.Empty<SfxCueBinding>())
            {
                if (cue == null || cue.CueId != cueId) continue;
                if (match != null) return Reject("Duplicate configured SFX id: " + cueId);
                match = cue;
            }
            return match == null ? Reject("SFX cue is not configured: " + cueId) : PlaySfx(match.Clip, match.Volume);
        }

        /// <summary>Menu feedback remains usable while gameplay sound is paused; it does not resume other channels.</summary>
        public bool TryPlayMenuCue(bool confirm = false)
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return false;
            if (!confirm && Time.unscaledTime - lastMenuCueTime < .075f) return false;
            string id = confirm ? "ui_confirm" : "ui_select";
            SfxCueBinding match = null;
            foreach (SfxCueBinding cue in sfxCues ?? Array.Empty<SfxCueBinding>())
            {
                if (cue == null || cue.CueId != id) continue;
                if (match != null) return Reject("Duplicate configured menu SFX id: " + id);
                match = cue;
            }
            if (match == null || !PrepareClip(match.Clip)) return false;
            EnsureSources();
            if (!menuSource)
            {
                menuSource = MakeSource("Menu feedback", false, sfxOutput, 48);
                menuSource.ignoreListenerPause = true;
            }
            menuSource.volume = Safe01(masterVolume) * Safe01(sfxVolume);
            menuSource.PlayOneShot(match.Clip, Safe01(match.Volume) * (confirm ? .8f : .45f));
            lastMenuCueTime = Time.unscaledTime;
            return true;
        }

        /// <summary>Freezes this presenter's audio and fades without changing Time.timeScale or the simulation clock.</summary>
        public void SetPresentationPaused(bool paused)
        {
            if (presentationPaused == paused) return;
            presentationPaused = paused;
            PauseSource(dialogueSource, paused);
            PauseSource(sfxSource, paused);
            foreach (AudioSource source in musicSources) PauseSource(source, paused);
        }

        /// <summary>Stops all owned playback and hides subtitles; keeps the caller's explicit pause state.</summary>
        public void StopAllPresentation()
        {
            StopDialogue();
            if (sfxSource) sfxSource.Stop();
            if (menuSource) menuSource.Stop();
            for (int i = 0; i < musicSources.Length; i++)
            {
                if (musicSources[i]) musicSources[i].Stop();
                musicStarted[i] = false;
                musicWeights[i] = fadeStartWeights[i] = 0;
            }
            CurrentMood = MusicMood.Silent;
            fadeElapsed = fadeDuration = 0;
            duckGain = 1f;
            ApplyVolumes();
        }

        public void SetLevels(float master, float music, float dialogue, float sfx)
        {
            masterVolume = Safe01(master);
            musicVolume = Safe01(music);
            dialogueVolume = Safe01(dialogue);
            sfxVolume = Safe01(sfx);
            ApplyVolumes();
        }

        /// <summary>Explicit composition/authoring API. Reconfiguration stops old playback; no path discovery occurs.</summary>
        public void ConfigureClips(AudioClip isolation, AudioClip tension, AudioClip emergency,
            DialogueCueBinding[] cleanDialogues, SfxCueBinding[] effects)
        {
            StopAllPresentation();
            isolationMusic = isolation;
            tensionMusic = tension;
            emergencyMusic = emergency;
            dialogueCues = cleanDialogues == null ? Array.Empty<DialogueCueBinding>() : (DialogueCueBinding[])cleanDialogues.Clone();
            sfxCues = effects == null ? Array.Empty<SfxCueBinding>() : (SfxCueBinding[])effects.Clone();
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (!isolationMusic || !tensionMusic || !emergencyMusic)
            { reason = "Assign all three music clips explicitly."; return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (DialogueCueBinding cue in dialogueCues ?? Array.Empty<DialogueCueBinding>())
            {
                if (cue == null || string.IsNullOrWhiteSpace(cue.CueId) || !cue.CleanClip || !ids.Add(cue.CueId))
                { reason = "Dialogue bindings need unique nonempty IDs and explicit clean clips."; return false; }
            }
            ids.Clear();
            foreach (SfxCueBinding cue in sfxCues ?? Array.Empty<SfxCueBinding>())
            {
                if (cue == null || string.IsNullOrWhiteSpace(cue.CueId) || !cue.Clip || !Finite(cue.Volume) || !ids.Add(cue.CueId))
                { reason = "SFX bindings need unique nonempty IDs, clips and finite levels."; return false; }
            }
            reason = string.Empty;
            return true;
        }

        private bool CanPlay()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return Reject("Audio presenter is not active in Play mode.");
            if (presentationPaused || AudioListener.pause) return Reject("Audio presentation is paused.");
            return true;
        }

        private bool PrepareClip(AudioClip clip)
        {
            if (!clip) return Reject("No audio clip was assigned.");
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            return clip.loadState == AudioDataLoadState.Loaded || Reject("Audio data is not loaded; retry after loading completes.");
        }

        private void FinishDialogue()
        {
            if (!dialogueActive) return;
            dialogueActive = false;
            currentSubtitle = new DialogueSubtitle(currentSubtitle.CueId, currentSubtitle.Text, false);
            NotifySubtitle(currentSubtitle);
        }

        private void NotifySubtitle(DialogueSubtitle subtitle)
        {
            Action<DialogueSubtitle> handlers = SubtitleChanged;
            if (handlers == null) return;
            foreach (Action<DialogueSubtitle> handler in handlers.GetInvocationList())
            {
                try { handler(subtitle); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private AudioClip MusicClip(MusicMood mood)
        {
            switch (mood)
            {
                case MusicMood.Isolation: return isolationMusic;
                case MusicMood.Tension: return tensionMusic;
                case MusicMood.Emergency: return emergencyMusic;
                default: return null;
            }
        }

        private void EnsureSources()
        {
            if (!channelRoot)
            {
                channelRoot = new GameObject("Owned audio channels").transform;
                channelRoot.SetParent(transform, false);
            }
            if (!dialogueSource) dialogueSource = MakeSource("Clean dialogue", false, dialogueOutput, 32);
            if (!sfxSource) sfxSource = MakeSource("UI suit radio and contact SFX", false, sfxOutput, 64);
            for (int i = 0; i < musicSources.Length; i++)
                if (!musicSources[i]) musicSources[i] = MakeSource("Score " + (MusicMood)(i + 1), true, musicOutput, 128);
        }

        private AudioSource MakeSource(string objectName, bool loop, AudioMixerGroup group, int priority)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(channelRoot, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            source.volume = 0;
            source.pitch = 1;
            source.priority = priority;
            source.outputAudioMixerGroup = group;
            source.ignoreListenerPause = false;
            return source;
        }

        private void ApplyVolumes()
        {
            float master = Safe01(masterVolume);
            if (dialogueSource) dialogueSource.volume = master * Safe01(dialogueVolume);
            if (sfxSource) sfxSource.volume = master * Safe01(sfxVolume);
            for (int i = 0; i < musicSources.Length; i++)
                if (musicSources[i]) musicSources[i].volume = master * Safe01(musicVolume) * musicWeights[i] * duckGain;
        }

        private static void PauseSource(AudioSource source, bool paused)
        {
            if (!source) return;
            if (paused) source.Pause();
            else source.UnPause();
        }

        private bool Reject(string reason) { LastRejection = reason; return false; }
        private float SafeDuckDb() => Finite(dialogueMusicDuckDb) ? Mathf.Clamp(dialogueMusicDuckDb, -40, 0) : -14;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Safe01(float value) => Finite(value) ? Mathf.Clamp01(value) : 0;
        private static float Positive(float value, float fallback) => Finite(value) && value > 0 ? value : fallback;
        private static float Nonnegative(float value, float fallback) => Finite(value) && value >= 0 ? value : fallback;
    }
}
