using System;
using System.Globalization;
using OutOfReach.Contracts;
using OutOfReach.Inventory;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// GAME ASSUMPTIONS for the two field modifications. None of these figures is NASA data or an engineering
    /// rating: the salvage yield and the costs are tuning, and the cartridge's capacity is simply expressed as
    /// crew-hours of the cited metabolic CO2 output (1.01 kg per crew-day, NASA BVAD REV2 Table 3-23). It is in the
    /// spirit of a lithium-hydroxide canister but it is not sized from, or rated like, a real scrubber.
    /// </summary>
    public sealed class FieldModificationSpec
    {
        public FieldModificationSpec(
            int salvageSpareParts = 3,
            double salvageEnergyKWh = 0.2d,
            int cartridgeSpareParts = 1,
            double cartridgeEnergyKWh = 0.2d,
            double cartridgeCrewHours = 12d)
        {
            if (salvageSpareParts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(salvageSpareParts), salvageSpareParts, "Salvage must yield at least one spare part.");
            }

            if (cartridgeSpareParts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cartridgeSpareParts), cartridgeSpareParts, "Costs cannot be negative.");
            }

            SalvageSpareParts = salvageSpareParts;
            SalvageEnergyKWh = RequireNonNegative(salvageEnergyKWh, nameof(salvageEnergyKWh));
            CartridgeSpareParts = cartridgeSpareParts;
            CartridgeEnergyKWh = RequireNonNegative(cartridgeEnergyKWh, nameof(cartridgeEnergyKWh));
            if (double.IsNaN(cartridgeCrewHours) || double.IsInfinity(cartridgeCrewHours) || cartridgeCrewHours <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(cartridgeCrewHours), cartridgeCrewHours, "A cartridge needs a capacity greater than zero.");
            }

            CartridgeCrewHours = cartridgeCrewHours;
        }

        /// <summary>Spare parts recovered by dismantling the science payload (as many as one spare-parts kit).</summary>
        public int SalvageSpareParts { get; }

        /// <summary>Tool energy for the salvage, kWh.</summary>
        public double SalvageEnergyKWh { get; }

        /// <summary>Spare parts used for one cartridge's improvised adapter and fan.</summary>
        public int CartridgeSpareParts { get; }

        public double CartridgeEnergyKWh { get; }

        /// <summary>Capacity of one cartridge as crew-hours of metabolic CO2 output.</summary>
        public double CartridgeCrewHours { get; }

        /// <summary>The same capacity in kilograms of CO2: 0.505 kg for 12 crew-hours.</summary>
        public double CartridgeCapacityKg => CrewMetabolicAssumptions.CarbonDioxideKgPerCrewDay * CartridgeCrewHours / 24d;

        private static double RequireNonNegative(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(name, value, "Costs must be finite and non-negative.");
            }

            return value;
        }
    }

    /// <summary>
    /// The crew's two field modifications (implements <see cref="IFieldModifications"/>). Both only convert what was
    /// carried; nothing is created.
    /// <list type="bullet">
    /// <item><b>Salvage the science payload</b>, once: spare parts are gained, the instrument's electrical load
    /// leaves the bus exactly once and the science objective is abandoned for good.</item>
    /// <item><b>CO2 absorber cartridge</b>: built at the workbench from a spare part and energy, then installed in
    /// the absorber bay. While cabin CO2 would rise, an installed cartridge takes up to the crew's own output, so
    /// the concentration stops rising until its capacity is used. It needs no power. It does not stop the oxygen
    /// leak and is not a life-support repair. It is not consumed while the main scrubbers cope or the crew
    /// breathes suit oxygen.</item>
    /// </list>
    /// Each modification exists only when it is enabled, which the scene binding decides: a ship without the
    /// workbench, the bay or a reachable instrument behaves exactly as before. Inside the ship only (Emergency,
    /// Repair, Transmission). Every rejected command changes nothing; time passes only through the session clock.
    /// </summary>
    public sealed class FieldModifications : IFieldModifications
    {
        private const double DepletedKg = 1e-12;

        private readonly FieldModificationSpec spec;
        private readonly MissionDefinition definition;
        private readonly MissionEventBus events;
        private readonly MissionGameState state;
        private readonly MissionPlanner planner;
        private readonly ResourceLedger resources;
        private readonly ShipSystems systems;
        private readonly Action modified;
        private readonly Action<string> log;
        private double absorberRemainingKg;
        private double absorbingKgPerSecond;

        internal FieldModifications(
            FieldModificationSpec spec,
            MissionDefinition definition,
            MissionEventBus events,
            MissionGameState state,
            MissionPlanner planner,
            ResourceLedger resources,
            ShipSystems systems,
            Action modified,
            Action<string> log)
        {
            this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.planner = planner ?? throw new ArgumentNullException(nameof(planner));
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            this.systems = systems ?? throw new ArgumentNullException(nameof(systems));
            this.modified = modified;
            this.log = log;
        }

        public FieldModificationSpec Spec => spec;

        /// <summary>True when the science instrument can be reached and dismantled in this ship (scene binding).</summary>
        public bool SalvageEnabled { get; internal set; }

        /// <summary>True when the ship has both the workbench and the absorber bay (scene binding).</summary>
        public bool AbsorberEnabled { get; internal set; }

        public bool SciencePayloadSalvaged { get; private set; }

        /// <summary>Simulation time of the salvage, or null.</summary>
        public double? SalvagedAtSimulationSeconds { get; private set; }

        /// <summary>Electrical load taken off the bus by the salvage, W (0 before it).</summary>
        public double RemovedLoadWatts { get; private set; }

        public int AbsorberCartridgesCarried { get; private set; }

        /// <summary>True while a cartridge with capacity left sits in the bay.</summary>
        public bool AbsorberInstalled => absorberRemainingKg > 0d;

        /// <summary>Simulated seconds the installed cartridge still lasts at the crew's full CO2 output; 0 without one.</summary>
        public double AbsorberRemainingSimulationSeconds => absorberRemainingKg / CrewKgPerSecond;

        /// <summary>True while the installed cartridge is taking up CO2 right now.</summary>
        public bool IsAbsorbing => absorbingKgPerSecond > 0d;

        public int CartridgesBuilt { get; private set; }

        public int CartridgesInstalled { get; private set; }

        /// <summary>CO2 taken up by cartridges so far, kg.</summary>
        public double AbsorbedCarbonDioxideKg { get; private set; }

        /// <summary>Spare parts a salvage could still add: the yield while it is possible, otherwise 0.</summary>
        public int SalvageablePartsNow => SalvageEnabled && !SciencePayloadSalvaged && HasSciencePayload() ? spec.SalvageSpareParts : 0;

        public event Action Changed;

        private double CrewKgPerSecond =>
            CrewMetabolicAssumptions.CarbonDioxideKgPerCrewDay * definition.CrewCount / CrewMetabolicAssumptions.SecondsPerDay;

        public CommandResult CheckSalvagePayload()
        {
            if (state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (!SalvageEnabled)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "The science instrument cannot be reached in this ship.");
            }

            if (!HasSciencePayload())
            {
                return Fail(CommandFailure.PreconditionsNotMet, "No science payload aboard");
            }

            if (SciencePayloadSalvaged)
            {
                return Fail(CommandFailure.AlreadyComplete, "Science instrument dismantled");
            }

            CommandResult inside = CheckInsideShip();
            if (!inside.Succeeded)
            {
                return inside;
            }

            if (spec.SalvageEnergyKWh > 0d)
            {
                CommandResult affordable = resources.CheckAffordable(SalvageCost());
                if (!affordable.Succeeded)
                {
                    return affordable;
                }
            }

            return Ok(string.Format(CultureInfo.InvariantCulture,
                "Dismantle science instrument: +{0} spare parts for {1:0.##} kWh. The science objective is abandoned for good.",
                spec.SalvageSpareParts, spec.SalvageEnergyKWh));
        }

        /// <summary>Dismantles the science payload for spare parts. Once; irreversible.</summary>
        public CommandResult TrySalvagePayload()
        {
            CommandResult check = CheckSalvagePayload();
            if (!check.Succeeded)
            {
                return check;
            }

            double load = SciencePayloadLoadWatts();
            using (events.DeferDispatch())
            {
                CommandResult exchanged = resources.TryExchangeForSpareParts(SalvageCost(), spec.SalvageSpareParts);
                if (!exchanged.Succeeded)
                {
                    return exchanged;
                }

                SciencePayloadSalvaged = true;
                SalvagedAtSimulationSeconds = state.ElapsedSimulationSeconds;
                RemovedLoadWatts = load;
                Log(string.Format(CultureInfo.InvariantCulture,
                    "Science instrument dismantled: +{0} spare parts, {1:0} W off the bus. The science objective is abandoned.",
                    spec.SalvageSpareParts, load));
                NotifyModified();
                RaiseChanged();
            }

            return Ok(string.Format(CultureInfo.InvariantCulture,
                "Science instrument dismantled: +{0} spare parts. Its {1:0} W load is off the bus.", spec.SalvageSpareParts, load));
        }

        public CommandResult CheckBuildCartridge()
        {
            CommandResult open = CheckAbsorberOpen();
            if (!open.Succeeded)
            {
                return open;
            }

            CommandResult affordable = resources.CheckAffordable(CartridgeCost());
            if (!affordable.Succeeded)
            {
                return affordable;
            }

            string warning = string.Empty;
            int antennaParts = AntennaRepairParts();
            int partsAfter = resources.Snapshot.SparePartsCount - spec.CartridgeSpareParts;
            if (!systems.Antenna.IsOperational && partsAfter + SalvageablePartsNow < antennaParts)
            {
                warning = string.Format(CultureInfo.InvariantCulture, " Warning: that leaves {0} spare part{1}; the antenna needs {2}.",
                    partsAfter, partsAfter == 1 ? string.Empty : "s", antennaParts);
            }

            return Ok(string.Format(CultureInfo.InvariantCulture, "Build CO2 absorber cartridge: {0} spare part{1}, {2:0.##} kWh.{3}",
                spec.CartridgeSpareParts, spec.CartridgeSpareParts == 1 ? string.Empty : "s", spec.CartridgeEnergyKWh, warning));
        }

        /// <summary>Builds one cartridge at the workbench. It is carried until it is installed.</summary>
        public CommandResult TryBuildCartridge()
        {
            CommandResult check = CheckBuildCartridge();
            if (!check.Succeeded)
            {
                return check;
            }

            using (events.DeferDispatch())
            {
                CommandResult spent = resources.TryConsume(CartridgeCost());
                if (!spent.Succeeded)
                {
                    return spent;
                }

                AbsorberCartridgesCarried++;
                CartridgesBuilt++;
                Log("CO2 absorber cartridge built at the workbench.");
                RaiseChanged();
                NotifyModified();
            }

            return Ok("CO2 absorber cartridge built. Install it in the absorber bay.");
        }

        public CommandResult CheckInstallCartridge()
        {
            CommandResult open = CheckAbsorberOpen();
            if (!open.Succeeded)
            {
                return open;
            }

            if (AbsorberInstalled)
            {
                return Fail(CommandFailure.AlreadyComplete, "CO2 absorber working: " + EvaOperations.FormatDuration(AbsorberRemainingSimulationSeconds) + " of capacity left");
            }

            if (AbsorberCartridgesCarried == 0)
            {
                return Fail(CommandFailure.PreconditionsNotMet, CartridgesInstalled > 0
                    ? "Cartridge spent. Build a new one at the workbench."
                    : "Empty absorber bay. Build a cartridge at the workbench.");
            }

            return Ok("Install CO2 absorber cartridge: cabin CO2 stops rising for up to "
                + EvaOperations.FormatDuration(spec.CartridgeCapacityKg / CrewKgPerSecond));
        }

        /// <summary>Puts a carried cartridge into the bay. A spent one is replaced; a working one is left alone.</summary>
        public CommandResult TryInstallCartridge()
        {
            CommandResult check = CheckInstallCartridge();
            if (!check.Succeeded)
            {
                return check;
            }

            AbsorberCartridgesCarried--;
            CartridgesInstalled++;
            absorberRemainingKg = spec.CartridgeCapacityKg;
            Log("CO2 absorber cartridge installed: it takes up the crew's CO2 output without power while cabin CO2 would rise.");
            NotifyModified();
            RaiseChanged();
            return Ok("CO2 absorber cartridge installed.");
        }

        /// <summary>
        /// Called by the director whenever rates are recomputed: how much of a rising cabin CO2 rate the installed
        /// cartridge takes up (never more than the crew's own output, never more than the rise itself), in ppm/s.
        /// </summary>
        internal double AbsorbPpmPerSecond(double baseChangePpmPerSecond)
        {
            if (absorberRemainingKg <= 0d || baseChangePpmPerSecond <= 0d)
            {
                absorbingKgPerSecond = 0d;
                return 0d;
            }

            double absorbed = Math.Min(baseChangePpmPerSecond, CabinAtmosphere.CrewCarbonDioxidePpmPerSecond(definition.CrewCount));
            absorbingKgPerSecond = absorbed / CabinAtmosphere.PpmPerKilogramCarbonDioxide();
            return absorbed;
        }

        /// <summary>Simulated seconds until the working cartridge is used up; +Infinity while it is idle or absent.</summary>
        internal double TimeToNextBoundary()
        {
            return absorbingKgPerSecond > 0d && absorberRemainingKg > 0d
                ? absorberRemainingKg / absorbingKgPerSecond
                : double.PositiveInfinity;
        }

        /// <summary>Called by the session inside each simulation sub-step: uses up capacity at the current uptake.</summary>
        internal void AdvanceTime(double simulationSeconds)
        {
            if (simulationSeconds <= 0d || absorbingKgPerSecond <= 0d || absorberRemainingKg <= 0d)
            {
                return;
            }

            double used = Math.Min(absorberRemainingKg, absorbingKgPerSecond * simulationSeconds);
            AbsorbedCarbonDioxideKg += used;
            absorberRemainingKg -= used;
            if (absorberRemainingKg > DepletedKg)
            {
                return;
            }

            absorberRemainingKg = 0d;
            absorbingKgPerSecond = 0d;
            Log("CO2 absorber cartridge spent.");
            RaiseChanged();
        }

        private CommandResult CheckAbsorberOpen()
        {
            if (state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            return AbsorberEnabled
                ? CheckInsideShip()
                : Fail(CommandFailure.PreconditionsNotMet, "This ship has no workbench and absorber bay.");
        }

        private CommandResult CheckInsideShip()
        {
            switch (state.Phase)
            {
                case GamePhase.Emergency:
                case GamePhase.Repair:
                case GamePhase.Transmission:
                    return Ok(string.Empty);
                case GamePhase.Eva:
                    return Fail(CommandFailure.WrongPhase, "Not possible during an EVA.");
                case GamePhase.Planning:
                case GamePhase.Intro:
                    return Fail(CommandFailure.WrongPhase, "Not needed before the emergency.");
                default:
                    return Fail(CommandFailure.WrongPhase, $"Not available during {state.Phase}.");
            }
        }

        private bool HasSciencePayload()
        {
            MissionConfiguration configuration = planner.LaunchedConfiguration ?? planner.DraftConfiguration;
            return configuration != null && configuration.HasSciencePayload;
        }

        private double SciencePayloadLoadWatts()
        {
            MissionConfiguration configuration = planner.LaunchedConfiguration;
            if (configuration == null)
            {
                return 0d;
            }

            double watts = 0d;
            foreach (EquipmentDefinition item in definition.Catalog.Definitions)
            {
                if (item.IsScience)
                {
                    watts += item.UnitPowerWatts * configuration.GetQuantity(item.Id);
                }
            }

            return watts;
        }

        private int AntennaRepairParts()
        {
            int parts = 0;
            foreach (ResourceAmount cost in systems.Antenna.RepairCosts)
            {
                if (cost.Resource == ResourceId.SparePartsCount)
                {
                    parts += (int)cost.Quantity;
                }
            }

            return parts;
        }

        private ResourceAmount[] SalvageCost()
        {
            return new[] { new ResourceAmount(ResourceId.BatteryKWh, spec.SalvageEnergyKWh) };
        }

        private ResourceAmount[] CartridgeCost()
        {
            return new[]
            {
                new ResourceAmount(ResourceId.SparePartsCount, spec.CartridgeSpareParts),
                new ResourceAmount(ResourceId.BatteryKWh, spec.CartridgeEnergyKWh),
            };
        }

        private void NotifyModified()
        {
            if (modified != null)
            {
                modified();
            }
        }

        private void Log(string text)
        {
            if (log != null)
            {
                log(text);
            }
        }

        private void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null)
            {
                handler();
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
