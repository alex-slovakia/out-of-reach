namespace OutOfReach.Core
{
    /// <summary>Stable interaction IDs. Scene targets select one of these; prompts and reports key on them.</summary>
    public static class InteractionIds
    {
        /// <summary>Cockpit console: the routine systems check that precedes the impact (transmission arrives in C04).</summary>
        public const string CockpitConsole = "cockpit-console";

        public const string RepairPower = "repair-power";

        public const string RepairLifeSupport = "repair-life-support";
    }
}
