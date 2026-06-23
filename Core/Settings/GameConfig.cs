using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cthangover.Core.Settings.Configs;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Settings
{
    
    public class GameConfig
    {
        private static bool godotVerified;
        private static bool godotAvailable;

        private static readonly Lazy<GameConfig> instance = new(() =>
        {
            var config = new GameConfig();
            config.Load();
            return config;
        });
        public static GameConfig Instance => instance.Value;
        
        [JsonPropertyName("audio")]    public AudioSection   Audio    { get; set; } = new();
        [JsonPropertyName("logging")]  public LoggingSection Logging  { get; set; } = new();
        [JsonPropertyName("language")] public string         Language { get; set; } = "ru-ru";

        [JsonConstructor]
        private GameConfig()
        {
        }

        public static bool IsGodotRunning()
        {
            if (godotVerified)
                return godotAvailable;
            godotVerified = true;
            try
            {
                var _ = Godot.Engine.GetMainLoop();
                godotAvailable = true;
            }
            catch
            {
                godotAvailable = false;
            }
            return godotAvailable;
        }

        private void Load()
        {
            if (!IsGodotRunning())
                return;

            var configPath = ProjectSettings.GlobalizePath("res://config/game_config.json");
            if (!File.Exists(configPath))
                return;

            try
            {
                var json = File.ReadAllText(configPath);
                var parsed = JsonSerializer.Deserialize<GameConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed == null)
                    return;

                Audio = parsed.Audio ?? new AudioSection();
                Language = parsed.Language ?? "ru-ru";
                Logging = parsed.Logging ?? new LoggingSection();
            }
            catch (Exception ex)
            {
                GameLogger.Log("CONFIG", $"Failed to parse game_config.json: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
