namespace OutOfReach.Core
{
    /// <summary>Stable IDs of the gameplay gates the mission controls.</summary>
    public static class GateIds
    {
        /// <summary>Between the cabin and the airlock: walkable only while the airlock state is InnerOpen.</summary>
        public const string AirlockInner = "airlock-inner";

        /// <summary>
        /// Between the airlock and vacuum: never walkable. The outer hatch is crossed only through the eva-exit and
        /// eva-return interactions, which need a sealed suit and an open hatch.
        /// </summary>
        public const string AirlockOuter = "airlock-outer";
    }
}
