using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods.Testing
{
    /// <summary>
    /// The only writer of mod test results (plan §7.3). Produces three
    /// channels:
    ///
    /// <para>1. Console block — always, through GameLogger (category MODTEST),
    /// so it lands in stdout and <c>user://logs/cthangover.log</c>.</para>
    ///
    /// <para>2. JSON — <c>user://mod_tests/last-report.json</c> (overwritten)
    /// plus a timestamped copy in <c>user://mod_tests/runs/</c>.</para>
    ///
    /// <para>3. History — one JSONL line per suite in
    /// <c>user://mod_tests/history.jsonl</c> (rotation: last 50 runs).</para>
    ///
    /// Additionally prints the unified CI contract
    /// <c>[TEST_RESULT] name=mod_units:&lt;id&gt; passed=N failed=M</c> per mod
    /// and a <c>mod_units</c> aggregate — consumed by Cthangover.TestRunner
    /// like every integration test.
    /// </summary>
    public static class ModTestReportWriter
    {
        private const int HistoryMaxLines = 50;

        private static string ReportRoot
        {
            get
            {
                if (GameEnvironment.IsRuntime)
                {
                    try { return ProjectSettings.GlobalizePath("user://mod_tests/"); }
                    catch { /* fall through */ }
                }
                return Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    "Godot", "app_userdata", "Cthangover", "mod_tests");
            }
        }

        /// <summary>Writes every channel and prints the console report; returns the aggregate pass/fail of the run.</summary>
        public static bool Write(IReadOnlyList<ModTestSuiteSummary> suites, string mode)
        {
            var runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var root = ReportRoot;
            Directory.CreateDirectory(root);

            // Console: per-suite block + итоги
            foreach (var suite in suites)
            {
                GameLogger.Log("MODTEST", $"=== MOD TESTS: {suite.ModId} v{suite.Version ?? "?"} (hash {ShortHash(suite.Hash)}) ===");
                foreach (var test in suite.Tests)
                {
                    var tag = test.Status switch
                    {
                        ModTestStatus.Passed => "PASS",
                        ModTestStatus.Failed => "FAIL",
                        ModTestStatus.Skipped => "SKIP",
                        ModTestStatus.Timeout => "TIMEOUT",
                        _ => "ERROR",
                    };
                    GameLogger.Log("MODTEST", $"[{tag}] {test.ModId}/{test.TestName} ({test.DurationMs:0} ms){Suffix(test)}");
                    if (!string.IsNullOrEmpty(test.Message))
                        GameLogger.Log("MODTEST", $"       {test.Message.Replace("\n", "\n       ")}");
                    foreach (var line in test.Log)
                        GameLogger.Log("MODTEST", $"       log: {line}");
                }
                GameLogger.Log("MODTEST", $"=== {suite.ModId}: {suite.Passed}/{suite.Tests.Count} passed, {suite.Failed} failed, {suite.Skipped} skipped, {suite.Errors} errors ({suite.DurationMs / 1000.0:0.0#} s) ===");
            }

            var totals = ComputeTotals(suites);
            var totalTests = totals.Passed + totals.Failed + totals.Skipped;
            var totalPassed = totals.Passed;
            var greenMods = suites.Count(s => s.Status == ModTestStatus.Passed || s.Status == ModTestStatus.Skipped);
            GameLogger.Log("MODTEST", "=== ИТОГИ MOD TESTS ===");
            foreach (var s in suites)
                GameLogger.Log("MODTEST", $"  {s.ModId,-20} {StatusTag(s.Status),-7} {s.Passed}/{s.Passed + s.Failed + s.Skipped + s.Errors,-4} {s.DurationMs / 1000.0:0.0#}s");
            GameLogger.Log("MODTEST", $"Модов: {greenMods}/{suites.Count} зелёных; тестов: {totalPassed}/{totalTests} прошедших, {totals.Failed} упавших, {totals.Skipped} пропущенных");
            GameLogger.Log("MODTEST", $"Отчёт: {root}last-report.json");

            // Unified CI contract lines
            foreach (var s in suites)
                GameLogger.Log("MODTEST", $"[TEST_RESULT] name=mod_units:{s.ModId} passed={s.Passed} failed={s.Failed + s.Errors} skipped={s.Skipped}");
            GameLogger.Log("MODTEST", $"[TEST_RESULT] name=mod_units passed={totals.Passed} failed={totals.Failed} skipped={totals.Skipped}");

            // JSON report
            var report = new ReportFile
            {
                RunId = runId,
                Mode = mode,
                WrittenUtc = DateTime.UtcNow,
                GodotVersion = Engine.GetVersionInfo()["string"].AsString(),
                Suites = suites,
            };
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() },
            });
            File.WriteAllText(Path.Combine(root, "last-report.json"), json);
            Directory.CreateDirectory(Path.Combine(root, "runs"));
            File.WriteAllText(Path.Combine(root, "runs", runId + ".json"), json);

            // History (JSONL, rotated)
            var historyPath = Path.Combine(root, "history.jsonl");
            var lines = File.Exists(historyPath)
                ? File.ReadAllLines(historyPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList()
                : new List<string>();
            foreach (var s in suites)
            {
                lines.Add(JsonSerializer.Serialize(new HistoryLine
                {
                    RunId = runId,
                    ModId = s.ModId,
                    Status = s.Status.ToString(),
                    Passed = s.Passed,
                    Failed = s.Failed,
                    Skipped = s.Skipped,
                    Errors = s.Errors,
                    DurationMs = s.DurationMs,
                    Hash = s.Hash,
                }));
            }
            if (lines.Count > HistoryMaxLines)
                lines = lines.Skip(lines.Count - HistoryMaxLines).ToList();
            File.WriteAllLines(historyPath, lines);

            return totals.Success;
        }

        /// <summary>Aggregate counts of a run; <see cref="Failed"/> includes errors and timeouts, skips never fail a run.</summary>
        public readonly record struct ModTestTotals(int Passed, int Failed, int Skipped, bool Success);

        /// <summary>
        /// Aggregates suite summaries for the CI contract line and the exit
        /// code: a run succeeds when no test failed, timed out or errored.
        /// Skipped tests are reported separately and never count as failures.
        /// </summary>
        public static ModTestTotals ComputeTotals(IReadOnlyList<ModTestSuiteSummary> suites)
        {
            var passed = suites.Sum(s => s.Passed);
            var failed = suites.Sum(s => s.Failed + s.Errors);
            var skipped = suites.Sum(s => s.Skipped);
            return new ModTestTotals(passed, failed, skipped, failed == 0);
        }

        /// <summary>Copies per-mod JSON reports into the CI artifact directory (runner collects them); best-effort.</summary>
        public static void CopyToArtifacts(IReadOnlyList<ModTestSuiteSummary> suites, string artifactsDir)
        {
            try
            {
                Directory.CreateDirectory(artifactsDir);
                var root = ReportRoot;
                foreach (var s in suites)
                {
                    var src = Path.Combine(root, "last-report.json");
                    if (File.Exists(src))
                        File.Copy(src, Path.Combine(artifactsDir, s.ModId + ".json"), overwrite: true);
                }
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODTEST", $"CopyToArtifacts failed: {ex.Message}", LogLevel.Warning);
            }
        }

        private static string Suffix(ModTestEntry e)
        {
            return e.Status == ModTestStatus.Skipped && !string.IsNullOrEmpty(e.Message) ? "" : "";
        }

        private static string StatusTag(ModTestStatus status)
        {
            return status switch
            {
                ModTestStatus.Passed => "PASS",
                ModTestStatus.Failed => "FAIL",
                ModTestStatus.Skipped => "SKIP",
                ModTestStatus.Timeout => "FAIL",
                _ => "ERROR",
            };
        }

        private static string ShortHash(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return "—";
            return hash.Length <= 4 ? hash : hash.Substring(0, 4) + "…";
        }

        private sealed class ReportFile
        {
            public string RunId { get; set; }
            public string Mode { get; set; }
            public DateTime WrittenUtc { get; set; }
            public string GodotVersion { get; set; }
            public IReadOnlyList<ModTestSuiteSummary> Suites { get; set; }
        }

        private sealed class HistoryLine
        {
            public string RunId { get; set; }
            public string ModId { get; set; }
            public string Status { get; set; }
            public int Passed { get; set; }
            public int Failed { get; set; }
            public int Skipped { get; set; }
            public int Errors { get; set; }
            public double DurationMs { get; set; }
            public string Hash { get; set; }
        }
    }
}
