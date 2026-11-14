using System;
using System.Collections.Generic;
using System.Globalization;
using OutOfReach.Contracts;

namespace OutOfReach.Simulation
{
    /// <summary>
    /// Authoritative stored reserves (oxygen kg, water L, food kg, battery kWh, whole spare parts) and cabin
    /// CO2 concentration (ppm, an atmospheric state that is never spendable). Deterministic: time only moves
    /// through <see cref="AdvanceTime"/>, which Core calls once per simulation step. Rates are constant within
    /// a step, so integration is exact and a step crossing zero records the exact depletion time.
    /// Every rejected command leaves all quantities unchanged and publishes nothing.
    /// </summary>
    public sealed class ResourceLedger : IResourceSystem
    {
        /// <summary>Absolute slack when comparing a cost with a reserve, so float dust cannot block exact spending.</summary>
        public const double AffordabilityTolerance = 1e-9;

        /// <summary>A reserve at or below this after a debit or drain is exactly depleted (set to zero).</summary>
        public const double DepletionTolerance = 1e-12;

        /// <summary>Physical ceiling of a concentration expressed in parts per million.</summary>
        public const double MaximumCarbonDioxidePpm = 1000000d;

        private const int ResourceCount = 5;

        private readonly IMissionEventBus events;
        private readonly Func<bool> isMissionActive;
        private readonly double?[] depletedAt = new double?[ResourceCount];

        private double oxygenKg;
        private double waterLitres;
        private double foodKg;
        private double batteryKWh;
        private int spareParts;
        private double carbonDioxidePpm;
        private double carbonDioxideFloorPpm;
        private ResourceRates rates = ResourceRates.Zero;

        /// <param name="events">Bus that receives ResourceChanged after each committed change.</param>
        /// <param name="isMissionActive">
        /// Optional gate supplied by Core: true only while the mission clock may run. When it returns false, spending
        /// is rejected and time does not drain reserves, so this ledger and the game state can never disagree about
        /// frozen phases or an ended session, whoever calls. Omitted in isolated tests (always active).
        /// </param>
        public ResourceLedger(IMissionEventBus events, Func<bool> isMissionActive = null)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.isMissionActive = isMissionActive;
        }

        /// <summary>False until launch loads the frozen configuration's reserves.</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>Simulation seconds applied since launch.</summary>
        public double ElapsedSimulationSeconds { get; private set; }

        public double BatteryCapacityKWh { get; private set; }

        public ResourceRates Rates => rates;

        /// <summary>Lowest concentration CO2 removal can reach (cabin baseline), ppm. Rising CO2 is never clamped.</summary>
        public double CarbonDioxideFloorPpm => carbonDioxideFloorPpm;

        public ResourceSnapshot Snapshot => new ResourceSnapshot(oxygenKg, waterLitres, foodKg, batteryKWh, spareParts, carbonDioxidePpm);

        /// <summary>Loads launch reserves once. Publishes ResourceChanged on success.</summary>
        public CommandResult Initialize(ResourceSnapshot initial, double batteryCapacityKWh, ResourceRates initialRates)
        {
            if (IsInitialized)
            {
                return Fail(CommandFailure.AlreadyComplete, "Launch reserves were already loaded.");
            }

            string problem = FindSnapshotProblem(initial) ?? initialRates.FindProblem();
            if (problem == null && (!IsFinite(batteryCapacityKWh) || batteryCapacityKWh < initial.BatteryKWh))
            {
                problem = "Battery capacity must be finite and at least the initial charge.";
            }

            if (problem != null)
            {
                return Fail(CommandFailure.InvalidInput, problem);
            }

            oxygenKg = initial.OxygenKg;
            waterLitres = initial.WaterLitres;
            foodKg = initial.FoodKg;
            batteryKWh = initial.BatteryKWh;
            spareParts = initial.SparePartsCount;
            carbonDioxidePpm = initial.CarbonDioxidePpm;
            BatteryCapacityKWh = batteryCapacityKWh;
            rates = initialRates;
            ElapsedSimulationSeconds = 0d;
            Array.Clear(depletedAt, 0, depletedAt.Length);
            IsInitialized = true;
            events.Publish(new ResourceChanged(Snapshot));
            return Ok("Launch reserves loaded.");
        }

        public double GetQuantity(ResourceId resource)
        {
            switch (resource)
            {
                case ResourceId.OxygenKg:
                    return oxygenKg;
                case ResourceId.WaterLitres:
                    return waterLitres;
                case ResourceId.FoodKg:
                    return foodKg;
                case ResourceId.BatteryKWh:
                    return batteryKWh;
                case ResourceId.SparePartsCount:
                    return spareParts;
                default:
                    throw new ArgumentOutOfRangeException(nameof(resource), resource, "Unknown resource. Cabin CO2 is read from Snapshot.CarbonDioxidePpm.");
            }
        }

        /// <summary>Simulation time at which a reserve reached zero, or null while it is above zero.</summary>
        public double? GetDepletionTime(ResourceId resource)
        {
            if (!Enum.IsDefined(typeof(ResourceId), resource))
            {
                throw new ArgumentOutOfRangeException(nameof(resource), resource, "Unknown resource.");
            }

            return depletedAt[(int)resource];
        }

        /// <summary>Validates a cost list exactly like <see cref="TryConsume"/> without changing anything.</summary>
        public CommandResult CheckAffordable(IReadOnlyList<ResourceAmount> costs)
        {
            return Evaluate(costs, out _);
        }

        /// <summary>
        /// Validates every entry, aggregates duplicate resources, checks affordability of the totals, then debits
        /// all of them at once. Publishes exactly one ResourceChanged on a non-empty success.
        /// </summary>
        public CommandResult TryConsume(IReadOnlyList<ResourceAmount> costs)
        {
            CommandResult check = Evaluate(costs, out CostTotals totals);
            if (!check.Succeeded)
            {
                return check;
            }

            if (totals.IsZero)
            {
                return Ok("Nothing to consume.");
            }

            double now = ElapsedSimulationSeconds;
            oxygenKg = Debit(oxygenKg, totals.OxygenKg, ResourceId.OxygenKg, now);
            waterLitres = Debit(waterLitres, totals.WaterLitres, ResourceId.WaterLitres, now);
            foodKg = Debit(foodKg, totals.FoodKg, ResourceId.FoodKg, now);
            batteryKWh = Debit(batteryKWh, totals.BatteryKWh, ResourceId.BatteryKWh, now);
            if (totals.SpareParts > 0)
            {
                spareParts -= totals.SpareParts;
                if (spareParts == 0)
                {
                    MarkDepleted(ResourceId.SparePartsCount, now);
                }
            }

            events.Publish(new ResourceChanged(Snapshot));
            return Ok("Consumed " + totals.Describe() + ".");
        }

        /// <summary>
        /// Salvage: debits the costs and adds whole spare parts in one all-or-nothing step, publishing exactly one
        /// ResourceChanged. Spare parts are the only reserve that can ever be credited, and only by converting
        /// something that was carried; oxygen, water, food and energy cannot be created this way.
        /// </summary>
        public CommandResult TryExchangeForSpareParts(IReadOnlyList<ResourceAmount> costs, int sparePartsGained)
        {
            if (sparePartsGained <= 0)
            {
                return Fail(CommandFailure.InvalidInput, "A salvage must yield at least one spare part.");
            }

            CommandResult check = Evaluate(costs, out CostTotals totals);
            if (!check.Succeeded)
            {
                return check;
            }

            long partsAfter = (long)spareParts - totals.SpareParts + sparePartsGained;
            if (partsAfter > int.MaxValue)
            {
                return Fail(CommandFailure.InvalidInput, "The spare parts count is too large to represent.");
            }

            double now = ElapsedSimulationSeconds;
            oxygenKg = Debit(oxygenKg, totals.OxygenKg, ResourceId.OxygenKg, now);
            waterLitres = Debit(waterLitres, totals.WaterLitres, ResourceId.WaterLitres, now);
            foodKg = Debit(foodKg, totals.FoodKg, ResourceId.FoodKg, now);
            batteryKWh = Debit(batteryKWh, totals.BatteryKWh, ResourceId.BatteryKWh, now);
            spareParts = (int)partsAfter;
            depletedAt[(int)ResourceId.SparePartsCount] = null;
            events.Publish(new ResourceChanged(Snapshot));
            return Ok(string.Format(CultureInfo.InvariantCulture, "Salvaged {0} spare part(s).", sparePartsGained));
        }

        /// <summary>Replaces the constant rates used by later steps (for example after damage or a repair).</summary>
        public CommandResult TrySetRates(ResourceRates newRates)
        {
            if (!IsInitialized)
            {
                return Fail(CommandFailure.WrongPhase, "Rates apply only after launch.");
            }

            string problem = newRates.FindProblem();
            if (problem != null)
            {
                return Fail(CommandFailure.InvalidInput, problem);
            }

            rates = newRates;
            return Ok("Rates updated: " + newRates + ".");
        }

        /// <summary>Sets the baseline that scrubbing cannot go below. A concentration already lower is not raised.</summary>
        public CommandResult TrySetCarbonDioxideFloor(double floorPpm)
        {
            if (!IsFinite(floorPpm) || floorPpm < 0d)
            {
                return Fail(CommandFailure.InvalidInput, "The CO2 floor must be a finite, non-negative concentration.");
            }

            carbonDioxideFloorPpm = floorPpm;
            return Ok(string.Format(CultureInfo.InvariantCulture, "CO2 floor {0:0.#} ppm.", floorPpm));
        }

        /// <summary>
        /// Simulation seconds until the next reserve reaches zero under the current rates; +Infinity if none.
        /// Core uses this to split long steps exactly at depletion boundaries.
        /// </summary>
        public double TimeToNextDepletion()
        {
            if (!IsInitialized)
            {
                return double.PositiveInfinity;
            }

            double earliest = double.PositiveInfinity;
            earliest = Earliest(earliest, oxygenKg, rates.OxygenUseKgPerSecond);
            earliest = Earliest(earliest, waterLitres, rates.WaterUseLitresPerSecond);
            earliest = Earliest(earliest, foodKg, rates.FoodUseKgPerSecond);
            earliest = Earliest(earliest, batteryKWh, EnergyMath.KilowattHoursPerSecond(rates.NetLoadWatts));
            return earliest;
        }

        /// <summary>
        /// Simulation seconds until rising cabin CO2 reaches the given concentration under the current rates;
        /// zero if it is already there, +Infinity if it is not rising.
        /// </summary>
        public double TimeUntilCarbonDioxideReaches(double ppm)
        {
            if (!IsInitialized || double.IsNaN(ppm))
            {
                return double.PositiveInfinity;
            }

            if (carbonDioxidePpm >= ppm)
            {
                return 0d;
            }

            double rate = rates.CarbonDioxideChangePpmPerSecond;
            return rate > 0d ? (ppm - carbonDioxidePpm) / rate : double.PositiveInfinity;
        }

        private bool IsActive => isMissionActive == null || isMissionActive();

        /// <summary>
        /// Applies the current rates for a finite, non-negative number of simulated seconds. Invalid time throws
        /// before any mutation. Reserves clamp at zero (depletion time recorded), the battery at capacity and CO2
        /// between the cabin baseline (when falling) and 1,000,000 ppm. Ignored while the mission is not active.
        /// Publishes one ResourceChanged when any value changed. Never reads Unity time.
        /// </summary>
        public void AdvanceTime(double simulationSeconds)
        {
            if (!IsFinite(simulationSeconds) || simulationSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationSeconds), simulationSeconds, "Simulation time must be finite and non-negative.");
            }

            if (!IsInitialized || simulationSeconds == 0d || !IsActive)
            {
                return;
            }

            if (double.IsInfinity(ElapsedSimulationSeconds + simulationSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(simulationSeconds), simulationSeconds, "Elapsed simulation time would overflow.");
            }

            double start = ElapsedSimulationSeconds;
            ResourceSnapshot before = Snapshot;
            oxygenKg = Drain(oxygenKg, rates.OxygenUseKgPerSecond * simulationSeconds, rates.OxygenUseKgPerSecond, start, simulationSeconds, ResourceId.OxygenKg);
            waterLitres = Drain(waterLitres, rates.WaterUseLitresPerSecond * simulationSeconds, rates.WaterUseLitresPerSecond, start, simulationSeconds, ResourceId.WaterLitres);
            foodKg = Drain(foodKg, rates.FoodUseKgPerSecond * simulationSeconds, rates.FoodUseKgPerSecond, start, simulationSeconds, ResourceId.FoodKg);
            batteryKWh = AdvanceBattery(start, simulationSeconds);
            carbonDioxidePpm = AdvanceCarbonDioxide(simulationSeconds);
            ElapsedSimulationSeconds = start + simulationSeconds;

            if (!SameValues(before, Snapshot))
            {
                events.Publish(new ResourceChanged(Snapshot));
            }
        }

        private double AdvanceCarbonDioxide(double seconds)
        {
            double change = rates.CarbonDioxideChangePpmPerSecond * seconds;
            if (change >= 0d)
            {
                return Math.Min(MaximumCarbonDioxidePpm, carbonDioxidePpm + change);
            }

            // Removal stops at the baseline; a concentration already below it is never raised.
            double lowest = Math.Min(carbonDioxidePpm, carbonDioxideFloorPpm);
            return Math.Max(lowest, carbonDioxidePpm + change);
        }

        private double AdvanceBattery(double start, double seconds)
        {
            double energyKWh = EnergyMath.KilowattHours(rates.NetLoadWatts, seconds);
            if (energyKWh > 0d)
            {
                return Drain(batteryKWh, energyKWh, EnergyMath.KilowattHoursPerSecond(rates.NetLoadWatts), start, seconds, ResourceId.BatteryKWh);
            }

            if (energyKWh < 0d && batteryKWh < BatteryCapacityKWh)
            {
                double charged = Math.Min(BatteryCapacityKWh, batteryKWh - energyKWh);
                if (charged > 0d)
                {
                    depletedAt[(int)ResourceId.BatteryKWh] = null;
                }

                return charged;
            }

            return batteryKWh;
        }

        private double Drain(double quantity, double amount, double ratePerSecond, double start, double seconds, ResourceId resource)
        {
            if (amount <= 0d || quantity <= 0d)
            {
                return quantity;
            }

            double remaining = quantity - amount;
            if (remaining > DepletionTolerance)
            {
                return remaining;
            }

            // Constant rate within the step: the exact crossing time, capped at the step end for float dust.
            MarkDepleted(resource, Math.Min(start + quantity / ratePerSecond, start + seconds));
            return 0d;
        }

        private double Debit(double available, double amount, ResourceId resource, double now)
        {
            if (amount <= 0d)
            {
                return available;
            }

            double remaining = available - amount;
            if (remaining > DepletionTolerance)
            {
                return remaining;
            }

            MarkDepleted(resource, now);
            return 0d;
        }

        private void MarkDepleted(ResourceId resource, double time)
        {
            int index = (int)resource;
            if (depletedAt[index] == null)
            {
                depletedAt[index] = time;
            }
        }

        private CommandResult Evaluate(IReadOnlyList<ResourceAmount> costs, out CostTotals totals)
        {
            totals = default;
            if (costs == null)
            {
                return Fail(CommandFailure.InvalidInput, "A cost list is required.");
            }

            double oxygen = 0d;
            double water = 0d;
            double food = 0d;
            double battery = 0d;
            long parts = 0L;
            for (int i = 0; i < costs.Count; i++)
            {
                ResourceAmount cost = costs[i];
                double quantity = cost.Quantity;
                if (!IsFinite(quantity) || quantity < 0d)
                {
                    return Fail(CommandFailure.InvalidInput, string.Format(CultureInfo.InvariantCulture,
                        "Cost {0} ({1}) must be a finite, non-negative quantity; got {2}.", i + 1, cost.Resource, quantity));
                }

                switch (cost.Resource)
                {
                    case ResourceId.OxygenKg:
                        oxygen += quantity;
                        break;
                    case ResourceId.WaterLitres:
                        water += quantity;
                        break;
                    case ResourceId.FoodKg:
                        food += quantity;
                        break;
                    case ResourceId.BatteryKWh:
                        battery += quantity;
                        break;
                    case ResourceId.SparePartsCount:
                        if (quantity != Math.Floor(quantity) || quantity > int.MaxValue)
                        {
                            return Fail(CommandFailure.InvalidInput, string.Format(CultureInfo.InvariantCulture,
                                "Cost {0}: spare parts are whole units; got {1}.", i + 1, quantity));
                        }

                        parts += (long)quantity;
                        break;
                    default:
                        return Fail(CommandFailure.InvalidInput, string.Format(CultureInfo.InvariantCulture,
                            "Cost {0} names an unknown resource ({1}). Cabin CO2 is not spendable.", i + 1, (int)cost.Resource));
                }
            }

            if (!IsFinite(oxygen) || !IsFinite(water) || !IsFinite(food) || !IsFinite(battery) || parts > int.MaxValue)
            {
                return Fail(CommandFailure.InvalidInput, "The combined cost is too large to represent.");
            }

            if (!IsInitialized)
            {
                return Fail(CommandFailure.WrongPhase, "Reserves are unavailable until the mission launches.");
            }

            if (!IsActive)
            {
                return Fail(CommandFailure.WrongPhase, "Reserves cannot be spent after the mission has ended.");
            }

            var shortfalls = new List<string>();
            AddShortfall(shortfalls, "oxygen", oxygen, oxygenKg, "kg");
            AddShortfall(shortfalls, "water", water, waterLitres, "L");
            AddShortfall(shortfalls, "food", food, foodKg, "kg");
            AddShortfall(shortfalls, "battery energy", battery, batteryKWh, "kWh");
            if (parts > spareParts)
            {
                shortfalls.Add(string.Format(CultureInfo.InvariantCulture, "{0} spare part(s) needed, {1} available", parts, spareParts));
            }

            if (shortfalls.Count > 0)
            {
                return Fail(CommandFailure.InsufficientResources, "Insufficient reserves: " + string.Join("; ", shortfalls) + ".");
            }

            totals = new CostTotals(oxygen, water, food, battery, (int)parts);
            return Ok("Affordable: " + totals.Describe() + ".");
        }

        private static void AddShortfall(List<string> shortfalls, string name, double needed, double available, string unit)
        {
            if (needed > available + AffordabilityTolerance)
            {
                shortfalls.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1:0.###} {2} needed, {3:0.###} {2} available", name, needed, unit, available));
            }
        }

        private static double Earliest(double current, double quantity, double usePerSecond)
        {
            if (usePerSecond <= 0d || quantity <= 0d)
            {
                return current;
            }

            double seconds = quantity / usePerSecond;
            return seconds < current ? seconds : current;
        }

        private static string FindSnapshotProblem(ResourceSnapshot snapshot)
        {
            if (!IsFinite(snapshot.OxygenKg) || snapshot.OxygenKg < 0d
                || !IsFinite(snapshot.WaterLitres) || snapshot.WaterLitres < 0d
                || !IsFinite(snapshot.FoodKg) || snapshot.FoodKg < 0d
                || !IsFinite(snapshot.BatteryKWh) || snapshot.BatteryKWh < 0d
                || !IsFinite(snapshot.CarbonDioxidePpm) || snapshot.CarbonDioxidePpm < 0d
                || snapshot.SparePartsCount < 0)
            {
                return "Initial reserves must be finite and non-negative.";
            }

            return null;
        }

        private static bool SameValues(ResourceSnapshot a, ResourceSnapshot b)
        {
            return a.OxygenKg == b.OxygenKg
                && a.WaterLitres == b.WaterLitres
                && a.FoodKg == b.FoodKg
                && a.BatteryKWh == b.BatteryKWh
                && a.SparePartsCount == b.SparePartsCount
                && a.CarbonDioxidePpm == b.CarbonDioxidePpm;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static CommandResult Ok(string explanation)
        {
            return new CommandResult(true, CommandFailure.None, explanation);
        }

        private static CommandResult Fail(CommandFailure failure, string explanation)
        {
            return new CommandResult(false, failure, explanation);
        }

        private readonly struct CostTotals
        {
            public CostTotals(double oxygenKg, double waterLitres, double foodKg, double batteryKWh, int spareParts)
            {
                OxygenKg = oxygenKg;
                WaterLitres = waterLitres;
                FoodKg = foodKg;
                BatteryKWh = batteryKWh;
                SpareParts = spareParts;
            }

            public double OxygenKg { get; }

            public double WaterLitres { get; }

            public double FoodKg { get; }

            public double BatteryKWh { get; }

            public int SpareParts { get; }

            public bool IsZero => OxygenKg == 0d && WaterLitres == 0d && FoodKg == 0d && BatteryKWh == 0d && SpareParts == 0;

            public string Describe()
            {
                var parts = new List<string>();
                if (OxygenKg > 0d)
                {
                    parts.Add(string.Format(CultureInfo.InvariantCulture, "{0:0.###} kg oxygen", OxygenKg));
                }

                if (WaterLitres > 0d)
                {
                    parts.Add(string.Format(CultureInfo.InvariantCulture, "{0:0.###} L water", WaterLitres));
                }

                if (FoodKg > 0d)
                {
                    parts.Add(string.Format(CultureInfo.InvariantCulture, "{0:0.###} kg food", FoodKg));
                }

                if (BatteryKWh > 0d)
                {
                    parts.Add(string.Format(CultureInfo.InvariantCulture, "{0:0.###} kWh battery energy", BatteryKWh));
                }

                if (SpareParts > 0)
                {
                    parts.Add(string.Format(CultureInfo.InvariantCulture, "{0} spare part(s)", SpareParts));
                }

                return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
            }
        }
    }
}
