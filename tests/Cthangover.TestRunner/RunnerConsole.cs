namespace Cthangover.TestRunner
{
    /// <summary>Serializes standalone runner command output without loading the game kernel into the orchestration tool.</summary>
    public static class RunnerConsole
    {
        private static readonly object Gate = new();

        /// <summary>Attributes parallel progress lines while preserving unprefixed sequential command output.</summary>
        public static void WriteLine(string message, string name = null)
        {
            Write(Console.Out, name == null ? message : "[" + name + "] " + message);
        }

        /// <summary>Keeps invocation failures on stderr, independently of structured child-game logs.</summary>
        public static void WriteError(string message)
        {
            Write(Console.Error, message);
        }

        private static void Write(TextWriter destination, string message)
        {
            lock (Gate)
            {
                destination.WriteLine(message);
            }
        }
    }
}
