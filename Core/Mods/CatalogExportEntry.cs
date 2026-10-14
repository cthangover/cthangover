using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// JSON DTO for a single mod entry in the online catalog. Designed
    /// to be embedded into the <c>"mods"</c> array of a catalog file
    /// hosted on a web server. Every field maps directly to the catalog
    /// schema researched from the in-game mod list UI: name, description,
    /// dependencies, thumbnail URL, download metadata, and two SHA256
    /// checksums for integrity and modification-detection.
    ///
    /// <see cref="Depends"/> and <see cref="ConflictsWith"/> use the
    /// same string format as the local <c>manifest.json</c>
    /// (e.g. <c>"visual_novel_core>=1"</c>, <c>"interface>=1?"</c>).
    /// </summary>
    public class CatalogExportEntry
    {
        /// <summary>Canonical mod identifier (from manifest.json).</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>Human-readable mod name.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>Mod author credit string.</summary>
        [JsonPropertyName("author")]
        public string Author { get; set; }

        /// <summary>Semver version string (e.g. "1.0.0").</summary>
        [JsonPropertyName("version")]
        public string Version { get; set; }

        /// <summary>Short description of what the mod does.</summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// Localized name/description entries. When populated the game
        /// picks the best match for the player's current language:
        /// requested lang → "en" → first available → flat fields.
        /// </summary>
        [JsonPropertyName("locale")]
        public List<CatalogLocaleEntry> Locale { get; set; }

        /// <summary>Category (or comma-separated combination of
        /// categories, e.g. "Library, Gameplay") for filtering and
        /// grouping. Serialised as enum name(s).</summary>
        [JsonPropertyName("category")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CategoryType Category { get; set; }

        /// <summary>Search tags (e.g. ["cooking", "crafting"]).</summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }

        /// <summary>Full URL to the mod's preview icon (PNG or JPEG).</summary>
        [JsonPropertyName("icon_url")]
        public string IconUrl { get; set; }

        /// <summary>
        /// Versioned dependency strings in the same format as local
        /// manifest.json (e.g. "visual_novel_core>=1", "interface>=2?").
        /// </summary>
        [JsonPropertyName("depends")]
        public List<string> Depends { get; set; }

        /// <summary>Mutually exclusive mod IDs.</summary>
        [JsonPropertyName("conflicts_with")]
        public List<string> ConflictsWith { get; set; }

        /// <summary>
        /// Version-specific download entries with mirror URLs and
        /// per-version checksums. Multiple entries allow downloading
        /// older versions of the mod.
        /// </summary>
        [JsonPropertyName("downloads")]
        public List<CatalogEntryDownload> Downloads { get; set; }

        /// <summary>Release status: stable, beta, deprecated, archived.</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; }

        /// <summary>Minimum compatible game version (semver).</summary>
        [JsonPropertyName("min_game_version")]
        public string MinGameVersion { get; set; }

        /// <summary>Maximum compatible game version, or null if unbounded.</summary>
        [JsonPropertyName("max_game_version")]
        public string MaxGameVersion { get; set; }

        /// <summary>
        /// Catalog URLs this entry was fetched from. Filled by
        /// <see cref="CatalogService"/> during refresh (one URL per catalog that
        /// published the mod) and persisted in the on-disk cache so the UI can show
        /// where a mod comes from without a network round-trip. Null for entries
        /// built for export — exporters omit it.
        /// </summary>
        [JsonPropertyName("source_urls")]
        public List<string> SourceUrls { get; set; }

        /// <summary>
        /// Set when several catalogs publish the same id with the same version and
        /// title but different content hashes. Such entries are not merged: each
        /// variant stays a separate entry flagged for the UI to highlight, because
        /// one of the catalogs is likely serving a modified build. Null (omitted)
        /// for normal entries so catalog export JSON stays clean.
        /// </summary>
        [JsonPropertyName("variant_conflict")]
        public bool? VariantConflict { get; set; }

        /// <summary>Display name for the language: matching locale entry, then "en", then the first entry, then the flat <see cref="Name"/> (or id).</summary>
        public string GetLocalizedName(string language)
        {
            var locale = ResolveLocale(language);
            if (locale != null && !string.IsNullOrEmpty(locale.Name))
                return locale.Name;
            return Name ?? Id;
        }

        /// <summary>Description for the language: matching locale entry, then "en", then the first entry, then the flat <see cref="Description"/>.</summary>
        public string GetLocalizedDescription(string language)
        {
            var locale = ResolveLocale(language);
            if (locale != null && !string.IsNullOrEmpty(locale.Description))
                return locale.Description;
            return Description ?? string.Empty;
        }

        private CatalogLocaleEntry ResolveLocale(string language)
        {
            if (Locale == null || Locale.Count == 0)
                return null;
            return Locale.FirstOrDefault(l => l.Lang == language)
                ?? Locale.FirstOrDefault(l => l.Lang == "en")
                ?? Locale[0];
        }
    }

    /// <summary>
    /// Version-specific download entry with mirror URLs and per-version
    /// integrity checksums. Multiple mirrors for the same version are
    /// supported — the game will try each URL in order.
    /// </summary>
    public class CatalogEntryDownload
    {
        /// <summary>Semver version this download entry provides.</summary>
        [JsonPropertyName("version")]
        public string Version { get; set; }

        /// <summary>Mirror URLs for the mod's .zip archive. The game
        /// tries each URL in order until one succeeds.</summary>
        [JsonPropertyName("urls")]
        public List<string> Urls { get; set; }

        /// <summary>File size of the downloadable archive in bytes, for
        /// progress-bar display before the download starts.</summary>
        [JsonPropertyName("size_bytes")]
        public long SizeBytes { get; set; }

        /// <summary>
        /// SHA256 checksum of the downloadable .zip archive for this
        /// specific version. Used to verify download integrity.
        /// </summary>
        [JsonPropertyName("archive_sha256")]
        public string ArchiveSha256 { get; set; }

        /// <summary>
        /// SHA256 checksum of the mod's extracted content for this
        /// specific version. Compared against the locally-installed
        /// copy to detect player modifications.
        /// </summary>
        [JsonPropertyName("install_sha256")]
        public string InstallSha256 { get; set; }

        /// <summary>Optional changelog describing what changed
        /// in this specific version.</summary>
        [JsonPropertyName("changelog")]
        public string Changelog { get; set; }
    }
}
