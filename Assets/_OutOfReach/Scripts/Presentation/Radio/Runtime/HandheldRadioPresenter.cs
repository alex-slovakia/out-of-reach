using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Presentation.Audio;
using OutOfReach.Presentation.Cinematics;
using OutOfReach.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.Radio
{
    public enum RadioBoardingPlacement { PadHatch, AuroraArrival }

    /// <summary>Maps a chapter dispatch content id (STORY-SCHEDULE-02) to a radio beat.</summary>
    [Serializable]
    public sealed class RadioDispatchBinding
    {
        public string contentId;
        public string beatId;
    }

    /// <summary>A recorded voice line (Audio/Voices/...) for a radio line's cue id.</summary>
    [Serializable]
    public sealed class RadioVoiceClip
    {
        public string cueId;
        public AudioClip clip;
    }

    /// <summary>
    /// STORY-RADIO-01: first-person handheld radio. Raises the radio into view, keys the PTT (amber LED), plays
    /// original procedural crackle/chirp/squelch, shows each line through the existing flight-board subtitle, then
    /// lowers it. Presentation only: it reads Core (phase, communications, chapter dispatches) and never changes
    /// time, resources or control. Pause freezes it, Enter skips it, reduced motion removes the sway.
    /// Self-binding: the scene needs only this component (installer), no composition change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandheldRadioPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject radioPrefab;
        [SerializeField] private Transform boardingThreshold;
        [SerializeField] private RadioBoardingPlacement boardingPlacement = RadioBoardingPlacement.AuroraArrival;
        [SerializeField] private float boardingRadius = 2.4f;
        [SerializeField] private List<RadioDispatchBinding> dispatchBeats = new List<RadioDispatchBinding>();
        [SerializeField] private List<RadioVoiceClip> voiceClips = new List<RadioVoiceClip>();
        [SerializeField] private Vector3 heldPosition = new Vector3(.125f, -.152f, .31f);
        [SerializeField] private Vector3 heldEuler = new Vector3(-10f, -26f, 8f);
        [SerializeField] private Vector3 hiddenPosition = new Vector3(.2f, -.47f, .24f);
        [SerializeField] private Vector3 hiddenEuler = new Vector3(40f, -32f, 20f);
        [SerializeField] private Vector3 pttPressOffset = new Vector3(.0022f, 0f, 0f);

        /// <summary>Other dialogue must have been quiet this long before a radio beat starts.</summary>
        private const float DialogueSettleSeconds = 1.5f;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly Color LedAmber = new Color(1f, .55f, .08f) * 4.5f;
        private static readonly Color LedCyan = new Color(.15f, .85f, 1f) * 3f;

        private readonly RadioBeatTimeline timeline = new RadioBeatTimeline();
        private readonly HashSet<string> played = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> queue = new Queue<string>();
        private MissionRuntime runtime;
        private MissionCinematicPresenter cinematics;
        private MissionAudioPresenter audioPresenter;
        private MissionFlightBoard board;
        private Camera view;
        private object boundSession;
        private CommunicationState? lastComms;
        private string lastDispatchId;
        private bool sawPlanning, subtitleShown, audioPaused, wasRoutine;
        private string shownText;
        private float reassertAt, swayClock, lastOtherDialogueAt = float.NegativeInfinity;
        private GameObject instance;
        private Transform ptt;
        private Renderer led;
        private Vector3 pttRest;
        private MaterialPropertyBlock block;
        private AudioSource sfx, bed, voice;
        private InteractionResolver resolver;
        private AudioClip chirp, keyUp, squelch, staticBed;
        private RadioPhase lastPhase = RadioPhase.Idle;
        private int lastLine = -1;

        public bool IsPlaying => timeline.IsActive;
        public string ActiveBeatId => timeline.IsActive ? timeline.Beat.Id : null;
        public RadioBeatTimeline Timeline => timeline;
        public IReadOnlyCollection<string> PlayedBeats => played;
        public int QueuedBeats => queue.Count;
        public RadioBoardingPlacement BoardingPlacement { get => boardingPlacement; set => boardingPlacement = value; }
        public GameObject RadioInstance => instance;

        /// <summary>Queue a beat once per session. Returns false for unknown or already played beats.</summary>
        public bool TryPlay(string beatId)
        {
            if (!RadioBeats.TryGet(beatId, out _) || played.Contains(beatId)) return false;
            played.Add(beatId);
            queue.Enqueue(beatId);
            return true;
        }

        /// <summary>Skip the active beat: lines stop, the radio lowers from where it is.</summary>
        public void Skip()
        {
            if (!timeline.IsActive) return;
            timeline.Skip();
            StopVoice();
            ClearSubtitle();
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            sfx = gameObject.AddComponent<AudioSource>();
            bed = gameObject.AddComponent<AudioSource>();
            foreach (AudioSource source in new[] { sfx, bed })
            {
                source.playOnAwake = false; source.spatialBlend = 0f; source.ignoreListenerPause = false;
            }
            bed.loop = true;
            // Recorded voices go through a radio band (300-3400 Hz) with a little grit, on their own source.
            var voiceObject = new GameObject("Radio voice");
            voiceObject.transform.SetParent(transform, false);
            voice = voiceObject.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.spatialBlend = 0f; voice.volume = 1f;
            // A voice take must never lose its channel to ambience or effects, and it pauses with sfx and bed.
            voice.priority = 0; voice.mute = false; voice.ignoreListenerPause = false;
            voiceObject.AddComponent<AudioHighPassFilter>().cutoffFrequency = 300f;
            voiceObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 3400f;
            voiceObject.AddComponent<AudioDistortionFilter>().distortionLevel = .12f;
            chirp = RadioSynth.IncomingChirp(); keyUp = RadioSynth.KeyUp(); squelch = RadioSynth.SquelchTail(); staticBed = RadioSynth.StaticBed();
            bed.clip = staticBed;
        }

        private void OnDisable()
        {
            timeline.Abort();
            StopVoice();
            ClearSubtitle();
            if (instance) instance.SetActive(false);
        }

        private void OnDestroy()
        {
            if (resolver) resolver.Interacted -= OnInteracted;
            if (instance) Destroy(instance);
            foreach (AudioClip clip in new[] { chirp, keyUp, squelch, staticBed }) if (clip) Destroy(clip);
        }

        private void Update()
        {
            Bind();
            if (!runtime || runtime.Session == null || runtime.GameState == null) { Reset(null); return; }
            if (!ReferenceEquals(boundSession, runtime.Session)) Reset(runtime.Session);
            bool paused = runtime.IsPaused;
            if (audioPresenter && audioPresenter.IsDialoguePlaying) lastOtherDialogueAt = Time.unscaledTime;
            // The routine lets days pass: a beat that has not started when it begins missed its moment (no old news
            // days later). Beats queued after this edge, such as the Day-14 call that stops the routine, stay.
            IMissionChapterFlow chapters = runtime.ChapterFlow;
            bool routine = chapters != null && chapters.Enabled && chapters.IsRoutineRunning;
            if (routine && !wasRoutine) queue.Clear();
            wasRoutine = routine;
            DetectTriggers();
            if (!timeline.IsActive) TryStartNext(paused);
            if (!timeline.IsActive) return;
            var player = runtime.Player;
            bool viewTaken = !player || !player.ControlAllowed;
            if (!paused && ((viewTaken && !VoiceOnlyAllowed(timeline.Beat)) || (cinematics && cinematics.IsPlaying)))
            {
                // A cutscene or departure took the view: the radio lowers out of the way, the beat is done.
                if (timeline.Phase != RadioPhase.Lower) Skip();
            }
            else if (!paused && Keyboard.current != null
                && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                Skip();
            if (!paused) swayClock += Time.unscaledDeltaTime;
            timeline.Tick(Time.unscaledDeltaTime, paused);
            ApplyAudio(paused);
            ApplySubtitle();
        }

        private void LateUpdate() => ApplyPose();

        private static bool VoiceOnlyAllowed(RadioBeat beat) => beat != null && beat.Id == RadioBeats.Day14Announcement;

        // Day 14: after the priority call, the next use of the cockpit console plays the voiced announcement once.
        private void OnInteracted(CommandResult result)
        {
            if (!resolver || !played.Contains(RadioBeats.Day14Call) || played.Contains(RadioBeats.Day14Announcement)) return;
            InteractableTarget target = resolver.CurrentTarget;
            if (!target || target.InteractionId != InteractionIds.CockpitConsole) return;
            // The announcement belongs to the day of the message: a console use days later must not play old news.
            MissionDispatch? dispatch = CurrentDispatch();
            if (!dispatch.HasValue || BeatFor(dispatch.Value) != RadioBeats.Day14Call || runtime.GameState == null
                || !RadioBeats.IsAnnouncementFresh(dispatch.Value.ReceivedAtSimulationSeconds, runtime.GameState.ElapsedSimulationSeconds)) return;
            TryPlay(RadioBeats.Day14Announcement);
        }

        private string BeatFor(MissionDispatch dispatch)
        {
            foreach (RadioDispatchBinding binding in dispatchBeats)
                if (binding != null && string.Equals(binding.contentId, dispatch.ContentId, StringComparison.Ordinal)) return binding.beatId;
            return null;
        }

        private void Bind()
        {
            if (!runtime) runtime = Object.FindFirstObjectByType<MissionRuntime>();
            if (!cinematics) cinematics = Object.FindFirstObjectByType<MissionCinematicPresenter>();
            if (!audioPresenter) audioPresenter = Object.FindFirstObjectByType<MissionAudioPresenter>();
            if (!board) board = Object.FindFirstObjectByType<MissionFlightBoard>();
            if (runtime && runtime.Player)
            {
                Camera candidate = runtime.Player.GetComponentInChildren<Camera>(true);
                if (candidate && candidate != view) view = candidate;
            }
            if (!view) view = Camera.main;
            InteractionResolver current = runtime && runtime.Player ? runtime.Player.Interaction : null;
            if (current != resolver)
            {
                if (resolver) resolver.Interacted -= OnInteracted;
                resolver = current;
                if (resolver) resolver.Interacted += OnInteracted;
            }
        }

        // New or ended session: forget everything; edges are measured from the state at binding (no retrigger).
        private void Reset(object session)
        {
            if (ReferenceEquals(boundSession, session) && session == null) return;
            timeline.Abort();
            StopVoice();
            ClearSubtitle();
            queue.Clear();
            played.Clear();
            boundSession = session;
            sawPlanning = false; wasRoutine = false;
            lastComms = session != null ? runtime.Communications?.State : null;
            lastDispatchId = session != null ? CurrentDispatch()?.Id : null;
            if (instance) instance.SetActive(false);
        }

        private MissionDispatch? CurrentDispatch()
        {
            IMissionChapterFlow flow = runtime ? runtime.ChapterFlow : null;
            return flow != null && flow.Enabled ? flow.LatestDispatch : (MissionDispatch?)null;
        }

        private void DetectTriggers()
        {
            GamePhase phase = runtime.GameState.Phase;
            if (phase == GamePhase.Planning)
            {
                sawPlanning = true;
                TryPlay(RadioBeats.PreflightBriefing);   // once per session, as the preflight planning begins
            }
            var player = runtime.Player;
            if (!played.Contains(RadioBeats.AuroraBoarding) && !played.Contains(RadioBeats.PadBoarding) && player && player.ControlAllowed)
            {
                if (boardingPlacement == RadioBoardingPlacement.PadHatch)
                {
                    if (phase == GamePhase.Planning && boardingThreshold && boardingThreshold.gameObject.activeInHierarchy)
                    {
                        Vector3 d = player.transform.position - boardingThreshold.position;
                        if (new Vector2(d.x, d.z).magnitude <= boardingRadius && Mathf.Abs(d.y) < 2.5f) TryPlay(RadioBeats.PadBoarding);
                    }
                }
                else if (sawPlanning && phase != GamePhase.Planning && player.IsFloating) TryPlay(RadioBeats.AuroraBoarding);
            }
            CommunicationState? comms = runtime.Communications?.State;
            if (comms != lastComms)
            {
                if (lastComms.HasValue && comms == CommunicationState.AwaitingReply) TryPlay(RadioBeats.DistressCall);
                if (lastComms.HasValue && comms == CommunicationState.ReplyReceived) TryPlay(RadioBeats.EarthReplyDay14);
                lastComms = comms;
            }
            MissionDispatch? dispatch = CurrentDispatch();
            string id = dispatch?.Id;
            if (!string.Equals(id, lastDispatchId, StringComparison.Ordinal))
            {
                lastDispatchId = id;
                if (id != null)
                    foreach (RadioDispatchBinding binding in dispatchBeats)
                        if (binding != null && string.Equals(binding.contentId, dispatch.Value.ContentId, StringComparison.Ordinal))
                            TryPlay(binding.beatId);
            }
        }

        private void TryStartNext(bool paused)
        {
            if (queue.Count == 0 || paused) return;
            var player = runtime.Player;
            if (!RadioBeats.TryGet(queue.Peek(), out RadioBeat beat)) { queue.Dequeue(); return; }
            if (RadioBeats.IsStale(beat.Id, runtime.GameState.Phase)) { queue.Dequeue(); return; }   // missed its moment
            if (!view || !player || (!player.ControlAllowed && !VoiceOnlyAllowed(beat))) return;
            if (cinematics && cinematics.IsPlaying) return;
            // Chapter cards come in a row with short gaps of control: wait until the pending decision is answered.
            IMissionChapterFlow flow = runtime.ChapterFlow;
            if (flow != null && flow.Enabled && flow.PendingDecision != ChapterDecision.None) return;
            // Never talk over the ship computer or Earth, and do not slip into the short gap between two of their lines.
            if (audioPresenter && (audioPresenter.IsDialoguePlaying || Time.unscaledTime - lastOtherDialogueAt < DialogueSettleSeconds)) return;
            queue.Dequeue();
            timeline.Start(beat.WithVoiceLengths(ClipSeconds), cinematics && cinematics.ReducedEffects);
            lastPhase = RadioPhase.Idle; lastLine = -1; swayClock = 0f;
            EnsureInstance();
        }

        private void EnsureInstance()
        {
            if (instance || !radioPrefab || !view) return;
            instance = Instantiate(radioPrefab, view.transform, false);
            instance.name = "Handheld radio (first person)";
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.lightProbeUsage = LightProbeUsage.Off;
                // Alex: the handheld radio is never shown (it clashed with a carried crate). The beat stays
                // audible and readable: chirp, squelch, static bed, voice takes and subtitles are unchanged.
                r.enabled = false;
            }
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) Destroy(c);
            ptt = FindChild(instance.transform, "PTT");
            Transform ledNode = FindChild(instance.transform, "LED");
            led = ledNode ? ledNode.GetComponent<Renderer>() : null;
            if (ptt) pttRest = ptt.localPosition;
            instance.SetActive(false);
        }

        private AudioClip ClipFor(string cueId)
        {
            if (string.IsNullOrEmpty(cueId)) return null;
            foreach (RadioVoiceClip v in voiceClips) if (v != null && v.clip && v.cueId == cueId) return v.clip;
            return null;
        }

        private float? ClipSeconds(string cueId)
        {
            AudioClip clip = ClipFor(cueId);
            return clip ? clip.length : (float?)null;
        }

        public bool IsVoicePlaying => voice && voice.isPlaying;

        /// <summary>True when a recorded voice is bound for the cue (installer reports it).</summary>
        public bool HasVoice(string cueId) => ClipFor(cueId);

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        private void ApplyPose()
        {
            float w = timeline.IsActive ? timeline.PoseWeight : 0f;
            if (runtime && runtime.Player && !runtime.Player.ControlAllowed && !runtime.IsPaused) w = 0f;   // view taken: voice only
            if (!instance) return;
            if (w <= .001f || !view) { if (instance.activeSelf) instance.SetActive(false); return; }
            if (instance.transform.parent != view.transform) instance.transform.SetParent(view.transform, false);
            if (!instance.activeSelf) instance.SetActive(true);
            float sway = timeline.SwayAmplitude * w;
            Vector3 pos = Vector3.LerpUnclamped(hiddenPosition, heldPosition, w)
                + sway * new Vector3(Mathf.Sin(swayClock * 1.3f) * .004f, Mathf.Sin(swayClock * 1.9f) * .003f, 0f);
            if (timeline.PttPressed) pos += new Vector3(-.012f, .014f, -.022f) * w;   // speaking: closer to the mouth
            instance.transform.localPosition = pos;
            instance.transform.localRotation = Quaternion.Slerp(Quaternion.Euler(hiddenEuler), Quaternion.Euler(heldEuler), w)
                * Quaternion.Euler(sway * Mathf.Sin(swayClock * 1.1f) * 1.2f, 0f, sway * Mathf.Sin(swayClock * .9f) * 1.5f);
            if (ptt) ptt.localPosition = pttRest + (timeline.PttPressed ? pttPressOffset : Vector3.zero);
            if (led)
            {
                Color c = timeline.Led == RadioLed.Transmit ? LedAmber
                    : timeline.Led == RadioLed.Receive ? LedCyan * (.55f + .45f * Mathf.Abs(Mathf.Sin(swayClock * 7f))) : Color.black;
                led.GetPropertyBlock(block);
                block.SetColor(EmissionColor, c);
                led.SetPropertyBlock(block);
            }
        }

        private void ApplyAudio(bool paused)
        {
            if (paused)
            {
                if (!audioPaused) { sfx.Pause(); bed.Pause(); voice.Pause(); audioPaused = true; }
                return;
            }
            if (audioPaused) { sfx.UnPause(); bed.UnPause(); voice.UnPause(); audioPaused = false; }
            RadioPhase phase = timeline.Phase;
            int line = timeline.LineIndex;
            if (phase != lastPhase && phase == RadioPhase.Incoming && chirp) sfx.PlayOneShot(chirp, .8f);
            if (line != lastLine)
            {
                if (lastLine >= 0 && squelch) sfx.PlayOneShot(squelch, .7f);
                if (lastLine >= 0 && voice.isPlaying) voice.Stop();
                if (line >= 0)
                {
                    if (keyUp) sfx.PlayOneShot(keyUp, .75f);
                    AudioClip recorded = ClipFor(timeline.Beat.Lines[line].CueId);
                    if (recorded)
                    {
                        // Never rely on lazy loading in a player: the data is in memory before the delayed start.
                        if (recorded.loadState == AudioDataLoadState.Unloaded) recorded.LoadAudioData();
                        voice.clip = recorded; voice.PlayDelayed(.12f);
                    }
                    bed.volume = timeline.Beat.Lines[line].Kind == RadioLineKind.Transmit ? .05f : .15f;
                    if (!bed.isPlaying) bed.Play();
                }
                else if (bed.isPlaying) bed.Stop();
            }
            if ((phase == RadioPhase.Lower || phase == RadioPhase.Done) && bed.isPlaying) bed.Stop();
            lastPhase = phase; lastLine = line;
        }

        private void ApplySubtitle()
        {
            RadioLine? line = timeline.CurrentLine;
            bool otherDialogue = audioPresenter && audioPresenter.IsDialoguePlaying;
            if (line.HasValue && board && !otherDialogue)
            {
                string text = line.Value.SubtitleAt(timeline.LineSeconds);
                if (!subtitleShown || shownText != text || Time.unscaledTime >= reassertAt)
                {
                    board.ShowSubtitle(text, line.Value.Speaker);
                    subtitleShown = true; shownText = text; reassertAt = Time.unscaledTime + .25f;
                }
            }
            else if (!line.HasValue) ClearSubtitle();
        }

        private void ClearSubtitle()
        {
            if (subtitleShown && board) board.ClearSubtitle();
            subtitleShown = false; shownText = null;
        }

        private void StopVoice()
        {
            if (sfx) sfx.Stop();
            if (bed) bed.Stop();
            if (voice) voice.Stop();
            audioPaused = false;
        }
    }
}
