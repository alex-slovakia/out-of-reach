using System.Collections.Generic;
using System.Globalization;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Simulation;
using UnityEngine;

namespace OutOfReach.Tests.PlayMode
{
    /// <summary>
    /// TEST FIXTURE, not production UI. A presentation-style listener that follows the documented binding pattern:
    /// read current state, subscribe when enabled, unsubscribe when disabled, and rebind when the runtime starts a
    /// new session. Tests use its counters; the harness scene also uses its plain debug overlay so the repair loop
    /// can be played by hand.
    /// </summary>
    public sealed class MissionStatusListener : MonoBehaviour
    {
        [SerializeField] private MissionRuntime runtime;
        [SerializeField] private InteractionResolver resolver;
        [SerializeField] private bool drawOverlay;

        private IMissionEventBus boundBus;
        private string lastInteraction = string.Empty;
        private Vector2 reportScroll;

        public int PhaseEvents { get; private set; }

        public int ResourceEvents { get; private set; }

        public List<SystemConditionChanged> SystemConditions { get; } = new List<SystemConditionChanged>();

        public List<PhaseChanged> Phases { get; } = new List<PhaseChanged>();

        public bool IsBound => boundBus != null;

        public void Configure(MissionRuntime missionRuntime, InteractionResolver interactionResolver, bool overlay)
        {
            runtime = missionRuntime;
            resolver = interactionResolver;
            drawOverlay = overlay;
        }

        private void OnEnable()
        {
            if (runtime == null)
            {
                return;
            }

            runtime.SessionStarted += OnSessionStarted;
            runtime.SessionEnding += OnSessionEnding;
            if (runtime.Session != null)
            {
                Bind(runtime.Session.Events);
            }

            if (resolver != null)
            {
                resolver.Interacted += OnInteracted;
            }
        }

        private void OnDisable()
        {
            Unbind();
            if (runtime != null)
            {
                runtime.SessionStarted -= OnSessionStarted;
                runtime.SessionEnding -= OnSessionEnding;
            }

            if (resolver != null)
            {
                resolver.Interacted -= OnInteracted;
            }
        }

        private void OnSessionStarted(MissionSession session)
        {
            Bind(session.Events);
        }

        private void OnSessionEnding(MissionSession session)
        {
            Unbind();
        }

        private void Bind(IMissionEventBus bus)
        {
            Unbind();
            boundBus = bus;
            bus.PhaseChanged += OnPhaseChanged;
            bus.ResourceChanged += OnResourceChanged;
            bus.SystemConditionChanged += OnSystemConditionChanged;
        }

        private void Unbind()
        {
            if (boundBus == null)
            {
                return;
            }

            boundBus.PhaseChanged -= OnPhaseChanged;
            boundBus.ResourceChanged -= OnResourceChanged;
            boundBus.SystemConditionChanged -= OnSystemConditionChanged;
            boundBus = null;
        }

        private void OnPhaseChanged(PhaseChanged message)
        {
            PhaseEvents++;
            Phases.Add(message);
        }

        private void OnResourceChanged(ResourceChanged message)
        {
            ResourceEvents++;
        }

        private void OnSystemConditionChanged(SystemConditionChanged message)
        {
            SystemConditions.Add(message);
        }

        private void OnInteracted(CommandResult result)
        {
            lastInteraction = (result.Succeeded ? "OK: " : "Rejected: ") + result.Explanation;
        }

        private void Update()
        {
            // Hand-play conveniences for the harness scene only: no planner UI exists here.
            if (!drawOverlay || runtime == null || runtime.Session == null)
            {
                return;
            }

            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (runtime.Session.State.Phase == GamePhase.Planning && keyboard.enterKey.wasPressedThisFrame)
            {
                runtime.Session.Planner.TryLaunch();
            }

            if (runtime.Session.State.Phase == GamePhase.Planning && keyboard.tKey.wasPressedThisFrame)
            {
                runtime.BeginTimedPlanning();
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                runtime.RestartMission();
            }
        }

        private void OnGUI()
        {
            if (!drawOverlay || runtime == null || runtime.Session == null)
            {
                return;
            }

            MissionSession session = runtime.Session;
            ResourceSnapshot r = session.Resources.Snapshot;
            CultureInfo c = CultureInfo.InvariantCulture;
            var lines = new List<string>
            {
                "OUT OF REACH - gameplay harness (test fixture, not game UI)",
                "Enter or the cockpit console: launch | T: start the 60 s timer | WASD + mouse | E: interact | Esc: pause | R: restart",
                string.Format(c, "Phase {0} | time x{1:0} | elapsed {2:0.0} h{3}", session.State.Phase, session.Clock.TimeScale,
                    session.State.ElapsedSimulationSeconds / 3600d, session.Clock.IsPaused ? " | PAUSED" : string.Empty),
                string.Format(c, "O2 {0:0.00} kg | water {1:0.0} L | food {2:0.0} kg | battery {3:0.00} kWh | parts {4} | CO2 {5:0} ppm",
                    r.OxygenKg, r.WaterLitres, r.FoodKg, r.BatteryKWh, r.SparePartsCount, r.CarbonDioxidePpm),
                string.Format(c, "Power {0:P0} | life support {1:P0} | antenna {2:P0}{3}", session.Systems.Power.Condition01,
                    session.Systems.LifeSupport.Condition01, session.Systems.Antenna.Condition01, session.Director.IsBrownout ? " | BROWNOUT" : string.Empty),
            };
            EvaOperations eva = session.Eva;
            lines.Add(string.Format(c, "Airlock {0}{1} | suit {2}{3}{4}", eva.Airlock,
                eva.AirlockTransitionRemainingSeconds > 0d ? string.Format(c, " ({0:0} s)", eva.AirlockTransitionRemainingSeconds) : string.Empty,
                eva.IsSuitOn ? "on" : "off",
                eva.IsSuitOn ? string.Format(c, " {0:0.00} kg O2 ({1:0} min)", eva.SuitOxygenKg, eva.SuitOxygenSecondsRemaining / 60d) : string.Empty,
                eva.IsOutside ? " | OUTSIDE on the tether" : string.Empty));
            if (session.State.Phase == GamePhase.Planning)
            {
                OutOfReach.Inventory.MissionConfiguration draft = session.Planner.DraftConfiguration;
                lines.Add(string.Format(c, "Loading: payload {0:0}/{1:0} kg | {2:0}/{3:0} credits | {4:0}/{5:0} W | in hands: {6}{7}",
                    draft.TotalMassKg, draft.MassLimitKg, draft.TotalBudgetCredits, draft.BudgetLimitCredits,
                    draft.RequiredPowerWatts, draft.AvailablePowerWatts, session.Cargo.CarriedEquipmentId ?? "nothing",
                    session.PlanningDeadline.IsEnabled ? string.Format(c, " | launch in {0:0} s", session.PlanningDeadline.RemainingRealSeconds) : string.Empty));
                OutOfReach.Contracts.CommandResult valid = draft.Validate();
                if (!valid.Succeeded)
                {
                    lines.Add("NOT READY: " + valid.Explanation);
                }
            }

            CommunicationSystem link = session.Communications;
            lines.Add(string.Format(c, "Day {0} ({1}) | link {2} | Earth {3}, one-way {4}{5}", session.Calendar.DayNumber,
                MissionCalendar.Format(session.Calendar.NowUtc), link.State, CommunicationSystem.FormatDistance(link.ReferenceDistanceKm),
                CommunicationSystem.FormatDuration(link.OneWayLightSeconds),
                link.State == CommunicationState.AwaitingReply
                    ? " | reply in " + CommunicationSystem.FormatDuration(link.ReplyRemainingSimulationSeconds, true) + " simulated"
                    : string.Empty));
            if (session.State.Outcome != MissionOutcome.None)
            {
                lines.Add("OUTCOME " + session.State.Outcome + ": " + session.State.OutcomeReason);
            }

            if (resolver != null && resolver.CurrentPrompt.Length > 0)
            {
                lines.Add((resolver.CurrentCanInteract ? "[E] " : "[unavailable] ") + resolver.CurrentPrompt);
            }

            if (lastInteraction.Length > 0)
            {
                lines.Add(lastInteraction);
            }

            float height = 24f * lines.Count + 12f;
            GUI.Label(new Rect(12f, 12f, Screen.width - 24f, height), string.Join("\n", lines));

            MissionReport report = session.State.Phase == GamePhase.Report ? runtime.Report : null;
            if (report == null)
            {
                return;
            }

            string text = report.ToPlainText();
            var view = new Rect(12f, height + 24f, Screen.width - 24f, Screen.height - height - 36f);
            float contentHeight = GUI.skin.label.CalcHeight(new GUIContent(text), view.width - 24f);
            reportScroll = GUI.BeginScrollView(view, reportScroll, new Rect(0f, 0f, view.width - 24f, contentHeight));
            GUI.Label(new Rect(0f, 0f, view.width - 24f, contentHeight), text);
            GUI.EndScrollView();
        }
    }
}
