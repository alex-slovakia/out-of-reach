using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Player;
using OutOfReach.Presentation.Cargo;
using OutOfReach.Presentation.AirlockPanel;
using OutOfReach.Presentation.Audio;
using OutOfReach.Presentation.Cinematics;
using OutOfReach.Presentation.Cinematics.Preflight;
using OutOfReach.Presentation.Lighting;
using OutOfReach.Presentation.Motion;
using OutOfReach.Presentation.Hands;
using OutOfReach.Presentation.Terminal;
using OutOfReach.Presentation.UI;
using OutOfReach.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

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
        [SerializeField] private FootstepAudioPresenter footsteps;
        [SerializeField] private PreflightTensionPresenter preflightTension;
        private MusicMood preflightPreviousMood;
        private bool preflightMusicOwned;
        [SerializeField] private InteractionResolver interaction;
        [SerializeField] private I01SessionControls controls;
        [SerializeField] private I01AirlockDoorView airlockDoors;
        [SerializeField] private ShipLightingPresenter shipLighting;
        [SerializeField] private FirstPersonMotionPresenter firstPersonMotion;
        [SerializeField] private EmbodiedBodyPresenter embodiedBody;
        [SerializeField] private I06MicrogravityBridge microgravityBridge;
        [SerializeField] private I06MicrogravityInputBindings microgravityInput;
        [SerializeField] private BareArmContactPresenter bareArmContacts;
        [SerializeField] private I06InteriorHandholds interiorHandholds;
        [SerializeField] private I06AntennaRepairBindings antennaRepair;
        [SerializeField] private I06EarthTerminalBindings earthTerminal;
        [SerializeField] private Transform antennaInteractionOrigin;
        [SerializeField] private AntennaWorkProgressPresenter antennaProgress;
        [SerializeField] private UIDocument antennaProgressDocument;
        [SerializeField] private Font antennaProgressFont;
        [SerializeField] private EvaWorkLampPresenter evaWorkLamp;
        private const float MicrogravityHelpSeconds = 20f;
        private const string MicrogravityHelp = "MICROGRAVITY\nAim at a nearby wall, floor or ceiling.\nLeft / right click · Push with that hand\nBoth buttons · Push with both hands\nSpace · Push off the floor with your feet\nHold WASD while pushing to steer.";
        private const string RailGripHelp = "Ctrl + click · Grip this rail";
        private float microgravityHelpRemaining;
        private float nextRailHintProbe;
        private bool railHintVisible;
        private bool orbitalLocationReached;
        private MissionSession transferredSession;
        [SerializeField] private I06HabitatBindings habitat;
        [SerializeField] private I06MarineBindings marine;
        [SerializeField] private Transform firstPersonMotionPivot;
        [SerializeField] private OpeningMenuMotionPresenter openingMenuMotion;
        [SerializeField] private Transform openingMenuMotionPivot;
        [SerializeField] private I01PauseBackdrop pauseBackdrop;
        [SerializeField] private Camera presentationCamera;
        [SerializeField] private AirlockStatusPanel airlockPanel;
        [SerializeField] private AudioClip airlockMotor;
        [SerializeField] private AudioClip airlockReady;
        [SerializeField] private AstronautHandsPresenter astronautHands;
        [SerializeField] private CargoVisualPresenter cargoPresentation;
        [SerializeField] private PreflightCargoHandsPresenter cargoHands;
        [SerializeField] private SurvivalChapterPresenter survivalUi;
        [SerializeField] private UIDocument survivalDocument;
        [SerializeField] private StyleSheet survivalStyle;
        private bool menuPresentationVisible;
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
        [SerializeField] private DialogueCueBinding houstonDistress;
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
        private FirstPersonController planningControlPlayer;
        private Func<bool> domainControlGate;
        private Func<bool> composedControlGate;
        private string cinematicCaption = string.Empty;
        private string dialogueSpeaker = "SHIP COMPUTER";
        private string latestPrompt = string.Empty;
        private bool latestPromptAvailable;
        private float rejectionUntil;

        public MissionRuntime Runtime => runtime;
        public MissionCinematicPresenter Cinematics => cinematics;
        public bool ModeChosen => modeChosen;
        public bool IsTransportDeparturePlaying => marine && marine.OwnsPresentation;
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

        public void ConfigureFootsteps(FootstepAudioPresenter presenter) => footsteps = presenter;

        public void ConfigureBody(EmbodiedBodyPresenter presenter) => embodiedBody = presenter;
        public void ConfigureEvaWorkLamp(EvaWorkLampPresenter presenter) => evaWorkLamp = presenter;
        public void ConfigureEarthTerminal(I06EarthTerminalBindings value)
        {
            if (Application.isPlaying && attached) throw new InvalidOperationException("Configure Earth terminal before binding.");
            earthTerminal = value;
        }
        private bool EarthViewerOwnsInput => earthTerminal && earthTerminal.BlocksPlayerInput;
        private bool CanPresentEarthViewer() => MicroSessionCurrent() && modeChosen && orbitalLocationReached
            && !menuPresentationVisible && cinematics && !cinematics.IsPlaying
            && !IsTransportDeparturePlaying && !(antennaRepair && antennaRepair.BlocksMovement);
        private void PrepareEarthViewer()
        {
            if (microgravityBridge) microgravityBridge.Release();
            if (embodiedBody) embodiedBody.RefreshPresentation();
        }
        private TerminalReachFrame ReadActualTerminalReach()
        {
            if (!embodiedBody || !embodiedBody.IsVisible || !runtime || !runtime.Player) return default;
            Transform rightShoulder = embodiedBody.GetVisibleBone("upperarm01.R");
            Transform leftShoulder = embodiedBody.GetVisibleBone("upperarm01.L");
            Transform rightElbow = embodiedBody.GetVisibleBone("lowerarm01.R");
            Transform rightWrist = embodiedBody.GetVisibleBone("wrist.R");
            if (!rightShoulder || !leftShoulder || !rightElbow || !rightWrist) return default;
            return new TerminalReachFrame(rightShoulder.position, leftShoulder.position,
                rightElbow.position, rightWrist.position, runtime.Player.transform.forward);
        }
        public void ConfigureAntennaRepair(I06AntennaRepairBindings bindings, Transform interactionOrigin)
        {
            if (Application.isPlaying && attached) throw new InvalidOperationException("Configure antenna work before binding.");
            antennaRepair = bindings;
            antennaInteractionOrigin = interactionOrigin;
        }
        public void ConfigureAntennaProgress(AntennaWorkProgressPresenter presenter, UIDocument document, Font font)
        {
            if (Application.isPlaying && attached) throw new InvalidOperationException("Configure antenna feedback before binding.");
            antennaProgress = presenter;
            antennaProgressDocument = document;
            antennaProgressFont = font;
        }

        // The tool owns its movement lock. A legitimate pause freezes the same work rather than invalidating it.
        private bool CanPresentAntennaWork() => MicroSessionCurrent() && modeChosen
            && runtime.Player && runtime.Player.isActiveAndEnabled
            && runtime.GameState.Phase == GamePhase.Eva && runtime.GameState.Outcome == MissionOutcome.None
            && runtime.EvaStatus.IsOutside && runtime.EvaStatus.IsSuitOn
            && cinematics && !cinematics.IsPlaying && !IsTransportDeparturePlaying
            && !(boundSession.SurvivalRecovery?.IsResting ?? false)
            && (!menuPresentationVisible || runtime.IsPaused);

        public void ConfigureMicrogravity(I06MicrogravityBridge bridge, I06MicrogravityInputBindings input,
            BareArmContactPresenter bareArms, I06InteriorHandholds handholds)
        {
            if (Application.isPlaying && attached) throw new InvalidOperationException("Configure microgravity before binding.");
            microgravityBridge = bridge;
            microgravityInput = input;
            bareArmContacts = bareArms;
            interiorHandholds = handholds;
        }

        private bool MicroSessionCurrent() => isActiveAndEnabled && attached && boundSession != null
            && runtime && runtime.isActiveAndEnabled && ReferenceEquals(boundSession, runtime.Session);

        private bool CanUseMicroContacts() => !EarthViewerOwnsInput && MicroSessionCurrent() && modeChosen && orbitalLocationReached
            && runtime.Player && runtime.Player.ControlAllowed && !runtime.IsPaused && !menuPresentationVisible
            && !runtime.Player.IsOnRoute && !runtime.EvaStatus.IsOutside
            && cinematics && !cinematics.IsPlaying && !IsTransportDeparturePlaying
            && !(boundSession.SurvivalRecovery?.IsResting ?? false)
            && string.IsNullOrEmpty(runtime.Cargo?.CarriedEquipmentId)
            && runtime.GameState.Outcome == MissionOutcome.None
            && runtime.GameState.Phase != GamePhase.Planning;

        private void UpdateMicrogravityHelp()
        {
            if (!flightBoard) return;
            if (!microgravityInput || !microgravityInput.HasInputLease || !CanUseMicroContacts())
            {
                railHintVisible = false;
                nextRailHintProbe = 0f;
                flightBoard.SetMicrogravityHelp(null);
                return;
            }
            if (Time.unscaledTime >= nextRailHintProbe)
            {
                railHintVisible = microgravityInput.AimAtReachableRail();
                nextRailHintProbe = Time.unscaledTime + .10f;
            }
            if (microgravityHelpRemaining > 0f)
            {
                flightBoard.SetMicrogravityHelp(railHintVisible ? MicrogravityHelp + "\n" + RailGripHelp : MicrogravityHelp);
                microgravityHelpRemaining -= Time.unscaledDeltaTime;
            }
            else flightBoard.SetMicrogravityHelp(railHintVisible ? RailGripHelp : null);
        }
        public bool HasInteriorRailGrip => microgravityInput && microgravityInput.HasRailGrip;

        private void MaintainMicrogravity()
        {
            if (!microgravityInput || !microgravityBridge || !microgravityBridge.IsBound || !MicroSessionCurrent()) return;
            // The real EVA route owns its exterior motion. Pause/menu keeps the input lease
            // so a resume click cannot leak into the resolver's default LMB binding.
            if (runtime.EvaStatus.IsOutside || runtime.Player.IsOnRoute
                || runtime.GameState.Outcome != MissionOutcome.None)
            {
                microgravityInput.Disarm();
                return;
            }
            if (!CanUseMicroContacts()) return;
            bool enteredHere = false;
            if (!runtime.Player.IsFloating)
            {
                // No air entry or fabricated support. This also handles the real return
                // from EVA, whose existing route places the feet back inside the airlock.
                if (runtime.Player.EnterFloatFromFootSupport() != FloatActionResult.Done) return;
                enteredHere = true;
            }
            if (!microgravityInput.ContactRoutingRequested && !microgravityInput.Arm() && enteredHere)
                runtime.Player.ExitFloat(); // Roll back only our new zero-motion entry; never a prior owner's flight.
        }
        public void ConfigureHabitat(I06HabitatBindings bindings) => habitat = bindings;
        public void ConfigureMarine(I06MarineBindings bindings) => marine = bindings;

        // A pause keeps the body in the actual scene behind the menu. Contact commands
        // use the stricter player-control gate; visibility alone never grants input.
        private bool CanPresentBody() => !EarthViewerOwnsInput && isActiveAndEnabled && attached && modeChosen && boundSession != null
            && runtime && runtime.isActiveAndEnabled && ReferenceEquals(boundSession, runtime.Session)
            && runtime.Player && cinematics && !cinematics.IsPlaying && !IsTransportDeparturePlaying
            && !(boundSession.SurvivalRecovery?.IsResting ?? false)
            && runtime.GameState.Phase != GamePhase.Outcome && runtime.GameState.Phase != GamePhase.Report;

        private EmbodiedBodyPresenter.BodyArmVisibility BodyArmMask() =>
            (cargoHands && cargoHands.IsVisible) || (astronautHands && astronautHands.IsVisible)
                ? EmbodiedBodyPresenter.BodyArmVisibility.None : EmbodiedBodyPresenter.BodyArmVisibility.Both;

        private bool CanPresentGroundedStepAudio() => !orbitalLocationReached && CanPresentWalkMotion() && modeChosen
            && !menuPresentationVisible && !(boundSession.SurvivalRecovery?.IsResting ?? false);

        // This is queried by the visual presenter in LateUpdate, after camera sequences update.
        // Read the real controller's gate; never replace it or introduce another input/movement authority.
        private bool CanPresentWalkMotion() => !EarthViewerOwnsInput && isActiveAndEnabled && attached && boundSession != null
            && runtime && runtime.isActiveAndEnabled && ReferenceEquals(boundSession, runtime.Session)
            && runtime.Player && runtime.Player.ControlAllowed && !runtime.Player.IsOnRoute && !runtime.Player.IsFloating
            && cinematics && !cinematics.IsPlaying;

        public void ConfigureOpeningMenuMotion(OpeningMenuMotionPresenter presenter, Transform visualOffset)
        {
            if (Application.isPlaying && attached)
                throw new InvalidOperationException("Configure opening menu motion before enabling composition.");
            openingMenuMotion = presenter;
            openingMenuMotionPivot = visualOffset;
        }

        private bool CanPresentOpeningMenuMotion() => !EarthViewerOwnsInput && isActiveAndEnabled && attached && boundSession != null
            && !IsTransportDeparturePlaying
            && !(antennaRepair && antennaRepair.IsWorking)
            && runtime && runtime.isActiveAndEnabled && ReferenceEquals(boundSession, runtime.Session)
            && runtime.Player && runtime.Player.isActiveAndEnabled && !runtime.Player.ControlAllowed
            && cinematics && !cinematics.IsPlaying;

        public void ConfigureHands(AstronautHandsPresenter presenter)
        {
            if (Application.isPlaying && attached)
                throw new InvalidOperationException("Configure hand presentation before enabling composition.");
            astronautHands = presenter;
        }

        public void ConfigurePauseBackdrop(I01PauseBackdrop backdrop, Camera camera)
        {
            pauseBackdrop = backdrop;
            presentationCamera = camera;
        }

        public void ConfigureAirlockPanel(AirlockStatusPanel panel, AudioClip motor, AudioClip ready)
        {
            airlockPanel = panel;
            airlockMotor = motor;
            airlockReady = ready;
        }

        public void ConfigurePreflightTension(PreflightTensionPresenter presenter, AudioClip existingTick)
        {
            if (Application.isPlaying && attached)
                throw new InvalidOperationException("Configure preflight tension before enabling composition.");
            preflightTension = presenter;
            if (preflightTension) preflightTension.ConfigureCue(existingTick);
        }

        private bool CanPresentTimedPreflight() => isActiveAndEnabled && attached && boundSession != null
            && runtime && runtime.isActiveAndEnabled && ReferenceEquals(boundSession, runtime.Session)
            && runtime.PreflightWalk && modeChosen && runtime.GameState.Phase == GamePhase.Planning;

        private bool RequestPreflightPulse()
        {
            if (!CanPresentTimedPreflight() || !audioPresenter || audioPresenter.IsPresentationPaused) return false;
            MusicMood previous = audioPresenter.CurrentMood;
            if (!audioPresenter.SetMood(MusicMood.Tension, 3f)) return false;
            preflightPreviousMood = previous;
            preflightMusicOwned = previous != MusicMood.Tension;
            return true;
        }

        private void ReleasePreflightPulse()
        {
            bool restore = preflightMusicOwned;
            preflightMusicOwned = false;
            // A phase transition owns its next mood. Never restore Planning music over Intro/Emergency.
            if (restore && audioPresenter && runtime && runtime.GameState != null
                && runtime.GameState.Phase == GamePhase.Planning && audioPresenter.CurrentMood == MusicMood.Tension)
                audioPresenter.SetMood(preflightPreviousMood, 1.25f);
        }

        // EVA is allowed here: these gloves depict an actually worn suit, including its exterior phase.
        private bool CanPresentHands() => !EarthViewerOwnsInput && isActiveAndEnabled && attached && boundSession != null
            && runtime && runtime.isActiveAndEnabled && ReferenceEquals(boundSession, runtime.Session)
            && runtime.Player && (runtime.Player.ControlAllowed
                || (antennaRepair && antennaRepair.BlocksMovement && CanPresentAntennaWork()))
            && cinematics && !cinematics.IsPlaying;

        public void ConfigureCargoPresentation(CargoVisualPresenter presenter)
        {
            if (Application.isPlaying && attached)
                throw new InvalidOperationException("Configure cargo presentation before enabling composition.");
            cargoPresentation = presenter;
        }

        public void ConfigureCargoHands(PreflightCargoHandsPresenter presenter) => cargoHands = presenter;

        public void ConfigureSurvivalUi(SurvivalChapterPresenter presenter, UIDocument document, StyleSheet style)
        {
            survivalUi = presenter;
            survivalDocument = document;
            survivalStyle = style;
        }

        private bool CanPresentSurvivalUi() => !EarthViewerOwnsInput && isActiveAndEnabled && attached && boundSession != null
            && runtime && runtime.isActiveAndEnabled && ReferenceEquals(boundSession, runtime.Session)
            && !menuPresentationVisible && cinematics && !cinematics.IsPlaying;

        public void SetMenuPresentationVisible(bool visible)
        {
            menuPresentationVisible = visible;
            if (cargoPresentation) cargoPresentation.SetMenuOverlayVisible(visible);
            if (survivalUi) survivalUi.RefreshPresentation();
        }

        public void PlayMenuCue(bool confirm = false)
        {
            if (audioPresenter) audioPresenter.TryPlayMenuCue(confirm);
        }

        // Add planning and physical-rest presentation locks to Core's existing gate. No parallel simulation.
        private void BindPlanningControlGate()
        {
            if (!runtime.Player) return;
            planningControlPlayer = runtime.Player;
            domainControlGate = planningControlPlayer.ControlGate;
            composedControlGate = CanUsePlanningControls;
            planningControlPlayer.ControlGate = composedControlGate;
        }

        private bool CanUsePlanningControls() => !EarthViewerOwnsInput && isActiveAndEnabled && attached && boundSession != null
            && runtime && runtime.isActiveAndEnabled && ReferenceEquals(boundSession, runtime.Session)
            && !IsTransportDeparturePlaying
            && !(antennaRepair && antennaRepair.BlocksMovement)
            && domainControlGate != null && domainControlGate()
            && !(boundSession.SurvivalRecovery?.IsResting ?? false)
            && (boundSession.State.Phase != GamePhase.Planning || modeChosen);

        private void ReleasePlanningControlGate()
        {
            // A new Core session may already have installed its own gate before SessionStarted fires.
            // Restore only a delegate this composition still owns; never overwrite that new binding.
            if (planningControlPlayer && ReferenceEquals(planningControlPlayer.ControlGate, composedControlGate))
                planningControlPlayer.ControlGate = domainControlGate;
            planningControlPlayer = null;
            domainControlGate = null;
            composedControlGate = null;
        }

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
            MaintainMicrogravity();
            UpdateMicrogravityHelp();
            if (astronautHands && CanPresentHands() && runtime && !runtime.IsPaused
                && !(microgravityInput && microgravityInput.ContactRoutingRequested)
                && string.IsNullOrEmpty(runtime.Cargo?.CarriedEquipmentId))
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame) astronautHands.TryPlayPress(true);
                else if (mouse != null && mouse.rightButton.wasPressedThisFrame) astronautHands.TryPlayPress(false);
            }
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

        private void OnSessionStarted(MissionSession session)
        {
            // A new mission resets exertion; re-enabling this presenter must not refill it.
            if (runtime && runtime.Player) runtime.Player.ResetExertionForNewSession();
            BindSession(session);
        }
        private void OnSessionEnding(MissionSession session)
        {
            if (ReferenceEquals(session, boundSession)) UnbindSession();
        }

        private void BindSession(MissionSession session)
        {
            UnbindSession();
            boundSession = session;
            boundEvents = runtime.Events;
            microgravityHelpRemaining = MicrogravityHelpSeconds;
            orbitalLocationReached = ReferenceEquals(session, transferredSession)
                || (!marine && runtime.GameState.Phase != GamePhase.Planning);
            if (!ReferenceEquals(session, transferredSession)) transferredSession = null;
            modeChosen = runtime.GameState.Phase != GamePhase.Planning || runtime.PlanningDeadline.IsEnabled;
            BindPlanningControlGate();
            if (earthTerminal)
            {
                earthTerminal.ConfigureSession(runtime, CanPresentEarthViewer, PrepareEarthViewer);
                earthTerminal.Terminal.ConfigureReach(ReadActualTerminalReach);
            }
            flightBoard.Configure(runtime.Planner, runtime.Resources, runtime.GameState, boundEvents,
                () => session.Systems.Power.IsOperational && session.Systems.LifeSupport.IsOperational);
            flightBoard.SetDeparturePresentation(false);
            flightBoard.SetPhysicalTerminalPresentation(false);
            flightBoard.ConfigureTiming(runtime.Clock, runtime.PlanningDeadline);
            flightBoard.ConfigureExertion(() => runtime && runtime.Player ? runtime.Player.ExertionRemaining01 : float.NaN);
            flightBoard.ConfigurePhysicalPlanning(runtime.PreflightWalk, runtime.Cargo);
            flightBoard.ConfigurePlanningInstruction(marine ? (Func<string>)(() => runtime.AwaitingBoarding
                ? "Loading closed. Board and secure your seat."
                : "Stow your supplies aboard the transport") : null);
            if (marine)
            {
                marine.CaptionChanged += OnCinematicCaption;
                marine.CueRaised += OnDepartureCue;
                marine.Transferred += OnTransportTransferred;
                marine.Bind(runtime, cinematics.ReducedEffects);
            }
            if (habitat) habitat.ConfigureSession(runtime);
            if (cargoPresentation)
                cargoPresentation.Configure(runtime.Cargo, runtime.Planner, runtime.GameState);
            if (cargoHands)
            {
                cargoHands.SetReducedMotion(cinematics.ReducedEffects);
                cargoHands.Configure(runtime.Cargo, runtime.GameState, runtime.Clock, CanPresentHands);
            }
            flightBoard.ConfigureEva(runtime.EvaStatus);
            if (survivalUi)
                survivalUi.Configure(survivalDocument, survivalStyle, runtime.Survival, runtime.SurvivalRecovery,
                    runtime.GameState, runtime.Clock, CanPresentSurvivalUi,
                    () => session.SurvivalRecovery?.BeaconPoweredAtSimulationSeconds == null
                        ? session.SurvivalRecovery?.ContactAtSimulationSeconds
                            + SurvivalRecovery.BeaconRequestAfterContactDays * MissionCalendar.SecondsPerDay
                        : null);
            flightBoard.ConfigureCo2Scale(SurvivalRules.CarbonDioxideActionLevelPpm, SurvivalRules.CarbonDioxideLossLimitPpm);
            if (runtime.GameState.Phase != GamePhase.Planning)
                flightBoard.ConfigureResourceReference(runtime.Configuration.InitialResources);
            flightBoard.enabled = true;
            cinematics.Configure(boundEvents, runtime.GameState, runtime.Clock);
            cinematics.enabled = true;
            if (footsteps && runtime.Player)
                footsteps.Configure(runtime.Player.GetComponent<CharacterController>(), runtime.GameState,
                    runtime.Clock, runtime.EvaStatus, CanPresentGroundedStepAudio,
                    (clip, gain) => audioPresenter && audioPresenter.PlaySfx(clip, gain));
            if (astronautHands)
            {
                astronautHands.ConfigureSession(runtime.EvaStatus, runtime.GameState, runtime.Clock, CanPresentHands);
                astronautHands.ConfigureRouteProgress(() => runtime && runtime.Player && runtime.Player.IsOnRoute
                    ? runtime.Player.RouteDistanceMetres : float.NaN);
            }
            if (embodiedBody)
            {
                embodiedBody.ConfigureSession(runtime.EvaStatus, runtime.Clock, CanPresentBody,
                    () => runtime && runtime.Player && runtime.Player.IsFloating, BodyArmMask);
                embodiedBody.ConfigureGroundLocomotion(runtime.Player.GetComponent<CharacterController>(),
                    CanPresentGroundedStepAudio, () => runtime && runtime.Player && runtime.Player.IsSprinting);
            }
            if (microgravityBridge && microgravityInput && bareArmContacts)
            {
                if (!interiorHandholds || !interiorHandholds.Bind(runtime.Player))
                    throw new InvalidOperationException("The interior contact rails could not bind to the current Player.");
                microgravityBridge.Configure(runtime.Player, embodiedBody, astronautHands, bareArmContacts,
                    runtime.EvaStatus, runtime.GameState, runtime.Clock, CanUseMicroContacts);
                microgravityInput.Configure(runtime.Player, microgravityBridge, runtime.Player.ViewPivot,
                    runtime.EvaStatus, runtime.GameState, runtime.Clock, MicroSessionCurrent, CanUseMicroContacts);
            }
            if (antennaRepair && !antennaRepair.ConfigureSession(runtime, astronautHands,
                antennaInteractionOrigin, runtime.Player.transform, CanPresentAntennaWork))
                throw new InvalidOperationException("The antenna worksite could not bind to the current session.");
            if (antennaProgress && antennaRepair)
                antennaProgress.Configure(antennaProgressDocument, () => antennaRepair.IsWorking,
                    () => antennaRepair.Progress01, () => CanPresentAntennaWork() && !menuPresentationVisible,
                    antennaProgressFont, () => runtime.IsPaused);
            if (evaWorkLamp)
                evaWorkLamp.ConfigureSession(runtime.EvaStatus, runtime.GameState, runtime.Clock, CanPresentAntennaWork);
            if (firstPersonMotion && firstPersonMotionPivot && runtime.Player)
            {
                firstPersonMotion.Configure(runtime.Player.GetComponent<CharacterController>(), firstPersonMotionPivot,
                    runtime.GameState, runtime.Clock, runtime.EvaStatus, CanPresentWalkMotion,
                    astronautHands && astronautHands.IsBound ? astronautHands.MotionMount : null);
                firstPersonMotion.SetReducedMotion(cinematics.ReducedEffects);
                firstPersonMotion.SetSuppressed(false);
            }
            if (openingMenuMotion && openingMenuMotionPivot)
            {
                openingMenuMotion.Configure(openingMenuMotionPivot, runtime.GameState, runtime.Clock, CanPresentOpeningMenuMotion);
                openingMenuMotion.SetReducedMotion(cinematics.ReducedEffects);
            }
            boundEvents.PhaseChanged += OnPhaseChanged;
            if (pauseBackdrop) pauseBackdrop.Configure(presentationCamera, runtime.Clock, runtime.GameState);
            boundEvents.SystemConditionChanged += OnSystemConditionChanged;
            boundEvents.CinematicRequested += OnCinematicRequested;
            boundEvents.MissionEnded += OnMissionEnded;
            boundEvents.CommunicationChanged += OnCommunicationChanged;
            boundEvents.AirlockStateChanged += OnAirlockStateChanged;
            if (airlockDoors) airlockDoors.Configure(runtime.EvaStatus, runtime.Clock);
            if (airlockPanel) airlockPanel.Configure(runtime.EvaStatus, runtime.Clock);
            if (shipLighting)
                shipLighting.Configure(boundEvents, runtime.GameState, Repairable(ShipSystemId.Power), workingLights, emergencyLights, runtime.Clock);
            audioPresenter.SetPresentationPaused(runtime.IsPaused);
            ApplyPhasePresentation(runtime.GameState.Phase, false);
            if (preflightTension)
            {
                preflightTension.SetReducedEffects(cinematics.ReducedEffects);
                preflightTension.Configure(runtime.PlanningDeadline, runtime.GameState, runtime.Clock,
                    CanPresentTimedPreflight, (clip, gain) => audioPresenter && audioPresenter.PlaySfx(clip, gain),
                    RequestPreflightPulse, ReleasePreflightPulse);
                preflightTension.RefreshPresentation();
            }
            UpdateSystemLights();
            controls.Refresh();
            MaintainMicrogravity(); // Rebind a previously transferred session before its next input update.
        }

        private void UnbindSession()
        {
            if (earthTerminal) earthTerminal.ClearBinding();
            if (evaWorkLamp) evaWorkLamp.ClearBinding();
            if (antennaProgress) antennaProgress.ClearBinding();
            if (antennaRepair) antennaRepair.ClearBinding();
            if (microgravityInput) microgravityInput.ClearBinding();
            if (microgravityBridge) microgravityBridge.Clear();
            if (interiorHandholds) interiorHandholds.Unbind();
            orbitalLocationReached = false;
            if (flightBoard) flightBoard.SetMicrogravityHelp(null);
            if (marine)
            {
                marine.Unbind();
                marine.CaptionChanged -= OnCinematicCaption;
                marine.CueRaised -= OnDepartureCue;
                marine.Transferred -= OnTransportTransferred;
            }
            if (habitat) habitat.ClearBinding();
            if (embodiedBody) embodiedBody.ClearBinding();
            if (footsteps) footsteps.ClearBinding();
            if (survivalUi) survivalUi.ClearBinding();
            menuPresentationVisible = false;
            if (cargoHands) cargoHands.ClearBinding();
            if (preflightTension) preflightTension.ClearBinding();
            if (airlockPanel) airlockPanel.ClearBinding();
            if (pauseBackdrop) pauseBackdrop.ClearBinding();
            if (openingMenuMotion) openingMenuMotion.ClearBinding();
            ReleasePlanningControlGate();
            if (cargoPresentation) cargoPresentation.ClearBinding();
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
                flightBoard.SetDeparturePresentation(false);
                flightBoard.ClearInteractionPrompt();
                flightBoard.SetPhysicalTerminalPresentation(false);
                flightBoard.ClearSubtitle();
                flightBoard.ConfigureExertion(null);
                flightBoard.ConfigurePlanningInstruction(null);
                flightBoard.enabled = false;
                flightBoard.ConfigurePhysicalPlanning(false, null);
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
            if (openingMenuMotion) openingMenuMotion.RefreshEligibility();
            if (preflightTension) preflightTension.RefreshPresentation();
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
            if (openingMenuMotion) openingMenuMotion.RefreshEligibility();
            if (preflightTension) preflightTension.RefreshPresentation();
            controls.Refresh();
        }

        private bool CanChooseMode() => boundSession != null && runtime.GameState.Phase == GamePhase.Planning;

        public void Resume() { if (runtime) runtime.SetPaused(false); }
        public void Restart() { if (runtime) runtime.RestartMission(); }
        public void SetReducedEffects(bool reduced)
        {
            if (marine) marine.SetReducedMotion(reduced);
            if (cinematics) cinematics.ReducedEffects = reduced;
            if (firstPersonMotion) firstPersonMotion.SetReducedMotion(reduced);
            if (openingMenuMotion) openingMenuMotion.SetReducedMotion(reduced);
            if (preflightTension) preflightTension.SetReducedEffects(reduced);
            if (cargoHands) cargoHands.SetReducedMotion(reduced);
        }

        private void OnPauseChanged(bool paused)
        {
            if (openingMenuMotion) openingMenuMotion.ResetToNeutral();
            if (openingMenuMotion) openingMenuMotion.RefreshEligibility();
            if (audioPresenter) audioPresenter.SetPresentationPaused(paused);
            if (preflightTension) preflightTension.RefreshPresentation();
            if (controls) controls.Refresh();
        }

        private void OnPhaseChanged(PhaseChanged message)
        {
            if (!marine && message.Current == GamePhase.Intro) orbitalLocationReached = true;
            if (marine && message.Previous == GamePhase.Planning && message.Current == GamePhase.Intro)
            {
                flightBoard.SetDeparturePresentation(true);
                if (cargoPresentation) cargoPresentation.SetAboardDepartureVisible(true);
                if (firstPersonMotion) firstPersonMotion.ResetToNeutral();
                if (openingMenuMotion) openingMenuMotion.ResetToNeutral();
                if (!marine.BeginDeparture() && !marine.OwnsPresentation)
                    flightBoard.SetDeparturePresentation(false);
            }
            if (preflightTension) preflightTension.RefreshPresentation();
            ApplyPhasePresentation(message.Current, true);
            controls.Refresh();
        }

        private void ApplyPhasePresentation(GamePhase phase, bool playTransition)
        {
            if (openingMenuMotion) openingMenuMotion.RefreshEligibility();
            if (phase == GamePhase.Planning && !runtime.PreflightWalk && planningFramingPivot && planningViewAnchor)
                planningFramingPivot.SetPositionAndRotation(planningViewAnchor.position, planningViewAnchor.rotation);
            else if (!IsTransportDeparturePlaying) RestorePlayerFraming();

            switch (phase)
            {
                case GamePhase.Planning:
                    audioPresenter.SetMood(MusicMood.Isolation);
                    break;
                case GamePhase.Intro:
                    modeChosen = true;
                    flightBoard.ConfigureResourceReference(runtime.Configuration.InitialResources);
                    audioPresenter.SetMood(MusicMood.Isolation);
                    if (playTransition && !IsTransportDeparturePlaying)
                    {
                        audioPresenter.TryPlaySfx("ui_confirm");
                        PlayOptional(recordedBriefing, "MISSION CONTROL · EARTH · RECORDED");
                    }
                    break;
                case GamePhase.Emergency:
                    audioPresenter.SetMood(MusicMood.Emergency, 0f);
                    break;
                case GamePhase.Repair:
                    audioPresenter.SetMood(InteriorRepairsComplete ? MusicMood.Isolation : MusicMood.Tension);
                    break;
                case GamePhase.Eva:
                case GamePhase.Transmission:
                    audioPresenter.SetMood(MusicMood.Tension);
                    break;
                case GamePhase.Survival:
                    audioPresenter.SetMood(MusicMood.Isolation);
                    break;
                case GamePhase.Outcome:
                case GamePhase.Report:
                    audioPresenter.SetMood(runtime.GameState.Outcome == MissionOutcome.Recovered
                        ? MusicMood.Recovery : MusicMood.Silent);
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

        private void OnDepartureCue(TransportDepartureCue cue)
        {
            if (!audioPresenter) return;
            if (cue == TransportDepartureCue.HatchClosing && airlockMotor) audioPresenter.PlaySfx(airlockMotor, .6f);
            if (cue == TransportDepartureCue.Acceleration) audioPresenter.SetMood(MusicMood.Tension, .7f);
            if (cue == TransportDepartureCue.LaterAboard) audioPresenter.SetMood(MusicMood.Isolation, 1f);
        }

        private void OnTransportTransferred()
        {
            orbitalLocationReached = true;
            transferredSession = boundSession;
            if (flightBoard) flightBoard.SetDeparturePresentation(false);
            if (cargoPresentation) cargoPresentation.SetAboardDepartureVisible(false);
            if (boundSession == null || !runtime) return;
            RestorePlayerFraming();
            ApplyPhasePresentation(runtime.GameState.Phase, true);
            controls.Refresh();
            MaintainMicrogravity(); // Transfer finishes in LateUpdate: acquire before the next device event.
        }

        private void OnCinematicRequested(CinematicRequested request)
        {
            if (astronautHands) astronautHands.ResetToNeutral();
            if (openingMenuMotion) openingMenuMotion.ResetToNeutral();
            if (request.Cinematic == CinematicId.FalseSignal) { PlayOptional(falseSignal, "SHIP COMPUTER · BUFFER DIAGNOSTIC", true); return; }
            if (request.Cinematic == CinematicId.CommunicationRestored) { QueueReceivedEarthMessage(); return; }
            if (request.Cinematic != CinematicId.Impact) return;
            audioPresenter.SetMood(MusicMood.Emergency, 0f);
            audioPresenter.TryPlaySfx("power_down");
            PlayOptional(impactWarning, "SHIP COMPUTER", true);
            // Houston is the playable astronaut; this is local speech directed to Earth.
            // The communication service still controls when an Earth reply can arrive.
            PlayOptional(houstonDistress, "HOUSTON · YOU");
        }

        private void OnSystemConditionChanged(SystemConditionChanged change)
        {
            UpdateSystemLights();
            if (change.Condition01 >= 1d)
            {
                if (change.SystemId == ShipSystemId.Power) PlayOptional(powerRestored, "SHIP COMPUTER");
                if (change.SystemId == ShipSystemId.LifeSupport) PlayOptional(lifeSupportRestored, "SHIP COMPUTER");
                if (change.SystemId == ShipSystemId.Antenna) PlayOptional(antennaRestored, "SHIP COMPUTER");
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
            if (change.State == CommunicationState.AwaitingReply) PlayOptional(transmissionSent, "SHIP COMPUTER");
            // C06 enters Survival directly at receipt, without the old ending cinematic.
            // The service owns delay and receipt; presentation never starts a second timer.
            if (change.State == CommunicationState.ReplyReceived) QueueReceivedEarthMessage();
        }

        private void QueueReceivedEarthMessage()
        {
            if (!runtime || boundSession == null || runtime.GameState.Outcome == MissionOutcome.Lost
                || runtime.Session.Communications.State != CommunicationState.ReplyReceived) return;
            // A fast-forward may cross receipt while a previous buffer diagnosis is
            // still speaking. It must not keep claiming that no Earth reply arrived.
            PlayOptional(earthReply, "MISSION CONTROL · EARTH · DELAYED REPLY", true);
        }

        private void OnAirlockStateChanged(AirlockStateChanged change)
        {
            // Cabin intercom / suit contact feedback, never airborne sound in exterior vacuum.
            // Typed state edges only: no retrigger on initial binding, polling or pause/resume.
            if (change.State == AirlockState.Depressurizing || change.State == AirlockState.Repressurizing
                || change.State == AirlockState.InnerOpen || change.State == AirlockState.OuterOpen)
                if (airlockMotor) audioPresenter.PlaySfx(airlockMotor, .32f);
            if (change.State == AirlockState.OuterOpen && airlockReady) audioPresenter.PlaySfx(airlockReady, .22f);
            if (change.State == AirlockState.OuterOpen) PlayOptional(outerHatchOpen, "SHIP COMPUTER");
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
            dialogueQueue.Clear();
            audioPresenter.StopDialogue();
            audioPresenter.SetMood(runtime.GameState.Outcome == MissionOutcome.Recovered
                ? MusicMood.Recovery : MusicMood.Silent);
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
                PresentSuccessfulHandAction();
            }
            else if (!string.IsNullOrWhiteSpace(explanation))
            {
                flightBoard.ShowInteractionPrompt(explanation, false);
                rejectionUntil = Time.unscaledTime + 4f;
            }
        }

        // Called only after InteractionResolver reports the real command succeeded.
        // These are local glove acknowledgements; no target placement, command delay or implied contact.
        private void PresentSuccessfulHandAction()
        {
            if (EarthViewerOwnsInput) return;
            if (antennaRepair && antennaRepair.IsWorking) return;
            if (microgravityInput && microgravityInput.ContactRoutingRequested) return;
            if (!astronautHands || !interaction || !interaction.CurrentTarget) return;
            switch (interaction.CurrentTarget.InteractionId)
            {
                case InteractionIds.SuitLocker:
                    astronautHands.TryPlayGrip();
                    break;
                case InteractionIds.RepairPower:
                case InteractionIds.RepairLifeSupport:
                case InteractionIds.RepairAntenna:
                case InteractionIds.AirlockControls:
                case InteractionIds.CockpitConsole:
                    // Mouse input also drives the free hand gesture. Both paths choose the same hand
                    // so their Update order cannot turn a left click into a right-hand press.
                    astronautHands.TryPlayPress(UnityEngine.InputSystem.Mouse.current != null
                        && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame);
                    break;
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
            RefreshCinematicHud();
            RefreshSubtitle();
        }

        private void LateUpdate() => RefreshCinematicHud();

        private void RefreshCinematicHud()
        {
            if (flightBoard && attached && boundSession != null)
            {
                flightBoard.SetDeparturePresentation(IsTransportDeparturePlaying || (cinematics && cinematics.IsPlaying) || EarthViewerOwnsInput);
                flightBoard.SetPhysicalTerminalPresentation(EarthViewerOwnsInput);
            }
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
