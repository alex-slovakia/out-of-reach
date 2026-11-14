using System;
using System.Collections.Generic;
using System.Globalization;
using OutOfReach.Contracts;
using OutOfReach.Inventory;

namespace OutOfReach.Core
{
    /// <summary>
    /// Physical preflight loading (implements <see cref="ICargoLoading"/>): the player carries one crate at a time
    /// between a type's supply pallet and its bay stack. This class keeps no inventory of its own.
    /// <list type="bullet">
    /// <item>A crate in a bay stack is one unit in the planner's draft. Loading and unloading go through
    /// <see cref="MissionPlanner.TrySetEquipment"/>, so mass, budget, power and supply validation, the planning
    /// deadline, its last-valid fallback and the launch are exactly the list planner's.</item>
    /// <item>The crate in hands is the only state here. It is not aboard: at launch (or at the deadline) it stays
    /// behind and is never counted.</item>
    /// <item>Supply left on a pallet is derived: the type's maximum minus what is loaded minus the crate in hands.</item>
    /// <item>Loading past a limit is allowed, as in the list planner: the draft shows the violation and the launch
    /// is refused until it is corrected.</item>
    /// </list>
    /// Only during Planning. Every rejected command changes nothing.
    /// </summary>
    public sealed class CargoLoading : ICargoLoading
    {
        /// <summary>GAMEPLAY TUNING: a crate this heavy (or heavier) halves the walk speed.</summary>
        public const double MassForSlowestCarryKg = 90d;

        /// <summary>GAMEPLAY TUNING: slowest carry speed as a share of the walk speed.</summary>
        public const double MinimumCarrySpeedFactor = 0.5d;

        private readonly MissionDefinition definition;
        private readonly MissionPlanner planner;
        private readonly MissionGameState state;
        private readonly Action<string> log;

        internal CargoLoading(MissionDefinition definition, MissionPlanner planner, MissionGameState state, IMissionEventBus events, Action<string> log = null)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.planner = planner ?? throw new ArgumentNullException(nameof(planner));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.log = log;
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            events.PhaseChanged += OnPhaseChanged;
            planner.Sealed += OnSealed;
        }

        /// <summary>Equipment ID of the crate in the player's hands, or null.</summary>
        public string CarriedEquipmentId { get; private set; }

        /// <summary>
        /// Walk-speed multiplier while carrying, from the crate's authoritative unit mass; 1 with empty hands.
        /// </summary>
        public float CarrySpeedFactor
        {
            get
            {
                return CarriedEquipmentId != null && definition.Catalog.TryGet(CarriedEquipmentId, out EquipmentDefinition item)
                    ? (float)SpeedFactorForMass(item.UnitMassKg)
                    : 1f;
            }
        }

        /// <summary>Raised when the crate in hands changes. Bay stacks follow the planner's draft instead.</summary>
        public event Action Changed;

        /// <summary>
        /// GAMEPLAY TUNING, not ergonomics: 1 - mass / 180 kg, never below one half. A 25 kg kit gives 0.86, the
        /// 90 kg instrument 0.50. Nobody hand-carries 90 kg; presentation should show a handling cart.
        /// </summary>
        public static double SpeedFactorForMass(double massKg)
        {
            if (double.IsNaN(massKg) || massKg <= 0d)
            {
                return 1d;
            }

            double factor = 1d - (massKg * (1d - MinimumCarrySpeedFactor) / MassForSlowestCarryKg);
            return Math.Max(MinimumCarrySpeedFactor, factor);
        }

        /// <summary>Crates of this type aboard: the planner draft's quantity.</summary>
        public int GetLoaded(string equipmentId)
        {
            return planner.DraftConfiguration.GetQuantity(equipmentId);
        }

        /// <summary>Crates of this type still on the supply pallet: maximum minus loaded minus the one in hands.</summary>
        public int GetSupplyRemaining(string equipmentId)
        {
            if (equipmentId == null || !definition.Catalog.TryGet(equipmentId, out EquipmentDefinition item))
            {
                return 0;
            }

            int inHands = string.Equals(CarriedEquipmentId, equipmentId, StringComparison.Ordinal) ? 1 : 0;
            return Math.Max(0, item.MaxQuantity - GetLoaded(equipmentId) - inHands);
        }

        /// <summary>What using this type's supply pallet would do now (pick up or put back), or why it cannot.</summary>
        public CommandResult CheckSupply(string equipmentId)
        {
            CommandResult open = CheckOpen(equipmentId, out EquipmentDefinition item);
            if (!open.Succeeded)
            {
                return open;
            }

            string name = Name(item);
            if (CarriedEquipmentId == null)
            {
                int left = GetSupplyRemaining(equipmentId);
                return left > 0
                    ? Ok(string.Format(CultureInfo.InvariantCulture, "Pick up {0}: {1:0} kg, {2:0} credits, {3} ({4} on the pallet)",
                        name, item.UnitMassKg, item.UnitBudgetCredits, DescribeEffect(item), left))
                    : Fail(CommandFailure.PreconditionsNotMet, item.MaxQuantity == 1
                        ? "The only " + name + " is already aboard"
                        : string.Format(CultureInfo.InvariantCulture, "No {0} left on the pallet: all {1} are aboard", name, item.MaxQuantity));
            }

            return string.Equals(CarriedEquipmentId, equipmentId, StringComparison.Ordinal)
                ? Ok("Put the " + name + " back on the pallet")
                : Fail(CommandFailure.PreconditionsNotMet, "Hands full: carrying " + CarriedName());
        }

        /// <summary>Picks a crate up from its pallet, or puts the carried crate of this type back.</summary>
        public CommandResult TryUseSupply(string equipmentId)
        {
            CommandResult check = CheckSupply(equipmentId);
            if (!check.Succeeded)
            {
                return check;
            }

            definition.Catalog.TryGet(equipmentId, out EquipmentDefinition item);
            if (CarriedEquipmentId == null)
            {
                SetCarried(equipmentId);
                return Ok("Carrying " + Name(item) + ".");
            }

            SetCarried(null);
            return Ok(Capitalize(Name(item)) + " back on the pallet.");
        }

        /// <summary>What using this type's bay stack would do now (load or unload), or why it cannot.</summary>
        public CommandResult CheckBay(string equipmentId)
        {
            CommandResult open = CheckOpen(equipmentId, out EquipmentDefinition item);
            if (!open.Succeeded)
            {
                return open;
            }

            string name = Name(item);
            MissionConfiguration draft = planner.DraftConfiguration;
            if (CarriedEquipmentId == null)
            {
                int loaded = draft.GetQuantity(equipmentId);
                return loaded > 0
                    ? Ok(string.Format(CultureInfo.InvariantCulture, "Unload one {0} ({1} aboard)", name, loaded))
                    : Fail(CommandFailure.PreconditionsNotMet, "No " + name + " aboard");
            }

            if (!string.Equals(CarriedEquipmentId, equipmentId, StringComparison.Ordinal))
            {
                return Fail(CommandFailure.PreconditionsNotMet, string.Format(CultureInfo.InvariantCulture,
                    "This stack takes {0}; you are carrying {1}", name, CarriedName()));
            }

            double after = draft.TotalMassKg + item.UnitMassKg;
            return Ok(string.Format(CultureInfo.InvariantCulture, "Load {0}: payload {1:0} -> {2:0} of {3:0} kg{4}",
                name, draft.TotalMassKg, after, draft.MassLimitKg, after > draft.MassLimitKg ? " (over the limit)" : string.Empty));
        }

        /// <summary>Loads the carried crate of this type into the bay, or takes one out of it.</summary>
        public CommandResult TryUseBay(string equipmentId)
        {
            CommandResult check = CheckBay(equipmentId);
            if (!check.Succeeded)
            {
                return check;
            }

            definition.Catalog.TryGet(equipmentId, out EquipmentDefinition item);
            int loaded = GetLoaded(equipmentId);
            bool loading = CarriedEquipmentId != null;
            CommandResult changed = planner.TrySetEquipment(equipmentId, loading ? loaded + 1 : loaded - 1);
            if (!changed.Succeeded)
            {
                return changed;
            }

            SetCarried(loading ? null : equipmentId);
            if (loading)
            {
                CommandResult valid = planner.DraftConfiguration.Validate();
                return Ok(valid.Succeeded
                    ? Capitalize(Name(item)) + " loaded."
                    : Capitalize(Name(item)) + " loaded. " + valid.Explanation);
            }

            return Ok("Carrying " + Name(item) + ".");
        }

        /// <summary>"+12 kg oxygen", "+3 spare parts", "science payload, 300 W load": what one crate adds.</summary>
        public static string DescribeEffect(EquipmentDefinition item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            Add(parts, item.OxygenKg, "+{0:0.##} kg oxygen");
            Add(parts, item.WaterLitres, "+{0:0.##} L water");
            Add(parts, item.FoodKg, "+{0:0.##} kg food");
            Add(parts, item.BatteryKWh, "+{0:0.##} kWh battery");
            Add(parts, item.GenerationWatts, "+{0:0} W generation");
            if (item.SpareParts > 0)
            {
                parts.Add(string.Format(CultureInfo.InvariantCulture, "+{0} spare part{1}", item.SpareParts, item.SpareParts == 1 ? string.Empty : "s"));
            }

            if (item.IsScience)
            {
                parts.Add("science payload");
            }

            Add(parts, item.UnitPowerWatts, "{0:0} W load");
            return parts.Count == 0 ? "no effect" : string.Join(", ", parts);
        }

        private static void Add(List<string> parts, double value, string format)
        {
            if (value > 0d)
            {
                parts.Add(string.Format(CultureInfo.InvariantCulture, format, value));
            }
        }

        private CommandResult CheckOpen(string equipmentId, out EquipmentDefinition item)
        {
            item = null;
            if (state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (planner.HasLaunched || state.Phase != GamePhase.Planning)
            {
                return Fail(CommandFailure.WrongPhase, "The cargo bay is sealed after launch.");
            }

            if (planner.IsSealed)
            {
                return Fail(CommandFailure.WrongPhase, PlanningDeadline.LoadingClosedMessage);
            }

            if (equipmentId == null || !definition.Catalog.TryGet(equipmentId, out item))
            {
                return Fail(CommandFailure.InvalidInput, $"Unknown equipment '{equipmentId}'.");
            }

            return Ok(string.Empty);
        }

        private void OnPhaseChanged(PhaseChanged message)
        {
            if (message.Previous != GamePhase.Planning || CarriedEquipmentId == null)
            {
                return;
            }

            string name = CarriedName();
            SetCarried(null);
            if (log != null)
            {
                log("At launch a crate was still in hand (" + name + "). It was not counted: only the frozen loadout is aboard.");
            }
        }

        /// <summary>The boarding-mode deadline sealed the manifest: a crate in hands stays behind, exactly as at launch.</summary>
        private void OnSealed()
        {
            if (CarriedEquipmentId == null)
            {
                return;
            }

            string name = CarriedName();
            SetCarried(null);
            if (log != null)
            {
                log("Loading closed with a crate still in hand (" + name + "). It was not counted: only the sealed loadout is aboard.");
            }
        }

        private void SetCarried(string equipmentId)
        {
            if (string.Equals(CarriedEquipmentId, equipmentId, StringComparison.Ordinal))
            {
                return;
            }

            CarriedEquipmentId = equipmentId;
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }

        private string CarriedName()
        {
            return definition.Catalog.TryGet(CarriedEquipmentId, out EquipmentDefinition carried) ? WithArticle(Name(carried)) : "a crate";
        }

        private static string WithArticle(string name)
        {
            return name.Length > 0 && "aeiou".IndexOf(name[0]) >= 0 ? "an " + name : "a " + name;
        }

        private static string Name(EquipmentDefinition item)
        {
            return item.DisplayName.ToLowerInvariant();
        }

        private static string Capitalize(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
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
