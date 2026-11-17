namespace Cthangover.TestRunner
{
    using System.Diagnostics;
    using System.Text.RegularExpressions;

    /// <summary>Serialises both pipe callbacks and timestamps the first stdout driver category, excluding stderr lookalikes.</summary>
    internal sealed class ProcessOutputCapture
    {
        private static readonly Regex Driver = new(@"^(?:\[[^\]\r\n]*\]\s*)*\[(TEST|MODTEST)\](?:\s|$)", RegexOptions.CultureInvariant);
        private static readonly Regex Ansi = new(@"\x1b\[[0-9;]*m", RegexOptions.CultureInvariant);
        private readonly ProcessResult result;
        private readonly StreamWriter writer;
        private readonly Stopwatch watch;
        private readonly ManualResetEventSlim stdout;
        private readonly ManualResetEventSlim stderr;
        private readonly string name;
        private readonly StreamWriter stdoutWriter;
        private readonly StreamWriter stderrWriter;

        internal ProcessOutputCapture(ProcessResult result, StreamWriter writer, Stopwatch watch, ManualResetEventSlim stdout,
            ManualResetEventSlim stderr, string name = null, StreamWriter stdoutWriter = null, StreamWriter stderrWriter = null)
        {
            this.result = result;
            this.writer = writer;
            this.watch = watch;
            this.stdout = stdout;
            this.stderr = stderr;
            this.name = name;
            this.stdoutWriter = stdoutWriter;
            this.stderrWriter = stderrWriter;
        }

        internal void StandardOutput(object sender, DataReceivedEventArgs args)
        {
            Append(args.Data, true);
        }

        internal void StandardError(object sender, DataReceivedEventArgs args)
        {
            Append(args.Data, false);
        }

        internal void Append(string line, bool standardOutput)
        {
            if (line == null)
            {
                (standardOutput ? stdout : stderr).Set();
                return;
            }
            lock (result.Output)
            {
                if (standardOutput && result.BootSeconds == null && Driver.IsMatch(Ansi.Replace(line, string.Empty).TrimStart()))
                    result.BootSeconds = watch.Elapsed.TotalSeconds;
                result.Output.Add(line);
                result.LogLines++;
                writer?.WriteLine(line);
                var pipe = standardOutput ? stdoutWriter : stderrWriter;
                if (pipe != null)
                {
                    pipe.WriteLine(line);
                    var bytes = System.Text.Encoding.UTF8.GetByteCount(line + pipe.NewLine);
                    if (standardOutput)
                    {
                        result.StdoutBytes += bytes;
                    }
                    else
                    {
                        result.StderrBytes += bytes;
                    }
                }
            }
            if (line.TrimStart().StartsWith("[", StringComparison.Ordinal)
                && (line.Contains("[TEST_RESULT]", StringComparison.Ordinal) || line.Contains("[FAIL]", StringComparison.Ordinal)))
                RunnerConsole.WriteLine("  " + line, name);
        }
    }
}
