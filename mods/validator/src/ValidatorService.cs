using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Cthangover.Validator
{
    /// <summary>
    /// Orchestrates the validator pass. <see cref="Start"/> is called on the main
    /// thread and returns immediately: it snapshots the ordered mod list and the
    /// resolved report directory, then hands pure .NET work to a background task so
    /// the game start is never blocked.
    ///
    /// The background task may touch only <see cref="IModInfo"/> and
    /// <see cref="IModFileProvider"/> — never Godot types and never resolver caches —
    /// because mod providers are disposed by <c>ModManager.Reload</c> on the main
    /// thread. Strategies are discovered by reflection and executed in
    /// (Phase, Order, Id) order: contract strategies fill the shared
    /// <see cref="ValidationGraph"/>, reconciliation strategies consume it.
    /// </summary>
    public static class ValidatorService
    {
        /// <summary>Version reported in the JSON report; bump alongside the manifest.</summary>
        public const string Version = "0.1.0";

        private static readonly object stateLock = new();
        private static bool running;

        /// <summary>True while a validation pass is in flight.</summary>
        public static bool IsRunning
        {
            get
            {
                lock (stateLock)
                    return running;
            }
        }

        /// <summary>
        /// Main-thread entry point invoked once after all mods are loaded. Reloads
        /// settings (persisted values are not applied by the engine yet at this
        /// point), checks the enable flags, guards against concurrent passes, takes
        /// the mod snapshot, and schedules the background run.
        /// </summary>
        public static void Start()
        {
            ValidatorSettings.Reload();

            if (!ValidatorSettings.Enabled || !ValidatorSettings.RunOnStart)
            {
                GameLogger.Log("VALIDATOR", "validator: disabled by settings");
                return;
            }

            lock (stateLock)
            {
                if (running)
                    return;
                running = true;
            }

            var mods = SnapshotMods();
            var reportDirectory = ValidatorSettings.GetReportDirectory();

            WarmScenarioRegistries();

            GameLogger.Log("VALIDATOR", $"validator: started, {mods.Count} mod(s) queued");
            Task.Run(() => Run(mods, reportDirectory));
        }

        private static List<IModInfo> SnapshotMods()
        {
            var result = new List<IModInfo>();
            var registry = ModRegistry.Instance;

            foreach (var modId in registry.GetOrderedModIds())
            {
                if (registry.Mods.TryGetValue(modId, out var info) && info != null)
                    result.Add(info);
            }

            return result;
        }

        private static void Run(List<IModInfo> mods, string reportDirectory)
        {
            try
            {
                var report = BuildReport(mods);

                GameLogger.Log("VALIDATOR",
                    $"validator: {report.Summary.Errors} errors, {report.Summary.Warnings} warnings, {report.Summary.Infos} infos");

                if (ValidatorSettings.WriteReport)
                    ReportWriter.Write(report, reportDirectory);
            }
            catch (Exception ex)
            {
                GameLogger.Log("VALIDATOR", $"validator: failed: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
            }
            finally
            {
                lock (stateLock)
                    running = false;
            }
        }

        private static ValidationReport BuildReport(List<IModInfo> mods)
        {
            var report = new ValidationReport
            {
                GeneratedAt = DateTime.UtcNow.ToString("O"),
                GameVersion = ValidatorSettings.GameVersion,
                ValidatorVersion = Version,
            };

            foreach (var mod in mods)
            {
                report.Mods.Add(new ValidationModStatus
                {
                    Id = mod.Id,
                    Version = mod.Version,
                    InstallSha256 = (mod as ModInfo)?.InstallSha256,
                    Status = "ok",
                });
            }

            var graph = new ValidationGraph();
            var context = new ValidationContext(mods, report, graph, CancellationToken.None);
            var strategies = ValidationStrategyRegistry.GetStrategies();

            GameLogger.Log("VALIDATOR", $"validator: running {strategies.Count} strategy(ies)");
            foreach (var strategy in strategies)
            {
                if (!IsFeatureEnabled(strategy.Feature))
                {
                    GameLogger.Log("VALIDATOR", $"validator: strategy {strategy.Id} skipped, feature {strategy.Feature} disabled", LogLevel.Debug);
                    continue;
                }

                GameLogger.Log("VALIDATOR", $"validator: strategy {strategy.Id} ({strategy.Phase})", LogLevel.Debug);
                try
                {
                    strategy.Validate(context);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("VALIDATOR", $"validator: strategy {strategy.Id} failed: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
                }
            }

            ApplySummary(report);
            return report;
        }

        private static bool IsFeatureEnabled(ValidationFeature feature)
        {
            switch (feature)
            {
                case ValidationFeature.CSharp:
                    return ValidatorSettings.ParseCSharp;
                case ValidationFeature.GdScript:
                    return ValidatorSettings.ParseGdScript;
                case ValidationFeature.Scenarios:
                    return ValidatorSettings.ParseScenarios;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Touches the scenario command and action registries while still on the
        /// main thread. Both are static/Lazy registries that instantiate plugin
        /// classes on first use; warming them here keeps the background
        /// validation pass free of first-touch instantiation of game types.
        /// </summary>
        private static void WarmScenarioRegistries()
        {
            var commands = 0;
            foreach (var _ in ScenarioCommandStrategyFactory.GetAllCommandNames())
                commands++;

            var actions = 0;
            foreach (var action in ScenarioActionFactory.Instance.GetAll())
                actions++;

            GameLogger.Log("VALIDATOR", $"validator: scenario registries warmed ({commands} command(s), {actions} action(s))", LogLevel.Debug);
        }

        private static void ApplySummary(ValidationReport report)
        {
            var errors = 0;
            var warnings = 0;
            var infos = 0;

            foreach (var issue in report.Issues)
            {
                switch (issue.Severity)
                {
                    case ValidationSeverity.Error:
                        errors++;
                        break;
                    case ValidationSeverity.Warning:
                        warnings++;
                        break;
                    case ValidationSeverity.Info:
                        infos++;
                        break;
                }
            }

            report.Summary.Errors = errors;
            report.Summary.Warnings = warnings;
            report.Summary.Infos = infos;

            foreach (var status in report.Mods)
                status.Status = HasErrors(report, status.Id) ? "errors" : "ok";
        }

        private static bool HasErrors(ValidationReport report, string modId)
        {
            foreach (var issue in report.Issues)
            {
                if (issue.Severity == ValidationSeverity.Error
                    && string.Equals(issue.Mod, modId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
