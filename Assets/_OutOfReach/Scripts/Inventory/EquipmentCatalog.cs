using System;
using System.Collections.Generic;
using OutOfReach.Contracts;

namespace OutOfReach.Inventory
{
    /// <summary>Immutable, ordered set of equipment definitions with unique, case-sensitive IDs.</summary>
    public sealed class EquipmentCatalog
    {
        private readonly EquipmentDefinition[] definitions;
        private readonly Dictionary<string, int> indexById;

        public EquipmentCatalog(IEnumerable<EquipmentDefinition> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var ordered = new List<EquipmentDefinition>();
            indexById = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (EquipmentDefinition item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException("Catalog entries cannot be null.", nameof(items));
                }

                if (indexById.ContainsKey(item.Id))
                {
                    throw new ArgumentException($"Duplicate equipment ID '{item.Id}'.", nameof(items));
                }

                indexById.Add(item.Id, ordered.Count);
                ordered.Add(item);
            }

            definitions = ordered.ToArray();
            var entries = new EquipmentSelection[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                entries[i] = definitions[i].ToCatalogEntry();
            }

            Definitions = Array.AsReadOnly((EquipmentDefinition[])definitions.Clone());
            Entries = Array.AsReadOnly(entries);
        }

        /// <summary>Definitions in authoring order (read-only wrapper; cannot be cast back to an array).</summary>
        public IReadOnlyList<EquipmentDefinition> Definitions { get; }

        /// <summary>Contract catalog: one entry per item, quantity 1, authoritative unit values.</summary>
        public IReadOnlyList<EquipmentSelection> Entries { get; }

        public int Count => definitions.Length;

        public bool TryGet(string equipmentId, out EquipmentDefinition definition)
        {
            if (equipmentId != null && indexById.TryGetValue(equipmentId, out int index))
            {
                definition = definitions[index];
                return true;
            }

            definition = null;
            return false;
        }

        /// <summary>Catalog position of an ID, or -1. Used to keep loadout ordering deterministic.</summary>
        public int IndexOf(string equipmentId)
        {
            return equipmentId != null && indexById.TryGetValue(equipmentId, out int index) ? index : -1;
        }
    }
}
