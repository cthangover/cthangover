using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// A curated set of mods that together implement a game mechanic
    /// ("card novel", "developer tools") — the game-construction unit the mod
    /// catalog is organized by. Unlike <see cref="CategoryType"/>, which labels
    /// a single mod, a collection describes a composition: the same mod can be a
    /// required backbone in one collection and an optional layer in another.
    /// <para/>
    /// Collections are authored as JSON files shipped inside mod folders
    /// (<c>collections/*.json</c>), fetched from online catalogs, or created by
    /// the player (<c>user://collections.json</c>) — see
    /// <see cref="ModCollectionStore"/>. The dependency graph shown to the player
    /// is not stored here: it is derived from the mods' own <c>depends</c> by
    /// <see cref="ModCollectionResolver"/>, with <see cref="Mechanics"/> only
    /// telling the graph view how to group the nodes.
    /// </summary>
    public class ModCollection
    {
        /// <summary>Canonical collection id, unique across all sources.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>Human-readable collection name.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>Author credit string.</summary>
        [JsonPropertyName("author")]
        public string Author { get; set; }

        /// <summary>Short description of the gameplay or toolset the collection assembles.</summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>Semver version, used to pick the newest of identically-named catalog collections.</summary>
        [JsonPropertyName("version")]
        public string Version { get; set; }

        /// <summary>
        /// Ordered mechanics that make up the collection. The order drives the
        /// top-to-bottom layout of the graph view; one mod belongs to the first
        /// mechanic that lists it.
        /// </summary>
        [JsonPropertyName("mechanics")]
        public List<ModCollectionMechanic> Mechanics { get; set; }

        /// <summary>
        /// Catalog URLs that published this collection. Filled by
        /// <see cref="CatalogService"/> during refresh; null for local files.
        /// </summary>
        [JsonPropertyName("source_urls")]
        public List<string> SourceUrls { get; set; }

        /// <summary>Where this instance was loaded from; set by <see cref="ModCollectionStore"/>.</summary>
        [JsonIgnore]
        public ModCollectionOrigin Origin { get; set; }

        /// <summary>Safe UI label falling back to the id.</summary>
        [JsonIgnore]
        public string DisplayTitle => string.IsNullOrEmpty(Name) ? Id : Name;
    }
}
