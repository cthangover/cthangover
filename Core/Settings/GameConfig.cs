using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cthangover.Core.Settings.Configs;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Thread-safe singleton holding deserialised application configuration
    /// from <c>res://config/game_config.json</c>. Wraps audio, display,
    /// language, and logging sections. Initialisation is lazy — the static
    /// <see cref="Instance"/> accessor triggers <see cref="Load"/> on first
    /// use. If the Godot engine is not running (e.g. in unit tests), loading
    /// is silently skipped so the class degrades to defaults.
    /// </summary>
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
        /// <summary>Global singleton instance, constructed on first access.</summary>
        public static GameConfig Instance => instance.Value;
        
        /// <summary>Audio bus preferences (volume, toggles).</summary>
        [JsonPropertyName("audio")]    public AudioSection    Audio    { get; set; } = new();
        /// <summary>Display mode, resolution, vsync, and scale.</summary>
        [JsonPropertyName("display")]  public DisplaySection  Display  { get; set; } = new();
        /// <summary>Logging verbosity and category whitelist.</summary>
        [JsonPropertyName("logging")]  public LoggingSection  Logging  { get; set; } = new();
        /// <summary>Locale code (e.g. "ru-ru", "en-us").</summary>
        [JsonPropertyName("language")] public string          Language { get; set; } = "ru-ru";

        /// <summary>
        /// JSON constructor kept private to enforce singleton access
        /// through <see cref="Instance"/>. The parameterless constructor
        /// is required by <c>System.Text.Json</c> for deserialization.
        /// </summary>
        [JsonConstructor]
        private GameConfig()
        {
        }

        /// <summary>
        /// Checks whether the Godot engine main loop is reachable.
        /// The result is cached after the first call so subsequent
        /// queries are cheap. Used by <see cref="Load"/> to decide
        /// whether file-system access via <c>ProjectSettings</c> is safe.
        /// </summary>
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

        /// <summary>
        /// Reads <c>res://config/game_config.json</c>, deserializes it via
        /// <c>System.Text.Json</c>, and copies each section into the
        /// corresponding property. Missing keys or malformed JSON leave
        /// defaults intact; parse errors are logged.
        /// </summary>
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
                Display = parsed.Display ?? new DisplaySection();
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
