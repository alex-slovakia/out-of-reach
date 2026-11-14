using System;
using OutOfReach.Contracts;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// The cockpit console (implements <see cref="IInteractable"/>). During Intro it completes the routine systems
    /// check, which triggers the scripted impact exactly once. After the impact it is the communications panel:
    /// its prompt says why the link is unavailable. In Transmission the first use runs the link diagnostic (the
    /// false signal: an old local recording, not a reply), the second sends the single distress call (the prompt
    /// gives its energy cost, Earth's distance and the earliest reply), and then the prompt shows the remaining wait.
    /// </summary>
    public sealed class CockpitConsoleInteraction : IInteractable
    {
        private readonly MissionDirector director;
        private readonly MissionGameState state;
        private readonly CommunicationSystem communications;

        public CockpitConsoleInteraction(MissionDirector director, MissionGameState state, CommunicationSystem communications)
        {
            this.director = director ?? throw new ArgumentNullException(nameof(director));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.communications = communications ?? throw new ArgumentNullException(nameof(communications));
        }

        public string InteractionId => InteractionIds.CockpitConsole;

        public string Prompt
        {
            get
            {
                switch (state.Phase)
                {
                    case GamePhase.Planning:
                        return "Console on standby until launch";
                    case GamePhase.Intro:
                        return director.AccidentTriggered ? "Impact detected" : "Run cockpit systems check";
                    case GamePhase.Outcome:
                    case GamePhase.Report:
                        return state.Outcome == MissionOutcome.RecoveryPossible ? "Contact with Earth restored" : "Console offline";
                    default:
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

        /// <summary>True while the next console action in Transmission is the one-time link diagnostic.</summary>
        private bool IsDiagnosticNext => state.Phase == GamePhase.Transmission && !director.LinkDiagnosticRequested;

        public bool CanInteract
        {
            get
            {
                if (state.IsClosed)
                {
                    return false;
                }

                if (state.Phase == GamePhase.Intro)
                {
                    return !director.AccidentTriggered;
                }

                return IsDiagnosticNext ? director.CheckLinkDiagnostic().Succeeded : communications.CheckTransmit().Succeeded;
            }
        }

        public CommandResult TryInteract()
        {
            if (state.Phase == GamePhase.Intro || state.Phase == GamePhase.Planning)
            {
                return director.TryCompleteCockpitCheck();
            }

            return IsDiagnosticNext ? director.TryRunLinkDiagnostic() : communications.TryTransmit();
        }
    }
}
