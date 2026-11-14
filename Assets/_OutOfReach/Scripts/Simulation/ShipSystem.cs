using System;
using System.Collections.Generic;
using System.Globalization;
using OutOfReach.Contracts;

namespace OutOfReach.Simulation
{
    /// <summary>
    /// One repairable ship system (implements <see cref="IRepairable"/>). Condition is dimensionless: 0 failed,
    /// 1 fully operational. A repair revalidates prerequisites and costs, debits all costs atomically through
    /// <see cref="IResourceSystem.TryConsume"/> and restores the condition in one synchronous transaction; a rejected
    /// or repeated repair spends nothing and publishes nothing. Core supplies the prerequisite check (phase gates),
    /// the transaction scope (so observers see the whole repair at once) and the after-repair hook.
    /// </summary>
    public sealed class ShipSystem : IRepairable
    {
        /// <summary>Conditions at or above this count as operational (absorbs float dust).</summary>
        public const double OperationalThreshold = 0.999999;

        private readonly IMissionEventBus events;
        private readonly Func<ShipSystem, CommandResult> prerequisites;
        private readonly Func<IDisposable> beginTransaction;
        private readonly Action<ShipSystem> afterRepair;

        public ShipSystem(
            ShipSystemId systemId,
            string displayName,
            IEnumerable<ResourceAmount> repairCosts,
            IMissionEventBus events,
            Func<ShipSystem, CommandResult> prerequisites = null,
            Func<IDisposable> beginTransaction = null,
            Action<ShipSystem> afterRepair = null)
        {
            if (!Enum.IsDefined(typeof(ShipSystemId), systemId))
            {
                throw new ArgumentOutOfRangeException(nameof(systemId), systemId, "Unknown ship system.");
            }

            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.prerequisites = prerequisites;
            this.beginTransaction = beginTransaction;
            this.afterRepair = afterRepair;
            SystemId = systemId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? systemId.ToString() : displayName;

            var costs = new List<ResourceAmount>();
            if (repairCosts != null)
            {
                foreach (ResourceAmount cost in repairCosts)
                {
                    bool invalid = !Enum.IsDefined(typeof(ResourceId), cost.Resource)
                        || double.IsNaN(cost.Quantity) || double.IsInfinity(cost.Quantity) || cost.Quantity < 0d
                        || (cost.Resource == ResourceId.SparePartsCount && cost.Quantity != Math.Floor(cost.Quantity));
                    if (invalid)
                    {
                        throw new ArgumentException($"Invalid repair cost for {systemId}: {cost.Resource} {cost.Quantity}.", nameof(repairCosts));
                    }

                    costs.Add(cost);
                }
            }

            RepairCosts = Array.AsReadOnly(costs.ToArray());
        }

        public ShipSystemId SystemId { get; }

        public string DisplayName { get; }

        public double Condition01 { get; private set; } = 1d;

        public IReadOnlyList<ResourceAmount> RepairCosts { get; }

        public bool IsOperational => Condition01 >= OperationalThreshold;

        /// <summary>True once the accident (or anything else) has damaged this system at least once.</summary>
        public bool WasDamaged { get; private set; }

        /// <summary>Number of completed repairs (0 or 1 in the slice).</summary>
        public int RepairCount { get; private set; }

        /// <summary>Human-readable cost, for example "2 spare parts, 1 kWh".</summary>
        public string DescribeCosts()
        {
            var parts = new List<string>();
            foreach (ResourceAmount cost in RepairCosts)
            {
                if (cost.Quantity > 0d)
                {
                    parts.Add(Describe(cost.Resource, cost.Quantity));
                }
            }

            return parts.Count == 0 ? "no materials" : string.Join(", ", parts);
        }

        /// <summary>
        /// Authored damage (the accident). Lowers the condition and publishes SystemConditionChanged once.
        /// Values that would not lower the condition are rejected, so damage cannot be applied twice by mistake.
        /// </summary>
        public CommandResult TryDamage(double condition01)
        {
            if (double.IsNaN(condition01) || condition01 < 0d || condition01 > 1d)
            {
                return Fail(CommandFailure.InvalidInput, "Condition must be between 0 and 1.");
            }

            if (condition01 >= Condition01)
            {
                return Fail(CommandFailure.AlreadyComplete, $"{DisplayName} is already at or below that condition.");
            }

            Condition01 = condition01;
            WasDamaged = true;
            events.Publish(new SystemConditionChanged(SystemId, Condition01));
            return Ok($"{DisplayName} damaged.");
        }

        /// <summary>Everything <see cref="TryRepair"/> would check, without changing anything.</summary>
        public CommandResult CheckRepair(IResourceSystem resources)
        {
            if (resources == null)
            {
                return Fail(CommandFailure.InvalidInput, "A resource system is required.");
            }

            if (IsOperational)
            {
                return Fail(CommandFailure.AlreadyComplete, $"{DisplayName} is operational.");
            }

            if (prerequisites != null)
            {
                CommandResult allowed = prerequisites(this);
                if (!allowed.Succeeded)
                {
                    return allowed;
                }
            }

            string shortfall = FindShortfall(resources);
            if (shortfall != null)
            {
                return Fail(CommandFailure.InsufficientResources, $"Cannot repair {DisplayName}: {shortfall}.");
            }

            return Ok($"Repair {DisplayName}: {DescribeCosts()}.");
        }

        public CommandResult TryRepair(IResourceSystem resources)
        {
            CommandResult check = CheckRepair(resources);
            if (!check.Succeeded)
            {
                return check;
            }

            IDisposable transaction = beginTransaction?.Invoke();
            try
            {
                CommandResult spent = resources.TryConsume(RepairCosts);
                if (!spent.Succeeded)
                {
                    return spent;
                }

                Condition01 = 1d;
                RepairCount++;
                events.Publish(new SystemConditionChanged(SystemId, Condition01));
                afterRepair?.Invoke(this);
            }
            finally
            {
                transaction?.Dispose();
            }

            return Ok($"{DisplayName} repaired ({DescribeCosts()}).");
        }

        private string FindShortfall(IResourceSystem resources)
        {
            var needed = new Dictionary<ResourceId, double>();
            foreach (ResourceAmount cost in RepairCosts)
            {
                needed.TryGetValue(cost.Resource, out double existing);
                needed[cost.Resource] = existing + cost.Quantity;
            }

            var missing = new List<string>();
            foreach (ResourceAmount cost in RepairCosts)
            {
                if (!needed.TryGetValue(cost.Resource, out double total))
                {
                    continue;
                }

                needed.Remove(cost.Resource);
                double available = resources.GetQuantity(cost.Resource);
                if (total > available + ResourceLedger.AffordabilityTolerance)
                {
                    missing.Add($"needs {Describe(cost.Resource, total)}, {Describe(cost.Resource, available)} available");
                }
            }

            return missing.Count == 0 ? null : string.Join("; ", missing);
        }

        private static string Describe(ResourceId resource, double quantity)
        {
            switch (resource)
            {
                case ResourceId.SparePartsCount:
                    return string.Format(CultureInfo.InvariantCulture, "{0:0} spare part{1}", quantity, quantity == 1d ? string.Empty : "s");
                case ResourceId.BatteryKWh:
                    return string.Format(CultureInfo.InvariantCulture, "{0:0.##} kWh", quantity);
                case ResourceId.OxygenKg:
                    return string.Format(CultureInfo.InvariantCulture, "{0:0.##} kg oxygen", quantity);
                case ResourceId.WaterLitres:
                    return string.Format(CultureInfo.InvariantCulture, "{0:0.##} L water", quantity);
                case ResourceId.FoodKg:
                    return string.Format(CultureInfo.InvariantCulture, "{0:0.##} kg food", quantity);
                default:
                    return string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", quantity, resource);
            }
        }

        private static CommandResult Ok(string explanation)
        {
            return new CommandResult(true, CommandFailure.None, explanation);
        }

        private static CommandResult Fail(CommandFailure failure, string explanation)
        {
            return new CommandResult(false, failure, explanation);
        }
    }
}
