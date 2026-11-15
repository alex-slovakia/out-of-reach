using System;
using OutOfReach.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.UI
{
    /// <summary>Contract-bound planner and reserve display. Never advances or owns mission state.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class MissionFlightBoard : MonoBehaviour, IDisposable
    {
        [SerializeField] private StyleSheet styleSheet;

        private IMissionPlanner planner;
        private IResourceSystem resources;
        private IGameState state;
        private IMissionEventBus events;
        private ISimulationClock clock;
        private IPlanningDeadline deadline;
        private IEvaStatus evaStatus, subscribedEva;
        private ICargoLoading cargoLoading, subscribedCargo;
        private Action evaChangedHandler;
        private int evaSubscriptionGeneration;
        private ResourceSnapshot resourceReference;
        private bool hasResourceReference, hasCo2Scale;
        private double co2ActionPpm, co2EmergencyPpm;
        private FlightBoardView view;
        private bool subscribed;
        private bool disposed;
        private bool physicalPlanning;
        private bool menuOverlayVisible;
        private bool departurePresentation;
        private bool physicalTerminalPresentation;
        private string commandExplanation = string.Empty;
        private string subtitleText = string.Empty;
        private string subtitleSpeaker = string.Empty;
        private string interactionPrompt = string.Empty;
        private bool interactionAvailable;
        private bool showMicrogravityTutorial, showRailPrompt;
        private Func<float> readExertionRemaining;
        private Func<bool> readInteriorSystemsOperational;
        private Func<string> readPlanningInstruction;
        public void ConfigurePlanningInstruction(Func<string> readInstruction) => readPlanningInstruction = readInstruction;
        private Func<string> readObjectiveTitle, readObjectiveDetail;

        /// <summary>
        /// Optional (CHAPTER-UI-02): the chapter loop's next step for the HUD objective, read every frame (cached strings).
        /// Null providers or empty text keep the phase copy; FlightBoardView applies it only after the repairs and in Survival.
        /// </summary>
        public void ConfigureObjectiveOverride(Func<string> readTitle, Func<string> readDetail)
        {
            readObjectiveTitle = readTitle;
            readObjectiveDetail = readDetail;
            view?.SetObjectiveOverride(readTitle?.Invoke(), readDetail?.Invoke());
        }

        public StyleSheet SharedStyleSheet => styleSheet;

        /// <summary>Root-owned transport shot visibility. Captions and the separate pause menu remain available.</summary>
        public void SetDeparturePresentation(bool active)
        {
            if (disposed) return;
            departurePresentation = active;
            view?.SetDeparturePresentation(active);
        }

        /// <summary>Keep live dialogue below the physical display while its exclusive viewer is active.</summary>
        public void SetPhysicalTerminalPresentation(bool active)
        {
            if (disposed) return;
            physicalTerminalPresentation = active;
            view?.SetPhysicalTerminalPresentation(active);
        }

        public void ConfigureExertion(Func<float> readRemaining)
        {
            readExertionRemaining = readRemaining;
            if (readRemaining == null) view?.SetExertion(float.NaN);
        }

        /// <summary>Composition-owned mode/pause overlay; hides presentation without changing mission state.</summary>
        public void SetMenuOverlayVisible(bool visible)
        {
            if (disposed) return;
            menuOverlayVisible = visible;
            view?.SetMenuOverlayVisible(visible);
        }

        public void Configure(IMissionPlanner missionPlanner, IResourceSystem resourceSystem,
            IGameState gameState, IMissionEventBus eventBus)
            => Configure(missionPlanner, resourceSystem, gameState, eventBus, null);

        /// <summary>Optional read-only operational status supplied by the composition owner; UI defines no repair threshold.</summary>
        public void Configure(IMissionPlanner missionPlanner, IResourceSystem resourceSystem,
            IGameState gameState, IMissionEventBus eventBus, Func<bool> readInteriorSystemsOperational)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MissionFlightBoard));
            if (missionPlanner == null) throw new ArgumentNullException(nameof(missionPlanner));
            if (resourceSystem == null) throw new ArgumentNullException(nameof(resourceSystem));
            if (gameState == null) throw new ArgumentNullException(nameof(gameState));
            if (eventBus == null) throw new ArgumentNullException(nameof(eventBus));

            Unsubscribe();
            if (state != null && !ReferenceEquals(state, gameState))
            {
                // Rebind timing explicitly for a replacement session; never retain its predecessor's clock.
                clock = null;
                deadline = null;
                evaStatus = null;
                cargoLoading = null;
                physicalPlanning = false;
                SetDeparturePresentation(false);
                view?.SetPhysicalPlanning(false);
                hasResourceReference = false;
                view?.ClearResourceReference();
            }
            planner = missionPlanner;
            resources = resourceSystem;
            state = gameState;
            events = eventBus;
            this.readInteriorSystemsOperational = readInteriorSystemsOperational;
            commandExplanation = string.Empty;
            ClearSubtitle();
            ClearInteractionPrompt();
            if (!isActiveAndEnabled) return;
            CreateView();
            Refresh(); // Read current snapshots before listening, including mid-mission binding.
            Subscribe();
        }

        /// <summary>Optional read-only timing. Null inputs hide the unavailable presentation.</summary>
        public void ConfigureTiming(ISimulationClock clock, IPlanningDeadline deadline)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MissionFlightBoard));
            this.clock = clock;
            this.deadline = deadline;
            RefreshTiming();
        }

        /// <summary>Bar reference from the committed launch configuration, never from a HUD estimate.</summary>
        public void ConfigureResourceReference(ResourceSnapshot launchResources)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MissionFlightBoard));
            resourceReference = launchResources;
            hasResourceReference = true;
            view?.ConfigureResourceReference(launchResources);
        }

        /// <summary>Inject the scenario's actual CO₂ action and emergency levels, in ppm.</summary>
        public void ConfigureCo2Scale(double actionPpm, double emergencyPpm)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MissionFlightBoard));
            if (double.IsNaN(actionPpm) || double.IsInfinity(actionPpm) || actionPpm <= 0
                || double.IsNaN(emergencyPpm) || double.IsInfinity(emergencyPpm) || emergencyPpm <= actionPpm)
                throw new ArgumentOutOfRangeException(nameof(emergencyPpm), "CO₂ levels must be finite, positive and ordered action < emergency.");
            co2ActionPpm = actionPpm;
            co2EmergencyPpm = emergencyPpm;
            hasCo2Scale = true;
            view?.ConfigureCo2Scale(actionPpm, emergencyPpm);
        }

        /// <summary>Bind a read-only suit view. Null detaches and hides the suit instrument.</summary>
        public void ConfigureEva(IEvaStatus status)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MissionFlightBoard));
            UnsubscribeEva();
            evaStatus = status;
            RefreshEva();
            SubscribeEva();
        }

        /// <summary>Optional physical-loading refresh; the planner remains the sole loadout authority.</summary>
        public void ConfigureCargoLoading(ICargoLoading cargo)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MissionFlightBoard));
            UnsubscribeCargo();
            cargoLoading = cargo;
            Refresh();
            SubscribeCargo();
        }

        /// <summary>Choose the physical preflight view; all loading and launch commands remain in Core.</summary>
        public void ConfigurePhysicalPlanning(bool enabled, ICargoLoading cargo)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MissionFlightBoard));
            physicalPlanning = enabled;
            view?.SetPhysicalPlanning(enabled);
            ConfigureCargoLoading(cargo);
        }

        /// <summary>The audio presenter owns when this cue begins and ends. No local timer is started.</summary>
        public void ShowSubtitle(string text, string speakerLabel)
        {
            if (disposed) return;
            subtitleText = text ?? string.Empty;
            subtitleSpeaker = speakerLabel ?? string.Empty;
            view?.ShowSubtitle(subtitleText, subtitleSpeaker);
        }

        public void ClearSubtitle()
        {
            subtitleText = string.Empty;
            subtitleSpeaker = string.Empty;
            view?.ClearSubtitle();
        }

        /// <summary>Presentation-only target prompt. The player adapter supplies the wording and availability.</summary>
        public void ShowInteractionPrompt(string text, bool available)
        {
            if (disposed) return;
            interactionPrompt = text ?? string.Empty;
            interactionAvailable = available;
            view?.ShowInteractionPrompt(interactionPrompt, available);
        }

        public void ClearInteractionPrompt()
        {
            interactionPrompt = string.Empty;
            interactionAvailable = false;
            view?.ClearInteractionPrompt();
        }

        public void SetMicrogravityPrompt(bool showTutorial, bool showRail)
        {
            if (disposed) return;
            showMicrogravityTutorial = showTutorial;
            showRailPrompt = showRail;
            view?.SetMicrogravityPrompt(showTutorial, showRail);
        }

        private void LateUpdate()
        {
            // These interfaces have no change events. Read Core's values after its frame update;
            // never integrate deltaTime, decrement a deadline or infer elapsed simulation time.
            if (disposed) return;
            RefreshTiming();
            view?.SetObjectiveOverride(readObjectiveTitle?.Invoke(), readObjectiveDetail?.Invoke());
            view?.SetExertion(readExertionRemaining != null && state != null && state.Outcome == MissionOutcome.None
                ? readExertionRemaining() : float.NaN);
            view?.AdvancePresentation(Time.unscaledDeltaTime, menuOverlayVisible || (clock != null && clock.IsPaused));
            // Tab reveals telemetry only. It never changes player, camera, cursor or mission authority.
            view?.SetSystemsHeld(state != null && state.Phase != GamePhase.Planning
                && Keyboard.current != null && Keyboard.current.tabKey.isPressed);
            view?.SetPlanningDetailsHeld(physicalPlanning && state != null && state.Phase == GamePhase.Planning
                && Keyboard.current != null && Keyboard.current.tabKey.isPressed);
        }

        private void OnEnable()
        {
            if (disposed) return;
            CreateView();
            Refresh();
            Subscribe();
        }

        private void Start()
        {
            // UIDocument may be enabled later in a composition root's initialization order.
            if (!disposed && view == null)
            {
                CreateView();
                Refresh();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            ReleaseView();
        }

        private void OnDestroy() => Dispose();

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Unsubscribe();
            ReleaseView();
            planner = null;
            resources = null;
            state = null;
            events = null;
            clock = null;
            deadline = null;
            evaStatus = null;
            cargoLoading = null;
            readExertionRemaining = null;
            readInteriorSystemsOperational = null;
            physicalPlanning = false;
            departurePresentation = false;
            physicalTerminalPresentation = false;
            hasResourceReference = false;
            subtitleText = string.Empty;
            subtitleSpeaker = string.Empty;
            interactionPrompt = string.Empty;
        }

        private void CreateView()
        {
            if (view != null) return;
            var document = GetComponent<UIDocument>();
            var root = document.rootVisualElement;
            if (root == null) return;
            view = new FlightBoardView(root, styleSheet);
            view.SetMenuOverlayVisible(menuOverlayVisible);
            view.SetDeparturePresentation(departurePresentation);
            view.SetPhysicalTerminalPresentation(physicalTerminalPresentation);
            view.SetPhysicalPlanning(physicalPlanning);
            view.QuantityChangeRequested += ChangeQuantity;
            view.LaunchRequested += Launch;
            view.ShowSubtitle(subtitleText, subtitleSpeaker);
            view.ShowInteractionPrompt(interactionPrompt, interactionAvailable);
            if (hasResourceReference) view.ConfigureResourceReference(resourceReference);
            if (hasCo2Scale) view.ConfigureCo2Scale(co2ActionPpm, co2EmergencyPpm);
            RefreshEva();
        }

        private void ReleaseView()
        {
            if (view == null) return;
            view.QuantityChangeRequested -= ChangeQuantity;
            view.LaunchRequested -= Launch;
            view.Dispose();
            view = null;
        }

        private void Subscribe()
        {
            SubscribeEva();
            SubscribeCargo();
            if (subscribed || events == null || !isActiveAndEnabled || disposed) return;
            events.PhaseChanged += OnPhaseChanged;
            events.ResourceChanged += OnResourceChanged;
            events.SystemConditionChanged += OnSystemConditionChanged;
            events.MissionEnded += OnMissionEnded;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            UnsubscribeEva();
            UnsubscribeCargo();
            if (!subscribed) return;
            events.PhaseChanged -= OnPhaseChanged;
            events.ResourceChanged -= OnResourceChanged;
            events.SystemConditionChanged -= OnSystemConditionChanged;
            events.MissionEnded -= OnMissionEnded;
            subscribed = false;
        }

        private void SubscribeEva()
        {
            if (subscribedEva != null || evaStatus == null || !isActiveAndEnabled || disposed) return;
            int generation = ++evaSubscriptionGeneration;
            subscribedEva = evaStatus;
            evaChangedHandler = () =>
            {
                if (!disposed && isActiveAndEnabled && subscribedEva != null && generation == evaSubscriptionGeneration)
                    RefreshEva();
            };
            subscribedEva.Changed += evaChangedHandler;
        }

        private void UnsubscribeEva()
        {
            evaSubscriptionGeneration++;
            if (subscribedEva != null) subscribedEva.Changed -= evaChangedHandler;
            subscribedEva = null;
            evaChangedHandler = null;
        }

        private void RefreshEva() => view?.SetEvaStatus(evaStatus);

        private void SubscribeCargo()
        {
            if (subscribedCargo != null || cargoLoading == null || !isActiveAndEnabled || disposed) return;
            subscribedCargo = cargoLoading;
            subscribedCargo.Changed += OnCargoChanged;
        }

        private void UnsubscribeCargo()
        {
            if (subscribedCargo != null) subscribedCargo.Changed -= OnCargoChanged;
            subscribedCargo = null;
        }

        private void OnCargoChanged()
        {
            if (!disposed && isActiveAndEnabled && state != null && state.Phase == GamePhase.Planning)
                Refresh();
        }

        private void Refresh()
        {
            if (view == null) return;
            if (planner == null || resources == null || state == null)
            {
                view.ShowUnbound();
                return;
            }

            bool planning = state.Phase == GamePhase.Planning;
            view.SetMicrogravityPrompt(showMicrogravityTutorial, showRailPrompt);
            view.SetPhysicalPlanning(physicalPlanning);
            view.ShowPlanning(planning);
            RefreshObjective();
            if (planning)
            {
                // Draft is a fresh authoritative snapshot after each command. Do not retain it as state.
                var draft = planner.Draft;
                var validation = draft.Validate();
                view.SetEquipment(planner.Catalog, draft.Equipment, !physicalPlanning);
                view.SetConstraints(draft.TotalMassKg, draft.MassLimitKg,
                    draft.TotalBudgetCredits, draft.BudgetLimitCredits,
                    draft.RequiredPowerWatts, draft.AvailablePowerWatts);
                view.SetPlanningResources(draft.InitialResources);
                view.SetValidation(validation.Succeeded, validation.Explanation, commandExplanation);
                view.SetCarriedEquipment(cargoLoading, planner.Catalog);
            }
            else
            {
                view.SetHudResources(resources.Snapshot);
            }
            RefreshTiming();
            RefreshEva();
        }

        private void RefreshTiming()
        {
            if (view == null) return;
            bool bound = state != null && planner != null;
            bool hasClock = bound && clock != null;
            bool hasDeadline = bound && deadline != null;
            view.SetSimulationTiming(hasClock, hasClock && clock.IsPaused, hasClock ? clock.TimeScale : 0);
            view.SetSimulationElapsedTime(hasClock, hasClock ? clock.ElapsedSimulationSeconds : 0);
            view.SetPlanningTiming(hasDeadline, hasDeadline && deadline.IsEnabled,
                hasDeadline ? deadline.RemainingRealSeconds : 0, hasClock && clock.IsPaused,
                hasDeadline ? deadline.LastValidConfiguration : null, bound ? planner.Catalog : null);
            if (physicalPlanning && bound && state.Phase == GamePhase.Planning && readPlanningInstruction != null)
                view.SetPhysicalPlanningInstruction(readPlanningInstruction());
            view.SetDeadlineFallback(hasDeadline && state.Phase != GamePhase.Planning
                && deadline.UsedLastValidLoadoutAtDeadline);
        }

        private void ChangeQuantity(string equipmentId, int delta)
        {
            if (physicalPlanning || planner == null || state == null || state.Phase != GamePhase.Planning) return;
            int quantity = 0;
            var draft = planner.Draft;
            foreach (var selection in draft.Equipment)
                if (selection.EquipmentId == equipmentId) quantity = selection.Quantity;

            if ((delta > 0 && quantity == int.MaxValue) || (delta < 0 && quantity == 0)) return;
            var result = planner.TrySetEquipment(equipmentId, quantity + delta);
            commandExplanation = result.Succeeded ? string.Empty : Explain(result);
            Refresh();
        }

        private void Launch()
        {
            if (physicalPlanning || planner == null || state == null || state.Phase != GamePhase.Planning) return;
            var validation = planner.Draft.Validate();
            if (!validation.Succeeded)
            {
                commandExplanation = Explain(validation);
                Refresh();
                return;
            }
            var result = planner.TryLaunch();
            commandExplanation = result.Succeeded ? string.Empty : Explain(result);
            Refresh();
        }

        private static string Explain(CommandResult result)
        {
            return string.IsNullOrWhiteSpace(result.Explanation)
                ? "This action is unavailable. Review the mission constraints."
                : result.Explanation;
        }

        private void OnPhaseChanged(PhaseChanged message)
        {
            commandExplanation = string.Empty;
            Refresh();
        }

        private void OnResourceChanged(ResourceChanged message)
        {
            if (view != null && state != null && state.Phase != GamePhase.Planning)
                view.SetHudResources(message.Snapshot);
        }

        private void RefreshObjective()
        {
            if (view == null || state == null) return;
            bool repairsComplete = state.Phase == GamePhase.Repair && state.Outcome == MissionOutcome.None
                && readInteriorSystemsOperational != null && readInteriorSystemsOperational();
            view.SetPhase(state.Phase, state.Outcome, repairsComplete);
        }

        private void OnSystemConditionChanged(SystemConditionChanged message)
        {
            if (disposed || !isActiveAndEnabled || !subscribed || state == null || state.Phase != GamePhase.Repair) return;
            if (message.SystemId == ShipSystemId.Power || message.SystemId == ShipSystemId.LifeSupport)
                RefreshObjective();
        }

        private void OnMissionEnded(MissionEnded message)
        {
            Refresh();
            if (view == null) return;
            view.SetHudResources(message.FinalResources);
            view.SetOutcomeReason(message.Reason);
        }
    }
}
