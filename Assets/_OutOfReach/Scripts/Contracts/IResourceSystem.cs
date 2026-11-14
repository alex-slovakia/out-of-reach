using System.Collections.Generic;

namespace OutOfReach.Contracts
{
    public interface IResourceSystem
    {
        ResourceSnapshot Snapshot { get; }
        double GetQuantity(ResourceId resource);

        /// <summary>
        /// Validate the full request first, aggregate duplicate resource entries, then debit atomically.
        /// Reject negative/non-finite quantities and fractional spare parts with no mutation or success events.
        /// CO2 is an atmospheric state, not a spendable inventory item.
        /// </summary>
        CommandResult TryConsume(IReadOnlyList<ResourceAmount> costs);

        /// <summary>
        /// Core supplies finite non-negative simulated seconds once per step. Implementations reject invalid
        /// time before mutation, remain deterministic, and clamp depletion without creating negative reserves.
        /// W * seconds / 3,600,000 = kWh. No internal Unity Time reads.
        /// </summary>
        void AdvanceTime(double simulationSeconds);
    }
}
