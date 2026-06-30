using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Settings.Configs
{
    /// <summary>
    /// Serialisable logging preferences loaded from game_config.json.
    /// Allows toggling file and console output independently, filtering
    /// by minimum severity level, and whitelisting named categories.
    /// Used by <see cref="GameConfig.Logging"/> to initialise
    /// <see cref="Cthangover.Core.Utils.GameLogger"/> at boot.
    /// </summary>
    [Serializable]
    public class LoggingSection
    {
        /// <summary>Master toggle; when <c>false</c> all logging is suppressed.</summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>If <c>true</c>, log entries are also printed to the Godot console.</summary>
        [JsonPropertyName("console_enabled")]
        public bool ConsoleEnabled { get; set; } = false;

        /// <summary>Minimum severity level ("debug", "info", "warning", "error").
        /// Entries below this level are dropped.</summary>
        [JsonPropertyName("minimum_level")]
        public string MinimumLevel { get; set; } = "debug";

        /// <summary>Category whitelist; when non-empty, only messages tagged
        /// with one of these categories are recorded.</summary>
        [JsonPropertyName("enabled_categories")]
        public List<string> EnabledCategories { get; set; } = new();
    }
}