namespace OutOfReach.Contracts
{
    public enum GamePhase { Planning, Intro, Emergency, Repair, Eva, Transmission, Outcome, Report, Survival }
    public enum MissionOutcome { None, RecoveryPossible, Lost }
    public enum ShipSystemId { Power, LifeSupport, Antenna }
    public enum ResourceId { OxygenKg, WaterLitres, FoodKg, BatteryKWh, SparePartsCount }
    public enum CommunicationState { Offline, Ready, AwaitingReply, ReplyReceived }
    public enum CinematicId { Impact, FalseSignal, CommunicationRestored, EndingPullback }
    public enum AirlockState { InnerOpen, Sealed, Depressurizing, OuterOpen, Repressurizing }
    public enum CommandFailure { None, InvalidInput, WrongPhase, PreconditionsNotMet, InsufficientResources, AlreadyComplete }

    /// <summary>A rejected command leaves all authoritative state unchanged.</summary>
    public readonly struct CommandResult
    {
        public bool Succeeded { get; }
        public CommandFailure Failure { get; }
        public string Explanation { get; }

        public CommandResult(bool succeeded, CommandFailure failure, string explanation)
        {
            Succeeded = succeeded;
            Failure = failure;
            Explanation = explanation;
        }
    }

    /// <summary>Units are part of ResourceId; SparePartsCount must be an integer.</summary>
    public readonly struct ResourceAmount
    {
        public ResourceId Resource { get; }
        public double Quantity { get; }

        public ResourceAmount(ResourceId resource, double quantity)
        {
            Resource = resource;
            Quantity = quantity;
        }
    }

    /// <summary>Stored reserves, not blood oxygen or cabin oxygen percentage. CO2 is cabin ppm.</summary>
    public readonly struct ResourceSnapshot
    {
        public double OxygenKg { get; }
        public double WaterLitres { get; }
        public double FoodKg { get; }
        public double BatteryKWh { get; }
        public int SparePartsCount { get; }
        public double CarbonDioxidePpm { get; }

        public ResourceSnapshot(double oxygenKg, double waterLitres, double foodKg,
            double batteryKWh, int sparePartsCount, double carbonDioxidePpm)
        {
            OxygenKg = oxygenKg;
            WaterLitres = waterLitres;
            FoodKg = foodKg;
            BatteryKWh = batteryKWh;
            SparePartsCount = sparePartsCount;
            CarbonDioxidePpm = carbonDioxidePpm;
        }
    }

    /// <summary>Frozen catalog values. Budget credits are fictional, not aerospace cost estimates.</summary>
    public readonly struct EquipmentSelection
    {
        public string EquipmentId { get; }
        public int Quantity { get; }
        public double UnitMassKg { get; }
        public double UnitPowerWatts { get; }
        public double UnitBudgetCredits { get; }

        public EquipmentSelection(string equipmentId, int quantity, double unitMassKg,
            double unitPowerWatts, double unitBudgetCredits)
        {
            EquipmentId = equipmentId;
            Quantity = quantity;
            UnitMassKg = unitMassKg;
            UnitPowerWatts = unitPowerWatts;
            UnitBudgetCredits = unitBudgetCredits;
        }
    }
}
