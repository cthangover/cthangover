namespace Cthangover.Core.Mods.Testing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Owns the early native-path proof and exclusive per-launch output claim. It initializes once on the main
    /// thread, before settings/configuration can log, and retains no Godot objects for process-exit publication.
    /// </summary>
    public static class TestExecutionContextRuntime
    {
        private static readonly object Gate = new();

        private static bool verified;

        private static FileStream owner;

        private static string actualProject;

        private static string actualData;

        private static string actualTemporary;

        private static string nativeEvidence;

        private static int ready;

        /// <summary>Validated launch context, or null for ordinary/legacy launches; reading it never initializes native state.</summary>
        public static TestExecutionContext Current { get; private set; }

        internal static void Verify()
        {
            if (!GameEnvironment.IsRuntime)
            {
                return;
            }

            lock (Gate)
            {
                if (verified)
                {
                    return;
                }

                if (!GameEnvironment.IsMainThread)
                {
                    throw new InvalidOperationException("Test execution context must initialize on the native main thread");
                }

                var variables = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (System.Collections.DictionaryEntry variable in System.Environment.GetEnvironmentVariables())
                {
                    variables[(string)variable.Key] = (string)variable.Value;
                }

                var context = TestExecutionContext.Parse(variables, OS.GetCmdlineUserArgs());
                if (context == null)
                {
                    VerifyLegacy(variables);
                    verified = true;
                    return;
                }

                actualProject = ProjectSettings.GlobalizePath("res://");
                actualData = OS.GetUserDataDir();
                actualTemporary = Path.GetTempPath();
                var receiptPath = Path.Combine(context.ArtifactRoot, "launch.json");
                var receipt = File.Exists(receiptPath) ? File.ReadAllText(receiptPath) : null;
                nativeEvidence = context.ValidateRuntimePaths(actualProject, actualData, actualTemporary,
                    OS.GetCmdlineArgs(), receipt);
                Directory.CreateDirectory(context.ArtifactRoot);
                owner = new FileStream(Path.Combine(context.ArtifactRoot, "core-context.owner"), FileMode.CreateNew,
                    System.IO.FileAccess.Write, FileShare.Read);
                try
                {
                    var identity = Encoding.UTF8.GetBytes(context.LogIdentity + "\n");
                    owner.Write(identity);
                    owner.Flush(true);
                    Directory.CreateDirectory(context.LogRoot);
                    CreateLog(context.MainLogPath);
                    CreateLog(context.ErrorLogPath);
                    TestExecutionContextEvent.Write(context, "verified", actualProject, actualData, actualTemporary, nativeEvidence);
                    Current = context;
                    verified = true;
                    AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                }
                catch
                {
                    owner.Dispose();
                    owner = null;
                    throw;
                }
            }
        }

        private static void CreateLog(string path)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read);
        }

        private static void VerifyLegacy(IReadOnlyDictionary<string, string> variables)
        {
            if (!variables.TryGetValue("CTHANGOVER_TEST_DATA", out var expected))
            {
                return;
            }

            TestExecutionContext.Check(expected, OS.GetUserDataDir(), "user data");
            if (!variables.TryGetValue("CTHANGOVER_TEST_PROJECT", out var project))
            {
                throw new ArgumentException("Test project root is missing");
            }

            TestExecutionContext.Check(project, ProjectSettings.GlobalizePath("res://"), "project");
        }

        internal static void LogReady()
        {
            if (Current == null || Interlocked.Exchange(ref ready, 1) != 0)
            {
                return;
            }

            GameLogger.Log("TEST_EXECUTION", "[TEST_EXECUTION_READY] " + Current.LogIdentity);
        }

        private static void OnProcessExit(object sender, EventArgs arguments)
        {
            lock (Gate)
            {
                try
                {
                    TestExecutionContextEvent.Write(Current, "process_exit", actualProject, actualData, actualTemporary,
                        nativeEvidence, System.Environment.ExitCode);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // The parent's observed exit remains authoritative when terminal evidence cannot be written.
                }
                finally
                {
                    owner?.Dispose();
                    owner = null;
                    AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                }
            }
        }
    }
}
