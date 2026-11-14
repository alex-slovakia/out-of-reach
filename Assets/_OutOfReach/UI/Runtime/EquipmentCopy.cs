namespace OutOfReach.Presentation.UI
{
    /// <summary>Presentation text for the stable C01 IDs. Numeric effects remain domain-owned.</summary>
    internal static class EquipmentCopy
    {
        public static string Name(string id)
        {
            switch (id)
            {
                case "oxygen-tank": return "Oxygen reserve tank";
                case "water-tank": return "Water reserve tank";
                case "food-locker": return "Food locker";
                case "battery-module": return "Battery module";
                case "solar-wing": return "Solar wing";
                case "spare-parts-kit": return "Spare parts kit";
                case "spectrometer": return "Research spectrometer";
                default: return id ?? "Equipment";
            }
        }

        public static string Description(string id)
        {
            switch (id)
            {
                case "oxygen-tank": return "Adds stored oxygen for life support.";
                case "water-tank": return "Adds water to the mission reserves.";
                case "food-locker": return "Adds packaged provisions.";
                case "battery-module": return "Stores energy for periods without generation.";
                case "solar-wing": return "Adds power generation capacity.";
                case "spare-parts-kit": return "Provides parts for repairs.";
                case "spectrometer": return "Supports research and adds an electrical load.";
                default: return "Review the updated mission totals after selection.";
            }
        }
    }
}
