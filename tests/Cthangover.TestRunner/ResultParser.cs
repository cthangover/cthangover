namespace Cthangover.TestRunner
{
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>Validates every marker; a later success cannot erase an earlier failure or malformed result.</summary>
    public static class ResultParser
    {
        private static readonly Regex Ansi = new(@"\x1b\[[0-9;]*m");
        private static readonly Regex Coverage = new(@"\[BATTLE_COVERAGE\]\s+coverage:\s+(\d+)/(\d+)\s+cores");

        public static ParsedSceneResult Parse(IEnumerable<string> lines, ResultContract contract)
        {
            ArgumentNullException.ThrowIfNull(contract);
            var result = new ParsedSceneResult();
            var terminalCount = 0;
            var names = new HashSet<string>(StringComparer.Ordinal);
            long childPassed = 0, childFailed = 0, childSkipped = 0;
            var childCount = 0;
            foreach (var raw in lines)
            {
                var line = Ansi.Replace(raw, "");
                var coverage = Coverage.Match(line);
                if (coverage.Success)
                    result.Coverage = $"{coverage.Groups[1].Value}/{coverage.Groups[2].Value}";
                var at = line.IndexOf("[TEST_RESULT]", StringComparison.Ordinal);
                if (at < 0) continue;
                var fields = ReadFields(line.Substring(at + "[TEST_RESULT]".Length), result.Problems);
                if (fields == null) continue;
                var name = fields["name"];
                if (!names.Add(name)) result.Problems.Add($"duplicate result marker '{name}'");
                var passed = ReadCount(fields, "passed", result.Problems);
                var failed = ReadCount(fields, "failed", result.Problems);
                var skipped = ReadCount(fields, "skipped", result.Problems);
                if (failed > 0) result.Problems.Add($"result '{name}' reports {failed} failure(s)");
                if (name != contract.Name)
                {
                    if (string.IsNullOrEmpty(contract.ChildPrefix) || !name.StartsWith(contract.ChildPrefix, StringComparison.Ordinal))
                        result.Problems.Add($"unexpected result '{name}'; expected '{contract.Name}'");
                    else
                    {
                        if (terminalCount > 0) result.Problems.Add("child result follows the terminal marker");
                        childCount++;
                        childPassed += passed;
                        childFailed += failed;
                        childSkipped += skipped;
                    }
                    continue;
                }
                terminalCount++;
                result.HasResult = true;
                result.Name = name;
                result.Passed = passed;
                result.Failed = failed;
                result.Skipped = skipped;
                result.Core = fields.GetValueOrDefault("core");
                if (contract.Core != null && result.Core != contract.Core)
                    result.Problems.Add($"core '{result.Core}' does not match expected '{contract.Core}'");
                if ((long)passed + failed + skipped == 0) result.Problems.Add("empty result: no checks executed or skipped");
                if (!contract.AllowSkipped && skipped > 0) result.Problems.Add("skipped checks are forbidden by this contract");
            }
            if (terminalCount == 0) result.Problems.Add($"missing result marker '{contract.Name}'");
            if (contract.ChildPrefix != null && result.HasResult && result.Failed == 0 &&
                (childCount == 0 || childPassed != result.Passed || childFailed != result.Failed || childSkipped != result.Skipped))
                result.Problems.Add("child results do not match terminal counters");
            return result;
        }

        private static Dictionary<string, string> ReadFields(string text, List<string> problems)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var token in text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                var split = token.IndexOf('=');
                if (split <= 0 || split == token.Length - 1 || !fields.TryAdd(token.Substring(0, split), token.Substring(split + 1)))
                {
                    problems.Add("malformed result marker: " + text.Trim());
                    return null;
                }
            }
            if (!fields.ContainsKey("name") || !fields.ContainsKey("passed") || !fields.ContainsKey("failed"))
            {
                problems.Add("result marker requires name, passed and failed: " + text.Trim());
                return null;
            }
            if (fields.Keys.Any(key => key is not ("name" or "core" or "passed" or "failed" or "skipped")))
                problems.Add("unknown result field: " + text.Trim());
            return fields;
        }

        private static int ReadCount(Dictionary<string, string> fields, string key, List<string> problems)
        {
            if (!fields.TryGetValue(key, out var value) && key == "skipped") return 0;
            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count)) return count;
            problems.Add($"invalid {key} counter '{value}'");
            return 0;
        }

        /// <summary>Process completion and the validated terminal contract must both succeed.</summary>
        public static TestResult Evaluate(string name, string kind, ProcessResult process, ResultContract contract)
        {
            var parsed = Parse(process.Output, contract);
            CheckEngineErrors(process.Output, contract.ExpectedErrors, parsed.Problems);
            var result = new TestResult
            {
                Name = name, Kind = kind, ExitCode = process.ExitCode, Duration = process.Duration,
                TimedOut = process.TimedOut, PassedChecks = parsed.Passed, FailedChecks = parsed.Failed,
                SkippedChecks = parsed.Skipped, Core = parsed.Core, Coverage = parsed.Coverage,
                Passed = !process.TimedOut && process.ExitCode == 0 && parsed.Problems.Count == 0,
                LogFile = process.LogFile,
                BootSeconds = process.BootSeconds, CpuSeconds = process.CpuSeconds,
                PeakMemoryMb = process.PeakMemoryMb, LogLines = process.LogLines,
            };
            if (process.TimedOut) parsed.Problems.Insert(0, "process timeout");
            else if (process.ExitCode != 0) parsed.Problems.Insert(0, $"process exited with code {process.ExitCode}");
            result.Note = string.Join("; ", parsed.Problems.Distinct());
            if (!result.Passed)
                result.FailureKind = process.TimedOut ? "timeout"
                    : parsed.Problems.Any(problem => problem.StartsWith("unexpected engine error:", StringComparison.Ordinal) || problem.StartsWith("engine error expectation", StringComparison.Ordinal)) ? "engine"
                    : parsed.Failed > 0 ? "assertion" : !parsed.HasResult && process.ExitCode != 0 ? "process"
                    : parsed.Problems.Count > 0 ? "contract" : "process";
            if (!result.Passed) result.Diagnostics = TestLogs.Highlights(process.Output);
            return result;
        }

        private static void CheckEngineErrors(IEnumerable<string> output, List<DiagnosticExpectation> expected, List<string> problems)
        {
            var counts = new int[expected.Count];
            foreach (var raw in output)
            {
                var line = Ansi.Replace(raw, "").TrimStart();
                if (!TestLogs.IsEngineFailure(line)) continue;
                var matched = false;
                for (var i = 0; i < expected.Count; i++)
                    if (Regex.IsMatch(line, expected[i].Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)))
                    {
                        counts[i]++;
                        matched = true;
                        break;
                    }
                if (!matched) problems.Add("unexpected engine error: " + line);
            }
            for (var i = 0; i < expected.Count; i++)
                if (counts[i] != expected[i].Count) problems.Add($"engine error expectation '{expected[i].Pattern}': expected {expected[i].Count}, actual {counts[i]}");
        }
    }
}
