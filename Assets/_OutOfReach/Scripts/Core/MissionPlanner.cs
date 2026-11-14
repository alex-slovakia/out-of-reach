using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using OutOfReach.Inventory;
using OutOfReach.Simulation;

namespace OutOfReach.Core
{
    /// <summary>
    /// Planning command boundary (implements <see cref="IMissionPlanner"/>). The catalog is authoritative; clients
    /// choose IDs and counts only. The draft is an immutable snapshot replaced on every accepted change, so a
    /// reference held by the UI never changes underneath it. Launch validates, freezes a fresh defensive copy,
    /// loads reserves and enters Intro once, then delivers ResourceChanged followed by PhaseChanged.
    /// </summary>
    public sealed class MissionPlanner : IMissionPlanner
    {
        private readonly MissionDefinition definition;
        private readonly MissionGameState state;
        private readonly ResourceLedger resources;
        private readonly MissionEventBus events;
        private readonly Dictionary<string, int> draftQuantities = new Dictionary<string, int>(StringComparer.Ordinal);
        private MissionConfiguration draft;

        public MissionPlanner(
            MissionDefinition definition,
            MissionGameState state,
            ResourceLedger resources,
            MissionEventBus events,
            IEnumerable<KeyValuePair<string, int>> initialDraft = null)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            this.events = events ?? throw new ArgumentNullException(nameof(events));

            if (initialDraft != null)
            {
                foreach (KeyValuePair<string, int> entry in initialDraft)
                {
                    if (!definition.Catalog.TryGet(entry.Key, out EquipmentDefinition item) || entry.Value < 0 || entry.Value > item.MaxQuantity)
                    {
                        throw new ArgumentException($"Invalid starting loadout entry '{entry.Key}' x{entry.Value}.", nameof(initialDraft));
                    }

                    if (entry.Value > 0)
                    {
                        draftQuantities[entry.Key] = entry.Value;
                    }
                }
            }

            draft = BuildDraft();
        }

        public IReadOnlyList<EquipmentSelection> Catalog => definition.Catalog.Entries;

        public IMissionConfiguration Draft => draft;

        /// <summary>Concrete draft with extra read-only detail (quantities, science objectives).</summary>
        public MissionConfiguration DraftConfiguration => draft;

        /// <summary>The frozen configuration, or null before launch.</summary>
        public MissionConfiguration LaunchedConfiguration { get; private set; }

        public bool HasLaunched => LaunchedConfiguration != null;

        /// <summary>Raised after every accepted draft change (Core bookkeeping such as the planning deadline).</summary>
        public event Action DraftChanged;

        public CommandResult TrySetEquipment(string equipmentId, int quantity)
        {
            if (state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (HasLaunched || state.Phase != GamePhase.Planning)
            {
                return Fail(CommandFailure.WrongPhase, "The loadout can only change during Planning; it is frozen after launch.");
            }

            if (string.IsNullOrEmpty(equipmentId) || !definition.Catalog.TryGet(equipmentId, out EquipmentDefinition item))
            {
                return Fail(CommandFailure.InvalidInput, $"Unknown equipment '{equipmentId}'.");
            }

            if (quantity < 0)
            {
                return Fail(CommandFailure.InvalidInput, $"Quantity cannot be negative ({quantity}).");
            }

            if (quantity > item.MaxQuantity)
            {
                return Fail(CommandFailure.InvalidInput, $"'{item.Id}' allows at most {item.MaxQuantity} unit(s).");
            }

            if (draft.GetQuantity(equipmentId) == quantity)
            {
                return Ok($"'{equipmentId}' is already set to {quantity}.");
            }

            if (quantity == 0)
            {
                draftQuantities.Remove(equipmentId);
            }
            else
            {
                draftQuantities[equipmentId] = quantity;
            }

            draft = BuildDraft();
            DraftChanged?.Invoke();
            return Ok(quantity == 0 ? $"Removed '{equipmentId}'." : $"Set '{equipmentId}' to {quantity}.");
        }

        /// <summary>
        /// Replaces the draft with a previously valid configuration and launches it in one step. Used only by the
        /// planning deadline when the current draft is invalid at zero; the draft then shows what was launched.
        /// </summary>
        internal CommandResult TryLaunchConfiguration(MissionConfiguration configuration)
        {
            if (configuration == null)
            {
                return Fail(CommandFailure.InvalidInput, "A configuration is required.");
            }

            if (state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (HasLaunched)
            {
                return Fail(CommandFailure.AlreadyComplete, "The mission has already launched; the loadout is frozen.");
            }

            if (state.Phase != GamePhase.Planning)
            {
                return Fail(CommandFailure.WrongPhase, $"Launch requires Planning; current phase is {state.Phase}.");
            }

            CommandResult validation = configuration.Validate();
            if (!validation.Succeeded)
            {
                return validation;
            }

            draftQuantities.Clear();
            foreach (KeyValuePair<string, int> entry in configuration.CopyQuantities())
            {
                draftQuantities[entry.Key] = entry.Value;
            }

            draft = BuildDraft();
            DraftChanged?.Invoke();
            return TryLaunch();
        }

        public CommandResult TryLaunch()
        {
            if (state.IsClosed)
            {
                return Fail(CommandFailure.WrongPhase, "The mission session has ended.");
            }

            if (HasLaunched)
            {
                return Fail(CommandFailure.AlreadyComplete, "The mission has already launched; the loadout is frozen.");
            }

            if (state.Phase != GamePhase.Planning)
            {
                return Fail(CommandFailure.WrongPhase, $"Launch requires Planning; current phase is {state.Phase}.");
            }

            if (resources.IsInitialized)
            {
                return Fail(CommandFailure.PreconditionsNotMet, "Reserves are already loaded; start a new mission session instead.");
            }

            CommandResult validation = draft.Validate();
            if (!validation.Succeeded)
            {
                return validation;
            }

            // Defensive copy recomputed from the authoritative catalog, independent of the draft object.
            MissionConfiguration frozen = MissionConfiguration.Create(definition.Catalog, definition.Parameters, draftQuantities);
            ResourceRates nominal = NominalResourceModel.Create(definition.CrewCount, frozen.RequiredPowerWatts, frozen.AvailablePowerWatts);

            using (events.DeferDispatch())
            {
                CommandResult loaded = resources.Initialize(frozen.InitialResources, frozen.InitialResources.BatteryKWh, nominal);
                if (!loaded.Succeeded)
                {
                    return loaded;
                }

                CommandResult launched = state.TryCommitLaunch();
                if (!launched.Succeeded)
                {
                    throw new InvalidOperationException("Launch transition failed after reserves were loaded: " + launched.Explanation);
                }

                LaunchedConfiguration = frozen;
            }

            return Ok($"{definition.Parameters.MissionId} launched with {frozen.Equipment.Count} equipment line(s).");
        }

        private MissionConfiguration BuildDraft()
        {
            return MissionConfiguration.Create(definition.Catalog, definition.Parameters, draftQuantities);
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
