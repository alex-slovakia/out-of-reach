using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using OutOfReach.Inventory;

namespace OutOfReach.Core
{
    /// <summary>Everything that defines one playable mission: catalog, limits, crew and named starting loadouts.</summary>
    public sealed class MissionDefinition
    {
        private readonly Dictionary<string, IReadOnlyDictionary<string, int>> presets;

        public MissionDefinition(
            EquipmentCatalog catalog,
            MissionParameters parameters,
            int crewCount,
            IEnumerable<KeyValuePair<string, IReadOnlyDictionary<string, int>>> namedPresets,
            string defaultPresetId,
            MissionScenario scenario)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            if (crewCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(crewCount), crewCount, "A crewed mission needs at least one crew member.");
            }

            CrewCount = crewCount;
            presets = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal);
            var ids = new List<string>();
            if (namedPresets != null)
            {
                foreach (KeyValuePair<string, IReadOnlyDictionary<string, int>> preset in namedPresets)
                {
                    if (string.IsNullOrWhiteSpace(preset.Key) || preset.Value == null)
                    {
                        throw new ArgumentException("Presets need an ID and a loadout.", nameof(namedPresets));
                    }

                    var copy = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (KeyValuePair<string, int> entry in preset.Value)
                    {
                        if (!catalog.TryGet(entry.Key, out EquipmentDefinition item) || entry.Value < 0 || entry.Value > item.MaxQuantity)
                        {
                            throw new ArgumentException($"Preset '{preset.Key}' has an invalid entry '{entry.Key}' x{entry.Value}.", nameof(namedPresets));
                        }

                        if (entry.Value > 0)
                        {
                            copy[entry.Key] = entry.Value;
                        }
                    }

                    presets.Add(preset.Key, new ReadOnlyDictionary<string, int>(copy));
                    ids.Add(preset.Key);
                }
            }

            if (defaultPresetId != null && !presets.ContainsKey(defaultPresetId))
            {
                throw new ArgumentException($"Unknown default preset '{defaultPresetId}'.", nameof(defaultPresetId));
            }

            PresetIds = ids.AsReadOnly();
            DefaultPresetId = defaultPresetId;
        }

        public EquipmentCatalog Catalog { get; }

        public MissionParameters Parameters { get; }

        /// <summary>Scripted accident, repair requirements and pacing.</summary>
        public MissionScenario Scenario { get; }

        public int CrewCount { get; }

        public IReadOnlyList<string> PresetIds { get; }

        /// <summary>Loadout the planner starts with, or null for an empty draft.</summary>
        public string DefaultPresetId { get; }

        /// <summary>Read-only copy of a named loadout (equipment ID to count).</summary>
        public IReadOnlyDictionary<string, int> GetPreset(string presetId)
        {
            if (presetId == null || !presets.TryGetValue(presetId, out IReadOnlyDictionary<string, int> preset))
            {
                throw new KeyNotFoundException($"Unknown preset '{presetId}'.");
            }

            return preset;
        }
    }
}
