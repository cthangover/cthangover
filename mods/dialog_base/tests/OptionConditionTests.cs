namespace Cthangover.Mods.Dialog.Tests
{
    using System.Linq;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Dialog.Action.Impls;
    using Cthangover.Mods.Dialog.Scenarios;

    /// <summary>
    /// <c>option … if=…</c>: an option is offered only while its condition holds; a choice whose
    /// options are all hidden is skipped, so the dialog never waits for an answer nobody can give.
    /// </summary>
    public class OptionConditionTests
    {
        [ModTest("options/parser_keeps_the_condition")]
        static void ParserKeepsTheCondition(ModTestContext ctx)
        {
            var dlg = ScenarioParser.Parse("select \"?\"\noption \"A\" -> :a\noption \"B\" \"if=door == 'open' && !false\" -> :b\n:a\nend\n:b\nend\n");
            var select = dlg.Queue.OfType<ActionSelect>().Single();
            ModAssert.Null(select.Variants[0].Condition, "a plain option has no condition");
            ModAssert.Equal("door == 'open' && !false", select.Variants[1].Condition, "the quoted pair keeps spaces");
            ModAssert.Equal("b", select.Variants[1].GoTo, "the jump target stays");
        }

        [ModTest("options/hidden_options_are_not_offered", TimeoutSeconds = 10)]
        static async Task HiddenOptionsAreNotOffered(ModTestContext ctx)
        {
            var stage = await DialogStage.Create();
            try
            {
                var dlg = ScenarioParser.Parse("select \"?\"\noption \"A\" if=false -> :a\noption \"B\" if=true -> :b\noption \"C\" -> :c\n:a\nend\n:b\nend\n:c\nend\n");
                ModAssert.True(stage.Box.SetDialogQueueAndRun(dlg, null, 0, null), "dialog starts");
                ModAssert.True(stage.Box.IsWaitingAnswer, "waits for an answer");
                ModAssert.Equal(2, stage.Box.AnswerVariantCount, "the false option is hidden");
            }
            finally
            {
                stage.Free();
            }
        }

        [ModTest("options/all_hidden_skips_the_choice", TimeoutSeconds = 10)]
        static async Task AllHiddenSkipsTheChoice(ModTestContext ctx)
        {
            var stage = await DialogStage.Create();
            try
            {
                var dlg = ScenarioParser.Parse("select \"?\"\noption \"A\" if=false -> :a\n\nset outcome skipped\nend\n:a\nset outcome a\nend\n");
                stage.Box.SetDialogQueueAndRun(dlg, null, 0, null);
                ModAssert.False(stage.Box.IsWaitingAnswer, "no answer is awaited");
                ModAssert.Equal("skipped", stage.Box.Runtime.GetVariable("outcome"), "the dialog went on past the choice");
            }
            finally
            {
                stage.Free();
            }
        }
    }
}
