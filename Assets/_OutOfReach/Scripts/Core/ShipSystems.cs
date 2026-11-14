using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>The mission's three repairable systems, addressable by ID. Created and owned by the director.</summary>
    public sealed class ShipSystems
    {
        private readonly Dictionary<ShipSystemId, ShipSystem> byId = new Dictionary<ShipSystemId, ShipSystem>();

        internal ShipSystems(IEnumerable<ShipSystem> systems)
        {
            var ordered = new List<ShipSystem>();
            foreach (ShipSystem system in systems)
            {
                byId.Add(system.SystemId, system);
                ordered.Add(system);
            }

            All = ordered.AsReadOnly();
            Repairables = Array.AsReadOnly(ToRepairables(ordered));
        }

        public IReadOnlyList<ShipSystem> All { get; }

        /// <summary>Contract view for diagnostics and repair presentation.</summary>
        public IReadOnlyList<IRepairable> Repairables { get; }

        public ShipSystem Power => byId[ShipSystemId.Power];

        public ShipSystem LifeSupport => byId[ShipSystemId.LifeSupport];

        public ShipSystem Antenna => byId[ShipSystemId.Antenna];

        public ShipSystem Get(ShipSystemId systemId)
        {
            if (!byId.TryGetValue(systemId, out ShipSystem system))
            {
                throw new ArgumentOutOfRangeException(nameof(systemId), systemId, "Unknown ship system.");
            }

            return system;
        }

        private static IRepairable[] ToRepairables(List<ShipSystem> systems)
        {
            var result = new IRepairable[systems.Count];
            for (int i = 0; i < systems.Count; i++)
            {
                result[i] = systems[i];
            }

            return result;
        }
    }
}
