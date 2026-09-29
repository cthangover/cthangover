#if TOOLS
using System.Collections.Generic;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.Skills;
using Cthangover.Core.UI;
using Cthangover.Core.UI.SkillsPanel;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Integration test for the collection skills panel. It loads the
    /// mod-provided scene through the same runtime tscn pipeline the game uses,
    /// shows the panel and locks the new UI contract: the window structure
    /// exists, the showcase builds exactly one card per owned skill, the study
    /// plate follows the selected card with the rarity colour, and the corner
    /// effect plate renders one row per aggregated skill effect. The test
    /// deliberately reads only public node contracts and the shared
    /// <see cref="SkillPalette"/>/<see cref="SkillEffectAggregator"/> logic, so
    /// it fails if the tscn and the behaviour drift apart.
    /// </summary>
    public partial class SkillsPanelTestSetup : Node
    {
        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            GameLogger.Log("SKILLS_TEST", "SkillsPanelTestSetup: starting skills panel tests");

            ModManager.Instance.Initialize();
            ModInitializerRegistry.NotifyResourcesReady();

            var skills = OwnedSkills();
            Assert(skills.Count > 0, "runtime owns at least one skill after mod init");

            var panel = TscnScenes.LoadAndBuild("scenes/ui/skills_panel/skills_panel.tscn") as SkillsPanelBehaviour;
            Assert(panel != null, "skills panel scene builds as SkillsPanelBehaviour");
            if (panel == null)
            {
                Finish();
                return;
            }

            CthangoverTheme.Apply(panel);
            AddChild(panel);
            panel.Show();

            TestStructure(panel);
            TestGrid(panel, skills);
            TestStudyPlate(panel);
            TestEffectPlate(panel, skills);

            Finish();
        }

        private void TestStructure(SkillsPanelBehaviour panel)
        {
            Assert(panel.GetNodeOrNull<Panel>("Window") != null, "window shell exists");
            Assert(panel.GetNodeOrNull<Label>("Window/Margin/Root/Header/Title") != null, "header title exists");
            Assert(panel.GetNodeOrNull<Button>("Window/Margin/Root/Header/CloseButton") != null, "close button exists");
            Assert(panel.GetNodeOrNull<Control>("Window/Margin/Root/Body/Side") != null, "study side column exists");
            Assert(panel.GetNodeOrNull<GridContainer>("Hud/HudMargin/HudColumn/HudScroll/HudList") != null, "corner effect plate exists");
        }

        private void TestGrid(SkillsPanelBehaviour panel, List<SkillInfo> skills)
        {
            var grid = panel.GetNodeOrNull<Control>("Window/Margin/Root/Body/Showcase/GridScroll/GridContent");
            Assert(grid != null, "showcase grid exists");
            if (grid == null)
                return;

            var cards = new List<SkillCardBehaviour>();
            foreach (var child in grid.GetChildren())
            {
                if (child is SkillCardBehaviour card)
                    cards.Add(card);
            }

            Assert(cards.Count == skills.Count, $"one card per owned skill ({skills.Count})");
            if (cards.Count == 0)
                return;

            var first = cards[0];
            Assert(first.Model != null, "first card is bound to a skill model");
            Assert(first.Size.X > 0f && first.Size.Y > first.Size.X, "card keeps the portrait card proportions");

            var accent = first.GetNodeOrNull<Panel>("Accent");
            var style = accent?.GetThemeStylebox("panel") as StyleBoxFlat;
            Assert(style != null && style.BgColor == SkillPalette.RarityColor(first.Model.RareType),
                "card accent is painted with the rarity palette colour");
        }

        private void TestStudyPlate(SkillsPanelBehaviour panel)
        {
            var name = panel.GetNodeOrNull<Label>(
                "Window/Margin/Root/Body/Side/SideScroll/Passport/Plate/PlateNameBox/PlateName");
            var gems = panel.GetNodeOrNull<Label>(
                "Window/Margin/Root/Body/Side/SideScroll/Passport/Plate/PlateGems");
            var grid = panel.GetNodeOrNull<Control>("Window/Margin/Root/Body/Showcase/GridScroll/GridContent");

            Assert(name != null && !string.IsNullOrEmpty(name.Text), "study plate shows the selected card name");
            Assert(gems != null && !string.IsNullOrEmpty(gems.Text), "study plate shows the rarity gems");

            if (name == null || gems == null || grid == null)
                return;

            foreach (var child in grid.GetChildren())
            {
                if (child is SkillCardBehaviour card && card.Model != null)
                {
                    Assert(name.Text == TranslationServer.Translate(card.Model.Name).ToString(),
                        "study plate name matches the first grid card");
                    Assert(gems.Text == new string('◆', Mathf.Clamp((int)card.Model.RareType + 1, 1, 5)),
                        "study plate gem count matches the card rarity");
                    return;
                }
            }
        }

        private void TestEffectPlate(SkillsPanelBehaviour panel, List<SkillInfo> skills)
        {
            var list = panel.GetNodeOrNull<GridContainer>("Hud/HudMargin/HudColumn/HudScroll/HudList");
            Assert(list != null, "effect list exists");
            if (list == null)
                return;

            var totals = SkillEffectAggregator.Aggregate(skills);
            Assert(totals.Count > 0, "owned skills contribute collection effects");
            Assert(list.GetChildCount() == totals.Count, "effect plate renders one row per aggregated effect");

            if (totals.Count == 0 || list.GetChildCount() == 0)
                return;

            var cell = list.GetChild(0) as HBoxContainer;
            var value = cell?.GetChildOrNull<Label>(0);
            var key = cell?.GetChildOrNull<Label>(1);
            Assert(value != null && value.Text == totals[0].FormatValue(), "effect row shows the summed value");
            Assert(key != null && key.Text == TranslationServer.Translate(totals[0].Key).ToString(),
                "effect row shows the translated effect name");
        }

        private static List<SkillInfo> OwnedSkills()
        {
            var result = new List<SkillInfo>();
            foreach (var id in GameData.Instance.Runtime.SkillData.Skills)
            {
                var skill = SkillFactory.Instance.Get(id);
                if (skill != null)
                    result.Add(skill);
            }
            return result;
        }

        private void Finish()
        {
            GameLogger.Log("SKILLS_TEST", $"SkillsPanelTestSetup: {_passed} passed, {_failed} failed");
            GameLogger.Log("SKILLS_TEST", $"[TEST_RESULT] name=skills_panel passed={_passed} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }

        private void Assert(bool condition, string label)
        {
            if (condition)
            {
                _passed++;
                GameLogger.Log("SKILLS_TEST", $"  [PASS] {label}");
            }
            else
            {
                _failed++;
                GameLogger.Log("SKILLS_TEST", $"  [FAIL] {label}", LogLevel.Error);
            }
        }
    }
}
#endif
