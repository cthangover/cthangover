using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// JSON DTO for the <c>manifest.json</c> file required inside every
    /// mod.
    ///
    /// <c>Id</c> is the canonical mod identifier used for
    /// deduplication and dependency resolution. When two mods declare
    /// the same <c>Id</c>, the one with the higher <c>Version</c> wins
    /// and the lower one is ignored. Falls back to the filesystem entry
    /// name when absent.
    ///
    /// <c>Sources</c> declares glob-style paths to C# files that
    /// <c>ModCompiler</c> should compile; <c>Depends</c> lists versioned
    /// dependencies (format: modId[op][version][?]) that affect topological
    /// compile order and assembly reference resolution;
    /// <c>ConflictsWith</c> declares mutually exclusive mods;
    /// <c>Subscriptions</c> binds wrapper templates to scene lifecycle
    /// events so that mods can inject UI layers without patching scene
    /// files.
    /// </summary>
    public class ModManifest
    {
        /// <summary>
        /// Canonical mod identifier. Used for deduplication (higher
        /// <c>Version</c> wins) and in <c>depends</c> lists of other
        /// mods. When absent, the filesystem entry name is used instead.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>Human-readable mod name (shown in UI).</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>Mod author credit string.</summary>
        [JsonPropertyName("author")]
        public string Author { get; set; }

        /// <summary>Short description of what the mod does.</summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// Optional catalog category (or combination) used to group the
        /// mod in the online catalog. Serialized as a string via
        /// <c>JsonStringEnumConverter</c> — a single name (e.g.
        /// <c>"gameplay"</c>) or a comma-separated list for mods that
        /// belong to several categories (e.g. <c>"library, gameplay"</c>).
        /// Omitted means <see cref="CategoryType.Other"/>. The Catalog
        /// Export dialog pre-checks these categories for
        /// <see cref="CatalogExportEntry.Category"/>.
        /// </summary>
        [JsonPropertyName("category")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CategoryType? Category { get; set; }

        /// <summary>
        /// Glob-style paths to C# source files (e.g. "scripts/*.cs",
        /// "src/"). <c>ModCompiler</c> collects and compiles them.
        /// </summary>
        [JsonPropertyName("sources")]
        public List<string> Sources { get; set; }

        /// <summary>
        /// Glob-style paths to GDScript source files (e.g. "scripts/*.gd",
        /// "gd/**/*.gd"). <c>ModGDScriptLoader</c> collects, compiles via
        /// Godot's GDScript runtime, and registers as scenario actions or
        /// mod initializers.
        /// </summary>
        [JsonPropertyName("gd_sources")]
        public List<string> GdSources { get; set; }

        /// <summary>
        /// Glob-style paths to native shared libraries (e.g. "bin/*.dll",
        /// "native/*.so", "lib/*.dylib"). <c>ModNativeLoader</c> collects
        /// platform-appropriate files, copies them to a cache directory,
        /// and registers a <c>DllImportResolver</c> on the core assembly
        /// so that P/Invoke declarations resolve against mod-provided
        /// native code.
        /// </summary>
        [JsonPropertyName("depends_native")]
        public List<string> DependsNative { get; set; }

        /// <summary>
        /// Versioned dependencies. Each entry is a string in format
        /// "modId[op][version][?]". Parsed into <see cref="ModDependency"/>
        /// via <see cref="ModDependencyListConverter"/>.
        /// Affects topological compile order and version validation.
        /// </summary>
        [JsonPropertyName("depends")]
        [JsonConverter(typeof(ModDependencyListConverter))]
        public List<ModDependency> Depends { get; set; }

        /// <summary>
        /// Convenience property: only the valid dependency mod IDs,
        /// for algorithms that don't need version operators.
        /// </summary>
        [JsonIgnore]
        public List<string> DependencyIds => Depends?
            .Where(d => d.IsValid)
            .Select(d => d.ModId)
            .ToList();

        /// <summary>
        /// Mutually exclusive mod IDs. If both mods are active,
        /// both are disabled with a conflict warning.
        /// </summary>
        [JsonPropertyName("conflicts_with")]
        public List<string> ConflictsWith { get; set; }

        /// <summary>Semver mod version (e.g. "1.0.0"). Used by the save
        /// system to detect downgrades and trigger migrations.</summary>
        [JsonPropertyName("version")]
        public string Version { get; set; }

        /// <summary>
        /// Mod-provided GUI declarations. Each entry is either a toolbar-toggled
        /// panel or a code-driven overlay, anchored to a named slot of the shell.
        /// Collected and validated by <c>UiRegistry</c>.
        /// </summary>
        [JsonPropertyName("ui")]
        public List<UiEntry> Ui { get; set; }

        /// <summary>
        /// Mod-provided buttons for the title screen. Each entry adds a button to
        /// the main menu that opens a fullscreen mod scene. Collected and validated
        /// by <c>MainMenuRegistry</c>; built once per menu instantiation by
        /// <c>MainMenuBuilder</c>. Only enabled mods contribute buttons.
        /// </summary>
        [JsonPropertyName("main_menu")]
        public List<MainMenuEntry> MainMenu { get; set; }
    }
}
