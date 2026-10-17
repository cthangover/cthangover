using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Cthangover.Validator;
using Godot;

namespace Cthangover.ResourceAtlas
{
    /// <summary>
    /// Runs one atlas pass after the boot: snapshots the loaded mods in load
    /// order, builds the validator graph and the atlas on a worker thread,
    /// writes the report, logs every error as <c>[FAIL]</c> and prints
    /// <c>[TEST_RESULT] name=resource_atlas passed=&lt;clean mods&gt;
    /// failed=&lt;mods with errors&gt;</c> before quitting with 0/1. A watchdog
    /// ends a hung pass with a failure instead of blocking the test run.
    /// </summary>
    public partial class ResourceAtlasDriverNode : Node
    {
        private const string ResultName = "resource_atlas";

        private readonly string directory;
        private readonly float timeoutSeconds;
        private double elapsed;
        private bool started;
        private bool finished;
        private Task<ResourceAtlasResult> pass;

        public ResourceAtlasDriverNode()
        {
        }

        public ResourceAtlasDriverNode(string directory, float timeoutSeconds)
        {
            this.directory = directory;
            this.timeoutSeconds = timeoutSeconds;
            Name = "ResourceAtlasDriver";
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
                ValidatorService.WarmUp();
                var mods = SnapshotMods();
                var actions = ScenarioActionFactory.Instance.GetAll()
                    .Select(a => (a.Name, a.GetType().Assembly.GetName().Name))
                    .ToList();
                GameLogger.Log("RESOURCE_ATLAS", $"building atlas for {mods.Count} mod(s)");
                var loaded = new HashSet<string>(mods.Select(m => m.Id), StringComparer.Ordinal);
                var outside = ModRegistry.Instance.AllDiscoveredMods.Values.Where(m => !loaded.Contains(m.Id)).ToList();
                if (outside.Count > 0)
                    GameLogger.Log("RESOURCE_ATLAS", $"{outside.Count} installed mod(s) are outside the loaded set; keys only they provide are errors");
                pass = Task.Run(() =>
                {
                    var context = ValidatorService.Analyze(mods, null, CancellationToken.None);
                    var outsideContext = outside.Count > 0 ? ValidatorService.Analyze(outside, null, CancellationToken.None) : null;
                    return ResourceAtlasBuilder.Build(mods, context.Graph, actions, outside, outsideContext?.Graph);
                });
                return;
            }

            if (pass is { IsCompleted: true })
            {
                if (pass.IsFaulted)
                    Finish(null, $"atlas pass failed: {pass.Exception?.GetBaseException().Message}");
                else
                    Finish(pass.Result, null);
            }
        }

        private static List<IModInfo> SnapshotMods()
        {
            var registry = ModRegistry.Instance;
            var result = new List<IModInfo>();
            foreach (var id in registry.GetOrderedModIds())
            {
                if (registry.Mods.TryGetValue(id, out var info) && info != null)
                    result.Add(info);
            }

            return result;
        }

        private void Finish(ResourceAtlasResult result, string failure)
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
                    ResourceAtlasReportWriter.Write(result, directory);
                }
                catch (Exception ex)
                {
                    failure = $"report write failed: {ex.Message}";
                }

                foreach (var error in result.Errors
                             .GroupBy(e => (e.Kind, e.ModId, Others: string.Join(",", e.OtherMods), e.ResourceKind, e.ResourceId))
                             .Select(g => g.First()))
                {
                    GameLogger.Log("RESOURCE_ATLAS",
                        $"[FAIL] {error.ModId} -> {string.Join("/", error.OtherMods)}: {error.ResourceKind} '{error.ResourceId}' {error.Message} ({error.File}{(error.Line > 0 ? ":" + error.Line : string.Empty)})");
                }

                var failing = result.FailingMods();
                failed = failing.Count;
                passed = result.LoadOrder.Count - failed;
                GameLogger.Log("RESOURCE_ATLAS",
                    $"atlas: {result.Errors.Count()} error(s), {result.Warnings.Count()} warning(s); report: {Path.Combine(directory, "report.md")}");
            }

            if (failure != null)
            {
                failed++;
                GameLogger.Log("RESOURCE_ATLAS", $"[FAIL] {failure}", LogLevel.Error);
            }

            GameLogger.Log("RESOURCE_ATLAS", $"[TEST_RESULT] name={ResultName} passed={passed} failed={failed}");
            GetTree().Quit(failed == 0 ? 0 : 1);
        }
    }
}
