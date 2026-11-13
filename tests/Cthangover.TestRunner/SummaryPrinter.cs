namespace Cthangover.TestRunner
{
    /// <summary>Human summary of the exact outcomes persisted by ReportWriter, including blocked and skipped entries.</summary>
    public static class SummaryPrinter
    {
        /// <summary>Prints measured runner phases separately from subprocess time and lists the ten slowest launches.</summary>
        public static void PrintTiming(RunReport report)
        {
            var p = report.Phases;
            Console.WriteLine($"Время / Time: wall={report.WallSeconds:0.00}s children={report.ChildSeconds:0.00}s build={p.BuildSeconds:0.00}s copy={p.WorkspaceCopySeconds:0.00}s restore={p.RestoreSeconds:0.00}s ({p.RestoreCount}) artifacts={p.ArtifactCollectionSeconds:0.00}s reports={p.ReportWritingSeconds:0.00}s cleanup={p.CleanupSeconds:0.00}s");
            Console.WriteLine($"Копии / Copies: {p.Copies}; launch wall={p.LaunchSeconds:0.00}s parallelism={p.Parallelism:0.00}; lanes: {string.Join(", ", p.LaneBusySeconds.Select(pair => pair.Key + "=" + pair.Value.ToString("0.00") + "s"))}");
            Console.WriteLine("Самые долгие прогоны / Slowest launches:");
            foreach (var result in report.Results.OrderByDescending(Duration).Take(10))
                Console.WriteLine($"  {result.Name,-38} process={result.DurationSeconds:0.00}s overhead={result.OverheadSeconds:0.00}s boot={Optional(result.BootSeconds)}s cpu={Optional(result.CpuSeconds)}s peak={Optional(result.PeakMemoryMb)}MiB lines={result.LogLines}");
        }

        private static double Duration(TestResult result) => result.DurationSeconds;
        private static string Optional(double? value) => value?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "—";

        public static int Print(IReadOnlyList<TestResult> results)
        {
            Console.WriteLine();
            Console.WriteLine("=== ИТОГИ ТЕСТИРОВАНИЯ ===");
            foreach (var result in results)
            {
                Console.WriteLine($"  {result.Name,-38} {result.Status.ToUpperInvariant(),-8} {result.PassedChecks}/{result.PassedChecks + result.FailedChecks} skipped={result.SkippedChecks} {result.DurationSeconds:0.0}s");
                if (!result.Passed)
                {
                    Console.WriteLine("      " + result.Note);
                    foreach (var diagnostic in result.Diagnostics) Console.WriteLine("      | " + diagnostic);
                    if (result.LogFile != null) Console.WriteLine("      лог: " + result.LogFile);
                }
            }
            var success = results.Count > 0 && results.All(result => result.Passed);
            Console.WriteLine($"Успешно: {results.Count(result => result.Passed)}/{results.Count}");
            Console.WriteLine(success ? "ВСЁ УСПЕШНО" : "ЕСТЬ ПАДЕНИЯ");
            return success ? 0 : 1;
        }
    }
}
