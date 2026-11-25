namespace Cthangover.Mods.ExecutionProbe.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Interface.View;
    using Godot;

    /// <summary>One native case exposes actual overlap, physical fixture restoration and independent capture/log/report paths.</summary>
    public static class ExecutionProbeTests
    {
        /// <summary>Runs under a parent-claimed context; plain game launches cannot accidentally create probe outputs.</summary>
        [ModTest("test_execution/probe", TimeoutSeconds = 90)]
        public static async Task Probe(ModTestContext test)
        {
            ModAssert.True(GameEnvironment.IsRuntime, "probe requires the actual engine");
            if (!GameEnvironment.IsRuntime)
            {
                return;
            }

            var context = TestExecutionContextRuntime.Current;
            ModAssert.NotNull(context, "early validated execution context");
            var mode = Mode();
            var source = test.ReadFileText("data/payload.json");
            ModAssert.NotNull(source, "immutable provider payload");
            var before = ExecutionProbeFiles.Digest(source);
            ModAssert.True(test.SandboxDir.StartsWith(context.SandboxDirectory + Path.DirectorySeparatorChar,
                StringComparison.Ordinal), "case sandbox is a launch child");
            using var gate = mode == "gate" ? new ExecutionProbeGate(context.ArtifactRoot, context.LaunchId) : null;
            GameLogger.Log("TEST_EXECUTION", "[EXECUTION_PROBE_INFO] launch=" + context.LaunchId);
            GameLogger.Log("TEST_EXECUTION", "[EXECUTION_PROBE_EXPECTED_ERROR] launch=" + context.LaunchId, LogLevel.Error);
            var ready = ExecutionProbeFiles.Record(context, mode);
            ready["phase"] = "ready";
            ready["gateTimeoutSeconds"] = 60;
            ExecutionProbeFiles.Publish(Path.Combine(context.ArtifactRoot, "probe-ready.json"), ready);
            GameLogger.Log("TEST_EXECUTION", "[EXECUTION_PROBE_READY] launch=" + context.LaunchId + " mode=" + mode);
            if (gate != null)
            {
                await gate.Wait(test.CancellationToken);
            }

            var fixture = Path.Combine(test.SandboxDir, "provider-fixture");
            ExecutionProbeFiles.ExerciseFixture(fixture, source, context.LaunchId);
            var screenshot = await Capture(context);
            var after = ExecutionProbeFiles.Digest(test.ReadFileText("data/payload.json"));
            ModAssert.Equal(before, after, "prepared source stayed immutable");
            var evidence = ExecutionProbeFiles.Record(context, mode);
            evidence["sourceDigestBefore"] = before;
            evidence["sourceDigestAfter"] = after;
            evidence["fixtureRoot"] = fixture;
            evidence["fixtureRestored"] = true;
            evidence["providerObserved"] = true;
            evidence["sandboxPath"] = test.SandboxDir;
            evidence["reportDirectory"] = context.ReportDirectory;
            evidence["mainLogPath"] = context.MainLogPath;
            evidence["errorLogPath"] = context.ErrorLogPath;
            evidence["nativeLogPath"] = context.NativeLogPath;
            evidence["screenshotPath"] = screenshot;
            evidence["screenshotDigest"] = ExecutionProbeFiles.FileDigest(screenshot);
            ExecutionProbeFiles.Publish(Path.Combine(context.ArtifactRoot, "probe-evidence.json"), evidence);
        }

        private static string Mode()
        {
            var mode = "read";
            foreach (var argument in OS.GetCmdlineUserArgs())
            {
                if (argument.StartsWith("--execution-probe-mode=", StringComparison.Ordinal))
                {
                    mode = argument.Substring("--execution-probe-mode=".Length);
                }
            }

            ModAssert.True(mode is "read" or "gate" or "fixture", "known probe mode");
            return mode;
        }

        private static async Task<string> Capture(TestExecutionContext context)
        {
            var tree = (SceneTree)Godot.Engine.GetMainLoop();
            var scene = new ModScenePrefab("scenes/execution_probe.tscn").Instantiate();
            ModAssert.NotNull(scene, "native capture scene");
            try
            {
                tree.Root.AddChild(scene);
                scene.GetNode<Label>("Identity").Text = "Execution isolation probe\nrun=" + context.RunId
                    + "\nlaunch=" + context.LaunchId + "\nworker=" + context.WorkerId;
                await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Directory.CreateDirectory(context.ScreenshotsDirectory);
                var path = Path.Combine(context.ScreenshotsDirectory, "probe.png");
                ModAssert.True(ViewportCapture.SavePng(tree.Root, path), "actual shared-helper PNG capture");
                var bytes = File.ReadAllBytes(path);
                ModAssert.True(bytes.Length > 8, "nonempty screenshot");
                ModAssert.Equal(137, bytes[0], "PNG signature byte0");
                ModAssert.Equal(80, bytes[1], "PNG signature byte1");
                ModAssert.Equal(78, bytes[2], "PNG signature byte2");
                ModAssert.Equal(71, bytes[3], "PNG signature byte3");
                return path;
            }
            finally
            {
                scene.Free();
            }
        }
    }
}
