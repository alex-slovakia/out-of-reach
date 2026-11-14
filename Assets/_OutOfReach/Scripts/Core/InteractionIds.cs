namespace OutOfReach.Core
{
    /// <summary>Stable interaction IDs. Scene targets select one of these; prompts and reports key on them.</summary>
    public static class InteractionIds
    {
        /// <summary>Cockpit console: the routine systems check that precedes the impact (transmission arrives in C04).</summary>
        public const string CockpitConsole = "cockpit-console";

        public const string RepairPower = "repair-power";

        public const string RepairLifeSupport = "repair-life-support";

        /// <summary>Exterior antenna repair point; reachable only from the tethered route during an EVA.</summary>
        public const string RepairAntenna = "repair-antenna";

        /// <summary>Suit stowage: puts the EVA suit on (oxygen cost and preparation time).</summary>
        public const string SuitLocker = "suit-locker";

        /// <summary>Panel inside the airlock: seal and depressurize, later close the outer hatch and repressurize.</summary>
        public const string AirlockControls = "airlock-controls";

        /// <summary>At the open outer hatch, inside: clip the tether and go outside.</summary>
        public const string EvaExit = "eva-exit";

        /// <summary>At the outer hatch, outside: enter the airlock.</summary>
        public const string EvaReturn = "eva-return";
    }
}
