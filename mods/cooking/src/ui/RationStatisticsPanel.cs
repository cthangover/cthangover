using Cthangover.Core.Items;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
using Godot;

namespace Mods.Cooking.Rations
{
    public class RationStatisticsPanel : Widget
    {
        private RichTextLabel label;

        protected override void OnceConstruct()
        {
            var margin = new MarginContainer();
            margin.Name = "RationMargin";
            margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 50);
            margin.AddThemeConstantOverride("margin_top", 100);
            margin.AddThemeConstantOverride("margin_right", 0);
            margin.AddThemeConstantOverride("margin_bottom", 100);
            AddChild(margin);

            label = new RichTextLabel();
            label.Name = "RationLabel";
            margin.AddChild(label);
            
            GameData.Instance.Runtime.Inventory.Change += UpdateInfo;
        }

        protected override void ShowConstruct()
        {
            UpdateInfo();
        }
        
        protected override void OnceDestruct()
        {
            GameData.Instance.Runtime.Inventory.Change -= UpdateInfo;
        }

        public void UpdateInfo()
        {
            if (label == null)
                return;
            
            var rationCount = GameData.Instance.Runtime.Inventory.CheckCount(Item.Ration);
            var characterCount = GameData.Instance.Runtime.CharacterData.Characters.Count;

            label.Text = string.Format(TranslationServer.Translate("ui/diner/rationcount"), rationCount) +
                         "\n" + string.Format(TranslationServer.Translate("ui/diner/rationday"), characterCount) +
                         "\n" + string.Format(TranslationServer.Translate("ui/diner/rationmax"), rationCount / Mathf.Max(1, characterCount));
        }
    }
}
