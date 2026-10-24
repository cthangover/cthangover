namespace Cthangover.Mods.Stats
{
    using System.Collections.Generic;

    /// <summary>
    /// A mechanic that changes characters' stats while something holds: equipment while
    /// an item is worn, statuses while they last, a buff of a place, a companion's aura.
    /// Implementations in any mod assembly are found and registered automatically
    /// (<see cref="TraitSourceConsumer"/>) and removed with their mod. A source calls
    /// <see cref="StatsService.Changed"/> when what it returns changes, so sheets, battle
    /// attributes and conditions follow at once.
    /// </summary>
    public interface ITraitSource
    {
        /// <summary>Stable id, for logs and for the sheet's "where from" hints.</summary>
        string Id { get; }

        /// <summary>The traits this source gives <paramref name="characterId"/> now (empty when none).</summary>
        IEnumerable<TraitInfo> TraitsOf(string characterId);
    }
}
