namespace Cthangover.Mods.Dialog.Tests
{
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Scenes;
    using Cthangover.Mods.Dialog.Scenarios;

    /// <summary><c>when</c> jumps on the shared condition language, evaluated when the line runs.</summary>
    public class WhenCommandStrategyTests
    {
        private static string Outcome(string scenario)
        {
            var dlg = ScenarioParser.Parse(scenario);
            var runtime = new DialogRuntime();
            runtime.SetDialogQueueAndRun(null, dlg.Queue, null);
            runtime.Run();
            return runtime.GetVariable("outcome");
        }

        [ModTest("when/true_false_and_else")]
        static void TrueFalseAndElse(ModTestContext ctx)
        {
            ModAssert.Equal("yes", Outcome("when true -> :yes\nset outcome no\nend\n:yes\nset outcome yes\nend\n"), "true jumps");
            ModAssert.Equal("no", Outcome("when false -> :yes\nset outcome no\nend\n:yes\nset outcome yes\nend\n"), "false falls through");
            ModAssert.Equal("other", Outcome("when false -> :yes else=other\nset outcome no\nend\n:yes\nset outcome yes\nend\n:other\nset outcome other\nend\n"), "false takes else");
        }

        [ModTest("when/reads_state_when_the_line_runs")]
        static void ReadsStateWhenTheLineRuns(ModTestContext ctx)
        {
            ScenarioCondition.SetFlag("test_when_flag", "0");
            try
            {
                const string scenario = "flag test_when_flag 2\nwhen \"test_when_flag >= 2 && !false\" -> :yes\nset outcome no\nend\n:yes\nset outcome yes\nend\n";
                ModAssert.Equal("yes", Outcome(scenario), "a flag set two lines earlier counts");
            }
            finally
            {
                ScenarioCondition.SetFlag("test_when_flag", "0");
            }
        }
    }
}
