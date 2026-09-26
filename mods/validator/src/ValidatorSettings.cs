using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Validator
{
    /// <summary>
    /// User-facing settings of the validator, discovered by
    /// <see cref="ModSettingsRegistry"/> from the compiled mod assembly and rendered
    /// by the standard mod settings tab. Values are held in static properties so the
    /// background validation task can read them without owning the discovered
    /// instance.
    ///
    /// <see cref="Reload"/> is called on the main thread from
    /// <see cref="ValidatorService.Start"/> before the task is scheduled: the engine
    /// applies persisted settings in <c>ModSettingsRegistry.LoadAll</c> only after
    /// <c>ModResourcesReady</c>, which is too late for the start decision, so the
    /// mod reads its own settings file eagerly.
    /// </summary>
    public class ValidatorSettings : IModSettings
    {
        /// <summary>Mod id — also the settings file name under <c>user://mod_settings/</c>.</summary>
        public const string ModId = "validator";

        /// <summary>Default report directory used when the setting is empty or missing.</summary>
        public const string DefaultReportPath = "user://validation/";

        /// <summary>Master switch; when false the validation task is not started at all.</summary>
        public static bool Enabled { get; private set; } = true;

        /// <summary>When false, the mod stays idle until a future manual trigger is added.</summary>
        public static bool RunOnStart { get; private set; } = true;

        /// <summary>Enables the Roslyn-based analysis of mod C# sources.</summary>
        public static bool ParseCSharp { get; private set; } = true;

        /// <summary>Enables the GDScript probe; off by default because it is expensive.</summary>
        public static bool ParseGdScript { get; private set; }

        /// <summary>Enables .scenario DSL validation (command names, arguments, labels).</summary>
        public static bool ParseScenarios { get; private set; } = true;

        /// <summary>When false the report is kept in memory only and no file is written.</summary>
        public static bool WriteReport { get; private set; } = true;

        /// <summary>Godot-style directory (<c>user://...</c>) the report is written to.</summary>
        public static string ReportPath { get; private set; } = DefaultReportPath;

        /// <summary>
        /// Reserved for the future "block mod on error" behaviour. Cannot be honoured
        /// today: mods are registered once and never unloaded, so the most it can do
        /// is disable the mod for the next launch.
        /// </summary>
        public static bool FailOnError { get; private set; }

        /// <summary>Game version resolved from <c>application/config/version</c>, cached for the report.</summary>
        public static string GameVersion { get; private set; } = "";

        /// <inheritdoc/>
        public IReadOnlyList<ModSettingDefinition> GetDefinitions()
        {
            return new List<ModSettingDefinition>
            {
                new() { Key = "enabled", Name = "validator/enabled", Type = SettingType.Bool, DefaultValue = "true" },
                new() { Key = "run_on_start", Name = "validator/run_on_start", Type = SettingType.Bool, DefaultValue = "true" },
                new() { Key = "parse_csharp", Name = "validator/parse_csharp", Type = SettingType.Bool, DefaultValue = "true" },
                new() { Key = "parse_gdscript", Name = "validator/parse_gdscript", Type = SettingType.Bool, DefaultValue = "false" },
                new() { Key = "parse_scenarios", Name = "validator/parse_scenarios", Type = SettingType.Bool, DefaultValue = "true" },
                new() { Key = "write_report", Name = "validator/write_report", Type = SettingType.Bool, DefaultValue = "true" },
                new() { Key = "report_path", Name = "validator/report_path", Type = SettingType.String, DefaultValue = DefaultReportPath },
                new() { Key = "fail_on_error", Name = "validator/fail_on_error", Type = SettingType.Bool, DefaultValue = "false" },
            };
        }

        /// <inheritdoc/>
        public void WriteValues(DataBlob blob)
        {
            blob.SetBool("enabled", Enabled);
            blob.SetBool("run_on_start", RunOnStart);
            blob.SetBool("parse_csharp", ParseCSharp);
            blob.SetBool("parse_gdscript", ParseGdScript);
            blob.SetBool("parse_scenarios", ParseScenarios);
            blob.SetBool("write_report", WriteReport);
            blob.SetString("report_path", ReportPath);
            blob.SetBool("fail_on_error", FailOnError);
        }

        /// <inheritdoc/>
        public void ReadValues(DataBlob blob)
        {
            Apply(blob);
        }

        /// <summary>
        /// Loads persisted values before the validation task starts and caches the
        /// game version for the report. Must be called on the main thread because it
        /// reads through <c>Godot.FileAccess</c> and <c>ProjectSettings</c>.
        /// </summary>
        public static void Reload()
        {
            Apply(ModSettingsRegistry.LoadModSettings(ModId));
            GameVersion = ResolveGameVersion();
        }

        /// <summary>
        /// Resolves the configured report directory to an absolute filesystem path.
        /// Must be called on the main thread because it touches <c>ProjectSettings</c>;
        /// returns null when the path cannot be resolved.
        /// </summary>
        public static string GetReportDirectory()
        {
            if (!GameEnvironment.IsRuntime)
                return ReportPath;

            try
            {
                return ProjectSettings.GlobalizePath(ReportPath);
            }
            catch
            {
                return null;
            }
        }

        private static void Apply(DataBlob blob)
        {
            Enabled = ReadBool(blob, "enabled", true);
            RunOnStart = ReadBool(blob, "run_on_start", true);
            ParseCSharp = ReadBool(blob, "parse_csharp", true);
            ParseGdScript = ReadBool(blob, "parse_gdscript", false);
            ParseScenarios = ReadBool(blob, "parse_scenarios", true);
            WriteReport = ReadBool(blob, "write_report", true);
            FailOnError = ReadBool(blob, "fail_on_error", false);

            ReportPath = blob.GetString("report_path", DefaultReportPath);
            if (string.IsNullOrWhiteSpace(ReportPath))
                ReportPath = DefaultReportPath;
        }

        /// <summary>
        /// Reads a boolean the way the settings UI persists it — as text. The typed
        /// <c>DataBlob.GetBool</c> is only used as a fallback because the UI writes
        /// every control through <c>SetString</c>, which a persisted blob later
        /// exposes as a JSON string.
        /// </summary>
        private static bool ReadBool(DataBlob blob, string key, bool defaultValue)
        {
            var raw = blob.GetString(key, null);
            if (raw == null)
                return defaultValue;

            return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
        }

        private static string ResolveGameVersion()
        {
            if (!GameEnvironment.IsRuntime)
                return "";

            try
            {
                return ProjectSettings.GetSetting("application/config/version").AsString();
            }
            catch
            {
                return "";
            }
        }
    }
}
