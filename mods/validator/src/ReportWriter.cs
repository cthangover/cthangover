using System;
using System.IO;
using System.Text.Json;
using Cthangover.Core.Utils;

namespace Cthangover.Validator
{
    /// <summary>
    /// Persists the completed <see cref="ValidationReport"/> as JSON. Called from
    /// the background validation task, so it relies only on plain
    /// <c>System.IO</c>; the Godot-style directory is resolved to an absolute path
    /// earlier, on the main thread, by
    /// <see cref="ValidatorSettings.GetReportDirectory"/>.
    /// </summary>
    public static class ReportWriter
    {
        /// <summary>File name inside the configured report directory.</summary>
        public const string FileName = "report.json";

        /// <summary>
        /// Writes the report and returns the absolute path, or null when the
        /// directory could not be resolved or the write failed. Never throws — a
        /// failed report must not interrupt the game.
        /// </summary>
        public static string Write(ValidationReport report, string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                GameLogger.Log("VALIDATOR", "validator: report directory is not resolved", LogLevel.Error);
                return null;
            }

            try
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, FileName);
                var json = JsonSerializer.Serialize(report, JsonOptionsProvider.WriteIndentedOptions);
                File.WriteAllText(path, json);
                GameLogger.Log("VALIDATOR", $"validator: report written to {path}");
                return path;
            }
            catch (Exception ex)
            {
                GameLogger.Log("VALIDATOR", $"validator: report write failed: {ex.Message}", LogLevel.Error);
                return null;
            }
        }
    }
}
