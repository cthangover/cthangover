namespace Cthangover.TestRunner
{
    using System.Diagnostics;

    /// <summary>Owns cancellation for the command and kills every registered child tree before partial reporting/cleanup.</summary>
    public sealed class RunnerLifetime : IDisposable
    {
        private static readonly object Gate = new();
        private static readonly HashSet<Process> Children = new();
        private static CancellationTokenSource cancellation;

        /// <summary>The command token also covers initial builds and the legacy sequential execution path.</summary>
        public static CancellationToken Token => cancellation?.Token ?? CancellationToken.None;

        /// <summary>Installs a cooperative Ctrl+C handler; running children are always registered under the same gate.</summary>
        public RunnerLifetime()
        {
            cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += CancelKeyPress;
        }

        internal static void Start(Process process, CancellationToken token)
        {
            lock (Gate)
            {
                token.ThrowIfCancellationRequested();
                Token.ThrowIfCancellationRequested();
                process.Start();
                Children.Add(process);
            }
        }

        internal static void Remove(Process process) { lock (Gate) Children.Remove(process); }

        /// <summary>Prevents further starts and stops active descendants; retained workspaces remain available to diagnose interruption.</summary>
        public static void Cancel()
        {
            lock (Gate)
            {
                cancellation?.Cancel();
                foreach (var process in Children)
                    try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception) { }
            }
        }

        private static void CancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            Cancel();
        }

        /// <summary>Unhooks the process-wide handler after all execution tasks have drained.</summary>
        public void Dispose()
        {
            Console.CancelKeyPress -= CancelKeyPress;
            cancellation?.Dispose();
            cancellation = null;
        }
    }
}
