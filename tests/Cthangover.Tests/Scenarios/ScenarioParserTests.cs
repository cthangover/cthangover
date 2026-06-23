using Cthangover.Core.Localization;
using Cthangover.Core.Scenarios;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Dialog.Action.Impls;

namespace Engine.Tests.Scenarios
{
    public class ScenarioParserTests
    {
        [Fact]
        public void Parse_EmptyText_ReturnsEmptyQueue()
        {
            var dlg = ScenarioParser.Parse("");
            Assert.Empty(dlg.Queue);
        }

        [Fact]
        public void Parse_CommentsOnly_ReturnsEmptyQueue()
        {
            var dlg = ScenarioParser.Parse("# just a comment\n  # another");
            Assert.Empty(dlg.Queue);
        }

        [Fact]
        public void Parse_BackgroundCommand_CreatesActionBackground()
        {
            var dlg = ScenarioParser.Parse("background test_sprite");
            Assert.Single(dlg.Queue);
            Assert.IsType<ActionBackground>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_ForegroundCommand_CreatesActionForeground()
        {
            var dlg = ScenarioParser.Parse("foreground test_sprite");
            Assert.Single(dlg.Queue);
            Assert.IsType<ActionForeground>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_TextCommand_CreatesActionText()
        {
            var dlg = ScenarioParser.Parse("text \"Hello world\"");
            Assert.Single(dlg.Queue);
            var action = Assert.IsType<ActionText>(dlg.Queue[0]);
            Assert.Equal("Hello world", action.Text);
        }

        [Fact]
        public void Parse_TextCommand_WithAvatar()
        {
            var dlg = ScenarioParser.Parse("text \"Hello\" first=Marao.What");
            var action = Assert.IsType<ActionText>(dlg.Queue[0]);
            Assert.Equal("Marao.What", action.FirstAvatar);
            Assert.Null(action.SecondAvatar);
        }

        [Fact]
        public void Parse_TextCommand_WithKey_ResolvesFromLocale()
        {
            var locale = new TestLocale();
            locale.Set("test.key", "Bonjour");

            var dlg = ScenarioParser.Parse("text \"Hello\" key=test.key", locale);
            var action = Assert.IsType<ActionText>(dlg.Queue[0]);
            Assert.Equal("Bonjour", action.Text);
        }

        [Fact]
        public void Parse_TextCommand_WithKey_NoLocaleOverride_KeepsOriginal()
        {
            var locale = new TestLocale();

            var dlg = ScenarioParser.Parse("text \"Hello\" key=test.key", locale);
            var action = Assert.IsType<ActionText>(dlg.Queue[0]);
            Assert.Equal("Hello", action.Text);
        }

        [Fact]
        public void Parse_PTextCommand_CreatesActionTextWithProcessText()
        {
            var dlg = ScenarioParser.Parse("ptext \"Hello ${name}\"");
            var action = Assert.IsType<ActionText>(dlg.Queue[0]);
            Assert.Equal("Hello ${name}", action.Text);
        }

        [Fact]
        public void Parse_TitleCommand_CreatesActionTitle()
        {
            var dlg = ScenarioParser.Parse("title \"My Title\"");
            var action = Assert.IsType<ActionTitle>(dlg.Queue[0]);
            Assert.Equal("My Title", action.TitleText);
        }

        [Fact]
        public void Parse_EndCommand_CreatesActionEnd()
        {
            var dlg = ScenarioParser.Parse("end");
            Assert.IsType<ActionEnd>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_End_CreatesActionEnd()
        {
            var dlg = ScenarioParser.Parse("end");
            Assert.IsType<ActionEnd>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_Label_CreatesPoint()
        {
            var dlg = ScenarioParser.Parse(":my_label");
            var action = Assert.IsType<ActionEmpty>(dlg.Queue[0]);
            Assert.Equal("my_label", action.ID);
        }

        [Fact]
        public void Parse_Goto_CreatesActionGoTo()
        {
            var dlg = ScenarioParser.Parse("goto -> :target");
            var action = Assert.IsType<ActionGoTo>(dlg.Queue[0]);
            Assert.Equal("target", action.GoTo);
        }

        [Fact]
        public void Parse_Select_WithOptions_CreatesActionSelect()
        {
            var text = "select \"Choose\"\n  option \"Option A\" -> :a\n  option \"Option B\" -> :b";
            var dlg = ScenarioParser.Parse(text);

            Assert.Single(dlg.Queue);
            var action = Assert.IsType<ActionSelect>(dlg.Queue[0]);
            Assert.Equal("Choose", action.Text);
            Assert.Equal(2, action.Variants.Count);
            Assert.Equal("Option A", action.Variants[0].Text);
            Assert.Equal("a", action.Variants[0].GoTo);
            Assert.Equal("Option B", action.Variants[1].Text);
            Assert.Equal("b", action.Variants[1].GoTo);
        }

        [Fact]
        public void Parse_Select_WithKey_ResolvesPrompt()
        {
            var locale = new TestLocale();
            locale.Set("prompt.key", "Translated");

            var text = "select \"Original\" key=prompt.key\n  option \"A\" -> :a";
            var dlg = ScenarioParser.Parse(text, locale);

            var action = Assert.IsType<ActionSelect>(dlg.Queue[0]);
            Assert.Equal("Translated", action.Text);
        }

        [Fact]
        public void Parse_Select_WithoutOptions_IsIgnored()
        {
            var text = "select \"Lonely prompt\"";
            var dlg = ScenarioParser.Parse(text);

            Assert.Empty(dlg.Queue);
        }

        [Fact]
        public void Parse_Delay_WithTime_CreatesActionDelay()
        {
            var dlg = ScenarioParser.Parse("delay 2.5");
            var action = Assert.IsType<ActionDelay>(dlg.Queue[0]);
            Assert.Equal(2.5f, action.WaitTime);
        }

        [Fact]
        public void Parse_Delay_Hidden_CreatesHiddenDelay()
        {
            var dlg = ScenarioParser.Parse("delay 1.5 hidden");
            var action = Assert.IsType<ActionDelay>(dlg.Queue[0]);
            Assert.True(action.HiddenMode);
        }

        [Fact]
        public void Parse_Delay_WithText()
        {
            var dlg = ScenarioParser.Parse("delay 1 \"Waiting...\"");
            var action = Assert.IsType<ActionDelay>(dlg.Queue[0]);
            Assert.Equal("Waiting...", action.ShowText);
        }

        [Fact]
        public void Parse_Music_CreatesActionMusic()
        {
            var dlg = ScenarioParser.Parse("music track_01");
            Assert.IsType<ActionMusic>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_MusicPause_CreatesActionMusicPause()
        {
            var dlg = ScenarioParser.Parse("music_pause");
            Assert.IsType<ActionMusicPause>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_MusicPlay_CreatesActionMusicPlay()
        {
            var dlg = ScenarioParser.Parse("music_play");
            Assert.IsType<ActionMusicPlay>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_Sound_CreatesActionSound()
        {
            var dlg = ScenarioParser.Parse("sound sfx_explosion");
            Assert.IsType<ActionSound>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_SwitchScene_CreatesActionSwitchScene()
        {
            var dlg = ScenarioParser.Parse("switch_scene town_entry");
            Assert.IsType<ActionSwitchScene>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_Action_CreatesActionScenario()
        {
            var dlg = ScenarioParser.Parse("action quest.set_status quest_id=PrologQuest status=Progress");

            Assert.Equal(3, dlg.Queue.Count);
            Assert.IsType<ActionSet>(dlg.Queue[0]);
            Assert.IsType<ActionSet>(dlg.Queue[1]);
            Assert.IsType<ActionScenario>(dlg.Queue[2]);
        }

        [Fact]
        public void Parse_Set_CreatesActionSet()
        {
            var dlg = ScenarioParser.Parse("set my_var my_value");
            var action = Assert.IsType<ActionSet>(dlg.Queue[0]);
            Assert.Equal("my_var", action.Name);
            Assert.Equal("my_value", action.Value);
        }

        [Fact]
        public void Parse_ShowDialog_CreatesActionShowDialog()
        {
            var dlg = ScenarioParser.Parse("show_dialog");
            Assert.IsType<ActionShowDialog>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_HideDialog_CreatesActionHideDialog()
        {
            var dlg = ScenarioParser.Parse("hide_dialog");
            Assert.IsType<ActionHideDialog>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_BackgroundColor_CreatesActionBackgroundColor()
        {
            var dlg = ScenarioParser.Parse("background_color #ff0000");
            Assert.IsType<ActionBackgroundColor>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_BackgroundShowHide_CreatesAction()
        {
            var dlg = ScenarioParser.Parse("background_show_hide show duration=2 wait=true");
            Assert.IsType<ActionBackgroundShowHide>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_LightUseTime_CreatesAction()
        {
            var dlg = ScenarioParser.Parse("light_use_time true");
            Assert.IsType<ActionLightUseTime>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_LightSet_CreatesAction()
        {
            var dlg = ScenarioParser.Parse("light_set \"[{\\\"x\\\":100,\\\"y\\\":200,\\\"radius\\\":80,\\\"influence\\\":0.5,\\\"color\\\":\\\"#FFE8C0\\\"}]\"");
            Assert.IsType<ActionLightSet>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_Empty_CreatesActionEmpty()
        {
            var dlg = ScenarioParser.Parse("empty");
            Assert.IsType<ActionEmpty>(dlg.Queue[0]);
        }

        [Fact]
        public void Parse_ComplexScenario_AllCommands()
        {
            var text = @"
# test scenario
background black
foreground empty
title ""Title"" key=title.key

text ""Hello"" first=Marao.What key=text.hello
text ""World""

select ""Choose"" key=select.prompt
  option ""Left"" key=opt.left -> :left
  option ""Right"" key=opt.right -> :right

:left
text ""You went left""
end

:right
delay 1 hidden
action quest.set_status quest_id=Q status=Progress
switch_scene Home
end
";
            var locale = new TestLocale();
            locale.Set("title.key", "Localized Title");
            locale.Set("opt.left", "Links");

            var dlg = ScenarioParser.Parse(text, locale);

            Assert.Equal(16, dlg.Queue.Count);

            Assert.IsType<ActionBackground>(dlg.Queue[0]);
            Assert.IsType<ActionForeground>(dlg.Queue[1]);

            var titleAction = Assert.IsType<ActionTitle>(dlg.Queue[2]);
            Assert.Equal("Localized Title", titleAction.TitleText);

            var helloAction = Assert.IsType<ActionText>(dlg.Queue[3]);
            Assert.Equal("Hello", helloAction.Text);
            Assert.Equal("Marao.What", helloAction.FirstAvatar);

            var worldAction = Assert.IsType<ActionText>(dlg.Queue[4]);
            Assert.Equal("World", worldAction.Text);

            var selectAction = Assert.IsType<ActionSelect>(dlg.Queue[5]);
            Assert.Equal("Choose", selectAction.Text);
            Assert.Equal(2, selectAction.Variants.Count);
            Assert.Equal("Links", selectAction.Variants[0].Text);
            Assert.Equal("left", selectAction.Variants[0].GoTo);
            Assert.Equal("Right", selectAction.Variants[1].Text);
            Assert.Equal("right", selectAction.Variants[1].GoTo);

            var pointLeft = Assert.IsType<ActionEmpty>(dlg.Queue[6]);
            Assert.Equal("left", pointLeft.ID);

            var leftText = Assert.IsType<ActionText>(dlg.Queue[7]);
            Assert.Equal("You went left", leftText.Text);

            Assert.IsType<ActionEnd>(dlg.Queue[8]);

            var pointRight = Assert.IsType<ActionEmpty>(dlg.Queue[9]);
            Assert.Equal("right", pointRight.ID);

            var delayAction = Assert.IsType<ActionDelay>(dlg.Queue[10]);
            Assert.True(delayAction.HiddenMode);

            Assert.IsType<ActionSet>(dlg.Queue[11]);
            Assert.IsType<ActionSet>(dlg.Queue[12]);
            Assert.IsType<ActionScenario>(dlg.Queue[13]);

            Assert.IsType<ActionSwitchScene>(dlg.Queue[14]);
            Assert.IsType<ActionEnd>(dlg.Queue[15]);
        }

        [Fact]
        public void Parse_MultipleLabels_ArePreserved()
        {
            var text = @"
:start
text ""Begin""
:middle
text ""Continue""
:end
text ""Finish""
end
";
            var dlg = ScenarioParser.Parse(text);
            Assert.Equal(7, dlg.Queue.Count);
            Assert.Equal("start", ((ActionEmpty)dlg.Queue[0]).ID);
            Assert.Equal("middle", ((ActionEmpty)dlg.Queue[2]).ID);
            Assert.Equal("end", ((ActionEmpty)dlg.Queue[4]).ID);
            Assert.IsType<ActionEnd>(dlg.Queue[6]);
        }

        [Fact]
        public void Parse_OptionWithoutArrow_WithColonLabel()
        {
            var text = "select\n  option \"A\" :target";
            var dlg = ScenarioParser.Parse(text);
            var action = Assert.IsType<ActionSelect>(dlg.Queue[0]);
            Assert.Equal("target", action.Variants[0].GoTo);
        }
    }

    public class TestLocale : ILocalizationProvider
    {
        private readonly Dictionary<string, string> data = new();

        public void Set(string key, string value) => data[key] = value;
        public string Get(string key) => data.TryGetValue(key, out var v) ? v : null;
    }
}
