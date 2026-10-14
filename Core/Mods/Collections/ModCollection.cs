using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Where this collection belongs in the assembly pyramid. Missing values
        /// deserialize to <see cref="ModCollectionKind.Game"/> so documents
        /// written before kinds existed keep appearing as playable games.
        /// </summary>
        [JsonPropertyName("kind")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ModCollectionKind Kind { get; set; } = ModCollectionKind.Game;

        /// <summary>Short description of the gameplay or toolset the collection assembles.</summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>Free-form search tags ("systems", "cards", "2d", ...).</summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }

        /// <summary>
        /// Capability ids this collection implements ("character_system",
        /// "inventory", ...). Used by the scheme editor to suggest compatible
        /// blocks; resolution does not enforce them.
        /// </summary>
        [JsonPropertyName("provides")]
        public List<string> Provides { get; set; }

        /// <summary>
        /// Capability ids this collection needs from its siblings. The resolver
        /// reports a <see cref="ModCollectionProblemKind.RequirementMissing"/>
        /// problem when no resolved block provides them.
        /// </summary>
        [JsonPropertyName("requires")]
        public List<string> Requires { get; set; }

        /// <summary>
        /// Child collections this one is assembled from, each with its slot role
        /// and optional flag. Includes are the recursive edge of the pyramid:
        /// the same collection may be included by several parents with different
        /// roles, and a parent may also declare its own
        /// <see cref="Mechanics"/>. Resolution flattens both into scheme groups.
        /// </summary>
        [JsonPropertyName("includes")]
        public List<ModCollectionInclude> Includes { get; set; }

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

        /// <summary>
        /// Localized name/description entries, same shape as catalog mod
        /// entries. When populated, consumers pick the best match for the
        /// player's language (requested lang -> flat fields -> "en" -> first),
        /// so a shipped collection can present itself in several languages
        /// without duplicating the document. Values starting with '@' are
        /// translation keys resolved by <see cref="CollectionText.Resolve"/>.
        /// </summary>
        [JsonPropertyName("locale")]
        public List<CatalogLocaleEntry> Locale { get; set; }

        /// <summary>
        /// Name for the given language: requested locale entry -> flat name ->
        /// English locale entry -> first entry -> id. A flat '@key' value is
        /// returned verbatim; the caller (UI or
        /// <see cref="ModCollectionResolver"/>) resolves it through
        /// <see cref="CollectionText.Resolve"/>, keeping this method free of
        /// Godot calls.
        /// </summary>
        public string GetLocalizedName(string language)
        {
            var localized = Match(language, true, englishFallback: false);
            if (localized != null)
                return localized;

            if (!string.IsNullOrEmpty(Name))
                return Name;

            localized = Match(language, true, englishFallback: true);
            if (localized != null)
                return localized;

            var first = Locale?.FirstOrDefault(l => !string.IsNullOrEmpty(l.Name));
            return first?.Name ?? Id;
        }

        /// <summary>
        /// Description for the given language: requested locale entry -> flat
        /// description -> English locale entry -> first entry. A flat '@key'
        /// value is returned verbatim for the caller's translator to resolve.
        /// </summary>
        public string GetLocalizedDescription(string language)
        {
            var localized = Match(language, false, englishFallback: false);
            if (localized != null)
                return localized;

            if (!string.IsNullOrEmpty(Description))
                return Description;

            localized = Match(language, false, englishFallback: true);
            if (localized != null)
                return localized;

            var first = Locale?.FirstOrDefault(l => !string.IsNullOrEmpty(l.Description));
            return first?.Description ?? string.Empty;
        }

        /// <summary>
        /// Scans <see cref="Locale"/> for the requested language; with
        /// <paramref name="englishFallback"/> the 'en' entry is consulted as a
        /// second pass, so callers can slot the flat fields between the two.
        /// </summary>
        private string Match(string language, bool wantName, bool englishFallback)
        {
            if (Locale == null || Locale.Count == 0 || string.IsNullOrEmpty(language))
                return null;

            foreach (var entry in Locale)
            {
                if (string.Equals(entry.Lang, language, System.StringComparison.OrdinalIgnoreCase) && HasText(entry, wantName))
                    return wantName ? entry.Name : entry.Description;
            }

            if (!englishFallback)
                return null;

            foreach (var entry in Locale)
            {
                if (string.Equals(entry.Lang, "en", System.StringComparison.OrdinalIgnoreCase) && HasText(entry, wantName))
                    return wantName ? entry.Name : entry.Description;
            }

            return null;
        }

        private static bool HasText(CatalogLocaleEntry entry, bool wantName)
            => wantName ? !string.IsNullOrEmpty(entry.Name) : !string.IsNullOrEmpty(entry.Description);
    }
}
