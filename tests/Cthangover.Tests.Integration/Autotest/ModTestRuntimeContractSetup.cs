#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System;
    using System.Linq;
    using System.Threading;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>Tests real Godot deferred cleanup and an uncooperative async timeout without executing another test after it.</summary>
    public partial class ModTestRuntimeContractSetup : Node
    {
        public override async void _Ready()
        {
            var failed = 0;
            var passed = 0;
            try
            {
                ModRuntimeCases.LaterTestRan = false;
                ModRuntimeCases.LateTaskFinished = false;
                await ModTestRunner.CaptureRootBaselineAsync();
                var assembly = typeof(ModRuntimeCases).Assembly;
                var cleanup = await ModTestRunner.RunSuiteAsync("runtime_contracts", assembly, new ModTestLedger(), CancellationToken.None, testFilter: "cleanup/*");
                ModAssert.Equal(2, cleanup.Passed, "both cleanup fixture cases completed");
                ModAssert.Equal(0, cleanup.Errors, "cleanup invariants");
                ModAssert.Equal(0, cleanup.Failed, "next case sees no stray node");
                ModAssert.Equal(1, cleanup.Tests[0].Artifacts.Count, "first case keeps its file artifact");
                passed++;
                var timeout = await ModTestRunner.RunSuiteAsync("runtime_contracts", assembly, new ModTestLedger(), CancellationToken.None, testFilter: "timeout/*");
                ModAssert.True(timeout.Tests.Any(test => test.Status == ModTestStatus.Timeout), "uncooperative task times out");
                ModAssert.False(ModRuntimeCases.LaterTestRan, "next case did not start");
                ModAssert.True(timeout.Tests.Any(test => test.TestName == "timeout/must_not_run" && test.Status == ModTestStatus.Error), "unrun case has explicit infrastructure outcome");
                passed++;
                await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
                ModAssert.True(ModRuntimeCases.LateTaskFinished, "the fixture really ignored cancellation");
                ModAssert.False(ModRuntimeCases.LaterTestRan, "late completion did not resume the suite");
                passed++;
            }
            catch (Exception error)
            {
                failed++;
                GameLogger.Log("TEST", "Runtime test contract failed: " + error, LogLevel.Error);
            }
            GameLogger.Log("TEST", $"[TEST_RESULT] name=test_engine_contracts passed={passed} failed={failed}");
            ApplicationExit.Quit(GetTree(), failed == 0 ? 0 : 1);
        }
    }
}
#endif
