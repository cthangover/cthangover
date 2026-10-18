using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.LocaleAtlas
{
    /// <summary>
    /// Runs one pass after the boot: scans every installed mod (loaded ones in
    /// load order, then the rest — localization belongs to the mod, not to the
    /// game that happens to load it) on a worker thread, writes the report,
    /// logs every error as <c>[FAIL]</c> and prints
    /// <c>[TEST_RESULT] name=locale_atlas passed=&lt;clean mods&gt;
    /// failed=&lt;mods with errors&gt;</c> before quitting with 0/1. A watchdog
    /// ends a hung pass with a failure instead of blocking the test run.
    /// </summary>
    public partial class LocaleAtlasDriverNode : Node
    {
        private const string ResultName = "locale_atlas";

        private readonly string directory;
        private readonly float timeoutSeconds;
        private double elapsed;
        private bool started;
        private bool finished;
        private Task<LocaleAtlasResult> pass;

        public LocaleAtlasDriverNode()
        {
        }

        public LocaleAtlasDriverNode(string directory, float timeoutSeconds)
        {
            this.directory = directory;
            this.timeoutSeconds = timeoutSeconds;
            Name = "LocaleAtlasDriver";
        }

        public override void _Process(double delta)
        {
            if (finished)
                return;

            elapsed += delta;
            if (elapsed > timeoutSeconds)
            {
                Finish(null, $"timeout after {timeoutSeconds:F0}s");
                return;
            }

            if (!started)
            {
                if (!Boot.IsReady)
                    return;

                started = true;
                var mods = SnapshotMods();
                GameLogger.Log("LOCALE_ATLAS", $"building locale map for {mods.Count} mod(s)");
                pass = Task.Run(() => LocaleAtlasBuilder.Build(mods));
                return;
            }

            if (pass is { IsCompleted: true })
            {
                if (pass.IsFaulted)
                    Finish(null, $"locale atlas pass failed: {pass.Exception?.GetBaseException()}");
                else
                    Finish(pass.Result, null);
            }
        }

        private static List<(string, IModFileProvider)> SnapshotMods()
        {
            var registry = ModRegistry.Instance;
            var result = new List<(string, IModFileProvider)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in registry.GetOrderedModIds())
            {
                if (registry.Mods.TryGetValue(id, out var info) && info?.FileProvider != null && seen.Add(id))
                    result.Add((id, info.FileProvider));
            }

            foreach (var info in registry.AllDiscoveredMods.Values.OrderBy(m => m.Id, StringComparer.Ordinal))
            {
                if (info?.FileProvider != null && seen.Add(info.Id))
                    result.Add((info.Id, info.FileProvider));
            }

            foreach (var collection in ModCollectionStore.LoadAll())
            {
                if (collection.Package != null)
                    result.Add((LocaleAtlasBuilder.CollectionSourcePrefix + collection.Id, collection.Package));
            }

            return result;
        }

        private void Finish(LocaleAtlasResult result, string failure)
        {
            if (finished)
                return;
            finished = true;

            var passed = 0;
            var failed = 0;
            if (result != null)
            {
                try
                {
                    LocaleAtlasReportWriter.Write(result, directory);
                }
                catch (Exception ex)
                {
                    failure = $"report write failed: {ex.Message}";
                }

                foreach (var error in result.Errors)
                {
                    GameLogger.Log("LOCALE_ATLAS",
                        $"[FAIL] {error.ModId}: {error.Code} '{error.Key}' [{error.Language}] {error.Message} ({error.File}{(error.Line > 0 ? ":" + error.Line : string.Empty)})");
                }

                var failing = result.FailingMods();
                failed = failing.Count;
                passed = result.Mods.Count - failed;
                GameLogger.Log("LOCALE_ATLAS",
                    $"locale atlas: {result.Keys.Count} key(s), {result.InterestingKeys.Count()} need attention, " +
                    $"{result.Errors.Count()} error(s), {result.Warnings.Count()} warning(s); report: {Path.Combine(directory, "report.md")}");
            }

            if (failure != null)
            {
                failed++;
                GameLogger.Log("LOCALE_ATLAS", $"[FAIL] {failure}", LogLevel.Error);
            }

            GameLogger.Log("LOCALE_ATLAS", $"[TEST_RESULT] name={ResultName} passed={passed} failed={failed}");
            GetTree().Quit(failed == 0 ? 0 : 1);
        }
    }
}
