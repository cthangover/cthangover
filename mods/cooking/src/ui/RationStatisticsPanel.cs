using Cthangover.Core.Items;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
using Godot;

namespace Mods.Cooking.Rations
{
    /// <summary>
    /// Displays live ration statistics on the dinner/ration UI screen.
    /// Shows the player the current ration count, how many rations are
    /// consumed daily (one per character), and the maximum number of days
    /// the party can survive on available rations. Listens to
    /// <see cref="Core.Items.InventoryBag.Change"/> events to refresh
    /// automatically whenever the inventory is modified.
    /// </summary>
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

        /// <summary>
        /// Recomputes and refreshes the ration-statistics text.
        /// Reads current ration inventory via <see cref="Core.Items.InventoryBag.CheckCount"/>
        /// for <c>Item.Ration</c> and the total character count from
        /// <see cref="CharacterData"/>. Formats three localized lines:
        /// total rations, daily consumption, and remaining days
        /// (rations divided by character count, clamped to 1 minimum).
        /// </summary>
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
