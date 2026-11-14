using System.Collections.Generic;

namespace OutOfReach.Contracts
{
    /// <summary>
    /// Planning UI command boundary, implemented by Core. The catalog contains immutable authoritative
    /// unit values; clients select equipment IDs and counts, never supply mass, power, or pricing.
    /// Catalog quantities describe one selectable unit. Draft is a read-only snapshot, never a mutable
    /// view into planner state. All commands are synchronous and run on the main thread.
    /// </summary>
    public interface IMissionPlanner
    {
        IReadOnlyList<EquipmentSelection> Catalog { get; }
        IMissionConfiguration Draft { get; }

        /// <summary>
        /// Replace the selected count for a known equipment ID; zero removes it. Reject negative
        /// quantities, unknown IDs, and commands outside Planning without mutating the draft.
        /// An over-limit draft may be displayed for correction; Validate reports its constraint failure.
        /// </summary>
        CommandResult TrySetEquipment(string equipmentId, int quantity);

        /// <summary>
        /// Validate the entire draft against authoritative mass, budget, power and mission prerequisites.
        /// On success, freeze a defensive configuration copy before transitioning from Planning to Intro.
        /// Invalid or repeated launches change neither configuration, resources nor game phase.
        /// </summary>
        CommandResult TryLaunch();
    }
}
