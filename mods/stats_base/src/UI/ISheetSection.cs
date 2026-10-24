namespace Cthangover.Mods.Stats
{
    using Godot;

    /// <summary>
    /// A block of the character sheet contributed by another mod: equipment shows the
    /// slots, a reputation mod could show factions, a 3D game the implants. The sheet
    /// owns the layout (a titled block below the stats) and asks each section to fill its
    /// container for the selected character; sections in any mod assembly are found
    /// automatically (<see cref="SheetSectionConsumer"/>). The sheet rebuilds the sections
    /// whenever <see cref="Cthangover.Core.Events.StatsChangedEvent"/> arrives, so a section
    /// that changes stats (taking an item off) needs no refresh logic of its own.
    /// </summary>
    public interface ISheetSection
    {
        /// <summary>Stable id (logs, tests).</summary>
        string Id { get; }

        /// <summary>Order among sections (lower first).</summary>
        int Order { get; }

        /// <summary>Locale key of the block title.</summary>
        string TitleKey { get; }

        /// <summary>Whether the block makes sense for <paramref name="characterId"/> (false hides it).</summary>
        bool AppliesTo(string characterId);

        /// <summary>Adds the block's rows for <paramref name="characterId"/> to the empty <paramref name="container"/>.</summary>
        void Build(VBoxContainer container, string characterId);
    }
}
