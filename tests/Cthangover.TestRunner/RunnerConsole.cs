namespace Cthangover.TestRunner
{
    /// <summary>All concurrent launch messages, including live pipe echoes, share one whole-line console lock.</summary>
    public static class RunnerConsole
    {
        private static readonly object Gate = new();

        /// <summary>A null name preserves sequential output; parallel launch names make interleaved lines attributable.</summary>
        public static void WriteLine(string message, string name = null)
        {
            lock (Gate) Console.WriteLine(name == null ? message : "[" + name + "] " + message);
        }
    }
}
