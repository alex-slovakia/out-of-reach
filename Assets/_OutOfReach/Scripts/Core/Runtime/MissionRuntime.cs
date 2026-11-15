using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using OutOfReach.Interaction;
using OutOfReach.Player;
using OutOfReach.Simulation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace OutOfReach.Core.Runtime
{
    /// <summary>How <see cref="MissionRuntime.RestartMission"/> restarts.</summary>
    public enum MissionRestartMode
    {
        /// <summary>Dispose the session and reload the active scene, so every view is rebuilt (first slice default).</summary>
        ReloadActiveScene,

        /// <summary>Dispose the session and start a new one in place. Only safe when every bound view rebinds on SessionStarted.</summary>
        RebuildInPlace
    }

    /// <summary>
    /// The mission's Unity composition root and its only simulation driver. It creates one pure C#
    /// <see cref="MissionSession"/>, advances it exactly once per frame, binds the player and the scene's
    /// interaction targets to the session's domain objects, and exposes the shared contract interfaces for
    /// presentation. Presentation never needs to reference this assembly: the integrator injects the contract
    /// properties, or wires the serialized UnityEvents and public methods in the Inspector.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class MissionRuntime : MonoBehaviour
    {
        private static MissionRuntime activeInstance;

        [Header("Bound gameplay components")]
        [SerializeField] private FirstPersonController player;

        [Tooltip("Feet position and heading the player starts from (the PlayerSpawn anchor).")]
        [SerializeField] private Transform playerSpawn;

        [SerializeField] private InteractableTarget[] interactionTargets = new InteractableTarget[0];

        [Header("EVA")]
        [Tooltip("Gameplay gates the airlock controls: airlock-inner and airlock-outer.")]
        [SerializeField] private GameplayGate[] gates = new GameplayGate[0];

        [Tooltip("Constrained route from EvaStart to the antenna work site.")]
        [SerializeField] private TetheredRoute evaRoute;

        [Tooltip("Feet position and heading after re-entering the airlock, inside by the outer hatch.")]
        [SerializeField] private Transform evaReentryPoint;

        [Header("Session")]
        [Tooltip("Create the mission session in Awake.")]
        [SerializeField] private bool startOnAwake = true;

        [Tooltip("Starting loadout preset ID; empty uses the mission default.")]
        [SerializeField] private string startingPresetId = string.Empty;

        [Tooltip("Advance the mission from Update. Tests switch this off and step manually.")]
        [SerializeField] private bool driveFromUpdate = true;

        [Tooltip("Longest real frame time fed to the simulation, seconds, so a hitch cannot drain reserves.")]
        [SerializeField] private double hitchClampRealSeconds = 0.25d;

        [SerializeField] private MissionRestartMode restartMode = MissionRestartMode.ReloadActiveScene;

        [Header("Physical preflight (C05-A)")]
        [Tooltip("Off: planning is a list, the player has no control before launch. On: the player walks during Planning, carries crates between cargo-supply and cargo-bay targets and launches from the cockpit console.")]
        [SerializeField] private bool preflightWalk;

        [Tooltip("With Preflight Walk and timed planning: at zero the loading is sealed (last valid loadout if the draft is invalid) and the mission launches only from the cockpit console, i.e. the transport seat. No teleport at expiry. Off: expiry launches at once, as before.")]
        [SerializeField] private bool boardAfterLoadingDeadline;

        [Tooltip("Allow player control during the first Impact sequence. Pause and other sequences still lock control.")]
        [SerializeField] private bool allowMovementDuringImpact;

        [Tooltip("Opt-in post-contact survival. Enable only with ration, water and grow-module presentation wired.")]
        [SerializeField] private bool survivalExtension;

        [Tooltip("Approved day 1-30 chapter loop (CORE-CHAPTER-LOOP). Needs Survival Extension; off keeps every scene as before. Enable only with the chapter/montage/decision presentation wired.")]
        [SerializeField] private bool chapterLoop;

        [Tooltip("Feet position and heading the player starts from during the physical preflight. Empty uses Player Spawn.")]
        [SerializeField] private Transform preflightSpawn;

        [Header("Timed planning")]
        [Tooltip("Unpaused real seconds for BeginTimedPlanning.")]
        [SerializeField] private double planningDeadlineRealSeconds = OutOfReach.Core.PlanningDeadline.DefaultDurationRealSeconds;

        [Header("Events for presentation wiring")]
        [Tooltip("Raised after a session was created and bound (also after a restart in place).")]
        [SerializeField] private UnityEvent sessionStarted = new UnityEvent();

        [Tooltip("Raised when the mission pause state changes.")]
        [SerializeField] private UnityEvent<bool> pauseChanged = new UnityEvent<bool>();

        private bool playerSubscribed;
        private bool playerOutside;
        private bool playerInPreflight;
        private int lastStepFrame = -1;

        /// <summary>The current mission session, or null before start and after teardown.</summary>
        public MissionSession Session { get; private set; }

        public IMissionEventBus Events => Session != null ? Session.Events : null;

        public IGameState GameState => Session != null ? Session.State : null;

        public IMissionPlanner Planner => Session != null ? Session.Planner : null;

        public IResourceSystem Resources => Session != null ? Session.Resources : null;

        public IMissionConfiguration Configuration => Session != null ? Session.Configuration : null;

        public IReadOnlyList<IRepairable> Repairables => Session != null ? Session.Systems.Repairables : null;

        /// <summary>Read-only clock for the HUD: pause, disclosed time scale, elapsed simulated time.</summary>
        public ISimulationClock Clock => Session != null ? Session.ClockView : null;

        /// <summary>Read-only planning deadline. Start timed planning with <see cref="BeginTimedPlanning"/>.</summary>
        public IPlanningDeadline PlanningDeadline => Session != null ? Session.PlanningDeadline : null;

        public bool IsPaused => Session != null && Session.Clock.IsPaused;

        public FirstPersonController Player => player;

        public IReadOnlyList<InteractableTarget> InteractionTargets => interactionTargets;

        public IReadOnlyList<GameplayGate> Gates => gates;

        public TetheredRoute EvaRoute => evaRoute;

        public Transform EvaReentryPoint => evaReentryPoint;

        /// <summary>
        /// Read-only suit and airlock state for an EVA HUD (suit oxygen, time left, airlock timer, Changed event)
        /// through the shared contract.
        /// </summary>
        public IEvaStatus EvaStatus => Session != null ? Session.Eva : null;

        /// <summary>The same object with its commands and counters, for code that references Core.</summary>
        public EvaOperations Eva => Session != null ? Session.Eva : null;

        /// <summary>
        /// The link to Earth for the console and finale views: state, NASA/JPL reference distance, one-way light
        /// time, Earth processing time and the remaining reply wait. CommunicationChanged is published on state
        /// changes only, so a countdown polls ReplyRemainingSimulationSeconds and shows Clock.TimeScale next to it.
        /// </summary>
        public ICommunicationSystem Communications => Session != null ? Session.Communications : null;

        /// <summary>
        /// The same link with the facts the contract does not carry yet: reference sample epoch, transmit energy,
        /// whether the mission date still has a reference sample, and the transmission and reply times.
        /// </summary>
        public CommunicationSystem CommunicationDetails => Session != null ? Session.Communications : null;

        /// <summary>
        /// Read-only field-modification state for the HUD: payload salvaged, cartridges carried, absorber installed
        /// and its remaining time. A modification is offered only when its targets are bound in this scene:
        /// salvage-payload for the salvage; workbench and absorber-bay together for the cartridge.
        /// </summary>
        public IFieldModifications FieldModifications => Session != null ? Session.FieldModifications : null;

        /// <summary>
        /// Preflight crate handling for presentation: the crate in hands (CarriedEquipmentId), the carry speed
        /// factor, the supply left per type and a Changed event. The bay's content is IMissionPlanner.Draft.
        /// The returned object implements the shared ICargoLoading.
        /// </summary>
        public CargoLoading Cargo => Session != null ? Session.Cargo : null;

        /// <summary>True when this runtime runs the physical preflight (walking and crates during Planning).</summary>
        public bool PreflightWalk => preflightWalk;

        /// <summary>True when an expiring loading deadline seals the cargo and waits for the seat (C05-BOARDING-01).</summary>
        public bool BoardAfterLoadingDeadline => boardAfterLoadingDeadline;

        /// <summary>
        /// True during the boarding window: the loading deadline has sealed the cargo and the mission waits for the
        /// seat (cockpit console). Show PlanningDeadline.LoadingClosedMessage meanwhile.
        /// </summary>
        public bool AwaitingBoarding => Session != null && Session.PlanningDeadline.IsAwaitingBoarding;

        public ISurvivalExtension Survival => Session != null ? Session.Survival : null;

        /// <summary>The approved chapter loop; null without a session or when the chapter loop is off.</summary>
        public IMissionChapterFlow ChapterFlow => Session != null ? Session.ChapterFlow : null;

        /// <summary>Rest and the fictional recovery timeline; null without a session or when survival is disabled.</summary>
        public ISurvivalRecovery SurvivalRecovery => Session != null ? Session.SurvivalRecovery : null;

        /// <summary>Mission date on the simulation clock (T0 + elapsed simulated time), for date and "mission day" labels.</summary>
        public MissionCalendar Calendar => Session != null ? Session.Calendar : null;

        /// <summary>
        /// The final mission report once an outcome is decided, otherwise null. Read it when MissionEnded arrives
        /// or when the phase becomes Report. It implements the shared IMissionReport (display-ready sections);
        /// its typed members carry the same facts for code that references Core.
        /// </summary>
        public MissionReport Report => Session != null ? Session.Report : null;

        public MissionRestartMode RestartMode
        {
            get { return restartMode; }
            set { restartMode = value; }
        }

        public bool DriveFromUpdate
        {
            get { return driveFromUpdate; }
            set { driveFromUpdate = value; }
        }

        public UnityEvent SessionStartedEvent => sessionStarted;

        public UnityEvent<bool> PauseChangedEvent => pauseChanged;

        /// <summary>Raised after a session was created and bound. Views read current state, then subscribe.</summary>
        public event Action<MissionSession> SessionStarted;

        /// <summary>Raised just before a session is disposed. Views drop their references here.</summary>
        public event Action<MissionSession> SessionEnding;

        public event Action<bool> PauseChanged;

        /// <summary>Explicit wiring for code-built scenes; authored scenes use the serialized fields.</summary>
        public void Configure(FirstPersonController boundPlayer, Transform spawn, IEnumerable<InteractableTarget> targets)
        {
            UnsubscribePlayer();
            player = boundPlayer;
            playerSpawn = spawn;
            interactionTargets = targets != null ? new List<InteractableTarget>(targets).ToArray() : new InteractableTarget[0];
            if (Application.isPlaying && isActiveAndEnabled)
            {
                SubscribePlayer();
            }
        }

        /// <summary>Explicit EVA wiring for code-built scenes; authored scenes use the serialized fields.</summary>
        public void ConfigureEva(IEnumerable<GameplayGate> airlockGates, TetheredRoute route, Transform reentryPoint)
        {
            gates = airlockGates != null ? new List<GameplayGate>(airlockGates).ToArray() : new GameplayGate[0];
            evaRoute = route;
            evaReentryPoint = reentryPoint;
        }

        /// <summary>Configures playable impact movement for the next session; default is off.</summary>
        public void ConfigureImpactMovement(bool enabled)
        {
            allowMovementDuringImpact = enabled;
        }

        /// <summary>
        /// Configures the boarding window after the loading deadline for the next session (StartSession or
        /// RestartMission). Only effective with the physical preflight. Default false keeps the immediate launch.
        /// </summary>
        public void ConfigureBoarding(bool boardAfterDeadline)
        {
            boardAfterLoadingDeadline = boardAfterDeadline;
        }

        /// <summary>Configures post-contact survival for the next session. Default false preserves the original ending.</summary>
        public void ConfigureSurvivalExtension(bool enabled)
        {
            survivalExtension = enabled;
        }

        /// <summary>
        /// Configures the approved day 1-30 chapter loop for the next session (StartSession or RestartMission). True also
        /// enables the survival extension it needs. Default false preserves every existing scene.
        /// </summary>
        public void ConfigureChapterLoop(bool enabled)
        {
            chapterLoop = enabled;
            if (enabled)
            {
                survivalExtension = true;
            }
        }

        /// <summary>
        /// Explicit preflight wiring for code-built scenes; authored scenes use the serialized fields. Takes effect
        /// with the next session (StartSession or RestartMission).
        /// </summary>
        public void ConfigurePreflight(bool walkDuringPlanning, Transform spawn)
        {
            preflightWalk = walkDuringPlanning;
            preflightSpawn = spawn;
        }

        /// <summary>
        /// Applies mission state to the scene: the inner gate is walkable only while the airlock is InnerOpen, the
        /// outer gate is never walkable, and the player is clipped to the EVA route exactly while the mission says
        /// the crew is outside. Runs every frame; safe to call directly after a manual step.
        /// </summary>
        public void SyncScene()
        {
            if (Session == null)
            {
                return;
            }

            SyncPreflight();
            EvaOperations eva = Session.Eva;
            if (gates != null)
            {
                foreach (GameplayGate gate in gates)
                {
                    if (gate != null)
                    {
                        gate.SetOpen(gate.GateId == GateIds.AirlockInner && eva.IsInnerHatchPassable);
                    }
                }
            }

            bool outside = eva.IsOutside;
            if (outside == playerOutside)
            {
                return;
            }

            playerOutside = outside;
            if (player == null)
            {
                return;
            }

            if (outside)
            {
                if (evaRoute != null && evaRoute.IsValid)
                {
                    player.EnterRoute(evaRoute);
                }
                else
                {
                    Debug.LogError("MissionRuntime: the crew went outside but no valid EVA route is assigned.", this);
                }
            }
            else
            {
                Transform reentry = evaReentryPoint != null ? evaReentryPoint : playerSpawn;
                if (reentry != null)
                {
                    player.ExitRoute(reentry.position, reentry.eulerAngles.y);
                }
            }
        }

        /// <summary>Creates and binds a fresh mission session. Any current session is disposed first.</summary>
        public void StartSession()
        {
            EndSession();
            string preset = string.IsNullOrWhiteSpace(startingPresetId) ? null : startingPresetId;
            Session = new MissionSession(AuroraMission.CreateDefinition(), Debug.LogException, preset, preflightWalk, allowMovementDuringImpact,
                survivalExtension, null, boardAfterLoadingDeadline, survivalExtension && chapterLoop);
            BindScene();
            if (SessionStarted != null)
            {
                SessionStarted(Session);
            }

            sessionStarted.Invoke();
        }

        /// <summary>
        /// Restart command for the report's Restart button (wire it with a UnityEvent). The session is disposed and,
        /// by default, the active scene is reloaded so no view keeps a stale reference.
        /// </summary>
        public void RestartMission()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (restartMode == MissionRestartMode.ReloadActiveScene && scene.buildIndex >= 0)
            {
                EndSession();
                SceneManager.LoadScene(scene.buildIndex);
                return;
            }

            if (restartMode == MissionRestartMode.ReloadActiveScene)
            {
                Debug.LogWarning("MissionRuntime: the active scene is not in the build list, restarting the mission in place instead of reloading it.", this);
            }

            StartSession();
        }

        /// <summary>
        /// Starts the real-time planning deadline (timed mode). Wire the planner's "start timed planning" control
        /// to it with a UnityEvent. Lifecycle: a new session starts in Planning with the deadline disabled
        /// (practice); call this once, before or during planning, to arm it. It needs a valid loadout to fall
        /// back to and cannot be disarmed afterwards. Not calling it keeps practice planning.
        /// </summary>
        public void BeginTimedPlanning()
        {
            if (Session == null)
            {
                return;
            }

            CommandResult result = Session.PlanningDeadline.TryEnable(planningDeadlineRealSeconds);
            if (!result.Succeeded && result.Failure != CommandFailure.AlreadyComplete)
            {
                Debug.LogWarning("MissionRuntime: timed planning was not started. " + result.Explanation, this);
            }
        }

        public void SetPaused(bool paused)
        {
            if (Session == null)
            {
                return;
            }

            CommandResult result = paused ? Session.Clock.Pause() : Session.Clock.Resume();
            if (!result.Succeeded)
            {
                return;
            }

            if (PauseChanged != null)
            {
                PauseChanged(paused);
            }

            pauseChanged.Invoke(paused);
        }

        public void TogglePause()
        {
            SetPaused(!IsPaused);
        }

        /// <summary>
        /// Advances the mission by one frame of unscaled real time. Called from Update when DriveFromUpdate is on;
        /// call it directly only when it is off. A second call in the same rendered frame is ignored.
        /// </summary>
        public CommandResult Step(double unscaledRealSeconds)
        {
            if (Session == null)
            {
                return new CommandResult(false, CommandFailure.WrongPhase, "No mission session is running.");
            }

            if (driveFromUpdate && lastStepFrame == Time.frameCount)
            {
                return new CommandResult(false, CommandFailure.AlreadyComplete, "The mission was already advanced this frame.");
            }

            lastStepFrame = Time.frameCount;
            return Session.TickFrame(unscaledRealSeconds, hitchClampRealSeconds);
        }

        /// <summary>
        /// Chapter jump for a chapter card or a "skip the wait" control: advances reserves, the airlock, the suit
        /// and the communication clock by the same simulated seconds, exactly like many frames would. A failure
        /// or the reply inside the jump ends the mission at its exact time. Rejected while paused and outside
        /// flight phases. The jump is disclosed in the report's time-compression section.
        /// </summary>
        public CommandResult SkipSimulatedTime(double simulationSeconds)
        {
            if (Session == null)
            {
                return new CommandResult(false, CommandFailure.WrongPhase, "No mission session is running.");
            }

            CommandResult result = Session.AdvanceSimulation(simulationSeconds);
            SyncScene();
            return result;
        }

        /// <summary>A report built from the current state at any time (IsFinal is false while the mission runs).</summary>
        public MissionReport BuildReportPreview()
        {
            return Session != null ? Session.BuildReport() : null;
        }

        public bool TryGetInteractable(string interactionId, out IInteractable interactable)
        {
            if (Session == null)
            {
                interactable = null;
                return false;
            }

            return Session.Interactions.TryGet(interactionId, out interactable);
        }

        private void Awake()
        {
            if (startOnAwake)
            {
                StartSession();
            }
        }

        private void OnEnable()
        {
            if (activeInstance != null && activeInstance != this)
            {
                Debug.LogError("MissionRuntime: a second mission runtime was enabled. Only one may drive the simulation; this one is disabled.", this);
                enabled = false;
                return;
            }

            activeInstance = this;
            SubscribePlayer();
        }

        private void OnDisable()
        {
            // A disabled runtime no longer drives the clock: wake a resting crew so that no 3,600x rest outlives it.
            // Pause alone keeps a rest.
            if (Session != null && Session.SurvivalRecovery != null)
            {
                Session.SurvivalRecovery.ReleaseForDisabledRuntime();
            }

            UnsubscribePlayer();
            if (activeInstance == this)
            {
                activeInstance = null;
            }
        }

        private void Update()
        {
            if (driveFromUpdate)
            {
                Step(Time.unscaledDeltaTime);
            }

            SyncScene();
        }

        private void OnDestroy()
        {
            EndSession();
        }

        private void EndSession()
        {
            if (Session == null)
            {
                return;
            }

            MissionSession ending = Session;
            if (SessionEnding != null)
            {
                SessionEnding(ending);
            }

            if (interactionTargets != null)
            {
                foreach (InteractableTarget target in interactionTargets)
                {
                    if (target != null)
                    {
                        target.Unbind();
                    }
                }
            }

            if (player != null)
            {
                player.ControlGate = DenyControl;
            }

            Session = null;
            ending.Dispose();
        }

        private void BindScene()
        {
            if (interactionTargets != null)
            {
                foreach (InteractableTarget target in interactionTargets)
                {
                    if (target == null)
                    {
                        continue;
                    }

                    if (Session.Interactions.TryGet(target.InteractionId, out IInteractable interactable))
                    {
                        target.Bind(interactable);
                    }
                    else
                    {
                        target.Unbind();
                        Debug.LogError($"MissionRuntime: interaction target '{target.name}' uses unknown interaction ID '{target.InteractionId}'.", target);
                    }
                }
            }

            Session.EnableFieldModifications(
                IsTargetBound(InteractionIds.SalvagePayload),
                IsTargetBound(InteractionIds.Workbench) && IsTargetBound(InteractionIds.AbsorberBay));

            if (gates != null)
            {
                foreach (GameplayGate gate in gates)
                {
                    if (gate != null && gate.GateId != GateIds.AirlockInner && gate.GateId != GateIds.AirlockOuter)
                    {
                        Debug.LogError($"MissionRuntime: gameplay gate '{gate.name}' uses unknown gate ID '{gate.GateId}'.", gate);
                    }
                }
            }

            playerOutside = false;
            playerInPreflight = Session.PreflightWalkEnabled && Session.State.Phase == GamePhase.Planning;
            if (player != null)
            {
                player.ControlGate = IsPlayerControlAllowed;
                player.SpeedMultiplier = 1f;
                Transform start = playerInPreflight && preflightSpawn != null ? preflightSpawn : playerSpawn;
                if (start != null)
                {
                    player.Teleport(start.position, start.eulerAngles.y);
                }
            }

            SyncScene();
        }

        /// <summary>
        /// Carry speed while a crate is in hands, and the move to the cockpit when the mission leaves Planning
        /// (the crate in hands stays behind; Core has already dropped it).
        /// </summary>
        private void SyncPreflight()
        {
            if (player == null)
            {
                return;
            }

            player.SpeedMultiplier = Session.Cargo.CarrySpeedFactor;
            if (!playerInPreflight || Session.State.Phase == GamePhase.Planning)
            {
                return;
            }

            playerInPreflight = false;
            if (playerSpawn != null)
            {
                player.Teleport(playerSpawn.position, playerSpawn.eulerAngles.y);
            }
        }

        private bool IsTargetBound(string interactionId)
        {
            if (interactionTargets == null)
            {
                return false;
            }

            foreach (InteractableTarget target in interactionTargets)
            {
                if (target != null && target.InteractionId == interactionId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPlayerControlAllowed()
        {
            return Session != null && Session.IsPlayerControlAllowed;
        }

        private static bool DenyControl()
        {
            return false;
        }

        private void SubscribePlayer()
        {
            if (player == null || playerSubscribed)
            {
                return;
            }

            player.PauseRequested += TogglePause;
            playerSubscribed = true;
        }

        private void UnsubscribePlayer()
        {
            if (player == null || !playerSubscribed)
            {
                return;
            }

            player.PauseRequested -= TogglePause;
            playerSubscribed = false;
        }
    }
}
