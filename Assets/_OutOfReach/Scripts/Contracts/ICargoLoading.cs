using System;

namespace OutOfReach.Contracts
{
    /// <summary>
    /// Read-only view of Core's optional physical preflight loading. The planner draft remains
    /// the only aboard inventory; presentation sends interactions through the existing targets.
    /// </summary>
    public interface ICargoLoading
    {
        /// <summary>Catalog equipment ID currently carried, or null when hands are empty.</summary>
        string CarriedEquipmentId { get; }

        /// <summary>Authoritative dimensionless movement multiplier, 1 when hands are empty.</summary>
        float CarrySpeedFactor { get; }

        /// <summary>Number of this catalog item's crates still available at the supply station.</summary>
        int GetSupplyRemaining(string equipmentId);

        event Action Changed;
    }
}
