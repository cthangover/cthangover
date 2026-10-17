using System;
using Cthangover.Mods.Dialog.Scenarios;
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
    ///
    /// A pass in flight is cancelled by <see cref="Stop"/> when the mod is
    /// unloaded or re-registered by an in-process mod-set reload: its thread
    /// runs validator code (and reflects over other mods' assemblies), so a
    /// pass left running would keep every one of those assemblies loaded, and
    /// it would read file providers the reload disposes.
    /// </summary>
    public static class ValidatorService
    {
        /// <summary>Version reported in the JSON report; bump alongside the manifest.</summary>
        public const string Version = "0.1.0";

        private static readonly object stateLock = new();
        private static bool running;
        private static CancellationTokenSource cancellation;
        private static Task pass;
        private static List<IModInfo> queuedMods;
        private static string queuedReportDirectory;

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

            WarmUp();

            GameLogger.Log("VALIDATOR", $"validator: started, {mods.Count} mod(s) queued");
            lock (stateLock)
            {
                queuedMods = mods;
                queuedReportDirectory = reportDirectory;
                cancellation = new CancellationTokenSource();
                pass = Task.Run(RunQueued);
            }
        }

        /// <summary>
        /// Cancels the pass in flight and waits (bounded) until its thread has
        /// left validator code. Main thread, from the unload notification.
        /// </summary>
        public static void Stop()
        {
            Task running;
            lock (stateLock)
            {
                cancellation?.Cancel();
                running = pass;
            }
            if (running == null)
                return;
            try
            {
                if (!running.Wait(TimeSpan.FromSeconds(10)))
                    GameLogger.Log("VALIDATOR", "validator: pass did not stop within 10 s; its mods stay in memory until it ends", LogLevel.Warning);
            }
            catch (AggregateException)
            {
                // Run logs its own failures; a cancelled pass is the expected outcome here.
            }
        }

        private static void RunQueued()
        {
            List<IModInfo> mods;
            string reportDirectory;
            CancellationToken token;
            lock (stateLock)
            {
                mods = queuedMods;
                reportDirectory = queuedReportDirectory;
                token = cancellation.Token;
                queuedMods = null;
            }
            Run(mods, reportDirectory, token);
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

        private static void Run(List<IModInfo> mods, string reportDirectory, CancellationToken token)
        {
            try
            {
                var report = BuildReport(mods, token);

                GameLogger.Log("VALIDATOR",
                    $"validator: {report.Summary.Errors} errors, {report.Summary.Warnings} warnings, {report.Summary.Infos} infos");

                if (ValidatorSettings.WriteReport)
                    ReportWriter.Write(report, reportDirectory);
            }
            catch (OperationCanceledException)
            {
                GameLogger.Log("VALIDATOR", "validator: pass cancelled (mod set reloading)");
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

        private static ValidationReport BuildReport(List<IModInfo> mods, CancellationToken token)
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

            Analyze(mods, report, token);
            ApplySummary(report);
            return report;
        }

        /// <summary>
        /// Runs every enabled strategy over <paramref name="mods"/> and returns
        /// the populated context — declarations, references and issues —
        /// without writing a report. Other tool mods (e.g. the resource atlas)
        /// build on the same graph instead of re-parsing scenarios and data.
        /// Call <see cref="WarmUp"/> on the main thread first.
        /// </summary>
        public static ValidationContext Analyze(IReadOnlyList<IModInfo> mods, ValidationReport report, CancellationToken cancellationToken)
        {
            var graph = new ValidationGraph();
            var context = new ValidationContext(mods, report ?? new ValidationReport(), graph, cancellationToken);
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
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    GameLogger.Log("VALIDATOR", $"validator: strategy {strategy.Id} failed: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
                }
            }

            return context;
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
        public static void WarmUp()
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
