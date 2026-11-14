using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using OutOfReach.Interaction;
using OutOfReach.Player;
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

        [Header("Timed planning")]
        [Tooltip("Unpaused real seconds for BeginTimedPlanning.")]
        [SerializeField] private double planningDeadlineRealSeconds = PlanningDeadline.DefaultDurationRealSeconds;

        [Header("Events for presentation wiring")]
        [Tooltip("Raised after a session was created and bound (also after a restart in place).")]
        [SerializeField] private UnityEvent sessionStarted = new UnityEvent();

        [Tooltip("Raised when the mission pause state changes.")]
        [SerializeField] private UnityEvent<bool> pauseChanged = new UnityEvent<bool>();

        private bool playerSubscribed;
        private int lastStepFrame = -1;

        /// <summary>The current mission session, or null before start and after teardown.</summary>
        public MissionSession Session { get; private set; }

        public IMissionEventBus Events => Session != null ? Session.Events : null;

        public IGameState GameState => Session != null ? Session.State : null;

        public IMissionPlanner Planner => Session != null ? Session.Planner : null;

        public IResourceSystem Resources => Session != null ? Session.Resources : null;

        public IMissionConfiguration Configuration => Session != null ? Session.Configuration : null;

        public IReadOnlyList<IRepairable> Repairables => Session != null ? Session.Systems.Repairables : null;

        /// <summary>Read-only clock for the HUD (shape of the proposed ISimulationClock).</summary>
        public SimulationClockView Clock => Session != null ? Session.ClockView : null;

        /// <summary>Planning deadline state (shape of the proposed IPlanningDeadline).</summary>
        public PlanningDeadline PlanningDeadline => Session != null ? Session.PlanningDeadline : null;

        public bool IsPaused => Session != null && Session.Clock.IsPaused;

        public FirstPersonController Player => player;

        public IReadOnlyList<InteractableTarget> InteractionTargets => interactionTargets;

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

        /// <summary>Creates and binds a fresh mission session. Any current session is disposed first.</summary>
        public void StartSession()
        {
            EndSession();
            string preset = string.IsNullOrWhiteSpace(startingPresetId) ? null : startingPresetId;
            Session = new MissionSession(AuroraMission.CreateDefinition(), Debug.LogException, preset);
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

        /// <summary>Starts the real-time planning deadline (timed mode). Wire to the planner's start button.</summary>
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

            if (player == null)
            {
                return;
            }

            player.ControlGate = IsPlayerControlAllowed;
            if (playerSpawn != null)
            {
                player.Teleport(playerSpawn.position, playerSpawn.eulerAngles.y);
            }
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
