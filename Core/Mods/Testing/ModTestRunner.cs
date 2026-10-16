using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods.Testing
{
    /// <summary>
    /// Executes the discovered tests of one mod and applies the automatic
    /// recovery cycle after every test (plan §6.3). The author never cleans
    /// up: registries reset via <see cref="IResettable"/>, derived registry
    /// state rebuilds through <see cref="AppServices"/>, the event-bus style
    /// global state is restored (<c>Engine.TimeScale</c>, root nodes), and the
    /// per-mod file sandbox is wiped before the suite. After recovery the
    /// runner self-checks the invariants (§6.4) and reports ERROR instead of
    /// letting silent contamination corrupt subsequent tests.
    /// </summary>
    public static class ModTestRunner
    {
        /// <summary>
        /// Children of <c>/root</c> that existed before the first test (core
        /// autoloads, mod-added services, the launcher scene), by instance id.
        /// Recovery frees only nodes outside this set and reports a baseline
        /// node a test destroyed. Null until <see cref="CaptureRootBaselineAsync"/> ran.
        /// </summary>
        private static Dictionary<ulong, string> rootBaseline;

        /// <summary>
        /// Records the <c>/root</c> children as the recovery baseline. Waits
        /// one frame first so nodes the boot added through
        /// <c>call_deferred("add_child")</c> are already in the tree. Must run
        /// once before the first suite.
        /// </summary>
        public static async Task CaptureRootBaselineAsync()
        {
            if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
                return;
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            rootBaseline = tree.Root.GetChildren().ToDictionary(n => n.GetInstanceId(), n => n.Name.ToString());
            GameLogger.Log("MODTEST", $"recovery baseline: /root children = {string.Join(", ", rootBaseline.Values)}", LogLevel.Debug);
        }

        /// <summary>
        /// Runs one mod's tests in-process. Returns the suite summary; also
        /// records every entry into <paramref name="ledger"/>.
        /// </summary>
        public static async Task<ModTestSuiteSummary> RunSuiteAsync(
            string modId,
            Assembly testAssembly,
            ModTestLedger ledger,
            CancellationToken cancellationToken,
            string compileErrors = null)
        {
            var summary = new ModTestSuiteSummary { ModId = modId };
            var mod = ModManager.Instance.GetMod(modId);
            summary.Version = mod?.Version;
            summary.Hash = mod is ModInfo info && info.Manifest?.TestSources != null
                ? TestSourceHash(info)
                : null;

            if (testAssembly == null)
            {
                summary.Status = ModTestStatus.Error;
                summary.Errors = 1;
                summary.Tests.Add(new ModTestEntry
                {
                    ModId = modId,
                    TestName = "<compile>",
                    Status = ModTestStatus.Error,
                    Message = string.IsNullOrEmpty(compileErrors)
                        ? "test assembly could not be compiled/loaded"
                        : "test assembly failed to compile:\n" + compileErrors,
                });
                ledger.AddSuite(summary);
                return summary;
            }

            List<DiscoveredModTest> tests;
            try
            {
                tests = ModTestDiscovery.Discover(testAssembly);
            }
            catch (Exception ex)
            {
                summary.Status = ModTestStatus.Error;
                summary.Errors = 1;
                summary.Tests.Add(new ModTestEntry
                {
                    ModId = modId,
                    TestName = "<discovery>",
                    Status = ModTestStatus.Error,
                    Message = ex.Message,
                    StackTrace = ex.StackTrace,
                });
                ledger.AddSuite(summary);
                return summary;
            }

            var sandboxDir = GetSandboxDir(modId);
            PrepareSandbox(sandboxDir);

            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            foreach (var test in tests)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = await RunOneAsync(modId, test, sandboxDir, cancellationToken);
                summary.Tests.Add(entry);
                ledger.Add(entry);

                var contamination = RecoveryCycle(modId);
                if (contamination != null)
                {
                    var err = new ModTestEntry
                    {
                        ModId = modId,
                        TestName = test.Name + " [recovery]",
                        Status = ModTestStatus.Error,
                        Message = contamination,
                    };
                    summary.Tests.Add(err);
                    ledger.Add(err);
                }
            }
            swTotal.Stop();
            summary.DurationMs = swTotal.Elapsed.TotalMilliseconds;
            summary.Passed = summary.Tests.Count(t => t.Status == ModTestStatus.Passed);
            summary.Failed = summary.Tests.Count(t => t.Status == ModTestStatus.Failed || t.Status == ModTestStatus.Timeout);
            summary.Skipped = summary.Tests.Count(t => t.Status == ModTestStatus.Skipped);
            // Recovery contamination and aborted tests are both Error entries.
            summary.Errors = summary.Tests.Count(t => t.Status == ModTestStatus.Error);
            summary.Status = summary.Errors > 0 ? ModTestStatus.Error
                : summary.Failed > 0 ? ModTestStatus.Failed
                : summary.Passed == 0 && summary.Skipped > 0 ? ModTestStatus.Skipped
                : ModTestStatus.Passed;

            ledger.AddSuite(summary);
            return summary;
        }

        private static async Task<ModTestEntry> RunOneAsync(string modId, DiscoveredModTest test, string sandboxDir, CancellationToken cancellationToken)
        {
            var entry = new ModTestEntry { ModId = modId, TestName = test.Name };

            if (!string.IsNullOrEmpty(test.Skip))
            {
                entry.Status = ModTestStatus.Skipped;
                entry.Message = test.Skip;
                return entry;
            }

            var logBuffer = new List<string>();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(test.TimeoutSeconds));
            var sandboxRoot = new Node { Name = $"ModTestSandbox_{modId}" };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var ctx = new ModTestContext(modId, logBuffer, sandboxDir, sandboxRoot, cts.Token);
                var result = Invoke(test.Method, ctx);

                if (result is Task task)
                {
                    var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
                    if (completed != task)
                    {
                        // The linked token fires for both the per-test timeout
                        // and an aborted run; only the former is a TIMEOUT.
                        cancellationToken.ThrowIfCancellationRequested();
                        throw new TimeoutException($"test exceeded {test.TimeoutSeconds}s timeout");
                    }
                    await task; // surface aggregate exceptions
                    if (task.IsFaulted)
                        throw task.Exception?.GetBaseException() ?? new Exception("task faulted");
                }

                entry.Status = ModTestStatus.Passed;
            }
            catch (ModAssertException ex)
            {
                entry.Status = ModTestStatus.Failed;
                entry.Message = ex.Message;
                entry.StackTrace = ex.StackTrace;
            }
            catch (TimeoutException ex)
            {
                entry.Status = ModTestStatus.Timeout;
                entry.Message = ex.Message;
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                entry.Status = ModTestStatus.Timeout;
                entry.Message = $"test exceeded {test.TimeoutSeconds}s timeout";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                entry.Status = ModTestStatus.Error;
                entry.Message = "test run aborted (external cancellation) while this test was running";
            }
            catch (Exception ex)
            {
                var baseEx = ex is AggregateException agg ? agg.GetBaseException() : ex;
                entry.Status = ModTestStatus.Failed;
                entry.Message = baseEx.Message;
                entry.StackTrace = baseEx.StackTrace;
            }
            finally
            {
                cts.Dispose();
                entry.DurationMs = sw.Elapsed.TotalMilliseconds;
                lock (logBuffer)
                    entry.Log = new List<string>(logBuffer);
                CollectArtifacts(entry, sandboxDir);
                FreeSandboxRoot(sandboxRoot);
            }

            return entry;
        }

        /// <summary>
        /// Calls the static test method and rethrows what the test itself
        /// threw: reflection wraps a synchronous throw in
        /// <see cref="TargetInvocationException"/>, which would replace the
        /// assertion message and the test's stack with reflection frames.
        /// </summary>
        public static object Invoke(MethodInfo method, ModTestContext ctx)
        {
            try
            {
                return method.GetParameters().Length == 0
                    ? method.Invoke(null, null)
                    : method.Invoke(null, new object[] { ctx });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw; // unreachable
            }
        }

        /// <summary>
        /// The automatic recovery cycle (plan §6.3): resettables, registry
        /// rebuild, Engine.TimeScale, stray root nodes, engine globals.
        /// Returns a human-readable contamination description when a
        /// self-check invariant (§6.4) fails after recovery, or null when the
        /// environment is clean.
        /// </summary>
        private static string RecoveryCycle(string modId)
        {
            try
            {
                Engine.TimeScale = 1.0;

                foreach (var resettable in AppServices.Current.Resettables)
                {
                    try { resettable.ResetForNewGame(); }
                    catch (Exception ex)
                    {
                        GameLogger.Log("MODTEST", $"resettable '{resettable.Id}' failed: {ex.Message}", LogLevel.Warning);
                    }
                }

                // Rebuild derived registry state the same way hot-reload does,
                // so re-collected registries are consistent again.
                AppServices.Current.RebuildRegistries();

                FreeStrayRootNodes();

                return SelfCheck();
            }
            catch (Exception ex)
            {
                return $"recovery cycle itself failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Frees the <c>/root</c> children a test added — everything outside
        /// the baseline. Without a baseline nothing is freed: guessing by name
        /// would destroy core autoloads and the launcher scene.
        /// </summary>
        private static void FreeStrayRootNodes()
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree?.Root == null || rootBaseline == null)
                return;
            foreach (var child in tree.Root.GetChildren().ToList())
            {
                if (!rootBaseline.ContainsKey(child.GetInstanceId()))
                {
                    GameLogger.Log("MODTEST", $"recovery: freeing stray root node '{child.Name}'", LogLevel.Debug);
                    child.QueueFree();
                }
            }
        }

        /// <summary>Self-check of the recovery invariants (plan §6.4): TimeScale, baseline root nodes alive, key registries non-empty.</summary>
        private static string SelfCheck()
        {
            if (Math.Abs(Engine.TimeScale - 1.0) > 1e-9)
                return "Engine.TimeScale is not 1 after recovery";

            if (rootBaseline != null)
            {
                var lost = rootBaseline
                    .Where(kv => GodotObject.InstanceFromId(kv.Key) is not Node node
                        || !GodotObject.IsInstanceValid(node)
                        || node.IsQueuedForDeletion())
                    .ToList();
                if (lost.Count > 0)
                {
                    // Reported once, against the test that caused it.
                    foreach (var kv in lost)
                        rootBaseline.Remove(kv.Key);
                    return $"test freed baseline root node(s): {string.Join(", ", lost.Select(kv => kv.Value))}";
                }
            }

            var services = AppServices.Current;
            if (services.Ui == null || services.Menus == null || services.SceneRoutes == null)
                return "key AppServices registries are missing after recovery";
            return null;
        }

        private static string GetSandboxDir(string modId)
        {
            if (GameEnvironment.IsRuntime)
            {
                try { return ProjectSettings.GlobalizePath($"user://mod_test_sandbox/{modId}"); }
                catch { /* fall through */ }
            }
            return Path.Combine(Path.GetTempPath(), "cthangover_mod_test_sandbox", modId);
        }

        private static void PrepareSandbox(string sandboxDir)
        {
            try
            {
                if (Directory.Exists(sandboxDir))
                    Directory.Delete(sandboxDir, recursive: true);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODTEST", $"sandbox cleanup failed: {ex.Message}", LogLevel.Warning);
            }
            Directory.CreateDirectory(sandboxDir);
        }

        private static void CollectArtifacts(ModTestEntry entry, string sandboxDir)
        {
            try
            {
                if (!Directory.Exists(sandboxDir))
                    return;
                entry.Artifacts = Directory.GetFiles(sandboxDir, "*", SearchOption.AllDirectories).ToList();
            }
            catch
            {
                entry.Artifacts = new List<string>();
            }
        }

        private static void FreeSandboxRoot(Node sandboxRoot)
        {
            if (sandboxRoot == null)
                return;
            foreach (var child in sandboxRoot.GetChildren().ToList())
                child.QueueFree();
            sandboxRoot.QueueFree();
        }

        private static string TestSourceHash(ModInfo info)
        {
            try
            {
                var sources = new List<(string, string)>();
                foreach (var pattern in info.Manifest.TestSources)
                {
                    foreach (var file in info.FileProvider.ListFiles(ParentDir(pattern)))
                    {
                        if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                            continue;
                        sources.Add((file, info.FileProvider.ReadFileText(file)));
                    }
                }
                return sources.Count > 0 ? ModHasher.ComputeHash(sources) : null;
            }
            catch
            {
                return null;
            }
        }

        private static string ParentDir(string pattern)
        {
            var sep = pattern.LastIndexOf('/');
            return sep < 0 ? "" : pattern.Substring(0, sep);
        }
    }
}
