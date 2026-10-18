namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>
    /// A key/value of game state in the "State" tab, grouped by its owner (time,
    /// quests, inventory…). Groups compare by <see cref="StudioText.Key"/>.
    /// </summary>
    public sealed class StudioStateEntry
    {
        public StudioText Group { get; set; }
        public StudioText Key { get; set; }
        public StudioText Value { get; set; }
    }
}
