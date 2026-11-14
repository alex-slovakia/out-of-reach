using System.Collections.Generic;

namespace OutOfReach.Contracts
{
    public interface IRepairable
    {
        ShipSystemId SystemId { get; }
        /// <summary>0 = failed, 1 = fully operational; dimensionless condition, not failure probability.</summary>
        double Condition01 { get; }
        IReadOnlyList<ResourceAmount> RepairCosts { get; }

        /// <summary>
        /// Final repair command after interaction prerequisites. Revalidate prerequisites and costs;
        /// debit through TryConsume and restore condition in one synchronous transaction. Repeated
        /// completed repairs must not spend resources. Publish success only after the full transaction.
        /// </summary>
        CommandResult TryRepair(IResourceSystem resources);
    }
}
