namespace Cthangover.Core.Mods.Testing
{
    using System;
    using System.Threading;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Early native bootstrap guard. The static GameData hook calls it before instance settings initialization;
    /// the logger repeats the idempotent check before reading configuration or opening any game sink.
    /// </summary>
    public static class TestRuntimeEnvironment
    {
        private static int rejected;

        /// <summary>
        /// A rejected early context prevents settings, logging and boot from starting while Godot performs its native exit.
        /// The flag is process-local and sticky; a later caller cannot fall back to ordinary production paths.
        /// </summary>
        public static bool StartupRejected => Volatile.Read(ref rejected) != 0;

        /// <summary>
        /// Verifies legacy data isolation or initializes the complete launch context. A real runtime mismatch
        /// latches rejection through the stderr-only bootstrap sink. The first autoload requests native exit78
        /// without shutting down the embedded CLR underneath Godot.
        /// </summary>
        public static void Verify()
        {
            if (StartupRejected)
            {
                return;
            }

            try
            {
                TestExecutionContextRuntime.Verify();
            }
            catch (Exception error)
            {
                GameLogger.FailTestStartup(error);
            }
        }

        internal static bool RejectStartup()
        {
            return Interlocked.Exchange(ref rejected, 1) == 0;
        }
    }
}
