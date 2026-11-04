namespace Cthangover.TestRunner
{
    /// <summary>Human summary of the exact outcomes persisted by ReportWriter, including blocked and skipped entries.</summary>
    public static class SummaryPrinter
    {
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
