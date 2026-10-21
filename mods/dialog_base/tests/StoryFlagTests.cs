namespace Cthangover.Mods.Dialog.Tests
{
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Mods.Dialog.Scenarios;

    /// <summary>
    /// The scenario <c>flag</c> command sets a story flag of the game: once the
    /// dialog reaches it, scenario conditions see the value, the game state
    /// holds it (so saves carry it), and conditions are rescanned.
    /// </summary>
    public class StoryFlagTests
    {
        private const string Flag = "__test_story_flag";

        [ModTest("flags/flag_command_sets_game_flag", TimeoutSeconds = 10)]
        static async Task FlagCommandSetsGameFlag(ModTestContext ctx)
        {
            var stage = await DialogStage.Create();
            var flags = GameData.Instance.Runtime.Flags;
            try
            {
                var dlg = new ScenarioConverter().Convert($"flag {Flag} open\nend\n");
                var versionBefore = ConditionStateMonitor.Version;

                ModAssert.False(ScenarioCondition.Evaluate($"{Flag} == \"open\""), "condition false before the dialog");
                ModAssert.True(stage.Box.SetDialogQueueAndRun(dlg, null, 0, null), "dialog starts");

                ModAssert.True(ScenarioCondition.Evaluate($"{Flag} == \"open\""), "condition sees the flag");
                ModAssert.Equal("open", flags.TryGetValue(Flag, out var value) ? value : null, "game state holds the flag");
                ModAssert.True(ConditionStateMonitor.Version > versionBefore, "conditions are rescanned");
            }
            finally
            {
                flags.Remove(Flag);
                stage.Free();
            }
        }
    }
}
