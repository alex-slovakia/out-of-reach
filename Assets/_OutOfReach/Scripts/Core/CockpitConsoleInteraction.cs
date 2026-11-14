using System;
using System.Globalization;
using OutOfReach.Contracts;
using OutOfReach.Inventory;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// The cockpit console (implements <see cref="IInteractable"/>). During Intro it completes the routine systems
    /// check, which triggers the scripted impact exactly once. After the impact it is the communications panel:
    /// its prompt says why the link is unavailable. In Transmission the first use runs the link diagnostic (the
    /// false signal: an old local recording, not a reply), the second sends the single distress call (the prompt
    /// gives its energy cost, Earth's distance and the earliest reply), and then the prompt shows the remaining wait.
    /// With the physical preflight on, the console also launches the mission during Planning: its prompt shows the
    /// loadout totals, or the exact violation that blocks the launch. Once Earth's reply is in and the mission goes
    /// on (the optional post-contact Survival chapter), it reports the restored contact and offers nothing to send.
    /// </summary>
    public sealed class CockpitConsoleInteraction : IInteractable
    {
        /// <summary>Prompt after Earth's reply while the mission continues: the one distress call has been answered.</summary>
        public const string ContactRestoredPrompt = "Contact with Earth restored: Earth's reply has been received";

        private readonly MissionDirector director;
        private readonly MissionGameState state;
        private readonly CommunicationSystem communications;
        private readonly MissionPlanner planner;
        private readonly bool launchesFromConsole;

        public CockpitConsoleInteraction(MissionDirector director, MissionGameState state, CommunicationSystem communications,
            MissionPlanner planner = null, bool launchesFromConsole = false)
        {
            this.director = director ?? throw new ArgumentNullException(nameof(director));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.communications = communications ?? throw new ArgumentNullException(nameof(communications));
            this.planner = planner;
            this.launchesFromConsole = launchesFromConsole && planner != null;
        }

        public string InteractionId => InteractionIds.CockpitConsole;

        public string Prompt
        {
            get
            {
                switch (state.Phase)
                {
                    case GamePhase.Planning:
                        return launchesFromConsole ? DescribeLaunch() : "Console on standby until launch";
                    case GamePhase.Intro:
                        return director.AccidentTriggered ? "Impact detected" : "Run cockpit systems check";
                    case GamePhase.Outcome:
                    case GamePhase.Report:
                        return state.Outcome == MissionOutcome.RecoveryPossible ? "Contact with Earth restored" : "Console offline";
                    default:
                        if (IsContactRestored)
                        {
                            return ContactRestoredPrompt;
                        }

                        if (communications.State == CommunicationState.AwaitingReply)
                        {
                            return communications.IsReplyHeldByUnpoweredLink
                                ? "Reply waiting: the link has no power"
                                : "Awaiting reply from Earth: " + CommunicationSystem.FormatDuration(communications.ReplyRemainingSimulationSeconds, true) + " (simulated)";
                        }

                        return IsDiagnosticNext ? director.CheckLinkDiagnostic().Explanation : communications.CheckTransmit().Explanation;
                }
            }
        }

        private string DescribeLaunch()
        {
            MissionConfiguration draft = planner.DraftConfiguration;
            CommandResult valid = draft.Validate();
            return valid.Succeeded
                ? string.Format(CultureInfo.InvariantCulture, "Begin mission: {0:0} of {1:0} kg, {2:0} of {3:0} credits",
                    draft.TotalMassKg, draft.MassLimitKg, draft.TotalBudgetCredits, draft.BudgetLimitCredits)
                : valid.Explanation;
        }

        /// <summary>True while the next console action in Transmission is the one-time link diagnostic.</summary>
        private bool IsDiagnosticNext => state.Phase == GamePhase.Transmission && !director.LinkDiagnosticRequested;

        /// <summary>True while the mission runs after Earth's reply: there is nothing left to send.</summary>
        private bool IsContactRestored => !state.IsClosed && communications.State == CommunicationState.ReplyReceived;

        public bool CanInteract
        {
            get
            {
                if (state.IsClosed || IsContactRestored)
                {
                    return false;
                }

                if (state.Phase == GamePhase.Intro)
                {
                    return !director.AccidentTriggered;
                }

                if (state.Phase == GamePhase.Planning)
                {
                    return launchesFromConsole && planner.DraftConfiguration.Validate().Succeeded;
                }

                return IsDiagnosticNext ? director.CheckLinkDiagnostic().Succeeded : communications.CheckTransmit().Succeeded;
            }
        }

        public CommandResult TryInteract()
        {
            if (state.Phase == GamePhase.Planning && launchesFromConsole)
            {
                return planner.TryLaunch();
            }

            if (state.Phase == GamePhase.Intro || state.Phase == GamePhase.Planning)
            {
                return director.TryCompleteCockpitCheck();
            }

            if (IsContactRestored)
            {
                return new CommandResult(false, CommandFailure.AlreadyComplete,
                    "Contact with Earth is already restored: the one distress call has been answered.");
            }

            return IsDiagnosticNext ? director.TryRunLinkDiagnostic() : communications.TryTransmit();
        }
    }
}
