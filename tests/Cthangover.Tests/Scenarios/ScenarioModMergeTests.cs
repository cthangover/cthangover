using Cthangover.Core.Localization;
using Cthangover.Core.Scenarios;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Dialog.Action.Impls;

namespace Engine.Tests.Scenarios
{
    public class ScenarioModMergeTests
    {
        private const string ModAScenario = @"
# TestModAEvent - from test_mod_a
background scene_a_bg
text ""Это текст из мода A. Привет из test_mod_a!"" key=cross_mod/text_a
text ""Если ты это видишь, локализация A работает."" key=cross_mod/text_a_2
end
";

        private const string ModBScenario = @"
# TestModBEvent - from test_mod_b
background scene_b_bg
text ""Это текст из мода B. Привет из test_mod_b!"" key=cross_mod/text_b
select ""Выбери действие:"" key=cross_mod/choose_b
option ""Продолжить"" key=cross_mod/opt_continue -> :continue
option ""Выйти"" key=cross_mod/opt_exit -> :exit

:continue
text ""Ты выбрал продолжить."" key=cross_mod/text_continue
end

:exit
switch_scene Menu
end
";

        [Fact]
        public void ModA_Scenario_Parses_Correctly()
        {
            var dlg = ScenarioParser.Parse(ModAScenario);

            Assert.Equal(4, dlg.Queue.Count);

            var bg = Assert.IsType<ActionBackground>(dlg.Queue[0]);
            Assert.Equal("scene_a_bg", bg.SpriteID);

            var text1 = Assert.IsType<ActionText>(dlg.Queue[1]);
            Assert.Equal("Это текст из мода A. Привет из test_mod_a!", text1.Text);

            var text2 = Assert.IsType<ActionText>(dlg.Queue[2]);
            Assert.Equal("Если ты это видишь, локализация A работает.", text2.Text);

            Assert.IsType<ActionEnd>(dlg.Queue[3]);
        }

        [Fact]
        public void ModB_Scenario_Parses_Correctly()
        {
            var dlg = ScenarioParser.Parse(ModBScenario);

            Assert.Equal(9, dlg.Queue.Count);

            var bg = Assert.IsType<ActionBackground>(dlg.Queue[0]);
            Assert.Equal("scene_b_bg", bg.SpriteID);

            var text = Assert.IsType<ActionText>(dlg.Queue[1]);
            Assert.Equal("Это текст из мода B. Привет из test_mod_b!", text.Text);

            var select = Assert.IsType<ActionSelect>(dlg.Queue[2]);
            Assert.Equal("Выбери действие:", select.Text);
            Assert.Equal(2, select.Variants.Count);
            Assert.Equal("Продолжить", select.Variants[0].Text);
            Assert.Equal("continue", select.Variants[0].GoTo);
            Assert.Equal("Выйти", select.Variants[1].Text);
            Assert.Equal("exit", select.Variants[1].GoTo);

            var pointContinue = Assert.IsType<ActionEmpty>(dlg.Queue[3]);
            Assert.Equal("continue", pointContinue.ID);

            var continueText = Assert.IsType<ActionText>(dlg.Queue[4]);
            Assert.Equal("Ты выбрал продолжить.", continueText.Text);

            Assert.IsType<ActionEnd>(dlg.Queue[5]);

            var pointExit = Assert.IsType<ActionEmpty>(dlg.Queue[6]);
            Assert.Equal("exit", pointExit.ID);

            Assert.IsType<ActionSwitchScene>(dlg.Queue[7]);
            Assert.IsType<ActionEnd>(dlg.Queue[8]);
        }

        [Fact]
        public void ModAB_Scenarios_With_Localization_Resolves_Keys()
        {
            var locale = new TestLocale();
            locale.Set("cross_mod/text_a", "This is text from mod A. Greetings from test_mod_a!");
            locale.Set("cross_mod/text_b", "This is text from mod B. Greetings from test_mod_b!");
            locale.Set("cross_mod/choose_b", "Choose an action:");
            locale.Set("cross_mod/opt_continue", "Continue");
            locale.Set("cross_mod/opt_exit", "Exit");
            locale.Set("cross_mod/text_continue", "You chose to continue.");

            var dlgA = ScenarioParser.Parse(ModAScenario, locale);
            var textA = Assert.IsType<ActionText>(dlgA.Queue[1]);
            Assert.Equal("This is text from mod A. Greetings from test_mod_a!", textA.Text);

            var dlgB = ScenarioParser.Parse(ModBScenario, locale);
            var textB = Assert.IsType<ActionText>(dlgB.Queue[1]);
            Assert.Equal("This is text from mod B. Greetings from test_mod_b!", textB.Text);

            var select = Assert.IsType<ActionSelect>(dlgB.Queue[2]);
            Assert.Equal("Choose an action:", select.Text);
            Assert.Equal("Continue", select.Variants[0].Text);
            Assert.Equal("Exit", select.Variants[1].Text);

            var continueText = Assert.IsType<ActionText>(dlgB.Queue[4]);
            Assert.Equal("You chose to continue.", continueText.Text);
        }

        [Fact]
        public void ModAB_Scenarios_Without_Locale_Fallback_To_Original_Text()
        {
            var locale = new TestLocale();

            var dlgA = ScenarioParser.Parse(ModAScenario, locale);
            var textA = Assert.IsType<ActionText>(dlgA.Queue[1]);
            Assert.Equal("Это текст из мода A. Привет из test_mod_a!", textA.Text);

            var dlgB = ScenarioParser.Parse(ModBScenario, locale);
            var textB = Assert.IsType<ActionText>(dlgB.Queue[1]);
            Assert.Equal("Это текст из мода B. Привет из test_mod_b!", textB.Text);
        }

        [Fact]
        public void ModAB_ScenarioQueue_Has_Different_Action_Counts()
        {
            var dlgA = ScenarioParser.Parse(ModAScenario);
            var dlgB = ScenarioParser.Parse(ModBScenario);

            Assert.Equal(4, dlgA.Queue.Count);
            Assert.Equal(9, dlgB.Queue.Count);

            Assert.NotEqual(dlgA.Queue.Count, dlgB.Queue.Count);
        }

        [Fact]
        public void CrossMod_EventMerge_Simulates_Two_Mods_Contributing_To_Same_Scene()
        {
            var modAEvents = new List<string> { "@scenario:TestModAEvent" };
            var modBEvents = new List<string> { "@scenario:TestModBEvent" };

            var seen = new HashSet<string>();
            var merged = new List<string>();

            foreach (var evt in modAEvents)
                if (seen.Add(evt))
                    merged.Add(evt);

            foreach (var evt in modBEvents)
                if (seen.Add(evt))
                    merged.Add(evt);

            Assert.Equal(2, merged.Count);
            Assert.Contains("@scenario:TestModAEvent", merged);
            Assert.Contains("@scenario:TestModBEvent", merged);
        }

        [Fact]
        public void CrossMod_Dedup_Removes_Duplicate_Events_From_Different_Mods()
        {
            var modAEvents = new List<string> { "@scenario:TestModAEvent", "@scenario:SharedEvent" };
            var modBEvents = new List<string> { "@scenario:TestModBEvent", "@scenario:SharedEvent" };

            var seen = new HashSet<string>();
            var merged = new List<string>();

            foreach (var evt in modAEvents)
                if (seen.Add(evt))
                    merged.Add(evt);

            foreach (var evt in modBEvents)
                if (seen.Add(evt))
                    merged.Add(evt);

            Assert.Equal(3, merged.Count);
            Assert.Contains("@scenario:TestModAEvent", merged);
            Assert.Contains("@scenario:TestModBEvent", merged);
            Assert.Contains("@scenario:SharedEvent", merged);
        }

        [Fact]
        public void CrossMod_Events_Process_Sequentially()
        {
            var dlgA = ScenarioParser.Parse(ModAScenario);
            var dlgB = ScenarioParser.Parse(ModBScenario);

            var combinedQueue = new List<IActionCommand>();
            combinedQueue.AddRange(dlgA.Queue);
            combinedQueue.AddRange(dlgB.Queue);

            Assert.Equal(4 + 9, combinedQueue.Count);

            Assert.IsType<ActionBackground>(combinedQueue[0]);
            Assert.IsType<ActionEnd>(combinedQueue[3]);
            Assert.IsType<ActionBackground>(combinedQueue[4]);
            Assert.IsType<ActionEnd>(combinedQueue[combinedQueue.Count - 1]);
        }
    }
}
