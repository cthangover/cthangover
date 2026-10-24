namespace Cthangover.Mods.StatChecks.Tests
{
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Dialog;
    using Cthangover.Mods.Dialog.Scenarios;

    /// <summary>
    /// <c>check</c> lines in a real scenario text: the queue rolls when it reaches the line,
    /// jumps to the arrow label on success and to <c>else</c> (or the next line) on failure,
    /// and leaves the roll in dialog variables.
    /// </summary>
    public class CheckCommandStrategyTests
    {
        private static DialogRuntime Run(string scenario)
        {
            var dlg = ScenarioParser.Parse(scenario);
            var runtime = new DialogRuntime();
            runtime.SetDialogQueueAndRun(null, dlg.Queue, null);
            runtime.Run();
            return runtime;
        }

        // A stat no mod defines reads 0 for anyone, so the outcomes below are fixed whatever packs are loaded.
        private const string Nobody = "who=test_nobody_has_stats";

        [ModTest("command/success_jumps_to_the_arrow")]
        static void SuccessJumps(ModTestContext ctx)
        {
            var runtime = Run($"check test_undefined_stat 0 {Nobody} dice=none bonus=3 quiet=1 -> :ok\nset outcome failed\nend\n:ok\nset outcome passed\nend\n");

            ModAssert.Equal("passed", runtime.GetVariable("outcome"), "jumped to :ok");
            ModAssert.Equal("3", runtime.GetVariable("check_total"), "0 + 0 + 3");
            ModAssert.Equal("0", runtime.GetVariable("check_dc"), "difficulty");
        }

        [ModTest("command/failure_falls_through_or_takes_else")]
        static void FailureFallsThroughOrTakesElse(ModTestContext ctx)
        {
            var through = Run($"check test_undefined_stat 5 {Nobody} dice=none quiet=1 -> :ok\nset outcome failed\nend\n:ok\nset outcome passed\nend\n");
            ModAssert.Equal("failed", through.GetVariable("outcome"), "fell through to the next line");

            var other = Run($"check test_undefined_stat 5 {Nobody} dice=none quiet=1 else=no -> :ok\nset outcome next\nend\n:ok\nset outcome passed\nend\n:no\nset outcome else\nend\n");
            ModAssert.Equal("else", other.GetVariable("outcome"), "jumped to else");
        }

        [ModTest("command/spec_for_the_validator")]
        static void SpecForTheValidator(ModTestContext ctx)
        {
            var spec = new CheckCommandStrategy();
            ModAssert.Equal(2, spec.MinPositional, "stat and difficulty");
            ModAssert.Equal(2, spec.MaxPositional, "nothing more");
            ModAssert.Equal("else", spec.JumpTargetNamedKey, "else is a label");
            ModAssert.NotNull(ScenarioCommandStrategyFactory.Get("check"), "registered as a DSL command");
        }
    }
}
