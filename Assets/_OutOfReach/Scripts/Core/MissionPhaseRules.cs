using OutOfReach.Contracts;

namespace OutOfReach.Core
{
    /// <summary>
    /// The mission's legal phase graph. Planning to Intro happens only through a validated launch, and every
    /// running phase may end in Outcome, which only Core decides. Report is terminal: a restart builds a new
    /// <see cref="MissionSession"/> instead of rewinding this one, so no state can leak between missions.
    /// </summary>
    public static class MissionPhaseRules
    {
        public static bool IsLegal(GamePhase from, GamePhase to)
        {
            switch (from)
            {
                case GamePhase.Planning:
                    return to == GamePhase.Intro;
                case GamePhase.Intro:
                    return to == GamePhase.Emergency || to == GamePhase.Outcome;
                case GamePhase.Emergency:
                    return to == GamePhase.Repair || to == GamePhase.Outcome;
                case GamePhase.Repair:
                    return to == GamePhase.Eva || to == GamePhase.Transmission || to == GamePhase.Outcome;
                case GamePhase.Eva:
                    return to == GamePhase.Repair || to == GamePhase.Transmission || to == GamePhase.Outcome;
                case GamePhase.Transmission:
                    return to == GamePhase.Outcome || to == GamePhase.Survival;
                case GamePhase.Survival:
                    return to == GamePhase.Outcome;
                case GamePhase.Outcome:
                    return to == GamePhase.Report;
                default:
                    return false;
            }
        }

        /// <summary>Phases in which the simulation clock advances. Planning, Outcome and Report are frozen.</summary>
        public static bool IsSimulationRunning(GamePhase phase)
        {
            return phase == GamePhase.Intro
                || phase == GamePhase.Emergency
                || phase == GamePhase.Repair
                || phase == GamePhase.Eva
                || phase == GamePhase.Transmission
                || phase == GamePhase.Survival;
        }
    }
}
