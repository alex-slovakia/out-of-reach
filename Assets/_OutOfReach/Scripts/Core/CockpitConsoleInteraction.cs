using System;
using OutOfReach.Contracts;

namespace OutOfReach.Core
{
    /// <summary>
    /// The cockpit console (implements <see cref="IInteractable"/>). During Intro it completes the routine systems
    /// check, which triggers the scripted impact exactly once. Afterwards it only reports the communications
    /// state until C04 adds the transmission.
    /// </summary>
    public sealed class CockpitConsoleInteraction : IInteractable
    {
        private readonly MissionDirector director;
        private readonly MissionGameState state;

        public CockpitConsoleInteraction(MissionDirector director, MissionGameState state)
        {
            this.director = director ?? throw new ArgumentNullException(nameof(director));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
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
                        return "Console offline";
                    default:
                        return director.Systems.Antenna.IsOperational
                            ? "Communications standing by"
                            : "Communications offline: antenna damaged";
                }
            }
        }

        public bool CanInteract => !state.IsClosed && state.Phase == GamePhase.Intro && !director.AccidentTriggered;

        public CommandResult TryInteract()
        {
            return director.TryCompleteCockpitCheck();
        }
    }
}
