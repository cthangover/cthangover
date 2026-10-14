namespace Mods.ModManager
{
    /// <summary>
    /// One row of the "+ мод" picker: the mods known to the local registry and
    /// the online catalog cache, merged by id. The picker excludes ids that are
    /// already resolved in the scheme, so this entity only ever describes mods
    /// the player can still add; <see cref="ModChoice.Id"/> becomes a new string
    /// in the target mechanic's <c>mods</c> array.
    /// </summary>
    public class ModChoice
    {
        /// <summary>Canonical mod id written into the collection document.</summary>
        public string Id { get; set; }

        /// <summary>Display title (localized catalog name or manifest name).</summary>
        public string Title { get; set; }

        /// <summary>Localized description shown under the title.</summary>
        public string Description { get; set; }
    }
}
