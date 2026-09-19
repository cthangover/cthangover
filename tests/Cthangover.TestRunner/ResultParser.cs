using System.Text.RegularExpressions;

namespace Cthangover.TestRunner
{
    /// <summary>
    /// Parses the machine-readable markers every integration scene prints at
    /// the end of its run. The contract is deliberately tiny and stable:
    /// <c>[TEST_RESULT] name=... [core=...] passed=N failed=N</c> plus the
    /// battle coverage summary <c>[BATTLE_COVERAGE] coverage: N/M cores</c>.
    /// Exit code alone is not enough: a scene can exit 0 after a crash-free
    /// but failing run, and timeouts must be distinguishable from failures.
    /// </summary>
    public static class ResultParser
    {
        private static readonly Regex ResultRegex = new(
            @"\[TEST_RESULT\]\s+name=(\S+)(?:\s+core=(\S+))?\s+passed=(\d+)\s+failed=(\d+)",
            RegexOptions.Compiled);

        private static readonly Regex CoverageRegex = new(
            @"\[BATTLE_COVERAGE\]\s+coverage:\s+(\d+)/(\d+)\s+cores",
            RegexOptions.Compiled);

        public static ParsedSceneResult Parse(IEnumerable<string> lines)
        {
            var result = new ParsedSceneResult();
            foreach (var line in lines)
            {
                var match = ResultRegex.Match(line);
                if (match.Success)
                {
                    result.HasResult = true;
                    result.Name = match.Groups[1].Value;
                    if (match.Groups[2].Success)
                        result.Core = match.Groups[2].Value;
                    result.Passed = int.Parse(match.Groups[3].Value);
                    result.Failed = int.Parse(match.Groups[4].Value);
                    continue;
                }

                var coverage = CoverageRegex.Match(line);
                if (coverage.Success)
                    result.Coverage = $"{coverage.Groups[1].Value}/{coverage.Groups[2].Value}";
            }
            return result;
        }
    }
}
