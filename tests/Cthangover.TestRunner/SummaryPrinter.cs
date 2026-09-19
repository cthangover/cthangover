using System.Text.Json;

namespace Cthangover.TestRunner
{
    /// <summary>
    /// Prints the single human-readable summary of the whole run and, when
    /// requested, writes the same data as JSON for machine consumption.
    /// Returns the aggregate exit code (0 only when every entry passed), which
    /// makes the runner usable from any CI or pre-commit hook without parsing
    /// console text.
    /// </summary>
    public static class SummaryPrinter
    {
        public static int Print(IReadOnlyList<TestResult> results, string jsonPath)
        {
            var allPassed = results.Count > 0 && results.All(r => r.Passed);

            Console.WriteLine();
            Console.WriteLine("=== ИТОГИ ТЕСТИРОВАНИЯ ===");
            foreach (var result in results)
            {
                var status = result.Passed ? "PASS" : result.TimedOut ? "TIMEOUT" : "FAIL";
                var checks = result.PassedChecks + result.FailedChecks > 0
                    ? $"{result.PassedChecks}/{result.PassedChecks + result.FailedChecks}"
                    : "-";
                var details = BuildDetails(result);
                var line = $"  {result.Name,-14} {status,-8} {checks,-10} {result.Duration.TotalSeconds,6:F1}s  {details}";
                WriteStatus(line, result.Passed);
            }

            Console.WriteLine();
            WriteStatus($"Успешно: {results.Count(r => r.Passed)}/{results.Count}", allPassed);
            WriteStatus(allPassed ? "ВСЁ УСПЕШНО" : "ЕСТЬ ПАДЕНИЯ", allPassed);

            if (!string.IsNullOrEmpty(jsonPath))
                WriteJson(jsonPath, results, allPassed);

            return allPassed ? 0 : 1;
        }

        private static string BuildDetails(TestResult result)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(result.Core)) parts.Add($"core={result.Core}");
            if (!string.IsNullOrEmpty(result.Coverage)) parts.Add($"coverage={result.Coverage}");
            if (!string.IsNullOrEmpty(result.Note)) parts.Add(result.Note);
            if (!result.Passed && !result.TimedOut) parts.Add($"exit={result.ExitCode}");
            return string.Join("  ", parts);
        }

        private static void WriteStatus(string text, bool ok)
        {
            if (Console.IsOutputRedirected)
            {
                Console.WriteLine(text);
                return;
            }

            var previous = Console.ForegroundColor;
            Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(text);
            Console.ForegroundColor = previous;
        }

        private static void WriteJson(string path, IReadOnlyList<TestResult> results, bool allPassed)
        {
            var report = new
            {
                success = allPassed,
                finishedAt = DateTimeOffset.Now,
                totalSeconds = Math.Round(results.Sum(r => r.Duration.TotalSeconds), 2),
                results = results.Select(r => new
                {
                    name = r.Name,
                    kind = r.Kind,
                    status = r.Passed ? "pass" : r.TimedOut ? "timeout" : "fail",
                    passed = r.PassedChecks,
                    failed = r.FailedChecks,
                    exitCode = r.ExitCode,
                    durationSeconds = Math.Round(r.Duration.TotalSeconds, 2),
                    core = r.Core,
                    coverage = r.Coverage,
                    note = r.Note
                })
            };

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            Console.WriteLine($"Отчёт записан: {path}");
        }
    }
}
