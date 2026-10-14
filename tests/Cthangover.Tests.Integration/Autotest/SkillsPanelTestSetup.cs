#if TOOLS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
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
    /// effect plate renders one row per aggregated skill effect.
    /// <para/>
    /// The skills subsystem lives in the skills_base engine mod, so every
    /// access to its types (SkillsPanelBehaviour, SkillCardBehaviour,
    /// SkillInfo, the effect aggregator, the rarity palette) goes through
    /// reflection here — the same rule every core-side setup follows. The
    /// test still reads only public node contracts and the mod's public
    /// helpers, so it fails if the tscn and the behaviour drift apart.
    /// </summary>
    public partial class SkillsPanelTestSetup : Node
    {
        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            GameLogger.Log("SKILLS_TEST", "SkillsPanelTestSetup: starting skills panel tests");

            // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync) before any scene _Ready.
            ModInitializerRegistry.NotifyResourcesReady();

            var skills = OwnedSkills();
            Assert(skills.Count > 0, "runtime owns at least one skill after mod init");

            var panel = TscnScenes.LoadAndBuild("scenes/ui/skills_panel/skills_panel.tscn") as Control;
            Assert(panel != null && panel.GetType().Name == "SkillsPanelBehaviour", "skills panel scene builds as SkillsPanelBehaviour");
            if (panel == null)
            {
                Finish();
                return;
            }

            UiThemeBridge.Apply(panel);
            AddChild(panel);
            panel.Show();

            TestStructure(panel);
            TestGrid(panel, skills.Count);
            TestStudyPlate(panel);
            TestEffectPlate(panel, skills);

            Finish();
        }

        private void TestStructure(Control panel)
        {
            Assert(panel.GetNodeOrNull<Panel>("Window") != null, "window shell exists");
            Assert(panel.GetNodeOrNull<Label>("Window/Margin/Root/Header/Title") != null, "header title exists");
            Assert(panel.GetNodeOrNull<Button>("Window/Margin/Root/Header/CloseButton") != null, "close button exists");
            Assert(panel.GetNodeOrNull<Control>("Window/Margin/Root/Body/Side") != null, "study side column exists");
            Assert(panel.GetNodeOrNull<GridContainer>("Hud/HudMargin/HudColumn/HudScroll/HudList") != null, "corner effect plate exists");
        }

        private void TestGrid(Control panel, int ownedSkillCount)
        {
            var grid = panel.GetNodeOrNull<Control>("Window/Margin/Root/Body/Showcase/GridScroll/GridContent");
            Assert(grid != null, "showcase grid exists");
            if (grid == null)
                return;

            var cards = new List<Control>();
            foreach (var child in grid.GetChildren())
            {
                if (child.GetType().Name == "SkillCardBehaviour")
                    cards.Add((Control)child);
            }

            Assert(cards.Count == ownedSkillCount, $"one card per owned skill ({ownedSkillCount})");
            if (cards.Count == 0)
                return;

            var first = cards[0];
            var model = CardModel(first);
            Assert(model != null, "first card is bound to a skill model");
            Assert(first.Size.X > 0f && first.Size.Y > first.Size.X, "card keeps the portrait card proportions");

            var accent = first.GetNodeOrNull<Panel>("Accent");
            var style = accent?.GetThemeStylebox("panel") as StyleBoxFlat;
            var rarityColor = RarityColor(model);
            Assert(style != null && rarityColor != null && style.BgColor == (Color)rarityColor,
                "card accent is painted with the rarity palette colour");
        }

        private void TestStudyPlate(Control panel)
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
                if (child.GetType().Name != "SkillCardBehaviour")
                    continue;

                var model = CardModel((Control)child);
                if (model == null)
                    continue;

                var skillName = Convert.ToString(ReadProperty(model, "Name") ?? string.Empty);
                var rareType = ReadProperty(model, "RareType");
                Assert(name.Text == TranslationServer.Translate(skillName).ToString(),
                    "study plate name matches the first grid card");
                Assert(gems.Text == new string('◆', Mathf.Clamp(Convert.ToInt32(rareType) + 1, 1, 5)),
                    "study plate gem count matches the card rarity");
                return;
            }
        }

        private void TestEffectPlate(Control panel, List<object> skills)
        {
            var list = panel.GetNodeOrNull<GridContainer>("Hud/HudMargin/HudColumn/HudScroll/HudList");
            Assert(list != null, "effect list exists");
            if (list == null)
                return;

            var totals = AggregateEffects(skills);
            Assert(totals.Count > 0, "owned skills contribute collection effects");
            Assert(list.GetChildCount() == totals.Count, "effect plate renders one row per aggregated effect");

            if (totals.Count == 0 || list.GetChildCount() == 0)
                return;

            var cell = list.GetChild(0) as HBoxContainer;
            var value = cell?.GetChildOrNull<Label>(0);
            var key = cell?.GetChildOrNull<Label>(1);
            var first = totals[0];
            Assert(value != null && value.Text == Convert.ToString(InvokeOn(first, "FormatValue")), "effect row shows the summed value");
            Assert(key != null && key.Text == TranslationServer.Translate(Convert.ToString(ReadProperty(first, "Key") ?? string.Empty)).ToString(),
                "effect row shows the translated effect name");
        }

        // ---- reflection helpers over the skills_base mod assembly ----------

        /// <summary>Resolves a mod type by simple name in the Cthangover.Mods.Skills namespace.</summary>
        private static Type ModType(string simpleName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .Select(a => a.GetType("Cthangover.Mods.Skills." + simpleName))
                .FirstOrDefault(t => t != null);
        }

        private static object ReadProperty(object instance, string name)
        {
            return instance.GetType().GetProperty(name)?.GetValue(instance);
        }

        private static object InvokeOn(object instance, string method)
        {
            return instance.GetType().GetMethod(method)?.Invoke(instance, null);
        }

        /// <summary>
        /// All owned skill models, resolved through the mod's SkillFactory in
        /// exactly the order the runtime stores them.
        /// </summary>
        private static List<object> OwnedSkills()
        {
            var result = new List<object>();
            var factory = ModType("SkillFactory")?.GetProperty("Instance")?.GetValue(null);
            if (factory == null)
                return result;

            var get = factory.GetType().GetMethod("Get");
            foreach (var id in GameData.Instance.Runtime.SkillData.Skills)
            {
                var skill = get?.Invoke(factory, new object[] { id });
                if (skill != null)
                    result.Add(skill);
            }

            return result;
        }

        /// <summary>Invokes the mod's SkillPalette.RarityColor for the model's rarity enum value.</summary>
        private static object RarityColor(object model)
        {
            var rareType = ReadProperty(model, "RareType");
            if (rareType == null)
                return null;

            return ModType("SkillPalette")?.GetMethod("RarityColor")?.Invoke(null, new[] { rareType });
        }

        /// <summary>
        /// Summed collection effects for the given skill models, routed through
        /// the mod's SkillEffectAggregator.Aggregate — the same static the
        /// panel itself renders from.
        /// </summary>
        private static List<object> AggregateEffects(List<object> skills)
        {
            var result = new List<object>();
            var infoType = ModType("SkillInfo");
            var aggregator = ModType("SkillEffectAggregator");
            if (infoType == null || aggregator == null)
                return result;

            // Aggregate takes IEnumerable<SkillInfo>; rebuild the list with the
            // mod-visible element type the reflection binder can match.
            var typed = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(infoType));
            foreach (var skill in skills)
                typed.Add(skill);

            var totals = aggregator.GetMethod("Aggregate")?.Invoke(null, new object[] { typed }) as IEnumerable;
            if (totals == null)
                return result;

            foreach (var total in totals)
                result.Add(total);

            return result;
        }

        private object CardModel(Control card)
        {
            return ReadProperty(card, "Model");
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
                GameLogger.Log("SKILLS_TEST", $"  [FAIL] {label}");
            }
        }
    }
}
#endif
