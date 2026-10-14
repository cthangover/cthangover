using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        /// Runs one mod's tests in-process. Returns the suite summary; also
        /// records every entry into <paramref name="ledger"/>.
        /// </summary>
        public static async Task<ModTestSuiteSummary> RunSuiteAsync(
            string modId,
            Assembly testAssembly,
            ModTestLedger ledger,
            CancellationToken cancellationToken)
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
                    Message = "test assembly could not be compiled/loaded",
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
                    summary.Errors++;
                    ledger.Add(err);
                }
            }
            swTotal.Stop();
            summary.DurationMs = swTotal.Elapsed.TotalMilliseconds;
            summary.Passed = summary.Tests.Count(t => t.Status == ModTestStatus.Passed);
            summary.Failed = summary.Tests.Count(t => t.Status == ModTestStatus.Failed || t.Status == ModTestStatus.Timeout);
            summary.Skipped = summary.Tests.Count(t => t.Status == ModTestStatus.Skipped);
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
                var parameters = test.Method.GetParameters();
                object result = parameters.Length == 0
                    ? test.Method.Invoke(null, null)
                    : test.Method.Invoke(null, new object[] { ctx });

                if (result is Task task)
                {
                    var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
                    if (completed != task)
                        throw new TimeoutException($"test exceeded {test.TimeoutSeconds}s timeout");
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

        private static void FreeStrayRootNodes()
        {
            var root = Engine.GetMainLoop() as SceneTree;
            var known = new HashSet<string>(StringComparer.Ordinal) { "SceneManager", "GameData", "SceneEventController" };
            if (root?.Root == null)
                return;
            foreach (var child in root.Root.GetChildren().ToList())
            {
                if (child is Node node && !known.Contains(node.Name))
                    node.QueueFree();
            }
        }

        /// <summary>Self-check of the recovery invariants (plan §6.4): TimeScale, key registries non-empty.</summary>
        private static string SelfCheck()
        {
            if (Math.Abs(Engine.TimeScale - 1.0) > 1e-9)
                return "Engine.TimeScale is not 1 after recovery";

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
