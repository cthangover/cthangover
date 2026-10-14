namespace Cthangover.Core.Events
{
    /// <summary>Published on the mod event bus when a mod finishes loading or hot-reloading; lets other mods react to new content appearing.</summary>
    [EventKey("mod_loaded")]
    public class ModLoadedEvent : ModEvent
    {
        /// <summary>Id of the mod that finished loading (or re-loading).</summary>
        public string ModId { get; init; }
    }
}
