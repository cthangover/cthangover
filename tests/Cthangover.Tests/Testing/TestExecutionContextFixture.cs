namespace Cthangover.Tests.Testing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// Pure context inputs use one owned scratch tree. Parsing must not create launch outputs; dictionaries,
    /// argv and receipts are independent of process-global environment and every native Godot singleton.
    /// </summary>
    internal sealed class TestExecutionContextFixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "cthangover-context-" + Guid.NewGuid().ToString("N"));

        internal Dictionary<string, string> Environment { get; }

        internal List<string> Arguments { get; } = new();

        private TestExecutionContext runtimeContext;

        private IReadOnlyList<string> engineArguments;

        internal string Project { get; set; }

        internal string Data { get; set; }

        internal string Temporary { get; set; }

        internal string Receipt { get; set; }

        internal void BindRuntime(TestExecutionContext context, IReadOnlyList<string> arguments)
        {
            runtimeContext = context;
            engineArguments = arguments;
            Project = context.ProjectRoot;
            Data = context.UserDataRoot;
            Temporary = context.TemporaryRoot;
            Receipt = null;
        }

        internal void Validate()
        {
            runtimeContext.ValidateRuntimePaths(Project, Data, Temporary, engineArguments, Receipt);
        }

        internal TestExecutionContextFixture()
        {
            Directory.CreateDirectory(Root);
            var output = Path.Combine(Root, "outputs", "run01", "probe", "attempt1", "launch01");
            var temporary = Path.Combine(output, "temp");
            Environment = new(StringComparer.Ordinal)
            {
                ["CTHANGOVER_TEST_RUN_ID"] = "run01",
                ["CTHANGOVER_TEST_ID"] = "probe/read",
                ["CTHANGOVER_TEST_ATTEMPT_ID"] = "1",
                ["CTHANGOVER_TEST_LAUNCH_ID"] = "launch01",
                ["CTHANGOVER_TEST_WORKER_ID"] = "worker01",
                ["CTHANGOVER_TEST_PREPARED_REVISION"] = new string('a', 64),
                ["CTHANGOVER_TEST_INPUT_IDENTITY"] = "input01",
                ["CTHANGOVER_TEST_LEASE_ID"] = "lease01",
                ["CTHANGOVER_TEST_ACCESS"] = "read",
                ["CTHANGOVER_TEST_ARTIFACTS"] = output,
                ["CTHANGOVER_TEST_LOGS"] = Path.Combine(output, "logs"),
                ["CTHANGOVER_TEST_PROJECT"] = Path.Combine(Root, "prepared"),
                ["CTHANGOVER_TEST_DATA"] = Path.Combine(Root, "component", "data"),
                ["CTHANGOVER_TEST_CACHE"] = Path.Combine(Root, "cache"),
                ["TEMP"] = temporary,
                ["TMP"] = temporary,
                ["TMPDIR"] = temporary
            };
        }

        internal TestExecutionContext Parse()
        {
            return TestExecutionContext.Parse(Environment, Arguments);
        }

        internal void ParseOnly()
        {
            Parse();
        }

        internal string CreateReceipt(TestExecutionContext context)
        {
            return JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["schemaVersion"] = 1,
                ["runId"] = context.RunId,
                ["testId"] = context.TestId,
                ["attemptId"] = context.AttemptId,
                ["launchId"] = context.LaunchId,
                ["nativeLogPath"] = context.NativeLogPath,
                ["engineArguments"] = new[]
                {
                    "--path",
                    context.ProjectRoot,
                    "--log-file",
                    context.NativeLogPath
                }
            });
        }

        /// <summary>Deletes only this fixture's generated scratch root, even when a parse/validation assertion fails.</summary>
        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
    }
}
