using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    public class ModConfig
    {
        private static readonly Lazy<ModConfig> instance = new(() =>
        {
            var config = new ModConfig();
            config.Load();
            return config;
        });
        public static ModConfig Instance => instance.Value;

        [JsonPropertyName("audio_extensions")]
        public List<string> AudioExtensions { get; set; } = new() { ".ogg", ".wav" };
        
        [JsonPropertyName("texture_extensions")]
        public List<string> TextureExtensions { get; set; } = new() { ".png", ".jpg", ".jpeg" };

        [JsonPropertyName("shader_extensions")]
        public List<string> ShaderExtensions { get; set; } = new() { ".gdshader", ".gdshaderinclude" };

        [JsonPropertyName("texture_groups")]
        public List<string> TextureGroups { get; set; } = new() { "ui", "backgrounds", "avatars", "icons", "items", "characters", "effects", "skills" };
        
        [JsonPropertyName("use_assembly_cache")]
        public bool UseAssemblyCache { get; set; } = true;

        [JsonPropertyName("cache")]
        public CacheConfig Cache { get; set; } = new();

        private HashSet<string> textureExtensionSet;
        private HashSet<string> shaderExtensionSet;

        public HashSet<string> GetTextureExtensionSet()
        {
            if (textureExtensionSet == null)
                textureExtensionSet = new HashSet<string>(TextureExtensions, StringComparer.OrdinalIgnoreCase);
            return textureExtensionSet;
        }

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
