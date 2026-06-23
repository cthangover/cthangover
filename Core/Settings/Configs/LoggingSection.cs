using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Settings.Configs
{
    [Serializable]
    public class LoggingSection
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("console_enabled")]
        public bool ConsoleEnabled { get; set; } = false;

        [JsonPropertyName("minimum_level")]
        public string MinimumLevel { get; set; } = "debug";

        [JsonPropertyName("enabled_categories")]
        public List<string> EnabledCategories { get; set; } = new();
    }
}