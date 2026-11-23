namespace Cthangover.Core.Events
{
    /// <summary>
    /// Announces a local development refresh after lookup caches were invalidated. Native representations
    /// remain owned by their respective mods, which can rebuild them independently.
    /// </summary>
    public sealed class ModAssetsRefreshedEvent : ModEvent
    {
        /// <summary>Mod whose files changed.</summary>
        public string ModId { get; init; }

        /// <summary>Optional mod-relative path identifying the changed resource.</summary>
        public string Path { get; init; }
    }
}
