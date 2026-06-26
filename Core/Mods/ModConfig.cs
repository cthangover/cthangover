using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    /// </summary>
    public class ModConfig
    {
        private static readonly Lazy<ModConfig> instance = new(() =>
        {
            var config = new ModConfig();
            config.Load();
            return config;
        });
        /// <summary>Thread-safe singleton, loaded from config/mod_config.json on first access.</summary>
        public static ModConfig Instance => instance.Value;

        /// <summary>File extensions treated as audio by <c>AudioFactory</c> (.ogg, .wav).</summary>
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

        [JsonConstructor]
        public ModConfig()
        {
        }

        private void Load()
        {
            var configPath = ProjectSettings.GlobalizePath("res://config/mod_config.json");
            if (!File.Exists(configPath))
            {
                GameLogger.Log("CONFIG", "mod_config.json not found, using defaults", LogLevel.Error);
                return;
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var parsed = JsonSerializer.Deserialize<ModConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed == null)
                    return;

                TextureExtensions = parsed.TextureExtensions ?? new List<string>();
                ShaderExtensions = parsed.ShaderExtensions ?? new List<string>();
                TextureGroups = parsed.TextureGroups ?? new List<string>();
                UseAssemblyCache = parsed.UseAssemblyCache;
                Cache = parsed.Cache ?? new CacheConfig();

                GameLogger.Log("CONFIG", $"Loaded mod_config.json: {TextureExtensions.Count} texture ext, {ShaderExtensions.Count} shader ext, {TextureGroups.Count} texture groups");
            }
            catch (Exception ex)
            {
                GameLogger.Log("CONFIG", $"Failed to parse mod_config.json: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
