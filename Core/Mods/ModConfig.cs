using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Singleton configuration bridge between the mod system and the JSON
    /// config file at <c>config/mod_config.json</c>. Centralises every
    /// mod-related tunable — which file extensions map to which asset
    /// pipeline, which resource groups are scanned for textures, whether
    /// compiled assembly caching is enabled, and per-factory LRU cache
    /// size overrides. The extension sets are pre-converted to
    /// <c>HashSet</c> on first access so that repeated lookups during
    /// file scanning are O(1) rather than O(n) list scans.
    /// <para/>
    /// The single instance is owned by the <see cref="AppServices"/>
    /// composition root; the static facade stays as the public API during
    /// migration, with laziness preserved by the owning <c>Lazy</c> slot.
    /// </summary>
    public class ModConfig
    {
        /// <summary>
        /// Facade over the AppServices-owned instance; kept as the public API
        /// during migration. Thread safety and laziness come from the owning
        /// <c>Lazy</c> slot; the instance is loaded from
        /// config/mod_config.json on first access at runtime.
        /// </summary>
        public static ModConfig Instance => AppServices.Current.ModConfigService.Value;

        /// <summary>
        /// Creates the instance for the composition root: a fresh config
        /// with defaults, populated from config/mod_config.json when running
        /// inside the Godot engine. Outside the runtime (unit tests) Load
        /// returns early, so the instance carries pure defaults.
        /// </summary>
        internal static ModConfig CreateInstance()
        {
            var config = new ModConfig();
            config.Load();
            return config;
        }

        /// <summary>File extensions treated as audio by the audio factories (.ogg, .wav).</summary>
        [JsonPropertyName("audio_extensions")]
        public List<string> AudioExtensions { get; set; } = new() { ".ogg", ".wav" };
        
        /// <summary>File extensions treated as textures (.png, .jpg, .jpeg).</summary>
        [JsonPropertyName("texture_extensions")]
        public List<string> TextureExtensions { get; set; } = new() { ".png", ".jpg", ".jpeg" };

        /// <summary>File extensions treated as shaders (.gdshader, .gdshaderinclude).</summary>
        [JsonPropertyName("shader_extensions")]
        public List<string> ShaderExtensions { get; set; } = new() { ".gdshader", ".gdshaderinclude" };

        /// <summary>
        /// Mod directory groups that are scanned for textures. Each
        /// group is a top-level directory inside a mod (e.g. "avatars",
        /// "items"). <c>CollectTextures</c> merges files from all these
        /// groups into a single flat namespace.
        /// </summary>
        [JsonPropertyName("texture_groups")]
        public List<string> TextureGroups { get; set; } = new() { "ui", "backgrounds", "avatars", "icons", "items", "characters", "effects", "skills" };
        
        /// <summary>
        /// When true, cached compilation output is reused across runs.
        /// When false, cached DLLs are deleted and recompiled every
        /// time — useful during mod development.
        /// </summary>
        [JsonPropertyName("use_assembly_cache")]
        public bool UseAssemblyCache { get; set; } = true;

        /// <summary>Per-factory cache configuration (root path, size overrides).</summary>
        [JsonPropertyName("cache")]
        public CacheConfig Cache { get; set; } = new();

        /// <summary>
        /// Master gate of the mod unit-test mode (plan-mod-testing.md §3.1/§5.2).
        /// When false (the default) <see cref="Cthangover.Core.Mods.Testing.ModTestEngine"/>
        /// is never activated by config alone: no test compilation, no
        /// <c>__tests</c> assemblies in memory. The CLI flag
        /// <c>--run-tests</c> overrides this setting for a single run.
        /// </summary>
        [JsonPropertyName("run_mod_tests")]
        public bool RunModTests { get; set; } = false;

        /// <summary>
        /// Mod ids temporarily excluded from the mod unit-test run — lets an
        /// author disable a foreign broken mod from the shared run without
        /// editing that mod. Applies only in test mode.
        /// </summary>
        [JsonPropertyName("mod_test_exclude")]
        public List<string> ModTestExclude { get; set; } = new();

        /// <summary>
        /// File path patterns to exclude when computing the mod's install
        /// SHA256 checksum for catalog export. Each pattern is a simple
        /// glob matched against path components. Default: <c>".*"</c>
        /// excludes all files and directories whose name starts with a dot
        /// (<c>.godot/</c>, <c>.import/</c>, <c>.gitignore</c>, etc.).
        /// </summary>
        [JsonPropertyName("catalog_ignore_patterns")]
        public List<string> CatalogIgnorePatterns { get; set; } = new() { ".*" };

        /// <summary>
        /// URLs of catalog JSON files to fetch when browsing online mods.
        /// Each URL should point to a JSON document following the catalog
        /// schema (a <c>CatalogRoot</c> envelope containing a <c>"mods"</c>
        /// array of <see cref="CatalogExportEntry"/> objects). Multiple
        /// URLs are merged and deduplicated by mod ID (highest version
        /// wins).
        /// </summary>
        [JsonPropertyName("catalog_urls")]
        public List<string> CatalogUrls { get; set; } = new();

        private HashSet<string> textureExtensionSet;
        private HashSet<string> shaderExtensionSet;

        /// <summary>
        /// Returns a lazily-built <c>HashSet</c> of texture extensions
        /// for O(1) membership tests during file scanning.
        /// </summary>
        public HashSet<string> GetTextureExtensionSet()
        {
            if (textureExtensionSet == null)
                textureExtensionSet = new HashSet<string>(TextureExtensions, StringComparer.OrdinalIgnoreCase);
            return textureExtensionSet;
        }

        /// <summary>
        /// Returns a lazily-built <c>HashSet</c> of shader extensions
        /// for O(1) membership tests during file scanning.
        /// </summary>
        public HashSet<string> GetShaderExtensionSet()
        {
            if (shaderExtensionSet == null)
                shaderExtensionSet = new HashSet<string>(ShaderExtensions, StringComparer.OrdinalIgnoreCase);
            return shaderExtensionSet;
        }

        /// <summary>
        /// Parameterless constructor used by the JSON deserializer
        /// (<c>[JsonConstructor]</c>) when reading mod_config.json; all
        /// fields are populated through property setters.
        /// </summary>
        [JsonConstructor]
        public ModConfig()
        {
        }

        private void Load()
        {
            if (!GameEnvironment.IsRuntime)
                return;

            var configPath = GamePaths.Globalize("res://config/mod_config.json");
            if (!File.Exists(configPath))
            {
                GameLogger.Log("CONFIG", "mod_config.json not found, using defaults", LogLevel.Error);
                return;
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var parsed = JsonSerializer.Deserialize<ModConfig>(json, JsonOptionsProvider.ReadOptions);

                if (parsed == null)
                    return;

                TextureExtensions = parsed.TextureExtensions ?? new List<string>();
                ShaderExtensions = parsed.ShaderExtensions ?? new List<string>();
                TextureGroups = parsed.TextureGroups ?? new List<string>();
                UseAssemblyCache = parsed.UseAssemblyCache;
                Cache = parsed.Cache ?? new CacheConfig();
                CatalogIgnorePatterns = parsed.CatalogIgnorePatterns ?? new List<string> { ".*" };
                CatalogUrls = parsed.CatalogUrls ?? new List<string>();
                RunModTests = parsed.RunModTests;
                ModTestExclude = parsed.ModTestExclude ?? new List<string>();

                GameLogger.Log("CONFIG", $"Loaded mod_config.json: {TextureExtensions.Count} texture ext, {ShaderExtensions.Count} shader ext, {TextureGroups.Count} texture groups");
            }
            catch (Exception ex)
            {
                GameLogger.Log("CONFIG", $"Failed to parse mod_config.json: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
