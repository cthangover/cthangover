namespace Cthangover.TestRunner
{
    using System.Diagnostics;

    /// <summary>Owns cancellation for the command and kills every registered child tree before partial reporting/cleanup.</summary>
    public sealed class RunnerLifetime : IDisposable
    {
        private static readonly object Gate = new();
        private static readonly HashSet<Process> Children = new();
        private static readonly HashSet<IDisposable> Retained = new();
        private static bool unsafeOwnership;
        private static CancellationTokenSource cancellation;

        /// <summary>The command token also covers initial builds and the legacy sequential execution path.</summary>
        public static CancellationToken Token => cancellation?.Token ?? CancellationToken.None;

        /// <summary>Unsafe process or terminal-publication ownership blocks later starts until outer process exit.</summary>
        public static bool HasUnprovenChildren
        {
            get
            {
                lock (Gate)
                {
                    return unsafeOwnership;
                }
            }
        }

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
                if (unsafeOwnership)
                {
                    throw new IOException("An earlier process has unproven termination or unfinished pipes");
                }

                process.Start();
                Children.Add(process);
            }
        }

        internal static void Complete(Process process, bool cleanupSafe, params IDisposable[] resources)
        {
            lock (Gate)
            {
                if (cleanupSafe)
                {
                    Children.Remove(process);
                }
                else
                {
                    // Keep handles, callbacks and their streams alive: disposing them would fabricate completed capture.
                    unsafeOwnership = true;
                    if (process != null)
                    {
                        Children.Add(process);
                    }

                    foreach (var resource in resources)
                    {
                        if (resource != null)
                        {
                            Retained.Add(resource);
                        }
                    }
                }
            }
        }

        /// <summary>Stops registered child trees after cancellation; unsafe ownership remains retained for diagnosis.</summary>
        public static void Cancel()
        {
            lock (Gate)
            {
                cancellation?.Cancel();
                foreach (var process in Children)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                    }
                    catch (AggregateException)
                    {
                        // ProcessRunner still owns bounded observation and must not infer success from this request.
                    }
                }
            }
        }

        private static void CancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            Cancel();
        }

        /// <summary>Unhooks cancellation after workers drain; unsafe handles/streams remain owned until outer OS process exit.</summary>
        public void Dispose()
        {
            Console.CancelKeyPress -= CancelKeyPress;
            cancellation?.Dispose();
            cancellation = null;
        }
    }
}
