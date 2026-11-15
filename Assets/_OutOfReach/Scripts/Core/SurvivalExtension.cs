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
        public bool IsActive => Enabled && !disposed && !state.IsClosed && (ChapterMode
            ? ActivatedAtSimulationSeconds.HasValue && IsChapterPhase(state.Phase)
            : ContactRestoredAtSimulationSeconds.HasValue && state.Phase == GamePhase.Survival);

        /// <summary>
        /// Approved chapter loop: needs and hand-issued portions start once, when the impact sequence ends (day 1), not
        /// at contact. Set by the director before launch; false keeps the legacy post-contact activation.
        /// </summary>
        internal bool ChapterMode { get; set; }

        /// <summary>When portion accounting began (chapter mode: end of the impact; legacy: contact), or null.</summary>
        public double? ActivatedAtSimulationSeconds => ChapterMode ? chapterActivatedAt : ContactRestoredAtSimulationSeconds;

        /// <summary>When the one grow module was built, simulation seconds, or null.</summary>
        public double? GrowModuleBuiltAtSimulationSeconds { get; private set; }

        /// <summary>Rations eaten and water portions drunk so far (each one real ledger debit).</summary>
        public int RationsEaten { get; private set; }
        public int DrinksTaken { get; private set; }

        /// <summary>
        /// Set by the chapter routine around the portions it issues: the debit is identical, only the per-portion journal
        /// line is left out (the routine records one summary line per segment instead of hundreds of report lines).
        /// </summary>
        internal bool QuietJournal { get; set; }

        /// <summary>Why the crew cannot use the galley or bunk right now (in the suit, outside), or null.</summary>
        internal Func<string> CabinBlocker { get; set; }

        private double? chapterActivatedAt;

        private static bool IsChapterPhase(GamePhase phase) => phase == GamePhase.Emergency || phase == GamePhase.Repair
            || phase == GamePhase.Eva || phase == GamePhase.Transmission || phase == GamePhase.Survival;
        public double? ContactRestoredAtSimulationSeconds { get; private set; }
        public double Hunger01 => Math.Max(0d, Math.Min(1d, hungerSeconds / Spec.HungerLimitSeconds));
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

        /// <summary>
        /// True while the crew rests (set by <see cref="SurvivalRecovery"/>): food and water are then drawn at the
        /// BVAD crew-day rates instead, so hunger and thirst neither accrue nor reset, and the awake actions are refused.
        /// </summary>
        internal bool NeedsPaused { get; set; }

        /// <summary>
        /// Chapter mode only, once: the one switch from continuous provisioning to hand-issued portions. Needs start at
        /// zero because the continuous draw has already charged every second until now; nothing is charged twice.
        /// </summary>
        internal void ActivateForChapter()
        {
            if (!Enabled || disposed || !ChapterMode || chapterActivatedAt.HasValue || !IsChapterPhase(state.Phase)) return;
            chapterActivatedAt = state.ElapsedSimulationSeconds;
            log?.Invoke("After the accident the automatic galley schedule is gone: from now on the crew takes each stored ration and water portion by hand.");
            modified?.Invoke();
            Notify();
        }

        /// <summary>Simulated seconds until a ration (food) or water portion is due; 0 when due now; +Infinity when inactive or resting.</summary>
        internal double TimeUntilPortionDue(bool food)
        {
            if (!IsActive || NeedsPaused) return double.PositiveInfinity;
            return Math.Max(0d, Spec.PortionSeconds - (food ? hungerSeconds : thirstSeconds));
        }

        /// <summary>
        /// Chapter decision: stops a growing module. Its light/fan load and irrigation end now and the crop is forfeited:
        /// no food is credited and nothing spent on it is refunded.
        /// </summary>
        internal CommandResult StopGrowModule(string reason)
        {
            if (GrowStage != GrowModuleStage.Growing) return Fail(CommandFailure.PreconditionsNotMet, "No crop is growing.");
            using (events.DeferDispatch())
            {
                GrowStage = GrowModuleStage.Failed;
                log?.Invoke(reason);
                modified?.Invoke();
                Notify();
            }
            return Ok(reason);
        }

        internal void Begin()
        {
            if (!Enabled || disposed || state.Phase != GamePhase.Survival || ContactRestoredAtSimulationSeconds.HasValue) return;
            ContactRestoredAtSimulationSeconds = state.ElapsedSimulationSeconds;
            if (ChapterMode)
            {
                log?.Invoke("Contact restored. Earth now relays the rescue planning; the crew keeps rationing by hand.");
                modified?.Invoke();
                Notify();
                return;
            }

            log?.Invoke("Contact restored. Survival continues while a nearby fictional recovery vehicle's rendezvous is assessed; no arrival is guaranteed. Rations and drinking now require crew actions.");
            modified?.Invoke();
            Notify();
        }

        internal double TimeToNextBoundary()
        {
            if (!IsActive) return double.PositiveInfinity;
            double boundary = NeedsPaused ? double.PositiveInfinity : Math.Min(Spec.HungerLimitSeconds - hungerSeconds, Spec.ThirstLimitSeconds - thirstSeconds);
            if (GrowStage == GrowModuleStage.Growing) boundary = Math.Min(boundary, Spec.GrowthSeconds - growingSeconds);
            return boundary > 0d ? boundary : double.PositiveInfinity;
        }

        internal void AdvanceTime(double seconds)
        {
            if (!IsActive || seconds <= 0d) return;
            if (!NeedsPaused)
            {
                hungerSeconds = Math.Min(Spec.HungerLimitSeconds, hungerSeconds + seconds);
                thirstSeconds = Math.Min(Spec.ThirstLimitSeconds, thirstSeconds + seconds);
            }
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

        public CommandResult CheckConsumeRation() => CheckPortion(IsFirstMeal ? Spec.PortionSeconds : hungerSeconds, rationCost,
            IsFirstMeal ? "Prepare and eat the first packaged meal" : "Eat stored ration", "No ration is needed yet.");

        /// <summary>
        /// Chapter mode: the first packaged meal after the accident may be eaten before the 6 h portion gate, once, at the
        /// full ration cost. The unused part of the portion becomes a satiation credit (negative hunger), so the next ration
        /// comes due that much later: no reset, no free food, no double debit.
        /// </summary>
        private bool IsFirstMeal => ChapterMode && RationsEaten == 0;
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
                if (food) { hungerSeconds = IsFirstMeal ? hungerSeconds - Spec.PortionSeconds : Math.Max(0d, hungerSeconds - Spec.PortionSeconds); RationsEaten++; }
                else { thirstSeconds = Math.Max(0d, thirstSeconds - Spec.PortionSeconds); DrinksTaken++; }
                if (!QuietJournal) log?.Invoke(check.Explanation + ".");
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
                GrowModuleBuiltAtSimulationSeconds = state.ElapsedSimulationSeconds;
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
            if (!IsActive) return Fail(CommandFailure.WrongPhase, ChapterMode
                ? "Rations and water are taken by hand once the accident is over."
                : "This action belongs to the enabled post-contact survival chapter.");
            if (clock.IsPaused || cinematics.IsWaiting) return Fail(CommandFailure.PreconditionsNotMet, "Resume crew control before acting.");
            string blocked = CabinBlocker?.Invoke();
            if (blocked != null) return Fail(CommandFailure.PreconditionsNotMet, blocked);
            if (NeedsPaused) return Fail(CommandFailure.PreconditionsNotMet, "The crew is resting. Wake up first.");
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
