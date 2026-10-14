using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods.Testing
{
    /// <summary>
    /// Everything a mod unit test may touch, passed to every
    /// <c>[ModTest]</c> method. Provides mod-file access through the same
    /// <see cref="ModManager"/> pipeline production code uses, a log sink
    /// that lands both in GameLogger and in the report entry, a cancellation
    /// token and two sandboxed scratch areas:
    ///
    /// <para><see cref="SandboxDir"/> — a filesystem directory under
    /// <c>user://mod_test_sandbox/{modId}</c>, wiped before the suite starts
    /// (not after — a failing test keeps its artifacts for debugging) and
    /// listed as the report's artifacts.</para>
    ///
    /// <para><see cref="SandboxRoot"/> — a detached Godot <see cref="Node"/>
    /// for tests that exercise Node-dependent logic; the runner frees it
    /// after each test so a leaked child cannot outlive the test.</para>
    ///
    /// The context makes the correct path the most convenient one: the
    /// engine cannot intercept arbitrary file IO (honest residual zone §6.5),
    /// so tests are encouraged to write only here.
    /// </summary>
    public sealed class ModTestContext
    {
        private readonly List<string> logBuffer;
        private readonly string sandboxDir;

        /// <summary>Creates a context for one test execution. Internal: tests receive it, never construct it.</summary>
        internal ModTestContext(string modId, List<string> logBuffer, string sandboxDir, Node sandboxRoot, CancellationToken cancellationToken)
        {
            ModId = modId;
            this.logBuffer = logBuffer;
            this.sandboxDir = sandboxDir;
            SandboxRoot = sandboxRoot;
            CancellationToken = cancellationToken;
        }

        /// <summary>Id of the mod under test; also the assembly the test code was compiled from.</summary>
        public string ModId { get; }

        /// <summary>Cooperative cancellation — set when the per-test timeout elapses or the whole run is aborted.</summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>Detached Godot node for Node-dependent test logic; freed by the engine after the test.</summary>
        public Node SandboxRoot { get; }

        /// <summary>
        /// Absolute path of the per-mod sandbox directory, created on first
        /// access. Wiped before the suite, kept after the run for artifacts.
        /// </summary>
        public string SandboxDir
        {
            get
            {
                Directory.CreateDirectory(sandboxDir);
                return sandboxDir;
            }
        }

        /// <summary>Reads a UTF-8 text file from the mod, or null when missing.</summary>
        public string ReadFileText(string path) => ModManager.Instance.ReadFileText(ModId, path);

        /// <summary>
        /// Reads a mod JSON file and deserializes it. Throws on parse failure
        /// rather than returning null — a mod-data error is a test failure,
        /// not a silent pass.
        /// </summary>
        public T ReadJson<T>(string path) where T : class
        {
            var text = ReadFileText(path);
            ModAssert.NotNull(text, $"mod file '{path}'");
            return JsonSerializer.Deserialize<T>(text, JsonOptionsProvider.ReadOptions)
                ?? throw new ModAssertException($"mod file '{path}' deserialized to null");
        }

        /// <summary>Lists the mod's files in <paramref name="directory"/> (entries ending with '/' are subdirectories).</summary>
        public IEnumerable<string> ListFiles(string directory = "") => ModManager.Instance.ListFiles(ModId, directory);

        /// <summary>
        /// Logs a line into the report entry of the current test and into
        /// GameLogger (category MODTEST) — visible in stdout and the log file.
        /// </summary>
        public void Log(string message)
        {
            lock (logBuffer)
                logBuffer.Add(message);
            GameLogger.Log("MODTEST", $"[{ModId}] {message}");
        }
    }
}
