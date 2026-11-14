using System;
using System.Globalization;
using OutOfReach.Contracts;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// Opt-in post-contact needs and one crop cycle. Food, water, power and parts remain in ResourceLedger.
    /// Needs are elapsed unmet demand, not another inventory. Only MissionSession advances this service.
    /// Limits, recipe, seedling age and ration-equivalent crop yield are explicit game assumptions in Spec.
    /// </summary>
    public sealed class SurvivalExtension : ISurvivalExtension, IDisposable
    {
        private readonly MissionEventBus events;
        private readonly MissionGameState state;
        private readonly ResourceLedger resources;
        private readonly SimulationClock clock;
        private readonly CinematicCoordinator cinematics;
        private readonly Func<bool> growingConditions;
        private readonly Action modified;
        private readonly Action<string> log;
        private readonly ResourceAmount[] rationCost;
        private readonly ResourceAmount[] drinkCost;
        private readonly ResourceAmount[] buildCost;
        private readonly ResourceAmount[] plantCost;
        private double hungerSeconds;
        private double thirstSeconds;
        private double growingSeconds;
        private bool disposed;

        internal SurvivalExtension(bool enabled, SurvivalExtensionSpec spec, int crewCount,
            MissionEventBus events, MissionGameState state, ResourceLedger resources, SimulationClock clock,
            CinematicCoordinator cinematics, Func<bool> growingConditions, Action modified, Action<string> log)
        {
            Enabled = enabled;
            Spec = spec ?? new SurvivalExtensionSpec();
            this.events = events;
            this.state = state;
            this.resources = resources;
            this.clock = clock;
            this.cinematics = cinematics;
            this.growingConditions = growingConditions;
            this.modified = modified;
            this.log = log;
            double crewDays = crewCount * Spec.PortionSeconds / CrewMetabolicAssumptions.SecondsPerDay;
            rationCost = new[] { new ResourceAmount(ResourceId.FoodKg, CrewMetabolicAssumptions.FoodKgPerCrewDay * crewDays) };
            drinkCost = new[] { new ResourceAmount(ResourceId.WaterLitres, CrewMetabolicAssumptions.WaterLitresPerCrewDay * crewDays) };
            buildCost = new[] { new ResourceAmount(ResourceId.SparePartsCount, Spec.BuildSpareParts), new ResourceAmount(ResourceId.BatteryKWh, Spec.BuildEnergyKWh) };
            plantCost = new[] { new ResourceAmount(ResourceId.WaterLitres, Spec.PlantingWaterLitres) };
        }

        public SurvivalExtensionSpec Spec { get; }
        public bool Enabled { get; }
        public bool IsActive => Enabled && !disposed && !state.IsClosed && ContactRestoredAtSimulationSeconds.HasValue && state.Phase == GamePhase.Survival;
        public double? ContactRestoredAtSimulationSeconds { get; private set; }
        public double Hunger01 => Math.Min(1d, hungerSeconds / Spec.HungerLimitSeconds);
        public double Thirst01 => Math.Min(1d, thirstSeconds / Spec.ThirstLimitSeconds);
        public GrowModuleStage GrowStage { get; private set; }
        public double CropAgeSimulationDays => GrowStage == GrowModuleStage.Unbuilt || GrowStage == GrowModuleStage.ReadyToPlant
            ? 0d : Spec.SeedlingAgeDays + growingSeconds / CrewMetabolicAssumptions.SecondsPerDay;
        public double CropRemainingSimulationSeconds => GrowStage == GrowModuleStage.Growing ? Math.Max(0d, Spec.GrowthSeconds - growingSeconds) : 0d;
        public double RationFoodKg => rationCost[0].Quantity;
        public double DrinkWaterLitres => drinkCost[0].Quantity;
        internal double GrowLoadWatts => IsActive && GrowStage == GrowModuleStage.Growing ? Spec.GrowPowerWatts : 0d;
        internal double GrowWaterLitresPerSecond => IsActive && GrowStage == GrowModuleStage.Growing ? Spec.GrowWaterLitresPerDay / CrewMetabolicAssumptions.SecondsPerDay : 0d;
        internal SurvivalFailure NeedsFailure => thirstSeconds >= Spec.ThirstLimitSeconds - SurvivalRules.TimeToleranceSeconds
            ? SurvivalFailure.ThirstLimit : hungerSeconds >= Spec.HungerLimitSeconds - SurvivalRules.TimeToleranceSeconds
                ? SurvivalFailure.HungerLimit : SurvivalFailure.None;
        public event Action Changed;

        internal void Begin()
        {
            if (!Enabled || disposed || state.Phase != GamePhase.Survival || ContactRestoredAtSimulationSeconds.HasValue) return;
            ContactRestoredAtSimulationSeconds = state.ElapsedSimulationSeconds;
            log?.Invoke("Contact restored. Survival continues while a nearby fictional recovery vehicle's rendezvous is assessed; no arrival is guaranteed. Rations and drinking now require crew actions.");
            modified?.Invoke();
            Notify();
        }

        internal double TimeToNextBoundary()
        {
            if (!IsActive) return double.PositiveInfinity;
            double boundary = Math.Min(Spec.HungerLimitSeconds - hungerSeconds, Spec.ThirstLimitSeconds - thirstSeconds);
            if (GrowStage == GrowModuleStage.Growing) boundary = Math.Min(boundary, Spec.GrowthSeconds - growingSeconds);
            return boundary > 0d ? boundary : double.PositiveInfinity;
        }

        internal void AdvanceTime(double seconds)
        {
            if (!IsActive || seconds <= 0d) return;
            hungerSeconds = Math.Min(Spec.HungerLimitSeconds, hungerSeconds + seconds);
            thirstSeconds = Math.Min(Spec.ThirstLimitSeconds, thirstSeconds + seconds);
            if (GrowStage == GrowModuleStage.Growing)
            {
                growingSeconds = Math.Min(Spec.GrowthSeconds, growingSeconds + seconds);
                // Session splits exactly at water/battery depletion. An interruption wins a simultaneous harvest.
                if (!growingConditions() || resources.Snapshot.WaterLitres <= 0d)
                {
                    GrowStage = GrowModuleStage.Failed;
                    log?.Invoke("Grow module failed: controlled conditions or irrigation were interrupted. No harvest was credited.");
                    modified?.Invoke();
                }
                else if (growingSeconds >= Spec.GrowthSeconds - SurvivalRules.TimeToleranceSeconds)
                {
                    growingSeconds = Spec.GrowthSeconds;
                    GrowStage = GrowModuleStage.HarvestReady;
                    log?.Invoke("The pre-grown crop reached its nominal harvest age after sustained cultivation. Collect the small supplement.");
                    modified?.Invoke();
                }
            }
            Notify();
        }

        public CommandResult CheckConsumeRation() => CheckPortion(hungerSeconds, rationCost, "Eat stored ration", "No ration is needed yet.");
        public CommandResult CheckDrinkWater() => CheckPortion(thirstSeconds, drinkCost, "Drink stored water", "No water portion is needed yet.");

        private CommandResult CheckPortion(double need, ResourceAmount[] cost, string action, string satisfied)
        {
            CommandResult gate = CheckActive();
            if (!gate.Succeeded) return gate;
            if (need < Spec.PortionSeconds - SurvivalRules.TimeToleranceSeconds) return Fail(CommandFailure.PreconditionsNotMet, satisfied);
            CommandResult affordable = resources.CheckAffordable(cost);
            return affordable.Succeeded ? Ok(string.Format(CultureInfo.InvariantCulture, "{0}: {1:0.####} {2}", action, cost[0].Quantity, cost[0].Resource == ResourceId.FoodKg ? "kg" : "L")) : affordable;
        }

        public CommandResult TryConsumeRation() => ConsumePortion(true);
        public CommandResult TryDrinkWater() => ConsumePortion(false);

        private CommandResult ConsumePortion(bool food)
        {
            CommandResult check = food ? CheckConsumeRation() : CheckDrinkWater();
            if (!check.Succeeded) return check;
            using (events.DeferDispatch())
            {
                CommandResult spent = resources.TryConsume(food ? rationCost : drinkCost);
                if (!spent.Succeeded) return spent;
                if (food) hungerSeconds = Math.Max(0d, hungerSeconds - Spec.PortionSeconds);
                else thirstSeconds = Math.Max(0d, thirstSeconds - Spec.PortionSeconds);
                log?.Invoke(check.Explanation + ".");
                Notify();
            }
            return check;
        }

        public CommandResult CheckBuildGrowModule()
        {
            CommandResult gate = CheckActive();
            if (!gate.Succeeded) return gate;
            if (GrowStage != GrowModuleStage.Unbuilt) return Fail(CommandFailure.AlreadyComplete, "The one grow module has already been built.");
            CommandResult affordable = resources.CheckAffordable(buildCost);
            return affordable.Succeeded ? Ok(string.Format(CultureInfo.InvariantCulture, "Build grow module: {0} spare part(s), {1:0.##} kWh", Spec.BuildSpareParts, Spec.BuildEnergyKWh)) : affordable;
        }

        public CommandResult TryBuildGrowModule()
        {
            CommandResult check = CheckBuildGrowModule();
            if (!check.Succeeded) return check;
            using (events.DeferDispatch())
            {
                CommandResult spent = resources.TryConsume(buildCost);
                if (!spent.Succeeded) return spent;
                GrowStage = GrowModuleStage.ReadyToPlant;
                log?.Invoke(check.Explanation + ". One pre-grown seedling kit is available as scenario equipment.");
                modified?.Invoke();
                Notify();
            }
            return check;
        }

        public CommandResult CheckPlant()
        {
            CommandResult gate = CheckActive();
            if (!gate.Succeeded) return gate;
            if (GrowStage != GrowModuleStage.ReadyToPlant) return Fail(CommandFailure.PreconditionsNotMet, "Build the grow module before planting its single seedling kit.");
            if (!growingConditions()) return Fail(CommandFailure.PreconditionsNotMet, "Planting needs operational power and life support.");
            if (resources.Snapshot.WaterLitres <= Spec.PlantingWaterLitres + ResourceLedger.AffordabilityTolerance)
                return Fail(CommandFailure.InsufficientResources, "Keep irrigation water after the planting cost.");
            CommandResult affordable = resources.CheckAffordable(plantCost);
            return affordable.Succeeded ? Ok(string.Format(CultureInfo.InvariantCulture,
                "Plant {0:0.#}-day seedlings: {1:0.##} L; {2:0.#} further days, {3:0.#} W and {4:0.###} L/day", Spec.SeedlingAgeDays, Spec.PlantingWaterLitres,
                Spec.GrowthSeconds / CrewMetabolicAssumptions.SecondsPerDay, Spec.GrowPowerWatts, Spec.GrowWaterLitresPerDay)) : affordable;
        }

        public CommandResult TryPlant()
        {
            CommandResult check = CheckPlant();
            if (!check.Succeeded) return check;
            using (events.DeferDispatch())
            {
                CommandResult spent = resources.TryConsume(plantCost);
                if (!spent.Succeeded) return spent;
                GrowStage = GrowModuleStage.Growing;
                log?.Invoke(check.Explanation + ". Controlled conditions support growth; food is not available now.");
                modified?.Invoke();
                Notify();
            }
            return check;
        }

        public CommandResult CheckHarvest()
        {
            CommandResult gate = CheckActive();
            if (!gate.Succeeded) return gate;
            return GrowStage == GrowModuleStage.HarvestReady
                ? Ok(string.Format(CultureInfo.InvariantCulture, "Harvest small supplement: {0:0.###} kg ration-equivalent game credit", Spec.HarvestFoodEquivalentKg))
                : Fail(CommandFailure.PreconditionsNotMet, "No mature crop is ready to harvest.");
        }

        public CommandResult TryHarvest()
        {
            CommandResult check = CheckHarvest();
            if (!check.Succeeded) return check;
            using (events.DeferDispatch())
            {
                CommandResult credited = resources.TryCreditHarvestFood(Spec.HarvestFoodEquivalentKg);
                if (!credited.Succeeded) return credited;
                GrowStage = GrowModuleStage.Harvested;
                log?.Invoke(check.Explanation + ". This is a supplement, not a complete diet or measured leaf caloric content.");
                Notify();
            }
            return check;
        }

        private CommandResult CheckActive()
        {
            if (!IsActive) return Fail(CommandFailure.WrongPhase, "This action belongs to the enabled post-contact survival chapter.");
            if (clock.IsPaused || cinematics.IsWaiting) return Fail(CommandFailure.PreconditionsNotMet, "Resume crew control before acting.");
            return Ok("Survival action available.");
        }

        private void Notify() => events.EnqueueObservation(() =>
        {
            if (disposed) return;
            Action handlers = Changed;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                if (disposed) break;
                try { handler(); }
                catch (Exception error) { events.ReportCallbackFailure(error); }
            }
        });

        public void Dispose() { disposed = true; Changed = null; }
        private static CommandResult Ok(string text) => new CommandResult(true, CommandFailure.None, text);
        private static CommandResult Fail(CommandFailure failure, string text) => new CommandResult(false, failure, text);
    }
}
