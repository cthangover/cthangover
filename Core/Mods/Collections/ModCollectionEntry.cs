using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Entry point of a <see cref="ModCollectionKind.Game"/> collection: the
    /// logical scene a new game opens. Kept as an object so later entry
    /// options (intro scenario, starting flags) extend it without breaking
    /// existing documents.
    /// </summary>
    public class ModCollectionEntry
    {
        /// <summary>Logical scene name (a scene definition from one of the collection's mods).</summary>
        [JsonPropertyName("scene")]
        public string Scene { get; set; }
    }
}
