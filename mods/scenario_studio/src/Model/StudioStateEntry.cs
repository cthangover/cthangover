namespace Cthangover.Mods.ScenarioStudio
{
    /// <summary>
    /// A key/value of game state in the "State" tab, grouped by its owner (time,
    /// quests, inventory…). Groups compare by <see cref="StudioText.Key"/>.
    /// <see cref="Id"/> is what <see cref="Editor"/> writes to (a variable name,
    /// item id, quest id).
    /// </summary>
    public sealed class StudioStateEntry
    {
        public StudioText Group { get; set; }
        public StudioText Key { get; set; }
        public StudioText Value { get; set; }
        public StudioStateEditor Editor { get; set; }
        public string Id { get; set; }
    }
}
