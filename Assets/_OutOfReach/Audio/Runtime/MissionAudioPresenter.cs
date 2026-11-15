using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace OutOfReach.Presentation.Audio
{
    /// <summary>Who owns the music during the opening (menu, loading, departure, arrival).</summary>
    public enum OpeningMusicState
    {
        /// <summary>No opening program; mood tracks behave as before.</summary>
        Idle,
        /// <summary>Planning after a mode was chosen: the approved score plays from 0 s, then rests before its launch section.</summary>
        Loading,
        /// <summary>The departure cutscene: score and launch radio follow the departure playhead.</summary>
        Departure,
        /// <summary>After the departure: the score has ended or is fading and no mood track may return until released.</summary>
        Held
    }

    /// <summary>
    /// The NASA launch radio countdown (`Audio/LaunchRadio`). Times are seconds in the radio clip; the clip starts at
    /// <see cref="DepartureStartSeconds"/> on the departure playhead. Measured from the waveform, see that README.
    /// </summary>
    public static class LaunchRadioCountdown
    {
        public const string CueId = "launch-radio-countdown";
        public const float DepartureStartSeconds = 1.82f;
        /// <summary>Radio-clip time of the onset of "zero" (departure 10.40 s, vehicle moves at 10.5 s).</summary>
        public const float ZeroRadioSeconds = 8.58f;

        private static readonly float[] Starts = { .05f, 2.54f, 3.47f, 4.49f, 5.38f, 6.47f, 7.44f, 8.58f, 9.44f, 11.37f, 12.44f, 14.48f, 16.03f };
        private static readonly float[] Ends = { 2.54f, 3.47f, 4.49f, 5.38f, 6.47f, 7.44f, 8.58f, 9.44f, 11.37f, 12.44f, 13.66f, 15.90f, 17.50f };
        private static readonly string[] Texts =
        {
            "Ignition sequence start.", "Six.", "Five.", "Four.", "Three.", "Two.", "One.", "Zero.",
            "All engines running.", "Liftoff!", "We have a liftoff.", "Tower cleared.", "We got a roll program."
        };

        public static int CaptionCount => Texts.Length;
        public static float CaptionStart(int index) => Starts[index];
        public static float CaptionEnd(int index) => Ends[index];
        public static string CaptionText(int index) => Texts[index];

        /// <summary>The caption to show at a radio-clip time, or an empty string between transmissions.</summary>
        public static string CaptionAt(float radioSeconds)
        {
            if (float.IsNaN(radioSeconds) || float.IsInfinity(radioSeconds)) return string.Empty;
            for (int i = Texts.Length - 1; i >= 0; i--)
                if (radioSeconds >= Starts[i]) return radioSeconds < Ends[i] ? Texts[i] : string.Empty;
            return string.Empty;
        }
    }

    /// <summary>
    /// Owns local playback, crossfades, music ducking and subtitles. It never publishes domain events,
    /// evaluates resource thresholds, chooses an outcome, advances signal time or creates an AudioListener.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionAudioPresenter : MonoBehaviour
    {
        /// <summary>Score seconds at departure 0 s: the 49.91 s downbeat that opens the score's launch section.</summary>
        public const float OpeningDepartureScoreSeconds = 49.90f;
        /// <summary>A loading score still playing at this position fades out so the launch section stays unused.</summary>
        public const float OpeningLoadingRestSeconds = 47.40f;
        public const float OpeningLoadingFadeInSeconds = 2f;
        public const float OpeningLoadingRestFadeSeconds = 2.5f;
        public const float OpeningDepartureEntranceSeconds = .12f;
        public const float OpeningCrossfadeOutSeconds = 1.5f;
        public const float OpeningEndFadeSeconds = 3.5f;
        public const float OpeningSkipFadeSeconds = .6f;
        /// <summary>Score/radio drift from the departure playhead that is corrected (a stalled frame, not normal jitter).</summary>
        public const float OpeningDriftToleranceSeconds = .30f;

        [Header("Explicitly selected clean dialogue and unchanged SFX")]
        [SerializeField] private DialogueCueBinding[] dialogueCues = Array.Empty<DialogueCueBinding>();
        [SerializeField] private SfxCueBinding[] sfxCues = Array.Empty<SfxCueBinding>();

        [Header("Original score: listening review pending; no automatic playback")]
        [SerializeField] private AudioClip isolationMusic;
        [SerializeField] private AudioClip tensionMusic;
        [SerializeField] private AudioClip emergencyMusic;
        [SerializeField] private AudioClip recoveryMusic; // Optional: old three-mood configurations remain valid.
        [Min(0)] [SerializeField] private float defaultCrossfadeSeconds = 1.25f;

        [Header("Opening program: NASA launch radio heard over the approved opening score")]
        [SerializeField] private AudioClip launchRadio;
        [Range(-40, 0)] [SerializeField] private float launchRadioDuckDb = -5f;
        [Range(0, 1)] [SerializeField] private float launchRadioVolume = 1f;
        [Min(.05f)] [SerializeField] private float moodReturnSeconds = 2.5f;

        [Header("Presentation mix")]
        [Range(0, 1)] [SerializeField] private float masterVolume = 1f;
        [Range(0, 1)] [SerializeField] private float dialogueVolume = 1f;
        [Range(0, 1)] [SerializeField] private float musicVolume = .8f;
        [Range(0, 1)] [SerializeField] private float backgroundScoreTrim = .44f;
        [Range(0, 1)] [SerializeField] private float departureMusicTrim = .65f;
        [Range(0, 1)] [SerializeField] private float sfxVolume = .8f;
        [Range(-40, 0)] [SerializeField] private float dialogueMusicDuckDb = -8f;
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
        private AudioSource departureCabinSource;
        private AudioSource departureMusicSource;
        private bool departureMusicActive;
        private float lastMenuCueTime = -10f;
        private readonly AudioSource[] musicSources = new AudioSource[4];
        private readonly bool[] musicStarted = new bool[4];
        private readonly float[] musicWeights = new float[4];
        private readonly float[] fadeStartWeights = new float[4];
        private float fadeElapsed;
        private float fadeDuration;
        private float duckGain = 1f;
        private bool dialogueActive;
        private bool presentationPaused;
        private DialogueSubtitle currentSubtitle;
        private SubtitleSegments subtitleSegments;

        // Opening program. Two score channels let a loading position fade out while the departure enters.
        private readonly AudioSource[] openingSources = new AudioSource[2];
        private readonly float[] openingGain = new float[2];
        private readonly float[] openingFadeFrom = new float[2];
        private readonly float[] openingFadeTo = new float[2];
        private readonly float[] openingFadeElapsed = new float[2];
        private readonly float[] openingFadeDuration = new float[2];
        private int openingCurrent = -1;
        private AudioClip openingScore;
        private float departureDurationSeconds;
        private float departurePlayhead;
        private float lastDriftCorrection = -10f;
        private AudioSource launchRadioSource;
        private bool radioActive;
        private bool radioStarted;
        private bool radioFinished;
        private float backgroundGate = 1f;
        private float backgroundGateReturnSeconds = 2.5f;

        public event Action<DialogueSubtitle> SubtitleChanged;

        /// <summary>True while a dialogue line or the launch radio is speaking; queued lines wait for both.</summary>
        public bool IsDialoguePlaying => dialogueActive || radioActive;
        public string ActiveCueId => dialogueActive ? currentSubtitle.CueId : radioActive ? LaunchRadioCountdown.CueId : string.Empty;
        public bool IsPresentationPaused => presentationPaused;
        /// <summary>The requested presentation mood, including while its crossfade is in progress.</summary>
        public MusicMood CurrentMood { get; private set; } = MusicMood.Silent;
        public DialogueSubtitle CurrentSubtitle => currentSubtitle;
        public string LastRejection { get; private set; } = string.Empty;

        public OpeningMusicState OpeningState { get; private set; } = OpeningMusicState.Idle;
        /// <summary>While true, mood tracks stay silent: the opening score (or its deliberate silence) owns the music.</summary>
        public bool OpeningOwnsMusic => OpeningState != OpeningMusicState.Idle;
        public bool IsLaunchRadioPlaying => radioActive;
        public AudioClip LaunchRadioClip => launchRadio;
        public int OpeningDriftCorrections { get; private set; }
        /// <summary>Score position of the channel that carries the opening timeline, or -1 when none is playing.</summary>
        public float OpeningScoreSeconds => openingCurrent >= 0 && openingSources[openingCurrent]
            && openingSources[openingCurrent].clip && (openingSources[openingCurrent].isPlaying || presentationPaused)
            ? openingSources[openingCurrent].time : -1f;
        /// <summary>Current output gain of the opening score (0 when silent). For diagnostics and tests.</summary>
        public float OpeningScoreLevel => openingCurrent >= 0 && openingSources[openingCurrent] ? openingSources[openingCurrent].volume : 0f;
        /// <summary>Mood tracks' current gate (0 while the opening owns the music, 1 when they may be heard).</summary>
        public float MoodGate => backgroundGate;

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
            if (dialogueActive && dialogueSource && dialogueSource.clip && subtitleSegments != null)
            {
                float progress = (float)dialogueSource.timeSamples / Math.Max(1, dialogueSource.clip.samples);
                string nextText = subtitleSegments.At(progress);
                if (nextText != currentSubtitle.Text)
                {
                    currentSubtitle = new DialogueSubtitle(currentSubtitle.CueId, nextText, nextText.Length > 0);
                    NotifySubtitle(currentSubtitle);
                }
            }
            if (radioActive && (!launchRadioSource || !launchRadioSource.isPlaying))
            {
                radioActive = false;
                radioFinished = true;
            }
            UpdateRadioCaption();
            if (departureMusicActive && (!departureMusicSource || !departureMusicSource.isPlaying))
                departureMusicActive = false;
            float delta = Time.unscaledDeltaTime;
            if (!Finite(delta) || delta < 0) return;

            AdvanceOpening(delta);
            float gateTarget = departureMusicActive || OpeningOwnsMusic ? 0f : 1f;
            float gateSeconds = gateTarget < backgroundGate ? .5f : Positive(backgroundGateReturnSeconds, 2.5f);
            backgroundGate = Mathf.MoveTowards(backgroundGate, gateTarget, delta / gateSeconds);

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

            float duckTarget = Mathf.Min(dialogueActive ? Mathf.Pow(10, SafeDuckDb() / 20f) : 1f,
                radioActive ? Mathf.Pow(10, SafeRadioDuckDb() / 20f) : 1f);
            bool ducking = duckTarget < duckGain;
            float seconds = ducking ? Positive(duckAttackSeconds, .08f) : Positive(duckReleaseSeconds, .65f);
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
            subtitleSegments = new SubtitleSegments(subtitleText);
            string firstCaption = subtitleSegments.At(0f);
            currentSubtitle = new DialogueSubtitle(cueId, firstCaption, firstCaption.Length > 0);
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
            if ((int)mood < 0 || (int)mood > 4) return Reject("Unknown music mood.");
            if (!Finite(fadeSeconds)) return Reject("Crossfade duration must be finite.");
            AudioClip requestedClip = MusicClip(mood);
            if (mood != MusicMood.Silent && !PrepareClip(requestedClip)) return false;
            EnsureSources();
            int target = (int)mood - 1;
            if (mood == CurrentMood)
            {
                // One retry per explicit request. Paused sources report not playing: never restart them here.
                if (target >= 0 && !presentationPaused && !AudioListener.pause
                    && (!musicStarted[target] || !musicSources[target].isPlaying))
                {
                    musicSources[target].clip = requestedClip;
                    musicSources[target].Play();
                    if (!musicSources[target].isPlaying) return Reject("Music recovery playback did not start.");
                    musicStarted[target] = true;
                    ApplyVolumes();
                }
                if (fadeSeconds == 0)
                {
                    fadeElapsed = fadeDuration = 0;
                    SnapMusicWeights(target); // Explicit same-mood hard cut still completes an in-flight fade.
                }
                LastRejection = string.Empty;
                return true;
            }
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
            if (fadeDuration == 0) SnapMusicWeights(target);
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
            return TryFindSfx(cueId, out SfxCueBinding match) && PlaySfx(match.Clip, match.Volume);
        }

        /// <summary>Plays a timed transport effect through the pause-aware, dialogue-ducked cabin source.</summary>
        private bool departureAtmosphericMix;

        public bool TryPlayDepartureEffectCue(string cueId)
        {
            if (!CanPlay() || !TryFindSfx(cueId, out SfxCueBinding match) || !PrepareClip(match.Clip)) return false;
            if (!Finite(match.Volume)) return Reject("Departure cabin SFX volume must be finite.");
            EnsureSources();
            if (!departureCabinSource)
                departureCabinSource = MakeSource("Departure launch effects", false, sfxOutput, 56);
            ApplyVolumes();
            if(cueId=="departure-pad-roar")departureAtmosphericMix=true;
            ApplyVolumes();
            departureCabinSource.PlayOneShot(match.Clip, Safe01(match.Volume));
            LastRejection = string.Empty;
            return true;
        }

        public void StopDepartureEffects()
        {
            if (departureCabinSource) departureCabinSource.Stop();
            departureAtmosphericMix=false;
        }

        /// <summary>Legacy: plays a departure score once, at a PCM offset, independently of mission mood.
        /// The opening program (<see cref="BeginOpeningDeparture"/>) replaces it for the I01 departure.</summary>
        public bool PlayDepartureMusic(AudioClip clip, float startSeconds = 0f)
        {
            if (!CanPlay() || !PrepareClip(clip)) return false;
            if (!Finite(startSeconds) || startSeconds < 0 || startSeconds >= clip.length || clip.samples <= 0)
                return Reject("Departure music needs a start time inside the clip.");
            if (departureMusicActive && departureMusicSource && departureMusicSource.clip == clip)
                return Reject("This departure score is already playing.");
            EnsureSources();
            if (!departureMusicSource)
                departureMusicSource = MakeSource("Departure score", false, musicOutput, 96);
            departureMusicSource.Stop();
            departureMusicSource.clip = clip;
            departureMusicSource.timeSamples = Math.Min(clip.samples - 1, (int)((double)startSeconds * clip.frequency));
            departureMusicSource.volume = 0;
            departureMusicSource.Play();
            departureMusicActive = departureMusicSource.isPlaying;
            ApplyVolumes();
            if (!departureMusicActive) return Reject("Departure music playback did not start.");
            LastRejection = string.Empty;
            return true;
        }

        public void StopDepartureMusic()
        {
            if (departureMusicSource) departureMusicSource.Stop();
            departureMusicActive = false;
            ApplyVolumes();
        }

        /// <summary>Explicit NASA launch radio clip (the prefab assigns it; composition may override before play).</summary>
        public void ConfigureLaunchRadio(AudioClip radio)
        {
            if (OpeningState == OpeningMusicState.Departure) StopLaunchRadio(true);
            launchRadio = radio;
        }

        /// <summary>
        /// Loading has begun (a mode was chosen in Planning). Starts the approved score from 0 s with a fade-in and
        /// silences mood tracks. Idempotent for the same score; never interrupts a departure.
        /// </summary>
        public bool BeginOpeningLoading(AudioClip score)
        {
            if (!CanPlay() || !PrepareClip(score)) return false;
            if (OpeningState == OpeningMusicState.Loading && openingScore == score) { LastRejection = string.Empty; return true; }
            if (OpeningState == OpeningMusicState.Departure || OpeningState == OpeningMusicState.Held)
                return Reject("The opening score belongs to this launch; a restart begins a new opening.");
            if (score.length <= OpeningDepartureScoreSeconds)
                return Reject("The opening score must be longer than its departure position.");
            EnsureSources();
            StopOpeningSources();
            StopLaunchRadio(true);
            openingScore = score;
            if (!StartOpeningChannel(0, 0f, OpeningLoadingFadeInSeconds)) return false;
            OpeningState = OpeningMusicState.Loading;
            ApplyVolumes();
            LastRejection = string.Empty;
            return true;
        }

        /// <summary>
        /// The departure cutscene began (seat). The score enters at <see cref="OpeningDepartureScoreSeconds"/> on its
        /// own channel while an audible loading position fades out under it. From now on
        /// <see cref="TrackOpeningDeparture"/> keeps score and radio on the departure playhead.
        /// </summary>
        public bool BeginOpeningDeparture(AudioClip score, float durationSeconds)
        {
            if (!CanPlay() || !PrepareClip(score)) return false;
            if (!Finite(durationSeconds) || durationSeconds <= 0) return Reject("Departure duration must be positive.");
            if (score.length <= OpeningDepartureScoreSeconds)
                return Reject("The opening score must be longer than its departure position.");
            if (OpeningState == OpeningMusicState.Departure && openingScore == score)
                return Reject("The departure score is already playing.");
            EnsureSources();
            StopLaunchRadio(true);
            // The launch section always enters on its downbeat; any audible loading position fades out under it.
            for (int i = 0; i < openingSources.Length; i++)
                if (openingSources[i] && openingSources[i].isPlaying) SetOpeningFade(i, 0f, OpeningCrossfadeOutSeconds);
            int next = openingCurrent == 0 ? 1 : 0;
            openingScore = score;
            if (!StartOpeningChannel(next, OpeningDepartureScoreSeconds, OpeningDepartureEntranceSeconds))
            {
                OpeningState = OpeningMusicState.Held;
                return false;
            }
            departureDurationSeconds = durationSeconds;
            departurePlayhead = 0f;
            radioStarted = radioFinished = false;
            OpeningState = OpeningMusicState.Departure;
            ApplyVolumes();
            LastRejection = string.Empty;
            return true;
        }

        /// <summary>
        /// Call every frame while the departure plays, with its presented playhead. Starts the launch radio at
        /// <see cref="LaunchRadioCountdown.DepartureStartSeconds"/> and corrects score/radio drift after stalled frames.
        /// Paused presentation is never advanced or corrected.
        /// </summary>
        public void TrackOpeningDeparture(float departureSeconds)
        {
            if (OpeningState != OpeningMusicState.Departure || presentationPaused || AudioListener.pause) return;
            if (!Finite(departureSeconds) || departureSeconds < 0) return;
            departurePlayhead = departureSeconds;
            bool mayCorrect = Time.unscaledTime - lastDriftCorrection >= .5f;
            if (openingCurrent >= 0 && openingScore)
            {
                AudioSource score = openingSources[openingCurrent];
                float expected = OpeningDepartureScoreSeconds + departureSeconds;
                if (expected < openingScore.length - .05f)
                {
                    if (!score.isPlaying)
                    {
                        score.clip = openingScore;
                        score.timeSamples = SampleAt(openingScore, expected);
                        score.Play();
                    }
                    else if (mayCorrect && Mathf.Abs(score.time - expected) > OpeningDriftToleranceSeconds)
                    {
                        score.timeSamples = SampleAt(openingScore, expected);
                        lastDriftCorrection = Time.unscaledTime;
                        OpeningDriftCorrections++;
                    }
                }
            }
            TrackLaunchRadio(departureSeconds - LaunchRadioCountdown.DepartureStartSeconds, mayCorrect);
            ApplyVolumes();
        }

        /// <summary>The departure ended (completed, skipped or cancelled). The radio stops; the score fades out and the
        /// opening keeps the music silent until <see cref="ReleaseOpeningMusic"/>.</summary>
        public void EndOpeningDeparture(float fadeSeconds)
        {
            if (OpeningState != OpeningMusicState.Departure && OpeningState != OpeningMusicState.Loading) return;
            StopLaunchRadio(true);
            float endFactor = OpeningState == OpeningMusicState.Departure ? DepartureEndFactor() : 1f;
            for (int i = 0; i < openingSources.Length; i++)
            {
                if (!openingSources[i] || !openingSources[i].isPlaying) continue;
                openingGain[i] *= endFactor;
                SetOpeningFade(i, 0f, Math.Max(.05f, Nonnegative(fadeSeconds, OpeningSkipFadeSeconds)));
            }
            OpeningState = OpeningMusicState.Held;
            ApplyVolumes();
        }

        /// <summary>The mission left the opening (e.g. a new phase). Mood tracks may return over the given fade.</summary>
        public void ReleaseOpeningMusic(float moodFadeInSeconds = -1f)
        {
            if (OpeningState == OpeningMusicState.Idle) return;
            StopLaunchRadio(true);
            for (int i = 0; i < openingSources.Length; i++)
                if (openingSources[i] && openingSources[i].isPlaying && openingGain[i] > 0f) SetOpeningFade(i, 0f, .5f);
            backgroundGateReturnSeconds = moodFadeInSeconds < 0 ? Positive(moodReturnSeconds, 2.5f) : Math.Max(.05f, moodFadeInSeconds);
            OpeningState = OpeningMusicState.Idle;
            ApplyVolumes();
        }

        /// <summary>Stops the opening score and radio immediately (restart). Mood tracks return with a fade.</summary>
        public void CancelOpening()
        {
            StopOpeningSources();
            StopLaunchRadio(true);
            if (OpeningState != OpeningMusicState.Idle) backgroundGateReturnSeconds = Positive(moodReturnSeconds, 2.5f);
            OpeningState = OpeningMusicState.Idle;
            openingScore = null;
            ApplyVolumes();
        }

        private bool TryFindSfx(string cueId, out SfxCueBinding match)
        {
            match = null;
            foreach (SfxCueBinding cue in sfxCues ?? Array.Empty<SfxCueBinding>())
            {
                if (cue == null || cue.CueId != cueId) continue;
                if (match != null) return Reject("Duplicate configured SFX id: " + cueId);
                match = cue;
            }
            return match != null || Reject("SFX cue is not configured: " + cueId);
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
            PauseSource(departureCabinSource, paused);
            PauseSource(departureMusicSource, paused);
            PauseSource(launchRadioSource, paused);
            foreach (AudioSource source in openingSources) PauseSource(source, paused);
            foreach (AudioSource source in musicSources) PauseSource(source, paused);
        }

        /// <summary>Stops all owned playback and hides subtitles; keeps the caller's explicit pause state.</summary>
        public void StopAllPresentation()
        {
            StopDialogue();
            if (sfxSource) sfxSource.Stop();
            StopDepartureEffects();
            StopDepartureMusic();
            CancelOpening();
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
            backgroundGate = 1f;
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

        /// <summary>Optional fourth mood; existing ConfigureClips callers keep their three-mood behavior.</summary>
        public void ConfigureRecoveryMusic(AudioClip recovery)
        {
            if (CurrentMood == MusicMood.Recovery) SetMood(MusicMood.Silent, 0);
            recoveryMusic = recovery;
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

        private void AdvanceOpening(float delta)
        {
            for (int i = 0; i < openingSources.Length; i++)
            {
                if (openingFadeDuration[i] > 0f)
                {
                    openingFadeElapsed[i] = Mathf.Min(openingFadeElapsed[i] + delta, openingFadeDuration[i]);
                    float blend = Mathf.SmoothStep(0f, 1f, openingFadeElapsed[i] / openingFadeDuration[i]);
                    openingGain[i] = Mathf.Lerp(openingFadeFrom[i], openingFadeTo[i], blend);
                    if (openingFadeElapsed[i] >= openingFadeDuration[i])
                    {
                        openingFadeDuration[i] = 0f;
                        openingGain[i] = openingFadeTo[i];
                    }
                }
                if (openingGain[i] <= 0f && openingFadeDuration[i] <= 0f && openingSources[i] && openingSources[i].isPlaying)
                {
                    openingSources[i].Stop();
                    if (i == openingCurrent && OpeningState != OpeningMusicState.Departure) openingCurrent = -1;
                }
            }
            if (OpeningState != OpeningMusicState.Loading || openingCurrent < 0) return;
            AudioSource loading = openingSources[openingCurrent];
            if (loading && loading.isPlaying && loading.time >= OpeningLoadingRestSeconds && openingFadeTo[openingCurrent] > 0f)
                SetOpeningFade(openingCurrent, 0f, OpeningLoadingRestFadeSeconds); // Rest before the launch section.
            else if (loading && !loading.isPlaying && !presentationPaused) openingCurrent = -1; // Clip reached its end.
        }

        private void TrackLaunchRadio(float radioSeconds, bool mayCorrect)
        {
            if (!launchRadio || radioFinished || radioSeconds < 0f) return;
            if (radioSeconds >= launchRadio.length)
            {
                StopLaunchRadio(false);
                radioFinished = true;
                return;
            }
            if (!radioStarted)
            {
                if (!PrepareClip(launchRadio)) return;
                if (!launchRadioSource) launchRadioSource = MakeSource("Launch radio countdown", false, dialogueOutput, 24);
                launchRadioSource.clip = launchRadio;
                launchRadioSource.timeSamples = SampleAt(launchRadio, radioSeconds);
                launchRadioSource.Play();
                radioStarted = true;
                radioActive = launchRadioSource.isPlaying;
                return;
            }
            if (radioActive && mayCorrect && Mathf.Abs(launchRadioSource.time - radioSeconds) > OpeningDriftToleranceSeconds)
            {
                launchRadioSource.timeSamples = SampleAt(launchRadio, radioSeconds);
                lastDriftCorrection = Time.unscaledTime;
                OpeningDriftCorrections++;
            }
        }

        private void StopLaunchRadio(bool reset)
        {
            if (launchRadioSource) launchRadioSource.Stop();
            radioActive = false;
            if (reset) radioStarted = radioFinished = false;
            if (currentSubtitle.CueId == LaunchRadioCountdown.CueId && currentSubtitle.IsVisible)
            {
                currentSubtitle = new DialogueSubtitle(LaunchRadioCountdown.CueId, currentSubtitle.Text, false);
                NotifySubtitle(currentSubtitle);
            }
        }

        // The radio's captions use the subtitle line only while no dialogue line owns it.
        private void UpdateRadioCaption()
        {
            if (dialogueActive) return;
            string text = radioActive && launchRadioSource ? LaunchRadioCountdown.CaptionAt(launchRadioSource.time) : string.Empty;
            bool visible = text.Length > 0;
            bool radioOwnsLine = currentSubtitle.CueId == LaunchRadioCountdown.CueId;
            if (!radioOwnsLine && !visible) return;
            if (radioOwnsLine && currentSubtitle.IsVisible == visible && (!visible || currentSubtitle.Text == text)) return;
            currentSubtitle = new DialogueSubtitle(LaunchRadioCountdown.CueId, visible ? text : currentSubtitle.Text, visible);
            NotifySubtitle(currentSubtitle);
        }

        private bool StartOpeningChannel(int index, float seconds, float fadeInSeconds)
        {
            if (!openingSources[index]) openingSources[index] = MakeSource("Opening score " + (index == 0 ? "A" : "B"), false, musicOutput, 90);
            AudioSource source = openingSources[index];
            source.Stop();
            source.clip = openingScore;
            source.timeSamples = SampleAt(openingScore, seconds);
            openingGain[index] = 0f;
            SetOpeningFade(index, 1f, fadeInSeconds);
            source.volume = 0f;
            source.Play();
            if (!source.isPlaying) { openingCurrent = -1; return Reject("Opening score playback did not start."); }
            openingCurrent = index;
            return true;
        }

        private void SetOpeningFade(int index, float target, float seconds)
        {
            openingFadeFrom[index] = openingGain[index];
            openingFadeTo[index] = target;
            openingFadeElapsed[index] = 0f;
            openingFadeDuration[index] = Math.Max(0f, seconds);
            if (openingFadeDuration[index] <= 0f) openingGain[index] = target;
        }

        private void StopOpeningSources()
        {
            for (int i = 0; i < openingSources.Length; i++)
            {
                if (openingSources[i]) openingSources[i].Stop();
                openingGain[i] = openingFadeFrom[i] = openingFadeTo[i] = 0f;
                openingFadeElapsed[i] = openingFadeDuration[i] = 0f;
            }
            openingCurrent = -1;
            departurePlayhead = 0f;
        }

        private float DepartureEndFactor()
        {
            if (departureDurationSeconds <= 0f) return 1f;
            float remaining = departureDurationSeconds - departurePlayhead;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(remaining / OpeningEndFadeSeconds));
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
            subtitleSegments = null;
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
                case MusicMood.Recovery: return recoveryMusic;
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

        private void SnapMusicWeights(int target)
        {
            for (int i = 0; i < musicWeights.Length; i++)
            {
                musicWeights[i] = fadeStartWeights[i] = i == target ? 1f : 0f;
                if (i != target && musicSources[i])
                {
                    musicSources[i].Stop();
                    musicStarted[i] = false;
                }
            }
            ApplyVolumes();
        }

        private void ApplyVolumes()
        {
            float master = Safe01(masterVolume);
            if (dialogueSource) dialogueSource.volume = master * Safe01(dialogueVolume);
            if (sfxSource) sfxSource.volume = master * Safe01(sfxVolume);
            if (launchRadioSource) launchRadioSource.volume = master * Safe01(dialogueVolume) * Safe01(launchRadioVolume);
            // Atmospheric roar/shock keep body beneath dialogue, capped at -4 dB duck floor.
            // Cabin effects and the user-approved full departure score retain their existing ducking.
            if (departureCabinSource) departureCabinSource.volume = master * Safe01(sfxVolume)
                * (departureAtmosphericMix ? Mathf.Max(duckGain,.631f) : duckGain);
            float scoreLevel = master * Safe01(musicVolume) * duckGain;
            if (departureMusicSource) departureMusicSource.volume = scoreLevel * Safe01(departureMusicTrim);
            float endFactor = OpeningState == OpeningMusicState.Departure ? DepartureEndFactor() : 1f;
            for (int i = 0; i < openingSources.Length; i++)
                if (openingSources[i]) openingSources[i].volume = scoreLevel * Safe01(departureMusicTrim) * Mathf.Clamp01(openingGain[i])
                    * (i == openingCurrent ? endFactor : 1f);
            float backgroundScoreLevel = scoreLevel * Safe01(backgroundScoreTrim) * Mathf.Clamp01(backgroundGate);
            for (int i = 0; i < musicSources.Length; i++)
                if (musicSources[i]) musicSources[i].volume = backgroundScoreLevel * musicWeights[i];
        }

        private static int SampleAt(AudioClip clip, float seconds) =>
            Math.Max(0, Math.Min(clip.samples - 1, (int)((double)seconds * clip.frequency)));

        private static void PauseSource(AudioSource source, bool paused)
        {
            if (!source) return;
            if (paused) source.Pause();
            else source.UnPause();
        }

        private bool Reject(string reason) { LastRejection = reason; return false; }
        private float SafeDuckDb() => Finite(dialogueMusicDuckDb) ? Mathf.Clamp(dialogueMusicDuckDb, -40, 0) : -8;
        private float SafeRadioDuckDb() => Finite(launchRadioDuckDb) ? Mathf.Clamp(launchRadioDuckDb, -40, 0) : -5;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Safe01(float value) => Finite(value) ? Mathf.Clamp01(value) : 0;
        private static float Positive(float value, float fallback) => Finite(value) && value > 0 ? value : fallback;
        private static float Nonnegative(float value, float fallback) => Finite(value) && value >= 0 ? value : fallback;
    }
}
