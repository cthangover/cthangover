namespace Cthangover.TestRunner
{
    /// <summary>
    /// Keeps the full process output of every test for post-mortem debugging
    /// and extracts the lines worth showing in the summary. Without it a
    /// failed scene leaves only "no result marker" behind: the Godot output
    /// that explains the failure (missing scene, mod compile error,
    /// exception) is gone with the process.
    /// </summary>
    public static class TestLogs
    {
        private const int MaxHighlights = 12;
        private const int TailLines = 8;

        /// <summary>Directory the logs land in: <c>tests/artifacts/logs/</c> (ignored by git via <c>*.log</c>).</summary>
        public static string Directory(string root) => Path.Combine(root, "tests", "artifacts", "logs");

        /// <summary>
        /// Writes the output to <c>tests/artifacts/logs/&lt;name&gt;.log</c>,
        /// overwriting the previous run. Returns the path, or null when the
        /// write failed (logging must never fail the test run).
        /// </summary>
        public static string Save(string root, string name, ProcessResult run)
        {
            try
            {
                var dir = Directory(root);
                System.IO.Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, name + ".log");
                File.WriteAllLines(path, run.Output);
                return path;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Lines that explain a failure: engine/script errors, exceptions,
        /// mod compile errors and failed checks, capped to a few lines. Falls
        /// back to the tail of the output when nothing matched (a hang or a
        /// silent exit still shows where the process stopped).
        /// </summary>
        public static List<string> Highlights(IReadOnlyList<string> output)
        {
            var hits = output
                .Where(IsDiagnostic)
                .Select(StripAnsi)
                .Distinct()
                .Take(MaxHighlights)
                .ToList();
            if (hits.Count > 0)
                return hits;
            return output.Skip(Math.Max(0, output.Count - TailLines)).Select(StripAnsi).ToList();
        }

        private static bool IsDiagnostic(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;
            // Engine shutdown noise is reported on every exit and explains nothing.
            if (line.Contains("Unreferenced static string") || line.Contains("leaked at exit") || line.Contains("Pages in use exist at exit"))
                return false;
            // GameLogger marks LogLevel.Error with red ANSI on non-Windows consoles.
            if (line.Contains("\x1b[31m[", StringComparison.Ordinal))
                return true;
            line = StripAnsi(line);
            return line.StartsWith("ERROR:", StringComparison.Ordinal)
                || line.StartsWith("SCRIPT ERROR:", StringComparison.Ordinal)
                || line.Contains("Exception:", StringComparison.Ordinal)
                || line.Contains("[ERROR]", StringComparison.Ordinal)
                || line.Contains("[FAIL]", StringComparison.Ordinal)
                || line.Contains("[TIMEOUT]", StringComparison.Ordinal)
                || (line.Contains("[MODS_COMPILE]", StringComparison.Ordinal) && (line.Contains("FAILED") || line.Contains(": error CS")))
                || line.Contains("Cannot open file", StringComparison.Ordinal);
        }

        private static string StripAnsi(string line)
            => System.Text.RegularExpressions.Regex.Replace(line, @"\x1b\[[0-9;]*m", "");
    }
}
