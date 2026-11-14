using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Presentation.Audio;
using OutOfReach.Presentation.Cinematics;
using OutOfReach.Presentation.Lighting;
using OutOfReach.Presentation.Motion;
using OutOfReach.Presentation.Hands;
using OutOfReach.Presentation.UI;
using OutOfReach.Simulation;
using UnityEngine;

namespace OutOfReach.Composition
{
    /// <summary>Explicit I01 wiring. MissionRuntime alone owns and advances the mission.</summary>
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    public sealed class I01MissionComposition : MonoBehaviour
    {
        [SerializeField] private MissionRuntime runtime;
        [SerializeField] private MissionFlightBoard flightBoard;
        [SerializeField] private MissionCinematicPresenter cinematics;
        [SerializeField] private MissionAudioPresenter audioPresenter;
        [SerializeField] private InteractionResolver interaction;
        [SerializeField] private I01SessionControls controls;
        [SerializeField] private I01AirlockDoorView airlockDoors;
        [SerializeField] private ShipLightingPresenter shipLighting;
        [SerializeField] private FirstPersonMotionPresenter firstPersonMotion;
        [SerializeField] private Transform firstPersonMotionPivot;
        [SerializeField] private AstronautHandsPresenter astronautHands;
        [SerializeField] private Light[] workingLights = Array.Empty<Light>();
        [SerializeField] private Light[] emergencyLights = Array.Empty<Light>();
        [Tooltip("Parent of the cinematic offset. This transform is used only to frame Planning.")]
        [SerializeField] private Transform planningFramingPivot;
        [SerializeField] private Transform planningViewAnchor;
        [SerializeField] private Light powerStatusLight;
        [SerializeField] private Light lifeSupportStatusLight;

        [Header("Optional explicitly approved clean dialogue; no implicit catalog or fallback")]
        [SerializeField] private DialogueCueBinding recordedBriefing;
        [SerializeField] private DialogueCueBinding impactWarning;
        [SerializeField] private DialogueCueBinding powerRestored;
        [SerializeField] private DialogueCueBinding lifeSupportRestored;
        [SerializeField] private DialogueCueBinding antennaRestored;
        [SerializeField] private DialogueCueBinding falseSignal;
        [SerializeField] private DialogueCueBinding transmissionSent;
        [SerializeField] private DialogueCueBinding earthReply;
        [SerializeField] private DialogueCueBinding outerHatchOpen;
        private readonly Queue<KeyValuePair<DialogueCueBinding, string>> dialogueQueue = new Queue<KeyValuePair<DialogueCueBinding, string>>();
        private readonly HashSet<string> requestedDialogue = new HashSet<string>(StringComparer.Ordinal);

        private MissionSession boundSession;
        private IMissionEventBus boundEvents;
        private bool started;
        private bool attached;
        private bool modeChosen;
        private string cinematicCaption = string.Empty;
        private string dialogueSpeaker = "SHIP";
        private string latestPrompt = string.Empty;
        private bool latestPromptAvailable;
        private float rejectionUntil;

        public MissionRuntime Runtime => runtime;
        public MissionCinematicPresenter Cinematics => cinematics;
        public bool ModeChosen => modeChosen;
        public bool InteriorRepairsComplete => Condition(ShipSystemId.Power) >= 1d && Condition(ShipSystemId.LifeSupport) >= 1d;

        public void Configure(MissionRuntime missionRuntime, MissionFlightBoard board,
            MissionCinematicPresenter cinematicPresenter, MissionAudioPresenter missionAudio,
            InteractionResolver resolver, I01SessionControls sessionControls,
            Transform framingPivot, Transform overviewAnchor, Light powerLight, Light lifeSupportLight)
        {
            if (Application.isPlaying && attached)
                throw new InvalidOperationException("Configure composition references before enabling the scene.");
            runtime = missionRuntime;
            flightBoard = board;
            cinematics = cinematicPresenter;
            audioPresenter = missionAudio;
            interaction = resolver;
            controls = sessionControls;
            planningFramingPivot = framingPivot;
            planningViewAnchor = overviewAnchor;
            powerStatusLight = powerLight;
            lifeSupportStatusLight = lifeSupportLight;
        }

        public void ConfigureShipPresentation(I01AirlockDoorView doors, ShipLightingPresenter lighting,
            Light[] working, Light[] emergency)
        {
            airlockDoors = doors;
            shipLighting = lighting;
            workingLights = working;
            emergencyLights = emergency;
        }

        public void ConfigureWalkMotion(FirstPersonMotionPresenter presenter, Transform visualOffset)
        {
            if (Application.isPlaying && attached)
                throw new InvalidOperationException("Configure walk motion before enabling composition.");
            firstPersonMotion = presenter;
            firstPersonMotionPivot = visualOffset;
        }

        // This is queried by the visual presenter in LateUpdate, after camera sequences update.
        // Read the real controller's gate; never replace it or introduce another input/movement authority.
        private bool CanPresentWalkMotion() => isActiveAndEnabled && attached && boundSession != null
            && runtime && runtime.isActiveAndEnabled && ReferenceEquals(boundSession, runtime.Session)
            && runtime.Player && runtime.Player.ControlAllowed && !runtime.Player.IsOnRoute
            && cinematics && !cinematics.IsPlaying;

        public void ConfigureHands(AstronautHandsPresenter presenter)
        {
            if (Application.isPlaying && attached)
                throw new InvalidOperationException("Configure hand presentation before enabling composition.");
            astronautHands = presenter;
        }

        // EVA is allowed here: these gloves depict an actually worn suit, including its exterior phase.
        private bool CanPresentHands() => isActiveAndEnabled && attached && boundSession != null
            && runtime && runtime.isActiveAndEnabled && ReferenceEquals(boundSession, runtime.Session)
            && runtime.Player && runtime.Player.ControlAllowed && cinematics && !cinematics.IsPlaying;

        private void Start()
        {
            started = true;
            Attach();
        }

        private void OnEnable()
        {
            if (started) Attach();
        }

        private void OnDisable() => Detach();

        private void Update()
        {
            if (rejectionUntil > 0f && Time.unscaledTime >= rejectionUntil)
            {
                rejectionUntil = 0f;
                RefreshInteractionPrompt();
            }
            if (!audioPresenter || !runtime || boundSession == null || runtime.IsPaused || audioPresenter.IsDialoguePlaying || dialogueQueue.Count == 0) return;
            var next = dialogueQueue.Peek();
            dialogueSpeaker = next.Value;
            if (audioPresenter.PlayDialogue(next.Key.CleanClip, next.Key.SubtitleText, next.Key.CueId)) dialogueQueue.Dequeue();
        }

        private void Attach()
        {
            if (attached) return;
            if (!runtime || !flightBoard || !cinematics || !audioPresenter || !interaction || !controls)
            {
                Debug.LogError("I01 composition is missing an explicit runtime, presentation or player binding.", this);
                enabled = false;
                return;
            }
            runtime.SessionStarted += OnSessionStarted;
            runtime.SessionEnding += OnSessionEnding;
            runtime.PauseChanged += OnPauseChanged;
            interaction.PromptChanged.AddListener(OnPromptChanged);
            interaction.InteractionAttempted.AddListener(OnInteractionAttempted);
            audioPresenter.SubtitleChanged += OnSubtitleChanged;
            cinematics.CaptionChanged += OnCinematicCaption;
            attached = true;
            controls.Configure(this);
            if (runtime.Session != null) BindSession(runtime.Session);
        }

        private void Detach()
        {
            if (!attached) return;
            attached = false;
            if (runtime)
            {
                runtime.SessionStarted -= OnSessionStarted;
                runtime.SessionEnding -= OnSessionEnding;
                runtime.PauseChanged -= OnPauseChanged;
            }
            if (interaction)
            {
                interaction.PromptChanged.RemoveListener(OnPromptChanged);
                interaction.InteractionAttempted.RemoveListener(OnInteractionAttempted);
            }
            if (audioPresenter) audioPresenter.SubtitleChanged -= OnSubtitleChanged;
            if (cinematics) cinematics.CaptionChanged -= OnCinematicCaption;
            UnbindSession();
            if (controls) controls.ClearBinding();
        }

        private void OnSessionStarted(MissionSession session) => BindSession(session);
        private void OnSessionEnding(MissionSession session)
        {
            if (ReferenceEquals(session, boundSession)) UnbindSession();
        }

        private void BindSession(MissionSession session)
        {
            UnbindSession();
            boundSession = session;
            boundEvents = runtime.Events;
            modeChosen = runtime.GameState.Phase != GamePhase.Planning || runtime.PlanningDeadline.IsEnabled;
            flightBoard.Configure(runtime.Planner, runtime.Resources, runtime.GameState, boundEvents);
            flightBoard.ConfigureTiming(runtime.Clock, runtime.PlanningDeadline);
            flightBoard.ConfigureEva(runtime.EvaStatus);
            flightBoard.ConfigureCo2Scale(SurvivalRules.CarbonDioxideActionLevelPpm, SurvivalRules.CarbonDioxideLossLimitPpm);
            if (runtime.GameState.Phase != GamePhase.Planning)
                flightBoard.ConfigureResourceReference(runtime.Configuration.InitialResources);
            flightBoard.enabled = true;
            cinematics.Configure(boundEvents, runtime.GameState, runtime.Clock);
            cinematics.enabled = true;
            if (astronautHands)
                astronautHands.ConfigureSession(runtime.EvaStatus, runtime.GameState, runtime.Clock, CanPresentHands);
            if (firstPersonMotion && firstPersonMotionPivot && runtime.Player)
            {
                firstPersonMotion.Configure(runtime.Player.GetComponent<CharacterController>(), firstPersonMotionPivot,
                    runtime.GameState, runtime.Clock, runtime.EvaStatus, CanPresentWalkMotion,
                    astronautHands && astronautHands.IsBound ? astronautHands.MotionMount : null);
                firstPersonMotion.SetReducedMotion(cinematics.ReducedEffects);
                firstPersonMotion.SetSuppressed(false);
            }
            boundEvents.PhaseChanged += OnPhaseChanged;
            boundEvents.SystemConditionChanged += OnSystemConditionChanged;
            boundEvents.CinematicRequested += OnCinematicRequested;
            boundEvents.MissionEnded += OnMissionEnded;
            boundEvents.CommunicationChanged += OnCommunicationChanged;
            boundEvents.AirlockStateChanged += OnAirlockStateChanged;
            if (airlockDoors) airlockDoors.Configure(runtime.EvaStatus, runtime.Clock);
            if (shipLighting)
                shipLighting.Configure(boundEvents, runtime.GameState, Repairable(ShipSystemId.Power), workingLights, emergencyLights, runtime.Clock);
            audioPresenter.SetPresentationPaused(runtime.IsPaused);
            ApplyPhasePresentation(runtime.GameState.Phase, false);
            UpdateSystemLights();
            controls.Refresh();
        }

        private void UnbindSession()
        {
            if (firstPersonMotion) firstPersonMotion.ClearBinding();
            if (astronautHands) astronautHands.ClearBinding();
            if (boundEvents != null)
            {
                boundEvents.PhaseChanged -= OnPhaseChanged;
                boundEvents.SystemConditionChanged -= OnSystemConditionChanged;
                boundEvents.CinematicRequested -= OnCinematicRequested;
                boundEvents.MissionEnded -= OnMissionEnded;
                boundEvents.CommunicationChanged -= OnCommunicationChanged;
                boundEvents.AirlockStateChanged -= OnAirlockStateChanged;
            }
            boundEvents = null;
            boundSession = null;
            cinematicCaption = string.Empty;
            latestPrompt = string.Empty;
            latestPromptAvailable = false;
            rejectionUntil = 0f;
            dialogueQueue.Clear();
            requestedDialogue.Clear();
            if (flightBoard)
            {
                flightBoard.ClearInteractionPrompt();
                flightBoard.ClearSubtitle();
                flightBoard.enabled = false;
            }
            // Disabling the presenter restores all offsets and acknowledges a pending request once.
            if (cinematics) cinematics.enabled = false;
            if (audioPresenter) audioPresenter.StopAllPresentation();
            if (airlockDoors) airlockDoors.ClearBinding();
            if (shipLighting) shipLighting.ClearBinding();
            RestorePlayerFraming();
        }

        /// <summary>Arms Core's 60-active-real-second deadline; its own fallback is shown by the board.</summary>
        public void StartTimedPlanning()
        {
            if (!CanChooseMode()) return;
            runtime.BeginTimedPlanning();
            if (!runtime.PlanningDeadline.IsEnabled)
            {
                controls.ShowFeedback("Timed planning needs a valid launch loadout.");
                return;
            }
            modeChosen = true;
            audioPresenter.TryPlaySfx("ui_confirm");
            controls.Refresh();
        }

        /// <summary>Practice is the unarmed Core session. An already-armed deadline is never silently reset.</summary>
        public void StartPractice()
        {
            if (!CanChooseMode()) return;
            if (runtime.PlanningDeadline.IsEnabled)
            {
                controls.ShowFeedback("The deadline is already running. Restart to choose Practice.");
                return;
            }
            modeChosen = true;
            audioPresenter.TryPlaySfx("ui_confirm");
            controls.Refresh();
        }

        private bool CanChooseMode() => boundSession != null && runtime.GameState.Phase == GamePhase.Planning;

        public void Resume() { if (runtime) runtime.SetPaused(false); }
        public void Restart() { if (runtime) runtime.RestartMission(); }
        public void SetReducedEffects(bool reduced)
        {
            if (cinematics) cinematics.ReducedEffects = reduced;
            if (firstPersonMotion) firstPersonMotion.SetReducedMotion(reduced);
        }

        private void OnPauseChanged(bool paused)
        {
            if (audioPresenter) audioPresenter.SetPresentationPaused(paused);
            if (controls) controls.Refresh();
        }

        private void OnPhaseChanged(PhaseChanged message)
        {
            ApplyPhasePresentation(message.Current, true);
            controls.Refresh();
        }

        private void ApplyPhasePresentation(GamePhase phase, bool playTransition)
        {
            if (phase == GamePhase.Planning && planningFramingPivot && planningViewAnchor)
                planningFramingPivot.SetPositionAndRotation(planningViewAnchor.position, planningViewAnchor.rotation);
            else RestorePlayerFraming();

            switch (phase)
            {
                case GamePhase.Planning:
                    audioPresenter.SetMood(MusicMood.Isolation);
                    break;
                case GamePhase.Intro:
                    modeChosen = true;
                    flightBoard.ConfigureResourceReference(runtime.Configuration.InitialResources);
                    audioPresenter.SetMood(MusicMood.Tension);
                    if (playTransition)
                    {
                        audioPresenter.TryPlaySfx("ui_confirm");
                        PlayOptional(recordedBriefing, "MISSION CONTROL · RECORDED");
                    }
                    break;
                case GamePhase.Emergency:
                    audioPresenter.SetMood(MusicMood.Emergency);
                    break;
                case GamePhase.Repair:
                    audioPresenter.SetMood(InteriorRepairsComplete ? MusicMood.Isolation : MusicMood.Tension);
                    break;
                case GamePhase.Eva:
                case GamePhase.Transmission:
                    audioPresenter.SetMood(MusicMood.Tension);
                    break;
                default:
                    audioPresenter.SetMood(MusicMood.Silent);
                    break;
            }
        }

        private void RestorePlayerFraming()
        {
            if (planningFramingPivot) planningFramingPivot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        private void OnCinematicRequested(CinematicRequested request)
        {
            if (request.Cinematic == CinematicId.FalseSignal) { PlayOptional(falseSignal, "SHIP · BUFFER DIAGNOSTIC", true); return; }
            if (request.Cinematic == CinematicId.CommunicationRestored) { PlayOptional(earthReply, "MISSION CONTROL · DELAYED REPLY", true); return; }
            if (request.Cinematic != CinematicId.Impact) return;
            audioPresenter.SetMood(MusicMood.Emergency, .3f);
            audioPresenter.TryPlaySfx("power_down");
            PlayOptional(impactWarning, "SHIP", true);
        }

        private void OnSystemConditionChanged(SystemConditionChanged change)
        {
            UpdateSystemLights();
            if (change.Condition01 >= 1d)
            {
                if (change.SystemId == ShipSystemId.Power) PlayOptional(powerRestored, "SHIP");
                if (change.SystemId == ShipSystemId.LifeSupport) PlayOptional(lifeSupportRestored, "SHIP");
                if (change.SystemId == ShipSystemId.Antenna) PlayOptional(antennaRestored, "SHIP");
            }
            if (InteriorRepairsComplete && runtime.GameState.Phase == GamePhase.Repair)
            {
                audioPresenter.SetMood(MusicMood.Isolation);
            }
            controls.Refresh();
        }

        private double Condition(ShipSystemId id)
        {
            return Repairable(id)?.Condition01 ?? 0;
        }

        private IRepairable Repairable(ShipSystemId id)
        {
            if (!runtime || boundSession == null || runtime.Repairables == null) return null;
            foreach (IRepairable repairable in runtime.Repairables)
                if (repairable.SystemId == id) return repairable;
            return null;
        }

        private void OnCommunicationChanged(CommunicationChanged change)
        {
            if (change.State == CommunicationState.AwaitingReply) PlayOptional(transmissionSent, "SHIP");
        }

        private void OnAirlockStateChanged(AirlockStateChanged change)
        {
            if (change.State == AirlockState.OuterOpen) PlayOptional(outerHatchOpen, "SHIP");
        }

        private void UpdateSystemLights()
        {
            SetStatusLight(powerStatusLight, Condition(ShipSystemId.Power));
            SetStatusLight(lifeSupportStatusLight, Condition(ShipSystemId.LifeSupport));
        }

        private static void SetStatusLight(Light lamp, double condition)
        {
            if (!lamp) return;
            lamp.color = condition >= 1d ? new Color(.3f, .75f, .86f) : new Color(.9f, .12f, .045f);
            lamp.intensity = condition >= 1d ? .28f : .42f;
        }

        private void OnMissionEnded(MissionEnded ended)
        {
            audioPresenter.SetMood(MusicMood.Silent);
            rejectionUntil = 0f;
            latestPrompt = string.Empty;
            flightBoard.ClearInteractionPrompt();
            controls.Refresh();
        }

        private void OnPromptChanged(string prompt, bool available)
        {
            latestPrompt = prompt ?? string.Empty;
            latestPromptAvailable = available;
            rejectionUntil = 0f;
            RefreshInteractionPrompt();
        }

        private void RefreshInteractionPrompt()
        {
            if (!flightBoard) return;
            if (string.IsNullOrWhiteSpace(latestPrompt)) flightBoard.ClearInteractionPrompt();
            else flightBoard.ShowInteractionPrompt(latestPrompt, latestPromptAvailable);
        }

        private void OnInteractionAttempted(bool succeeded, string explanation)
        {
            // An empty-space E press has no useful instruction. Keep actual target rejections visible.
            if (!succeeded && (!interaction || !interaction.CurrentTarget)) return;
            if (succeeded)
            {
                rejectionUntil = 0f;
                RefreshInteractionPrompt();
                audioPresenter.TryPlaySfx("ui_confirm");
            }
            else if (!string.IsNullOrWhiteSpace(explanation))
            {
                flightBoard.ShowInteractionPrompt(explanation, false);
                rejectionUntil = Time.unscaledTime + 4f;
            }
        }

        private void PlayOptional(DialogueCueBinding cue, string speaker, bool interrupt = false)
        {
            if (cue == null || !cue.CleanClip || string.IsNullOrWhiteSpace(cue.CueId)) return;
            if (!requestedDialogue.Add(cue.CueId)) return;
            if (interrupt)
            {
                dialogueQueue.Clear();
                audioPresenter.StopDialogue();
            }
            dialogueQueue.Enqueue(new KeyValuePair<DialogueCueBinding, string>(cue, speaker));
        }

        private void OnSubtitleChanged(DialogueSubtitle subtitle) => RefreshSubtitle();
        private void OnCinematicCaption(string caption)
        {
            cinematicCaption = caption ?? string.Empty;
            RefreshSubtitle();
        }

        private void RefreshSubtitle()
        {
            if (!flightBoard) return;
            if (audioPresenter && audioPresenter.CurrentSubtitle.IsVisible)
                flightBoard.ShowSubtitle(audioPresenter.CurrentSubtitle.Text, dialogueSpeaker);
            else if (!string.IsNullOrEmpty(cinematicCaption)) flightBoard.ShowSubtitle(cinematicCaption, "SHIP STATUS");
            else flightBoard.ClearSubtitle();
        }
    }
}
