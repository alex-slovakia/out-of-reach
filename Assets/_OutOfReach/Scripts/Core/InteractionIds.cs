namespace OutOfReach.Core
{
    /// <summary>Stable interaction IDs. Scene targets select one of these; prompts and reports key on them.</summary>
    public static class InteractionIds
    {
        /// <summary>
        /// Cockpit console: the routine systems check that precedes the impact, and later, in Transmission, the
        /// single distress call to Earth.
        /// </summary>
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

        /// <summary>At the science instrument's mount: dismantle it for spare parts (the science objective is lost).</summary>
        public const string SalvagePayload = "salvage-payload";

        /// <summary>Compact workbench: build a CO2 absorber cartridge from a spare part and energy.</summary>
        public const string Workbench = "workbench";

        /// <summary>Fixed slot on the life-support unit: install a CO2 absorber cartridge.</summary>
        public const string AbsorberBay = "absorber-bay";

        /// <summary>Optional post-contact actions, registered only when the survival extension is enabled.</summary>
        public const string ConsumeRation = "survival-ration";
        public const string DrinkWater = "survival-water";
        public const string BuildGrowModule = "grow-build";
        public const string PlantSeedlings = "grow-plant";
        public const string HarvestCrop = "grow-harvest";

        /// <summary>Prefix of a type's supply pallet during preflight: "cargo-supply:" + equipment ID.</summary>
        public const string CargoSupplyPrefix = "cargo-supply:";

        /// <summary>Prefix of a type's stack in the cargo bay during preflight: "cargo-bay:" + equipment ID.</summary>
        public const string CargoBayPrefix = "cargo-bay:";

        /// <summary>Pallet of one equipment type: pick a crate up, or put the carried crate of that type back.</summary>
        public static string CargoSupply(string equipmentId)
        {
            return CargoSupplyPrefix + equipmentId;
        }

        /// <summary>Bay stack of one equipment type: load the carried crate of that type, or take one out.</summary>
        public static string CargoBay(string equipmentId)
        {
            return CargoBayPrefix + equipmentId;
        }
    }
}
